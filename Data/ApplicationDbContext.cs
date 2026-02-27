using ByteBill_BS.Models;
using ByteBill_BS.Models.Enums;
using Microsoft.EntityFrameworkCore;

namespace ByteBill_BS.Data;

public class ApplicationDbContext : DbContext
{
    public ApplicationDbContext(DbContextOptions<ApplicationDbContext> options)
        : base(options)
    {
    }

    // ── DbSets ──────────────────────────────────────────────────────────
    public DbSet<Shop> Shops => Set<Shop>();
    public DbSet<User> Users => Set<User>();
    public DbSet<Role> Roles => Set<Role>();
    public DbSet<UserRoleAssignment> UserRoles => Set<UserRoleAssignment>();
    public DbSet<Customer> Customers => Set<Customer>();
    public DbSet<Device> Devices => Set<Device>();
    public DbSet<ServiceCategory> ServiceCategories => Set<ServiceCategory>();
    public DbSet<ServiceCatalog> ServiceCatalogs => Set<ServiceCatalog>();
    public DbSet<InventoryCategory> InventoryCategories => Set<InventoryCategory>();
    public DbSet<InventoryItem> InventoryItems => Set<InventoryItem>();
    public DbSet<InventoryTxn> InventoryTxns => Set<InventoryTxn>();
    public DbSet<JobOrder> JobOrders => Set<JobOrder>();
    public DbSet<JobOrderService> JobOrderServices => Set<JobOrderService>();
    public DbSet<JobOrderPart> JobOrderParts => Set<JobOrderPart>();
    public DbSet<JobOrderStatusHistory> JobOrderStatusHistories => Set<JobOrderStatusHistory>();
    public DbSet<Invoice> Invoices => Set<Invoice>();
    public DbSet<InvoiceLine> InvoiceLines => Set<InvoiceLine>();
    public DbSet<Payment> Payments => Set<Payment>();
    public DbSet<PaymentAllocation> PaymentAllocations => Set<PaymentAllocation>();
    public DbSet<PayMongoTxn> PayMongoTxns => Set<PayMongoTxn>();
    public DbSet<CreditDebitAdjustment> CreditDebitAdjustments => Set<CreditDebitAdjustment>();
    public DbSet<Notification> Notifications => Set<Notification>();
    public DbSet<AuditLog> AuditLogs => Set<AuditLog>();
    public DbSet<AccountingEntry> AccountingEntries => Set<AccountingEntry>();
    public DbSet<XeroSyncLog> XeroSyncLogs => Set<XeroSyncLog>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        base.OnModelCreating(modelBuilder);

        // ═══════════════════════════════════════════════════════════════
        // A. SHOP
        // ═══════════════════════════════════════════════════════════════
        modelBuilder.Entity<Shop>(entity =>
        {
            entity.ToTable("SHOP");
            entity.HasKey(e => e.ShopId);
            entity.Property(e => e.ShopId).HasColumnName("ShopID");
            entity.Property(e => e.ShopCode).HasMaxLength(30).IsRequired();
            entity.Property(e => e.ShopName).HasMaxLength(150).IsRequired();
            entity.Property(e => e.Email).HasMaxLength(100);
            entity.Property(e => e.Phone).HasMaxLength(30);
            entity.Property(e => e.Address).HasMaxLength(255);
            entity.Property(e => e.Status).HasMaxLength(20).IsRequired().HasDefaultValue("Active");
            entity.Property(e => e.CreatedAt).HasColumnType("datetime2(0)").HasDefaultValueSql("SYSDATETIME()");
            entity.Property(e => e.UpdatedAt).HasColumnType("datetime2(0)");
            entity.HasIndex(e => e.ShopCode).IsUnique();
        });

        // ═══════════════════════════════════════════════════════════════
        // B. USERS
        // ═══════════════════════════════════════════════════════════════
        modelBuilder.Entity<User>(entity =>
        {
            entity.ToTable("USERS");
            entity.HasKey(e => e.UserId);
            entity.Property(e => e.UserId).HasColumnName("UserID");
            entity.Property(e => e.ShopId).HasColumnName("ShopID");
            entity.Property(e => e.FirstName).HasMaxLength(50).IsRequired();
            entity.Property(e => e.MiddleName).HasMaxLength(50);
            entity.Property(e => e.LastName).HasMaxLength(50).IsRequired();
            entity.Property(e => e.UserName).HasMaxLength(100).IsRequired();
            entity.Property(e => e.PasswordHash).HasMaxLength(255).IsRequired();
            entity.Property(e => e.Email).HasMaxLength(150);
            entity.Property(e => e.Phone).HasMaxLength(20);
            entity.Property(e => e.ThemePreference).HasMaxLength(10).HasDefaultValue("light");
            entity.Property(e => e.EmailNotifications).HasDefaultValue(true);
            entity.Property(e => e.InAppNotifications).HasDefaultValue(true);
            entity.Property(e => e.IsActive).HasDefaultValue(true);
            entity.Property(e => e.CreatedAt).HasColumnType("datetime2(0)").HasDefaultValueSql("SYSDATETIME()");
            entity.Property(e => e.UpdatedAt).HasColumnType("datetime2(0)");
            entity.HasIndex(e => new { e.ShopId, e.UserName }).IsUnique();
            entity.HasIndex(e => e.ShopId);
            entity.Ignore(e => e.FullName);
            entity.Ignore(e => e.Initials);

            entity.HasOne(e => e.Shop)
                  .WithMany(s => s.Users)
                  .HasForeignKey(e => e.ShopId)
                  .OnDelete(DeleteBehavior.NoAction);
        });

