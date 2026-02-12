/*  ================================================================
    ByteBillDB – Schema Enhancement Script  (V2)
    Purpose : Add missing columns to support the full ByteBill UI.
    Author  : ByteBill Dev Team
    Date    : 2025-06-XX
    ================================================================
    Adds the following:
      SERVICE_CATALOG  → Description, EstimatedDuration
      CUSTOMERS        → Notes, IsActive
      INVENTORY_ITEMS  → Description, Category, Brand
      INVOICES         → DueDate, TaxRate, TaxAmount, DiscountAmount, Notes
      PAYMENTS         → PaymentNo, Notes
    ================================================================
    This script is idempotent – safe to run multiple times.
    ================================================================ */

USE ByteBillDB;
GO

SET NOCOUNT ON;
SET XACT_ABORT ON;
GO

PRINT '================================================================';
PRINT '  ByteBillDB – Schema Enhancement Script V2';
PRINT '  Started : ' + CONVERT(NVARCHAR(30), SYSDATETIME(), 120);
PRINT '================================================================';
PRINT '';
GO

-- ================================================================
--  SECTION 1 : SERVICE_CATALOG – Add Description & EstimatedDuration
-- ================================================================
PRINT '--- Section 1: SERVICE_CATALOG ---';

IF NOT EXISTS (
    SELECT 1 FROM INFORMATION_SCHEMA.COLUMNS
    WHERE TABLE_NAME = 'SERVICE_CATALOG' AND COLUMN_NAME = 'Description'
)
BEGIN
    ALTER TABLE SERVICE_CATALOG ADD [Description] NVARCHAR(255) NULL;
    PRINT '  + Added SERVICE_CATALOG.Description (NVARCHAR(255) NULL)';
END
ELSE
    PRINT '  = SERVICE_CATALOG.Description already exists – skipped.';
GO

IF NOT EXISTS (
    SELECT 1 FROM INFORMATION_SCHEMA.COLUMNS
    WHERE TABLE_NAME = 'SERVICE_CATALOG' AND COLUMN_NAME = 'EstimatedDuration'
)
BEGIN
    ALTER TABLE SERVICE_CATALOG ADD [EstimatedDuration] INT NULL;  -- stored in minutes
    PRINT '  + Added SERVICE_CATALOG.EstimatedDuration (INT NULL, minutes)';
END
ELSE
    PRINT '  = SERVICE_CATALOG.EstimatedDuration already exists – skipped.';
GO

-- ================================================================
--  SECTION 2 : CUSTOMERS – Add Notes & IsActive
-- ================================================================
PRINT '';
PRINT '--- Section 2: CUSTOMERS ---';

IF NOT EXISTS (
    SELECT 1 FROM INFORMATION_SCHEMA.COLUMNS
    WHERE TABLE_NAME = 'CUSTOMERS' AND COLUMN_NAME = 'Notes'
)
BEGIN
    ALTER TABLE CUSTOMERS ADD [Notes] NVARCHAR(255) NULL;
    PRINT '  + Added CUSTOMERS.Notes (NVARCHAR(255) NULL)';
END
ELSE
    PRINT '  = CUSTOMERS.Notes already exists – skipped.';
GO

IF NOT EXISTS (
    SELECT 1 FROM INFORMATION_SCHEMA.COLUMNS
    WHERE TABLE_NAME = 'CUSTOMERS' AND COLUMN_NAME = 'IsActive'
)
BEGIN
    ALTER TABLE CUSTOMERS ADD [IsActive] BIT NOT NULL CONSTRAINT DF_CUSTOMERS_IsActive DEFAULT 1;
    PRINT '  + Added CUSTOMERS.IsActive (BIT NOT NULL DEFAULT 1)';
END
ELSE
    PRINT '  = CUSTOMERS.IsActive already exists – skipped.';
GO

-- ================================================================
--  SECTION 3 : INVENTORY_ITEMS – Add Description, Category, Brand
-- ================================================================
PRINT '';
PRINT '--- Section 3: INVENTORY_ITEMS ---';

IF NOT EXISTS (
    SELECT 1 FROM INFORMATION_SCHEMA.COLUMNS
    WHERE TABLE_NAME = 'INVENTORY_ITEMS' AND COLUMN_NAME = 'Description'
)
BEGIN
    ALTER TABLE INVENTORY_ITEMS ADD [Description] NVARCHAR(255) NULL;
    PRINT '  + Added INVENTORY_ITEMS.Description (NVARCHAR(255) NULL)';
END
ELSE
    PRINT '  = INVENTORY_ITEMS.Description already exists – skipped.';
GO

