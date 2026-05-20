using ByteBill_BS.Models;
using ByteBill_BS.Models.Enums;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.Extensions.Logging;
using System.IO;

namespace ByteBill_BS.Data;

/// <summary>
/// Seeds the database with default roles, demo users, and sample data on first run.
/// Called from Program.cs during application startup.
/// </summary>
public static class DbSeeder
{
    private static readonly string[] ProtectedSuperAdminUserNames = ["vkpadao", "vkbackup"];
    private static readonly string[] LegacySuperAdminUserNames = ["rootadmin", "root admin", "guardadmin", "guard admin", "backupsuperadmin", "backup-superadmin", "backup superadmin"];

    public static async Task SeedAsync(ApplicationDbContext db, bool isDevelopment)
    {
        // Ensure the database exists (no-op if already created via SQL script)
        // Use a generous timeout for cold-start scenarios (LocalDB spin-up)
        db.Database.SetCommandTimeout(TimeSpan.FromSeconds(300));

        // Warm up the LocalDB connection before any schema work
        try { await db.Database.CanConnectAsync(); }
        catch { /* will fail naturally below if truly unreachable */ }

        await db.Database.EnsureCreatedAsync();
        await ApplyAuthAndPasswordResetSchemaFixesAsync(db);
        await ApplyPiiSchemaFixesAsync(db);
        await BackfillProtectedPiiAsync(db);

        await SeedShopAsync(db);
        await SeedRolesAsync(db);
        await PurgeLegacySuperAdminAccountsAsync(db);
        await EnsureSystemSuperAdminsAsync(db);
        if (isDevelopment)
        {
            await SeedUsersAsync(db);
            await SeedUserRolesAsync(db);
        }

        // Sample data for all modules
        await SeedCustomersAsync(db);
        await SeedDevicesAsync(db);
        await SeedServiceCategoriesAsync(db);
        await SeedServiceCatalogAsync(db);
        await SeedInventoryCategoriesAsync(db);
        await SeedInventoryItemsAsync(db);
        await SeedJobOrdersAsync(db);
        await SeedInvoicesAsync(db);
        await SeedPaymentsAsync(db);
        await SeedAdjustmentTypeConfigsAsync(db);

        // ── Transaction records that must survive DB re-creation ──────
        await SeedAccountingEntriesAsync(db);
        await SeedInvoiceDiscountsAsync(db);
        await SeedAdjustmentsAsync(db);
        await SeedPayMongoTxnsAsync(db);
        await SeedNotificationsAsync(db);
        await SeedAuditLogsAsync(db);
        await SeedSubscriptionPlansAsync(db);
        await SeedMainShopSubscriptionAsync(db);

        // ── Repair any inconsistent invoice balances ──────────────────
        await RepairInvoiceBalancesAsync(db);
    }

    private static async Task ApplyAuthAndPasswordResetSchemaFixesAsync(ApplicationDbContext db)
    {
        var logger = db.GetService<ILoggerFactory>().CreateLogger("DbSeeder");

        // Use the migration SQL script as the single source of truth.
        var relativePath = Path.Combine("Database", "Migration_ForgotPasswordRecovery.sql");
        var candidatePaths = new[]
        {
            Path.Combine(AppContext.BaseDirectory, relativePath),
            Path.Combine(Directory.GetCurrentDirectory(), relativePath),
            Path.GetFullPath(Path.Combine(AppContext.BaseDirectory, "..", "..", "..", "..", relativePath))
        }
        .Distinct(StringComparer.OrdinalIgnoreCase)
        .ToArray();

        var scriptPath = candidatePaths.FirstOrDefault(File.Exists);
        if (scriptPath is null)
        {
            logger.LogWarning("Schema patch script not found. Checked: {Paths}", string.Join(" | ", candidatePaths));
            return;
        }

        var sql = await File.ReadAllTextAsync(scriptPath);
        if (string.IsNullOrWhiteSpace(sql))
        {
            logger.LogWarning("Schema patch script is empty: {ScriptPath}", scriptPath);
            return;
        }

        try
        {
            await db.Database.ExecuteSqlRawAsync(sql);
        }
        catch (Exception ex)
        {
            logger.LogWarning(ex, "Schema patch failed: {ScriptPath}", scriptPath);
        }
    }

    private static async Task ApplyPiiSchemaFixesAsync(ApplicationDbContext db)
    {
        var logger = db.GetService<ILoggerFactory>().CreateLogger("DbSeeder");

        var relativePath = Path.Combine("Database", "Migration_PhoneHash.sql");
        var candidatePaths = new[]
        {
            Path.Combine(AppContext.BaseDirectory, relativePath),
            Path.Combine(Directory.GetCurrentDirectory(), relativePath),
            Path.GetFullPath(Path.Combine(AppContext.BaseDirectory, "..", "..", "..", "..", relativePath))
        }
        .Distinct(StringComparer.OrdinalIgnoreCase)
        .ToArray();

        var scriptPath = candidatePaths.FirstOrDefault(File.Exists);
        if (scriptPath is null)
        {
            logger.LogWarning("Schema patch script not found. Checked: {Paths}", string.Join(" | ", candidatePaths));
            return;
        }

        var sql = await File.ReadAllTextAsync(scriptPath);
        if (string.IsNullOrWhiteSpace(sql))
        {
            logger.LogWarning("Schema patch script is empty: {ScriptPath}", scriptPath);
            return;
        }

        try
        {
            await db.Database.ExecuteSqlRawAsync(sql);
        }
        catch (Exception ex)
        {
            logger.LogWarning(ex, "Schema patch failed: {ScriptPath}", scriptPath);
        }
    }

    private static async Task BackfillProtectedPiiAsync(ApplicationDbContext db)
    {
        var emailSecurity = db.GetService<ByteBill_BS.Services.IEmailSecurityService>();

        var users = await db.Users.Where(u => u.Email != null || u.Phone != null).ToListAsync();
        BackfillPiiForEntities(users, emailSecurity, BackfillUserPii);

        var customers = await db.Customers.Where(c => c.Email != null || c.Phone != null).ToListAsync();
        BackfillPiiForEntities(customers, emailSecurity, BackfillCustomerPii);

        var shops = await db.Shops.Where(s => s.Email != null || s.Phone != null).ToListAsync();
        BackfillPiiForEntities(shops, emailSecurity, BackfillShopPii);

        await db.SaveChangesAsync();
    }

    private static void BackfillPiiForEntities<T>(IEnumerable<T> entities, ByteBill_BS.Services.IEmailSecurityService emailSecurity, Action<T, ByteBill_BS.Services.IEmailSecurityService> update)
    {
        foreach (var entity in entities)
        {
            update(entity, emailSecurity);
        }
    }

    private static void BackfillUserPii(User user, ByteBill_BS.Services.IEmailSecurityService emailSecurity)
    {
        var emailPlain = emailSecurity.Decrypt(user.Email);
        if (!emailSecurity.IsEncryptedV2(user.Email))
            user.Email = emailSecurity.Encrypt(emailPlain);
        user.EmailHash = emailSecurity.ComputeHash(emailPlain);

        var phonePlain = emailSecurity.Decrypt(user.Phone);
        if (!emailSecurity.IsEncryptedV2(user.Phone))
            user.Phone = emailSecurity.Encrypt(phonePlain);
        user.PhoneHash = emailSecurity.ComputePhoneHash(phonePlain);
    }

    private static void BackfillCustomerPii(Customer customer, ByteBill_BS.Services.IEmailSecurityService emailSecurity)
    {
        var emailPlain = emailSecurity.Decrypt(customer.Email);
        if (!emailSecurity.IsEncryptedV2(customer.Email))
            customer.Email = emailSecurity.Encrypt(emailPlain);
        customer.EmailHash = emailSecurity.ComputeHash(emailPlain);

        var phonePlain = emailSecurity.Decrypt(customer.Phone);
        if (!emailSecurity.IsEncryptedV2(customer.Phone))
            customer.Phone = emailSecurity.Encrypt(phonePlain);
        customer.PhoneHash = emailSecurity.ComputePhoneHash(phonePlain);
    }

    private static void BackfillShopPii(Shop shop, ByteBill_BS.Services.IEmailSecurityService emailSecurity)
    {
        var emailPlain = emailSecurity.Decrypt(shop.Email);
        if (!emailSecurity.IsEncryptedV2(shop.Email))
            shop.Email = emailSecurity.Encrypt(emailPlain);
        shop.EmailHash = emailSecurity.ComputeHash(emailPlain);

        var phonePlain = emailSecurity.Decrypt(shop.Phone);
        if (!emailSecurity.IsEncryptedV2(shop.Phone))
            shop.Phone = emailSecurity.Encrypt(phonePlain);
        shop.PhoneHash = emailSecurity.ComputePhoneHash(phonePlain);
    }

    // ── Shop ─────────────────────────────────────────────────────────────
    private static async Task SeedShopAsync(ApplicationDbContext db)
    {
        if (await db.Shops.AnyAsync()) return;

        db.Shops.Add(new Shop
        {
            ShopCode = "MAIN",
            ShopName = "ByteBill Main Shop",
            Email = "admin@bytebill.com",
            Phone = "+63 XXX XXX XXXX", // Philippine format: +63 XXX XXX XXXX (mobile) or +63 XX XXX XXXX (landline)
            Address = "J.P. Laurel Ave., Davao City, Philippines",
            Status = "Active",
            CreatedAt = DateTime.UtcNow
        });

        await db.SaveChangesAsync();
    }

    // ── Roles ────────────────────────────────────────────────────────────
    private static async Task SeedRolesAsync(ApplicationDbContext db)
    {
        var requiredRoles = new List<Role>
        {
            new() { RoleName = "SuperAdmin",  Description = "Full system access across all shops" },
            new() { RoleName = "Admin",        Description = "Shop Owner — full access within a single shop" },
            new() { RoleName = "Billing",      Description = "Billing staff — invoices, payments, and customer management" },
            new() { RoleName = "Technician",   Description = "Technician — job orders, diagnostics, and repairs" },
            new() { RoleName = "Auditor",      Description = "Auditor — read-only access for review and compliance" }
        };

        var existingRoleNames = await db.Roles.Select(r => r.RoleName).ToListAsync();
        var existingSet = existingRoleNames.ToHashSet(StringComparer.OrdinalIgnoreCase);

        var missingRoles = requiredRoles
            .Where(r => !existingSet.Contains(r.RoleName))
            .ToList();

        if (missingRoles.Count == 0)
        {
            return;
        }

        db.Roles.AddRange(missingRoles);
        await db.SaveChangesAsync();
    }

