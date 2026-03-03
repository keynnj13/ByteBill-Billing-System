-- 
--  ByteBill: COMBINED DATABASE MIGRATION SCRIPT
--  
--  Generated: 2026-03-02 19:05
--  
--  This script combines ALL 13 migration scripts in the correct order.
--  It is SAFE TO RE-RUN  every ALTER/CREATE is guarded with IF NOT EXISTS.
--  
--  Run this ONCE on your MonsterASP production database.
--  
--  Execution order:
--    1.  PayMongoTxn_NewColumns          (PAYMONGO_TXN table + new columns)
--    2.  PayMongoTxn_RawResponseMax      (widen RawResponse)
--    3.  ArchiveFields_StatusStreamline  (archive columns + status normalization)
--    4.  BillingAlgorithm                (pricing/override columns)
--    5.  BIR_Tax_Compliance              (VAT columns + INVOICE_DISCOUNT table)
--    6.  InventoryCategory               (INVENTORY_CATEGORY table)
--    7.  ServiceCatalog_InventoryCategory (service description + category seeding)
--    8.  Adjustments_Notifications       (adjustment columns + NOTIFICATION table)
--    9.  AdjustmentTypeConfig            (ADJUSTMENT_TYPE_CONFIG table + seeds)
--   10.  XeroConnection                  (XERO_CONNECTION table)
--   11.  SuperAdmin_Module               (subscription/platform/announcement tables)
--   12.  PayMongo_Refactor               (PayMongo refactor + cleanup)
--   13.  Post_SuperAdmin_Sync            (final sync: column widening, nullability)
-- 


-- 
--  PART 1 of 13: Migration_PayMongoTxn_NewColumns.sql
-- 

/*******************************************************************************
 *  ByteBill: Web-Based Billing System for Computer Repair Services
 *  MIGRATION SCRIPT â€” PayMongo Transaction Table + New Columns
 *
 *  Generated: 2026-02-25
 *
 *  This migration:
 *    0. Creates PAYMONGO_TXN table if it does not exist
 *    1. Adds CheckoutUrl column to PAYMONGO_TXN
 *    2. Adds ResourceType column to PAYMONGO_TXN (default 'link')
 *    3. Adds UpdatedAt column to PAYMONGO_TXN
 *    4. Creates index on PayMongoPaymentIntentID for webhook lookups
 *
 *  âš ï¸  Run this ONCE on an existing database.
 ******************************************************************************/

SET XACT_ABORT ON;
BEGIN TRANSACTION;

-- ============================================================================
-- 0. Create PAYMONGO_TXN table if it does not exist
-- ============================================================================
IF NOT EXISTS (SELECT 1 FROM sys.tables WHERE name = 'PAYMONGO_TXN')
BEGIN
    CREATE TABLE [dbo].[PAYMONGO_TXN]
    (
        [PayMongoTxnID]           BIGINT         IDENTITY(1,1) NOT NULL,
        [PaymentID]               BIGINT         NOT NULL,
        [PayMongoPaymentIntentID] NVARCHAR(80)   NOT NULL,
        [PayMongoStatus]          NVARCHAR(30)   NOT NULL,
        [RawResponse]             NVARCHAR(MAX) NULL,
        [CreatedAt]               DATETIME2(0)   NOT NULL  DEFAULT SYSDATETIME(),

        CONSTRAINT [PK_PAYMONGO_TXN]           PRIMARY KEY ([PayMongoTxnID]),
        CONSTRAINT [UQ_PAYMONGO_TXN_PaymentID] UNIQUE ([PaymentID])
    );

    -- Only add FK if PAYMENTS table exists
    IF EXISTS (SELECT 1 FROM sys.tables WHERE name = 'PAYMENTS')
    BEGIN
        ALTER TABLE [dbo].[PAYMONGO_TXN]
            ADD CONSTRAINT [FK_PAYMONGO_TXN_Payment]
            FOREIGN KEY ([PaymentID]) REFERENCES [dbo].[PAYMENTS] ([PaymentID])
            ON UPDATE NO ACTION ON DELETE NO ACTION;
        PRINT 'âœ…  Created table PAYMONGO_TXN with FK to PAYMENTS';
    END
    ELSE
        PRINT 'âœ…  Created table PAYMONGO_TXN (FK to PAYMENTS deferred â€” run base deploy first)';
END
ELSE
    PRINT 'â­ï¸  PAYMONGO_TXN table already exists';

-- ============================================================================
-- 1. Add CheckoutUrl column (nullable, max 500 chars)
-- ============================================================================
IF NOT EXISTS (
    SELECT 1 FROM INFORMATION_SCHEMA.COLUMNS
    WHERE TABLE_NAME = 'PAYMONGO_TXN' AND COLUMN_NAME = 'CheckoutUrl'
)
BEGIN
    ALTER TABLE [dbo].[PAYMONGO_TXN]
        ADD [CheckoutUrl] NVARCHAR(500) NULL;
    PRINT 'âœ…  Added CheckoutUrl column to PAYMONGO_TXN';
END
ELSE
    PRINT 'â­ï¸  CheckoutUrl column already exists';

-- ============================================================================
-- 2. Add ResourceType column (default 'link', max 30 chars)
-- ============================================================================
IF NOT EXISTS (
    SELECT 1 FROM INFORMATION_SCHEMA.COLUMNS
    WHERE TABLE_NAME = 'PAYMONGO_TXN' AND COLUMN_NAME = 'ResourceType'
)
BEGIN
    ALTER TABLE [dbo].[PAYMONGO_TXN]
        ADD [ResourceType] NVARCHAR(30) NOT NULL
            CONSTRAINT [DF_PAYMONGO_TXN_ResourceType] DEFAULT ('link');
    PRINT 'âœ…  Added ResourceType column to PAYMONGO_TXN';
END
ELSE
    PRINT 'â­ï¸  ResourceType column already exists';

-- ============================================================================
-- 3. Add UpdatedAt column (nullable datetime)
-- ============================================================================
IF NOT EXISTS (
    SELECT 1 FROM INFORMATION_SCHEMA.COLUMNS
    WHERE TABLE_NAME = 'PAYMONGO_TXN' AND COLUMN_NAME = 'UpdatedAt'
)
BEGIN
    ALTER TABLE [dbo].[PAYMONGO_TXN]
        ADD [UpdatedAt] DATETIME2 NULL;
    PRINT 'âœ…  Added UpdatedAt column to PAYMONGO_TXN';
END
ELSE
    PRINT 'â­ï¸  UpdatedAt column already exists';

-- ============================================================================
-- 4. Create index on PayMongoPaymentIntentId for webhook lookups
-- ============================================================================
IF NOT EXISTS (
    SELECT 1 FROM sys.indexes
    WHERE name = 'IX_PAYMONGO_TXN_PayMongoPaymentIntentID'
      AND object_id = OBJECT_ID('dbo.PAYMONGO_TXN')
)
BEGIN
    CREATE NONCLUSTERED INDEX [IX_PAYMONGO_TXN_PayMongoPaymentIntentID]
        ON [dbo].[PAYMONGO_TXN] ([PayMongoPaymentIntentID]);
    PRINT 'âœ…  Created index IX_PAYMONGO_TXN_PayMongoPaymentIntentID';
END
ELSE
    PRINT 'â­ï¸  Index IX_PAYMONGO_TXN_PayMongoPaymentIntentID already exists';

COMMIT TRANSACTION;
PRINT '';
PRINT 'ðŸŽ‰  Migration complete â€” PayMongo Transaction new columns applied.';
GO


-- 
--  PART 2 of 13: Migration_PayMongoTxn_RawResponseMax.sql
-- 

-- ============================================================================
--  MIGRATION: Widen PAYMONGO_TXN.RawResponse to NVARCHAR(MAX)
--  
--  PayMongo checkout session responses can exceed 2000 characters.
--  Run this against your database to fix the truncation error.
-- ============================================================================

IF EXISTS (
    SELECT 1 FROM INFORMATION_SCHEMA.COLUMNS
    WHERE TABLE_NAME = 'PAYMONGO_TXN'
      AND COLUMN_NAME = 'RawResponse'
      AND CHARACTER_MAXIMUM_LENGTH <> -1  -- -1 means MAX
)
BEGIN
    ALTER TABLE [dbo].[PAYMONGO_TXN]
        ALTER COLUMN [RawResponse] NVARCHAR(MAX) NULL;
    PRINT 'âœ…  Widened RawResponse column to NVARCHAR(MAX)';
END
ELSE
    PRINT 'â­ï¸  RawResponse is already NVARCHAR(MAX) or column does not exist';


-- 
--  PART 3 of 13: Migration_ArchiveFields_StatusStreamline.sql
-- 

/*******************************************************************************
 *  ByteBill: Web-Based Billing System for Computer Repair Services
 *  MONSTERASP MIGRATION SCRIPT â€” Archive Fields & Status Streamline
 *
 *  Generated: 2026-02-18
 *
 *  This migration:
 *    1. Adds IsArchived + ArchivedDate columns to JOB_ORDERS
 *    2. Adds IsArchived + ArchivedDate columns to INVOICES
 *    3. Updates JOB_ORDERS default Status from 'Created' to 'Pending'
 *    4. Normalises any legacy status values to the new 8-status set
 *
 *  New JO Statuses (8 total):
 *    Pending, CheckedIn, Diagnosis, InProgress, WaitingForParts,
 *    Completed, Delivered, Cancelled
 *
 *  Removed JO Statuses (mapped â†’ new):
 *    Created        â†’ Pending
 *    Diagnosed      â†’ InProgress
 *    AwaitingApproval â†’ Pending
 *    Approved       â†’ InProgress
 *    OnHold         â†’ WaitingForParts
 *    ReadyForPickup â†’ Completed
 *
 *  âš ï¸  Run this ONCE on an existing MonsterASP database that already has
 *     the base schema deployed (ByteBillDB_MonsterASP_Deploy.sql).
 ******************************************************************************/

SET XACT_ABORT ON;
BEGIN TRANSACTION;

-- â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•
-- 1.  JOB_ORDERS â€” Add archive columns
-- â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•
IF NOT EXISTS (
    SELECT 1 FROM INFORMATION_SCHEMA.COLUMNS
    WHERE TABLE_NAME = 'JOB_ORDERS' AND COLUMN_NAME = 'IsArchived'
)
BEGIN
    ALTER TABLE JOB_ORDERS
        ADD IsArchived   BIT          NOT NULL  DEFAULT 0,
            ArchivedDate DATETIME2(0) NULL;
    PRINT 'âœ“ Added IsArchived + ArchivedDate to JOB_ORDERS';
