USE [Gamefilleddb];
GO

SET NOCOUNT ON;
GO

IF OBJECT_ID(N'dbo.Follows', N'U') IS NULL
BEGIN
    RAISERROR(N'Missing dbo.Follows. Run Database/schema-audit.sql and the required schema upgrades before the support-index script.', 16, 1);
    RETURN;
END;

IF COL_LENGTH(N'dbo.Follows', N'FollowingId') IS NULL
BEGIN
    RAISERROR(N'Missing dbo.Follows.FollowingId. The support-index script cannot continue.', 16, 1);
    RETURN;
END;

IF NOT EXISTS
(
    SELECT 1
    FROM sys.indexes
    WHERE name = N'IX_Follows_FollowingId'
      AND object_id = OBJECT_ID(N'dbo.Follows')
)
BEGIN
    CREATE INDEX [IX_Follows_FollowingId]
        ON [dbo].[Follows] ([FollowingId]);
END;
GO

IF OBJECT_ID(N'dbo.UserActivities', N'U') IS NULL
BEGIN
    RAISERROR(N'Missing dbo.UserActivities. Run Database/schema-audit.sql and the required schema upgrades before the support-index script.', 16, 1);
    RETURN;
END;

IF COL_LENGTH(N'dbo.UserActivities', N'TargetUserId') IS NULL
BEGIN
    RAISERROR(N'Missing dbo.UserActivities.TargetUserId. The support-index script cannot continue.', 16, 1);
    RETURN;
END;

IF NOT EXISTS
(
    SELECT 1
    FROM sys.indexes
    WHERE name = N'IX_UserActivities_TargetUserId'
      AND object_id = OBJECT_ID(N'dbo.UserActivities')
)
BEGIN
    CREATE INDEX [IX_UserActivities_TargetUserId]
        ON [dbo].[UserActivities] ([TargetUserId]);
END;
GO

IF OBJECT_ID(N'dbo.UserNotifications', N'U') IS NULL
BEGIN
    RAISERROR(N'Missing dbo.UserNotifications. Run Database/upgrades/2026-07-10-user-notifications.sql first.', 16, 1);
    RETURN;
END;

IF COL_LENGTH(N'dbo.UserNotifications', N'ActorUserId') IS NULL
BEGIN
    RAISERROR(N'Missing dbo.UserNotifications.ActorUserId. Re-run the Notifications V1 schema upgrade before continuing.', 16, 1);
    RETURN;
END;

IF NOT EXISTS
(
    SELECT 1
    FROM sys.indexes
    WHERE name = N'IX_UserNotifications_ActorUserId'
      AND object_id = OBJECT_ID(N'dbo.UserNotifications')
)
BEGIN
    CREATE INDEX [IX_UserNotifications_ActorUserId]
        ON [dbo].[UserNotifications] ([ActorUserId]);
END;
GO

SELECT
    N'PASS' AS [SupportIndexes],
    N'All EF baseline relationship support indexes are present.' AS [Message];
GO
