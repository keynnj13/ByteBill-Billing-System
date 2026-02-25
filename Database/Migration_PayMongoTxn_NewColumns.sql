/*******************************************************************************
 *  ByteBill: Web-Based Billing System for Computer Repair Services
 *  MIGRATION SCRIPT — PayMongo Transaction Table + New Columns
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
 *  ⚠️  Run this ONCE on an existing database.
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
        PRINT '✅  Created table PAYMONGO_TXN with FK to PAYMENTS';
    END
    ELSE
        PRINT '✅  Created table PAYMONGO_TXN (FK to PAYMENTS deferred — run base deploy first)';
END
ELSE
    PRINT '⏭️  PAYMONGO_TXN table already exists';

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
    PRINT '✅  Added CheckoutUrl column to PAYMONGO_TXN';
END
ELSE
    PRINT '⏭️  CheckoutUrl column already exists';

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
    PRINT '✅  Added ResourceType column to PAYMONGO_TXN';
END
ELSE
    PRINT '⏭️  ResourceType column already exists';

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
    PRINT '✅  Added UpdatedAt column to PAYMONGO_TXN';
END
ELSE
    PRINT '⏭️  UpdatedAt column already exists';

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
    PRINT '✅  Created index IX_PAYMONGO_TXN_PayMongoPaymentIntentID';
END
ELSE
    PRINT '⏭️  Index IX_PAYMONGO_TXN_PayMongoPaymentIntentID already exists';

COMMIT TRANSACTION;
PRINT '';
PRINT '🎉  Migration complete — PayMongo Transaction new columns applied.';
GO
