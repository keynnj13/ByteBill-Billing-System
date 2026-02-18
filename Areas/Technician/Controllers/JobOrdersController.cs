using ByteBill_BS.Data;
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

        // Technicians see only their assigned jobs
        var query = _db.JobOrders
            .Where(j => j.ShopId == shopId && j.AssignedTechUserId == userId)
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
                Description = s.ServiceName,
                Type = "Service",
                Quantity = s.Qty,
                UnitPrice = s.UnitPrice,
                Total = s.LineTotal
            })
            .Concat(dto.Parts.Select(p => new JobOrderDetailViewModel.LineItem
            {
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
        }
        else
        {
            TempData["Success"] = $"Job order status updated to {status}";
        }

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
}
