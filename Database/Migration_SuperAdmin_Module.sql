-- ═══════════════════════════════════════════════════════════════════════════
--  ByteBill SuperAdmin Module — Database Migration
--  Creates: SubscriptionPlans, Subscriptions, SubscriptionPayments,
--           PlatformSettings, Announcements, SuperAdminAuditLog
--  Alters:  SHOP (IsDefault), USERS (LastLoginAt, LastIpAddress)
--  Seeds:   3 subscription plans, default subscription for Main Shop,
--           default platform settings
-- ═══════════════════════════════════════════════════════════════════════════

SET XACT_ABORT ON;
BEGIN TRANSACTION;

-- ──────────────────────────────────────────────────────────────────────────
-- 1. ALTER SHOP — add IsDefault flag
-- ──────────────────────────────────────────────────────────────────────────
IF NOT EXISTS (SELECT 1 FROM sys.columns WHERE object_id = OBJECT_ID('SHOP') AND name = 'IsDefault')
BEGIN
    ALTER TABLE [SHOP] ADD [IsDefault] BIT NOT NULL CONSTRAINT DF_SHOP_IsDefault DEFAULT 0;
    -- Use EXEC so the column reference is deferred past the ALTER
    EXEC sp_executesql N'UPDATE [SHOP] SET [IsDefault] = 1 WHERE [ShopCode] = N''MAIN''';
END 

-- ──────────────────────────────────────────────────────────────────────────
-- 2. ALTER USERS — add LastLoginAt, LastIpAddress
-- ──────────────────────────────────────────────────────────────────────────
IF NOT EXISTS (SELECT 1 FROM sys.columns WHERE object_id = OBJECT_ID('USERS') AND name = 'LastLoginAt')
BEGIN
    ALTER TABLE [USERS] ADD [LastLoginAt] DATETIME2(0) NULL;
END

IF NOT EXISTS (SELECT 1 FROM sys.columns WHERE object_id = OBJECT_ID('USERS') AND name = 'LastIpAddres  s')
BEGIN
    ALTER TABLE [USERS] ADD [LastIpAddress] NVARCHAR(50) NULL;
END

-- ──────────────────────────────────────────────────────────────────────────
-- 3. SUBSCRIPTION_PLANS
-- ──────────────────────────────────────────────────────────────────────────
IF NOT EXISTS (SELECT 1 FROM sys.objects WHERE object_id = OBJECT_ID('SUBSCRIPTION_PLANS') AND type = 'U')
BEGIN
    CREATE TABLE [SUBSCRIPTION_PLANS] (
        [PlanID]               BIGINT IDENTITY(1,1) NOT NULL,
        [PlanName]             NVARCHAR(100)   NOT NULL,
        [Description]          NVARCHAR(500)   NULL,
        [MonthlyPrice]         DECIMAL(18,2)   NOT NULL,
        [YearlyPrice]          DECIMAL(18,2)   NOT NULL,
        [PermanentPrice]       DECIMAL(18,2)   NOT NULL,
        [MaxUsers]             INT             NOT NULL DEFAULT 0,
        [MaxCustomers]         INT             NOT NULL DEFAULT 0,
        [MaxJobOrdersPerMonth] INT             NOT NULL DEFAULT 0,
        [HasXeroIntegration]   BIT             NOT NULL DEFAULT 0,
        [HasPrioritySupport]   BIT             NOT NULL DEFAULT 0,
        [HasAdvancedReports]   BIT             NOT NULL DEFAULT 0,
        [SortOrder]            INT             NOT NULL DEFAULT 0,
        [IsActive]             BIT             NOT NULL DEFAULT 1,
        [CreatedAt]            DATETIME2(0)    NOT NULL DEFAULT SYSDATETIME(),
        [UpdatedAt]            DATETIME2(0)    NULL,
        CONSTRAINT [PK_SUBSCRIPTION_PLANS] PRIMARY KEY ([PlanID])
    );
END