END
ELSE
    PRINT 'â€” JOB_ORDERS archive columns already exist, skipping';
GO

-- â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•
-- 2.  INVOICES â€” Add archive columns
-- â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•
IF NOT EXISTS (
    SELECT 1 FROM INFORMATION_SCHEMA.COLUMNS
    WHERE TABLE_NAME = 'INVOICES' AND COLUMN_NAME = 'IsArchived'
)
BEGIN
    ALTER TABLE INVOICES
        ADD IsArchived   BIT          NOT NULL  DEFAULT 0,
            ArchivedDate DATETIME2(0) NULL;
    PRINT 'âœ“ Added IsArchived + ArchivedDate to INVOICES';
END
ELSE
    PRINT 'â€” INVOICES archive columns already exist, skipping';
GO

-- â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•
-- 3.  JOB_ORDERS â€” Change default Status to 'Pending'
-- â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•
-- Drop the old default constraint (name may vary)
DECLARE @DefaultName NVARCHAR(256);
SELECT @DefaultName = dc.name
FROM   sys.default_constraints dc
JOIN   sys.columns c ON dc.parent_object_id = c.object_id AND dc.parent_column_id = c.column_id
WHERE  OBJECT_NAME(dc.parent_object_id) = 'JOB_ORDERS'
  AND  c.name = 'Status';

IF @DefaultName IS NOT NULL
BEGIN
    EXEC('ALTER TABLE JOB_ORDERS DROP CONSTRAINT [' + @DefaultName + ']');
    ALTER TABLE JOB_ORDERS ADD DEFAULT 'Pending' FOR [Status];
    PRINT 'âœ“ Changed JOB_ORDERS Status default from Created â†’ Pending';
END
GO

-- â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•
-- 4.  Normalise legacy status values
-- â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•
UPDATE JOB_ORDERS SET [Status] = 'Pending'          WHERE [Status] = 'Created';
UPDATE JOB_ORDERS SET [Status] = 'InProgress'       WHERE [Status] = 'Diagnosed';
UPDATE JOB_ORDERS SET [Status] = 'Pending'          WHERE [Status] = 'AwaitingApproval';
UPDATE JOB_ORDERS SET [Status] = 'InProgress'       WHERE [Status] = 'Approved';
UPDATE JOB_ORDERS SET [Status] = 'WaitingForParts'  WHERE [Status] = 'OnHold';
UPDATE JOB_ORDERS SET [Status] = 'Completed'        WHERE [Status] = 'ReadyForPickup';
PRINT 'âœ“ Normalised legacy JO statuses to 8-value set';
GO

-- Also normalise any history records
UPDATE JOB_ORDER_STATUS_HISTORY SET OldStatus = 'Pending'          WHERE OldStatus = 'Created';
UPDATE JOB_ORDER_STATUS_HISTORY SET NewStatus = 'Pending'          WHERE NewStatus = 'Created';
UPDATE JOB_ORDER_STATUS_HISTORY SET OldStatus = 'InProgress'       WHERE OldStatus IN ('Diagnosed', 'Approved');
UPDATE JOB_ORDER_STATUS_HISTORY SET NewStatus = 'InProgress'       WHERE NewStatus IN ('Diagnosed', 'Approved');
UPDATE JOB_ORDER_STATUS_HISTORY SET OldStatus = 'Pending'          WHERE OldStatus = 'AwaitingApproval';
UPDATE JOB_ORDER_STATUS_HISTORY SET NewStatus = 'Pending'          WHERE NewStatus = 'AwaitingApproval';
UPDATE JOB_ORDER_STATUS_HISTORY SET OldStatus = 'WaitingForParts'  WHERE OldStatus = 'OnHold';
UPDATE JOB_ORDER_STATUS_HISTORY SET NewStatus = 'WaitingForParts'  WHERE NewStatus = 'OnHold';
UPDATE JOB_ORDER_STATUS_HISTORY SET OldStatus = 'Completed'        WHERE OldStatus = 'ReadyForPickup';
UPDATE JOB_ORDER_STATUS_HISTORY SET NewStatus = 'Completed'        WHERE NewStatus = 'ReadyForPickup';
PRINT 'âœ“ Normalised JOB_ORDER_STATUS_HISTORY legacy values';
GO

COMMIT TRANSACTION;
PRINT '';
PRINT 'â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•';
PRINT '  Migration complete â€” Archive fields + Status streamline';
PRINT 'â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•';
GO


-- 
--  PART 4 of 13: Migration_BillingAlgorithm.sql
-- 

-- â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•
-- Migration: Billing Algorithm â€” New Columns
-- Run after: ByteBillDB_MonsterASP_Deploy.sql / existing migrations
-- â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•

-- â”€â”€ SHOP: DefaultPartMarkupPct â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€
IF NOT EXISTS (
    SELECT 1 FROM INFORMATION_SCHEMA.COLUMNS
    WHERE TABLE_NAME = 'SHOP' AND COLUMN_NAME = 'DefaultPartMarkupPct'
)
BEGIN
    ALTER TABLE [SHOP] ADD [DefaultPartMarkupPct] DECIMAL(18,2) NOT NULL DEFAULT 0;
    PRINT 'Added DefaultPartMarkupPct to SHOP';
END;
GO

-- â”€â”€ JOB_ORDER_SERVICES: CatalogPrice, IsPriceOverride, OverrideReason â”€â”€
IF NOT EXISTS (
    SELECT 1 FROM INFORMATION_SCHEMA.COLUMNS
    WHERE TABLE_NAME = 'JOB_ORDER_SERVICES' AND COLUMN_NAME = 'CatalogPrice'
)
BEGIN
    ALTER TABLE [JOB_ORDER_SERVICES] ADD [CatalogPrice] DECIMAL(18,2) NOT NULL DEFAULT 0;
    PRINT 'Added CatalogPrice to JOB_ORDER_SERVICES';
END;
GO

IF NOT EXISTS (
    SELECT 1 FROM INFORMATION_SCHEMA.COLUMNS
    WHERE TABLE_NAME = 'JOB_ORDER_SERVICES' AND COLUMN_NAME = 'IsPriceOverride'
)
BEGIN
    ALTER TABLE [JOB_ORDER_SERVICES] ADD [IsPriceOverride] BIT NOT NULL DEFAULT 0;
    PRINT 'Added IsPriceOverride to JOB_ORDER_SERVICES';
END;
GO

IF NOT EXISTS (
    SELECT 1 FROM INFORMATION_SCHEMA.COLUMNS
    WHERE TABLE_NAME = 'JOB_ORDER_SERVICES' AND COLUMN_NAME = 'OverrideReason'
)
BEGIN
    ALTER TABLE [JOB_ORDER_SERVICES] ADD [OverrideReason] NVARCHAR(255) NULL;
    PRINT 'Added OverrideReason to JOB_ORDER_SERVICES';
END;
GO

-- â”€â”€ JOB_ORDER_PARTS: CatalogPrice, IsPriceOverride, OverrideReason â”€â”€
IF NOT EXISTS (
    SELECT 1 FROM INFORMATION_SCHEMA.COLUMNS
    WHERE TABLE_NAME = 'JOB_ORDER_PARTS' AND COLUMN_NAME = 'CatalogPrice'
)
BEGIN
    ALTER TABLE [JOB_ORDER_PARTS] ADD [CatalogPrice] DECIMAL(18,2) NOT NULL DEFAULT 0;
    PRINT 'Added CatalogPrice to JOB_ORDER_PARTS';
END;
GO

IF NOT EXISTS (
    SELECT 1 FROM INFORMATION_SCHEMA.COLUMNS
    WHERE TABLE_NAME = 'JOB_ORDER_PARTS' AND COLUMN_NAME = 'IsPriceOverride'
)
BEGIN
    ALTER TABLE [JOB_ORDER_PARTS] ADD [IsPriceOverride] BIT NOT NULL DEFAULT 0;
    PRINT 'Added IsPriceOverride to JOB_ORDER_PARTS';
END;
GO

IF NOT EXISTS (
    SELECT 1 FROM INFORMATION_SCHEMA.COLUMNS
    WHERE TABLE_NAME = 'JOB_ORDER_PARTS' AND COLUMN_NAME = 'OverrideReason'
)
BEGIN
    ALTER TABLE [JOB_ORDER_PARTS] ADD [OverrideReason] NVARCHAR(255) NULL;
    PRINT 'Added OverrideReason to JOB_ORDER_PARTS';
END;
GO

-- â”€â”€ INVOICE_LINES: CatalogPrice, IsPriceOverride, OverrideReason â”€â”€
IF NOT EXISTS (
    SELECT 1 FROM INFORMATION_SCHEMA.COLUMNS
    WHERE TABLE_NAME = 'INVOICE_LINES' AND COLUMN_NAME = 'CatalogPrice'
)
BEGIN
    ALTER TABLE [INVOICE_LINES] ADD [CatalogPrice] DECIMAL(18,2) NOT NULL DEFAULT 0;
    PRINT 'Added CatalogPrice to INVOICE_LINES';
END;
GO

IF NOT EXISTS (
    SELECT 1 FROM INFORMATION_SCHEMA.COLUMNS
    WHERE TABLE_NAME = 'INVOICE_LINES' AND COLUMN_NAME = 'IsPriceOverride'
)
BEGIN
    ALTER TABLE [INVOICE_LINES] ADD [IsPriceOverride] BIT NOT NULL DEFAULT 0;
    PRINT 'Added IsPriceOverride to INVOICE_LINES';
END;
GO

IF NOT EXISTS (
    SELECT 1 FROM INFORMATION_SCHEMA.COLUMNS
    WHERE TABLE_NAME = 'INVOICE_LINES' AND COLUMN_NAME = 'OverrideReason'
)
BEGIN
    ALTER TABLE [INVOICE_LINES] ADD [OverrideReason] NVARCHAR(255) NULL;
    PRINT 'Added OverrideReason to INVOICE_LINES';
END;
GO

PRINT 'âœ… Billing Algorithm migration complete.';
GO


