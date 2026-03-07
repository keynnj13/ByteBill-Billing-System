-- Migration: Add WriteOff fields to InventoryItems
-- Date: 2025-02-19

-- Add WriteOffReason and WriteOffNotes columns to InventoryItems
IF NOT EXISTS (SELECT 1 FROM sys.columns WHERE object_id = OBJECT_ID('InventoryItems') AND name = 'WriteOffReason')
BEGIN
    ALTER TABLE [InventoryItems] ADD [WriteOffReason] NVARCHAR(50) NULL;
END
GO

IF NOT EXISTS (SELECT 1 FROM sys.columns WHERE object_id = OBJECT_ID('InventoryItems') AND name = 'WriteOffNotes')
BEGIN
    ALTER TABLE [InventoryItems] ADD [WriteOffNotes] NVARCHAR(500) NULL;
END
GO
