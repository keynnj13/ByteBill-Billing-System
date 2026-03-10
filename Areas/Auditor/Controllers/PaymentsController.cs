using ByteBill_BS.Data;
using ByteBill_BS.Extensions;
using ByteBill_BS.Models.Enums;
using ByteBill_BS.ViewModels.Payments;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace ByteBill_BS.Areas.Auditor.Controllers;

[Area("Auditor")]
[Authorize]
public class PaymentsController : Controller
{
    private readonly ApplicationDbContext _db;
    public PaymentsController(ApplicationDbContext db) => _db = db;

    private bool IsAuthorized()
    {
        var roleClaim = User.Claims.FirstOrDefault(c => c.Type == "Role")?.Value;
        return roleClaim == UserRole.Auditor.ToString();
    }

    [HttpGet]
    public async Task<IActionResult> Index(string? search, PaymentMethod? method, int page = 1)
    {
        if (!IsAuthorized()) return RedirectToAction("AccessDenied", "Auth", new { area = "" });
        var shopId = User.GetShopId();
        const int pageSize = 20;

        var query = _db.Payments
            .Where(p => p.ShopId == shopId)
            .Include(p => p.Customer)
            .Include(p => p.ReceivedByUser)
            .Include(p => p.PaymentAllocations).ThenInclude(pa => pa.Invoice)
            .AsQueryable();

        if (method.HasValue)
            query = query.Where(p => p.Method == method.Value);

        if (!string.IsNullOrWhiteSpace(search))
            query = query.Where(p => p.PaymentNo.Contains(search) || p.Customer!.FirstName.Contains(search) || p.Customer.LastName.Contains(search));

        var totalCount = await query.CountAsync();
        var totalReceived = await query
            .Where(p => p.Status == PaymentStatus.Confirmed)
            .SumAsync(p => (decimal?)p.Amount) ?? 0;
        var todayReceived = await query
            .Where(p => p.Status == PaymentStatus.Confirmed && p.PaymentDate.Date == DateTime.UtcNow.Date)
            .SumAsync(p => (decimal?)p.Amount) ?? 0;

        var payments = await query
            .OrderByDescending(p => p.PaymentDate)
            .Skip((page - 1) * pageSize).Take(pageSize)
            .Select(p => new PaymentItemViewModel
            {
                Id = p.PaymentId,
                PaymentNumber = p.PaymentNo,
                CustomerName = p.Customer!.FirstName + " " + p.Customer.LastName,
                CustomerInitials = (p.Customer.FirstName.Substring(0, 1) + p.Customer.LastName.Substring(0, 1)).ToUpper(),
                InvoiceNumber = p.PaymentAllocations.Any() ? p.PaymentAllocations.First().Invoice!.InvoiceNo : null,
                InvoiceId = p.PaymentAllocations.Any() ? p.PaymentAllocations.First().InvoiceId : (long?)null,
                Method = p.Method,
                Amount = p.Amount,
                PaidAt = p.PaymentDate,
                PaymentDate = p.PaymentDate,
                ReferenceNo = p.ReferenceNo,
                ReceivedByName = p.ReceivedByUser != null ? p.ReceivedByUser.FirstName + " " + p.ReceivedByUser.LastName : null,
                Status = p.Status,
                IsVoid = p.Status == PaymentStatus.Refunded ? true : p.Status == PaymentStatus.Failed ? true : false
            })
            .ToListAsync();

        var viewModel = new PaymentListViewModel
        {
            SearchTerm = search,
            MethodFilter = method,
            CurrentPage = page,
            PageSize = pageSize,
            TotalCount = totalCount,
            TotalReceived = totalReceived,
            TodayReceived = todayReceived,
            Payments = payments
        };

        return View(viewModel);
    }

