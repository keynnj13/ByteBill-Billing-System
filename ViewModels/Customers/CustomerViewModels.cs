using System.ComponentModel.DataAnnotations;

namespace ByteBill_BS.ViewModels.Customers;

public class CustomerListViewModel
{
    public List<CustomerItemViewModel> Customers { get; set; } = new();
    public string? SearchTerm { get; set; }
    public int TotalCount { get; set; }
    public int CurrentPage { get; set; } = 1;
    public int PageSize { get; set; } = 10;
    public int TotalPages => (int)Math.Ceiling((double)TotalCount / PageSize);
}

public class CustomerItemViewModel
{
    public long Id { get; set; }
    public string FullName { get; set; } = string.Empty;
    public string Name { get; set; } = string.Empty;
    public string Initials { get; set; } = string.Empty;
    public string? Email { get; set; }
    public string Phone { get; set; } = string.Empty;
    public int TotalJobOrders { get; set; }
    public int TotalOrders { get; set; }
    public decimal TotalSpent { get; set; }
    public DateTime CreatedAt { get; set; }
    public bool IsActive { get; set; }
}

public class CustomerFormViewModel
{
    public long Id { get; set; }
    
    [Required(ErrorMessage = "First name is required")]
    [StringLength(50)]
    [Display(Name = "First Name")]
    public string FirstName { get; set; } = string.Empty;

    [StringLength(50)]
    [Display(Name = "Middle Name")]
    public string? MiddleName { get; set; }
    
    [Required(ErrorMessage = "Last name is required")]
    [StringLength(50)]
    [Display(Name = "Last Name")]
    public string LastName { get; set; } = string.Empty;
    
    [EmailAddress(ErrorMessage = "Invalid email address")]
    [StringLength(100)]
    [Display(Name = "Email")]
    public string? Email { get; set; }
    
    [StringLength(30)]
    [Display(Name = "Phone")]
    public string? Phone { get; set; } // Format: +63 XXX XXX XXXX (mobile) or +63 XX XXX XXXX (landline)
    
    [StringLength(255)]
    [Display(Name = "Address")]
    public string? Address { get; set; }

    [StringLength(500)]
    [Display(Name = "Notes")]
    public string? Notes { get; set; }

    [Display(Name = "Active")]
    public bool IsActive { get; set; } = true;

    // Computed helper for views
    public string Name => $"{FirstName} {LastName}".Trim();
}

public class CustomerDetailViewModel
{
    public long Id { get; set; }
    public string FullName { get; set; } = string.Empty;
    public string Name { get; set; } = string.Empty;
    public string Initials { get; set; } = string.Empty;
    public string? Email { get; set; }
    public string Phone { get; set; } = string.Empty;
    public string? Address { get; set; }
    public string? Notes { get; set; }
    public bool IsActive { get; set; }
    public DateTime CreatedAt { get; set; }
    
    public int TotalJobOrders { get; set; }
    public int TotalOrders { get; set; }
    public decimal TotalSpent { get; set; }
    public decimal OutstandingBalance { get; set; }
    
    public List<CustomerJobOrderViewModel> RecentJobOrders { get; set; } = new();
    public List<OrderHistoryItem> RecentOrders { get; set; } = new();
}

public class CustomerJobOrderViewModel
{
    public long Id { get; set; }
    public string JobNumber { get; set; } = string.Empty;
    public string DeviceType { get; set; } = string.Empty;
    public string Status { get; set; } = string.Empty;
    public string StatusClass { get; set; } = string.Empty;
    public decimal Total { get; set; }
    public DateTime CreatedAt { get; set; }
}

public class OrderHistoryItem
{
    public long Id { get; set; }
    public string JobNumber { get; set; } = string.Empty;
    public string OrderNumber { get; set; } = string.Empty;
    public string DeviceType { get; set; } = string.Empty;
    public string Status { get; set; } = string.Empty;
    public string StatusClass { get; set; } = string.Empty;
    public decimal Total { get; set; }
    public DateTime Date { get; set; }
    public DateTime CreatedAt { get; set; }
}
