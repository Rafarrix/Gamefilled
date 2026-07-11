USE [Gamefilleddb];
GO

SET NOCOUNT ON;
SET XACT_ABORT ON;

/*
    Reconciles the existing local database with the canonical EF Core baseline.

    Compatibility and safety:
    - Compatible with older SQL Server engines that do not support THROW.
    - No rows are deleted, merged or rewritten.
    - Duplicate data is reported before any schema change is attempted.
    - The schema changes run inside one transaction.
    - XACT_ABORT rolls the transaction back if a runtime error occurs.
    - The script is idempotent and can be run again safely.
*/

IF OBJECT_ID(N'dbo.Users', N'U') IS NULL
   OR OBJECT_ID(N'dbo.Follows', N'U') IS NULL
   OR OBJECT_ID(N'dbo.UserFavoriteGames', N'U') IS NULL
BEGIN
    RAISERROR(N'Required Gamefilled tables are missing. Run Database/schema-audit.sql and resolve missing tables first.', 16, 1);
    RETURN;
END;

IF COL_LENGTH(N'dbo.Users', N'CreatedAt') IS NULL
BEGIN
    RAISERROR(N'Users.CreatedAt is missing. Reconciliation cannot continue.', 16, 1);
    RETURN;
END;

DECLARE @CreatedAtType SYSNAME;
DECLARE @CreatedAtNullable BIT;

SELECT
    @CreatedAtType = TYPE_NAME([user_type_id]),
    @CreatedAtNullable = [is_nullable]
FROM sys.columns
WHERE [object_id] = OBJECT_ID(N'dbo.Users')
  AND [name] = N'CreatedAt';

IF @CreatedAtNullable <> 0
BEGIN
    RAISERROR(N'Users.CreatedAt is nullable, but the canonical schema requires NOT NULL.', 16, 1);
    RETURN;
END;

IF @CreatedAtType NOT IN (N'datetime', N'datetime2')
BEGIN
    RAISERROR(N'Users.CreatedAt has an unexpected SQL type. No schema changes were made.', 16, 1);
    RETURN;
END;

DECLARE @DuplicateIssues TABLE
(
    [Area] NVARCHAR(80) NOT NULL,
    [DuplicateKey] NVARCHAR(500) NOT NULL,
    [DuplicateCount] INT NOT NULL
);

INSERT INTO @DuplicateIssues ([Area], [DuplicateKey], [DuplicateCount])
SELECT
    N'Users.Username',
    COALESCE([Username], N'<NULL>'),
    COUNT(*)
FROM [dbo].[Users]
WHERE [Username] IS NOT NULL
GROUP BY [Username]
HAVING COUNT(*) > 1;

INSERT INTO @DuplicateIssues ([Area], [DuplicateKey], [DuplicateCount])
SELECT
    N'Follows.FollowerId+FollowingId',
    CAST([FollowerId] AS NVARCHAR(20)) + N' -> ' + CAST([FollowingId] AS NVARCHAR(20)),
    COUNT(*)
FROM [dbo].[Follows]
GROUP BY [FollowerId], [FollowingId]
HAVING COUNT(*) > 1;

INSERT INTO @DuplicateIssues ([Area], [DuplicateKey], [DuplicateCount])
SELECT
    N'UserFavoriteGames.UserId+GameId',
    N'User ' + CAST([UserId] AS NVARCHAR(20)) + N' / Game ' + CAST([GameId] AS NVARCHAR(20)),
    COUNT(*)
FROM [dbo].[UserFavoriteGames]
GROUP BY [UserId], [GameId]
HAVING COUNT(*) > 1;

INSERT INTO @DuplicateIssues ([Area], [DuplicateKey], [DuplicateCount])
SELECT
    N'UserFavoriteGames.UserId+SortOrder',
    N'User ' + CAST([UserId] AS NVARCHAR(20)) + N' / Position ' + CAST([SortOrder] AS NVARCHAR(20)),
    COUNT(*)
FROM [dbo].[UserFavoriteGames]
GROUP BY [UserId], [SortOrder]
HAVING COUNT(*) > 1;