        // ═══════════════════════════════════════════════════════════════
        // C. ROLES
        // ═══════════════════════════════════════════════════════════════
        modelBuilder.Entity<Role>(entity =>
        {
            entity.ToTable("ROLES");
            entity.HasKey(e => e.RoleId);
            entity.Property(e => e.RoleId).HasColumnName("RoleID");
            entity.Property(e => e.RoleName).HasMaxLength(50).IsRequired();
            entity.Property(e => e.Description).HasMaxLength(150);
            entity.HasIndex(e => e.RoleName).IsUnique();
        });

        // ═══════════════════════════════════════════════════════════════
        // D. USER_ROLES
        // ═══════════════════════════════════════════════════════════════
        modelBuilder.Entity<UserRoleAssignment>(entity =>
        {
            entity.ToTable("USER_ROLES");
            entity.HasKey(e => e.UserRoleId);
            entity.Property(e => e.UserRoleId).HasColumnName("UserRoleID");
            entity.Property(e => e.UserId).HasColumnName("UserID");
            entity.Property(e => e.RoleId).HasColumnName("RoleID");
            entity.Property(e => e.AssignedAt).HasColumnType("datetime2(0)").HasDefaultValueSql("SYSDATETIME()");
            entity.HasIndex(e => new { e.UserId, e.RoleId }).IsUnique();
            entity.HasIndex(e => e.UserId);
            entity.HasIndex(e => e.RoleId);

            entity.HasOne(e => e.User)
                  .WithMany(u => u.UserRoles)
                  .HasForeignKey(e => e.UserId)
                  .OnDelete(DeleteBehavior.NoAction);

            entity.HasOne(e => e.Role)
                  .WithMany(r => r.UserRoles)
                  .HasForeignKey(e => e.RoleId)
                  .OnDelete(DeleteBehavior.NoAction);
        });

        // ═══════════════════════════════════════════════════════════════
        // E. CUSTOMERS
        // ═══════════════════════════════════════════════════════════════
        modelBuilder.Entity<Customer>(entity =>
        {
            entity.ToTable("CUSTOMERS");
            entity.HasKey(e => e.CustomerId);
            entity.Property(e => e.CustomerId).HasColumnName("CustomerID");
            entity.Property(e => e.ShopId).HasColumnName("ShopID");
            entity.Property(e => e.FirstName).HasMaxLength(50).IsRequired();
            entity.Property(e => e.MiddleName).HasMaxLength(50);
            entity.Property(e => e.LastName).HasMaxLength(50).IsRequired();
            entity.Property(e => e.Email).HasMaxLength(100);
            entity.Property(e => e.Phone).HasMaxLength(30);
            entity.Property(e => e.Address).HasMaxLength(255);
            entity.Property(e => e.IsActive).HasDefaultValue(true);
            entity.Property(e => e.CreatedAt).HasColumnType("datetime2(0)").HasDefaultValueSql("SYSDATETIME()");
            entity.HasIndex(e => e.ShopId);
            entity.Ignore(e => e.FullName);
            entity.Ignore(e => e.Initials);

            entity.HasOne(e => e.Shop)
                  .WithMany(s => s.Customers)
                  .HasForeignKey(e => e.ShopId)
                  .OnDelete(DeleteBehavior.NoAction);
        });

        // ═══════════════════════════════════════════════════════════════
        // F. DEVICES
        // ═══════════════════════════════════════════════════════════════
        modelBuilder.Entity<Device>(entity =>
        {
            entity.ToTable("DEVICES");
            entity.HasKey(e => e.DeviceId);
            entity.Property(e => e.DeviceId).HasColumnName("DeviceID");
            entity.Property(e => e.CustomerId).HasColumnName("CustomerID");
            entity.Property(e => e.DeviceType).HasMaxLength(50).IsRequired();
            entity.Property(e => e.Brand).HasMaxLength(50).IsRequired();
            entity.Property(e => e.Model).HasMaxLength(80).IsRequired();
            entity.Property(e => e.SerialNo).HasMaxLength(60);
            entity.Property(e => e.Notes).HasMaxLength(255);
            entity.HasIndex(e => e.CustomerId);

            entity.HasOne(e => e.Customer)
                  .WithMany(c => c.Devices)
                  .HasForeignKey(e => e.CustomerId)
                  .OnDelete(DeleteBehavior.NoAction);
        });

        // ═══════════════════════════════════════════════════════════════
        // G. SERVICE_CATEGORY
        // ═══════════════════════════════════════════════════════════════
        modelBuilder.Entity<ServiceCategory>(entity =>
        {
            entity.ToTable("SERVICE_CATEGORY");
            entity.HasKey(e => e.ServiceCategoryId);
            entity.Property(e => e.ServiceCategoryId).HasColumnName("ServiceCategoryID");
            entity.Property(e => e.ShopId).HasColumnName("ShopID");
            entity.Property(e => e.CategoryName).HasMaxLength(80).IsRequired();
            entity.Property(e => e.Description).HasMaxLength(150);
            entity.HasIndex(e => new { e.ShopId, e.CategoryName }).IsUnique();
            entity.HasIndex(e => e.ShopId);

            entity.HasOne(e => e.Shop)
                  .WithMany(s => s.ServiceCategories)
                  .HasForeignKey(e => e.ShopId)
                  .OnDelete(DeleteBehavior.NoAction);
        });

