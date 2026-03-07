using ByteBill_BS.Data;
using ByteBill_BS.Models;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace ByteBill_BS.Services;

// ─── Configuration ──────────────────────────────────────────────────────
public class XeroSettings
{
    public string ClientId { get; set; } = string.Empty;
    public string ClientSecret { get; set; } = string.Empty;
    public string CallbackUrl { get; set; } = string.Empty;
    public string Scopes { get; set; } = "openid profile email accounting.transactions accounting.contacts offline_access";
}

// ─── Interface ──────────────────────────────────────────────────────────
public interface IXeroService
{
    /// <summary>Build the Xero OAuth 2.0 authorization URL for a shop.</summary>
    string GetAuthorizationUrl(long shopId);

    /// <summary>Exchange the authorization code for access + refresh tokens.</summary>
    Task<bool> ExchangeCodeForTokensAsync(long shopId, string code);

    /// <summary>Disconnect a shop from Xero.</summary>
    Task DisconnectAsync(long shopId);

    /// <summary>Check if a shop has an active Xero connection.</summary>
    Task<bool> IsConnectedAsync(long shopId);

    // ── Sync operations ──────────────────────────────
    Task SyncInvoiceAsync(long invoiceId, long? userId = null);
    Task SyncPaymentAsync(long paymentId, long? userId = null);
    Task SyncCreditNoteAsync(long adjustmentId, long? userId = null);
    Task SyncContactAsync(long customerId, long? userId = null);

    /// <summary>Bulk-sync all unsynced invoices + payments for a shop.</summary>
    Task<(int Synced, int Failed)> SyncAllAsync(long shopId, long? userId = null);

    /// <summary>Test Xero connection and return available accounts (for diagnostics).</summary>
    Task<object> TestXeroAccountsAsync(long shopId);
}

// ─── Implementation ─────────────────────────────────────────────────────
public class XeroService : IXeroService
{
    private readonly ApplicationDbContext _db;
    private readonly HttpClient _http;
    private readonly XeroSettings _settings;
    private readonly ILogger<XeroService> _logger;

    private const string XeroIdentityUrl = "https://identity.xero.com";
    private const string XeroLoginUrl  = "https://login.xero.com/identity";
    private const string XeroApiUrl    = "https://api.xero.com/api.xro/2.0";

    public XeroService(
        ApplicationDbContext db,
        HttpClient http,
        IOptions<XeroSettings> settings,
        ILogger<XeroService> logger)
    {
        _db = db;
        _http = http;
        _settings = settings.Value;
        _logger = logger;
    }

    // ═══════════════════════════════════════════════════════════════════
    // OAuth 2.0 Flow
    // ═══════════════════════════════════════════════════════════════════

    public string GetAuthorizationUrl(long shopId)
    {
        var state = Convert.ToBase64String(Encoding.UTF8.GetBytes($"shop:{shopId}"));
        return $"{XeroLoginUrl}/connect/authorize" +
               $"?response_type=code" +
               $"&client_id={Uri.EscapeDataString(_settings.ClientId)}" +
               $"&redirect_uri={Uri.EscapeDataString(_settings.CallbackUrl)}" +
               $"&scope={Uri.EscapeDataString(_settings.Scopes)}" +
               $"&state={Uri.EscapeDataString(state)}";
    }

    public async Task<bool> ExchangeCodeForTokensAsync(long shopId, string code)
    {
        try
        {
            var tokenRequest = new FormUrlEncodedContent(new Dictionary<string, string>
            {
                ["grant_type"] = "authorization_code",
                ["code"] = code,
                ["redirect_uri"] = _settings.CallbackUrl
            });

            using var req = new HttpRequestMessage(HttpMethod.Post, $"{XeroIdentityUrl}/connect/token");
            req.Content = tokenRequest;
            req.Headers.Authorization = new AuthenticationHeaderValue(
                "Basic",
                Convert.ToBase64String(Encoding.UTF8.GetBytes($"{_settings.ClientId}:{_settings.ClientSecret}"))
            );

            var response = await _http.SendAsync(req);
            if (!response.IsSuccessStatusCode)
            {
                _logger.LogError("Xero token exchange failed: {Status}", response.StatusCode);
                return false;
            }

            var json = await response.Content.ReadAsStringAsync();
            var tokenData = JsonSerializer.Deserialize<XeroTokenResponse>(json);
            if (tokenData is null) return false;

            // Get connected tenants
            var tenants = await GetTenantsAsync(tokenData.AccessToken);
            if (tenants is null || tenants.Count == 0)
            {
                _logger.LogWarning("No Xero tenants found for shop {ShopId}", shopId);
                return false;
            }

            var tenant = tenants[0]; // Use first tenant

            // Deactivate existing connections for this shop
            var existing = await _db.Set<XeroConnection>()
                .Where(x => x.ShopId == shopId && x.IsActive)
                .ToListAsync();
            foreach (var c in existing) c.IsActive = false;

            // Save new connection
            var connection = new XeroConnection
            {
                ShopId = shopId,
                XeroTenantId = tenant.TenantId,
                TenantName = tenant.TenantName,
                AccessToken = tokenData.AccessToken,
                RefreshToken = tokenData.RefreshToken,
                TokenExpiresAt = DateTime.UtcNow.AddSeconds(tokenData.ExpiresIn),
                ConnectedAt = DateTime.UtcNow,
                IsActive = true
            };

            _db.Set<XeroConnection>().Add(connection);
            await _db.SaveChangesAsync();

            _logger.LogInformation("Xero connected for shop {ShopId}, tenant {TenantName}",
                shopId, tenant.TenantName);
            return true;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error exchanging Xero code for shop {ShopId}", shopId);
            return false;
        }
    }

