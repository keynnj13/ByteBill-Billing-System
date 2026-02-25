using System.ComponentModel.DataAnnotations;

namespace ByteBill_BS.ViewModels.Auth;

public class LoginViewModel
{
    [Required(ErrorMessage = "Username or email is required")]
    [StringLength(100)]
    [Display(Name = "Username or Email")]
    public string UserName { get; set; } = string.Empty;
    
    [Required(ErrorMessage = "Password is required")]
    [DataType(DataType.Password)]
    public string Password { get; set; } = string.Empty;
    
    public bool RememberMe { get; set; }
    
    public string? ReturnUrl { get; set; }
    
    public string? ErrorMessage { get; set; }
}