        // ═══════════════════════════════════════════════════════════════
        // H. SERVICE_CATALOG
        // ═══════════════════════════════════════════════════════════════
        modelBuilder.Entity<ServiceCatalog>(entity =>
        {
            entity.ToTable("SERVICE_CATALOG");
            entity.HasKey(e => e.ServiceId);
            entity.Property(e => e.ServiceId).HasColumnName("ServiceID");
            entity.Property(e => e.ShopId).HasColumnName("ShopID");
            entity.Property(e => e.ServiceCategoryId).HasColumnName("ServiceCategoryID");
            entity.Property(e => e.ServiceName).HasMaxLength(120).IsRequired();
            entity.Property(e => e.Description).HasMaxLength(500);
            entity.Property(e => e.BasePrice).HasPrecision(18, 2).HasDefaultValue(0m);
            entity.Property(e => e.EstimatedDuration).HasDefaultValue(0);
            entity.Property(e => e.IsActive).HasDefaultValue(true);
            entity.HasIndex(e => new { e.ShopId, e.ServiceName }).IsUnique();
            entity.HasIndex(e => e.ShopId);
            entity.HasIndex(e => e.ServiceCategoryId);

            entity.HasOne(e => e.Shop)
                  .WithMany(s => s.ServiceCatalogs)
                  .HasForeignKey(e => e.ShopId)
                  .OnDelete(DeleteBehavior.NoAction);

            entity.HasOne(e => e.ServiceCategory)
                  .WithMany(sc => sc.Services)
                  .HasForeignKey(e => e.ServiceCategoryId)
                  .OnDelete(DeleteBehavior.NoAction);
        });

        // ═══════════════════════════════════════════════════════════════
        // I0. INVENTORY_CATEGORY
        // ═══════════════════════════════════════════════════════════════
        modelBuilder.Entity<InventoryCategory>(entity =>
        {
            entity.ToTable("INVENTORY_CATEGORY");
            entity.HasKey(e => e.InventoryCategoryId);
            entity.Property(e => e.InventoryCategoryId).HasColumnName("InventoryCategoryID");
            entity.Property(e => e.ShopId).HasColumnName("ShopID");
            entity.Property(e => e.CategoryName).HasMaxLength(80).IsRequired();
            entity.Property(e => e.Description).HasMaxLength(150);
            entity.HasIndex(e => new { e.ShopId, e.CategoryName }).IsUnique();
            entity.HasIndex(e => e.ShopId);

            entity.HasOne(e => e.Shop)
                  .WithMany(s => s.InventoryCategories)
                  .HasForeignKey(e => e.ShopId)
                  .OnDelete(DeleteBehavior.NoAction);
        });

        // ═══════════════════════════════════════════════════════════════
        // I. INVENTORY_ITEMS
        // ═══════════════════════════════════════════════════════════════
        modelBuilder.Entity<InventoryItem>(entity =>
        {
            entity.ToTable("INVENTORY_ITEMS");
            entity.HasKey(e => e.ItemId);
            entity.Property(e => e.ItemId).HasColumnName("ItemID");
            entity.Property(e => e.ShopId).HasColumnName("ShopID");
            entity.Property(e => e.InventoryCategoryId).HasColumnName("InventoryCategoryID");
            entity.Property(e => e.SKU).HasMaxLength(40).IsRequired();
            entity.Property(e => e.ItemName).HasMaxLength(120).IsRequired();
            entity.Property(e => e.Unit).HasMaxLength(20).IsRequired();
            entity.Property(e => e.UnitCost).HasPrecision(18, 2).HasDefaultValue(0m);
            entity.Property(e => e.UnitPrice).HasPrecision(18, 2).HasDefaultValue(0m);
            entity.Property(e => e.QtyOnHand).HasDefaultValue(0);
            entity.Property(e => e.ReorderLevel).HasDefaultValue(0);
            entity.Property(e => e.IsActive).HasDefaultValue(true);
            entity.HasIndex(e => new { e.ShopId, e.SKU }).IsUnique();
            entity.HasIndex(e => e.ShopId);
            entity.Ignore(e => e.IsLowStock);

            entity.HasOne(e => e.Shop)
                  .WithMany(s => s.InventoryItems)
                  .HasForeignKey(e => e.ShopId)
                  .OnDelete(DeleteBehavior.NoAction);

            entity.HasOne(e => e.InventoryCategory)
                  .WithMany(ic => ic.Items)
                  .HasForeignKey(e => e.InventoryCategoryId)
                  .OnDelete(DeleteBehavior.NoAction);
        });

        // ═══════════════════════════════════════════════════════════════
        // J. INVENTORY_TXN
        // ═══════════════════════════════════════════════════════════════
        modelBuilder.Entity<InventoryTxn>(entity =>
        {
            entity.ToTable("INVENTORY_TXN");
            entity.HasKey(e => e.InventoryTxnId);
            entity.Property(e => e.InventoryTxnId).HasColumnName("InventoryTxnID");
            entity.Property(e => e.ItemId).HasColumnName("ItemID");
            entity.Property(e => e.TxnType).HasMaxLength(10).IsRequired()
                  .HasConversion<string>();
            entity.Property(e => e.ReferenceType).HasMaxLength(30);
            entity.Property(e => e.ReferenceId).HasColumnName("ReferenceID");
            entity.Property(e => e.Remarks).HasMaxLength(150);
            entity.Property(e => e.CreatedAt).HasColumnType("datetime2(0)").HasDefaultValueSql("SYSDATETIME()");
            entity.HasIndex(e => e.ItemId);

            entity.HasOne(e => e.Item)
                  .WithMany(i => i.Transactions)
                  .HasForeignKey(e => e.ItemId)
                  .OnDelete(DeleteBehavior.NoAction);
        });

