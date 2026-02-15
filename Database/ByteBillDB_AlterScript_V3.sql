-- ═══════════════════════════════════════════════════════════════════════════
-- ByteBillDB Alter Script V3 — Core Operations support
-- Run AFTER the base schema (ByteBillDB_Schema.sql) and V1/V2 scripts.
-- ═══════════════════════════════════════════════════════════════════════════

-- ─────────────────────────────────────────────────────────────────────────
-- 1. New columns
-- ─────────────────────────────────────────────────────────────────────────

-- Customer soft-delete flag
IF NOT EXISTS (
    SELECT 1 FROM sys.columns
    WHERE object_id = OBJECT_ID('CUSTOMERS') AND name = 'IsActive'
)
BEGIN
    ALTER TABLE CUSTOMERS ADD IsActive BIT NOT NULL CONSTRAINT DF_Customers_IsActive DEFAULT 1;
END
GO

-- Invoice due date
IF NOT EXISTS (
    SELECT 1 FROM sys.columns
    WHERE object_id = OBJECT_ID('INVOICES') AND name = 'DueDate'
)
BEGIN
    ALTER TABLE INVOICES ADD DueDate DATETIME2(0) NULL;
END
GO

-- Payment notes
IF NOT EXISTS (
    SELECT 1 FROM sys.columns
    WHERE object_id = OBJECT_ID('PAYMENTS') AND name = 'Notes'
)
BEGIN
    ALTER TABLE PAYMENTS ADD Notes NVARCHAR(500) NULL;
END
GO

-- ─────────────────────────────────────────────────────────────────────────
-- 2. Unique constraints (idempotent — skip if already exists)
-- ─────────────────────────────────────────────────────────────────────────

-- Unique JobOrderNo per Shop
IF NOT EXISTS (
    SELECT 1 FROM sys.indexes
    WHERE object_id = OBJECT_ID('JOB_ORDERS') AND name = 'UX_JobOrders_ShopID_JobOrderNo'
)
BEGIN
    CREATE UNIQUE INDEX UX_JobOrders_ShopID_JobOrderNo
        ON JOB_ORDERS (ShopID, JobOrderNo);
END
GO

-- Unique InvoiceNo per Shop
IF NOT EXISTS (
    SELECT 1 FROM sys.indexes
    WHERE object_id = OBJECT_ID('INVOICES') AND name = 'UX_Invoices_ShopID_InvoiceNo'
)
BEGIN
    CREATE UNIQUE INDEX UX_Invoices_ShopID_InvoiceNo
        ON INVOICES (ShopID, InvoiceNo);
END
GO

-- 1:1 Invoice ↔ JobOrder
IF NOT EXISTS (
    SELECT 1 FROM sys.indexes
    WHERE object_id = OBJECT_ID('INVOICES') AND name = 'UX_Invoices_JobOrderID'
)
BEGIN
    CREATE UNIQUE INDEX UX_Invoices_JobOrderID
        ON INVOICES (JobOrderID);
END
GO

-- Unique PaymentAllocation per Payment+Invoice pair
IF NOT EXISTS (
    SELECT 1 FROM sys.indexes
    WHERE object_id = OBJECT_ID('PAYMENT_ALLOCATION') AND name = 'UX_PayAlloc_PaymentID_InvoiceID'
)
BEGIN
    CREATE UNIQUE INDEX UX_PayAlloc_PaymentID_InvoiceID
        ON PAYMENT_ALLOCATION (PaymentID, InvoiceID);
END
GO

-- ─────────────────────────────────────────────────────────────────────────
-- 3. Performance indexes — ShopID + search/filter columns
-- ─────────────────────────────────────────────────────────────────────────

-- Customers: search by name within shop
IF NOT EXISTS (
    SELECT 1 FROM sys.indexes
    WHERE object_id = OBJECT_ID('CUSTOMERS') AND name = 'IX_Customers_ShopID_Name'
)
BEGIN
    CREATE NONCLUSTERED INDEX IX_Customers_ShopID_Name
        ON CUSTOMERS (ShopID, LastName, FirstName)
        INCLUDE (Email, Phone, IsActive);
