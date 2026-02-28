using ByteBill_BS.Data;
using ByteBill_BS.DTOs.Common;
using ByteBill_BS.DTOs.Invoices;
using ByteBill_BS.Models;
using ByteBill_BS.Models.Enums;
using Microsoft.AspNetCore.Http;
using Microsoft.EntityFrameworkCore;

namespace ByteBill_BS.Services;

public interface IInvoiceService
{
    Task<PagedResult<InvoiceListItemDto>> GetListAsync(long shopId, InvoicePagedRequest req);
    Task<InvoiceDetailDto?> GetDetailAsync(long shopId, long invoiceId);
    Task<InvoiceMetricsDto> GetMetricsAsync(long shopId);
    Task<ApiResponse<InvoiceDetailDto>> CreateFromJobOrderAsync(long shopId, long userId, CreateInvoiceRequest req);
    Task<ApiResponse<AdjustmentDto>> CreateAdjustmentAsync(long shopId, long userId, long invoiceId, DTOs.Invoices.CreateAdjustmentRequest req);
}

public class InvoiceService : IInvoiceService
{
    private readonly ApplicationDbContext _db;
    private readonly IAuditService _audit;
    private readonly IHttpContextAccessor _httpCtx;
    private readonly IBillingCalculationService _billing;
    private readonly IXeroService _xero;
    private readonly ITaxCalculationService _tax;

    public InvoiceService(ApplicationDbContext db, IAuditService audit, IHttpContextAccessor httpCtx,
        IBillingCalculationService billing, IXeroService xero, ITaxCalculationService tax)
    {
        _db = db;
        _audit = audit;
        _httpCtx = httpCtx;
        _billing = billing;
        _xero = xero;
        _tax = tax;
    }

    private string? ClientIp => _httpCtx.HttpContext?.Connection.RemoteIpAddress?.ToString();

    // ── List / Search / Filter ───────────────────────────────────────────
    public async Task<PagedResult<InvoiceListItemDto>> GetListAsync(long shopId, InvoicePagedRequest req)
    {
        var query = _db.Invoices
            .Where(i => i.ShopId == shopId && !i.IsArchived)
            .AsNoTracking();

        // Status filter
        if (!string.IsNullOrWhiteSpace(req.StatusFilter) &&
            Enum.TryParse<InvoiceStatus>(req.StatusFilter, true, out var sf))
        {
            query = query.Where(i => i.Status == sf);
        }

        // Search by InvoiceNo, customer name, or JobOrderNo
        if (!string.IsNullOrWhiteSpace(req.Search))
        {
            var term = req.Search.Trim().ToLower();
            query = query.Where(i =>
                i.InvoiceNo.ToLower().Contains(term) ||
                (i.Customer!.FirstName + " " + i.Customer.LastName).ToLower().Contains(term) ||
                i.JobOrder!.JobOrderNo.ToLower().Contains(term));
        }

        var totalCount = await query.CountAsync();

        var items = await query
            .OrderByDescending(i => i.CreatedAt)
            .Skip((req.Page - 1) * req.PageSize)
            .Take(req.PageSize)
            .Select(i => new InvoiceListItemDto
            {
                InvoiceId = i.InvoiceId,
                InvoiceNo = i.InvoiceNo,
                CustomerName = i.Customer!.FirstName + " " + i.Customer.LastName,
                JobOrderNo = i.JobOrder!.JobOrderNo,
                TotalAmount = i.TotalAmount,
                AmountPaid = i.AmountPaid,
                Balance = i.Balance,
                Status = i.Status.ToString(),
                InvoiceDate = i.InvoiceDate,
                DueDate = i.DueDate
            })
            .ToListAsync();

        return new PagedResult<InvoiceListItemDto>
        {
            Items = items,
            TotalCount = totalCount,
            Page = req.Page,
            PageSize = req.PageSize
        };
    }