        // ═══════════════════════════════════════════════════════════════
        // K. JOB_ORDERS
        // ═══════════════════════════════════════════════════════════════
        modelBuilder.Entity<JobOrder>(entity =>
        {
            entity.ToTable("JOB_ORDERS");
            entity.HasKey(e => e.JobOrderId);
            entity.Property(e => e.JobOrderId).HasColumnName("JobOrderID");
            entity.Property(e => e.ShopId).HasColumnName("ShopID");
            entity.Property(e => e.CustomerId).HasColumnName("CustomerID");
            entity.Property(e => e.DeviceId).HasColumnName("DeviceID");
            entity.Property(e => e.CreatedByUserId).HasColumnName("CreatedByUserID");
            entity.Property(e => e.AssignedTechUserId).HasColumnName("AssignedTechUserID");
            entity.Property(e => e.JobOrderNo).HasMaxLength(30).IsRequired();
            entity.Property(e => e.ProblemReported).HasMaxLength(255).IsRequired();
            entity.Property(e => e.DiagnosisNotes).HasMaxLength(255);
            entity.Property(e => e.Status).HasMaxLength(30).IsRequired().HasDefaultValue(JobOrderStatus.Pending)
                  .HasConversion<string>();
            entity.Property(e => e.CreatedAt).HasColumnType("datetime2(0)").HasDefaultValueSql("SYSDATETIME()");
            entity.Property(e => e.UpdatedAt).HasColumnType("datetime2(0)");
            entity.Property(e => e.IsArchived).HasDefaultValue(false);
            entity.Property(e => e.ArchivedDate).HasColumnType("datetime2(0)");
            entity.HasIndex(e => new { e.ShopId, e.JobOrderNo }).IsUnique();
            entity.HasIndex(e => e.ShopId);
            entity.HasIndex(e => e.CustomerId);
            entity.HasIndex(e => e.DeviceId);
            entity.HasIndex(e => e.CreatedByUserId);
            entity.HasIndex(e => e.AssignedTechUserId);

            entity.HasOne(e => e.Shop)
                  .WithMany(s => s.JobOrders)
                  .HasForeignKey(e => e.ShopId)
                  .OnDelete(DeleteBehavior.NoAction);

            entity.HasOne(e => e.Customer)
                  .WithMany(c => c.JobOrders)
                  .HasForeignKey(e => e.CustomerId)
                  .OnDelete(DeleteBehavior.NoAction);

            entity.HasOne(e => e.Device)
                  .WithMany(d => d.JobOrders)
                  .HasForeignKey(e => e.DeviceId)
                  .OnDelete(DeleteBehavior.NoAction);

            entity.HasOne(e => e.CreatedByUser)
                  .WithMany()
                  .HasForeignKey(e => e.CreatedByUserId)
                  .OnDelete(DeleteBehavior.NoAction);

            entity.HasOne(e => e.AssignedTechUser)
                  .WithMany()
                  .HasForeignKey(e => e.AssignedTechUserId)
                  .OnDelete(DeleteBehavior.NoAction);
        });

        // ═══════════════════════════════════════════════════════════════
        // L. JOB_ORDER_SERVICES
        // ═══════════════════════════════════════════════════════════════
        modelBuilder.Entity<JobOrderService>(entity =>
        {
            entity.ToTable("JOB_ORDER_SERVICES");
            entity.HasKey(e => e.JobOrderServiceId);
            entity.Property(e => e.JobOrderServiceId).HasColumnName("JobOrderServiceID");
            entity.Property(e => e.JobOrderId).HasColumnName("JobOrderID");
            entity.Property(e => e.ServiceId).HasColumnName("ServiceID");
            entity.Property(e => e.Qty).HasDefaultValue(1);
            entity.Property(e => e.UnitPrice).HasPrecision(18, 2).HasDefaultValue(0m);
            entity.Property(e => e.LineTotal).HasPrecision(18, 2)
                  .HasComputedColumnSql("[Qty] * [UnitPrice]", stored: true);
            entity.HasIndex(e => e.JobOrderId);
            entity.HasIndex(e => e.ServiceId);

            entity.HasOne(e => e.JobOrder)
                  .WithMany(j => j.JobOrderServices)
                  .HasForeignKey(e => e.JobOrderId)
                  .OnDelete(DeleteBehavior.NoAction);

            entity.HasOne(e => e.Service)
                  .WithMany(s => s.JobOrderServices)
                  .HasForeignKey(e => e.ServiceId)
                  .OnDelete(DeleteBehavior.NoAction);
        });

        // ═══════════════════════════════════════════════════════════════
        // M. JOB_ORDER_PARTS
        // ═══════════════════════════════════════════════════════════════
        modelBuilder.Entity<JobOrderPart>(entity =>
        {
            entity.ToTable("JOB_ORDER_PARTS");
            entity.HasKey(e => e.JobOrderPartId);
            entity.Property(e => e.JobOrderPartId).HasColumnName("JobOrderPartID");
            entity.Property(e => e.JobOrderId).HasColumnName("JobOrderID");
            entity.Property(e => e.ItemId).HasColumnName("ItemID");
            entity.Property(e => e.QtyUsed).HasDefaultValue(1);
            entity.Property(e => e.UnitPrice).HasPrecision(18, 2).HasDefaultValue(0m);
            entity.Property(e => e.LineTotal).HasPrecision(18, 2)
                  .HasComputedColumnSql("[QtyUsed] * [UnitPrice]", stored: true);
            entity.HasIndex(e => e.JobOrderId);
            entity.HasIndex(e => e.ItemId);

            entity.HasOne(e => e.JobOrder)
                  .WithMany(j => j.JobOrderParts)
                  .HasForeignKey(e => e.JobOrderId)
                  .OnDelete(DeleteBehavior.NoAction);

            entity.HasOne(e => e.Item)
                  .WithMany(i => i.JobOrderParts)
                  .HasForeignKey(e => e.ItemId)
                  .OnDelete(DeleteBehavior.NoAction);
        });

