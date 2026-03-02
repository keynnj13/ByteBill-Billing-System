-- ═══════════════════════════════════════════════════════════════════════════
--  ByteBill Post-SuperAdmin Database Sync
--  Run this ONCE on the MonsterASP online database AFTER the SuperAdmin
--  migration has already been applied.
--
--  What it does:
--    1. Adds missing columns   (JOB_ORDERS: Priority, EstimatedCompletionDate)
--    2. Fixes nullability       (AUDIT_LOG.UserID → nullable, PAYMONGO_TXN cols → NOT NULL)
--    3. Adds missing FK         (PAYMONGO_TXN.ShopID → SHOP)
--    4. Widens NVARCHAR columns (to match C# MaxLength attributes)
--    5. Widens DECIMAL columns  (from 6,2/8,2 → 18,2)
--    6. Fixes datetime precision (PAYMONGO_TXN.UpdatedAt → DATETIME2(0))
--
--  Safe to re-run: every ALTER is guarded with IF NOT EXISTS / IF checks.
-- ═══════════════════════════════════════════════════════════════════════════

SET XACT_ABORT ON;
BEGIN TRANSACTION;

-- ══════════════════════════════════════════════════════════════════════════
-- 1. MISSING COLUMNS — App will crash without these
-- ══════════════════════════════════════════════════════════════════════════

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

-- ══════════════════════════════════════════════════════════════════════════
-- 2. NULLABILITY FIXES
-- ══════════════════════════════════════════════════════════════════════════

-- 2a. AUDIT_LOG.UserID → BIGINT NULL (C# model: long? UserId)
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

-- 2b. PAYMONGO_TXN.ShopID → BIGINT NOT NULL
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

-- 2c. PAYMONGO_TXN.InvoiceID → BIGINT NOT NULL
IF EXISTS (SELECT 1 FROM sys.columns WHERE object_id = OBJECT_ID('PAYMONGO_TXN') AND name = 'InvoiceID' AND is_nullable = 1)
BEGIN
    -- Delete orphan rows with no InvoiceID (can't be fixed)
    DELETE FROM [PAYMONGO_TXN] WHERE [InvoiceID] IS NULL;

    ALTER TABLE [PAYMONGO_TXN] ALTER COLUMN [InvoiceID] BIGINT NOT NULL;
    PRINT '  PAYMONGO_TXN.InvoiceID changed to NOT NULL';
END

-- ══════════════════════════════════════════════════════════════════════════
-- 3. MISSING FOREIGN KEY CONSTRAINT
-- ══════════════════════════════════════════════════════════════════════════

-- 3a. PAYMONGO_TXN.ShopID → SHOP(ShopID)
IF NOT EXISTS (
    SELECT 1 FROM sys.foreign_keys fk
    INNER JOIN sys.foreign_key_columns fkc ON fk.object_id = fkc.constraint_object_id
    WHERE fk.parent_object_id = OBJECT_ID('PAYMONGO_TXN')
      AND COL_NAME(fk.parent_object_id, fkc.parent_column_id) = 'ShopID'
)
BEGIN
    ALTER TABLE [PAYMONGO_TXN] ADD CONSTRAINT [FK_PAYMONGO_TXN_SHOP]
        FOREIGN KEY ([ShopID]) REFERENCES [SHOP]([ShopID]);
    PRINT '  Added FK PAYMONGO_TXN.ShopID → SHOP';
END

-- ══════════════════════════════════════════════════════════════════════════
-- 4. WIDEN NVARCHAR COLUMNS (prevent truncation errors)
-- ══════════════════════════════════════════════════════════════════════════

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

-- ══════════════════════════════════════════════════════════════════════════
-- 5. WIDEN DECIMAL COLUMNS (prevent arithmetic overflow)
--    From DECIMAL(6,2) / DECIMAL(8,2) → DECIMAL(18,2)
--    Must drop/recreate computed columns that depend on UnitPrice
-- ══════════════════════════════════════════════════════════════════════════

-- SERVICE_CATALOG
ALTER TABLE [SERVICE_CATALOG] ALTER COLUMN [BasePrice] DECIMAL(18,2) NOT NULL;
PRINT '  Widened SERVICE_CATALOG.BasePrice';

-- INVENTORY_ITEMS
ALTER TABLE [INVENTORY_ITEMS] ALTER COLUMN [UnitCost]  DECIMAL(18,2) NOT NULL;
ALTER TABLE [INVENTORY_ITEMS] ALTER COLUMN [UnitPrice] DECIMAL(18,2) NOT NULL;
PRINT '  Widened INVENTORY_ITEMS decimal columns';

-- JOB_ORDER_SERVICES — has computed LineTotal = Qty * UnitPrice
IF EXISTS (SELECT 1 FROM sys.computed_columns WHERE object_id = OBJECT_ID('JOB_ORDER_SERVICES') AND name = 'LineTotal')
    ALTER TABLE [JOB_ORDER_SERVICES] DROP COLUMN [LineTotal];
ALTER TABLE [JOB_ORDER_SERVICES] ALTER COLUMN [UnitPrice] DECIMAL(18,2) NOT NULL;
ALTER TABLE [JOB_ORDER_SERVICES] ADD [LineTotal] AS ([Qty] * [UnitPrice]) PERSISTED;
PRINT '  Widened JOB_ORDER_SERVICES.UnitPrice (rebuilt computed LineTotal)';

-- JOB_ORDER_PARTS — has computed LineTotal = QtyUsed * UnitPrice
IF EXISTS (SELECT 1 FROM sys.computed_columns WHERE object_id = OBJECT_ID('JOB_ORDER_PARTS') AND name = 'LineTotal')
    ALTER TABLE [JOB_ORDER_PARTS] DROP COLUMN [LineTotal];
ALTER TABLE [JOB_ORDER_PARTS] ALTER COLUMN [UnitPrice] DECIMAL(18,2) NOT NULL;
ALTER TABLE [JOB_ORDER_PARTS] ADD [LineTotal] AS ([QtyUsed] * [UnitPrice]) PERSISTED;
PRINT '  Widened JOB_ORDER_PARTS.UnitPrice (rebuilt computed LineTotal)';

-- INVOICE_LINES — has computed LineTotal = Qty * UnitPrice
IF EXISTS (SELECT 1 FROM sys.computed_columns WHERE object_id = OBJECT_ID('INVOICE_LINES') AND name = 'LineTotal')
    ALTER TABLE [INVOICE_LINES] DROP COLUMN [LineTotal];
ALTER TABLE [INVOICE_LINES] ALTER COLUMN [UnitPrice] DECIMAL(18,2) NOT NULL;
ALTER TABLE [INVOICE_LINES] ADD [LineTotal] AS ([Qty] * [UnitPrice]) PERSISTED;
PRINT '  Widened INVOICE_LINES.UnitPrice (rebuilt computed LineTotal)';

-- INVOICES — all money columns (handle indexes containing these columns)
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

-- ══════════════════════════════════════════════════════════════════════════
-- 6. DATETIME PRECISION FIX
-- ══════════════════════════════════════════════════════════════════════════

-- PAYMONGO_TXN.UpdatedAt should be DATETIME2(0), not DATETIME2(7)
ALTER TABLE [PAYMONGO_TXN] ALTER COLUMN [UpdatedAt] DATETIME2(0) NULL;
PRINT '  Fixed PAYMONGO_TXN.UpdatedAt precision';

-- ══════════════════════════════════════════════════════════════════════════
-- 7. VERIFICATION QUERIES (informational — check counts after running)
-- ══════════════════════════════════════════════════════════════════════════

PRINT '';
PRINT '═══ VERIFICATION ═══';
PRINT 'Checking JOB_ORDERS.Priority exists...';
IF EXISTS (SELECT 1 FROM sys.columns WHERE object_id = OBJECT_ID('JOB_ORDERS') AND name = 'Priority')
    PRINT '  ✓ JOB_ORDERS.Priority exists';
ELSE
    PRINT '  ✗ JOB_ORDERS.Priority MISSING — something went wrong!';

PRINT 'Checking AUDIT_LOG.UserID is nullable...';
IF EXISTS (SELECT 1 FROM sys.columns WHERE object_id = OBJECT_ID('AUDIT_LOG') AND name = 'UserID' AND is_nullable = 1)
    PRINT '  ✓ AUDIT_LOG.UserID is nullable';
ELSE
    PRINT '  ✗ AUDIT_LOG.UserID is still NOT NULL — something went wrong!';

PRINT 'Checking PAYMONGO_TXN.ShopID is NOT NULL...';
IF EXISTS (SELECT 1 FROM sys.columns WHERE object_id = OBJECT_ID('PAYMONGO_TXN') AND name = 'ShopID' AND is_nullable = 0)
    PRINT '  ✓ PAYMONGO_TXN.ShopID is NOT NULL';
ELSE
    PRINT '  ✗ PAYMONGO_TXN.ShopID is still nullable — something went wrong!';

PRINT 'Checking PAYMONGO_TXN.InvoiceID is NOT NULL...';
IF EXISTS (SELECT 1 FROM sys.columns WHERE object_id = OBJECT_ID('PAYMONGO_TXN') AND name = 'InvoiceID' AND is_nullable = 0)
    PRINT '  ✓ PAYMONGO_TXN.InvoiceID is NOT NULL';
ELSE
    PRINT '  ✗ PAYMONGO_TXN.InvoiceID is still nullable — something went wrong!';

COMMIT TRANSACTION;
PRINT '';
PRINT '═══════════════════════════════════════════════════════════════';
PRINT '  Post-SuperAdmin sync completed successfully!';
PRINT '  You can now publish the updated ByteBill application.';
PRINT '═══════════════════════════════════════════════════════════════';
