using ByteBill_BS.Data;
using ByteBill_BS.DTOs.Common;
using ByteBill_BS.DTOs.JobOrders;
using ByteBill_BS.Models;
using ByteBill_BS.Models.Enums;
using Microsoft.EntityFrameworkCore;

namespace ByteBill_BS.Services;

public interface IJobOrderService
{
    Task<PagedResult<JobOrderListItemDto>> GetListAsync(long shopId, JobOrderPagedRequest req);
    Task<JobOrderDetailDto?> GetDetailAsync(long shopId, long jobOrderId);
    Task<ApiResponse<JobOrderDetailDto>> CreateAsync(long shopId, long userId, CreateJobOrderRequest req);
    Task<ApiResponse<bool>> UpdateStatusAsync(long shopId, long userId, string userRole, long jobOrderId, UpdateJobOrderStatusRequest req);
    Task<ApiResponse<bool>> AssignTechnicianAsync(long shopId, long userId, long jobOrderId, AssignTechnicianRequest req);
}

public class JobOrderService : IJobOrderService
{
    private readonly ApplicationDbContext _db;
    private readonly IAuditService _audit;

    public JobOrderService(ApplicationDbContext db, IAuditService audit)
    {
        _db = db;
        _audit = audit;
    }

    // ═══════════════════════════════════════════════════════════════════
    //  Valid status transitions
    // ═══════════════════════════════════════════════════════════════════
    private static readonly Dictionary<JobOrderStatus, HashSet<JobOrderStatus>> ValidTransitions = new()
    {
        [JobOrderStatus.Pending]         = new() { JobOrderStatus.CheckedIn, JobOrderStatus.Cancelled },
        [JobOrderStatus.CheckedIn]       = new() { JobOrderStatus.Diagnosis, JobOrderStatus.Cancelled },
        [JobOrderStatus.Diagnosis]       = new() { JobOrderStatus.InProgress, JobOrderStatus.WaitingForParts, JobOrderStatus.Cancelled },
        [JobOrderStatus.InProgress]      = new() { JobOrderStatus.WaitingForParts, JobOrderStatus.Completed, JobOrderStatus.Cancelled },
        [JobOrderStatus.WaitingForParts] = new() { JobOrderStatus.InProgress, JobOrderStatus.Cancelled },
        [JobOrderStatus.Completed]       = new() { JobOrderStatus.Delivered },
        // Terminal states: Delivered, Cancelled — no transitions out
    };

    // Roles allowed to change status (all transitions).
    // Technician can do diagnosis/repair transitions; Billing can do billing-related.
    private static readonly Dictionary<string, HashSet<JobOrderStatus>> RoleAllowedTargets = new()
    {
        ["Technician"] = new()
        {
            JobOrderStatus.CheckedIn, JobOrderStatus.Diagnosis,
            JobOrderStatus.InProgress, JobOrderStatus.WaitingForParts,
            JobOrderStatus.Completed
        },
        ["Billing"] = new()
        {
            JobOrderStatus.CheckedIn,
            JobOrderStatus.Delivered, JobOrderStatus.Cancelled
        }
    };

    // ── List / Search / Filter ───────────────────────────────────────────
    public async Task<PagedResult<JobOrderListItemDto>> GetListAsync(long shopId, JobOrderPagedRequest req)
    {
        var query = _db.JobOrders
            .Where(j => j.ShopId == shopId && !j.IsArchived)
            .AsNoTracking();

        // Status filter
        if (!string.IsNullOrWhiteSpace(req.StatusFilter) &&
            Enum.TryParse<JobOrderStatus>(req.StatusFilter, true, out var statusFilter))
        {
            query = query.Where(j => j.Status == statusFilter);
        }

        // Search by job order number or customer name
        if (!string.IsNullOrWhiteSpace(req.Search))
        {
            var term = req.Search.Trim().ToLower();
            query = query.Where(j =>
                j.JobOrderNo.ToLower().Contains(term) ||
                (j.Customer!.FirstName + " " + j.Customer.LastName).ToLower().Contains(term));
        }

        var totalCount = await query.CountAsync();

        var items = await query
            .OrderByDescending(j => j.CreatedAt)
            .Skip((req.Page - 1) * req.PageSize)
            .Take(req.PageSize)
            .Select(j => new JobOrderListItemDto
            {
                JobOrderId = j.JobOrderId,
                JobOrderNo = j.JobOrderNo,
                CustomerName = j.Customer!.FirstName + " " + j.Customer.LastName,
                DeviceSummary = j.Device!.DeviceType + " - " + j.Device.Brand + " " + j.Device.Model,
                Status = j.Status.ToString(),
                TechnicianName = j.AssignedTechUser != null
                    ? j.AssignedTechUser.FirstName + " " + j.AssignedTechUser.LastName
                    : null,
                CreatedAt = j.CreatedAt
            })
            .ToListAsync();

        return new PagedResult<JobOrderListItemDto>
        {
            Items = items,
            TotalCount = totalCount,
            Page = req.Page,
            PageSize = req.PageSize
        };
    }