    public async Task DisconnectAsync(long shopId)
    {
        var connections = await _db.Set<XeroConnection>()
            .Where(x => x.ShopId == shopId && x.IsActive)
            .ToListAsync();

        foreach (var c in connections)
            c.IsActive = false;

        await _db.SaveChangesAsync();
        _logger.LogInformation("Xero disconnected for shop {ShopId}", shopId);
    }

    public async Task<bool> IsConnectedAsync(long shopId)
    {
        return await _db.Set<XeroConnection>()
            .AnyAsync(x => x.ShopId == shopId && x.IsActive);
    }

    // ═══════════════════════════════════════════════════════════════════
    // Sync: Invoice → Xero Invoice
    // ═══════════════════════════════════════════════════════════════════

    public async Task SyncInvoiceAsync(long invoiceId, long? userId = null)
    {
        var invoice = await _db.Invoices
            .Include(i => i.InvoiceLines)
            .Include(i => i.Customer)
            .FirstOrDefaultAsync(i => i.InvoiceId == invoiceId);

        if (invoice is null) return;

        // Idempotency: check if already synced successfully
        var existingLog = await _db.XeroSyncLogs
            .FirstOrDefaultAsync(l => l.InvoiceId == invoiceId && l.SyncType == "Invoice" && l.Status == "Success");
        if (existingLog is not null) return;

        var connection = await GetActiveConnectionAsync(invoice.ShopId);
        if (connection is null) return;

        // Ensure contact exists in Xero first
        if (invoice.CustomerId > 0)
            await SyncContactAsync(invoice.CustomerId, userId);

        // Reuse existing pending/failed log or create new one
        var log = await _db.XeroSyncLogs
            .FirstOrDefaultAsync(l => l.InvoiceId == invoiceId && l.SyncType == "Invoice" && l.Status != "Success");

        if (log is not null)
        {
            log.SyncedByUserId = userId;
            log.SyncedAt = DateTime.UtcNow;
            log.Status = "Pending";
            log.Message = null;
        }
        else
        {
            log = new XeroSyncLog
            {
                ShopId = invoice.ShopId,
                SyncedByUserId = userId,
                SyncType = "Invoice",
                InvoiceId = invoiceId,
                Status = "Pending",
                SyncedAt = DateTime.UtcNow
            };
            _db.XeroSyncLogs.Add(log);
        }
        await _db.SaveChangesAsync();

        try
        {
            var xeroInvoice = BuildXeroInvoice(invoice);
            var result = await PostToXeroAsync(connection, "/Invoices", new { Invoices = new[] { xeroInvoice } });

            if (result.Success)
            {
                log.XeroRecordId = result.XeroId;
                log.Status = "Success";
                log.Message = "Invoice synced successfully";
            }
            else if (result.Error?.Contains("must be unique", StringComparison.OrdinalIgnoreCase) == true)
            {
                // Invoice with same number already exists in Xero — look it up and treat as success
                var existingId = await LookupXeroInvoiceByNumberAsync(connection, invoice.InvoiceNo);
                if (existingId is not null)
                {
                    log.XeroRecordId = existingId;
                    log.Status = "Success";
                    log.Message = "Invoice synced (existing in Xero)";
                    _logger.LogInformation("Xero invoice '{InvoiceNo}' already exists (InvoiceID: {Id}), reusing",
                        invoice.InvoiceNo, existingId);
                }
                else
                {
                    log.Status = "Failed";
                    log.Message = TruncateMessage(result.Error);
                }
            }
            else
            {
                log.Status = "Failed";
                log.Message = TruncateMessage(result.Error);
            }
        }
        catch (Exception ex)
        {
            log.Status = "Failed";
            log.Message = TruncateMessage(ex.Message);
            _logger.LogError(ex, "Failed to sync invoice {InvoiceId} to Xero", invoiceId);
        }

        await _db.SaveChangesAsync();
    }

    // ═══════════════════════════════════════════════════════════════════
    // Sync: Payment → Xero Payment
    // ═══════════════════════════════════════════════════════════════════

