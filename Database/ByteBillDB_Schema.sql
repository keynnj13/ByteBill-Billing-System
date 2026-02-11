/*******************************************************************************
 *  ByteBill: Web-Based Billing System for Computer Repair Services
 *  Microsoft SQL Server Database Schema
 *  Generated: 2026-02-10
 *
 *  Run this entire script in SSMS (or sqlcmd) to create the database,
 *  all tables, constraints, indexes, and seed data.
 *
 *  Dependency order is respected — every FK target exists before its source.
 ******************************************************************************/

-- ============================================================================
-- 0. DATABASE
-- ============================================================================
CREATE DATABASE ByteBillDB;
GO
USE ByteBillDB;
GO


-- ============================================================================
-- A. SHOP
-- ============================================================================
CREATE TABLE SHOP
(
    ShopID    BIGINT        IDENTITY(1,1) NOT NULL,
    ShopCode  NVARCHAR(30)  NOT NULL,
    ShopName  NVARCHAR(150) NOT NULL,
    Email     NVARCHAR(100) NULL,
    Phone     NVARCHAR(30)  NULL,
    [Address] NVARCHAR(255) NULL,
    [Status]  NVARCHAR(20)  NOT NULL  DEFAULT 'Active',
    CreatedAt DATETIME2(0)  NOT NULL  DEFAULT SYSDATETIME(),
    UpdatedAt DATETIME2(0)  NULL,

    CONSTRAINT PK_SHOP        PRIMARY KEY (ShopID),
    CONSTRAINT UQ_SHOP_Code   UNIQUE      (ShopCode)
);
GO


-- ============================================================================
-- B. USERS
-- ============================================================================
CREATE TABLE USERS
(
    UserID       BIGINT        IDENTITY(1,1) NOT NULL,
    ShopID       BIGINT        NOT NULL,                        -- FK → SHOP
    FirstName    NVARCHAR(50)  NOT NULL,
    MiddleName   NVARCHAR(50)  NULL,
    LastName     NVARCHAR(50)  NOT NULL,
    UserName     NVARCHAR(100) NOT NULL,
    PasswordHash NVARCHAR(255) NOT NULL,
    IsActive     BIT           NOT NULL  DEFAULT 1,
    CreatedAt    DATETIME2(0)  NOT NULL  DEFAULT SYSDATETIME(),
    UpdatedAt    DATETIME2(0)  NULL,

    CONSTRAINT PK_USERS               PRIMARY KEY (UserID),
    CONSTRAINT FK_USERS_Shop          FOREIGN KEY (ShopID) REFERENCES SHOP (ShopID)
                                      ON UPDATE NO ACTION ON DELETE NO ACTION,
    CONSTRAINT UQ_USERS_ShopUserName  UNIQUE      (ShopID, UserName)
);
GO


-- ============================================================================
-- C. ROLES
-- ============================================================================
CREATE TABLE ROLES
(
    RoleID      BIGINT        IDENTITY(1,1) NOT NULL,
    RoleName    NVARCHAR(50)  NOT NULL,
    [Description] NVARCHAR(150) NULL,

    CONSTRAINT PK_ROLES          PRIMARY KEY (RoleID),
    CONSTRAINT UQ_ROLES_Name     UNIQUE      (RoleName)
);
GO


-- ============================================================================
-- D. USER_ROLES
-- ============================================================================
CREATE TABLE USER_ROLES
(
    UserRoleID  BIGINT       IDENTITY(1,1) NOT NULL,
    UserID      BIGINT       NOT NULL,                          -- FK → USERS
    RoleID      BIGINT       NOT NULL,                          -- FK → ROLES
    AssignedAt  DATETIME2(0) NOT NULL  DEFAULT SYSDATETIME(),

    CONSTRAINT PK_USER_ROLES              PRIMARY KEY (UserRoleID),
    CONSTRAINT FK_USER_ROLES_User         FOREIGN KEY (UserID) REFERENCES USERS (UserID)
                                          ON UPDATE NO ACTION ON DELETE NO ACTION,
    CONSTRAINT FK_USER_ROLES_Role         FOREIGN KEY (RoleID) REFERENCES ROLES (RoleID)
                                          ON UPDATE NO ACTION ON DELETE NO ACTION,
    CONSTRAINT UQ_USER_ROLES_UserRole     UNIQUE      (UserID, RoleID)
);
GO


-- ============================================================================
-- E. CUSTOMERS
-- ============================================================================
CREATE TABLE CUSTOMERS
(
    CustomerID  BIGINT        IDENTITY(1,1) NOT NULL,
    ShopID      BIGINT        NOT NULL,                         -- FK → SHOP
    FirstName   NVARCHAR(50)  NOT NULL,
    MiddleName  NVARCHAR(50)  NULL,
    LastName    NVARCHAR(50)  NOT NULL,
    Email       NVARCHAR(100) NULL,
    Phone       NVARCHAR(30)  NULL,
    [Address]   NVARCHAR(255) NULL,
    CreatedAt   DATETIME2(0)  NOT NULL  DEFAULT SYSDATETIME(),

    CONSTRAINT PK_CUSTOMERS           PRIMARY KEY (CustomerID),
    CONSTRAINT FK_CUSTOMERS_Shop      FOREIGN KEY (ShopID) REFERENCES SHOP (ShopID)
                                      ON UPDATE NO ACTION ON DELETE NO ACTION
);
GO


