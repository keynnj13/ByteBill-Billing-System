using ByteBill_BS.Models.Enums;
using ByteBill_BS.Services;
using ByteBill_BS.ViewModels.Register;
using ByteBill_BS.Extensions;
using Microsoft.AspNetCore.Mvc;
using System.Text.Json;

namespace ByteBill_BS.Controllers;

/// <summary>
/// Self-service registration flow (Register → Pay):
///   1. GET  CreateAccount      → Show registration form (plan pre-selected from landing)
///   2. POST CreateAccount      → Validate form, store in session, create PayMongo checkout, redirect
///   3. GET  PaymentSuccess     → After payment, verify, create shop + user, auto-login
///   4. GET  PaymentCancelled   → Show retry page with saved info
/// </summary>
public class RegisterController : Controller
{
    private readonly IRegistrationService _reg;
    private readonly INotificationService _notifications;
    private readonly ILogger<RegisterController> _logger;

    private const string RegSessionKey = "BB_PendingRegistration";

    public RegisterController(IRegistrationService reg, INotificationService notifications, ILogger<RegisterController> logger)
    {
        _reg = reg;
        _notifications = notifications;
        _logger = logger;
    }

    // ═══════════════════════════════════════════════════════════════════
    //  STEP 1: REGISTRATION FORM (before payment)
    // ═══════════════════════════════════════════════════════════════════
    [HttpGet]
    public async Task<IActionResult> CreateAccount(long planId, string cycle = "Monthly")
    {
        if (!ModelState.IsValid) return BadRequest();

        var plan = await _reg.GetPlanAsync(planId);
        if (plan is null)
        {
            TempData["Error"] = "The selected plan was not found.";
            return RedirectToAction("Index", "Landing");
        }

        if (cycle != "Monthly" && cycle != "Yearly" && cycle != "Permanent")
            cycle = "Monthly";

        var price = cycle switch
        {
            "Yearly" => plan.YearlyPrice,
            "Permanent" => plan.PermanentPrice,
            _ => plan.MonthlyPrice
        };

        var vm = new CreateAccountViewModel
        {
            PlanId = planId,
            BillingCycle = cycle,
            PlanName = plan.PlanName,
            Price = price
        };

        ViewData["HideNavigation"] = true;
        return View(vm);
    }

    // ═══════════════════════════════════════════════════════════════════
    //  STEP 2: SUBMIT REGISTRATION → STORE DATA → PAY
    // ═══════════════════════════════════════════════════════════════════
    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> CreateAccount(CreateAccountViewModel model)
    {
        ViewData["HideNavigation"] = true;

        if (!ModelState.IsValid)
            return View(model);

        // Check uniqueness before going to payment
        var uniquenessError = await _reg.CheckUniquenessAsync(model.ShopName, model.UserName, model.Email);
        if (uniquenessError != null)
        {
            ModelState.AddModelError("", uniquenessError);
            return View(model);
        }

        // Store registration data in session so we can retrieve after payment
        var json = JsonSerializer.Serialize(model);
        HttpContext.Session.SetString(RegSessionKey, json);

        // Create PayMongo checkout
        var (success, checkoutUrl, sessionId, error) =
            await _reg.CreateSubscriptionCheckoutAsync(model.PlanId, model.BillingCycle);

        if (!success || string.IsNullOrEmpty(checkoutUrl))
        {
            ModelState.AddModelError("", error ?? "Failed to create payment session. Please try again.");
            return View(model);
        }

        // Store PayMongo session ID in cookie for verification after redirect
        Response.Cookies.Append("BB_CheckoutSession", sessionId!, new CookieOptions
        {
            HttpOnly = true,
            Secure = true,
            SameSite = SameSiteMode.Lax,
            MaxAge = TimeSpan.FromHours(1),
            Path = "/Register"
        });

        _logger.LogInformation("Redirecting to PayMongo checkout {SessionId} for plan {PlanId}", sessionId, model.PlanId);
        return Redirect(checkoutUrl);
    }

