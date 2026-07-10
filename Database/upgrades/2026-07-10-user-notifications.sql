USE [Gamefilleddb];
GO

IF OBJECT_ID(N'[dbo].[UserNotifications]', N'U') IS NULL
BEGIN
    CREATE TABLE [dbo].[UserNotifications]
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
            CHECK ([Type] IN ('new_follower', 'mutual_connection'))
    );

    CREATE INDEX [IX_UserNotifications_UserId_ReadAt_CreatedAt]
        ON [dbo].[UserNotifications] ([UserId], [ReadAt], [CreatedAt] DESC);

    CREATE INDEX [IX_UserNotifications_UserId_ActorUserId_Type_CreatedAt]
        ON [dbo].[UserNotifications] ([UserId], [ActorUserId], [Type], [CreatedAt] DESC);
END;
GO