-- ============================================================================
-- F. DEVICES
-- ============================================================================
CREATE TABLE DEVICES
(
    DeviceID    BIGINT        IDENTITY(1,1) NOT NULL,
    CustomerID  BIGINT        NOT NULL,                         -- FK → CUSTOMERS
    DeviceType  NVARCHAR(50)  NOT NULL,
    Brand       NVARCHAR(50)  NOT NULL,
    Model       NVARCHAR(80)  NOT NULL,
    SerialNo    NVARCHAR(60)  NULL,
    Notes       NVARCHAR(255) NULL,

    CONSTRAINT PK_DEVICES             PRIMARY KEY (DeviceID),
    CONSTRAINT FK_DEVICES_Customer    FOREIGN KEY (CustomerID) REFERENCES CUSTOMERS (CustomerID)
                                      ON UPDATE NO ACTION ON DELETE NO ACTION
);
GO


-- ============================================================================
-- G. SERVICE_CATEGORY
-- ============================================================================
CREATE TABLE SERVICE_CATEGORY
(
    ServiceCategoryID  BIGINT        IDENTITY(1,1) NOT NULL,
    ShopID             BIGINT        NOT NULL,                  -- FK → SHOP
    CategoryName       NVARCHAR(80)  NOT NULL,
    [Description]      NVARCHAR(150) NULL,

    CONSTRAINT PK_SERVICE_CATEGORY              PRIMARY KEY (ServiceCategoryID),
    CONSTRAINT FK_SERVICE_CATEGORY_Shop         FOREIGN KEY (ShopID) REFERENCES SHOP (ShopID)
                                                ON UPDATE NO ACTION ON DELETE NO ACTION,
    CONSTRAINT UQ_SERVICE_CATEGORY_ShopName     UNIQUE      (ShopID, CategoryName)
);
GO


-- ============================================================================
-- H. SERVICE_CATALOG
-- ============================================================================
CREATE TABLE SERVICE_CATALOG
(
    ServiceID          BIGINT          IDENTITY(1,1) NOT NULL,
    ShopID             BIGINT          NOT NULL,                -- FK → SHOP
    ServiceCategoryID  BIGINT          NOT NULL,                -- FK → SERVICE_CATEGORY
    ServiceName        NVARCHAR(120)   NOT NULL,
    BasePrice          DECIMAL(18,2)   NOT NULL  DEFAULT 0,
    IsActive           BIT             NOT NULL  DEFAULT 1,

    CONSTRAINT PK_SERVICE_CATALOG               PRIMARY KEY (ServiceID),
    CONSTRAINT FK_SERVICE_CATALOG_Shop          FOREIGN KEY (ShopID) REFERENCES SHOP (ShopID)
                                                ON UPDATE NO ACTION ON DELETE NO ACTION,
    CONSTRAINT FK_SERVICE_CATALOG_Category      FOREIGN KEY (ServiceCategoryID) REFERENCES SERVICE_CATEGORY (ServiceCategoryID)
                                                ON UPDATE NO ACTION ON DELETE NO ACTION,
    CONSTRAINT UQ_SERVICE_CATALOG_ShopName      UNIQUE      (ShopID, ServiceName)
);
GO


-- ============================================================================
-- I. INVENTORY_ITEMS
-- ============================================================================
CREATE TABLE INVENTORY_ITEMS
(
    ItemID       BIGINT         IDENTITY(1,1) NOT NULL,
    ShopID       BIGINT         NOT NULL,                       -- FK → SHOP
    SKU          NVARCHAR(40)   NOT NULL,
    ItemName     NVARCHAR(120)  NOT NULL,
    Unit         NVARCHAR(20)   NOT NULL,
    UnitCost     DECIMAL(18,2)  NOT NULL  DEFAULT 0,
    UnitPrice    DECIMAL(18,2)  NOT NULL  DEFAULT 0,
    QtyOnHand    INT            NOT NULL  DEFAULT 0,
    ReorderLevel INT            NOT NULL  DEFAULT 0,
    IsActive     BIT            NOT NULL  DEFAULT 1,

    CONSTRAINT PK_INVENTORY_ITEMS            PRIMARY KEY (ItemID),
    CONSTRAINT FK_INVENTORY_ITEMS_Shop       FOREIGN KEY (ShopID) REFERENCES SHOP (ShopID)
                                             ON UPDATE NO ACTION ON DELETE NO ACTION,
    CONSTRAINT UQ_INVENTORY_ITEMS_ShopSKU    UNIQUE      (ShopID, SKU)
);
GO


-- ============================================================================
-- J. INVENTORY_TXN
-- ============================================================================
CREATE TABLE INVENTORY_TXN
(
    InventoryTxnID  BIGINT        IDENTITY(1,1) NOT NULL,
    ItemID          BIGINT        NOT NULL,                     -- FK → INVENTORY_ITEMS
    TxnType         NVARCHAR(10)  NOT NULL,
    Quantity        INT           NOT NULL,
    ReferenceType   NVARCHAR(30)  NULL,
    ReferenceID     BIGINT        NULL,
    Remarks         NVARCHAR(150) NULL,
    CreatedAt       DATETIME2(0)  NOT NULL  DEFAULT SYSDATETIME(),

    CONSTRAINT PK_INVENTORY_TXN              PRIMARY KEY (InventoryTxnID),
    CONSTRAINT FK_INVENTORY_TXN_Item         FOREIGN KEY (ItemID) REFERENCES INVENTORY_ITEMS (ItemID)
                                             ON UPDATE NO ACTION ON DELETE NO ACTION,
    CONSTRAINT CK_INVENTORY_TXN_Type         CHECK (TxnType IN ('IN','OUT','ADJUST'))
);
GO


