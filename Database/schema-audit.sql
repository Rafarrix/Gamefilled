USE [Gamefilleddb];
GO

SET NOCOUNT ON;

DECLARE @Issues TABLE
(
    [Area] NVARCHAR(40) NOT NULL,
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
SELECT N'Table', e.[TableName], N'Missing table.'
FROM @ExpectedTables e
WHERE OBJECT_ID(N'dbo.' + QUOTENAME(e.[TableName]), N'U') IS NULL;

DECLARE @ExpectedColumns TABLE
(
    [TableName] SYSNAME NOT NULL,
    [ColumnName] SYSNAME NOT NULL,
    [TypeName] SYSNAME NOT NULL,
    [MaxLengthBytes] SMALLINT NULL,
    [IsNullable] BIT NOT NULL,
    PRIMARY KEY ([TableName], [ColumnName])
);

INSERT INTO @ExpectedColumns
    ([TableName], [ColumnName], [TypeName], [MaxLengthBytes], [IsNullable])
VALUES
    (N'Users', N'Id', N'int', 4, 0),
    (N'Users', N'Email', N'nvarchar', 300, 1),
    (N'Users', N'CreatedAt', N'datetime2', 8, 0),
    (N'Users', N'Username', N'nvarchar', 200, 1),
    (N'Users', N'Role', N'nvarchar', 40, 0),
    (N'Users', N'PasswordHash', N'nvarchar', 510, 0),
    (N'Users', N'DisplayName', N'nvarchar', 200, 1),
    (N'Users', N'Bio', N'nvarchar', 1000, 1),
    (N'Users', N'AvatarUrl', N'nvarchar', 1000, 1),
    (N'Users', N'BannerUrl', N'nvarchar', 1000, 1),
    (N'Users', N'LastSeenAt', N'datetime2', 8, 1),

    (N'Follows', N'Id', N'int', 4, 0),
    (N'Follows', N'FollowerId', N'int', 4, 0),
    (N'Follows', N'FollowingId', N'int', 4, 0),
    (N'Follows', N'CreatedAt', N'datetime2', 8, 0),

    (N'UserFavoriteGames', N'Id', N'int', 4, 0),
    (N'UserFavoriteGames', N'UserId', N'int', 4, 0),
    (N'UserFavoriteGames', N'GameId', N'int', 4, 0),
    (N'UserFavoriteGames', N'SortOrder', N'int', 4, 0),
    (N'UserFavoriteGames', N'CreatedAt', N'datetime2', 8, 0),
    (N'UserFavoriteGames', N'IsPrimary', N'bit', 1, 0),

    (N'UserActivities', N'Id', N'int', 4, 0),
    (N'UserActivities', N'UserId', N'int', 4, 0),
    (N'UserActivities', N'Type', N'nvarchar', 100, 0),
    (N'UserActivities', N'TargetUserId', N'int', 4, 1),
    (N'UserActivities', N'GameId', N'int', 4, 1),
    (N'UserActivities', N'MetaJson', N'nvarchar', -1, 1),
    (N'UserActivities', N'CreatedAt', N'datetime2', 8, 0),

    (N'UserGameEntries', N'Id', N'int', 4, 0),
    (N'UserGameEntries', N'UserId', N'int', 4, 0),
    (N'UserGameEntries', N'GameId', N'int', 4, 0),
    (N'UserGameEntries', N'Status', N'nvarchar', 40, 0),
    (N'UserGameEntries', N'Rating', N'int', 4, 1),
    (N'UserGameEntries', N'ReviewText', N'nvarchar', -1, 1),
    (N'UserGameEntries', N'ContainsSpoilers', N'bit', 1, 0),
    (N'UserGameEntries', N'CreatedAt', N'datetime2', 8, 0),
    (N'UserGameEntries', N'UpdatedAt', N'datetime2', 8, 0),

    (N'UserNotifications', N'Id', N'int', 4, 0),
    (N'UserNotifications', N'UserId', N'int', 4, 0),
    (N'UserNotifications', N'ActorUserId', N'int', 4, 1),
    (N'UserNotifications', N'Type', N'nvarchar', 128, 0),
    (N'UserNotifications', N'TargetUrl', N'nvarchar', 1000, 1),
    (N'UserNotifications', N'GameId', N'int', 4, 1),
    (N'UserNotifications', N'CreatedAt', N'datetime2', 8, 0),
    (N'UserNotifications', N'ReadAt', N'datetime2', 8, 1);

INSERT INTO @Issues ([Area], [ObjectName], [Problem])
SELECT
    N'Column',
    e.[TableName] + N'.' + e.[ColumnName],
    CASE
        WHEN c.[column_id] IS NULL THEN N'Missing column.'
        ELSE CONCAT(
            N'Expected ', e.[TypeName],
            CASE WHEN e.[MaxLengthBytes] IS NULL THEN N'' ELSE CONCAT(N' max_length=', e.[MaxLengthBytes]) END,
            N' nullable=', e.[IsNullable],
            N'; found ', TYPE_NAME(c.[user_type_id]),
            N' max_length=', c.[max_length],
            N' nullable=', c.[is_nullable], N'.')
    END
FROM @ExpectedColumns e
LEFT JOIN sys.tables t
    ON t.[name] = e.[TableName]
   AND SCHEMA_NAME(t.[schema_id]) = N'dbo'
LEFT JOIN sys.columns c
    ON c.[object_id] = t.[object_id]
   AND c.[name] = e.[ColumnName]
WHERE c.[column_id] IS NULL
   OR TYPE_NAME(c.[user_type_id]) <> e.[TypeName]
   OR (e.[MaxLengthBytes] IS NOT NULL AND c.[max_length] <> e.[MaxLengthBytes])
   OR c.[is_nullable] <> e.[IsNullable];

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
SELECT N'Index', e.[TableName] + N'.' + e.[IndexName], N'Missing index.'
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
SELECT N'Constraint', e.[TableName] + N'.' + e.[ConstraintName], N'Missing check constraint.'
FROM @ExpectedConstraints e
LEFT JOIN sys.tables t
    ON t.[name] = e.[TableName]
   AND SCHEMA_NAME(t.[schema_id]) = N'dbo'
LEFT JOIN sys.check_constraints c
    ON c.[parent_object_id] = t.[object_id]
   AND c.[name] = e.[ConstraintName]
WHERE c.[object_id] IS NULL;

IF EXISTS (SELECT 1 FROM @Issues)
BEGIN
    SELECT N'FAIL' AS [SchemaAudit], [Area], [ObjectName], [Problem]
    FROM @Issues
    ORDER BY [Area], [ObjectName];
END
ELSE
BEGIN
    SELECT
        N'PASS' AS [SchemaAudit],
        N'All canonical tables, columns, indexes and check constraints were found.' AS [Message];
END
GO
