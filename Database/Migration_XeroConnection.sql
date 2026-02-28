-- ═══════════════════════════════════════════════════════════════════════
-- Migration: Xero Connection Table
-- Run after: ByteBillDB_MonsterASP_Deploy.sql / existing migrations
-- ═══════════════════════════════════════════════════════════════════════

IF NOT EXISTS (
    SELECT 1 FROM INFORMATION_SCHEMA.TABLES
    WHERE TABLE_NAME = 'XERO_CONNECTION'
)
BEGIN
    CREATE TABLE [XERO_CONNECTION] (
        [XeroConnectionID]  BIGINT          IDENTITY(1,1) NOT NULL,
        [ShopID]            BIGINT          NOT NULL,
        [XeroTenantId]      NVARCHAR(80)    NOT NULL,
        [TenantName]        NVARCHAR(150)   NULL,
        [AccessToken]       NVARCHAR(2048)  NOT NULL,
        [RefreshToken]      NVARCHAR(2048)  NOT NULL,
        [TokenExpiresAt]    DATETIME2(0)    NOT NULL,
        [ConnectedAt]       DATETIME2(0)    NOT NULL DEFAULT SYSDATETIME(),
        [IsActive]          BIT             NOT NULL DEFAULT 1,

        CONSTRAINT [PK_XERO_CONNECTION] PRIMARY KEY ([XeroConnectionID]),
        CONSTRAINT [FK_XERO_CONNECTION_SHOP] FOREIGN KEY ([ShopID])
            REFERENCES [SHOP]([ShopID]) ON DELETE NO ACTION
    );

    CREATE NONCLUSTERED INDEX [IX_XERO_CONNECTION_ShopID]
        ON [XERO_CONNECTION]([ShopID]);

    PRINT 'Created XERO_CONNECTION table';
END;
GO

PRINT '✅ Xero Connection migration complete.';
GO
