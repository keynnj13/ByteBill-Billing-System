-- ============================================================================
--  MIGRATION: Widen PAYMONGO_TXN.RawResponse to NVARCHAR(MAX)
--  
--  PayMongo checkout session responses can exceed 2000 characters.
--  Run this against your database to fix the truncation error.
-- ============================================================================

IF EXISTS (
    SELECT 1 FROM INFORMATION_SCHEMA.COLUMNS
    WHERE TABLE_NAME = 'PAYMONGO_TXN'
      AND COLUMN_NAME = 'RawResponse'
      AND CHARACTER_MAXIMUM_LENGTH <> -1  -- -1 means MAX
)
BEGIN
    ALTER TABLE [dbo].[PAYMONGO_TXN]
        ALTER COLUMN [RawResponse] NVARCHAR(MAX) NULL;
    PRINT '✅  Widened RawResponse column to NVARCHAR(MAX)';
END
ELSE
    PRINT '⏭️  RawResponse is already NVARCHAR(MAX) or column does not exist';