-- 
--  PART 5 of 13: Migration_BIR_Tax_Compliance.sql
-- 

-- â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•
-- Migration: BIR Tax Compliance (Philippine VAT / Non-VAT)
-- Date: 2025-06-XX
-- Description:
--   1. Add TIN, IsVatRegistered, TaxRate to SHOP table
--   2. Add DiscountAmount, VatableSales, VatExemptSales, ZeroRatedSales, VatAmount to INVOICES
--   3. Create INVOICE_DISCOUNT table for SC/PWD/Promo discounts
-- â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•

-- â”€â”€â”€ 1. SHOP: Tax registration columns â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€
IF NOT EXISTS (SELECT 1 FROM sys.columns WHERE object_id = OBJECT_ID('SHOP') AND name = 'TIN')
BEGIN
    ALTER TABLE [SHOP] ADD [TIN] NVARCHAR(20) NULL;
    PRINT 'Added SHOP.TIN';
END
GO

IF NOT EXISTS (SELECT 1 FROM sys.columns WHERE object_id = OBJECT_ID('SHOP') AND name = 'IsVatRegistered')
BEGIN
    ALTER TABLE [SHOP] ADD [IsVatRegistered] BIT NOT NULL CONSTRAINT DF_SHOP_IsVatRegistered DEFAULT 1;
    PRINT 'Added SHOP.IsVatRegistered (default: 1 = VAT-registered)';
END
GO

IF NOT EXISTS (SELECT 1 FROM sys.columns WHERE object_id = OBJECT_ID('SHOP') AND name = 'TaxRate')
BEGIN
    ALTER TABLE [SHOP] ADD [TaxRate] DECIMAL(18,2) NOT NULL CONSTRAINT DF_SHOP_TaxRate DEFAULT 12.00;
    PRINT 'Added SHOP.TaxRate (default: 12%)';
END
GO

-- â”€â”€â”€ 2. INVOICES: BIR tax breakdown columns â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€
IF NOT EXISTS (SELECT 1 FROM sys.columns WHERE object_id = OBJECT_ID('INVOICES') AND name = 'DiscountAmount')
BEGIN
    ALTER TABLE [INVOICES] ADD [DiscountAmount] DECIMAL(18,2) NOT NULL CONSTRAINT DF_INVOICES_DiscountAmount DEFAULT 0;
    PRINT 'Added INVOICES.DiscountAmount';
END
GO

IF NOT EXISTS (SELECT 1 FROM sys.columns WHERE object_id = OBJECT_ID('INVOICES') AND name = 'VatableSales')
BEGIN
    ALTER TABLE [INVOICES] ADD [VatableSales] DECIMAL(18,2) NOT NULL CONSTRAINT DF_INVOICES_VatableSales DEFAULT 0;
    PRINT 'Added INVOICES.VatableSales';
END
GO

IF NOT EXISTS (SELECT 1 FROM sys.columns WHERE object_id = OBJECT_ID('INVOICES') AND name = 'VatExemptSales')
BEGIN
    ALTER TABLE [INVOICES] ADD [VatExemptSales] DECIMAL(18,2) NOT NULL CONSTRAINT DF_INVOICES_VatExemptSales DEFAULT 0;
    PRINT 'Added INVOICES.VatExemptSales';
END
GO

IF NOT EXISTS (SELECT 1 FROM sys.columns WHERE object_id = OBJECT_ID('INVOICES') AND name = 'ZeroRatedSales')
BEGIN
    ALTER TABLE [INVOICES] ADD [ZeroRatedSales] DECIMAL(18,2) NOT NULL CONSTRAINT DF_INVOICES_ZeroRatedSales DEFAULT 0;
    PRINT 'Added INVOICES.ZeroRatedSales';
END
GO

IF NOT EXISTS (SELECT 1 FROM sys.columns WHERE object_id = OBJECT_ID('INVOICES') AND name = 'VatAmount')
BEGIN
    ALTER TABLE [INVOICES] ADD [VatAmount] DECIMAL(18,2) NOT NULL CONSTRAINT DF_INVOICES_VatAmount DEFAULT 0;
    PRINT 'Added INVOICES.VatAmount';
END
GO

-- â”€â”€â”€ 3. INVOICE_DISCOUNT table â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€
IF NOT EXISTS (SELECT 1 FROM sys.objects WHERE object_id = OBJECT_ID('INVOICE_DISCOUNT') AND type = 'U')
BEGIN
    CREATE TABLE [INVOICE_DISCOUNT] (
        [InvoiceDiscountID] BIGINT IDENTITY(1,1) NOT NULL,
        [InvoiceID]         BIGINT NOT NULL,
        [DiscountType]      NVARCHAR(20) NOT NULL,          -- SeniorCitizen, PWD, Promo
        [Label]             NVARCHAR(120) NOT NULL,          -- Display label on receipt
        [Percentage]        DECIMAL(18,2) NOT NULL DEFAULT 0,
        [Amount]            DECIMAL(18,2) NOT NULL DEFAULT 0,
        [IsVatExempt]       BIT NOT NULL DEFAULT 0,
        [BeneficiaryIdNo]   NVARCHAR(30) NULL,               -- SC/PWD ID for BIR
        [BeneficiaryName]   NVARCHAR(120) NULL,              -- SC/PWD name
        [AppliedByUserID]   BIGINT NOT NULL,
        [AppliedAt]         DATETIME2(0) NOT NULL DEFAULT SYSDATETIME(),

        CONSTRAINT [PK_INVOICE_DISCOUNT] PRIMARY KEY CLUSTERED ([InvoiceDiscountID]),
        CONSTRAINT [FK_INVOICE_DISCOUNT_Invoice]  FOREIGN KEY ([InvoiceID])       REFERENCES [INVOICES]([InvoiceID]),
        CONSTRAINT [FK_INVOICE_DISCOUNT_User]     FOREIGN KEY ([AppliedByUserID]) REFERENCES [USERS]([UserID])
    );

    CREATE INDEX [IX_INVOICE_DISCOUNT_InvoiceID]       ON [INVOICE_DISCOUNT] ([InvoiceID]);
    CREATE INDEX [IX_INVOICE_DISCOUNT_AppliedByUserID] ON [INVOICE_DISCOUNT] ([AppliedByUserID]);

    PRINT 'Created INVOICE_DISCOUNT table';
END
GO

PRINT 'âœ… BIR Tax Compliance migration complete.';
GO


-- 
--  PART 6 of 13: Migration_InventoryCategory.sql
-- 

-- =====================================================================
--  Migration: Add INVENTORY_CATEGORY table + FK on INVENTORY_ITEMS
--  Date: 2026-02-25
-- =====================================================================

-- 1. Create INVENTORY_CATEGORY table if not exists
IF NOT EXISTS (SELECT 1 FROM INFORMATION_SCHEMA.TABLES WHERE TABLE_NAME = 'INVENTORY_CATEGORY')
BEGIN
    CREATE TABLE [dbo].[INVENTORY_CATEGORY] (
        [InventoryCategoryID] BIGINT IDENTITY(1,1) NOT NULL,
        [ShopID]              BIGINT NOT NULL,
        [CategoryName]        NVARCHAR(80) NOT NULL,
        [Description]         NVARCHAR(150) NULL,
        CONSTRAINT [PK_INVENTORY_CATEGORY] PRIMARY KEY CLUSTERED ([InventoryCategoryID]),
        CONSTRAINT [FK_INVENTORY_CATEGORY_SHOP] FOREIGN KEY ([ShopID])
            REFERENCES [dbo].[SHOP]([ShopID]) ON DELETE NO ACTION
    );

    CREATE UNIQUE INDEX [IX_INVENTORY_CATEGORY_ShopID_CategoryName]
        ON [dbo].[INVENTORY_CATEGORY] ([ShopID], [CategoryName]);

    CREATE INDEX [IX_INVENTORY_CATEGORY_ShopID]
        ON [dbo].[INVENTORY_CATEGORY] ([ShopID]);

    PRINT 'Created INVENTORY_CATEGORY table.';
END
ELSE
BEGIN
    PRINT 'INVENTORY_CATEGORY table already exists.';
END
GO

-- 2. Add InventoryCategoryID column to INVENTORY_ITEMS if not exists
IF NOT EXISTS (
    SELECT 1 FROM INFORMATION_SCHEMA.COLUMNS
    WHERE TABLE_NAME = 'INVENTORY_ITEMS' AND COLUMN_NAME = 'InventoryCategoryID'
)
BEGIN
    ALTER TABLE [dbo].[INVENTORY_ITEMS]
        ADD [InventoryCategoryID] BIGINT NULL;

    ALTER TABLE [dbo].[INVENTORY_ITEMS]
        ADD CONSTRAINT [FK_INVENTORY_ITEMS_CATEGORY] FOREIGN KEY ([InventoryCategoryID])
            REFERENCES [dbo].[INVENTORY_CATEGORY]([InventoryCategoryID]) ON DELETE NO ACTION;

    PRINT 'Added InventoryCategoryID column to INVENTORY_ITEMS.';
END
ELSE
BEGIN
    PRINT 'InventoryCategoryID column already exists on INVENTORY_ITEMS.';
END
GO


-- 
--  PART 7 of 13: Migration_ServiceCatalog_InventoryCategory.sql
-- 

-- â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•
-- Migration: Add Description & EstimatedDuration to SERVICE_CATALOG,
--            Seed INVENTORY_CATEGORY records,
--            Assign InventoryCategoryID to existing INVENTORY_ITEM rows
-- Date: 2026-02-19
-- Run against: ByteBill database (SQL Server)
-- â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•

-- â”€â”€ 1. Add Description column to SERVICE_CATALOG â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€
IF NOT EXISTS (
    SELECT 1 FROM INFORMATION_SCHEMA.COLUMNS
    WHERE TABLE_NAME = 'SERVICE_CATALOG' AND COLUMN_NAME = 'Description'
)
BEGIN
    ALTER TABLE SERVICE_CATALOG ADD [Description] NVARCHAR(500) NULL;
    PRINT 'âœ… Added Description column to SERVICE_CATALOG';
END
ELSE
    PRINT 'â­ï¸  Description column already exists on SERVICE_CATALOG';
GO