    // ── Seed Users ───────────────────────────────────────────────────────
    // Each user has a unique password hashed with BCrypt (cost 12).
    // ┌──────────────┬──────────────┬────────────────────┐
    // │ Username      │ Role         │ Password           │
    // ├──────────────┼──────────────┼────────────────────┤
    // │ vkpadao       │ SuperAdmin   │ Superadmin123!     │
    // │ admin         │ Admin        │ Admin123!          │
    // │ billing       │ Billing      │ Billing123!        │
    // │ technician    │ Technician   │ Technician123!     │
    // │ auditor       │ Auditor      │ Auditor123!        │
    // └──────────────┴──────────────┴────────────────────┘
    private static async Task SeedUsersAsync(ApplicationDbContext db)
    {
        var shop = await db.Shops.FirstAsync();

        var seedUsers = new (string FirstName, string LastName, string UserName, string Password)[]
        {
            ("Vaness", "Padao",   "vkpadao",    "Superadmin123!"),
            ("Maria",      "Santos",  "admin",      "Admin123!"),
            ("Juan",       "Cruz",    "billing",    "Billing123!"),
            ("Carlos",     "Reyes",   "technician", "Technician123!"),
            ("Ana",        "Garcia",  "auditor",    "Auditor123!")
        };

        foreach (var s in seedUsers)
        {
            var exists = await db.Users.AnyAsync(u => u.UserName == s.UserName);
            if (exists)
            {
                continue;
            }

            db.Users.Add(new User
            {
                ShopId       = shop.ShopId,
                FirstName    = s.FirstName,
                LastName     = s.LastName,
                UserName     = s.UserName,
                PasswordHash = BCrypt.Net.BCrypt.HashPassword(s.Password, workFactor: 12),
                IsActive     = true,
                CreatedAt    = DateTime.UtcNow
            });
        }

        await db.SaveChangesAsync();
    }

    private static async Task PurgeLegacySuperAdminAccountsAsync(ApplicationDbContext db)
    {
        var legacyUserNameSet = LegacySuperAdminUserNames
            .Select(NormalizeLegacyKey)
            .ToHashSet(StringComparer.OrdinalIgnoreCase);

        var legacyUsers = await db.Users
            .Include(u => u.UserRoles)
            .ThenInclude(ur => ur.Role)
            .Where(u => legacyUserNameSet.Contains((u.UserName ?? string.Empty).ToLower().Replace("-", string.Empty).Replace("_", string.Empty).Replace(" ", string.Empty))
                || u.UserRoles.Any(ur => ur.Role != null && (ur.Role.RoleName ?? string.Empty).ToLower().Replace("-", string.Empty).Replace("_", string.Empty).Replace(" ", string.Empty) == "backupsuperadmin"))
            .ToListAsync();

        if (legacyUsers.Count == 0)
        {
            var obsoleteRoles = await db.Roles
                .Where(r => (r.RoleName ?? string.Empty).ToLower().Replace("-", string.Empty).Replace("_", string.Empty).Replace(" ", string.Empty) == "backupsuperadmin")
                .ToListAsync();
            foreach (var obsoleteRole in obsoleteRoles)
            {
                var hasAssignments = await db.UserRoles.AnyAsync(ur => ur.RoleId == obsoleteRole.RoleId);
                if (!hasAssignments)
                {
                    db.Roles.Remove(obsoleteRole);
                }
            }

            await db.SaveChangesAsync();

            return;
        }

        var mainSuperAdmin = await db.Users.FirstOrDefaultAsync(u => u.UserName == "vkpadao");
        var fallbackSuperAdminId = mainSuperAdmin?.UserId;

        var legacyUserIds = legacyUsers.Select(u => u.UserId).ToList();

        // Repoint required FK references to vkpadao when available.
        if (fallbackSuperAdminId.HasValue)
        {
            var announcements = await db.Announcements
                .Where(a => legacyUserIds.Contains(a.CreatedByUserId))
                .ToListAsync();
            foreach (var row in announcements)
            {
                row.CreatedByUserId = fallbackSuperAdminId.Value;
            }

            var jobOrders = await db.JobOrders
                .Where(j => legacyUserIds.Contains(j.CreatedByUserId) || (j.AssignedTechUserId.HasValue && legacyUserIds.Contains(j.AssignedTechUserId.Value)))
                .ToListAsync();
            foreach (var row in jobOrders)
            {
                if (legacyUserIds.Contains(row.CreatedByUserId))
                {
                    row.CreatedByUserId = fallbackSuperAdminId.Value;
                }

                if (row.AssignedTechUserId.HasValue && legacyUserIds.Contains(row.AssignedTechUserId.Value))
                {
                    row.AssignedTechUserId = fallbackSuperAdminId.Value;
                }
            }

            var payments = await db.Payments
                .Where(p => legacyUserIds.Contains(p.ReceivedByUserId))
                .ToListAsync();
            foreach (var row in payments)
            {
                row.ReceivedByUserId = fallbackSuperAdminId.Value;
            }

            var adjustments = await db.CreditDebitAdjustments
                .Where(a => legacyUserIds.Contains(a.CreatedByUserId) || (a.ReviewedByUserId.HasValue && legacyUserIds.Contains(a.ReviewedByUserId.Value)))
                .ToListAsync();
            foreach (var row in adjustments)
            {
                if (legacyUserIds.Contains(row.CreatedByUserId))
                {
                    row.CreatedByUserId = fallbackSuperAdminId.Value;
                }

                if (row.ReviewedByUserId.HasValue && legacyUserIds.Contains(row.ReviewedByUserId.Value))
                {
                    row.ReviewedByUserId = fallbackSuperAdminId.Value;
                }
            }

            var payMongo = await db.PayMongoTxns
                .Where(t => legacyUserIds.Contains(t.InitiatedByUserId))
                .ToListAsync();
            foreach (var row in payMongo)
            {
                row.InitiatedByUserId = fallbackSuperAdminId.Value;
            }
        }

        var auditLogs = await db.AuditLogs
            .Where(a => a.UserId.HasValue && legacyUserIds.Contains(a.UserId.Value))
            .ToListAsync();
        foreach (var row in auditLogs)
        {
            row.UserId = null;
        }

        var superAdminAuditLogs = await db.SuperAdminAuditLogs
            .Where(a => legacyUserIds.Contains(a.UserId))
            .ToListAsync();
        if (superAdminAuditLogs.Count > 0)
        {
            db.SuperAdminAuditLogs.RemoveRange(superAdminAuditLogs);
        }

        var notifications = await db.Notifications
            .Where(n => legacyUserIds.Contains(n.UserId))
            .ToListAsync();
        if (notifications.Count > 0)
        {
            db.Notifications.RemoveRange(notifications);
        }

        var resetTokens = await db.PasswordResetTokens
            .Where(t => legacyUserIds.Contains(t.UserId))
            .ToListAsync();
        if (resetTokens.Count > 0)
        {
            db.PasswordResetTokens.RemoveRange(resetTokens);
        }

        var userRoles = await db.UserRoles
            .Where(ur => legacyUserIds.Contains(ur.UserId))
            .ToListAsync();
        if (userRoles.Count > 0)
        {
            db.UserRoles.RemoveRange(userRoles);
        }

        db.Users.RemoveRange(legacyUsers);
        await db.SaveChangesAsync();

        var obsoleteBackupRoles = await db.Roles
            .Where(r => (r.RoleName ?? string.Empty).ToLower().Replace("-", string.Empty).Replace("_", string.Empty).Replace(" ", string.Empty) == "backupsuperadmin")
            .ToListAsync();
        foreach (var obsoleteBackupRole in obsoleteBackupRoles)
        {
            var hasAssignments = await db.UserRoles.AnyAsync(ur => ur.RoleId == obsoleteBackupRole.RoleId);
            if (!hasAssignments)
            {
                db.Roles.Remove(obsoleteBackupRole);
            }
        }

        await db.SaveChangesAsync();
    }

    private static string NormalizeLegacyKey(string? value)
    {
        return (value ?? string.Empty)
            .Trim()
            .ToLowerInvariant()
            .Replace("-", string.Empty)
            .Replace("_", string.Empty)
            .Replace(" ", string.Empty);
    }

    private static async Task EnsureSystemSuperAdminsAsync(ApplicationDbContext db)
    {
        var superAdminRole = await db.Roles.FirstOrDefaultAsync(r => r.RoleName == "SuperAdmin");
        if (superAdminRole is null)
        {
            return;
        }

        var shop = await db.Shops.FirstOrDefaultAsync();
        if (shop is null)
        {
            return;
        }

        var desiredUsers = new[]
        {
            new { UserName = "vkpadao", FirstName = "Vaness", LastName = "Padao", Email = "vkpadao@bytebill.local", Password = "Superadmin123!" },
            new { UserName = "vkbackup", FirstName = "VK", LastName = "Backup", Email = "vkbackup@bytebill.local", Password = "SuperAdminBackup123!" }
        };

        foreach (var su in desiredUsers)
        {
            var user = await db.Users
                .Include(u => u.UserRoles)
                .FirstOrDefaultAsync(u => u.UserName == su.UserName);

            if (user is null)
            {
                user = new User
                {
                    ShopId = shop.ShopId,
                    FirstName = su.FirstName,
                    LastName = su.LastName,
                    UserName = su.UserName,
                    Email = su.Email,
                    PasswordHash = BCrypt.Net.BCrypt.HashPassword(su.Password, workFactor: 12),
                    IsActive = true,
                    IsMfaEnabled = true,
                    MfaType = "TOTP",
                    CreatedAt = DateTime.UtcNow
                };

                db.Users.Add(user);
                await db.SaveChangesAsync();
            }
            else
            {
                if (string.IsNullOrWhiteSpace(user.Email))
                {
                    user.Email = su.Email;
                }

                user.IsActive = true;
                user.IsMfaEnabled = true;
                user.MfaType = "TOTP";
                await db.SaveChangesAsync();
            }

            var hasRole = await db.UserRoles.AnyAsync(ur => ur.UserId == user.UserId && ur.RoleId == superAdminRole.RoleId);
            if (!hasRole)
            {
                db.UserRoles.Add(new UserRoleAssignment
                {
                    UserId = user.UserId,
                    RoleId = superAdminRole.RoleId,
                    AssignedAt = DateTime.UtcNow
                });
                await db.SaveChangesAsync();
            }
        }
    }

    // ── User-Role Assignments ────────────────────────────────────────────
    private static async Task SeedUserRolesAsync(ApplicationDbContext db)
    {
        var roleMap = await db.Roles.ToDictionaryAsync(r => r.RoleName, r => r.RoleId);
        var users = await db.Users.ToListAsync();

        var mapping = new Dictionary<string, string>
        {
            ["vkpadao"]    = "SuperAdmin",
            ["vkbackup"]   = "SuperAdmin",
            ["admin"]      = "Admin",
            ["billing"]    = "Billing",
            ["technician"] = "Technician",
            ["auditor"]    = "Auditor"
        };

        foreach (var user in users)
        {
            if (mapping.TryGetValue(user.UserName, out var roleName) &&
                roleMap.TryGetValue(roleName, out var roleId))
            {
                var exists = await db.UserRoles.AnyAsync(ur => ur.UserId == user.UserId && ur.RoleId == roleId);
                if (exists) continue;

                db.UserRoles.Add(new UserRoleAssignment
                {
                    UserId = user.UserId,
                    RoleId = roleId,
                    AssignedAt = DateTime.UtcNow
                });
            }
        }

        await db.SaveChangesAsync();
    }