-- ============================================================================
-- K. JOB_ORDERS
-- ============================================================================
CREATE TABLE JOB_ORDERS
(
    JobOrderID         BIGINT        IDENTITY(1,1) NOT NULL,
    ShopID             BIGINT        NOT NULL,                  -- FK → SHOP
    CustomerID         BIGINT        NOT NULL,                  -- FK → CUSTOMERS
    DeviceID           BIGINT        NOT NULL,                  -- FK → DEVICES
    CreatedByUserID    BIGINT        NOT NULL,                  -- FK → USERS
    AssignedTechUserID BIGINT        NULL,                      -- FK → USERS (nullable)
    JobOrderNo         NVARCHAR(30)  NOT NULL,
    ProblemReported    NVARCHAR(255) NOT NULL,
    DiagnosisNotes     NVARCHAR(255) NULL,
    [Status]           NVARCHAR(30)  NOT NULL  DEFAULT 'Created',
    CreatedAt          DATETIME2(0)  NOT NULL  DEFAULT SYSDATETIME(),
    UpdatedAt          DATETIME2(0)  NULL,

    CONSTRAINT PK_JOB_ORDERS                   PRIMARY KEY (JobOrderID),
    CONSTRAINT FK_JOB_ORDERS_Shop              FOREIGN KEY (ShopID) REFERENCES SHOP (ShopID)
                                               ON UPDATE NO ACTION ON DELETE NO ACTION,
    CONSTRAINT FK_JOB_ORDERS_Customer          FOREIGN KEY (CustomerID) REFERENCES CUSTOMERS (CustomerID)
                                               ON UPDATE NO ACTION ON DELETE NO ACTION,
    CONSTRAINT FK_JOB_ORDERS_Device            FOREIGN KEY (DeviceID) REFERENCES DEVICES (DeviceID)
                                               ON UPDATE NO ACTION ON DELETE NO ACTION,
    CONSTRAINT FK_JOB_ORDERS_CreatedBy         FOREIGN KEY (CreatedByUserID) REFERENCES USERS (UserID)
                                               ON UPDATE NO ACTION ON DELETE NO ACTION,
    CONSTRAINT FK_JOB_ORDERS_AssignedTech      FOREIGN KEY (AssignedTechUserID) REFERENCES USERS (UserID)
                                               ON UPDATE NO ACTION ON DELETE NO ACTION,
    CONSTRAINT UQ_JOB_ORDERS_ShopJobOrderNo    UNIQUE      (ShopID, JobOrderNo)
);
GO


-- ============================================================================
-- L. JOB_ORDER_SERVICES
-- ============================================================================
CREATE TABLE JOB_ORDER_SERVICES
(
    JobOrderServiceID  BIGINT         IDENTITY(1,1) NOT NULL,
    JobOrderID         BIGINT         NOT NULL,                 -- FK → JOB_ORDERS
    ServiceID          BIGINT         NOT NULL,                 -- FK → SERVICE_CATALOG
    Qty                INT            NOT NULL  DEFAULT 1,
    UnitPrice          DECIMAL(18,2)  NOT NULL  DEFAULT 0,
    LineTotal          AS (Qty * UnitPrice) PERSISTED,

    CONSTRAINT PK_JOB_ORDER_SERVICES            PRIMARY KEY (JobOrderServiceID),
    CONSTRAINT FK_JOB_ORDER_SERVICES_JobOrder   FOREIGN KEY (JobOrderID) REFERENCES JOB_ORDERS (JobOrderID)
                                                ON UPDATE NO ACTION ON DELETE NO ACTION,
    CONSTRAINT FK_JOB_ORDER_SERVICES_Service    FOREIGN KEY (ServiceID)  REFERENCES SERVICE_CATALOG (ServiceID)
                                                ON UPDATE NO ACTION ON DELETE NO ACTION
);
GO


-- ============================================================================
-- M. JOB_ORDER_PARTS
-- ============================================================================
CREATE TABLE JOB_ORDER_PARTS
(
    JobOrderPartID  BIGINT         IDENTITY(1,1) NOT NULL,
    JobOrderID      BIGINT         NOT NULL,                    -- FK → JOB_ORDERS
    ItemID          BIGINT         NOT NULL,                    -- FK → INVENTORY_ITEMS
    QtyUsed         INT            NOT NULL  DEFAULT 1,
    UnitPrice       DECIMAL(18,2)  NOT NULL  DEFAULT 0,
    LineTotal       AS (QtyUsed * UnitPrice) PERSISTED,

    CONSTRAINT PK_JOB_ORDER_PARTS              PRIMARY KEY (JobOrderPartID),
    CONSTRAINT FK_JOB_ORDER_PARTS_JobOrder     FOREIGN KEY (JobOrderID) REFERENCES JOB_ORDERS (JobOrderID)
                                               ON UPDATE NO ACTION ON DELETE NO ACTION,
    CONSTRAINT FK_JOB_ORDER_PARTS_Item         FOREIGN KEY (ItemID)     REFERENCES INVENTORY_ITEMS (ItemID)
                                               ON UPDATE NO ACTION ON DELETE NO ACTION
);
GO