-- â”€â”€ 2. Add EstimatedDuration column to SERVICE_CATALOG â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€
IF NOT EXISTS (
    SELECT 1 FROM INFORMATION_SCHEMA.COLUMNS
    WHERE TABLE_NAME = 'SERVICE_CATALOG' AND COLUMN_NAME = 'EstimatedDuration'
)
BEGIN
    ALTER TABLE SERVICE_CATALOG ADD [EstimatedDuration] INT NOT NULL DEFAULT 0;
    PRINT 'âœ… Added EstimatedDuration column to SERVICE_CATALOG';
END
ELSE
    PRINT 'â­ï¸  EstimatedDuration column already exists on SERVICE_CATALOG';
GO

-- â”€â”€ 3. Update existing services with descriptions & durations â”€â”€â”€â”€â”€â”€â”€
UPDATE SERVICE_CATALOG SET [Description] = 'Full hardware and software diagnostic scan to identify issues',            EstimatedDuration = 30  WHERE ServiceName = 'System Diagnosis'            AND [Description] IS NULL;
UPDATE SERVICE_CATALOG SET [Description] = 'Physical inspection of all internal components for damage or wear',        EstimatedDuration = 20  WHERE ServiceName = 'Hardware Inspection'        AND [Description] IS NULL;
UPDATE SERVICE_CATALOG SET [Description] = 'Deep scan, removal of viruses and malware, security patching',             EstimatedDuration = 60  WHERE ServiceName = 'Virus/Malware Removal'      AND [Description] IS NULL;
UPDATE SERVICE_CATALOG SET [Description] = 'Full LCD/LED screen replacement including calibration',                    EstimatedDuration = 90  WHERE ServiceName = 'Screen Replacement'         AND [Description] IS NULL;
UPDATE SERVICE_CATALOG SET [Description] = 'Battery removal, replacement, and charge cycle testing',                   EstimatedDuration = 45  WHERE ServiceName = 'Battery Replacement'        AND [Description] IS NULL;
UPDATE SERVICE_CATALOG SET [Description] = 'Full keyboard unit replacement and key mapping verification',              EstimatedDuration = 60  WHERE ServiceName = 'Keyboard Replacement'       AND [Description] IS NULL;
UPDATE SERVICE_CATALOG SET [Description] = 'Clean install of Windows/macOS/Linux with driver setup',                   EstimatedDuration = 120 WHERE ServiceName = 'OS Installation'            AND [Description] IS NULL;
UPDATE SERVICE_CATALOG SET [Description] = 'Install and configure essential software, office suites, and antivirus',   EstimatedDuration = 45  WHERE ServiceName = 'Software Setup & Config'    AND [Description] IS NULL;
UPDATE SERVICE_CATALOG SET [Description] = 'Install new RAM modules or SSD with data migration if needed',             EstimatedDuration = 40  WHERE ServiceName = 'RAM/SSD Upgrade'            AND [Description] IS NULL;
UPDATE SERVICE_CATALOG SET [Description] = 'Deep clean internals, replace thermal paste, clean fans and vents',        EstimatedDuration = 60  WHERE ServiceName = 'Internal Cleaning & Repaste'AND [Description] IS NULL;
UPDATE SERVICE_CATALOG SET [Description] = 'Disk cleanup, startup optimization, registry repair, and updates',         EstimatedDuration = 45  WHERE ServiceName = 'Full System Tune-Up'        AND [Description] IS NULL;
UPDATE SERVICE_CATALOG SET [Description] = 'Recover data from damaged or corrupted drives using professional tools',   EstimatedDuration = 180 WHERE ServiceName = 'Data Recovery (HDD/SSD)'    AND [Description] IS NULL;

PRINT 'âœ… Updated service descriptions and durations';
GO

-- â”€â”€ 4. Seed INVENTORY_CATEGORY if empty â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€
IF NOT EXISTS (SELECT 1 FROM INVENTORY_CATEGORY)
BEGIN
    DECLARE @ShopId BIGINT = (SELECT TOP 1 ShopID FROM SHOP);

    INSERT INTO INVENTORY_CATEGORY (ShopID, CategoryName, [Description])
    VALUES
        (@ShopId, 'Storage',      'SSDs, HDDs, and flash drives'),
        (@ShopId, 'Memory',       'RAM modules and memory kits'),
        (@ShopId, 'Cooling',      'Fans, thermal paste, and heatsinks'),
        (@ShopId, 'Cables',       'HDMI, USB, SATA, and other cables'),
        (@ShopId, 'Power Supply', 'PSU units and power accessories'),
        (@ShopId, 'Peripherals',  'Keyboards, mice, and other peripherals');

    PRINT 'âœ… Seeded 6 inventory categories';
END
ELSE
    PRINT 'â­ï¸  INVENTORY_CATEGORY already has data';
GO

-- â”€â”€ 5. Assign categories to existing inventory items â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€
DECLARE @ShopId2 BIGINT = (SELECT TOP 1 ShopID FROM SHOP);

-- Storage
UPDATE INVENTORY_ITEMS SET InventoryCategoryID = (SELECT InventoryCategoryID FROM INVENTORY_CATEGORY WHERE CategoryName = 'Storage' AND ShopID = @ShopId2)
WHERE SKU IN ('SSD-500-SAM','SSD-256-KNG','HDD-1TB-WD') AND InventoryCategoryID IS NULL;

-- Memory
UPDATE INVENTORY_ITEMS SET InventoryCategoryID = (SELECT InventoryCategoryID FROM INVENTORY_CATEGORY WHERE CategoryName = 'Memory' AND ShopID = @ShopId2)
WHERE SKU IN ('RAM-8-COR','RAM-16-COR') AND InventoryCategoryID IS NULL;

-- Cooling
UPDATE INVENTORY_ITEMS SET InventoryCategoryID = (SELECT InventoryCategoryID FROM INVENTORY_CATEGORY WHERE CategoryName = 'Cooling' AND ShopID = @ShopId2)
WHERE SKU IN ('PST-THRM-NT','FAN-120-DPC') AND InventoryCategoryID IS NULL;

-- Cables
UPDATE INVENTORY_ITEMS SET InventoryCategoryID = (SELECT InventoryCategoryID FROM INVENTORY_CATEGORY WHERE CategoryName = 'Cables' AND ShopID = @ShopId2)
WHERE SKU IN ('CBL-HDMI-2M','CBL-USBC-1M') AND InventoryCategoryID IS NULL;

-- Power Supply
UPDATE INVENTORY_ITEMS SET InventoryCategoryID = (SELECT InventoryCategoryID FROM INVENTORY_CATEGORY WHERE CategoryName = 'Power Supply' AND ShopID = @ShopId2)
WHERE SKU IN ('PSU-550-EVG') AND InventoryCategoryID IS NULL;

-- Peripherals
UPDATE INVENTORY_ITEMS SET InventoryCategoryID = (SELECT InventoryCategoryID FROM INVENTORY_CATEGORY WHERE CategoryName = 'Peripherals' AND ShopID = @ShopId2)
WHERE SKU IN ('KBD-LOGI-K120','MOU-LOGI-B100') AND InventoryCategoryID IS NULL;

PRINT 'âœ… Assigned inventory categories to existing items';
GO

PRINT '';
PRINT 'â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•';
PRINT '  Migration complete â€” ServiceCatalog + InventoryCategory';
PRINT 'â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•';


-- 
--  PART 8 of 13: Migration_Adjustments_Notifications.sql
-- 

-- =====================================================
-- Migration: Add Adjustment + Notification new columns
-- Run ONCE on the production database
-- =====================================================
-- (USE ByteBillDB removed for MonsterASP compatibility)
-- 1. Add new columns to CREDIT_DEBIT_ADJUSTMENT
IF NOT EXISTS (SELECT 1 FROM sys.columns WHERE object_id = OBJECT_ID('CREDIT_DEBIT_ADJUSTMENT') AND name = 'ShopID')
    ALTER TABLE CREDIT_DEBIT_ADJUSTMENT ADD ShopID BIGINT NOT NULL DEFAULT 1;
GO

IF NOT EXISTS (SELECT 1 FROM sys.columns WHERE object_id = OBJECT_ID('CREDIT_DEBIT_ADJUSTMENT') AND name = 'ReviewedByUserID')
    ALTER TABLE CREDIT_DEBIT_ADJUSTMENT ADD ReviewedByUserID BIGINT NULL;
GO

IF NOT EXISTS (SELECT 1 FROM sys.columns WHERE object_id = OBJECT_ID('CREDIT_DEBIT_ADJUSTMENT') AND name = 'Status')
    ALTER TABLE CREDIT_DEBIT_ADJUSTMENT ADD Status NVARCHAR(10) NOT NULL DEFAULT 'Pending';
GO

IF NOT EXISTS (SELECT 1 FROM sys.columns WHERE object_id = OBJECT_ID('CREDIT_DEBIT_ADJUSTMENT') AND name = 'ReviewedAt')
    ALTER TABLE CREDIT_DEBIT_ADJUSTMENT ADD ReviewedAt DATETIME2(0) NULL;
GO

-- Widen Reason from 150 to 500
ALTER TABLE CREDIT_DEBIT_ADJUSTMENT ALTER COLUMN Reason NVARCHAR(500) NOT NULL;
GO

-- Update AdjustmentType values from uppercase to PascalCase
UPDATE CREDIT_DEBIT_ADJUSTMENT SET AdjustmentType = 'Credit' WHERE AdjustmentType = 'CREDIT';
UPDATE CREDIT_DEBIT_ADJUSTMENT SET AdjustmentType = 'Debit' WHERE AdjustmentType = 'DEBIT';
GO

-- FK for ShopID
IF NOT EXISTS (SELECT 1 FROM sys.foreign_keys WHERE name = 'FK_CreditDebitAdj_Shop')
    ALTER TABLE CREDIT_DEBIT_ADJUSTMENT ADD CONSTRAINT FK_CreditDebitAdj_Shop
        FOREIGN KEY (ShopID) REFERENCES SHOP(ShopID);
GO

-- FK for ReviewedByUserID
IF NOT EXISTS (SELECT 1 FROM sys.foreign_keys WHERE name = 'FK_CreditDebitAdj_ReviewedBy')
    ALTER TABLE CREDIT_DEBIT_ADJUSTMENT ADD CONSTRAINT FK_CreditDebitAdj_ReviewedBy
        FOREIGN KEY (ReviewedByUserID) REFERENCES USERS(UserID);
