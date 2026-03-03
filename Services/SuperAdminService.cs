using ByteBill_BS.Data;
using ByteBill_BS.Models;
using ByteBill_BS.Models.Enums;
using ByteBill_BS.ViewModels.SuperAdmin;
using ByteBill_BS.ViewModels.Dashboard;
using Microsoft.EntityFrameworkCore;

namespace ByteBill_BS.Services;

public interface ISuperAdminService
{
    // ── Dashboard ────────────────────────────────────────────────
    Task<SuperAdminDashboardData> GetDashboardDataAsync(string period);

    // ── Shops ────────────────────────────────────────────────────
    Task<ShopListViewModel> GetShopsAsync(string? search, string? status, string? plan, int page, int pageSize = 10);
    Task<ShopFormViewModel?> GetShopForEditAsync(long id);
    Task<ShopDetailViewModel?> GetShopDetailsAsync(long id);
    Task<(bool Success, string Message, long ShopId)> CreateShopAsync(ShopCreateViewModel model, long createdByUserId, string? ipAddress);
    Task<(bool Success, string Message)> UpdateShopAsync(ShopFormViewModel model, long updatedByUserId, string? ipAddress);
    Task<(bool Success, string Message)> ToggleShopStatusAsync(long shopId, long userId, string? ipAddress);
    Task<(bool Success, string Message)> DeleteShopAsync(long shopId, long userId, string? ipAddress);

    // ── Users ────────────────────────────────────────────────────
    Task<GlobalUserListViewModel> GetUsersAsync(string? search, UserRole? role, string? shop, string? status, int page, int pageSize = 10);
    Task<GlobalUserFormViewModel?> GetUserForEditAsync(long id);
    Task<GlobalUserDetailViewModel?> GetUserDetailsAsync(long id);
    Task<(bool Success, string Message)> CreateUserAsync(GlobalUserFormViewModel model, long createdByUserId, string? ipAddress);
    Task<(bool Success, string Message)> UpdateUserAsync(GlobalUserFormViewModel model, long updatedByUserId, string? ipAddress);
    Task<(bool Success, string Message)> ToggleUserStatusAsync(long userId, long actionUserId, string? ipAddress);
    Task<(bool Success, string Message)> ResetUserPasswordAsync(long userId, string newPassword, long actionUserId, string? ipAddress);
    Task<List<ShopDropdownItem>> GetShopDropdownAsync();

    // ── Subscriptions ────────────────────────────────────────────
    Task<List<SubscriptionPlan>> GetActivePlansAsync();
    Task<SubscriptionListViewModel> GetSubscriptionsAsync(string? search, string? status, string? plan, int page, int pageSize = 10);
    Task<SubscriptionDetailViewModel?> GetSubscriptionDetailsAsync(long id);
    Task<(bool Success, string Message, string? CheckoutUrl)> AssignSubscriptionAsync(long shopId, long planId, string billingCycle, long userId, string? ipAddress);
    Task<(bool Success, string Message)> CancelSubscriptionAsync(long subscriptionId, long userId, string? ipAddress);

    // ── Subscription Payments ────────────────────────────────────
    Task<SubscriptionPaymentListViewModel> GetPaymentsAsync(string? search, string? status, string? method, DateTime? from, DateTime? to, int page, int pageSize = 10);
    Task<SubscriptionPaymentDetailViewModel?> GetPaymentDetailsAsync(long id);

    // ── Announcements ────────────────────────────────────────────
    Task<AnnouncementListViewModel> GetAnnouncementsAsync(string? search, string? type, string? status, int page, int pageSize = 10);
    Task<AnnouncementFormViewModel?> GetAnnouncementForEditAsync(long id);
    Task<(bool Success, string Message)> CreateAnnouncementAsync(AnnouncementFormViewModel model, long userId);
    Task<(bool Success, string Message)> UpdateAnnouncementAsync(AnnouncementFormViewModel model, long userId);
    Task<(bool Success, string Message)> PublishAnnouncementAsync(long id, long userId);
    Task<(bool Success, string Message)> DeleteAnnouncementAsync(long id, long userId);

    // ── Settings ─────────────────────────────────────────────────
    Task<Dictionary<string, string>> GetSettingsAsync(string? category = null);
    Task<(bool Success, string Message)> SaveSettingsAsync(Dictionary<string, string> settings, string category, long userId);

    // ── Reports ──────────────────────────────────────────────────
    Task<ReportsIndexViewModel> GenerateReportAsync(string report, DateTime from, DateTime to);
    Task<byte[]> ExportReportCsvAsync(string report, DateTime from, DateTime to);

    // ── Audit Log ────────────────────────────────────────────────
    Task LogActionAsync(long userId, string action, string? entityType, long? entityId, string? details, string? ipAddress);
}

/// <summary>
/// Dashboard data DTO for SuperAdmin.
/// </summary>
public class SuperAdminDashboardData
{
    public int TotalShops { get; set; }
    public int ActiveShops { get; set; }
    public int NewShopsThisMonth { get; set; }
    public int TotalUsers { get; set; }
    public decimal MonthRevenue { get; set; }
    public decimal PreviousMonthRevenue { get; set; }
    public int ActiveSubscriptions { get; set; }
    public int ExpiringSubscriptions { get; set; } // expiring in 7 days
    public int OverduePayments { get; set; }
    public List<ChartDataPoint> RevenueChart { get; set; } = new();
    public List<ChartDataPoint> ShopsGrowthChart { get; set; } = new();
    public List<ChartDataPoint> SubscriptionDistribution { get; set; } = new();
    public List<RecentActivityItem> RecentActivity { get; set; } = new();
}

public class SuperAdminService : ISuperAdminService
{
    private readonly ApplicationDbContext _db;

    public SuperAdminService(ApplicationDbContext db)
    {
        _db = db;
    }

