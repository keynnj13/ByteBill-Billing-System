using ByteBill_BS.Data;
using ByteBill_BS.DTOs.Common;
using ByteBill_BS.DTOs.JobOrders;
using ByteBill_BS.Extensions;
using ByteBill_BS.Models.Enums;
using ByteBill_BS.Services;
using ByteBill_BS.ViewModels.JobOrders;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace ByteBill_BS.Areas.Technician.Controllers;

[Area("Technician")]
[Authorize]
public class JobOrdersController : Controller
{
    private readonly IJobOrderService _jobOrderService;
    private readonly ApplicationDbContext _db;

    public JobOrdersController(IJobOrderService jobOrderService, ApplicationDbContext db)
    {
        _jobOrderService = jobOrderService;
        _db = db;
    }

    private bool IsAuthorized() => User.IsInRoles("Technician");

    private static string GetInitials(string name)
    {
        var parts = name.Split(' ', StringSplitOptions.RemoveEmptyEntries);
        return parts.Length >= 2
            ? $"{parts[0][0]}{parts[^1][0]}".ToUpper()
            : (parts.Length == 1 ? parts[0][..1].ToUpper() : "??");
    }

    [HttpGet]
    public async Task<IActionResult> Index(JobOrderStatus? status, int page = 1)
    {
        if (!IsAuthorized()) return RedirectToAction("AccessDenied", "Auth", new { area = "" });

        var shopId = User.GetShopId();
        var userId = User.GetUserId();

        // Technicians see only their assigned jobs (exclude archived)
        var query = _db.JobOrders
            .Where(j => j.ShopId == shopId && j.AssignedTechUserId == userId && !j.IsArchived)
            .AsNoTracking();

        if (status.HasValue)
        {
            query = query.Where(j => j.Status == status.Value);
        }

        var totalCount = await query.CountAsync();

        var items = await query
            .OrderByDescending(j => j.CreatedAt)
            .Skip((page - 1) * 10)
            .Take(10)
            .Select(j => new JobOrderItemViewModel
            {
                Id = j.JobOrderId,
                JobNumber = j.JobOrderNo,
                JobOrderNumber = j.JobOrderNo,
                OrderNumber = j.JobOrderNo,
                CustomerName = j.Customer!.FirstName + " " + j.Customer.LastName,
                CustomerInitials = "",
                DeviceType = j.Device!.DeviceType,
                DeviceInfo = j.Device.DeviceType + " - " + j.Device.Brand + " " + j.Device.Model,
                DeviceBrand = j.Device.Brand,
                DeviceModel = j.Device.Model,
                Brand = j.Device.Brand,
                Status = j.Status,
                TechnicianName = j.AssignedTechUser != null
                    ? j.AssignedTechUser.FirstName + " " + j.AssignedTechUser.LastName : null,
                ProblemDescription = j.ProblemReported,
                CreatedAt = j.CreatedAt,
                UpdatedAt = j.UpdatedAt
            })
            .ToListAsync();

        // Set initials after materialization
        foreach (var item in items)
        {
            item.CustomerInitials = GetInitials(item.CustomerName);
        }

        var viewModel = new JobOrderListViewModel
        {
            StatusFilter = status,
            CurrentPage = page,
            TotalCount = totalCount,
            PageSize = 10,
            JobOrders = items
        };

        return View(viewModel);
    }