-- ──────────────────────────────────────────────────────────────────────────
-- 4. SUBSCRIPTIONS
-- ──────────────────────────────────────────────────────────────────────────
IF NOT EXISTS (SELECT 1 FROM sys.objects WHERE object_id = OBJECT_ID('SUBSCRIPTIONS') AND type = 'U')
BEGIN
    CREATE TABLE [SUBSCRIPTIONS] (
        [SubscriptionID]  BIGINT IDENTITY(1,1) NOT NULL,
        [ShopID]          BIGINT          NOT NULL,
        [PlanID]          BIGINT          NOT NULL,
        [BillingCycle]    NVARCHAR(20)    NOT NULL DEFAULT 'Monthly',
        [Status]          NVARCHAR(20)    NOT NULL DEFAULT 'Active',
        [Price]           DECIMAL(18,2)   NOT NULL,
        [StartDate]       DATETIME2(0)    NOT NULL,
        [EndDate]         DATETIME2(0)    NULL,
        [NextBillingDate] DATETIME2(0)    NULL,
        [CancelledAt]     DATETIME2(0)    NULL,
        [IsDefault]       BIT             NOT NULL DEFAULT 0,
        [CreatedAt]       DATETIME2(0)    NOT NULL DEFAULT SYSDATETIME(),
        [UpdatedAt]       DATETIME2(0)    NULL,
        CONSTRAINT [PK_SUBSCRIPTIONS] PRIMARY KEY ([SubscriptionID]),
        CONSTRAINT [FK_SUBSCRIPTIONS_SHOP] FOREIGN KEY ([ShopID]) REFERENCES [SHOP]([ShopID]),
        CONSTRAINT [FK_SUBSCRIPTIONS_PLAN] FOREIGN KEY ([PlanID]) REFERENCES [SUBSCRIPTION_PLANS]([PlanID])
    );

    CREATE INDEX [IX_SUBSCRIPTIONS_ShopID] ON [SUBSCRIPTIONS]([ShopID]);
    CREATE INDEX [IX_SUBSCRIPTIONS_PlanID] ON [SUBSCRIPTIONS]([PlanID]);
    CREATE INDEX [IX_SUBSCRIPTIONS_Status] ON [SUBSCRIPTIONS]([Status]);
END

-- ──────────────────────────────────────────────────────────────────────────
-- 5. SUBSCRIPTION_PAYMENTS
-- ──────────────────────────────────────────────────────────────────────────
IF NOT EXISTS (SELECT 1 FROM sys.objects WHERE object_id = OBJECT_ID('SUBSCRIPTION_PAYMENTS') AND type = 'U')
BEGIN
    CREATE TABLE [SUBSCRIPTION_PAYMENTS] (
        [SubscriptionPaymentID] BIGINT IDENTITY(1,1) NOT NULL,
        [SubscriptionID]        BIGINT          NOT NULL,
        [ShopID]                BIGINT          NOT NULL,
        [Amount]                DECIMAL(18,2)   NOT NULL,
        [Currency]              NVARCHAR(10)    NOT NULL DEFAULT 'PHP',
        [Status]                NVARCHAR(20)    NOT NULL DEFAULT 'Pending',
        [PaymentMethod]         NVARCHAR(50)    NULL,
        [ReferenceNumber]       NVARCHAR(50)    NOT NULL,
        [PayMongoPaymentId]     NVARCHAR(200)   NULL,
        [PayMongoCheckoutUrl]   NVARCHAR(500)   NULL,
        [PeriodStart]           DATETIME2(0)    NOT NULL,
        [PeriodEnd]             DATETIME2(0)    NOT NULL,
        [Notes]                 NVARCHAR(500)   NULL,
        [CreatedAt]             DATETIME2(0)    NOT NULL DEFAULT SYSDATETIME(),
        [PaidAt]                DATETIME2(0)    NULL,
        CONSTRAINT [PK_SUBSCRIPTION_PAYMENTS] PRIMARY KEY ([SubscriptionPaymentID]),
        CONSTRAINT [FK_SUBPAY_SUBSCRIPTION] FOREIGN KEY ([SubscriptionID]) REFERENCES [SUBSCRIPTIONS]([SubscriptionID]),
        CONSTRAINT [FK_SUBPAY_SHOP] FOREIGN KEY ([ShopID]) REFERENCES [SHOP]([ShopID])
    );

    CREATE INDEX [IX_SUBPAY_SubscriptionID] ON [SUBSCRIPTION_PAYMENTS]([SubscriptionID]);
    CREATE INDEX [IX_SUBPAY_ShopID] ON [SUBSCRIPTION_PAYMENTS]([ShopID]);
    CREATE INDEX [IX_SUBPAY_Status] ON [SUBSCRIPTION_PAYMENTS]([Status]);
    CREATE INDEX [IX_SUBPAY_ReferenceNumber] ON [SUBSCRIPTION_PAYMENTS]([ReferenceNumber]);
END