    // ═══════════════════════════════════════════════════════════════
    //  DASHBOARD
    // ═══════════════════════════════════════════════════════════════
    public async Task<SuperAdminDashboardData> GetDashboardDataAsync(string period)
    {
        var now = DateTime.UtcNow;
        var monthStart = new DateTime(now.Year, now.Month, 1);
        var prevMonthStart = monthStart.AddMonths(-1);

        // Determine chart range based on period
        int chartMonths = period switch
        {
            "week" => 1,
            "month" => 1,
            "3months" => 3,
            "6months" => 6,
            "year" => 12,
            _ => 6
        };

        var chartStart = monthStart.AddMonths(-chartMonths + 1);

        var data = new SuperAdminDashboardData
        {
            TotalShops = await _db.Shops.CountAsync(),
            ActiveShops = await _db.Shops.CountAsync(s => s.Status == "Active"),
            NewShopsThisMonth = await _db.Shops.CountAsync(s => s.CreatedAt >= monthStart),
            TotalUsers = await _db.Users.CountAsync(),
            MonthRevenue = await _db.SubscriptionPayments
                .Where(p => p.Status == "Paid" && p.PaidAt >= monthStart)
                .SumAsync(p => (decimal?)p.Amount) ?? 0m,
            PreviousMonthRevenue = await _db.SubscriptionPayments
                .Where(p => p.Status == "Paid" && p.PaidAt >= prevMonthStart && p.PaidAt < monthStart)
                .SumAsync(p => (decimal?)p.Amount) ?? 0m,
            ActiveSubscriptions = await _db.Subscriptions.CountAsync(s => s.Status == "Active"),
            ExpiringSubscriptions = await _db.Subscriptions
                .CountAsync(s => s.Status == "Active" && s.EndDate != null && s.EndDate <= now.AddDays(7)),
            OverduePayments = await _db.SubscriptionPayments
                .CountAsync(p => p.Status == "Pending" && p.PeriodEnd < now),
        };

        // Revenue chart — monthly totals
        var revenueData = await _db.SubscriptionPayments
            .Where(p => p.Status == "Paid" && p.PaidAt >= chartStart)
            .GroupBy(p => new { p.PaidAt!.Value.Year, p.PaidAt!.Value.Month })
            .Select(g => new { g.Key.Year, g.Key.Month, Total = g.Sum(p => p.Amount) })
            .ToListAsync();

        for (var m = chartStart; m <= monthStart; m = m.AddMonths(1))
        {
            var match = revenueData.FirstOrDefault(r => r.Year == m.Year && r.Month == m.Month);
            data.RevenueChart.Add(new ChartDataPoint
            {
                Label = m.ToString("MMM"),
                Value = match?.Total ?? 0
            });
        }

        // Shops growth — cumulative count each month
        var shopDates = await _db.Shops
            .Where(s => s.CreatedAt >= chartStart)
            .Select(s => s.CreatedAt)
            .ToListAsync();
        var totalBefore = await _db.Shops.CountAsync(s => s.CreatedAt < chartStart);
        var cumulative = totalBefore;
        for (var m = chartStart; m <= monthStart; m = m.AddMonths(1))
        {
            cumulative += shopDates.Count(d => d.Year == m.Year && d.Month == m.Month);
            data.ShopsGrowthChart.Add(new ChartDataPoint
            {
                Label = m.ToString("MMM"),
                Value = cumulative
            });
        }

        // Subscription distribution
        var planDist = await _db.Subscriptions
            .Where(s => s.Status == "Active")
            .GroupBy(s => s.Plan!.PlanName)
            .Select(g => new ChartDataPoint { Label = g.Key, Value = g.Count() })
            .ToListAsync();
        data.SubscriptionDistribution = planDist;

        // Recent activity from audit log
        var recentLogs = await _db.SuperAdminAuditLogs
            .Include(l => l.User)
            .OrderByDescending(l => l.Timestamp)
            .Take(8)
            .ToListAsync();

        data.RecentActivity = recentLogs.Select(l => new RecentActivityItem
        {
            Title = l.Action,
            Description = l.Details ?? "",
            Icon = GetAuditIcon(l.Action),
            IconColor = GetAuditColor(l.Action),
            TimeAgo = GetTimeAgo(l.Timestamp)
        }).ToList();

        // If no audit logs yet, generate from recent data
        if (!data.RecentActivity.Any())
        {
            var recentShops = await _db.Shops
                .OrderByDescending(s => s.CreatedAt)
                .Take(3)
                .Select(s => new { s.ShopName, s.CreatedAt })
                .ToListAsync();
            var recentUsers = await _db.Users
                .Include(u => u.Shop)
                .Include(u => u.UserRoles).ThenInclude(ur => ur.Role)
                .OrderByDescending(u => u.CreatedAt)
                .Take(3)
                .ToListAsync();

            foreach (var s in recentShops)
            {
                data.RecentActivity.Add(new RecentActivityItem
                {
                    Title = "Shop Registered",
                    Description = $"{s.ShopName} joined the platform",
                    Icon = "store",
                    IconColor = "success",
                    TimeAgo = GetTimeAgo(s.CreatedAt)
                });
            }
            foreach (var u in recentUsers)
            {
                var roleName = u.UserRoles.FirstOrDefault()?.Role?.RoleName ?? "User";
                data.RecentActivity.Add(new RecentActivityItem
                {
                    Title = "User Created",
                    Description = $"{u.FullName} ({roleName}) at {u.Shop?.ShopName ?? "System"}",
                    Icon = "user",
                    IconColor = "primary",
                    TimeAgo = GetTimeAgo(u.CreatedAt)
                });
            }
            data.RecentActivity = data.RecentActivity.OrderByDescending(a => a.TimeAgo).Take(8).ToList();
        }

        return data;
    }

    // ═══════════════════════════════════════════════════════════════
    //  SHOPS
    // ═══════════════════════════════════════════════════════════════
    public async Task<ShopListViewModel> GetShopsAsync(string? search, string? status, string? plan, int page, int pageSize = 10)
    {
        var query = _db.Shops
            .Include(s => s.Users)
            .Include(s => s.Subscriptions).ThenInclude(sub => sub.Plan)
            .AsQueryable();

        if (!string.IsNullOrWhiteSpace(search))
        {
            var q = search.ToLower();
            query = query.Where(s =>
                s.ShopName.ToLower().Contains(q) ||
                (s.Email != null && s.Email.ToLower().Contains(q)) ||
                s.ShopCode.ToLower().Contains(q));
        }

        if (!string.IsNullOrEmpty(status))
            query = query.Where(s => s.Status == status);

        if (!string.IsNullOrEmpty(plan))
            query = query.Where(s => s.Subscriptions.Any(sub => sub.Status == "Active" && sub.Plan!.PlanName == plan));

        var totalCount = await query.CountAsync();
        var activeCount = await _db.Shops.CountAsync(s => s.Status == "Active");
        var suspendedCount = await _db.Shops.CountAsync(s => s.Status == "Suspended");
        var monthStart = new DateTime(DateTime.UtcNow.Year, DateTime.UtcNow.Month, 1);
        var newThisMonth = await _db.Shops.CountAsync(s => s.CreatedAt >= monthStart);

        var shops = await query
            .OrderByDescending(s => s.CreatedAt)
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .ToListAsync();

        return new ShopListViewModel
        {
            SearchTerm = search,
            StatusFilter = status,
            CurrentPage = page,
            TotalCount = totalCount,
            ActiveCount = activeCount,
            SuspendedCount = suspendedCount,
            NewThisMonth = newThisMonth,
            Shops = shops.Select(s =>
            {
                var admin = s.Users.FirstOrDefault(); // first user is typically admin
                var activeSub = s.Subscriptions.FirstOrDefault(sub => sub.Status == "Active");
                return new ShopItemViewModel
                {
                    Id = s.ShopId,
                    Name = s.ShopName,
                    Initials = GetInitials(s.ShopName),
                    Owner = admin?.FullName ?? "—",
                    Email = s.Email ?? "—",
                    Phone = s.Phone,
                    UserCount = s.Users.Count,
                    JobOrderCount = 0, // will be loaded separately if needed
                    Status = s.Status,
                    PlanName = activeSub?.Plan?.PlanName ?? "No Plan",
                    BillingCycle = activeSub?.BillingCycle ?? "—",
                    IsDefault = s.IsDefault,
                    CreatedAt = s.CreatedAt
                };
            }).ToList()
        };
    }

    public async Task<ShopFormViewModel?> GetShopForEditAsync(long id)
    {
        var shop = await _db.Shops
            .Include(s => s.Users)
            .FirstOrDefaultAsync(s => s.ShopId == id);

        if (shop == null) return null;
        var admin = shop.Users.FirstOrDefault();

        return new ShopFormViewModel
        {
            Id = shop.ShopId,
            Name = shop.ShopName,
            Owner = admin?.FullName ?? "",
            Email = shop.Email ?? "",
            Phone = shop.Phone,
            Address = shop.Address,
            Status = shop.Status,
            Notes = null
        };
    }

    public async Task<ShopDetailViewModel?> GetShopDetailsAsync(long id)
    {
        var shop = await _db.Shops
            .Include(s => s.Subscriptions).ThenInclude(sub => sub.Plan)
            .Include(s => s.SubscriptionPayments)
            .FirstOrDefaultAsync(s => s.ShopId == id);

        if (shop == null) return null;

        // Find the admin (owner) user for this shop
        var admin = await _db.Users
            .Where(u => u.ShopId == id && u.UserRoles.Any(ur => ur.Role!.RoleName == "Admin"))
            .FirstOrDefaultAsync();

        var activeSub = shop.Subscriptions.FirstOrDefault(s => s.Status == "Active");

        // Get last active date (most recent login of any user in this shop)
        var lastActive = await _db.Users
            .Where(u => u.ShopId == id && u.LastLoginAt != null)
            .OrderByDescending(u => u.LastLoginAt)
            .Select(u => u.LastLoginAt)
            .FirstOrDefaultAsync();

        // Calculate revenue from subscription payments for this shop
        var totalRevenue = shop.SubscriptionPayments
            .Where(p => p.Status == "Paid")
            .Sum(p => p.Amount);

        return new ShopDetailViewModel
        {
            Id = shop.ShopId,
            Name = shop.ShopName,
            Initials = GetInitials(shop.ShopName),
            Owner = admin?.FullName ?? "—",
            Email = shop.Email ?? "—",
            Phone = shop.Phone,
            Address = shop.Address,
            Status = shop.Status,
            CreatedAt = shop.CreatedAt,
            IsDefault = shop.IsDefault,
            PlanName = activeSub?.Plan?.PlanName ?? "No Plan",
            BillingCycle = activeSub?.BillingCycle ?? "—",
            TotalRevenue = totalRevenue,
            LastActiveAt = lastActive
        };
    }