    public async Task SyncPaymentAsync(long paymentId, long? userId = null)
    {
        var payment = await _db.Payments
            .Include(p => p.PaymentAllocations)
                .ThenInclude(pa => pa.Invoice)
            .FirstOrDefaultAsync(p => p.PaymentId == paymentId);

        if (payment is null) return;

        var existingLog = await _db.XeroSyncLogs
            .FirstOrDefaultAsync(l => l.PaymentId == paymentId && l.SyncType == "Payment" && l.Status == "Success");
        if (existingLog is not null) return;

        var connection = await GetActiveConnectionAsync(payment.ShopId);
        if (connection is null) return;

        // Reuse existing pending/failed log or create new one
        var log = await _db.XeroSyncLogs
            .FirstOrDefaultAsync(l => l.PaymentId == paymentId && l.SyncType == "Payment" && l.Status != "Success");

        if (log is not null)
        {
            log.SyncedByUserId = userId;
            log.SyncedAt = DateTime.UtcNow;
            log.Status = "Pending";
            log.Message = null;
        }
        else
        {
            log = new XeroSyncLog
            {
                ShopId = payment.ShopId,
                SyncedByUserId = userId,
                SyncType = "Payment",
                PaymentId = paymentId,
                Status = "Pending",
                SyncedAt = DateTime.UtcNow
            };
            _db.XeroSyncLogs.Add(log);
        }
        await _db.SaveChangesAsync();

        try
        {
            bool anyAllocSynced = false;

            // Fetch a valid payment account from Xero (cached per sync batch)
            var (bankAccountId, bankAccountCode) = await GetPaymentAccountAsync(connection);

            _logger.LogInformation("Payment {PaymentNo}: using Xero account ID={AccountId} Code={Code}",
                payment.PaymentNo, bankAccountId ?? "(none)", bankAccountCode);

            // For each allocation, find the Xero Invoice ID and create a payment
            foreach (var alloc in payment.PaymentAllocations)
            {
                var invoiceXeroLog = await _db.XeroSyncLogs
                    .FirstOrDefaultAsync(l => l.InvoiceId == alloc.InvoiceId
                        && l.SyncType == "Invoice" && l.Status == "Success");

                if (invoiceXeroLog?.XeroRecordId is null) continue;

                // Build payment using Dictionary to ensure proper JSON serialization
                // Xero requires AccountID (GUID) for reliable account identification
                var accountDict = new Dictionary<string, object>();
                if (bankAccountId is not null)
                    accountDict["AccountID"] = bankAccountId;
                accountDict["Code"] = bankAccountCode;

                var xeroPayment = new Dictionary<string, object>
                {
                    ["Invoice"] = new Dictionary<string, object> { ["InvoiceID"] = invoiceXeroLog.XeroRecordId },
                    ["Account"] = accountDict,
                    ["Amount"] = alloc.AmountApplied,
                    ["Date"] = payment.PaymentDate.ToString("yyyy-MM-dd"),
                    ["Reference"] = payment.PaymentNo ?? ""
                };

                var result = await PostToXeroAsync(connection, "/Payments", new { Payments = new[] { xeroPayment } });

                if (result.Success)
                {
                    log.XeroRecordId = result.XeroId;
                    log.Status = "Success";
                    log.Message = "Payment synced successfully";
                    anyAllocSynced = true;
                }
                else
                {
                    log.Status = "Failed";
                    log.Message = TruncateMessage(result.Error);
                }
            }

            // If no allocations could be synced (invoice not in Xero yet), mark as failed with clear message
            if (!anyAllocSynced && log.Status == "Pending")
            {
                log.Status = "Failed";
                log.Message = "Invoice not yet synced to Xero. Will retry on next Sync.";
            }
        }
        catch (Exception ex)
        {
            log.Status = "Failed";
            log.Message = TruncateMessage(ex.Message);
            _logger.LogError(ex, "Failed to sync payment {PaymentId} to Xero", paymentId);
        }

        await _db.SaveChangesAsync();
    }

    // ═══════════════════════════════════════════════════════════════════
    // Sync: Credit/Refund Adjustment → Xero Credit Note
    // ═══════════════════════════════════════════════════════════════════

