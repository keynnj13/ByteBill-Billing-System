-- ============================================================================
--  MIGRATION: PayMongo Refactor — No pre-created payments, actual method tracking
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

-- ───────────────────────────────────────────────────────
-- 1. Add ShopID column to PAYMONGO_TXN
-- ───────────────────────────────────────────────────────
IF NOT EXISTS (SELECT 1 FROM INFORMATION_SCHEMA.COLUMNS WHERE TABLE_NAME='PAYMONGO_TXN' AND COLUMN_NAME='ShopID')
BEGIN
    ALTER TABLE [dbo].[PAYMONGO_TXN] ADD [ShopID] BIGINT NULL;
    PRINT '✅ Added ShopID column';
END
ELSE PRINT '⏭️ ShopID already exists';
GO

-- Backfill ShopID from linked Payment
IF COL_LENGTH('PAYMONGO_TXN','ShopID') IS NOT NULL
    EXEC('UPDATE t SET t.ShopID = p.ShopID FROM [dbo].[PAYMONGO_TXN] t INNER JOIN [dbo].[PAYMENTS] p ON t.PaymentID = p.PaymentID WHERE t.ShopID IS NULL');
GO

-- ───────────────────────────────────────────────────────
-- 2. Add InvoiceID column
-- ───────────────────────────────────────────────────────
IF NOT EXISTS (SELECT 1 FROM INFORMATION_SCHEMA.COLUMNS WHERE TABLE_NAME='PAYMONGO_TXN' AND COLUMN_NAME='InvoiceID')
BEGIN
    ALTER TABLE [dbo].[PAYMONGO_TXN] ADD [InvoiceID] BIGINT NULL;
    PRINT '✅ Added InvoiceID column';
END
ELSE PRINT '⏭️ InvoiceID already exists';
GO

-- Backfill InvoiceID from PaymentAllocations
IF COL_LENGTH('PAYMONGO_TXN','InvoiceID') IS NOT NULL
    EXEC('UPDATE t SET t.InvoiceID = pa.InvoiceID FROM [dbo].[PAYMONGO_TXN] t INNER JOIN [dbo].[PAYMENT_ALLOCATION] pa ON t.PaymentID = pa.PaymentID WHERE t.InvoiceID IS NULL');
GO

-- ───────────────────────────────────────────────────────
-- 3. Add InitiatedByUserID column
-- ───────────────────────────────────────────────────────
IF NOT EXISTS (SELECT 1 FROM INFORMATION_SCHEMA.COLUMNS WHERE TABLE_NAME='PAYMONGO_TXN' AND COLUMN_NAME='InitiatedByUserID')
BEGIN
    ALTER TABLE [dbo].[PAYMONGO_TXN] ADD [InitiatedByUserID] BIGINT NOT NULL DEFAULT(0);
    PRINT '✅ Added InitiatedByUserID column';
END
ELSE PRINT '⏭️ InitiatedByUserID already exists';
GO

-- Backfill InitiatedByUserID from linked Payment
IF COL_LENGTH('PAYMONGO_TXN','InitiatedByUserID') IS NOT NULL
    EXEC('UPDATE t SET t.InitiatedByUserID = p.ReceivedByUserID FROM [dbo].[PAYMONGO_TXN] t INNER JOIN [dbo].[PAYMENTS] p ON t.PaymentID = p.PaymentID WHERE t.InitiatedByUserID = 0 AND p.ReceivedByUserID IS NOT NULL');
GO

-- ───────────────────────────────────────────────────────
-- 4. Add Amount column
-- ───────────────────────────────────────────────────────
IF NOT EXISTS (SELECT 1 FROM INFORMATION_SCHEMA.COLUMNS WHERE TABLE_NAME='PAYMONGO_TXN' AND COLUMN_NAME='Amount')
BEGIN
    ALTER TABLE [dbo].[PAYMONGO_TXN] ADD [Amount] DECIMAL(18,2) NOT NULL DEFAULT(0);
    PRINT '✅ Added Amount column';
END
ELSE PRINT '⏭️ Amount already exists';
GO