    public async Task<(bool Success, string Message, long ShopId)> CreateShopAsync(ShopCreateViewModel model, long createdByUserId, string? ipAddress)
    {
        // Generate unique shop code
        var shopCode = GenerateShopCode(model.Name);
        var existing = await _db.Shops.AnyAsync(s => s.ShopCode == shopCode);
        if (existing)
            shopCode = shopCode + DateTime.UtcNow.Ticks.ToString()[^4..];

        var shop = new Shop
        {
            ShopCode = shopCode,
            ShopName = model.Name,
            Email = model.Email,
            Phone = model.Phone,
            Address = model.Address,
            Status = "Active",
            IsDefault = false,
            CreatedAt = DateTime.UtcNow
        };

        _db.Shops.Add(shop);
        await _db.SaveChangesAsync();

        // Create admin user for the shop
        var adminUser = new User
        {
            ShopId = shop.ShopId,
            FirstName = model.AdminFirstName,
            MiddleName = model.AdminMiddleName,
            LastName = model.AdminLastName,
            UserName = model.AdminEmail.Split('@')[0],
            PasswordHash = BCrypt.Net.BCrypt.HashPassword(model.AdminPassword),
            Email = model.AdminEmail,
            Phone = model.AdminPhone,
            IsActive = true,
            CreatedAt = DateTime.UtcNow
        };

        _db.Users.Add(adminUser);
        await _db.SaveChangesAsync();

        // Assign Admin role
        var adminRole = await _db.Roles.FirstOrDefaultAsync(r => r.RoleName == "Admin");
        if (adminRole != null)
        {
            _db.UserRoles.Add(new UserRoleAssignment
            {
                UserId = adminUser.UserId,
                RoleId = adminRole.RoleId,
                AssignedAt = DateTime.UtcNow
            });
            await _db.SaveChangesAsync();
        }

        await LogActionAsync(createdByUserId, "ShopCreated", "Shop", shop.ShopId,
            $"Created shop '{shop.ShopName}' with admin user '{adminUser.FullName}'", ipAddress);

        return (true, $"Shop '{shop.ShopName}' created with admin user '{adminUser.FullName}'.", shop.ShopId);
    }

    public async Task<(bool Success, string Message)> UpdateShopAsync(ShopFormViewModel model, long updatedByUserId, string? ipAddress)
    {
        var shop = await _db.Shops.FindAsync(model.Id);
        if (shop == null) return (false, "Shop not found.");
        if (shop.IsDefault && model.Status != "Active")
            return (false, "The default system shop cannot be suspended.");

        shop.ShopName = model.Name;
        shop.Email = model.Email;
        shop.Phone = model.Phone;
        shop.Address = model.Address;
        shop.Status = model.Status;
        shop.UpdatedAt = DateTime.UtcNow;

        // Update the Admin (owner) user's name if Owner field was changed
        if (!string.IsNullOrWhiteSpace(model.Owner))
        {
            var admin = await _db.Users
                .Where(u => u.ShopId == model.Id && u.UserRoles.Any(ur => ur.Role!.RoleName == "Admin"))
                .FirstOrDefaultAsync();

            if (admin != null)
            {
                var nameParts = model.Owner.Trim().Split(' ', 2, StringSplitOptions.RemoveEmptyEntries);
                admin.FirstName = nameParts[0];
                admin.LastName = nameParts.Length > 1 ? nameParts[1] : admin.LastName;
                admin.UpdatedAt = DateTime.UtcNow;
            }
        }

        await _db.SaveChangesAsync();
        await LogActionAsync(updatedByUserId, "ShopUpdated", "Shop", shop.ShopId,
            $"Updated shop '{shop.ShopName}'", ipAddress);

        return (true, "Shop updated successfully.");
    }

    public async Task<(bool Success, string Message)> ToggleShopStatusAsync(long shopId, long userId, string? ipAddress)
    {
        var shop = await _db.Shops.FindAsync(shopId);
        if (shop == null) return (false, "Shop not found.");
        if (shop.IsDefault) return (false, "The default system shop cannot be suspended.");

        shop.Status = shop.Status == "Active" ? "Suspended" : "Active";
        shop.UpdatedAt = DateTime.UtcNow;
        await _db.SaveChangesAsync();

        await LogActionAsync(userId, shop.Status == "Active" ? "ShopActivated" : "ShopSuspended",
            "Shop", shopId, $"Shop '{shop.ShopName}' status changed to {shop.Status}", ipAddress);

        return (true, $"Shop status changed to {shop.Status}.");
    }

    public async Task<(bool Success, string Message)> DeleteShopAsync(long shopId, long userId, string? ipAddress)
    {
        var shop = await _db.Shops.FindAsync(shopId);
        if (shop == null) return (false, "Shop not found.");
        if (shop.IsDefault) return (false, "The default system shop cannot be deleted.");

        // Check for active data
        var hasJobs = await _db.JobOrders.AnyAsync(j => j.ShopId == shopId);
        if (hasJobs) return (false, "Cannot delete shop with existing job orders. Suspend it instead.");

        var shopName = shop.ShopName;
        _db.Shops.Remove(shop);
        await _db.SaveChangesAsync();

        await LogActionAsync(userId, "ShopDeleted", "Shop", shopId,
            $"Deleted shop '{shopName}'", ipAddress);

        return (true, "Shop deleted successfully.");
    }

    // ═══════════════════════════════════════════════════════════════
    //  USERS
    // ═══════════════════════════════════════════════════════════════
    public async Task<GlobalUserListViewModel> GetUsersAsync(string? search, UserRole? role, string? shop, string? status, int page, int pageSize = 10)
    {
        // Exclude SuperAdmin users from the list — SuperAdmin is the system owner, not a shop user
        var query = _db.Users
            .Include(u => u.Shop)
            .Include(u => u.UserRoles).ThenInclude(ur => ur.Role)
            .Where(u => !u.UserRoles.Any(ur => ur.Role!.RoleName == "SuperAdmin"))
            .AsQueryable();

        if (!string.IsNullOrWhiteSpace(search))
        {
            var q = search.ToLower();
            query = query.Where(u =>
                (u.FirstName + " " + u.LastName).ToLower().Contains(q) ||
                (u.Email != null && u.Email.ToLower().Contains(q)) ||
                u.UserName.ToLower().Contains(q));
        }

        if (role.HasValue)
        {
            var roleName = role.Value.ToString();
            query = query.Where(u => u.UserRoles.Any(ur => ur.Role!.RoleName == roleName));
        }

        if (!string.IsNullOrEmpty(shop))
            query = query.Where(u => u.Shop!.ShopName == shop);

        if (!string.IsNullOrEmpty(status))
            query = query.Where(u => (status == "Active") == u.IsActive);

        var totalCount = await query.CountAsync();
        var nonSuperAdminUsers = _db.Users.Where(u => !u.UserRoles.Any(ur => ur.Role!.RoleName == "SuperAdmin"));
        var activeCount = await nonSuperAdminUsers.CountAsync(u => u.IsActive);
        var adminCount = await nonSuperAdminUsers.CountAsync(u => u.UserRoles.Any(ur => ur.Role!.RoleName == "Admin"));
        var superAdminCount = 0; // SuperAdmin is hidden from this page

        var users = await query
            .OrderByDescending(u => u.CreatedAt)
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .ToListAsync();

        var shopNames = await _db.Shops.Select(s => s.ShopName).Distinct().OrderBy(s => s).ToListAsync();

        return new GlobalUserListViewModel
        {
            SearchTerm = search,
            RoleFilter = role,
            ShopFilter = shop,
            StatusFilter = status,
            CurrentPage = page,
            TotalCount = totalCount,
            ActiveCount = activeCount,
            AdminCount = adminCount,
            SuperAdminCount = superAdminCount,
            ShopCount = shopNames.Count,
            Users = users.Select(u =>
            {
                var userRole = u.UserRoles.FirstOrDefault()?.Role;
                var roleEnum = Enum.TryParse<UserRole>(userRole?.RoleName, out var r) ? r : UserRole.Billing;
                return new GlobalUserItemViewModel
                {
                    Id = u.UserId,
                    FullName = u.FullName,
                    Initials = u.Initials,
                    Email = u.Email ?? "—",
                    Phone = u.Phone,
                    ShopName = u.Shop?.ShopName ?? "System",
                    Role = roleEnum,
                    RoleName = userRole?.RoleName ?? "—",
                    IsActive = u.IsActive,
                    LastLoginAt = u.LastLoginAt,
                    CreatedAt = u.CreatedAt
                };
            }).ToList(),
            AvailableShops = shopNames
        };
    }

