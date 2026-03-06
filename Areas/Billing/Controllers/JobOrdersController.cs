using ByteBill_BS.Data;
using ByteBill_BS.DTOs.Common;
using ByteBill_BS.DTOs.JobOrders;
using ByteBill_BS.Extensions;
using ByteBill_BS.Models;
using ByteBill_BS.Models.Enums;
using ByteBill_BS.Services;
using ByteBill_BS.ViewModels.JobOrders;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace ByteBill_BS.Areas.Billing.Controllers;

[Area("Billing")]
[Authorize]
public class JobOrdersController : Controller
{
    private readonly IJobOrderService _jobOrderService;
    private readonly ApplicationDbContext _db;
    private readonly IAuditService _audit;

    public JobOrdersController(IJobOrderService jobOrderService, ApplicationDbContext db, IAuditService audit)
    {
        _jobOrderService = jobOrderService;
        _db = db;
        _audit = audit;
    }

    private bool IsAuthorized() => User.IsInRoles("Billing", "Admin", "SuperAdmin");

    private static string GetInitials(string name)
    {
        var parts = name.Split(' ', StringSplitOptions.RemoveEmptyEntries);
        return parts.Length >= 2
            ? $"{parts[0][0]}{parts[^1][0]}".ToUpper()
            : (parts.Length == 1 ? parts[0][..1].ToUpper() : "??");
    }

    [HttpGet]
    public async Task<IActionResult> Index(string? search, JobOrderStatus? status, int page = 1)
    {
        if (!IsAuthorized()) return RedirectToAction("AccessDenied", "Auth", new { area = "" });

        var shopId = User.GetShopId();
        var result = await _jobOrderService.GetListAsync(shopId, new JobOrderPagedRequest
        {
            Page = page,
            PageSize = 10,
            Search = search,
            StatusFilter = status?.ToString()
        });

        // KPI counts — single query with GroupBy
        var kpiBase = _db.JobOrders.Where(j => j.ShopId == shopId && !j.IsArchived);
        var statusCounts = await kpiBase
            .GroupBy(j => j.Status)
            .Select(g => new { Status = g.Key, Count = g.Count() })
            .ToDictionaryAsync(g => g.Status, g => g.Count);

        var completedIds = await kpiBase
            .Where(j => j.Status == JobOrderStatus.Completed
                && !_db.Invoices.Any(i => i.JobOrderId == j.JobOrderId))
            .CountAsync();

        var viewModel = new JobOrderListViewModel
        {
            SearchTerm = search,
            StatusFilter = status,
            CurrentPage = result.Page,
            TotalCount = result.TotalCount,
            PageSize = result.PageSize,
            InProgressCount = statusCounts.GetValueOrDefault(JobOrderStatus.InProgress),
            CompletedCount = statusCounts.GetValueOrDefault(JobOrderStatus.Completed),
            PendingInvoicingCount = completedIds,
            JobOrders = result.Items.Select(j =>
            {
                var deviceParts = j.DeviceSummary?.Split(" - ", 2) ?? Array.Empty<string>();
                var deviceType = deviceParts.Length > 0 ? deviceParts[0] : "";
                _ = Enum.TryParse<JobOrderStatus>(j.Status, true, out var parsedStatus);

                return new JobOrderItemViewModel
                {
                    Id = j.JobOrderId,
                    JobNumber = j.JobOrderNo,
                    JobOrderNumber = j.JobOrderNo,
                    OrderNumber = j.JobOrderNo,
                    CustomerName = j.CustomerName,
                    CustomerInitials = GetInitials(j.CustomerName),
                    DeviceType = deviceType,
                    DeviceInfo = j.DeviceSummary ?? "",
                    Status = parsedStatus,
                    TechnicianName = j.TechnicianName,
                    AssignedTechnicianName = j.TechnicianName,
                    CreatedAt = j.CreatedAt
                };
            }).ToList()
        };

        return View(viewModel);
    }

