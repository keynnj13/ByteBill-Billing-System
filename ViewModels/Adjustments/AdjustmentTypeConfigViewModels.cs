using System.ComponentModel.DataAnnotations;

namespace ByteBill_BS.ViewModels.Adjustments;

public class AdjustmentTypeConfigCreateViewModel
{
    [Required(ErrorMessage = "Name is required")]
    [StringLength(100, ErrorMessage = "Name cannot exceed 100 characters")]
    public string Name { get; set; } = string.Empty;

    [Required(ErrorMessage = "Category is required")]
    [RegularExpression("^(Credit|Debit|Refund)$", ErrorMessage = "Category must be Credit, Debit, or Refund")]
    public string Category { get; set; } = "Credit";

    [Range(0, 100, ErrorMessage = "Percentage must be between 0 and 100")]
    public decimal Percentage { get; set; }
}

public class AdjustmentTypeConfigUpdateViewModel : AdjustmentTypeConfigCreateViewModel
{
    [Range(1, long.MaxValue, ErrorMessage = "Invalid configuration id")]
    public long ConfigId { get; set; }

    public bool IsActive { get; set; } = true;
}