    public async Task<GlobalUserFormViewModel?> GetUserForEditAsync(long id)
    {
        var user = await _db.Users
            .Include(u => u.UserRoles).ThenInclude(ur => ur.Role)
            .FirstOrDefaultAsync(u => u.UserId == id);

        if (user == null) return null;
        var userRole = user.UserRoles.FirstOrDefault()?.Role;
        var roleEnum = Enum.TryParse<UserRole>(userRole?.RoleName, out var r) ? r : UserRole.Billing;

        return new GlobalUserFormViewModel
        {
            Id = user.UserId,
            FirstName = user.FirstName,
            MiddleName = user.MiddleName,
            LastName = user.LastName,
            Email = user.Email ?? "",
            Phone = user.Phone,
            ShopId = user.ShopId,
            Role = roleEnum,
            IsActive = user.IsActive,
            AvailableShops = await GetShopDropdownAsync()
        };
    }

    public async Task<GlobalUserDetailViewModel?> GetUserDetailsAsync(long id)
    {
        var user = await _db.Users
            .Include(u => u.Shop)
            .Include(u => u.UserRoles).ThenInclude(ur => ur.Role)
            .FirstOrDefaultAsync(u => u.UserId == id);

        if (user == null) return null;
        var userRole = user.UserRoles.FirstOrDefault()?.Role;
        var roleEnum = Enum.TryParse<UserRole>(userRole?.RoleName, out var r) ? r : UserRole.Billing;

        // Get recent audit log entries for this user
        var recentAudit = await _db.AuditLogs
            .Where(a => a.UserId == id)
            .OrderByDescending(a => a.CreatedAt)
            .Take(5)
            .ToListAsync();

        return new GlobalUserDetailViewModel
        {
            Id = user.UserId,
            FullName = user.FullName,
            Initials = user.Initials,
            Email = user.Email ?? "—",
            Phone = user.Phone,
            ShopName = user.Shop?.ShopName ?? "System",
            Role = roleEnum,
            RoleName = userRole?.RoleName ?? "—",
            IsActive = user.IsActive,
            CreatedAt = user.CreatedAt,
            LastLoginAt = user.LastLoginAt,
            LastIpAddress = user.LastIpAddress,
            JobOrdersHandled = 0,
            PaymentsProcessed = 0,
            RecentActivity = recentAudit.Select(a => new UserActivityLogItem
            {
                Action = a.Action,
                Description = a.Details ?? "",
                Timestamp = a.CreatedAt
            }).ToList()
        };
    }

    public async Task<(bool Success, string Message)> CreateUserAsync(GlobalUserFormViewModel model, long createdByUserId, string? ipAddress)
    {
        // Prevent creating another SuperAdmin
        if (model.Role == UserRole.SuperAdmin)
            return (false, "Cannot create a SuperAdmin user. SuperAdmin is the system owner.");

        // Check duplicate email/username in same shop
        var username = model.Email.Split('@')[0];
        var exists = await _db.Users.AnyAsync(u => u.ShopId == model.ShopId && u.UserName == username);
        if (exists)
            return (false, "A user with this username already exists in that shop.");

        var user = new User
        {
            ShopId = model.ShopId,
            FirstName = model.FirstName,
            MiddleName = model.MiddleName,
            LastName = model.LastName,
            UserName = username,
            PasswordHash = BCrypt.Net.BCrypt.HashPassword(model.Password!),
            Email = model.Email,
            Phone = model.Phone,
            IsActive = model.IsActive,
            CreatedAt = DateTime.UtcNow
        };

        _db.Users.Add(user);
        await _db.SaveChangesAsync();

        var role = await _db.Roles.FirstOrDefaultAsync(r => r.RoleName == model.Role.ToString());
        if (role != null)
        {
            _db.UserRoles.Add(new UserRoleAssignment
            {
                UserId = user.UserId,
                RoleId = role.RoleId,
                AssignedAt = DateTime.UtcNow
            });
            await _db.SaveChangesAsync();
        }

        await LogActionAsync(createdByUserId, "UserCreated", "User", user.UserId,
            $"Created user '{user.FullName}' as {model.Role} in shop #{model.ShopId}", ipAddress);

        return (true, $"User '{user.FullName}' created successfully.");
    }

    public async Task<(bool Success, string Message)> UpdateUserAsync(GlobalUserFormViewModel model, long updatedByUserId, string? ipAddress)
    {
        var user = await _db.Users
            .Include(u => u.UserRoles).ThenInclude(ur => ur.Role)
            .FirstOrDefaultAsync(u => u.UserId == model.Id);

        if (user == null) return (false, "User not found.");

        // Prevent changing any user to SuperAdmin role
        if (model.Role == UserRole.SuperAdmin)
            return (false, "Cannot assign SuperAdmin role.");

        // Prevent editing a SuperAdmin user from this page
        if (user.UserRoles.Any(ur => ur.Role != null && ur.Role.RoleName == "SuperAdmin"))
            return (false, "Cannot edit the SuperAdmin user from here.");

        user.FirstName = model.FirstName;
        user.MiddleName = model.MiddleName;
        user.LastName = model.LastName;
        user.Email = model.Email;
        user.Phone = model.Phone;
        user.ShopId = model.ShopId;
        user.IsActive = model.IsActive;
        user.UpdatedAt = DateTime.UtcNow;

        // Update password if provided
        if (!string.IsNullOrEmpty(model.Password))
            user.PasswordHash = BCrypt.Net.BCrypt.HashPassword(model.Password);

        // Update role
        var newRole = await _db.Roles.FirstOrDefaultAsync(r => r.RoleName == model.Role.ToString());
        if (newRole != null)
        {
            var existingRoles = user.UserRoles.ToList();
            _db.UserRoles.RemoveRange(existingRoles);
            _db.UserRoles.Add(new UserRoleAssignment
            {
                UserId = user.UserId,
                RoleId = newRole.RoleId,
                AssignedAt = DateTime.UtcNow
            });
        }

        await _db.SaveChangesAsync();
        await LogActionAsync(updatedByUserId, "UserUpdated", "User", user.UserId,
            $"Updated user '{user.FullName}'", ipAddress);

        return (true, "User updated successfully.");
    }

    public async Task<(bool Success, string Message)> ToggleUserStatusAsync(long userId, long actionUserId, string? ipAddress)
    {
        var user = await _db.Users.FindAsync(userId);
        if (user == null) return (false, "User not found.");

        user.IsActive = !user.IsActive;
        user.UpdatedAt = DateTime.UtcNow;
        await _db.SaveChangesAsync();

        await LogActionAsync(actionUserId, user.IsActive ? "UserActivated" : "UserDeactivated",
            "User", userId, $"User '{user.FullName}' status changed", ipAddress);

        return (true, $"User {(user.IsActive ? "activated" : "deactivated")} successfully.");
    }

    public async Task<(bool Success, string Message)> ResetUserPasswordAsync(long userId, string newPassword, long actionUserId, string? ipAddress)
    {
        var user = await _db.Users.FindAsync(userId);
        if (user == null) return (false, "User not found.");

        user.PasswordHash = BCrypt.Net.BCrypt.HashPassword(newPassword);
        user.UpdatedAt = DateTime.UtcNow;
        await _db.SaveChangesAsync();

        await LogActionAsync(actionUserId, "PasswordReset", "User", userId,
            $"Password reset for user '{user.FullName}'", ipAddress);

        return (true, "Password reset successfully.");
    }

    public async Task<List<ShopDropdownItem>> GetShopDropdownAsync()
    {
        return await _db.Shops
            .Where(s => s.Status == "Active")
            .OrderBy(s => s.ShopName)
            .Select(s => new ShopDropdownItem { Id = s.ShopId, Name = s.ShopName })
            .ToListAsync();
    }

    // ═══════════════════════════════════════════════════════════════
    //  SUBSCRIPTIONS
    // ═══════════════════════════════════════════════════════════════
    public async Task<List<SubscriptionPlan>> GetActivePlansAsync()
    {
        return await _db.SubscriptionPlans
            .Where(p => p.IsActive)
            .OrderBy(p => p.SortOrder)
            .ToListAsync();
    }