    [HttpGet]
    public async Task<IActionResult> DetailsModal(long id)
    {
        if (!IsAuthorized()) return Forbid();

        var vm = await GetJobOrderDetailAsync(id);
        if (vm == null) return NotFound();
        return PartialView("_DetailsModal", vm);
    }

    // ── helpers ──────────────────────────────────────────────────────────
    private async Task PopulateDropdowns(JobOrderCreateViewModel model, long shopId)
    {
        var customers = await _db.Customers
            .Where(c => c.ShopId == shopId && c.IsActive)
            .OrderBy(c => c.FirstName)
            .Select(c => new CustomerSelectItem
            {
                Id = c.CustomerId,
                FullName = c.FirstName + " " + c.LastName,
                Name = c.FirstName + " " + c.LastName,
                Phone = c.Phone ?? "",
                Email = c.Email
            })
            .ToListAsync();

        // Pre-load active JO counts per technician in a single query
        var techActiveJoCounts = await _db.JobOrders
            .Where(j => j.ShopId == shopId
                && j.AssignedTechUserId != null
                && j.Status != JobOrderStatus.Completed
                && j.Status != JobOrderStatus.Cancelled)
            .GroupBy(j => j.AssignedTechUserId!.Value)
            .Select(g => new { TechId = g.Key, Count = g.Count() })
            .ToDictionaryAsync(g => g.TechId, g => g.Count);

        var technicians = await _db.Users
            .Include(u => u.UserRoles).ThenInclude(ur => ur.Role)
            .Where(u => u.ShopId == shopId && u.IsActive &&
                        u.UserRoles.Any(ur => ur.Role!.RoleName == "Technician"))
            .Select(u => new TechnicianSelectItem
            {
                Id = u.UserId,
                FullName = u.FirstName + " " + u.LastName,
                Name = u.FirstName + " " + u.LastName,
                ActiveJobOrders = 0
            })
            .ToListAsync();

        foreach (var tech in technicians)
            tech.ActiveJobOrders = techActiveJoCounts.GetValueOrDefault(tech.Id);

        model.AvailableCustomers = customers;
        model.Customers = customers;
        model.AvailableTechnicians = technicians;
        model.Technicians = technicians;
    }