    public async Task SyncCreditNoteAsync(long adjustmentId, long? userId = null)
    {
        var adjustment = await _db.CreditDebitAdjustments
            .Include(a => a.Invoice)
                .ThenInclude(i => i!.Customer)
            .FirstOrDefaultAsync(a => a.AdjustmentId == adjustmentId);

        if (adjustment is null || adjustment.Invoice is null) return;

        // Only sync Credit / Refund types as credit notes
        if (adjustment.AdjustmentType != Models.Enums.AdjustmentType.Credit
            && adjustment.AdjustmentType != Models.Enums.AdjustmentType.Refund)
            return;

        var existingLog = await _db.XeroSyncLogs
            .FirstOrDefaultAsync(l => l.SyncType == "CreditNote"
                && l.Status == "Success"
                && l.Message != null && l.Message.Contains($"adj:{adjustmentId}"));
        if (existingLog is not null) return;

        var connection = await GetActiveConnectionAsync(adjustment.ShopId);
        if (connection is null) return;

        var log = new XeroSyncLog
        {
            ShopId = adjustment.ShopId,
            SyncedByUserId = userId,
            SyncType = "CreditNote",
            InvoiceId = adjustment.InvoiceId,
            Status = "Pending",
            SyncedAt = DateTime.UtcNow
        };
        _db.XeroSyncLogs.Add(log);
        await _db.SaveChangesAsync();

        try
        {
            var customer = adjustment.Invoice.Customer;
            var creditNote = new
            {
                Type = "ACCRECCREDIT",
                Contact = new { Name = customer is not null ? $"{customer.FirstName} {customer.LastName}" : "Unknown" },
                Date = DateTime.UtcNow.ToString("yyyy-MM-dd"),
                LineAmountTypes = "Exclusive",
                LineItems = new[]
                {
                    new
                    {
                        Description = $"{adjustment.AdjustmentType}: {adjustment.Reason}",
                        Quantity = 1,
                        UnitAmount = adjustment.Amount,
                        AccountCode = "200" // Xero default: Sales
                    }
                },
                Reference = $"ADJ-{adjustmentId} for INV-{adjustment.Invoice.InvoiceNo}",
                Status = "AUTHORISED"
            };

            var result = await PostToXeroAsync(connection, "/CreditNotes", new { CreditNotes = new[] { creditNote } });

            if (result.Success)
            {
                log.XeroRecordId = result.XeroId;
                log.Status = "Success";
                log.Message = $"adj:{adjustmentId} synced";
            }
            else
            {
                log.Status = "Failed";
                log.Message = TruncateMessage(result.Error);
            }
        }
        catch (Exception ex)
        {
            log.Status = "Failed";
            log.Message = TruncateMessage(ex.Message);
            _logger.LogError(ex, "Failed to sync credit note for adjustment {AdjustmentId}", adjustmentId);
        }

        await _db.SaveChangesAsync();
    }

    // ═══════════════════════════════════════════════════════════════════
    // Sync: Customer → Xero Contact
    // ═══════════════════════════════════════════════════════════════════

    public async Task SyncContactAsync(long customerId, long? userId = null)
    {
        var customer = await _db.Customers
            .FirstOrDefaultAsync(c => c.CustomerId == customerId);

        if (customer is null) return;

        // Idempotency — already synced?
        var existingLog = await _db.XeroSyncLogs
            .FirstOrDefaultAsync(l => l.SyncType == "Contact"
                && l.Status == "Success"
                && l.Message != null && l.Message.Contains($"cust:{customerId}"));
        if (existingLog is not null) return;

        var connection = await GetActiveConnectionAsync(customer.ShopId);
        if (connection is null) return;

        var log = new XeroSyncLog
        {
            ShopId = customer.ShopId,
            SyncedByUserId = userId,
            SyncType = "Contact",
            Status = "Pending",
            SyncedAt = DateTime.UtcNow
        };
        _db.XeroSyncLogs.Add(log);
        await _db.SaveChangesAsync();

        try
        {
            var contact = new
            {
                Name = $"{customer.FirstName} {customer.LastName}",
                FirstName = customer.FirstName,
                LastName = customer.LastName,
                EmailAddress = customer.Email ?? "",
                Phones = !string.IsNullOrEmpty(customer.Phone)
                    ? new[] { new { PhoneType = "DEFAULT", PhoneNumber = customer.Phone } }
                    : Array.Empty<object>(),
                Addresses = !string.IsNullOrEmpty(customer.Address)
                    ? new[] { new { AddressType = "STREET", AddressLine1 = customer.Address } }
                    : Array.Empty<object>()
            };

            var result = await PostToXeroAsync(connection, "/Contacts", new { Contacts = new[] { contact } });

            if (result.Success)
            {
                log.XeroRecordId = result.XeroId;
                log.Status = "Success";
                log.Message = $"cust:{customerId} synced";
            }
            else if (result.Error?.Contains("already assigned", StringComparison.OrdinalIgnoreCase) == true)
            {
                // Contact with same name exists — look it up by name and treat as success
                var contactName = $"{customer.FirstName} {customer.LastName}";
                var existingId = await LookupXeroContactByNameAsync(connection, contactName);
                if (existingId is not null)
                {
                    log.XeroRecordId = existingId;
                    log.Status = "Success";
                    log.Message = $"cust:{customerId} synced (existing contact)";
                    _logger.LogInformation("Xero contact '{Name}' already exists (ContactID: {Id}), reusing", contactName, existingId);
                }
                else
                {
                    log.Status = "Failed";
                    log.Message = TruncateMessage(result.Error);
                }
            }
            else
            {
                log.Status = "Failed";
                log.Message = TruncateMessage(result.Error);
            }
        }
        catch (Exception ex)
        {
            log.Status = "Failed";
            log.Message = TruncateMessage(ex.Message);
            _logger.LogError(ex, "Failed to sync contact for customer {CustomerId}", customerId);
        }

        await _db.SaveChangesAsync();
    }

    // ═══════════════════════════════════════════════════════════════════
    // Bulk Sync
    // ═══════════════════════════════════════════════════════════════════

