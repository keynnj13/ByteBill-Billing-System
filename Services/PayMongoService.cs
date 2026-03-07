using ByteBill_BS.Data;
using ByteBill_BS.DTOs.Common;
using ByteBill_BS.DTOs.PayMongo;
using ByteBill_BS.Models;
using ByteBill_BS.Models.Enums;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using System.Net.Http.Headers;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using System.Text.Json.Nodes;

namespace ByteBill_BS.Services;

// ── Configuration ────────────────────────────────────────────────────────
public class PayMongoSettings
{
    public string SecretKey { get; set; } = string.Empty;
    public string PublicKey { get; set; } = string.Empty;
    public string BaseUrl { get; set; } = "https://api.paymongo.com/v1";
    public string WebhookSecret { get; set; } = string.Empty;
    public string Currency { get; set; } = "PHP";
    public string SuccessUrl { get; set; } = string.Empty;
    public string CancelUrl { get; set; } = string.Empty;
    public string? SiteBaseUrl { get; set; }
}

// ── Interface ────────────────────────────────────────────────────────────
public interface IPayMongoService
{
    /// <summary>Create a PayMongo Payment Link for an invoice (for remote / emailed payments).</summary>
    Task<ApiResponse<PayMongoPaymentResult>> CreatePaymentLinkAsync(long shopId, long userId, CreatePayMongoLinkRequest req);

    /// <summary>Create a PayMongo Checkout Session for an invoice (for in-shop / kiosk payments).</summary>
    Task<ApiResponse<PayMongoPaymentResult>> CreateCheckoutSessionAsync(long shopId, long userId, CreateCheckoutSessionRequest req);

    /// <summary>Process a PayMongo webhook event. Returns true if handled.</summary>
    Task<bool> HandleWebhookAsync(string rawBody, string? svixId, string? svixTimestamp, string? svixSignature);

    /// <summary>Check the status of a PayMongo transaction.</summary>
    Task<PayMongoStatusDto?> GetStatusAsync(long shopId, long payMongoTxnId);

    /// <summary>Get all PayMongo transactions for an invoice.</summary>
    Task<List<PayMongoStatusDto>> GetByInvoiceAsync(long shopId, long invoiceId);

    /// <summary>
    /// Verify payment status directly with PayMongo API and record the payment if confirmed.
    /// Used as a fallback when webhooks can't reach the server (e.g. localhost development).
    /// </summary>
    Task<bool> VerifyAndRecordPaymentAsync(long invoiceId);

    /// <summary>Tests PayMongo API connectivity using the configured secret key.</summary>
    Task<(bool Success, string Message)> TestConnectionAsync();

    /// <summary>Issue a refund via PayMongo API for a confirmed payment linked to an invoice.</summary>
    Task<(bool Success, string Message)> RefundPaymentAsync(long shopId, long invoiceId, decimal amount, string reason);

    /// <summary>Expire/cancel all pending PayMongo checkout sessions and links for an invoice.</summary>
    Task<int> ExpirePendingSessionsAsync(long shopId, long invoiceId);
}

// ── Implementation ───────────────────────────────────────────────────────
public class PayMongoService : IPayMongoService
{
    private readonly ApplicationDbContext _db;
    private readonly IAuditService _audit;
    private readonly HttpClient _http;
    private readonly PayMongoSettings _settings;
    private readonly ILogger<PayMongoService> _logger;
    private readonly IHttpContextAccessor _httpCtx;
    private readonly INotificationService _notif;
    private readonly IBillingCalculationService _billing;
    private readonly IServiceScopeFactory _scopeFactory;

    public PayMongoService(
        ApplicationDbContext db,
        IAuditService audit,
        HttpClient http,
        IOptions<PayMongoSettings> settings,
        ILogger<PayMongoService> logger,
        IHttpContextAccessor httpCtx,
        INotificationService notif,
        IBillingCalculationService billing,
        IServiceScopeFactory scopeFactory)
    {
        _db = db;
        _audit = audit;
        _http = http;
        _settings = settings.Value;
        _logger = logger;
        _httpCtx = httpCtx;
        _notif = notif;
        _billing = billing;
        _scopeFactory = scopeFactory;

        // Configure HTTP client for PayMongo Basic Auth (secret key as username, blank password)
        var authBytes = Encoding.UTF8.GetBytes($"{_settings.SecretKey}:");
        _http.DefaultRequestHeaders.Authorization =
            new AuthenticationHeaderValue("Basic", Convert.ToBase64String(authBytes));
        _http.DefaultRequestHeaders.Accept.Add(new MediaTypeWithQualityHeaderValue("application/json"));
    }

    private string? ClientIp => _httpCtx.HttpContext?.Connection.RemoteIpAddress?.ToString();