    // ── Customers (12) ──────────────────────────────────────────────────
    private static async Task SeedCustomersAsync(ApplicationDbContext db)
    {
        // Allow seeding even when a few customers exist (e.g. user had 2 existing)
        if (await db.Customers.CountAsync() >= 12) return;

        var shop = await db.Shops.FirstAsync();

        var customers = new List<Customer>
        {
            new() { ShopId = shop.ShopId, FirstName = "Jose",      LastName = "Dela Cruz",   Phone = "09171234567", Email = "jose.delacruz@email.com",  Address = "123 Rizal St, Quezon City",         IsActive = true, CreatedAt = DateTime.UtcNow.AddDays(-90) },
            new() { ShopId = shop.ShopId, FirstName = "Maria",     LastName = "Santos",      Phone = "09182345678", Email = "maria.santos@email.com",   Address = "45 Mabini Ave, Makati City",        IsActive = true, CreatedAt = DateTime.UtcNow.AddDays(-85) },
            new() { ShopId = shop.ShopId, FirstName = "Juan",      LastName = "Reyes",       Phone = "09193456789", Email = "juan.reyes@email.com",     Address = "78 Bonifacio Blvd, Taguig City",    IsActive = true, CreatedAt = DateTime.UtcNow.AddDays(-80) },
            new() { ShopId = shop.ShopId, FirstName = "Ana",       LastName = "Garcia",      Phone = "09204567890", Email = "ana.garcia@email.com",     Address = "12 Luna St, Pasig City",            IsActive = true, CreatedAt = DateTime.UtcNow.AddDays(-75) },
            new() { ShopId = shop.ShopId, FirstName = "Carlos",    LastName = "Mendoza",     Phone = "09215678901", Email = "carlos.mendoza@email.com", Address = "56 Aguinaldo Rd, Mandaluyong City", IsActive = true, CreatedAt = DateTime.UtcNow.AddDays(-70) },
            new() { ShopId = shop.ShopId, FirstName = "Liza",      LastName = "Villanueva",  Phone = "09226789012", Email = "liza.villa@email.com",     Address = "99 Katipunan Ave, Quezon City",     IsActive = true, CreatedAt = DateTime.UtcNow.AddDays(-65) },
            new() { ShopId = shop.ShopId, FirstName = "Mark",      LastName = "Bautista",    Phone = "09237890123", Email = "mark.bautista@email.com",  Address = "34 Roxas Blvd, Manila",             IsActive = true, CreatedAt = DateTime.UtcNow.AddDays(-60) },
            new() { ShopId = shop.ShopId, FirstName = "Cherry",    LastName = "Tanada",      Phone = "09248901234", Email = "cherry.tanada@email.com",  Address = "67 Ortigas Ave, Pasig City",        IsActive = true, CreatedAt = DateTime.UtcNow.AddDays(-55) },
            new() { ShopId = shop.ShopId, FirstName = "Rodel",     LastName = "Aquino",      Phone = "09259012345", Email = "rodel.aquino@email.com",   Address = "21 Magsaysay St, San Juan City",    IsActive = true, CreatedAt = DateTime.UtcNow.AddDays(-50) },
            new() { ShopId = shop.ShopId, FirstName = "Grace",     LastName = "Fernandez",   Phone = "09260123456", Email = "grace.fern@email.com",     Address = "88 Shaw Blvd, Mandaluyong City",    IsActive = true, CreatedAt = DateTime.UtcNow.AddDays(-45) },
            new() { ShopId = shop.ShopId, FirstName = "Paolo",     LastName = "De Leon",     Phone = "09271234568", Email = "paolo.deleon@email.com",    Address = "15 Taft Ave, Manila",               IsActive = true, CreatedAt = DateTime.UtcNow.AddDays(-40) },
            new() { ShopId = shop.ShopId, FirstName = "Diane",     LastName = "Soriano",     Phone = "09282345679", Email = "diane.soriano@email.com",  Address = "42 EDSA, Quezon City",              IsActive = true, CreatedAt = DateTime.UtcNow.AddDays(-35) },
        };

        db.Customers.AddRange(customers);
        await db.SaveChangesAsync();
    }

    // ── Devices (12 — at least one per customer) ────────────────────────
    private static async Task SeedDevicesAsync(ApplicationDbContext db)
    {
        if (await db.Devices.AnyAsync()) return;

        var customers = await db.Customers.OrderBy(c => c.CustomerId).ToListAsync();
        if (customers.Count < 10) return;

        var devices = new List<Device>
        {
            new() { CustomerId = customers[0].CustomerId, DeviceType = "Laptop",  Brand = "Lenovo",   Model = "ThinkPad X1 Carbon",   SerialNo = "LNV-2024-X1C-0917" },
            new() { CustomerId = customers[1].CustomerId, DeviceType = "Desktop", Brand = "HP",       Model = "ProDesk 400 G7",       SerialNo = "HP-2024-PD4-0918" },
            new() { CustomerId = customers[2].CustomerId, DeviceType = "Laptop",  Brand = "ASUS",     Model = "VivoBook 15",          SerialNo = "ASUS-2024-VB15-0919" },
            new() { CustomerId = customers[3].CustomerId, DeviceType = "Phone",   Brand = "Samsung",  Model = "Galaxy S24 Ultra",     SerialNo = "SAM-2024-S24U-0920" },
            new() { CustomerId = customers[4].CustomerId, DeviceType = "Laptop",  Brand = "Acer",     Model = "Aspire 5",             SerialNo = "ACR-2024-A5-0921" },
            new() { CustomerId = customers[5].CustomerId, DeviceType = "Desktop", Brand = "Dell",     Model = "OptiPlex 7020",        SerialNo = "DEL-2024-OP7-0922" },
            new() { CustomerId = customers[6].CustomerId, DeviceType = "Tablet",  Brand = "Apple",    Model = "iPad Air M2",          SerialNo = "APL-2024-IPA-0923" },
            new() { CustomerId = customers[7].CustomerId, DeviceType = "Printer", Brand = "Epson",    Model = "L3250 EcoTank",        SerialNo = "EPS-2024-L32-0924" },
            new() { CustomerId = customers[8].CustomerId, DeviceType = "Laptop",  Brand = "HP",       Model = "Pavilion 15",          SerialNo = "HP-2024-PV15-0925" },
            new() { CustomerId = customers[9].CustomerId, DeviceType = "Phone",   Brand = "Vivo",     Model = "V30 Pro",              SerialNo = "VIV-2024-V30-0926" },
            // Extra devices for customers who have more than one
            new() { CustomerId = customers[0].CustomerId, DeviceType = "Phone",   Brand = "Xiaomi",   Model = "Redmi Note 13 Pro",    SerialNo = "XIA-2024-RN13-0100" },
            new() { CustomerId = customers[2].CustomerId, DeviceType = "Desktop", Brand = "Custom",   Model = "Ryzen 5 Build",        SerialNo = "CUS-2024-R5B-0200" },
        };

        db.Devices.AddRange(devices);
        await db.SaveChangesAsync();
    }

    // ── Service Categories (5) ──────────────────────────────────────────
    private static async Task SeedServiceCategoriesAsync(ApplicationDbContext db)
    {
        if (await db.ServiceCategories.AnyAsync()) return;

        var shop = await db.Shops.FirstAsync();

        var categories = new List<ServiceCategory>
        {
            new() { ShopId = shop.ShopId, CategoryName = "Diagnosis",      Description = "Hardware and software diagnostic services" },
            new() { ShopId = shop.ShopId, CategoryName = "Repair",         Description = "Repair and replacement services" },
            new() { ShopId = shop.ShopId, CategoryName = "Installation",   Description = "Software and hardware installation services" },
            new() { ShopId = shop.ShopId, CategoryName = "Maintenance",    Description = "Preventive maintenance and cleaning services" },
            new() { ShopId = shop.ShopId, CategoryName = "Data Recovery",  Description = "Data backup and recovery services" },
        };

        db.ServiceCategories.AddRange(categories);
        await db.SaveChangesAsync();
    }

    // ── Service Catalog (12) ────────────────────────────────────────────
    private static async Task SeedServiceCatalogAsync(ApplicationDbContext db)
    {
        if (await db.ServiceCatalogs.AnyAsync()) return;

        var shop = await db.Shops.FirstAsync();
        var cats = await db.ServiceCategories.Where(c => c.ShopId == shop.ShopId).ToDictionaryAsync(c => c.CategoryName, c => c.ServiceCategoryId);
        if (cats.Count == 0) return;

        var services = new List<ServiceCatalog>
        {
            new() { ShopId = shop.ShopId, ServiceCategoryId = cats["Diagnosis"],     ServiceName = "System Diagnosis",           Description = "Full hardware and software diagnostic scan to identify issues",            BasePrice = 350m,  EstimatedDuration = 30,  IsActive = true },
            new() { ShopId = shop.ShopId, ServiceCategoryId = cats["Diagnosis"],     ServiceName = "Hardware Inspection",        Description = "Physical inspection of all internal components for damage or wear",        BasePrice = 250m,  EstimatedDuration = 20,  IsActive = true },
            new() { ShopId = shop.ShopId, ServiceCategoryId = cats["Repair"],        ServiceName = "Virus/Malware Removal",      Description = "Deep scan, removal of viruses and malware, security patching",             BasePrice = 500m,  EstimatedDuration = 60,  IsActive = true },
            new() { ShopId = shop.ShopId, ServiceCategoryId = cats["Repair"],        ServiceName = "Screen Replacement",         Description = "Full LCD/LED screen replacement including calibration",                    BasePrice = 2500m, EstimatedDuration = 90,  IsActive = true },
            new() { ShopId = shop.ShopId, ServiceCategoryId = cats["Repair"],        ServiceName = "Battery Replacement",        Description = "Battery removal, replacement, and charge cycle testing",                   BasePrice = 1200m, EstimatedDuration = 45,  IsActive = true },
            new() { ShopId = shop.ShopId, ServiceCategoryId = cats["Repair"],        ServiceName = "Keyboard Replacement",       Description = "Full keyboard unit replacement and key mapping verification",              BasePrice = 1800m, EstimatedDuration = 60,  IsActive = true },
            new() { ShopId = shop.ShopId, ServiceCategoryId = cats["Installation"],  ServiceName = "OS Installation",            Description = "Clean install of Windows/macOS/Linux with driver setup",                   BasePrice = 800m,  EstimatedDuration = 120, IsActive = true },
            new() { ShopId = shop.ShopId, ServiceCategoryId = cats["Installation"],  ServiceName = "Software Setup & Config",    Description = "Install and configure essential software, office suites, and antivirus",   BasePrice = 400m,  EstimatedDuration = 45,  IsActive = true },
            new() { ShopId = shop.ShopId, ServiceCategoryId = cats["Installation"],  ServiceName = "RAM/SSD Upgrade",            Description = "Install new RAM modules or SSD with data migration if needed",             BasePrice = 500m,  EstimatedDuration = 40,  IsActive = true },
            new() { ShopId = shop.ShopId, ServiceCategoryId = cats["Maintenance"],   ServiceName = "Internal Cleaning & Repaste",Description = "Deep clean internals, replace thermal paste, clean fans and vents",        BasePrice = 600m,  EstimatedDuration = 60,  IsActive = true },
            new() { ShopId = shop.ShopId, ServiceCategoryId = cats["Maintenance"],   ServiceName = "Full System Tune-Up",        Description = "Disk cleanup, startup optimization, registry repair, and updates",         BasePrice = 450m,  EstimatedDuration = 45,  IsActive = true },
            new() { ShopId = shop.ShopId, ServiceCategoryId = cats["Data Recovery"], ServiceName = "Data Recovery (HDD/SSD)",    Description = "Recover data from damaged or corrupted drives using professional tools",   BasePrice = 1500m, EstimatedDuration = 180, IsActive = true },
        };

        db.ServiceCatalogs.AddRange(services);
        await db.SaveChangesAsync();
    }

