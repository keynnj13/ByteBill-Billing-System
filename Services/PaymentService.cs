using ByteBill_BS.Data;
using ByteBill_BS.DTOs.Common;
using ByteBill_BS.DTOs.Payments;
using ByteBill_BS.Models;
using ByteBill_BS.Models.Enums;
using Microsoft.AspNetCore.Http;
using Microsoft.EntityFrameworkCore;

namespace ByteBill_BS.Services;

public interface IPaymentService
{
    Task<PagedResult<PaymentListItemDto>> GetListAsync(long shopId, PaymentPagedRequest req);
    Task<PaymentDetailDto?> GetDetailAsync(long shopId, long paymentId);
    Task<PaymentMetricsDto> GetMetricsAsync(long shopId);
    Task<ApiResponse<PaymentDetailDto>> RecordPaymentAsync(long shopId, long userId, RecordPaymentRequest req);
}

public class PaymentService : IPaymentService
{
    private readonly ApplicationDbContext _db;
    private readonly IAuditService _audit;
    private readonly IHttpContextAccessor _httpCtx;

    public PaymentService(ApplicationDbContext db, IAuditService audit, IHttpContextAccessor httpCtx)
    {
        _db = db;
        _audit = audit;
        _httpCtx = httpCtx;
    }

    private string? ClientIp => _httpCtx.HttpContext?.Connection.RemoteIpAddress?.ToString();

    // ── List / Search / Filter ───────────────────────────────────────────
    public async Task<PagedResult<PaymentListItemDto>> GetListAsync(long shopId, PaymentPagedRequest req)
    {
        var query = _db.Payments
            .Where(p => p.ShopId == shopId)
            .AsNoTracking();

        // Status filter
        if (!string.IsNullOrWhiteSpace(req.StatusFilter) &&
            Enum.TryParse<PaymentStatus>(req.StatusFilter, true, out var sf))
        {
            query = query.Where(p => p.Status == sf);
        }

        // Search by customer name or reference
        if (!string.IsNullOrWhiteSpace(req.Search))
        {
            var term = req.Search.Trim().ToLower();
            query = query.Where(p =>
                (p.Customer!.FirstName + " " + p.Customer.LastName).ToLower().Contains(term) ||
                (p.ReferenceNo != null && p.ReferenceNo.ToLower().Contains(term)));
        }

        var totalCount = await query.CountAsync();

        var items = await query
            .OrderByDescending(p => p.PaymentDate)
            .Skip((req.Page - 1) * req.PageSize)
            .Take(req.PageSize)
            .Select(p => new PaymentListItemDto
            {
                PaymentId = p.PaymentId,
                PaymentNo = (p.PaymentNo != null && p.PaymentNo != "") ? p.PaymentNo : ("PAY-" + p.PaymentId),
                CustomerName = p.Customer!.FirstName + " " + p.Customer.LastName,
                InvoiceNo = p.PaymentAllocations
                    .Select(pa => pa.Invoice!.InvoiceNo)
                    .FirstOrDefault(),
                Amount = p.Amount,
                Method = p.Method.ToString(),
                PaymentDate = p.PaymentDate,
                ReceivedByName = p.ReceivedByUser!.FirstName + " " + p.ReceivedByUser.LastName,
                Status = p.Status.ToString(),
                ReferenceNo = p.ReferenceNo,
                Notes = p.Notes
            })
            .ToListAsync();

        return new PagedResult<PaymentListItemDto>
        {
            Items = items,
            TotalCount = totalCount,
            Page = req.Page,
            PageSize = req.PageSize
        };
    }

    // ── Detail ───────────────────────────────────────────────────────────
    public async Task<PaymentDetailDto?> GetDetailAsync(long shopId, long paymentId)
    {
        return await _db.Payments
            .Where(p => p.ShopId == shopId && p.PaymentId == paymentId)
            .AsNoTracking()
            .Select(p => new PaymentDetailDto
            {
                PaymentId = p.PaymentId,
                PaymentNo = (p.PaymentNo != null && p.PaymentNo != "") ? p.PaymentNo : ("PAY-" + p.PaymentId),
                Amount = p.Amount,
                Method = p.Method.ToString(),
                PaymentDate = p.PaymentDate,
                ReferenceNo = p.ReferenceNo,
                Status = p.Status.ToString(),
                Notes = p.Notes,

                CustomerId = p.CustomerId,
                CustomerName = p.Customer!.FirstName + " " + p.Customer.LastName,
                ReceivedByName = p.ReceivedByUser!.FirstName + " " + p.ReceivedByUser.LastName,

                Allocations = p.PaymentAllocations.Select(pa => new PaymentAllocationDto
                {
                    PaymentAllocationId = pa.PaymentAllocationId,
                    InvoiceId = pa.InvoiceId,
                    InvoiceNo = pa.Invoice!.InvoiceNo,
                    AmountApplied = pa.AmountApplied
                }).ToList()
            })
            .FirstOrDefaultAsync();
    }

