-- Add Profile & Audit fields (idempotent)
IF NOT EXISTS (SELECT 1 FROM sys.columns WHERE object_id = OBJECT_ID('USERS') AND name = 'Email')
    ALTER TABLE USERS ADD Email NVARCHAR(150) NULL;

IF NOT EXISTS (SELECT 1 FROM sys.columns WHERE object_id = OBJECT_ID('USERS') AND name = 'Phone')
    ALTER TABLE USERS ADD Phone NVARCHAR(20) NULL;

IF NOT EXISTS (SELECT 1 FROM sys.columns WHERE object_id = OBJECT_ID('USERS') AND name = 'ThemePreference')
    ALTER TABLE USERS ADD ThemePreference NVARCHAR(10) NOT NULL DEFAULT 'light';

IF NOT EXISTS (SELECT 1 FROM sys.columns WHERE object_id = OBJECT_ID('USERS') AND name = 'EmailNotifications')
    ALTER TABLE USERS ADD EmailNotifications BIT NOT NULL DEFAULT 1;

IF NOT EXISTS (SELECT 1 FROM sys.columns WHERE object_id = OBJECT_ID('USERS') AND name = 'InAppNotifications')
    ALTER TABLE USERS ADD InAppNotifications BIT NOT NULL DEFAULT 1;

IF NOT EXISTS (SELECT 1 FROM sys.columns WHERE object_id = OBJECT_ID('AUDIT_LOG') AND name = 'IpAddress')
    ALTER TABLE AUDIT_LOG ADD IpAddress NVARCHAR(45) NULL;

IF NOT EXISTS (SELECT 1 FROM sys.columns WHERE object_id = OBJECT_ID('AUDIT_LOG') AND name = 'OldValues')
    ALTER TABLE AUDIT_LOG ADD OldValues NVARCHAR(2000) NULL;

IF NOT EXISTS (SELECT 1 FROM sys.columns WHERE object_id = OBJECT_ID('AUDIT_LOG') AND name = 'NewValues')
    ALTER TABLE AUDIT_LOG ADD NewValues NVARCHAR(2000) NULL;

PRINT 'Done - all columns verified/added';
