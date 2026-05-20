/*
  Forgot Password Recovery schema update
  Safe to run multiple times.
*/

IF COL_LENGTH('USERS', 'AuthVersion') IS NULL
BEGIN
    ALTER TABLE USERS
    ADD AuthVersion INT NOT NULL CONSTRAINT DF_USERS_AuthVersion DEFAULT (1);
END;

IF COL_LENGTH('USERS', 'FailedLoginAttempts') IS NULL
BEGIN
    ALTER TABLE USERS
    ADD FailedLoginAttempts INT NOT NULL CONSTRAINT DF_USERS_FailedLoginAttempts DEFAULT (0);
END;

IF COL_LENGTH('USERS', 'LockoutEndAt') IS NULL
BEGIN
    ALTER TABLE USERS
    ADD LockoutEndAt DATETIME2(0) NULL;
END;

IF COL_LENGTH('USERS', 'LockoutCycleCount') IS NULL
BEGIN
    ALTER TABLE USERS
    ADD LockoutCycleCount INT NOT NULL CONSTRAINT DF_USERS_LockoutCycleCount DEFAULT (0);
END;

IF COL_LENGTH('USERS', 'IsPermanentlyLocked') IS NULL
BEGIN
    ALTER TABLE USERS
    ADD IsPermanentlyLocked BIT NOT NULL CONSTRAINT DF_USERS_IsPermanentlyLocked DEFAULT (0);
END;

IF COL_LENGTH('USERS', 'PermanentlyLockedAt') IS NULL
BEGIN
    ALTER TABLE USERS
    ADD PermanentlyLockedAt DATETIME2(0) NULL;
END;

IF COL_LENGTH('USERS', 'LockoutReason') IS NULL
BEGIN
    ALTER TABLE USERS
    ADD LockoutReason NVARCHAR(200) NULL;
END;

IF COL_LENGTH('USERS', 'LastFailedLoginAt') IS NULL
BEGIN
    ALTER TABLE USERS
    ADD LastFailedLoginAt DATETIME2(0) NULL;
END;

IF COL_LENGTH('USERS', 'IsMfaEnabled') IS NULL
BEGIN
    ALTER TABLE USERS
    ADD IsMfaEnabled BIT NOT NULL CONSTRAINT DF_USERS_IsMfaEnabled DEFAULT (0);
END;

IF COL_LENGTH('USERS', 'MfaType') IS NULL
BEGIN
    ALTER TABLE USERS
    ADD MfaType NVARCHAR(20) NULL;
END;

IF COL_LENGTH('USERS', 'TotpSecretKey') IS NULL
BEGIN
    ALTER TABLE USERS
    ADD TotpSecretKey NVARCHAR(256) NULL;
END;

IF COL_LENGTH('USERS', 'EmailOtpHash') IS NULL
BEGIN
    ALTER TABLE USERS
    ADD EmailOtpHash NVARCHAR(128) NULL;
END;

IF COL_LENGTH('USERS', 'EmailOtpExpiresAt') IS NULL
BEGIN
    ALTER TABLE USERS
    ADD EmailOtpExpiresAt DATETIME2(0) NULL;
END;

IF COL_LENGTH('USERS', 'EmailOtpFailedAttempts') IS NULL
BEGIN
    ALTER TABLE USERS
    ADD EmailOtpFailedAttempts INT NOT NULL CONSTRAINT DF_USERS_EmailOtpFailedAttempts DEFAULT (0);
END;

IF COL_LENGTH('USERS', 'LastMfaAt') IS NULL
BEGIN
    ALTER TABLE USERS
    ADD LastMfaAt DATETIME2(0) NULL;
END;

IF COL_LENGTH('USERS', 'MustChangePassword') IS NULL
BEGIN
    ALTER TABLE USERS
    ADD MustChangePassword BIT NOT NULL CONSTRAINT DF_USERS_MustChangePassword DEFAULT (0);
END;

IF COL_LENGTH('USERS', 'TemporaryPasswordIssuedAt') IS NULL
BEGIN
    ALTER TABLE USERS
    ADD TemporaryPasswordIssuedAt DATETIME2(0) NULL;
END;

IF COL_LENGTH('USERS', 'EmailHash') IS NULL
BEGIN
    ALTER TABLE USERS
    ADD EmailHash NVARCHAR(64) NULL;
END;

IF COL_LENGTH('CUSTOMERS', 'EmailHash') IS NULL
BEGIN
    ALTER TABLE CUSTOMERS
    ADD EmailHash NVARCHAR(64) NULL;
END;

IF COL_LENGTH('SHOP', 'EmailHash') IS NULL
BEGIN
    ALTER TABLE SHOP
    ADD EmailHash NVARCHAR(64) NULL;
END;

