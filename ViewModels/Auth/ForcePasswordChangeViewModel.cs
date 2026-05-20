using System.ComponentModel.DataAnnotations;

namespace ByteBill_BS.ViewModels.Auth;

public class ForcePasswordChangeViewModel
{
    [Required(ErrorMessage = "New password is required")]
    [StringLength(100, MinimumLength = 12, ErrorMessage = "Password must be at least 12 characters")]
    [RegularExpression(@"^(?=.*[a-z])(?=.*[A-Z])(?=.*\d)(?=.*[^a-zA-Z\d]).{12,}$",
        ErrorMessage = "Password must include uppercase, lowercase, number, and special character")]
    [DataType(DataType.Password)]
    public string NewPassword { get; set; } = string.Empty;

    [Required(ErrorMessage = "Please confirm your new password")]
    [Compare(nameof(NewPassword), ErrorMessage = "Passwords do not match")]
    [DataType(DataType.Password)]
    public string ConfirmPassword { get; set; } = string.Empty;
}