    // ═══════════════════════════════════════════════════════════════════
    //  CREATE
    // ═══════════════════════════════════════════════════════════════════
    [HttpGet]
    public async Task<IActionResult> CreateModal()
    {
        if (!IsAuthorized()) return Forbid();

        var model = new JobOrderCreateViewModel { CurrentStep = 1 };
        await PopulateDropdowns(model, User.GetShopId());
        return PartialView("_CreateModal", model);
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Create(JobOrderCreateViewModel model)
    {
        if (!IsAuthorized()) return RedirectToAction("AccessDenied", "Auth", new { area = "" });

        if (!ModelState.IsValid)
        {
            await PopulateDropdowns(model, User.GetShopId());
            if (Request.Headers["X-Requested-With"] == "XMLHttpRequest")
                return PartialView("_CreateModal", model);
            return View(model);
        }

        var shopId = User.GetShopId();
        var userId = User.GetUserId();

        var request = new CreateJobOrderRequest
        {
            CustomerId = model.CustomerId,
            ProblemReported = model.ProblemDescription ?? model.IssueDescription ?? "",
            DiagnosisNotes = model.IssueDescription,
            AssignedTechUserId = model.AssignedTechnicianId,
            Priority = model.Priority ?? "Normal",
            EstimatedCompletionDate = model.EstimatedCompletionDate,
            NewDevice = new CreateDeviceDto
            {
                DeviceType = model.DeviceType ?? "",
                Brand = model.Brand ?? "N/A",
                Model = model.DeviceModel ?? "N/A",
                SerialNo = model.SerialNumber ?? model.DeviceSerial,
                Notes = model.DeviceAccessories
            }
        };

        var result = await _jobOrderService.CreateAsync(shopId, userId, request);
        if (!result.Success)
        {
            ModelState.AddModelError("", result.Message ?? "Failed to create job order.");
            await PopulateDropdowns(model, shopId);
            if (Request.Headers["X-Requested-With"] == "XMLHttpRequest")
                return PartialView("_CreateModal", model);
            return View(model);
        }

        TempData["Success"] = "Job order created successfully!";
        if (Request.Headers["X-Requested-With"] == "XMLHttpRequest")
            return Json(new { success = true, message = "Job order created successfully!", id = result.Data?.JobOrderId });
        return RedirectToAction(nameof(Index));
    }

    // ═══════════════════════════════════════════════════════════════════
    //  EDIT
    // ═══════════════════════════════════════════════════════════════════
    [HttpGet]
    public async Task<IActionResult> EditModal(long id)
    {
        if (!IsAuthorized()) return Forbid();

        var model = await GetJobOrderEditModelAsync(id);
        if (model == null) return NotFound();
        return PartialView("_EditModal", model);
    }

    private async Task<JobOrderCreateViewModel?> GetJobOrderEditModelAsync(long id)
    {
        var shopId = User.GetShopId();
        var dto = await _jobOrderService.GetDetailAsync(shopId, id);
        if (dto == null) return null;

        var model = new JobOrderCreateViewModel
        {
            Id = dto.JobOrderId,
            CustomerId = dto.CustomerId,
            DeviceType = dto.DeviceType,
            Brand = dto.Brand,
            DeviceModel = dto.Model,
            SerialNumber = dto.SerialNo,
            DeviceSerial = dto.SerialNo,
            DeviceAccessories = dto.DeviceAccessories,
            ProblemDescription = !string.IsNullOrWhiteSpace(dto.ProblemReported) ? dto.ProblemReported : dto.DiagnosisNotes ?? "",
            Priority = dto.Priority,
            EstimatedCompletionDate = dto.EstimatedCompletionDate,
            AssignedTechnicianId = dto.AssignedTechUserId
        };

        await PopulateDropdowns(model, shopId);
        return model;
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Edit(JobOrderCreateViewModel model)
    {
        if (!IsAuthorized()) return RedirectToAction("AccessDenied", "Auth", new { area = "" });

        if (!ModelState.IsValid)
        {
            await PopulateDropdowns(model, User.GetShopId());
            if (Request.Headers["X-Requested-With"] == "XMLHttpRequest")
                return PartialView("_EditModal", model);
            return View(model);
        }

        var shopId = User.GetShopId();
        var userId = User.GetUserId();

        var jobOrder = await _db.JobOrders
            .FirstOrDefaultAsync(j => j.ShopId == shopId && j.JobOrderId == model.Id);

        if (jobOrder == null)
        {
            ModelState.AddModelError("", "Job order not found.");
            await PopulateDropdowns(model, shopId);
            if (Request.Headers["X-Requested-With"] == "XMLHttpRequest")
                return PartialView("_EditModal", model);
            return View(model);
        }

        jobOrder.ProblemReported = model.ProblemDescription?.Trim() ?? jobOrder.ProblemReported;
        jobOrder.Priority = model.Priority ?? jobOrder.Priority;
        jobOrder.EstimatedCompletionDate = model.EstimatedCompletionDate;
        jobOrder.CustomerId = model.CustomerId;
        jobOrder.UpdatedAt = DateTime.UtcNow;

        // Update device
        var device = await _db.Devices.FindAsync(jobOrder.DeviceId);
        if (device != null)
        {
            device.DeviceType = model.DeviceType?.Trim() ?? device.DeviceType;
            device.Brand = model.Brand?.Trim() ?? device.Brand;
            device.Model = model.DeviceModel?.Trim() ?? device.Model;
            device.SerialNo = model.SerialNumber?.Trim() ?? model.DeviceSerial?.Trim();
            device.Notes = model.DeviceAccessories?.Trim();
        }

        // Reassign technician if changed
        if (model.AssignedTechnicianId.HasValue &&
            model.AssignedTechnicianId != jobOrder.AssignedTechUserId)
        {
            await _jobOrderService.AssignTechnicianAsync(shopId, userId, model.Id,
                new AssignTechnicianRequest { TechnicianUserId = model.AssignedTechnicianId.Value });
        }

        await _db.SaveChangesAsync();

        TempData["Success"] = "Job order updated successfully!";
        if (Request.Headers["X-Requested-With"] == "XMLHttpRequest")
            return Json(new { success = true, message = "Job order updated successfully!" });
        return RedirectToAction(nameof(Index));
    }

    private async Task<JobOrderDetailViewModel?> GetJobOrderDetailAsync(long id)
    {
        var shopId = User.GetShopId();
        var dto = await _jobOrderService.GetDetailAsync(shopId, id);
        if (dto == null) return null;

        _ = Enum.TryParse<JobOrderStatus>(dto.Status, true, out var parsedStatus);
        var serviceCost = dto.Services.Sum(s => s.LineTotal);
        var partsCost = dto.Parts.Sum(p => p.LineTotal);
        var subtotal = serviceCost + partsCost;

        // Get shop tax rate
        var shop = await _db.Shops.AsNoTracking().FirstOrDefaultAsync(s => s.ShopId == shopId);
        var taxRate = shop?.TaxRate ?? 0m;
        var taxAmount = shop?.IsVatRegistered == true && subtotal > 0
            ? Math.Round(subtotal - (subtotal / (1m + taxRate / 100m)), 2)
            : 0m;

        return new JobOrderDetailViewModel
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
            AssignedTechnician = dto.TechnicianName,
            CreatedBy = dto.CreatedByName,
            ProblemDescription = dto.ProblemReported,
            ProblemReported = dto.ProblemReported,
            IssueDescription = !string.IsNullOrWhiteSpace(dto.ProblemReported) ? dto.ProblemReported : dto.DiagnosisNotes ?? "",
            DiagnosisNotes = dto.DiagnosisNotes,
            TechnicianNotes = dto.DiagnosisNotes,
            Priority = dto.Priority,
            EstimatedCompletionDate = dto.EstimatedCompletionDate,
            CreatedAt = dto.CreatedAt,
            UpdatedAt = dto.UpdatedAt,
            InvoiceId = dto.InvoiceId,
            TotalServiceCost = serviceCost,
            TotalPartsCost = partsCost,
            Subtotal = subtotal,
            TaxRate = taxRate,
            TaxAmount = taxAmount,
            Total = subtotal,
            EstimatedCost = subtotal,
            Services = dto.Services.Select(s => new JobOrderServiceItemViewModel
            {
                Id = s.JobOrderServiceId,
                ServiceName = s.ServiceName,
                Quantity = s.Qty,
                UnitPrice = s.UnitPrice,
                Total = s.LineTotal
            }).ToList(),
            Parts = dto.Parts.Select(p => new JobOrderPartItemViewModel
            {
                Id = p.JobOrderPartId,
                PartName = p.ItemName,
                Quantity = p.QtyUsed,
                UnitPrice = p.UnitPrice,
                Total = p.LineTotal
            }).ToList(),
            Items = dto.Services.Select(s => new JobOrderItemLineViewModel
            {
                Id = s.JobOrderServiceId,
                Description = s.ServiceName,
                Quantity = s.Qty,
                UnitPrice = s.UnitPrice,
                Total = s.LineTotal,
                IsService = true
            })
            .Concat(dto.Parts.Select(p => new JobOrderItemLineViewModel
            {
                Id = p.JobOrderPartId,
                Description = p.ItemName,
                Quantity = p.QtyUsed,
                UnitPrice = p.UnitPrice,
                Total = p.LineTotal,
                IsService = false
            })).ToList(),
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
                IsCompleted = true,
                Icon = t.NewStatus switch
                {
                    "Pending" => "plus",
                    "CheckedIn" => "log-in",
                    "Diagnosis" => "search",
                    "InProgress" => "wrench",
                    "WaitingForParts" => "pause",
                    "Completed" => "check-circle",
                    "Cancelled" => "x-circle",
                    _ => "activity"
                }
            }).ToList()
        };
    }
}