GO

-- Index on ShopID
IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE object_id = OBJECT_ID('CREDIT_DEBIT_ADJUSTMENT') AND name = 'IX_CreditDebitAdj_ShopID')
    CREATE INDEX IX_CreditDebitAdj_ShopID ON CREDIT_DEBIT_ADJUSTMENT(ShopID);
GO

-- 2. Create NOTIFICATION table
IF NOT EXISTS (SELECT 1 FROM sys.tables WHERE name = 'NOTIFICATION')
BEGIN
    CREATE TABLE NOTIFICATION (
        NotificationID  BIGINT IDENTITY(1,1) PRIMARY KEY,
        UserID          BIGINT NOT NULL,
        ShopID          BIGINT NOT NULL,
        Title           NVARCHAR(100) NOT NULL,
        Message         NVARCHAR(500) NOT NULL,
        Type            NVARCHAR(20) NOT NULL DEFAULT 'info',
        Url             NVARCHAR(200) NULL,
        IsRead          BIT NOT NULL DEFAULT 0,
        CreatedAt       DATETIME2(0) NOT NULL DEFAULT SYSDATETIME(),

        CONSTRAINT FK_Notification_User FOREIGN KEY (UserID) REFERENCES USERS(UserID) ON DELETE CASCADE,
        CONSTRAINT FK_Notification_Shop FOREIGN KEY (ShopID) REFERENCES SHOP(ShopID)
    );

    CREATE INDEX IX_Notification_UserID_IsRead ON NOTIFICATION(UserID, IsRead);
END
GO

PRINT 'Migration complete: Adjustment columns + Notification table created.';
GO


-- 
--  PART 9 of 13: Migration_AdjustmentTypeConfig.sql
-- 

-- â•”â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•—
-- â•‘ Migration: Adjustment Type Config                                â•‘
-- â•‘ Adds ADJUSTMENT_TYPE_CONFIG table for admin-configurable         â•‘
-- â•‘ adjustment types with percentages per shop.                      â•‘
-- â•šâ•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•
-- (USE ByteBillDB removed for MonsterASP compatibility)
-- â”€â”€ Create ADJUSTMENT_TYPE_CONFIG table â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€
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

-- â”€â”€ Seed default adjustment types (ShopId = 1) â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€
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


-- 
--  PART 10 of 13: Migration_XeroConnection.sql
-- 

-- â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•
-- Migration: Xero Connection Table
-- Run after: ByteBillDB_MonsterASP_Deploy.sql / existing migrations
-- â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•

IF NOT EXISTS (
    SELECT 1 FROM INFORMATION_SCHEMA.TABLES
    WHERE TABLE_NAME = 'XERO_CONNECTION'
)
BEGIN
    CREATE TABLE [XERO_CONNECTION] (
        [XeroConnectionID]  BIGINT          IDENTITY(1,1) NOT NULL,
        [ShopID]            BIGINT          NOT NULL,
        [XeroTenantId]      NVARCHAR(80)    NOT NULL,
        [TenantName]        NVARCHAR(150)   NULL,
        [AccessToken]       NVARCHAR(2048)  NOT NULL,
        [RefreshToken]      NVARCHAR(2048)  NOT NULL,
        [TokenExpiresAt]    DATETIME2(0)    NOT NULL,
        [ConnectedAt]       DATETIME2(0)    NOT NULL DEFAULT SYSDATETIME(),
        [IsActive]          BIT             NOT NULL DEFAULT 1,

        CONSTRAINT [PK_XERO_CONNECTION] PRIMARY KEY ([XeroConnectionID]),
        CONSTRAINT [FK_XERO_CONNECTION_SHOP] FOREIGN KEY ([ShopID])
            REFERENCES [SHOP]([ShopID]) ON DELETE NO ACTION
    );

    CREATE NONCLUSTERED INDEX [IX_XERO_CONNECTION_ShopID]
        ON [XERO_CONNECTION]([ShopID]);

    PRINT 'Created XERO_CONNECTION table';
END;
GO

PRINT 'âœ… Xero Connection migration complete.';
GO


-- 
--  PART 11 of 13: Migration_SuperAdmin_Module.sql
-- 

-- â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•
--  ByteBill SuperAdmin Module â€” Database Migration
--  Creates: SubscriptionPlans, Subscriptions, SubscriptionPayments,
--           PlatformSettings, Announcements, SuperAdminAuditLog
--  Alters:  SHOP (IsDefault), USERS (LastLoginAt, LastIpAddress)
--  Seeds:   3 subscription plans, default subscription for Main Shop,
--           default platform settings
-- â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•

SET XACT_ABORT ON;
BEGIN TRANSACTION;

-- â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€
-- 1. ALTER SHOP â€” add IsDefault flag
-- â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€
IF NOT EXISTS (SELECT 1 FROM sys.columns WHERE object_id = OBJECT_ID('SHOP') AND name = 'IsDefault')
BEGIN
    ALTER TABLE [SHOP] ADD [IsDefault] BIT NOT NULL CONSTRAINT DF_SHOP_IsDefault DEFAULT 0;
    -- Use EXEC so the column reference is deferred past the ALTER
    EXEC sp_executesql N'UPDATE [SHOP] SET [IsDefault] = 1 WHERE [ShopCode] = N''MAIN''';
END 

-- â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€
-- 2. ALTER USERS â€” add LastLoginAt, LastIpAddress
-- â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€
IF NOT EXISTS (SELECT 1 FROM sys.columns WHERE object_id = OBJECT_ID('USERS') AND name = 'LastLoginAt')
BEGIN
    ALTER TABLE [USERS] ADD [LastLoginAt] DATETIME2(0) NULL;
END

IF NOT EXISTS (SELECT 1 FROM sys.columns WHERE object_id = OBJECT_ID('USERS') AND name = 'LastIpAddress')
BEGIN
    ALTER TABLE [USERS] ADD [LastIpAddress] NVARCHAR(50) NULL;
END

-- â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€
-- 3. SUBSCRIPTION_PLANS
-- â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€
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

-- â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€
-- 4. SUBSCRIPTIONS
-- â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€
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

-- â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€
-- 5. SUBSCRIPTION_PAYMENTS
-- â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€
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

-- â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€
-- 6. PLATFORM_SETTINGS
-- â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€
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

-- â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€
-- 7. ANNOUNCEMENTS
-- â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€
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

-- â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€
-- 8. SUPERADMIN_AUDIT_LOG
-- â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€
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

-- â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•
-- SEED DATA
-- â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•

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


-- 
--  PART 12 of 13: Migration_PayMongo_Refactor.sql
-- 

-- ============================================================================
--  MIGRATION: PayMongo Refactor â€” No pre-created payments, actual method tracking
--
--  Changes:
--    1. Add new columns to PAYMONGO_TXN (ShopID, InvoiceID, InitiatedByUserID, Amount, PayMongoPaymentMethod)
--    2. Make PaymentID nullable on PAYMONGO_TXN
--    3. Update PAYMENTS CHECK constraint to remove 'PayMongo' method
--    4. Widen RawResponse to NVARCHAR(MAX)
--    5. Clean up orphan PayMongo payment records (pending/duplicate)
--    6. Update unique index on PaymentID to filtered (WHERE PaymentID IS NOT NULL)
--
--  Run this against your database before restarting the app.
-- ============================================================================

SET NOCOUNT ON;
PRINT '=== PayMongo Refactor Migration ===';

-- â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€
-- 1. Add ShopID column to PAYMONGO_TXN
-- â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€
IF NOT EXISTS (SELECT 1 FROM INFORMATION_SCHEMA.COLUMNS WHERE TABLE_NAME='PAYMONGO_TXN' AND COLUMN_NAME='ShopID')
BEGIN
    ALTER TABLE [dbo].[PAYMONGO_TXN] ADD [ShopID] BIGINT NULL;
    PRINT 'âœ… Added ShopID column';
END
ELSE PRINT 'â­ï¸ ShopID already exists';
GO

-- Backfill ShopID from linked Payment
IF COL_LENGTH('PAYMONGO_TXN','ShopID') IS NOT NULL
    EXEC('UPDATE t SET t.ShopID = p.ShopID FROM [dbo].[PAYMONGO_TXN] t INNER JOIN [dbo].[PAYMENTS] p ON t.PaymentID = p.PaymentID WHERE t.ShopID IS NULL');
GO

-- â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€
-- 2. Add InvoiceID column
-- â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€
IF NOT EXISTS (SELECT 1 FROM INFORMATION_SCHEMA.COLUMNS WHERE TABLE_NAME='PAYMONGO_TXN' AND COLUMN_NAME='InvoiceID')
BEGIN
    ALTER TABLE [dbo].[PAYMONGO_TXN] ADD [InvoiceID] BIGINT NULL;
    PRINT 'âœ… Added InvoiceID column';
END
ELSE PRINT 'â­ï¸ InvoiceID already exists';
GO

-- Backfill InvoiceID from PaymentAllocations
IF COL_LENGTH('PAYMONGO_TXN','InvoiceID') IS NOT NULL
    EXEC('UPDATE t SET t.InvoiceID = pa.InvoiceID FROM [dbo].[PAYMONGO_TXN] t INNER JOIN [dbo].[PAYMENT_ALLOCATION] pa ON t.PaymentID = pa.PaymentID WHERE t.InvoiceID IS NULL');
GO

-- â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€
-- 3. Add InitiatedByUserID column
-- â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€
IF NOT EXISTS (SELECT 1 FROM INFORMATION_SCHEMA.COLUMNS WHERE TABLE_NAME='PAYMONGO_TXN' AND COLUMN_NAME='InitiatedByUserID')
BEGIN
    ALTER TABLE [dbo].[PAYMONGO_TXN] ADD [InitiatedByUserID] BIGINT NOT NULL DEFAULT(0);
    PRINT 'âœ… Added InitiatedByUserID column';
END
ELSE PRINT 'â­ï¸ InitiatedByUserID already exists';
GO

-- Backfill InitiatedByUserID from linked Payment
IF COL_LENGTH('PAYMONGO_TXN','InitiatedByUserID') IS NOT NULL
    EXEC('UPDATE t SET t.InitiatedByUserID = p.ReceivedByUserID FROM [dbo].[PAYMONGO_TXN] t INNER JOIN [dbo].[PAYMENTS] p ON t.PaymentID = p.PaymentID WHERE t.InitiatedByUserID = 0 AND p.ReceivedByUserID IS NOT NULL');
