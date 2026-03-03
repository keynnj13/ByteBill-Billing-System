using ByteBill_BS.Data;
using ByteBill_BS.DTOs.Common;
using ByteBill_BS.DTOs.JobOrders;
using ByteBill_BS.Models;
using ByteBill_BS.Models.Enums;
using Microsoft.AspNetCore.Http;
using Microsoft.EntityFrameworkCore;

namespace ByteBill_BS.Services;

public interface IJobOrderService
{
    Task<PagedResult<JobOrderListItemDto>> GetListAsync(long shopId, JobOrderPagedRequest req);
    Task<JobOrderDetailDto?> GetDetailAsync(long shopId, long jobOrderId);
    Task<ApiResponse<JobOrderDetailDto>> CreateAsync(long shopId, long userId, CreateJobOrderRequest req);
    Task<ApiResponse<bool>> UpdateStatusAsync(long shopId, long userId, string userRole, long jobOrderId, UpdateJobOrderStatusRequest req);
    Task<ApiResponse<bool>> AssignTechnicianAsync(long shopId, long userId, long jobOrderId, AssignTechnicianRequest req);
    Task<ApiResponse<JobOrderServiceLineDto>> AddServiceLineAsync(long shopId, long userId, long jobOrderId, AddServiceLineDto dto);
    Task<ApiResponse<bool>> RemoveServiceLineAsync(long shopId, long userId, long jobOrderId, long serviceLineId);
    Task<ApiResponse<JobOrderPartLineDto>> AddPartLineAsync(long shopId, long userId, long jobOrderId, AddPartLineDto dto);
    Task<ApiResponse<bool>> RemovePartLineAsync(long shopId, long userId, long jobOrderId, long partLineId);
}

public class JobOrderService : IJobOrderService
{
    private readonly ApplicationDbContext _db;
    private readonly IAuditService _audit;
    private readonly IHttpContextAccessor _httpCtx;
    private readonly IInvoiceService _invoiceService;
    private readonly INotificationService _notif;
    private readonly IBillingCalculationService _billing;

    public JobOrderService(ApplicationDbContext db, IAuditService audit, IHttpContextAccessor httpCtx,
        IInvoiceService invoiceService, INotificationService notif, IBillingCalculationService billing)
    {
        _db = db;
        _audit = audit;
        _httpCtx = httpCtx;
        _invoiceService = invoiceService;
        _notif = notif;
        _billing = billing;
    }