END
GO

-- Job Orders: filter by shop + status
IF NOT EXISTS (
    SELECT 1 FROM sys.indexes
    WHERE object_id = OBJECT_ID('JOB_ORDERS') AND name = 'IX_JobOrders_ShopID_Status'
)
BEGIN
    CREATE NONCLUSTERED INDEX IX_JobOrders_ShopID_Status
        ON JOB_ORDERS (ShopID, Status)
        INCLUDE (CustomerID, DeviceID, JobOrderNo, CreatedAt);
END
GO

-- Job Orders: lookup by customer within shop
IF NOT EXISTS (
    SELECT 1 FROM sys.indexes
    WHERE object_id = OBJECT_ID('JOB_ORDERS') AND name = 'IX_JobOrders_ShopID_CustomerID'
)
BEGIN
    CREATE NONCLUSTERED INDEX IX_JobOrders_ShopID_CustomerID
        ON JOB_ORDERS (ShopID, CustomerID);
END
GO

-- Invoices: filter by shop + status
IF NOT EXISTS (
    SELECT 1 FROM sys.indexes
    WHERE object_id = OBJECT_ID('INVOICES') AND name = 'IX_Invoices_ShopID_Status'
)
BEGIN
    CREATE NONCLUSTERED INDEX IX_Invoices_ShopID_Status
        ON INVOICES (ShopID, Status)
        INCLUDE (InvoiceNo, TotalAmount, AmountPaid, Balance, DueDate, CustomerID);
END
GO

-- Invoices: overdue check (ShopID + DueDate where Balance > 0)
IF NOT EXISTS (
    SELECT 1 FROM sys.indexes
    WHERE object_id = OBJECT_ID('INVOICES') AND name = 'IX_Invoices_ShopID_DueDate'
)
BEGIN
    CREATE NONCLUSTERED INDEX IX_Invoices_ShopID_DueDate
        ON INVOICES (ShopID, DueDate)
        INCLUDE (Balance, Status);
END
GO

-- Payments: filter by shop + status
IF NOT EXISTS (
    SELECT 1 FROM sys.indexes
    WHERE object_id = OBJECT_ID('PAYMENTS') AND name = 'IX_Payments_ShopID_Status'
)
BEGIN
    CREATE NONCLUSTERED INDEX IX_Payments_ShopID_Status
        ON PAYMENTS (ShopID, Status)
        INCLUDE (CustomerID, Amount, PaymentDate, Method);
END
GO

-- Payments: today's payments (for metrics)
IF NOT EXISTS (
    SELECT 1 FROM sys.indexes
    WHERE object_id = OBJECT_ID('PAYMENTS') AND name = 'IX_Payments_ShopID_PaymentDate'
)
BEGIN
    CREATE NONCLUSTERED INDEX IX_Payments_ShopID_PaymentDate
        ON PAYMENTS (ShopID, PaymentDate)
        INCLUDE (Amount, Status);
END
GO

-- PaymentAllocation: sum by InvoiceID
IF NOT EXISTS (
    SELECT 1 FROM sys.indexes
    WHERE object_id = OBJECT_ID('PAYMENT_ALLOCATION') AND name = 'IX_PayAlloc_InvoiceID'
)
BEGIN
    CREATE NONCLUSTERED INDEX IX_PayAlloc_InvoiceID
        ON PAYMENT_ALLOCATION (InvoiceID)
        INCLUDE (AmountApplied);
END
GO

-- Audit Log: query by shop + date
IF NOT EXISTS (
    SELECT 1 FROM sys.indexes
    WHERE object_id = OBJECT_ID('AUDIT_LOG') AND name = 'IX_AuditLog_ShopID_CreatedAt'
)
BEGIN
    CREATE NONCLUSTERED INDEX IX_AuditLog_ShopID_CreatedAt
        ON AUDIT_LOG (ShopID, CreatedAt DESC)
        INCLUDE (UserID, Action, EntityName, EntityID);
END
GO

PRINT 'ByteBillDB_AlterScript_V3 completed successfully.';
GO
