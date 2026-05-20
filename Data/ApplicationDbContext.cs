using ByteBill_BS.Models;
using ByteBill_BS.Models.Enums;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.ChangeTracking;
using Microsoft.EntityFrameworkCore.Storage.ValueConversion;

namespace ByteBill_BS.Data;

public class ApplicationDbContext : DbContext
{
      private readonly ByteBill_BS.Services.IEmailSecurityService _emailSecurity;

      public ApplicationDbContext(DbContextOptions<ApplicationDbContext> options, ByteBill_BS.Services.IEmailSecurityService emailSecurity)
            : base(options)
      {
            _emailSecurity = emailSecurity;
    }

      public override int SaveChanges()
      {
            ApplyPiiHashes();
            return base.SaveChanges();
      }

      public override int SaveChanges(bool acceptAllChangesOnSuccess)
      {
            ApplyPiiHashes();
            return base.SaveChanges(acceptAllChangesOnSuccess);
      }

      public override Task<int> SaveChangesAsync(CancellationToken cancellationToken = default)
      {
            ApplyPiiHashes();
            return base.SaveChangesAsync(cancellationToken);
      }

      public override Task<int> SaveChangesAsync(bool acceptAllChangesOnSuccess, CancellationToken cancellationToken = default)
      {
            ApplyPiiHashes();
            return base.SaveChangesAsync(acceptAllChangesOnSuccess, cancellationToken);
      }

      private void ApplyPiiHashes()
      {
            UpdatePiiHashes(ChangeTracker.Entries<User>(), NormalizeUserPii);
            UpdatePiiHashes(ChangeTracker.Entries<Customer>(), NormalizeCustomerPii);
            UpdatePiiHashes(ChangeTracker.Entries<Shop>(), NormalizeShopPii);
      }

      private void UpdatePiiHashes<T>(IEnumerable<EntityEntry<T>> entries, Action<T> normalize) where T : class
      {
            foreach (var entry in entries)
            {
                  if (entry.State is EntityState.Added or EntityState.Modified)
                  {
                        normalize(entry.Entity);
                  }
            }
      }

      private void NormalizeUserPii(User user)
      {
            NormalizeContactFields(user.Email, user.Phone,
                  normalized => user.Email = normalized,
                  hash => user.EmailHash = hash,
                  normalized => user.Phone = normalized,
                  hash => user.PhoneHash = hash);
      }

      private void NormalizeCustomerPii(Customer customer)
      {
            NormalizeContactFields(customer.Email, customer.Phone,
                  normalized => customer.Email = normalized,
                  hash => customer.EmailHash = hash,
                  normalized => customer.Phone = normalized,
                  hash => customer.PhoneHash = hash);
      }

      private void NormalizeShopPii(Shop shop)
      {
            NormalizeContactFields(shop.Email, shop.Phone,
                  normalized => shop.Email = normalized,
                  hash => shop.EmailHash = hash,
                  normalized => shop.Phone = normalized,
                  hash => shop.PhoneHash = hash);
      }

