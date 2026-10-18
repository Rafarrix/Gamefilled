USE [Gamefilleddb];
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
        [ReviewText] NVARCHAR(MAX) NULL,
        [ContainsSpoilers] BIT NOT NULL CONSTRAINT [DF_UserGameEntries_ContainsSpoilers] DEFAULT 0,
        [CreatedAt] DATETIME2 NOT NULL CONSTRAINT [DF_UserGameEntries_CreatedAt] DEFAULT SYSUTCDATETIME(),
        [UpdatedAt] DATETIME2 NOT NULL CONSTRAINT [DF_UserGameEntries_UpdatedAt] DEFAULT SYSUTCDATETIME(),

        CONSTRAINT [FK_UserGameEntries_Users]
            FOREIGN KEY ([UserId]) REFERENCES [dbo].[Users] ([Id]) ON DELETE CASCADE,

        CONSTRAINT [CK_UserGameEntries_Status]
            CHECK ([Status] IN (N'played', N'playing', N'backlog', N'wishlist')),

        CONSTRAINT [CK_UserGameEntries_Rating]
            CHECK ([Rating] IS NULL OR [Rating] BETWEEN 1 AND 10),

        CONSTRAINT [CK_UserGameEntries_ReviewTextLength]
            CHECK ([ReviewText] IS NULL OR LEN([ReviewText]) <= 5000)
    );

    CREATE UNIQUE INDEX [UX_UserGameEntries_UserId_GameId]
        ON [dbo].[UserGameEntries] ([UserId], [GameId]);

    CREATE INDEX [IX_UserGameEntries_GameId_Status]
        ON [dbo].[UserGameEntries] ([GameId], [Status]);

    CREATE INDEX [IX_UserGameEntries_UserId_UpdatedAt]
        ON [dbo].[UserGameEntries] ([UserId], [UpdatedAt] DESC);
END
ELSE
BEGIN
    ALTER TABLE [dbo].[UserGameEntries]
        ALTER COLUMN [ReviewText] NVARCHAR(MAX) NULL;

    IF NOT EXISTS
    (
        SELECT 1
        FROM sys.check_constraints
        WHERE [name] = N'CK_UserGameEntries_ReviewTextLength'
          AND [parent_object_id] = OBJECT_ID(N'dbo.UserGameEntries')
    )
    BEGIN
        ALTER TABLE [dbo].[UserGameEntries]
            ADD CONSTRAINT [CK_UserGameEntries_ReviewTextLength]
            CHECK ([ReviewText] IS NULL OR LEN([ReviewText]) <= 5000);
    END
END
GO
