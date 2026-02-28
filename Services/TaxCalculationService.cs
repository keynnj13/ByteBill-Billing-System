using ByteBill_BS.Data;
using ByteBill_BS.Models;
using ByteBill_BS.Models.Enums;
using Microsoft.EntityFrameworkCore;

namespace ByteBill_BS.Services;

// ─── Tax breakdown result ───────────────────────────────────────────────
public class TaxBreakdown
{
    public decimal Subtotal { get; set; }
    public decimal DiscountAmount { get; set; }
    public decimal VatableSales { get; set; }
    public decimal VatExemptSales { get; set; }
    public decimal ZeroRatedSales { get; set; }
    public decimal VatAmount { get; set; }

    /// <summary>Final amount the customer pays (Subtotal − Discount).</summary>
    public decimal TotalAmount { get; set; }
}

// ─── Interface ──────────────────────────────────────────────────────────
public interface ITaxCalculationService
{
    /// <summary>
    /// Compute BIR-compliant VAT breakdown for an invoice (VAT-inclusive pricing).
    /// Call this after invoice lines + discounts are set, then persist the result.
    /// </summary>
    Task<TaxBreakdown> ComputeTaxAsync(long invoiceId);

    /// <summary>Apply a discount to an invoice, recalculate, and persist.</summary>
    Task<InvoiceDiscount> ApplyDiscountAsync(long invoiceId, long userId, ApplyDiscountRequest req);

    /// <summary>Remove a discount from an invoice, recalculate, and persist.</summary>
    Task<bool> RemoveDiscountAsync(long invoiceDiscountId, long invoiceId);
}

// ─── DTO ────────────────────────────────────────────────────────────────
public class ApplyDiscountRequest
{
    public DiscountType DiscountType { get; set; }

    /// <summary>Custom label (required for Promo). Auto-set for SC/PWD.</summary>
    public string? Label { get; set; }

    /// <summary>Discount percentage (0–100). Ignored for SC/PWD (always 20%).</summary>
    public decimal? Percentage { get; set; }

    /// <summary>Fixed discount amount — used only if Percentage is null/0 for Promo.</summary>
    public decimal? FixedAmount { get; set; }

    /// <summary>SC/PWD ID number for BIR compliance.</summary>
    public string? BeneficiaryIdNo { get; set; }

    /// <summary>SC/PWD beneficiary name.</summary>
    public string? BeneficiaryName { get; set; }
}

// ─── Implementation ─────────────────────────────────────────────────────
public class TaxCalculationService : ITaxCalculationService
{
    private readonly ApplicationDbContext _db;
    private readonly IBillingCalculationService _billing;

    public TaxCalculationService(ApplicationDbContext db, IBillingCalculationService billing)
    {
        _db = db;
        _billing = billing;
    }

    // ═══════════════════════════════════════════════════════════════════
    // Compute Tax (BIR VAT-inclusive)
    // ═══════════════════════════════════════════════════════════════════

    public async Task<TaxBreakdown> ComputeTaxAsync(long invoiceId)
    {
        var invoice = await _db.Invoices
            .Include(i => i.InvoiceLines)
            .Include(i => i.InvoiceDiscounts)
            .Include(i => i.Shop)
            .FirstOrDefaultAsync(i => i.InvoiceId == invoiceId);

        if (invoice is null)
            throw new InvalidOperationException($"Invoice {invoiceId} not found.");

        var shop = invoice.Shop
            ?? await _db.Shops.FindAsync(invoice.ShopId)
            ?? throw new InvalidOperationException($"Shop {invoice.ShopId} not found.");

        var subtotal = invoice.InvoiceLines.Sum(l => l.Qty * l.UnitPrice);
        var totalDiscount = invoice.InvoiceDiscounts.Sum(d => d.Amount);
        var hasVatExemptDiscount = invoice.InvoiceDiscounts.Any(d => d.IsVatExempt);

        // Net amount after discounts
        var netAmount = subtotal - totalDiscount;
        if (netAmount < 0) netAmount = 0;

        decimal vatableSales = 0, vatExemptSales = 0, zeroRatedSales = 0, vatAmount = 0;

        if (shop.IsVatRegistered)
        {
            // VAT rate (default 12%)
            var vatRate = shop.TaxRate > 0 ? shop.TaxRate : 12m;

            if (hasVatExemptDiscount)
            {
                // SC/PWD: The discounted amount is VAT-exempt
                // The discount itself removes VAT from the computation
                var exemptPortion = totalDiscount; // SC/PWD discount portion is exempt
                vatExemptSales = netAmount; // Entire remaining amount treated as VAT-exempt per BIR
                vatableSales = 0;
                vatAmount = 0;
            }
            else
            {
                // Standard VAT-inclusive: Total ÷ 1.12
                vatableSales = Math.Round(netAmount / (1m + vatRate / 100m), 2);
                vatAmount = netAmount - vatableSales;
                vatExemptSales = 0;
            }
        }
        else
        {
            // Non-VAT registered: 3% Percentage Tax (not shown as line item, just for BIR filing)
            // All sales are non-vatable
            vatableSales = 0;
            vatExemptSales = netAmount;
            vatAmount = 0;
        }

        var result = new TaxBreakdown
        {
            Subtotal = subtotal,
            DiscountAmount = totalDiscount,
            VatableSales = vatableSales,
            VatExemptSales = vatExemptSales,
            ZeroRatedSales = zeroRatedSales,
            VatAmount = vatAmount,
            TotalAmount = netAmount
        };

        // Persist the breakdown to the invoice (preserve TotalAdjustments from billing engine)
        invoice.Subtotal = subtotal;
        invoice.DiscountAmount = totalDiscount;
        invoice.VatableSales = vatableSales;
        invoice.VatExemptSales = vatExemptSales;
        invoice.ZeroRatedSales = zeroRatedSales;
        invoice.VatAmount = vatAmount;
        invoice.TotalAmount = netAmount + invoice.TotalAdjustments;
        invoice.Balance = Math.Max(0, invoice.TotalAmount - invoice.AmountPaid);

        if (invoice.Status == InvoiceStatus.Void)
        {
            // Don't change void status
        }
        else if (invoice.Balance <= 0 && invoice.TotalAmount > 0)
        {
            invoice.Balance = 0;
            invoice.Status = InvoiceStatus.Paid;
        }
        else if (invoice.AmountPaid > 0)
            invoice.Status = InvoiceStatus.Partial;
        else
            invoice.Status = InvoiceStatus.Unpaid;

        await _db.SaveChangesAsync();

        return result;
    }