        // ═══════════════════════════════════════════════════════════════
        // N. JOB_ORDER_STATUS_HISTORY
        // ═══════════════════════════════════════════════════════════════
        modelBuilder.Entity<JobOrderStatusHistory>(entity =>
        {
            entity.ToTable("JOB_ORDER_STATUS_HISTORY");
            entity.HasKey(e => e.JobOrderStatusHistoryId);
            entity.Property(e => e.JobOrderStatusHistoryId).HasColumnName("JobOrderStatusHistoryID");
            entity.Property(e => e.JobOrderId).HasColumnName("JobOrderID");
            entity.Property(e => e.OldStatus).HasMaxLength(30).IsRequired();
            entity.Property(e => e.NewStatus).HasMaxLength(30).IsRequired();
            entity.Property(e => e.ChangedByUserId).HasColumnName("ChangedByUserID");
            entity.Property(e => e.ChangedAt).HasColumnType("datetime2(0)").HasDefaultValueSql("SYSDATETIME()");
            entity.Property(e => e.Remarks).HasMaxLength(150);
            entity.HasIndex(e => e.JobOrderId);
            entity.HasIndex(e => e.ChangedByUserId);

            entity.HasOne(e => e.JobOrder)
                  .WithMany(j => j.StatusHistory)
                  .HasForeignKey(e => e.JobOrderId)
                  .OnDelete(DeleteBehavior.NoAction);

            entity.HasOne(e => e.ChangedByUser)
                  .WithMany()
                  .HasForeignKey(e => e.ChangedByUserId)
                  .OnDelete(DeleteBehavior.NoAction);
        });

        // ═══════════════════════════════════════════════════════════════
        // O. INVOICES
        //    UNIQUE(JobOrderID) → enforces 1:1 JOB_ORDERS → INVOICES
        // ═══════════════════════════════════════════════════════════════
        modelBuilder.Entity<Invoice>(entity =>
        {
            entity.ToTable("INVOICES");
            entity.HasKey(e => e.InvoiceId);
            entity.Property(e => e.InvoiceId).HasColumnName("InvoiceID");
            entity.Property(e => e.ShopId).HasColumnName("ShopID");
            entity.Property(e => e.JobOrderId).HasColumnName("JobOrderID");
            entity.Property(e => e.CustomerId).HasColumnName("CustomerID");
            entity.Property(e => e.InvoiceNo).HasMaxLength(30).IsRequired();
            entity.Property(e => e.InvoiceDate).HasColumnType("datetime2(0)").HasDefaultValueSql("SYSDATETIME()");
            entity.Property(e => e.Subtotal).HasPrecision(18, 2).HasDefaultValue(0m);
            entity.Property(e => e.TotalAdjustments).HasPrecision(18, 2).HasDefaultValue(0m);
            entity.Property(e => e.TotalAmount).HasPrecision(18, 2).HasDefaultValue(0m);
            entity.Property(e => e.AmountPaid).HasPrecision(18, 2).HasDefaultValue(0m);
            entity.Property(e => e.Balance).HasPrecision(18, 2).HasDefaultValue(0m);
            entity.Property(e => e.Status).HasMaxLength(20).IsRequired().HasDefaultValue(InvoiceStatus.Unpaid)
                  .HasConversion<string>();
            entity.Property(e => e.CreatedAt).HasColumnType("datetime2(0)").HasDefaultValueSql("SYSDATETIME()");
            entity.Property(e => e.DueDate).HasColumnType("datetime2(0)");
            entity.Property(e => e.IsArchived).HasDefaultValue(false);
            entity.Property(e => e.ArchivedDate).HasColumnType("datetime2(0)");
            entity.HasIndex(e => new { e.ShopId, e.InvoiceNo }).IsUnique();
            entity.HasIndex(e => e.JobOrderId).IsUnique(); // 1:1 with JOB_ORDERS
            entity.HasIndex(e => e.ShopId);
            entity.HasIndex(e => e.CustomerId);

            entity.HasOne(e => e.Shop)
                  .WithMany(s => s.Invoices)
                  .HasForeignKey(e => e.ShopId)
                  .OnDelete(DeleteBehavior.NoAction);

            // 1:1 relationship — each Job Order may have at most one Invoice
            entity.HasOne(e => e.JobOrder)
                  .WithOne(j => j.Invoice)
                  .HasForeignKey<Invoice>(e => e.JobOrderId)
                  .OnDelete(DeleteBehavior.NoAction);

            entity.HasOne(e => e.Customer)
                  .WithMany(c => c.Invoices)
                  .HasForeignKey(e => e.CustomerId)
                  .OnDelete(DeleteBehavior.NoAction);
        });

        // ═══════════════════════════════════════════════════════════════
        // P. INVOICE_LINES
        // ═══════════════════════════════════════════════════════════════
        modelBuilder.Entity<InvoiceLine>(entity =>
        {
            entity.ToTable("INVOICE_LINES");
            entity.HasKey(e => e.InvoiceLineId);
            entity.Property(e => e.InvoiceLineId).HasColumnName("InvoiceLineID");
            entity.Property(e => e.InvoiceId).HasColumnName("InvoiceID");
            entity.Property(e => e.LineType).HasMaxLength(20).IsRequired();
            entity.Property(e => e.Description).HasMaxLength(150).IsRequired();
            entity.Property(e => e.Qty).HasDefaultValue(1);
            entity.Property(e => e.UnitPrice).HasPrecision(18, 2).HasDefaultValue(0m);
            entity.Property(e => e.LineTotal).HasPrecision(18, 2)
                  .HasComputedColumnSql("[Qty] * [UnitPrice]", stored: true);
            entity.HasIndex(e => e.InvoiceId);

            entity.HasOne(e => e.Invoice)
                  .WithMany(i => i.InvoiceLines)
                  .HasForeignKey(e => e.InvoiceId)
                  .OnDelete(DeleteBehavior.NoAction);
        });