IF EXISTS (SELECT 1 FROM @DuplicateIssues)
BEGIN
    SELECT
        N'BLOCKED' AS [Reconciliation],
        [Area],
        [DuplicateKey],
        [DuplicateCount]
    FROM @DuplicateIssues
    ORDER BY [Area], [DuplicateKey];

    RAISERROR(N'Schema reconciliation stopped because duplicate rows would violate the required unique indexes. No schema changes were made.', 16, 1);
    RETURN;
END;

BEGIN TRANSACTION;

IF @CreatedAtType = N'datetime'
BEGIN
    DECLARE @DefaultConstraintName SYSNAME;
    DECLARE @DropDefaultSql NVARCHAR(1000);

    SELECT @DefaultConstraintName = dc.[name]
    FROM sys.default_constraints dc
    INNER JOIN sys.columns c
        ON c.[object_id] = dc.[parent_object_id]
       AND c.[column_id] = dc.[parent_column_id]
    WHERE dc.[parent_object_id] = OBJECT_ID(N'dbo.Users')
      AND c.[name] = N'CreatedAt';

    IF @DefaultConstraintName IS NOT NULL
    BEGIN
        SET @DropDefaultSql =
            N'ALTER TABLE [dbo].[Users] DROP CONSTRAINT ' + QUOTENAME(@DefaultConstraintName) + N';';

        EXEC sp_executesql @DropDefaultSql;
    END;

    ALTER TABLE [dbo].[Users]
        ALTER COLUMN [CreatedAt] DATETIME2 NOT NULL;
END;

IF NOT EXISTS
(
    SELECT 1
    FROM sys.default_constraints dc
    INNER JOIN sys.columns c
        ON c.[object_id] = dc.[parent_object_id]
       AND c.[column_id] = dc.[parent_column_id]
    WHERE dc.[parent_object_id] = OBJECT_ID(N'dbo.Users')
      AND c.[name] = N'CreatedAt'
)
BEGIN
    ALTER TABLE [dbo].[Users]
        ADD CONSTRAINT [DF_Users_CreatedAt]
        DEFAULT (SYSUTCDATETIME()) FOR [CreatedAt];
END;

IF NOT EXISTS
(
    SELECT 1
    FROM sys.indexes
    WHERE [name] = N'UX_Users_Username'
      AND [object_id] = OBJECT_ID(N'dbo.Users')
)
BEGIN
    CREATE UNIQUE INDEX [UX_Users_Username]
        ON [dbo].[Users] ([Username])
        WHERE [Username] IS NOT NULL;
END;

IF NOT EXISTS
(
    SELECT 1
    FROM sys.indexes
    WHERE [name] = N'UX_Follows_FollowerId_FollowingId'
      AND [object_id] = OBJECT_ID(N'dbo.Follows')
)
BEGIN
    CREATE UNIQUE INDEX [UX_Follows_FollowerId_FollowingId]
        ON [dbo].[Follows] ([FollowerId], [FollowingId]);
END;

IF NOT EXISTS
(
    SELECT 1
    FROM sys.indexes
    WHERE [name] = N'UX_UserFavoriteGames_UserId_GameId'
      AND [object_id] = OBJECT_ID(N'dbo.UserFavoriteGames')
)
BEGIN
    CREATE UNIQUE INDEX [UX_UserFavoriteGames_UserId_GameId]
        ON [dbo].[UserFavoriteGames] ([UserId], [GameId]);
END;

IF NOT EXISTS
(
    SELECT 1
    FROM sys.indexes
    WHERE [name] = N'UX_UserFavoriteGames_UserId_SortOrder'
      AND [object_id] = OBJECT_ID(N'dbo.UserFavoriteGames')
)
BEGIN
    CREATE UNIQUE INDEX [UX_UserFavoriteGames_UserId_SortOrder]
        ON [dbo].[UserFavoriteGames] ([UserId], [SortOrder]);
END;

COMMIT TRANSACTION;

SELECT
    N'PASS' AS [Reconciliation],
    N'Users.CreatedAt and the four required unique indexes now match the EF baseline.' AS [Message];
GO