    // ═══════════════════════════════════════════════════════════════════
    // Apply Discount
    // ═══════════════════════════════════════════════════════════════════

    public async Task<InvoiceDiscount> ApplyDiscountAsync(long invoiceId, long userId, ApplyDiscountRequest req)
    {
        var invoice = await _db.Invoices
            .Include(i => i.InvoiceLines)
            .Include(i => i.InvoiceDiscounts)
            .FirstOrDefaultAsync(i => i.InvoiceId == invoiceId)
            ?? throw new InvalidOperationException("Invoice not found.");

        var subtotal = invoice.InvoiceLines.Sum(l => l.Qty * l.UnitPrice);
        var existingDiscounts = invoice.InvoiceDiscounts.Sum(d => d.Amount);

        decimal discountPct;
        decimal discountAmt;
        string label;
        bool isVatExempt;

        switch (req.DiscountType)
        {
            case DiscountType.SeniorCitizen:
                discountPct = 20m;
                discountAmt = Math.Round(subtotal * 0.20m, 2);
                label = "Senior Citizen (20%)";
                isVatExempt = true;
                break;

            case DiscountType.PWD:
                discountPct = 20m;
                discountAmt = Math.Round(subtotal * 0.20m, 2);
                label = "PWD (20%)";
                isVatExempt = true;
                break;

            case DiscountType.Promo:
                label = req.Label ?? "Promo Discount";
                isVatExempt = false;

                if (req.Percentage.HasValue && req.Percentage.Value > 0)
                {
                    discountPct = req.Percentage.Value;
                    discountAmt = Math.Round(subtotal * discountPct / 100m, 2);
                }
                else if (req.FixedAmount.HasValue && req.FixedAmount.Value > 0)
                {
                    discountAmt = req.FixedAmount.Value;
                    discountPct = subtotal > 0 ? Math.Round(discountAmt / subtotal * 100m, 2) : 0;
                }
                else
                    throw new InvalidOperationException("Promo discount requires either a percentage or fixed amount.");
                break;

            default:
                throw new InvalidOperationException("Invalid discount type.");
        }

        // Prevent over-discounting
        if (existingDiscounts + discountAmt > subtotal)
            throw new InvalidOperationException("Total discounts cannot exceed the invoice subtotal.");

        var discount = new InvoiceDiscount
        {
            InvoiceId = invoiceId,
            DiscountType = req.DiscountType,
            Label = label,
            Percentage = discountPct,
            Amount = discountAmt,
            IsVatExempt = isVatExempt,
            BeneficiaryIdNo = req.BeneficiaryIdNo?.Trim(),
            BeneficiaryName = req.BeneficiaryName?.Trim(),
            AppliedByUserId = userId,
            AppliedAt = DateTime.UtcNow
        };

        _db.Set<InvoiceDiscount>().Add(discount);
        await _db.SaveChangesAsync();

        // Recalculate tax breakdown
        await ComputeTaxAsync(invoiceId);

        return discount;
    }

    // ═══════════════════════════════════════════════════════════════════
    // Remove Discount
    // ═══════════════════════════════════════════════════════════════════

    public async Task<bool> RemoveDiscountAsync(long invoiceDiscountId, long invoiceId)
    {
        var discount = await _db.Set<InvoiceDiscount>()
            .FirstOrDefaultAsync(d => d.InvoiceDiscountId == invoiceDiscountId && d.InvoiceId == invoiceId);

        if (discount is null) return false;

        _db.Set<InvoiceDiscount>().Remove(discount);
        await _db.SaveChangesAsync();

        // Recalculate tax breakdown
        await ComputeTaxAsync(invoiceId);

        return true;
    }
}
