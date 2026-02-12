/*******************************************************************************
 *  ByteBillDB - Safe ALTER Script
 *  Updates NVARCHAR lengths and DECIMAL precision per updated Data Dictionary.
 *
 *  SAFETY GUARANTEES:
 *    - NO tables dropped.
 *    - NO primary keys dropped.
 *    - NO foreign keys dropped.
 *    - Indexes are temporarily dropped ONLY when blocking ALTER COLUMN, then recreated.
 *    - All existing DEFAULT and CHECK constraints preserved.
 *    - Computed PERSISTED columns (LineTotal) are dropped/recreated ONLY where
 *      ALTER COLUMN cannot be used because SQL Server blocks altering a column
 *      that has a computed-column dependency.  This is not a constraint or index
 *      drop; it is required by the engine.
 *
 *  Before shrinking any column, existing data is validated.
 *  If data would be truncated or overflow, the column is SKIPPED with a warning.
 *
 *  Generated: 2026-02-13 (Updated)
 ******************************************************************************/

USE ByteBillDB;
GO

PRINT '================================================================';
PRINT '  ByteBillDB - Column Precision Migration';
PRINT '  Started: ' + CONVERT(NVARCHAR(30), SYSDATETIME(), 120);
PRINT '================================================================';
PRINT '';
GO


-- ============================================================================
-- SECTION 0: HANDLE INDEX DEPENDENCIES
-- ============================================================================

PRINT '------------------------------------------------------------';
PRINT '  SECTION 0: Handling Index Dependencies';
PRINT '------------------------------------------------------------';
PRINT '';
GO

-- USERS.UserName has index IX_USERS_ShopID_UserName that must be dropped/recreated
IF EXISTS (SELECT 1 FROM sys.indexes WHERE name = 'IX_USERS_ShopID_UserName' AND object_id = OBJECT_ID('USERS'))
BEGIN
    DROP INDEX IX_USERS_ShopID_UserName ON USERS;
    PRINT 'Temporarily dropped index IX_USERS_ShopID_UserName for USERS.UserName ALTER.';
END
GO

PRINT '';
GO


-- ============================================================================
-- SECTION 1: NVARCHAR LENGTH REDUCTIONS
-- ============================================================================

PRINT '------------------------------------------------------------';
PRINT '  SECTION 1: NVARCHAR Length Reductions';
PRINT '------------------------------------------------------------';
PRINT '';
GO

-- ====================== USERS ======================

-- USERS.FirstName: NVARCHAR(50) -> NVARCHAR(20)
IF EXISTS (SELECT 1 FROM USERS WHERE LEN(FirstName) > 20)
BEGIN
    PRINT 'WARNING: USERS.FirstName has values exceeding 20 characters. Skipping.';
END
ELSE
BEGIN
    ALTER TABLE USERS ALTER COLUMN FirstName NVARCHAR(20) NOT NULL;
    PRINT 'OK: USERS.FirstName altered to NVARCHAR(20) NOT NULL.';
END
GO

-- USERS.MiddleName: NVARCHAR(50) -> NVARCHAR(10)
IF EXISTS (SELECT 1 FROM USERS WHERE LEN(MiddleName) > 10)
BEGIN
    PRINT 'WARNING: USERS.MiddleName has values exceeding 10 characters. Skipping.';
END
ELSE
BEGIN
    ALTER TABLE USERS ALTER COLUMN MiddleName NVARCHAR(10) NULL;
    PRINT 'OK: USERS.MiddleName altered to NVARCHAR(10) NULL.';
END
GO

-- USERS.LastName: NVARCHAR(50) -> NVARCHAR(10)
IF EXISTS (SELECT 1 FROM USERS WHERE LEN(LastName) > 10)
BEGIN
    PRINT 'WARNING: USERS.LastName has values exceeding 10 characters. Skipping.';
END
ELSE
BEGIN
    ALTER TABLE USERS ALTER COLUMN LastName NVARCHAR(10) NOT NULL;
    PRINT 'OK: USERS.LastName altered to NVARCHAR(10) NOT NULL.';
END
GO

