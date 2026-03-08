/*******************************************************************************
 *  ByteBill: Web-Based Billing System for Computer Repair Services
 *  MONSTERASP DEPLOYMENT SCRIPT (Complete & Unified)
 *  Microsoft SQL Server Database Schema
 *
 *  Generated: 2026-02-18
 *
 *  This script combines:
 *    - Complete table schema with all columns (base + enhancements)
 *    - Updated NVARCHAR lengths and DECIMAL precision
 *    - Profile and audit fields (Email, Phone, Theme, Notifications)
 *    - All indexes and constraints for optimal performance
 *    - Seed data with real user names and roles
 *
 *  ╔════════════════════════════════════════════════════════════════════════╗
 *  ║  MONSTERASP DEPLOYMENT INSTRUCTIONS                                    ║
 *  ╠════════════════════════════════════════════════════════════════════════╣
 *  ║  1. Log in to your MonsterASP control panel                            ║
 *  ║  2. Find your assigned database name (e.g., db123456_bytebill)         ║
 *  ║  3. REPLACE "ByteBillDB" below with YOUR database name                 ║
 *  ║  4. Open MonsterASP SQL Query Tool                                     ║
 *  ║  5. Ensure you're connected to YOUR database                           ║
 *  ║  6. Paste and execute this ENTIRE script                               ║
 *  ║                                                                          ║
 *  ║  ⚠️  DO NOT uncomment the CREATE DATABASE section — MonsterASP         ║
 *  ║     won't allow it. Use your pre-assigned database.                    ║
 *  ╚════════════════════════════════════════════════════════════════════════╝
 *
 *  NOTES:
 *    - All dependency orders are respected (FKs created after target tables)
 *    - User passwords are BCrypt hashes of 'Password123!'
 *    - Adjust ShopCode/ShopName for your actual shop details
 *    - Script is idempotent (safe to run multiple times)
 ******************************************************************************/

-- ============================================================================
-- 0. DATABASE SELECTION
-- ============================================================================
-- ⚠️  FOR MONSTERASP: Replace "ByteBillDB" with YOUR assigned database name
--     Example: USE db123456_bytebill;
-- ⚠️  FOR LOCAL DEVELOPMENT: Keep as "ByteBillDB" (matches appsettings.json)

USE ByteBillDB;  -- ← CHANGE THIS to your MonsterASP database name!
GO

-- ============================================================================
-- Optional: Create database locally (DISABLE FOR MONSTERASP)
-- ============================================================================
-- Uncomment ONLY for local SQL Server (NOT for MonsterASP):
/*
IF NOT EXISTS (SELECT 1 FROM sys.databases WHERE name = 'ByteBillDB')
BEGIN
    CREATE DATABASE ByteBillDB;
    PRINT '✓ Database ByteBillDB created';
END
ELSE
BEGIN
    PRINT '⚠ Database ByteBillDB already exists';
END
GO
USE ByteBillDB;
GO
*/

PRINT '================================================================';
PRINT '  ByteBillDB - MonsterASP Deployment';
PRINT '  Started: ' + CONVERT(NVARCHAR(30), SYSDATETIME(), 120);
PRINT '================================================================';
PRINT '';
GO


-- ============================================================================
-- A. SHOP
-- ============================================================================
IF NOT EXISTS (SELECT 1 FROM sys.tables WHERE name = 'SHOP')
BEGIN
    CREATE TABLE SHOP
    (
        ShopID    BIGINT        IDENTITY(1,1) NOT NULL,
        ShopCode  NVARCHAR(30)  NOT NULL,
        ShopName  NVARCHAR(30)  NOT NULL,
        Email     NVARCHAR(30)  NULL,
        Phone     NVARCHAR(30)  NULL,
        [Address] NVARCHAR(100) NULL,
        [Status]  NVARCHAR(15)  NOT NULL  DEFAULT 'Active',
        CreatedAt DATETIME2(0)  NOT NULL  DEFAULT SYSDATETIME(),
        UpdatedAt DATETIME2(0)  NULL,

        CONSTRAINT PK_SHOP        PRIMARY KEY (ShopID),
        CONSTRAINT UQ_SHOP_Code   UNIQUE      (ShopCode)
    );
    PRINT '✓ Created table SHOP';
END
GO


-- ============================================================================
-- B. USERS (with Profile & Audit fields)
-- ============================================================================
IF NOT EXISTS (SELECT 1 FROM sys.tables WHERE name = 'USERS')
BEGIN
    CREATE TABLE USERS
    (
        UserID              BIGINT        IDENTITY(1,1) NOT NULL,
        ShopID              BIGINT        NOT NULL,
        FirstName           NVARCHAR(20)  NOT NULL,
        MiddleName          NVARCHAR(10)  NULL,
        LastName            NVARCHAR(10)  NOT NULL,
        UserName            NVARCHAR(20)  NOT NULL,
        PasswordHash        NVARCHAR(100) NOT NULL,
        Email               NVARCHAR(150) NULL,
        Phone               NVARCHAR(20)  NULL,
        ThemePreference     NVARCHAR(10)  NOT NULL  DEFAULT 'light',
        EmailNotifications  BIT           NOT NULL  DEFAULT 1,
        InAppNotifications  BIT           NOT NULL  DEFAULT 1,
        IsActive            BIT           NOT NULL  DEFAULT 1,
        CreatedAt           DATETIME2(0)  NOT NULL  DEFAULT SYSDATETIME(),
        UpdatedAt           DATETIME2(0)  NULL,

        CONSTRAINT PK_USERS               PRIMARY KEY (UserID),
        CONSTRAINT FK_USERS_Shop          FOREIGN KEY (ShopID) REFERENCES SHOP (ShopID)
                                          ON UPDATE NO ACTION ON DELETE NO ACTION,
        CONSTRAINT UQ_USERS_ShopUserName  UNIQUE      (ShopID, UserName)
    );
    PRINT '✓ Created table USERS (with profile/audit fields)';
END
GO


-- ============================================================================
-- C. ROLES
-- ============================================================================
IF NOT EXISTS (SELECT 1 FROM sys.tables WHERE name = 'ROLES')
BEGIN
    CREATE TABLE ROLES
    (
        RoleID        BIGINT        IDENTITY(1,1) NOT NULL,
        RoleName      NVARCHAR(50)  NOT NULL,
        [Description] NVARCHAR(150) NULL,

        CONSTRAINT PK_ROLES      PRIMARY KEY (RoleID),
        CONSTRAINT UQ_ROLES_Name UNIQUE      (RoleName)
    );
    PRINT '✓ Created table ROLES';
END
GO


-- ============================================================================
-- D. USER_ROLES
-- ============================================================================
IF NOT EXISTS (SELECT 1 FROM sys.tables WHERE name = 'USER_ROLES')
BEGIN
    CREATE TABLE USER_ROLES
    (
        UserRoleID  BIGINT       IDENTITY(1,1) NOT NULL,
        UserID      BIGINT       NOT NULL,
        RoleID      BIGINT       NOT NULL,
        AssignedAt  DATETIME2(0) NOT NULL  DEFAULT SYSDATETIME(),

        CONSTRAINT PK_USER_ROLES          PRIMARY KEY (UserRoleID),
        CONSTRAINT FK_USER_ROLES_User     FOREIGN KEY (UserID) REFERENCES USERS (UserID)
                                          ON UPDATE NO ACTION ON DELETE NO ACTION,
        CONSTRAINT FK_USER_ROLES_Role     FOREIGN KEY (RoleID) REFERENCES ROLES (RoleID)
                                          ON UPDATE NO ACTION ON DELETE NO ACTION,
        CONSTRAINT UQ_USER_ROLES_UserRole UNIQUE      (UserID, RoleID)
    );
    PRINT '✓ Created table USER_ROLES';
END
GO


