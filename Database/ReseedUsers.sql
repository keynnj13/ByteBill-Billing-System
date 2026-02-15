-- ═══════════════════════════════════════════════════════════════════════════
-- RESEED Users Table (DELETE + RESTART APP)
-- This script clears existing users so the DbSeeder can recreate them
-- with the new names and fresh password hashes.
-- 
-- ⚠️  WARNING: This will delete all users and their role assignments!
-- ═══════════════════════════════════════════════════════════════════════════

USE ByteBillDB;
GO

PRINT '═══════════════════════════════════════════════════════════════';
PRINT '⚠️  RESEED Users - This will DELETE all existing users!';
PRINT '═══════════════════════════════════════════════════════════════';
PRINT '';

-- Show current users before deletion
PRINT 'Current users:';
SELECT UserID, FirstName + ' ' + LastName AS FullName, UserName FROM USERS;
PRINT '';

-- Confirm before proceeding
PRINT 'Press Ctrl+C to CANCEL, or press F5 to CONTINUE...';
WAITFOR DELAY '00:00:03';
PRINT '';

-- Delete user role assignments first (FK constraint)
DELETE FROM USER_ROLES;
PRINT '✓ Deleted all user-role assignments';

-- Delete users
DELETE FROM USERS;
PRINT '✓ Deleted all users';

-- Reset identity seed
DBCC CHECKIDENT ('USERS', RESEED, 0);
PRINT '✓ Reset UserID identity seed';

PRINT '';
PRINT '═══════════════════════════════════════════════════════════════';
PRINT '✅ Users table cleared successfully!';
PRINT '═══════════════════════════════════════════════════════════════';
PRINT '';
PRINT 'NEXT STEPS:';
PRINT '1. Stop your application if it''s running';
PRINT '2. Start the application: dotnet run';
PRINT '3. DbSeeder will automatically create users with new names:';
PRINT '';
PRINT '   ┌──────────────┬───────────────────┬────────────────────┐';
PRINT '   │ Username      │ Full Name         │ Password           │';
PRINT '   ├──────────────┼───────────────────┼────────────────────┤';
PRINT '   │ vkpadao       │ Vince Kyle Padao  │ Superadmin123!     │';
PRINT '   │ admin         │ Maria Santos      │ Admin123!          │';
PRINT '   │ billing       │ Juan Cruz         │ Billing123!        │';
PRINT '   │ technician    │ Carlos Reyes      │ Technician123!     │';
PRINT '   │ auditor       │ Ana Garcia        │ Auditor123!        │';
PRINT '   └──────────────┴───────────────────┴────────────────────┘';
PRINT '';
GO
