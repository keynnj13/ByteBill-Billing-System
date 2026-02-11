using System.ComponentModel.DataAnnotations;

namespace ByteBill_BS.ViewModels.Services;

public class ServiceListViewModel
{
    public List<ServiceItemViewModel> Services { get; set; } = new();
    public string? SearchTerm { get; set; }
    public string? CategoryFilter { get; set; }
    public int TotalCount { get; set; }
    public int CurrentPage { get; set; } = 1;
    public int PageSize { get; set; } = 10;
    public int TotalPages => (int)Math.Ceiling((double)TotalCount / PageSize);
    
    public List<string> Categories { get; set; } = new();
}

public class ServiceItemViewModel
{
    public long Id { get; set; }
    public string Name { get; set; } = string.Empty;
    public string ServiceName { get; set; } = string.Empty;
    public string Category { get; set; } = string.Empty;
    public string? Description { get; set; }
    public decimal Price { get; set; }
    public decimal BasePrice { get; set; }
    public string? EstimatedDuration { get; set; }
    public bool IsActive { get; set; }
    public int UsageCount { get; set; }
}

public class ServiceFormViewModel
{
    public long Id { get; set; }
    
    [Required(ErrorMessage = "Service name is required")]
    [StringLength(100)]
    [Display(Name = "Service Name")]
    public string Name { get; set; } = string.Empty;
    
    [Required(ErrorMessage = "Category is required")]
    [StringLength(50)]
    [Display(Name = "Category")]
    public string Category { get; set; } = string.Empty;
    
    [Display(Name = "Category")]
    public long? CategoryId { get; set; }
    
    [StringLength(500)]
    [Display(Name = "Description")]
    public string? Description { get; set; }
    
    [Required(ErrorMessage = "Price is required")]
    [Display(Name = "Price")]
    [Range(0, double.MaxValue)]
    public decimal Price { get; set; }
    
    [Display(Name = "Estimated Duration (minutes)")]
    public int EstimatedDuration { get; set; }
    
    [Display(Name = "Active")]
    public bool IsActive { get; set; } = true;
    
    // Dropdown lists
    public List<string> ExistingCategories { get; set; } = new();
}

public class ServiceDetailViewModel
{
    public long Id { get; set; }
    public string Name { get; set; } = string.Empty;
    public string Category { get; set; } = string.Empty;
    public string? Description { get; set; }
    public decimal Price { get; set; }
    public bool IsActive { get; set; }
    public int UsageCount { get; set; }
    
    public decimal TotalRevenue { get; set; }
    public decimal AverageRating { get; set; }
}