    [HttpGet]
    public async Task<IActionResult> Details(long id)
    {
        if (!IsAuthorized()) return RedirectToAction("AccessDenied", "Auth", new { area = "" });

        var shopId = User.GetShopId();
        var userId = User.GetUserId();

        var dto = await _jobOrderService.GetDetailAsync(shopId, id);
        if (dto == null) return NotFound();

        // Technicians can only view their assigned jobs
        if (dto.AssignedTechUserId != userId)
            return RedirectToAction("AccessDenied", "Auth", new { area = "" });

        _ = Enum.TryParse<JobOrderStatus>(dto.Status, true, out var parsedStatus);
        var serviceCost = dto.Services.Sum(s => s.LineTotal);
        var partsCost = dto.Parts.Sum(p => p.LineTotal);

        var model = new JobOrderDetailViewModel
        {
            Id = dto.JobOrderId,
            JobNumber = dto.JobOrderNo,
            JobOrderNumber = dto.JobOrderNo,
            OrderNumber = dto.JobOrderNo,
            CustomerId = dto.CustomerId,
            CustomerName = dto.CustomerName,
            CustomerInitials = GetInitials(dto.CustomerName),
            CustomerEmail = dto.CustomerEmail,
            CustomerPhone = dto.CustomerPhone ?? "",
            DeviceId = dto.DeviceId,
            DeviceType = dto.DeviceType,
            Brand = dto.Brand,
            DeviceBrand = dto.Brand,
            DeviceModel = dto.Model,
            SerialNumber = dto.SerialNo,
            DeviceSerial = dto.SerialNo,
            DeviceInfo = $"{dto.DeviceType} - {dto.Brand} {dto.Model}",
            Status = parsedStatus,
            TechnicianId = dto.AssignedTechUserId,
            AssignedTechnicianId = dto.AssignedTechUserId,
            TechnicianName = dto.TechnicianName,
            AssignedTechnicianName = dto.TechnicianName,
            CreatedBy = dto.CreatedByName,
            ProblemDescription = dto.ProblemReported,
            ProblemReported = dto.ProblemReported,
            IssueDescription = dto.ProblemReported,
            DiagnosisNotes = dto.DiagnosisNotes,
            TechnicianNotes = dto.DiagnosisNotes,
            Priority = dto.Priority,
            EstimatedCompletionDate = dto.EstimatedCompletionDate,
            CreatedAt = dto.CreatedAt,
            UpdatedAt = dto.UpdatedAt,
            InvoiceId = dto.InvoiceId,
            TotalServiceCost = serviceCost,
            TotalPartsCost = partsCost,
            Subtotal = serviceCost + partsCost,
            Total = serviceCost + partsCost,
            LineItems = dto.Services.Select(s => new JobOrderDetailViewModel.LineItem
            {
                Id = s.JobOrderServiceId,
                Description = s.ServiceName,
                Type = "Service",
                Quantity = s.Qty,
                UnitPrice = s.UnitPrice,
                Total = s.LineTotal
            })
            .Concat(dto.Parts.Select(p => new JobOrderDetailViewModel.LineItem
            {
                Id = p.JobOrderPartId,
                Description = p.ItemName,
                Type = "Part",
                Quantity = p.QtyUsed,
                UnitPrice = p.UnitPrice,
                Total = p.LineTotal
            })).ToList(),
            Timeline = dto.Timeline.Select(t => new TimelineEventViewModel
            {
                Title = $"Status → {t.NewStatus}",
                Description = $"By {t.ChangedByName}" + (string.IsNullOrEmpty(t.Remarks) ? "" : $" — {t.Remarks}"),
                Timestamp = t.ChangedAt,
                Status = t.NewStatus,
                CompletedBy = t.ChangedByName,
                IsCompleted = true
            }).ToList()
        };

        // Load available services and parts for add forms
        var canModifyLines = dto.InvoiceId == null
            && parsedStatus != JobOrderStatus.Completed
            && parsedStatus != JobOrderStatus.Cancelled;

        if (canModifyLines)
        {
            ViewBag.AvailableServices = await _db.ServiceCatalogs
                .Where(s => s.IsActive && s.ShopId == shopId)
                .OrderBy(s => s.ServiceName)
                .Select(s => new { Id = s.ServiceId, Name = s.ServiceName, Price = s.BasePrice })
                .ToListAsync();

            ViewBag.AvailableParts = await _db.InventoryItems
                .Where(i => i.ShopId == shopId && i.IsActive && i.QtyOnHand > 0)
                .OrderBy(i => i.ItemName)
                .Select(i => new { Id = i.ItemId, Name = i.ItemName, Stock = i.QtyOnHand, Price = i.UnitPrice })
                .ToListAsync();
        }

        ViewBag.CanModifyLines = canModifyLines;

        return View(model);
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> UpdateStatus(long id, JobOrderStatus status, string? notes)
    {
        if (!IsAuthorized()) return RedirectToAction("AccessDenied", "Auth", new { area = "" });

        var shopId = User.GetShopId();
        var userId = User.GetUserId();
        var userRole = User.GetRole();

        var result = await _jobOrderService.UpdateStatusAsync(shopId, userId, userRole, id,
            new UpdateJobOrderStatusRequest
            {
                NewStatus = status.ToString(),
                Remarks = notes
            });

        if (!result.Success)
        {
            TempData["Error"] = result.Message ?? "Failed to update status.";
            return RedirectToAction(nameof(Details), new { id });
        }

        TempData["Success"] = $"Job order status updated to {status}";

        // Redirect to Details so the tech stays on the same job order
        // and can see the updated status without confusion
        return RedirectToAction(nameof(Details), new { id });
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> AddNotes(long id, string notes)
    {
        if (!IsAuthorized()) return RedirectToAction("AccessDenied", "Auth", new { area = "" });

        var shopId = User.GetShopId();
        var userId = User.GetUserId();

        // Verify technician owns this job order
        var jobOrder = await _db.JobOrders
            .FirstOrDefaultAsync(j => j.ShopId == shopId && j.JobOrderId == id &&
                                      j.AssignedTechUserId == userId);

        if (jobOrder == null)
        {
            TempData["Error"] = "Job order not found or not assigned to you.";
            return RedirectToAction(nameof(Index));
        }

        // Append notes to diagnosis notes
        var existingNotes = jobOrder.DiagnosisNotes ?? "";
        var timestamp = DateTime.UtcNow.ToString("yyyy-MM-dd HH:mm");
        var fullName = User.GetFullName();
        jobOrder.DiagnosisNotes = string.IsNullOrWhiteSpace(existingNotes)
            ? $"[{timestamp}] {fullName}: {notes}"
            : $"{existingNotes}\n[{timestamp}] {fullName}: {notes}";
        jobOrder.UpdatedAt = DateTime.UtcNow;

        await _db.SaveChangesAsync();

        TempData["Success"] = "Notes added successfully";
        return RedirectToAction(nameof(Details), new { id });
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> AddItems(long jobOrderId, long? serviceCatalogId, long? inventoryItemId, int quantity = 1)
    {
        if (!IsAuthorized()) return RedirectToAction("AccessDenied", "Auth", new { area = "" });

        var shopId = User.GetShopId();
        var userId = User.GetUserId();
        var messages = new List<string>();
        var errors = new List<string>();

        // Add service if selected
        if (serviceCatalogId.HasValue && serviceCatalogId.Value > 0)
        {
            var svc = await _db.ServiceCatalogs.FindAsync(serviceCatalogId.Value);
            if (svc != null)
            {
                var dto = new DTOs.JobOrders.AddServiceLineDto
                {
                    ServiceId = serviceCatalogId.Value,
                    Qty = 1
                };
                var result = await _jobOrderService.AddServiceLineAsync(shopId, userId, jobOrderId, dto);
                if (result.Success) messages.Add("Service added");
                else errors.Add(result.Message ?? "Failed to add service");
            }
            else errors.Add("Service not found");
        }

        // Add part if selected
        if (inventoryItemId.HasValue && inventoryItemId.Value > 0)
        {
            var item = await _db.InventoryItems.FindAsync(inventoryItemId.Value);
            if (item != null)
            {
                var dto = new DTOs.JobOrders.AddPartLineDto
                {
                    ItemId = inventoryItemId.Value,
                    QtyUsed = quantity
                };
                var result = await _jobOrderService.AddPartLineAsync(shopId, userId, jobOrderId, dto);
                if (result.Success) messages.Add("Part added");
                else errors.Add(result.Message ?? "Failed to add part");
            }
            else errors.Add("Part not found");
        }

        if (!serviceCatalogId.HasValue && !inventoryItemId.HasValue)
            errors.Add("Please select at least a service or a part to add.");

        if (messages.Any()) TempData["Success"] = string.Join(" & ", messages) + " successfully.";
        if (errors.Any()) TempData["Error"] = string.Join("; ", errors);

        return RedirectToAction(nameof(Details), new { id = jobOrderId });
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> AddServiceLine(long jobOrderId, long serviceCatalogId)
    {
        if (!IsAuthorized()) return RedirectToAction("AccessDenied", "Auth", new { area = "" });

        var shopId = User.GetShopId();
        var userId = User.GetUserId();

        // Look up the service to get its price
        var svc = await _db.ServiceCatalogs.FindAsync(serviceCatalogId);
        if (svc == null)
        {
            TempData["Error"] = "Service not found.";
            return RedirectToAction(nameof(Details), new { id = jobOrderId });
        }

        var dto = new DTOs.JobOrders.AddServiceLineDto
        {
            ServiceId = serviceCatalogId,
            Qty = 1
        };

        var result = await _jobOrderService.AddServiceLineAsync(shopId, userId, jobOrderId, dto);
        if (result.Success)
            TempData["Success"] = "Service added successfully.";
        else
            TempData["Error"] = result.Message ?? "Failed to add service.";

        return RedirectToAction(nameof(Details), new { id = jobOrderId });
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> AddPartLine(long jobOrderId, long inventoryItemId, int quantity)
    {
        if (!IsAuthorized()) return RedirectToAction("AccessDenied", "Auth", new { area = "" });

        var shopId = User.GetShopId();
        var userId = User.GetUserId();

        // Look up the part to get its price
        var item = await _db.InventoryItems.FindAsync(inventoryItemId);
        if (item == null)
        {
            TempData["Error"] = "Part not found.";
            return RedirectToAction(nameof(Details), new { id = jobOrderId });
        }

        var dto = new DTOs.JobOrders.AddPartLineDto
        {
            ItemId = inventoryItemId,
            QtyUsed = quantity
        };

        var result = await _jobOrderService.AddPartLineAsync(shopId, userId, jobOrderId, dto);
        if (result.Success)
            TempData["Success"] = "Part added successfully.";
        else
            TempData["Error"] = result.Message ?? "Failed to add part.";

        return RedirectToAction(nameof(Details), new { id = jobOrderId });
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> RemoveLine(long jobOrderId, long lineId, string lineType)
    {
        if (!IsAuthorized()) return RedirectToAction("AccessDenied", "Auth", new { area = "" });

        var shopId = User.GetShopId();
        var userId = User.GetUserId();

        DTOs.Common.ApiResponse<bool>? result;
        if (lineType == "Service")
            result = await _jobOrderService.RemoveServiceLineAsync(shopId, userId, jobOrderId, lineId);
        else
            result = await _jobOrderService.RemovePartLineAsync(shopId, userId, jobOrderId, lineId);

        if (result.Success)
            TempData["Success"] = $"{lineType} removed successfully.";
        else
            TempData["Error"] = result.Message ?? $"Failed to remove {lineType.ToLower()}.";

        return RedirectToAction(nameof(Details), new { id = jobOrderId });
    }
}
