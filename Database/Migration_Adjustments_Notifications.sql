-- =====================================================
-- Migration: Add Adjustment + Notification new columns
-- Run ONCE on the production database
-- =====================================================
USE ByteBillDB;
GO

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