    public async Task<(int Synced, int Failed)> SyncAllAsync(long shopId, long? userId = null)
    {
        _accountCache.Clear(); // Fresh cache for each sync batch

        var connection = await GetActiveConnectionAsync(shopId);
        if (connection is null) return (0, 0);

        int synced = 0, failed = 0;

        // 1. Batch-load all sync status in one query
        var allSyncLogs = await _db.XeroSyncLogs
            .Where(l => l.ShopId == shopId && l.Status == "Success")
            .Select(l => new { l.SyncType, l.InvoiceId, l.PaymentId })
            .ToListAsync();

        var syncedInvoiceIds = allSyncLogs
            .Where(l => l.SyncType == "Invoice" && l.InvoiceId != null)
            .Select(l => l.InvoiceId!.Value)
            .ToHashSet();

        var syncedPaymentIds = allSyncLogs
            .Where(l => l.SyncType == "Payment" && l.PaymentId != null)
            .Select(l => l.PaymentId!.Value)
            .ToHashSet();

        // 2. Get unsynced invoices
        var unsyncedInvoiceIds = await _db.Invoices
            .Where(i => i.ShopId == shopId && !i.IsArchived)
            .Select(i => i.InvoiceId)
            .Where(id => !syncedInvoiceIds.Contains(id))
            .ToListAsync();

        _logger.LogInformation("SyncAll: {InvCount} invoices to sync", unsyncedInvoiceIds.Count);

        foreach (var invoiceId in unsyncedInvoiceIds)
        {
            try
            {
                await SyncInvoiceAsync(invoiceId, userId);
                var log = await _db.XeroSyncLogs
                    .OrderByDescending(l => l.SyncedAt)
                    .FirstOrDefaultAsync(l => l.InvoiceId == invoiceId && l.SyncType == "Invoice");
                if (log?.Status == "Success") synced++; else failed++;
            }
            catch { failed++; }
        }

        // 3. Pre-warm account cache before payment syncs (single API call)
        await GetPaymentAccountAsync(connection);

        // 4. Get unsynced payments
        var unsyncedPaymentIds = await _db.Payments
            .Where(p => p.ShopId == shopId)
            .Select(p => p.PaymentId)
            .Where(id => !syncedPaymentIds.Contains(id))
            .ToListAsync();

        _logger.LogInformation("SyncAll: {PayCount} payments to sync", unsyncedPaymentIds.Count);

        foreach (var paymentId in unsyncedPaymentIds)
        {
            try
            {
                await SyncPaymentAsync(paymentId, userId);
                var log = await _db.XeroSyncLogs
                    .OrderByDescending(l => l.SyncedAt)
                    .FirstOrDefaultAsync(l => l.PaymentId == paymentId && l.SyncType == "Payment");
                if (log?.Status == "Success") synced++; else failed++;
            }
            catch { failed++; }
        }

        return (synced, failed);
    }

    /// <summary>
    /// Diagnostic: Test Xero accounts API and return available accounts.
    /// </summary>
    public async Task<object> TestXeroAccountsAsync(long shopId)
    {
        var connection = await GetActiveConnectionAsync(shopId);
        if (connection is null)
            return new { success = false, message = "No active Xero connection" };

        try
        {
            using var req = new HttpRequestMessage(HttpMethod.Get, $"{XeroApiUrl}/Accounts");
            req.Headers.Authorization = new AuthenticationHeaderValue("Bearer", connection.AccessToken);
            req.Headers.Add("Xero-Tenant-Id", connection.XeroTenantId);
            req.Headers.Accept.Add(new MediaTypeWithQualityHeaderValue("application/json"));

            var resp = await _http.SendAsync(req);
            var body = await resp.Content.ReadAsStringAsync();

            if (!resp.IsSuccessStatusCode)
                return new { success = false, status = (int)resp.StatusCode, body };

            using var doc = JsonDocument.Parse(body);
            var accountSummaries = new List<object>();

            if (doc.RootElement.TryGetProperty("Accounts", out var accounts))
            {
                foreach (var acct in accounts.EnumerateArray())
                {
                    accountSummaries.Add(new
                    {
                        AccountID = acct.TryGetProperty("AccountID", out var id) ? id.GetString() : null,
                        Code = acct.TryGetProperty("Code", out var code) ? code.GetString() : null,
                        Name = acct.TryGetProperty("Name", out var name) ? name.GetString() : null,
                        Type = acct.TryGetProperty("Type", out var type) ? type.GetString() : null,
                        Status = acct.TryGetProperty("Status", out var status) ? status.GetString() : null,
                        EnablePayments = acct.TryGetProperty("EnablePaymentsToAccount", out var ep) && ep.ValueKind == JsonValueKind.True
                    });
                }
            }

            return new { success = true, totalAccounts = accountSummaries.Count, accounts = accountSummaries };
        }
        catch (Exception ex)
        {
            return new { success = false, message = ex.Message };
        }
    }

    // ═══════════════════════════════════════════════════════════════════
    // Private Helpers
    // ═══════════════════════════════════════════════════════════════════

