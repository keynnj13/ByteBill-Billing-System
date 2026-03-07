using ByteBill_BS.Data;
using ByteBill_BS.Services;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.RateLimiting;
using Microsoft.EntityFrameworkCore;
using System.Globalization;
using System.Threading.RateLimiting;

// ── Set Philippine Peso as default currency culture ──────────────────
var phpCulture = new CultureInfo("en-PH");
CultureInfo.DefaultThreadCurrentCulture = phpCulture;
CultureInfo.DefaultThreadCurrentUICulture = phpCulture;

QuestPDF.Settings.License = QuestPDF.Infrastructure.LicenseType.Community;

var builder = WebApplication.CreateBuilder(args);

// Add services to the container.
builder.Services.AddControllersWithViews();

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
    options.Cookie.SecurePolicy = builder.Environment.IsDevelopment() 
        ? CookieSecurePolicy.SameAsRequest 
        : CookieSecurePolicy.Always;
});
builder.Services.AddAuthentication(CookieAuthenticationDefaults.AuthenticationScheme)
    .AddCookie(options =>
    {
        options.LoginPath = "/Auth/Login";
        options.LogoutPath = "/Auth/Logout";
        options.AccessDeniedPath = "/Auth/AccessDenied";
        options.ExpireTimeSpan = TimeSpan.FromHours(8);
        options.SlidingExpiration = true;
        options.Cookie.SecurePolicy = builder.Environment.IsDevelopment() 
            ? CookieSecurePolicy.SameAsRequest 
            : CookieSecurePolicy.Always;
        options.Cookie.SameSite = SameSiteMode.Lax;
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

// ── Email notifications (SendGrid) ───────────────────────────────────
builder.Services.Configure<SendGridSettings>(builder.Configuration.GetSection("SendGrid"));
builder.Services.AddScoped<IEmailService, EmailService>();

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

// ── Self-service registration ────────────────────────────────────────
builder.Services.AddHttpClient<IRegistrationService, RegistrationService>(client =>
{
    client.Timeout = TimeSpan.FromSeconds(30);
});

// Rate limiting for login endpoint
builder.Services.AddRateLimiter(options =>
{
    options.RejectionStatusCode = StatusCodes.Status429TooManyRequests;
    options.AddFixedWindowLimiter("LoginPolicy", opt =>
    {
        opt.PermitLimit = 10;
        opt.Window = TimeSpan.FromMinutes(1);
        opt.QueueProcessingOrder = QueueProcessingOrder.OldestFirst;
        opt.QueueLimit = 0;
    });
});

var app = builder.Build();

// Pre-start LocalDB instance (avoids cold-start timeout during seeding)
try
{
    using var startProcess = System.Diagnostics.Process.Start("sqllocaldb", "start MSSQLLocalDB");
    startProcess?.WaitForExit(30_000);
}
catch { /* sqllocaldb not on PATH or not installed — will connect normally */ }

// Seed database on first run (wrapped in try-catch to prevent 500.30 on hosting)
try
{
    using var scope = app.Services.CreateScope();
    var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
    await DbSeeder.SeedAsync(db);
}
catch (Exception ex)
{
    var logger = app.Services.GetRequiredService<ILoggerFactory>().CreateLogger("Startup");
    logger.LogError(ex, "DbSeeder failed during startup — app will continue without seeding.");
}

// Configure the HTTP request pipeline.
// Temporarily show detailed errors on all environments for debugging
app.UseDeveloperExceptionPage();
if (!app.Environment.IsDevelopment())
{
    // app.UseExceptionHandler("/Home/Error");  // Re-enable after debugging
    app.UseHsts();
}

app.UseHttpsRedirection();
app.UseRouting();

app.UseAuthentication();
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

await app.RunAsync();
    