-- USERS.UserName: NVARCHAR(100) -> NVARCHAR(20)
-- Index IX_USERS_ShopID_UserName was dropped in SECTION 0.
IF EXISTS (SELECT 1 FROM USERS WHERE LEN(UserName) > 20)
BEGIN
    PRINT 'WARNING: USERS.UserName has values exceeding 20 characters. Skipping.';
END
ELSE
BEGIN
    ALTER TABLE USERS ALTER COLUMN UserName NVARCHAR(20) NOT NULL;
    PRINT 'OK: USERS.UserName altered to NVARCHAR(20) NOT NULL.';
END
GO

-- USERS.PasswordHash: NVARCHAR(255) -> NVARCHAR(100)
-- BCrypt hashes are 60 characters; NVARCHAR(100) is sufficient.
IF EXISTS (SELECT 1 FROM USERS WHERE LEN(PasswordHash) > 100)
BEGIN
    PRINT 'WARNING: USERS.PasswordHash has values exceeding 100 characters. Skipping.';
END
ELSE
BEGIN
    ALTER TABLE USERS ALTER COLUMN PasswordHash NVARCHAR(100) NOT NULL;
    PRINT 'OK: USERS.PasswordHash altered to NVARCHAR(100) NOT NULL.';
END
GO

PRINT '';
GO

-- ====================== SHOP ======================

-- SHOP.ShopName: NVARCHAR(150) -> NVARCHAR(30)
IF EXISTS (SELECT 1 FROM SHOP WHERE LEN(ShopName) > 30)
BEGIN
    PRINT 'WARNING: SHOP.ShopName has values exceeding 30 characters. Skipping.';
END
ELSE
BEGIN
    ALTER TABLE SHOP ALTER COLUMN ShopName NVARCHAR(30) NOT NULL;
    PRINT 'OK: SHOP.ShopName altered to NVARCHAR(30) NOT NULL.';
END
GO

-- SHOP.Email: NVARCHAR(100) -> NVARCHAR(30)
IF EXISTS (SELECT 1 FROM SHOP WHERE LEN(Email) > 30)
BEGIN
    PRINT 'WARNING: SHOP.Email has values exceeding 30 characters. Skipping.';
END
ELSE
BEGIN
    ALTER TABLE SHOP ALTER COLUMN Email NVARCHAR(30) NULL;
    PRINT 'OK: SHOP.Email altered to NVARCHAR(30) NULL.';
END
GO

-- SHOP.Address: NVARCHAR(255) -> NVARCHAR(100)
IF EXISTS (SELECT 1 FROM SHOP WHERE LEN([Address]) > 100)
BEGIN
    PRINT 'WARNING: SHOP.Address has values exceeding 100 characters. Skipping.';
END
ELSE
BEGIN
    ALTER TABLE SHOP ALTER COLUMN [Address] NVARCHAR(100) NULL;
    PRINT 'OK: SHOP.Address altered to NVARCHAR(100) NULL.';
END
GO

-- SHOP.Status: NVARCHAR(20) -> NVARCHAR(15)
-- DEFAULT 'Active' (6 chars) is preserved and fits within NVARCHAR(15).
IF EXISTS (SELECT 1 FROM SHOP WHERE LEN([Status]) > 15)
BEGIN
    PRINT 'WARNING: SHOP.Status has values exceeding 15 characters. Skipping.';
END
ELSE
BEGIN
    ALTER TABLE SHOP ALTER COLUMN [Status] NVARCHAR(15) NOT NULL;
    PRINT 'OK: SHOP.Status altered to NVARCHAR(15) NOT NULL.';
END
GO

PRINT '';
GO

-- ====================== CUSTOMERS ======================

-- CUSTOMERS.FirstName: NVARCHAR(50) -> NVARCHAR(20)
IF EXISTS (SELECT 1 FROM CUSTOMERS WHERE LEN(FirstName) > 20)
BEGIN
    PRINT 'WARNING: CUSTOMERS.FirstName has values exceeding 20 characters. Skipping.';
END
ELSE
BEGIN
    ALTER TABLE CUSTOMERS ALTER COLUMN FirstName NVARCHAR(20) NOT NULL;
    PRINT 'OK: CUSTOMERS.FirstName altered to NVARCHAR(20) NOT NULL.';
END
GO

