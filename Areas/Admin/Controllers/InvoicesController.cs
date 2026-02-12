using ByteBill_BS.Models.Enums;
using ByteBill_BS.ViewModels.Invoices;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace ByteBill_BS.Areas.Admin.Controllers;

[Area("Admin")]
[Authorize]
public class InvoicesController : Controller
{
    private bool IsAuthorized()
    {
        var roleClaim = User.Claims.FirstOrDefault(c => c.Type == "Role")?.Value;
        return roleClaim == UserRole.Admin.ToString();
    }

    [HttpGet]
    public IActionResult Index(string? search, InvoiceStatus? status, int page = 1)
    {
        if (!IsAuthorized()) return RedirectToAction("AccessDenied", "Auth", new { area = "" });

        var viewModel = new InvoiceListViewModel
        {
            SearchTerm = search,
            StatusFilter = status,
            CurrentPage = page,
            TotalCount = 145,
            TotalOutstanding = 8450.00m,
            OverdueCount = 3,
            Invoices = new List<InvoiceItemViewModel>
            {
                new() { Id = 1, InvoiceNumber = "INV-2024-0145", CustomerName = "Sarah Chen", CustomerInitials = "SC", JobNumber = "JO-2024-0085", Status = InvoiceStatus.Unpaid, Total = 450.00m, AmountPaid = 0, Balance = 450.00m, CreatedAt = DateTime.Now.AddHours(-2), DueDate = DateTime.Now.AddDays(14) },
                new() { Id = 2, InvoiceNumber = "INV-2024-0144", CustomerName = "Mike Johnson", CustomerInitials = "MJ", JobNumber = "JO-2024-0082", Status = InvoiceStatus.Paid, Total = 320.00m, AmountPaid = 320.00m, Balance = 0, CreatedAt = DateTime.Now.AddDays(-3), DueDate = DateTime.Now.AddDays(11) },
                new() { Id = 3, InvoiceNumber = "INV-2024-0143", CustomerName = "Alice Thompson", CustomerInitials = "AT", JobNumber = "JO-2024-0079", Status = InvoiceStatus.Partial, Total = 680.00m, AmountPaid = 400.00m, Balance = 280.00m, CreatedAt = DateTime.Now.AddDays(-7), DueDate = DateTime.Now.AddDays(7) },
                new() { Id = 4, InvoiceNumber = "INV-2024-0140", CustomerName = "Bob Martinez", CustomerInitials = "BM", JobNumber = "JO-2024-0072", Status = InvoiceStatus.Unpaid, Total = 520.00m, AmountPaid = 0, Balance = 520.00m, CreatedAt = DateTime.Now.AddDays(-20), DueDate = DateTime.Now.AddDays(-6) },
                new() { Id = 5, InvoiceNumber = "INV-2024-0138", CustomerName = "Carol White", CustomerInitials = "CW", JobNumber = "JO-2024-0068", Status = InvoiceStatus.Void, Total = 150.00m, AmountPaid = 0, Balance = 0, CreatedAt = DateTime.Now.AddDays(-25) }
            }
        };
        
        return View(viewModel);
    }

