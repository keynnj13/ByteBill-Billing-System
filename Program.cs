using ByteBill_BS.Data;
using ByteBill_BS.Services;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.DataProtection;
using Microsoft.AspNetCore.HttpOverrides;
using Microsoft.AspNetCore.RateLimiting;
using Microsoft.EntityFrameworkCore;
using System.Security.Claims;
using System.Globalization;
using System.Security.Cryptography;
using System.Threading.RateLimiting;
using System.IO;
using System.Linq;

// ── Set Philippine Peso as default currency culture ──────────────────
var phpCulture = new CultureInfo("en-PH");
CultureInfo.DefaultThreadCurrentCulture = phpCulture;
CultureInfo.DefaultThreadCurrentUICulture = phpCulture;

QuestPDF.Settings.License = QuestPDF.Infrastructure.LicenseType.Community;

var builder = WebApplication.CreateBuilder(args);

if (builder.Environment.IsProduction())
{
    var emailKey = builder.Configuration["Security:EmailEncryptionKey"];
    if (string.IsNullOrWhiteSpace(emailKey) || emailKey == "ByteBill-Default-Email-Key-Please-Override")
    {
        throw new InvalidOperationException("Security:EmailEncryptionKey must be set in production.");
    }
}

var secureCookiePolicy = builder.Environment.IsDevelopment()
    ? CookieSecurePolicy.SameAsRequest
    : CookieSecurePolicy.Always;

var dataProtectionPath = Path.Combine(builder.Environment.ContentRootPath, "App_Data", "DataProtectionKeys");
Directory.CreateDirectory(dataProtectionPath);
builder.Services.AddDataProtection()
    .PersistKeysToFileSystem(new DirectoryInfo(dataProtectionPath))
    .SetApplicationName("ByteBill_BS");

builder.Services.AddSingleton<IEmailSecurityService, EmailSecurityService>();

// Add services to the container.
builder.Services.AddControllersWithViews(options =>
{
    options.Filters.Add<ByteBill_BS.Filters.RequestedWithActionFilter>();
    options.Filters.Add<ByteBill_BS.Filters.DbExceptionFilter>();
});

// Configure SQL Server database
builder.Services.AddDbContext<ApplicationDbContext>(options =>
    options.UseSqlServer(builder.Configuration.GetConnectionString("DefaultConnection"),
        sql => sql.CommandTimeout(120)));

// Configure authentication
builder.Services.AddDistributedMemoryCache();
builder.Services.AddSession(options =>
{
    options.IdleTimeout = TimeSpan.FromHours(1);
    options.Cookie.HttpOnly = true;
    options.Cookie.IsEssential = true;
    options.Cookie.SameSite = SameSiteMode.Lax;
    options.Cookie.SecurePolicy = secureCookiePolicy;
});
builder.Services.AddAuthentication(CookieAuthenticationDefaults.AuthenticationScheme)
    .AddCookie(options =>
    {
        options.LoginPath = "/Auth/Login";
        options.LogoutPath = "/Auth/Logout";
        options.AccessDeniedPath = "/Auth/AccessDenied";
        options.ExpireTimeSpan = TimeSpan.FromHours(8);
        options.SlidingExpiration = true;
        options.Cookie.SecurePolicy = secureCookiePolicy;
        options.Cookie.SameSite = SameSiteMode.Lax;
        options.Events.OnValidatePrincipal = async ctx =>
        {
            var now = DateTime.UtcNow;
            if (await ShouldRejectPrincipalAsync(ctx, now))
            {
                await RejectPrincipalAsync(ctx);
                return;
            }

            RefreshLastActivityClaim(ctx, now);
        };
        // Return 401 for API requests instead of redirecting
        options.Events.OnRedirectToLogin = ctx =>
        {
            if (ctx.Request.Path.StartsWithSegments("/api"))
            {
                ctx.Response.StatusCode = StatusCodes.Status401Unauthorized;
                return Task.CompletedTask;
            }
            ctx.Response.Redirect(ctx.RedirectUri);
            return Task.CompletedTask;
        };
        options.Events.OnRedirectToAccessDenied = ctx =>
        {
            if (ctx.Request.Path.StartsWithSegments("/api"))
            {
                ctx.Response.StatusCode = StatusCodes.Status403Forbidden;
                return Task.CompletedTask;
            }
            ctx.Response.Redirect(ctx.RedirectUri);
            return Task.CompletedTask;
        };
    });

