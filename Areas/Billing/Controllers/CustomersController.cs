using ByteBill_BS.Models.Enums;
using ByteBill_BS.ViewModels.Customers;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace ByteBill_BS.Areas.Billing.Controllers;

[Area("Billing")]
[Authorize]
public class CustomersController : Controller
{
    private bool IsAuthorized()
    {
        var roleClaim = User.Claims.FirstOrDefault(c => c.Type == "Role")?.Value;
        return roleClaim == UserRole.Billing.ToString();
    }

    [HttpGet]
    public IActionResult Index(string? search, int page = 1)
    {
        if (!IsAuthorized()) return RedirectToAction("AccessDenied", "Auth", new { area = "" });

        var viewModel = new CustomerListViewModel
        {
            SearchTerm = search,
            CurrentPage = page,
            TotalCount = 156,
            Customers = new List<CustomerItemViewModel>
            {
                new() { Id = 1, Name = "Mike Johnson", Email = "mike@email.com", Phone = "(555) 123-4567", TotalOrders = 5, TotalSpent = 1250.00m, Initials = "MJ" },
                new() { Id = 2, Name = "Sarah Chen", Email = "sarah@email.com", Phone = "(555) 234-5678", TotalOrders = 3, TotalSpent = 890.00m, Initials = "SC" },
                new() { Id = 3, Name = "Bob Martinez", Email = "bob@email.com", Phone = "(555) 345-6789", TotalOrders = 8, TotalSpent = 2340.00m, Initials = "BM" },
                new() { Id = 4, Name = "Alice Thompson", Email = "alice@email.com", Phone = "(555) 456-7890", TotalOrders = 2, TotalSpent = 450.00m, Initials = "AT" },
                new() { Id = 5, Name = "Carol White", Email = "carol@email.com", Phone = "(555) 567-8901", TotalOrders = 4, TotalSpent = 1680.00m, Initials = "CW" }
            }
        };
        
        return View(viewModel);
    }

    [HttpGet]
    public IActionResult Details(long id)
    {
        if (!IsAuthorized()) return RedirectToAction("AccessDenied", "Auth", new { area = "" });
        
        var model = new CustomerDetailViewModel
        {
            Id = id,
            Name = "Mike Johnson",
            Email = "mike@email.com",
            Phone = "(555) 123-4567",
            Address = "123 Main Street, Anytown, USA 12345",
            Notes = "Preferred customer. Usually brings in multiple devices.",
            CreatedAt = DateTime.Now.AddMonths(-8),
            TotalOrders = 5,
            TotalSpent = 1250.00m,
            OutstandingBalance = 280.00m,
            RecentOrders = new List<OrderHistoryItem>
            {
                new() { Id = 156, OrderNumber = "JO-2024-0156", DeviceType = "MacBook Pro", Status = "Completed", StatusClass = "success", CreatedAt = DateTime.Now.AddDays(-2), Total = 450.00m },
                new() { Id = 142, OrderNumber = "JO-2024-0142", DeviceType = "iPhone 12", Status = "Delivered", StatusClass = "info", CreatedAt = DateTime.Now.AddDays(-15), Total = 280.00m },
                new() { Id = 128, OrderNumber = "JO-2024-0128", DeviceType = "Dell Laptop", Status = "Delivered", StatusClass = "info", CreatedAt = DateTime.Now.AddDays(-30), Total = 520.00m }
            }
        };
        
        return View(model);
    }
}