    private async Task<XeroConnection?> GetActiveConnectionAsync(long shopId)
    {
        var conn = await _db.Set<XeroConnection>()
            .FirstOrDefaultAsync(x => x.ShopId == shopId && x.IsActive);

        if (conn is null) return null;

        // Refresh token if near expiry (< 5 min remaining)
        if (conn.TokenExpiresAt <= DateTime.UtcNow.AddMinutes(5))
        {
            var refreshed = await RefreshTokenAsync(conn);
            if (!refreshed)
            {
                _logger.LogWarning("Token refresh failed for shop {ShopId}, deactivating connection", shopId);
                conn.IsActive = false;
                await _db.SaveChangesAsync();
                return null;
            }
        }

        return conn;
    }

    private async Task<bool> RefreshTokenAsync(XeroConnection connection)
    {
        try
        {
            var tokenRequest = new FormUrlEncodedContent(new Dictionary<string, string>
            {
                ["grant_type"] = "refresh_token",
                ["refresh_token"] = connection.RefreshToken
            });

            using var req = new HttpRequestMessage(HttpMethod.Post, $"{XeroIdentityUrl}/connect/token");
            req.Content = tokenRequest;
            req.Headers.Authorization = new AuthenticationHeaderValue(
                "Basic",
                Convert.ToBase64String(Encoding.UTF8.GetBytes($"{_settings.ClientId}:{_settings.ClientSecret}"))
            );

            var response = await _http.SendAsync(req);
            var json = await response.Content.ReadAsStringAsync();

            if (!response.IsSuccessStatusCode)
            {
                _logger.LogError("Xero token refresh failed: {Status} - {Body}", response.StatusCode, json);
                return false;
            }
            var tokenData = JsonSerializer.Deserialize<XeroTokenResponse>(json);
            if (tokenData is null) return false;

            connection.AccessToken = tokenData.AccessToken;
            connection.RefreshToken = tokenData.RefreshToken;
            connection.TokenExpiresAt = DateTime.UtcNow.AddSeconds(tokenData.ExpiresIn);
            await _db.SaveChangesAsync();

            return true;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error refreshing Xero token for connection {Id}", connection.XeroConnectionId);
            return false;
        }
    }

    private async Task<List<XeroTenant>?> GetTenantsAsync(string accessToken)
    {
        using var req = new HttpRequestMessage(HttpMethod.Get, "https://api.xero.com/connections");
        req.Headers.Authorization = new AuthenticationHeaderValue("Bearer", accessToken);
        var response = await _http.SendAsync(req);
        if (!response.IsSuccessStatusCode) return null;

        var json = await response.Content.ReadAsStringAsync();
        return JsonSerializer.Deserialize<List<XeroTenant>>(json);
    }

    private Task<XeroApiResult> PostToXeroAsync(XeroConnection connection, string endpoint, object payload)
        => SendToXeroAsync(connection, endpoint, payload, HttpMethod.Put);

    private async Task<XeroApiResult> SendToXeroAsync(XeroConnection connection, string endpoint, object payload, HttpMethod method)
    {
        var jsonOptions = new JsonSerializerOptions
        {
            // Xero API expects PascalCase property names (e.g. InvoiceID, AccountCode)
            PropertyNamingPolicy = null,
            DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull
        };

        var jsonPayload = JsonSerializer.Serialize(payload, jsonOptions);
        _logger.LogInformation("Xero API {Method} {Endpoint} payload: {Payload}", method, endpoint, jsonPayload);

        using var req = new HttpRequestMessage(method, $"{XeroApiUrl}{endpoint}");
        req.Headers.Authorization = new AuthenticationHeaderValue("Bearer", connection.AccessToken);
        req.Headers.Add("Xero-Tenant-Id", connection.XeroTenantId);
        req.Headers.Accept.Add(new MediaTypeWithQualityHeaderValue("application/json"));
        req.Content = new StringContent(jsonPayload, Encoding.UTF8, "application/json");

        var response = await _http.SendAsync(req);
        var responseBody = await response.Content.ReadAsStringAsync();

        if (response.IsSuccessStatusCode)
        {
            // Try to extract the Xero ID from the response
            var xeroId = ExtractXeroId(responseBody, endpoint);
            return new XeroApiResult { Success = true, XeroId = xeroId };
        }

        _logger.LogError("Xero API error on {Endpoint}: {Status} - {Body}",
            endpoint, response.StatusCode, responseBody);

        // Extract meaningful validation error messages from Xero response
        var errorMessage = ExtractXeroValidationErrors(responseBody) ?? responseBody;
        return new XeroApiResult { Success = false, Error = errorMessage };
    }

    private static string? ExtractXeroId(string responseBody, string endpoint)
    {
        try
        {
            using var doc = JsonDocument.Parse(responseBody);
            var root = doc.RootElement;

            // Xero wraps responses: { "Invoices": [...], "Payments": [...], etc. }
            string collectionKey = endpoint.TrimStart('/') switch
            {
                "Invoices" => "Invoices",
                "Payments" => "Payments",
                "CreditNotes" => "CreditNotes",
                "Contacts" => "Contacts",
                _ => ""
            };

            if (string.IsNullOrEmpty(collectionKey)) return null;

            if (root.TryGetProperty(collectionKey, out var array) && array.GetArrayLength() > 0)
            {
                var first = array[0];
                // Try common ID property names
                foreach (var idProp in new[] { $"{collectionKey[..^1]}ID", "ContactID", "InvoiceID", "PaymentID", "CreditNoteID" })
                {
                    if (first.TryGetProperty(idProp, out var idVal))
                        return idVal.GetString();
                }
            }
        }
        catch { /* Swallow parse errors */ }
        return null;
    }

