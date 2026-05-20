using ByteBill_BS.Data;
using ByteBill_BS.Models;
using ByteBill_BS.Models.Enums;
using ByteBill_BS.Services;
using ByteBill_BS.ViewModels.Auth;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;
using Microsoft.EntityFrameworkCore;
using System.Security.Claims;
using Microsoft.Extensions.Options;

namespace ByteBill_BS.Controllers;

public class AuthController : Controller
{
    private readonly ApplicationDbContext _db;
    private readonly IAuditService _audit;
    private readonly IPasswordResetService _passwordReset;
    private readonly IPasswordBlacklistValidator _passwordBlacklist;
    private readonly IRecaptchaService _recaptcha;
    private readonly IMfaService _mfa;
    private readonly IEmailService _email;
    private readonly IEmailSecurityService _emailSecurity;
    private readonly ILogger<AuthController> _logger;
    private readonly RecaptchaSettings _recaptchaSettings;
    private readonly SecuritySettings _securitySettings;

    public AuthController(
        ApplicationDbContext db,
        IAuditService audit,
        IPasswordResetService passwordReset,
        IPasswordBlacklistValidator passwordBlacklist,
        IRecaptchaService recaptcha,
        IMfaService mfa,
        IEmailService email,
        IEmailSecurityService emailSecurity,
        ILogger<AuthController> logger,
        IOptions<RecaptchaSettings> recaptchaSettings,
        IOptions<SecuritySettings> securitySettings)
    {
        _db = db;
        _audit = audit;
        _passwordReset = passwordReset;
        _passwordBlacklist = passwordBlacklist;
        _recaptcha = recaptcha;
        _mfa = mfa;
        _email = email;
        _emailSecurity = emailSecurity;
        _logger = logger;
        _recaptchaSettings = recaptchaSettings.Value;
        _securitySettings = securitySettings.Value;
    }

    private const int MaxFailedAttempts = 5;
    private const int MaxMfaSetupAttempts = 5;
    private const string PendingMfaSetupSecretSessionKey = "PendingMfaSetupSecret";
    private const string PendingMfaSetupAttemptsSessionKey = "PendingMfaSetupAttempts";
    private const string MfaMethodEmail = "email";
    private const string MfaMethodTotp = "totp";

    private void PopulateLoginViewData()
    {
        ViewData["HideNavigation"] = true;
        ViewBag.RecaptchaEnabled = _recaptchaSettings.Enabled;
        ViewBag.RecaptchaSiteKey = _recaptchaSettings.SiteKey;
    }

