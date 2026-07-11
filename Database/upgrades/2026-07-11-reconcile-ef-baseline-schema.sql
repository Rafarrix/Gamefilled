USE [Gamefilleddb];
GO

SET NOCOUNT ON;
SET XACT_ABORT ON;

/*
    Reconciles the existing local database with the canonical EF Core baseline.

    Safety rules:
    - No rows are deleted, merged or rewritten.
    - Duplicate data is reported before any schema change is attempted.
    - The whole schema change runs inside one transaction.
    - The script is idempotent and can be run again safely.
*/

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
    CONCAT([FollowerId], N' -> ', [FollowingId]),
    COUNT(*)
FROM [dbo].[Follows]
GROUP BY [FollowerId], [FollowingId]
HAVING COUNT(*) > 1;

INSERT INTO @DuplicateIssues ([Area], [DuplicateKey], [DuplicateCount])
SELECT
    N'UserFavoriteGames.UserId+GameId',
    CONCAT(N'User ', [UserId], N' / Game ', [GameId]),
    COUNT(*)
FROM [dbo].[UserFavoriteGames]
GROUP BY [UserId], [GameId]
HAVING COUNT(*) > 1;

INSERT INTO @DuplicateIssues ([Area], [DuplicateKey], [DuplicateCount])
SELECT
    N'UserFavoriteGames.UserId+SortOrder',
    CONCAT(N'User ', [UserId], N' / Position ', [SortOrder]),
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

    THROW 51001,
        'Schema reconciliation was stopped because duplicate rows would violate the required unique indexes. No schema changes were made.',
        1;
END;

BEGIN TRY
    BEGIN TRANSACTION;

    IF COL_LENGTH(N'dbo.Users', N'CreatedAt') IS NULL
    BEGIN
        THROW 51002, 'Users.CreatedAt is missing. Reconciliation cannot continue.', 1;
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
        THROW 51003, 'Users.CreatedAt is nullable, but the canonical schema requires NOT NULL.', 1;
    END;

    IF @CreatedAtType = N'datetime'
    BEGIN
        ALTER TABLE [dbo].[Users]
            ALTER COLUMN [CreatedAt] DATETIME2 NOT NULL;
    END
    ELSE IF @CreatedAtType <> N'datetime2'
    BEGIN
        THROW 51004, 'Users.CreatedAt has an unexpected SQL type. No schema changes were committed.', 1;
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
END TRY
BEGIN CATCH
    IF XACT_STATE() <> 0
    BEGIN
        ROLLBACK TRANSACTION;
    END;

    THROW;
END CATCH;
GO