    [HttpGet]
    public IActionResult Create(long jobOrderId)
    {
        if (!IsAuthorized()) return RedirectToAction("AccessDenied", "Auth", new { area = "" });
        
        var model = new InvoiceCreateViewModel
        {
            JobOrderId = jobOrderId,
            JobNumber = "JO-2024-0088",
            CustomerName = "Bob Martinez",
            TaxRate = 8.5m,
            DueDate = DateTime.Now.AddDays(14),
            LineItems = new List<InvoiceLineItemFormViewModel>
            {
                new() { Description = "System Diagnosis", Quantity = 1, UnitPrice = 50.00m },
                new() { Description = "Malware Removal", Quantity = 1, UnitPrice = 75.00m },
                new() { Description = "500GB SSD", Quantity = 1, UnitPrice = 65.00m }
            }
        };
        
        return View(model);
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public IActionResult Create(InvoiceCreateViewModel model)
    {
        if (!IsAuthorized()) return RedirectToAction("AccessDenied", "Auth", new { area = "" });
        
        if (!ModelState.IsValid)
        {
            if (Request.Headers["X-Requested-With"] == "XMLHttpRequest")
                return PartialView("_CreateModal", model);
            return View(model);
        }
        
        TempData["Success"] = "Invoice created successfully!";
        if (Request.Headers["X-Requested-With"] == "XMLHttpRequest")
            return Json(new { success = true, message = "Invoice created successfully!" });
        return RedirectToAction(nameof(Index));
    }

    [HttpGet]
    public IActionResult CreateModal(long jobOrderId = 0)
    {
        if (!IsAuthorized()) return Forbid();
        
        var model = new InvoiceCreateViewModel
        {
            JobOrderId = jobOrderId,
            JobNumber = "JO-2024-0088",
            CustomerName = "Bob Martinez",
            TaxRate = 8.5m,
            DueDate = DateTime.Now.AddDays(14),
            LineItems = new List<InvoiceLineItemFormViewModel>
            {
                new() { Description = "System Diagnosis", Quantity = 1, UnitPrice = 50.00m },
                new() { Description = "Malware Removal", Quantity = 1, UnitPrice = 75.00m },
                new() { Description = "500GB SSD", Quantity = 1, UnitPrice = 65.00m }
            }
        };
        
        return PartialView("_CreateModal", model);
    }

    [HttpGet]
    public IActionResult Details(long id)
    {
        if (!IsAuthorized()) return RedirectToAction("AccessDenied", "Auth", new { area = "" });
        
        var model = GetInvoiceDetail(id);
        return View(model);
    }

    [HttpGet]
    public IActionResult DetailsModal(long id)
    {
        if (!IsAuthorized()) return Forbid();
        
        var model = GetInvoiceDetail(id);
        return PartialView("_DetailsModal", model);
    }

    private InvoiceDetailViewModel GetInvoiceDetail(long id)
    {
        return new InvoiceDetailViewModel
        {
            Id = id,
            InvoiceNumber = "INV-2024-0143",
            CustomerId = 1,
            CustomerName = "Alice Thompson",
            CustomerEmail = "alice@email.com",
            CustomerPhone = "(555) 111-2222",
            CustomerAddress = "123 Main St, Anytown, USA 12345",
            JobOrderId = 79,
            JobNumber = "JO-2024-0079",
            ShopName = "TechFix Pro",
            ShopAddress = "456 Tech Lane, Silicon City, CA 94000",
            ShopPhone = "(555) 987-6543",
            ShopEmail = "billing@techfixpro.com",
            Status = InvoiceStatus.Partial,
            Subtotal = 627.88m,
            TaxRate = 8.5m,
            TaxAmount = 52.12m,
            DiscountAmount = 0,
            Total = 680.00m,
            AmountPaid = 400.00m,
            Balance = 280.00m,
            CreatedAt = DateTime.Now.AddDays(-7),
            IssuedAt = DateTime.Now.AddDays(-7),
            DueDate = DateTime.Now.AddDays(7),
            Notes = "Thank you for your business!",
            LineItems = new List<InvoiceLineItemViewModel>
            {
                new() { Id = 1, Description = "Hardware Diagnostic & Assessment", Quantity = 1, UnitPrice = 75.00m, Total = 75.00m },
                new() { Id = 2, Description = "Motherboard Repair (Capacitor Replacement)", Quantity = 1, UnitPrice = 150.00m, Total = 150.00m },
                new() { Id = 3, Description = "16GB DDR4 RAM Module", Quantity = 2, UnitPrice = 89.00m, Total = 178.00m },
                new() { Id = 4, Description = "Labor - Installation & Testing", Quantity = 3, UnitPrice = 75.00m, Total = 225.00m }
            },
            Payments = new List<PaymentSummaryViewModel>
            {
                new() { Id = 1, PaymentNumber = "PAY-2024-0098", Amount = 250.00m, Method = PaymentMethod.Card, PaidAt = DateTime.Now.AddDays(-5), IsVoid = false },
                new() { Id = 2, PaymentNumber = "PAY-2024-0102", Amount = 150.00m, Method = PaymentMethod.Cash, PaidAt = DateTime.Now.AddDays(-2), IsVoid = false }
            }
        };
    }
}