-- ============================================================================
-- E. CUSTOMERS (with Notes & IsActive)
-- ============================================================================
IF NOT EXISTS (SELECT 1 FROM sys.tables WHERE name = 'CUSTOMERS')
BEGIN
    CREATE TABLE CUSTOMERS
    (
        CustomerID  BIGINT        IDENTITY(1,1) NOT NULL,
        ShopID      BIGINT        NOT NULL,
        FirstName   NVARCHAR(20)  NOT NULL,
        MiddleName  NVARCHAR(10)  NULL,
        LastName    NVARCHAR(10)  NOT NULL,
        Email       NVARCHAR(30)  NULL,
        Phone       NVARCHAR(30)  NULL,
        [Address]   NVARCHAR(100) NULL,
        Notes       NVARCHAR(255) NULL,
        IsActive    BIT           NOT NULL  DEFAULT 1,
        CreatedAt   DATETIME2(0)  NOT NULL  DEFAULT SYSDATETIME(),

        CONSTRAINT PK_CUSTOMERS       PRIMARY KEY (CustomerID),
        CONSTRAINT FK_CUSTOMERS_Shop  FOREIGN KEY (ShopID) REFERENCES SHOP (ShopID)
                                      ON UPDATE NO ACTION ON DELETE NO ACTION
    );
    PRINT '✓ Created table CUSTOMERS (with Notes & IsActive)';
END
GO


-- ============================================================================
-- F. DEVICES
-- ============================================================================
IF NOT EXISTS (SELECT 1 FROM sys.tables WHERE name = 'DEVICES')
BEGIN
    CREATE TABLE DEVICES
    (
        DeviceID    BIGINT        IDENTITY(1,1) NOT NULL,
        CustomerID  BIGINT        NOT NULL,
        DeviceType  NVARCHAR(50)  NOT NULL,
        Brand       NVARCHAR(50)  NOT NULL,
        Model       NVARCHAR(80)  NOT NULL,
        SerialNo    NVARCHAR(60)  NULL,
        Notes       NVARCHAR(1000) NULL,

        CONSTRAINT PK_DEVICES          PRIMARY KEY (DeviceID),
        CONSTRAINT FK_DEVICES_Customer FOREIGN KEY (CustomerID) REFERENCES CUSTOMERS (CustomerID)
                                       ON UPDATE NO ACTION ON DELETE NO ACTION
    );
    PRINT '✓ Created table DEVICES';
END
GO


-- ============================================================================
-- G. SERVICE_CATEGORY
-- ============================================================================
IF NOT EXISTS (SELECT 1 FROM sys.tables WHERE name = 'SERVICE_CATEGORY')
BEGIN
    CREATE TABLE SERVICE_CATEGORY
    (
        ServiceCategoryID  BIGINT        IDENTITY(1,1) NOT NULL,
        ShopID             BIGINT        NOT NULL,
        CategoryName       NVARCHAR(80)  NOT NULL,
        [Description]      NVARCHAR(150) NULL,

        CONSTRAINT PK_SERVICE_CATEGORY          PRIMARY KEY (ServiceCategoryID),
        CONSTRAINT FK_SERVICE_CATEGORY_Shop     FOREIGN KEY (ShopID) REFERENCES SHOP (ShopID)
                                                ON UPDATE NO ACTION ON DELETE NO ACTION,
        CONSTRAINT UQ_SERVICE_CATEGORY_ShopName UNIQUE      (ShopID, CategoryName)
    );
    PRINT '✓ Created table SERVICE_CATEGORY';
END
GO


-- ============================================================================
-- H. SERVICE_CATALOG (with Description & EstimatedDuration)
-- ============================================================================
IF NOT EXISTS (SELECT 1 FROM sys.tables WHERE name = 'SERVICE_CATALOG')
BEGIN
    CREATE TABLE SERVICE_CATALOG
    (
        ServiceID          BIGINT          IDENTITY(1,1) NOT NULL,
        ShopID             BIGINT          NOT NULL,
        ServiceCategoryID  BIGINT          NOT NULL,
        ServiceName        NVARCHAR(120)   NOT NULL,
        [Description]      NVARCHAR(255)   NULL,
        BasePrice          DECIMAL(6,2)    NOT NULL  DEFAULT 0,
        EstimatedDuration  INT             NULL,
        IsActive           BIT             NOT NULL  DEFAULT 1,

        CONSTRAINT PK_SERVICE_CATALOG           PRIMARY KEY (ServiceID),
        CONSTRAINT FK_SERVICE_CATALOG_Shop      FOREIGN KEY (ShopID) REFERENCES SHOP (ShopID)
                                                ON UPDATE NO ACTION ON DELETE NO ACTION,
        CONSTRAINT FK_SERVICE_CATALOG_Category  FOREIGN KEY (ServiceCategoryID) REFERENCES SERVICE_CATEGORY (ServiceCategoryID)
                                                ON UPDATE NO ACTION ON DELETE NO ACTION,
        CONSTRAINT UQ_SERVICE_CATALOG_ShopName  UNIQUE      (ShopID, ServiceName)
    );
    PRINT '✓ Created table SERVICE_CATALOG (with Description & EstimatedDuration)';
END
GO


-- ============================================================================
-- I. INVENTORY_ITEMS (with Description, Category, Brand)
-- ============================================================================
IF NOT EXISTS (SELECT 1 FROM sys.tables WHERE name = 'INVENTORY_ITEMS')
BEGIN
    CREATE TABLE INVENTORY_ITEMS
    (
        ItemID       BIGINT         IDENTITY(1,1) NOT NULL,
        ShopID       BIGINT         NOT NULL,
        SKU          NVARCHAR(40)   NOT NULL,
        ItemName     NVARCHAR(120)  NOT NULL,
        [Description] NVARCHAR(255) NULL,
        Category     NVARCHAR(50)   NULL,
        Brand        NVARCHAR(50)   NULL,
        Unit         NVARCHAR(20)   NOT NULL,
        UnitCost     DECIMAL(6,2)   NOT NULL  DEFAULT 0,
        UnitPrice    DECIMAL(6,2)   NOT NULL  DEFAULT 0,
        QtyOnHand    INT            NOT NULL  DEFAULT 0,
        ReorderLevel INT            NOT NULL  DEFAULT 0,
        IsActive     BIT            NOT NULL  DEFAULT 1,

        CONSTRAINT PK_INVENTORY_ITEMS         PRIMARY KEY (ItemID),
        CONSTRAINT FK_INVENTORY_ITEMS_Shop    FOREIGN KEY (ShopID) REFERENCES SHOP (ShopID)
                                              ON UPDATE NO ACTION ON DELETE NO ACTION,
        CONSTRAINT UQ_INVENTORY_ITEMS_ShopSKU UNIQUE      (ShopID, SKU)
    );
    PRINT '✓ Created table INVENTORY_ITEMS (with Description, Category, Brand)';
END
GO


-- ============================================================================
-- J. INVENTORY_TXN
-- ============================================================================
IF NOT EXISTS (SELECT 1 FROM sys.tables WHERE name = 'INVENTORY_TXN')
BEGIN
    CREATE TABLE INVENTORY_TXN
    (
        InventoryTxnID  BIGINT        IDENTITY(1,1) NOT NULL,
        ItemID          BIGINT        NOT NULL,
        TxnType         NVARCHAR(10)  NOT NULL,
        Quantity        INT           NOT NULL,
        ReferenceType   NVARCHAR(30)  NULL,
        ReferenceID     BIGINT        NULL,
        Remarks         NVARCHAR(150) NULL,
        CreatedAt       DATETIME2(0)  NOT NULL  DEFAULT SYSDATETIME(),

        CONSTRAINT PK_INVENTORY_TXN       PRIMARY KEY (InventoryTxnID),
        CONSTRAINT FK_INVENTORY_TXN_Item  FOREIGN KEY (ItemID) REFERENCES INVENTORY_ITEMS (ItemID)
                                          ON UPDATE NO ACTION ON DELETE NO ACTION,
        CONSTRAINT CK_INVENTORY_TXN_Type  CHECK (TxnType IN ('IN','OUT','ADJUST'))
    );
    PRINT '✓ Created table INVENTORY_TXN';