-- ============================================================================
-- N. JOB_ORDER_STATUS_HISTORY
-- ============================================================================
CREATE TABLE JOB_ORDER_STATUS_HISTORY
(
    JobOrderStatusHistoryID  BIGINT        IDENTITY(1,1) NOT NULL,
    JobOrderID               BIGINT        NOT NULL,            -- FK → JOB_ORDERS
    OldStatus                NVARCHAR(30)  NOT NULL,
    NewStatus                NVARCHAR(30)  NOT NULL,
    ChangedByUserID          BIGINT        NOT NULL,            -- FK → USERS
    ChangedAt                DATETIME2(0)  NOT NULL  DEFAULT SYSDATETIME(),
    Remarks                  NVARCHAR(150) NULL,

    CONSTRAINT PK_JOB_ORDER_STATUS_HISTORY              PRIMARY KEY (JobOrderStatusHistoryID),
    CONSTRAINT FK_JOB_ORDER_STATUS_HISTORY_JobOrder     FOREIGN KEY (JobOrderID)      REFERENCES JOB_ORDERS (JobOrderID)
                                                        ON UPDATE NO ACTION ON DELETE NO ACTION,
    CONSTRAINT FK_JOB_ORDER_STATUS_HISTORY_ChangedBy    FOREIGN KEY (ChangedByUserID) REFERENCES USERS (UserID)
                                                        ON UPDATE NO ACTION ON DELETE NO ACTION
);
GO


-- ============================================================================
-- O. INVOICES
--    • UNIQUE(JobOrderID) enforces a strict 1:1 relationship:
--      each JOB_ORDER can have at most ONE Invoice.
-- ============================================================================
CREATE TABLE INVOICES
(
    InvoiceID        BIGINT         IDENTITY(1,1) NOT NULL,
    ShopID           BIGINT         NOT NULL,                   -- FK → SHOP
    JobOrderID       BIGINT         NOT NULL,                   -- FK → JOB_ORDERS  (1:1)
    CustomerID       BIGINT         NOT NULL,                   -- FK → CUSTOMERS
    InvoiceNo        NVARCHAR(30)   NOT NULL,
    InvoiceDate      DATETIME2(0)   NOT NULL  DEFAULT SYSDATETIME(),
    Subtotal         DECIMAL(18,2)  NOT NULL  DEFAULT 0,
    TotalAdjustments DECIMAL(18,2)  NOT NULL  DEFAULT 0,
    TotalAmount      DECIMAL(18,2)  NOT NULL  DEFAULT 0,
    AmountPaid       DECIMAL(18,2)  NOT NULL  DEFAULT 0,
    Balance          DECIMAL(18,2)  NOT NULL  DEFAULT 0,
    [Status]         NVARCHAR(20)   NOT NULL  DEFAULT 'Unpaid',
    CreatedAt        DATETIME2(0)   NOT NULL  DEFAULT SYSDATETIME(),

    CONSTRAINT PK_INVOICES                    PRIMARY KEY (InvoiceID),
    CONSTRAINT FK_INVOICES_Shop               FOREIGN KEY (ShopID)     REFERENCES SHOP (ShopID)
                                              ON UPDATE NO ACTION ON DELETE NO ACTION,
    CONSTRAINT FK_INVOICES_JobOrder           FOREIGN KEY (JobOrderID) REFERENCES JOB_ORDERS (JobOrderID)
                                              ON UPDATE NO ACTION ON DELETE NO ACTION,
    CONSTRAINT FK_INVOICES_Customer           FOREIGN KEY (CustomerID) REFERENCES CUSTOMERS (CustomerID)
                                              ON UPDATE NO ACTION ON DELETE NO ACTION,
    CONSTRAINT CK_INVOICES_Status             CHECK ([Status] IN ('Unpaid','Partial','Paid','Void')),
    CONSTRAINT UQ_INVOICES_ShopInvoiceNo      UNIQUE (ShopID, InvoiceNo),
    CONSTRAINT UQ_INVOICES_JobOrderID         UNIQUE (JobOrderID)       -- enforces 1:1 JOB_ORDERS → INVOICES
);
GO


-- ============================================================================
-- P. INVOICE_LINES
-- ============================================================================
CREATE TABLE INVOICE_LINES
(
    InvoiceLineID  BIGINT         IDENTITY(1,1) NOT NULL,
    InvoiceID      BIGINT         NOT NULL,                     -- FK → INVOICES
    LineType       NVARCHAR(20)   NOT NULL,
    [Description]  NVARCHAR(150)  NOT NULL,
    Qty            INT            NOT NULL  DEFAULT 1,
    UnitPrice      DECIMAL(18,2)  NOT NULL  DEFAULT 0,
    LineTotal      AS (Qty * UnitPrice) PERSISTED,

    CONSTRAINT PK_INVOICE_LINES              PRIMARY KEY (InvoiceLineID),
    CONSTRAINT FK_INVOICE_LINES_Invoice      FOREIGN KEY (InvoiceID) REFERENCES INVOICES (InvoiceID)
                                             ON UPDATE NO ACTION ON DELETE NO ACTION
);
GO


