using ByteBill_BS.Models;
using Microsoft.EntityFrameworkCore;

namespace ByteBill_BS.Data;

/// <summary>
/// Seeds the database with default roles and demo users on first run.
/// Called from Program.cs during application startup.
/// </summary>
public static class DbSeeder
{
    public static async Task SeedAsync(ApplicationDbContext db)
    {
        // Ensure the database exists (no-op if already created via SQL script)
        await db.Database.EnsureCreatedAsync();

        await SeedShopAsync(db);
        await SeedRolesAsync(db);
        await SeedUsersAsync(db);
        await SeedUserRolesAsync(db);
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
            Address = "Metro Manila, Philippines",
            Status = "Active",
            CreatedAt = DateTime.UtcNow
        });

        await db.SaveChangesAsync();
    }

    // ── Roles ────────────────────────────────────────────────────────────
    private static async Task SeedRolesAsync(ApplicationDbContext db)
    {
        if (await db.Roles.AnyAsync()) return;

        var roles = new List<Role>
        {
            new() { RoleName = "SuperAdmin",  Description = "Full system access across all shops" },
            new() { RoleName = "Admin",        Description = "Shop Owner — full access within a single shop" },
            new() { RoleName = "Billing",      Description = "Billing staff — invoices, payments, and customer management" },
            new() { RoleName = "Technician",   Description = "Technician — job orders, diagnostics, and repairs" },
            new() { RoleName = "Auditor",      Description = "Auditor — read-only access for review and compliance" }
        };

        db.Roles.AddRange(roles);
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
        if (await db.Users.AnyAsync()) return;

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

    // ── User-Role Assignments ────────────────────────────────────────────
    private static async Task SeedUserRolesAsync(ApplicationDbContext db)
    {
        if (await db.UserRoles.AnyAsync()) return;

        var roleMap = await db.Roles.ToDictionaryAsync(r => r.RoleName, r => r.RoleId);
        var users = await db.Users.ToListAsync();

        var mapping = new Dictionary<string, string>
        {
            ["vkpadao"]    = "SuperAdmin",
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
}
