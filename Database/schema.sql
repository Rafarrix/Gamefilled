IF DB_ID(N'Gamefilleddb') IS NULL
BEGIN
    CREATE DATABASE [Gamefilleddb];
END
GO

USE [Gamefilleddb];
GO

IF OBJECT_ID(N'dbo.Users', N'U') IS NULL
BEGIN
    CREATE TABLE [dbo].[Users]
    (
        [Id] INT IDENTITY(1,1) NOT NULL CONSTRAINT [PK_Users] PRIMARY KEY,
        [Email] NVARCHAR(150) NULL,
        [CreatedAt] DATETIME2 NOT NULL CONSTRAINT [DF_Users_CreatedAt] DEFAULT SYSUTCDATETIME(),
        [Username] NVARCHAR(100) NULL,
        [Role] NVARCHAR(20) NOT NULL CONSTRAINT [DF_Users_Role] DEFAULT N'User',
        [PasswordHash] NVARCHAR(255) NOT NULL,
        [DisplayName] NVARCHAR(100) NULL,
        [Bio] NVARCHAR(500) NULL,
        [AvatarUrl] NVARCHAR(500) NULL,
        [BannerUrl] NVARCHAR(500) NULL,
        [LastSeenAt] DATETIME2 NULL
    );

    CREATE UNIQUE INDEX [UX_Users_Username]
        ON [dbo].[Users] ([Username])
        WHERE [Username] IS NOT NULL;

    CREATE UNIQUE INDEX [UX_Users_Email]
        ON [dbo].[Users] ([Email])
        WHERE [Email] IS NOT NULL;
END
GO

IF OBJECT_ID(N'dbo.Follows', N'U') IS NULL
BEGIN
    CREATE TABLE [dbo].[Follows]
    (
        [Id] INT IDENTITY(1,1) NOT NULL CONSTRAINT [PK_Follows] PRIMARY KEY,
        [FollowerId] INT NOT NULL,
        [FollowingId] INT NOT NULL,
        [CreatedAt] DATETIME2 NOT NULL CONSTRAINT [DF_Follows_CreatedAt] DEFAULT SYSUTCDATETIME(),

        CONSTRAINT [FK_Follows_Follower]
            FOREIGN KEY ([FollowerId]) REFERENCES [dbo].[Users] ([Id]),

        CONSTRAINT [FK_Follows_Following]
            FOREIGN KEY ([FollowingId]) REFERENCES [dbo].[Users] ([Id]),

        CONSTRAINT [CK_Follows_NoSelfFollow]
            CHECK ([FollowerId] <> [FollowingId])
    );

    CREATE UNIQUE INDEX [UX_Follows_FollowerId_FollowingId]
        ON [dbo].[Follows] ([FollowerId], [FollowingId]);
END
GO

IF OBJECT_ID(N'dbo.UserFavoriteGames', N'U') IS NULL
BEGIN
    CREATE TABLE [dbo].[UserFavoriteGames]
    (
        [Id] INT IDENTITY(1,1) NOT NULL CONSTRAINT [PK_UserFavoriteGames] PRIMARY KEY,
        [UserId] INT NOT NULL,
        [GameId] INT NOT NULL,
        [SortOrder] INT NOT NULL,
        [CreatedAt] DATETIME2 NOT NULL CONSTRAINT [DF_UserFavoriteGames_CreatedAt] DEFAULT SYSUTCDATETIME(),
        [IsPrimary] BIT NOT NULL CONSTRAINT [DF_UserFavoriteGames_IsPrimary] DEFAULT 0,

        CONSTRAINT [FK_UserFavoriteGames_Users]
            FOREIGN KEY ([UserId]) REFERENCES [dbo].[Users] ([Id]) ON DELETE CASCADE,

        CONSTRAINT [CK_UserFavoriteGames_SortOrder]
            CHECK ([SortOrder] BETWEEN 1 AND 5)
    );

    CREATE UNIQUE INDEX [UX_UserFavoriteGames_UserId_GameId]
        ON [dbo].[UserFavoriteGames] ([UserId], [GameId]);

    CREATE UNIQUE INDEX [UX_UserFavoriteGames_UserId_SortOrder]
        ON [dbo].[UserFavoriteGames] ([UserId], [SortOrder]);
END
GO

IF OBJECT_ID(N'dbo.UserActivities', N'U') IS NULL
BEGIN
    CREATE TABLE [dbo].[UserActivities]
    (
        [Id] INT IDENTITY(1,1) NOT NULL CONSTRAINT [PK_UserActivities] PRIMARY KEY,
        [UserId] INT NOT NULL,
        [Type] NVARCHAR(50) NOT NULL,
        [TargetUserId] INT NULL,
        [GameId] INT NULL,
        [MetaJson] NVARCHAR(MAX) NULL,
        [CreatedAt] DATETIME2 NOT NULL CONSTRAINT [DF_UserActivities_CreatedAt] DEFAULT SYSUTCDATETIME(),

        CONSTRAINT [FK_UserActivities_Users]
            FOREIGN KEY ([UserId]) REFERENCES [dbo].[Users] ([Id]),

        CONSTRAINT [FK_UserActivities_TargetUser]
            FOREIGN KEY ([TargetUserId]) REFERENCES [dbo].[Users] ([Id])
    );

    CREATE INDEX [IX_UserActivities_UserId_CreatedAt]
        ON [dbo].[UserActivities] ([UserId], [CreatedAt] DESC);
END
GO

IF OBJECT_ID(N'dbo.UserGameEntries', N'U') IS NULL
BEGIN
    CREATE TABLE [dbo].[UserGameEntries]
    (
        [Id] INT IDENTITY(1,1) NOT NULL CONSTRAINT [PK_UserGameEntries] PRIMARY KEY,
        [UserId] INT NOT NULL,
        [GameId] INT NOT NULL,
        [Status] NVARCHAR(20) NOT NULL,
        [Rating] INT NULL,
        [ReviewText] NVARCHAR(5000) NULL,
        [ContainsSpoilers] BIT NOT NULL CONSTRAINT [DF_UserGameEntries_ContainsSpoilers] DEFAULT 0,
        [CreatedAt] DATETIME2 NOT NULL CONSTRAINT [DF_UserGameEntries_CreatedAt] DEFAULT SYSUTCDATETIME(),
        [UpdatedAt] DATETIME2 NOT NULL CONSTRAINT [DF_UserGameEntries_UpdatedAt] DEFAULT SYSUTCDATETIME(),

        CONSTRAINT [FK_UserGameEntries_Users]
            FOREIGN KEY ([UserId]) REFERENCES [dbo].[Users] ([Id]) ON DELETE CASCADE,

        CONSTRAINT [CK_UserGameEntries_Status]
            CHECK ([Status] IN (N'played', N'playing', N'backlog', N'wishlist')),

        CONSTRAINT [CK_UserGameEntries_Rating]
            CHECK ([Rating] IS NULL OR [Rating] BETWEEN 1 AND 10)
    );

    CREATE UNIQUE INDEX [UX_UserGameEntries_UserId_GameId]
        ON [dbo].[UserGameEntries] ([UserId], [GameId]);

    CREATE INDEX [IX_UserGameEntries_GameId_Status]
        ON [dbo].[UserGameEntries] ([GameId], [Status]);

    CREATE INDEX [IX_UserGameEntries_UserId_UpdatedAt]
        ON [dbo].[UserGameEntries] ([UserId], [UpdatedAt] DESC);
END
GO
