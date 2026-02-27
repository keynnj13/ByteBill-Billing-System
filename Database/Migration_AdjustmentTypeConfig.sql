-- ╔═══════════════════════════════════════════════════════════════════╗
-- ║ Migration: Adjustment Type Config                                ║
-- ║ Adds ADJUSTMENT_TYPE_CONFIG table for admin-configurable         ║
-- ║ adjustment types with percentages per shop.                      ║
-- ╚═══════════════════════════════════════════════════════════════════╝

USE ByteBillDB;
GO

-- ── Create ADJUSTMENT_TYPE_CONFIG table ──────────────────────────────
IF NOT EXISTS (SELECT 1 FROM INFORMATION_SCHEMA.TABLES WHERE TABLE_NAME = 'ADJUSTMENT_TYPE_CONFIG')
BEGIN
    CREATE TABLE [dbo].[ADJUSTMENT_TYPE_CONFIG] (
        [AdjustmentTypeConfigID] BIGINT IDENTITY(1,1) NOT NULL,
        [ShopID]      BIGINT         NOT NULL,
        [Name]        NVARCHAR(100)  NOT NULL,
        [Category]    NVARCHAR(20)   NOT NULL DEFAULT 'Credit',
        [Percentage]  DECIMAL(5,2)   NOT NULL DEFAULT 0,
        [IsActive]    BIT            NOT NULL DEFAULT 1,
        [CreatedAt]   DATETIME2(0)   NOT NULL DEFAULT SYSDATETIME(),
        [UpdatedAt]   DATETIME2(0)   NULL,

        CONSTRAINT [PK_ADJUSTMENT_TYPE_CONFIG] PRIMARY KEY ([AdjustmentTypeConfigID]),
        CONSTRAINT [FK_ADJUSTMENT_TYPE_CONFIG_SHOP] FOREIGN KEY ([ShopID]) REFERENCES [SHOP]([ShopID]) ON DELETE CASCADE
    );

    PRINT 'Created ADJUSTMENT_TYPE_CONFIG table.';
END
GO

-- ── Seed default adjustment types (ShopId = 1) ──────────────────────
-- These are common defaults; admin can modify via UI
IF NOT EXISTS (SELECT 1 FROM [ADJUSTMENT_TYPE_CONFIG] WHERE [ShopID] = 1)
BEGIN
    INSERT INTO [ADJUSTMENT_TYPE_CONFIG] ([ShopID], [Name], [Category], [Percentage])
    VALUES
        (1, 'Senior Citizen Discount', 'Credit', 20.00),
        (1, 'PWD Discount',            'Credit', 20.00),
        (1, 'Loyalty Discount',        'Credit', 10.00),
        (1, 'Anniversary Discount',    'Credit', 15.00),
        (1, 'Regular Discount',        'Credit',  5.00),
        (1, 'Refund - Unit Damage',    'Refund',100.00),
        (1, 'Refund - Misdiagnosis',   'Refund',100.00),
        (1, 'Refund - Overcharge',     'Refund',100.00),
        (1, 'Additional Charge',       'Debit',   0.00);

    PRINT 'Seeded default adjustment type configs.';
END
GO