      private void NormalizeContactFields(
            string? email,
            string? phone,
            Action<string?> setEmail,
            Action<string?> setEmailHash,
            Action<string?> setPhone,
            Action<string?> setPhoneHash)
      {
            var normalizedEmail = string.IsNullOrWhiteSpace(email) ? null : email.Trim();
            setEmail(normalizedEmail);
            setEmailHash(_emailSecurity.ComputeHash(normalizedEmail));

            var normalizedPhone = string.IsNullOrWhiteSpace(phone) ? null : phone.Trim();
            setPhone(normalizedPhone);
            setPhoneHash(_emailSecurity.ComputePhoneHash(normalizedPhone));
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
    public DbSet<AdjustmentTypeConfig> AdjustmentTypeConfigs => Set<AdjustmentTypeConfig>();
    public DbSet<Notification> Notifications => Set<Notification>();
    public DbSet<AuditLog> AuditLogs => Set<AuditLog>();
            public DbSet<PasswordResetToken> PasswordResetTokens => Set<PasswordResetToken>();
    public DbSet<AccountingEntry> AccountingEntries => Set<AccountingEntry>();
    public DbSet<XeroSyncLog> XeroSyncLogs => Set<XeroSyncLog>();
    public DbSet<XeroConnection> XeroConnections => Set<XeroConnection>();
    public DbSet<InvoiceDiscount> InvoiceDiscounts => Set<InvoiceDiscount>();

    // ── SuperAdmin Module ────────────────────────────────────────────────
    public DbSet<SubscriptionPlan> SubscriptionPlans => Set<SubscriptionPlan>();
    public DbSet<Subscription> Subscriptions => Set<Subscription>();
    public DbSet<SubscriptionPayment> SubscriptionPayments => Set<SubscriptionPayment>();
    public DbSet<PlatformSetting> PlatformSettings => Set<PlatformSetting>();
    public DbSet<Announcement> Announcements => Set<Announcement>();
    public DbSet<SuperAdminAuditLog> SuperAdminAuditLogs => Set<SuperAdminAuditLog>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        base.OnModelCreating(modelBuilder);

            var piiConverter = new ValueConverter<string?, string?>(
                  value => _emailSecurity.Encrypt(value),
                  value => _emailSecurity.Decrypt(value));

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
            entity.Property(e => e.Email).HasMaxLength(512).HasConversion(piiConverter);
            entity.Property(e => e.EmailHash).HasMaxLength(64);
            entity.Property(e => e.Phone).HasMaxLength(512).HasConversion(piiConverter);
            entity.Property(e => e.PhoneHash).HasMaxLength(64);
            entity.Property(e => e.Address).HasMaxLength(255);
            entity.Property(e => e.Status).HasMaxLength(20).IsRequired().HasDefaultValue("Active");
            entity.Property(e => e.DefaultPartMarkupPct).HasPrecision(18, 2).HasDefaultValue(0m);
            entity.Property(e => e.TIN).HasMaxLength(20);
            entity.Property(e => e.IsVatRegistered).HasDefaultValue(true);
            entity.Property(e => e.TaxRate).HasPrecision(18, 2).HasDefaultValue(12m);
            entity.Property(e => e.IsDefault).HasDefaultValue(false);
            entity.Property(e => e.CreatedAt).HasColumnType("datetime2(0)").HasDefaultValueSql("SYSDATETIME()");
            entity.Property(e => e.UpdatedAt).HasColumnType("datetime2(0)");
            entity.HasIndex(e => e.ShopCode).IsUnique();
                  entity.HasIndex(e => e.EmailHash);
                  entity.HasIndex(e => e.PhoneHash);
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
            entity.Property(e => e.Email).HasMaxLength(512).HasConversion(piiConverter);
            entity.Property(e => e.EmailHash).HasMaxLength(64);
            entity.Property(e => e.Phone).HasMaxLength(512).HasConversion(piiConverter);
            entity.Property(e => e.PhoneHash).HasMaxLength(64);
            entity.Property(e => e.ThemePreference).HasMaxLength(10).HasDefaultValue("light");
            entity.Property(e => e.EmailNotifications).HasDefaultValue(true);
            entity.Property(e => e.InAppNotifications).HasDefaultValue(true);
            entity.Property(e => e.IsActive).HasDefaultValue(true);
            entity.Property(e => e.AuthVersion).HasDefaultValue(1);
            entity.Property(e => e.FailedLoginAttempts).HasDefaultValue(0);
            entity.Property(e => e.LockoutEndAt).HasColumnType("datetime2(0)");
            entity.Property(e => e.LockoutCycleCount).HasDefaultValue(0);
            entity.Property(e => e.IsPermanentlyLocked).HasDefaultValue(false);
            entity.Property(e => e.PermanentlyLockedAt).HasColumnType("datetime2(0)");
            entity.Property(e => e.LockoutReason).HasMaxLength(200);
            entity.Property(e => e.LastFailedLoginAt).HasColumnType("datetime2(0)");
            entity.Property(e => e.IsMfaEnabled).HasDefaultValue(false);
            entity.Property(e => e.MfaType).HasMaxLength(20);
            entity.Property(e => e.TotpSecretKey).HasMaxLength(256);
            entity.Property(e => e.EmailOtpHash).HasMaxLength(128);
            entity.Property(e => e.EmailOtpExpiresAt).HasColumnType("datetime2(0)");
            entity.Property(e => e.EmailOtpFailedAttempts).HasDefaultValue(0);
            entity.Property(e => e.LastMfaAt).HasColumnType("datetime2(0)");
            entity.Property(e => e.MustChangePassword).HasDefaultValue(false);
            entity.Property(e => e.TemporaryPasswordIssuedAt).HasColumnType("datetime2(0)");
            entity.Property(e => e.LastLoginAt).HasColumnType("datetime2(0)");
            entity.Property(e => e.LastIpAddress).HasMaxLength(50);
            entity.Property(e => e.CreatedAt).HasColumnType("datetime2(0)").HasDefaultValueSql("SYSDATETIME()");
            entity.Property(e => e.UpdatedAt).HasColumnType("datetime2(0)");
            entity.HasIndex(e => new { e.ShopId, e.UserName }).IsUnique();
            entity.HasIndex(e => e.ShopId);
            entity.HasIndex(e => e.EmailHash);
            entity.HasIndex(e => e.PhoneHash);
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
            entity.Property(e => e.Email).HasMaxLength(512).HasConversion(piiConverter);
            entity.Property(e => e.EmailHash).HasMaxLength(64);
            entity.Property(e => e.Phone).HasMaxLength(512).HasConversion(piiConverter);
            entity.Property(e => e.PhoneHash).HasMaxLength(64);
            entity.Property(e => e.Address).HasMaxLength(255);
            entity.Property(e => e.IsActive).HasDefaultValue(true);
            entity.Property(e => e.CreatedAt).HasColumnType("datetime2(0)").HasDefaultValueSql("SYSDATETIME()");
            entity.HasIndex(e => e.ShopId);
            entity.HasIndex(e => new { e.ShopId, e.EmailHash });
            entity.HasIndex(e => new { e.ShopId, e.PhoneHash });
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
            entity.Property(e => e.CatalogPrice).HasPrecision(18, 2).HasDefaultValue(0m);
            entity.Property(e => e.IsPriceOverride).HasDefaultValue(false);
            entity.Property(e => e.OverrideReason).HasMaxLength(255);
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
            entity.Property(e => e.CatalogPrice).HasPrecision(18, 2).HasDefaultValue(0m);
            entity.Property(e => e.IsPriceOverride).HasDefaultValue(false);
            entity.Property(e => e.OverrideReason).HasMaxLength(255);
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
            entity.Property(e => e.DiscountAmount).HasPrecision(18, 2).HasDefaultValue(0m);
            entity.Property(e => e.VatableSales).HasPrecision(18, 2).HasDefaultValue(0m);
            entity.Property(e => e.VatExemptSales).HasPrecision(18, 2).HasDefaultValue(0m);
            entity.Property(e => e.ZeroRatedSales).HasPrecision(18, 2).HasDefaultValue(0m);
            entity.Property(e => e.VatAmount).HasPrecision(18, 2).HasDefaultValue(0m);
            entity.Property(e => e.AmountPaid).HasPrecision(18, 2).HasDefaultValue(0m);
            entity.Property(e => e.Balance).HasPrecision(18, 2).HasDefaultValue(0m);
            entity.Property(e => e.Status).HasMaxLength(20).IsRequired().HasDefaultValue(InvoiceStatus.Unpaid)
                  .HasConversion<string>();
            entity.Property(e => e.CreatedAt).HasColumnType("datetime2(0)").HasDefaultValueSql("SYSDATETIME()");
            entity.Property(e => e.DueDate).HasColumnType("datetime2(0)");
            entity.Property(e => e.IsArchived).HasDefaultValue(false);
            entity.Property(e => e.ArchivedDate).HasColumnType("datetime2(0)");
            entity.Property(e => e.RowVersion).IsRowVersion();
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
            entity.Property(e => e.CatalogPrice).HasPrecision(18, 2).HasDefaultValue(0m);
            entity.Property(e => e.IsPriceOverride).HasDefaultValue(false);
            entity.Property(e => e.OverrideReason).HasMaxLength(255);
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
        // T1b. ADJUSTMENT_TYPE_CONFIG
        // ═══════════════════════════════════════════════════════════════
        modelBuilder.Entity<AdjustmentTypeConfig>(entity =>
        {
            entity.ToTable("ADJUSTMENT_TYPE_CONFIG");
            entity.HasKey(e => e.AdjustmentTypeConfigId);
            entity.Property(e => e.AdjustmentTypeConfigId).HasColumnName("AdjustmentTypeConfigID");
            entity.Property(e => e.ShopId).HasColumnName("ShopID");
            entity.Property(e => e.Name).HasMaxLength(100).IsRequired();
            entity.Property(e => e.Category).HasMaxLength(20).IsRequired();
            entity.Property(e => e.Percentage).HasColumnType("decimal(5,2)");
            entity.Property(e => e.IsActive).HasDefaultValue(true);
            entity.Property(e => e.CreatedAt).HasColumnType("datetime2(0)").HasDefaultValueSql("SYSDATETIME()");
            entity.Property(e => e.UpdatedAt).HasColumnType("datetime2(0)");

            entity.HasOne(e => e.Shop)
                  .WithMany()
                  .HasForeignKey(e => e.ShopId)
                  .OnDelete(DeleteBehavior.Cascade);
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
            // U1. PASSWORD_RESET_TOKENS
            // ═══════════════════════════════════════════════════════════════
            modelBuilder.Entity<PasswordResetToken>(entity =>
            {
                  entity.ToTable("PASSWORD_RESET_TOKENS");
                  entity.HasKey(e => e.PasswordResetTokenId);
                  entity.Property(e => e.PasswordResetTokenId).HasColumnName("PasswordResetTokenID");
                  entity.Property(e => e.UserId).HasColumnName("UserID");
                  entity.Property(e => e.TokenHash).HasMaxLength(128).IsRequired();
                  entity.Property(e => e.ExpiresAt).HasColumnType("datetime2(0)");
                  entity.Property(e => e.UsedAt).HasColumnType("datetime2(0)");
                  entity.Property(e => e.RequestedIp).HasMaxLength(45);
                  entity.Property(e => e.CreatedAt).HasColumnType("datetime2(0)").HasDefaultValueSql("SYSDATETIME()");

                  entity.HasIndex(e => e.TokenHash).IsUnique();
                  entity.HasIndex(e => new { e.UserId, e.CreatedAt });

                  entity.HasOne(e => e.User)
                          .WithMany(u => u.PasswordResetTokens)
                          .HasForeignKey(e => e.UserId)
                          .OnDelete(DeleteBehavior.Cascade);
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

        // ═══════════════════════════════════════════════════════════════
        // X. XERO_CONNECTION
        // ═══════════════════════════════════════════════════════════════
        modelBuilder.Entity<XeroConnection>(entity =>
        {
            entity.ToTable("XERO_CONNECTION");
            entity.HasKey(e => e.XeroConnectionId);
            entity.Property(e => e.XeroConnectionId).HasColumnName("XeroConnectionID");
            entity.Property(e => e.ShopId).HasColumnName("ShopID");
            entity.Property(e => e.XeroTenantId).HasMaxLength(80).IsRequired();
            entity.Property(e => e.TenantName).HasMaxLength(150);
            entity.Property(e => e.AccessToken).HasMaxLength(2048).IsRequired();
            entity.Property(e => e.RefreshToken).HasMaxLength(2048).IsRequired();
            entity.Property(e => e.TokenExpiresAt).HasColumnType("datetime2(0)");
            entity.Property(e => e.ConnectedAt).HasColumnType("datetime2(0)").HasDefaultValueSql("SYSDATETIME()");
            entity.Property(e => e.IsActive).HasDefaultValue(true);
            entity.HasIndex(e => e.ShopId);

            entity.HasOne(e => e.Shop)
                  .WithMany(s => s.XeroConnections)
                  .HasForeignKey(e => e.ShopId)
                  .OnDelete(DeleteBehavior.NoAction);
        });

        // ═══════════════════════════════════════════════════════════════
        // Y. INVOICE_DISCOUNT  (BIR SC/PWD/Promo discounts)
        // ═══════════════════════════════════════════════════════════════
        modelBuilder.Entity<InvoiceDiscount>(entity =>
        {
            entity.ToTable("INVOICE_DISCOUNT");
            entity.HasKey(e => e.InvoiceDiscountId);
            entity.Property(e => e.InvoiceDiscountId).HasColumnName("InvoiceDiscountID");
            entity.Property(e => e.InvoiceId).HasColumnName("InvoiceID");
            entity.Property(e => e.DiscountType)
                  .HasConversion<string>()
                  .HasMaxLength(20)
                  .IsRequired();
            entity.Property(e => e.Label).HasMaxLength(120).IsRequired();
            entity.Property(e => e.Percentage).HasColumnType("decimal(18,2)").HasDefaultValue(0m);
            entity.Property(e => e.Amount).HasColumnType("decimal(18,2)").HasDefaultValue(0m);
            entity.Property(e => e.IsVatExempt).HasDefaultValue(false);
            entity.Property(e => e.BeneficiaryIdNo).HasMaxLength(30);
            entity.Property(e => e.BeneficiaryName).HasMaxLength(120);
            entity.Property(e => e.AppliedByUserId).HasColumnName("AppliedByUserID");
            entity.Property(e => e.AppliedAt).HasColumnType("datetime2(0)").HasDefaultValueSql("SYSDATETIME()");

            entity.HasIndex(e => e.InvoiceId);
            entity.HasIndex(e => e.AppliedByUserId);

            entity.HasOne(e => e.Invoice)
                  .WithMany(i => i.InvoiceDiscounts)
                  .HasForeignKey(e => e.InvoiceId)
                  .OnDelete(DeleteBehavior.Cascade);

            entity.HasOne(e => e.AppliedByUser)
                  .WithMany()
                  .HasForeignKey(e => e.AppliedByUserId)
                  .OnDelete(DeleteBehavior.NoAction);
        });

        // ═══════════════════════════════════════════════════════════════
        // Z1. SUBSCRIPTION_PLANS
        // ═══════════════════════════════════════════════════════════════
        modelBuilder.Entity<SubscriptionPlan>(entity =>
        {
            entity.ToTable("SUBSCRIPTION_PLANS");
            entity.HasKey(e => e.PlanId);
            entity.Property(e => e.PlanId).HasColumnName("PlanID");
            entity.Property(e => e.PlanName).HasMaxLength(100).IsRequired();
            entity.Property(e => e.Description).HasMaxLength(500);
            entity.Property(e => e.MonthlyPrice).HasPrecision(18, 2);
            entity.Property(e => e.YearlyPrice).HasPrecision(18, 2);
            entity.Property(e => e.PermanentPrice).HasPrecision(18, 2);
            entity.Property(e => e.IsActive).HasDefaultValue(true);
            entity.Property(e => e.CreatedAt).HasColumnType("datetime2(0)").HasDefaultValueSql("SYSDATETIME()");
            entity.Property(e => e.UpdatedAt).HasColumnType("datetime2(0)");
        });

        // ═══════════════════════════════════════════════════════════════
        // Z2. SUBSCRIPTIONS
        // ═══════════════════════════════════════════════════════════════
        modelBuilder.Entity<Subscription>(entity =>
        {
            entity.ToTable("SUBSCRIPTIONS");
            entity.HasKey(e => e.SubscriptionId);
            entity.Property(e => e.SubscriptionId).HasColumnName("SubscriptionID");
            entity.Property(e => e.ShopId).HasColumnName("ShopID");
            entity.Property(e => e.PlanId).HasColumnName("PlanID");
            entity.Property(e => e.BillingCycle).HasMaxLength(20).IsRequired();
            entity.Property(e => e.Status).HasMaxLength(20).IsRequired().HasDefaultValue("Active");
            entity.Property(e => e.Price).HasPrecision(18, 2);
            entity.Property(e => e.StartDate).HasColumnType("datetime2(0)");
            entity.Property(e => e.EndDate).HasColumnType("datetime2(0)");
            entity.Property(e => e.NextBillingDate).HasColumnType("datetime2(0)");
            entity.Property(e => e.CancelledAt).HasColumnType("datetime2(0)");
            entity.Property(e => e.IsDefault).HasDefaultValue(false);
            entity.Property(e => e.CreatedAt).HasColumnType("datetime2(0)").HasDefaultValueSql("SYSDATETIME()");
            entity.Property(e => e.UpdatedAt).HasColumnType("datetime2(0)");
            entity.HasIndex(e => e.ShopId);
            entity.HasIndex(e => e.PlanId);
            entity.HasIndex(e => e.Status);

            entity.HasOne(e => e.Shop)
                  .WithMany(s => s.Subscriptions)
                  .HasForeignKey(e => e.ShopId)
                  .OnDelete(DeleteBehavior.NoAction);

            entity.HasOne(e => e.Plan)
                  .WithMany(p => p.Subscriptions)
                  .HasForeignKey(e => e.PlanId)
                  .OnDelete(DeleteBehavior.NoAction);
        });

        // ═══════════════════════════════════════════════════════════════
        // Z3. SUBSCRIPTION_PAYMENTS
        // ═══════════════════════════════════════════════════════════════
        modelBuilder.Entity<SubscriptionPayment>(entity =>
        {
            entity.ToTable("SUBSCRIPTION_PAYMENTS");
            entity.HasKey(e => e.SubscriptionPaymentId);
            entity.Property(e => e.SubscriptionPaymentId).HasColumnName("SubscriptionPaymentID");
            entity.Property(e => e.SubscriptionId).HasColumnName("SubscriptionID");
            entity.Property(e => e.ShopId).HasColumnName("ShopID");
            entity.Property(e => e.Amount).HasPrecision(18, 2);
            entity.Property(e => e.Currency).HasMaxLength(10).HasDefaultValue("PHP");
            entity.Property(e => e.Status).HasMaxLength(20).IsRequired().HasDefaultValue("Pending");
            entity.Property(e => e.PaymentMethod).HasMaxLength(50);
            entity.Property(e => e.ReferenceNumber).HasMaxLength(50).IsRequired();
            entity.Property(e => e.PayMongoPaymentId).HasMaxLength(200);
            entity.Property(e => e.PayMongoCheckoutUrl).HasMaxLength(500);
            entity.Property(e => e.PeriodStart).HasColumnType("datetime2(0)");
            entity.Property(e => e.PeriodEnd).HasColumnType("datetime2(0)");
            entity.Property(e => e.Notes).HasMaxLength(500);
            entity.Property(e => e.CreatedAt).HasColumnType("datetime2(0)").HasDefaultValueSql("SYSDATETIME()");
            entity.Property(e => e.PaidAt).HasColumnType("datetime2(0)");
            entity.HasIndex(e => e.SubscriptionId);
            entity.HasIndex(e => e.ShopId);
            entity.HasIndex(e => e.Status);
            entity.HasIndex(e => e.ReferenceNumber);

            entity.HasOne(e => e.Subscription)
                  .WithMany(s => s.Payments)
                  .HasForeignKey(e => e.SubscriptionId)
                  .OnDelete(DeleteBehavior.NoAction);

            entity.HasOne(e => e.Shop)
                  .WithMany(s => s.SubscriptionPayments)
                  .HasForeignKey(e => e.ShopId)
                  .OnDelete(DeleteBehavior.NoAction);
        });

        // ═══════════════════════════════════════════════════════════════
        // Z4. PLATFORM_SETTINGS
        // ═══════════════════════════════════════════════════════════════
        modelBuilder.Entity<PlatformSetting>(entity =>
        {
            entity.ToTable("PLATFORM_SETTINGS");
            entity.HasKey(e => e.SettingId);
            entity.Property(e => e.SettingId).HasColumnName("SettingID");
            entity.Property(e => e.SettingKey).HasMaxLength(100).IsRequired();
            entity.Property(e => e.SettingValue).IsRequired();
            entity.Property(e => e.Category).HasMaxLength(50).HasDefaultValue("General");
            entity.Property(e => e.Description).HasMaxLength(300);
            entity.Property(e => e.UpdatedAt).HasColumnType("datetime2(0)").HasDefaultValueSql("SYSDATETIME()");
            entity.Property(e => e.UpdatedBy).HasMaxLength(100);
            entity.HasIndex(e => e.SettingKey).IsUnique();
        });

        // ═══════════════════════════════════════════════════════════════
        // Z5. ANNOUNCEMENTS
        // ═══════════════════════════════════════════════════════════════
        modelBuilder.Entity<Announcement>(entity =>
        {
            entity.ToTable("ANNOUNCEMENTS");
            entity.HasKey(e => e.AnnouncementId);
            entity.Property(e => e.AnnouncementId).HasColumnName("AnnouncementID");
            entity.Property(e => e.Title).HasMaxLength(200).IsRequired();
            entity.Property(e => e.Content).IsRequired();
            entity.Property(e => e.Type).HasMaxLength(20).HasDefaultValue("Info");
            entity.Property(e => e.Status).HasMaxLength(20).HasDefaultValue("Draft");
            entity.Property(e => e.PublishedAt).HasColumnType("datetime2(0)");
            entity.Property(e => e.ExpiresAt).HasColumnType("datetime2(0)");
            entity.Property(e => e.CreatedByUserId).HasColumnName("CreatedByUserID");
            entity.Property(e => e.CreatedAt).HasColumnType("datetime2(0)").HasDefaultValueSql("SYSDATETIME()");
            entity.Property(e => e.UpdatedAt).HasColumnType("datetime2(0)");

            entity.HasOne(e => e.CreatedBy)
                  .WithMany()
                  .HasForeignKey(e => e.CreatedByUserId)
                  .OnDelete(DeleteBehavior.NoAction);
        });

        // ═══════════════════════════════════════════════════════════════
        // Z6. SUPERADMIN_AUDIT_LOG
        // ═══════════════════════════════════════════════════════════════
        modelBuilder.Entity<SuperAdminAuditLog>(entity =>
        {
            entity.ToTable("SUPERADMIN_AUDIT_LOG");
            entity.HasKey(e => e.AuditId);
            entity.Property(e => e.AuditId).HasColumnName("AuditID");
            entity.Property(e => e.UserId).HasColumnName("UserID");
            entity.Property(e => e.Action).HasMaxLength(100).IsRequired();
            entity.Property(e => e.EntityType).HasMaxLength(50);
            entity.Property(e => e.EntityId).HasColumnName("EntityID");
            entity.Property(e => e.Details);
            entity.Property(e => e.IpAddress).HasMaxLength(50);
            entity.Property(e => e.Timestamp).HasColumnType("datetime2(0)").HasDefaultValueSql("SYSDATETIME()");
            entity.HasIndex(e => e.UserId);
            entity.HasIndex(e => e.Timestamp).IsDescending(true);

            entity.HasOne(e => e.User)
                  .WithMany()
                  .HasForeignKey(e => e.UserId)
                  .OnDelete(DeleteBehavior.NoAction);
        });
    }
}