    // ── Inventory Categories (6) ────────────────────────────────────────
    private static async Task SeedInventoryCategoriesAsync(ApplicationDbContext db)
    {
        if (await db.Set<InventoryCategory>().AnyAsync()) return;

        var shop = await db.Shops.FirstAsync();

        var categories = new List<InventoryCategory>
        {
            new() { ShopId = shop.ShopId, CategoryName = "Storage",      Description = "SSDs, HDDs, and flash drives" },
            new() { ShopId = shop.ShopId, CategoryName = "Memory",       Description = "RAM modules and memory kits" },
            new() { ShopId = shop.ShopId, CategoryName = "Cooling",      Description = "Fans, thermal paste, and heatsinks" },
            new() { ShopId = shop.ShopId, CategoryName = "Cables",       Description = "HDMI, USB, SATA, and other cables" },
            new() { ShopId = shop.ShopId, CategoryName = "Power Supply", Description = "PSU units and power accessories" },
            new() { ShopId = shop.ShopId, CategoryName = "Peripherals",  Description = "Keyboards, mice, and other peripherals" },
        };

        db.Set<InventoryCategory>().AddRange(categories);
        await db.SaveChangesAsync();
    }

    // ── Inventory Items (12) ────────────────────────────────────────────
    private static async Task SeedInventoryItemsAsync(ApplicationDbContext db)
    {
        if (await db.InventoryItems.AnyAsync()) return;

        var shop = await db.Shops.FirstAsync();
        var cats = await db.Set<InventoryCategory>().Where(c => c.ShopId == shop.ShopId)
            .ToDictionaryAsync(c => c.CategoryName, c => c.InventoryCategoryId);

        var items = new List<InventoryItem>
        {
            new() { ShopId = shop.ShopId, InventoryCategoryId = cats.GetValueOrDefault("Storage"),      SKU = "SSD-500-SAM",    ItemName = "Samsung 870 EVO 500GB SSD",      Unit = "pcs", UnitCost = 2500m,  UnitPrice = 3500m,  QtyOnHand = 15, ReorderLevel = 5,  IsActive = true },
            new() { ShopId = shop.ShopId, InventoryCategoryId = cats.GetValueOrDefault("Storage"),      SKU = "SSD-256-KNG",    ItemName = "Kingston A400 256GB SSD",        Unit = "pcs", UnitCost = 1200m,  UnitPrice = 1800m,  QtyOnHand = 20, ReorderLevel = 5,  IsActive = true },
            new() { ShopId = shop.ShopId, InventoryCategoryId = cats.GetValueOrDefault("Memory"),       SKU = "RAM-8-COR",      ItemName = "Corsair Vengeance 8GB DDR4",     Unit = "pcs", UnitCost = 1500m,  UnitPrice = 2200m,  QtyOnHand = 10, ReorderLevel = 5,  IsActive = true },
            new() { ShopId = shop.ShopId, InventoryCategoryId = cats.GetValueOrDefault("Memory"),       SKU = "RAM-16-COR",     ItemName = "Corsair Vengeance 16GB DDR4",    Unit = "pcs", UnitCost = 2800m,  UnitPrice = 3900m,  QtyOnHand = 8,  ReorderLevel = 5,  IsActive = true },
            new() { ShopId = shop.ShopId, InventoryCategoryId = cats.GetValueOrDefault("Storage"),      SKU = "HDD-1TB-WD",     ItemName = "WD Blue 1TB HDD",               Unit = "pcs", UnitCost = 2000m,  UnitPrice = 2800m,  QtyOnHand = 3,  ReorderLevel = 5,  IsActive = true },
            new() { ShopId = shop.ShopId, InventoryCategoryId = cats.GetValueOrDefault("Cooling"),      SKU = "PST-THRM-NT",    ItemName = "Noctua NT-H1 Thermal Paste",    Unit = "pcs", UnitCost = 400m,   UnitPrice = 750m,   QtyOnHand = 25, ReorderLevel = 10, IsActive = true },
            new() { ShopId = shop.ShopId, InventoryCategoryId = cats.GetValueOrDefault("Cables"),       SKU = "CBL-HDMI-2M",    ItemName = "HDMI Cable 2m",                 Unit = "pcs", UnitCost = 150m,   UnitPrice = 350m,   QtyOnHand = 30, ReorderLevel = 10, IsActive = true },
            new() { ShopId = shop.ShopId, InventoryCategoryId = cats.GetValueOrDefault("Cables"),       SKU = "CBL-USBC-1M",    ItemName = "USB-C Cable 1m",                Unit = "pcs", UnitCost = 100m,   UnitPrice = 250m,   QtyOnHand = 40, ReorderLevel = 10, IsActive = true },
            new() { ShopId = shop.ShopId, InventoryCategoryId = cats.GetValueOrDefault("Cooling"),      SKU = "FAN-120-DPC",    ItemName = "DeepCool 120mm Case Fan",       Unit = "pcs", UnitCost = 250m,   UnitPrice = 450m,   QtyOnHand = 12, ReorderLevel = 5,  IsActive = true },
            new() { ShopId = shop.ShopId, InventoryCategoryId = cats.GetValueOrDefault("Power Supply"), SKU = "PSU-550-EVG",    ItemName = "EVGA 550W 80+ Bronze PSU",      Unit = "pcs", UnitCost = 2500m,  UnitPrice = 3600m,  QtyOnHand = 4,  ReorderLevel = 3,  IsActive = true },
            new() { ShopId = shop.ShopId, InventoryCategoryId = cats.GetValueOrDefault("Peripherals"),  SKU = "KBD-LOGI-K120",  ItemName = "Logitech K120 Keyboard",        Unit = "pcs", UnitCost = 450m,   UnitPrice = 750m,   QtyOnHand = 6,  ReorderLevel = 5,  IsActive = true },
            new() { ShopId = shop.ShopId, InventoryCategoryId = cats.GetValueOrDefault("Peripherals"),  SKU = "MOU-LOGI-B100",  ItemName = "Logitech B100 Mouse",           Unit = "pcs", UnitCost = 250m,   UnitPrice = 450m,   QtyOnHand = 2,  ReorderLevel = 5,  IsActive = true },
        };

        db.InventoryItems.AddRange(items);
        await db.SaveChangesAsync();

        // Seed initial stock-in transactions
        var savedItems = await db.InventoryItems.Where(i => i.ShopId == shop.ShopId).ToListAsync();
        foreach (var item in savedItems)
        {
            db.InventoryTxns.Add(new InventoryTxn
            {
                ItemId = item.ItemId,
                TxnType = InventoryTxnType.IN,
                Quantity = item.QtyOnHand,
                ReferenceType = "Seed",
                Remarks = "Initial stock — seeded",
                CreatedAt = DateTime.UtcNow.AddDays(-30)
            });
        }
        await db.SaveChangesAsync();
    }