    public async Task<SubscriptionListViewModel> GetSubscriptionsAsync(string? search, string? status, string? plan, int page, int pageSize = 10)
    {
        var query = _db.Subscriptions
            .Include(s => s.Shop)
            .Include(s => s.Plan)
            .AsQueryable();

        if (!string.IsNullOrWhiteSpace(search))
        {
            var q = search.ToLower();
            query = query.Where(s => s.Shop!.ShopName.ToLower().Contains(q));
        }

        if (!string.IsNullOrEmpty(status))
            query = query.Where(s => s.Status == status);

        if (!string.IsNullOrEmpty(plan))
            query = query.Where(s => s.Plan!.PlanName == plan);

        var totalCount = await query.CountAsync();
        var activeCount = await _db.Subscriptions.CountAsync(s => s.Status == "Active");
        var expiredCount = await _db.Subscriptions.CountAsync(s => s.Status == "Expired");
        var totalMRR = await _db.Subscriptions
            .Where(s => s.Status == "Active" && s.BillingCycle == "Monthly")
            .SumAsync(s => (decimal?)s.Price) ?? 0m;

        var subs = await query
            .OrderByDescending(s => s.CreatedAt)
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .ToListAsync();

        return new SubscriptionListViewModel
        {
            SearchTerm = search,
            StatusFilter = status,
            PlanFilter = plan,
            CurrentPage = page,
            TotalCount = totalCount,
            ActiveCount = activeCount,
            ExpiredCount = expiredCount,
            TotalMRR = totalMRR,
            Subscriptions = subs.Select(s => new SubscriptionItemViewModel
            {
                Id = s.SubscriptionId,
                ShopName = s.Shop?.ShopName ?? "—",
                ShopInitials = GetInitials(s.Shop?.ShopName ?? ""),
                PlanName = s.Plan?.PlanName ?? "—",
                BillingCycle = s.BillingCycle,
                Price = s.Price,
                Status = s.Status,
                StartDate = s.StartDate,
                EndDate = s.EndDate,
                NextBillingDate = s.NextBillingDate,
                IsDefault = s.IsDefault
            }).ToList()
        };
    }

    public async Task<SubscriptionDetailViewModel?> GetSubscriptionDetailsAsync(long id)
    {
        var sub = await _db.Subscriptions
            .Include(s => s.Shop).ThenInclude(s => s!.Users)
            .Include(s => s.Plan)
            .Include(s => s.Payments)
            .FirstOrDefaultAsync(s => s.SubscriptionId == id);

        if (sub == null) return null;

        return new SubscriptionDetailViewModel
        {
            Id = sub.SubscriptionId,
            ShopName = sub.Shop?.ShopName ?? "—",
            ShopInitials = GetInitials(sub.Shop?.ShopName ?? ""),
            PlanName = sub.Plan?.PlanName ?? "—",
            PlanDescription = sub.Plan?.Description ?? "",
            BillingCycle = sub.BillingCycle,
            Price = sub.Price,
            Status = sub.Status,
            StartDate = sub.StartDate,
            EndDate = sub.EndDate,
            NextBillingDate = sub.NextBillingDate,
            IsDefault = sub.IsDefault,
            MaxUsers = sub.Plan?.MaxUsers ?? 0,
            MaxCustomers = sub.Plan?.MaxCustomers ?? 0,
            MaxJobOrdersPerMonth = sub.Plan?.MaxJobOrdersPerMonth ?? 0,
            CurrentUsers = sub.Shop?.Users.Count ?? 0,
            TotalPaid = sub.Payments.Where(p => p.Status == "Paid").Sum(p => p.Amount),
            PaymentHistory = sub.Payments
                .OrderByDescending(p => p.CreatedAt)
                .Take(10)
                .Select(p => new SubscriptionPaymentSummary
                {
                    Amount = p.Amount,
                    Status = p.Status,
                    ReferenceNumber = p.ReferenceNumber,
                    PaidAt = p.PaidAt,
                    PaymentMethod = p.PaymentMethod
                }).ToList()
        };
    }

    public async Task<(bool Success, string Message, string? CheckoutUrl)> AssignSubscriptionAsync(long shopId, long planId, string billingCycle, long userId, string? ipAddress)
    {
        var shop = await _db.Shops.FindAsync(shopId);
        if (shop == null) return (false, "Shop not found.", null);

        var plan = await _db.SubscriptionPlans.FindAsync(planId);
        if (plan == null) return (false, "Plan not found.", null);

        // Cancel existing active subscription
        var existing = await _db.Subscriptions
            .Where(s => s.ShopId == shopId && s.Status == "Active" && !s.IsDefault)
            .ToListAsync();
        foreach (var e in existing)
        {
            e.Status = "Cancelled";
            e.CancelledAt = DateTime.UtcNow;
            e.UpdatedAt = DateTime.UtcNow;
        }

        var price = billingCycle switch
        {
            "Monthly" => plan.MonthlyPrice,
            "Yearly" => plan.YearlyPrice,
            "Permanent" => plan.PermanentPrice,
            _ => plan.MonthlyPrice
        };

        var now = DateTime.UtcNow;
        DateTime? endDate = billingCycle switch
        {
            "Monthly" => now.AddMonths(1),
            "Yearly" => now.AddYears(1),
            "Permanent" => null,
            _ => now.AddMonths(1)
        };

        var subscription = new Subscription
        {
            ShopId = shopId,
            PlanId = planId,
            BillingCycle = billingCycle,
            Status = "Active",
            Price = price,
            StartDate = now,
            EndDate = endDate,
            NextBillingDate = billingCycle == "Permanent" ? null : endDate,
            CreatedAt = now
        };

        _db.Subscriptions.Add(subscription);
        await _db.SaveChangesAsync();

        // Create payment record
        var refNum = $"SUBPAY-{now:yyyyMMdd}-{subscription.SubscriptionId:D4}";
        var payment = new SubscriptionPayment
        {
            SubscriptionId = subscription.SubscriptionId,
            ShopId = shopId,
            Amount = price,
            Status = billingCycle == "Permanent" ? "Pending" : "Pending",
            ReferenceNumber = refNum,
            PeriodStart = now,
            PeriodEnd = endDate ?? now.AddYears(99),
            CreatedAt = now
        };

        _db.SubscriptionPayments.Add(payment);
        await _db.SaveChangesAsync();

        await LogActionAsync(userId, "SubscriptionAssigned", "Subscription", subscription.SubscriptionId,
            $"Assigned {plan.PlanName} ({billingCycle}) to '{shop.ShopName}' at ₱{price:N2}", ipAddress);

        // In production, create PayMongo checkout session here
        return (true, $"Subscription '{plan.PlanName}' assigned to '{shop.ShopName}'. Payment of ₱{price:N2} is pending.", null);
    }

    public async Task<(bool Success, string Message)> CancelSubscriptionAsync(long subscriptionId, long userId, string? ipAddress)
    {
        var sub = await _db.Subscriptions.Include(s => s.Shop).Include(s => s.Plan).FirstOrDefaultAsync(s => s.SubscriptionId == subscriptionId);
        if (sub == null) return (false, "Subscription not found.");
        if (sub.IsDefault) return (false, "Cannot cancel the default system subscription.");

        sub.Status = "Cancelled";
        sub.CancelledAt = DateTime.UtcNow;
        sub.UpdatedAt = DateTime.UtcNow;
        await _db.SaveChangesAsync();

        await LogActionAsync(userId, "SubscriptionCancelled", "Subscription", subscriptionId,
            $"Cancelled {sub.Plan?.PlanName} for '{sub.Shop?.ShopName}'", ipAddress);

        return (true, "Subscription cancelled.");
    }