    // ── Detail ───────────────────────────────────────────────────────────
    public async Task<JobOrderDetailDto?> GetDetailAsync(long shopId, long jobOrderId)
    {
        return await _db.JobOrders
            .Where(j => j.ShopId == shopId && j.JobOrderId == jobOrderId)
            .AsNoTracking()
            .Select(j => new JobOrderDetailDto
            {
                JobOrderId = j.JobOrderId,
                JobOrderNo = j.JobOrderNo,
                ProblemReported = j.ProblemReported,
                DiagnosisNotes = j.DiagnosisNotes,
                Status = j.Status.ToString(),
                Priority = j.Priority ?? "Normal",
                EstimatedCompletionDate = j.EstimatedCompletionDate,
                CreatedAt = j.CreatedAt,
                UpdatedAt = j.UpdatedAt,

                CustomerId = j.CustomerId,
                CustomerName = j.Customer!.FirstName + " " + j.Customer.LastName,
                CustomerPhone = j.Customer.Phone,
                CustomerEmail = j.Customer.Email,

                DeviceId = j.DeviceId,
                DeviceType = j.Device!.DeviceType,
                Brand = j.Device.Brand,
                Model = j.Device.Model,
                SerialNo = j.Device.SerialNo,
                DeviceAccessories = j.Device.Notes,

                CreatedByName = j.CreatedByUser!.FirstName + " " + j.CreatedByUser.LastName,
                TechnicianName = j.AssignedTechUser != null
                    ? j.AssignedTechUser.FirstName + " " + j.AssignedTechUser.LastName
                    : null,
                AssignedTechUserId = j.AssignedTechUserId,

                Services = j.JobOrderServices.Select(s => new JobOrderServiceLineDto
                {
                    JobOrderServiceId = s.JobOrderServiceId,
                    ServiceId = s.ServiceId,
                    ServiceName = s.Service!.ServiceName,
                    Qty = s.Qty,
                    UnitPrice = s.UnitPrice,
                    LineTotal = s.LineTotal
                }).ToList(),

                Parts = j.JobOrderParts.Select(p => new JobOrderPartLineDto
                {
                    JobOrderPartId = p.JobOrderPartId,
                    ItemId = p.ItemId,
                    ItemName = p.Item!.ItemName,
                    QtyUsed = p.QtyUsed,
                    UnitPrice = p.UnitPrice,
                    LineTotal = p.LineTotal
                }).ToList(),

                Timeline = j.StatusHistory
                    .OrderByDescending(h => h.ChangedAt)
                    .Select(h => new StatusHistoryDto
                    {
                        OldStatus = h.OldStatus,
                        NewStatus = h.NewStatus,
                        ChangedByName = h.ChangedByUser!.FirstName + " " + h.ChangedByUser.LastName,
                        ChangedAt = h.ChangedAt,
                        Remarks = h.Remarks
                    }).ToList(),

                InvoiceId = j.Invoice != null ? j.Invoice.InvoiceId : null,
                InvoiceNo = j.Invoice != null ? j.Invoice.InvoiceNo : null
            })
            .FirstOrDefaultAsync();
    }

