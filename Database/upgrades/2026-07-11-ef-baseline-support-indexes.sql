USE [Gamefilleddb];
GO

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
END
GO

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
END
GO

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
END
GO
