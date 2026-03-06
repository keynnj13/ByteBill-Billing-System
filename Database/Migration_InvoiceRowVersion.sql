-- Add RowVersion column to INVOICE table for optimistic concurrency
-- This is a rowversion (timestamp) column managed automatically by SQL Server

IF NOT EXISTS (
    SELECT 1 FROM sys.columns 
    WHERE object_id = OBJECT_ID(N'INVOICES') AND name = 'RowVersion'
)
BEGIN
    ALTER TABLE [INVOICES] ADD [RowVersion] rowversion NOT NULL;
    PRINT 'Added RowVersion column to INVOICE table.';
END
ELSE
BEGIN
    PRINT 'RowVersion column already exists on INVOICE table.';
END