    private object BuildXeroInvoice(Invoice invoice)
    {
        var lineItems = invoice.InvoiceLines.Select(line => new
        {
            Description = line.Description,
            Quantity = (decimal)line.Qty,
            UnitAmount = line.UnitPrice,
            AccountCode = "200" // Xero default: Sales
        }).ToList();

        var contactName = invoice.Customer is not null
            ? $"{invoice.Customer.FirstName} {invoice.Customer.LastName}"
            : "Unknown";

        return new
        {
            Type = "ACCREC",
            Contact = new { Name = contactName },
            Date = invoice.InvoiceDate.ToString("yyyy-MM-dd"),
            DueDate = (invoice.DueDate ?? invoice.InvoiceDate.AddDays(30)).ToString("yyyy-MM-dd"),
            LineAmountTypes = "Inclusive",
            LineItems = lineItems,
            InvoiceNumber = invoice.InvoiceNo,
            Reference = $"ByteBill-{invoice.InvoiceNo}",
            Status = "AUTHORISED"
        };
    }

    private static string MapPaymentMethodToAccountCode(Models.Enums.PaymentMethod method)
    {
        // Xero default chart of accounts
        return method switch
        {
            Models.Enums.PaymentMethod.Cash => "090",  // Xero: Checking Account
            Models.Enums.PaymentMethod.GCash => "090",
            Models.Enums.PaymentMethod.Card => "090",
            _ => "090"
        };
    }

    /// <summary>
    /// Fetch a valid payment-eligible account from Xero.
    /// Searches for BANK accounts first, then falls back to any ASSET/CURRENT account.
    /// Caches the result per connection to avoid repeated API calls.
    /// </summary>
    private async Task<(string? AccountId, string Code)> GetPaymentAccountAsync(XeroConnection connection)
    {
        // Return cached result if we already looked up for this connection
        var cacheKey = $"xero_acct_{connection.XeroConnectionId}";
        if (_accountCache.TryGetValue(cacheKey, out var cached))
            return cached;

        try
        {
            // Fetch ALL accounts that can receive payments (BANK accounts + enable-for-payments)
            using var req = new HttpRequestMessage(HttpMethod.Get,
                $"{XeroApiUrl}/Accounts");
            req.Headers.Authorization = new AuthenticationHeaderValue("Bearer", connection.AccessToken);
            req.Headers.Add("Xero-Tenant-Id", connection.XeroTenantId);
            req.Headers.Accept.Add(new MediaTypeWithQualityHeaderValue("application/json"));

            var resp = await _http.SendAsync(req);
            var body = await resp.Content.ReadAsStringAsync();

            if (!resp.IsSuccessStatusCode)
            {
                _logger.LogError("Xero Accounts API failed ({Status}): {Body}", resp.StatusCode, body);
                return (null, "090");
            }

            using var doc = JsonDocument.Parse(body);
            if (!doc.RootElement.TryGetProperty("Accounts", out var accounts))
                return (null, "090");

            // Priority: 1) BANK type, 2) EnablePaymentsToAccount=true, 3) Any asset account
            string? bestAccountId = null;
            string bestCode = "090";

            foreach (var acct in accounts.EnumerateArray())
            {
                var type = acct.TryGetProperty("Type", out var typeProp) ? typeProp.GetString() : "";
                var status = acct.TryGetProperty("Status", out var statusProp) ? statusProp.GetString() : "";
                var accountId = acct.TryGetProperty("AccountID", out var idProp) ? idProp.GetString() : null;
                var code = acct.TryGetProperty("Code", out var codeProp) ? codeProp.GetString() : null;
                var name = acct.TryGetProperty("Name", out var nameProp) ? nameProp.GetString() : "";
                var enablePayments = acct.TryGetProperty("EnablePaymentsToAccount", out var epProp) && epProp.GetBoolean();

                if (status != "ACTIVE" || accountId is null || code is null)
                    continue;

                _logger.LogDebug("Xero Account: {Code} {Name} Type={Type} EnablePayments={Enable}",
                    code, name, type, enablePayments);

                // Best match: BANK account
                if (type == "BANK")
                {
                    bestAccountId = accountId;
                    bestCode = code;
                    _logger.LogInformation("Using Xero BANK account: {Code} {Name} ({AccountId})", code, name, accountId);
                    break; // BANK is ideal, stop looking
                }

                // Second best: account with payments enabled
                if (enablePayments && bestAccountId is null)
                {
                    bestAccountId = accountId;
                    bestCode = code;
                }
            }

            if (bestAccountId is not null)
            {
                var result = (bestAccountId, bestCode);
                _accountCache[cacheKey] = result;
                return result;
            }

            _logger.LogWarning("No suitable payment account found in Xero. Total accounts: {Count}",
                accounts.GetArrayLength());
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to fetch Xero accounts");
        }

        return (null, "090");
    }