// ── Authorization policies (role-based) ──────────────────────────────
builder.Services.AddAuthorization(options =>
{
    options.AddPolicy("AdminOrAbove", policy =>
        policy.RequireClaim("Role", "SuperAdmin", "Admin"));

    options.AddPolicy("BillingOrAbove", policy =>
        policy.RequireClaim("Role", "SuperAdmin", "Admin", "Billing"));

    options.AddPolicy("TechnicianOrAbove", policy =>
        policy.RequireClaim("Role", "SuperAdmin", "Admin", "Billing", "Technician"));

    options.AddPolicy("AnyAuthenticated", policy =>
        policy.RequireClaim("Role", "SuperAdmin", "Admin", "Billing", "Technician", "Auditor"));
});

// ── Register application services ────────────────────────────────────
builder.Services.AddHttpContextAccessor();
builder.Services.AddScoped<INavigationService, NavigationService>();
builder.Services.AddScoped<IAuditService, AuditService>();
builder.Services.AddScoped<ICustomerService, CustomerService>();
builder.Services.AddScoped<IJobOrderService, JobOrderService>();
builder.Services.AddScoped<IInvoiceService, InvoiceService>();
builder.Services.AddScoped<IPaymentService, PaymentService>();
builder.Services.AddScoped<IServiceCatalogService, ServiceCatalogService>();
builder.Services.AddScoped<IInventoryService, InventoryService>();
builder.Services.AddScoped<IAdjustmentService, AdjustmentService>();
builder.Services.AddScoped<INotificationService, NotificationService>();
builder.Services.AddScoped<IBillingCalculationService, BillingCalculationService>();
builder.Services.AddScoped<ITaxCalculationService, TaxCalculationService>();
builder.Services.AddScoped<ISuperAdminService, SuperAdminService>();
builder.Services.AddScoped<IPasswordResetService, PasswordResetService>();
builder.Services.AddSingleton<IPasswordBlacklistValidator, PasswordBlacklistValidator>();

// ── Email notifications (SendGrid) ───────────────────────────────────
builder.Services.Configure<SendGridSettings>(builder.Configuration.GetSection("SendGrid"));
builder.Services.Configure<SmtpSettings>(builder.Configuration.GetSection("Smtp"));
builder.Services.Configure<PasswordResetSettings>(builder.Configuration.GetSection("PasswordReset"));
builder.Services.Configure<RecaptchaSettings>(builder.Configuration.GetSection("Recaptcha"));
builder.Services.Configure<SecuritySettings>(builder.Configuration.GetSection("Security"));
builder.Services.AddScoped<IEmailService, EmailService>();
builder.Services.AddScoped<IRecaptchaService, RecaptchaService>();
builder.Services.AddScoped<IMfaService, MfaService>();

// ── Xero Accounting integration ──────────────────────────────────────
builder.Services.Configure<XeroSettings>(builder.Configuration.GetSection("Xero"));
builder.Services.AddHttpClient<IXeroService, XeroService>(client =>
{
    client.Timeout = TimeSpan.FromSeconds(30);
});

// ── PayMongo integration ─────────────────────────────────────────────
builder.Services.Configure<PayMongoSettings>(builder.Configuration.GetSection("PayMongo"));
builder.Services.AddHttpClient<IPayMongoService, PayMongoService>(client =>
{
    client.Timeout = TimeSpan.FromSeconds(30);
});

// ── SignalR for real-time notifications ───────────────────────────────
builder.Services.AddSignalR();

// ── Self-service registration ────────────────────────────────────────
builder.Services.AddHttpClient<IRegistrationService, RegistrationService>(client =>
{
    client.Timeout = TimeSpan.FromSeconds(30);
});

