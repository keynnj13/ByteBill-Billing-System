using ByteBill_BS.Data;
using ByteBill_BS.Models;
using ByteBill_BS.ViewModels.Register;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using System.Net.Http.Headers;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using System.Text.Json.Nodes;

namespace ByteBill_BS.Services;

// ── Interface ────────────────────────────────────────────────────────────
public interface IRegistrationService
{
    /// <summary>Get a subscription plan by ID for the checkout page.</summary>
    Task<SubscriptionPlan?> GetPlanAsync(long planId);

    /// <summary>Get all active plans (for plan selection / landing page).</summary>
    Task<List<SubscriptionPlan>> GetActivePlansAsync();

    /// <summary>Create a PayMongo checkout session for a subscription plan.</summary>
    Task<(bool Success, string? CheckoutUrl, string? SessionId, string? Error)>
        CreateSubscriptionCheckoutAsync(long planId, string billingCycle);

    /// <summary>Verify a PayMongo checkout session was paid.</summary>
    Task<(bool Paid, long PlanId, string BillingCycle, decimal Amount, string? PaymentMethod, string? PayMongoPaymentId)>
        VerifyCheckoutSessionAsync(string sessionId);

    /// <summary>Create the shop, admin user, subscription, and payment record.</summary>
    Task<RegistrationResult> CreateAccountAsync(CreateAccountViewModel model,
        decimal paidAmount, string? paymentMethod, string? payMongoPaymentId);

    /// <summary>Check if shop name, username, or email is already taken (before payment).</summary>
    Task<string?> CheckUniquenessAsync(string shopName, string username, string email);

    /// <summary>Generate a unique shop code from the shop name.</summary>
    Task<string> GenerateShopCodeAsync(string shopName);
}

// ── Implementation ───────────────────────────────────────────────────────
public class RegistrationService : IRegistrationService
{
    private readonly ApplicationDbContext _db;
    private readonly HttpClient _http;
    private readonly PayMongoSettings _payMongoSettings;
    private readonly ILogger<RegistrationService> _logger;
    private readonly IServiceScopeFactory _scopeFactory;
    private readonly IEmailSecurityService _emailSecurity;

    public RegistrationService(
        ApplicationDbContext db,
        HttpClient http,
        IOptions<PayMongoSettings> payMongoSettings,
        ILogger<RegistrationService> logger,
        IServiceScopeFactory scopeFactory,
        IEmailSecurityService emailSecurity)
    {
        _db = db;
        _http = http;
        _payMongoSettings = payMongoSettings.Value;
        _logger = logger;
        _scopeFactory = scopeFactory;
        _emailSecurity = emailSecurity;

        // Configure HTTP client for PayMongo
        var authBytes = Encoding.UTF8.GetBytes($"{_payMongoSettings.SecretKey}:");
        _http.DefaultRequestHeaders.Authorization =
            new AuthenticationHeaderValue("Basic", Convert.ToBase64String(authBytes));
        _http.DefaultRequestHeaders.Accept.Add(new MediaTypeWithQualityHeaderValue("application/json"));
    }

    public async Task<SubscriptionPlan?> GetPlanAsync(long planId)
    {
        return await _db.SubscriptionPlans
            .FirstOrDefaultAsync(p => p.PlanId == planId && p.IsActive);
    }

    public async Task<List<SubscriptionPlan>> GetActivePlansAsync()
    {
        return await _db.SubscriptionPlans
            .Where(p => p.IsActive)
            .OrderBy(p => p.SortOrder)
            .ToListAsync();
    }