    // ── Metrics ──────────────────────────────────────────────────────────
    public async Task<PaymentMetricsDto> GetMetricsAsync(long shopId)
    {
        var today = DateTime.UtcNow.Date;

        var payments = _db.Payments
            .Where(p => p.ShopId == shopId)
            .AsNoTracking();

        var totalReceived = await payments
            .Where(p => p.Status == PaymentStatus.Confirmed)
            .SumAsync(p => (decimal?)p.Amount) ?? 0;

        var todayReceived = await payments
            .Where(p => p.Status == PaymentStatus.Confirmed && p.PaymentDate >= today)
            .SumAsync(p => (decimal?)p.Amount) ?? 0;

        var transactions = await payments.CountAsync();

        return new PaymentMetricsDto
        {
            TotalReceived = totalReceived,
            TodayReceived = todayReceived,
            Transactions = transactions
        };
    }

    // ── Record Payment ───────────────────────────────────────────────────
    public async Task<ApiResponse<PaymentDetailDto>> RecordPaymentAsync(
        long shopId, long userId, RecordPaymentRequest req)
    {
        // Validate customer
        var customerExists = await _db.Customers
            .AnyAsync(c => c.ShopId == shopId && c.CustomerId == req.CustomerId && c.IsActive);
        if (!customerExists)
            return ApiResponse<PaymentDetailDto>.Fail("Customer not found or inactive.");

        // Parse payment method
        if (!Enum.TryParse<PaymentMethod>(req.Method, true, out var method))
            return ApiResponse<PaymentDetailDto>.Fail($"Invalid payment method '{req.Method}'.");

        // Validate allocations sum
        var allocationSum = req.Allocations.Sum(a => a.AmountApplied);
        if (allocationSum != req.Amount)
            return ApiResponse<PaymentDetailDto>.Fail(
                $"Sum of allocations ({allocationSum:F2}) must equal payment amount ({req.Amount:F2}).");

        // Validate each allocation — invoice belongs to shop+customer, and not over-applied
        var invoiceIds = req.Allocations.Select(a => a.InvoiceId).Distinct().ToList();
        var invoices = await _db.Invoices
            .Where(i => i.ShopId == shopId && invoiceIds.Contains(i.InvoiceId))
            .ToListAsync();

        if (invoices.Count != invoiceIds.Count)
            return ApiResponse<PaymentDetailDto>.Fail("One or more invoices not found.");

        foreach (var alloc in req.Allocations)
        {
            var invoice = invoices.First(i => i.InvoiceId == alloc.InvoiceId);

            if (invoice.CustomerId != req.CustomerId)
                return ApiResponse<PaymentDetailDto>.Fail(
                    $"Invoice '{invoice.InvoiceNo}' does not belong to this customer.");

            if (invoice.Status == InvoiceStatus.Void)
                return ApiResponse<PaymentDetailDto>.Fail(
                    $"Invoice '{invoice.InvoiceNo}' is voided.");

            if (alloc.AmountApplied > invoice.Balance)
                return ApiResponse<PaymentDetailDto>.Fail(
                    $"Amount applied ({alloc.AmountApplied:F2}) exceeds invoice '{invoice.InvoiceNo}' balance ({invoice.Balance:F2}).");
        }

        // ── Create payment ───────────────────────────────────────────
        var payment = new Payment
        {
            ShopId = shopId,
            CustomerId = req.CustomerId,
            PaymentDate = DateTime.UtcNow,
            Amount = req.Amount,
            Method = method,
            ReferenceNo = req.ReferenceNo?.Trim(),
            ReceivedByUserId = userId,
            Status = PaymentStatus.Confirmed,
            Notes = req.Notes?.Trim()
        };

        _db.Payments.Add(payment);
        await _db.SaveChangesAsync();

        // ── Create allocations and update invoices ───────────────────
        foreach (var alloc in req.Allocations)
        {
            _db.PaymentAllocations.Add(new PaymentAllocation
            {
                PaymentId = payment.PaymentId,
                InvoiceId = alloc.InvoiceId,
                AmountApplied = alloc.AmountApplied
            });

            var invoice = invoices.First(i => i.InvoiceId == alloc.InvoiceId);
            invoice.AmountPaid += alloc.AmountApplied;
            invoice.Balance = invoice.TotalAmount - invoice.AmountPaid;

            if (invoice.Balance <= 0)
            {
                invoice.Balance = 0;
                invoice.Status = InvoiceStatus.Paid;
            }
            else if (invoice.AmountPaid > 0)
            {
                invoice.Status = InvoiceStatus.Partial;
            }
        }

        await _db.SaveChangesAsync();

        await _audit.LogAsync(shopId, userId, "Create", "Payment", payment.PaymentId,
            $"Recorded payment of {req.Amount:C} via {method}. Allocated to {req.Allocations.Count} invoice(s).", ClientIp);

        var detail = await GetDetailAsync(shopId, payment.PaymentId);
        return ApiResponse<PaymentDetailDto>.Ok(detail!);
    }
}