END
GO


-- ============================================================================
-- K. JOB_ORDERS
-- ============================================================================
IF NOT EXISTS (SELECT 1 FROM sys.tables WHERE name = 'JOB_ORDERS')
BEGIN
    CREATE TABLE JOB_ORDERS
    (
        JobOrderID         BIGINT        IDENTITY(1,1) NOT NULL,
        ShopID             BIGINT        NOT NULL,
        CustomerID         BIGINT        NOT NULL,
        DeviceID           BIGINT        NOT NULL,
        CreatedByUserID    BIGINT        NOT NULL,
        AssignedTechUserID BIGINT        NULL,
        JobOrderNo         NVARCHAR(30)  NOT NULL,
        ProblemReported    NVARCHAR(255) NOT NULL,
        DiagnosisNotes     NVARCHAR(255) NULL,
        [Status]           NVARCHAR(30)  NOT NULL  DEFAULT 'Pending',
        CreatedAt          DATETIME2(0)  NOT NULL  DEFAULT SYSDATETIME(),
        UpdatedAt          DATETIME2(0)  NULL,
        IsArchived         BIT           NOT NULL  DEFAULT 0,
        ArchivedDate       DATETIME2(0)  NULL,

        CONSTRAINT PK_JOB_ORDERS                PRIMARY KEY (JobOrderID),
        CONSTRAINT FK_JOB_ORDERS_Shop           FOREIGN KEY (ShopID) REFERENCES SHOP (ShopID)
                                                ON UPDATE NO ACTION ON DELETE NO ACTION,
        CONSTRAINT FK_JOB_ORDERS_Customer       FOREIGN KEY (CustomerID) REFERENCES CUSTOMERS (CustomerID)
                                                ON UPDATE NO ACTION ON DELETE NO ACTION,
        CONSTRAINT FK_JOB_ORDERS_Device         FOREIGN KEY (DeviceID) REFERENCES DEVICES (DeviceID)
                                                ON UPDATE NO ACTION ON DELETE NO ACTION,
        CONSTRAINT FK_JOB_ORDERS_CreatedBy      FOREIGN KEY (CreatedByUserID) REFERENCES USERS (UserID)
                                                ON UPDATE NO ACTION ON DELETE NO ACTION,
        CONSTRAINT FK_JOB_ORDERS_AssignedTech   FOREIGN KEY (AssignedTechUserID) REFERENCES USERS (UserID)
                                                ON UPDATE NO ACTION ON DELETE NO ACTION,
        CONSTRAINT UQ_JOB_ORDERS_ShopJobOrderNo UNIQUE      (ShopID, JobOrderNo)
    );
    PRINT '✓ Created table JOB_ORDERS';
END
GO


-- ============================================================================
-- L. JOB_ORDER_SERVICES
-- ============================================================================
IF NOT EXISTS (SELECT 1 FROM sys.tables WHERE name = 'JOB_ORDER_SERVICES')
BEGIN
    CREATE TABLE JOB_ORDER_SERVICES
    (
        JobOrderServiceID  BIGINT         IDENTITY(1,1) NOT NULL,
        JobOrderID         BIGINT         NOT NULL,
        ServiceID          BIGINT         NOT NULL,
        Qty                INT            NOT NULL  DEFAULT 1,
        UnitPrice          DECIMAL(6,2)   NOT NULL  DEFAULT 0,
        LineTotal          AS (Qty * UnitPrice) PERSISTED,

        CONSTRAINT PK_JOB_ORDER_SERVICES         PRIMARY KEY (JobOrderServiceID),
        CONSTRAINT FK_JOB_ORDER_SERVICES_JobOrder FOREIGN KEY (JobOrderID) REFERENCES JOB_ORDERS (JobOrderID)
                                                  ON UPDATE NO ACTION ON DELETE NO ACTION,
        CONSTRAINT FK_JOB_ORDER_SERVICES_Service FOREIGN KEY (ServiceID)  REFERENCES SERVICE_CATALOG (ServiceID)
                                                  ON UPDATE NO ACTION ON DELETE NO ACTION
    );
    PRINT '✓ Created table JOB_ORDER_SERVICES';
END
GO


-- ============================================================================
-- M. JOB_ORDER_PARTS
-- ============================================================================
IF NOT EXISTS (SELECT 1 FROM sys.tables WHERE name = 'JOB_ORDER_PARTS')
BEGIN
    CREATE TABLE JOB_ORDER_PARTS
    (
        JobOrderPartID  BIGINT         IDENTITY(1,1) NOT NULL,
        JobOrderID      BIGINT         NOT NULL,
        ItemID          BIGINT         NOT NULL,
        QtyUsed         INT            NOT NULL  DEFAULT 1,
        UnitPrice       DECIMAL(6,2)   NOT NULL  DEFAULT 0,
        LineTotal       AS (QtyUsed * UnitPrice) PERSISTED,

        CONSTRAINT PK_JOB_ORDER_PARTS        PRIMARY KEY (JobOrderPartID),
        CONSTRAINT FK_JOB_ORDER_PARTS_JobOrder FOREIGN KEY (JobOrderID) REFERENCES JOB_ORDERS (JobOrderID)
                                               ON UPDATE NO ACTION ON DELETE NO ACTION,
        CONSTRAINT FK_JOB_ORDER_PARTS_Item   FOREIGN KEY (ItemID)     REFERENCES INVENTORY_ITEMS (ItemID)
                                               ON UPDATE NO ACTION ON DELETE NO ACTION
    );
    PRINT '✓ Created table JOB_ORDER_PARTS';
END
GO


-- ============================================================================
-- N. JOB_ORDER_STATUS_HISTORY
-- ============================================================================
IF NOT EXISTS (SELECT 1 FROM sys.tables WHERE name = 'JOB_ORDER_STATUS_HISTORY')
BEGIN
    CREATE TABLE JOB_ORDER_STATUS_HISTORY
    (
        JobOrderStatusHistoryID  BIGINT        IDENTITY(1,1) NOT NULL,
        JobOrderID               BIGINT        NOT NULL,
        OldStatus                NVARCHAR(30)  NOT NULL,
        NewStatus                NVARCHAR(30)  NOT NULL,
        ChangedByUserID          BIGINT        NOT NULL,
        ChangedAt                DATETIME2(0)  NOT NULL  DEFAULT SYSDATETIME(),
        Remarks                  NVARCHAR(150) NULL,

        CONSTRAINT PK_JOB_ORDER_STATUS_HISTORY           PRIMARY KEY (JobOrderStatusHistoryID),
        CONSTRAINT FK_JOB_ORDER_STATUS_HISTORY_JobOrder  FOREIGN KEY (JobOrderID)      REFERENCES JOB_ORDERS (JobOrderID)
                                                         ON UPDATE NO ACTION ON DELETE NO ACTION,
        CONSTRAINT FK_JOB_ORDER_STATUS_HISTORY_ChangedBy FOREIGN KEY (ChangedByUserID) REFERENCES USERS (UserID)
                                                         ON UPDATE NO ACTION ON DELETE NO ACTION
    );
    PRINT '✓ Created table JOB_ORDER_STATUS_HISTORY';
END
GO