    [HttpGet]
    public async Task<IActionResult> Details(long id)
    {
        if (!IsAuthorized()) return RedirectToAction("AccessDenied", "Auth", new { area = "" });
        var shopId = User.GetShopId();

        var p = await _db.Payments
            .AsNoTracking()
            .Where(p => p.PaymentId == id && p.ShopId == shopId)
            .Include(p => p.Customer)
            .Include(p => p.ReceivedByUser)
            .Include(p => p.PaymentAllocations).ThenInclude(pa => pa.Invoice)
            .FirstOrDefaultAsync();

        if (p == null) return NotFound();

        var model = new PaymentDetailViewModel
        {
            Id = p.PaymentId,
            PaymentNumber = p.PaymentNo,
            CustomerId = p.CustomerId,
            CustomerName = p.Customer!.FirstName + " " + p.Customer.LastName,
            CustomerEmail = p.Customer.Email,
            CustomerPhone = p.Customer.Phone ?? "",
            InvoiceId = p.PaymentAllocations.FirstOrDefault()?.InvoiceId,
            InvoiceNumber = p.PaymentAllocations.FirstOrDefault()?.Invoice?.InvoiceNo,
            Method = p.Method,
            Amount = p.Amount,
            ReferenceNo = p.ReferenceNo,
            PaidAt = p.PaymentDate,
            PaymentDate = p.PaymentDate,
            ReceivedByName = p.ReceivedByUser != null ? p.ReceivedByUser.FirstName + " " + p.ReceivedByUser.LastName : null,
            Status = p.Status,
            IsVoid = p.Status == PaymentStatus.Refunded || p.Status == PaymentStatus.Failed,
            Notes = p.Notes,
            Allocations = p.PaymentAllocations.Select(pa => new PaymentAllocationItem
            {
                InvoiceId = pa.InvoiceId,
                InvoiceNumber = pa.Invoice?.InvoiceNo ?? "",
                AmountApplied = pa.AmountApplied
            }).ToList()
        };

        return View(model);
    }

    [HttpGet]
    public async Task<IActionResult> DetailsModal(long id)
    {
        if (!IsAuthorized()) return Forbid();
        var shopId = User.GetShopId();

        var p = await _db.Payments
            .AsNoTracking()
            .Where(p => p.PaymentId == id && p.ShopId == shopId)
            .Include(p => p.Customer)
            .Include(p => p.ReceivedByUser)
            .Include(p => p.PaymentAllocations).ThenInclude(pa => pa.Invoice)
            .FirstOrDefaultAsync();

        if (p == null) return NotFound();

        var model = new PaymentDetailViewModel
        {
            Id = p.PaymentId,
            PaymentNumber = p.PaymentNo,
            CustomerId = p.CustomerId,
            CustomerName = p.Customer!.FirstName + " " + p.Customer.LastName,
            CustomerEmail = p.Customer.Email,
            CustomerPhone = p.Customer.Phone ?? "",
            InvoiceId = p.PaymentAllocations.FirstOrDefault()?.InvoiceId,
            InvoiceNumber = p.PaymentAllocations.FirstOrDefault()?.Invoice?.InvoiceNo,
            Method = p.Method,
            Amount = p.Amount,
            ReferenceNo = p.ReferenceNo,
            ReferenceNumber = p.ReferenceNo,
            PaidAt = p.PaymentDate,
            PaymentDate = p.PaymentDate,
            ReceivedByName = p.ReceivedByUser != null ? p.ReceivedByUser.FirstName + " " + p.ReceivedByUser.LastName : null,
            Status = p.Status,
            IsVoid = p.Status == PaymentStatus.Refunded || p.Status == PaymentStatus.Failed,
            Notes = p.Notes,
            Allocations = p.PaymentAllocations.Select(pa => new PaymentAllocationItem
            {
                InvoiceId = pa.InvoiceId,
                InvoiceNumber = pa.Invoice?.InvoiceNo ?? "",
                AmountApplied = pa.AmountApplied
            }).ToList()
        };

        return PartialView("_DetailsModal", model);
    }
}