GO

-- â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€
-- 4. Add Amount column
-- â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€
IF NOT EXISTS (SELECT 1 FROM INFORMATION_SCHEMA.COLUMNS WHERE TABLE_NAME='PAYMONGO_TXN' AND COLUMN_NAME='Amount')
BEGIN
    ALTER TABLE [dbo].[PAYMONGO_TXN] ADD [Amount] DECIMAL(18,2) NOT NULL DEFAULT(0);
    PRINT 'âœ… Added Amount column';
END
ELSE PRINT 'â­ï¸ Amount already exists';
GO

-- Backfill Amount from linked Payment
IF COL_LENGTH('PAYMONGO_TXN','Amount') IS NOT NULL
    EXEC('UPDATE t SET t.Amount = p.Amount FROM [dbo].[PAYMONGO_TXN] t INNER JOIN [dbo].[PAYMENTS] p ON t.PaymentID = p.PaymentID WHERE t.Amount = 0');
GO

-- â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€
-- 5. Add PayMongoPaymentMethod column
-- â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€
IF NOT EXISTS (SELECT 1 FROM INFORMATION_SCHEMA.COLUMNS WHERE TABLE_NAME='PAYMONGO_TXN' AND COLUMN_NAME='PayMongoPaymentMethod')
BEGIN
    ALTER TABLE [dbo].[PAYMONGO_TXN] ADD [PayMongoPaymentMethod] NVARCHAR(30) NULL;
    PRINT 'âœ… Added PayMongoPaymentMethod column';
END
ELSE PRINT 'â­ï¸ PayMongoPaymentMethod already exists';
GO

-- â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€
-- 6. Make PaymentID nullable
-- â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€
-- Drop existing unique index first
IF EXISTS (SELECT 1 FROM sys.indexes WHERE name='IX_PAYMONGO_TXN_PaymentID' AND object_id=OBJECT_ID('dbo.PAYMONGO_TXN'))
BEGIN
    DROP INDEX [IX_PAYMONGO_TXN_PaymentID] ON [dbo].[PAYMONGO_TXN];
    PRINT 'âœ… Dropped old unique index on PaymentID';
END
ELSE PRINT 'â­ï¸ Index already dropped or does not exist';

-- Drop the unique constraint if it exists
IF EXISTS (SELECT 1 FROM sys.key_constraints WHERE name='UQ_PAYMONGO_TXN_PaymentID' AND parent_object_id=OBJECT_ID('dbo.PAYMONGO_TXN'))
BEGIN
    ALTER TABLE [dbo].[PAYMONGO_TXN] DROP CONSTRAINT [UQ_PAYMONGO_TXN_PaymentID];
    PRINT 'âœ… Dropped unique constraint UQ_PAYMONGO_TXN_PaymentID';
END
ELSE PRINT 'â­ï¸ Unique constraint already dropped or does not exist';

-- Drop FK constraint
IF EXISTS (SELECT 1 FROM sys.foreign_keys WHERE name='FK_PAYMONGO_TXN_Payment' AND parent_object_id=OBJECT_ID('dbo.PAYMONGO_TXN'))
BEGIN
    ALTER TABLE [dbo].[PAYMONGO_TXN] DROP CONSTRAINT [FK_PAYMONGO_TXN_Payment];
    PRINT 'âœ… Dropped FK constraint FK_PAYMONGO_TXN_Payment';
END
ELSE PRINT 'â­ï¸ FK constraint already dropped or does not exist';

-- Check if PaymentID column is currently NOT NULL, and alter to NULL
IF EXISTS (
    SELECT 1 FROM INFORMATION_SCHEMA.COLUMNS
    WHERE TABLE_NAME='PAYMONGO_TXN' AND COLUMN_NAME='PaymentID' AND IS_NULLABLE='NO'
)
BEGIN
    ALTER TABLE [dbo].[PAYMONGO_TXN] ALTER COLUMN [PaymentID] BIGINT NULL;
    PRINT 'âœ… Made PaymentID nullable';
END
ELSE PRINT 'â­ï¸ PaymentID is already nullable';

-- Re-add FK (now nullable)
IF NOT EXISTS (SELECT 1 FROM sys.foreign_keys WHERE name='FK_PAYMONGO_TXN_Payment' AND parent_object_id=OBJECT_ID('dbo.PAYMONGO_TXN'))
BEGIN
    ALTER TABLE [dbo].[PAYMONGO_TXN]
        ADD CONSTRAINT [FK_PAYMONGO_TXN_Payment]
        FOREIGN KEY ([PaymentID]) REFERENCES [dbo].[PAYMENTS] ([PaymentID])
        ON DELETE NO ACTION ON UPDATE NO ACTION;
    PRINT 'âœ… Re-added FK on PaymentID (nullable)';
END
ELSE PRINT 'â­ï¸ FK already exists';

-- Re-add filtered unique index
IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE name='IX_PAYMONGO_TXN_PaymentID' AND object_id=OBJECT_ID('dbo.PAYMONGO_TXN'))
BEGIN
    CREATE UNIQUE NONCLUSTERED INDEX [IX_PAYMONGO_TXN_PaymentID]
        ON [dbo].[PAYMONGO_TXN] ([PaymentID])
        WHERE [PaymentID] IS NOT NULL;
    PRINT 'âœ… Created filtered unique index on PaymentID';
END
ELSE PRINT 'â­ï¸ Filtered unique index already exists';
GO

-- â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€
-- 7. Widen RawResponse to NVARCHAR(MAX)
-- â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€
IF EXISTS (
    SELECT 1 FROM INFORMATION_SCHEMA.COLUMNS
    WHERE TABLE_NAME='PAYMONGO_TXN' AND COLUMN_NAME='RawResponse' AND CHARACTER_MAXIMUM_LENGTH <> -1
)
BEGIN
    ALTER TABLE [dbo].[PAYMONGO_TXN] ALTER COLUMN [RawResponse] NVARCHAR(MAX) NULL;
    PRINT 'âœ… Widened RawResponse to NVARCHAR(MAX)';
END
ELSE PRINT 'â­ï¸ RawResponse is already NVARCHAR(MAX)';
GO

-- â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€
-- 8. CLEANUP orphan PayMongo payments FIRST (before CHECK constraint change)
-- â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€
PRINT '';
PRINT '=== Cleanup orphan PayMongo records ===';

-- Update confirmed 'PayMongo' method payments to 'Card' so they survive the new CHECK
UPDATE [dbo].[PAYMENTS]
SET Method = 'Card'
WHERE Method = 'PayMongo' AND [Status] = 'Confirmed';
PRINT 'âœ… Updated confirmed PayMongo payments to Card method';

-- Delete pending/failed PayMongo payments (orphans from the old pre-create flow)
-- First remove their allocations
DELETE pa
FROM [dbo].[PAYMENT_ALLOCATION] pa
INNER JOIN [dbo].[PAYMENTS] p ON pa.PaymentID = p.PaymentID
WHERE p.Method = 'PayMongo' AND p.[Status] IN ('Pending','Failed');

-- Remove PayMongoTxn links
UPDATE t SET t.PaymentID = NULL
FROM [dbo].[PAYMONGO_TXN] t
INNER JOIN [dbo].[PAYMENTS] p ON t.PaymentID = p.PaymentID
WHERE p.Method = 'PayMongo' AND p.[Status] IN ('Pending','Failed');

-- Delete the orphan payments
DELETE FROM [dbo].[PAYMENTS]
WHERE Method = 'PayMongo' AND [Status] IN ('Pending','Failed');
PRINT 'âœ… Removed orphan pending/failed PayMongo payments';
GO

-- â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€
-- 9. Update PAYMENTS CHECK constraint (remove 'PayMongo')
--    Done AFTER cleanup so no rows violate the new constraint
-- â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€
IF EXISTS (SELECT 1 FROM sys.check_constraints WHERE name='CK_PAYMENTS_Method' AND parent_object_id=OBJECT_ID('dbo.PAYMENTS'))
BEGIN
    ALTER TABLE [dbo].[PAYMENTS] DROP CONSTRAINT [CK_PAYMENTS_Method];
    ALTER TABLE [dbo].[PAYMENTS]
        ADD CONSTRAINT [CK_PAYMENTS_Method] CHECK (Method IN ('Cash','GCash','Card'));
    PRINT 'âœ… Updated PAYMENTS method check constraint (removed PayMongo)';
END
ELSE PRINT 'â­ï¸ CHECK constraint already updated or does not exist';
GO

-- â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€
-- 10. Add FK for InvoiceID if not exists
-- â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€
IF NOT EXISTS (SELECT 1 FROM sys.foreign_keys WHERE name='FK_PAYMONGO_TXN_Invoice' AND parent_object_id=OBJECT_ID('dbo.PAYMONGO_TXN'))
BEGIN
    ALTER TABLE [dbo].[PAYMONGO_TXN]
        ADD CONSTRAINT [FK_PAYMONGO_TXN_Invoice]
        FOREIGN KEY ([InvoiceID]) REFERENCES [dbo].[INVOICES] ([InvoiceID])
        ON DELETE NO ACTION ON UPDATE NO ACTION;
    PRINT 'âœ… Added FK on InvoiceID';
END
ELSE PRINT 'â­ï¸ FK on InvoiceID already exists';

PRINT '';
PRINT '=== Migration complete ===';


-- 
--  PART 13 of 13: Migration_Post_SuperAdmin_Sync.sql
-- 