    // ═══════════════════════════════════════════════════════════════════
    //  CREATE PAYMENT LINK
    // ═══════════════════════════════════════════════════════════════════
    public async Task<ApiResponse<PayMongoPaymentResult>> CreatePaymentLinkAsync(
        long shopId, long userId, CreatePayMongoLinkRequest req)
    {
        // Load invoice with customer
        var invoice = await _db.Invoices
            .Include(i => i.Customer)
            .Include(i => i.InvoiceLines)
            .FirstOrDefaultAsync(i => i.ShopId == shopId && i.InvoiceId == req.InvoiceId);

        if (invoice is null)
            return ApiResponse<PayMongoPaymentResult>.Fail("Invoice not found.");

        if (invoice.Status == InvoiceStatus.Paid)
            return ApiResponse<PayMongoPaymentResult>.Fail("Invoice is already fully paid.");

        if (invoice.Status == InvoiceStatus.Void)
            return ApiResponse<PayMongoPaymentResult>.Fail("Cannot create payment for a voided invoice.");

        if (invoice.Balance <= 0)
            return ApiResponse<PayMongoPaymentResult>.Fail("Invoice has no outstanding balance.");

        // Check for existing pending PayMongo transaction for this invoice
        var existingPending = await _db.PayMongoTxns
            .Where(t => t.ShopId == shopId
                && t.InvoiceId == req.InvoiceId
                && t.PayMongoStatus == "pending"
                && t.ResourceType == "link")
            .FirstOrDefaultAsync();

        if (existingPending != null)
        {
            // Return existing pending link
            return ApiResponse<PayMongoPaymentResult>.Ok(new PayMongoPaymentResult
            {
                PaymentId = 0,
                PayMongoTxnId = existingPending.PayMongoTxnId,
                PayMongoResourceId = existingPending.PayMongoPaymentIntentId,
                ResourceType = "link",
                CheckoutUrl = existingPending.CheckoutUrl ?? "",
                Status = "pending",
                InvoiceId = req.InvoiceId,
                InvoiceNo = invoice.InvoiceNo,
                Amount = invoice.Balance
            });
        }

        // Amount in centavos (PHP minor unit)
        var amountCentavos = (long)(invoice.Balance * 100);
        var description = req.Description ?? $"Payment for Invoice {invoice.InvoiceNo}";

        // Create PayMongo Payment Link via API
        var payload = new
        {
            data = new
            {
                attributes = new
                {
                    amount = amountCentavos,
                    description,
                    remarks = $"Invoice: {invoice.InvoiceNo} | Customer: {invoice.Customer?.FullName ?? "N/A"}"
                }
            }
        };

        var json = JsonSerializer.Serialize(payload, new JsonSerializerOptions { PropertyNamingPolicy = JsonNamingPolicy.CamelCase });
        var content = new StringContent(json, Encoding.UTF8, "application/json");

        HttpResponseMessage response;
        string responseBody;
        try
        {
            response = await _http.PostAsync($"{_settings.BaseUrl}/links", content);
            responseBody = await response.Content.ReadAsStringAsync();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "PayMongo API call failed for Payment Link creation");
            return ApiResponse<PayMongoPaymentResult>.Fail($"PayMongo API error: {ex.Message}");
        }

        if (!response.IsSuccessStatusCode)
        {
            _logger.LogWarning("PayMongo Payment Link creation failed: {Status} {Body}", response.StatusCode, responseBody);
            return ApiResponse<PayMongoPaymentResult>.Fail($"PayMongo returned {response.StatusCode}. Check logs for details.");
        }

        // Parse response
        var jsonDoc = JsonNode.Parse(responseBody);
        var linkId = jsonDoc?["data"]?["id"]?.GetValue<string>() ?? "";
        var checkoutUrl = jsonDoc?["data"]?["attributes"]?["checkout_url"]?.GetValue<string>() ?? "";
        var status = jsonDoc?["data"]?["attributes"]?["status"]?.GetValue<string>() ?? "unpaid";

        // Only create a PayMongoTxn record — NO Payment until webhook confirms
        var txn = new PayMongoTxn
        {
            ShopId = shopId,
            InvoiceId = invoice.InvoiceId,
            InitiatedByUserId = userId,
            Amount = invoice.Balance,
            PayMongoPaymentIntentId = linkId,
            PayMongoStatus = "pending",
            CheckoutUrl = checkoutUrl,
            ResourceType = "link",
            RawResponse = responseBody,
            CreatedAt = DateTime.UtcNow
        };

        _db.PayMongoTxns.Add(txn);
        await _db.SaveChangesAsync();

        await _audit.LogAsync(shopId, userId, "Create", "PayMongo", txn.PayMongoTxnId,
            $"Created PayMongo Payment Link for invoice '{invoice.InvoiceNo}'. Amount: ₱{invoice.Balance:N2}. Link ID: {linkId}.", ClientIp);

