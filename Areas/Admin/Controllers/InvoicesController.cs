using ByteBill_BS.Data;
using ByteBill_BS.DTOs.Common;
using ByteBill_BS.DTOs.Invoices;
using ByteBill_BS.Extensions;
using ByteBill_BS.Models.Enums;
using ByteBill_BS.Services;
using ByteBill_BS.ViewModels.Invoices;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace ByteBill_BS.Areas.Admin.Controllers;

[Area("Admin")]
[Authorize]
public class InvoicesController : Controller
{
    private readonly IInvoiceService _invoiceService;
    private readonly ApplicationDbContext _db;
    private readonly IAuditService _audit;

    public InvoicesController(IInvoiceService invoiceService, ApplicationDbContext db, IAuditService audit)
    {
        _invoiceService = invoiceService;
        _db = db;
        _audit = audit;
    }

    private bool IsAuthorized() => User.IsInRoles("Admin", "SuperAdmin");

    private static string GetInitials(string name)
    {
        var parts = name.Split(' ', StringSplitOptions.RemoveEmptyEntries);
        return parts.Length >= 2
            ? $"{parts[0][0]}{parts[^1][0]}".ToUpper()
            : (parts.Length == 1 ? parts[0][..1].ToUpper() : "??");
    }

    // ═══════════════════════════════════════════════════════════════════
    //  INDEX
    // ═══════════════════════════════════════════════════════════════════
    [HttpGet]
    public async Task<IActionResult> Index(string? search, InvoiceStatus? status, int page = 1)
    {
        if (!IsAuthorized()) return RedirectToAction("AccessDenied", "Auth", new { area = "" });

        var shopId = User.GetShopId();

        var result = await _invoiceService.GetListAsync(shopId, new InvoicePagedRequest
        {
            Page = page,
            PageSize = 10,
            Search = search,
            StatusFilter = status?.ToString()
        });

        var metrics = await _invoiceService.GetMetricsAsync(shopId);

        var viewModel = new InvoiceListViewModel
        {
            SearchTerm = search,
            StatusFilter = status,
            CurrentPage = result.Page,
            TotalCount = result.TotalCount,
            PageSize = result.PageSize,
            TotalOutstanding = metrics.Outstanding,
            OverdueCount = metrics.Overdue,
            Invoices = result.Items.Select(i =>
            {
                _ = Enum.TryParse<InvoiceStatus>(i.Status, true, out var parsedStatus);
                return new InvoiceItemViewModel
                {
                    Id = i.InvoiceId,
                    InvoiceNumber = i.InvoiceNo,
                    CustomerName = i.CustomerName,
                    CustomerInitials = GetInitials(i.CustomerName),
                    JobNumber = i.JobOrderNo,
                    Status = parsedStatus,
                    Total = i.TotalAmount,
                    AmountPaid = i.AmountPaid,
                    Balance = i.Balance,
                    CreatedAt = i.InvoiceDate,
                    DueDate = i.DueDate
                };
            }).ToList()
        };

        return View(viewModel);
    }

    // ═══════════════════════════════════════════════════════════════════
    //  CREATE (from Job Order)
    // ═══════════════════════════════════════════════════════════════════
    [HttpGet]
    public async Task<IActionResult> Create(long jobOrderId)
    {
        if (!IsAuthorized()) return RedirectToAction("AccessDenied", "Auth", new { area = "" });

        var model = await BuildCreateModelAsync(jobOrderId);
        if (model == null) return NotFound();
        return View(model);
    }

    [HttpGet]
    public async Task<IActionResult> CreateModal(long jobOrderId = 0)
    {
        if (!IsAuthorized()) return Forbid();

        var model = await BuildCreateModelAsync(jobOrderId);
        if (model == null) return NotFound();
        return PartialView("_CreateModal", model);
    }

