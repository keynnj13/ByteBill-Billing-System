using ByteBill_BS.Data;
using ByteBill_BS.Models;
using ByteBill_BS.Models.Enums;
using Microsoft.EntityFrameworkCore;

namespace ByteBill_BS.Services;

// ─── DTOs ────────────────────────────────────────────────────────────

public class AdjustmentListItemDto
{
    public long AdjustmentId { get; set; }
    public string AdjustmentType { get; set; } = "";
    public string InvoiceNumber { get; set; } = "";
    public long InvoiceId { get; set; }
    public string CustomerName { get; set; } = "";
    public decimal Amount { get; set; }
    public string Reason { get; set; } = "";
    public string Status { get; set; } = "";
    public string RequestedBy { get; set; } = "";
    public string? ReviewedBy { get; set; }
    public DateTime CreatedAt { get; set; }
    public DateTime? ReviewedAt { get; set; }
}

public class AdjustmentMetricsDto
{
    public int Approved { get; set; }
    public int Pending { get; set; }
    public int Rejected { get; set; }
    public decimal TotalValue { get; set; }
}

public class CreateAdjustmentRequest
{
    public long InvoiceId { get; set; }
    public string AdjustmentType { get; set; } = "";
    public decimal Amount { get; set; }
    public string Reason { get; set; } = "";
}

// ─── Interface ───────────────────────────────────────────────────────

public interface IAdjustmentService
{
    Task<List<AdjustmentListItemDto>> GetAllAsync(long shopId, AdjustmentStatus? statusFilter = null);
    Task<List<AdjustmentListItemDto>> GetByUserAsync(long shopId, long userId);
    Task<AdjustmentMetricsDto> GetMetricsAsync(long shopId);
    Task<AdjustmentMetricsDto> GetUserMetricsAsync(long shopId, long userId);
    Task<CreditDebitAdjustment> CreateAsync(long shopId, long userId, CreateAdjustmentRequest request);
    Task<bool> ApproveAsync(long adjustmentId, long shopId, long reviewerId);
    Task<bool> RejectAsync(long adjustmentId, long shopId, long reviewerId);
}

// ─── Implementation ──────────────────────────────────────────────────

public class AdjustmentService : IAdjustmentService
{
    private readonly ApplicationDbContext _db;
    private readonly INotificationService _notifications;

    public AdjustmentService(ApplicationDbContext db, INotificationService notifications)
    {
        _db = db;
        _notifications = notifications;
    }

    public async Task<List<AdjustmentListItemDto>> GetAllAsync(long shopId, AdjustmentStatus? statusFilter = null)
    {
        var query = _db.CreditDebitAdjustments
            .Where(a => a.ShopId == shopId);

        if (statusFilter.HasValue)
            query = query.Where(a => a.Status == statusFilter.Value);

        return await query
            .OrderByDescending(a => a.CreatedAt)
            .Select(a => new AdjustmentListItemDto
            {
                AdjustmentId = a.AdjustmentId,
                AdjustmentType = a.AdjustmentType.ToString(),
                InvoiceNumber = a.Invoice != null ? a.Invoice.InvoiceNo : "",
                InvoiceId = a.InvoiceId,
                CustomerName = a.Invoice != null && a.Invoice.Customer != null ? a.Invoice.Customer.FullName : "",
                Amount = a.Amount,
                Reason = a.Reason,
                Status = a.Status.ToString(),
                RequestedBy = a.CreatedByUser != null ? a.CreatedByUser.FullName : "",
                ReviewedBy = a.ReviewedByUser != null ? a.ReviewedByUser.FullName : null,
                CreatedAt = a.CreatedAt,
                ReviewedAt = a.ReviewedAt
            })
            .ToListAsync();
    }

