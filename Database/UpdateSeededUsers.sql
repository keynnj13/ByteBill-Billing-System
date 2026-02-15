-- ═══════════════════════════════════════════════════════════════════════════
-- Update Seeded Users with Real Names
-- Run this in SSMS to update existing user records
-- 
-- OPTION 1: Run this script (updates names only, passwords remain the same)
-- OPTION 2: For new passwords, delete users and restart the app (recommended)
-- ═══════════════════════════════════════════════════════════════════════════

USE ByteBillDB;
GO

PRINT '═══════════════════════════════════════════════════════════════';
PRINT 'Updating Seeded Users with Real Names';
PRINT '═══════════════════════════════════════════════════════════════';
PRINT '';

-- Update SuperAdmin (vkpadao)
UPDATE USERS
SET 
    FirstName = 'Vaness',
    LastName = 'Padao'
WHERE UserName = 'vkpadao';
PRINT '✓ Updated vkpadao → Vaness Padao';

-- Update Admin
UPDATE USERS
SET 
    FirstName = 'Maria',
    LastName = 'Santos'
WHERE UserName = 'admin';
PRINT '✓ Updated admin → Maria Santos';

-- Update Billing
UPDATE USERS
SET 
    FirstName = 'Juan',
    LastName = 'Cruz'
WHERE UserName = 'billing';
PRINT '✓ Updated billing → Juan Cruz';

-- Update Technician
UPDATE USERS
SET 
    FirstName = 'Carlos',
    LastName = 'Reyes'
WHERE UserName = 'technician';
PRINT '✓ Updated technician → Carlos Reyes';

-- Update Auditor
UPDATE USERS
SET 
    FirstName = 'Ana',
    LastName = 'Garcia'
WHERE UserName = 'auditor';
PRINT '✓ Updated auditor → Ana Garcia';

PRINT '';
PRINT '═══════════════════════════════════════════════════════════════';
PRINT 'Users updated successfully!';
PRINT '═══════════════════════════════════════════════════════════════';
PRINT '';

-- Verify the updates
SELECT 
    UserID,
    FirstName + ' ' + LastName AS FullName,
    UserName,
    IsActive,
    CreatedAt
FROM USERS
ORDER BY UserID;

PRINT '';
PRINT 'Current User Credentials (passwords unchanged):';
PRINT '┌──────────────┬───────────────────┬────────────────────┐';
PRINT '│ Username      │ Full Name         │ Password           │';
PRINT '├──────────────┼───────────────────┼────────────────────┤';
PRINT '│ vkpadao       │ Vince Kyle Padao  │ Superadmin123!     │';
PRINT '│ admin         │ Maria Santos      │ Admin123!          │';
PRINT '│ billing       │ Juan Cruz         │ Billing123!        │';
PRINT '│ technician    │ Carlos Reyes      │ Technician123!     │';
PRINT '│ auditor       │ Ana Garcia        │ Auditor123!        │';
PRINT '└──────────────┴───────────────────┴────────────────────┘';
PRINT '';
PRINT 'NOTE: To update passwords with new hashes, run the next script.';
GO
