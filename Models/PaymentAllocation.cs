namespace ByteBill_BS.Models;

public class PaymentAllocation
{
    public long PaymentAllocationId { get; set; }
    public long PaymentId { get; set; }
    public long InvoiceId { get; set; }
    public decimal AmountApplied { get; set; }

    // Navigation properties
    public Payment? Payment { get; set; }
    public Invoice? Invoice { get; set; }
}