// Rate limiting for login endpoint
builder.Services.AddRateLimiter(options =>
{
    options.RejectionStatusCode = StatusCodes.Status429TooManyRequests;
    options.AddPolicy("LoginPolicy", context =>
        RateLimitPartition.GetFixedWindowLimiter(
            partitionKey: context.Connection.RemoteIpAddress?.ToString() ?? "unknown",
            factory: _ => new FixedWindowRateLimiterOptions
            {
                PermitLimit = 10,
                Window = TimeSpan.FromMinutes(1),
                QueueProcessingOrder = QueueProcessingOrder.OldestFirst,
                QueueLimit = 0
            }));

    options.AddPolicy("ForgotPasswordPolicy", context =>
        RateLimitPartition.GetFixedWindowLimiter(
            partitionKey: context.Connection.RemoteIpAddress?.ToString() ?? "unknown",
            factory: _ => new FixedWindowRateLimiterOptions
            {
                PermitLimit = 5,
                Window = TimeSpan.FromMinutes(1),
                QueueProcessingOrder = QueueProcessingOrder.OldestFirst,
                QueueLimit = 0
            }));
});

var app = builder.Build();

var sqlLocalDbPath = FindSqlLocalDbPath();

// Pre-start LocalDB instance (avoids cold-start timeout during seeding)
try
{
    if (!string.IsNullOrWhiteSpace(sqlLocalDbPath))
    {
        using var startProcess = System.Diagnostics.Process.Start(sqlLocalDbPath, "start MSSQLLocalDB");
        startProcess?.WaitForExit(30_000);
    }
}
catch { /* sqllocaldb not found or not installed — will connect normally */ }

static string? FindSqlLocalDbPath()
{
    const string SqlServerFolder = "Microsoft SQL Server";
    const string ToolsFolder = "Tools";
    const string BinnFolder = "Binn";
    const string LocalDbExe = "sqllocaldb.exe";
    var versions = new[] { "160", "150", "140" };
    var programFiles = Environment.GetFolderPath(Environment.SpecialFolder.ProgramFiles);
    var programFilesX86 = Environment.GetFolderPath(Environment.SpecialFolder.ProgramFilesX86);
    var roots = new[] { programFiles, programFilesX86 };

    var candidates =
        from root in roots
        from version in versions
        select Path.Combine(root, SqlServerFolder, version, ToolsFolder, BinnFolder, LocalDbExe);

    return candidates.FirstOrDefault(path => !string.IsNullOrWhiteSpace(path) && File.Exists(path));
}

static async Task<bool> ShouldRejectPrincipalAsync(CookieValidatePrincipalContext ctx, DateTime now)
{
    if (!TryGetAuthClaims(ctx.Principal, out var userId, out var authVersion, out var mustChangePasswordClaim))
    {
        return true;
    }

    var db = ctx.HttpContext.RequestServices.GetRequiredService<ApplicationDbContext>();
    if (await IsUserInvalidAsync(db, userId, authVersion, mustChangePasswordClaim, now))
    {
        return true;
    }

    var timeoutMinutes = await GetSessionTimeoutMinutesAsync(db);
    return IsSessionTimedOut(ctx.Principal, timeoutMinutes, now);
}

static bool TryGetAuthClaims(ClaimsPrincipal? principal, out long userId, out int authVersion, out bool mustChangePasswordClaim)
{
    userId = 0;
    authVersion = 0;
    mustChangePasswordClaim = principal?.FindFirstValue("MustChangePassword") == "1";

    var userIdClaim = principal?.FindFirstValue("UserId");
    var authVersionClaim = principal?.FindFirstValue("AuthVersion");
    return long.TryParse(userIdClaim, out userId) && int.TryParse(authVersionClaim, out authVersion);
}

static async Task<bool> IsUserInvalidAsync(ApplicationDbContext db, long userId, int authVersion, bool mustChangePasswordClaim, DateTime now)
{
    var dbUser = await db.Users
        .AsNoTracking()
        .Where(u => u.UserId == userId)
        .Select(u => new { u.IsActive, u.AuthVersion, u.IsPermanentlyLocked, u.LockoutEndAt, u.MustChangePassword })
        .FirstOrDefaultAsync();

    return dbUser is null
        || !dbUser.IsActive
        || dbUser.AuthVersion != authVersion
        || dbUser.MustChangePassword != mustChangePasswordClaim
        || dbUser.IsPermanentlyLocked
        || (dbUser.LockoutEndAt.HasValue && dbUser.LockoutEndAt.Value > now);
}

static async Task<int> GetSessionTimeoutMinutesAsync(ApplicationDbContext db)
{
    var timeoutSetting = await db.PlatformSettings
        .AsNoTracking()
        .Where(s => s.SettingKey == "Security.SessionTimeout")
        .Select(s => s.SettingValue)
        .FirstOrDefaultAsync();

    return int.TryParse(timeoutSetting, out var parsedTimeout)
        ? Math.Clamp(parsedTimeout, 5, 1440)
        : 5;
}