    [HttpGet]
    public IActionResult Login(string? returnUrl = null)
    {
        if (User.Identity?.IsAuthenticated ?? false)
        {
            return RedirectToRoleDashboard();
        }

        PopulateLoginViewData();
        return View(new LoginViewModel { ReturnUrl = returnUrl });
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    [EnableRateLimiting("LoginPolicy")]
    public async Task<IActionResult> Login(LoginViewModel model)
    {
        PopulateLoginViewData();

        if (!ModelState.IsValid)
        {
            return View(model);
        }

        var requestIp = HttpContext.Connection.RemoteIpAddress?.ToString();
        var captchaOk = await _recaptcha.VerifyAsync(model.RecaptchaToken, "login", requestIp);
        if (!captchaOk)
        {
            ModelState.AddModelError(string.Empty, "Security verification failed. Please try again.");
            return View(model);
        }

        var dbUser = await FindLoginUserAsync(model.UserName);

        if (dbUser is null || !dbUser.IsActive)
        {
            ModelState.AddModelError(string.Empty, "The credentials you entered are incorrect. Please try again.");
            return View(model);
        }

        var lockoutResult = await TryHandleLockoutAsync(dbUser, model, requestIp);
        if (lockoutResult is not null)
        {
            return lockoutResult;
        }

        if (!BCrypt.Net.BCrypt.Verify(model.Password, dbUser.PasswordHash))
        {
            return await HandleInvalidPasswordAsync(dbUser, model, requestIp);
        }

        return await HandleSuccessfulPasswordAsync(dbUser, model);
    }

    private async Task<User?> FindLoginUserAsync(string? userName)
    {
        var key = (userName ?? string.Empty).ToLowerInvariant().Trim();
        if (string.IsNullOrWhiteSpace(key))
        {
            return null;
        }

        var emailHash = _emailSecurity.ComputeHash(key);

        return await _db.Users
            .Include(u => u.UserRoles).ThenInclude(ur => ur.Role)
            .FirstOrDefaultAsync(u => u.UserName == key || (u.EmailHash != null && u.EmailHash == emailHash));
    }

    private async Task<IActionResult?> TryHandleLockoutAsync(User dbUser, LoginViewModel model, string? requestIp)
    {
        if (dbUser.IsPermanentlyLocked)
        {
            await _audit.LogAsync(dbUser.ShopId, dbUser.UserId, "SecurityPermanentLockLoginAttempt", "User", dbUser.UserId,
                "Login attempted while account is permanently locked.", requestIp);
            ViewBag.LockoutMessage = "Account is permanently locked. Please contact a SuperAdmin.";
            return View(model);
        }

        if (dbUser.LockoutEndAt.HasValue && dbUser.LockoutEndAt.Value > DateTime.UtcNow)
        {
            await _audit.LogAsync(dbUser.ShopId, dbUser.UserId, "SecurityLockedLoginAttempt", "User", dbUser.UserId,
                "Login attempted while temporary lockout is active.", requestIp);
            var remaining = (int)Math.Ceiling((dbUser.LockoutEndAt.Value - DateTime.UtcNow).TotalMinutes);
            ViewBag.LockoutMessage = $"Account locked. Try again in {Math.Max(1, remaining)} minute{(remaining != 1 ? "s" : "")}.";
            return View(model);
        }

        return null;
    }

    private async Task<IActionResult> HandleSuccessfulPasswordAsync(User dbUser, LoginViewModel model)
    {
        dbUser.FailedLoginAttempts = 0;
        dbUser.LockoutEndAt = null;

        if (dbUser.IsMfaEnabled)
        {
            return await BeginMfaLoginAsync(dbUser, model);
        }

        var roleName = dbUser.UserRoles.FirstOrDefault()?.Role?.RoleName ?? "Billing";
        if (!Enum.TryParse<UserRole>(roleName, out var userRole))
        {
            userRole = UserRole.Billing;
        }

        return await CompleteSignInAsync(dbUser, userRole, model.RememberMe, model.ReturnUrl);
    }

    private async Task<IActionResult> BeginMfaLoginAsync(User dbUser, LoginViewModel model)
    {
        HttpContext.Session.SetString("PendingLoginUserId", dbUser.UserId.ToString());
        HttpContext.Session.SetString("PendingLoginRememberMe", model.RememberMe ? "1" : "0");
        HttpContext.Session.SetString("PendingLoginReturnUrl", model.ReturnUrl ?? string.Empty);

        try
        {
            await IssueEmailOtpAsync(dbUser);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to issue MFA email OTP for user {UserId}", dbUser.UserId);
            ClearPendingLoginSession();
            ModelState.AddModelError(string.Empty, "Unable to send verification code email right now. Please try again in a moment.");
            return View(model);
        }

        await _db.SaveChangesAsync();
        return RedirectToAction(nameof(VerifyMfa));
    }

    private async Task<IActionResult> HandleInvalidPasswordAsync(User dbUser, LoginViewModel model, string? requestIp)
    {
        var failed = await RegisterFailedPasswordAttemptAsync(dbUser);
        await _audit.LogAsync(dbUser.ShopId, dbUser.UserId, "FailedLogin", "User", dbUser.UserId,
            "Invalid password entered during login.", requestIp);

        if (!string.IsNullOrWhiteSpace(failed.LockoutMessage))
        {
            ViewBag.LockoutMessage = failed.LockoutMessage;
        }
        else
        {
            ModelState.AddModelError(string.Empty,
                $"Invalid password. {failed.AttemptsRemaining} attempt{(failed.AttemptsRemaining == 1 ? string.Empty : "s")} remaining before lockout.");
        }

        return View(model);
    }

    [HttpGet]
    public async Task<IActionResult> Logout()
    {
        // ── Audit log the logout ─────────────────────────────────────
        var userIdStr = User.FindFirstValue("UserId");
        var shopIdStr = User.FindFirstValue("ShopId");
        if (long.TryParse(userIdStr, out var userId) && long.TryParse(shopIdStr, out var shopId))
        {
            var ip = HttpContext.Connection.RemoteIpAddress?.ToString();
            await _audit.LogAsync(shopId, userId, "Logout", "User", userId, "User logged out.", ip);
        }

        await HttpContext.SignOutAsync(CookieAuthenticationDefaults.AuthenticationScheme);
        return RedirectToAction(nameof(Login));
    }

    [HttpGet]
    public IActionResult AccessDenied()
    {
        return View();
    }

    [HttpGet]
    public IActionResult ForgotPassword()
    {
        ViewData["HideNavigation"] = true;
        ViewBag.RecaptchaEnabled = _recaptchaSettings.Enabled;
        ViewBag.RecaptchaSiteKey = _recaptchaSettings.SiteKey;
        return View(new ForgotPasswordViewModel());
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    [EnableRateLimiting("ForgotPasswordPolicy")]
    public async Task<IActionResult> ForgotPassword(ForgotPasswordViewModel model)
    {
        ViewData["HideNavigation"] = true;
        ViewBag.RecaptchaEnabled = _recaptchaSettings.Enabled;
        ViewBag.RecaptchaSiteKey = _recaptchaSettings.SiteKey;

        if (!ModelState.IsValid)
        {
            return View(model);
        }

        var ip = HttpContext.Connection.RemoteIpAddress?.ToString();
        var captchaOk = await _recaptcha.VerifyAsync(model.RecaptchaToken, "forgot_password", ip);
        if (!captchaOk)
        {
            ModelState.AddModelError(string.Empty, "Security verification failed. Please try again.");
            return View(model);
        }

        var resetResult = await _passwordReset.RequestResetAsync(model.Email, ip);
        if (resetResult == PasswordResetRequestResult.DeniedLowPrivilege)
        {
            ModelState.AddModelError(string.Empty, "Self-service password reset is not available for this account. Please request a reset from your Admin.");
            return View(model);
        }

        ViewBag.SuccessMessage = "If the account exists, a password reset link has been sent to the email address.";
        ModelState.Clear();
        return View(new ForgotPasswordViewModel());
    }

    [HttpGet]
    public async Task<IActionResult> VerifyMfa()
    {
        ViewData["HideNavigation"] = true;

        var userId = GetPendingLoginUserId();
        if (!userId.HasValue)
        {
            TempData["AuthMessage"] = "Your verification session expired. Please sign in again.";
            return RedirectToAction(nameof(Login));
        }

        var user = await _db.Users
            .AsNoTracking()
            .FirstOrDefaultAsync(u => u.UserId == userId.Value && u.IsActive);

        if (user is null)
        {
            ClearPendingLoginSession();
            TempData["AuthMessage"] = "Unable to continue verification. Please sign in again.";
            return RedirectToAction(nameof(Login));
        }

        var canUseTotp = !string.IsNullOrWhiteSpace(user.TotpSecretKey);
        var canUseEmailOtp = !string.IsNullOrWhiteSpace(user.Email);

        return View(new MfaChallengeViewModel
        {
            CanUseTotp = canUseTotp,
            CanUseEmailOtp = canUseEmailOtp,
            SelectedMethod = GetDefaultMfaMethod(canUseEmailOtp, canUseTotp),
            RememberMe = HttpContext.Session.GetString("PendingLoginRememberMe") == "1",
            ReturnUrl = HttpContext.Session.GetString("PendingLoginReturnUrl")
        });
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> VerifyMfa(MfaChallengeViewModel model)
    {
        ViewData["HideNavigation"] = true;

        var userId = GetPendingLoginUserId();
        if (!userId.HasValue)
        {
            TempData["AuthMessage"] = "Your verification session expired. Please sign in again.";
            return RedirectToAction(nameof(Login));
        }

        var dbUser = await _db.Users
            .Include(u => u.UserRoles).ThenInclude(ur => ur.Role)
            .FirstOrDefaultAsync(u => u.UserId == userId.Value && u.IsActive);

        if (dbUser is null)
        {
            ClearPendingLoginSession();
            TempData["AuthMessage"] = "Unable to continue verification. Please sign in again.";
            return RedirectToAction(nameof(Login));
        }

        model.CanUseTotp = !string.IsNullOrWhiteSpace(dbUser.TotpSecretKey);
        model.CanUseEmailOtp = !string.IsNullOrWhiteSpace(dbUser.Email);
        model.RememberMe = HttpContext.Session.GetString("PendingLoginRememberMe") == "1";
        model.ReturnUrl = HttpContext.Session.GetString("PendingLoginReturnUrl");

        var selectedMethod = NormalizeMfaMethod(model.SelectedMethod)
            ?? GetDefaultMfaMethod(model.CanUseEmailOtp, model.CanUseTotp);
        model.SelectedMethod = selectedMethod;
        ModelState.Remove(nameof(model.SelectedMethod));

        if (!ModelState.IsValid)
        {
            return View(model);
        }

        if (selectedMethod == MfaMethodEmail && !model.CanUseEmailOtp)
        {
            ModelState.AddModelError(string.Empty, "Email verification is not available for this account.");
            return View(model);
        }

        if (selectedMethod == MfaMethodTotp && !model.CanUseTotp)
        {
            ModelState.AddModelError(string.Empty, "Authenticator verification is not available for this account.");
            return View(model);
        }

        if (dbUser.IsPermanentlyLocked)
        {
            ClearPendingLoginSession();
            TempData["AuthMessage"] = "Account is permanently locked. Please contact a SuperAdmin.";
            return RedirectToAction(nameof(Login));
        }

        var verified = false;
        if (selectedMethod == MfaMethodTotp)
        {
            if (string.IsNullOrWhiteSpace(model.TotpCode))
            {
                ModelState.AddModelError(nameof(model.TotpCode), "Enter the 6-digit code from your authenticator app.");
                return View(model);
            }

            verified = _mfa.VerifyTotpCode(dbUser.TotpSecretKey!, model.TotpCode);
        }
        else
        {
            if (string.IsNullOrWhiteSpace(model.EmailCode))
            {
                ModelState.AddModelError(nameof(model.EmailCode), "Enter the 6-digit code sent to your email.");
                return View(model);
            }

            verified = VerifyEmailOtp(dbUser, model.EmailCode);
            if (!verified)
            {
                dbUser.EmailOtpFailedAttempts += 1;
            }
        }

        if (!verified)
        {
            await _db.SaveChangesAsync();
            ModelState.AddModelError(string.Empty, "Invalid verification code.");
            return View(model);
        }

        dbUser.EmailOtpHash = null;
        dbUser.EmailOtpExpiresAt = null;
        dbUser.EmailOtpFailedAttempts = 0;
        dbUser.LastMfaAt = DateTime.UtcNow;
        await _db.SaveChangesAsync();

        var roleName = dbUser.UserRoles.FirstOrDefault()?.Role?.RoleName ?? "Billing";
        if (!Enum.TryParse<UserRole>(roleName, out var userRole))
            userRole = UserRole.Billing;

        ClearPendingLoginSession();
        return await CompleteSignInAsync(dbUser, userRole, model.RememberMe, model.ReturnUrl);
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> ResendMfaEmailCode()
    {
        var userId = GetPendingLoginUserId();
        if (!userId.HasValue)
        {
            TempData["AuthMessage"] = "Your verification session expired. Please sign in again.";
            return RedirectToAction(nameof(Login));
        }

        var dbUser = await _db.Users.FirstOrDefaultAsync(u => u.UserId == userId.Value && u.IsActive);
        if (dbUser is null)
        {
            TempData["AuthMessage"] = "Unable to continue verification. Please sign in again.";
            return RedirectToAction(nameof(Login));
        }

        await IssueEmailOtpAsync(dbUser);
        await _db.SaveChangesAsync();
        TempData["AuthMessage"] = "A new email verification code has been sent.";
        return RedirectToAction(nameof(VerifyMfa));
    }

    [HttpGet]
    public async Task<IActionResult> ResetPassword(string? email, string? token)
    {
        ViewData["HideNavigation"] = true;

        if (string.IsNullOrWhiteSpace(email) || string.IsNullOrWhiteSpace(token))
        {
            TempData["AuthMessage"] = "The password reset link is invalid or incomplete.";
            return RedirectToAction(nameof(ForgotPassword));
        }

        var isValid = await _passwordReset.ValidateTokenAsync(email, token);
        if (!isValid)
        {
            TempData["AuthMessage"] = "This password reset link is invalid or has expired. Please request a new one.";
            return RedirectToAction(nameof(ForgotPassword));
        }

        return View(new ResetPasswordViewModel
        {
            Email = email,
            Token = token
        });
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> ResetPassword(ResetPasswordViewModel model)
    {
        ViewData["HideNavigation"] = true;

        if (!ModelState.IsValid)
        {
            return View(model);
        }

        if (_passwordBlacklist.IsDisallowed(model.NewPassword))
        {
            ModelState.AddModelError(nameof(model.NewPassword), _passwordBlacklist.ErrorMessage);
            return View(model);
        }

        var ip = HttpContext.Connection.RemoteIpAddress?.ToString();
        var success = await _passwordReset.ResetPasswordAsync(model.Email, model.Token, model.NewPassword, ip);
        if (!success)
        {
            TempData["AuthMessage"] = "This password reset link is invalid or has expired. Please request a new one.";
            return RedirectToAction(nameof(ForgotPassword));
        }

        await HttpContext.SignOutAsync(CookieAuthenticationDefaults.AuthenticationScheme);
        TempData["AuthMessage"] = "Your password has been reset successfully. Please sign in.";
        return RedirectToAction(nameof(Login));
    }

    [HttpGet]
    public async Task<IActionResult> ForcePasswordChange()
    {
        if (!(User.Identity?.IsAuthenticated ?? false))
        {
            return RedirectToAction(nameof(Login));
        }

        var userIdClaim = User.FindFirstValue("UserId");
        if (!long.TryParse(userIdClaim, out var userId))
        {
            await HttpContext.SignOutAsync(CookieAuthenticationDefaults.AuthenticationScheme);
            return RedirectToAction(nameof(Login));
        }

        var user = await _db.Users
            .AsNoTracking()
            .Include(u => u.UserRoles)
                .ThenInclude(ur => ur.Role)
            .FirstOrDefaultAsync(u => u.UserId == userId && u.IsActive);

        if (user is null)
        {
            await HttpContext.SignOutAsync(CookieAuthenticationDefaults.AuthenticationScheme);
            return RedirectToAction(nameof(Login));
        }

        if (!user.MustChangePassword)
        {
            var roleName = user.UserRoles.FirstOrDefault()?.Role?.RoleName ?? "Billing";
            if (!Enum.TryParse<UserRole>(roleName, out var role))
            {
                role = UserRole.Billing;
            }

            if (RequiresFirstTimeMfaOnboarding(user, role))
            {
                return RedirectToAction(nameof(SetupMfa));
            }

            return RedirectToRoleDashboard(role);
        }

        ViewData["HideNavigation"] = true;
        return View(new ForcePasswordChangeViewModel());
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> ForcePasswordChange(ForcePasswordChangeViewModel model)
    {
        if (!(User.Identity?.IsAuthenticated ?? false))
        {
            return RedirectToAction(nameof(Login));
        }

        ViewData["HideNavigation"] = true;

        if (!ModelState.IsValid)
        {
            return View(model);
        }

        var userIdClaim = User.FindFirstValue("UserId");
        if (!long.TryParse(userIdClaim, out var userId))
        {
            await HttpContext.SignOutAsync(CookieAuthenticationDefaults.AuthenticationScheme);
            return RedirectToAction(nameof(Login));
        }

        var user = await _db.Users
            .Include(u => u.UserRoles)
                .ThenInclude(ur => ur.Role)
            .FirstOrDefaultAsync(u => u.UserId == userId && u.IsActive);

        if (user is null)
        {
            await HttpContext.SignOutAsync(CookieAuthenticationDefaults.AuthenticationScheme);
            return RedirectToAction(nameof(Login));
        }

        if (!user.MustChangePassword)
        {
            var roleName = user.UserRoles.FirstOrDefault()?.Role?.RoleName ?? "Billing";
            if (!Enum.TryParse<UserRole>(roleName, out var role))
            {
                role = UserRole.Billing;
            }

            return RedirectToRoleDashboard(role);
        }

        if (BCrypt.Net.BCrypt.Verify(model.NewPassword, user.PasswordHash))
        {
            ModelState.AddModelError(nameof(model.NewPassword), "New password must be different from your temporary password.");
            return View(model);
        }

        if (_passwordBlacklist.IsDisallowed(model.NewPassword))
        {
            ModelState.AddModelError(nameof(model.NewPassword), _passwordBlacklist.ErrorMessage);
            return View(model);
        }

        user.PasswordHash = BCrypt.Net.BCrypt.HashPassword(model.NewPassword, workFactor: 12);
        user.MustChangePassword = false;
        user.TemporaryPasswordIssuedAt = null;
        user.AuthVersion += 1;
        user.UpdatedAt = DateTime.UtcNow;

        await _db.SaveChangesAsync();

        var roleNameAfter = user.UserRoles.FirstOrDefault()?.Role?.RoleName ?? "Billing";
        if (!Enum.TryParse<UserRole>(roleNameAfter, out var roleAfter))
        {
            roleAfter = UserRole.Billing;
        }

        await SignInPrincipalAsync(user, roleAfter, rememberMe: false);

        var ip = HttpContext.Connection.RemoteIpAddress?.ToString();
        await _audit.LogAsync(user.ShopId, user.UserId, "PasswordChangedFirstLogin", "User", user.UserId,
            "User changed temporary password on first login.", ip);

        if (RequiresFirstTimeMfaOnboarding(user, roleAfter))
        {
            TempData["Success"] = "Password updated. Set up multi-factor authentication to continue.";
            return RedirectToAction(nameof(SetupMfa));
        }

        TempData["Success"] = "Your password was updated successfully.";
        return RedirectToRoleDashboard(roleAfter);
    }

    [HttpGet]
    public async Task<IActionResult> SetupMfa(bool fromPreferences = false, bool regenerate = false)
    {
        if (!(User.Identity?.IsAuthenticated ?? false))
        {
            return RedirectToAction(nameof(Login));
        }

        if (!ModelState.IsValid)
        {
            return BadRequest();
        }

        ViewData["HideNavigation"] = true;

        var userIdClaim = User.FindFirstValue("UserId");
        if (!long.TryParse(userIdClaim, out var userId))
        {
            await HttpContext.SignOutAsync(CookieAuthenticationDefaults.AuthenticationScheme);
            return RedirectToAction(nameof(Login));
        }

        var user = await _db.Users
            .Include(u => u.UserRoles)
                .ThenInclude(ur => ur.Role)
            .FirstOrDefaultAsync(u => u.UserId == userId && u.IsActive);

        if (user is null)
        {
            await HttpContext.SignOutAsync(CookieAuthenticationDefaults.AuthenticationScheme);
            return RedirectToAction(nameof(Login));
        }

        var roleName = user.UserRoles.FirstOrDefault()?.Role?.RoleName ?? "Billing";
        if (!Enum.TryParse<UserRole>(roleName, out var userRole))
        {
            userRole = UserRole.Billing;
        }

        if (user.MustChangePassword)
        {
            return RedirectToAction(nameof(ForcePasswordChange));
        }

        if (userRole is not UserRole.Admin and not UserRole.SuperAdmin)
        {
            return RedirectToRoleDashboard(userRole);
        }

        var isOnboarding = RequiresFirstTimeMfaOnboarding(user, userRole);
        if (!isOnboarding && !fromPreferences && user.IsMfaEnabled)
        {
            return RedirectToRoleDashboard(userRole);
        }

        var setupSecret = HttpContext.Session.GetString(PendingMfaSetupSecretSessionKey);
        if (regenerate || string.IsNullOrWhiteSpace(setupSecret))
        {
            setupSecret = _mfa.GenerateTotpSecret();
            HttpContext.Session.SetString(PendingMfaSetupSecretSessionKey, setupSecret);
            HttpContext.Session.Remove(PendingMfaSetupAttemptsSessionKey);
        }

        PopulateSetupMfaViewData(user, setupSecret, fromPreferences);
        return View(new MfaChallengeViewModel { CanUseTotp = true });
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> SetupMfa(MfaChallengeViewModel model, bool fromPreferences = false)
    {
        if (!(User.Identity?.IsAuthenticated ?? false))
        {
            return RedirectToAction(nameof(Login));
        }

        ViewData["HideNavigation"] = true;

        var userIdClaim = User.FindFirstValue("UserId");
        if (!long.TryParse(userIdClaim, out var userId))
        {
            await HttpContext.SignOutAsync(CookieAuthenticationDefaults.AuthenticationScheme);
            return RedirectToAction(nameof(Login));
        }

        var user = await _db.Users
            .Include(u => u.UserRoles)
                .ThenInclude(ur => ur.Role)
            .FirstOrDefaultAsync(u => u.UserId == userId && u.IsActive);

        if (user is null)
        {
            await HttpContext.SignOutAsync(CookieAuthenticationDefaults.AuthenticationScheme);
            return RedirectToAction(nameof(Login));
        }

        var roleName = user.UserRoles.FirstOrDefault()?.Role?.RoleName ?? "Billing";
        if (!Enum.TryParse<UserRole>(roleName, out var userRole))
        {
            userRole = UserRole.Billing;
        }

        if (user.MustChangePassword)
        {
            return RedirectToAction(nameof(ForcePasswordChange));
        }

        if (userRole is not UserRole.Admin and not UserRole.SuperAdmin)
        {
            return RedirectToRoleDashboard(userRole);
        }

        var setupSecret = HttpContext.Session.GetString(PendingMfaSetupSecretSessionKey);
        if (string.IsNullOrWhiteSpace(setupSecret))
        {
            TempData["AuthMessage"] = "MFA setup session expired. Please try again.";
            return RedirectToAction(nameof(SetupMfa), new { fromPreferences, regenerate = true });
        }

        if (!ModelState.IsValid)
        {
            PopulateSetupMfaViewData(user, setupSecret, fromPreferences);
            return View(model);
        }

        if (!_mfa.VerifyTotpCode(setupSecret, model.TotpCode ?? string.Empty))
        {
            var attempts = 0;
            _ = int.TryParse(HttpContext.Session.GetString(PendingMfaSetupAttemptsSessionKey), out attempts);
            attempts += 1;
            HttpContext.Session.SetString(PendingMfaSetupAttemptsSessionKey, attempts.ToString());

            if (attempts >= MaxMfaSetupAttempts)
            {
                HttpContext.Session.Remove(PendingMfaSetupSecretSessionKey);
                HttpContext.Session.Remove(PendingMfaSetupAttemptsSessionKey);
                TempData["AuthMessage"] = "Too many invalid codes. Restart MFA setup and try again.";
                return RedirectToAction(nameof(SetupMfa), new { fromPreferences, regenerate = true });
            }

            var remaining = MaxMfaSetupAttempts - attempts;
            ModelState.AddModelError(nameof(model.TotpCode), $"Invalid authenticator code. {remaining} attempt{(remaining == 1 ? string.Empty : "s")} remaining.");
            PopulateSetupMfaViewData(user, setupSecret, fromPreferences);
            return View(model);
        }

        var wasEnabled = user.IsMfaEnabled;
        user.TotpSecretKey = setupSecret;
        user.IsMfaEnabled = true;
        user.MfaType = "TOTP";
        user.LastMfaAt = DateTime.UtcNow;
        user.AuthVersion += 1;
        user.EmailOtpHash = null;
        user.EmailOtpExpiresAt = null;
        user.EmailOtpFailedAttempts = 0;
        user.UpdatedAt = DateTime.UtcNow;

        await _db.SaveChangesAsync();

        HttpContext.Session.Remove(PendingMfaSetupSecretSessionKey);
        HttpContext.Session.Remove(PendingMfaSetupAttemptsSessionKey);

        var ip = HttpContext.Connection.RemoteIpAddress?.ToString();
        await _audit.LogAsync(user.ShopId, user.UserId,
            wasEnabled ? "MfaResetCompleted" : "MfaEnabled",
            "User", user.UserId,
            wasEnabled ? "User completed MFA reconfiguration." : "User enabled MFA.",
            ip);

        await SignInPrincipalAsync(user, userRole, rememberMe: false);

        if (fromPreferences)
        {
            TempData["Success"] = "Multi-factor authentication has been updated.";
            return RedirectToAction("Preferences", "Profile");
        }

        TempData["Success"] = "Multi-factor authentication has been enabled.";
        return RedirectToRoleDashboard(userRole);
    }

    private async Task<IActionResult> CompleteSignInAsync(User dbUser, UserRole userRole, bool rememberMe, string? returnUrl)
    {
        dbUser.LastLoginAt = DateTime.UtcNow;
        dbUser.LastIpAddress = HttpContext.Connection.RemoteIpAddress?.ToString();
        dbUser.FailedLoginAttempts = 0;
        dbUser.LockoutEndAt = null;
        await _db.SaveChangesAsync();

        await SignInPrincipalAsync(dbUser, userRole, rememberMe);

        var ip = HttpContext.Connection.RemoteIpAddress?.ToString();
        await _audit.LogAsync(dbUser.ShopId, dbUser.UserId, "Login", "User", dbUser.UserId,
            $"User '{dbUser.UserName}' logged in successfully.", ip);

        if (dbUser.MustChangePassword)
        {
            return RedirectToAction(nameof(ForcePasswordChange));
        }

        if (RequiresFirstTimeMfaOnboarding(dbUser, userRole))
        {
            return RedirectToAction(nameof(SetupMfa));
        }

        if (!string.IsNullOrEmpty(returnUrl) && Url.IsLocalUrl(returnUrl))
        {
            return Redirect(returnUrl);
        }

        return RedirectToRoleDashboard(userRole);
    }

    private async Task SignInPrincipalAsync(User dbUser, UserRole userRole, bool rememberMe)
    {

        var claims = new List<Claim>
        {
            new Claim(ClaimTypes.NameIdentifier, dbUser.UserId.ToString()),
            new Claim(ClaimTypes.Name, dbUser.FullName),
            new Claim("UserId", dbUser.UserId.ToString()),
            new Claim("AuthVersion", dbUser.AuthVersion.ToString()),
            new Claim("LastActivityUtc", DateTime.UtcNow.ToString("O")),
            new Claim("FullName", dbUser.FullName),
            new Claim("FirstName", dbUser.FirstName),
            new Claim("LastName", dbUser.LastName),
            new Claim("Initials", dbUser.Initials),
            new Claim("Role", userRole.ToString()),
            new Claim("ShopId", dbUser.ShopId.ToString()),
            new Claim("MustChangePassword", dbUser.MustChangePassword ? "1" : "0"),
            new Claim("IsMfaEnabled", dbUser.IsMfaEnabled ? "1" : "0"),
            new Claim("MfaOnboarded", dbUser.LastMfaAt.HasValue ? "1" : "0")
        };

        var identity = new ClaimsIdentity(claims, CookieAuthenticationDefaults.AuthenticationScheme);
        var principal = new ClaimsPrincipal(identity);

        var authProperties = new AuthenticationProperties
        {
            IsPersistent = rememberMe,
            ExpiresUtc = DateTimeOffset.UtcNow.AddHours(8)
        };

        await HttpContext.SignInAsync(CookieAuthenticationDefaults.AuthenticationScheme, principal, authProperties);

        Response.Cookies.Append("ByteBillTheme", dbUser.ThemePreference ?? "light", new CookieOptions
        {
            HttpOnly = true,
            Secure = true,
            Expires = DateTimeOffset.UtcNow.AddDays(365),
            SameSite = SameSiteMode.Lax,
            Path = "/"
        });
    }

    private async Task<(int AttemptsRemaining, string? LockoutMessage)> RegisterFailedPasswordAttemptAsync(User dbUser)
    {
        dbUser.LastFailedLoginAt = DateTime.UtcNow;
        dbUser.FailedLoginAttempts += 1;

        if (dbUser.FailedLoginAttempts < MaxFailedAttempts)
        {
            await _db.SaveChangesAsync();
            return (MaxFailedAttempts - dbUser.FailedLoginAttempts, null);
        }

        dbUser.FailedLoginAttempts = 0;
        dbUser.LockoutCycleCount += 1;

        if (dbUser.LockoutCycleCount == 1)
        {
            dbUser.LockoutEndAt = DateTime.UtcNow.AddMinutes(15);
            dbUser.LockoutReason = "Too many failed password attempts (cycle 1).";
            await _db.SaveChangesAsync();
            return (0, "Too many failed attempts. Account locked for 15 minutes.");
        }

        if (dbUser.LockoutCycleCount == 2)
        {
            dbUser.LockoutEndAt = DateTime.UtcNow.AddMinutes(30);
            dbUser.LockoutReason = "Too many failed password attempts (cycle 2).";
            await _db.SaveChangesAsync();
            return (0, "Too many failed attempts again. Account locked for 30 minutes.");
        }

        dbUser.IsPermanentlyLocked = true;
        dbUser.PermanentlyLockedAt = DateTime.UtcNow;
        dbUser.LockoutEndAt = null;
        dbUser.LockoutReason = "Permanently locked after repeated failed login lock cycles.";
        await _db.SaveChangesAsync();

        return (0, "Account is permanently locked. Please contact a SuperAdmin.");
    }

    private static string? NormalizeMfaMethod(string? method)
    {
        if (string.Equals(method, MfaMethodEmail, StringComparison.OrdinalIgnoreCase))
        {
            return MfaMethodEmail;
        }

        if (string.Equals(method, MfaMethodTotp, StringComparison.OrdinalIgnoreCase))
        {
            return MfaMethodTotp;
        }

        return null;
    }

    private static string GetDefaultMfaMethod(bool canUseEmailOtp, bool canUseTotp)
    {
        if (canUseEmailOtp)
        {
            return MfaMethodEmail;
        }

        if (canUseTotp)
        {
            return MfaMethodTotp;
        }

        return MfaMethodEmail;
    }

    private async Task IssueEmailOtpAsync(User user)
    {
        if (string.IsNullOrWhiteSpace(user.Email))
        {
            return;
        }

        var code = _mfa.GenerateEmailOtpCode();
        var expiresAt = DateTime.UtcNow.AddMinutes(Math.Max(3, _securitySettings.EmailOtpExpiryMinutes));
        user.EmailOtpHash = _mfa.HashToken(code);
        user.EmailOtpExpiresAt = expiresAt;
        user.EmailOtpFailedAttempts = 0;

        await _email.SendSecurityCodeAsync(user.Email, user.FullName, code, expiresAt);
    }

    private bool VerifyEmailOtp(User user, string code)
    {
        if (string.IsNullOrWhiteSpace(user.EmailOtpHash) || !user.EmailOtpExpiresAt.HasValue)
        {
            return false;
        }

        if (user.EmailOtpExpiresAt.Value < DateTime.UtcNow)
        {
            return false;
        }

        if (user.EmailOtpFailedAttempts >= Math.Max(3, _securitySettings.EmailOtpMaxAttempts))
        {
            return false;
        }

        var hash = _mfa.HashToken(code.Trim());
        return string.Equals(hash, user.EmailOtpHash, StringComparison.OrdinalIgnoreCase);
    }

    private long? GetPendingLoginUserId()
    {
        var value = HttpContext.Session.GetString("PendingLoginUserId");
        return long.TryParse(value, out var userId) ? userId : null;
    }

    private void ClearPendingLoginSession()
    {
        HttpContext.Session.Remove("PendingLoginUserId");
        HttpContext.Session.Remove("PendingLoginRememberMe");
        HttpContext.Session.Remove("PendingLoginReturnUrl");
    }

    private static bool RequiresFirstTimeMfaOnboarding(User user, UserRole role)
    {
        return role == UserRole.Admin
            && !user.MustChangePassword
            && !user.IsMfaEnabled
            && !user.LastMfaAt.HasValue;
    }

    private void PopulateSetupMfaViewData(User user, string setupSecret, bool fromPreferences)
    {
        var identifier = string.IsNullOrWhiteSpace(user.Email) ? user.UserName : user.Email!;
        const string issuer = "ByteBill";

        var otpauthUri = $"otpauth://totp/{Uri.EscapeDataString(issuer)}:{Uri.EscapeDataString(identifier)}?secret={Uri.EscapeDataString(setupSecret)}&issuer={Uri.EscapeDataString(issuer)}&digits=6&period=30";
        var qrCodeUrl = "https://api.qrserver.com/v1/create-qr-code/?size=220x220&data=" + Uri.EscapeDataString(otpauthUri);

        ViewData["SetupMfaManualKey"] = setupSecret;
        ViewData["SetupMfaQrCodeUrl"] = qrCodeUrl;
        ViewData["FromPreferences"] = fromPreferences;
    }

    private IActionResult RedirectToRoleDashboard(UserRole? role = null)
    {
        var userRole = role ?? GetUserRole();
        
        return userRole switch
        {
            UserRole.SuperAdmin => RedirectToAction("Index", "Dashboard", new { area = "SuperAdmin" }),
            UserRole.Admin => RedirectToAction("Index", "Dashboard", new { area = "Admin" }),
            UserRole.Billing => RedirectToAction("Index", "Dashboard", new { area = "Billing" }),
            UserRole.Technician => RedirectToAction("Index", "Dashboard", new { area = "Technician" }),
            UserRole.Auditor => RedirectToAction("Index", "Dashboard", new { area = "Auditor" }),
            _ => RedirectToAction(nameof(Login))
        };
    }

    private UserRole GetUserRole()
    {
        var roleClaim = User.Claims.FirstOrDefault(c => c.Type == "Role")?.Value;
        return Enum.TryParse<UserRole>(roleClaim, out var role) ? role : UserRole.Billing;
    }
}