    // ── Job Orders (10) ─────────────────────────────────────────────────
    private static async Task SeedJobOrdersAsync(ApplicationDbContext db)
    {
        if (await db.JobOrders.AnyAsync()) return;

        var shop = await db.Shops.FirstAsync();
        var customers = await db.Customers.Where(c => c.ShopId == shop.ShopId).OrderBy(c => c.CustomerId).ToListAsync();
        var devices   = await db.Devices.ToListAsync();
        var services  = await db.ServiceCatalogs.Where(s => s.ShopId == shop.ShopId).ToListAsync();
        var parts     = await db.InventoryItems.Where(i => i.ShopId == shop.ShopId).ToListAsync();

        if (customers.Count < 10 || devices.Count < 10 || services.Count < 5 || parts.Count < 5) return;

        var billingUser = await db.Users.FirstOrDefaultAsync(u => u.ShopId == shop.ShopId && u.UserName == "billing");
        var techUser    = await db.Users.FirstOrDefaultAsync(u => u.ShopId == shop.ShopId && u.UserName == "technician");
        if (billingUser == null || techUser == null) return;

        var joSeedData = new (int CustIdx, int DevIdx, string Problem, JobOrderStatus Status, int DaysAgo)[]
        {
            (0, 0,  "Laptop overheating and shuts down randomly",                         JobOrderStatus.Completed,        28),
            (1, 1,  "Desktop not turning on, no display",                                 JobOrderStatus.Completed,        25),
            (2, 2,  "Slow performance, suspected malware infection",                      JobOrderStatus.Completed,        22),
            (3, 3,  "Cracked screen, touch not responding",                               JobOrderStatus.Completed,        20),
            (4, 4,  "Battery drains very fast, only lasts 30 mins",                       JobOrderStatus.Completed,        18),
            (5, 5,  "Needs RAM and SSD upgrade for better performance",                   JobOrderStatus.InProgress,       10),
            (6, 6,  "iPad charging port loose, intermittent charging",                    JobOrderStatus.Diagnosis,         7),
            (7, 7,  "Printer head clogged, prints with streaks",                          JobOrderStatus.WaitingForParts,   5),
            (8, 8,  "Blue screen errors after Windows update",                            JobOrderStatus.Pending,           3),
            (9, 9,  "Phone stuck on boot loop after software update",                     JobOrderStatus.Pending,           1),
        };

        var jobOrders = new List<JobOrder>();

        for (int i = 0; i < joSeedData.Length; i++)
        {
            var (custIdx, devIdx, problem, status, daysAgo) = joSeedData[i];
            jobOrders.Add(new JobOrder
            {
                ShopId = shop.ShopId,
                CustomerId = customers[custIdx].CustomerId,
                DeviceId = devices[devIdx].DeviceId,
                CreatedByUserId = billingUser.UserId,
                AssignedTechUserId = techUser.UserId,
                JobOrderNo = $"JO-2026-{(i + 1).ToString("D4")}",
                ProblemReported = problem,
                DiagnosisNotes = status >= JobOrderStatus.InProgress ? "Diagnosed: " + problem : null,
                Status = status,
                CreatedAt = DateTime.UtcNow.AddDays(-daysAgo),
                UpdatedAt = DateTime.UtcNow.AddDays(-daysAgo + 1)
            });
        }

        db.JobOrders.AddRange(jobOrders);
        await db.SaveChangesAsync();

        // Reload to get IDs
        var savedJOs = await db.JobOrders.Where(j => j.ShopId == shop.ShopId).OrderBy(j => j.JobOrderId).ToListAsync();

        // Add services and parts to completed/in-progress JOs (first 6)
        var joServices = new List<JobOrderService>();
        var joParts = new List<JobOrderPart>();

        // JO-0001: Cleaning & repaste + thermal paste
        joServices.Add(new JobOrderService { JobOrderId = savedJOs[0].JobOrderId, ServiceId = services.First(s => s.ServiceName.Contains("Cleaning")).ServiceId, Qty = 1, UnitPrice = 600m });
        joParts.Add(new JobOrderPart { JobOrderId = savedJOs[0].JobOrderId, ItemId = parts.First(p => p.SKU == "PST-THRM-NT").ItemId, QtyUsed = 1, UnitPrice = 750m });

        // JO-0002: System diagnosis + PSU replacement
        joServices.Add(new JobOrderService { JobOrderId = savedJOs[1].JobOrderId, ServiceId = services.First(s => s.ServiceName == "System Diagnosis").ServiceId, Qty = 1, UnitPrice = 350m });
        joParts.Add(new JobOrderPart { JobOrderId = savedJOs[1].JobOrderId, ItemId = parts.First(p => p.SKU == "PSU-550-EVG").ItemId, QtyUsed = 1, UnitPrice = 3600m });

        // JO-0003: Virus removal + OS install
        joServices.Add(new JobOrderService { JobOrderId = savedJOs[2].JobOrderId, ServiceId = services.First(s => s.ServiceName.Contains("Virus")).ServiceId, Qty = 1, UnitPrice = 500m });
        joServices.Add(new JobOrderService { JobOrderId = savedJOs[2].JobOrderId, ServiceId = services.First(s => s.ServiceName == "OS Installation").ServiceId, Qty = 1, UnitPrice = 800m });

        // JO-0004: Screen replacement
        joServices.Add(new JobOrderService { JobOrderId = savedJOs[3].JobOrderId, ServiceId = services.First(s => s.ServiceName.Contains("Screen")).ServiceId, Qty = 1, UnitPrice = 2500m });

        // JO-0005: Battery replacement
        joServices.Add(new JobOrderService { JobOrderId = savedJOs[4].JobOrderId, ServiceId = services.First(s => s.ServiceName.Contains("Battery")).ServiceId, Qty = 1, UnitPrice = 1200m });

        // JO-0006 (InProgress): RAM + SSD upgrade
        joServices.Add(new JobOrderService { JobOrderId = savedJOs[5].JobOrderId, ServiceId = services.First(s => s.ServiceName.Contains("RAM/SSD")).ServiceId, Qty = 1, UnitPrice = 500m });
        joParts.Add(new JobOrderPart { JobOrderId = savedJOs[5].JobOrderId, ItemId = parts.First(p => p.SKU == "RAM-16-COR").ItemId, QtyUsed = 1, UnitPrice = 3900m });
        joParts.Add(new JobOrderPart { JobOrderId = savedJOs[5].JobOrderId, ItemId = parts.First(p => p.SKU == "SSD-500-SAM").ItemId, QtyUsed = 1, UnitPrice = 3500m });

        db.JobOrderServices.AddRange(joServices);
        db.JobOrderParts.AddRange(joParts);
        await db.SaveChangesAsync();

        // Add status history for completed JOs
        foreach (var jo in savedJOs.Where(j => j.Status == JobOrderStatus.Completed))
        {
            db.JobOrderStatusHistories.Add(new JobOrderStatusHistory
            {
                JobOrderId = jo.JobOrderId,
                OldStatus = JobOrderStatus.Pending.ToString(),
                NewStatus = JobOrderStatus.Completed.ToString(),
                ChangedByUserId = techUser.UserId,
                ChangedAt = jo.CreatedAt.AddDays(2),
                Remarks = "Work completed"
            });
        }
        await db.SaveChangesAsync();
    }

    // ── Invoices (10 — one per completed JO + extras) ───────────────────
    private static async Task SeedInvoicesAsync(ApplicationDbContext db)
    {
        if (await db.Invoices.AnyAsync()) return;

        var shop = await db.Shops.FirstAsync();
        var completedJOs = await db.JobOrders
            .Include(j => j.JobOrderServices)
            .Include(j => j.JobOrderParts)
            .Where(j => j.ShopId == shop.ShopId && j.Status == JobOrderStatus.Completed)
            .OrderBy(j => j.JobOrderId)
            .ToListAsync();

        if (completedJOs.Count == 0) return;

        int invoiceSeq = 1;

        foreach (var jo in completedJOs)
        {
            var serviceTotals = jo.JobOrderServices.Sum(s => s.Qty * s.UnitPrice);
            var partTotals    = jo.JobOrderParts.Sum(p => p.QtyUsed * p.UnitPrice);
            var subtotal      = serviceTotals + partTotals;

            // First 3 invoices are Paid, next 2 are Unpaid
            var isPaid = invoiceSeq <= 3;

            // BIR VAT-inclusive computation (12%)
            var vatableSales  = Math.Round(subtotal / 1.12m, 2);
            var vatAmount     = subtotal - vatableSales;

            var invoice = new Invoice
            {
                ShopId = shop.ShopId,
                JobOrderId = jo.JobOrderId,
                CustomerId = jo.CustomerId,
                InvoiceNo = $"INV-2026-{invoiceSeq.ToString("D4")}",
                InvoiceDate = jo.CreatedAt.AddDays(3),
                Subtotal = subtotal,
                TotalAdjustments = 0,
                TotalAmount = subtotal,
                VatableSales = vatableSales,
                VatExemptSales = 0,
                ZeroRatedSales = 0,
                VatAmount = vatAmount,
                AmountPaid = isPaid ? subtotal : 0,
                Balance = isPaid ? 0 : subtotal,
                Status = isPaid ? InvoiceStatus.Paid : InvoiceStatus.Unpaid,
                DueDate = jo.CreatedAt.AddDays(33),
                CreatedAt = jo.CreatedAt.AddDays(3)
            };

            db.Invoices.Add(invoice);
            await db.SaveChangesAsync();

            // Add invoice lines from JO services
            foreach (var svc in jo.JobOrderServices)
            {
                db.InvoiceLines.Add(new InvoiceLine
                {
                    InvoiceId = invoice.InvoiceId,
                    LineType = "Service",
                    Description = (await db.ServiceCatalogs.FindAsync(svc.ServiceId))?.ServiceName ?? "Service",
                    Qty = svc.Qty,
                    UnitPrice = svc.UnitPrice
                });
            }

            // Add invoice lines from JO parts
            foreach (var part in jo.JobOrderParts)
            {
                db.InvoiceLines.Add(new InvoiceLine
                {
                    InvoiceId = invoice.InvoiceId,
                    LineType = "Part",
                    Description = (await db.InventoryItems.FindAsync(part.ItemId))?.ItemName ?? "Part",
                    Qty = part.QtyUsed,
                    UnitPrice = part.UnitPrice
                });
            }

            await db.SaveChangesAsync();
            invoiceSeq++;
        }
    }

    // ── Payments (for paid invoices) ────────────────────────────────────
    private static async Task SeedPaymentsAsync(ApplicationDbContext db)
    {
        if (await db.Payments.AnyAsync()) return;

        var shop = await db.Shops.FirstAsync();
        var paidInvoices = await db.Invoices
            .Where(i => i.ShopId == shop.ShopId && i.Status == InvoiceStatus.Paid)
            .OrderBy(i => i.InvoiceId)
            .ToListAsync();

        if (paidInvoices.Count == 0) return;

        var billingUser = await db.Users.FirstAsync(u => u.ShopId == shop.ShopId && u.UserName == "billing");

        var methods = new[] { PaymentMethod.Cash, PaymentMethod.GCash, PaymentMethod.Card };
        var refNos  = new[] { (string?)null, "GCASH-REF-20260120-001", "CARD-****-4532" };

        for (int i = 0; i < paidInvoices.Count; i++)
        {
            var inv = paidInvoices[i];
            var method = methods[i % methods.Length];
            var refNo  = refNos[i % refNos.Length];

            var payment = new Payment
            {
                ShopId = shop.ShopId,
                PaymentNo = $"PAY-2026-{(i + 1).ToString("D4")}",
                CustomerId = inv.CustomerId,
                PaymentDate = inv.InvoiceDate.AddDays(2),
                Amount = inv.TotalAmount,
                Method = method,
                ReferenceNo = refNo,
                ReceivedByUserId = billingUser.UserId,
                Status = PaymentStatus.Confirmed,
                Notes = $"Payment for {inv.InvoiceNo}"
            };

            db.Payments.Add(payment);
            await db.SaveChangesAsync();

            db.PaymentAllocations.Add(new PaymentAllocation
            {
                PaymentId = payment.PaymentId,
                InvoiceId = inv.InvoiceId,
                AmountApplied = inv.TotalAmount
            });
            await db.SaveChangesAsync();
        }
    }