    private string? ClientIp => _httpCtx.HttpContext?.Connection.RemoteIpAddress?.ToString();

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
        // Terminal states: Completed, Cancelled — no transitions out
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
            JobOrderStatus.CheckedIn, JobOrderStatus.Cancelled
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
                    UnitPrice = svc.OverridePrice ?? 0
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
                    UnitPrice = part.OverridePrice ?? 0
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
            $"Created job order '{jobOrderNo}'.", ClientIp);

        // Notify assigned technician
        if (jobOrder.AssignedTechUserId.HasValue)
        {
            await _notif.CreateAsync(
                jobOrder.AssignedTechUserId.Value, shopId,
                "New Job Order Assigned",
                $"You have been assigned to {jobOrderNo}.",
                "info",
                $"/Technician/JobOrders/Details/{jobOrder.JobOrderId}");
        }

        var detail = await GetDetailAsync(shopId, jobOrder.JobOrderId);
        return ApiResponse<JobOrderDetailDto>.Ok(detail!);
    }

    // ── Update Status ────────────────────────────────────────────────────
    public async Task<ApiResponse<bool>> UpdateStatusAsync(
        long shopId, long userId, string userRole, long jobOrderId, UpdateJobOrderStatusRequest req)
    {
        var jobOrder = await _db.JobOrders
            .Include(j => j.JobOrderServices)
            .Include(j => j.JobOrderParts)
            .Include(j => j.Invoice)
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

        // ── Workflow step validations ───────────────────────────────────
        // CheckedIn → Diagnosis: must have a technician assigned
        if (newStatus == JobOrderStatus.Diagnosis && jobOrder.AssignedTechUserId == null)
            return ApiResponse<bool>.Fail("A technician must be assigned before moving to Diagnosis.");

        // Diagnosis → InProgress: must have diagnosis notes filled in
        if (newStatus == JobOrderStatus.InProgress && jobOrder.Status == JobOrderStatus.Diagnosis
            && string.IsNullOrWhiteSpace(jobOrder.DiagnosisNotes))
            return ApiResponse<bool>.Fail("Diagnosis notes are required before moving to In Progress. Please add your findings first.");

        // → WaitingForParts: must provide notes explaining which parts are needed
        if (newStatus == JobOrderStatus.WaitingForParts && string.IsNullOrWhiteSpace(req.Remarks))
            return ApiResponse<bool>.Fail("Please provide notes explaining which parts are needed.");

        // WaitingForParts → InProgress: must have confirmation notes (either in status remarks or already added to diagnosis notes)
        if (newStatus == JobOrderStatus.InProgress && jobOrder.Status == JobOrderStatus.WaitingForParts
            && string.IsNullOrWhiteSpace(req.Remarks)
            && (string.IsNullOrWhiteSpace(jobOrder.DiagnosisNotes) || !jobOrder.DiagnosisNotes.Contains("Confirm", StringComparison.OrdinalIgnoreCase)))
            return ApiResponse<bool>.Fail("Please provide notes confirming parts have been received (either in the notes field above or via Add Notes).");

        // → Completed: must have at least one service line item
        if (newStatus == JobOrderStatus.Completed)
        {
            var hasService = jobOrder.JobOrderServices.Any();
            if (!hasService)
                return ApiResponse<bool>.Fail("At least one service must be listed before marking as Completed.");

            if (string.IsNullOrWhiteSpace(jobOrder.DiagnosisNotes))
                return ApiResponse<bool>.Fail("Diagnosis notes are required before marking as Completed.");
        }

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
            $"Status changed from '{oldStatus}' to '{newStatus}'. {req.Remarks}", ClientIp);

        // Notify assigned technician about status change (if not the one who changed it)
        if (jobOrder.AssignedTechUserId.HasValue && jobOrder.AssignedTechUserId.Value != userId)
        {
            await _notif.CreateAsync(
                jobOrder.AssignedTechUserId.Value, shopId,
                "Job Order Updated",
                $"{jobOrder.JobOrderNo} status changed to {newStatus}.",
                "info",
                $"/Technician/JobOrders/Details/{jobOrderId}");
        }

        // Notify admins about status change
        var adminIds = await _db.Users
            .Where(u => u.ShopId == shopId && u.IsActive && u.UserId != userId
                && u.UserRoles.Any(ur => ur.Role!.RoleName == "Admin"))
            .Select(u => u.UserId)
            .ToListAsync();
        foreach (var adminId in adminIds)
        {
            await _notif.CreateAsync(adminId, shopId,
                "Job Order Status Changed",
                $"{jobOrder.JobOrderNo} changed from {oldStatus} to {newStatus}.",
                "info",
                $"/Admin/JobOrders/DetailsModal/{jobOrderId}");
        }

        // ── Auto-generate invoice when job is marked Completed ──────────
        if (newStatus == JobOrderStatus.Completed && jobOrder.Invoice is null)
        {
            var invoiceResult = await _invoiceService.CreateFromJobOrderAsync(shopId, userId,
                new DTOs.Invoices.CreateInvoiceRequest { JobOrderId = jobOrderId });

            // If invoice creation fails, log it but don't block the status change
            if (!invoiceResult.Success)
            {
                await _audit.LogAsync(shopId, userId, "Warning", "Invoice", 0,
                    $"Auto-invoice for JO #{jobOrderId} failed: {invoiceResult.Message}", ClientIp);
            }
        }

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
            $"Technician (UserID={req.TechnicianUserId}) assigned.", ClientIp);

        // Notify the technician
        await _notif.CreateAsync(
            req.TechnicianUserId, shopId,
            "New Job Order Assigned",
            $"You have been assigned to {jobOrder.JobOrderNo}.",
            "info",
            $"/Technician/JobOrders/Details/{jobOrderId}");

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

    // ── Add Service Line ─────────────────────────────────────────────────
    public async Task<ApiResponse<JobOrderServiceLineDto>> AddServiceLineAsync(
        long shopId, long userId, long jobOrderId, AddServiceLineDto dto)
    {
        var jobOrder = await _db.JobOrders
            .FirstOrDefaultAsync(j => j.ShopId == shopId && j.JobOrderId == jobOrderId);

        if (jobOrder is null)
            return ApiResponse<JobOrderServiceLineDto>.Fail("Job order not found.");

        // Only allow adding lines before invoice is created and while in active states
        if (jobOrder.Status == JobOrderStatus.Completed || jobOrder.Status == JobOrderStatus.Cancelled)
            return ApiResponse<JobOrderServiceLineDto>.Fail("Cannot modify a completed or cancelled job order.");

        var existingInvoice = await _db.Invoices.AnyAsync(i => i.JobOrderId == jobOrderId);
        if (existingInvoice)
            return ApiResponse<JobOrderServiceLineDto>.Fail("Cannot add lines after invoice has been created.");

        // Validate service exists
        var service = await _db.ServiceCatalogs
            .FirstOrDefaultAsync(s => s.ServiceId == dto.ServiceId && s.IsActive);
        if (service is null)
            return ApiResponse<JobOrderServiceLineDto>.Fail("Service not found or inactive.");

        // Prevent duplicate service
        var alreadyAdded = await _db.JobOrderServices
            .AnyAsync(s => s.JobOrderId == jobOrderId && s.ServiceId == dto.ServiceId);
        if (alreadyAdded)
            return ApiResponse<JobOrderServiceLineDto>.Fail($"'{service.ServiceName}' has already been added to this job order.");

        // ── Rule-Based Price Resolution ──────────────────────────────
        var catalogPrice = await _billing.ResolveServicePriceAsync(dto.ServiceId);
        var unitPrice = catalogPrice;
        var isPriceOverride = false;
        string? overrideReason = null;

        if (dto.OverridePrice.HasValue && dto.OverridePrice.Value > 0 && dto.OverridePrice.Value != catalogPrice)
        {
            if (string.IsNullOrWhiteSpace(dto.OverrideReason))
                return ApiResponse<JobOrderServiceLineDto>.Fail(
                    $"Override reason is required when price differs from catalog (₱{catalogPrice:N2}).");

            unitPrice = dto.OverridePrice.Value;
            isPriceOverride = true;
            overrideReason = dto.OverrideReason.Trim();
        }

        var line = new Models.JobOrderService
        {
            JobOrderId = jobOrderId,
            ServiceId = dto.ServiceId,
            Qty = dto.Qty,
            UnitPrice = unitPrice,
            CatalogPrice = catalogPrice,
            IsPriceOverride = isPriceOverride,
            OverrideReason = overrideReason
        };

        _db.JobOrderServices.Add(line);
        jobOrder.UpdatedAt = DateTime.UtcNow;
        await _db.SaveChangesAsync();

        await _audit.LogAsync(shopId, userId, "AddServiceLine", "JobOrder", jobOrderId,
            $"Added service '{service.ServiceName}' (Qty:{dto.Qty}, Price:₱{unitPrice:N2}{(isPriceOverride ? $", Override from ₱{catalogPrice:N2}: {overrideReason}" : "")}).", ClientIp);

        return ApiResponse<JobOrderServiceLineDto>.Ok(new JobOrderServiceLineDto
        {
            JobOrderServiceId = line.JobOrderServiceId,
            ServiceId = line.ServiceId,
            ServiceName = service.ServiceName,
            Qty = line.Qty,
            UnitPrice = line.UnitPrice,
            LineTotal = line.Qty * line.UnitPrice
        });
    }

    // ── Remove Service Line ──────────────────────────────────────────────
    public async Task<ApiResponse<bool>> RemoveServiceLineAsync(
        long shopId, long userId, long jobOrderId, long serviceLineId)
    {
        var jobOrder = await _db.JobOrders
            .FirstOrDefaultAsync(j => j.ShopId == shopId && j.JobOrderId == jobOrderId);

        if (jobOrder is null)
            return ApiResponse<bool>.Fail("Job order not found.");

        if (jobOrder.Status == JobOrderStatus.Completed || jobOrder.Status == JobOrderStatus.Cancelled)
            return ApiResponse<bool>.Fail("Cannot modify a completed or cancelled job order.");

        var existingInvoice = await _db.Invoices.AnyAsync(i => i.JobOrderId == jobOrderId);
        if (existingInvoice)
            return ApiResponse<bool>.Fail("Cannot remove lines after invoice has been created.");

        var line = await _db.JobOrderServices
            .FirstOrDefaultAsync(s => s.JobOrderServiceId == serviceLineId && s.JobOrderId == jobOrderId);

        if (line is null)
            return ApiResponse<bool>.Fail("Service line not found.");

        _db.JobOrderServices.Remove(line);
        jobOrder.UpdatedAt = DateTime.UtcNow;
        await _db.SaveChangesAsync();

        await _audit.LogAsync(shopId, userId, "RemoveServiceLine", "JobOrder", jobOrderId,
            $"Removed service line #{serviceLineId}.", ClientIp);

        return ApiResponse<bool>.Ok(true);
    }

    // ── Add Part Line ────────────────────────────────────────────────────
    public async Task<ApiResponse<JobOrderPartLineDto>> AddPartLineAsync(
        long shopId, long userId, long jobOrderId, AddPartLineDto dto)
    {
        var jobOrder = await _db.JobOrders
            .FirstOrDefaultAsync(j => j.ShopId == shopId && j.JobOrderId == jobOrderId);

        if (jobOrder is null)
            return ApiResponse<JobOrderPartLineDto>.Fail("Job order not found.");

        if (jobOrder.Status == JobOrderStatus.Completed || jobOrder.Status == JobOrderStatus.Cancelled)
            return ApiResponse<JobOrderPartLineDto>.Fail("Cannot modify a completed or cancelled job order.");

        var existingInvoice = await _db.Invoices.AnyAsync(i => i.JobOrderId == jobOrderId);
        if (existingInvoice)
            return ApiResponse<JobOrderPartLineDto>.Fail("Cannot add lines after invoice has been created.");

        // Validate inventory item exists and has sufficient stock
        var item = await _db.InventoryItems
            .FirstOrDefaultAsync(i => i.ItemId == dto.ItemId && i.ShopId == shopId);
        if (item is null)
            return ApiResponse<JobOrderPartLineDto>.Fail("Inventory item not found.");

        if (item.QtyOnHand < dto.QtyUsed)
            return ApiResponse<JobOrderPartLineDto>.Fail(
                $"Insufficient stock for '{item.ItemName}'. Available: {item.QtyOnHand}, Required: {dto.QtyUsed}.");

        // ── Rule-Based Price Resolution (with shop markup) ───────────
        var catalogPrice = await _billing.ResolvePartPriceAsync(shopId, dto.ItemId);
        var unitPrice = catalogPrice;
        var isPriceOverride = false;
        string? overrideReason = null;

        if (dto.OverridePrice.HasValue && dto.OverridePrice.Value > 0 && dto.OverridePrice.Value != catalogPrice)
        {
            if (string.IsNullOrWhiteSpace(dto.OverrideReason))
                return ApiResponse<JobOrderPartLineDto>.Fail(
                    $"Override reason is required when price differs from catalog (₱{catalogPrice:N2}).");

            unitPrice = dto.OverridePrice.Value;
            isPriceOverride = true;
            overrideReason = dto.OverrideReason.Trim();
        }

        var line = new JobOrderPart
        {
            JobOrderId = jobOrderId,
            ItemId = dto.ItemId,
            QtyUsed = dto.QtyUsed,
            UnitPrice = unitPrice,
            CatalogPrice = catalogPrice,
            IsPriceOverride = isPriceOverride,
            OverrideReason = overrideReason
        };

        _db.JobOrderParts.Add(line);

        // Deduct inventory
        item.QtyOnHand -= dto.QtyUsed;

        // Record inventory transaction
        _db.InventoryTxns.Add(new InventoryTxn
        {
            ItemId = dto.ItemId,
            TxnType = InventoryTxnType.OUT,
            Quantity = dto.QtyUsed,
            ReferenceType = "JobOrder",
            ReferenceId = jobOrder.JobOrderId,
            Remarks = $"Used in JO#{jobOrder.JobOrderNo}",
            CreatedAt = DateTime.UtcNow
        });

        jobOrder.UpdatedAt = DateTime.UtcNow;
        await _db.SaveChangesAsync();

        await _audit.LogAsync(shopId, userId, "AddPartLine", "JobOrder", jobOrderId,
            $"Added part '{item.ItemName}' (Qty:{dto.QtyUsed}, Price:₱{unitPrice:N2}{(isPriceOverride ? $", Override from ₱{catalogPrice:N2}: {overrideReason}" : "")}). Stock: {item.QtyOnHand + dto.QtyUsed} → {item.QtyOnHand}.", ClientIp);

        return ApiResponse<JobOrderPartLineDto>.Ok(new JobOrderPartLineDto
        {
            JobOrderPartId = line.JobOrderPartId,
            ItemId = line.ItemId,
            ItemName = item.ItemName,
            QtyUsed = line.QtyUsed,
            UnitPrice = line.UnitPrice,
            LineTotal = line.QtyUsed * line.UnitPrice
        });
    }

    // ── Remove Part Line ─────────────────────────────────────────────────
    public async Task<ApiResponse<bool>> RemovePartLineAsync(
        long shopId, long userId, long jobOrderId, long partLineId)
    {
        var jobOrder = await _db.JobOrders
            .FirstOrDefaultAsync(j => j.ShopId == shopId && j.JobOrderId == jobOrderId);

        if (jobOrder is null)
            return ApiResponse<bool>.Fail("Job order not found.");

        if (jobOrder.Status == JobOrderStatus.Completed || jobOrder.Status == JobOrderStatus.Cancelled)
            return ApiResponse<bool>.Fail("Cannot modify a completed or cancelled job order.");

        var existingInvoice = await _db.Invoices.AnyAsync(i => i.JobOrderId == jobOrderId);
        if (existingInvoice)
            return ApiResponse<bool>.Fail("Cannot remove lines after invoice has been created.");

        var line = await _db.JobOrderParts
            .FirstOrDefaultAsync(p => p.JobOrderPartId == partLineId && p.JobOrderId == jobOrderId);

        if (line is null)
            return ApiResponse<bool>.Fail("Part line not found.");

        // Restore inventory
        var item = await _db.InventoryItems.FindAsync(line.ItemId);
        if (item != null)
        {
            item.QtyOnHand += line.QtyUsed;

            _db.InventoryTxns.Add(new InventoryTxn
            {
                ItemId = line.ItemId,
                TxnType = InventoryTxnType.IN,
                Quantity = line.QtyUsed,
                ReferenceType = "JobOrder",
                ReferenceId = jobOrder.JobOrderId,
                Remarks = $"Returned from JO#{jobOrder.JobOrderNo} (line removed)",
                CreatedAt = DateTime.UtcNow
            });
        }

        _db.JobOrderParts.Remove(line);
        jobOrder.UpdatedAt = DateTime.UtcNow;
        await _db.SaveChangesAsync();

        await _audit.LogAsync(shopId, userId, "RemovePartLine", "JobOrder", jobOrderId,
            $"Removed part line #{partLineId}. Inventory restored.", ClientIp);

        return ApiResponse<bool>.Ok(true);
    }
}