static bool IsSessionTimedOut(ClaimsPrincipal? principal, int timeoutMinutes, DateTime now)
{
    if (!TryGetLastActivityUtc(principal, out var lastActivityUtc))
    {
        return false;
    }

    return now - lastActivityUtc > TimeSpan.FromMinutes(timeoutMinutes);
}

static bool TryGetLastActivityUtc(ClaimsPrincipal? principal, out DateTime lastActivityUtc)
{
    lastActivityUtc = default;
    var lastActivityClaim = principal?.FindFirstValue("LastActivityUtc");
    if (string.IsNullOrWhiteSpace(lastActivityClaim))
    {
        return false;
    }

    return DateTime.TryParseExact(
        lastActivityClaim,
        "O",
        CultureInfo.InvariantCulture,
        DateTimeStyles.RoundtripKind,
        out lastActivityUtc);
}

static void RefreshLastActivityClaim(CookieValidatePrincipalContext ctx, DateTime now)
{
    if (ctx.Principal?.Identity is not ClaimsIdentity identity)
    {
        return;
    }

    var existingLastActivityClaim = identity.FindFirst("LastActivityUtc");
    if (existingLastActivityClaim is not null)
    {
        identity.RemoveClaim(existingLastActivityClaim);
    }

    identity.AddClaim(new Claim("LastActivityUtc", now.ToString("O", CultureInfo.InvariantCulture)));
    ctx.ReplacePrincipal(new ClaimsPrincipal(identity));
    ctx.ShouldRenew = true;
}

static async Task RejectPrincipalAsync(CookieValidatePrincipalContext ctx)
{
    ctx.RejectPrincipal();
    await ctx.HttpContext.SignOutAsync(CookieAuthenticationDefaults.AuthenticationScheme);
}

static bool IsStaticAssetPath(string path)
{
    return path.StartsWith("/css/", StringComparison.OrdinalIgnoreCase)
        || path.StartsWith("/js/", StringComparison.OrdinalIgnoreCase)
        || path.StartsWith("/lib/", StringComparison.OrdinalIgnoreCase)
        || path.StartsWith("/img/", StringComparison.OrdinalIgnoreCase)
        || path.Equals("/favicon.ico", StringComparison.OrdinalIgnoreCase);
}

static bool ShouldRedirectToForcePasswordChange(ClaimsPrincipal user, string path, bool isStaticAsset)
{
    if (user.FindFirstValue("MustChangePassword") != "1")
    {
        return false;
    }

    return !IsAllowedForForcePasswordChange(path, isStaticAsset);
}

static bool IsAllowedForForcePasswordChange(string path, bool isStaticAsset)
{
    return isStaticAsset
        || path.StartsWith("/Auth/ForcePasswordChange", StringComparison.OrdinalIgnoreCase)
        || path.StartsWith("/Auth/Logout", StringComparison.OrdinalIgnoreCase)
        || path.StartsWith("/Auth/Login", StringComparison.OrdinalIgnoreCase)
        || path.StartsWith("/Auth/VerifyMfa", StringComparison.OrdinalIgnoreCase)
        || path.StartsWith("/Auth/ResendMfaEmailCode", StringComparison.OrdinalIgnoreCase)
        || path.StartsWith("/Auth/AccessDenied", StringComparison.OrdinalIgnoreCase)
        || path.StartsWith("/Auth/ResetPassword", StringComparison.OrdinalIgnoreCase)
        || path.StartsWith("/Auth/ForgotPassword", StringComparison.OrdinalIgnoreCase);
}

static bool ShouldRedirectToMfaSetup(ClaimsPrincipal user, string path, bool isStaticAsset)
{
    var role = user.FindFirstValue("Role") ?? string.Empty;
    var mustChangePassword = user.FindFirstValue("MustChangePassword") == "1";
    if (!role.Equals("Admin", StringComparison.OrdinalIgnoreCase) || mustChangePassword)
    {
        return false;
    }

    var hasMfaClaims = user.HasClaim(c => c.Type == "IsMfaEnabled")
        && user.HasClaim(c => c.Type == "MfaOnboarded");
    if (!hasMfaClaims)
    {
        return false;
    }

    var isMfaEnabled = user.FindFirstValue("IsMfaEnabled") == "1";
    var mfaOnboarded = user.FindFirstValue("MfaOnboarded") == "1";
    if (isMfaEnabled || mfaOnboarded)
    {
        return false;
    }

    return !IsAllowedForMfaSetup(path, isStaticAsset);
}