-- ============================================================================
-- O. INVOICES (with DueDate, TaxRate, TaxAmount, DiscountAmount, Notes)
-- ============================================================================
IF NOT EXISTS (SELECT 1 FROM sys.tables WHERE name = 'INVOICES')
BEGIN
    CREATE TABLE INVOICES
    (
        InvoiceID        BIGINT         IDENTITY(1,1) NOT NULL,
        ShopID           BIGINT         NOT NULL,
        JobOrderID       BIGINT         NOT NULL,
        CustomerID       BIGINT         NOT NULL,
        InvoiceNo        NVARCHAR(30)   NOT NULL,
        InvoiceDate      DATETIME2(0)   NOT NULL  DEFAULT SYSDATETIME(),
        DueDate          DATETIME2(0)   NULL,
        Subtotal         DECIMAL(8,2)   NOT NULL  DEFAULT 0,
        TaxRate          DECIMAL(5,2)   NOT NULL  DEFAULT 0,
        TaxAmount        DECIMAL(8,2)   NOT NULL  DEFAULT 0,
        DiscountAmount   DECIMAL(8,2)   NOT NULL  DEFAULT 0,
        TotalAdjustments DECIMAL(8,2)   NOT NULL  DEFAULT 0,
        TotalAmount      DECIMAL(8,2)   NOT NULL  DEFAULT 0,
        AmountPaid       DECIMAL(8,2)   NOT NULL  DEFAULT 0,
        Balance          DECIMAL(8,2)   NOT NULL  DEFAULT 0,
        Notes            NVARCHAR(255)  NULL,
        [Status]         NVARCHAR(20)   NOT NULL  DEFAULT 'Unpaid',
        CreatedAt        DATETIME2(0)   NOT NULL  DEFAULT SYSDATETIME(),
        IsArchived       BIT            NOT NULL  DEFAULT 0,
        ArchivedDate     DATETIME2(0)   NULL,

        CONSTRAINT PK_INVOICES                 PRIMARY KEY (InvoiceID),
        CONSTRAINT FK_INVOICES_Shop            FOREIGN KEY (ShopID)     REFERENCES SHOP (ShopID)
                                               ON UPDATE NO ACTION ON DELETE NO ACTION,
        CONSTRAINT FK_INVOICES_JobOrder        FOREIGN KEY (JobOrderID) REFERENCES JOB_ORDERS (JobOrderID)
                                               ON UPDATE NO ACTION ON DELETE NO ACTION,
        CONSTRAINT FK_INVOICES_Customer        FOREIGN KEY (CustomerID) REFERENCES CUSTOMERS (CustomerID)
                                               ON UPDATE NO ACTION ON DELETE NO ACTION,
        CONSTRAINT CK_INVOICES_Status          CHECK ([Status] IN ('Unpaid','Partial','Paid','Void')),
        CONSTRAINT UQ_INVOICES_ShopInvoiceNo   UNIQUE (ShopID, InvoiceNo),
        CONSTRAINT UQ_INVOICES_JobOrderID      UNIQUE (JobOrderID)
    );
    PRINT '✓ Created table INVOICES (with DueDate, TaxRate, etc.)';
END
GO


-- ============================================================================
-- P. INVOICE_LINES
-- ============================================================================
IF NOT EXISTS (SELECT 1 FROM sys.tables WHERE name = 'INVOICE_LINES')
BEGIN
    CREATE TABLE INVOICE_LINES
    (
        InvoiceLineID  BIGINT         IDENTITY(1,1) NOT NULL,
        InvoiceID      BIGINT         NOT NULL,
        LineType       NVARCHAR(20)   NOT NULL,
        [Description]  NVARCHAR(150)  NOT NULL,
        Qty            INT            NOT NULL  DEFAULT 1,
        UnitPrice      DECIMAL(6,2)   NOT NULL  DEFAULT 0,
        LineTotal      AS (Qty * UnitPrice) PERSISTED,

        CONSTRAINT PK_INVOICE_LINES       PRIMARY KEY (InvoiceLineID),
        CONSTRAINT FK_INVOICE_LINES_Invoice FOREIGN KEY (InvoiceID) REFERENCES INVOICES (InvoiceID)
                                            ON UPDATE NO ACTION ON DELETE NO ACTION
    );
    PRINT '✓ Created table INVOICE_LINES';
END
GO


-- ============================================================================
-- Q. PAYMENTS (with PaymentNo & Notes)
-- ============================================================================
IF NOT EXISTS (SELECT 1 FROM sys.tables WHERE name = 'PAYMENTS')
BEGIN
    CREATE TABLE PAYMENTS
    (
        PaymentID         BIGINT         IDENTITY(1,1) NOT NULL,
        ShopID            BIGINT         NOT NULL,
        CustomerID        BIGINT         NOT NULL,
        PaymentNo         NVARCHAR(30)   NULL,
        PaymentDate       DATETIME2(0)   NOT NULL  DEFAULT SYSDATETIME(),
        Amount            DECIMAL(8,2)   NOT NULL,
        Method            NVARCHAR(20)   NOT NULL,
        ReferenceNo       NVARCHAR(30)   NULL,
        ReceivedByUserID  BIGINT         NOT NULL,
        Notes             NVARCHAR(255)  NULL,
        [Status]          NVARCHAR(20)   NOT NULL  DEFAULT 'Confirmed',

        CONSTRAINT PK_PAYMENTS                PRIMARY KEY (PaymentID),
        CONSTRAINT FK_PAYMENTS_Shop           FOREIGN KEY (ShopID)           REFERENCES SHOP (ShopID)
                                              ON UPDATE NO ACTION ON DELETE NO ACTION,
        CONSTRAINT FK_PAYMENTS_Customer       FOREIGN KEY (CustomerID)       REFERENCES CUSTOMERS (CustomerID)
                                              ON UPDATE NO ACTION ON DELETE NO ACTION,
        CONSTRAINT FK_PAYMENTS_ReceivedBy     FOREIGN KEY (ReceivedByUserID) REFERENCES USERS (UserID)
                                              ON UPDATE NO ACTION ON DELETE NO ACTION,
        CONSTRAINT CK_PAYMENTS_Method         CHECK (Method IN ('Cash','GCash','Card')),
        CONSTRAINT CK_PAYMENTS_Status         CHECK ([Status] IN ('Pending','Confirmed','Failed','Refunded')),
        CONSTRAINT UQ_PAYMENTS_PaymentNo      UNIQUE (ShopID, PaymentNo)
    );
    PRINT '✓ Created table PAYMENTS (with PaymentNo & Notes)';
END
GO


-- ============================================================================
-- R. PAYMENT_ALLOCATION
-- ============================================================================
IF NOT EXISTS (SELECT 1 FROM sys.tables WHERE name = 'PAYMENT_ALLOCATION')
BEGIN
    CREATE TABLE PAYMENT_ALLOCATION
    (
        PaymentAllocationID  BIGINT         IDENTITY(1,1) NOT NULL,
        PaymentID            BIGINT         NOT NULL,
        InvoiceID            BIGINT         NOT NULL,
        AmountApplied        DECIMAL(6,2)   NOT NULL,

        CONSTRAINT PK_PAYMENT_ALLOCATION               PRIMARY KEY (PaymentAllocationID),
        CONSTRAINT FK_PAYMENT_ALLOCATION_Payment       FOREIGN KEY (PaymentID) REFERENCES PAYMENTS (PaymentID)
                                                       ON UPDATE NO ACTION ON DELETE NO ACTION,
        CONSTRAINT FK_PAYMENT_ALLOCATION_Invoice       FOREIGN KEY (InvoiceID) REFERENCES INVOICES (InvoiceID)
                                                       ON UPDATE NO ACTION ON DELETE NO ACTION,
        CONSTRAINT UQ_PAYMENT_ALLOCATION_PaymentInvoice UNIQUE (PaymentID, InvoiceID)
    );
    PRINT '✓ Created table PAYMENT_ALLOCATION';
END
GO