    // ═══════════════════════════════════════════════════════════════
    //  SUBSCRIPTION PAYMENTS
    // ═══════════════════════════════════════════════════════════════
    public async Task<SubscriptionPaymentListViewModel> GetPaymentsAsync(string? search, string? status, string? method, DateTime? from, DateTime? to, int page, int pageSize = 10)
    {
        var query = _db.SubscriptionPayments
            .Include(p => p.Shop)
            .Include(p => p.Subscription).ThenInclude(s => s!.Plan)
            .AsQueryable();

        if (!string.IsNullOrWhiteSpace(search))
        {
            var q = search.ToLower();
            query = query.Where(p =>
                p.ReferenceNumber.ToLower().Contains(q) ||
                p.Shop!.ShopName.ToLower().Contains(q));
        }

        if (!string.IsNullOrEmpty(status))
            query = query.Where(p => p.Status == status);
        if (!string.IsNullOrEmpty(method))
            query = query.Where(p => p.PaymentMethod == method);
        if (from.HasValue)
            query = query.Where(p => p.CreatedAt >= from.Value);
        if (to.HasValue)
            query = query.Where(p => p.CreatedAt <= to.Value.AddDays(1));

        var totalCount = await query.CountAsync();
        var totalPaid = await _db.SubscriptionPayments
            .Where(p => p.Status == "Paid")
            .SumAsync(p => (decimal?)p.Amount) ?? 0m;
        var pendingCount = await _db.SubscriptionPayments.CountAsync(p => p.Status == "Pending");
        var failedCount = await _db.SubscriptionPayments.CountAsync(p => p.Status == "Failed");

        var payments = await query
            .OrderByDescending(p => p.CreatedAt)
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .ToListAsync();

        return new SubscriptionPaymentListViewModel
        {
            SearchTerm = search,
            StatusFilter = status,
            MethodFilter = method,
            DateFrom = from,
            DateTo = to,
            CurrentPage = page,
            TotalCount = totalCount,
            TotalPaid = totalPaid,
            PendingCount = pendingCount,
            FailedCount = failedCount,
            Payments = payments.Select(p => new SubscriptionPaymentItemViewModel
            {
                Id = p.SubscriptionPaymentId,
                ShopName = p.Shop?.ShopName ?? "—",
                PlanName = p.Subscription?.Plan?.PlanName ?? "—",
                Amount = p.Amount,
                Status = p.Status,
                PaymentMethod = p.PaymentMethod ?? "—",
                ReferenceNumber = p.ReferenceNumber,
                PeriodStart = p.PeriodStart,
                PeriodEnd = p.PeriodEnd,
                PaidAt = p.PaidAt,
                CreatedAt = p.CreatedAt
            }).ToList()
        };
    }

    public async Task<SubscriptionPaymentDetailViewModel?> GetPaymentDetailsAsync(long id)
    {
        var p = await _db.SubscriptionPayments
            .Include(p => p.Shop)
            .Include(p => p.Subscription).ThenInclude(s => s!.Plan)
            .FirstOrDefaultAsync(p => p.SubscriptionPaymentId == id);

        if (p == null) return null;

        return new SubscriptionPaymentDetailViewModel
        {
            Id = p.SubscriptionPaymentId,
            ShopName = p.Shop?.ShopName ?? "—",
            PlanName = p.Subscription?.Plan?.PlanName ?? "—",
            BillingCycle = p.Subscription?.BillingCycle ?? "—",
            Amount = p.Amount,
            Currency = p.Currency,
            Status = p.Status,
            PaymentMethod = p.PaymentMethod ?? "—",
            ReferenceNumber = p.ReferenceNumber,
            PayMongoPaymentId = p.PayMongoPaymentId,
            PeriodStart = p.PeriodStart,
            PeriodEnd = p.PeriodEnd,
            PaidAt = p.PaidAt,
            CreatedAt = p.CreatedAt,
            Notes = p.Notes
        };
    }

    // ═══════════════════════════════════════════════════════════════
    //  ANNOUNCEMENTS
    // ═══════════════════════════════════════════════════════════════
    public async Task<AnnouncementListViewModel> GetAnnouncementsAsync(string? search, string? type, string? status, int page, int pageSize = 10)
    {
        var query = _db.Announcements
            .Include(a => a.CreatedBy)
            .AsQueryable();

        if (!string.IsNullOrWhiteSpace(search))
        {
            var q = search.ToLower();
            query = query.Where(a => a.Title.ToLower().Contains(q) || a.Content.ToLower().Contains(q));
        }

        if (!string.IsNullOrEmpty(type))
            query = query.Where(a => a.Type == type);
        if (!string.IsNullOrEmpty(status))
            query = query.Where(a => a.Status == status);

        var totalCount = await query.CountAsync();
        var publishedCount = await _db.Announcements.CountAsync(a => a.Status == "Published");
        var draftCount = await _db.Announcements.CountAsync(a => a.Status == "Draft");

        var announcements = await query
            .OrderByDescending(a => a.CreatedAt)
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .ToListAsync();

        return new AnnouncementListViewModel
        {
            SearchTerm = search,
            TypeFilter = type,
            StatusFilter = status,
            CurrentPage = page,
            TotalCount = totalCount,
            PublishedCount = publishedCount,
            DraftCount = draftCount,
            Announcements = announcements.Select(a => new AnnouncementItemViewModel
            {
                Id = a.AnnouncementId,
                Title = a.Title,
                Content = a.Content,
                Type = a.Type,
                Status = a.Status,
                CreatedBy = a.CreatedBy?.FullName ?? "—",
                CreatedByName = a.CreatedBy?.FullName ?? "—",
                PublishedAt = a.PublishedAt,
                ExpiresAt = a.ExpiresAt,
                CreatedAt = a.CreatedAt
            }).ToList()
        };
    }

    public async Task<AnnouncementFormViewModel?> GetAnnouncementForEditAsync(long id)
    {
        var a = await _db.Announcements.FindAsync(id);
        if (a == null) return null;

        return new AnnouncementFormViewModel
        {
            Id = a.AnnouncementId,
            Title = a.Title,
            Content = a.Content,
            Type = a.Type,
            ExpiresAt = a.ExpiresAt
        };
    }

    public async Task<(bool Success, string Message)> CreateAnnouncementAsync(AnnouncementFormViewModel model, long userId)
    {
        var announcement = new Announcement
        {
            Title = model.Title,
            Content = model.Content,
            Type = model.Type,
            Status = "Draft",
            ExpiresAt = model.ExpiresAt,
            CreatedByUserId = userId,
            CreatedAt = DateTime.UtcNow
        };

        _db.Announcements.Add(announcement);
        await _db.SaveChangesAsync();
        return (true, "Announcement created as draft.");
    }

    public async Task<(bool Success, string Message)> UpdateAnnouncementAsync(AnnouncementFormViewModel model, long userId)
    {
        var a = await _db.Announcements.FindAsync(model.Id);
        if (a == null) return (false, "Announcement not found.");

        a.Title = model.Title;
        a.Content = model.Content;
        a.Type = model.Type;
        a.ExpiresAt = model.ExpiresAt;
        a.UpdatedAt = DateTime.UtcNow;
        await _db.SaveChangesAsync();

        return (true, "Announcement updated.");
    }

    public async Task<(bool Success, string Message)> PublishAnnouncementAsync(long id, long userId)
    {
        var a = await _db.Announcements.FindAsync(id);
        if (a == null) return (false, "Announcement not found.");

        a.Status = a.Status == "Published" ? "Draft" : "Published";
        a.PublishedAt = a.Status == "Published" ? DateTime.UtcNow : null;
        a.UpdatedAt = DateTime.UtcNow;
        await _db.SaveChangesAsync();

        return (true, a.Status == "Published" ? "Announcement published." : "Announcement unpublished.");
    }

    public async Task<(bool Success, string Message)> DeleteAnnouncementAsync(long id, long userId)
    {
        var a = await _db.Announcements.FindAsync(id);
        if (a == null) return (false, "Announcement not found.");

        _db.Announcements.Remove(a);
        await _db.SaveChangesAsync();
        return (true, "Announcement deleted.");
    }

    // ═══════════════════════════════════════════════════════════════
    //  SETTINGS
    // ═══════════════════════════════════════════════════════════════
    public async Task<Dictionary<string, string>> GetSettingsAsync(string? category = null)
    {
        var query = _db.PlatformSettings.AsQueryable();
        if (!string.IsNullOrEmpty(category))
            query = query.Where(s => s.Category == category);

        return await query.ToDictionaryAsync(s => s.SettingKey, s => s.SettingValue);
    }