-- Backfill Amount from linked Payment
IF COL_LENGTH('PAYMONGO_TXN','Amount') IS NOT NULL
    EXEC('UPDATE t SET t.Amount = p.Amount FROM [dbo].[PAYMONGO_TXN] t INNER JOIN [dbo].[PAYMENTS] p ON t.PaymentID = p.PaymentID WHERE t.Amount = 0');
GO

-- ───────────────────────────────────────────────────────
-- 5. Add PayMongoPaymentMethod column
-- ───────────────────────────────────────────────────────
IF NOT EXISTS (SELECT 1 FROM INFORMATION_SCHEMA.COLUMNS WHERE TABLE_NAME='PAYMONGO_TXN' AND COLUMN_NAME='PayMongoPaymentMethod')
BEGIN
    ALTER TABLE [dbo].[PAYMONGO_TXN] ADD [PayMongoPaymentMethod] NVARCHAR(30) NULL;
    PRINT '✅ Added PayMongoPaymentMethod column';
END
ELSE PRINT '⏭️ PayMongoPaymentMethod already exists';
GO

-- ───────────────────────────────────────────────────────
-- 6. Make PaymentID nullable
-- ───────────────────────────────────────────────────────
-- Drop existing unique index first
IF EXISTS (SELECT 1 FROM sys.indexes WHERE name='IX_PAYMONGO_TXN_PaymentID' AND object_id=OBJECT_ID('dbo.PAYMONGO_TXN'))
BEGIN
    DROP INDEX [IX_PAYMONGO_TXN_PaymentID] ON [dbo].[PAYMONGO_TXN];
    PRINT '✅ Dropped old unique index on PaymentID';
END
ELSE PRINT '⏭️ Index already dropped or does not exist';

-- Drop the unique constraint if it exists
IF EXISTS (SELECT 1 FROM sys.key_constraints WHERE name='UQ_PAYMONGO_TXN_PaymentID' AND parent_object_id=OBJECT_ID('dbo.PAYMONGO_TXN'))
BEGIN
    ALTER TABLE [dbo].[PAYMONGO_TXN] DROP CONSTRAINT [UQ_PAYMONGO_TXN_PaymentID];
    PRINT '✅ Dropped unique constraint UQ_PAYMONGO_TXN_PaymentID';
END
ELSE PRINT '⏭️ Unique constraint already dropped or does not exist';

-- Drop FK constraint
IF EXISTS (SELECT 1 FROM sys.foreign_keys WHERE name='FK_PAYMONGO_TXN_Payment' AND parent_object_id=OBJECT_ID('dbo.PAYMONGO_TXN'))
BEGIN
    ALTER TABLE [dbo].[PAYMONGO_TXN] DROP CONSTRAINT [FK_PAYMONGO_TXN_Payment];
    PRINT '✅ Dropped FK constraint FK_PAYMONGO_TXN_Payment';
END
ELSE PRINT '⏭️ FK constraint already dropped or does not exist';

-- Check if PaymentID column is currently NOT NULL, and alter to NULL
IF EXISTS (
    SELECT 1 FROM INFORMATION_SCHEMA.COLUMNS
    WHERE TABLE_NAME='PAYMONGO_TXN' AND COLUMN_NAME='PaymentID' AND IS_NULLABLE='NO'
)
BEGIN
    ALTER TABLE [dbo].[PAYMONGO_TXN] ALTER COLUMN [PaymentID] BIGINT NULL;
    PRINT '✅ Made PaymentID nullable';
END
ELSE PRINT '⏭️ PaymentID is already nullable';

-- Re-add FK (now nullable)
IF NOT EXISTS (SELECT 1 FROM sys.foreign_keys WHERE name='FK_PAYMONGO_TXN_Payment' AND parent_object_id=OBJECT_ID('dbo.PAYMONGO_TXN'))
BEGIN
    ALTER TABLE [dbo].[PAYMONGO_TXN]
        ADD CONSTRAINT [FK_PAYMONGO_TXN_Payment]
        FOREIGN KEY ([PaymentID]) REFERENCES [dbo].[PAYMENTS] ([PaymentID])
        ON DELETE NO ACTION ON UPDATE NO ACTION;
    PRINT '✅ Re-added FK on PaymentID (nullable)';
