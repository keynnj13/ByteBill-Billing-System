-- ═══════════════════════════════════════════════════════════════════════
-- Migration: Add Description & EstimatedDuration to SERVICE_CATALOG,
--            Seed INVENTORY_CATEGORY records,
--            Assign InventoryCategoryID to existing INVENTORY_ITEM rows
-- Date: 2026-02-19
-- Run against: ByteBill database (SQL Server)
-- ═══════════════════════════════════════════════════════════════════════

-- ── 1. Add Description column to SERVICE_CATALOG ────────────────────
IF NOT EXISTS (
    SELECT 1 FROM INFORMATION_SCHEMA.COLUMNS
    WHERE TABLE_NAME = 'SERVICE_CATALOG' AND COLUMN_NAME = 'Description'
)
BEGIN
    ALTER TABLE SERVICE_CATALOG ADD [Description] NVARCHAR(500) NULL;
    PRINT '✅ Added Description column to SERVICE_CATALOG';
END
ELSE
    PRINT '⏭️  Description column already exists on SERVICE_CATALOG';
GO

-- ── 2. Add EstimatedDuration column to SERVICE_CATALOG ──────────────
IF NOT EXISTS (
    SELECT 1 FROM INFORMATION_SCHEMA.COLUMNS
    WHERE TABLE_NAME = 'SERVICE_CATALOG' AND COLUMN_NAME = 'EstimatedDuration'
)
BEGIN
    ALTER TABLE SERVICE_CATALOG ADD [EstimatedDuration] INT NOT NULL DEFAULT 0;
    PRINT '✅ Added EstimatedDuration column to SERVICE_CATALOG';
END
ELSE
    PRINT '⏭️  EstimatedDuration column already exists on SERVICE_CATALOG';
GO

-- ── 3. Update existing services with descriptions & durations ───────
UPDATE SERVICE_CATALOG SET [Description] = 'Full hardware and software diagnostic scan to identify issues',            EstimatedDuration = 30  WHERE ServiceName = 'System Diagnosis'            AND [Description] IS NULL;
UPDATE SERVICE_CATALOG SET [Description] = 'Physical inspection of all internal components for damage or wear',        EstimatedDuration = 20  WHERE ServiceName = 'Hardware Inspection'        AND [Description] IS NULL;
UPDATE SERVICE_CATALOG SET [Description] = 'Deep scan, removal of viruses and malware, security patching',             EstimatedDuration = 60  WHERE ServiceName = 'Virus/Malware Removal'      AND [Description] IS NULL;
UPDATE SERVICE_CATALOG SET [Description] = 'Full LCD/LED screen replacement including calibration',                    EstimatedDuration = 90  WHERE ServiceName = 'Screen Replacement'         AND [Description] IS NULL;
UPDATE SERVICE_CATALOG SET [Description] = 'Battery removal, replacement, and charge cycle testing',                   EstimatedDuration = 45  WHERE ServiceName = 'Battery Replacement'        AND [Description] IS NULL;
UPDATE SERVICE_CATALOG SET [Description] = 'Full keyboard unit replacement and key mapping verification',              EstimatedDuration = 60  WHERE ServiceName = 'Keyboard Replacement'       AND [Description] IS NULL;
UPDATE SERVICE_CATALOG SET [Description] = 'Clean install of Windows/macOS/Linux with driver setup',                   EstimatedDuration = 120 WHERE ServiceName = 'OS Installation'            AND [Description] IS NULL;
UPDATE SERVICE_CATALOG SET [Description] = 'Install and configure essential software, office suites, and antivirus',   EstimatedDuration = 45  WHERE ServiceName = 'Software Setup & Config'    AND [Description] IS NULL;
UPDATE SERVICE_CATALOG SET [Description] = 'Install new RAM modules or SSD with data migration if needed',             EstimatedDuration = 40  WHERE ServiceName = 'RAM/SSD Upgrade'            AND [Description] IS NULL;
UPDATE SERVICE_CATALOG SET [Description] = 'Deep clean internals, replace thermal paste, clean fans and vents',        EstimatedDuration = 60  WHERE ServiceName = 'Internal Cleaning & Repaste'AND [Description] IS NULL;
UPDATE SERVICE_CATALOG SET [Description] = 'Disk cleanup, startup optimization, registry repair, and updates',         EstimatedDuration = 45  WHERE ServiceName = 'Full System Tune-Up'        AND [Description] IS NULL;
UPDATE SERVICE_CATALOG SET [Description] = 'Recover data from damaged or corrupted drives using professional tools',   EstimatedDuration = 180 WHERE ServiceName = 'Data Recovery (HDD/SSD)'    AND [Description] IS NULL;

