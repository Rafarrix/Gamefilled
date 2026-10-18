USE [Gamefilleddb];
GO

-- Ignore impossible timestamps left by old manual/test data.
UPDATE [dbo].[Users]
SET [LastSeenAt] = NULL
WHERE [LastSeenAt] > DATEADD(MINUTE, 5, SYSUTCDATETIME());
GO

IF NOT EXISTS
(
    SELECT 1
    FROM sys.indexes
    WHERE name = N'IX_Users_LastSeenAt'
      AND object_id = OBJECT_ID(N'dbo.Users')
)
BEGIN
    CREATE INDEX [IX_Users_LastSeenAt]
        ON [dbo].[Users] ([LastSeenAt] DESC)
        INCLUDE ([Username], [DisplayName], [AvatarUrl]);
END
GO