        // ═══════════════════════════════════════════════════════════════
        // Q. PAYMENTS
        // ═══════════════════════════════════════════════════════════════
        modelBuilder.Entity<Payment>(entity =>
        {
            entity.ToTable("PAYMENTS");
            entity.HasKey(e => e.PaymentId);
            entity.Property(e => e.PaymentId).HasColumnName("PaymentID");
            entity.Property(e => e.ShopId).HasColumnName("ShopID");
            entity.Property(e => e.CustomerId).HasColumnName("CustomerID");
            entity.Property(e => e.PaymentNo).HasMaxLength(30).IsRequired();
            entity.Property(e => e.PaymentDate).HasColumnType("datetime2(0)").HasDefaultValueSql("SYSDATETIME()");
            entity.Property(e => e.Amount).HasPrecision(18, 2).IsRequired();
            entity.Property(e => e.Method).HasMaxLength(30).IsRequired()
                  .HasConversion<string>();
            entity.Property(e => e.ReferenceNo).HasMaxLength(60);
            entity.Property(e => e.ReceivedByUserId).HasColumnName("ReceivedByUserID");
            entity.Property(e => e.Status).HasMaxLength(20).IsRequired().HasDefaultValue(PaymentStatus.Confirmed)
                  .HasConversion<string>()
                  .HasSentinel((PaymentStatus)(-1));
            entity.Property(e => e.Notes).HasMaxLength(500);
            entity.HasIndex(e => e.ShopId);
            entity.HasIndex(e => e.CustomerId);
            entity.HasIndex(e => e.ReceivedByUserId);

            entity.HasOne(e => e.Shop)
                  .WithMany(s => s.Payments)
                  .HasForeignKey(e => e.ShopId)
                  .OnDelete(DeleteBehavior.NoAction);

            entity.HasOne(e => e.Customer)
                  .WithMany(c => c.Payments)
                  .HasForeignKey(e => e.CustomerId)
                  .OnDelete(DeleteBehavior.NoAction);

            entity.HasOne(e => e.ReceivedByUser)
                  .WithMany()
                  .HasForeignKey(e => e.ReceivedByUserId)
                  .OnDelete(DeleteBehavior.NoAction);
        });

        // ═══════════════════════════════════════════════════════════════
        // R. PAYMENT_ALLOCATION
        // ═══════════════════════════════════════════════════════════════
        modelBuilder.Entity<PaymentAllocation>(entity =>
        {
            entity.ToTable("PAYMENT_ALLOCATION");
            entity.HasKey(e => e.PaymentAllocationId);
            entity.Property(e => e.PaymentAllocationId).HasColumnName("PaymentAllocationID");
            entity.Property(e => e.PaymentId).HasColumnName("PaymentID");
            entity.Property(e => e.InvoiceId).HasColumnName("InvoiceID");
            entity.Property(e => e.AmountApplied).HasPrecision(18, 2).IsRequired();
            entity.HasIndex(e => new { e.PaymentId, e.InvoiceId }).IsUnique();
            entity.HasIndex(e => e.PaymentId);
            entity.HasIndex(e => e.InvoiceId);

            entity.HasOne(e => e.Payment)
                  .WithMany(p => p.PaymentAllocations)
                  .HasForeignKey(e => e.PaymentId)
                  .OnDelete(DeleteBehavior.NoAction);

            entity.HasOne(e => e.Invoice)
                  .WithMany(i => i.PaymentAllocations)
                  .HasForeignKey(e => e.InvoiceId)
                  .OnDelete(DeleteBehavior.NoAction);
        });

        // ═══════════════════════════════════════════════════════════════
        // S. PAYMONGO_TXN
        //    PaymentID is nullable — set only after webhook confirms payment
        // ═══════════════════════════════════════════════════════════════
        modelBuilder.Entity<PayMongoTxn>(entity =>
        {
            entity.ToTable("PAYMONGO_TXN");
            entity.HasKey(e => e.PayMongoTxnId);
            entity.Property(e => e.PayMongoTxnId).HasColumnName("PayMongoTxnID");
            entity.Property(e => e.PaymentId).HasColumnName("PaymentID");
            entity.Property(e => e.ShopId).HasColumnName("ShopID");
            entity.Property(e => e.InvoiceId).HasColumnName("InvoiceID");
            entity.Property(e => e.InitiatedByUserId).HasColumnName("InitiatedByUserID");
            entity.Property(e => e.Amount).HasColumnType("decimal(18,2)");
            entity.Property(e => e.PayMongoPaymentIntentId).HasColumnName("PayMongoPaymentIntentID").HasMaxLength(80).IsRequired();
            entity.Property(e => e.PayMongoStatus).HasMaxLength(30).IsRequired();
            entity.Property(e => e.PayMongoPaymentMethod).HasMaxLength(30);
            entity.Property(e => e.CheckoutUrl).HasMaxLength(500);
            entity.Property(e => e.ResourceType).HasMaxLength(30).IsRequired().HasDefaultValue("link");
            entity.Property(e => e.RawResponse).HasColumnType("nvarchar(max)");
            entity.Property(e => e.CreatedAt).HasColumnType("datetime2(0)").HasDefaultValueSql("SYSDATETIME()");
            entity.Property(e => e.UpdatedAt).HasColumnType("datetime2(0)");
            entity.HasIndex(e => e.PaymentId).IsUnique().HasFilter("[PaymentID] IS NOT NULL");
            entity.HasIndex(e => e.PayMongoPaymentIntentId); // For webhook lookups

            // Optional 1:0..1 relationship — Payment set after webhook confirmation
            entity.HasOne(e => e.Payment)
                  .WithOne(p => p.PayMongoTxn)
                  .HasForeignKey<PayMongoTxn>(e => e.PaymentId)
                  .OnDelete(DeleteBehavior.NoAction);

            // Invoice relationship
            entity.HasOne(e => e.Invoice)
                  .WithMany()
                  .HasForeignKey(e => e.InvoiceId)
                  .OnDelete(DeleteBehavior.NoAction);

            // Shop relationship
            entity.HasOne(e => e.Shop)
                  .WithMany()
                  .HasForeignKey(e => e.ShopId)
                  .OnDelete(DeleteBehavior.NoAction);
        });