    // ═══════════════════════════════════════════════════════════════════
    //  PAYMONGO CHECKOUT FOR SUBSCRIPTION
    // ═══════════════════════════════════════════════════════════════════
    public async Task<(bool Success, string? CheckoutUrl, string? SessionId, string? Error)>
        CreateSubscriptionCheckoutAsync(long planId, string billingCycle)
    {
        var plan = await GetPlanAsync(planId);
        if (plan is null)
            return (false, null, null, "Selected plan not found.");

        var price = billingCycle switch
        {
            "Yearly" => plan.YearlyPrice,
            "Permanent" => plan.PermanentPrice,
            _ => plan.MonthlyPrice
        };

        var amountCentavos = (long)(price * 100);
        var periodLabel = billingCycle switch
        {
            "Yearly" => "per year",
            "Permanent" => "lifetime",
            _ => "per month"
        };

        // Build the checkout session payload
        // Use SiteBaseUrl from config (falls back to deriving from SuccessUrl)
        var baseUrl = _payMongoSettings.SiteBaseUrl
            ?? _payMongoSettings.SuccessUrl?.Replace("/payment/success", "")
            ?? "https://localhost:7048";
        var successUrl = $"{baseUrl}/Register/PaymentSuccess";
        var cancelUrl = $"{baseUrl}/Register/PaymentCancelled";

        var payload = new
        {
            data = new
            {
                attributes = new
                {
                    send_email_receipt = true,
                    show_description = true,
                    show_line_items = true,
                    description = $"ByteBill {plan.PlanName} Plan — {billingCycle} Subscription",
                    line_items = new[]
                    {
                        new
                        {
                            currency = "PHP",
                            amount = amountCentavos,
                            name = $"ByteBill {plan.PlanName} Plan",
                            description = $"{plan.PlanName} subscription ({periodLabel})",
                            quantity = 1
                        }
                    },
                    payment_method_types = new[] { "gcash", "card", "grab_pay", "paymaya" },
                    success_url = successUrl,
                    cancel_url = cancelUrl,
                    metadata = new
                    {
                        plan_id = planId.ToString(),
                        billing_cycle = billingCycle,
                        plan_name = plan.PlanName,
                        price = price.ToString("F2"),
                        type = "subscription_signup"
                    }
                }
            }
        };

        var json = JsonSerializer.Serialize(payload, new JsonSerializerOptions { PropertyNamingPolicy = JsonNamingPolicy.CamelCase });
        var content = new StringContent(json, Encoding.UTF8, "application/json");

        try
        {
            var response = await _http.PostAsync($"{_payMongoSettings.BaseUrl}/checkout_sessions", content);
            var responseBody = await response.Content.ReadAsStringAsync();

            if (!response.IsSuccessStatusCode)
            {
                _logger.LogWarning("PayMongo subscription checkout failed: {Status} {Body}", response.StatusCode, responseBody);
                return (false, null, null, $"Payment gateway returned {response.StatusCode}.");
            }

            var jsonDoc = JsonNode.Parse(responseBody);
            var sessionId = jsonDoc?["data"]?["id"]?.GetValue<string>() ?? "";
            var checkoutUrl = jsonDoc?["data"]?["attributes"]?["checkout_url"]?.GetValue<string>() ?? "";

            _logger.LogInformation("Created PayMongo subscription checkout session {SessionId} for plan {PlanName} ({Cycle})",
                sessionId, plan.PlanName, billingCycle);

            return (true, checkoutUrl, sessionId, null);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "PayMongo API error creating subscription checkout");
            return (false, null, null, $"Payment gateway error: {ex.Message}");
        }
    }

    // ═══════════════════════════════════════════════════════════════════
    //  VERIFY PAYMONGO CHECKOUT SESSION
    // ═══════════════════════════════════════════════════════════════════
    public async Task<(bool Paid, long PlanId, string BillingCycle, decimal Amount, string? PaymentMethod, string? PayMongoPaymentId)>
        VerifyCheckoutSessionAsync(string sessionId)
    {
        try
        {
            var response = await _http.GetAsync($"{_payMongoSettings.BaseUrl}/checkout_sessions/{sessionId}");
            var responseBody = await response.Content.ReadAsStringAsync();

            if (!response.IsSuccessStatusCode)
            {
                _logger.LogWarning("Failed to retrieve checkout session {SessionId}: {Status}", sessionId, response.StatusCode);
                return (false, 0, "Monthly", 0, null, null);
            }

            var jsonDoc = JsonNode.Parse(responseBody);
            var attrs = jsonDoc?["data"]?["attributes"];
            var status = attrs?["status"]?.GetValue<string>();
            var paymentIntent = attrs?["payment_intent"];
            var piStatus = paymentIntent?["attributes"]?["status"]?.GetValue<string>();

            // Check if payment is confirmed
            var isPaid = status == "active" || piStatus == "succeeded";

            if (!isPaid)
            {
                return (false, 0, "Monthly", 0, null, null);
            }

            // Extract metadata
            var metadata = attrs?["metadata"];
            var planIdStr = metadata?["plan_id"]?.GetValue<string>() ?? "0";
            var billingCycle = metadata?["billing_cycle"]?.GetValue<string>() ?? "Monthly";
            var priceStr = metadata?["price"]?.GetValue<string>() ?? "0";

            long.TryParse(planIdStr, out var planId);
            decimal.TryParse(priceStr, out var amount);

            // Get payment method and payment ID
            string? paymentMethod = null;
            string? payMongoPaymentId = null;

            var payments = attrs?["payments"]?.AsArray();
            if (payments != null && payments.Count > 0)
            {
                var firstPayment = payments[0];
                payMongoPaymentId = firstPayment?["id"]?.GetValue<string>();
                var pmAttrs = firstPayment?["attributes"];
                var source = pmAttrs?["source"];
                paymentMethod = source?["type"]?.GetValue<string>();
            }

            // Fallback: try payment_intent.attributes.payments
            if (payMongoPaymentId == null)
            {
                var piPayments = paymentIntent?["attributes"]?["payments"]?.AsArray();
                if (piPayments != null && piPayments.Count > 0)
                {
                    payMongoPaymentId = piPayments[0]?["id"]?.GetValue<string>();
                }
            }

            return (true, planId, billingCycle, amount, paymentMethod, payMongoPaymentId);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error verifying checkout session {SessionId}", sessionId);
            return (false, 0, "Monthly", 0, null, null);
        }
    }

    // ═══════════════════════════════════════════════════════════════════
    //  CHECK UNIQUENESS (before payment)
    // ═══════════════════════════════════════════════════════════════════
    public async Task<string?> CheckUniquenessAsync(string shopName, string username, string email)
    {
        var emailHash = _emailSecurity.ComputeHash(email);

        var shopNameTaken = await _db.Shops
            .AnyAsync(s => s.ShopName.ToLower() == shopName.ToLower().Trim());
        if (shopNameTaken)
            return "A shop with this name already exists. Please choose a different name.";

        var usernameTaken = await _db.Users
            .AnyAsync(u => u.UserName == username.ToLower().Trim());
        if (usernameTaken)
            return "Username is already taken. Please choose a different one.";

        var emailTaken = await _db.Users
            .AnyAsync(u => u.EmailHash != null && u.EmailHash == emailHash);
        if (emailTaken)
            return "Email is already registered. Please use a different email or log in.";

        return null;
    }

    // ═══════════════════════════════════════════════════════════════════
    //  CREATE ACCOUNT (Shop + User + Subscription + Payment)
    // ═══════════════════════════════════════════════════════════════════
    public async Task<RegistrationResult> CreateAccountAsync(CreateAccountViewModel model,
        decimal paidAmount, string? paymentMethod, string? payMongoPaymentId)
    {
        var emailHash = _emailSecurity.ComputeHash(model.Email);

        // ── Validate uniqueness ──────────────────────────────────────
        var existingUser = await _db.Users
            .AnyAsync(u => u.UserName == model.UserName.ToLower().Trim());
        if (existingUser)
            return RegistrationResult.Fail("Username is already taken. Please choose a different one.");

        var existingEmail = await _db.Users
            .AnyAsync(u => u.EmailHash != null && u.EmailHash == emailHash);
        if (existingEmail)
            return RegistrationResult.Fail("Email is already registered. Please use a different email or log in.");

        var plan = await GetPlanAsync(model.PlanId);
        if (plan is null)
            return RegistrationResult.Fail("Selected subscription plan not found.");

        using var transaction = await _db.Database.BeginTransactionAsync();
        try
        {
            // ── 1. Create Shop ───────────────────────────────────────
            var shopCode = await GenerateShopCodeAsync(model.ShopName);
            var shop = new Shop
            {
                ShopCode = shopCode,
                ShopName = model.ShopName.Trim(),
                Email = model.ShopEmail.Trim(),
                Phone = model.ShopPhone?.Trim(),
                Address = model.ShopAddress?.Trim(),
                TIN = model.TIN?.Trim(),
                IsVatRegistered = model.IsVatRegistered,
                TaxRate = model.IsVatRegistered ? 12m : 3m,
                Status = "Active",
                IsDefault = false,
                CreatedAt = DateTime.UtcNow
            };
            _db.Shops.Add(shop);
            await _db.SaveChangesAsync();

            // ── 2. Create Admin User ─────────────────────────────────
            var temporaryPassword = GenerateTemporaryPassword();
            var user = new User
            {
                ShopId = shop.ShopId,
                FirstName = model.FirstName.Trim(),
                MiddleName = model.MiddleName?.Trim(),
                LastName = model.LastName.Trim(),
                UserName = model.UserName.ToLower().Trim(),
                PasswordHash = BCrypt.Net.BCrypt.HashPassword(temporaryPassword, workFactor: 12),
                Email = model.Email.Trim(),
                MustChangePassword = true,
                TemporaryPasswordIssuedAt = DateTime.UtcNow,
                IsActive = true,
                CreatedAt = DateTime.UtcNow
            };
            _db.Users.Add(user);
            await _db.SaveChangesAsync();

            // ── 3. Assign Admin Role ─────────────────────────────────
            var adminRole = await _db.Roles.FirstOrDefaultAsync(r => r.RoleName == "Admin");
            if (adminRole is not null)
            {
                _db.UserRoles.Add(new UserRoleAssignment
                {
                    UserId = user.UserId,
                    RoleId = adminRole.RoleId,
                    AssignedAt = DateTime.UtcNow
                });
                await _db.SaveChangesAsync();
            }

            // ── 4. Create Subscription ───────────────────────────────
            var price = model.BillingCycle switch
            {
                "Yearly" => plan.YearlyPrice,
                "Permanent" => plan.PermanentPrice,
                _ => plan.MonthlyPrice
            };

            DateTime startDate = DateTime.UtcNow;
            DateTime? endDate = model.BillingCycle switch
            {
                "Monthly" => startDate.AddMonths(1),
                "Yearly" => startDate.AddYears(1),
                "Permanent" => null,
                _ => startDate.AddMonths(1)
            };
            DateTime? nextBillingDate = model.BillingCycle == "Permanent" ? null : endDate;

            var subscription = new Subscription
            {
                ShopId = shop.ShopId,
                PlanId = model.PlanId,
                BillingCycle = model.BillingCycle,
                Status = "Active",
                Price = price,
                StartDate = startDate,
                EndDate = endDate,
                NextBillingDate = nextBillingDate,
                IsDefault = false,
                CreatedAt = DateTime.UtcNow
            };
            _db.Subscriptions.Add(subscription);
            await _db.SaveChangesAsync();

            // ── 5. Create Subscription Payment Record ────────────────
            var refNumber = $"SUBPAY-{DateTime.UtcNow:yyyyMMdd}-{Guid.NewGuid().ToString("N")[..4].ToUpper()}";
            var subPayment = new SubscriptionPayment
            {
                SubscriptionId = subscription.SubscriptionId,
                ShopId = shop.ShopId,
                Amount = paidAmount > 0 ? paidAmount : price,
                Currency = "PHP",
                Status = "Paid",
                PaymentMethod = paymentMethod,
                ReferenceNumber = refNumber,
                PayMongoPaymentId = payMongoPaymentId,
                PeriodStart = startDate,
                PeriodEnd = endDate ?? startDate.AddYears(99),
                PaidAt = DateTime.UtcNow,
                CreatedAt = DateTime.UtcNow
            };
            _db.SubscriptionPayments.Add(subPayment);
            await _db.SaveChangesAsync();

            // Fire-and-forget: send subscription confirmation email to shop owner
            var subPayId = subPayment.SubscriptionPaymentId;
            _ = Task.Run(async () =>
            {
                try
                {
                    using var scope = _scopeFactory.CreateScope();
                    var emailService = scope.ServiceProvider.GetRequiredService<IEmailService>();
                    await emailService.SendSubscriptionConfirmationAsync(subPayId);
                    await emailService.SendInitialCredentialsAsync(user.Email!, user.FullName, user.UserName, temporaryPassword);
                }
                catch (Exception ex)
                {
                    Console.WriteLine($"[EMAIL ERROR] Subscription email failed for SubscriptionPaymentId {subPayId}: {ex.Message}");
                }
            });

            // ── 6. Create Audit Log ──────────────────────────────────
            _db.AuditLogs.Add(new AuditLog
            {
                ShopId = shop.ShopId,
                UserId = user.UserId,
                Action = "Create",
                EntityName = "Shop",
                EntityId = shop.ShopId,
                Details = $"Self-service registration: Shop '{shop.ShopName}' created with {plan.PlanName} ({model.BillingCycle}) subscription.",
                CreatedAt = DateTime.UtcNow
            });
            await _db.SaveChangesAsync();

            await transaction.CommitAsync();

            return new RegistrationResult
            {
                Success = true,
                ShopId = shop.ShopId,
                UserId = user.UserId,
                UserName = user.UserName,
                FullName = user.FullName,
                FirstName = user.FirstName,
                LastName = user.LastName,
                Initials = user.Initials
            };
        }
        catch (Exception ex)
        {
            await transaction.RollbackAsync();
            _logger.LogError(ex, "Failed to create account during registration");
            return RegistrationResult.Fail("An error occurred during registration. Please try again.");
        }
    }

    // ═══════════════════════════════════════════════════════════════════
    //  GENERATE UNIQUE SHOP CODE
    // ═══════════════════════════════════════════════════════════════════
    public async Task<string> GenerateShopCodeAsync(string shopName)
    {
        // Generate code from first letters of words, uppercased
        var words = shopName.Trim().Split(' ', StringSplitOptions.RemoveEmptyEntries);
        var baseCode = words.Length switch
        {
            0 => "SHOP",
            1 => words[0].Length >= 4 ? words[0][..4].ToUpper() : words[0].ToUpper(),
            2 => $"{words[0][..Math.Min(2, words[0].Length)]}{words[1][..Math.Min(2, words[1].Length)]}".ToUpper(),
            _ => string.Concat(words.Take(4).Select(w => w[0])).ToUpper()
        };

        // Ensure it doesn't start with a number
        if (char.IsDigit(baseCode[0]))
            baseCode = "S" + baseCode;

        // Check uniqueness, append number if needed
        var code = baseCode;
        var counter = 1;
        while (await _db.Shops.AnyAsync(s => s.ShopCode == code))
        {
            code = $"{baseCode}{counter:D2}";
            counter++;
        }

        return code;
    }

    private static string GenerateTemporaryPassword()
    {
        const string upper = "ABCDEFGHJKLMNPQRSTUVWXYZ";
        const string lower = "abcdefghijkmnopqrstuvwxyz";
        const string digits = "23456789";
        const string special = "!@#$%^&*()_+-=[]{}";
        const string all = upper + lower + digits + special;

        var chars = new List<char>
        {
            upper[RandomNumberGenerator.GetInt32(upper.Length)],
            lower[RandomNumberGenerator.GetInt32(lower.Length)],
            digits[RandomNumberGenerator.GetInt32(digits.Length)],
            special[RandomNumberGenerator.GetInt32(special.Length)]
        };

        for (var i = chars.Count; i < 14; i++)
        {
            chars.Add(all[RandomNumberGenerator.GetInt32(all.Length)]);
        }

        for (var i = chars.Count - 1; i > 0; i--)
        {
            var j = RandomNumberGenerator.GetInt32(i + 1);
            (chars[i], chars[j]) = (chars[j], chars[i]);
        }

        return new string(chars.ToArray());
    }
}
