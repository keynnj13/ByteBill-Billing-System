-- =============================================
-- Migration: Add IsArchived to Category tables
-- =============================================

-- SERVICE_CATEGORY
IF NOT EXISTS (
    SELECT 1 FROM INFORMATION_SCHEMA.COLUMNS
    WHERE TABLE_NAME = 'SERVICE_CATEGORY' AND COLUMN_NAME = 'IsArchived'
)
BEGIN
    ALTER TABLE SERVICE_CATEGORY ADD IsArchived BIT NOT NULL DEFAULT 0;
    PRINT '✅ Added IsArchived to SERVICE_CATEGORY';
END
ELSE
    PRINT '⏭️  SERVICE_CATEGORY.IsArchived already exists';

-- INVENTORY_CATEGORY
IF NOT EXISTS (
    SELECT 1 FROM INFORMATION_SCHEMA.COLUMNS
    WHERE TABLE_NAME = 'INVENTORY_CATEGORY' AND COLUMN_NAME = 'IsArchived'
)
BEGIN
    ALTER TABLE INVENTORY_CATEGORY ADD IsArchived BIT NOT NULL DEFAULT 0;
    PRINT '✅ Added IsArchived to INVENTORY_CATEGORY';
END
ELSE
    PRINT '⏭️  INVENTORY_CATEGORY.IsArchived already exists';

PRINT '✅ Migration_CategoryArchive complete';
