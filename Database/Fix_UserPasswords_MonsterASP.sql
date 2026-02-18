/*******************************************************************************
 *  ByteBill: FIX USER PASSWORDS ON MONSTERASP
 *  
 *  Run this on your MonsterASP SQL Query Tool to update all user passwords
 *  to match the application's expected credentials.
 *
 *  The original deployment script used a single hash for 'Password123!' 
 *  but the app's DbSeeder uses unique passwords per user.
 *
 *  ⚠️  Replace "ByteBillDB" with your MonsterASP database name!
 ******************************************************************************/

-- USE ByteBillDB;  -- ← CHANGE THIS to your MonsterASP database name!
-- GO

-- Update each user with their correct unique password hash (BCrypt cost 12)

-- vkpadao (SuperAdmin): Superadmin123!
UPDATE USERS 
SET PasswordHash = '$2a$12$RMVVCzlpcg7ckzii6W9aG.dJMpjq57OjNM3S3kdNGXzJaQCtciHA.'
WHERE UserName = 'vkpadao';

-- admin (Admin): Admin123!
UPDATE USERS 
SET PasswordHash = '$2a$12$8ModjUcaRtWQCsW7c8RGeufnBMPihYnf6lHE9p5H0ApkkQEckrdEK'
WHERE UserName = 'admin';

-- billing (Billing): Billing123!
UPDATE USERS 
SET PasswordHash = '$2a$12$4rVvnj4wroAuJkOORA3uT.IALBEXp5gi5/865MfgKZ/AWzmrYAiWi'
WHERE UserName = 'billing';

-- technician (Technician): Technician123!
UPDATE USERS 
SET PasswordHash = '$2a$12$dBtT/QFYy2ScGvTdykBbD.l5ZqVp2DxdjXsPoktyyTnRwqJ0vtcPm'
WHERE UserName = 'technician';

-- auditor (Auditor): Auditor123!
UPDATE USERS 
SET PasswordHash = '$2a$12$qibG2sU9lwoTTN0q1KUECe5u8REijfmScUV8d8Q5tt67avJEnCqjK'
WHERE UserName = 'auditor';

-- Verify the updates
SELECT UserName, LEFT(PasswordHash, 30) + '...' AS PasswordHashPreview 
FROM USERS 
ORDER BY UserID;

PRINT '✓ All user passwords updated successfully!';
PRINT '';
PRINT '┌──────────────┬────────────────────┐';
PRINT '│ Username      │ Password           │';
PRINT '├──────────────┼────────────────────┤';
PRINT '│ vkpadao       │ Superadmin123!     │';
PRINT '│ admin         │ Admin123!          │';
PRINT '│ billing       │ Billing123!        │';
PRINT '│ technician    │ Technician123!     │';
PRINT '│ auditor       │ Auditor123!        │';
PRINT '└──────────────┴────────────────────┘';
GO
