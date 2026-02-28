using ByteBill_BS.Data;
using ByteBill_BS.Models;
using ByteBill_BS.Models.Enums;
using Microsoft.EntityFrameworkCore;

namespace ByteBill_BS.Services;

// ─── Interface ───────────────────────────────────────────────────────

public interface IBillingCalculationService
{
    // ── Price Resolution ─────────────────────────────────────────────
    /// <summary>Resolves the selling price for a service from ServiceCatalog.BasePrice.</summary>
    Task<decimal> ResolveServicePriceAsync(long serviceId);

    /// <summary>Resolves the selling price for an inventory part, applying shop markup if configured.</summary>
    Task<decimal> ResolvePartPriceAsync(long shopId, long itemId);

    /// <summary>Gets the shop's default part markup percentage (0 = no markup).</summary>
    Task<decimal> GetShopMarkupPctAsync(long shopId);

    // ── Invoice Recalculation ────────────────────────────────────────
    /// <summary>
    /// Recalculates Subtotal, TotalAmount, Balance, and Status for an invoice.
    /// Called after any mutation: add/remove line, adjustment approval, payment.
    /// </summary>
    Task RecalculateInvoiceAsync(long invoiceId);

    // ── Accounting Entries ───────────────────────────────────────────
    /// <summary>Generates double-entry accounting records when an invoice is created.</summary>
    Task GenerateInvoiceEntriesAsync(long shopId, long invoiceId);

    /// <summary>Generates double-entry accounting records when a payment is recorded.</summary>
    Task GeneratePaymentEntriesAsync(long shopId, long paymentId);

    /// <summary>Generates double-entry accounting records when a credit/refund adjustment is approved.</summary>
    Task GenerateAdjustmentEntriesAsync(long shopId, long adjustmentId);
}

// ─── Implementation ──────────────────────────────────────────────────

public class BillingCalculationService : IBillingCalculationService
{
    private readonly ApplicationDbContext _db;

    // Standard account codes for double-entry bookkeeping
    private const string ACCT_REVENUE = "4000";         // Revenue / Sales
    private const string ACCT_ACCOUNTS_RECEIVABLE = "1200"; // Accounts Receivable
    private const string ACCT_CASH = "1000";            // Cash / Bank
    private const string ACCT_REFUND_EXPENSE = "5100";  // Refund Expense
    private const string ACCT_ADJUSTMENT = "5200";      // Adjustments

    public BillingCalculationService(ApplicationDbContext db)
    {
        _db = db;
    }

    // ═════════════════════════════════════════════════════════════════
    // PRICE RESOLUTION
    // ═════════════════════════════════════════════════════════════════

    public async Task<decimal> ResolveServicePriceAsync(long serviceId)
    {
        var service = await _db.ServiceCatalogs
            .AsNoTracking()
            .FirstOrDefaultAsync(s => s.ServiceId == serviceId && s.IsActive);

        if (service is null)
            throw new InvalidOperationException($"Service #{serviceId} not found or inactive.");

        return service.BasePrice;
    }

    public async Task<decimal> ResolvePartPriceAsync(long shopId, long itemId)
    {
        var item = await _db.InventoryItems
            .AsNoTracking()
            .FirstOrDefaultAsync(i => i.ItemId == itemId && i.ShopId == shopId);

        if (item is null)
            throw new InvalidOperationException($"Inventory item #{itemId} not found.");

        // If the shop has a markup configured and the item has a UnitPrice of 0 (not set),
        // calculate from UnitCost + markup. Otherwise use the explicit UnitPrice.
        var markupPct = await GetShopMarkupPctAsync(shopId);

        if (markupPct > 0 && item.UnitPrice == 0 && item.UnitCost > 0)
        {
            // Apply markup: SellingPrice = UnitCost + (UnitCost × MarkupPct / 100)
            return Math.Round(item.UnitCost + (item.UnitCost * markupPct / 100m), 2);
        }

        // If UnitPrice is explicitly set, use it (it may already include markup)
        // If markup is configured AND UnitPrice is set, UnitPrice takes precedence
        return item.UnitPrice > 0 ? item.UnitPrice : item.UnitCost;
    }

    public async Task<decimal> GetShopMarkupPctAsync(long shopId)
    {
        return await _db.Shops
            .Where(s => s.ShopId == shopId)
            .Select(s => s.DefaultPartMarkupPct)
            .FirstOrDefaultAsync();
    }

