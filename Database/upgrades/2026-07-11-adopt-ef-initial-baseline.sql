USE [Gamefilleddb];
GO

SET NOCOUNT ON;
SET XACT_ABORT ON;

/*
    Marks the canonical EF Core baseline as already applied to an existing
    Gamefilled database.

    This script does not create or alter application tables. It only:
    1. verifies the essential baseline objects;
    2. creates dbo.__EFMigrationsHistory when missing;
    3. records 20260711171753_InitialBaseline as applied.

    Run Database/schema-audit.sql and the support-index upgrade first.
*/

DECLARE @MigrationId NVARCHAR(150);
DECLARE @ProductVersion NVARCHAR(32);

SET @MigrationId = N'20260711171753_InitialBaseline';
SET @ProductVersion = N'10.0.9';

DECLARE @Issues TABLE
(
    [Area] NVARCHAR(50) NOT NULL,
    [ObjectName] NVARCHAR(256) NOT NULL,
    [Problem] NVARCHAR(500) NOT NULL
);

DECLARE @ExpectedTables TABLE ([TableName] SYSNAME PRIMARY KEY);
INSERT INTO @ExpectedTables ([TableName])
VALUES
    (N'Users'),
    (N'Follows'),
    (N'UserFavoriteGames'),
    (N'UserActivities'),
    (N'UserGameEntries'),
    (N'UserNotifications');

INSERT INTO @Issues ([Area], [ObjectName], [Problem])
SELECT N'Table', e.[TableName], N'Missing canonical application table.'
FROM @ExpectedTables e
WHERE OBJECT_ID(N'dbo.' + QUOTENAME(e.[TableName]), N'U') IS NULL;

IF OBJECT_ID(N'dbo.Users', N'U') IS NOT NULL
BEGIN
    IF COL_LENGTH(N'dbo.Users', N'CreatedAt') IS NULL
    BEGIN
        INSERT INTO @Issues ([Area], [ObjectName], [Problem])
        VALUES (N'Column', N'Users.CreatedAt', N'Missing required column.');
    END
    ELSE
    BEGIN
        INSERT INTO @Issues ([Area], [ObjectName], [Problem])
        SELECT N'Column', N'Users.CreatedAt', N'Expected DATETIME2 NOT NULL.'
        FROM sys.columns c
        WHERE c.[object_id] = OBJECT_ID(N'dbo.Users')
          AND c.[name] = N'CreatedAt'
          AND (TYPE_NAME(c.[user_type_id]) <> N'datetime2' OR c.[is_nullable] <> 0);
    END;
END;

DECLARE @ExpectedIndexes TABLE
(
    [TableName] SYSNAME NOT NULL,
    [IndexName] SYSNAME NOT NULL,
    PRIMARY KEY ([TableName], [IndexName])
);

INSERT INTO @ExpectedIndexes ([TableName], [IndexName])
VALUES
    (N'Users', N'UX_Users_Username'),
    (N'Users', N'UX_Users_Email'),
    (N'Users', N'IX_Users_LastSeenAt'),
    (N'Follows', N'UX_Follows_FollowerId_FollowingId'),
    (N'Follows', N'IX_Follows_FollowingId'),
    (N'UserFavoriteGames', N'UX_UserFavoriteGames_UserId_GameId'),
    (N'UserFavoriteGames', N'UX_UserFavoriteGames_UserId_SortOrder'),
    (N'UserActivities', N'IX_UserActivities_UserId_CreatedAt'),
    (N'UserActivities', N'IX_UserActivities_TargetUserId'),
    (N'UserGameEntries', N'UX_UserGameEntries_UserId_GameId'),
    (N'UserGameEntries', N'IX_UserGameEntries_GameId_Status'),
    (N'UserGameEntries', N'IX_UserGameEntries_UserId_UpdatedAt'),
    (N'UserNotifications', N'IX_UserNotifications_ActorUserId'),
    (N'UserNotifications', N'IX_UserNotifications_UserId_ReadAt_CreatedAt'),
    (N'UserNotifications', N'IX_UserNotifications_UserId_ActorUserId_Type_CreatedAt');

INSERT INTO @Issues ([Area], [ObjectName], [Problem])
SELECT N'Index', e.[TableName] + N'.' + e.[IndexName], N'Missing baseline index.'
FROM @ExpectedIndexes e
LEFT JOIN sys.tables t
    ON t.[name] = e.[TableName]
   AND SCHEMA_NAME(t.[schema_id]) = N'dbo'
LEFT JOIN sys.indexes i
    ON i.[object_id] = t.[object_id]
   AND i.[name] = e.[IndexName]
WHERE i.[index_id] IS NULL;

DECLARE @ExpectedConstraints TABLE
(
    [TableName] SYSNAME NOT NULL,
    [ConstraintName] SYSNAME NOT NULL,
    PRIMARY KEY ([TableName], [ConstraintName])
);