    // ── Create ───────────────────────────────────────────────────────────
    public async Task<ApiResponse<JobOrderDetailDto>> CreateAsync(long shopId, long userId, CreateJobOrderRequest req)
    {
        // Validate customer belongs to shop
        var customerExists = await _db.Customers
            .AnyAsync(c => c.ShopId == shopId && c.CustomerId == req.CustomerId && c.IsActive);
        if (!customerExists)
            return ApiResponse<JobOrderDetailDto>.Fail("Customer not found or inactive.");

        long deviceId;

        if (req.DeviceId.HasValue && req.DeviceId.Value > 0)
        {
            // Use existing device — must belong to the customer
            var deviceExists = await _db.Devices
                .AnyAsync(d => d.DeviceId == req.DeviceId.Value && d.CustomerId == req.CustomerId);
            if (!deviceExists)
                return ApiResponse<JobOrderDetailDto>.Fail("Device not found for this customer.");
            deviceId = req.DeviceId.Value;
        }
        else if (req.NewDevice is not null)
        {
            // Create new device
            var device = new Device
            {
                CustomerId = req.CustomerId,
                DeviceType = req.NewDevice.DeviceType.Trim(),
                Brand = req.NewDevice.Brand.Trim(),
                Model = req.NewDevice.Model.Trim(),
                SerialNo = req.NewDevice.SerialNo?.Trim(),
                Notes = req.NewDevice.Notes?.Trim()
            };
            _db.Devices.Add(device);
            await _db.SaveChangesAsync();
            deviceId = device.DeviceId;
        }
        else
        {
            return ApiResponse<JobOrderDetailDto>.Fail("Either DeviceId or NewDevice must be provided.");
        }

        // Validate technician if assigned
        if (req.AssignedTechUserId.HasValue)
        {
            var techExists = await _db.Users
                .Include(u => u.UserRoles).ThenInclude(ur => ur.Role)
                .AnyAsync(u => u.UserId == req.AssignedTechUserId.Value
                    && u.ShopId == shopId
                    && u.IsActive
                    && u.UserRoles.Any(ur => ur.Role!.RoleName == "Technician"));
            if (!techExists)
                return ApiResponse<JobOrderDetailDto>.Fail("Assigned technician not found or not a technician.");
        }

        // Generate JobOrderNo: JO-YYYY-####
        var jobOrderNo = await GenerateJobOrderNoAsync(shopId);

        var jobOrder = new JobOrder
        {
            ShopId = shopId,
            CustomerId = req.CustomerId,
            DeviceId = deviceId,
            CreatedByUserId = userId,
            AssignedTechUserId = req.AssignedTechUserId,
            JobOrderNo = jobOrderNo,
            ProblemReported = req.ProblemReported.Trim(),
            DiagnosisNotes = req.DiagnosisNotes?.Trim(),
            Priority = req.Priority ?? "Normal",
            EstimatedCompletionDate = req.EstimatedCompletionDate,
            Status = JobOrderStatus.Pending,
            CreatedAt = DateTime.UtcNow
        };

        _db.JobOrders.Add(jobOrder);
        await _db.SaveChangesAsync();

        // Add service lines
        if (req.Services?.Any() == true)
        {
            foreach (var svc in req.Services)
            {
                _db.JobOrderServices.Add(new Models.JobOrderService
                {
                    JobOrderId = jobOrder.JobOrderId,
                    ServiceId = svc.ServiceId,
                    Qty = svc.Qty,
                    UnitPrice = svc.UnitPrice
                });
            }
            await _db.SaveChangesAsync();
        }

        // Add part lines
        if (req.Parts?.Any() == true)
        {
            foreach (var part in req.Parts)
            {
                _db.JobOrderParts.Add(new JobOrderPart
                {
                    JobOrderId = jobOrder.JobOrderId,
                    ItemId = part.ItemId,
                    QtyUsed = part.QtyUsed,
                    UnitPrice = part.UnitPrice
                });
            }
            await _db.SaveChangesAsync();
        }

        // Initial status history entry
        _db.JobOrderStatusHistories.Add(new JobOrderStatusHistory
        {
            JobOrderId = jobOrder.JobOrderId,
            OldStatus = "",
            NewStatus = JobOrderStatus.Pending.ToString(),
            ChangedByUserId = userId,
            ChangedAt = DateTime.UtcNow,
            Remarks = "Job order created."
        });
        await _db.SaveChangesAsync();

        await _audit.LogAsync(shopId, userId, "Create", "JobOrder", jobOrder.JobOrderId,
            $"Created job order '{jobOrderNo}'.");

        var detail = await GetDetailAsync(shopId, jobOrder.JobOrderId);
        return ApiResponse<JobOrderDetailDto>.Ok(detail!);
    }