    // ═════════════════════════════════════════════════════════════════
    // INVOICE RECALCULATION ENGINE
    // ═════════════════════════════════════════════════════════════════

    public async Task RecalculateInvoiceAsync(long invoiceId)
    {
        var invoice = await _db.Invoices
            .Include(i => i.InvoiceLines)
            .FirstOrDefaultAsync(i => i.InvoiceId == invoiceId);

        if (invoice is null) return;

        // 1. Subtotal = sum of all line totals
        var subtotal = invoice.InvoiceLines.Sum(l => l.Qty * l.UnitPrice);

        // 2. TotalAdjustments = sum of approved adjustments (credits/refunds negative, debits positive)
        var adjustments = await _db.CreditDebitAdjustments
            .Where(a => a.InvoiceId == invoiceId && a.Status == AdjustmentStatus.Approved)
            .ToListAsync();

        decimal totalAdjustments = 0;
        foreach (var adj in adjustments)
        {
            if (adj.AdjustmentType == AdjustmentType.Credit || adj.AdjustmentType == AdjustmentType.Refund)
                totalAdjustments -= adj.Amount;
            else // Debit
                totalAdjustments += adj.Amount;
        }

        // 3. AmountPaid = sum of all payment allocations
        var amountPaid = await _db.PaymentAllocations
            .Where(pa => pa.InvoiceId == invoiceId
                      && pa.Payment!.Status == PaymentStatus.Confirmed)
            .SumAsync(pa => (decimal?)pa.AmountApplied) ?? 0;

        // 4. Compute totals (preserve DiscountAmount from tax engine)
        invoice.Subtotal = subtotal;
        invoice.TotalAdjustments = totalAdjustments;
        invoice.TotalAmount = subtotal - invoice.DiscountAmount + totalAdjustments;
        invoice.AmountPaid = amountPaid;
        invoice.Balance = Math.Max(0, invoice.TotalAmount - amountPaid);

        // 5. Derive status
        if (invoice.Status == InvoiceStatus.Void)
        {
            // Don't change void status — keep it
        }
        else if (invoice.Balance <= 0)
        {
            invoice.Balance = 0;
            invoice.Status = InvoiceStatus.Paid;
        }
        else if (amountPaid > 0)
        {
            invoice.Status = InvoiceStatus.Partial;
        }
        else
        {
            invoice.Status = InvoiceStatus.Unpaid;
        }

        await _db.SaveChangesAsync();
    }

    // ═════════════════════════════════════════════════════════════════
    // ACCOUNTING ENTRY GENERATION (Double-Entry Bookkeeping)
    // ═════════════════════════════════════════════════════════════════

    /// <summary>
    /// Invoice Created → DR Accounts Receivable, CR Revenue.
    /// Idempotent: skips if entries already exist for this invoice.
    /// </summary>
    public async Task GenerateInvoiceEntriesAsync(long shopId, long invoiceId)
    {
        // Idempotency check
        var exists = await _db.AccountingEntries
            .AnyAsync(e => e.SourceInvoiceId == invoiceId && e.SourceType == "Invoice");
        if (exists) return;

        var invoice = await _db.Invoices
            .AsNoTracking()
            .FirstOrDefaultAsync(i => i.InvoiceId == invoiceId);
        if (invoice is null) return;

        var now = DateTime.UtcNow;

        // DR Accounts Receivable
        _db.AccountingEntries.Add(new AccountingEntry
        {
            ShopId = shopId,
            SourceType = "Invoice",
            SourceInvoiceId = invoiceId,
            EntryDate = now,
            AccountCode = ACCT_ACCOUNTS_RECEIVABLE,
            Debit = invoice.TotalAmount,
            Credit = 0,
            Memo = $"Invoice {invoice.InvoiceNo} issued"
        });

        // CR Revenue
        _db.AccountingEntries.Add(new AccountingEntry
        {
            ShopId = shopId,
            SourceType = "Invoice",
            SourceInvoiceId = invoiceId,
            EntryDate = now,
            AccountCode = ACCT_REVENUE,
            Debit = 0,
            Credit = invoice.TotalAmount,
            Memo = $"Revenue from invoice {invoice.InvoiceNo}"
        });

        await _db.SaveChangesAsync();
    }