PRINT '✅ Updated service descriptions and durations';
GO

-- ── 4. Seed INVENTORY_CATEGORY if empty ─────────────────────────────
IF NOT EXISTS (SELECT 1 FROM INVENTORY_CATEGORY)
BEGIN
    DECLARE @ShopId BIGINT = (SELECT TOP 1 ShopID FROM SHOP);

    INSERT INTO INVENTORY_CATEGORY (ShopID, CategoryName, [Description])
    VALUES
        (@ShopId, 'Storage',      'SSDs, HDDs, and flash drives'),
        (@ShopId, 'Memory',       'RAM modules and memory kits'),
        (@ShopId, 'Cooling',      'Fans, thermal paste, and heatsinks'),
        (@ShopId, 'Cables',       'HDMI, USB, SATA, and other cables'),
        (@ShopId, 'Power Supply', 'PSU units and power accessories'),
        (@ShopId, 'Peripherals',  'Keyboards, mice, and other peripherals');

    PRINT '✅ Seeded 6 inventory categories';
END
ELSE
    PRINT '⏭️  INVENTORY_CATEGORY already has data';
GO

-- ── 5. Assign categories to existing inventory items ────────────────
DECLARE @ShopId2 BIGINT = (SELECT TOP 1 ShopID FROM SHOP);

-- Storage
UPDATE INVENTORY_ITEMS SET InventoryCategoryID = (SELECT InventoryCategoryID FROM INVENTORY_CATEGORY WHERE CategoryName = 'Storage' AND ShopID = @ShopId2)
WHERE SKU IN ('SSD-500-SAM','SSD-256-KNG','HDD-1TB-WD') AND InventoryCategoryID IS NULL;

-- Memory
UPDATE INVENTORY_ITEMS SET InventoryCategoryID = (SELECT InventoryCategoryID FROM INVENTORY_CATEGORY WHERE CategoryName = 'Memory' AND ShopID = @ShopId2)
WHERE SKU IN ('RAM-8-COR','RAM-16-COR') AND InventoryCategoryID IS NULL;

-- Cooling
UPDATE INVENTORY_ITEMS SET InventoryCategoryID = (SELECT InventoryCategoryID FROM INVENTORY_CATEGORY WHERE CategoryName = 'Cooling' AND ShopID = @ShopId2)
WHERE SKU IN ('PST-THRM-NT','FAN-120-DPC') AND InventoryCategoryID IS NULL;

-- Cables
UPDATE INVENTORY_ITEMS SET InventoryCategoryID = (SELECT InventoryCategoryID FROM INVENTORY_CATEGORY WHERE CategoryName = 'Cables' AND ShopID = @ShopId2)
WHERE SKU IN ('CBL-HDMI-2M','CBL-USBC-1M') AND InventoryCategoryID IS NULL;

-- Power Supply
UPDATE INVENTORY_ITEMS SET InventoryCategoryID = (SELECT InventoryCategoryID FROM INVENTORY_CATEGORY WHERE CategoryName = 'Power Supply' AND ShopID = @ShopId2)
WHERE SKU IN ('PSU-550-EVG') AND InventoryCategoryID IS NULL;

-- Peripherals
UPDATE INVENTORY_ITEMS SET InventoryCategoryID = (SELECT InventoryCategoryID FROM INVENTORY_CATEGORY WHERE CategoryName = 'Peripherals' AND ShopID = @ShopId2)
WHERE SKU IN ('KBD-LOGI-K120','MOU-LOGI-B100') AND InventoryCategoryID IS NULL;

PRINT '✅ Assigned inventory categories to existing items';
GO

PRINT '';
PRINT '═══════════════════════════════════════════════════════════════';
PRINT '  Migration complete — ServiceCatalog + InventoryCategory';
PRINT '═══════════════════════════════════════════════════════════════';
