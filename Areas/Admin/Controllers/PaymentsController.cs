using ByteBill_BS.Models.Enums;
using ByteBill_BS.ViewModels.Payments;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace ByteBill_BS.Areas.Admin.Controllers;

[Area("Admin")]
[Authorize]
public class PaymentsController : Controller
{
    private bool IsAuthorized()
    {
        var roleClaim = User.Claims.FirstOrDefault(c => c.Type == "Role")?.Value;
        return roleClaim == UserRole.Admin.ToString();
    }

    [HttpGet]
    public IActionResult Index(string? search, PaymentMethod? method, int page = 1)
    {
        if (!IsAuthorized()) return RedirectToAction("AccessDenied", "Auth", new { area = "" });

        var viewModel = new PaymentListViewModel
        {
            SearchTerm = search,
            MethodFilter = method,
            CurrentPage = page,
            TotalCount = 98,
            TotalReceived = 45820.00m,
            TodayReceived = 1250.00m,
            Payments = new List<PaymentItemViewModel>
            {
                new() { Id = 1, PaymentNumber = "PAY-2024-0142", InvoiceNumber = "INV-2024-0142", InvoiceId = 142, CustomerName = "Mike Johnson", CustomerInitials = "MJ", Amount = 450.00m, Method = PaymentMethod.Card, PaidAt = DateTime.Now.AddMinutes(-5), ReceivedByName = "Emily Brown", IsVoid = false },
                new() { Id = 2, PaymentNumber = "PAY-2024-0141", InvoiceNumber = "INV-2024-0140", InvoiceId = 140, CustomerName = "Sarah Chen", CustomerInitials = "SC", Amount = 320.00m, Method = PaymentMethod.Cash, PaidAt = DateTime.Now.AddHours(-3), ReceivedByName = "Emily Brown", IsVoid = false },
                new() { Id = 3, PaymentNumber = "PAY-2024-0140", InvoiceNumber = "INV-2024-0138", InvoiceId = 138, CustomerName = "Bob Martinez", CustomerInitials = "BM", Amount = 150.00m, Method = PaymentMethod.Card, PaidAt = DateTime.Now.AddDays(-1), ReceivedByName = "John Anderson", IsVoid = false },
                new() { Id = 4, PaymentNumber = "PAY-2024-0139", InvoiceNumber = "INV-2024-0135", InvoiceId = 135, CustomerName = "Alice Thompson", CustomerInitials = "AT", Amount = 680.00m, Method = PaymentMethod.GCash, PaidAt = DateTime.Now.AddDays(-2), ReceivedByName = "Emily Brown", IsVoid = false },
                new() { Id = 5, PaymentNumber = "PAY-2024-0138", InvoiceNumber = "INV-2024-0132", InvoiceId = 132, CustomerName = "Carol White", CustomerInitials = "CW", Amount = 95.00m, Method = PaymentMethod.Cash, ReferenceNumber = null, PaidAt = DateTime.Now.AddDays(-3), ReceivedByName = "John Anderson", IsVoid = true }
            }
        };
        
        return View(viewModel);
    }

    [HttpGet]
    public IActionResult Create(long invoiceId)
    {
        if (!IsAuthorized()) return RedirectToAction("AccessDenied", "Auth", new { area = "" });
        
        var model = new PaymentCreateViewModel
        {
            InvoiceId = invoiceId,
            InvoiceNumber = "INV-2024-0143",
            CustomerName = "Alice Thompson",
            InvoiceBalance = 280.00m,
            Amount = 280.00m,
            Method = PaymentMethod.Cash
        };
        
        return View(model);
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public IActionResult Create(PaymentCreateViewModel model)
    {
        if (!IsAuthorized()) return RedirectToAction("AccessDenied", "Auth", new { area = "" });
        
        if (!ModelState.IsValid) return View(model);
        
        TempData["Success"] = "Payment recorded successfully!";
        return RedirectToAction(nameof(Index));
    }

    [HttpGet]
    public IActionResult Details(long id)
    {
        if (!IsAuthorized()) return RedirectToAction("AccessDenied", "Auth", new { area = "" });
        
        var model = new PaymentDetailViewModel
        {
            Id = id,
            PaymentNumber = "PAY-2024-0142",
            InvoiceId = 142,
            InvoiceNumber = "INV-2024-0142",
            CustomerName = "Mike Johnson",
            CustomerEmail = "mike@email.com",
            CustomerPhone = "(555) 666-7777",
            Amount = 450.00m,
            Method = PaymentMethod.Card,
            ReferenceNumber = "CC-4832",
            Notes = "Full payment for laptop repair",
            PaidAt = DateTime.Now.AddMinutes(-5),
            ReceivedByName = "Emily Brown",
            IsVoid = false
        };
        
        return View(model);
    }
}
