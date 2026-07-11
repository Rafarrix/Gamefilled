USE [Gamefilleddb];
GO

SET NOCOUNT ON;
SET XACT_ABORT ON;
GO

IF OBJECT_ID(N'dbo.Users', N'U') IS NULL
BEGIN
    RAISERROR(N'dbo.Users is missing. The notifications upgrade cannot continue.', 16, 1);
    RETURN;
END;
GO

IF OBJECT_ID(N'dbo.UserNotifications', N'U') IS NULL
BEGIN
    EXEC
    (
        N'CREATE TABLE [dbo].[UserNotifications]
          (
              [Id] INT IDENTITY(1,1) NOT NULL,
              [UserId] INT NOT NULL,
              [ActorUserId] INT NULL,
              [Type] NVARCHAR(64) NOT NULL,
              [TargetUrl] NVARCHAR(500) NULL,
              [GameId] INT NULL,
              [CreatedAt] DATETIME2 NOT NULL
                  CONSTRAINT [DF_UserNotifications_CreatedAt] DEFAULT SYSUTCDATETIME(),
              [ReadAt] DATETIME2 NULL,

              CONSTRAINT [PK_UserNotifications]
                  PRIMARY KEY CLUSTERED ([Id] ASC),

              CONSTRAINT [FK_UserNotifications_Users_UserId]
                  FOREIGN KEY ([UserId]) REFERENCES [dbo].[Users]([Id]) ON DELETE CASCADE,

              CONSTRAINT [FK_UserNotifications_Users_ActorUserId]
                  FOREIGN KEY ([ActorUserId]) REFERENCES [dbo].[Users]([Id]),

              CONSTRAINT [CK_UserNotifications_Type]
                  CHECK ([Type] IN (N''new_follower'', N''mutual_connection''))
          );'
    );
END;
GO

IF OBJECT_ID(N'dbo.UserNotifications', N'U') IS NULL
BEGIN
    RAISERROR(N'dbo.UserNotifications could not be created.', 16, 1);
    RETURN;
END;
GO

IF COL_LENGTH(N'dbo.UserNotifications', N'UserId') IS NULL
   OR COL_LENGTH(N'dbo.UserNotifications', N'ActorUserId') IS NULL
   OR COL_LENGTH(N'dbo.UserNotifications', N'Type') IS NULL
   OR COL_LENGTH(N'dbo.UserNotifications', N'CreatedAt') IS NULL
   OR COL_LENGTH(N'dbo.UserNotifications', N'ReadAt') IS NULL
BEGIN
    RAISERROR(N'dbo.UserNotifications exists but is missing one or more required columns.', 16, 1);
    RETURN;
END;
GO

IF NOT EXISTS
(
    SELECT 1
    FROM sys.indexes
    WHERE [name] = N'IX_UserNotifications_UserId_ReadAt_CreatedAt'
      AND [object_id] = OBJECT_ID(N'dbo.UserNotifications')
)
BEGIN
    CREATE INDEX [IX_UserNotifications_UserId_ReadAt_CreatedAt]
        ON [dbo].[UserNotifications] ([UserId], [ReadAt], [CreatedAt] DESC);
END;
GO

IF NOT EXISTS
(
    SELECT 1
    FROM sys.indexes
    WHERE [name] = N'IX_UserNotifications_UserId_ActorUserId_Type_CreatedAt'
      AND [object_id] = OBJECT_ID(N'dbo.UserNotifications')
)
BEGIN
    CREATE INDEX [IX_UserNotifications_UserId_ActorUserId_Type_CreatedAt]
        ON [dbo].[UserNotifications] ([UserId], [ActorUserId], [Type], [CreatedAt] DESC);
END;
GO

SELECT
    N'PASS' AS [NotificationsUpgrade],
    N'UserNotifications and its primary indexes are present.' AS [Message];
GO