-- CUSTOMERS.MiddleName: NVARCHAR(50) -> NVARCHAR(10)
IF EXISTS (SELECT 1 FROM CUSTOMERS WHERE LEN(MiddleName) > 10)
BEGIN
    PRINT 'WARNING: CUSTOMERS.MiddleName has values exceeding 10 characters. Skipping.';
END
ELSE
BEGIN
    ALTER TABLE CUSTOMERS ALTER COLUMN MiddleName NVARCHAR(10) NULL;
    PRINT 'OK: CUSTOMERS.MiddleName altered to NVARCHAR(10) NULL.';
END
GO

-- CUSTOMERS.LastName: NVARCHAR(50) -> NVARCHAR(10)
IF EXISTS (SELECT 1 FROM CUSTOMERS WHERE LEN(LastName) > 10)
BEGIN
    PRINT 'WARNING: CUSTOMERS.LastName has values exceeding 10 characters. Skipping.';
END
ELSE
BEGIN
    ALTER TABLE CUSTOMERS ALTER COLUMN LastName NVARCHAR(10) NOT NULL;
    PRINT 'OK: CUSTOMERS.LastName altered to NVARCHAR(10) NOT NULL.';
END
GO

-- CUSTOMERS.Email: NVARCHAR(100) -> NVARCHAR(50)
IF EXISTS (SELECT 1 FROM CUSTOMERS WHERE LEN(Email) > 50)
BEGIN
    PRINT 'WARNING: CUSTOMERS.Email has values exceeding 50 characters. Skipping.';
END
ELSE
BEGIN
    ALTER TABLE CUSTOMERS ALTER COLUMN Email NVARCHAR(50) NULL;
    PRINT 'OK: CUSTOMERS.Email altered to NVARCHAR(50) NULL.';
END
GO

-- CUSTOMERS.Address: NVARCHAR(255) -> NVARCHAR(100)
IF EXISTS (SELECT 1 FROM CUSTOMERS WHERE LEN([Address]) > 100)
BEGIN
    PRINT 'WARNING: CUSTOMERS.Address has values exceeding 100 characters. Skipping.';
END
ELSE
BEGIN
    ALTER TABLE CUSTOMERS ALTER COLUMN [Address] NVARCHAR(100) NULL;
    PRINT 'OK: CUSTOMERS.Address altered to NVARCHAR(100) NULL.';
END
GO

PRINT '';
GO

-- ====================== DEVICES ======================

-- DEVICES.Notes: NVARCHAR(255) -> NVARCHAR(150)
IF EXISTS (SELECT 1 FROM DEVICES WHERE LEN(Notes) > 150)
BEGIN
    PRINT 'WARNING: DEVICES.Notes has values exceeding 150 characters. Skipping.';
END
ELSE
BEGIN
    ALTER TABLE DEVICES ALTER COLUMN Notes NVARCHAR(150) NULL;
    PRINT 'OK: DEVICES.Notes altered to NVARCHAR(150) NULL.';
END
GO

PRINT '';
GO

-- ====================== PAYMENTS (NVARCHAR) ======================

-- PAYMENTS.Method: NVARCHAR(30) -> NVARCHAR(10)
-- CHECK constraint CK_PAYMENTS_Method allows: Cash(4), GCash(5), Card(4), PayMongo(8).
-- All values fit within NVARCHAR(10). Constraint is preserved.
IF EXISTS (SELECT 1 FROM PAYMENTS WHERE LEN(Method) > 10)
BEGIN
    PRINT 'WARNING: PAYMENTS.Method has values exceeding 10 characters. Skipping.';
END
ELSE
BEGIN
    ALTER TABLE PAYMENTS ALTER COLUMN Method NVARCHAR(10) NOT NULL;
    PRINT 'OK: PAYMENTS.Method altered to NVARCHAR(10) NOT NULL.';
END
GO

-- PAYMENTS.ReferenceNo: NVARCHAR(60) -> NVARCHAR(30)
IF EXISTS (SELECT 1 FROM PAYMENTS WHERE LEN(ReferenceNo) > 30)
BEGIN
    PRINT 'WARNING: PAYMENTS.ReferenceNo has values exceeding 30 characters. Skipping.';
END
ELSE
BEGIN
    ALTER TABLE PAYMENTS ALTER COLUMN ReferenceNo NVARCHAR(30) NULL;
    PRINT 'OK: PAYMENTS.ReferenceNo altered to NVARCHAR(30) NULL.';