    public async Task<List<AdjustmentListItemDto>> GetByUserAsync(long shopId, long userId)
    {
        return await _db.CreditDebitAdjustments
            .Where(a => a.ShopId == shopId && a.CreatedByUserId == userId)
            .OrderByDescending(a => a.CreatedAt)
            .Select(a => new AdjustmentListItemDto
            {
                AdjustmentId = a.AdjustmentId,
                AdjustmentType = a.AdjustmentType.ToString(),
                InvoiceNumber = a.Invoice != null ? a.Invoice.InvoiceNo : "",
                InvoiceId = a.InvoiceId,
                CustomerName = a.Invoice != null && a.Invoice.Customer != null ? a.Invoice.Customer.FullName : "",
                Amount = a.Amount,
                Reason = a.Reason,
                Status = a.Status.ToString(),
                RequestedBy = a.CreatedByUser != null ? a.CreatedByUser.FullName : "",
                ReviewedBy = a.ReviewedByUser != null ? a.ReviewedByUser.FullName : null,
                CreatedAt = a.CreatedAt,
                ReviewedAt = a.ReviewedAt
            })
            .ToListAsync();
    }

    public async Task<AdjustmentMetricsDto> GetMetricsAsync(long shopId)
    {
        var adjustments = await _db.CreditDebitAdjustments
            .Where(a => a.ShopId == shopId)
            .Select(a => new { a.Status, a.Amount, a.AdjustmentType })
            .ToListAsync();

        return new AdjustmentMetricsDto
        {
            Approved = adjustments.Count(a => a.Status == AdjustmentStatus.Approved),
            Pending = adjustments.Count(a => a.Status == AdjustmentStatus.Pending),
            Rejected = adjustments.Count(a => a.Status == AdjustmentStatus.Rejected),
            TotalValue = adjustments.Where(a => a.Status == AdjustmentStatus.Approved).Sum(a => a.Amount)
        };
    }

    public async Task<AdjustmentMetricsDto> GetUserMetricsAsync(long shopId, long userId)
    {
        var adjustments = await _db.CreditDebitAdjustments
            .Where(a => a.ShopId == shopId && a.CreatedByUserId == userId)
            .Select(a => new { a.Status, a.Amount })
            .ToListAsync();

        return new AdjustmentMetricsDto
        {
            Approved = adjustments.Count(a => a.Status == AdjustmentStatus.Approved),
            Pending = adjustments.Count(a => a.Status == AdjustmentStatus.Pending),
            Rejected = adjustments.Count(a => a.Status == AdjustmentStatus.Rejected),
            TotalValue = adjustments.Where(a => a.Status == AdjustmentStatus.Approved).Sum(a => a.Amount)
        };
    }

    public async Task<CreditDebitAdjustment> CreateAsync(long shopId, long userId, CreateAdjustmentRequest request)
    {
        if (!Enum.TryParse<AdjustmentType>(request.AdjustmentType, true, out var adjType))
            throw new ArgumentException("Invalid adjustment type.");

        var invoice = await _db.Invoices
            .Include(i => i.Customer)
            .FirstOrDefaultAsync(i => i.InvoiceId == request.InvoiceId && i.ShopId == shopId)
            ?? throw new ArgumentException("Invoice not found.");

        var adjustment = new CreditDebitAdjustment
        {
            ShopId = shopId,
            InvoiceId = request.InvoiceId,
            CreatedByUserId = userId,
            AdjustmentType = adjType,
            Amount = request.Amount,
            Reason = request.Reason,
            Status = AdjustmentStatus.Pending,
            CreatedAt = DateTime.UtcNow
        };

        _db.CreditDebitAdjustments.Add(adjustment);
        await _db.SaveChangesAsync();

        // Notify all admins in the shop
        var requesterName = await _db.Users.Where(u => u.UserId == userId).Select(u => u.FullName).FirstOrDefaultAsync() ?? "Staff";
        var amountSign = adjType == AdjustmentType.Debit ? "+" : "-";
        var adminUserIds = await _db.UserRoles
            .Where(ura => ura.User != null && ura.User.ShopId == shopId
                       && (ura.Role!.RoleName == "Admin" || ura.Role!.RoleName == "SuperAdmin"))
            .Select(ura => ura.UserId)
            .Distinct()
            .ToListAsync();

        foreach (var adminId in adminUserIds)
        {
            await _notifications.CreateAsync(
                adminId,
                shopId,
                "New Adjustment Request",
                $"{requesterName} requested a {amountSign}₱{request.Amount:N2} {adjType.ToString().ToLower()} on {invoice.InvoiceNo}.",
                "adjustment",
                "/Admin/Adjustments"
            );
        }

        return adjustment;
    }