INSERT INTO @ExpectedConstraints ([TableName], [ConstraintName])
VALUES
    (N'Follows', N'CK_Follows_NoSelfFollow'),
    (N'UserFavoriteGames', N'CK_UserFavoriteGames_SortOrder'),
    (N'UserGameEntries', N'CK_UserGameEntries_Status'),
    (N'UserGameEntries', N'CK_UserGameEntries_Rating'),
    (N'UserGameEntries', N'CK_UserGameEntries_ReviewTextLength'),
    (N'UserNotifications', N'CK_UserNotifications_Type');

INSERT INTO @Issues ([Area], [ObjectName], [Problem])
SELECT N'Constraint', e.[TableName] + N'.' + e.[ConstraintName], N'Missing baseline check constraint.'
FROM @ExpectedConstraints e
LEFT JOIN sys.tables t
    ON t.[name] = e.[TableName]
   AND SCHEMA_NAME(t.[schema_id]) = N'dbo'
LEFT JOIN sys.check_constraints c
    ON c.[parent_object_id] = t.[object_id]
   AND c.[name] = e.[ConstraintName]
WHERE c.[object_id] IS NULL;

IF OBJECT_ID(N'dbo.__EFMigrationsHistory', N'U') IS NOT NULL
BEGIN
    IF COL_LENGTH(N'dbo.__EFMigrationsHistory', N'MigrationId') IS NULL
    BEGIN
        INSERT INTO @Issues VALUES
            (N'History', N'__EFMigrationsHistory.MigrationId', N'Existing history table has an invalid structure.');
    END;

    IF COL_LENGTH(N'dbo.__EFMigrationsHistory', N'ProductVersion') IS NULL
    BEGIN
        INSERT INTO @Issues VALUES
            (N'History', N'__EFMigrationsHistory.ProductVersion', N'Existing history table has an invalid structure.');
    END;
END;

IF EXISTS (SELECT 1 FROM @Issues)
BEGIN
    SELECT
        N'BLOCKED' AS [BaselineAdoption],
        [Area],
        [ObjectName],
        [Problem]
    FROM @Issues
    ORDER BY [Area], [ObjectName];

    RAISERROR(N'EF baseline adoption was blocked because the existing schema does not match the canonical baseline. No migration history was changed.', 16, 1);
    RETURN;
END;

DECLARE @ExistingProductVersion NVARCHAR(32);
DECLARE @AlreadyAdopted BIT;
SET @ExistingProductVersion = NULL;
SET @AlreadyAdopted = 0;

IF OBJECT_ID(N'dbo.__EFMigrationsHistory', N'U') IS NOT NULL
BEGIN
    EXEC sp_executesql
        N'SELECT @ExistingVersion = [ProductVersion]
          FROM [dbo].[__EFMigrationsHistory]
          WHERE [MigrationId] = @ExpectedMigrationId;',
        N'@ExpectedMigrationId NVARCHAR(150), @ExistingVersion NVARCHAR(32) OUTPUT',
        @ExpectedMigrationId = @MigrationId,
        @ExistingVersion = @ExistingProductVersion OUTPUT;
END;

IF @ExistingProductVersion IS NOT NULL
   AND @ExistingProductVersion <> @ProductVersion
BEGIN
    SELECT
        N'BLOCKED' AS [BaselineAdoption],
        @MigrationId AS [MigrationId],
        @ExistingProductVersion AS [ExistingProductVersion],
        @ProductVersion AS [ExpectedProductVersion];

    RAISERROR(N'The baseline migration already exists with a different EF Core product version. No changes were made.', 16, 1);
    RETURN;
END;

IF @ExistingProductVersion = @ProductVersion
BEGIN
    SET @AlreadyAdopted = 1;
END;

BEGIN TRANSACTION;

IF OBJECT_ID(N'dbo.__EFMigrationsHistory', N'U') IS NULL
BEGIN
    EXEC
    (
        N'CREATE TABLE [dbo].[__EFMigrationsHistory]
          (
              [MigrationId] NVARCHAR(150) NOT NULL,
              [ProductVersion] NVARCHAR(32) NOT NULL,
              CONSTRAINT [PK___EFMigrationsHistory]
                  PRIMARY KEY ([MigrationId])
          );'
    );
END;

EXEC sp_executesql
    N'IF NOT EXISTS
      (
          SELECT 1
          FROM [dbo].[__EFMigrationsHistory]
          WHERE [MigrationId] = @ExpectedMigrationId
      )
      BEGIN
          INSERT INTO [dbo].[__EFMigrationsHistory]
              ([MigrationId], [ProductVersion])
          VALUES
              (@ExpectedMigrationId, @ExpectedProductVersion);
      END;',
    N'@ExpectedMigrationId NVARCHAR(150), @ExpectedProductVersion NVARCHAR(32)',
    @ExpectedMigrationId = @MigrationId,
    @ExpectedProductVersion = @ProductVersion;

COMMIT TRANSACTION;

SELECT
    N'PASS' AS [BaselineAdoption],
    CASE
        WHEN @AlreadyAdopted = 1
            THEN N'InitialBaseline was already registered. No duplicate row was created.'
        ELSE N'InitialBaseline is now registered as applied. Existing application tables were not recreated or modified.'
    END AS [Message],
    @MigrationId AS [MigrationId],
    @ProductVersion AS [ProductVersion];
GO