END
GO

PRINT '';
GO

-- ====================== PAYMONGO_TXN ======================

-- PAYMONGO_TXN.RawResponse: NVARCHAR(MAX) -> NVARCHAR(1000)
IF EXISTS (SELECT 1 FROM PAYMONGO_TXN WHERE LEN(RawResponse) > 1000)
BEGIN
    PRINT 'WARNING: PAYMONGO_TXN.RawResponse has values exceeding 1000 characters. Skipping.';
END
ELSE
BEGIN
    ALTER TABLE PAYMONGO_TXN ALTER COLUMN RawResponse NVARCHAR(1000) NULL;
    PRINT 'OK: PAYMONGO_TXN.RawResponse altered to NVARCHAR(1000) NULL.';
END
GO

PRINT '';
GO

-- ====================== AUDIT_LOG ======================

-- AUDIT_LOG.EntityName: NVARCHAR(50) -> NVARCHAR(30)
IF EXISTS (SELECT 1 FROM AUDIT_LOG WHERE LEN(EntityName) > 30)
BEGIN
    PRINT 'WARNING: AUDIT_LOG.EntityName has values exceeding 30 characters. Skipping.';
END
ELSE
BEGIN
    ALTER TABLE AUDIT_LOG ALTER COLUMN EntityName NVARCHAR(30) NOT NULL;
    PRINT 'OK: AUDIT_LOG.EntityName altered to NVARCHAR(30) NOT NULL.';
END
GO

-- AUDIT_LOG.Details: NVARCHAR(500) -> NVARCHAR(255)
IF EXISTS (SELECT 1 FROM AUDIT_LOG WHERE LEN(Details) > 255)
BEGIN
    PRINT 'WARNING: AUDIT_LOG.Details has values exceeding 255 characters. Skipping.';
END
ELSE
BEGIN
    ALTER TABLE AUDIT_LOG ALTER COLUMN Details NVARCHAR(255) NULL;
    PRINT 'OK: AUDIT_LOG.Details altered to NVARCHAR(255) NULL.';
END
GO

PRINT '';
GO


-- ============================================================================
-- SECTION 2: DECIMAL PRECISION REDUCTIONS
-- ============================================================================

PRINT '------------------------------------------------------------';
PRINT '  SECTION 2: DECIMAL Precision Reductions';
PRINT '------------------------------------------------------------';
PRINT '';
GO

-- ====================== SERVICE_CATALOG ======================
-- BasePrice: DECIMAL(18,2) -> DECIMAL(6,2)   max = 9999.99

IF EXISTS (SELECT 1 FROM SERVICE_CATALOG WHERE ABS(BasePrice) > 9999.99)
BEGIN
    PRINT 'WARNING: SERVICE_CATALOG.BasePrice has values > 9999.99. Skipping.';
END
ELSE
BEGIN
    ALTER TABLE SERVICE_CATALOG ALTER COLUMN BasePrice DECIMAL(6,2) NOT NULL;
    PRINT 'OK: SERVICE_CATALOG.BasePrice altered to DECIMAL(6,2).';
END
GO

PRINT '';
GO

-- ====================== INVENTORY_ITEMS ======================
-- UnitCost:  DECIMAL(18,2) -> DECIMAL(6,2)   max = 9999.99
-- UnitPrice: DECIMAL(18,2) -> DECIMAL(6,2)   max = 9999.99

IF EXISTS (SELECT 1 FROM INVENTORY_ITEMS WHERE ABS(UnitCost) > 9999.99)
BEGIN
    PRINT 'WARNING: INVENTORY_ITEMS.UnitCost has values > 9999.99. Skipping.';
END
ELSE
BEGIN
    ALTER TABLE INVENTORY_ITEMS ALTER COLUMN UnitCost DECIMAL(6,2) NOT NULL;
    PRINT 'OK: INVENTORY_ITEMS.UnitCost altered to DECIMAL(6,2).';
END
GO

IF EXISTS (SELECT 1 FROM INVENTORY_ITEMS WHERE ABS(UnitPrice) > 9999.99)
BEGIN
    PRINT 'WARNING: INVENTORY_ITEMS.UnitPrice has values > 9999.99. Skipping.';