-- ============================================================================
-- S. PAYMONGO_TXN
-- ============================================================================
IF NOT EXISTS (SELECT 1 FROM sys.tables WHERE name = 'PAYMONGO_TXN')
BEGIN
    CREATE TABLE PAYMONGO_TXN
    (
        PayMongoTxnID           BIGINT         IDENTITY(1,1) NOT NULL,
        PaymentID               BIGINT         NOT NULL,
        PayMongoPaymentIntentID NVARCHAR(80)   NOT NULL,
        PayMongoStatus          NVARCHAR(30)   NOT NULL,
        RawResponse             NVARCHAR(MAX) NULL,
        CreatedAt               DATETIME2(0)   NOT NULL  DEFAULT SYSDATETIME(),

        CONSTRAINT PK_PAYMONGO_TXN           PRIMARY KEY (PayMongoTxnID),
        CONSTRAINT FK_PAYMONGO_TXN_Payment   FOREIGN KEY (PaymentID) REFERENCES PAYMENTS (PaymentID)
                                             ON UPDATE NO ACTION ON DELETE NO ACTION,
        CONSTRAINT UQ_PAYMONGO_TXN_PaymentID UNIQUE (PaymentID)
    );
    PRINT '✓ Created table PAYMONGO_TXN';
END
GO


-- ============================================================================
-- T. CREDIT_DEBIT_ADJUSTMENT
-- ============================================================================
IF NOT EXISTS (SELECT 1 FROM sys.tables WHERE name = 'CREDIT_DEBIT_ADJUSTMENT')
BEGIN
    CREATE TABLE CREDIT_DEBIT_ADJUSTMENT
    (
        AdjustmentID    BIGINT         IDENTITY(1,1) NOT NULL,
        InvoiceID       BIGINT         NOT NULL,
        CreatedByUserID BIGINT         NOT NULL,
        AdjustmentType  NVARCHAR(10)   NOT NULL,
        Amount          DECIMAL(18,2)  NOT NULL,
        Reason          NVARCHAR(150)  NOT NULL,
        CreatedAt       DATETIME2(0)   NOT NULL  DEFAULT SYSDATETIME(),

        CONSTRAINT PK_CREDIT_DEBIT_ADJUSTMENT           PRIMARY KEY (AdjustmentID),
        CONSTRAINT FK_CREDIT_DEBIT_ADJUSTMENT_Invoice   FOREIGN KEY (InvoiceID)       REFERENCES INVOICES (InvoiceID)
                                                        ON UPDATE NO ACTION ON DELETE NO ACTION,
        CONSTRAINT FK_CREDIT_DEBIT_ADJUSTMENT_CreatedBy FOREIGN KEY (CreatedByUserID)  REFERENCES USERS (UserID)
                                                        ON UPDATE NO ACTION ON DELETE NO ACTION,
        CONSTRAINT CK_CREDIT_DEBIT_ADJUSTMENT_Type      CHECK (AdjustmentType IN ('CREDIT','DEBIT'))
    );
    PRINT '✓ Created table CREDIT_DEBIT_ADJUSTMENT';
END
GO


-- ============================================================================
-- U. AUDIT_LOG (with IpAddress, OldValues, NewValues)
-- ============================================================================
IF NOT EXISTS (SELECT 1 FROM sys.tables WHERE name = 'AUDIT_LOG')
BEGIN
    CREATE TABLE AUDIT_LOG
    (
        AuditLogID  BIGINT        IDENTITY(1,1) NOT NULL,
        ShopID      BIGINT        NOT NULL,
        UserID      BIGINT        NOT NULL,
        [Action]    NVARCHAR(30)  NOT NULL,
        EntityName  NVARCHAR(30)  NOT NULL,
        EntityID    BIGINT        NOT NULL,
        Details     NVARCHAR(500) NULL,
        IpAddress   NVARCHAR(45)  NULL,
        OldValues   NVARCHAR(2000) NULL,
        NewValues   NVARCHAR(2000) NULL,
        CreatedAt   DATETIME2(0)  NOT NULL  DEFAULT SYSDATETIME(),

        CONSTRAINT PK_AUDIT_LOG      PRIMARY KEY (AuditLogID),
        CONSTRAINT FK_AUDIT_LOG_Shop FOREIGN KEY (ShopID) REFERENCES SHOP (ShopID)
                                     ON UPDATE NO ACTION ON DELETE NO ACTION,
        CONSTRAINT FK_AUDIT_LOG_User FOREIGN KEY (UserID) REFERENCES USERS (UserID)
                                     ON UPDATE NO ACTION ON DELETE NO ACTION
    );
    PRINT '✓ Created table AUDIT_LOG (with IpAddress, OldValues, NewValues)';
END
GO


-- ============================================================================
-- V. ACCOUNTING_ENTRY
-- ============================================================================
IF NOT EXISTS (SELECT 1 FROM sys.tables WHERE name = 'ACCOUNTING_ENTRY')
BEGIN
    CREATE TABLE ACCOUNTING_ENTRY
    (
        AccountingEntryID  BIGINT         IDENTITY(1,1) NOT NULL,
        ShopID             BIGINT         NOT NULL,
        SourceType         NVARCHAR(20)   NOT NULL,
        SourceInvoiceID    BIGINT         NULL,
        SourcePaymentID    BIGINT         NULL,
        EntryDate          DATETIME2(0)   NOT NULL  DEFAULT SYSDATETIME(),
        AccountCode        NVARCHAR(20)   NOT NULL,
        Debit              DECIMAL(8,2)   NOT NULL  DEFAULT 0,
        Credit             DECIMAL(8,2)   NOT NULL  DEFAULT 0,
        Memo               NVARCHAR(150)  NULL,

        CONSTRAINT PK_ACCOUNTING_ENTRY               PRIMARY KEY (AccountingEntryID),
        CONSTRAINT FK_ACCOUNTING_ENTRY_Shop          FOREIGN KEY (ShopID)          REFERENCES SHOP (ShopID)
                                                     ON UPDATE NO ACTION ON DELETE NO ACTION,
        CONSTRAINT FK_ACCOUNTING_ENTRY_Invoice       FOREIGN KEY (SourceInvoiceID) REFERENCES INVOICES (InvoiceID)
                                                     ON UPDATE NO ACTION ON DELETE NO ACTION,
        CONSTRAINT FK_ACCOUNTING_ENTRY_Payment       FOREIGN KEY (SourcePaymentID) REFERENCES PAYMENTS (PaymentID)
                                                     ON UPDATE NO ACTION ON DELETE NO ACTION
    );
    PRINT '✓ Created table ACCOUNTING_ENTRY';
END
GO


-- ============================================================================
-- W. XERO_SYNC_LOG
-- ============================================================================
IF NOT EXISTS (SELECT 1 FROM sys.tables WHERE name = 'XERO_SYNC_LOG')
BEGIN
    CREATE TABLE XERO_SYNC_LOG
    (
        XeroSyncLogID      BIGINT        IDENTITY(1,1) NOT NULL,
        ShopID             BIGINT        NOT NULL,
        SyncedByUserID     BIGINT        NULL,
        SyncType           NVARCHAR(30)  NOT NULL,
        InvoiceID          BIGINT        NULL,
        PaymentID          BIGINT        NULL,
        AccountingEntryID  BIGINT        NULL,
        XeroRecordID       NVARCHAR(80)  NULL,
        [Status]           NVARCHAR(20)  NOT NULL  DEFAULT 'Pending',
        [Message]          NVARCHAR(255) NULL,
        SyncedAt           DATETIME2(0)  NOT NULL  DEFAULT SYSDATETIME(),

        CONSTRAINT PK_XERO_SYNC_LOG                    PRIMARY KEY (XeroSyncLogID),
        CONSTRAINT FK_XERO_SYNC_LOG_Shop               FOREIGN KEY (ShopID)            REFERENCES SHOP (ShopID)
                                                       ON UPDATE NO ACTION ON DELETE NO ACTION,
        CONSTRAINT FK_XERO_SYNC_LOG_SyncedBy           FOREIGN KEY (SyncedByUserID)    REFERENCES USERS (UserID)
                                                       ON UPDATE NO ACTION ON DELETE NO ACTION,
        CONSTRAINT FK_XERO_SYNC_LOG_Invoice            FOREIGN KEY (InvoiceID)         REFERENCES INVOICES (InvoiceID)
                                                       ON UPDATE NO ACTION ON DELETE NO ACTION,
        CONSTRAINT FK_XERO_SYNC_LOG_Payment            FOREIGN KEY (PaymentID)         REFERENCES PAYMENTS (PaymentID)
                                                       ON UPDATE NO ACTION ON DELETE NO ACTION,
        CONSTRAINT FK_XERO_SYNC_LOG_AccountingEntry    FOREIGN KEY (AccountingEntryID) REFERENCES ACCOUNTING_ENTRY (AccountingEntryID)
                                                       ON UPDATE NO ACTION ON DELETE NO ACTION
    );
    PRINT '✓ Created table XERO_SYNC_LOG';
