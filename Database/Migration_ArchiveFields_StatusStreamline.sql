/*******************************************************************************
 *  ByteBill: Web-Based Billing System for Computer Repair Services
 *  MONSTERASP MIGRATION SCRIPT — Archive Fields & Status Streamline
 *
 *  Generated: 2026-02-18
 *
 *  This migration:
 *    1. Adds IsArchived + ArchivedDate columns to JOB_ORDERS
 *    2. Adds IsArchived + ArchivedDate columns to INVOICES
 *    3. Updates JOB_ORDERS default Status from 'Created' to 'Pending'
 *    4. Normalises any legacy status values to the new 8-status set
 *
 *  New JO Statuses (8 total):
 *    Pending, CheckedIn, Diagnosis, InProgress, WaitingForParts,
 *    Completed, Delivered, Cancelled
 *
 *  Removed JO Statuses (mapped → new):
 *    Created        → Pending
 *    Diagnosed      → InProgress
 *    AwaitingApproval → Pending
 *    Approved       → InProgress
 *    OnHold         → WaitingForParts
 *    ReadyForPickup → Completed
 *
 *  ⚠️  Run this ONCE on an existing MonsterASP database that already has
 *     the base schema deployed (ByteBillDB_MonsterASP_Deploy.sql).
 ******************************************************************************/

SET XACT_ABORT ON;
BEGIN TRANSACTION;

-- ════════════════════════════════════════════════════════════════════════════
-- 1.  JOB_ORDERS — Add archive columns
-- ════════════════════════════════════════════════════════════════════════════
IF NOT EXISTS (
    SELECT 1 FROM INFORMATION_SCHEMA.COLUMNS
    WHERE TABLE_NAME = 'JOB_ORDERS' AND COLUMN_NAME = 'IsArchived'
)
BEGIN
    ALTER TABLE JOB_ORDERS
        ADD IsArchived   BIT          NOT NULL  DEFAULT 0,
            ArchivedDate DATETIME2(0) NULL;
    PRINT '✓ Added IsArchived + ArchivedDate to JOB_ORDERS';
END
ELSE
    PRINT '— JOB_ORDERS archive columns already exist, skipping';
GO

-- ════════════════════════════════════════════════════════════════════════════
-- 2.  INVOICES — Add archive columns
-- ════════════════════════════════════════════════════════════════════════════
IF NOT EXISTS (
    SELECT 1 FROM INFORMATION_SCHEMA.COLUMNS
    WHERE TABLE_NAME = 'INVOICES' AND COLUMN_NAME = 'IsArchived'
)
BEGIN
    ALTER TABLE INVOICES
        ADD IsArchived   BIT          NOT NULL  DEFAULT 0,
            ArchivedDate DATETIME2(0) NULL;
    PRINT '✓ Added IsArchived + ArchivedDate to INVOICES';
END
ELSE
    PRINT '— INVOICES archive columns already exist, skipping';
GO

-- ════════════════════════════════════════════════════════════════════════════
-- 3.  JOB_ORDERS — Change default Status to 'Pending'
-- ════════════════════════════════════════════════════════════════════════════
-- Drop the old default constraint (name may vary)
DECLARE @DefaultName NVARCHAR(256);
SELECT @DefaultName = dc.name
FROM   sys.default_constraints dc
JOIN   sys.columns c ON dc.parent_object_id = c.object_id AND dc.parent_column_id = c.column_id
WHERE  OBJECT_NAME(dc.parent_object_id) = 'JOB_ORDERS'
  AND  c.name = 'Status';

IF @DefaultName IS NOT NULL
BEGIN
    EXEC('ALTER TABLE JOB_ORDERS DROP CONSTRAINT [' + @DefaultName + ']');
    ALTER TABLE JOB_ORDERS ADD DEFAULT 'Pending' FOR [Status];
    PRINT '✓ Changed JOB_ORDERS Status default from Created → Pending';
END
GO

-- ════════════════════════════════════════════════════════════════════════════
-- 4.  Normalise legacy status values
-- ════════════════════════════════════════════════════════════════════════════
UPDATE JOB_ORDERS SET [Status] = 'Pending'          WHERE [Status] = 'Created';
UPDATE JOB_ORDERS SET [Status] = 'InProgress'       WHERE [Status] = 'Diagnosed';
UPDATE JOB_ORDERS SET [Status] = 'Pending'          WHERE [Status] = 'AwaitingApproval';
UPDATE JOB_ORDERS SET [Status] = 'InProgress'       WHERE [Status] = 'Approved';
UPDATE JOB_ORDERS SET [Status] = 'WaitingForParts'  WHERE [Status] = 'OnHold';
UPDATE JOB_ORDERS SET [Status] = 'Completed'        WHERE [Status] = 'ReadyForPickup';
PRINT '✓ Normalised legacy JO statuses to 8-value set';
GO

-- Also normalise any history records
UPDATE JOB_ORDER_STATUS_HISTORY SET OldStatus = 'Pending'          WHERE OldStatus = 'Created';
UPDATE JOB_ORDER_STATUS_HISTORY SET NewStatus = 'Pending'          WHERE NewStatus = 'Created';
UPDATE JOB_ORDER_STATUS_HISTORY SET OldStatus = 'InProgress'       WHERE OldStatus IN ('Diagnosed', 'Approved');
UPDATE JOB_ORDER_STATUS_HISTORY SET NewStatus = 'InProgress'       WHERE NewStatus IN ('Diagnosed', 'Approved');
UPDATE JOB_ORDER_STATUS_HISTORY SET OldStatus = 'Pending'          WHERE OldStatus = 'AwaitingApproval';
UPDATE JOB_ORDER_STATUS_HISTORY SET NewStatus = 'Pending'          WHERE NewStatus = 'AwaitingApproval';
UPDATE JOB_ORDER_STATUS_HISTORY SET OldStatus = 'WaitingForParts'  WHERE OldStatus = 'OnHold';
UPDATE JOB_ORDER_STATUS_HISTORY SET NewStatus = 'WaitingForParts'  WHERE NewStatus = 'OnHold';
UPDATE JOB_ORDER_STATUS_HISTORY SET OldStatus = 'Completed'        WHERE OldStatus = 'ReadyForPickup';
UPDATE JOB_ORDER_STATUS_HISTORY SET NewStatus = 'Completed'        WHERE NewStatus = 'ReadyForPickup';
PRINT '✓ Normalised JOB_ORDER_STATUS_HISTORY legacy values';
GO

COMMIT TRANSACTION;
PRINT '';
PRINT '══════════════════════════════════════════════════════════════';
PRINT '  Migration complete — Archive fields + Status streamline';
PRINT '══════════════════════════════════════════════════════════════';
GO