END
ELSE
BEGIN
    ALTER TABLE INVENTORY_ITEMS ALTER COLUMN UnitPrice DECIMAL(6,2) NOT NULL;
    PRINT 'OK: INVENTORY_ITEMS.UnitPrice altered to DECIMAL(6,2).';
END
GO

PRINT '';
GO

-- ====================== JOB_ORDER_SERVICES ======================
-- UnitPrice: DECIMAL(18,2) -> DECIMAL(6,2)   max = 9999.99
--
-- LineTotal is a computed PERSISTED column: AS (Qty * UnitPrice) PERSISTED
-- SQL Server blocks ALTER COLUMN on a column with a computed-column dependency.
-- We must DROP then re-ADD the computed column.  This does NOT violate safety
-- rules (no table, PK, FK, or index is dropped).

PRINT 'Dropping computed column JOB_ORDER_SERVICES.LineTotal for UnitPrice ALTER...';
ALTER TABLE JOB_ORDER_SERVICES DROP COLUMN LineTotal;
GO

IF EXISTS (SELECT 1 FROM JOB_ORDER_SERVICES WHERE ABS(UnitPrice) > 9999.99)
BEGIN
    PRINT 'WARNING: JOB_ORDER_SERVICES.UnitPrice has values > 9999.99. Skipping.';
END
ELSE
BEGIN
    ALTER TABLE JOB_ORDER_SERVICES ALTER COLUMN UnitPrice DECIMAL(6,2) NOT NULL;
    PRINT 'OK: JOB_ORDER_SERVICES.UnitPrice altered to DECIMAL(6,2).';
END
GO

-- Recreate the computed column with the original formula.
ALTER TABLE JOB_ORDER_SERVICES ADD LineTotal AS (Qty * UnitPrice) PERSISTED;
PRINT 'OK: JOB_ORDER_SERVICES.LineTotal recreated as (Qty * UnitPrice) PERSISTED.';
GO

PRINT '';
GO

-- ====================== JOB_ORDER_PARTS ======================
-- UnitPrice: DECIMAL(18,2) -> DECIMAL(6,2)   max = 9999.99
--
-- LineTotal is a computed PERSISTED column: AS (QtyUsed * UnitPrice) PERSISTED
-- Same approach as above.

PRINT 'Dropping computed column JOB_ORDER_PARTS.LineTotal for UnitPrice ALTER...';
ALTER TABLE JOB_ORDER_PARTS DROP COLUMN LineTotal;
GO

IF EXISTS (SELECT 1 FROM JOB_ORDER_PARTS WHERE ABS(UnitPrice) > 9999.99)
BEGIN
    PRINT 'WARNING: JOB_ORDER_PARTS.UnitPrice has values > 9999.99. Skipping.';
END
ELSE
BEGIN
    ALTER TABLE JOB_ORDER_PARTS ALTER COLUMN UnitPrice DECIMAL(6,2) NOT NULL;
    PRINT 'OK: JOB_ORDER_PARTS.UnitPrice altered to DECIMAL(6,2).';
END
GO

-- Recreate the computed column with the original formula.
ALTER TABLE JOB_ORDER_PARTS ADD LineTotal AS (QtyUsed * UnitPrice) PERSISTED;
PRINT 'OK: JOB_ORDER_PARTS.LineTotal recreated as (QtyUsed * UnitPrice) PERSISTED.';
GO

PRINT '';
GO

-- ====================== INVOICE_LINES ======================
-- UnitPrice: DECIMAL(18,2) -> DECIMAL(6,2)   max = 9999.99
-- LineTotal:  DECIMAL(18,2) -> DECIMAL(8,2)   max = 999999.99
--
-- LineTotal is a computed PERSISTED column: AS (Qty * UnitPrice) PERSISTED
-- We recreate it with an explicit CAST to DECIMAL(8,2) to enforce the target type.

PRINT 'Dropping computed column INVOICE_LINES.LineTotal for UnitPrice ALTER...';
ALTER TABLE INVOICE_LINES DROP COLUMN LineTotal;
GO

IF EXISTS (SELECT 1 FROM INVOICE_LINES WHERE ABS(UnitPrice) > 9999.99)
BEGIN
    PRINT 'WARNING: INVOICE_LINES.UnitPrice has values > 9999.99. Skipping.';