-- ──────────────────────────────────────────────────────────────────────────
-- 6. PLATFORM_SETTINGS
-- ──────────────────────────────────────────────────────────────────────────
IF NOT EXISTS (SELECT 1 FROM sys.objects WHERE object_id = OBJECT_ID('PLATFORM_SETTINGS') AND type = 'U')
BEGIN
    CREATE TABLE [PLATFORM_SETTINGS] (
        [SettingID]    BIGINT IDENTITY(1,1) NOT NULL,
        [SettingKey]   NVARCHAR(100) NOT NULL,
        [SettingValue] NVARCHAR(MAX) NOT NULL,
        [Category]     NVARCHAR(50)  NOT NULL DEFAULT 'General',
        [Description]  NVARCHAR(300) NULL,
        [UpdatedAt]    DATETIME2(0)  NOT NULL DEFAULT SYSDATETIME(),
        [UpdatedBy]    NVARCHAR(100) NULL,
        CONSTRAINT [PK_PLATFORM_SETTINGS] PRIMARY KEY ([SettingID])
    );

    CREATE UNIQUE INDEX [UX_PLATFORM_SETTINGS_Key] ON [PLATFORM_SETTINGS]([SettingKey]);
END

-- ──────────────────────────────────────────────────────────────────────────
-- 7. ANNOUNCEMENTS
-- ──────────────────────────────────────────────────────────────────────────
IF NOT EXISTS (SELECT 1 FROM sys.objects WHERE object_id = OBJECT_ID('ANNOUNCEMENTS') AND type = 'U')
BEGIN
    CREATE TABLE [ANNOUNCEMENTS] (
        [AnnouncementID]  BIGINT IDENTITY(1,1) NOT NULL,
        [Title]           NVARCHAR(200)  NOT NULL,
        [Content]         NVARCHAR(MAX)  NOT NULL,
        [Type]            NVARCHAR(20)   NOT NULL DEFAULT 'Info',
        [Status]          NVARCHAR(20)   NOT NULL DEFAULT 'Draft',
        [PublishedAt]     DATETIME2(0)   NULL,
        [ExpiresAt]       DATETIME2(0)   NULL,
        [CreatedByUserId] BIGINT         NOT NULL,
        [CreatedAt]       DATETIME2(0)   NOT NULL DEFAULT SYSDATETIME(),
        [UpdatedAt]       DATETIME2(0)   NULL,
        CONSTRAINT [PK_ANNOUNCEMENTS] PRIMARY KEY ([AnnouncementID]),
        CONSTRAINT [FK_ANNOUNCEMENTS_USER] FOREIGN KEY ([CreatedByUserId]) REFERENCES [USERS]([UserID])
    );
END

-- ──────────────────────────────────────────────────────────────────────────
-- 8. SUPERADMIN_AUDIT_LOG
-- ──────────────────────────────────────────────────────────────────────────
IF NOT EXISTS (SELECT 1 FROM sys.objects WHERE object_id = OBJECT_ID('SUPERADMIN_AUDIT_LOG') AND type = 'U')
BEGIN
    CREATE TABLE [SUPERADMIN_AUDIT_LOG] (
        [AuditID]     BIGINT IDENTITY(1,1) NOT NULL,
        [UserID]      BIGINT         NOT NULL,
        [Action]      NVARCHAR(100)  NOT NULL,
        [EntityType]  NVARCHAR(50)   NULL,
        [EntityID]    BIGINT         NULL,
        [Details]     NVARCHAR(MAX)  NULL,
        [IpAddress]   NVARCHAR(50)   NULL,
        [Timestamp]   DATETIME2(0)   NOT NULL DEFAULT SYSDATETIME(),
        CONSTRAINT [PK_SUPERADMIN_AUDIT_LOG] PRIMARY KEY ([AuditID]),
        CONSTRAINT [FK_SA_AUDIT_USER] FOREIGN KEY ([UserID]) REFERENCES [USERS]([UserID])
    );

    CREATE INDEX [IX_SA_AUDIT_UserID] ON [SUPERADMIN_AUDIT_LOG]([UserID]);
    CREATE INDEX [IX_SA_AUDIT_Timestamp] ON [SUPERADMIN_AUDIT_LOG]([Timestamp] DESC);
END

-- ══════════════════════════════════════════════════════════════════════════
-- SEED DATA
-- ══════════════════════════════════════════════════════════════════════════

