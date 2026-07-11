USE [Gamefilleddb];
GO

SET NOCOUNT ON;
SET XACT_ABORT ON;

DECLARE @Issues TABLE
(
    [ObjectName] NVARCHAR(256) NOT NULL,
    [Problem] NVARCHAR(500) NOT NULL
);

IF OBJECT_ID(N'dbo.Follows', N'U') IS NULL
BEGIN
    INSERT INTO @Issues VALUES (N'dbo.Follows', N'Missing table.');
END
ELSE IF COL_LENGTH(N'dbo.Follows', N'FollowingId') IS NULL
BEGIN
    INSERT INTO @Issues VALUES (N'dbo.Follows.FollowingId', N'Missing required column.');
END;

IF OBJECT_ID(N'dbo.UserActivities', N'U') IS NULL
BEGIN
    INSERT INTO @Issues VALUES (N'dbo.UserActivities', N'Missing table.');
END
ELSE IF COL_LENGTH(N'dbo.UserActivities', N'TargetUserId') IS NULL
BEGIN
    INSERT INTO @Issues VALUES (N'dbo.UserActivities.TargetUserId', N'Missing required column.');
END;

IF OBJECT_ID(N'dbo.UserNotifications', N'U') IS NULL
BEGIN
    INSERT INTO @Issues VALUES
    (
        N'dbo.UserNotifications',
        N'Missing table. Run Database/upgrades/2026-07-10-user-notifications.sql first.'
    );
END
ELSE IF COL_LENGTH(N'dbo.UserNotifications', N'ActorUserId') IS NULL
BEGIN
    INSERT INTO @Issues VALUES
    (
        N'dbo.UserNotifications.ActorUserId',
        N'Missing required column. Re-run the Notifications V1 schema upgrade.'
    );
END;

IF EXISTS (SELECT 1 FROM @Issues)
BEGIN
    SELECT
        N'BLOCKED' AS [SupportIndexes],
        [ObjectName],
        [Problem]
    FROM @Issues
    ORDER BY [ObjectName];

    RAISERROR(N'Baseline support-index creation was blocked because required tables or columns are missing. No support indexes were changed.', 16, 1);
    RETURN;
END;

BEGIN TRANSACTION;

IF NOT EXISTS
(
    SELECT 1
    FROM sys.indexes
    WHERE [name] = N'IX_Follows_FollowingId'
      AND [object_id] = OBJECT_ID(N'dbo.Follows')
)
BEGIN
    EXEC
    (
        N'CREATE INDEX [IX_Follows_FollowingId]
          ON [dbo].[Follows] ([FollowingId]);'
    );
END;

IF NOT EXISTS
(
    SELECT 1
    FROM sys.indexes
    WHERE [name] = N'IX_UserActivities_TargetUserId'
      AND [object_id] = OBJECT_ID(N'dbo.UserActivities')
)
BEGIN
    EXEC
    (
        N'CREATE INDEX [IX_UserActivities_TargetUserId]
          ON [dbo].[UserActivities] ([TargetUserId]);'
    );
END;

IF NOT EXISTS
(
    SELECT 1
    FROM sys.indexes
    WHERE [name] = N'IX_UserNotifications_ActorUserId'
      AND [object_id] = OBJECT_ID(N'dbo.UserNotifications')
)
BEGIN
    EXEC
    (
        N'CREATE INDEX [IX_UserNotifications_ActorUserId]
          ON [dbo].[UserNotifications] ([ActorUserId]);'
    );
END;

COMMIT TRANSACTION;

DECLARE @MissingIndexes TABLE
(
    [TableName] SYSNAME NOT NULL,
    [IndexName] SYSNAME NOT NULL
);

IF NOT EXISTS
(
    SELECT 1
    FROM sys.indexes
    WHERE [name] = N'IX_Follows_FollowingId'
      AND [object_id] = OBJECT_ID(N'dbo.Follows')
)
BEGIN
    INSERT INTO @MissingIndexes VALUES (N'Follows', N'IX_Follows_FollowingId');
END;

IF NOT EXISTS
(
    SELECT 1
    FROM sys.indexes
    WHERE [name] = N'IX_UserActivities_TargetUserId'
      AND [object_id] = OBJECT_ID(N'dbo.UserActivities')
)
BEGIN
    INSERT INTO @MissingIndexes VALUES (N'UserActivities', N'IX_UserActivities_TargetUserId');
END;

IF NOT EXISTS
(
    SELECT 1
    FROM sys.indexes
    WHERE [name] = N'IX_UserNotifications_ActorUserId'
      AND [object_id] = OBJECT_ID(N'dbo.UserNotifications')
)
BEGIN
    INSERT INTO @MissingIndexes VALUES (N'UserNotifications', N'IX_UserNotifications_ActorUserId');
END;

IF EXISTS (SELECT 1 FROM @MissingIndexes)
BEGIN
    SELECT
        N'FAILED' AS [SupportIndexes],
        [TableName],
        [IndexName]
    FROM @MissingIndexes
    ORDER BY [TableName], [IndexName];

    RAISERROR(N'One or more EF baseline support indexes are still missing after the upgrade.', 16, 1);
    RETURN;
END;

SELECT
    N'PASS' AS [SupportIndexes],
    N'All EF baseline relationship support indexes are present.' AS [Message];
GO