IF NOT EXISTS (
    SELECT 1 FROM INFORMATION_SCHEMA.COLUMNS
    WHERE TABLE_NAME = 'INVENTORY_ITEMS' AND COLUMN_NAME = 'Category'
)
BEGIN
    ALTER TABLE INVENTORY_ITEMS ADD [Category] NVARCHAR(50) NULL;
    PRINT '  + Added INVENTORY_ITEMS.Category (NVARCHAR(50) NULL)';
END
ELSE
    PRINT '  = INVENTORY_ITEMS.Category already exists – skipped.';
GO

IF NOT EXISTS (
    SELECT 1 FROM INFORMATION_SCHEMA.COLUMNS
    WHERE TABLE_NAME = 'INVENTORY_ITEMS' AND COLUMN_NAME = 'Brand'
)
BEGIN
    ALTER TABLE INVENTORY_ITEMS ADD [Brand] NVARCHAR(50) NULL;
    PRINT '  + Added INVENTORY_ITEMS.Brand (NVARCHAR(50) NULL)';
END
ELSE
    PRINT '  = INVENTORY_ITEMS.Brand already exists – skipped.';
GO

-- ================================================================
--  SECTION 4 : INVOICES – Add DueDate, TaxRate, TaxAmount,
--              DiscountAmount, Notes
-- ================================================================
PRINT '';
PRINT '--- Section 4: INVOICES ---';

IF NOT EXISTS (
    SELECT 1 FROM INFORMATION_SCHEMA.COLUMNS
    WHERE TABLE_NAME = 'INVOICES' AND COLUMN_NAME = 'DueDate'
)
BEGIN
    ALTER TABLE INVOICES ADD [DueDate] DATETIME2(0) NULL;
    PRINT '  + Added INVOICES.DueDate (DATETIME2(0) NULL)';
END
ELSE
    PRINT '  = INVOICES.DueDate already exists – skipped.';
GO

IF NOT EXISTS (
    SELECT 1 FROM INFORMATION_SCHEMA.COLUMNS
    WHERE TABLE_NAME = 'INVOICES' AND COLUMN_NAME = 'TaxRate'
)
BEGIN
    ALTER TABLE INVOICES ADD [TaxRate] DECIMAL(5,2) NOT NULL CONSTRAINT DF_INVOICES_TaxRate DEFAULT 0;
    PRINT '  + Added INVOICES.TaxRate (DECIMAL(5,2) NOT NULL DEFAULT 0)';
END
ELSE
    PRINT '  = INVOICES.TaxRate already exists – skipped.';
GO

IF NOT EXISTS (
    SELECT 1 FROM INFORMATION_SCHEMA.COLUMNS
    WHERE TABLE_NAME = 'INVOICES' AND COLUMN_NAME = 'TaxAmount'
)
BEGIN
    ALTER TABLE INVOICES ADD [TaxAmount] DECIMAL(8,2) NOT NULL CONSTRAINT DF_INVOICES_TaxAmount DEFAULT 0;
    PRINT '  + Added INVOICES.TaxAmount (DECIMAL(8,2) NOT NULL DEFAULT 0)';
END
ELSE
    PRINT '  = INVOICES.TaxAmount already exists – skipped.';
GO

IF NOT EXISTS (
    SELECT 1 FROM INFORMATION_SCHEMA.COLUMNS
    WHERE TABLE_NAME = 'INVOICES' AND COLUMN_NAME = 'DiscountAmount'
)
BEGIN
    ALTER TABLE INVOICES ADD [DiscountAmount] DECIMAL(8,2) NOT NULL CONSTRAINT DF_INVOICES_DiscountAmount DEFAULT 0;
    PRINT '  + Added INVOICES.DiscountAmount (DECIMAL(8,2) NOT NULL DEFAULT 0)';
END
ELSE
    PRINT '  = INVOICES.DiscountAmount already exists – skipped.';
GO

IF NOT EXISTS (
    SELECT 1 FROM INFORMATION_SCHEMA.COLUMNS
    WHERE TABLE_NAME = 'INVOICES' AND COLUMN_NAME = 'Notes'
)
BEGIN
    ALTER TABLE INVOICES ADD [Notes] NVARCHAR(255) NULL;
    PRINT '  + Added INVOICES.Notes (NVARCHAR(255) NULL)';
END
ELSE
    PRINT '  = INVOICES.Notes already exists – skipped.';
GO

-- ================================================================
--  SECTION 5 : PAYMENTS – Add PaymentNo, Notes
-- ================================================================
PRINT '';
PRINT '--- Section 5: PAYMENTS ---';