-- 9. Seed Subscription Plans
IF NOT EXISTS (SELECT 1 FROM [SUBSCRIPTION_PLANS])
BEGIN
    INSERT INTO [SUBSCRIPTION_PLANS]
        ([PlanName], [Description], [MonthlyPrice], [YearlyPrice], [PermanentPrice],
         [MaxUsers], [MaxCustomers], [MaxJobOrdersPerMonth],
         [HasXeroIntegration], [HasPrioritySupport], [HasAdvancedReports], [SortOrder])
    VALUES
        ('Basic',
         'For small repair shops getting started. Includes essential billing, job orders, and inventory management.',
         999.00, 9590.40, 35964.00,
         3, 50, 100,
         0, 0, 0, 1),

        ('Professional',
         'For growing shops. Unlimited customers, Xero integration, and advanced reporting.',
         2499.00, 23990.40, 89964.00,
         10, 0, 0,
         1, 0, 1, 2),

        ('Enterprise',
         'For large operations. Unlimited everything with priority support and all integrations.',
         4999.00, 47990.40, 179964.00,
         0, 0, 0,
         1, 1, 1, 3);
END

-- 10. Seed default subscription for ByteBill Main Shop
IF NOT EXISTS (SELECT 1 FROM [SUBSCRIPTIONS])
BEGIN
    DECLARE @MainShopId BIGINT = (SELECT TOP 1 [ShopID] FROM [SHOP] WHERE [ShopCode] = 'MAIN');
    DECLARE @EnterprisePlanId BIGINT = (SELECT TOP 1 [PlanID] FROM [SUBSCRIPTION_PLANS] WHERE [PlanName] = 'Enterprise');

    IF @MainShopId IS NOT NULL AND @EnterprisePlanId IS NOT NULL
    BEGIN
        INSERT INTO [SUBSCRIPTIONS]
            ([ShopID], [PlanID], [BillingCycle], [Status], [Price], [StartDate], [EndDate], [NextBillingDate], [IsDefault])
        VALUES
            (@MainShopId, @EnterprisePlanId, 'Permanent', 'Active', 0.00, SYSDATETIME(), NULL, NULL, 1);
    END
END

-- 11. Seed default platform settings
IF NOT EXISTS (SELECT 1 FROM [PLATFORM_SETTINGS])
BEGIN
    INSERT INTO [PLATFORM_SETTINGS] ([SettingKey], [SettingValue], [Category], [Description])
    VALUES
        ('General.PlatformName',     'ByteBill',                   'General',  'Platform display name'),
        ('General.Tagline',          'A Web-Based Billing System', 'General',  'Platform tagline'),
        ('General.Currency',         'PHP',                        'General',  'Default currency code'),
        ('General.Timezone',         'Asia/Manila',                'General',  'Default timezone'),
        ('General.DateFormat',       'MMM dd, yyyy',               'General',  'Date display format'),
        ('Tax.DefaultVatRate',       '12',                         'Tax',      'Default VAT rate for new shops (%)'),
        ('Tax.DefaultIsVatRegistered', 'true',                     'Tax',      'Default VAT registration for new shops'),
        ('Security.MinPasswordLength', '6',                        'Security', 'Minimum password length'),
        ('Security.RequireUppercase',  'true',                     'Security', 'Require uppercase in passwords'),
        ('Security.RequireNumbers',    'true',                     'Security', 'Require numbers in passwords'),
        ('Security.SessionTimeout',    '60',                       'Security', 'Session timeout in minutes'),
        ('Security.MaxLoginAttempts',  '5',                        'Security', 'Max failed login attempts before lockout'),
        ('Email.SmtpHost',            'smtp.gmail.com',            'Email',    'SMTP server host'),
        ('Email.SmtpPort',            '587',                       'Email',    'SMTP server port'),
        ('Email.SmtpUseSsl',          'true',                      'Email',    'Use SSL for SMTP'),
        ('Email.FromEmail',           'noreply@bytebill.ph',       'Email',    'Sender email address'),
        ('Email.FromName',            'ByteBill System',           'Email',    'Sender display name'),
        ('Email.EnableNotifications',  'true',                     'Email',    'Enable email notifications'),
        ('PayMongo.TestMode',          'true',                     'PayMongo', 'Use PayMongo test/sandbox mode'),
        ('Subscription.TrialDays',     '14',                       'Subscription', 'Free trial period in days');
END

COMMIT TRANSACTION;
PRINT 'SuperAdmin module migration completed successfully.';
