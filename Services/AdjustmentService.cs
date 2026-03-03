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
    // Refund-specific fields
    public string? RefundCategory { get; set; }
    public string? RefundExplanation { get; set; }
}

public class AdjustmentTypeConfigDto
{
    public long Id { get; set; }
    public string Name { get; set; } = "";
    public string Category { get; set; } = "";  // Credit, Debit, Refund
    public decimal Percentage { get; set; }
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
    Task<List<AdjustmentTypeConfigDto>> GetTypeConfigsAsync(long shopId);
    Task<AdjustmentTypeConfig> CreateTypeConfigAsync(long shopId, string name, string category, decimal percentage);
    Task<bool> UpdateTypeConfigAsync(long shopId, long configId, string name, string category, decimal percentage, bool isActive);
    Task<bool> DeleteTypeConfigAsync(long shopId, long configId);
}

// ─── Implementation ──────────────────────────────────────────────────

public class AdjustmentService : IAdjustmentService
{
    private readonly ApplicationDbContext _db;
    private readonly INotificationService _notifications;
    private readonly IBillingCalculationService _billing;
    private readonly IXeroService _xero;
    private readonly IPayMongoService _payMongo;

    public AdjustmentService(ApplicationDbContext db, INotificationService notifications,
        IBillingCalculationService billing, IXeroService xero, IPayMongoService payMongo)
    {
        _db = db;
        _notifications = notifications;
        _billing = billing;
        _xero = xero;
        _payMongo = payMongo;
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

        // Build reason with refund details if applicable
        var reason = request.Reason;
        if (adjType == AdjustmentType.Refund && !string.IsNullOrWhiteSpace(request.RefundCategory))
        {
            reason = $"[{request.RefundCategory}] {reason}";
            if (!string.IsNullOrWhiteSpace(request.RefundExplanation))
                reason += $" | Detail: {request.RefundExplanation}";
        }

        var adjustment = new CreditDebitAdjustment
        {
            ShopId = shopId,
            InvoiceId = request.InvoiceId,
            CreatedByUserId = userId,
            AdjustmentType = adjType,
            Amount = request.Amount,
            Reason = reason,
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
                       && ura.Role!.RoleName == "Admin")
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

        await _db.SaveChangesAsync();

        // Use centralized recalculation engine
        if (adj.Invoice != null)
        {
            await _billing.RecalculateInvoiceAsync(adj.InvoiceId);
            await _billing.GenerateAdjustmentEntriesAsync(adj.ShopId, adj.AdjustmentId);

            // Auto-sync credit note to Xero for credit/refund adjustments
            try { await _xero.SyncCreditNoteAsync(adj.AdjustmentId, reviewerId); } catch { /* logged in XeroService */ }

            // ── PayMongo integration on adjustment approval ──────────────
            try
            {
                if (adj.AdjustmentType == AdjustmentType.Refund)
                {
                    // Issue refund via PayMongo API for the original online payment
                    await _payMongo.RefundPaymentAsync(adj.ShopId, adj.InvoiceId, adj.Amount, adj.Reason);
                }

                if (adj.AdjustmentType == AdjustmentType.Credit || adj.AdjustmentType == AdjustmentType.Refund)
                {
                    // Expire any pending checkout sessions so customer can't overpay with old amount
                    await _payMongo.ExpirePendingSessionsAsync(adj.ShopId, adj.InvoiceId);
                }
            }
            catch { /* logged inside PayMongoService — don't block approval flow */ }
        }

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

    // ── Adjustment Type Config CRUD ──────────────────────────────────
    public async Task<List<AdjustmentTypeConfigDto>> GetTypeConfigsAsync(long shopId)
    {
        return await _db.AdjustmentTypeConfigs
            .Where(c => c.ShopId == shopId && c.IsActive)
            .OrderBy(c => c.Category).ThenBy(c => c.Name)
            .Select(c => new AdjustmentTypeConfigDto
            {
                Id = c.AdjustmentTypeConfigId,
                Name = c.Name,
                Category = c.Category,
                Percentage = c.Percentage
            })
            .ToListAsync();
    }

    public async Task<AdjustmentTypeConfig> CreateTypeConfigAsync(long shopId, string name, string category, decimal percentage)
    {
        var config = new AdjustmentTypeConfig
        {
            ShopId = shopId,
            Name = name.Trim(),
            Category = category,
            Percentage = percentage,
            IsActive = true,
            CreatedAt = DateTime.UtcNow
        };
        _db.AdjustmentTypeConfigs.Add(config);
        await _db.SaveChangesAsync();
        return config;
    }

    public async Task<bool> UpdateTypeConfigAsync(long shopId, long configId, string name, string category, decimal percentage, bool isActive)
    {
        var config = await _db.AdjustmentTypeConfigs
            .FirstOrDefaultAsync(c => c.AdjustmentTypeConfigId == configId && c.ShopId == shopId);
        if (config == null) return false;

        config.Name = name.Trim();
        config.Category = category;
        config.Percentage = percentage;
        config.IsActive = isActive;
        config.UpdatedAt = DateTime.UtcNow;
        await _db.SaveChangesAsync();
        return true;
    }

    public async Task<bool> DeleteTypeConfigAsync(long shopId, long configId)
    {
        var config = await _db.AdjustmentTypeConfigs
            .FirstOrDefaultAsync(c => c.AdjustmentTypeConfigId == configId && c.ShopId == shopId);
        if (config == null) return false;

        config.IsActive = false;
        config.UpdatedAt = DateTime.UtcNow;
        await _db.SaveChangesAsync();
        return true;
    }
}