    // ── Adjustment Type Configs (default adjustment categories) ─────────
    private static async Task SeedAdjustmentTypeConfigsAsync(ApplicationDbContext db)
    {
        if (await db.AdjustmentTypeConfigs.AnyAsync()) return;

        var shop = await db.Shops.FirstAsync();

        var configs = new List<AdjustmentTypeConfig>
        {
            new() { ShopId = shop.ShopId, Name = "Senior Citizen Discount", Category = "Credit", Percentage = 20.00m, IsActive = true, CreatedAt = DateTime.UtcNow },
            new() { ShopId = shop.ShopId, Name = "PWD Discount",            Category = "Credit", Percentage = 20.00m, IsActive = true, CreatedAt = DateTime.UtcNow },
            new() { ShopId = shop.ShopId, Name = "Loyalty Discount",        Category = "Credit", Percentage = 10.00m, IsActive = true, CreatedAt = DateTime.UtcNow },
            new() { ShopId = shop.ShopId, Name = "Anniversary Discount",    Category = "Credit", Percentage = 15.00m, IsActive = true, CreatedAt = DateTime.UtcNow },
            new() { ShopId = shop.ShopId, Name = "Regular Discount",        Category = "Credit", Percentage =  5.00m, IsActive = true, CreatedAt = DateTime.UtcNow },
            new() { ShopId = shop.ShopId, Name = "Refund - Unit Damage",    Category = "Refund", Percentage = 100.00m, IsActive = true, CreatedAt = DateTime.UtcNow },
            new() { ShopId = shop.ShopId, Name = "Refund - Misdiagnosis",   Category = "Refund", Percentage = 100.00m, IsActive = true, CreatedAt = DateTime.UtcNow },
            new() { ShopId = shop.ShopId, Name = "Refund - Overcharge",     Category = "Refund", Percentage = 100.00m, IsActive = true, CreatedAt = DateTime.UtcNow },
            new() { ShopId = shop.ShopId, Name = "Additional Charge",       Category = "Debit",  Percentage =   0.00m, IsActive = true, CreatedAt = DateTime.UtcNow }
        };

        db.AdjustmentTypeConfigs.AddRange(configs);
        await db.SaveChangesAsync();
    }

    // ── Accounting Entries (double-entry journal) ───────────────────────
    private static async Task SeedAccountingEntriesAsync(ApplicationDbContext db)
    {
        if (await db.AccountingEntries.AnyAsync()) return;

        var shop = await db.Shops.FirstAsync();

        // Generate DR Accounts Receivable / CR Revenue for every invoice
        var invoices = await db.Invoices
            .Where(i => i.ShopId == shop.ShopId)
            .OrderBy(i => i.InvoiceId)
            .ToListAsync();

        foreach (var inv in invoices)
        {
            db.AccountingEntries.Add(new AccountingEntry
            {
                ShopId = shop.ShopId,
                SourceType = "Invoice",
                SourceInvoiceId = inv.InvoiceId,
                EntryDate = inv.InvoiceDate,
                AccountCode = "1200", // Accounts Receivable
                Debit = inv.TotalAmount,
                Credit = 0,
                Memo = $"Invoice {inv.InvoiceNo} issued"
            });
            db.AccountingEntries.Add(new AccountingEntry
            {
                ShopId = shop.ShopId,
                SourceType = "Invoice",
                SourceInvoiceId = inv.InvoiceId,
                EntryDate = inv.InvoiceDate,
                AccountCode = "4000", // Revenue
                Debit = 0,
                Credit = inv.TotalAmount,
                Memo = $"Revenue from invoice {inv.InvoiceNo}"
            });
        }

        // Generate DR Cash / CR Accounts Receivable for every confirmed payment
        var payments = await db.Payments
            .Where(p => p.ShopId == shop.ShopId && p.Status == PaymentStatus.Confirmed)
            .OrderBy(p => p.PaymentId)
            .ToListAsync();

        foreach (var pmt in payments)
        {
            db.AccountingEntries.Add(new AccountingEntry
            {
                ShopId = shop.ShopId,
                SourceType = "Payment",
                SourcePaymentId = pmt.PaymentId,
                EntryDate = pmt.PaymentDate,
                AccountCode = "1000", // Cash / Bank
                Debit = pmt.Amount,
                Credit = 0,
                Memo = $"Payment {pmt.PaymentNo} received via {pmt.Method}"
            });
            db.AccountingEntries.Add(new AccountingEntry
            {
                ShopId = shop.ShopId,
                SourceType = "Payment",
                SourcePaymentId = pmt.PaymentId,
                EntryDate = pmt.PaymentDate,
                AccountCode = "1200", // Accounts Receivable
                Debit = 0,
                Credit = pmt.Amount,
                Memo = $"Applied payment {pmt.PaymentNo}"
            });
        }

        await db.SaveChangesAsync();
    }

    // ── Invoice Discounts (SC/PWD + promo samples) ──────────────────────
    private static async Task SeedInvoiceDiscountsAsync(ApplicationDbContext db)
    {
        if (await db.Set<InvoiceDiscount>().AnyAsync()) return;

        var shop = await db.Shops.FirstAsync();
        var billingUser = await db.Users.FirstAsync(u => u.ShopId == shop.ShopId && u.UserName == "billing");

        // Apply a Senior Citizen discount to the 4th invoice (first Unpaid)
        var invoices = await db.Invoices
            .Include(i => i.InvoiceLines)
            .Where(i => i.ShopId == shop.ShopId)
            .OrderBy(i => i.InvoiceId)
            .ToListAsync();

        if (invoices.Count < 5) return;

        // 4th invoice: SC discount (20%, VAT-exempt)
        var inv4 = invoices[3];
        var subtotal4 = inv4.InvoiceLines.Sum(l => l.Qty * l.UnitPrice);
        var scDiscountAmt = Math.Round(subtotal4 * 0.20m, 2);

        db.Set<InvoiceDiscount>().Add(new InvoiceDiscount
        {
            InvoiceId = inv4.InvoiceId,
            DiscountType = DiscountType.SeniorCitizen,
            Label = "Senior Citizen (20%)",
            Percentage = 20m,
            Amount = scDiscountAmt,
            IsVatExempt = true,
            BeneficiaryIdNo = "SC-2024-001234",
            BeneficiaryName = "Jose Dela Cruz Sr.",
            AppliedByUserId = billingUser.UserId,
            AppliedAt = inv4.CreatedAt.AddHours(1)
        });

        // Update invoice 4 with discount-adjusted totals (VAT-exempt per BIR)
        inv4.DiscountAmount = scDiscountAmt;
        var netAmount4 = subtotal4 - scDiscountAmt;
        inv4.VatableSales = 0;           // SC discount → entire net is VAT-exempt
        inv4.VatExemptSales = netAmount4;
        inv4.VatAmount = 0;
        inv4.TotalAmount = netAmount4;
        inv4.Balance = Math.Max(0, netAmount4 - inv4.AmountPaid);

        // 5th invoice: Promo discount (5%)
        var inv5 = invoices[4];
        var subtotal5 = inv5.InvoiceLines.Sum(l => l.Qty * l.UnitPrice);
        var promoDiscount = Math.Round(subtotal5 * 0.05m, 2);

        db.Set<InvoiceDiscount>().Add(new InvoiceDiscount
        {
            InvoiceId = inv5.InvoiceId,
            DiscountType = DiscountType.Promo,
            Label = "Loyalty Discount (5%)",
            Percentage = 5m,
            Amount = promoDiscount,
            IsVatExempt = false,
            AppliedByUserId = billingUser.UserId,
            AppliedAt = inv5.CreatedAt.AddHours(1)
        });

        // Update invoice 5 with discount-adjusted totals (still VAT-inclusive)
        var netAmount5 = subtotal5 - promoDiscount;
        inv5.DiscountAmount = promoDiscount;
        inv5.VatableSales = Math.Round(netAmount5 / 1.12m, 2);
        inv5.VatAmount = netAmount5 - inv5.VatableSales;
        inv5.VatExemptSales = 0;
        inv5.TotalAmount = netAmount5;
        inv5.Balance = Math.Max(0, netAmount5 - inv5.AmountPaid);

        await db.SaveChangesAsync();
    }

    // ── Credit/Debit Adjustments ────────────────────────────────────────
    private static async Task SeedAdjustmentsAsync(ApplicationDbContext db)
    {
        if (await db.CreditDebitAdjustments.AnyAsync()) return;

        var shop = await db.Shops.FirstAsync();
        var billingUser = await db.Users.FirstAsync(u => u.ShopId == shop.ShopId && u.UserName == "billing");
        var adminUser = await db.Users.FirstAsync(u => u.ShopId == shop.ShopId && u.UserName == "admin");

        var invoices = await db.Invoices
            .Where(i => i.ShopId == shop.ShopId)
            .OrderBy(i => i.InvoiceId)
            .ToListAsync();

        if (invoices.Count < 5) return;

        // Adjustment 1: Approved credit on invoice 1 (overcharge correction)
        var adj1 = new CreditDebitAdjustment
        {
            ShopId = shop.ShopId,
            InvoiceId = invoices[0].InvoiceId,
            CreatedByUserId = billingUser.UserId,
            ReviewedByUserId = adminUser.UserId,
            AdjustmentType = AdjustmentType.Credit,
            Amount = 100m,
            Reason = "Overcharge correction — customer was quoted ₱100 less",
            Status = AdjustmentStatus.Approved,
            CreatedAt = invoices[0].CreatedAt.AddDays(1),
            ReviewedAt = invoices[0].CreatedAt.AddDays(1).AddHours(2)
        };
        db.CreditDebitAdjustments.Add(adj1);

        // Adjustment 2: Approved debit on invoice 2 (additional charge for rush service)
        var adj2 = new CreditDebitAdjustment
        {
            ShopId = shop.ShopId,
            InvoiceId = invoices[1].InvoiceId,
            CreatedByUserId = billingUser.UserId,
            ReviewedByUserId = adminUser.UserId,
            AdjustmentType = AdjustmentType.Debit,
            Amount = 200m,
            Reason = "Rush service surcharge — agreed with customer",
            Status = AdjustmentStatus.Approved,
            CreatedAt = invoices[1].CreatedAt.AddDays(1),
            ReviewedAt = invoices[1].CreatedAt.AddDays(1).AddHours(3)
        };
        db.CreditDebitAdjustments.Add(adj2);

        // Adjustment 3: Pending refund request on invoice 3
        db.CreditDebitAdjustments.Add(new CreditDebitAdjustment
        {
            ShopId = shop.ShopId,
            InvoiceId = invoices[2].InvoiceId,
            CreatedByUserId = billingUser.UserId,
            AdjustmentType = AdjustmentType.Refund,
            Amount = 500m,
            Reason = "Customer requesting partial refund for virus removal — issue re-occurred",
            Status = AdjustmentStatus.Pending,
            CreatedAt = DateTime.UtcNow.AddDays(-5)
        });

        // Adjustment 4: Rejected credit on invoice 4
        db.CreditDebitAdjustments.Add(new CreditDebitAdjustment
        {
            ShopId = shop.ShopId,
            InvoiceId = invoices[3].InvoiceId,
            CreatedByUserId = billingUser.UserId,
            ReviewedByUserId = adminUser.UserId,
            AdjustmentType = AdjustmentType.Credit,
            Amount = 300m,
            Reason = "Customer claims parts were overpriced",
            Status = AdjustmentStatus.Rejected,
            CreatedAt = invoices[3].CreatedAt.AddDays(2),
            ReviewedAt = invoices[3].CreatedAt.AddDays(3)
        });

        await db.SaveChangesAsync();

        // Update totals for invoices with APPROVED adjustments
        // Invoice 1: Credit ₱100 → reduces total
        invoices[0].TotalAdjustments = -100m;
        invoices[0].TotalAmount = invoices[0].Subtotal - invoices[0].DiscountAmount + invoices[0].TotalAdjustments;
        invoices[0].Balance = Math.Max(0, invoices[0].TotalAmount - invoices[0].AmountPaid);

        // Invoice 2: Debit ₱200 → increases total
        invoices[1].TotalAdjustments = 200m;
        invoices[1].TotalAmount = invoices[1].Subtotal - invoices[1].DiscountAmount + invoices[1].TotalAdjustments;
        invoices[1].Balance = Math.Max(0, invoices[1].TotalAmount - invoices[1].AmountPaid);

        await db.SaveChangesAsync();

        // Generate accounting entries for approved adjustments
        var approvedAdjs = await db.CreditDebitAdjustments
            .Include(a => a.Invoice)
            .Where(a => a.ShopId == shop.ShopId && a.Status == AdjustmentStatus.Approved)
            .ToListAsync();

        foreach (var adj in approvedAdjs)
        {
            if (adj.Invoice is null) continue;
            var invoiceNo = adj.Invoice.InvoiceNo;

            if (adj.AdjustmentType == AdjustmentType.Credit || adj.AdjustmentType == AdjustmentType.Refund)
            {
                var acctCode = adj.AdjustmentType == AdjustmentType.Refund ? "5100" : "5200";
                db.AccountingEntries.Add(new AccountingEntry
                {
                    ShopId = shop.ShopId,
                    SourceType = "Adjustment",
                    SourceInvoiceId = adj.InvoiceId,
                    EntryDate = adj.ReviewedAt ?? adj.CreatedAt,
                    AccountCode = acctCode,
                    Debit = adj.Amount,
                    Credit = 0,
                    Memo = $"{adj.AdjustmentType} on {invoiceNo} (adj#{adj.AdjustmentId})"
                });
                db.AccountingEntries.Add(new AccountingEntry
                {
                    ShopId = shop.ShopId,
                    SourceType = "Adjustment",
                    SourceInvoiceId = adj.InvoiceId,
                    EntryDate = adj.ReviewedAt ?? adj.CreatedAt,
                    AccountCode = "1200",
                    Debit = 0,
                    Credit = adj.Amount,
                    Memo = $"{adj.AdjustmentType} applied to {invoiceNo} (adj#{adj.AdjustmentId})"
                });
            }
            else // Debit
            {
                db.AccountingEntries.Add(new AccountingEntry
                {
                    ShopId = shop.ShopId,
                    SourceType = "Adjustment",
                    SourceInvoiceId = adj.InvoiceId,
                    EntryDate = adj.ReviewedAt ?? adj.CreatedAt,
                    AccountCode = "1200",
                    Debit = adj.Amount,
                    Credit = 0,
                    Memo = $"Debit adjustment on {invoiceNo} (adj#{adj.AdjustmentId})"
                });
                db.AccountingEntries.Add(new AccountingEntry
                {
                    ShopId = shop.ShopId,
                    SourceType = "Adjustment",
                    SourceInvoiceId = adj.InvoiceId,
                    EntryDate = adj.ReviewedAt ?? adj.CreatedAt,
                    AccountCode = "5200",
                    Debit = 0,
                    Credit = adj.Amount,
                    Memo = $"Debit adjustment on {invoiceNo} (adj#{adj.AdjustmentId})"
                });
            }
        }

        await db.SaveChangesAsync();
    }