        // ═══════════════════════════════════════════════════════════════
        // T. CREDIT_DEBIT_ADJUSTMENT
        // ═══════════════════════════════════════════════════════════════
        modelBuilder.Entity<CreditDebitAdjustment>(entity =>
        {
            entity.ToTable("CREDIT_DEBIT_ADJUSTMENT");
            entity.HasKey(e => e.AdjustmentId);
            entity.Property(e => e.AdjustmentId).HasColumnName("AdjustmentID");
            entity.Property(e => e.ShopId).HasColumnName("ShopID");
            entity.Property(e => e.InvoiceId).HasColumnName("InvoiceID");
            entity.Property(e => e.CreatedByUserId).HasColumnName("CreatedByUserID");
            entity.Property(e => e.ReviewedByUserId).HasColumnName("ReviewedByUserID");
            entity.Property(e => e.AdjustmentType).HasMaxLength(10).IsRequired()
                  .HasConversion<string>();
            entity.Property(e => e.Amount).HasPrecision(18, 2).IsRequired();
            entity.Property(e => e.Reason).HasMaxLength(500).IsRequired();
            entity.Property(e => e.Status).HasMaxLength(10).IsRequired()
                  .HasConversion<string>()
                  .HasDefaultValue(Models.Enums.AdjustmentStatus.Pending);
            entity.Property(e => e.CreatedAt).HasColumnType("datetime2(0)").HasDefaultValueSql("SYSDATETIME()");
            entity.Property(e => e.ReviewedAt).HasColumnType("datetime2(0)");
            entity.HasIndex(e => e.InvoiceId);
            entity.HasIndex(e => e.CreatedByUserId);
            entity.HasIndex(e => e.ShopId);

            entity.HasOne(e => e.Shop)
                  .WithMany()
                  .HasForeignKey(e => e.ShopId)
                  .OnDelete(DeleteBehavior.NoAction);

            entity.HasOne(e => e.Invoice)
                  .WithMany(i => i.Adjustments)
                  .HasForeignKey(e => e.InvoiceId)
                  .OnDelete(DeleteBehavior.NoAction);

            entity.HasOne(e => e.CreatedByUser)
                  .WithMany()
                  .HasForeignKey(e => e.CreatedByUserId)
                  .OnDelete(DeleteBehavior.NoAction);

            entity.HasOne(e => e.ReviewedByUser)
                  .WithMany()
                  .HasForeignKey(e => e.ReviewedByUserId)
                  .OnDelete(DeleteBehavior.NoAction);
        });

        // ═══════════════════════════════════════════════════════════════
        // T2. NOTIFICATION
        // ═══════════════════════════════════════════════════════════════
        modelBuilder.Entity<Notification>(entity =>
        {
            entity.ToTable("NOTIFICATION");
            entity.HasKey(e => e.NotificationId);
            entity.Property(e => e.NotificationId).HasColumnName("NotificationID");
            entity.Property(e => e.UserId).HasColumnName("UserID");
            entity.Property(e => e.ShopId).HasColumnName("ShopID");
            entity.Property(e => e.Title).HasMaxLength(100).IsRequired();
            entity.Property(e => e.Message).HasMaxLength(500).IsRequired();
            entity.Property(e => e.Type).HasMaxLength(20).IsRequired().HasDefaultValue("info");
            entity.Property(e => e.Url).HasMaxLength(200);
            entity.Property(e => e.IsRead).HasDefaultValue(false);
            entity.Property(e => e.CreatedAt).HasColumnType("datetime2(0)").HasDefaultValueSql("SYSDATETIME()");
            entity.HasIndex(e => new { e.UserId, e.IsRead });

            entity.HasOne(e => e.User)
                  .WithMany()
                  .HasForeignKey(e => e.UserId)
                  .OnDelete(DeleteBehavior.Cascade);

            entity.HasOne(e => e.Shop)
                  .WithMany()
                  .HasForeignKey(e => e.ShopId)
                  .OnDelete(DeleteBehavior.NoAction);
        });

        // ═══════════════════════════════════════════════════════════════
        // U. AUDIT_LOG
        // ═══════════════════════════════════════════════════════════════
        modelBuilder.Entity<AuditLog>(entity =>
        {
            entity.ToTable("AUDIT_LOG");
            entity.HasKey(e => e.AuditLogId);
            entity.Property(e => e.AuditLogId).HasColumnName("AuditLogID");
            entity.Property(e => e.ShopId).HasColumnName("ShopID");
            entity.Property(e => e.UserId).HasColumnName("UserID");
            entity.Property(e => e.Action).HasMaxLength(50).IsRequired();
            entity.Property(e => e.EntityName).HasMaxLength(50).IsRequired();
            entity.Property(e => e.EntityId).HasColumnName("EntityID");
            entity.Property(e => e.Details).HasMaxLength(500);
            entity.Property(e => e.IpAddress).HasMaxLength(45);
            entity.Property(e => e.OldValues).HasMaxLength(2000);
            entity.Property(e => e.NewValues).HasMaxLength(2000);
            entity.Property(e => e.CreatedAt).HasColumnType("datetime2(0)").HasDefaultValueSql("SYSDATETIME()");
            entity.HasIndex(e => e.ShopId);
            entity.HasIndex(e => e.UserId);
            entity.HasIndex(e => e.CreatedAt);

            entity.HasOne(e => e.Shop)
                  .WithMany(s => s.AuditLogs)
                  .HasForeignKey(e => e.ShopId)
                  .OnDelete(DeleteBehavior.NoAction);

            entity.HasOne(e => e.User)
                  .WithMany()
                  .HasForeignKey(e => e.UserId)
                  .IsRequired(false)
                  .OnDelete(DeleteBehavior.NoAction);
        });