END
ELSE PRINT '⏭️ FK already exists';

-- Re-add filtered unique index
IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE name='IX_PAYMONGO_TXN_PaymentID' AND object_id=OBJECT_ID('dbo.PAYMONGO_TXN'))
BEGIN
    CREATE UNIQUE NONCLUSTERED INDEX [IX_PAYMONGO_TXN_PaymentID]
        ON [dbo].[PAYMONGO_TXN] ([PaymentID])
        WHERE [PaymentID] IS NOT NULL;
    PRINT '✅ Created filtered unique index on PaymentID';
END
ELSE PRINT '⏭️ Filtered unique index already exists';
GO

-- ───────────────────────────────────────────────────────
-- 7. Widen RawResponse to NVARCHAR(MAX)
-- ───────────────────────────────────────────────────────
IF EXISTS (
    SELECT 1 FROM INFORMATION_SCHEMA.COLUMNS
    WHERE TABLE_NAME='PAYMONGO_TXN' AND COLUMN_NAME='RawResponse' AND CHARACTER_MAXIMUM_LENGTH <> -1
)
BEGIN
    ALTER TABLE [dbo].[PAYMONGO_TXN] ALTER COLUMN [RawResponse] NVARCHAR(MAX) NULL;
    PRINT '✅ Widened RawResponse to NVARCHAR(MAX)';
END
ELSE PRINT '⏭️ RawResponse is already NVARCHAR(MAX)';
GO

-- ───────────────────────────────────────────────────────
-- 8. CLEANUP orphan PayMongo payments FIRST (before CHECK constraint change)
-- ───────────────────────────────────────────────────────
PRINT '';
PRINT '=== Cleanup orphan PayMongo records ===';

-- Update confirmed 'PayMongo' method payments to 'Card' so they survive the new CHECK
UPDATE [dbo].[PAYMENTS]
SET Method = 'Card'
WHERE Method = 'PayMongo' AND [Status] = 'Confirmed';
PRINT '✅ Updated confirmed PayMongo payments to Card method';

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
PRINT '✅ Removed orphan pending/failed PayMongo payments';
GO

-- ───────────────────────────────────────────────────────
-- 9. Update PAYMENTS CHECK constraint (remove 'PayMongo')
--    Done AFTER cleanup so no rows violate the new constraint
-- ───────────────────────────────────────────────────────
IF EXISTS (SELECT 1 FROM sys.check_constraints WHERE name='CK_PAYMENTS_Method' AND parent_object_id=OBJECT_ID('dbo.PAYMENTS'))
BEGIN
    ALTER TABLE [dbo].[PAYMENTS] DROP CONSTRAINT [CK_PAYMENTS_Method];
    ALTER TABLE [dbo].[PAYMENTS]
        ADD CONSTRAINT [CK_PAYMENTS_Method] CHECK (Method IN ('Cash','GCash','Card'));
    PRINT '✅ Updated PAYMENTS method check constraint (removed PayMongo)';
END
ELSE PRINT '⏭️ CHECK constraint already updated or does not exist';
GO

-- ───────────────────────────────────────────────────────
-- 10. Add FK for InvoiceID if not exists
-- ───────────────────────────────────────────────────────
IF NOT EXISTS (SELECT 1 FROM sys.foreign_keys WHERE name='FK_PAYMONGO_TXN_Invoice' AND parent_object_id=OBJECT_ID('dbo.PAYMONGO_TXN'))
BEGIN
    ALTER TABLE [dbo].[PAYMONGO_TXN]
        ADD CONSTRAINT [FK_PAYMONGO_TXN_Invoice]
        FOREIGN KEY ([InvoiceID]) REFERENCES [dbo].[INVOICES] ([InvoiceID])
        ON DELETE NO ACTION ON UPDATE NO ACTION;
    PRINT '✅ Added FK on InvoiceID';
END
ELSE PRINT '⏭️ FK on InvoiceID already exists';

PRINT '';
PRINT '=== Migration complete ===';