    // ── Update Status ────────────────────────────────────────────────────
    public async Task<ApiResponse<bool>> UpdateStatusAsync(
        long shopId, long userId, string userRole, long jobOrderId, UpdateJobOrderStatusRequest req)
    {
        var jobOrder = await _db.JobOrders
            .FirstOrDefaultAsync(j => j.ShopId == shopId && j.JobOrderId == jobOrderId);

        if (jobOrder is null)
            return ApiResponse<bool>.Fail("Job order not found.");

        if (!Enum.TryParse<JobOrderStatus>(req.NewStatus, true, out var newStatus))
            return ApiResponse<bool>.Fail($"Invalid status '{req.NewStatus}'.");

        // Validate transition
        if (!ValidTransitions.TryGetValue(jobOrder.Status, out var allowed) || !allowed.Contains(newStatus))
            return ApiResponse<bool>.Fail($"Cannot transition from '{jobOrder.Status}' to '{newStatus}'.");

        // Validate role permission
        if (userRole == "Auditor")
            return ApiResponse<bool>.Fail("Auditors have read-only access.");

        if (RoleAllowedTargets.TryGetValue(userRole, out var roleTargets) && !roleTargets.Contains(newStatus))
            return ApiResponse<bool>.Fail($"Role '{userRole}' is not allowed to set status to '{newStatus}'.");

        var oldStatus = jobOrder.Status;
        jobOrder.Status = newStatus;
        jobOrder.UpdatedAt = DateTime.UtcNow;

        _db.JobOrderStatusHistories.Add(new JobOrderStatusHistory
        {
            JobOrderId = jobOrderId,
            OldStatus = oldStatus.ToString(),
            NewStatus = newStatus.ToString(),
            ChangedByUserId = userId,
            ChangedAt = DateTime.UtcNow,
            Remarks = req.Remarks?.Trim()
        });

        await _db.SaveChangesAsync();

        await _audit.LogAsync(shopId, userId, "StatusChange", "JobOrder", jobOrderId,
            $"Status changed from '{oldStatus}' to '{newStatus}'. {req.Remarks}");

        return ApiResponse<bool>.Ok(true);
    }

    // ── Assign Technician ────────────────────────────────────────────────
    public async Task<ApiResponse<bool>> AssignTechnicianAsync(
        long shopId, long userId, long jobOrderId, AssignTechnicianRequest req)
    {
        var jobOrder = await _db.JobOrders
            .FirstOrDefaultAsync(j => j.ShopId == shopId && j.JobOrderId == jobOrderId);

        if (jobOrder is null)
            return ApiResponse<bool>.Fail("Job order not found.");

        var techExists = await _db.Users
            .Include(u => u.UserRoles).ThenInclude(ur => ur.Role)
            .AnyAsync(u => u.UserId == req.TechnicianUserId
                && u.ShopId == shopId
                && u.IsActive
                && u.UserRoles.Any(ur => ur.Role!.RoleName == "Technician"));

        if (!techExists)
            return ApiResponse<bool>.Fail("Technician not found or not a valid technician.");

        jobOrder.AssignedTechUserId = req.TechnicianUserId;
        jobOrder.UpdatedAt = DateTime.UtcNow;
        await _db.SaveChangesAsync();

        await _audit.LogAsync(shopId, userId, "AssignTechnician", "JobOrder", jobOrderId,
            $"Technician (UserID={req.TechnicianUserId}) assigned.");

        return ApiResponse<bool>.Ok(true);
    }

    // ── Generate JO-YYYY-#### ────────────────────────────────────────────
    private async Task<string> GenerateJobOrderNoAsync(long shopId)
    {
        var year = DateTime.UtcNow.Year;
        var prefix = $"JO-{year}-";

        var lastNo = await _db.JobOrders
            .Where(j => j.ShopId == shopId && j.JobOrderNo.StartsWith(prefix))
            .OrderByDescending(j => j.JobOrderNo)
            .Select(j => j.JobOrderNo)
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
