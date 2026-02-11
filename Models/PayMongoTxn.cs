namespace ByteBill_BS.Models;

public class PayMongoTxn
{
    public long PayMongoTxnId { get; set; }
    public long PaymentId { get; set; }
    public string PayMongoPaymentIntentId { get; set; } = string.Empty;
    public string PayMongoStatus { get; set; } = string.Empty;
    public string? RawResponse { get; set; }
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

    // Navigation properties
    public Payment? Payment { get; set; }
}