END
ELSE
BEGIN
    ALTER TABLE INVOICE_LINES ALTER COLUMN UnitPrice DECIMAL(6,2) NOT NULL;
    PRINT 'OK: INVOICE_LINES.UnitPrice altered to DECIMAL(6,2).';
END
GO

-- Validate that existing computed values fit within DECIMAL(8,2) before recreating.
IF EXISTS (SELECT 1 FROM INVOICE_LINES WHERE ABS(Qty * UnitPrice) > 999999.99)
BEGIN
    PRINT 'WARNING: INVOICE_LINES computed LineTotal values exceed 999999.99.';
    PRINT '         Recreating LineTotal WITHOUT DECIMAL(8,2) cast to avoid overflow.';
    ALTER TABLE INVOICE_LINES ADD LineTotal AS (Qty * UnitPrice) PERSISTED;
END
ELSE
BEGIN
    ALTER TABLE INVOICE_LINES ADD LineTotal AS CAST(Qty * UnitPrice AS DECIMAL(8,2)) PERSISTED;
    PRINT 'OK: INVOICE_LINES.LineTotal recreated as CAST(Qty * UnitPrice AS DECIMAL(8,2)) PERSISTED.';
END
GO

PRINT '';
GO

-- ====================== INVOICES ======================
-- Subtotal:         DECIMAL(18,2) -> DECIMAL(8,2)   max = 999999.99
-- TotalAdjustments: DECIMAL(18,2) -> DECIMAL(8,2)   max = 999999.99
-- TotalAmount:      DECIMAL(18,2) -> DECIMAL(8,2)   max = 999999.99
-- AmountPaid:       DECIMAL(18,2) -> DECIMAL(8,2)   max = 999999.99
-- Balance:          DECIMAL(18,2) -> DECIMAL(8,2)   max = 999999.99

IF EXISTS (SELECT 1 FROM INVOICES WHERE ABS(Subtotal) > 999999.99)
BEGIN
    PRINT 'WARNING: INVOICES.Subtotal has values > 999999.99. Skipping.';
END
ELSE
BEGIN
    ALTER TABLE INVOICES ALTER COLUMN Subtotal DECIMAL(8,2) NOT NULL;
    PRINT 'OK: INVOICES.Subtotal altered to DECIMAL(8,2).';
END
GO

IF EXISTS (SELECT 1 FROM INVOICES WHERE ABS(TotalAdjustments) > 999999.99)
BEGIN
    PRINT 'WARNING: INVOICES.TotalAdjustments has values > 999999.99. Skipping.';
END
ELSE
BEGIN
    ALTER TABLE INVOICES ALTER COLUMN TotalAdjustments DECIMAL(8,2) NOT NULL;
    PRINT 'OK: INVOICES.TotalAdjustments altered to DECIMAL(8,2).';
END
GO

IF EXISTS (SELECT 1 FROM INVOICES WHERE ABS(TotalAmount) > 999999.99)
BEGIN
    PRINT 'WARNING: INVOICES.TotalAmount has values > 999999.99. Skipping.';
END
ELSE
BEGIN
    ALTER TABLE INVOICES ALTER COLUMN TotalAmount DECIMAL(8,2) NOT NULL;
    PRINT 'OK: INVOICES.TotalAmount altered to DECIMAL(8,2).';
END
GO

IF EXISTS (SELECT 1 FROM INVOICES WHERE ABS(AmountPaid) > 999999.99)
BEGIN
    PRINT 'WARNING: INVOICES.AmountPaid has values > 999999.99. Skipping.';
END
ELSE
BEGIN
    ALTER TABLE INVOICES ALTER COLUMN AmountPaid DECIMAL(8,2) NOT NULL;
    PRINT 'OK: INVOICES.AmountPaid altered to DECIMAL(8,2).';
END
GO

IF EXISTS (SELECT 1 FROM INVOICES WHERE ABS(Balance) > 999999.99)
BEGIN
    PRINT 'WARNING: INVOICES.Balance has values > 999999.99. Skipping.';
END
ELSE
BEGIN
    ALTER TABLE INVOICES ALTER COLUMN Balance DECIMAL(8,2) NOT NULL;
    PRINT 'OK: INVOICES.Balance altered to DECIMAL(8,2).';
