using ByteBill_BS.Data;
using ByteBill_BS.Extensions;
using ByteBill_BS.Models.Enums;
using ByteBill_BS.ViewModels.Invoices;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace ByteBill_BS.Areas.Auditor.Controllers;

[Area("Auditor")]
[Authorize]
public class InvoicesController : Controller
{
    private readonly ApplicationDbContext _db;
    public InvoicesController(ApplicationDbContext db) => _db = db;

    private bool IsAuthorized()
    {
        var roleClaim = User.Claims.FirstOrDefault(c => c.Type == "Role")?.Value;
        return roleClaim == UserRole.Auditor.ToString();
    }

    [HttpGet]
    public async Task<IActionResult> Index(string? search, InvoiceStatus? status, int page = 1)
    {
        if (!IsAuthorized()) return RedirectToAction("AccessDenied", "Auth", new { area = "" });
        var shopId = User.GetShopId();
        const int pageSize = 20;

        var query = _db.Invoices
            .Where(i => i.ShopId == shopId && !i.IsArchived)
            .Include(i => i.Customer)
            .Include(i => i.JobOrder)
            .AsQueryable();

        if (status.HasValue)
            query = query.Where(i => i.Status == status.Value);

        if (!string.IsNullOrWhiteSpace(search))
            query = query.Where(i => i.InvoiceNo.Contains(search) || i.Customer!.FirstName.Contains(search) || i.Customer.LastName.Contains(search));

        var totalCount = await query.CountAsync();
        var totalOutstanding = await query.Where(i => i.Status != InvoiceStatus.Void).SumAsync(i => (decimal?)i.Balance) ?? 0;
        var overdueCount = await query.CountAsync(i => i.DueDate.HasValue && i.DueDate.Value < DateTime.UtcNow && i.Status != InvoiceStatus.Paid && i.Status != InvoiceStatus.Void);

        var invoices = await query
            .OrderByDescending(i => i.CreatedAt)
            .Skip((page - 1) * pageSize).Take(pageSize)
            .Select(i => new InvoiceItemViewModel
            {
                Id = i.InvoiceId,
                InvoiceNumber = i.InvoiceNo,
                CustomerName = i.Customer!.FirstName + " " + i.Customer.LastName,
                CustomerInitials = (i.Customer.FirstName.Substring(0, 1) + i.Customer.LastName.Substring(0, 1)).ToUpper(),
                JobNumber = i.JobOrder != null ? i.JobOrder.JobOrderNo : null,
                Status = i.Status,
                Subtotal = i.Subtotal,
                DiscountAmount = i.DiscountAmount,
                TotalAdjustments = i.TotalAdjustments,
                Total = i.TotalAmount,
                AmountPaid = i.AmountPaid,
                Balance = i.Balance,
                CreatedAt = i.CreatedAt,
                DueDate = i.DueDate
            })
            .ToListAsync();

        var viewModel = new InvoiceListViewModel
        {
            SearchTerm = search,
            StatusFilter = status,
            CurrentPage = page,
            PageSize = pageSize,
            TotalCount = totalCount,
            TotalOutstanding = totalOutstanding,
            OverdueCount = overdueCount,
            Invoices = invoices
        };

        return View(viewModel);
    }

    [HttpGet]
    public async Task<IActionResult> Details(long id)
    {
        if (!IsAuthorized()) return RedirectToAction("AccessDenied", "Auth", new { area = "" });
        var shopId = User.GetShopId();

        var inv = await _db.Invoices
            .AsNoTracking()
            .Where(i => i.InvoiceId == id && i.ShopId == shopId)
            .Include(i => i.Customer)
            .Include(i => i.JobOrder)
            .Include(i => i.InvoiceLines)
            .Include(i => i.InvoiceDiscounts)
            .Include(i => i.PaymentAllocations).ThenInclude(pa => pa.Payment).ThenInclude(p => p!.ReceivedByUser)
            .Include(i => i.Shop)
            .FirstOrDefaultAsync();

        if (inv == null) return NotFound();

        var model = new InvoiceDetailViewModel
        {
            Id = inv.InvoiceId,
            InvoiceNumber = inv.InvoiceNo,
            Status = inv.Status,
            CustomerId = inv.CustomerId,
            CustomerName = inv.Customer!.FirstName + " " + inv.Customer.LastName,
            CustomerEmail = inv.Customer.Email,
            CustomerPhone = inv.Customer.Phone ?? "",
            CustomerAddress = inv.Customer.Address,
            JobOrderId = inv.JobOrderId,
            JobOrderNumber = inv.JobOrder?.JobOrderNo ?? "",
            JobNumber = inv.JobOrder?.JobOrderNo ?? "",
            ShopName = inv.Shop?.ShopName ?? "",
            ShopAddress = inv.Shop?.Address,
            ShopPhone = inv.Shop?.Phone,
            ShopEmail = inv.Shop?.Email,
            CreatedAt = inv.CreatedAt,
            DueDate = inv.DueDate,
            Subtotal = inv.Subtotal,
            TotalAdjustments = inv.TotalAdjustments,
            DiscountAmount = inv.DiscountAmount,
            VatableSales = inv.VatableSales,
            VatExemptSales = inv.VatExemptSales,
            ZeroRatedSales = inv.ZeroRatedSales,
            VatAmount = inv.VatAmount,
            IsVatRegistered = inv.Shop?.IsVatRegistered ?? true,
            ShopTIN = inv.Shop?.TIN,
            Total = inv.TotalAmount,
            AmountPaid = inv.AmountPaid,
            Balance = inv.Balance,
            Discounts = inv.InvoiceDiscounts.Select(d => new InvoiceDiscountViewModel
            {
                InvoiceDiscountId = d.InvoiceDiscountId,
                DiscountType = d.DiscountType.ToString(),
                Label = d.Label,
                Percentage = d.Percentage,
                Amount = d.Amount,
                IsVatExempt = d.IsVatExempt,
                BeneficiaryIdNo = d.BeneficiaryIdNo,
                BeneficiaryName = d.BeneficiaryName,
                AppliedAt = d.AppliedAt
            }).ToList(),
            LineItems = inv.InvoiceLines.Select(l => new InvoiceLineItemViewModel
            {
                Id = l.InvoiceLineId,
                Description = l.Description,
                Quantity = l.Qty,
                UnitPrice = l.UnitPrice,
                Total = l.LineTotal,
                Type = l.LineType
            }).ToList(),
            Payments = inv.PaymentAllocations.Select(pa => new PaymentSummaryViewModel
            {
                Id = pa.PaymentId,
                PaymentNumber = pa.Payment?.PaymentNo ?? "",
                Amount = pa.AmountApplied,
                Method = pa.Payment?.Method ?? PaymentMethod.Cash,
                PaidAt = pa.Payment?.PaymentDate ?? DateTime.UtcNow,
                IsVoid = pa.Payment?.Status == PaymentStatus.Refunded || pa.Payment?.Status == PaymentStatus.Failed,
                ReceivedBy = pa.Payment?.ReceivedByUser != null ? pa.Payment.ReceivedByUser.FirstName + " " + pa.Payment.ReceivedByUser.LastName : null
            }).ToList()
        };

        return View(model);
    }
}
