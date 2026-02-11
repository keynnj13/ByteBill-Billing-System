using ByteBill_BS.Models.Enums;
using ByteBill_BS.ViewModels.Invoices;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace ByteBill_BS.Areas.Billing.Controllers;

[Area("Billing")]
[Authorize]
public class InvoicesController : Controller
{
    private bool IsAuthorized()
    {
        var roleClaim = User.Claims.FirstOrDefault(c => c.Type == "Role")?.Value;
        return roleClaim == UserRole.Billing.ToString();
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
            TotalCount = 142,
            TotalOutstanding = 8450.00m,
            Invoices = new List<InvoiceItemViewModel>
            {
                new() { Id = 1, InvoiceNumber = "INV-2024-0143", CustomerName = "Sarah Chen", CustomerInitials = "SC", Status = InvoiceStatus.Unpaid, Total = 320.00m, AmountPaid = 0m, Balance = 320.00m, CreatedAt = DateTime.Now.AddMinutes(-30), DueDate = DateTime.Now.AddDays(30) },
                new() { Id = 2, InvoiceNumber = "INV-2024-0142", CustomerName = "Mike Johnson", CustomerInitials = "MJ", Status = InvoiceStatus.Paid, Total = 450.00m, AmountPaid = 450.00m, Balance = 0m, CreatedAt = DateTime.Now.AddDays(-1), DueDate = DateTime.Now.AddDays(29) },
                new() { Id = 3, InvoiceNumber = "INV-2024-0141", CustomerName = "Bob Martinez", CustomerInitials = "BM", Status = InvoiceStatus.Unpaid, Total = 680.00m, AmountPaid = 0m, Balance = 680.00m, CreatedAt = DateTime.Now.AddDays(-3), DueDate = DateTime.Now.AddDays(27) },
                new() { Id = 4, InvoiceNumber = "INV-2024-0140", CustomerName = "Alice Thompson", CustomerInitials = "AT", Status = InvoiceStatus.Partial, Total = 520.00m, AmountPaid = 200.00m, Balance = 320.00m, CreatedAt = DateTime.Now.AddDays(-5), DueDate = DateTime.Now.AddDays(25) },
                new() { Id = 5, InvoiceNumber = "INV-2024-0128", CustomerName = "Alice Thompson", CustomerInitials = "AT", Status = InvoiceStatus.Unpaid, Total = 280.00m, AmountPaid = 0m, Balance = 280.00m, CreatedAt = DateTime.Now.AddDays(-35), DueDate = DateTime.Now.AddDays(-5) }
            }
        };
        
        return View(viewModel);
    }

    [HttpGet]
    public IActionResult Create(long? jobOrderId)
    {
        if (!IsAuthorized()) return RedirectToAction("AccessDenied", "Auth", new { area = "" });
        
        var model = new InvoiceCreateViewModel
        {
            JobOrderId = jobOrderId ?? 0,
            DueDate = DateTime.Now.AddDays(30),
            TaxRate = 8.25m,
            Customers = new List<InvoiceCreateViewModel.CustomerOption>
            {
                new() { Id = 1, Name = "Mike Johnson", Email = "mike@email.com" },
                new() { Id = 2, Name = "Sarah Chen", Email = "sarah@email.com" },
                new() { Id = 3, Name = "Bob Martinez", Email = "bob@email.com" }
            },
            AvailableServices = new List<InvoiceCreateViewModel.ServiceOption>
            {
                new() { Id = 1, Name = "System Diagnosis", Price = 50.00m },
                new() { Id = 2, Name = "Virus/Malware Removal", Price = 75.00m },
                new() { Id = 3, Name = "Screen Replacement", Price = 120.00m }
            }
        };

        if (jobOrderId.HasValue)
        {
            model.CustomerId = 1;
            model.CustomerName = "Mike Johnson";
            model.Items = new List<InvoiceCreateViewModel.LineItemInput>
            {
                new() { Description = "Display Cable Replacement", Quantity = 1, UnitPrice = 120.00m },
                new() { Description = "Display Cable (MacBook Pro 2021)", Quantity = 1, UnitPrice = 85.00m },
                new() { Description = "System Diagnosis", Quantity = 1, UnitPrice = 50.00m }
            };
        }
        
        return View(model);
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public IActionResult Create(InvoiceCreateViewModel model)
    {
        if (!IsAuthorized()) return RedirectToAction("AccessDenied", "Auth", new { area = "" });
        
        if (!ModelState.IsValid) return View(model);
        
        TempData["Success"] = "Invoice created successfully!";
        return RedirectToAction(nameof(Index));
    }

    [HttpGet]
    public IActionResult Details(long id)
    {
        if (!IsAuthorized()) return RedirectToAction("AccessDenied", "Auth", new { area = "" });
        
        var model = new InvoiceDetailViewModel
        {
            Id = id,
            InvoiceNumber = "INV-2024-0142",
            Status = InvoiceStatus.Paid,
            CustomerId = 1,
            CustomerName = "Mike Johnson",
            CustomerEmail = "mike@email.com",
            CustomerPhone = "(555) 123-4567",
            CustomerAddress = "123 Main Street, Anytown, USA 12345",
            JobOrderId = 156,
            JobOrderNumber = "JO-2024-0156",
            CreatedAt = DateTime.Now.AddDays(-1),
            DueDate = DateTime.Now.AddDays(29),
            PaidAt = DateTime.Now.AddMinutes(-5),
            Notes = "Thank you for your business!",
            LineItems = new List<InvoiceLineItemViewModel>
            {
                new() { Description = "Display Cable Replacement", Quantity = 1, UnitPrice = 120.00m, Total = 120.00m },
                new() { Description = "Display Cable (MacBook Pro 2021)", Quantity = 1, UnitPrice = 85.00m, Total = 85.00m },
                new() { Description = "System Diagnosis", Quantity = 1, UnitPrice = 50.00m, Total = 50.00m }
            },
            Subtotal = 255.00m,
            TaxRate = 8.25m,
            TaxAmount = 21.04m,
            Total = 276.04m,
            AmountPaid = 276.04m,
            Balance = 0m,
            Payments = new List<PaymentSummaryViewModel>
            {
                new() { Id = 142, PaymentNumber = "PAY-2024-0142", Amount = 276.04m, Method = PaymentMethod.Card, PaidAt = DateTime.Now.AddMinutes(-5) }
            }
        };
        
        return View(model);
    }
}