    public async Task<bool> ApproveAsync(long adjustmentId, long shopId, long reviewerId)
    {
        var adj = await _db.CreditDebitAdjustments
            .Include(a => a.Invoice)
            .FirstOrDefaultAsync(a => a.AdjustmentId == adjustmentId && a.ShopId == shopId && a.Status == AdjustmentStatus.Pending);
        if (adj == null) return false;

        adj.Status = AdjustmentStatus.Approved;
        adj.ReviewedByUserId = reviewerId;
        adj.ReviewedAt = DateTime.UtcNow;

        // Apply to invoice balance
        if (adj.Invoice != null)
        {
            if (adj.AdjustmentType == AdjustmentType.Credit || adj.AdjustmentType == AdjustmentType.Refund)
            {
                // Credit/Refund reduces balance
                adj.Invoice.Balance = Math.Max(0, adj.Invoice.Balance - adj.Amount);
                adj.Invoice.AmountPaid += adj.Amount;
            }
            else if (adj.AdjustmentType == AdjustmentType.Debit)
            {
                // Debit increases balance
                adj.Invoice.Balance += adj.Amount;
                adj.Invoice.AmountPaid = Math.Max(0, adj.Invoice.AmountPaid - adj.Amount);
            }

            // Update status
            if (adj.Invoice.Balance <= 0)
            {
                adj.Invoice.Balance = 0;
                adj.Invoice.Status = InvoiceStatus.Paid;
            }
            else if (adj.Invoice.AmountPaid > 0)
                adj.Invoice.Status = InvoiceStatus.Partial;
            else
                adj.Invoice.Status = InvoiceStatus.Unpaid;
        }

        await _db.SaveChangesAsync();

        // Notify the requester
        var reviewerName = await _db.Users.Where(u => u.UserId == reviewerId).Select(u => u.FullName).FirstOrDefaultAsync() ?? "Admin";
        var invoiceNo = adj.Invoice?.InvoiceNo ?? $"INV-{adj.InvoiceId}";
        await _notifications.CreateAsync(
            adj.CreatedByUserId,
            shopId,
            "Adjustment Approved",
            $"{reviewerName} approved your ₱{adj.Amount:N2} {adj.AdjustmentType.ToString().ToLower()} on {invoiceNo}.",
            "approved",
            "/Billing/Adjustments"
        );

        return true;
    }

    public async Task<bool> RejectAsync(long adjustmentId, long shopId, long reviewerId)
    {
        var adj = await _db.CreditDebitAdjustments
            .Include(a => a.Invoice)
            .FirstOrDefaultAsync(a => a.AdjustmentId == adjustmentId && a.ShopId == shopId && a.Status == AdjustmentStatus.Pending);
        if (adj == null) return false;

        adj.Status = AdjustmentStatus.Rejected;
        adj.ReviewedByUserId = reviewerId;
        adj.ReviewedAt = DateTime.UtcNow;

        await _db.SaveChangesAsync();

        // Notify the requester
        var reviewerName = await _db.Users.Where(u => u.UserId == reviewerId).Select(u => u.FullName).FirstOrDefaultAsync() ?? "Admin";
        var invoiceNo = adj.Invoice?.InvoiceNo ?? $"INV-{adj.InvoiceId}";
        await _notifications.CreateAsync(
            adj.CreatedByUserId,
            shopId,
            "Adjustment Rejected",
            $"{reviewerName} rejected your ₱{adj.Amount:N2} {adj.AdjustmentType.ToString().ToLower()} on {invoiceNo}.",
            "rejected",
            "/Billing/Adjustments"
        );

        return true;
    }
}
