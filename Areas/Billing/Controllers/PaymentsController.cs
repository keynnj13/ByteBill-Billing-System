using ByteBill_BS.Data;
using ByteBill_BS.DTOs.Common;
using ByteBill_BS.DTOs.Payments;
using ByteBill_BS.Extensions;
using ByteBill_BS.Models.Enums;
using ByteBill_BS.Services;
using ByteBill_BS.ViewModels.Payments;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace ByteBill_BS.Areas.Billing.Controllers;

[Area("Billing")]
[Authorize]
public class PaymentsController : Controller
{
    private readonly IPaymentService _paymentService;
    private readonly ApplicationDbContext _db;

    public PaymentsController(IPaymentService paymentService, ApplicationDbContext db)
    {
        _paymentService = paymentService;
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

    [HttpGet]
    public async Task<IActionResult> Index(string? search, PaymentMethod? method, int page = 1)
    {
        if (!IsAuthorized()) return RedirectToAction("AccessDenied", "Auth", new { area = "" });

        var shopId = User.GetShopId();
        var result = await _paymentService.GetListAsync(shopId, new PaymentPagedRequest
        {
            Page = page,
            PageSize = 10,
            Search = search
        });

        var metrics = await _paymentService.GetMetricsAsync(shopId);

        var viewModel = new PaymentListViewModel
        {
            SearchTerm = search,
            MethodFilter = method,
            CurrentPage = result.Page,
            TotalCount = result.TotalCount,
            PageSize = result.PageSize,
            TotalReceived = metrics.TotalReceived,
            TodayReceived = metrics.TodayReceived,
            Payments = result.Items.Select(p =>
            {
                _ = Enum.TryParse<PaymentMethod>(p.Method, true, out var parsedMethod);
                _ = Enum.TryParse<PaymentStatus>(p.Status, true, out var parsedStatus);
                return new PaymentItemViewModel
                {
                    Id = p.PaymentId,
                    CustomerName = p.CustomerName,
                    CustomerInitials = GetInitials(p.CustomerName),
                    InvoiceNumber = p.InvoiceNo,
                    Method = parsedMethod,
                    Amount = p.Amount,
                    PaidAt = p.PaymentDate,
                    PaymentDate = p.PaymentDate,
                    ReceivedByName = p.ReceivedByName,
                    ReceivedBy = p.ReceivedByName,
                    ReferenceNo = p.ReferenceNo,
                    ReferenceNumber = p.ReferenceNo,
                    Status = parsedStatus,
                    IsVoid = parsedStatus == PaymentStatus.Refunded
                };
            }).ToList()
        };

        if (method.HasValue)
        {
            viewModel.Payments = viewModel.Payments
                .Where(p => p.Method == method.Value).ToList();
        }

        return View(viewModel);
    }

    [HttpGet]
    public async Task<IActionResult> Create(long invoiceId)
    {
        if (!IsAuthorized()) return RedirectToAction("AccessDenied", "Auth", new { area = "" });

        var shopId = User.GetShopId();
        var invoice = await _db.Invoices
            .Include(i => i.Customer)
            .Where(i => i.ShopId == shopId && i.InvoiceId == invoiceId)
            .AsNoTracking()
            .FirstOrDefaultAsync();

        if (invoice == null) return NotFound();

        var model = new PaymentCreateViewModel
        {
            InvoiceId = invoiceId,
            InvoiceNumber = invoice.InvoiceNo,
            CustomerName = $"{invoice.Customer?.FirstName} {invoice.Customer?.LastName}".Trim(),
            InvoiceBalance = invoice.Balance,
            Amount = invoice.Balance,
            Method = PaymentMethod.Cash
        };

        return View(model);
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Create(PaymentCreateViewModel model)
    {
        if (!IsAuthorized()) return RedirectToAction("AccessDenied", "Auth", new { area = "" });

        if (!ModelState.IsValid) return View(model);

        var shopId = User.GetShopId();
        var userId = User.GetUserId();

        var invoice = await _db.Invoices
            .Where(i => i.ShopId == shopId && i.InvoiceId == model.InvoiceId)
            .AsNoTracking()
            .FirstOrDefaultAsync();

        if (invoice == null)
        {
            ModelState.AddModelError("", "Invoice not found.");
            return View(model);
        }

        if (model.Amount > invoice.Balance)
        {
            ModelState.AddModelError("Amount", $"Amount cannot exceed invoice balance of {invoice.Balance:F2}.");
            return View(model);
        }

        var request = new RecordPaymentRequest
        {
            CustomerId = invoice.CustomerId,
            Amount = model.Amount,
            Method = model.Method.ToString(),
            ReferenceNo = model.ReferenceNumber,
            Notes = model.Notes,
            Allocations = new List<PaymentAllocationRequestDto>
            {
                new() { InvoiceId = model.InvoiceId, AmountApplied = model.Amount }
            }
        };

        var result = await _paymentService.RecordPaymentAsync(shopId, userId, request);
        if (!result.Success)
        {
            ModelState.AddModelError("", result.Message ?? "Failed to record payment.");
            return View(model);
        }

        TempData["Success"] = "Payment recorded successfully!";
        return RedirectToAction(nameof(Index));
    }

    [HttpGet]
    public async Task<IActionResult> Details(long id)
    {
        if (!IsAuthorized()) return RedirectToAction("AccessDenied", "Auth", new { area = "" });

        var shopId = User.GetShopId();
        var dto = await _paymentService.GetDetailAsync(shopId, id);
        if (dto == null) return NotFound();

        _ = Enum.TryParse<PaymentMethod>(dto.Method, true, out var parsedMethod);
        _ = Enum.TryParse<PaymentStatus>(dto.Status, true, out var parsedStatus);

        var customer = await _db.Customers
            .Where(c => c.CustomerId == dto.CustomerId)
            .AsNoTracking()
            .FirstOrDefaultAsync();

        var model = new PaymentDetailViewModel
        {
            Id = dto.PaymentId,
            CustomerId = dto.CustomerId,
            CustomerName = dto.CustomerName,
            CustomerEmail = customer?.Email,
            CustomerPhone = customer?.Phone ?? "",
            Method = parsedMethod,
            Amount = dto.Amount,
            ReferenceNo = dto.ReferenceNo,
            ReferenceNumber = dto.ReferenceNo,
            PaidAt = dto.PaymentDate,
            PaymentDate = dto.PaymentDate,
            ReceivedByName = dto.ReceivedByName,
            ReceivedBy = dto.ReceivedByName,
            Status = parsedStatus,
            IsVoid = parsedStatus == PaymentStatus.Refunded,
            Notes = dto.Notes,
            InvoiceId = dto.Allocations.FirstOrDefault()?.InvoiceId,
            InvoiceNumber = dto.Allocations.FirstOrDefault()?.InvoiceNo,
            Allocations = dto.Allocations.Select(a => new PaymentAllocationItem
            {
                InvoiceId = a.InvoiceId,
                InvoiceNumber = a.InvoiceNo,
                AmountApplied = a.AmountApplied
            }).ToList()
        };

        return View(model);
    }
}