        // ═══════════════════════════════════════════════════════════════
        // V. ACCOUNTING_ENTRY
        // ═══════════════════════════════════════════════════════════════
        modelBuilder.Entity<AccountingEntry>(entity =>
        {
            entity.ToTable("ACCOUNTING_ENTRY");
            entity.HasKey(e => e.AccountingEntryId);
            entity.Property(e => e.AccountingEntryId).HasColumnName("AccountingEntryID");
            entity.Property(e => e.ShopId).HasColumnName("ShopID");
            entity.Property(e => e.SourceType).HasMaxLength(20).IsRequired();
            entity.Property(e => e.SourceInvoiceId).HasColumnName("SourceInvoiceID");
            entity.Property(e => e.SourcePaymentId).HasColumnName("SourcePaymentID");
            entity.Property(e => e.EntryDate).HasColumnType("datetime2(0)").HasDefaultValueSql("SYSDATETIME()");
            entity.Property(e => e.AccountCode).HasMaxLength(20).IsRequired();
            entity.Property(e => e.Debit).HasPrecision(18, 2).HasDefaultValue(0m);
            entity.Property(e => e.Credit).HasPrecision(18, 2).HasDefaultValue(0m);
            entity.Property(e => e.Memo).HasMaxLength(150);
            entity.HasIndex(e => e.ShopId);
            entity.HasIndex(e => e.SourceInvoiceId);
            entity.HasIndex(e => e.SourcePaymentId);

            entity.HasOne(e => e.Shop)
                  .WithMany(s => s.AccountingEntries)
                  .HasForeignKey(e => e.ShopId)
                  .OnDelete(DeleteBehavior.NoAction);

            entity.HasOne(e => e.SourceInvoice)
                  .WithMany(i => i.AccountingEntries)
                  .HasForeignKey(e => e.SourceInvoiceId)
                  .OnDelete(DeleteBehavior.NoAction);

            entity.HasOne(e => e.SourcePayment)
                  .WithMany(p => p.AccountingEntries)
                  .HasForeignKey(e => e.SourcePaymentId)
                  .OnDelete(DeleteBehavior.NoAction);
        });

        // ═══════════════════════════════════════════════════════════════
        // W. XERO_SYNC_LOG
        // ═══════════════════════════════════════════════════════════════
        modelBuilder.Entity<XeroSyncLog>(entity =>
        {
            entity.ToTable("XERO_SYNC_LOG");
            entity.HasKey(e => e.XeroSyncLogId);
            entity.Property(e => e.XeroSyncLogId).HasColumnName("XeroSyncLogID");
            entity.Property(e => e.ShopId).HasColumnName("ShopID");
            entity.Property(e => e.SyncedByUserId).HasColumnName("SyncedByUserID");
            entity.Property(e => e.SyncType).HasMaxLength(30).IsRequired();
            entity.Property(e => e.InvoiceId).HasColumnName("InvoiceID");
            entity.Property(e => e.PaymentId).HasColumnName("PaymentID");
            entity.Property(e => e.AccountingEntryId).HasColumnName("AccountingEntryID");
            entity.Property(e => e.XeroRecordId).HasColumnName("XeroRecordID").HasMaxLength(80);
            entity.Property(e => e.Status).HasMaxLength(20).IsRequired().HasDefaultValue("Pending");
            entity.Property(e => e.Message).HasMaxLength(255);
            entity.Property(e => e.SyncedAt).HasColumnType("datetime2(0)").HasDefaultValueSql("SYSDATETIME()");
            entity.HasIndex(e => e.ShopId);
            entity.HasIndex(e => e.SyncedByUserId);
            entity.HasIndex(e => e.InvoiceId);
            entity.HasIndex(e => e.PaymentId);
            entity.HasIndex(e => e.AccountingEntryId);

            entity.HasOne(e => e.Shop)
                  .WithMany(s => s.XeroSyncLogs)
                  .HasForeignKey(e => e.ShopId)
                  .OnDelete(DeleteBehavior.NoAction);

            entity.HasOne(e => e.SyncedByUser)
                  .WithMany()
                  .HasForeignKey(e => e.SyncedByUserId)
                  .OnDelete(DeleteBehavior.NoAction);

            entity.HasOne(e => e.Invoice)
                  .WithMany(i => i.XeroSyncLogs)
                  .HasForeignKey(e => e.InvoiceId)
                  .OnDelete(DeleteBehavior.NoAction);

            entity.HasOne(e => e.Payment)
                  .WithMany(p => p.XeroSyncLogs)
                  .HasForeignKey(e => e.PaymentId)
                  .OnDelete(DeleteBehavior.NoAction);

            entity.HasOne(e => e.AccountingEntry)
                  .WithMany(ae => ae.XeroSyncLogs)
                  .HasForeignKey(e => e.AccountingEntryId)
                  .OnDelete(DeleteBehavior.NoAction);
        });
    }
}