        return ApiResponse<PayMongoPaymentResult>.Ok(new PayMongoPaymentResult
        {
            PaymentId = 0,
            PayMongoTxnId = txn.PayMongoTxnId,
            PayMongoResourceId = linkId,
            ResourceType = "link",
            CheckoutUrl = checkoutUrl,
            Status = "pending",
            InvoiceId = invoice.InvoiceId,
            InvoiceNo = invoice.InvoiceNo,
            Amount = invoice.Balance
        });
    }

    // ═══════════════════════════════════════════════════════════════════
    //  CREATE CHECKOUT SESSION
    // ═══════════════════════════════════════════════════════════════════
    public async Task<ApiResponse<PayMongoPaymentResult>> CreateCheckoutSessionAsync(
        long shopId, long userId, CreateCheckoutSessionRequest req)
    {
        var invoice = await _db.Invoices
            .Include(i => i.Customer)
            .Include(i => i.InvoiceLines)
            .Include(i => i.InvoiceDiscounts)
            .Include(i => i.Shop)
            .FirstOrDefaultAsync(i => i.ShopId == shopId && i.InvoiceId == req.InvoiceId);

        if (invoice is null)
            return ApiResponse<PayMongoPaymentResult>.Fail("Invoice not found.");

        if (invoice.Status == InvoiceStatus.Paid)
            return ApiResponse<PayMongoPaymentResult>.Fail("Invoice is already fully paid.");

        if (invoice.Status == InvoiceStatus.Void)
            return ApiResponse<PayMongoPaymentResult>.Fail("Cannot create payment for a voided invoice.");

        if (invoice.Balance <= 0)
            return ApiResponse<PayMongoPaymentResult>.Fail("Invoice has no outstanding balance.");

        var amountCentavos = (long)(invoice.Balance * 100);

        // Build description with VAT/discount breakdown
        var descParts = new List<string> { $"Payment for Invoice {invoice.InvoiceNo}" };
        var subtotal = invoice.InvoiceLines.Sum(l => l.Qty * l.UnitPrice);

        if (invoice.DiscountAmount > 0)
        {
            var discountLabels = invoice.InvoiceDiscounts.Any()
                ? string.Join(", ", invoice.InvoiceDiscounts.Select(d => d.Label))
                : "Discount";
            descParts.Add($"Subtotal: ₱{subtotal:N2}");
            descParts.Add($"{discountLabels}: -₱{invoice.DiscountAmount:N2}");
        }

        if (invoice.VatAmount > 0)
            descParts.Add($"VAT (12%): ₱{invoice.VatAmount:N2}");

        descParts.Add($"Total: ₱{invoice.Balance:N2}");

        var description = req.Description ?? string.Join(" | ", descParts);

        // Build line items from invoice lines
        // When discounts exist, proportionally scale item prices so the PayMongo total equals the invoice balance
        var lineItems = new List<object>();
        var hasDiscountOrAdjustment = subtotal != invoice.Balance && subtotal > 0;
        var scaleFactor = hasDiscountOrAdjustment && subtotal > 0
            ? invoice.Balance / subtotal
            : 1m;

        foreach (var line in invoice.InvoiceLines)
        {
            var lineTotal = line.Qty * line.UnitPrice;
            var adjustedTotal = Math.Round(lineTotal * scaleFactor, 2);
            var adjustedUnitPrice = line.Qty > 0 ? Math.Round(adjustedTotal / line.Qty, 2) : adjustedTotal;

            var itemName = line.Description;
            var itemDesc = line.Description;

            // If prices were adjusted, show original price in description
            if (hasDiscountOrAdjustment && adjustedUnitPrice != line.UnitPrice)
            {
                itemDesc = $"{line.Description} (Original: ₱{line.UnitPrice:N2})";
            }

            lineItems.Add(new
            {
                currency = _settings.Currency,
                amount = (long)(adjustedUnitPrice * 100) * line.Qty,
                description = itemDesc,
                name = itemName,
                quantity = 1 // Use quantity=1 since amount is already the line total
            });
        }

        // Ensure line items total matches the invoice balance exactly
        var lineItemsSum = lineItems.Cast<dynamic>().Sum(li => (long)li.amount);
        if (lineItemsSum != amountCentavos && lineItems.Count > 0)
        {
            // Adjust the last line item to reconcile any rounding differences
            var diff = amountCentavos - lineItemsSum;
            var lastItem = lineItems[^1];
            var lastAmount = (long)((dynamic)lastItem).amount;
            var lastName = (string)((dynamic)lastItem).name;
            var lastDesc = (string)((dynamic)lastItem).description;

            lineItems[^1] = new
            {
                currency = _settings.Currency,
                amount = lastAmount + diff,
                description = lastDesc,
                name = lastName,
                quantity = 1
            };
        }

        // Build success/cancel URLs with invoice ID for tracking
        var baseUrl = GetBaseUrl();
        var successUrl = $"{baseUrl}/payment/success?invoice={invoice.InvoiceId}";
        var cancelUrl = $"{baseUrl}/payment/cancel?invoice={invoice.InvoiceId}";

        var payload = new
        {
            data = new
            {
                attributes = new
                {
                    send_email_receipt = false,
                    show_description = true,
                    show_line_items = true,
                    description,
                    line_items = lineItems,
                    payment_method_types = new[] { "gcash", "card", "grab_pay", "paymaya" },
                    reference_number = invoice.InvoiceNo,
                    success_url = successUrl,
                    cancel_url = cancelUrl
                }
            }
        };

        var json = JsonSerializer.Serialize(payload, new JsonSerializerOptions
        {
            PropertyNamingPolicy = JsonNamingPolicy.SnakeCaseLower
        });
        var content = new StringContent(json, Encoding.UTF8, "application/json");

        HttpResponseMessage response;
        string responseBody;
        try
        {
            response = await _http.PostAsync($"{_settings.BaseUrl}/checkout_sessions", content);
            responseBody = await response.Content.ReadAsStringAsync();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "PayMongo API call failed for Checkout Session creation");
            return ApiResponse<PayMongoPaymentResult>.Fail($"PayMongo API error: {ex.Message}");
        }

        if (!response.IsSuccessStatusCode)
        {
            _logger.LogWarning("PayMongo Checkout Session creation failed: {Status} {Body}", response.StatusCode, responseBody);
            return ApiResponse<PayMongoPaymentResult>.Fail($"PayMongo returned {response.StatusCode}. Check logs for details.");
        }

        // Parse response
        var jsonDoc = JsonNode.Parse(responseBody);
        var sessionId = jsonDoc?["data"]?["id"]?.GetValue<string>() ?? "";
        var checkoutUrl = jsonDoc?["data"]?["attributes"]?["checkout_url"]?.GetValue<string>() ?? "";
        var status = jsonDoc?["data"]?["attributes"]?["status"]?.GetValue<string>() ?? "active";

        // Only create a PayMongoTxn record — NO Payment until webhook confirms
        var txn = new PayMongoTxn
        {
            ShopId = shopId,
            InvoiceId = invoice.InvoiceId,
            InitiatedByUserId = userId,
            Amount = invoice.Balance,
            PayMongoPaymentIntentId = sessionId,
            PayMongoStatus = "active",
            CheckoutUrl = checkoutUrl,
            ResourceType = "checkout_session",
            RawResponse = responseBody,
            CreatedAt = DateTime.UtcNow
        };

        _db.PayMongoTxns.Add(txn);
        await _db.SaveChangesAsync();

        await _audit.LogAsync(shopId, userId, "Create", "PayMongo", txn.PayMongoTxnId,
            $"Created PayMongo Checkout Session for invoice '{invoice.InvoiceNo}'. Amount: ₱{invoice.Balance:N2}. Session ID: {sessionId}.", ClientIp);

        return ApiResponse<PayMongoPaymentResult>.Ok(new PayMongoPaymentResult
        {
            PaymentId = 0,
            PayMongoTxnId = txn.PayMongoTxnId,
            PayMongoResourceId = sessionId,
            ResourceType = "checkout_session",
            CheckoutUrl = checkoutUrl,
            Status = "active",
            InvoiceId = invoice.InvoiceId,
            InvoiceNo = invoice.InvoiceNo,
            Amount = invoice.Balance
        });
    }

    // ═══════════════════════════════════════════════════════════════════
    //  WEBHOOK HANDLER
    // ═══════════════════════════════════════════════════════════════════
    public async Task<bool> HandleWebhookAsync(
        string rawBody, string? svixId, string? svixTimestamp, string? svixSignature)
    {
        _logger.LogInformation("PayMongo webhook received. Svix-Id: {SvixId}", svixId);

        // Validate webhook signature if secret is configured
        if (!string.IsNullOrEmpty(_settings.WebhookSecret))
        {
            if (!ValidateWebhookSignature(rawBody, svixId, svixTimestamp, svixSignature))
            {
                _logger.LogWarning("PayMongo webhook signature validation failed.");
                return false;
            }
        }

        // Parse the webhook event
        JsonNode? eventJson;
        try
        {
            eventJson = JsonNode.Parse(rawBody);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to parse PayMongo webhook body");
            return false;
        }

        var eventType = eventJson?["data"]?["attributes"]?["type"]?.GetValue<string>();
        _logger.LogInformation("PayMongo webhook event type: {EventType}", eventType);

        // Handle payment events
        if (eventType is "link.payment.paid" or "checkout_session.payment.paid"
            or "payment.paid")
        {
            return await HandlePaymentPaidAsync(eventJson!);
        }

        if (eventType is "payment.failed" or "link.payment.failed"
            or "checkout_session.payment.failed")
        {
            return await HandlePaymentFailedAsync(eventJson!);
        }

        _logger.LogInformation("Unhandled PayMongo webhook event type: {EventType}", eventType);
        return true; // Acknowledge but don't process
    }

    private async Task<bool> HandlePaymentPaidAsync(JsonNode eventJson)
    {
        // Extract resource data
        var resourceData = eventJson["data"]?["attributes"]?["data"];
        var resourceId = resourceData?["id"]?.GetValue<string>();
        var resourceType = resourceData?["type"]?.GetValue<string>();

        if (string.IsNullOrEmpty(resourceId))
        {
            _logger.LogWarning("PayMongo webhook: no resource ID found in event");
            return false;
        }

        _logger.LogInformation("PayMongo payment paid. Resource: {Type} {Id}", resourceType, resourceId);

        // Find the PayMongoTxn by resource ID
        var txn = await _db.PayMongoTxns
            .Include(t => t.Invoice)
            .ThenInclude(i => i!.Customer)
            .FirstOrDefaultAsync(t => t.PayMongoPaymentIntentId == resourceId);

        if (txn is null)
        {
            _logger.LogWarning("PayMongo webhook: no transaction found for resource {Id}", resourceId);
            return false;
        }

        // Already processed — don't create duplicate payment
        if (txn.PayMongoStatus == "paid" && txn.PaymentId.HasValue)
        {
            _logger.LogInformation("PayMongo webhook: transaction {Id} already confirmed. Skipping.", txn.PayMongoTxnId);
            return true;
        }

        // Extract the actual payment method from PayMongo webhook data
        var paymentMethodType = resourceData?["attributes"]?["payments"]?[0]?["data"]?["attributes"]?["source"]?["type"]?.GetValue<string>()
            ?? resourceData?["attributes"]?["payment_method_used"]?.GetValue<string>();

        // Map PayMongo method to our PaymentMethod enum
        var method = MapPayMongoMethod(paymentMethodType);

        // Update PayMongoTxn status
        txn.PayMongoStatus = "paid";
        txn.PayMongoPaymentMethod = paymentMethodType;
        txn.RawResponse = eventJson.ToJsonString();
        txn.UpdatedAt = DateTime.UtcNow;

        // NOW create the Payment record (only after confirmation)
        var payment = new Payment
        {
            ShopId = txn.ShopId,
            CustomerId = txn.Invoice!.CustomerId,
            PaymentDate = DateTime.UtcNow,
            Amount = txn.Amount,
            Method = method,
            ReferenceNo = resourceId,
            ReceivedByUserId = txn.InitiatedByUserId,
            Status = PaymentStatus.Confirmed,
            Notes = $"Paid via PayMongo ({paymentMethodType ?? "online"}). Session: {txn.PayMongoPaymentIntentId}"
        };

        payment.PaymentNo = await GeneratePaymentNoAsync(txn.ShopId);

        _db.Payments.Add(payment);
        await _db.SaveChangesAsync();

        // Link the PayMongoTxn → Payment
        txn.PaymentId = payment.PaymentId;

        // Create allocation
        _db.PaymentAllocations.Add(new PaymentAllocation
        {
            PaymentId = payment.PaymentId,
            InvoiceId = txn.InvoiceId,
            AmountApplied = txn.Amount
        });

        await _db.SaveChangesAsync();

        // Recalculate invoice totals, balance & status from payment allocations
        await _billing.RecalculateInvoiceAsync(txn.InvoiceId);

        // Generate double-entry accounting records for the payment
        await _billing.GeneratePaymentEntriesAsync(txn.ShopId, payment.PaymentId);

        // Audit log (no specific user for webhook, use 0)
        await _audit.LogAsync(payment.ShopId, 0, "PayMongoWebhook", "Payment", payment.PaymentId,
            $"PayMongo payment confirmed via webhook. Amount: ₱{payment.Amount:N2}. Method: {paymentMethodType ?? "unknown"}. Resource: {resourceId}.", null);

        // Notify shop admins about PayMongo payment confirmation
        try
        {
            var adminUsers = await _db.Users
                .Where(u => u.ShopId == txn.ShopId && u.IsActive
                    && u.UserRoles.Any(ur => ur.Role!.RoleName == "Admin"))
                .Select(u => u.UserId)
                .ToListAsync();
            foreach (var adminId in adminUsers)
            {
                await _notif.CreateAsync(adminId, txn.ShopId,
                    "Online Payment Confirmed",
                    $"PayMongo payment of ₱{payment.Amount:N2} for Invoice {txn.Invoice?.InvoiceNo} has been confirmed via {paymentMethodType ?? "online"}.",
                    "success",
                    $"/Admin/Payments/DetailsModal/{payment.PaymentId}");
            }
            // Also notify the user who initiated the payment
            if (txn.InitiatedByUserId > 0)
            {
                await _notif.CreateAsync(txn.InitiatedByUserId, txn.ShopId,
                    "Payment Confirmed",
                    $"Your online payment of ₱{payment.Amount:N2} has been confirmed.",
                    "success", null);
            }
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Failed to send payment confirmation notification");
        }

        // Fire-and-forget: send payment receipt email to customer
        var paymentIdForEmail = payment.PaymentId;
        _ = Task.Run(async () =>
        {
            try
            {
                using var scope = _scopeFactory.CreateScope();
                await scope.ServiceProvider.GetRequiredService<IEmailService>()
                    .SendReceiptAsync(paymentIdForEmail);
            }
            catch (Exception ex)
            {
                Console.WriteLine($"[EMAIL ERROR] Receipt email failed for PaymentId {paymentIdForEmail}: {ex.Message}");
            }
        });

        _logger.LogInformation("PayMongo payment confirmed. PaymentId: {PaymentId}, Amount: {Amount}, Method: {Method}",
            payment.PaymentId, payment.Amount, paymentMethodType);

        return true;
    }

    /// <summary>Map PayMongo payment method string to our PaymentMethod enum.</summary>
    private static PaymentMethod MapPayMongoMethod(string? payMongoMethod)
    {
        return payMongoMethod?.ToLowerInvariant() switch
        {
            "gcash" => PaymentMethod.GCash,
            "card" => PaymentMethod.Card,
            "grab_pay" => PaymentMethod.GCash,  // categorize as GCash (e-wallet)
            "paymaya" => PaymentMethod.GCash,    // categorize as GCash (e-wallet)
            _ => PaymentMethod.Card               // default to Card for online payments
        };
    }

    private async Task<bool> HandlePaymentFailedAsync(JsonNode eventJson)
    {
        var resourceData = eventJson["data"]?["attributes"]?["data"];
        var resourceId = resourceData?["id"]?.GetValue<string>();

        if (string.IsNullOrEmpty(resourceId))
            return false;

        var txn = await _db.PayMongoTxns
            .FirstOrDefaultAsync(t => t.PayMongoPaymentIntentId == resourceId);

        if (txn is null) return false;

        txn.PayMongoStatus = "failed";
        txn.RawResponse = eventJson.ToJsonString();
        txn.UpdatedAt = DateTime.UtcNow;

        await _db.SaveChangesAsync();

        _logger.LogWarning("PayMongo payment failed. Resource: {Id}", resourceId);
        return true;
    }

    // ═══════════════════════════════════════════════════════════════════
    //  STATUS CHECK
    // ═══════════════════════════════════════════════════════════════════
    public async Task<PayMongoStatusDto?> GetStatusAsync(long shopId, long payMongoTxnId)
    {
        return await _db.PayMongoTxns
            .Where(t => t.PayMongoTxnId == payMongoTxnId && t.ShopId == shopId)
            .Select(t => new PayMongoStatusDto
            {
                PayMongoTxnId = t.PayMongoTxnId,
                PaymentId = t.PaymentId ?? 0,
                InvoiceId = t.InvoiceId,
                InvoiceNo = t.Invoice!.InvoiceNo,
                PayMongoStatus = t.PayMongoStatus,
                PaymentStatus = t.PaymentId.HasValue ? t.Payment!.Status.ToString() : t.PayMongoStatus,
                CheckoutUrl = t.CheckoutUrl,
                Amount = t.Amount,
                CreatedAt = t.CreatedAt
            })
            .FirstOrDefaultAsync();
    }

    public async Task<List<PayMongoStatusDto>> GetByInvoiceAsync(long shopId, long invoiceId)
    {
        return await _db.PayMongoTxns
            .Where(t => t.ShopId == shopId && t.InvoiceId == invoiceId)
            .OrderByDescending(t => t.CreatedAt)
            .Select(t => new PayMongoStatusDto
            {
                PayMongoTxnId = t.PayMongoTxnId,
                PaymentId = t.PaymentId ?? 0,
                InvoiceId = invoiceId,
                InvoiceNo = t.Invoice!.InvoiceNo,
                PayMongoStatus = t.PayMongoStatus,
                PaymentStatus = t.PaymentId.HasValue ? t.Payment!.Status.ToString() : t.PayMongoStatus,
                CheckoutUrl = t.CheckoutUrl,
                Amount = t.Amount,
                CreatedAt = t.CreatedAt
            })
            .ToListAsync();
    }

    // ═══════════════════════════════════════════════════════════════════
    //  VERIFY & RECORD (fallback for localhost / missing webhooks)
    // ═══════════════════════════════════════════════════════════════════
    public async Task<bool> VerifyAndRecordPaymentAsync(long invoiceId)
    {
        // Find the latest active/pending PayMongoTxn for this invoice
        var txn = await _db.PayMongoTxns
            .Include(t => t.Invoice)
                .ThenInclude(i => i!.Customer)
            .Where(t => t.InvoiceId == invoiceId
                     && t.PayMongoStatus != "paid"
                     && t.PayMongoStatus != "failed"
                     && !t.PaymentId.HasValue)
            .OrderByDescending(t => t.CreatedAt)
            .FirstOrDefaultAsync();

        if (txn is null)
        {
            _logger.LogInformation("VerifyAndRecord: No pending PayMongoTxn for invoice {InvoiceId}", invoiceId);
            // Check if already paid (idempotent)
            var alreadyPaid = await _db.PayMongoTxns
                .AnyAsync(t => t.InvoiceId == invoiceId && t.PayMongoStatus == "paid" && t.PaymentId.HasValue);
            return alreadyPaid;
        }

        // Call PayMongo API to get the checkout session / link status
        string endpoint = txn.ResourceType == "link"
            ? $"{_settings.BaseUrl}/links/{txn.PayMongoPaymentIntentId}"
            : $"{_settings.BaseUrl}/checkout_sessions/{txn.PayMongoPaymentIntentId}";

        HttpResponseMessage response;
        string responseBody;
        try
        {
            response = await _http.GetAsync(endpoint);
            responseBody = await response.Content.ReadAsStringAsync();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "VerifyAndRecord: PayMongo API call failed for {ResourceType} {Id}",
                txn.ResourceType, txn.PayMongoPaymentIntentId);
            return false;
        }

        if (!response.IsSuccessStatusCode)
        {
            _logger.LogWarning("VerifyAndRecord: PayMongo returned {Status} for {Id}", response.StatusCode, txn.PayMongoPaymentIntentId);
            return false;
        }

        var jsonDoc = JsonNode.Parse(responseBody);
        var attributes = jsonDoc?["data"]?["attributes"];

        // Check for payments in the response
        var payments = attributes?["payments"];
        var paymentsArray = payments as JsonArray;

        if (paymentsArray is null || paymentsArray.Count == 0)
        {
            _logger.LogInformation("VerifyAndRecord: No payments found yet for {Id}", txn.PayMongoPaymentIntentId);
            return false;
        }

        // Extract payment details from the first payment
        var paymentNode = paymentsArray[0];
        var paymentAttrs = paymentNode?["data"]?["attributes"]
                        ?? paymentNode?["attributes"];
        var paymentStatus = paymentAttrs?["status"]?.GetValue<string>();

        if (paymentStatus != "paid")
        {
            _logger.LogInformation("VerifyAndRecord: Payment status is '{Status}' (not paid yet)", paymentStatus);
            return false;
        }

        // Extract actual payment method
        var paymentMethodType = paymentAttrs?["source"]?["type"]?.GetValue<string>()
            ?? paymentAttrs?["payment_method_used"]?.GetValue<string>();

        var method = MapPayMongoMethod(paymentMethodType);

        // Update PayMongoTxn status
        txn.PayMongoStatus = "paid";
        txn.PayMongoPaymentMethod = paymentMethodType;
        txn.RawResponse = responseBody;
        txn.UpdatedAt = DateTime.UtcNow;

        // Create the Payment record
        var payment = new Payment
        {
            ShopId = txn.ShopId,
            CustomerId = txn.Invoice!.CustomerId,
            PaymentDate = DateTime.UtcNow,
            Amount = txn.Amount,
            Method = method,
            ReferenceNo = txn.PayMongoPaymentIntentId,
            ReceivedByUserId = txn.InitiatedByUserId,
            Status = PaymentStatus.Confirmed,
            Notes = $"Paid via PayMongo ({paymentMethodType ?? "online"}). Verified on success redirect."
        };

        payment.PaymentNo = await GeneratePaymentNoAsync(txn.ShopId);

        _db.Payments.Add(payment);
        await _db.SaveChangesAsync();

        // Link txn → payment
        txn.PaymentId = payment.PaymentId;

        // Create allocation
        _db.PaymentAllocations.Add(new PaymentAllocation
        {
            PaymentId = payment.PaymentId,
            InvoiceId = txn.InvoiceId,
            AmountApplied = txn.Amount
        });

        await _db.SaveChangesAsync();

        // Recalculate invoice totals, balance & status from payment allocations
        await _billing.RecalculateInvoiceAsync(txn.InvoiceId);

        // Generate double-entry accounting records for the payment
        await _billing.GeneratePaymentEntriesAsync(txn.ShopId, payment.PaymentId);

        await _audit.LogAsync(payment.ShopId, txn.InitiatedByUserId, "PayMongoVerify", "Payment", payment.PaymentId,
            $"PayMongo payment verified on redirect. Amount: ₱{payment.Amount:N2}. Method: {paymentMethodType ?? "unknown"}.", ClientIp);

        // Notify shop admins about verified payment
        try
        {
            var adminUsers = await _db.Users
                .Where(u => u.ShopId == txn.ShopId && u.IsActive
                    && u.UserRoles.Any(ur => ur.Role!.RoleName == "Admin"))
                .Select(u => u.UserId)
                .ToListAsync();
            foreach (var adminId in adminUsers)
            {
                await _notif.CreateAsync(adminId, txn.ShopId,
                    "Online Payment Verified",
                    $"PayMongo payment of ₱{payment.Amount:N2} for Invoice {txn.Invoice?.InvoiceNo} verified.",
                    "success",
                    $"/Admin/Payments/DetailsModal/{payment.PaymentId}");
            }
            if (txn.InitiatedByUserId > 0)
            {
                await _notif.CreateAsync(txn.InitiatedByUserId, txn.ShopId,
                    "Payment Confirmed",
                    $"Your online payment of ₱{payment.Amount:N2} has been confirmed.",
                    "success", null);
            }
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Failed to send payment verification notification");
        }

        // Fire-and-forget: send payment receipt email to customer
        var paymentIdForReceipt = payment.PaymentId;
        _ = Task.Run(async () =>
        {
            try
            {
                using var scope = _scopeFactory.CreateScope();
                await scope.ServiceProvider.GetRequiredService<IEmailService>()
                    .SendReceiptAsync(paymentIdForReceipt);
            }
            catch (Exception ex)
            {
                Console.WriteLine($"[EMAIL ERROR] Receipt email failed for PaymentId {paymentIdForReceipt}: {ex.Message}");
            }
        });

        _logger.LogInformation("VerifyAndRecord: Payment recorded. PaymentId: {PaymentId}, Amount: {Amount}",
            payment.PaymentId, payment.Amount);

        return true;
    }

    // ═══════════════════════════════════════════════════════════════════
    //  PRIVATE HELPERS
    // ═══════════════════════════════════════════════════════════════════

    /// <summary>
    /// Validate PayMongo webhook signature using HMAC-SHA256.
    /// PayMongo uses Svix for webhook delivery.
    /// </summary>
    private bool ValidateWebhookSignature(string rawBody, string? svixId, string? svixTimestamp, string? svixSignature)
    {
        if (string.IsNullOrEmpty(svixId) || string.IsNullOrEmpty(svixTimestamp) || string.IsNullOrEmpty(svixSignature))
            return false;

        try
        {
            var secret = _settings.WebhookSecret;
            // Svix secret may be prefixed with "whsec_"
            if (secret.StartsWith("whsec_"))
                secret = secret["whsec_".Length..];

            var secretBytes = Convert.FromBase64String(secret);
            var signedContent = $"{svixId}.{svixTimestamp}.{rawBody}";
            using var hmac = new HMACSHA256(secretBytes);
            var hash = hmac.ComputeHash(Encoding.UTF8.GetBytes(signedContent));
            var expectedSignature = Convert.ToBase64String(hash);

            // svixSignature can contain multiple signatures separated by space
            var signatures = svixSignature.Split(' ');
            foreach (var sig in signatures)
            {
                var parts = sig.Split(',');
                if (parts.Length == 2)
                {
                    var sigValue = parts[1];
                    if (CryptographicOperations.FixedTimeEquals(
                        Encoding.UTF8.GetBytes(expectedSignature),
                        Encoding.UTF8.GetBytes(sigValue)))
                    {
                        return true;
                    }
                }
            }

            return false;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error validating PayMongo webhook signature");
            return false;
        }
    }

    private string GetBaseUrl()
    {
        var request = _httpCtx.HttpContext?.Request;
        if (request != null)
        {
            return $"{request.Scheme}://{request.Host}";
        }
        // Fallback from settings
        return !string.IsNullOrEmpty(_settings.SuccessUrl)
            ? new Uri(_settings.SuccessUrl).GetLeftPart(UriPartial.Authority)
            : "https://localhost:7001";
    }

    private async Task<string> GeneratePaymentNoAsync(long shopId)
    {
        var year = DateTime.UtcNow.Year;
        var prefix = $"PAY-{year}-";

        var lastNo = await _db.Payments
            .Where(p => p.ShopId == shopId && p.PaymentNo.StartsWith(prefix))
            .OrderByDescending(p => p.PaymentNo)
            .Select(p => p.PaymentNo)
            .FirstOrDefaultAsync();

        int next = 1;
        if (lastNo is not null)
        {
            var numPart = lastNo.Replace(prefix, "");
            if (int.TryParse(numPart, out var parsed))
                next = parsed + 1;
        }

        return $"{prefix}{next:D4}";
    }

    // ═══════════════════════════════════════════════════════════════════
    //  TEST CONNECTION
    // ═══════════════════════════════════════════════════════════════════
    public async Task<(bool Success, string Message)> TestConnectionAsync()
    {
        if (string.IsNullOrWhiteSpace(_settings.SecretKey))
            return (false, "PayMongo Secret Key is not configured.");

        try
        {
            var response = await _http.GetAsync($"{_settings.BaseUrl}/links?limit=1");

            if (response.IsSuccessStatusCode)
                return (true, "PayMongo connection successful! API keys are valid.");

            var body = await response.Content.ReadAsStringAsync();
            return (false, $"PayMongo returned {(int)response.StatusCode}: {body}");
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "PayMongo test connection failed");
            return (false, $"Connection failed: {ex.Message}");
        }
    }

    // ═══════════════════════════════════════════════════════════════════
    //  REFUND PAYMENT
    // ═══════════════════════════════════════════════════════════════════
    public async Task<(bool Success, string Message)> RefundPaymentAsync(
        long shopId, long invoiceId, decimal amount, string reason)
    {
        // Find the confirmed PayMongo transaction(s) for this invoice
        var paidTxns = await _db.PayMongoTxns
            .Include(t => t.Invoice)
            .Where(t => t.ShopId == shopId
                     && t.InvoiceId == invoiceId
                     && t.PayMongoStatus == "paid"
                     && t.PaymentId.HasValue)
            .OrderByDescending(t => t.CreatedAt)
            .ToListAsync();

        if (!paidTxns.Any())
            return (false, "No confirmed PayMongo payment found for this invoice.");

        // Refund the most recent paid transaction first
        var txn = paidTxns.First();
        var refundAmountCentavos = (long)(amount * 100);

        // First, we need the PayMongo payment ID from the transaction.
        // For checkout sessions, we retrieve the payment ID from the session.
        string? paymongoPaymentId = null;

        try
        {
            string endpoint = txn.ResourceType == "link"
                ? $"{_settings.BaseUrl}/links/{txn.PayMongoPaymentIntentId}"
                : $"{_settings.BaseUrl}/checkout_sessions/{txn.PayMongoPaymentIntentId}";

            var getResponse = await _http.GetAsync(endpoint);
            if (getResponse.IsSuccessStatusCode)
            {
                var getBody = await getResponse.Content.ReadAsStringAsync();
                var jsonDoc = JsonNode.Parse(getBody);
                var payments = jsonDoc?["data"]?["attributes"]?["payments"] as JsonArray;
                if (payments is { Count: > 0 })
                {
                    paymongoPaymentId = payments[0]?["data"]?["id"]?.GetValue<string>()
                                     ?? payments[0]?["id"]?.GetValue<string>();
                }
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to retrieve PayMongo payment ID for refund");
            return (false, $"Failed to retrieve payment details: {ex.Message}");
        }

        if (string.IsNullOrEmpty(paymongoPaymentId))
            return (false, "Could not find the PayMongo payment ID to refund.");

        // Create refund via PayMongo API
        var payload = new
        {
            data = new
            {
                attributes = new
                {
                    amount = refundAmountCentavos,
                    payment_id = paymongoPaymentId,
                    reason = "others",
                    notes = reason
                }
            }
        };

        var json = JsonSerializer.Serialize(payload, new JsonSerializerOptions { PropertyNamingPolicy = JsonNamingPolicy.SnakeCaseLower });
        var content = new StringContent(json, Encoding.UTF8, "application/json");

        HttpResponseMessage response;
        string responseBody;
        try
        {
            response = await _http.PostAsync($"{_settings.BaseUrl}/refunds", content);
            responseBody = await response.Content.ReadAsStringAsync();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "PayMongo refund API call failed");
            return (false, $"PayMongo refund API error: {ex.Message}");
        }

        if (!response.IsSuccessStatusCode)
        {
            _logger.LogWarning("PayMongo refund failed: {Status} {Body}", response.StatusCode, responseBody);
            return (false, $"PayMongo refund returned {(int)response.StatusCode}. The refund was applied internally but could not be processed on PayMongo.");
        }

        // Update local PayMongoTxn status
        txn.PayMongoStatus = "refunded";
        txn.UpdatedAt = DateTime.UtcNow;
        txn.RawResponse = responseBody;
        await _db.SaveChangesAsync();

        var refundDocId = JsonNode.Parse(responseBody)?["data"]?["id"]?.GetValue<string>() ?? "unknown";

        await _audit.LogAsync(shopId, 0, "Refund", "PayMongo", txn.PayMongoTxnId,
            $"PayMongo refund issued. Amount: ₱{amount:N2}. Reason: {reason}. Refund ID: {refundDocId}.", null);

        _logger.LogInformation("PayMongo refund issued. TxnId: {TxnId}, Amount: {Amount}, RefundId: {RefundId}",
            txn.PayMongoTxnId, amount, refundDocId);

        return (true, $"PayMongo refund of ₱{amount:N2} issued successfully. Refund ID: {refundDocId}");
    }

    // ═══════════════════════════════════════════════════════════════════
    //  EXPIRE PENDING SESSIONS
    // ═══════════════════════════════════════════════════════════════════
    public async Task<int> ExpirePendingSessionsAsync(long shopId, long invoiceId)
    {
        var pendingTxns = await _db.PayMongoTxns
            .Where(t => t.ShopId == shopId
                     && t.InvoiceId == invoiceId
                     && (t.PayMongoStatus == "pending" || t.PayMongoStatus == "active")
                     && !t.PaymentId.HasValue)
            .ToListAsync();

        if (!pendingTxns.Any()) return 0;

        int expired = 0;
        foreach (var txn in pendingTxns)
        {
            try
            {
                if (txn.ResourceType == "checkout_session")
                {
                    // Expire checkout session via PayMongo API
                    var response = await _http.PostAsync(
                        $"{_settings.BaseUrl}/checkout_sessions/{txn.PayMongoPaymentIntentId}/expire",
                        new StringContent("{}", Encoding.UTF8, "application/json"));

                    if (response.IsSuccessStatusCode)
                    {
                        txn.PayMongoStatus = "expired";
                        txn.UpdatedAt = DateTime.UtcNow;
                        expired++;
                        _logger.LogInformation("Expired PayMongo checkout session {Id} for invoice {InvoiceId}",
                            txn.PayMongoPaymentIntentId, invoiceId);
                    }
                    else
                    {
                        _logger.LogWarning("Failed to expire checkout session {Id}: {Status}",
                            txn.PayMongoPaymentIntentId, response.StatusCode);
                        // Still mark locally as expired to prevent reuse
                        txn.PayMongoStatus = "expired";
                        txn.UpdatedAt = DateTime.UtcNow;
                        expired++;
                    }
                }
                else
                {
                    // Payment links can't be expired via API — archive locally via status
                    txn.PayMongoStatus = "expired";
                    txn.UpdatedAt = DateTime.UtcNow;
                    expired++;
                    _logger.LogInformation("Marked PayMongo link {Id} as expired for invoice {InvoiceId}",
                        txn.PayMongoPaymentIntentId, invoiceId);
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error expiring PayMongo session {Id}", txn.PayMongoPaymentIntentId);
                // Still mark locally as expired
                txn.PayMongoStatus = "expired";
                txn.UpdatedAt = DateTime.UtcNow;
                expired++;
            }
        }

        if (expired > 0)
        {
            await _db.SaveChangesAsync();
            await _audit.LogAsync(shopId, 0, "ExpireSessions", "PayMongo", 0,
                $"Expired {expired} pending PayMongo session(s) for invoice ID {invoiceId} due to balance change.", null);
        }

        return expired;
    }
}