IF NOT EXISTS (
    SELECT 1 FROM INFORMATION_SCHEMA.COLUMNS
    WHERE TABLE_NAME = 'PAYMENTS' AND COLUMN_NAME = 'PaymentNo'
)
BEGIN
    ALTER TABLE PAYMENTS ADD [PaymentNo] NVARCHAR(30) NULL;
    PRINT '  + Added PAYMENTS.PaymentNo (NVARCHAR(30) NULL)';
END
ELSE
    PRINT '  = PAYMENTS.PaymentNo already exists – checking data integrity...';
GO

-- Back-fill any NULL PaymentNo values
IF EXISTS (SELECT 1 FROM PAYMENTS WHERE PaymentNo IS NULL)
BEGIN
    UPDATE PAYMENTS
    SET PaymentNo = 'PAY-' + RIGHT('000000' + CAST(PaymentID AS NVARCHAR(6)), 6)
    WHERE PaymentNo IS NULL;
    PRINT '  + Back-filled PaymentNo for rows with NULL values.';
END
ELSE
    PRINT '  = All PAYMENTS rows already have PaymentNo values.';
GO

-- Ensure PaymentNo is NOT NULL
IF EXISTS (
    SELECT 1 FROM INFORMATION_SCHEMA.COLUMNS
    WHERE TABLE_NAME = 'PAYMENTS' AND COLUMN_NAME = 'PaymentNo' AND IS_NULLABLE = 'YES'
)
BEGIN
    ALTER TABLE PAYMENTS ALTER COLUMN [PaymentNo] NVARCHAR(30) NOT NULL;
    PRINT '  + Set PAYMENTS.PaymentNo to NOT NULL.';
END
ELSE
    PRINT '  = PAYMENTS.PaymentNo is already NOT NULL.';
GO

-- Unique constraint on PaymentNo within a shop
IF NOT EXISTS (
    SELECT 1 FROM sys.indexes
    WHERE name = 'UQ_PAYMENTS_PaymentNo' AND object_id = OBJECT_ID('PAYMENTS')
)
BEGIN
    ALTER TABLE PAYMENTS ADD CONSTRAINT UQ_PAYMENTS_PaymentNo UNIQUE (ShopID, PaymentNo);
    PRINT '  + Added unique constraint UQ_PAYMENTS_PaymentNo (ShopID, PaymentNo)';
END
ELSE
    PRINT '  = UQ_PAYMENTS_PaymentNo already exists – skipped.';
GO

IF NOT EXISTS (
    SELECT 1 FROM INFORMATION_SCHEMA.COLUMNS
    WHERE TABLE_NAME = 'PAYMENTS' AND COLUMN_NAME = 'Notes'
)
BEGIN
    ALTER TABLE PAYMENTS ADD [Notes] NVARCHAR(255) NULL;
    PRINT '  + Added PAYMENTS.Notes (NVARCHAR(255) NULL)';
END
ELSE
    PRINT '  = PAYMENTS.Notes already exists – skipped.';
GO

-- ================================================================
--  SECTION 6 : VERIFICATION
-- ================================================================
PRINT '';
PRINT '--- Section 6: Verification ---';
PRINT '';

SELECT
    t.TABLE_NAME,
    c.COLUMN_NAME,
    c.DATA_TYPE,
    c.CHARACTER_MAXIMUM_LENGTH,
    c.NUMERIC_PRECISION,
    c.NUMERIC_SCALE,
    c.IS_NULLABLE,
    c.COLUMN_DEFAULT
FROM INFORMATION_SCHEMA.TABLES t
JOIN INFORMATION_SCHEMA.COLUMNS c ON t.TABLE_NAME = c.TABLE_NAME
WHERE (
    (t.TABLE_NAME = 'SERVICE_CATALOG'  AND c.COLUMN_NAME IN ('Description','EstimatedDuration'))
 OR (t.TABLE_NAME = 'CUSTOMERS'        AND c.COLUMN_NAME IN ('Notes','IsActive'))
 OR (t.TABLE_NAME = 'INVENTORY_ITEMS'  AND c.COLUMN_NAME IN ('Description','Category','Brand'))
 OR (t.TABLE_NAME = 'INVOICES'         AND c.COLUMN_NAME IN ('DueDate','TaxRate','TaxAmount','DiscountAmount','Notes'))
 OR (t.TABLE_NAME = 'PAYMENTS'         AND c.COLUMN_NAME IN ('PaymentNo','Notes'))
)
ORDER BY t.TABLE_NAME, c.ORDINAL_POSITION;
GO

PRINT '';
PRINT '================================================================';
PRINT '  ByteBillDB – Schema Enhancement V2 COMPLETE';
PRINT '  Finished: ' + CONVERT(NVARCHAR(30), SYSDATETIME(), 120);
PRINT '================================================================';
GO
