using ByteBill_BS.Models;
using Microsoft.EntityFrameworkCore;

namespace ByteBill_BS.Data;

/// <summary>
/// Seeds the database with default roles and demo users on first run.
/// Called from Program.cs during application startup.
/// </summary>
public static class DbSeeder
{
    // BCrypt hash of 'Password123!' (cost 12)
    private const string DefaultPasswordHash =
        "$2a$12$LJ3m4ys3Lk0TSwHleDJruOEGCxCXOGyGqoLNaHPbBMp7c8.hgy7G6";

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
            Phone = "+63-000-000-0000",
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

    // ── Demo Users ───────────────────────────────────────────────────────
    private static async Task SeedUsersAsync(ApplicationDbContext db)
    {
        if (await db.Users.AnyAsync()) return;

        var shop = await db.Shops.FirstAsync();

        var users = new List<User>
        {
            new() { ShopId = shop.ShopId, FirstName = "Super",    LastName = "Admin",   UserName = "superadmin", PasswordHash = DefaultPasswordHash, IsActive = true, CreatedAt = DateTime.UtcNow },
            new() { ShopId = shop.ShopId, FirstName = "Shop",     LastName = "Owner",   UserName = "admin",      PasswordHash = DefaultPasswordHash, IsActive = true, CreatedAt = DateTime.UtcNow },
            new() { ShopId = shop.ShopId, FirstName = "Billing",  LastName = "Staff",   UserName = "billing",    PasswordHash = DefaultPasswordHash, IsActive = true, CreatedAt = DateTime.UtcNow },
            new() { ShopId = shop.ShopId, FirstName = "Tech",     LastName = "Support", UserName = "tech",       PasswordHash = DefaultPasswordHash, IsActive = true, CreatedAt = DateTime.UtcNow },
            new() { ShopId = shop.ShopId, FirstName = "External", LastName = "Auditor", UserName = "auditor",    PasswordHash = DefaultPasswordHash, IsActive = true, CreatedAt = DateTime.UtcNow }
        };

        db.Users.AddRange(users);
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
            ["superadmin"] = "SuperAdmin",
            ["admin"]      = "Admin",
            ["billing"]    = "Billing",
            ["tech"]       = "Technician",
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
