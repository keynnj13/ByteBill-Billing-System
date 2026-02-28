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

            // For each allocation, find the Xero Invoice ID and create a payment
            foreach (var alloc in payment.PaymentAllocations)
            {
                var invoiceXeroLog = await _db.XeroSyncLogs
                    .FirstOrDefaultAsync(l => l.InvoiceId == alloc.InvoiceId
                        && l.SyncType == "Invoice" && l.Status == "Success");

                if (invoiceXeroLog?.XeroRecordId is null) continue;

                var xeroPayment = new
                {
                    Invoice = new { InvoiceID = invoiceXeroLog.XeroRecordId },
                    Account = new { Code = MapPaymentMethodToAccountCode(payment.Method) },
                    Amount = alloc.AmountApplied,
                    Date = payment.PaymentDate.ToString("yyyy-MM-dd"),
                    Reference = payment.PaymentNo
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
        var connection = await GetActiveConnectionAsync(shopId);
        if (connection is null) return (0, 0);

        int synced = 0, failed = 0;

        // 1. Get all invoice IDs for this shop that haven't been successfully synced
        var allInvoiceIds = await _db.Invoices
            .Where(i => i.ShopId == shopId && !i.IsArchived)
            .Select(i => i.InvoiceId)
            .ToListAsync();

        var syncedInvoiceIds = await _db.XeroSyncLogs
            .Where(l => l.ShopId == shopId && l.SyncType == "Invoice" && l.Status == "Success" && l.InvoiceId != null)
            .Select(l => l.InvoiceId!.Value)
            .ToListAsync();

        var unsyncedInvoiceIds = allInvoiceIds.Except(syncedInvoiceIds).ToList();

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

        // 2. Get all payment IDs that haven't been successfully synced
        //    (includes previously failed payments that couldn't find their Xero invoice)
        var allPaymentIds = await _db.Payments
            .Where(p => p.ShopId == shopId)
            .Select(p => p.PaymentId)
            .ToListAsync();

        var syncedPaymentIds = await _db.XeroSyncLogs
            .Where(l => l.ShopId == shopId && l.SyncType == "Payment" && l.Status == "Success" && l.PaymentId != null)
            .Select(l => l.PaymentId!.Value)
            .ToListAsync();

        var unsyncedPaymentIds = allPaymentIds.Except(syncedPaymentIds).ToList();

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
            if (!response.IsSuccessStatusCode)
            {
                _logger.LogError("Xero token refresh failed: {Status}", response.StatusCode);
                return false;
            }

            var json = await response.Content.ReadAsStringAsync();
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

    private async Task<XeroApiResult> PostToXeroAsync(XeroConnection connection, string endpoint, object payload)
    {
        var jsonOptions = new JsonSerializerOptions
        {
            PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
            DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull
        };

        using var req = new HttpRequestMessage(HttpMethod.Post, $"{XeroApiUrl}{endpoint}");
        req.Headers.Authorization = new AuthenticationHeaderValue("Bearer", connection.AccessToken);
        req.Headers.Add("Xero-Tenant-Id", connection.XeroTenantId);
        req.Headers.Accept.Add(new MediaTypeWithQualityHeaderValue("application/json"));
        req.Content = new StringContent(
            JsonSerializer.Serialize(payload, jsonOptions),
            Encoding.UTF8,
            "application/json"
        );

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
        return new XeroApiResult { Success = false, Error = responseBody };
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

    private static string TruncateMessage(string? msg)
        => msg is null ? "" : msg.Length > 250 ? msg[..250] : msg;

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