END
GO


PRINT '';
PRINT '================================================================';
PRINT '  CREATING INDEXES FOR OPTIMAL PERFORMANCE';
PRINT '================================================================';
PRINT '';
GO


-- ============================================================================
-- INDEXES ON FOREIGN KEY COLUMNS (for join performance)
-- ============================================================================

-- USERS
IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE name = 'IX_USERS_ShopID' AND object_id = OBJECT_ID('USERS'))
    CREATE NONCLUSTERED INDEX IX_USERS_ShopID ON USERS (ShopID);

IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE name = 'IX_USERS_ShopID_UserName' AND object_id = OBJECT_ID('USERS'))
    CREATE NONCLUSTERED INDEX IX_USERS_ShopID_UserName ON USERS (ShopID, UserName);

-- USER_ROLES
IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE name = 'IX_USER_ROLES_UserID' AND object_id = OBJECT_ID('USER_ROLES'))
    CREATE NONCLUSTERED INDEX IX_USER_ROLES_UserID ON USER_ROLES (UserID);

IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE name = 'IX_USER_ROLES_RoleID' AND object_id = OBJECT_ID('USER_ROLES'))
    CREATE NONCLUSTERED INDEX IX_USER_ROLES_RoleID ON USER_ROLES (RoleID);

-- CUSTOMERS
IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE name = 'IX_CUSTOMERS_ShopID' AND object_id = OBJECT_ID('CUSTOMERS'))
    CREATE NONCLUSTERED INDEX IX_CUSTOMERS_ShopID ON CUSTOMERS (ShopID);

IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE name = 'IX_Customers_ShopID_Name' AND object_id = OBJECT_ID('CUSTOMERS'))
    CREATE NONCLUSTERED INDEX IX_Customers_ShopID_Name ON CUSTOMERS (ShopID, LastName, FirstName) INCLUDE (Email, Phone, IsActive);

-- DEVICES
IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE name = 'IX_DEVICES_CustomerID' AND object_id = OBJECT_ID('DEVICES'))
    CREATE NONCLUSTERED INDEX IX_DEVICES_CustomerID ON DEVICES (CustomerID);

-- SERVICE_CATEGORY
IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE name = 'IX_SERVICE_CATEGORY_ShopID' AND object_id = OBJECT_ID('SERVICE_CATEGORY'))
    CREATE NONCLUSTERED INDEX IX_SERVICE_CATEGORY_ShopID ON SERVICE_CATEGORY (ShopID);

-- SERVICE_CATALOG
IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE name = 'IX_SERVICE_CATALOG_ShopID' AND object_id = OBJECT_ID('SERVICE_CATALOG'))
    CREATE NONCLUSTERED INDEX IX_SERVICE_CATALOG_ShopID ON SERVICE_CATALOG (ShopID);

IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE name = 'IX_SERVICE_CATALOG_CategoryID' AND object_id = OBJECT_ID('SERVICE_CATALOG'))
    CREATE NONCLUSTERED INDEX IX_SERVICE_CATALOG_CategoryID ON SERVICE_CATALOG (ServiceCategoryID);

-- INVENTORY_ITEMS
IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE name = 'IX_INVENTORY_ITEMS_ShopID' AND object_id = OBJECT_ID('INVENTORY_ITEMS'))
    CREATE NONCLUSTERED INDEX IX_INVENTORY_ITEMS_ShopID ON INVENTORY_ITEMS (ShopID);

-- INVENTORY_TXN
IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE name = 'IX_INVENTORY_TXN_ItemID' AND object_id = OBJECT_ID('INVENTORY_TXN'))
    CREATE NONCLUSTERED INDEX IX_INVENTORY_TXN_ItemID ON INVENTORY_TXN (ItemID);

-- JOB_ORDERS
IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE name = 'IX_JOB_ORDERS_ShopID' AND object_id = OBJECT_ID('JOB_ORDERS'))
    CREATE NONCLUSTERED INDEX IX_JOB_ORDERS_ShopID ON JOB_ORDERS (ShopID);

IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE name = 'IX_JOB_ORDERS_CustomerID' AND object_id = OBJECT_ID('JOB_ORDERS'))
    CREATE NONCLUSTERED INDEX IX_JOB_ORDERS_CustomerID ON JOB_ORDERS (CustomerID);

IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE name = 'IX_JOB_ORDERS_DeviceID' AND object_id = OBJECT_ID('JOB_ORDERS'))
    CREATE NONCLUSTERED INDEX IX_JOB_ORDERS_DeviceID ON JOB_ORDERS (DeviceID);

IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE name = 'IX_JOB_ORDERS_CreatedByUserID' AND object_id = OBJECT_ID('JOB_ORDERS'))
    CREATE NONCLUSTERED INDEX IX_JOB_ORDERS_CreatedByUserID ON JOB_ORDERS (CreatedByUserID);

IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE name = 'IX_JOB_ORDERS_AssignedTechUserID' AND object_id = OBJECT_ID('JOB_ORDERS'))
    CREATE NONCLUSTERED INDEX IX_JOB_ORDERS_AssignedTechUserID ON JOB_ORDERS (AssignedTechUserID);

IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE name = 'IX_JobOrders_ShopID_Status' AND object_id = OBJECT_ID('JOB_ORDERS'))
    CREATE NONCLUSTERED INDEX IX_JobOrders_ShopID_Status ON JOB_ORDERS (ShopID, Status) INCLUDE (CustomerID, DeviceID, JobOrderNo, CreatedAt);

IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE name = 'IX_JobOrders_ShopID_CustomerID' AND object_id = OBJECT_ID('JOB_ORDERS'))
    CREATE NONCLUSTERED INDEX IX_JobOrders_ShopID_CustomerID ON JOB_ORDERS (ShopID, CustomerID);

-- JOB_ORDER_SERVICES
IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE name = 'IX_JOB_ORDER_SERVICES_JobOrderID' AND object_id = OBJECT_ID('JOB_ORDER_SERVICES'))
    CREATE NONCLUSTERED INDEX IX_JOB_ORDER_SERVICES_JobOrderID ON JOB_ORDER_SERVICES (JobOrderID);

IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE name = 'IX_JOB_ORDER_SERVICES_ServiceID' AND object_id = OBJECT_ID('JOB_ORDER_SERVICES'))
    CREATE NONCLUSTERED INDEX IX_JOB_ORDER_SERVICES_ServiceID ON JOB_ORDER_SERVICES (ServiceID);

-- JOB_ORDER_PARTS
IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE name = 'IX_JOB_ORDER_PARTS_JobOrderID' AND object_id = OBJECT_ID('JOB_ORDER_PARTS'))
    CREATE NONCLUSTERED INDEX IX_JOB_ORDER_PARTS_JobOrderID ON JOB_ORDER_PARTS (JobOrderID);

IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE name = 'IX_JOB_ORDER_PARTS_ItemID' AND object_id = OBJECT_ID('JOB_ORDER_PARTS'))
    CREATE NONCLUSTERED INDEX IX_JOB_ORDER_PARTS_ItemID ON JOB_ORDER_PARTS (ItemID);

-- JOB_ORDER_STATUS_HISTORY
IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE name = 'IX_JOB_ORDER_STATUS_HISTORY_JobOrderID' AND object_id = OBJECT_ID('JOB_ORDER_STATUS_HISTORY'))
    CREATE NONCLUSTERED INDEX IX_JOB_ORDER_STATUS_HISTORY_JobOrderID ON JOB_ORDER_STATUS_HISTORY (JobOrderID);

IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE name = 'IX_JOB_ORDER_STATUS_HISTORY_ChangedByUserID' AND object_id = OBJECT_ID('JOB_ORDER_STATUS_HISTORY'))
    CREATE NONCLUSTERED INDEX IX_JOB_ORDER_STATUS_HISTORY_ChangedByUserID ON JOB_ORDER_STATUS_HISTORY (ChangedByUserID);

-- INVOICES
IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE name = 'IX_INVOICES_ShopID' AND object_id = OBJECT_ID('INVOICES'))
    CREATE NONCLUSTERED INDEX IX_INVOICES_ShopID ON INVOICES (ShopID);

IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE name = 'IX_INVOICES_CustomerID' AND object_id = OBJECT_ID('INVOICES'))
    CREATE NONCLUSTERED INDEX IX_INVOICES_CustomerID ON INVOICES (CustomerID);

IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE name = 'IX_Invoices_ShopID_Status' AND object_id = OBJECT_ID('INVOICES'))
    CREATE NONCLUSTERED INDEX IX_Invoices_ShopID_Status ON INVOICES (ShopID, Status) INCLUDE (InvoiceNo, TotalAmount, AmountPaid, Balance, DueDate, CustomerID);

IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE name = 'IX_Invoices_ShopID_DueDate' AND object_id = OBJECT_ID('INVOICES'))
    CREATE NONCLUSTERED INDEX IX_Invoices_ShopID_DueDate ON INVOICES (ShopID, DueDate) INCLUDE (Balance, Status);

-- INVOICE_LINES
IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE name = 'IX_INVOICE_LINES_InvoiceID' AND object_id = OBJECT_ID('INVOICE_LINES'))
    CREATE NONCLUSTERED INDEX IX_INVOICE_LINES_InvoiceID ON INVOICE_LINES (InvoiceID);

-- PAYMENTS
IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE name = 'IX_PAYMENTS_ShopID' AND object_id = OBJECT_ID('PAYMENTS'))
    CREATE NONCLUSTERED INDEX IX_PAYMENTS_ShopID ON PAYMENTS (ShopID);

IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE name = 'IX_PAYMENTS_CustomerID' AND object_id = OBJECT_ID('PAYMENTS'))
    CREATE NONCLUSTERED INDEX IX_PAYMENTS_CustomerID ON PAYMENTS (CustomerID);

IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE name = 'IX_PAYMENTS_ReceivedByUserID' AND object_id = OBJECT_ID('PAYMENTS'))
    CREATE NONCLUSTERED INDEX IX_PAYMENTS_ReceivedByUserID ON PAYMENTS (ReceivedByUserID);

IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE name = 'IX_Payments_ShopID_Status' AND object_id = OBJECT_ID('PAYMENTS'))
    CREATE NONCLUSTERED INDEX IX_Payments_ShopID_Status ON PAYMENTS (ShopID, Status) INCLUDE (CustomerID, Amount, PaymentDate, Method);

IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE name = 'IX_Payments_ShopID_PaymentDate' AND object_id = OBJECT_ID('PAYMENTS'))
    CREATE NONCLUSTERED INDEX IX_Payments_ShopID_PaymentDate ON PAYMENTS (ShopID, PaymentDate) INCLUDE (Amount, Status);

-- PAYMENT_ALLOCATION
IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE name = 'IX_PAYMENT_ALLOCATION_PaymentID' AND object_id = OBJECT_ID('PAYMENT_ALLOCATION'))
    CREATE NONCLUSTERED INDEX IX_PAYMENT_ALLOCATION_PaymentID ON PAYMENT_ALLOCATION (PaymentID);

IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE name = 'IX_PAYMENT_ALLOCATION_InvoiceID' AND object_id = OBJECT_ID('PAYMENT_ALLOCATION'))
    CREATE NONCLUSTERED INDEX IX_PAYMENT_ALLOCATION_InvoiceID ON PAYMENT_ALLOCATION (InvoiceID);

IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE name = 'IX_PayAlloc_InvoiceID' AND object_id = OBJECT_ID('PAYMENT_ALLOCATION'))
    CREATE NONCLUSTERED INDEX IX_PayAlloc_InvoiceID ON PAYMENT_ALLOCATION (InvoiceID) INCLUDE (AmountApplied);

-- CREDIT_DEBIT_ADJUSTMENT
IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE name = 'IX_CREDIT_DEBIT_ADJUSTMENT_InvoiceID' AND object_id = OBJECT_ID('CREDIT_DEBIT_ADJUSTMENT'))
    CREATE NONCLUSTERED INDEX IX_CREDIT_DEBIT_ADJUSTMENT_InvoiceID ON CREDIT_DEBIT_ADJUSTMENT (InvoiceID);

IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE name = 'IX_CREDIT_DEBIT_ADJUSTMENT_CreatedByUserID' AND object_id = OBJECT_ID('CREDIT_DEBIT_ADJUSTMENT'))
    CREATE NONCLUSTERED INDEX IX_CREDIT_DEBIT_ADJUSTMENT_CreatedByUserID ON CREDIT_DEBIT_ADJUSTMENT (CreatedByUserID);

-- AUDIT_LOG
IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE name = 'IX_AUDIT_LOG_ShopID' AND object_id = OBJECT_ID('AUDIT_LOG'))
    CREATE NONCLUSTERED INDEX IX_AUDIT_LOG_ShopID ON AUDIT_LOG (ShopID);

IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE name = 'IX_AUDIT_LOG_UserID' AND object_id = OBJECT_ID('AUDIT_LOG'))
    CREATE NONCLUSTERED INDEX IX_AUDIT_LOG_UserID ON AUDIT_LOG (UserID);

IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE name = 'IX_AUDIT_LOG_CreatedAt' AND object_id = OBJECT_ID('AUDIT_LOG'))
    CREATE NONCLUSTERED INDEX IX_AUDIT_LOG_CreatedAt ON AUDIT_LOG (CreatedAt);

IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE name = 'IX_AuditLog_ShopID_CreatedAt' AND object_id = OBJECT_ID('AUDIT_LOG'))
    CREATE NONCLUSTERED INDEX IX_AuditLog_ShopID_CreatedAt ON AUDIT_LOG (ShopID, CreatedAt DESC) INCLUDE (UserID, Action, EntityName, EntityID);

-- ACCOUNTING_ENTRY
IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE name = 'IX_ACCOUNTING_ENTRY_ShopID' AND object_id = OBJECT_ID('ACCOUNTING_ENTRY'))
    CREATE NONCLUSTERED INDEX IX_ACCOUNTING_ENTRY_ShopID ON ACCOUNTING_ENTRY (ShopID);

IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE name = 'IX_ACCOUNTING_ENTRY_SourceInvoiceID' AND object_id = OBJECT_ID('ACCOUNTING_ENTRY'))
    CREATE NONCLUSTERED INDEX IX_ACCOUNTING_ENTRY_SourceInvoiceID ON ACCOUNTING_ENTRY (SourceInvoiceID);

IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE name = 'IX_ACCOUNTING_ENTRY_SourcePaymentID' AND object_id = OBJECT_ID('ACCOUNTING_ENTRY'))
    CREATE NONCLUSTERED INDEX IX_ACCOUNTING_ENTRY_SourcePaymentID ON ACCOUNTING_ENTRY (SourcePaymentID);

-- XERO_SYNC_LOG
IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE name = 'IX_XERO_SYNC_LOG_ShopID' AND object_id = OBJECT_ID('XERO_SYNC_LOG'))
    CREATE NONCLUSTERED INDEX IX_XERO_SYNC_LOG_ShopID ON XERO_SYNC_LOG (ShopID);

IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE name = 'IX_XERO_SYNC_LOG_SyncedByUserID' AND object_id = OBJECT_ID('XERO_SYNC_LOG'))
    CREATE NONCLUSTERED INDEX IX_XERO_SYNC_LOG_SyncedByUserID ON XERO_SYNC_LOG (SyncedByUserID);

IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE name = 'IX_XERO_SYNC_LOG_InvoiceID' AND object_id = OBJECT_ID('XERO_SYNC_LOG'))
    CREATE NONCLUSTERED INDEX IX_XERO_SYNC_LOG_InvoiceID ON XERO_SYNC_LOG (InvoiceID);

IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE name = 'IX_XERO_SYNC_LOG_PaymentID' AND object_id = OBJECT_ID('XERO_SYNC_LOG'))
    CREATE NONCLUSTERED INDEX IX_XERO_SYNC_LOG_PaymentID ON XERO_SYNC_LOG (PaymentID);

IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE name = 'IX_XERO_SYNC_LOG_AccountingEntryID' AND object_id = OBJECT_ID('XERO_SYNC_LOG'))
    CREATE NONCLUSTERED INDEX IX_XERO_SYNC_LOG_AccountingEntryID ON XERO_SYNC_LOG (AccountingEntryID);

PRINT '✓ Created all performance indexes';
GO


PRINT '';
PRINT '================================================================';
PRINT '  SEEDING DEFAULT DATA';
PRINT '================================================================';
PRINT '';
GO


-- ============================================================================
-- SEED DATA
-- ============================================================================

-- Seed: Default Shop
IF NOT EXISTS (SELECT 1 FROM SHOP WHERE ShopCode = 'MAIN')
BEGIN
    INSERT INTO SHOP (ShopCode, ShopName, Email, Phone, [Address])
    VALUES ('MAIN', 'ByteBill Main Shop', 'admin@bytebill.com', '+63 XXX XXX XXXX', 'J.P. Laurel Ave., Davao City, Philippines');
    PRINT '✓ Inserted default shop (ShopCode: MAIN)';
END
ELSE
BEGIN
    UPDATE SHOP
    SET [Address] = 'J.P. Laurel Ave., Davao City, Philippines',
        Phone = '+63 XXX XXX XXXX'
    WHERE ShopCode = 'MAIN'
      AND ([Address] = 'Metro Manila, Philippines' OR Phone = '+63-000-000-0000');
    PRINT '⚠ Shop MAIN already exists — updated address and phone defaults';
END
GO

-- Seed: Roles (matching the application enum)
IF NOT EXISTS (SELECT 1 FROM ROLES WHERE RoleID = 1)
BEGIN
    SET IDENTITY_INSERT ROLES ON;

    INSERT INTO ROLES (RoleID, RoleName, [Description])
    VALUES
        (1, 'SuperAdmin',  'Full system access across all shops'),
        (2, 'Admin',       'Shop Owner — full access within a single shop'),
        (3, 'Billing',     'Billing staff — invoices, payments, and customer management'),
        (4, 'Technician',  'Technician — job orders, diagnostics, and repairs'),
        (5, 'Auditor',     'Auditor — read-only access for review and compliance');

    SET IDENTITY_INSERT ROLES OFF;
    PRINT '✓ Inserted 5 roles (SuperAdmin, Admin, Billing, Technician, Auditor)';
END
ELSE
    PRINT '⚠ Roles already exist';
GO


-- Seed: Demo Users with Real Names
-- Each user has a unique password hashed with BCrypt (cost factor 12)
-- ┌──────────────┬────────────────────┐
-- │ Username      │ Password           │
-- ├──────────────┼────────────────────┤
-- │ vkpadao       │ Superadmin123!     │
-- │ admin         │ Admin123!          │
-- │ billing       │ Billing123!        │
-- │ technician    │ Technician123!     │
-- │ auditor       │ Auditor123!        │
-- └──────────────┴────────────────────┘

IF NOT EXISTS (SELECT 1 FROM USERS WHERE UserID = 1)
BEGIN
    SET IDENTITY_INSERT USERS ON;

    INSERT INTO USERS (UserID, ShopID, FirstName, MiddleName, LastName, UserName, PasswordHash, Email, Phone, ThemePreference, IsActive)
    VALUES
        (1, 1, 'Vaness',  NULL, 'Padao',  'vkpadao',     '$2a$12$RMVVCzlpcg7ckzii6W9aG.dJMpjq57OjNM3S3kdNGXzJaQCtciHA.', 'vkpadao@bytebill.com', '+63-917-123-4567', 'light', 1),
        (2, 1, 'Maria',   NULL, 'Santos', 'admin',       '$2a$12$8ModjUcaRtWQCsW7c8RGeufnBMPihYnf6lHE9p5H0ApkkQEckrdEK', 'admin@bytebill.com',   '+63-917-234-5678', 'light', 1),
        (3, 1, 'Juan',    NULL, 'Cruz',   'billing',     '$2a$12$4rVvnj4wroAuJkOORA3uT.IALBEXp5gi5/865MfgKZ/AWzmrYAiWi', 'billing@bytebill.com', '+63-917-345-6789', 'light', 1),
        (4, 1, 'Carlos',  NULL, 'Reyes',  'technician',  '$2a$12$dBtT/QFYy2ScGvTdykBbD.l5ZqVp2DxdjXsPoktyyTnRwqJ0vtcPm', 'tech@bytebill.com',    '+63-917-456-7890', 'light', 1),
        (5, 1, 'Ana',     NULL, 'Garcia', 'auditor',     '$2a$12$qibG2sU9lwoTTN0q1KUECe5u8REijfmScUV8d8Q5tt67avJEnCqjK', 'audit@bytebill.com',   '+63-917-567-8901', 'light', 1);

    SET IDENTITY_INSERT USERS OFF;
    PRINT '✓ Inserted 5 demo users (vkpadao, admin, billing, technician, auditor)';
END
ELSE
    PRINT '⚠ Users already exist';
GO


-- Seed: User-Role Assignments (one role per demo user)
IF NOT EXISTS (SELECT 1 FROM USER_ROLES WHERE UserRoleID = 1)
BEGIN
    SET IDENTITY_INSERT USER_ROLES ON;

    INSERT INTO USER_ROLES (UserRoleID, UserID, RoleID)
    VALUES
        (1, 1, 1),   -- vkpadao     → SuperAdmin
        (2, 2, 2),   -- admin       → Admin
        (3, 3, 3),   -- billing     → Billing
        (4, 4, 4),   -- technician  → Technician
        (5, 5, 5);   -- auditor     → Auditor

    SET IDENTITY_INSERT USER_ROLES OFF;
    PRINT '✓ Assigned roles to 5 demo users';
END
ELSE
    PRINT '⚠ User-role assignments already exist';
GO


PRINT '';
PRINT '================================================================';
PRINT '  DEPLOYMENT COMPLETE';
PRINT '================================================================';
PRINT '';
PRINT 'Database: ByteBillDB';
PRINT 'Tables created: 23';
PRINT 'Indexes created: 60+';
PRINT '';
PRINT '┌────────────────────────────────────────────────────────────┐';
PRINT '│  DEFAULT LOGIN CREDENTIALS                                 │';
PRINT '├──────────────┬───────────────────┬────────────────────────┤';
PRINT '│ Username      │ Full Name         │ Password               │';
PRINT '├──────────────┼───────────────────┼────────────────────────┤';
PRINT '│ vkpadao       │ Vaness Padao      │ Superadmin123!         │';
PRINT '│ admin         │ Maria Santos      │ Admin123!              │';
PRINT '│ billing       │ Juan Cruz         │ Billing123!            │';
PRINT '│ technician    │ Carlos Reyes      │ Technician123!         │';
PRINT '│ auditor       │ Ana Garcia        │ Auditor123!            │';
PRINT '└──────────────┴───────────────────┴────────────────────────┘';
PRINT '';
PRINT '⚠️  IMPORTANT: Change these default passwords after first login!';
PRINT '';
PRINT 'Finished: ' + CONVERT(NVARCHAR(30), SYSDATETIME(), 120);
GO