    // ── Detail ───────────────────────────────────────────────────────────
    public async Task<InvoiceDetailDto?> GetDetailAsync(long shopId, long invoiceId)
    {
        return await _db.Invoices
            .Where(i => i.ShopId == shopId && i.InvoiceId == invoiceId)
            .AsNoTracking()
            .Select(i => new InvoiceDetailDto
            {
                InvoiceId = i.InvoiceId,
                InvoiceNo = i.InvoiceNo,
                InvoiceDate = i.InvoiceDate,
                DueDate = i.DueDate,
                Status = i.Status.ToString(),

                Subtotal = i.Subtotal,
                DiscountAmount = i.DiscountAmount,
                TotalAdjustments = i.TotalAdjustments,
                TotalAmount = i.TotalAmount,
                AmountPaid = i.AmountPaid,
                Balance = i.Balance,

                VatableSales = i.VatableSales,
                VatExemptSales = i.VatExemptSales,
                ZeroRatedSales = i.ZeroRatedSales,
                VatAmount = i.VatAmount,

                CustomerId = i.CustomerId,
                CustomerName = i.Customer!.FirstName + " " + i.Customer.LastName,

                JobOrderId = i.JobOrderId,
                JobOrderNo = i.JobOrder!.JobOrderNo,

                Lines = i.InvoiceLines.Select(l => new InvoiceLineDto
                {
                    InvoiceLineId = l.InvoiceLineId,
                    LineType = l.LineType,
                    Description = l.Description,
                    Qty = l.Qty,
                    UnitPrice = l.UnitPrice,
                    LineTotal = l.LineTotal
                }).ToList(),

                Discounts = i.InvoiceDiscounts.Select(d => new InvoiceDiscountDto
                {
                    InvoiceDiscountId = d.InvoiceDiscountId,
                    DiscountType = d.DiscountType.ToString(),
                    Label = d.Label,
                    Percentage = d.Percentage,
                    Amount = d.Amount,
                    IsVatExempt = d.IsVatExempt,
                    BeneficiaryIdNo = d.BeneficiaryIdNo,
                    BeneficiaryName = d.BeneficiaryName,
                    AppliedByName = d.AppliedByUser != null
                        ? d.AppliedByUser.FirstName + " " + d.AppliedByUser.LastName
                        : null,
                    AppliedAt = d.AppliedAt
                }).ToList(),

                Adjustments = i.Adjustments.Select(a => new AdjustmentDto
                {
                    AdjustmentId = a.AdjustmentId,
                    AdjustmentType = a.AdjustmentType.ToString(),
                    Amount = a.Amount,
                    Reason = a.Reason,
                    CreatedByName = a.CreatedByUser!.FirstName + " " + a.CreatedByUser.LastName,
                    CreatedAt = a.CreatedAt
                }).ToList(),

                Payments = i.PaymentAllocations.Select(pa => new InvoicePaymentDto
                {
                    PaymentId = pa.PaymentId,
                    AmountApplied = pa.AmountApplied,
                    PaymentDate = pa.Payment!.PaymentDate,
                    Method = pa.Payment.Method.ToString()
                }).ToList()
            })
            .FirstOrDefaultAsync();
    }

    // ── Metrics ──────────────────────────────────────────────────────────
    public async Task<InvoiceMetricsDto> GetMetricsAsync(long shopId)
    {
        var today = DateTime.UtcNow.Date;

        var invoices = _db.Invoices
            .Where(i => i.ShopId == shopId && !i.IsArchived)
            .AsNoTracking();

        var totalInvoices = await invoices.CountAsync();

        var outstanding = await invoices
            .Where(i => i.Balance > 0 && i.Status != InvoiceStatus.Void)
            .SumAsync(i => i.Balance);

        var overdue = await invoices
            .Where(i => i.DueDate != null && i.DueDate < today && i.Balance > 0 && i.Status != InvoiceStatus.Void)
            .CountAsync();

        return new InvoiceMetricsDto
        {
            TotalInvoices = totalInvoices,
            Outstanding = outstanding,
            Overdue = overdue
        };
    }

