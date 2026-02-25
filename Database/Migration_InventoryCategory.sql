-- =====================================================================
--  Migration: Add INVENTORY_CATEGORY table + FK on INVENTORY_ITEMS
--  Date: 2026-02-25
-- =====================================================================

-- 1. Create INVENTORY_CATEGORY table if not exists
IF NOT EXISTS (SELECT 1 FROM INFORMATION_SCHEMA.TABLES WHERE TABLE_NAME = 'INVENTORY_CATEGORY')
BEGIN
    CREATE TABLE [dbo].[INVENTORY_CATEGORY] (
        [InventoryCategoryID] BIGINT IDENTITY(1,1) NOT NULL,
        [ShopID]              BIGINT NOT NULL,
        [CategoryName]        NVARCHAR(80) NOT NULL,
        [Description]         NVARCHAR(150) NULL,
        CONSTRAINT [PK_INVENTORY_CATEGORY] PRIMARY KEY CLUSTERED ([InventoryCategoryID]),
        CONSTRAINT [FK_INVENTORY_CATEGORY_SHOP] FOREIGN KEY ([ShopID])
            REFERENCES [dbo].[SHOP]([ShopID]) ON DELETE NO ACTION
    );

    CREATE UNIQUE INDEX [IX_INVENTORY_CATEGORY_ShopID_CategoryName]
        ON [dbo].[INVENTORY_CATEGORY] ([ShopID], [CategoryName]);

    CREATE INDEX [IX_INVENTORY_CATEGORY_ShopID]
        ON [dbo].[INVENTORY_CATEGORY] ([ShopID]);

    PRINT 'Created INVENTORY_CATEGORY table.';
END
ELSE
BEGIN
    PRINT 'INVENTORY_CATEGORY table already exists.';
END
GO

-- 2. Add InventoryCategoryID column to INVENTORY_ITEMS if not exists
IF NOT EXISTS (
    SELECT 1 FROM INFORMATION_SCHEMA.COLUMNS
    WHERE TABLE_NAME = 'INVENTORY_ITEMS' AND COLUMN_NAME = 'InventoryCategoryID'
)
BEGIN
    ALTER TABLE [dbo].[INVENTORY_ITEMS]
        ADD [InventoryCategoryID] BIGINT NULL;

    ALTER TABLE [dbo].[INVENTORY_ITEMS]
        ADD CONSTRAINT [FK_INVENTORY_ITEMS_CATEGORY] FOREIGN KEY ([InventoryCategoryID])
            REFERENCES [dbo].[INVENTORY_CATEGORY]([InventoryCategoryID]) ON DELETE NO ACTION;

    PRINT 'Added InventoryCategoryID column to INVENTORY_ITEMS.';
END
ELSE
BEGIN
    PRINT 'InventoryCategoryID column already exists on INVENTORY_ITEMS.';
END
GO
