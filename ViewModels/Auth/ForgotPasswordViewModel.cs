using System.ComponentModel.DataAnnotations;

namespace ByteBill_BS.ViewModels.Auth;

public class ForgotPasswordViewModel
{
    [Required(ErrorMessage = "Email is required")]
    [EmailAddress(ErrorMessage = "Please enter a valid email address")]
    [StringLength(150)]
    public string Email { get; set; } = string.Empty;

    public string? RecaptchaToken { get; set; }
}