IF OBJECT_ID('PASSWORD_RESET_TOKENS', 'U') IS NULL
BEGIN
    CREATE TABLE PASSWORD_RESET_TOKENS
    (
        PasswordResetTokenID BIGINT IDENTITY(1,1) NOT NULL PRIMARY KEY,
        UserID BIGINT NOT NULL,
        TokenHash NVARCHAR(128) NOT NULL,
        ExpiresAt DATETIME2(0) NOT NULL,
        UsedAt DATETIME2(0) NULL,
        RequestedIp NVARCHAR(45) NULL,
        CreatedAt DATETIME2(0) NOT NULL CONSTRAINT DF_PASSWORD_RESET_TOKENS_CreatedAt DEFAULT (SYSDATETIME()),
        CONSTRAINT FK_PASSWORD_RESET_TOKENS_USERS FOREIGN KEY (UserID) REFERENCES USERS(UserID) ON DELETE CASCADE
    );
END;
ELSE
BEGIN
    DECLARE @hasAnyIdentity BIT = CASE
        WHEN EXISTS (
            SELECT 1
            FROM sys.identity_columns
            WHERE object_id = OBJECT_ID('PASSWORD_RESET_TOKENS')
        ) THEN 1
        ELSE 0
    END;

    IF COL_LENGTH('PASSWORD_RESET_TOKENS', 'PasswordResetTokenID') IS NULL
    BEGIN
        IF @hasAnyIdentity = 0
        BEGIN
            ALTER TABLE PASSWORD_RESET_TOKENS
            ADD PasswordResetTokenID BIGINT IDENTITY(1,1) NOT NULL;
        END;
        ELSE
        BEGIN
            ALTER TABLE PASSWORD_RESET_TOKENS
            ADD PasswordResetTokenID BIGINT NULL;
        END;
    END;

    IF COL_LENGTH('PASSWORD_RESET_TOKENS', 'PasswordResetTokenID') IS NOT NULL
       AND EXISTS (
            SELECT 1
            FROM sys.columns
            WHERE object_id = OBJECT_ID('PASSWORD_RESET_TOKENS')
              AND name = 'PasswordResetTokenID'
              AND is_nullable = 1
       )
    BEGIN
        DECLARE @backfillPasswordResetTokenSql NVARCHAR(MAX) = N'
            DECLARE @nextPasswordResetTokenId BIGINT = ISNULL((SELECT MAX([PasswordResetTokenID]) FROM [PASSWORD_RESET_TOKENS]), 0);

            WHILE EXISTS (SELECT 1 FROM [PASSWORD_RESET_TOKENS] WHERE [PasswordResetTokenID] IS NULL)
            BEGIN
                SET @nextPasswordResetTokenId = @nextPasswordResetTokenId + 1;

                UPDATE TOP (1) [PASSWORD_RESET_TOKENS]
                SET [PasswordResetTokenID] = @nextPasswordResetTokenId
                WHERE [PasswordResetTokenID] IS NULL;
            END;';

        EXEC sp_executesql @backfillPasswordResetTokenSql;

        ALTER TABLE PASSWORD_RESET_TOKENS
        ALTER COLUMN PasswordResetTokenID BIGINT NOT NULL;
    END;

    IF COL_LENGTH('PASSWORD_RESET_TOKENS', 'UserID') IS NULL
    BEGIN
        ALTER TABLE PASSWORD_RESET_TOKENS
        ADD UserID BIGINT NULL;
    END;

    IF COL_LENGTH('PASSWORD_RESET_TOKENS', 'TokenHash') IS NULL
    BEGIN
        ALTER TABLE PASSWORD_RESET_TOKENS
        ADD TokenHash NVARCHAR(128) NULL;
    END;

    IF COL_LENGTH('PASSWORD_RESET_TOKENS', 'ExpiresAt') IS NULL
    BEGIN
        ALTER TABLE PASSWORD_RESET_TOKENS
        ADD ExpiresAt DATETIME2(0) NULL;
    END;

    IF COL_LENGTH('PASSWORD_RESET_TOKENS', 'UsedAt') IS NULL
    BEGIN
        ALTER TABLE PASSWORD_RESET_TOKENS
        ADD UsedAt DATETIME2(0) NULL;
    END;

    IF COL_LENGTH('PASSWORD_RESET_TOKENS', 'RequestedIp') IS NULL
    BEGIN
        ALTER TABLE PASSWORD_RESET_TOKENS
        ADD RequestedIp NVARCHAR(45) NULL;
    END;

    IF COL_LENGTH('PASSWORD_RESET_TOKENS', 'CreatedAt') IS NULL
    BEGIN
        ALTER TABLE PASSWORD_RESET_TOKENS
        ADD CreatedAt DATETIME2(0) NULL;
    END;

    IF NOT EXISTS (
        SELECT 1
        FROM sys.default_constraints dc
        INNER JOIN sys.columns c ON c.object_id = dc.parent_object_id AND c.column_id = dc.parent_column_id
        WHERE dc.parent_object_id = OBJECT_ID('PASSWORD_RESET_TOKENS')
          AND c.name = 'CreatedAt'
    )
    BEGIN
        ALTER TABLE PASSWORD_RESET_TOKENS
        ADD CONSTRAINT DF_PASSWORD_RESET_TOKENS_CreatedAt DEFAULT (SYSDATETIME()) FOR CreatedAt;
    END;

    IF COL_LENGTH('PASSWORD_RESET_TOKENS', 'PasswordResetTokenID') IS NOT NULL
       AND NOT EXISTS (
            SELECT 1
            FROM sys.identity_columns
            WHERE object_id = OBJECT_ID('PASSWORD_RESET_TOKENS')
              AND name = 'PasswordResetTokenID'
       )
    BEGIN
        DECLARE @nextSeqStart BIGINT = 1;
        DECLARE @getNextSeqStartSql NVARCHAR(MAX) = N'
            SELECT @nextOut = ISNULL(MAX([PasswordResetTokenID]) + 1, 1)
            FROM [PASSWORD_RESET_TOKENS];';

        EXEC sp_executesql @getNextSeqStartSql, N'@nextOut BIGINT OUTPUT', @nextOut = @nextSeqStart OUTPUT;

        IF OBJECT_ID('SQ_PASSWORD_RESET_TOKENS_ID', 'SO') IS NULL
        BEGIN
            DECLARE @createSequenceSql NVARCHAR(300) =
                N'CREATE SEQUENCE dbo.SQ_PASSWORD_RESET_TOKENS_ID AS BIGINT START WITH '
                + CAST(@nextSeqStart AS NVARCHAR(30))
                + N' INCREMENT BY 1;';
            EXEC(@createSequenceSql);
        END;

        IF NOT EXISTS (
            SELECT 1
            FROM sys.default_constraints dc
            INNER JOIN sys.columns c ON c.object_id = dc.parent_object_id AND c.column_id = dc.parent_column_id
            WHERE dc.parent_object_id = OBJECT_ID('PASSWORD_RESET_TOKENS')
              AND c.name = 'PasswordResetTokenID'
        )
        BEGIN
            ALTER TABLE PASSWORD_RESET_TOKENS
            ADD CONSTRAINT DF_PASSWORD_RESET_TOKENS_PasswordResetTokenID
                DEFAULT (NEXT VALUE FOR dbo.SQ_PASSWORD_RESET_TOKENS_ID) FOR PasswordResetTokenID;
        END;
    END;