static bool IsAllowedForMfaSetup(string path, bool isStaticAsset)
{
    return isStaticAsset
        || path.StartsWith("/Auth/SetupMfa", StringComparison.OrdinalIgnoreCase)
        || path.StartsWith("/Auth/Logout", StringComparison.OrdinalIgnoreCase)
        || path.StartsWith("/Auth/Login", StringComparison.OrdinalIgnoreCase)
        || path.StartsWith("/Auth/AccessDenied", StringComparison.OrdinalIgnoreCase);
}

// Seed database on first run (wrapped in try-catch to prevent 500.30 on hosting)
try
{
    using var scope = app.Services.CreateScope();
    var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
    await DbSeeder.SeedAsync(db, app.Environment.IsDevelopment());
}
catch (Exception ex)
{
    var logger = app.Services.GetRequiredService<ILoggerFactory>().CreateLogger("Startup");
    logger.LogError(ex, "DbSeeder failed during startup — app will continue without seeding.");
}

// Configure the HTTP request pipeline.
if (app.Environment.IsDevelopment())
{
    app.UseDeveloperExceptionPage();
}
else
{
    app.UseExceptionHandler("/Home/Error");
    app.UseHsts();
}

app.UseForwardedHeaders(new ForwardedHeadersOptions
{
    ForwardedHeaders = ForwardedHeaders.XForwardedFor | ForwardedHeaders.XForwardedProto
});

app.UseHttpsRedirection();

app.Use(async (context, next) =>
{
    var cspNonce = Convert.ToBase64String(RandomNumberGenerator.GetBytes(16));
    context.Items["CspNonce"] = cspNonce;

    context.Response.Headers.XContentTypeOptions = "nosniff";
    context.Response.Headers.XFrameOptions = "SAMEORIGIN";
    context.Response.Headers["Referrer-Policy"] = "strict-origin-when-cross-origin";
    context.Response.Headers["Permissions-Policy"] = "geolocation=(), microphone=(), camera=()";

    context.Response.Headers.ContentSecurityPolicy =
        "default-src 'self'; " +
        $"script-src 'self' 'nonce-{cspNonce}' https://cdnjs.cloudflare.com; " +
        "script-src-attr 'unsafe-inline'; " +
        "style-src 'self' 'unsafe-inline'; " +
        "font-src 'self' data:; " +
        "img-src 'self' data:; " +
        "connect-src 'self'; " +
        "object-src 'none'; " +
        "frame-ancestors 'self'; " +
        "base-uri 'self'; " +
        "form-action 'self';";

    await next();
});

app.UseRouting();

app.UseAuthentication();

app.Use(async (context, next) =>
{
    if (context.User.Identity?.IsAuthenticated == true)
    {
        var path = context.Request.Path.Value ?? string.Empty;
        var isStaticAsset = IsStaticAssetPath(path);

        if (ShouldRedirectToForcePasswordChange(context.User, path, isStaticAsset))
        {
            context.Response.Redirect("/Auth/ForcePasswordChange");
            return;
        }

        if (ShouldRedirectToMfaSetup(context.User, path, isStaticAsset))
        {
            context.Response.Redirect("/Auth/SetupMfa");
            return;
        }
    }

    await next();
});

app.UseSession();
app.UseAuthorization();
app.UseRateLimiter();

app.MapStaticAssets();

// Area routing for role-based controllers
app.MapControllerRoute(
    name: "areas",
    pattern: "{area:exists}/{controller=Dashboard}/{action=Index}/{id?}")
    .WithStaticAssets();

app.MapControllerRoute(
    name: "default",
    pattern: "{controller=Landing}/{action=Index}/{id?}")
    .WithStaticAssets();

// ── SignalR hub endpoint ─────────────────────────────────────────────
app.MapHub<ByteBill_BS.Hubs.NotificationHub>("/hubs/notifications");

await app.RunAsync();
    