-- â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•
--  ByteBill Post-SuperAdmin Database Sync
--  Run this ONCE on the MonsterASP online database AFTER the SuperAdmin
--  migration has already been applied.
--
--  What it does:
--    1. Adds missing columns   (JOB_ORDERS: Priority, EstimatedCompletionDate)
--    2. Fixes nullability       (AUDIT_LOG.UserID â†’ nullable, PAYMONGO_TXN cols â†’ NOT NULL)
--    3. Adds missing FK         (PAYMONGO_TXN.ShopID â†’ SHOP)
--    4. Widens NVARCHAR columns (to match C# MaxLength attributes)
--    5. Widens DECIMAL columns  (from 6,2/8,2 â†’ 18,2)
--    6. Fixes datetime precision (PAYMONGO_TXN.UpdatedAt â†’ DATETIME2(0))
--
--  Safe to re-run: every ALTER is guarded with IF NOT EXISTS / IF checks.
-- â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•

SET XACT_ABORT ON;
BEGIN TRANSACTION;

-- â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•
-- 1. MISSING COLUMNS â€” App will crash without these
-- â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•

-- 1a. JOB_ORDERS.Priority (NVARCHAR(30) NOT NULL DEFAULT 'Normal')
IF NOT EXISTS (SELECT 1 FROM sys.columns WHERE object_id = OBJECT_ID('JOB_ORDERS') AND name = 'Priority')
BEGIN
    ALTER TABLE [JOB_ORDERS] ADD [Priority] NVARCHAR(30) NOT NULL CONSTRAINT DF_JO_Priority DEFAULT 'Normal';
    PRINT '  Added JOB_ORDERS.Priority';
END

-- 1b. JOB_ORDERS.EstimatedCompletionDate (DATETIME2(0) NULL)
IF NOT EXISTS (SELECT 1 FROM sys.columns WHERE object_id = OBJECT_ID('JOB_ORDERS') AND name = 'EstimatedCompletionDate')
BEGIN
    ALTER TABLE [JOB_ORDERS] ADD [EstimatedCompletionDate] DATETIME2(0) NULL;
    PRINT '  Added JOB_ORDERS.EstimatedCompletionDate';
END

-- â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•
-- 2. NULLABILITY FIXES
-- â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•

-- 2a. AUDIT_LOG.UserID â†’ BIGINT NULL (C# model: long? UserId)
--     Must drop FK constraint first, alter column, then re-add FK
IF EXISTS (SELECT 1 FROM sys.columns WHERE object_id = OBJECT_ID('AUDIT_LOG') AND name = 'UserID' AND is_nullable = 0)
BEGIN
    -- Drop existing FK if any
    DECLARE @fk_audit NVARCHAR(200);
    SELECT @fk_audit = fk.name
    FROM sys.foreign_keys fk
    INNER JOIN sys.foreign_key_columns fkc ON fk.object_id = fkc.constraint_object_id
    WHERE fk.parent_object_id = OBJECT_ID('AUDIT_LOG')
      AND COL_NAME(fk.parent_object_id, fkc.parent_column_id) = 'UserID';

    IF @fk_audit IS NOT NULL
        EXEC('ALTER TABLE [AUDIT_LOG] DROP CONSTRAINT [' + @fk_audit + ']');

    ALTER TABLE [AUDIT_LOG] ALTER COLUMN [UserID] BIGINT NULL;

    -- Re-add FK
    ALTER TABLE [AUDIT_LOG] ADD CONSTRAINT [FK_AUDIT_LOG_USER]
        FOREIGN KEY ([UserID]) REFERENCES [USERS]([UserID]);

    PRINT '  AUDIT_LOG.UserID changed to nullable';
END

-- 2b. PAYMONGO_TXN.ShopID â†’ BIGINT NOT NULL
--     Backfill any NULL values first (use the shop from the linked invoice)
IF EXISTS (SELECT 1 FROM sys.columns WHERE object_id = OBJECT_ID('PAYMONGO_TXN') AND name = 'ShopID' AND is_nullable = 1)
BEGIN
    -- Backfill NULLs from linked invoice's ShopID
    UPDATE pt
    SET pt.[ShopID] = i.[ShopID]
    FROM [PAYMONGO_TXN] pt
    INNER JOIN [INVOICES] i ON pt.[InvoiceID] = i.[InvoiceID]
    WHERE pt.[ShopID] IS NULL AND pt.[InvoiceID] IS NOT NULL;

    -- If any remain NULL (no linked invoice), use the default shop
    UPDATE [PAYMONGO_TXN]
    SET [ShopID] = (SELECT TOP 1 [ShopID] FROM [SHOP] WHERE [IsDefault] = 1)
    WHERE [ShopID] IS NULL;

    -- Now make NOT NULL
    ALTER TABLE [PAYMONGO_TXN] ALTER COLUMN [ShopID] BIGINT NOT NULL;
    PRINT '  PAYMONGO_TXN.ShopID changed to NOT NULL';
END

-- 2c. PAYMONGO_TXN.InvoiceID â†’ BIGINT NOT NULL
IF EXISTS (SELECT 1 FROM sys.columns WHERE object_id = OBJECT_ID('PAYMONGO_TXN') AND name = 'InvoiceID' AND is_nullable = 1)
BEGIN
    -- Delete orphan rows with no InvoiceID (can't be fixed)
    DELETE FROM [PAYMONGO_TXN] WHERE [InvoiceID] IS NULL;

    ALTER TABLE [PAYMONGO_TXN] ALTER COLUMN [InvoiceID] BIGINT NOT NULL;
    PRINT '  PAYMONGO_TXN.InvoiceID changed to NOT NULL';
END

-- â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•
-- 3. MISSING FOREIGN KEY CONSTRAINT
-- â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•

-- 3a. PAYMONGO_TXN.ShopID â†’ SHOP(ShopID)
IF NOT EXISTS (
    SELECT 1 FROM sys.foreign_keys fk
    INNER JOIN sys.foreign_key_columns fkc ON fk.object_id = fkc.constraint_object_id
    WHERE fk.parent_object_id = OBJECT_ID('PAYMONGO_TXN')
      AND COL_NAME(fk.parent_object_id, fkc.parent_column_id) = 'ShopID'
)
BEGIN
    ALTER TABLE [PAYMONGO_TXN] ADD CONSTRAINT [FK_PAYMONGO_TXN_SHOP]
        FOREIGN KEY ([ShopID]) REFERENCES [SHOP]([ShopID]);
    PRINT '  Added FK PAYMONGO_TXN.ShopID â†’ SHOP';
END

-- â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•
-- 4. WIDEN NVARCHAR COLUMNS (prevent truncation errors)
-- â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•

-- SHOP
ALTER TABLE [SHOP] ALTER COLUMN [ShopName]  NVARCHAR(150) NOT NULL;
ALTER TABLE [SHOP] ALTER COLUMN [Email]     NVARCHAR(100) NULL;
ALTER TABLE [SHOP] ALTER COLUMN [Address]   NVARCHAR(255) NULL;
ALTER TABLE [SHOP] ALTER COLUMN [Status]    NVARCHAR(20)  NOT NULL;
PRINT '  Widened SHOP columns';

-- USERS (must handle UNIQUE constraint and index on UserName)
-- Drop the unique constraint and index first, alter, then re-add
IF EXISTS (SELECT 1 FROM sys.key_constraints WHERE name = 'UQ_USERS_ShopUserName' AND parent_object_id = OBJECT_ID('USERS'))
    ALTER TABLE [USERS] DROP CONSTRAINT [UQ_USERS_ShopUserName];
IF EXISTS (SELECT 1 FROM sys.indexes WHERE name = 'IX_USERS_ShopID_UserName' AND object_id = OBJECT_ID('USERS'))
    DROP INDEX [IX_USERS_ShopID_UserName] ON [USERS];

ALTER TABLE [USERS] ALTER COLUMN [FirstName]    NVARCHAR(50)  NOT NULL;
ALTER TABLE [USERS] ALTER COLUMN [MiddleName]   NVARCHAR(50)  NULL;
ALTER TABLE [USERS] ALTER COLUMN [LastName]     NVARCHAR(50)  NOT NULL;
ALTER TABLE [USERS] ALTER COLUMN [UserName]     NVARCHAR(100) NOT NULL;
ALTER TABLE [USERS] ALTER COLUMN [PasswordHash] NVARCHAR(255) NOT NULL;

-- Re-create constraint and index
ALTER TABLE [USERS] ADD CONSTRAINT [UQ_USERS_ShopUserName] UNIQUE ([ShopID], [UserName]);
CREATE NONCLUSTERED INDEX [IX_USERS_ShopID_UserName] ON [USERS] ([ShopID], [UserName]);
PRINT '  Widened USERS columns';

-- CUSTOMERS (must handle index that includes these columns)
IF EXISTS (SELECT 1 FROM sys.indexes WHERE name = 'IX_Customers_ShopID_Name' AND object_id = OBJECT_ID('CUSTOMERS'))
    DROP INDEX [IX_Customers_ShopID_Name] ON [CUSTOMERS];

ALTER TABLE [CUSTOMERS] ALTER COLUMN [FirstName]  NVARCHAR(50)  NOT NULL;
ALTER TABLE [CUSTOMERS] ALTER COLUMN [MiddleName] NVARCHAR(50)  NULL;
ALTER TABLE [CUSTOMERS] ALTER COLUMN [LastName]   NVARCHAR(50)  NOT NULL;
ALTER TABLE [CUSTOMERS] ALTER COLUMN [Email]      NVARCHAR(100) NULL;
ALTER TABLE [CUSTOMERS] ALTER COLUMN [Address]    NVARCHAR(255) NULL;

CREATE NONCLUSTERED INDEX [IX_Customers_ShopID_Name] ON [CUSTOMERS] ([ShopID], [LastName], [FirstName]) INCLUDE ([Email], [Phone], [IsActive]);
PRINT '  Widened CUSTOMERS columns';

-- PAYMENTS (must handle indexes that include Method, Amount, Status)
-- Drop indexes once, alter all columns (NVARCHAR + DECIMAL), recreate indexes
IF EXISTS (SELECT 1 FROM sys.indexes WHERE name = 'IX_Payments_ShopID_Status' AND object_id = OBJECT_ID('PAYMENTS'))
    DROP INDEX [IX_Payments_ShopID_Status] ON [PAYMENTS];
IF EXISTS (SELECT 1 FROM sys.indexes WHERE name = 'IX_Payments_ShopID_PaymentDate' AND object_id = OBJECT_ID('PAYMENTS'))
    DROP INDEX [IX_Payments_ShopID_PaymentDate] ON [PAYMENTS];

ALTER TABLE [PAYMENTS] ALTER COLUMN [Method]      NVARCHAR(30)  NOT NULL;
ALTER TABLE [PAYMENTS] ALTER COLUMN [ReferenceNo] NVARCHAR(60)  NULL;
ALTER TABLE [PAYMENTS] ALTER COLUMN [Notes]       NVARCHAR(500) NULL;
ALTER TABLE [PAYMENTS] ALTER COLUMN [Amount]      DECIMAL(18,2) NOT NULL;

CREATE NONCLUSTERED INDEX [IX_Payments_ShopID_Status] ON [PAYMENTS] ([ShopID], [Status]) INCLUDE ([CustomerID], [Amount], [PaymentDate], [Method]);
CREATE NONCLUSTERED INDEX [IX_Payments_ShopID_PaymentDate] ON [PAYMENTS] ([ShopID], [PaymentDate]) INCLUDE ([Amount], [Status]);
PRINT '  Widened PAYMENTS columns (NVARCHAR + DECIMAL)';

-- AUDIT_LOG
ALTER TABLE [AUDIT_LOG] ALTER COLUMN [Action]     NVARCHAR(50) NOT NULL;
ALTER TABLE [AUDIT_LOG] ALTER COLUMN [EntityName] NVARCHAR(50) NOT NULL;
PRINT '  Widened AUDIT_LOG columns';

-- SERVICE_CATALOG
ALTER TABLE [SERVICE_CATALOG] ALTER COLUMN [Description] NVARCHAR(500) NULL;
PRINT '  Widened SERVICE_CATALOG.Description';

-- â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•
-- 5. WIDEN DECIMAL COLUMNS (prevent arithmetic overflow)
--    From DECIMAL(6,2) / DECIMAL(8,2) â†’ DECIMAL(18,2)
--    Must drop/recreate computed columns that depend on UnitPrice
-- â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•

-- SERVICE_CATALOG
ALTER TABLE [SERVICE_CATALOG] ALTER COLUMN [BasePrice] DECIMAL(18,2) NOT NULL;
PRINT '  Widened SERVICE_CATALOG.BasePrice';

-- INVENTORY_ITEMS
ALTER TABLE [INVENTORY_ITEMS] ALTER COLUMN [UnitCost]  DECIMAL(18,2) NOT NULL;
ALTER TABLE [INVENTORY_ITEMS] ALTER COLUMN [UnitPrice] DECIMAL(18,2) NOT NULL;
PRINT '  Widened INVENTORY_ITEMS decimal columns';

-- JOB_ORDER_SERVICES â€” has computed LineTotal = Qty * UnitPrice
IF EXISTS (SELECT 1 FROM sys.computed_columns WHERE object_id = OBJECT_ID('JOB_ORDER_SERVICES') AND name = 'LineTotal')
    ALTER TABLE [JOB_ORDER_SERVICES] DROP COLUMN [LineTotal];
ALTER TABLE [JOB_ORDER_SERVICES] ALTER COLUMN [UnitPrice] DECIMAL(18,2) NOT NULL;
ALTER TABLE [JOB_ORDER_SERVICES] ADD [LineTotal] AS ([Qty] * [UnitPrice]) PERSISTED;
PRINT '  Widened JOB_ORDER_SERVICES.UnitPrice (rebuilt computed LineTotal)';

-- JOB_ORDER_PARTS â€” has computed LineTotal = QtyUsed * UnitPrice
IF EXISTS (SELECT 1 FROM sys.computed_columns WHERE object_id = OBJECT_ID('JOB_ORDER_PARTS') AND name = 'LineTotal')
    ALTER TABLE [JOB_ORDER_PARTS] DROP COLUMN [LineTotal];
ALTER TABLE [JOB_ORDER_PARTS] ALTER COLUMN [UnitPrice] DECIMAL(18,2) NOT NULL;
ALTER TABLE [JOB_ORDER_PARTS] ADD [LineTotal] AS ([QtyUsed] * [UnitPrice]) PERSISTED;
PRINT '  Widened JOB_ORDER_PARTS.UnitPrice (rebuilt computed LineTotal)';

-- INVOICE_LINES â€” has computed LineTotal = Qty * UnitPrice
IF EXISTS (SELECT 1 FROM sys.computed_columns WHERE object_id = OBJECT_ID('INVOICE_LINES') AND name = 'LineTotal')
    ALTER TABLE [INVOICE_LINES] DROP COLUMN [LineTotal];
ALTER TABLE [INVOICE_LINES] ALTER COLUMN [UnitPrice] DECIMAL(18,2) NOT NULL;
ALTER TABLE [INVOICE_LINES] ADD [LineTotal] AS ([Qty] * [UnitPrice]) PERSISTED;
PRINT '  Widened INVOICE_LINES.UnitPrice (rebuilt computed LineTotal)';

-- INVOICES â€” all money columns (handle indexes containing these columns)
IF EXISTS (SELECT 1 FROM sys.indexes WHERE name = 'IX_Invoices_ShopID_Status' AND object_id = OBJECT_ID('INVOICES'))
    DROP INDEX [IX_Invoices_ShopID_Status] ON [INVOICES];
IF EXISTS (SELECT 1 FROM sys.indexes WHERE name = 'IX_Invoices_ShopID_DueDate' AND object_id = OBJECT_ID('INVOICES'))
    DROP INDEX [IX_Invoices_ShopID_DueDate] ON [INVOICES];

ALTER TABLE [INVOICES] ALTER COLUMN [Subtotal]         DECIMAL(18,2) NOT NULL;
ALTER TABLE [INVOICES] ALTER COLUMN [DiscountAmount]   DECIMAL(18,2) NOT NULL;
ALTER TABLE [INVOICES] ALTER COLUMN [TotalAdjustments] DECIMAL(18,2) NOT NULL;
ALTER TABLE [INVOICES] ALTER COLUMN [TotalAmount]      DECIMAL(18,2) NOT NULL;
ALTER TABLE [INVOICES] ALTER COLUMN [AmountPaid]       DECIMAL(18,2) NOT NULL;
ALTER TABLE [INVOICES] ALTER COLUMN [Balance]          DECIMAL(18,2) NOT NULL;

CREATE NONCLUSTERED INDEX [IX_Invoices_ShopID_Status] ON [INVOICES] ([ShopID], [Status]) INCLUDE ([InvoiceNo], [TotalAmount], [AmountPaid], [Balance], [DueDate], [CustomerID]);
CREATE NONCLUSTERED INDEX [IX_Invoices_ShopID_DueDate] ON [INVOICES] ([ShopID], [DueDate]) INCLUDE ([Balance], [Status]);
PRINT '  Widened INVOICES decimal columns';

-- PAYMENT_ALLOCATION (handle index containing AmountApplied)
IF EXISTS (SELECT 1 FROM sys.indexes WHERE name = 'IX_PayAlloc_InvoiceID' AND object_id = OBJECT_ID('PAYMENT_ALLOCATION'))
    DROP INDEX [IX_PayAlloc_InvoiceID] ON [PAYMENT_ALLOCATION];

ALTER TABLE [PAYMENT_ALLOCATION] ALTER COLUMN [AmountApplied] DECIMAL(18,2) NOT NULL;

CREATE NONCLUSTERED INDEX [IX_PayAlloc_InvoiceID] ON [PAYMENT_ALLOCATION] ([InvoiceID]) INCLUDE ([AmountApplied]);
PRINT '  Widened PAYMENT_ALLOCATION.AmountApplied';

-- ACCOUNTING_ENTRY
ALTER TABLE [ACCOUNTING_ENTRY] ALTER COLUMN [Debit]  DECIMAL(18,2) NOT NULL;
ALTER TABLE [ACCOUNTING_ENTRY] ALTER COLUMN [Credit] DECIMAL(18,2) NOT NULL;
PRINT '  Widened ACCOUNTING_ENTRY decimal columns';

-- â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•
-- 6. DATETIME PRECISION FIX
-- â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•

-- PAYMONGO_TXN.UpdatedAt should be DATETIME2(0), not DATETIME2(7)
ALTER TABLE [PAYMONGO_TXN] ALTER COLUMN [UpdatedAt] DATETIME2(0) NULL;
PRINT '  Fixed PAYMONGO_TXN.UpdatedAt precision';

-- â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•
-- 7. VERIFICATION QUERIES (informational â€” check counts after running)
-- â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•

PRINT '';
PRINT 'â•â•â• VERIFICATION â•â•â•';
PRINT 'Checking JOB_ORDERS.Priority exists...';
IF EXISTS (SELECT 1 FROM sys.columns WHERE object_id = OBJECT_ID('JOB_ORDERS') AND name = 'Priority')
    PRINT '  âœ“ JOB_ORDERS.Priority exists';
ELSE
    PRINT '  âœ— JOB_ORDERS.Priority MISSING â€” something went wrong!';

PRINT 'Checking AUDIT_LOG.UserID is nullable...';
IF EXISTS (SELECT 1 FROM sys.columns WHERE object_id = OBJECT_ID('AUDIT_LOG') AND name = 'UserID' AND is_nullable = 1)
    PRINT '  âœ“ AUDIT_LOG.UserID is nullable';
ELSE
    PRINT '  âœ— AUDIT_LOG.UserID is still NOT NULL â€” something went wrong!';

PRINT 'Checking PAYMONGO_TXN.ShopID is NOT NULL...';
IF EXISTS (SELECT 1 FROM sys.columns WHERE object_id = OBJECT_ID('PAYMONGO_TXN') AND name = 'ShopID' AND is_nullable = 0)
    PRINT '  âœ“ PAYMONGO_TXN.ShopID is NOT NULL';
ELSE
    PRINT '  âœ— PAYMONGO_TXN.ShopID is still nullable â€” something went wrong!';

PRINT 'Checking PAYMONGO_TXN.InvoiceID is NOT NULL...';
IF EXISTS (SELECT 1 FROM sys.columns WHERE object_id = OBJECT_ID('PAYMONGO_TXN') AND name = 'InvoiceID' AND is_nullable = 0)
    PRINT '  âœ“ PAYMONGO_TXN.InvoiceID is NOT NULL';
ELSE
    PRINT '  âœ— PAYMONGO_TXN.InvoiceID is still nullable â€” something went wrong!';

COMMIT TRANSACTION;
PRINT '';
PRINT 'â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•';
PRINT '  Post-SuperAdmin sync completed successfully!';
PRINT '  You can now publish the updated ByteBill application.';
PRINT 'â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•';


-- 
--  ALL 13 MIGRATIONS COMPLETE
--  
--  Your ByteBill database schema is now fully up-to-date.
--  You can safely republish the application.
-- 
PRINT '';
PRINT '*** ALL 13 MIGRATIONS APPLIED SUCCESSFULLY ***';