    public async Task<(bool Success, string Message)> SaveSettingsAsync(Dictionary<string, string> settings, string category, long userId)
    {
        var existing = await _db.PlatformSettings
            .Where(s => settings.Keys.Contains(s.SettingKey))
            .ToListAsync();

        foreach (var kvp in settings)
        {
            var setting = existing.FirstOrDefault(s => s.SettingKey == kvp.Key);
            if (setting != null)
            {
                setting.SettingValue = kvp.Value;
                setting.UpdatedAt = DateTime.UtcNow;
                setting.UpdatedBy = userId.ToString();
            }
            else
            {
                _db.PlatformSettings.Add(new PlatformSetting
                {
                    SettingKey = kvp.Key,
                    SettingValue = kvp.Value,
                    Category = category,
                    UpdatedAt = DateTime.UtcNow,
                    UpdatedBy = userId.ToString()
                });
            }
        }

        await _db.SaveChangesAsync();
        await LogActionAsync(userId, "SettingsUpdated", "Setting", null,
            $"{category} settings updated ({settings.Count} values)", null);

        return (true, $"{category} settings saved successfully.");
    }

    // ═══════════════════════════════════════════════════════════════
    //  REPORTS
    // ═══════════════════════════════════════════════════════════════
    public async Task<ReportsIndexViewModel> GenerateReportAsync(string report, DateTime from, DateTime to)
    {
        var vm = new ReportsIndexViewModel
        {
            SelectedReport = report,
            DateFrom = from,
            DateTo = to
        };

        switch (report)
        {
            case "revenue":
                await BuildRevenueReport(vm, from, to);
                break;
            case "shops":
                await BuildShopsReport(vm, from, to);
                break;
            case "users":
                await BuildUsersReport(vm, from, to);
                break;
            case "subscriptions":
                await BuildSubscriptionReport(vm, from, to);
                break;
            case "payments":
                await BuildPaymentReport(vm, from, to);
                break;
            case "growth":
                await BuildGrowthReport(vm, from, to);
                break;
        }

        return vm;
    }

    private async Task BuildRevenueReport(ReportsIndexViewModel vm, DateTime from, DateTime to)
    {
        var payments = await _db.SubscriptionPayments
            .Include(p => p.Subscription).ThenInclude(s => s.Shop)
            .Include(p => p.Subscription).ThenInclude(s => s.Plan)
            .Where(p => p.Status == "Paid" && p.PaidAt >= from && p.PaidAt <= to)
            .OrderByDescending(p => p.PaidAt)
            .ToListAsync();

        var total = payments.Sum(p => p.Amount);
        var shopCount = payments.Where(p => p.Subscription != null).Select(p => p.Subscription!.ShopId).Distinct().Count();

        vm.SummaryCards = new()
        {
            new() { Label = "Total Revenue", Value = total.ToString("C"), Color = "#10b981", Icon = "dollar-sign" },
            new() { Label = "Transactions", Value = payments.Count.ToString(), Color = "#6366f1", Icon = "activity" },
            new() { Label = "Paying Shops", Value = shopCount.ToString(), Color = "#3b82f6", Icon = "store" },
            new() { Label = "Avg / Shop", Value = (shopCount > 0 ? total / shopCount : 0).ToString("C"), Color = "#f59e0b", Icon = "trending-up" }
        };

        // Monthly chart
        vm.ChartData = payments
            .Where(p => p.PaidAt.HasValue)
            .GroupBy(p => new { p.PaidAt!.Value.Year, p.PaidAt!.Value.Month })
            .Select(g => new ChartDataPoint { Label = $"{g.Key.Year}-{g.Key.Month:D2}", Value = g.Sum(x => x.Amount) })
            .OrderBy(c => c.Label)
            .ToList();

        vm.TableRows = payments.Select(p => new ReportTableRow
        {
            Cells = new[] { p.ReferenceNumber, p.Subscription?.Shop?.ShopName ?? "—", p.Subscription?.Plan?.PlanName ?? "—", p.Amount.ToString("C"), p.PaidAt?.ToString("MMM dd, yyyy") ?? "—" }
        }).ToList();
    }

    private async Task BuildShopsReport(ReportsIndexViewModel vm, DateTime from, DateTime to)
    {
        var shops = await _db.Shops.Include(s => s.Users)
            .Where(s => s.CreatedAt >= from && s.CreatedAt <= to)
            .OrderByDescending(s => s.CreatedAt)
            .ToListAsync();

        var allShops = await _db.Shops.CountAsync();
        var activeShops = await _db.Shops.CountAsync(s => s.Status == "Active");

        vm.SummaryCards = new()
        {
            new() { Label = "New Shops", Value = shops.Count.ToString(), Color = "#6366f1", Icon = "store" },
            new() { Label = "Total Shops", Value = allShops.ToString(), Color = "#3b82f6", Icon = "database" },
            new() { Label = "Active", Value = activeShops.ToString(), Color = "#10b981", Icon = "check-circle" },
            new() { Label = "Inactive", Value = (allShops - activeShops).ToString(), Color = "#ef4444", Icon = "x-circle" }
        };

        vm.ChartData = shops
            .GroupBy(s => s.CreatedAt.ToString("yyyy-MM"))
            .Select(g => new ChartDataPoint { Label = g.Key, Value = g.Count() })
            .OrderBy(c => c.Label)
            .ToList();

        vm.TableRows = shops.Select(s => new ReportTableRow
        {
            Cells = new[] { s.ShopName, s.Status, s.Users.Count.ToString(), s.CreatedAt.ToString("MMM dd, yyyy") }
        }).ToList();
    }

    private async Task BuildUsersReport(ReportsIndexViewModel vm, DateTime from, DateTime to)
    {
        var users = await _db.Users.Include(u => u.UserRoles).ThenInclude(ur => ur.Role).Include(u => u.Shop)
            .Where(u => u.CreatedAt >= from && u.CreatedAt <= to)
            .OrderByDescending(u => u.CreatedAt)
            .ToListAsync();

        var totalUsers = await _db.Users.CountAsync();
        var activeUsers = await _db.Users.CountAsync(u => u.IsActive);
        var roleBreakdown = await _db.UserRoles.Include(ur => ur.Role)
            .GroupBy(ur => ur.Role.RoleName)
            .Select(g => new { Role = g.Key, Count = g.Count() })
            .ToListAsync();

        vm.SummaryCards = new()
        {
            new() { Label = "New Users", Value = users.Count.ToString(), Color = "#6366f1", Icon = "users" },
            new() { Label = "Total Users", Value = totalUsers.ToString(), Color = "#3b82f6", Icon = "database" },
            new() { Label = "Active", Value = activeUsers.ToString(), Color = "#10b981", Icon = "check-circle" },
            new() { Label = "Roles", Value = roleBreakdown.Count.ToString(), Color = "#f59e0b", Icon = "shield" }
        };

        vm.ChartData = roleBreakdown.Select(r => new ChartDataPoint { Label = r.Role ?? "Unknown", Value = r.Count }).ToList();

        vm.TableRows = users.Select(u => new ReportTableRow
        {
            Cells = new[] { u.FullName, u.UserRoles.FirstOrDefault()?.Role?.RoleName ?? "—", u.Shop?.ShopName ?? "—", u.IsActive ? "Active" : "Inactive", u.CreatedAt.ToString("MMM dd, yyyy") }
        }).ToList();
    }

    private async Task BuildSubscriptionReport(ReportsIndexViewModel vm, DateTime from, DateTime to)
    {
        var subs = await _db.Subscriptions
            .Include(s => s.Shop).Include(s => s.Plan)
            .Where(s => s.StartDate >= from && s.StartDate <= to)
            .OrderByDescending(s => s.StartDate)
            .ToListAsync();

        var activeSubs = await _db.Subscriptions.CountAsync(s => s.Status == "Active");
        var totalMRR = await _db.Subscriptions.Where(s => s.Status == "Active").SumAsync(s => s.Price);

        vm.SummaryCards = new()
        {
            new() { Label = "New Subscriptions", Value = subs.Count.ToString(), Color = "#6366f1", Icon = "credit-card" },
            new() { Label = "Active", Value = activeSubs.ToString(), Color = "#10b981", Icon = "check-circle" },
            new() { Label = "MRR", Value = totalMRR.ToString("C"), Color = "#3b82f6", Icon = "trending-up" },
            new() { Label = "Avg Price", Value = (subs.Count > 0 ? subs.Average(s => s.Price) : 0).ToString("C"), Color = "#f59e0b", Icon = "dollar-sign" }
        };

        vm.ChartData = subs
            .GroupBy(s => s.Plan?.PlanName ?? "Unknown")
            .Select(g => new ChartDataPoint { Label = g.Key, Value = g.Count() })
            .ToList();

        vm.TableRows = subs.Select(s => new ReportTableRow
        {
            Cells = new[] { s.Shop?.ShopName ?? "—", s.Plan?.PlanName ?? "—", s.BillingCycle, s.Price.ToString("C"), s.Status, s.StartDate.ToString("MMM dd, yyyy") }
        }).ToList();
    }

