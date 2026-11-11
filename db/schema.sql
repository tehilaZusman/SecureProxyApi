IF OBJECT_ID(N'[__EFMigrationsHistory]') IS NULL
BEGIN
    CREATE TABLE [__EFMigrationsHistory] (
        [MigrationId] nvarchar(150) NOT NULL,
        [ProductVersion] nvarchar(32) NOT NULL,
        CONSTRAINT [PK___EFMigrationsHistory] PRIMARY KEY ([MigrationId])
    );
END;
GO

BEGIN TRANSACTION;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261108193342_InitialCreate'
)
BEGIN
    CREATE TABLE [ConfigurationChangeLogs] (
        [Id] bigint NOT NULL IDENTITY,
        [EntityType] nvarchar(100) NOT NULL,
        [EntityId] nvarchar(100) NOT NULL,
        [Action] nvarchar(50) NOT NULL,
        [Summary] nvarchar(1000) NULL,
        [ChangedBy] nvarchar(200) NULL,
        [ChangedAtUtc] datetimeoffset NOT NULL,
        CONSTRAINT [PK_ConfigurationChangeLogs] PRIMARY KEY ([Id])
    );
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261108193342_InitialCreate'
)
BEGIN
    CREATE TABLE [Destinations] (
        [Id] int NOT NULL IDENTITY,
        [Name] nvarchar(200) NOT NULL,
        [BaseUrl] nvarchar(2000) NOT NULL,
        [IsActive] bit NOT NULL,
        [TimeoutMs] int NOT NULL,
        [AllowedPorts] nvarchar(200) NULL,
        [AuthProfileRef] nvarchar(200) NULL,
        [CreatedAtUtc] datetimeoffset NOT NULL,
        [UpdatedAtUtc] datetimeoffset NOT NULL,
        [RowVersion] rowversion NULL,
        CONSTRAINT [PK_Destinations] PRIMARY KEY ([Id])
    );
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261108193342_InitialCreate'
)
BEGIN
    CREATE TABLE [EncryptionProfiles] (
        [Id] int NOT NULL IDENTITY,
        [Name] nvarchar(200) NOT NULL,
        [Algorithm] nvarchar(50) NOT NULL,
        [KeyRef] nvarchar(500) NOT NULL,
        [CreatedAtUtc] datetimeoffset NOT NULL,
        [UpdatedAtUtc] datetimeoffset NOT NULL,
        CONSTRAINT [PK_EncryptionProfiles] PRIMARY KEY ([Id])
    );
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261108193342_InitialCreate'
)
BEGIN
    CREATE TABLE [RequestLogs] (
        [Id] bigint NOT NULL IDENTITY,
        [CorrelationId] nvarchar(100) NOT NULL,
        [OperationKey] nvarchar(200) NULL,
        [DestinationId] int NULL,
        [StartedAtUtc] datetimeoffset NOT NULL,
        [CompletedAtUtc] datetimeoffset NULL,
        [DurationMs] bigint NOT NULL,
        [ForwardingOccurred] bit NOT NULL,
        [UpstreamStatusCode] int NULL,
        [ProxyStatusCode] int NOT NULL,
        [Outcome] int NOT NULL,
        [ReasonCode] nvarchar(100) NULL,
        CONSTRAINT [PK_RequestLogs] PRIMARY KEY ([Id])
    );
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261108193342_InitialCreate'
)
BEGIN
    CREATE TABLE [Operations] (
        [Id] int NOT NULL IDENTITY,
        [OperationKey] nvarchar(200) NOT NULL,
        [DestinationId] int NOT NULL,
        [HttpMethod] nvarchar(10) NOT NULL,
        [RelativePathTemplate] nvarchar(2000) NOT NULL,
        [IsActive] bit NOT NULL,
        [MaxRequestBytes] bigint NOT NULL,
        [MaxResponseBytes] bigint NOT NULL,
        [EncryptionRequired] bit NOT NULL,
        [EncryptionProfileId] int NULL,
        [RejectUnknownParameters] bit NOT NULL,
        [CreatedAtUtc] datetimeoffset NOT NULL,
        [UpdatedAtUtc] datetimeoffset NOT NULL,
        [RowVersion] rowversion NULL,
        CONSTRAINT [PK_Operations] PRIMARY KEY ([Id]),
        CONSTRAINT [FK_Operations_Destinations_DestinationId] FOREIGN KEY ([DestinationId]) REFERENCES [Destinations] ([Id]) ON DELETE NO ACTION,
        CONSTRAINT [FK_Operations_EncryptionProfiles_EncryptionProfileId] FOREIGN KEY ([EncryptionProfileId]) REFERENCES [EncryptionProfiles] ([Id]) ON DELETE NO ACTION
    );
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261108193342_InitialCreate'
)
BEGIN
    CREATE TABLE [HeaderRules] (
        [Id] int NOT NULL IDENTITY,
        [OperationId] int NOT NULL,
        [HeaderName] nvarchar(200) NOT NULL,
        CONSTRAINT [PK_HeaderRules] PRIMARY KEY ([Id]),
        CONSTRAINT [FK_HeaderRules_Operations_OperationId] FOREIGN KEY ([OperationId]) REFERENCES [Operations] ([Id]) ON DELETE CASCADE
    );
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261108193342_InitialCreate'
)
BEGIN
    CREATE TABLE [ParameterRules] (
        [Id] int NOT NULL IDENTITY,
        [OperationId] int NOT NULL,
        [Name] nvarchar(200) NOT NULL,
        [Required] bit NOT NULL,
        [DataType] int NOT NULL,
        [Location] int NOT NULL,
        [MaxLength] int NULL,
        [MinValue] bigint NULL,
        [MaxValue] bigint NULL,
        [Pattern] nvarchar(500) NULL,
        CONSTRAINT [PK_ParameterRules] PRIMARY KEY ([Id]),
        CONSTRAINT [FK_ParameterRules_Operations_OperationId] FOREIGN KEY ([OperationId]) REFERENCES [Operations] ([Id]) ON DELETE CASCADE
    );
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261108193342_InitialCreate'
)
BEGIN
    CREATE UNIQUE INDEX [IX_Destinations_Name] ON [Destinations] ([Name]);
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261108193342_InitialCreate'
)
BEGIN
    CREATE UNIQUE INDEX [IX_EncryptionProfiles_Name] ON [EncryptionProfiles] ([Name]);
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261108193342_InitialCreate'
)
BEGIN
    CREATE UNIQUE INDEX [IX_HeaderRules_OperationId_HeaderName] ON [HeaderRules] ([OperationId], [HeaderName]);
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261108193342_InitialCreate'
)
BEGIN
    CREATE INDEX [IX_Operations_DestinationId] ON [Operations] ([DestinationId]);
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261108193342_InitialCreate'
)
BEGIN
    CREATE INDEX [IX_Operations_EncryptionProfileId] ON [Operations] ([EncryptionProfileId]);
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261108193342_InitialCreate'
)
BEGIN
    CREATE UNIQUE INDEX [IX_Operations_OperationKey] ON [Operations] ([OperationKey]);
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261108193342_InitialCreate'
)
BEGIN
    CREATE UNIQUE INDEX [IX_ParameterRules_OperationId_Name] ON [ParameterRules] ([OperationId], [Name]);
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261108193342_InitialCreate'
)
BEGIN
    CREATE INDEX [IX_RequestLogs_CorrelationId] ON [RequestLogs] ([CorrelationId]);
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261108193342_InitialCreate'
)
BEGIN
    CREATE INDEX [IX_RequestLogs_StartedAtUtc] ON [RequestLogs] ([StartedAtUtc]);
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261108193342_InitialCreate'
)
BEGIN
    INSERT INTO [__EFMigrationsHistory] ([MigrationId], [ProductVersion])
    VALUES (N'20261108193342_InitialCreate', N'8.0.10');
END;
GO

COMMIT;
GO

