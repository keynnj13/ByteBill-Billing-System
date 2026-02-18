using ByteBill_BS.Models;
using ByteBill_BS.Models.Enums;
using Microsoft.EntityFrameworkCore;

namespace ByteBill_BS.Data;

/// <summary>
/// Seeds the database with default roles, demo users, and sample data on first run.
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

        // Sample data for all modules
        await SeedCustomersAsync(db);
        await SeedDevicesAsync(db);
        await SeedServiceCategoriesAsync(db);
        await SeedServiceCatalogAsync(db);
        await SeedInventoryItemsAsync(db);
        await SeedJobOrdersAsync(db);
        await SeedInvoicesAsync(db);
        await SeedPaymentsAsync(db);
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
            new() { ShopId = shop.ShopId, ServiceCategoryId = cats["Diagnosis"],     ServiceName = "System Diagnosis",           BasePrice = 350m,  IsActive = true },
            new() { ShopId = shop.ShopId, ServiceCategoryId = cats["Diagnosis"],     ServiceName = "Hardware Inspection",        BasePrice = 250m,  IsActive = true },
            new() { ShopId = shop.ShopId, ServiceCategoryId = cats["Repair"],        ServiceName = "Virus/Malware Removal",      BasePrice = 500m,  IsActive = true },
            new() { ShopId = shop.ShopId, ServiceCategoryId = cats["Repair"],        ServiceName = "Screen Replacement",         BasePrice = 2500m, IsActive = true },
            new() { ShopId = shop.ShopId, ServiceCategoryId = cats["Repair"],        ServiceName = "Battery Replacement",        BasePrice = 1200m, IsActive = true },
            new() { ShopId = shop.ShopId, ServiceCategoryId = cats["Repair"],        ServiceName = "Keyboard Replacement",       BasePrice = 1800m, IsActive = true },
            new() { ShopId = shop.ShopId, ServiceCategoryId = cats["Installation"],  ServiceName = "OS Installation",            BasePrice = 800m,  IsActive = true },
            new() { ShopId = shop.ShopId, ServiceCategoryId = cats["Installation"],  ServiceName = "Software Setup & Config",    BasePrice = 400m,  IsActive = true },
            new() { ShopId = shop.ShopId, ServiceCategoryId = cats["Installation"],  ServiceName = "RAM/SSD Upgrade",            BasePrice = 500m,  IsActive = true },
            new() { ShopId = shop.ShopId, ServiceCategoryId = cats["Maintenance"],   ServiceName = "Internal Cleaning & Repaste",BasePrice = 600m,  IsActive = true },
            new() { ShopId = shop.ShopId, ServiceCategoryId = cats["Maintenance"],   ServiceName = "Full System Tune-Up",        BasePrice = 450m,  IsActive = true },
            new() { ShopId = shop.ShopId, ServiceCategoryId = cats["Data Recovery"], ServiceName = "Data Recovery (HDD/SSD)",    BasePrice = 1500m, IsActive = true },
        };

        db.ServiceCatalogs.AddRange(services);
        await db.SaveChangesAsync();
    }

    // ── Inventory Items (12) ────────────────────────────────────────────
    private static async Task SeedInventoryItemsAsync(ApplicationDbContext db)
    {
        if (await db.InventoryItems.AnyAsync()) return;

        var shop = await db.Shops.FirstAsync();

        var items = new List<InventoryItem>
        {
            new() { ShopId = shop.ShopId, SKU = "SSD-500-SAM",    ItemName = "Samsung 870 EVO 500GB SSD",      Unit = "pcs", UnitCost = 2500m,  UnitPrice = 3500m,  QtyOnHand = 15, ReorderLevel = 5,  IsActive = true },
            new() { ShopId = shop.ShopId, SKU = "SSD-256-KNG",    ItemName = "Kingston A400 256GB SSD",        Unit = "pcs", UnitCost = 1200m,  UnitPrice = 1800m,  QtyOnHand = 20, ReorderLevel = 5,  IsActive = true },
            new() { ShopId = shop.ShopId, SKU = "RAM-8-COR",      ItemName = "Corsair Vengeance 8GB DDR4",     Unit = "pcs", UnitCost = 1500m,  UnitPrice = 2200m,  QtyOnHand = 10, ReorderLevel = 5,  IsActive = true },
            new() { ShopId = shop.ShopId, SKU = "RAM-16-COR",     ItemName = "Corsair Vengeance 16GB DDR4",    Unit = "pcs", UnitCost = 2800m,  UnitPrice = 3900m,  QtyOnHand = 8,  ReorderLevel = 5,  IsActive = true },
            new() { ShopId = shop.ShopId, SKU = "HDD-1TB-WD",     ItemName = "WD Blue 1TB HDD",               Unit = "pcs", UnitCost = 2000m,  UnitPrice = 2800m,  QtyOnHand = 3,  ReorderLevel = 5,  IsActive = true },
            new() { ShopId = shop.ShopId, SKU = "PST-THRM-NT",    ItemName = "Noctua NT-H1 Thermal Paste",    Unit = "pcs", UnitCost = 400m,   UnitPrice = 750m,   QtyOnHand = 25, ReorderLevel = 10, IsActive = true },
            new() { ShopId = shop.ShopId, SKU = "CBL-HDMI-2M",    ItemName = "HDMI Cable 2m",                 Unit = "pcs", UnitCost = 150m,   UnitPrice = 350m,   QtyOnHand = 30, ReorderLevel = 10, IsActive = true },
            new() { ShopId = shop.ShopId, SKU = "CBL-USBC-1M",    ItemName = "USB-C Cable 1m",                Unit = "pcs", UnitCost = 100m,   UnitPrice = 250m,   QtyOnHand = 40, ReorderLevel = 10, IsActive = true },
            new() { ShopId = shop.ShopId, SKU = "FAN-120-DPC",    ItemName = "DeepCool 120mm Case Fan",       Unit = "pcs", UnitCost = 250m,   UnitPrice = 450m,   QtyOnHand = 12, ReorderLevel = 5,  IsActive = true },
            new() { ShopId = shop.ShopId, SKU = "PSU-550-EVG",    ItemName = "EVGA 550W 80+ Bronze PSU",      Unit = "pcs", UnitCost = 2500m,  UnitPrice = 3600m,  QtyOnHand = 4,  ReorderLevel = 3,  IsActive = true },
            new() { ShopId = shop.ShopId, SKU = "KBD-LOGI-K120",  ItemName = "Logitech K120 Keyboard",        Unit = "pcs", UnitCost = 450m,   UnitPrice = 750m,   QtyOnHand = 6,  ReorderLevel = 5,  IsActive = true },
            new() { ShopId = shop.ShopId, SKU = "MOU-LOGI-B100",  ItemName = "Logitech B100 Mouse",           Unit = "pcs", UnitCost = 250m,   UnitPrice = 450m,   QtyOnHand = 2,  ReorderLevel = 5,  IsActive = true },
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
}