    // ═══════════════════════════════════════════════════════════════════
    //  STEP 3: PAYMENT SUCCESS → CREATE ACCOUNT → AUTO-LOGIN
    // ═══════════════════════════════════════════════════════════════════
    [HttpGet]
    public async Task<IActionResult> PaymentSuccess(
        string? session_id,
        [FromCookie(Name = "BB_CheckoutSession")] string? checkoutSession)
    {
        ViewData["HideNavigation"] = true;

        var sessionId = checkoutSession ?? session_id;
        if (string.IsNullOrWhiteSpace(sessionId))
        {
            TempData["Error"] = "Invalid payment session. Please start the registration again.";
            return RedirectToAction("Index", "Landing");
        }

        // Verify payment with PayMongo
        var (paid, planId, billingCycle, amount, paymentMethod, payMongoPaymentId) =
            await _reg.VerifyCheckoutSessionAsync(sessionId);

        if (!paid)
        {
            TempData["Error"] = "Payment was not completed. Please try again.";
            return RedirectToAction("Index", "Landing");
        }

        // Retrieve registration data from session
        var regJson = HttpContext.Session.GetString(RegSessionKey);
        if (string.IsNullOrEmpty(regJson))
        {
            TempData["Error"] = "Registration data expired. Please register again.";
            return RedirectToAction("Index", "Landing");
        }

        var model = JsonSerializer.Deserialize<CreateAccountViewModel>(regJson);
        if (model is null)
        {
            TempData["Error"] = "Registration data is invalid. Please register again.";
            return RedirectToAction("Index", "Landing");
        }

        // Create the account
        var result = await _reg.CreateAccountAsync(model, amount, paymentMethod, payMongoPaymentId);

        if (!result.Success)
        {
            TempData["Error"] = result.ErrorMessage ?? "Registration failed. Please contact support.";
            return RedirectToAction("Index", "Landing");
        }

        // Clean up session and cookie
        HttpContext.Session.Remove(RegSessionKey);
        Response.Cookies.Delete("BB_CheckoutSession", new CookieOptions
        {
            Path = "/Register",
            Secure = true,
            HttpOnly = true,
            SameSite = SameSiteMode.Lax
        });

        _logger.LogInformation("New shop registered: {ShopId} by user {UserName}", result.ShopId, result.UserName);

        // Notify SuperAdmin users
        await _notifications.NotifySuperAdminsAsync(result.ShopId,
            "New Shop Registered",
            $"{model.ShopName} has registered with the {model.PlanName} plan ({model.BillingCycle}).",
            "info", "/SuperAdmin/Shops");

        await _notifications.NotifySuperAdminsAsync(result.ShopId,
            "Subscription Payment Received",
            $"Payment of {amount:C} received from {model.ShopName} for {model.PlanName} ({model.BillingCycle}).",
            "info", "/SuperAdmin/Payments");

        TempData["AuthMessage"] = "Payment successful. Your temporary login password was sent to your email. Sign in to continue.";
        return RedirectToAction("Login", "Auth");
    }

    // ═══════════════════════════════════════════════════════════════════
    //  PAYMENT CANCELLED / FAILED — Retry with saved data
    // ═══════════════════════════════════════════════════════════════════
    [HttpGet]
    public IActionResult PaymentCancelled()
    {
        ViewData["HideNavigation"] = true;

        // Try to recover registration data so user can retry
        var regJson = HttpContext.Session.GetString(RegSessionKey);
        if (!string.IsNullOrEmpty(regJson))
        {
            var model = JsonSerializer.Deserialize<CreateAccountViewModel>(regJson);
            if (model != null)
            {
                ViewBag.PlanId = model.PlanId;
                ViewBag.Cycle = model.BillingCycle;
                ViewBag.PlanName = model.PlanName;
                ViewBag.Price = model.Price;
                ViewBag.ShopName = model.ShopName;
                ViewBag.HasSavedData = true;
            }
        }

        return View();
    }

    // ═══════════════════════════════════════════════════════════════════
    //  RETRY PAYMENT (re-create PayMongo checkout from saved session)
    // ═══════════════════════════════════════════════════════════════════
    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> RetryPayment()
    {
        var regJson = HttpContext.Session.GetString(RegSessionKey);
        if (string.IsNullOrEmpty(regJson))
        {
            TempData["Error"] = "Registration data expired. Please register again.";
            return RedirectToAction("Index", "Landing");
        }

        var model = JsonSerializer.Deserialize<CreateAccountViewModel>(regJson);
        if (model is null)
        {
            TempData["Error"] = "Registration data is invalid. Please register again.";
            return RedirectToAction("Index", "Landing");
        }

        var (success, checkoutUrl, sessionId, error) =
            await _reg.CreateSubscriptionCheckoutAsync(model.PlanId, model.BillingCycle);

        if (!success || string.IsNullOrEmpty(checkoutUrl))
        {
            TempData["Error"] = error ?? "Failed to create payment session.";
            return RedirectToAction("PaymentCancelled");
        }

        Response.Cookies.Append("BB_CheckoutSession", sessionId!, new CookieOptions
        {
            HttpOnly = true,
            Secure = true,
            SameSite = SameSiteMode.Lax,
            MaxAge = TimeSpan.FromHours(1),
            Path = "/Register"
        });

        return Redirect(checkoutUrl);
    }
}