    private async Task BuildPaymentReport(ReportsIndexViewModel vm, DateTime from, DateTime to)
    {
        var payments = await _db.SubscriptionPayments
            .Include(p => p.Subscription).ThenInclude(s => s.Shop)
            .Where(p => p.CreatedAt >= from && p.CreatedAt <= to)
            .OrderByDescending(p => p.CreatedAt)
            .ToListAsync();

        var paid = payments.Where(p => p.Status == "Paid").Sum(p => p.Amount);
        var pending = payments.Where(p => p.Status == "Pending").Sum(p => p.Amount);
        var failed = payments.Count(p => p.Status == "Failed");

        vm.SummaryCards = new()
        {
            new() { Label = "Total Collected", Value = paid.ToString("C"), Color = "#10b981", Icon = "check-circle" },
            new() { Label = "Pending", Value = pending.ToString("C"), Color = "#f59e0b", Icon = "clock" },
            new() { Label = "Failed", Value = failed.ToString(), Color = "#ef4444", Icon = "x-circle" },
            new() { Label = "Transactions", Value = payments.Count.ToString(), Color = "#6366f1", Icon = "activity" }
        };

        vm.ChartData = payments.Where(p => p.Status == "Paid")
            .GroupBy(p => (p.PaidAt ?? p.CreatedAt).ToString("yyyy-MM"))
            .Select(g => new ChartDataPoint { Label = g.Key, Value = g.Sum(x => x.Amount) })
            .OrderBy(c => c.Label)
            .ToList();

        vm.TableRows = payments.Select(p => new ReportTableRow
        {
            Cells = new[] { p.ReferenceNumber, p.Subscription?.Shop?.ShopName ?? "—", p.Amount.ToString("C"), p.Status, p.PaymentMethod ?? "—", (p.PaidAt ?? p.CreatedAt).ToString("MMM dd, yyyy") }
        }).ToList();
    }

    private async Task BuildGrowthReport(ReportsIndexViewModel vm, DateTime from, DateTime to)
    {
        var months = new List<DateTime>();
        var cursor = new DateTime(from.Year, from.Month, 1);
        var endMonth = new DateTime(to.Year, to.Month, 1);
        while (cursor <= endMonth)
        {
            months.Add(cursor);
            cursor = cursor.AddMonths(1);
        }

        var shopCounts = new List<ChartDataPoint>();
        foreach (var m in months)
        {
            var nextMonth = m.AddMonths(1);
            var count = await _db.Shops.CountAsync(s => s.CreatedAt < nextMonth);
            shopCounts.Add(new ChartDataPoint { Label = m.ToString("yyyy-MM"), Value = count });
        }

        var currentShops = await _db.Shops.CountAsync();
        var currentUsers = await _db.Users.CountAsync();
        var currentSubs = await _db.Subscriptions.CountAsync(s => s.Status == "Active");
        var totalRevenue = await _db.SubscriptionPayments.Where(p => p.Status == "Paid").SumAsync(p => p.Amount);

        vm.SummaryCards = new()
        {
            new() { Label = "Total Shops", Value = currentShops.ToString(), Color = "#6366f1", Icon = "store" },
            new() { Label = "Total Users", Value = currentUsers.ToString(), Color = "#3b82f6", Icon = "users" },
            new() { Label = "Active Subs", Value = currentSubs.ToString(), Color = "#10b981", Icon = "trending-up" },
            new() { Label = "Lifetime Revenue", Value = totalRevenue.ToString("C"), Color = "#f59e0b", Icon = "dollar-sign" }
        };

        vm.ChartData = shopCounts;

        // Growth table showing month-over-month
        vm.TableRows = new();
        for (int i = 0; i < shopCounts.Count; i++)
        {
            var prev = i > 0 ? shopCounts[i - 1].Value : shopCounts[i].Value;
            var change = prev > 0 ? ((shopCounts[i].Value - prev) / prev * 100).ToString("F1") + "%" : "—";
            vm.TableRows.Add(new ReportTableRow
            {
                Cells = new[] { shopCounts[i].Label, shopCounts[i].Value.ToString(), change }
            });
        }
    }

    public async Task<byte[]> ExportReportCsvAsync(string report, DateTime from, DateTime to)
    {
        var data = await GenerateReportAsync(report, from, to);
        var sb = new System.Text.StringBuilder();

        // Header
        var headers = report switch
        {
            "revenue" => new[] { "Reference", "Shop", "Plan", "Amount", "Paid Date" },
            "shops" => new[] { "Shop", "Status", "Users", "Created" },
            "users" => new[] { "Name", "Role", "Shop", "Status", "Created" },
            "subscriptions" => new[] { "Shop", "Plan", "Cycle", "Price", "Status", "Start Date" },
            "payments" => new[] { "Reference", "Shop", "Amount", "Status", "Method", "Date" },
            "growth" => new[] { "Month", "Cumulative Shops", "Change" },
            _ => new[] { "Data" }
        };

        sb.AppendLine(string.Join(",", headers));

        foreach (var row in data.TableRows)
        {
            sb.AppendLine(string.Join(",", row.Cells.Select(c => $"\"{c?.Replace("\"", "\"\"") ?? ""}\"" )));
        }

        return System.Text.Encoding.UTF8.GetBytes(sb.ToString());
    }

    // ═══════════════════════════════════════════════════════════════
    //  AUDIT LOG
    // ═══════════════════════════════════════════════════════════════
    public async Task LogActionAsync(long userId, string action, string? entityType, long? entityId, string? details, string? ipAddress)
    {
        _db.SuperAdminAuditLogs.Add(new SuperAdminAuditLog
        {
            UserId = userId,
            Action = action,
            EntityType = entityType,
            EntityId = entityId,
            Details = details,
            IpAddress = ipAddress,
            Timestamp = DateTime.UtcNow
        });
        await _db.SaveChangesAsync();
    }

    // ═══════════════════════════════════════════════════════════════
    //  HELPERS
    // ═══════════════════════════════════════════════════════════════
    private static string GetInitials(string name)
    {
        if (string.IsNullOrWhiteSpace(name)) return "??";
        var parts = name.Split(' ', StringSplitOptions.RemoveEmptyEntries);
        return parts.Length >= 2
            ? $"{parts[0][0]}{parts[^1][0]}".ToUpper()
            : name[..Math.Min(2, name.Length)].ToUpper();
    }

    private static string GenerateShopCode(string shopName)
    {
        var parts = shopName.Split(' ', StringSplitOptions.RemoveEmptyEntries);
        var code = string.Join("", parts.Select(p => p[0])).ToUpper();
        return code.Length >= 2 ? code[..Math.Min(6, code.Length)] : code.PadRight(2, 'X');
    }

    private static string GetRoleBadgeClass(string? roleName) => roleName switch
    {
        "SuperAdmin" => "status-purple",
        "Admin" => "badge-primary",
        "Billing" => "badge-success",
        "Technician" => "badge-info",
        "Auditor" => "badge-warning",
        _ => "status-muted"
    };

    private static string GetAuditIcon(string action) => action switch
    {
        "ShopCreated" => "store",
        "ShopSuspended" or "ShopActivated" => "alert",
        "UserCreated" => "user",
        "SubscriptionAssigned" => "credit-card",
        "SettingsUpdated" => "settings",
        _ => "activity"
    };

    private static string GetAuditColor(string action) => action switch
    {
        "ShopCreated" or "UserCreated" or "ShopActivated" => "success",
        "ShopSuspended" or "SubscriptionCancelled" => "warning",
        "ShopDeleted" or "UserDeactivated" => "danger",
        _ => "primary"
    };

    private static string GetTimeAgo(DateTime timestamp)
    {
        var diff = DateTime.UtcNow - timestamp;
        if (diff.TotalMinutes < 1) return "just now";
        if (diff.TotalMinutes < 60) return $"{(int)diff.TotalMinutes} min ago";
        if (diff.TotalHours < 24) return $"{(int)diff.TotalHours} hr ago";
        if (diff.TotalDays < 7) return $"{(int)diff.TotalDays} day(s) ago";
        return timestamp.ToString("MMM dd, yyyy");
    }
}