    private async Task<InvoiceCreateViewModel?> BuildCreateModelAsync(long jobOrderId)
    {
        var shopId = User.GetShopId();

        if (jobOrderId <= 0)
        {
            // No job order selected yet – return model with available job orders for selection
            var availableJobOrders = await _db.JobOrders
                .Include(j => j.Customer)
                .Where(j => j.ShopId == shopId &&
                       (j.Status == Models.Enums.JobOrderStatus.Completed) &&
                       !_db.Invoices.Any(i => i.JobOrderId == j.JobOrderId && i.Status != Models.Enums.InvoiceStatus.Void))
                .OrderByDescending(j => j.CreatedAt)
                .Select(j => new AvailableJobOrderOption
                {
                    JobOrderId = j.JobOrderId,
                    JobNumber = j.JobOrderNo,
                    CustomerName = j.Customer!.FirstName + " " + j.Customer.LastName,
                    DeviceSummary = j.Device != null ? j.Device.DeviceType : ""
                })
                .ToListAsync();

            return new InvoiceCreateViewModel
            {
                DueDate = DateTime.Now.AddDays(14),
                AvailableJobOrders = availableJobOrders
            };
        }

        var jobOrder = await _db.JobOrders
            .Include(j => j.Customer)
            .Include(j => j.JobOrderServices).ThenInclude(s => s.Service)
            .Include(j => j.JobOrderParts).ThenInclude(p => p.Item)
            .Where(j => j.ShopId == shopId && j.JobOrderId == jobOrderId)
            .AsNoTracking()
            .FirstOrDefaultAsync();

        if (jobOrder == null) return null;

        var lines = new List<InvoiceLineItemFormViewModel>();
        foreach (var svc in jobOrder.JobOrderServices)
        {
            lines.Add(new InvoiceLineItemFormViewModel
            {
                Description = svc.Service?.ServiceName ?? $"Service #{svc.ServiceId}",
                Quantity = svc.Qty,
                UnitPrice = svc.UnitPrice
            });
        }
        foreach (var part in jobOrder.JobOrderParts)
        {
            lines.Add(new InvoiceLineItemFormViewModel
            {
                Description = part.Item?.ItemName ?? $"Part #{part.ItemId}",
                Quantity = part.QtyUsed,
                UnitPrice = part.UnitPrice
            });
        }

        return new InvoiceCreateViewModel
        {
            JobOrderId = jobOrderId,
            JobNumber = jobOrder.JobOrderNo,
            CustomerName = $"{jobOrder.Customer?.FirstName} {jobOrder.Customer?.LastName}".Trim(),
            CustomerId = jobOrder.CustomerId,
            DueDate = DateTime.Now.AddDays(14),
            LineItems = lines
        };
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Create(InvoiceCreateViewModel model)
    {
        if (!IsAuthorized()) return RedirectToAction("AccessDenied", "Auth", new { area = "" });

        if (!ModelState.IsValid)
        {
            if (Request.Headers["X-Requested-With"] == "XMLHttpRequest")
                return PartialView("_CreateModal", model);
            return View(model);
        }

        var shopId = User.GetShopId();
        var userId = User.GetUserId();

        var request = new CreateInvoiceRequest
        {
            JobOrderId = model.JobOrderId,
            DueDate = model.DueDate
        };

        var result = await _invoiceService.CreateFromJobOrderAsync(shopId, userId, request);
        if (!result.Success)
        {
            ModelState.AddModelError("", result.Message ?? "Failed to create invoice.");
            if (Request.Headers["X-Requested-With"] == "XMLHttpRequest")
                return PartialView("_CreateModal", model);
            return View(model);
        }

        TempData["Success"] = "Invoice created successfully!";
        if (Request.Headers["X-Requested-With"] == "XMLHttpRequest")
            return Json(new { success = true, message = "Invoice created successfully!", id = result.Data?.InvoiceId });
        return RedirectToAction(nameof(Index));
    }

    // ═══════════════════════════════════════════════════════════════════
    //  DETAILS
    // ═══════════════════════════════════════════════════════════════════
    [HttpGet]
    public async Task<IActionResult> Details(long id)
    {
        if (!IsAuthorized()) return RedirectToAction("AccessDenied", "Auth", new { area = "" });

        var vm = await GetInvoiceDetailAsync(id);
        if (vm == null) return NotFound();
        return View(vm);
    }

    [HttpGet]
    public async Task<IActionResult> DetailsModal(long id)
    {
        if (!IsAuthorized()) return Forbid();

        var vm = await GetInvoiceDetailAsync(id);
        if (vm == null) return NotFound();
        return PartialView("_DetailsModal", vm);
    }

    private async Task<InvoiceDetailViewModel?> GetInvoiceDetailAsync(long id)
    {
        var shopId = User.GetShopId();
        var dto = await _invoiceService.GetDetailAsync(shopId, id);
        if (dto == null) return null;

        _ = Enum.TryParse<InvoiceStatus>(dto.Status, true, out var parsedStatus);

        // Get customer details
        var customer = await _db.Customers
            .Where(c => c.CustomerId == dto.CustomerId)
            .AsNoTracking()
            .FirstOrDefaultAsync();

        // Get shop details
        var shop = await _db.Shops
            .Where(s => s.ShopId == shopId)
            .AsNoTracking()
            .FirstOrDefaultAsync();

        return new InvoiceDetailViewModel
        {
            Id = dto.InvoiceId,
            InvoiceNumber = dto.InvoiceNo,
            CustomerId = dto.CustomerId,
            CustomerName = dto.CustomerName,
            CustomerEmail = customer?.Email,
            CustomerPhone = customer?.Phone ?? "",
            CustomerAddress = customer?.Address,
            JobOrderId = dto.JobOrderId,
            JobNumber = dto.JobOrderNo,
            JobOrderNumber = dto.JobOrderNo,
            ShopName = shop?.ShopName ?? "",
            ShopAddress = shop?.Address,
            ShopPhone = shop?.Phone,
            ShopEmail = shop?.Email,
            Status = parsedStatus,
            Subtotal = dto.Subtotal,
            TotalAdjustments = dto.TotalAdjustments,
            Total = dto.TotalAmount,
            AmountPaid = dto.AmountPaid,
            Balance = dto.Balance,
            CreatedAt = dto.InvoiceDate,
            IssuedAt = dto.InvoiceDate,
            DueDate = dto.DueDate,
            LineItems = dto.Lines.Select(l => new InvoiceLineItemViewModel
            {
                Id = l.InvoiceLineId,
                Description = l.Description,
                Quantity = l.Qty,
                UnitPrice = l.UnitPrice,
                Total = l.LineTotal,
                Type = l.LineType
            }).ToList(),
            Payments = dto.Payments.Select(p =>
            {
                _ = Enum.TryParse<PaymentMethod>(p.Method, true, out var pm);
                return new PaymentSummaryViewModel
                {
                    Id = p.PaymentId,
                    Amount = p.AmountApplied,
                    Method = pm,
                    PaidAt = p.PaymentDate,
                    IsVoid = false
                };
            }).ToList()
        };
    }

    // ═══════════════════════════════════════════════════════════════════
    //  ARCHIVE
    // ═══════════════════════════════════════════════════════════════════
    [HttpGet]
    public async Task<IActionResult> Archive(string? search, InvoiceStatus? status, int page = 1)
    {
        if (!IsAuthorized()) return RedirectToAction("AccessDenied", "Auth", new { area = "" });

        var shopId = User.GetShopId();
        var query = _db.Invoices
            .Where(i => i.ShopId == shopId && i.IsArchived)
            .AsNoTracking();

        if (status.HasValue)
            query = query.Where(i => i.Status == status.Value);

        if (!string.IsNullOrWhiteSpace(search))
        {
            var term = search.Trim().ToLower();
            query = query.Where(i =>
                i.InvoiceNo.ToLower().Contains(term) ||
                (i.Customer!.FirstName + " " + i.Customer.LastName).ToLower().Contains(term));
        }

        var totalCount = await query.CountAsync();
        var pageSize = 10;
        var items = await query
            .OrderByDescending(i => i.ArchivedDate)
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .Select(i => new InvoiceItemViewModel
            {
                Id = i.InvoiceId,
                InvoiceNumber = i.InvoiceNo,
                CustomerName = i.Customer!.FirstName + " " + i.Customer.LastName,
                CustomerInitials = GetInitials(i.Customer!.FirstName + " " + i.Customer.LastName),
                JobNumber = i.JobOrder!.JobOrderNo,
                Status = i.Status,
                Total = i.TotalAmount,
                AmountPaid = i.AmountPaid,
                Balance = i.Balance,
                CreatedAt = i.InvoiceDate,
                DueDate = i.DueDate
            })
            .ToListAsync();

        var viewModel = new InvoiceListViewModel
        {
            SearchTerm = search,
            StatusFilter = status,
            CurrentPage = page,
            TotalCount = totalCount,
            PageSize = pageSize,
            Invoices = items
        };

        return View(viewModel);
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> ArchiveInvoice(long id)
    {
        if (!IsAuthorized()) return RedirectToAction("AccessDenied", "Auth", new { area = "" });

        var shopId = User.GetShopId();
        var invoice = await _db.Invoices.FirstOrDefaultAsync(i => i.ShopId == shopId && i.InvoiceId == id);
        if (invoice == null) return NotFound();

        invoice.IsArchived = true;
        invoice.ArchivedDate = DateTime.UtcNow;
        await _db.SaveChangesAsync();

        await _audit.LogAsync(shopId, User.GetUserId(), "Archive", "Invoice", invoice.InvoiceId,
            $"Archived invoice {invoice.InvoiceNo}",
            HttpContext.Connection.RemoteIpAddress?.ToString());

        TempData["Success"] = $"Invoice {invoice.InvoiceNo} archived successfully.";
        return RedirectToAction(nameof(Index));
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> RestoreInvoice(long id)
    {
        if (!IsAuthorized()) return RedirectToAction("AccessDenied", "Auth", new { area = "" });

        var shopId = User.GetShopId();
        var invoice = await _db.Invoices.FirstOrDefaultAsync(i => i.ShopId == shopId && i.InvoiceId == id);
        if (invoice == null) return NotFound();

        invoice.IsArchived = false;
        invoice.ArchivedDate = null;
        await _db.SaveChangesAsync();

        await _audit.LogAsync(shopId, User.GetUserId(), "Restore", "Invoice", invoice.InvoiceId,
            $"Restored invoice {invoice.InvoiceNo} from archive",
            HttpContext.Connection.RemoteIpAddress?.ToString());

        TempData["Success"] = $"Invoice {invoice.InvoiceNo} restored successfully.";
        return RedirectToAction(nameof(Archive));
    }
}