    /// <summary>
    /// Payment Recorded → DR Cash, CR Accounts Receivable.
    /// Idempotent: skips if entries already exist for this payment.
    /// </summary>
    public async Task GeneratePaymentEntriesAsync(long shopId, long paymentId)
    {
        var exists = await _db.AccountingEntries
            .AnyAsync(e => e.SourcePaymentId == paymentId && e.SourceType == "Payment");
        if (exists) return;

        var payment = await _db.Payments
            .AsNoTracking()
            .FirstOrDefaultAsync(p => p.PaymentId == paymentId);
        if (payment is null) return;

        var now = DateTime.UtcNow;

        // DR Cash / Bank
        _db.AccountingEntries.Add(new AccountingEntry
        {
            ShopId = shopId,
            SourceType = "Payment",
            SourcePaymentId = paymentId,
            EntryDate = now,
            AccountCode = ACCT_CASH,
            Debit = payment.Amount,
            Credit = 0,
            Memo = $"Payment {payment.PaymentNo} received via {payment.Method}"
        });

        // CR Accounts Receivable
        _db.AccountingEntries.Add(new AccountingEntry
        {
            ShopId = shopId,
            SourceType = "Payment",
            SourcePaymentId = paymentId,
            EntryDate = now,
            AccountCode = ACCT_ACCOUNTS_RECEIVABLE,
            Debit = 0,
            Credit = payment.Amount,
            Memo = $"Applied payment {payment.PaymentNo}"
        });

        await _db.SaveChangesAsync();
    }

    /// <summary>
    /// Adjustment Approved →
    ///   Credit/Refund: DR Refund Expense, CR Accounts Receivable
    ///   Debit: DR Accounts Receivable, CR Adjustment
    /// Idempotent: skips if entries already exist.
    /// </summary>
    public async Task GenerateAdjustmentEntriesAsync(long shopId, long adjustmentId)
    {
        var exists = await _db.AccountingEntries
            .AnyAsync(e => e.SourceInvoiceId != null && e.SourceType == "Adjustment"
                        && e.Memo != null && e.Memo.Contains($"adj#{adjustmentId}"));
        if (exists) return;

        var adj = await _db.CreditDebitAdjustments
            .Include(a => a.Invoice)
            .AsNoTracking()
            .FirstOrDefaultAsync(a => a.AdjustmentId == adjustmentId);
        if (adj?.Invoice is null) return;

        var now = DateTime.UtcNow;
        var invoiceNo = adj.Invoice.InvoiceNo;

        if (adj.AdjustmentType == AdjustmentType.Credit || adj.AdjustmentType == AdjustmentType.Refund)
        {
            var acctCode = adj.AdjustmentType == AdjustmentType.Refund ? ACCT_REFUND_EXPENSE : ACCT_ADJUSTMENT;

            // DR Refund/Adjustment Expense
            _db.AccountingEntries.Add(new AccountingEntry
            {
                ShopId = shopId,
                SourceType = "Adjustment",
                SourceInvoiceId = adj.InvoiceId,
                EntryDate = now,
                AccountCode = acctCode,
                Debit = adj.Amount,
                Credit = 0,
                Memo = $"{adj.AdjustmentType} on {invoiceNo} (adj#{adjustmentId})"
            });

            // CR Accounts Receivable
            _db.AccountingEntries.Add(new AccountingEntry
            {
                ShopId = shopId,
                SourceType = "Adjustment",
                SourceInvoiceId = adj.InvoiceId,
                EntryDate = now,
                AccountCode = ACCT_ACCOUNTS_RECEIVABLE,
                Debit = 0,
                Credit = adj.Amount,
                Memo = $"{adj.AdjustmentType} applied to {invoiceNo} (adj#{adjustmentId})"
            });
        }
        else // Debit — increases what customer owes
        {
            // DR Accounts Receivable
            _db.AccountingEntries.Add(new AccountingEntry
            {
                ShopId = shopId,
                SourceType = "Adjustment",
                SourceInvoiceId = adj.InvoiceId,
                EntryDate = now,
                AccountCode = ACCT_ACCOUNTS_RECEIVABLE,
                Debit = adj.Amount,
                Credit = 0,
                Memo = $"Debit adjustment on {invoiceNo} (adj#{adjustmentId})"
            });

            // CR Adjustment account
            _db.AccountingEntries.Add(new AccountingEntry
            {
                ShopId = shopId,
                SourceType = "Adjustment",
                SourceInvoiceId = adj.InvoiceId,
                EntryDate = now,
                AccountCode = ACCT_ADJUSTMENT,
                Debit = 0,
                Credit = adj.Amount,
                Memo = $"Debit adjustment on {invoiceNo} (adj#{adjustmentId})"
            });
        }

        await _db.SaveChangesAsync();
    }
}