    // Simple in-memory cache for account lookups during a sync batch
    private readonly Dictionary<string, (string? AccountId, string Code)> _accountCache = new();

    /// <summary>Look up an existing Xero contact by exact name. Returns ContactID or null.</summary>
    private async Task<string?> LookupXeroContactByNameAsync(XeroConnection connection, string contactName)
    {
        try
        {
            var encoded = Uri.EscapeDataString($"Name==\"{contactName}\"");
            using var req = new HttpRequestMessage(HttpMethod.Get,
                $"{XeroApiUrl}/Contacts?where={encoded}");
            req.Headers.Authorization = new AuthenticationHeaderValue("Bearer", connection.AccessToken);
            req.Headers.Add("Xero-Tenant-Id", connection.XeroTenantId);
            req.Headers.Accept.Add(new MediaTypeWithQualityHeaderValue("application/json"));

            var response = await _http.SendAsync(req);
            if (!response.IsSuccessStatusCode) return null;

            var body = await response.Content.ReadAsStringAsync();
            using var doc = JsonDocument.Parse(body);
            if (doc.RootElement.TryGetProperty("Contacts", out var contacts) && contacts.GetArrayLength() > 0)
            {
                if (contacts[0].TryGetProperty("ContactID", out var id))
                    return id.GetString();
            }
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Failed to look up Xero contact by name '{Name}'", contactName);
        }
        return null;
    }

    /// <summary>Look up an existing Xero invoice by InvoiceNumber. Returns InvoiceID or null.</summary>
    private async Task<string?> LookupXeroInvoiceByNumberAsync(XeroConnection connection, string invoiceNo)
    {
        try
        {
            var encoded = Uri.EscapeDataString($"InvoiceNumber==\"{invoiceNo}\"");
            using var req = new HttpRequestMessage(HttpMethod.Get,
                $"{XeroApiUrl}/Invoices?where={encoded}");
            req.Headers.Authorization = new AuthenticationHeaderValue("Bearer", connection.AccessToken);
            req.Headers.Add("Xero-Tenant-Id", connection.XeroTenantId);
            req.Headers.Accept.Add(new MediaTypeWithQualityHeaderValue("application/json"));

            var response = await _http.SendAsync(req);
            if (!response.IsSuccessStatusCode) return null;

            var body = await response.Content.ReadAsStringAsync();
            using var doc = JsonDocument.Parse(body);
            if (doc.RootElement.TryGetProperty("Invoices", out var invoices) && invoices.GetArrayLength() > 0)
            {
                if (invoices[0].TryGetProperty("InvoiceID", out var id))
                    return id.GetString();
            }
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Failed to look up Xero invoice by number '{InvoiceNo}'", invoiceNo);
        }
        return null;
    }

    /// <summary>
    /// Parse Xero API error response to extract human-readable validation error messages.
    /// </summary>
    private static string? ExtractXeroValidationErrors(string responseBody)
    {
        try
        {
            using var doc = JsonDocument.Parse(responseBody);
            var root = doc.RootElement;
            var errors = new List<string>();

            // Check top-level Message
            if (root.TryGetProperty("Message", out var topMsg))
                errors.Add(topMsg.GetString() ?? "");

            // Check Elements[].ValidationErrors[].Message
            if (root.TryGetProperty("Elements", out var elements))
            {
                foreach (var element in elements.EnumerateArray())
                {
                    if (element.TryGetProperty("ValidationErrors", out var valErrors))
                    {
                        foreach (var ve in valErrors.EnumerateArray())
                        {
                            if (ve.TryGetProperty("Message", out var veMsg))
                                errors.Add(veMsg.GetString() ?? "");
                        }
                    }
                }
            }

            if (errors.Count > 0)
                return string.Join(" | ", errors.Where(e => !string.IsNullOrWhiteSpace(e)));
        }
        catch { /* Fall back to raw response */ }
        return null;
    }

    private static string TruncateMessage(string? msg)
        => msg is null ? "" : msg.Length > 500 ? msg[..500] : msg;

    // ─── DTO classes for Xero responses ─────────────────────────────
    private class XeroTokenResponse
    {
        [JsonPropertyName("access_token")]
        public string AccessToken { get; set; } = string.Empty;

        [JsonPropertyName("refresh_token")]
        public string RefreshToken { get; set; } = string.Empty;

        [JsonPropertyName("expires_in")]
        public int ExpiresIn { get; set; }

        [JsonPropertyName("token_type")]
        public string TokenType { get; set; } = string.Empty;
    }

    private class XeroTenant
    {
        [JsonPropertyName("tenantId")]
        public string TenantId { get; set; } = string.Empty;

        [JsonPropertyName("tenantName")]
        public string TenantName { get; set; } = string.Empty;

        [JsonPropertyName("tenantType")]
        public string TenantType { get; set; } = string.Empty;
    }

    private class XeroApiResult
    {
        public bool Success { get; set; }
        public string? XeroId { get; set; }
        public string? Error { get; set; }
    }
}