-- ============================================================================
-- Q. PAYMENTS
-- ============================================================================
CREATE TABLE PAYMENTS
(
    PaymentID         BIGINT         IDENTITY(1,1) NOT NULL,
    ShopID            BIGINT         NOT NULL,                  -- FK → SHOP
    CustomerID        BIGINT         NOT NULL,                  -- FK → CUSTOMERS
    PaymentDate       DATETIME2(0)   NOT NULL  DEFAULT SYSDATETIME(),
    Amount            DECIMAL(18,2)  NOT NULL,
    Method            NVARCHAR(30)   NOT NULL,
    ReferenceNo       NVARCHAR(60)   NULL,
    ReceivedByUserID  BIGINT         NOT NULL,                  -- FK → USERS
    [Status]          NVARCHAR(20)   NOT NULL  DEFAULT 'Confirmed',

    CONSTRAINT PK_PAYMENTS                   PRIMARY KEY (PaymentID),
    CONSTRAINT FK_PAYMENTS_Shop              FOREIGN KEY (ShopID)           REFERENCES SHOP (ShopID)
                                             ON UPDATE NO ACTION ON DELETE NO ACTION,
    CONSTRAINT FK_PAYMENTS_Customer          FOREIGN KEY (CustomerID)       REFERENCES CUSTOMERS (CustomerID)
                                             ON UPDATE NO ACTION ON DELETE NO ACTION,
    CONSTRAINT FK_PAYMENTS_ReceivedBy        FOREIGN KEY (ReceivedByUserID) REFERENCES USERS (UserID)
                                             ON UPDATE NO ACTION ON DELETE NO ACTION,
    CONSTRAINT CK_PAYMENTS_Method            CHECK (Method IN ('Cash','GCash','Card','PayMongo')),
    CONSTRAINT CK_PAYMENTS_Status            CHECK ([Status] IN ('Pending','Confirmed','Failed','Refunded'))
);
GO


-- ============================================================================
-- R. PAYMENT_ALLOCATION
-- ============================================================================
CREATE TABLE PAYMENT_ALLOCATION
(
    PaymentAllocationID  BIGINT         IDENTITY(1,1) NOT NULL,
    PaymentID            BIGINT         NOT NULL,               -- FK → PAYMENTS
    InvoiceID            BIGINT         NOT NULL,               -- FK → INVOICES
    AmountApplied        DECIMAL(18,2)  NOT NULL,

    CONSTRAINT PK_PAYMENT_ALLOCATION                  PRIMARY KEY (PaymentAllocationID),
    CONSTRAINT FK_PAYMENT_ALLOCATION_Payment          FOREIGN KEY (PaymentID) REFERENCES PAYMENTS (PaymentID)
                                                      ON UPDATE NO ACTION ON DELETE NO ACTION,
    CONSTRAINT FK_PAYMENT_ALLOCATION_Invoice          FOREIGN KEY (InvoiceID) REFERENCES INVOICES (InvoiceID)
                                                      ON UPDATE NO ACTION ON DELETE NO ACTION,
    CONSTRAINT UQ_PAYMENT_ALLOCATION_PaymentInvoice   UNIQUE (PaymentID, InvoiceID)
);
GO


-- ============================================================================
-- S. PAYMONGO_TXN
--    • UNIQUE(PaymentID) enforces an optional 1:0..1 relationship:
--      a PAYMENT may have zero or one PayMongo transaction record.
--      If the payment method is not 'PayMongo', no row exists here.
-- ============================================================================
CREATE TABLE PAYMONGO_TXN
(
    PayMongoTxnID           BIGINT         IDENTITY(1,1) NOT NULL,
    PaymentID               BIGINT         NOT NULL,            -- FK → PAYMENTS
    PayMongoPaymentIntentID NVARCHAR(80)   NOT NULL,
    PayMongoStatus          NVARCHAR(30)   NOT NULL,
    RawResponse             NVARCHAR(MAX)  NULL,
    CreatedAt               DATETIME2(0)   NOT NULL  DEFAULT SYSDATETIME(),

    CONSTRAINT PK_PAYMONGO_TXN              PRIMARY KEY (PayMongoTxnID),
    CONSTRAINT FK_PAYMONGO_TXN_Payment      FOREIGN KEY (PaymentID) REFERENCES PAYMENTS (PaymentID)
                                            ON UPDATE NO ACTION ON DELETE NO ACTION,
    CONSTRAINT UQ_PAYMONGO_TXN_PaymentID    UNIQUE (PaymentID)  -- enforces 1:0..1 PAYMENT → PAYMONGO_TXN
);
GO


-- ============================================================================
-- T. CREDIT_DEBIT_ADJUSTMENT
-- ============================================================================
CREATE TABLE CREDIT_DEBIT_ADJUSTMENT
(
    AdjustmentID    BIGINT         IDENTITY(1,1) NOT NULL,
    InvoiceID       BIGINT         NOT NULL,                    -- FK → INVOICES
    CreatedByUserID BIGINT         NOT NULL,                    -- FK → USERS
    AdjustmentType  NVARCHAR(10)   NOT NULL,
    Amount          DECIMAL(18,2)  NOT NULL,
    Reason          NVARCHAR(150)  NOT NULL,
    CreatedAt       DATETIME2(0)   NOT NULL  DEFAULT SYSDATETIME(),

    CONSTRAINT PK_CREDIT_DEBIT_ADJUSTMENT              PRIMARY KEY (AdjustmentID),
    CONSTRAINT FK_CREDIT_DEBIT_ADJUSTMENT_Invoice      FOREIGN KEY (InvoiceID)       REFERENCES INVOICES (InvoiceID)
                                                       ON UPDATE NO ACTION ON DELETE NO ACTION,
    CONSTRAINT FK_CREDIT_DEBIT_ADJUSTMENT_CreatedBy    FOREIGN KEY (CreatedByUserID)  REFERENCES USERS (UserID)
                                                       ON UPDATE NO ACTION ON DELETE NO ACTION,
    CONSTRAINT CK_CREDIT_DEBIT_ADJUSTMENT_Type         CHECK (AdjustmentType IN ('CREDIT','DEBIT'))
);
GO