    // ── PayMongo Transactions ───────────────────────────────────────────
    private static async Task SeedPayMongoTxnsAsync(ApplicationDbContext db)
    {
        if (await db.PayMongoTxns.AnyAsync()) return;

        var shop = await db.Shops.FirstAsync();
        var billingUser = await db.Users.FirstAsync(u => u.ShopId == shop.ShopId && u.UserName == "billing");

        // Get the Card payment (3rd payment) to link as a PayMongo transaction
        var cardPayment = await db.Payments
            .Where(p => p.ShopId == shop.ShopId && p.Method == PaymentMethod.Card && p.Status == PaymentStatus.Confirmed)
            .FirstOrDefaultAsync();

        var unpaidInvoices = await db.Invoices
            .Where(i => i.ShopId == shop.ShopId && i.Status != InvoiceStatus.Paid && i.Status != InvoiceStatus.Void)
            .OrderBy(i => i.InvoiceId)
            .ToListAsync();

        // PayMongo Txn 1: Completed transaction linked to card payment
        if (cardPayment != null)
        {
            var allocation = await db.PaymentAllocations
                .FirstOrDefaultAsync(pa => pa.PaymentId == cardPayment.PaymentId);

            if (allocation != null)
            {
                db.PayMongoTxns.Add(new PayMongoTxn
                {
                    PaymentId = cardPayment.PaymentId,
                    ShopId = shop.ShopId,
                    InvoiceId = allocation.InvoiceId,
                    InitiatedByUserId = billingUser.UserId,
                    Amount = cardPayment.Amount,
                    PayMongoPaymentIntentId = "pi_seed_completed_001",
                    PayMongoStatus = "paid",
                    PayMongoPaymentMethod = "card",
                    ResourceType = "checkout_session",
                    CheckoutUrl = "https://checkout.paymongo.com/cs_seed_001",
                    RawResponse = "{\"data\":{\"id\":\"cs_seed_001\",\"attributes\":{\"status\":\"paid\",\"payment_intent\":{\"id\":\"pi_seed_completed_001\"}}}}",
                    CreatedAt = cardPayment.PaymentDate.AddHours(-1),
                    UpdatedAt = cardPayment.PaymentDate
                });
            }
        }

        // PayMongo Txn 2: Expired checkout session (customer didn't complete)
        if (unpaidInvoices.Count > 0)
        {
            db.PayMongoTxns.Add(new PayMongoTxn
            {
                PaymentId = null,  // no payment — session expired
                ShopId = shop.ShopId,
                InvoiceId = unpaidInvoices[0].InvoiceId,
                InitiatedByUserId = billingUser.UserId,
                Amount = unpaidInvoices[0].TotalAmount,
                PayMongoPaymentIntentId = "pi_seed_expired_002",
                PayMongoStatus = "expired",
                PayMongoPaymentMethod = null,
                ResourceType = "checkout_session",
                CheckoutUrl = "https://checkout.paymongo.com/cs_seed_002",
                RawResponse = "{\"data\":{\"id\":\"cs_seed_002\",\"attributes\":{\"status\":\"expired\"}}}",
                CreatedAt = DateTime.UtcNow.AddDays(-10),
                UpdatedAt = DateTime.UtcNow.AddDays(-9)
            });
        }

        // PayMongo Txn 3: Pending payment link (awaiting customer action)
        if (unpaidInvoices.Count > 1)
        {
            db.PayMongoTxns.Add(new PayMongoTxn
            {
                PaymentId = null,
                ShopId = shop.ShopId,
                InvoiceId = unpaidInvoices[1].InvoiceId,
                InitiatedByUserId = billingUser.UserId,
                Amount = unpaidInvoices[1].TotalAmount,
                PayMongoPaymentIntentId = "link_seed_pending_003",
                PayMongoStatus = "unpaid",
                PayMongoPaymentMethod = null,
                ResourceType = "link",
                CheckoutUrl = "https://pm.link/bytebill-seed/test/seed003",
                RawResponse = "{\"data\":{\"id\":\"link_seed_003\",\"attributes\":{\"status\":\"unpaid\",\"checkout_url\":\"https://pm.link/bytebill-seed/test/seed003\"}}}",
                CreatedAt = DateTime.UtcNow.AddDays(-2)
            });
        }

        await db.SaveChangesAsync();
    }

    // ── Notifications ───────────────────────────────────────────────────
    private static async Task SeedNotificationsAsync(ApplicationDbContext db)
    {
        if (await db.Notifications.AnyAsync()) return;

        var shop = await db.Shops.FirstAsync();
        var adminUser = await db.Users.FirstAsync(u => u.ShopId == shop.ShopId && u.UserName == "admin");
        var billingUser = await db.Users.FirstAsync(u => u.ShopId == shop.ShopId && u.UserName == "billing");
        var techUser = await db.Users.FirstAsync(u => u.ShopId == shop.ShopId && u.UserName == "technician");

        var invoices = await db.Invoices.Where(i => i.ShopId == shop.ShopId).OrderBy(i => i.InvoiceId).ToListAsync();
        var jobOrders = await db.JobOrders.Where(j => j.ShopId == shop.ShopId).OrderBy(j => j.JobOrderId).ToListAsync();

        var notifications = new List<Notification>();

        // Invoice creation notifications → admin
        foreach (var inv in invoices.Take(3))
        {
            notifications.Add(new Notification
            {
                UserId = adminUser.UserId,
                ShopId = shop.ShopId,
                Title = "Invoice Created",
                Message = $"Invoice {inv.InvoiceNo} for {inv.TotalAmount:C} has been created.",
                Type = "info",
                Url = $"/Admin/Invoices/DetailsModal/{inv.InvoiceId}",
                IsRead = true,
                CreatedAt = inv.CreatedAt
            });
        }

        // Payment received notifications → admin
        var payments = await db.Payments.Where(p => p.ShopId == shop.ShopId).OrderBy(p => p.PaymentId).ToListAsync();
        foreach (var pmt in payments)
        {
            notifications.Add(new Notification
            {
                UserId = adminUser.UserId,
                ShopId = shop.ShopId,
                Title = "Payment Received",
                Message = $"Payment {pmt.PaymentNo} of {pmt.Amount:C} received via {pmt.Method}.",
                Type = "info",
                Url = $"/Admin/Payments/Receipt/{pmt.PaymentId}",
                IsRead = true,
                CreatedAt = pmt.PaymentDate
            });
        }

        // Job order status change notifications → technician
        foreach (var jo in jobOrders.Where(j => j.Status == JobOrderStatus.Completed).Take(3))
        {
            notifications.Add(new Notification
            {
                UserId = techUser.UserId,
                ShopId = shop.ShopId,
                Title = "Job Order Completed",
                Message = $"Job order {jo.JobOrderNo} has been marked as Completed.",
                Type = "info",
                Url = $"/Technician/JobOrders/{jo.JobOrderId}",
                IsRead = true,
                CreatedAt = jo.UpdatedAt ?? jo.CreatedAt.AddDays(2)
            });
        }

        // Adjustment pending notification → admin (unread)
        var pendingAdj = await db.CreditDebitAdjustments
            .FirstOrDefaultAsync(a => a.ShopId == shop.ShopId && a.Status == AdjustmentStatus.Pending);
        if (pendingAdj != null)
        {
            notifications.Add(new Notification
            {
                UserId = adminUser.UserId,
                ShopId = shop.ShopId,
                Title = "Adjustment Pending Approval",
                Message = $"A {pendingAdj.AdjustmentType} adjustment of {pendingAdj.Amount:C} is awaiting your review.",
                Type = "adjustment",
                Url = $"/Admin/Invoices/DetailsModal/{pendingAdj.InvoiceId}",
                IsRead = false,
                CreatedAt = pendingAdj.CreatedAt
            });
        }

        // Low stock alert → admin (unread)
        var lowStockItems = await db.InventoryItems
            .Where(i => i.ShopId == shop.ShopId && i.QtyOnHand <= i.ReorderLevel && i.IsActive)
            .Take(3)
            .ToListAsync();
        foreach (var item in lowStockItems)
        {
            notifications.Add(new Notification
            {
                UserId = adminUser.UserId,
                ShopId = shop.ShopId,
                Title = "Low Stock Alert",
                Message = $"{item.ItemName} is low on stock ({item.QtyOnHand} remaining, reorder level: {item.ReorderLevel}).",
                Type = "warning",
                Url = "/Admin/Inventory",
                IsRead = false,
                CreatedAt = DateTime.UtcNow.AddDays(-1)
            });
        }

        // New job order notification → billing
        foreach (var jo in jobOrders.Where(j => j.Status == JobOrderStatus.Pending).Take(2))
        {
            notifications.Add(new Notification
            {
                UserId = billingUser.UserId,
                ShopId = shop.ShopId,
                Title = "New Job Order",
                Message = $"Job order {jo.JobOrderNo} has been created and is pending assignment.",
                Type = "info",
                Url = $"/Billing/JobOrders/{jo.JobOrderId}",
                IsRead = false,
                CreatedAt = jo.CreatedAt
            });
        }

        db.Notifications.AddRange(notifications);
        await db.SaveChangesAsync();
    }

