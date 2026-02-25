using System.ComponentModel.DataAnnotations;

namespace ByteBill_BS.DTOs.PayMongo;

// ── Create Payment Link Request ─────────────────────────────────────────
public class CreatePayMongoLinkRequest
{
    [Required]
    public long InvoiceId { get; set; }

    /// <summary>Optional description override. Defaults to invoice number.</summary>
    [MaxLength(200)]
    public string? Description { get; set; }
}

// ── Create Checkout Session Request ─────────────────────────────────────
public class CreateCheckoutSessionRequest
{
    [Required]
    public long InvoiceId { get; set; }

    /// <summary>Optional description override.</summary>
    [MaxLength(200)]
    public string? Description { get; set; }
}

// ── PayMongo creation response ──────────────────────────────────────────
public class PayMongoPaymentResult
{
    public long PaymentId { get; set; }
    public long PayMongoTxnId { get; set; }
    public string PayMongoResourceId { get; set; } = string.Empty;
    public string ResourceType { get; set; } = string.Empty;
    public string CheckoutUrl { get; set; } = string.Empty;
    public string Status { get; set; } = string.Empty;
    public long InvoiceId { get; set; }
    public string InvoiceNo { get; set; } = string.Empty;
    public decimal Amount { get; set; }
}

// ── Webhook event DTO ───────────────────────────────────────────────────
public class PayMongoWebhookEvent
{
    public PayMongoWebhookData? Data { get; set; }
}

public class PayMongoWebhookData
{
    public string? Id { get; set; }
    public string? Type { get; set; }
    public PayMongoWebhookAttributes? Attributes { get; set; }
}

public class PayMongoWebhookAttributes
{
    public string? Type { get; set; }
    public bool Livemode { get; set; }
    public PayMongoWebhookResourceData? Data { get; set; }
}

public class PayMongoWebhookResourceData
{
    public string? Id { get; set; }
    public string? Type { get; set; }
    public PayMongoResourceAttributes? Attributes { get; set; }
}

public class PayMongoResourceAttributes
{
    public long Amount { get; set; }
    public string? Currency { get; set; }
    public string? Description { get; set; }
    public string? Status { get; set; }
    public string? Reference_number { get; set; }
    public string? Checkout_url { get; set; }
    public PayMongoPaymentsList? Payments { get; set; }
}

public class PayMongoPaymentsList
{
    public List<PayMongoPaymentData>? Data { get; set; }
}

public class PayMongoPaymentData
{
    public string? Id { get; set; }
    public string? Type { get; set; }
    public PayMongoPaymentAttributes? Attributes { get; set; }
}

public class PayMongoPaymentAttributes
{
    public long Amount { get; set; }
    public string? Currency { get; set; }
    public string? Status { get; set; }
    public string? Fee { get; set; }
    public string? Net_amount { get; set; }
    public string? Description { get; set; }
    public PayMongoPaymentSource? Source { get; set; }
}

public class PayMongoPaymentSource
{
    public string? Id { get; set; }
    public string? Type { get; set; }
}

// ── Status check ────────────────────────────────────────────────────────
public class PayMongoStatusDto
{
    public long PayMongoTxnId { get; set; }
    public long PaymentId { get; set; }
    public long InvoiceId { get; set; }
    public string InvoiceNo { get; set; } = string.Empty;
    public string PayMongoStatus { get; set; } = string.Empty;
    public string PaymentStatus { get; set; } = string.Empty;
    public string? CheckoutUrl { get; set; }
    public decimal Amount { get; set; }
    public DateTime CreatedAt { get; set; }
}
