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

namespace ByteBill_BS.Areas.Billing.Controllers;

[Area("Billing")]
[Authorize]
public class InvoicesController : Controller
{
    private readonly IInvoiceService _invoiceService;
    private readonly ApplicationDbContext _db;

    public InvoicesController(IInvoiceService invoiceService, ApplicationDbContext db)
    {
        _invoiceService = invoiceService;
        _db = db;
    }

    private bool IsAuthorized() => User.IsInRoles("Billing", "Admin", "SuperAdmin");

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

        var customer = await _db.Customers
            .Where(c => c.CustomerId == dto.CustomerId)
            .AsNoTracking()
            .FirstOrDefaultAsync();

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
}