    // ── Audit Logs (transaction history trail) ──────────────────────────
    private static async Task SeedAuditLogsAsync(ApplicationDbContext db)
    {
        if (await db.AuditLogs.AnyAsync()) return;

        var shop = await db.Shops.FirstAsync();
        var billingUser = await db.Users.FirstAsync(u => u.ShopId == shop.ShopId && u.UserName == "billing");
        var adminUser = await db.Users.FirstAsync(u => u.ShopId == shop.ShopId && u.UserName == "admin");
        var techUser = await db.Users.FirstAsync(u => u.ShopId == shop.ShopId && u.UserName == "technician");

        var jobOrders = await db.JobOrders.Where(j => j.ShopId == shop.ShopId).OrderBy(j => j.JobOrderId).ToListAsync();
        var invoices = await db.Invoices.Where(i => i.ShopId == shop.ShopId).OrderBy(i => i.InvoiceId).ToListAsync();
        var payments = await db.Payments.Where(p => p.ShopId == shop.ShopId).OrderBy(p => p.PaymentId).ToListAsync();

        var logs = new List<AuditLog>();

        // Job order creation logs
        foreach (var jo in jobOrders)
        {
            logs.Add(new AuditLog
            {
                ShopId = shop.ShopId,
                UserId = billingUser.UserId,
                Action = "Create",
                EntityName = "JobOrder",
                EntityId = jo.JobOrderId,
                Details = $"Created job order {jo.JobOrderNo} for customer #{jo.CustomerId}. Problem: {jo.ProblemReported}",
                IpAddress = "127.0.0.1",
                CreatedAt = jo.CreatedAt
            });
        }

        // Job order status change logs (completed ones)
        foreach (var jo in jobOrders.Where(j => j.Status == JobOrderStatus.Completed))
        {
            logs.Add(new AuditLog
            {
                ShopId = shop.ShopId,
                UserId = techUser.UserId,
                Action = "StatusChange",
                EntityName = "JobOrder",
                EntityId = jo.JobOrderId,
                Details = $"Job order {jo.JobOrderNo} status changed from Pending to Completed.",
                IpAddress = "127.0.0.1",
                OldValues = "{\"Status\":\"Pending\"}",
                NewValues = "{\"Status\":\"Completed\"}",
                CreatedAt = jo.CreatedAt.AddDays(2)
            });
        }

        // Invoice creation logs
        foreach (var inv in invoices)
        {
            logs.Add(new AuditLog
            {
                ShopId = shop.ShopId,
                UserId = billingUser.UserId,
                Action = "Create",
                EntityName = "Invoice",
                EntityId = inv.InvoiceId,
                Details = $"Created invoice {inv.InvoiceNo}. Total: {inv.TotalAmount:C}.",
                IpAddress = "127.0.0.1",
                CreatedAt = inv.CreatedAt
            });
        }

        // Payment logs
        foreach (var pmt in payments)
        {
            logs.Add(new AuditLog
            {
                ShopId = shop.ShopId,
                UserId = billingUser.UserId,
                Action = "Create",
                EntityName = "Payment",
                EntityId = pmt.PaymentId,
                Details = $"Recorded payment {pmt.PaymentNo} of {pmt.Amount:C} via {pmt.Method}.",
                IpAddress = "127.0.0.1",
                CreatedAt = pmt.PaymentDate
            });
        }

        // Adjustment logs
        var adjustments = await db.CreditDebitAdjustments
            .Include(a => a.Invoice)
            .Where(a => a.ShopId == shop.ShopId)
            .ToListAsync();

        foreach (var adj in adjustments)
        {
            logs.Add(new AuditLog
            {
                ShopId = shop.ShopId,
                UserId = adj.CreatedByUserId,
                Action = "Adjustment",
                EntityName = "Invoice",
                EntityId = adj.InvoiceId,
                Details = $"{adj.AdjustmentType} adjustment of {adj.Amount:C} on {adj.Invoice?.InvoiceNo ?? ""}. Reason: {adj.Reason}. Status: {adj.Status}.",
                IpAddress = "127.0.0.1",
                CreatedAt = adj.CreatedAt
            });
        }

        db.AuditLogs.AddRange(logs);
        await db.SaveChangesAsync();
    }

    // ── Repair Invoice Balances ──────────────────────────────────────────
    // Recalculates Balance & Status for every invoice based on confirmed
    // PaymentAllocations to fix any inconsistencies from prior seeding or
    // payment processing bugs.
    private static async Task RepairInvoiceBalancesAsync(ApplicationDbContext db)
    {
        var invoices = await db.Invoices.ToListAsync();
        if (invoices.Count == 0) return;

        var paidLookup = await db.PaymentAllocations
            .Where(pa => pa.Payment!.Status == PaymentStatus.Confirmed)
            .GroupBy(pa => pa.InvoiceId)
            .Select(g => new { InvoiceId = g.Key, TotalPaid = g.Sum(pa => pa.AmountApplied) })
            .ToDictionaryAsync(x => x.InvoiceId, x => x.TotalPaid);

        bool changed = false;

        foreach (var inv in invoices)
        {
            if (inv.Status == InvoiceStatus.Void) continue;

            var amountPaid = paidLookup.GetValueOrDefault(inv.InvoiceId, 0m);
            var balance = Math.Max(0, inv.TotalAmount - amountPaid);

            InvoiceStatus correctStatus;
            if (balance <= 0)
                correctStatus = InvoiceStatus.Paid;
            else if (amountPaid > 0)
                correctStatus = InvoiceStatus.Partial;
            else
                correctStatus = InvoiceStatus.Unpaid;

            if (inv.AmountPaid != amountPaid || inv.Balance != balance || inv.Status != correctStatus)
            {
                inv.AmountPaid = amountPaid;
                inv.Balance = balance;
                inv.Status = correctStatus;
                changed = true;
            }
        }

        if (changed)
            await db.SaveChangesAsync();
    }

    // ── Subscription Plans ───────────────────────────────────────────
    private static async Task SeedSubscriptionPlansAsync(ApplicationDbContext db)
    {
        if (await db.SubscriptionPlans.AnyAsync()) return;

        db.SubscriptionPlans.AddRange(
            new SubscriptionPlan
            {
                PlanName = "Basic",
                Description = "Perfect for small repair shops just getting started.",
                MonthlyPrice = 999m,
                YearlyPrice = 9_590m,       // ~₱799/mo — 20% off
                PermanentPrice = 35_964m,   // 36× monthly
                MaxUsers = 3,
                MaxCustomers = 50,
                MaxJobOrdersPerMonth = 100,
                HasAdvancedReports = false,
                HasXeroIntegration = false,
                HasPrioritySupport = false,
                SortOrder = 1,
                IsActive = true
            },
            new SubscriptionPlan
            {
                PlanName = "Professional",
                Description = "For growing businesses that need more power and integrations.",
                MonthlyPrice = 3_499m,
                YearlyPrice = 33_590m,      // ~₱2,799/mo — 20% off
                PermanentPrice = 125_964m,  // 36× monthly
                MaxUsers = 10,
                MaxCustomers = 200,
                MaxJobOrdersPerMonth = 500,
                HasAdvancedReports = true,
                HasXeroIntegration = true,
                HasPrioritySupport = false,
                SortOrder = 2,
                IsActive = true
            },
            new SubscriptionPlan
            {
                PlanName = "Enterprise",
                Description = "Unlimited everything for established multi-branch operations.",
                MonthlyPrice = 6_999m,
                YearlyPrice = 67_190m,      // ~₱5,599/mo — 20% off
                PermanentPrice = 251_964m,  // 36× monthly
                MaxUsers = 0,               // unlimited
                MaxCustomers = 0,           // unlimited
                MaxJobOrdersPerMonth = 0,   // unlimited
                HasAdvancedReports = true,
                HasXeroIntegration = true,
                HasPrioritySupport = true,
                SortOrder = 3,
                IsActive = true
            }
        );

        await db.SaveChangesAsync();
    }

    // ── Assign Subscription to ByteBill Main Shop ────────────────────
    private static async Task SeedMainShopSubscriptionAsync(ApplicationDbContext db)
    {
        var mainShop = await db.Shops.FirstOrDefaultAsync(s => s.ShopCode == "MAIN");
        if (mainShop == null) return;

        // Skip if already has a subscription
        if (await db.Subscriptions.AnyAsync(s => s.ShopId == mainShop.ShopId)) return;

        var enterprisePlan = await db.SubscriptionPlans.FirstOrDefaultAsync(p => p.PlanName == "Enterprise");
        if (enterprisePlan == null) return;

        var subscription = new Subscription
        {
            ShopId = mainShop.ShopId,
            PlanId = enterprisePlan.PlanId,
            BillingCycle = "Permanent",
            Status = "Active",
            Price = 0m, // Demo shop — no charge
            StartDate = DateTime.UtcNow,
            EndDate = null,
            NextBillingDate = null,
            IsDefault = true,
            CreatedAt = DateTime.UtcNow
        };
        db.Subscriptions.Add(subscription);
        await db.SaveChangesAsync();
    }
}
