using System.ComponentModel.DataAnnotations;

namespace ByteBill_BS.ViewModels.Auth;

public class MfaChallengeViewModel
{
    [Display(Name = "Authenticator Code")]
    [StringLength(6, MinimumLength = 6, ErrorMessage = "Code must be 6 digits")]
    public string? TotpCode { get; set; }

    [Display(Name = "Email Code")]
    [StringLength(6, MinimumLength = 6, ErrorMessage = "Code must be 6 digits")]
    public string? EmailCode { get; set; }

    public string SelectedMethod { get; set; } = "email";

    public bool CanUseTotp { get; set; }
    public bool CanUseEmailOtp { get; set; }
    public bool RememberMe { get; set; }
    public string? ReturnUrl { get; set; }
}