-- ============================================================================
-- U. AUDIT_LOG
-- ============================================================================
CREATE TABLE AUDIT_LOG
(
    AuditLogID  BIGINT        IDENTITY(1,1) NOT NULL,
    ShopID      BIGINT        NOT NULL,                         -- FK → SHOP
    UserID      BIGINT        NOT NULL,                         -- FK → USERS
    [Action]    NVARCHAR(50)  NOT NULL,
    EntityName  NVARCHAR(50)  NOT NULL,
    EntityID    BIGINT        NOT NULL,
    Details     NVARCHAR(500) NULL,
    CreatedAt   DATETIME2(0)  NOT NULL  DEFAULT SYSDATETIME(),

    CONSTRAINT PK_AUDIT_LOG           PRIMARY KEY (AuditLogID),
    CONSTRAINT FK_AUDIT_LOG_Shop      FOREIGN KEY (ShopID) REFERENCES SHOP (ShopID)
                                      ON UPDATE NO ACTION ON DELETE NO ACTION,
    CONSTRAINT FK_AUDIT_LOG_User      FOREIGN KEY (UserID) REFERENCES USERS (UserID)
                                      ON UPDATE NO ACTION ON DELETE NO ACTION
);
GO


-- ============================================================================
-- V. ACCOUNTING_ENTRY
-- ============================================================================
CREATE TABLE ACCOUNTING_ENTRY
(
    AccountingEntryID  BIGINT         IDENTITY(1,1) NOT NULL,
    ShopID             BIGINT         NOT NULL,                 -- FK → SHOP
    SourceType         NVARCHAR(20)   NOT NULL,
    SourceInvoiceID    BIGINT         NULL,                     -- FK → INVOICES (nullable)
    SourcePaymentID    BIGINT         NULL,                     -- FK → PAYMENTS (nullable)
    EntryDate          DATETIME2(0)   NOT NULL  DEFAULT SYSDATETIME(),
    AccountCode        NVARCHAR(20)   NOT NULL,
    Debit              DECIMAL(18,2)  NOT NULL  DEFAULT 0,
    Credit             DECIMAL(18,2)  NOT NULL  DEFAULT 0,
    Memo               NVARCHAR(150)  NULL,

    CONSTRAINT PK_ACCOUNTING_ENTRY                  PRIMARY KEY (AccountingEntryID),
    CONSTRAINT FK_ACCOUNTING_ENTRY_Shop             FOREIGN KEY (ShopID)          REFERENCES SHOP (ShopID)
                                                    ON UPDATE NO ACTION ON DELETE NO ACTION,
    CONSTRAINT FK_ACCOUNTING_ENTRY_Invoice          FOREIGN KEY (SourceInvoiceID) REFERENCES INVOICES (InvoiceID)
                                                    ON UPDATE NO ACTION ON DELETE NO ACTION,
    CONSTRAINT FK_ACCOUNTING_ENTRY_Payment          FOREIGN KEY (SourcePaymentID) REFERENCES PAYMENTS (PaymentID)
                                                    ON UPDATE NO ACTION ON DELETE NO ACTION
);
GO


-- ============================================================================
-- W. XERO_SYNC_LOG
-- ============================================================================
CREATE TABLE XERO_SYNC_LOG
(
    XeroSyncLogID      BIGINT        IDENTITY(1,1) NOT NULL,
    ShopID             BIGINT        NOT NULL,                  -- FK → SHOP
    SyncedByUserID     BIGINT        NULL,                      -- FK → USERS (nullable)
    SyncType           NVARCHAR(30)  NOT NULL,
    InvoiceID          BIGINT        NULL,                      -- FK → INVOICES (nullable)
    PaymentID          BIGINT        NULL,                      -- FK → PAYMENTS (nullable)
    AccountingEntryID  BIGINT        NULL,                      -- FK → ACCOUNTING_ENTRY (nullable)
    XeroRecordID       NVARCHAR(80)  NULL,
    [Status]           NVARCHAR(20)  NOT NULL  DEFAULT 'Pending',
    [Message]          NVARCHAR(255) NULL,
    SyncedAt           DATETIME2(0)  NOT NULL  DEFAULT SYSDATETIME(),

    CONSTRAINT PK_XERO_SYNC_LOG                       PRIMARY KEY (XeroSyncLogID),
    CONSTRAINT FK_XERO_SYNC_LOG_Shop                  FOREIGN KEY (ShopID)            REFERENCES SHOP (ShopID)
                                                      ON UPDATE NO ACTION ON DELETE NO ACTION,
    CONSTRAINT FK_XERO_SYNC_LOG_SyncedBy              FOREIGN KEY (SyncedByUserID)    REFERENCES USERS (UserID)
                                                      ON UPDATE NO ACTION ON DELETE NO ACTION,
    CONSTRAINT FK_XERO_SYNC_LOG_Invoice               FOREIGN KEY (InvoiceID)         REFERENCES INVOICES (InvoiceID)
                                                      ON UPDATE NO ACTION ON DELETE NO ACTION,
    CONSTRAINT FK_XERO_SYNC_LOG_Payment               FOREIGN KEY (PaymentID)         REFERENCES PAYMENTS (PaymentID)
                                                      ON UPDATE NO ACTION ON DELETE NO ACTION,
    CONSTRAINT FK_XERO_SYNC_LOG_AccountingEntry       FOREIGN KEY (AccountingEntryID) REFERENCES ACCOUNTING_ENTRY (AccountingEntryID)
                                                      ON UPDATE NO ACTION ON DELETE NO ACTION
);
GO


