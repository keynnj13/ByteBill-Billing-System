namespace ByteBill_BS.Models;

public class PayMongoTxn
{
    public long PayMongoTxnId { get; set; }
    public long? PaymentId { get; set; }         // nullable — set only after webhook confirms payment
    public long ShopId { get; set; }              // shop context for the transaction
    public long InvoiceId { get; set; }           // the invoice being paid
    public long InitiatedByUserId { get; set; }   // the user who initiated the checkout
    public decimal Amount { get; set; }           // amount in PHP
    public string PayMongoPaymentIntentId { get; set; } = string.Empty;
    public string PayMongoStatus { get; set; } = string.Empty;
    public string? PayMongoPaymentMethod { get; set; } // actual method from PayMongo (card, gcash, etc.)
    public string? CheckoutUrl { get; set; }
    public string ResourceType { get; set; } = string.Empty; // "link" or "checkout_session"
    public string? RawResponse { get; set; }
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public DateTime? UpdatedAt { get; set; }

    // Navigation properties
    public Payment? Payment { get; set; }
    public Invoice? Invoice { get; set; }
    public Shop? Shop { get; set; }
}