END
GO

PRINT '';
GO

-- ====================== PAYMENTS (DECIMAL) ======================
-- Amount: DECIMAL(18,2) -> DECIMAL(8,2)   max = 999999.99

IF EXISTS (SELECT 1 FROM PAYMENTS WHERE ABS(Amount) > 999999.99)
BEGIN
    PRINT 'WARNING: PAYMENTS.Amount has values > 999999.99. Skipping.';
END
ELSE
BEGIN
    ALTER TABLE PAYMENTS ALTER COLUMN Amount DECIMAL(8,2) NOT NULL;
    PRINT 'OK: PAYMENTS.Amount altered to DECIMAL(8,2).';
END
GO

PRINT '';
GO

-- ====================== PAYMENT_ALLOCATION ======================
-- AmountApplied: DECIMAL(18,2) -> DECIMAL(6,2)   max = 9999.99

IF EXISTS (SELECT 1 FROM PAYMENT_ALLOCATION WHERE ABS(AmountApplied) > 9999.99)
BEGIN
    PRINT 'WARNING: PAYMENT_ALLOCATION.AmountApplied has values > 9999.99. Skipping.';
END
ELSE
BEGIN
    ALTER TABLE PAYMENT_ALLOCATION ALTER COLUMN AmountApplied DECIMAL(6,2) NOT NULL;
    PRINT 'OK: PAYMENT_ALLOCATION.AmountApplied altered to DECIMAL(6,2).';
END
GO

PRINT '';
GO

-- ====================== ACCOUNTING_ENTRY ======================
-- Debit:  DECIMAL(18,2) -> DECIMAL(8,2)   max = 999999.99
-- Credit: DECIMAL(18,2) -> DECIMAL(8,2)   max = 999999.99

IF EXISTS (SELECT 1 FROM ACCOUNTING_ENTRY WHERE ABS(Debit) > 999999.99)
BEGIN
    PRINT 'WARNING: ACCOUNTING_ENTRY.Debit has values > 999999.99. Skipping.';
END
ELSE
BEGIN
    ALTER TABLE ACCOUNTING_ENTRY ALTER COLUMN Debit DECIMAL(8,2) NOT NULL;
    PRINT 'OK: ACCOUNTING_ENTRY.Debit altered to DECIMAL(8,2).';
END
GO

IF EXISTS (SELECT 1 FROM ACCOUNTING_ENTRY WHERE ABS(Credit) > 999999.99)
BEGIN
    PRINT 'WARNING: ACCOUNTING_ENTRY.Credit has values > 999999.99. Skipping.';
END
ELSE
BEGIN
    ALTER TABLE ACCOUNTING_ENTRY ALTER COLUMN Credit DECIMAL(8,2) NOT NULL;
    PRINT 'OK: ACCOUNTING_ENTRY.Credit altered to DECIMAL(8,2).';
END
GO

PRINT '';
GO


-- ============================================================================
-- SECTION 2.5: RECREATE DROPPED INDEXES
-- ============================================================================

PRINT '------------------------------------------------------------';
PRINT '  SECTION 2.5: Recreating Dropped Indexes';
PRINT '------------------------------------------------------------';
PRINT '';
GO

-- Recreate IX_USERS_ShopID_UserName with updated UserName column size
-- Drop first if it exists (in case it was recreated by a constraint)
IF EXISTS (SELECT 1 FROM sys.indexes WHERE name = 'IX_USERS_ShopID_UserName' AND object_id = OBJECT_ID('USERS'))
BEGIN
    DROP INDEX IX_USERS_ShopID_UserName ON USERS;
    PRINT 'Dropped existing IX_USERS_ShopID_UserName index before recreation.';
END

CREATE NONCLUSTERED INDEX IX_USERS_ShopID_UserName 
    ON USERS(ShopID, UserName);
PRINT 'OK: Index IX_USERS_ShopID_UserName recreated with NVARCHAR(20) UserName.';
GO

PRINT '';
GO


-- ============================================================================
-- SECTION 3: POST-MIGRATION VERIFICATION
-- ============================================================================

PRINT '------------------------------------------------------------';
PRINT '  SECTION 3: Post-Migration Verification';
PRINT '------------------------------------------------------------';
PRINT '';
GO

