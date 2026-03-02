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
using QuestPDF.Fluent;
using QuestPDF.Helpers;
using QuestPDF.Infrastructure;

namespace ByteBill_BS.Areas.Billing.Controllers;

[Area("Billing")]
[Authorize]
public class PaymentsController : Controller
{
    private readonly IPaymentService _paymentService;
    private readonly ApplicationDbContext _db;
    private readonly IXeroService _xero;

    public PaymentsController(IPaymentService paymentService, ApplicationDbContext db, IXeroService xero)
    {
        _paymentService = paymentService;
        _db = db;
        _xero = xero;
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
                    PaymentNumber = p.PaymentNo,
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

    // ═══════════════════════════════════════════════════════════════════
    //  CREATE (Record Payment)
    // ═══════════════════════════════════════════════════════════════════
    [HttpGet]
    public async Task<IActionResult> Create(long invoiceId)
    {
        if (!IsAuthorized()) return RedirectToAction("AccessDenied", "Auth", new { area = "" });

        var model = await BuildCreateModelAsync(invoiceId);
        if (model == null) return NotFound();
        return View(model);
    }

    [HttpGet]
    public async Task<IActionResult> CreateModal(long invoiceId = 0)
    {
        if (!IsAuthorized()) return Forbid();

        var model = await BuildCreateModelAsync(invoiceId);
        if (model == null) return NotFound();
        return PartialView("_CreateModal", model);
    }

    private async Task<PaymentCreateViewModel?> BuildCreateModelAsync(long invoiceId)
    {
        var shopId = User.GetShopId();

        if (invoiceId <= 0)
        {
            var unpaidInvoices = await _db.Invoices
                .Include(i => i.Customer)
                .Where(i => i.ShopId == shopId &&
                       (i.Status == InvoiceStatus.Unpaid || i.Status == InvoiceStatus.Partial))
                .OrderByDescending(i => i.CreatedAt)
                .Select(i => new AvailableInvoiceOption
                {
                    InvoiceId = i.InvoiceId,
                    InvoiceNumber = i.InvoiceNo,
                    CustomerName = i.Customer!.FirstName + " " + i.Customer.LastName,
                    Balance = i.Balance
                })
                .ToListAsync();

            return new PaymentCreateViewModel
            {
                Method = PaymentMethod.Cash,
                AvailableInvoices = unpaidInvoices
            };
        }

        var invoice = await _db.Invoices
            .Include(i => i.Customer)
            .Where(i => i.ShopId == shopId && i.InvoiceId == invoiceId)
            .AsNoTracking()
            .FirstOrDefaultAsync();

        if (invoice == null) return null;

        return new PaymentCreateViewModel
        {
            InvoiceId = invoiceId,
            InvoiceNumber = invoice.InvoiceNo,
            CustomerName = $"{invoice.Customer?.FirstName} {invoice.Customer?.LastName}".Trim(),
            InvoiceBalance = invoice.Balance,
            Amount = invoice.Balance,
            Method = PaymentMethod.Cash
        };
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Create(PaymentCreateViewModel model)
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

        var invoice = await _db.Invoices
            .Where(i => i.ShopId == shopId && i.InvoiceId == model.InvoiceId)
            .AsNoTracking()
            .FirstOrDefaultAsync();

        if (invoice == null)
        {
            ModelState.AddModelError("", "Invoice not found.");
            if (Request.Headers["X-Requested-With"] == "XMLHttpRequest")
                return PartialView("_CreateModal", model);
            return View(model);
        }

        if (model.Amount > invoice.Balance)
        {
            ModelState.AddModelError("Amount", $"Amount cannot exceed invoice balance of {invoice.Balance:F2}.");
            if (Request.Headers["X-Requested-With"] == "XMLHttpRequest")
                return PartialView("_CreateModal", model);
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
            if (Request.Headers["X-Requested-With"] == "XMLHttpRequest")
                return PartialView("_CreateModal", model);
            return View(model);
        }

        // Auto-sync payment to Xero
        if (result.Data?.PaymentId > 0)
        {
            var userId2 = User.GetUserId();
            try { await _xero.SyncPaymentAsync(result.Data.PaymentId, userId2); }
            catch { /* logged inside service */ }
        }

        TempData["Success"] = "Payment recorded successfully!";
        if (Request.Headers["X-Requested-With"] == "XMLHttpRequest")
            return Json(new { success = true, message = "Payment recorded successfully!", id = result.Data?.PaymentId });
        return RedirectToAction(nameof(Index));
    }

    // ═══════════════════════════════════════════════════════════════════
    //  DETAILS
    // ═══════════════════════════════════════════════════════════════════
    [HttpGet]
    public async Task<IActionResult> Details(long id)
    {
        if (!IsAuthorized()) return RedirectToAction("AccessDenied", "Auth", new { area = "" });

        var vm = await GetPaymentDetailAsync(id);
        if (vm == null) return NotFound();
        return View(vm);
    }

    [HttpGet]
    public async Task<IActionResult> DetailsModal(long id)
    {
        if (!IsAuthorized()) return Forbid();

        var vm = await GetPaymentDetailAsync(id);
        if (vm == null) return NotFound();
        return PartialView("_DetailsModal", vm);
    }

    [HttpGet]
    public async Task<IActionResult> Receipt(long id)
    {
        if (!IsAuthorized()) return RedirectToAction("AccessDenied", "Auth", new { area = "" });

        var vm = await GetPaymentDetailAsync(id);
        if (vm == null) return NotFound();

        var shop = await _db.Shops
            .AsNoTracking()
            .FirstOrDefaultAsync(s => s.ShopId == User.GetShopId());

        ViewBag.ShopName = shop?.ShopName ?? "ByteBill";
        ViewBag.ShopAddress = shop?.Address ?? "";
        ViewBag.ShopPhone = shop?.Phone ?? "";
        ViewBag.ShopEmail = shop?.Email ?? "";
        ViewBag.ShopTIN = shop?.TIN ?? "";

        return View("~/Views/Shared/_Receipt.cshtml", vm);
    }

    [HttpGet]
    public async Task<IActionResult> ReceiptPdf(long id)
    {
        if (!IsAuthorized()) return Forbid();

        var vm = await GetPaymentDetailAsync(id);
        if (vm == null) return NotFound();

        var shop = await _db.Shops.AsNoTracking().FirstOrDefaultAsync(s => s.ShopId == User.GetShopId());
        var shopName = shop?.ShopName ?? "ByteBill";
        var shopAddress = shop?.Address ?? "";
        var shopPhone = shop?.Phone ?? "";
        var shopEmail = shop?.Email ?? "";

        var pdf = GenerateReceiptPdf(vm, shopName, shopAddress, shopPhone, shopEmail);
        return File(pdf, "application/pdf", $"Receipt_{vm.PaymentNumber}.pdf");
    }

    private static byte[] GenerateReceiptPdf(PaymentDetailViewModel vm, string shopName, string shopAddress, string shopPhone, string shopEmail)
    {
        return Document.Create(doc =>
        {
            doc.Page(page =>
            {
                page.Size(PageSizes.A6);
                page.Margin(24);
                page.DefaultTextStyle(x => x.FontSize(10));

                page.Content().Column(col =>
                {
                    // Header
                    col.Item().AlignCenter().Text(shopName).Bold().FontSize(16);
                    if (!string.IsNullOrEmpty(shopAddress))
                        col.Item().AlignCenter().Text(shopAddress).FontSize(8).FontColor(Colors.Grey.Medium);
                    if (!string.IsNullOrEmpty(shopPhone) || !string.IsNullOrEmpty(shopEmail))
                        col.Item().AlignCenter().Text($"{shopPhone}  {shopEmail}".Trim()).FontSize(8).FontColor(Colors.Grey.Medium);
                    if (!string.IsNullOrEmpty(vm.ShopTIN))
                        col.Item().AlignCenter().Text($"TIN: {vm.ShopTIN}").FontSize(8).FontColor(Colors.Grey.Medium);

                    col.Item().PaddingVertical(8).AlignCenter().Text("PAYMENT RECEIPT").Bold().FontSize(11).FontColor(Colors.Grey.Darken2);
                    col.Item().LineHorizontal(1).LineColor(Colors.Grey.Lighten2);
                    col.Item().PaddingVertical(6);

                    // Details
                    col.Item().Row(r => { r.RelativeItem().Text("Receipt #").FontColor(Colors.Grey.Medium); r.RelativeItem().AlignRight().Text(vm.PaymentNumber ?? "").Bold(); });
                    col.Item().Row(r => { r.RelativeItem().Text("Date").FontColor(Colors.Grey.Medium); r.RelativeItem().AlignRight().Text(vm.PaidAt.ToString("MMM dd, yyyy h:mm tt")); });
                    col.Item().Row(r => { r.RelativeItem().Text("Customer").FontColor(Colors.Grey.Medium); r.RelativeItem().AlignRight().Text(vm.CustomerName ?? "").Bold(); });
                    col.Item().Row(r => { r.RelativeItem().Text("Method").FontColor(Colors.Grey.Medium); r.RelativeItem().AlignRight().Text(vm.Method.ToString()); });
                    if (!string.IsNullOrEmpty(vm.ReferenceNumber))
                        col.Item().Row(r => { r.RelativeItem().Text("Reference #").FontColor(Colors.Grey.Medium); r.RelativeItem().AlignRight().Text(vm.ReferenceNumber); });
                    col.Item().Row(r => { r.RelativeItem().Text("Received By").FontColor(Colors.Grey.Medium); r.RelativeItem().AlignRight().Text(vm.ReceivedByName ?? "\u2014"); });

                    col.Item().PaddingVertical(6).LineHorizontal(1).LineColor(Colors.Grey.Lighten2);

                    // Amount
                    col.Item().PaddingVertical(10).AlignCenter().Text(t =>
                    {
                        t.Span("Amount Paid: ").FontColor(Colors.Grey.Medium);
                        t.Span($"\u20B1{vm.Amount:N2}").Bold().FontSize(18).FontColor(Colors.Green.Darken3);
                    });

                    // Line Items
                    if (vm.LineItems.Count > 0)
                    {
                        col.Item().PaddingVertical(4).Text("Items").Bold().FontSize(9).FontColor(Colors.Grey.Darken1);
                        foreach (var li in vm.LineItems)
                        {
                            col.Item().Row(r =>
                            {
                                r.RelativeItem().Text($"{li.Description} x{li.Quantity}").FontSize(9);
                                r.RelativeItem().AlignRight().Text($"\u20B1{li.Total:N2}").FontSize(9).Bold();
                            });
                        }
                        col.Item().PaddingVertical(4).LineHorizontal(1).LineColor(Colors.Grey.Lighten2);
                    }

                    // Allocations
                    if (vm.Allocations.Count > 0)
                    {
                        col.Item().PaddingVertical(4).Text("Applied to Invoices").Bold().FontSize(9).FontColor(Colors.Grey.Darken1);
                        foreach (var a in vm.Allocations)
                            col.Item().Row(r => { r.RelativeItem().Text(a.InvoiceNumber ?? "").FontColor(Colors.Blue.Medium); r.RelativeItem().AlignRight().Text($"\u20B1{a.AmountApplied:N2}").Bold(); });
                        col.Item().PaddingVertical(4).LineHorizontal(1).LineColor(Colors.Grey.Lighten2);
                    }

                    if (!string.IsNullOrEmpty(vm.Notes))
                        col.Item().PaddingVertical(4).Text($"Notes: {vm.Notes}").FontSize(8).FontColor(Colors.Grey.Medium);

                    // BIR Tax Breakdown
                    if (vm.InvoiceSubtotal > 0)
                    {
                        col.Item().PaddingVertical(4).LineHorizontal(1).LineColor(Colors.Grey.Lighten2);
                        col.Item().PaddingVertical(4).Text("TAX BREAKDOWN (BIR)").Bold().FontSize(9).FontColor(Colors.Grey.Darken1);

                        col.Item().Row(r => { r.RelativeItem().Text("Subtotal").FontSize(9).FontColor(Colors.Grey.Medium); r.RelativeItem().AlignRight().Text($"\u20B1{vm.InvoiceSubtotal:N2}").FontSize(9); });

                        if (vm.IsVatRegistered)
                        {
                            if (vm.VatableSales > 0)
                                col.Item().Row(r => { r.RelativeItem().Text("Vatable Sales").FontSize(9).FontColor(Colors.Grey.Medium); r.RelativeItem().AlignRight().Text($"\u20B1{vm.VatableSales:N2}").FontSize(9); });
                            if (vm.VatExemptSales > 0)
                                col.Item().Row(r => { r.RelativeItem().Text("VAT-Exempt Sales").FontSize(9).FontColor(Colors.Grey.Medium); r.RelativeItem().AlignRight().Text($"\u20B1{vm.VatExemptSales:N2}").FontSize(9); });
                            if (vm.ZeroRatedSales > 0)
                                col.Item().Row(r => { r.RelativeItem().Text("Zero-Rated Sales").FontSize(9).FontColor(Colors.Grey.Medium); r.RelativeItem().AlignRight().Text($"\u20B1{vm.ZeroRatedSales:N2}").FontSize(9); });
                            col.Item().Row(r => { r.RelativeItem().Text("VAT (12%)").FontSize(9).FontColor(Colors.Grey.Medium); r.RelativeItem().AlignRight().Text($"\u20B1{vm.VatAmount:N2}").FontSize(9); });
                        }

                        foreach (var disc in vm.Discounts)
                        {
                            col.Item().Row(r => { r.RelativeItem().Text(disc.Label).FontSize(9).FontColor(Colors.Red.Darken1); r.RelativeItem().AlignRight().Text($"-\u20B1{disc.Amount:N2}").FontSize(9).FontColor(Colors.Red.Darken1); });
                            if (!string.IsNullOrEmpty(disc.BeneficiaryName))
                                col.Item().Text($"  {disc.BeneficiaryName}{(!string.IsNullOrEmpty(disc.BeneficiaryIdNo) ? $" (ID: {disc.BeneficiaryIdNo})" : "")}").FontSize(7).FontColor(Colors.Grey.Medium);
                        }

                        col.Item().PaddingTop(4).Row(r => { r.RelativeItem().Text("Total").FontSize(9).Bold(); r.RelativeItem().AlignRight().Text($"\u20B1{vm.InvoiceTotal:N2}").FontSize(9).Bold(); });
                    }

                    col.Item().PaddingVertical(10).AlignCenter().Text("Thank you for your payment!").Bold().FontSize(10);
                    col.Item().AlignCenter().Text("This is a computer-generated receipt.").FontSize(7).FontColor(Colors.Grey.Medium);
                });
            });
        }).GeneratePdf();
    }

    private async Task<PaymentDetailViewModel?> GetPaymentDetailAsync(long id)
    {
        var shopId = User.GetShopId();
        var dto = await _paymentService.GetDetailAsync(shopId, id);
        if (dto == null) return null;

        _ = Enum.TryParse<PaymentMethod>(dto.Method, true, out var parsedMethod);
        _ = Enum.TryParse<PaymentStatus>(dto.Status, true, out var parsedStatus);

        var customer = await _db.Customers
            .Where(c => c.CustomerId == dto.CustomerId)
            .AsNoTracking()
            .FirstOrDefaultAsync();

        // Get invoice tax breakdown for receipt
        var firstAlloc = dto.Allocations.FirstOrDefault();
        decimal invSubtotal = 0, invDiscount = 0, vatableSales = 0, vatExemptSales = 0, zeroRatedSales = 0, vatAmount = 0, invTotal = 0;
        var discounts = new List<ReceiptDiscountItem>();
        var lineItems = new List<ReceiptLineItem>();
        var shop = await _db.Shops.AsNoTracking().FirstOrDefaultAsync(s => s.ShopId == shopId);

        if (firstAlloc != null)
        {
            var invoice = await _db.Invoices
                .Include(i => i.InvoiceDiscounts)
                .Include(i => i.InvoiceLines)
                .AsNoTracking()
                .FirstOrDefaultAsync(i => i.InvoiceId == firstAlloc.InvoiceId);
            if (invoice != null)
            {
                invSubtotal = invoice.Subtotal;
                invDiscount = invoice.DiscountAmount;
                vatableSales = invoice.VatableSales;
                vatExemptSales = invoice.VatExemptSales;
                zeroRatedSales = invoice.ZeroRatedSales;
                vatAmount = invoice.VatAmount;
                invTotal = invoice.TotalAmount;
                discounts = invoice.InvoiceDiscounts.Select(d => new ReceiptDiscountItem
                {
                    Label = d.Label,
                    Amount = d.Amount,
                    BeneficiaryIdNo = d.BeneficiaryIdNo,
                    BeneficiaryName = d.BeneficiaryName
                }).ToList();
                lineItems = invoice.InvoiceLines.Select(l => new ReceiptLineItem
                {
                    Description = l.Description,
                    Quantity = l.Qty,
                    UnitPrice = l.UnitPrice,
                    Total = l.LineTotal
                }).ToList();
            }
        }

        return new PaymentDetailViewModel
        {
            Id = dto.PaymentId,
            PaymentNumber = dto.PaymentNo,
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
            }).ToList(),
            InvoiceSubtotal = invSubtotal,
            InvoiceDiscountAmount = invDiscount,
            VatableSales = vatableSales,
            VatExemptSales = vatExemptSales,
            ZeroRatedSales = zeroRatedSales,
            VatAmount = vatAmount,
            InvoiceTotal = invTotal,
            ShopTIN = shop?.TIN,
            IsVatRegistered = shop?.IsVatRegistered ?? true,
            Discounts = discounts,
            LineItems = lineItems
        };
    }
}