END;

IF NOT EXISTS (
    SELECT 1
    FROM sys.indexes
    WHERE name = 'UX_PASSWORD_RESET_TOKENS_PasswordResetTokenID'
      AND object_id = OBJECT_ID('PASSWORD_RESET_TOKENS')
)
BEGIN
    CREATE UNIQUE INDEX UX_PASSWORD_RESET_TOKENS_PasswordResetTokenID
        ON PASSWORD_RESET_TOKENS(PasswordResetTokenID);
END;

IF NOT EXISTS (
    SELECT 1
    FROM sys.indexes
    WHERE name = 'UX_PASSWORD_RESET_TOKENS_TokenHash'
      AND object_id = OBJECT_ID('PASSWORD_RESET_TOKENS')
)
BEGIN
    CREATE UNIQUE INDEX UX_PASSWORD_RESET_TOKENS_TokenHash
        ON PASSWORD_RESET_TOKENS(TokenHash)
        WHERE TokenHash IS NOT NULL;
END;

IF NOT EXISTS (
    SELECT 1
    FROM sys.indexes
    WHERE name = 'IX_PASSWORD_RESET_TOKENS_UserID_CreatedAt'
      AND object_id = OBJECT_ID('PASSWORD_RESET_TOKENS')
)
BEGIN
    CREATE INDEX IX_PASSWORD_RESET_TOKENS_UserID_CreatedAt
        ON PASSWORD_RESET_TOKENS(UserID, CreatedAt);
END;

IF NOT EXISTS (
    SELECT 1
    FROM sys.indexes
    WHERE name = 'IX_USERS_EmailHash'
      AND object_id = OBJECT_ID('USERS')
)
BEGIN
    CREATE INDEX IX_USERS_EmailHash
        ON USERS(EmailHash);
END;

IF NOT EXISTS (
    SELECT 1
    FROM sys.indexes
    WHERE name = 'IX_CUSTOMERS_ShopID_EmailHash'
      AND object_id = OBJECT_ID('CUSTOMERS')
)
BEGIN
    CREATE INDEX IX_CUSTOMERS_ShopID_EmailHash
        ON CUSTOMERS(ShopID, EmailHash);
END;

IF NOT EXISTS (
    SELECT 1
    FROM sys.indexes
    WHERE name = 'IX_SHOP_EmailHash'
      AND object_id = OBJECT_ID('SHOP')
)
BEGIN
    CREATE INDEX IX_SHOP_EmailHash
        ON SHOP(EmailHash);
END;
