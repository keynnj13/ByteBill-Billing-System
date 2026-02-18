using ByteBill_BS.Data;
using ByteBill_BS.DTOs.JobOrders;
using ByteBill_BS.Extensions;
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

        var viewModel = new JobOrderListViewModel
        {
            SearchTerm = search,
            StatusFilter = status,
            CurrentPage = result.Page,
            TotalCount = result.TotalCount,
            PageSize = result.PageSize,
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
    public async Task<IActionResult> Details(long id)
    {
        if (!IsAuthorized()) return RedirectToAction("AccessDenied", "Auth", new { area = "" });

        var shopId = User.GetShopId();
        var dto = await _jobOrderService.GetDetailAsync(shopId, id);
        if (dto == null) return NotFound();

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

    [HttpGet]
    public async Task<IActionResult> Create()
    {
        if (!IsAuthorized()) return RedirectToAction("AccessDenied", "Auth", new { area = "" });

        var shopId = User.GetShopId();

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

        var technicians = await _db.Users
            .Include(u => u.UserRoles).ThenInclude(ur => ur.Role)
            .Where(u => u.ShopId == shopId && u.IsActive &&
                        u.UserRoles.Any(ur => ur.Role!.RoleName == "Technician"))
            .Select(u => new TechnicianSelectItem
            {
                Id = u.UserId,
                FullName = u.FirstName + " " + u.LastName,
                Name = u.FirstName + " " + u.LastName,
                ActiveJobOrders = _db.JobOrders.Count(j => j.AssignedTechUserId == u.UserId &&
                    j.Status != JobOrderStatus.Completed && j.Status != JobOrderStatus.Delivered && j.Status != JobOrderStatus.Cancelled)
            })
            .ToListAsync();

        var model = new JobOrderCreateViewModel
        {
            CurrentStep = 1,
            AvailableCustomers = customers,
            Customers = customers,
            AvailableTechnicians = technicians,
            Technicians = technicians
        };

        return View(model);
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Create(JobOrderCreateViewModel viewModel)
    {
        if (!IsAuthorized()) return RedirectToAction("AccessDenied", "Auth", new { area = "" });

        var shopId = User.GetShopId();
        var userId = User.GetUserId();

        // Remove validations for non-required wizard fields
        ModelState.Remove("IssueDescription");
        ModelState.Remove("Priority");
        ModelState.Remove("DeviceAccessories");
        ModelState.Remove("DeviceSerial");
        ModelState.Remove("EstimatedCompletionDate");

        if (!ModelState.IsValid)
        {
            // Repopulate dropdowns
            await PopulateCreateDropdowns(viewModel, shopId);
            return View(viewModel);
        }

        var request = new CreateJobOrderRequest
        {
            CustomerId = viewModel.CustomerId,
            NewDevice = new CreateDeviceDto
            {
                DeviceType = viewModel.DeviceType,
                Brand = viewModel.Brand ?? "",
                Model = viewModel.DeviceModel ?? "",
                SerialNo = viewModel.SerialNumber
            },
            ProblemReported = viewModel.ProblemDescription,
            Priority = viewModel.Priority ?? "Normal",
            EstimatedCompletionDate = viewModel.EstimatedCompletionDate,
            AssignedTechUserId = viewModel.AssignedTechnicianId
        };

        var result = await _jobOrderService.CreateAsync(shopId, userId, request);
        if (!result.Success)
        {
            ModelState.AddModelError("", result.Message ?? "Failed to create job order.");
            await PopulateCreateDropdowns(viewModel, shopId);
            return View(viewModel);
        }

        TempData["Success"] = "Job order created successfully!";
        return RedirectToAction(nameof(Details), new { id = result.Data!.JobOrderId });
    }

    /// <summary>
    /// AJAX endpoint to load customer devices for the wizard.
    /// </summary>
    [HttpGet]
    public async Task<IActionResult> GetCustomerDevices(long customerId)
    {
        if (!IsAuthorized()) return Forbid();

        var shopId = User.GetShopId();
        var devices = await _db.Devices
            .Where(d => d.CustomerId == customerId &&
                        d.Customer!.ShopId == shopId)
            .Select(d => new
            {
                d.DeviceId,
                d.DeviceType,
                d.Brand,
                d.Model,
                d.SerialNo
            })
            .ToListAsync();

        return Json(devices);
    }

    private async Task PopulateCreateDropdowns(JobOrderCreateViewModel model, long shopId)
    {
        model.AvailableCustomers = await _db.Customers
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
        model.Customers = model.AvailableCustomers;

        model.AvailableTechnicians = await _db.Users
            .Include(u => u.UserRoles).ThenInclude(ur => ur.Role)
            .Where(u => u.ShopId == shopId && u.IsActive &&
                        u.UserRoles.Any(ur => ur.Role!.RoleName == "Technician"))
            .Select(u => new TechnicianSelectItem
            {
                Id = u.UserId,
                FullName = u.FirstName + " " + u.LastName,
                Name = u.FirstName + " " + u.LastName
            })
            .ToListAsync();
        model.Technicians = model.AvailableTechnicians;
    }

    // ═══════════════════════════════════════════════════════════════════
    //  ARCHIVE
    // ═══════════════════════════════════════════════════════════════════
    [HttpGet]
    public async Task<IActionResult> Archive(string? search, JobOrderStatus? status, int page = 1)
    {
        if (!IsAuthorized()) return RedirectToAction("AccessDenied", "Auth", new { area = "" });

        var shopId = User.GetShopId();
        var query = _db.JobOrders
            .Where(j => j.ShopId == shopId && j.IsArchived)
            .AsNoTracking();

        if (status.HasValue)
            query = query.Where(j => j.Status == status.Value);

        if (!string.IsNullOrWhiteSpace(search))
        {
            var term = search.Trim().ToLower();
            query = query.Where(j =>
                j.JobOrderNo.ToLower().Contains(term) ||
                (j.Customer!.FirstName + " " + j.Customer.LastName).ToLower().Contains(term));
        }

        var totalCount = await query.CountAsync();
        var pageSize = 10;
        var items = await query
            .OrderByDescending(j => j.ArchivedDate)
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .Select(j => new JobOrderItemViewModel
            {
                Id = j.JobOrderId,
                JobNumber = j.JobOrderNo,
                JobOrderNumber = j.JobOrderNo,
                OrderNumber = j.JobOrderNo,
                CustomerName = j.Customer!.FirstName + " " + j.Customer.LastName,
                CustomerInitials = GetInitials(j.Customer!.FirstName + " " + j.Customer.LastName),
                DeviceType = j.Device!.DeviceType,
                DeviceInfo = j.Device.DeviceType + " - " + j.Device.Brand + " " + j.Device.Model,
                DeviceBrand = j.Device.Brand,
                DeviceModel = j.Device.Model,
                Status = j.Status,
                TechnicianName = j.AssignedTechUser != null
                    ? j.AssignedTechUser.FirstName + " " + j.AssignedTechUser.LastName
                    : null,
                CreatedAt = j.CreatedAt
            })
            .ToListAsync();

        var viewModel = new JobOrderListViewModel
        {
            SearchTerm = search,
            StatusFilter = status,
            CurrentPage = page,
            TotalCount = totalCount,
            PageSize = pageSize,
            JobOrders = items
        };

        return View(viewModel);
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> ArchiveJobOrder(long id)
    {
        if (!IsAuthorized()) return RedirectToAction("AccessDenied", "Auth", new { area = "" });

        var shopId = User.GetShopId();
        var jobOrder = await _db.JobOrders.FirstOrDefaultAsync(j => j.ShopId == shopId && j.JobOrderId == id);
        if (jobOrder == null) return NotFound();

        jobOrder.IsArchived = true;
        jobOrder.ArchivedDate = DateTime.UtcNow;
        await _db.SaveChangesAsync();

        await _audit.LogAsync(shopId, User.GetUserId(), "Archive", "JobOrder", jobOrder.JobOrderId,
            $"Archived job order {jobOrder.JobOrderNo}",
            HttpContext.Connection.RemoteIpAddress?.ToString());

        TempData["Success"] = $"Job order {jobOrder.JobOrderNo} archived successfully.";
        return RedirectToAction(nameof(Index));
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> RestoreJobOrder(long id)
    {
        if (!IsAuthorized()) return RedirectToAction("AccessDenied", "Auth", new { area = "" });

        var shopId = User.GetShopId();
        var jobOrder = await _db.JobOrders.FirstOrDefaultAsync(j => j.ShopId == shopId && j.JobOrderId == id);
        if (jobOrder == null) return NotFound();

        jobOrder.IsArchived = false;
        jobOrder.ArchivedDate = null;
        await _db.SaveChangesAsync();

        await _audit.LogAsync(shopId, User.GetUserId(), "Restore", "JobOrder", jobOrder.JobOrderId,
            $"Restored job order {jobOrder.JobOrderNo} from archive",
            HttpContext.Connection.RemoteIpAddress?.ToString());

        TempData["Success"] = $"Job order {jobOrder.JobOrderNo} restored successfully.";
        return RedirectToAction(nameof(Archive));
    }
}
