using System.ComponentModel.DataAnnotations;

namespace ByteBill_BS.ViewModels.Inventory;

public class InventoryListViewModel
{
    public List<InventoryItemViewModel> Items { get; set; } = new();
    public string? SearchTerm { get; set; }
    public string? CategoryFilter { get; set; }
    public bool? LowStockOnly { get; set; }
    public int TotalCount { get; set; }
    public int CurrentPage { get; set; } = 1;
    public int PageSize { get; set; } = 10;
    public int TotalPages => (int)Math.Ceiling((double)TotalCount / PageSize);
    
    public int LowStockCount { get; set; }
    public int TotalItems { get; set; }
    public List<string> Categories { get; set; } = new();
}

public class InventoryItemViewModel
{
    public long Id { get; set; }
    public string SKU { get; set; } = string.Empty;
    public string Name { get; set; } = string.Empty;
    public string ItemName { get; set; } = string.Empty;
    public string Category { get; set; } = string.Empty;
    public string? Brand { get; set; }
    public string Unit { get; set; } = string.Empty;
    public decimal UnitCost { get; set; }
    public decimal UnitPrice { get; set; }
    public decimal CostPrice { get; set; }
    public decimal SellingPrice { get; set; }
    public decimal Price { get; set; }
    public int Quantity { get; set; }
    public int QtyOnHand { get; set; }
    public int QuantityInStock { get; set; }
    public int ReorderLevel { get; set; }
    public bool IsActive { get; set; }
    public bool IsLowStock { get; set; }
    
    public string StockStatus => IsLowStock ? "Low Stock" : "In Stock";
    public string StockStatusClass => IsLowStock ? "status-warning" : "status-success";
}

public class InventoryFormViewModel
{
    public long Id { get; set; }
    
    [Required(ErrorMessage = "SKU is required")]
    [StringLength(30)]
    [Display(Name = "SKU")]
    public string SKU { get; set; } = string.Empty;
    
    [Required(ErrorMessage = "Item name is required")]
    [StringLength(100)]
    [Display(Name = "Item Name")]
    public string Name { get; set; } = string.Empty;
    
    [StringLength(500)]
    [Display(Name = "Description")]
    public string? Description { get; set; }
    
    [StringLength(50)]
    [Display(Name = "Category")]
    public string? Category { get; set; }
    
    [StringLength(50)]
    [Display(Name = "Brand")]
    public string? Brand { get; set; }
    
    [Required(ErrorMessage = "Unit is required")]
    [StringLength(20)]
    [Display(Name = "Unit")]
    public string Unit { get; set; } = "pcs";
    
    [Display(Name = "Cost Price")]
    [Range(0, double.MaxValue)]
    public decimal CostPrice { get; set; }
    
    [Required(ErrorMessage = "Selling price is required")]
    [Display(Name = "Selling Price")]
    [Range(0, double.MaxValue)]
    public decimal SellingPrice { get; set; }
    
    [Required(ErrorMessage = "Quantity is required")]
    [Display(Name = "Quantity in Stock")]
    [Range(0, int.MaxValue)]
    public int QuantityInStock { get; set; }
    
    [Display(Name = "Reorder Level")]
    [Range(0, int.MaxValue)]
    public int ReorderLevel { get; set; } = 5;
    
    [Display(Name = "Active")]
    public bool IsActive { get; set; } = true;
    
    // Dropdown lists
    public List<string> ExistingCategories { get; set; } = new();
    public List<string> ExistingBrands { get; set; } = new();
}

public class InventoryDetailViewModel
{
    public long Id { get; set; }
    public string SKU { get; set; } = string.Empty;
    public string Name { get; set; } = string.Empty;
    public string Category { get; set; } = string.Empty;
    public string? Brand { get; set; }
    public string Unit { get; set; } = string.Empty;
    public decimal UnitCost { get; set; }
    public decimal UnitPrice { get; set; }
    public int QtyOnHand { get; set; }
    public int ReorderLevel { get; set; }
    public bool IsActive { get; set; }
    public bool IsLowStock => QtyOnHand <= ReorderLevel;
    
    public decimal TotalValue => QtyOnHand * UnitCost;
    public decimal TotalRetailValue => QtyOnHand * UnitPrice;
    
    public List<InventoryTxnItemViewModel> RecentTransactions { get; set; } = new();
}

public class InventoryTxnItemViewModel
{
    public long Id { get; set; }
    public string TxnType { get; set; } = string.Empty;
    public int Quantity { get; set; }
    public string? ReferenceType { get; set; }
    public long? ReferenceId { get; set; }
    public string? Remarks { get; set; }
    public DateTime CreatedAt { get; set; }
}