    // ── Create Invoice from Job Order ────────────────────────────────────
    public async Task<ApiResponse<InvoiceDetailDto>> CreateFromJobOrderAsync(
        long shopId, long userId, CreateInvoiceRequest req)
    {
        // Load job order with lines
        var jobOrder = await _db.JobOrders
            .Include(j => j.JobOrderServices).ThenInclude(s => s.Service)
            .Include(j => j.JobOrderParts).ThenInclude(p => p.Item)
            .Include(j => j.Invoice)
            .FirstOrDefaultAsync(j => j.ShopId == shopId && j.JobOrderId == req.JobOrderId);

        if (jobOrder is null)
            return ApiResponse<InvoiceDetailDto>.Fail("Job order not found.");

        // Must be Completed before billing
        if (jobOrder.Status != JobOrderStatus.Completed)
            return ApiResponse<InvoiceDetailDto>.Fail(
                $"Job order must be in 'Completed' status to create an invoice. Current status: '{jobOrder.Status}'.");

        // 1:1 — prevent duplicate invoice
        if (jobOrder.Invoice is not null)
            return ApiResponse<InvoiceDetailDto>.Fail(
                $"An invoice ('{jobOrder.Invoice.InvoiceNo}') already exists for this job order.");

        // Generate InvoiceNo: INV-YYYY-####
        var invoiceNo = await GenerateInvoiceNoAsync(shopId);

        // Build invoice lines from job order services + parts
        var lines = new List<InvoiceLine>();

        foreach (var svc in jobOrder.JobOrderServices)
        {
            lines.Add(new InvoiceLine
            {
                LineType = "Service",
                Description = svc.Service?.ServiceName ?? $"Service #{svc.ServiceId}",
                Qty = svc.Qty,
                UnitPrice = svc.UnitPrice,
                CatalogPrice = svc.CatalogPrice,
                IsPriceOverride = svc.IsPriceOverride,
                OverrideReason = svc.OverrideReason
            });
        }

        foreach (var part in jobOrder.JobOrderParts)
        {
            lines.Add(new InvoiceLine
            {
                LineType = "Part",
                Description = part.Item?.ItemName ?? $"Part #{part.ItemId}",
                Qty = part.QtyUsed,
                UnitPrice = part.UnitPrice,
                CatalogPrice = part.CatalogPrice,
                IsPriceOverride = part.IsPriceOverride,
                OverrideReason = part.OverrideReason
            });
        }

        var subtotal = lines.Sum(l => l.Qty * l.UnitPrice);

        var invoice = new Invoice
        {
            ShopId = shopId,
            JobOrderId = jobOrder.JobOrderId,
            CustomerId = jobOrder.CustomerId,
            InvoiceNo = invoiceNo,
            InvoiceDate = DateTime.UtcNow,
            Subtotal = subtotal,
            TotalAdjustments = 0,
            TotalAmount = subtotal,
            AmountPaid = 0,
            Balance = subtotal,
            Status = InvoiceStatus.Unpaid,
            CreatedAt = DateTime.UtcNow,
            DueDate = req.DueDate ?? DateTime.UtcNow.AddDays(30)
        };

        _db.Invoices.Add(invoice);
        await _db.SaveChangesAsync();

        // Add lines (need InvoiceId)
        foreach (var line in lines)
        {
            line.InvoiceId = invoice.InvoiceId;
            _db.InvoiceLines.Add(line);
        }
        await _db.SaveChangesAsync();

        await _audit.LogAsync(shopId, userId, "Create", "Invoice", invoice.InvoiceId,
            $"Created invoice '{invoiceNo}' from job order '{jobOrder.JobOrderNo}'. Total: {subtotal:C}.", ClientIp);

        // Generate double-entry accounting records
        await _billing.GenerateInvoiceEntriesAsync(shopId, invoice.InvoiceId);

        // Compute BIR tax breakdown (VAT-inclusive)
        await _tax.ComputeTaxAsync(invoice.InvoiceId);

        // Auto-sync to Xero (fire-and-forget, errors logged internally)
        try { await _xero.SyncInvoiceAsync(invoice.InvoiceId, userId); } catch { /* logged in XeroService */ }

        var detail = await GetDetailAsync(shopId, invoice.InvoiceId);
        return ApiResponse<InvoiceDetailDto>.Ok(detail!);
    }

    // ── Create Adjustment ────────────────────────────────────────────────
    public async Task<ApiResponse<AdjustmentDto>> CreateAdjustmentAsync(
        long shopId, long userId, long invoiceId, DTOs.Invoices.CreateAdjustmentRequest req)
    {
        var invoice = await _db.Invoices
            .FirstOrDefaultAsync(i => i.ShopId == shopId && i.InvoiceId == invoiceId);

        if (invoice is null)
            return ApiResponse<AdjustmentDto>.Fail("Invoice not found.");

        if (invoice.Status == InvoiceStatus.Void)
            return ApiResponse<AdjustmentDto>.Fail("Cannot adjust a voided invoice.");

        if (!Enum.TryParse<AdjustmentType>(req.AdjustmentType, true, out var adjType))
            return ApiResponse<AdjustmentDto>.Fail("Invalid adjustment type.");

        var adjustment = new CreditDebitAdjustment
        {
            InvoiceId = invoiceId,
            CreatedByUserId = userId,
            AdjustmentType = adjType,
            Amount = req.Amount,
            Reason = req.Reason.Trim(),
            CreatedAt = DateTime.UtcNow
        };

        _db.CreditDebitAdjustments.Add(adjustment);

        // Use centralized recalculation engine instead of manual balance math
        await _db.SaveChangesAsync();
        await _billing.RecalculateInvoiceAsync(invoiceId);

        await _db.SaveChangesAsync();

        await _audit.LogAsync(shopId, userId, "Adjustment", "Invoice", invoiceId,
            $"{adjType} adjustment of {req.Amount:C}. Reason: {req.Reason}. New balance: {invoice.Balance:C}.", ClientIp);

        return ApiResponse<AdjustmentDto>.Ok(new AdjustmentDto
        {
            AdjustmentId = adjustment.AdjustmentId,
            AdjustmentType = adjType.ToString(),
            Amount = adjustment.Amount,
            Reason = adjustment.Reason,
            CreatedByName = "", // Caller can re-fetch detail if needed
            CreatedAt = adjustment.CreatedAt
        });
    }

    // ── Generate INV-YYYY-#### ───────────────────────────────────────────
    private async Task<string> GenerateInvoiceNoAsync(long shopId)
    {
        var year = DateTime.UtcNow.Year;
        var prefix = $"INV-{year}-";

        var lastNo = await _db.Invoices
            .Where(i => i.ShopId == shopId && i.InvoiceNo.StartsWith(prefix))
            .OrderByDescending(i => i.InvoiceNo)
            .Select(i => i.InvoiceNo)
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
}