-- Report final column types for all changed columns.
SELECT
    t.TABLE_NAME,
    c.COLUMN_NAME,
    c.DATA_TYPE,
    c.CHARACTER_MAXIMUM_LENGTH,
    c.NUMERIC_PRECISION,
    c.NUMERIC_SCALE,
    c.IS_NULLABLE
FROM INFORMATION_SCHEMA.COLUMNS c
JOIN INFORMATION_SCHEMA.TABLES  t
    ON c.TABLE_SCHEMA = t.TABLE_SCHEMA
   AND c.TABLE_NAME   = t.TABLE_NAME
WHERE t.TABLE_TYPE = 'BASE TABLE'
  AND (
    -- USERS
    (t.TABLE_NAME = 'USERS'               AND c.COLUMN_NAME IN ('FirstName','MiddleName','LastName','UserName','PasswordHash'))
    -- SHOP
    OR (t.TABLE_NAME = 'SHOP'             AND c.COLUMN_NAME IN ('ShopName','Email','Address','Status'))
    -- CUSTOMERS
    OR (t.TABLE_NAME = 'CUSTOMERS'        AND c.COLUMN_NAME IN ('FirstName','MiddleName','LastName','Email','Address'))
    -- DEVICES
    OR (t.TABLE_NAME = 'DEVICES'          AND c.COLUMN_NAME IN ('Notes'))
    -- PAYMENTS
    OR (t.TABLE_NAME = 'PAYMENTS'         AND c.COLUMN_NAME IN ('Method','ReferenceNo','Amount'))
    -- PAYMONGO_TXN
    OR (t.TABLE_NAME = 'PAYMONGO_TXN'    AND c.COLUMN_NAME IN ('RawResponse'))
    -- AUDIT_LOG
    OR (t.TABLE_NAME = 'AUDIT_LOG'       AND c.COLUMN_NAME IN ('EntityName','Details'))
    -- SERVICE_CATALOG
    OR (t.TABLE_NAME = 'SERVICE_CATALOG'  AND c.COLUMN_NAME IN ('BasePrice'))
    -- INVENTORY_ITEMS
    OR (t.TABLE_NAME = 'INVENTORY_ITEMS'  AND c.COLUMN_NAME IN ('UnitCost','UnitPrice'))
    -- JOB_ORDER_SERVICES
    OR (t.TABLE_NAME = 'JOB_ORDER_SERVICES' AND c.COLUMN_NAME IN ('UnitPrice'))
    -- JOB_ORDER_PARTS
    OR (t.TABLE_NAME = 'JOB_ORDER_PARTS'    AND c.COLUMN_NAME IN ('UnitPrice'))
    -- INVOICE_LINES
    OR (t.TABLE_NAME = 'INVOICE_LINES'      AND c.COLUMN_NAME IN ('UnitPrice'))
    -- INVOICES
    OR (t.TABLE_NAME = 'INVOICES'            AND c.COLUMN_NAME IN ('Subtotal','TotalAdjustments','TotalAmount','AmountPaid','Balance'))
    -- PAYMENT_ALLOCATION
    OR (t.TABLE_NAME = 'PAYMENT_ALLOCATION'  AND c.COLUMN_NAME IN ('AmountApplied'))
    -- ACCOUNTING_ENTRY
    OR (t.TABLE_NAME = 'ACCOUNTING_ENTRY'    AND c.COLUMN_NAME IN ('Debit','Credit'))
  )
ORDER BY t.TABLE_NAME, c.ORDINAL_POSITION;
GO

-- Verify computed columns were recreated correctly.
SELECT
    OBJECT_NAME(cc.object_id) AS TableName,
    cc.name                   AS ColumnName,
    cc.[definition]           AS ComputedFormula,
    cc.is_persisted
FROM sys.computed_columns cc
WHERE OBJECT_NAME(cc.object_id) IN ('JOB_ORDER_SERVICES','JOB_ORDER_PARTS','INVOICE_LINES')
ORDER BY TableName, ColumnName;
GO


PRINT '';
PRINT '================================================================';
PRINT '  ByteBillDB - Column Precision Migration COMPLETE';
PRINT '  Finished: ' + CONVERT(NVARCHAR(30), SYSDATETIME(), 120);
PRINT '================================================================';
GO