-- ============================================================================
-- INDEXES ON FK COLUMNS
-- ============================================================================

-- USERS
CREATE NONCLUSTERED INDEX IX_USERS_ShopID
    ON USERS (ShopID);
GO

-- USER_ROLES
CREATE NONCLUSTERED INDEX IX_USER_ROLES_UserID
    ON USER_ROLES (UserID);
GO
CREATE NONCLUSTERED INDEX IX_USER_ROLES_RoleID
    ON USER_ROLES (RoleID);
GO

-- CUSTOMERS
CREATE NONCLUSTERED INDEX IX_CUSTOMERS_ShopID
    ON CUSTOMERS (ShopID);
GO

-- DEVICES
CREATE NONCLUSTERED INDEX IX_DEVICES_CustomerID
    ON DEVICES (CustomerID);
GO

-- SERVICE_CATEGORY
CREATE NONCLUSTERED INDEX IX_SERVICE_CATEGORY_ShopID
    ON SERVICE_CATEGORY (ShopID);
GO

-- SERVICE_CATALOG
CREATE NONCLUSTERED INDEX IX_SERVICE_CATALOG_ShopID
    ON SERVICE_CATALOG (ShopID);
GO
CREATE NONCLUSTERED INDEX IX_SERVICE_CATALOG_CategoryID
    ON SERVICE_CATALOG (ServiceCategoryID);
GO

-- INVENTORY_ITEMS
CREATE NONCLUSTERED INDEX IX_INVENTORY_ITEMS_ShopID
    ON INVENTORY_ITEMS (ShopID);
GO

-- INVENTORY_TXN
CREATE NONCLUSTERED INDEX IX_INVENTORY_TXN_ItemID
    ON INVENTORY_TXN (ItemID);
GO

-- JOB_ORDERS
CREATE NONCLUSTERED INDEX IX_JOB_ORDERS_ShopID
    ON JOB_ORDERS (ShopID);
GO
CREATE NONCLUSTERED INDEX IX_JOB_ORDERS_CustomerID
    ON JOB_ORDERS (CustomerID);
GO
CREATE NONCLUSTERED INDEX IX_JOB_ORDERS_DeviceID
    ON JOB_ORDERS (DeviceID);
GO
CREATE NONCLUSTERED INDEX IX_JOB_ORDERS_CreatedByUserID
    ON JOB_ORDERS (CreatedByUserID);
GO
CREATE NONCLUSTERED INDEX IX_JOB_ORDERS_AssignedTechUserID
    ON JOB_ORDERS (AssignedTechUserID);
GO

-- JOB_ORDER_SERVICES
CREATE NONCLUSTERED INDEX IX_JOB_ORDER_SERVICES_JobOrderID
    ON JOB_ORDER_SERVICES (JobOrderID);
GO
CREATE NONCLUSTERED INDEX IX_JOB_ORDER_SERVICES_ServiceID
    ON JOB_ORDER_SERVICES (ServiceID);
GO

-- JOB_ORDER_PARTS
CREATE NONCLUSTERED INDEX IX_JOB_ORDER_PARTS_JobOrderID
    ON JOB_ORDER_PARTS (JobOrderID);
GO
CREATE NONCLUSTERED INDEX IX_JOB_ORDER_PARTS_ItemID
    ON JOB_ORDER_PARTS (ItemID);
GO

-- JOB_ORDER_STATUS_HISTORY
CREATE NONCLUSTERED INDEX IX_JOB_ORDER_STATUS_HISTORY_JobOrderID
    ON JOB_ORDER_STATUS_HISTORY (JobOrderID);
GO
CREATE NONCLUSTERED INDEX IX_JOB_ORDER_STATUS_HISTORY_ChangedByUserID
    ON JOB_ORDER_STATUS_HISTORY (ChangedByUserID);
GO

-- INVOICES
CREATE NONCLUSTERED INDEX IX_INVOICES_ShopID
    ON INVOICES (ShopID);
GO
CREATE NONCLUSTERED INDEX IX_INVOICES_CustomerID
    ON INVOICES (CustomerID);
GO
-- JobOrderID already has a unique index via UQ_INVOICES_JobOrderID

-- INVOICE_LINES
CREATE NONCLUSTERED INDEX IX_INVOICE_LINES_InvoiceID
    ON INVOICE_LINES (InvoiceID);
GO

-- PAYMENTS
CREATE NONCLUSTERED INDEX IX_PAYMENTS_ShopID
    ON PAYMENTS (ShopID);
GO
CREATE NONCLUSTERED INDEX IX_PAYMENTS_CustomerID
    ON PAYMENTS (CustomerID);
GO
CREATE NONCLUSTERED INDEX IX_PAYMENTS_ReceivedByUserID
    ON PAYMENTS (ReceivedByUserID);
GO

-- PAYMENT_ALLOCATION
CREATE NONCLUSTERED INDEX IX_PAYMENT_ALLOCATION_PaymentID
    ON PAYMENT_ALLOCATION (PaymentID);
GO
CREATE NONCLUSTERED INDEX IX_PAYMENT_ALLOCATION_InvoiceID
    ON PAYMENT_ALLOCATION (InvoiceID);
GO

-- PAYMONGO_TXN
-- PaymentID already has a unique index via UQ_PAYMONGO_TXN_PaymentID

-- CREDIT_DEBIT_ADJUSTMENT
CREATE NONCLUSTERED INDEX IX_CREDIT_DEBIT_ADJUSTMENT_InvoiceID
    ON CREDIT_DEBIT_ADJUSTMENT (InvoiceID);
GO
CREATE NONCLUSTERED INDEX IX_CREDIT_DEBIT_ADJUSTMENT_CreatedByUserID
    ON CREDIT_DEBIT_ADJUSTMENT (CreatedByUserID);
GO

-- AUDIT_LOG
CREATE NONCLUSTERED INDEX IX_AUDIT_LOG_ShopID
    ON AUDIT_LOG (ShopID);
GO
CREATE NONCLUSTERED INDEX IX_AUDIT_LOG_UserID
    ON AUDIT_LOG (UserID);
GO
CREATE NONCLUSTERED INDEX IX_AUDIT_LOG_CreatedAt
    ON AUDIT_LOG (CreatedAt);
GO

-- ACCOUNTING_ENTRY
CREATE NONCLUSTERED INDEX IX_ACCOUNTING_ENTRY_ShopID
    ON ACCOUNTING_ENTRY (ShopID);
GO
CREATE NONCLUSTERED INDEX IX_ACCOUNTING_ENTRY_SourceInvoiceID
    ON ACCOUNTING_ENTRY (SourceInvoiceID);
GO
CREATE NONCLUSTERED INDEX IX_ACCOUNTING_ENTRY_SourcePaymentID
    ON ACCOUNTING_ENTRY (SourcePaymentID);
GO

-- XERO_SYNC_LOG
CREATE NONCLUSTERED INDEX IX_XERO_SYNC_LOG_ShopID
    ON XERO_SYNC_LOG (ShopID);
GO
CREATE NONCLUSTERED INDEX IX_XERO_SYNC_LOG_SyncedByUserID
    ON XERO_SYNC_LOG (SyncedByUserID);
GO
CREATE NONCLUSTERED INDEX IX_XERO_SYNC_LOG_InvoiceID
    ON XERO_SYNC_LOG (InvoiceID);
GO
CREATE NONCLUSTERED INDEX IX_XERO_SYNC_LOG_PaymentID
    ON XERO_SYNC_LOG (PaymentID);
GO
CREATE NONCLUSTERED INDEX IX_XERO_SYNC_LOG_AccountingEntryID
    ON XERO_SYNC_LOG (AccountingEntryID);
GO


-- ============================================================================
-- SEED DATA
-- ============================================================================

-- Seed: Default Shop
INSERT INTO SHOP (ShopCode, ShopName, Email, Phone, [Address])
VALUES ('MAIN', 'ByteBill Main Shop', 'admin@bytebill.com', '+63-000-000-0000', 'Metro Manila, Philippines');
GO

-- Seed: Roles (matching the application enum)
SET IDENTITY_INSERT ROLES ON;
GO

INSERT INTO ROLES (RoleID, RoleName, [Description])
VALUES
    (1, 'SuperAdmin',  'Full system access across all shops'),
    (2, 'Admin',       'Shop Owner — full access within a single shop'),
    (3, 'Billing',     'Billing staff — invoices, payments, and customer management'),
    (4, 'Technician',  'Technician — job orders, diagnostics, and repairs'),
    (5, 'Auditor',     'Auditor — read-only access for review and compliance');
GO

SET IDENTITY_INSERT ROLES OFF;
GO


-- Seed: Demo Users (password: Password123!)
-- BCrypt hash of 'Password123!' using cost factor 12
DECLARE @PwdHash NVARCHAR(255) = '$2a$12$LJ3m4ys3Lk0TSwHleDJruOEGCxCXOGyGqoLNaHPbBMp7c8.hgy7G6';

SET IDENTITY_INSERT USERS ON;
GO

INSERT INTO USERS (UserID, ShopID, FirstName, MiddleName, LastName, UserName, PasswordHash, IsActive)
VALUES
    (1, 1, 'Super',   NULL, 'Admin',   'superadmin', @PwdHash, 1),
    (2, 1, 'Shop',    NULL, 'Owner',   'admin',      @PwdHash, 1),
    (3, 1, 'Billing', NULL, 'Staff',   'billing',    @PwdHash, 1),
    (4, 1, 'Tech',    NULL, 'Support', 'tech',       @PwdHash, 1),
    (5, 1, 'External',NULL, 'Auditor', 'auditor',    @PwdHash, 1);
GO

SET IDENTITY_INSERT USERS OFF;
GO


-- Seed: User-Role Assignments (one role per demo user)
SET IDENTITY_INSERT USER_ROLES ON;
GO

INSERT INTO USER_ROLES (UserRoleID, UserID, RoleID)
VALUES
    (1, 1, 1),   -- superadmin  → SuperAdmin
    (2, 2, 2),   -- admin       → Admin
    (3, 3, 3),   -- billing     → Billing
    (4, 4, 4),   -- tech        → Technician
    (5, 5, 5);   -- auditor     → Auditor
GO

SET IDENTITY_INSERT USER_ROLES OFF;
GO


PRINT '=== ByteBillDB schema created successfully ===';
GO
