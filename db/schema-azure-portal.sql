-- PhishGuard schema for Azure SQL (Portal Query editor friendly: single batch, no GO).
-- Generated from EF Core migrations: dotnet ef migrations script --idempotent --project src/PhishGuard.Data
-- Safe to run more than once: each migration is skipped if already applied.

IF OBJECT_ID(N'[__EFMigrationsHistory]') IS NULL
BEGIN
    CREATE TABLE [__EFMigrationsHistory] (
        [MigrationId] nvarchar(150) NOT NULL,
        [ProductVersion] nvarchar(32) NOT NULL,
        CONSTRAINT [PK___EFMigrationsHistory] PRIMARY KEY ([MigrationId])
    );
END;
BEGIN TRANSACTION;
IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261023125823_InitialSchema'
)
BEGIN
    CREATE TABLE [Brands] (
        [Id] int NOT NULL,
        [Name] nvarchar(100) NOT NULL,
        [PrimaryDomain] nvarchar(253) NOT NULL,
        [Keywords] nvarchar(500) NOT NULL,
        [OwnedDomains] nvarchar(2000) NOT NULL,
        CONSTRAINT [PK_Brands] PRIMARY KEY ([Id])
    );
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261023125823_InitialSchema'
)
BEGIN
    CREATE TABLE [DomainEvidence] (
        [RegisteredDomain] nvarchar(253) NOT NULL,
        [CreatedOnUtc] datetime2 NULL,
        [Registrar] nvarchar(200) NULL,
        [RegistrarAbuseEmail] nvarchar(254) NULL,
        [RegistrantCountry] nvarchar(2) NULL,
        [PrivacyProtected] bit NOT NULL,
        [HostingProvider] nvarchar(200) NULL,
        [HostingAbuseEmail] nvarchar(254) NULL,
        [CertIssuer] nvarchar(200) NULL,
        [CertIssuedOnUtc] datetime2 NULL,
        [PageTitle] nvarchar(500) NULL,
        [HasLoginForm] bit NOT NULL,
        [BrandLogoDetected] bit NOT NULL,
        [ScreenshotRef] nvarchar(500) NULL,
        CONSTRAINT [PK_DomainEvidence] PRIMARY KEY ([RegisteredDomain])
    );
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261023125823_InitialSchema'
)
BEGIN
    CREATE TABLE [Campaigns] (
        [Id] bigint NOT NULL IDENTITY,
        [GroupingKey] nvarchar(600) NOT NULL,
        [RegisteredDomain] nvarchar(253) NOT NULL,
        [PathPattern] nvarchar(300) NULL,
        [BrandId] int NULL,
        [UrlCount] int NOT NULL,
        [MaxProbability] real NOT NULL,
        [CreatedAtUtc] datetime2 NOT NULL,
        CONSTRAINT [PK_Campaigns] PRIMARY KEY ([Id]),
        CONSTRAINT [FK_Campaigns_Brands_BrandId] FOREIGN KEY ([BrandId]) REFERENCES [Brands] ([Id]) ON DELETE SET NULL
    );
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261023125823_InitialSchema'
)
BEGIN
    CREATE TABLE [Urls] (
        [Id] bigint NOT NULL IDENTITY,
        [Url] nvarchar(2048) NOT NULL,
        [Host] nvarchar(253) NOT NULL,
        [RegisteredDomain] nvarchar(253) NOT NULL,
        [Tld] nvarchar(63) NOT NULL,
        [Source] nvarchar(40) NOT NULL,
        [Category] nvarchar(40) NOT NULL,
        [IsPhishing] bit NOT NULL,
        [TargetBrandId] int NULL,
        [IngestedAtUtc] datetime2 NOT NULL,
        CONSTRAINT [PK_Urls] PRIMARY KEY ([Id]),
        CONSTRAINT [FK_Urls_Brands_TargetBrandId] FOREIGN KEY ([TargetBrandId]) REFERENCES [Brands] ([Id]) ON DELETE SET NULL
    );
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261023125823_InitialSchema'
)
BEGIN
    CREATE TABLE [Cases] (
        [Id] bigint NOT NULL IDENTITY,
        [CampaignId] bigint NOT NULL,
        [Status] nvarchar(40) NOT NULL,
        [Verdict] nvarchar(40) NOT NULL,
        [Confidence] real NULL,
        [Rationale] nvarchar(max) NULL,
        [CreatedAtUtc] datetime2 NOT NULL,
        [UpdatedAtUtc] datetime2 NOT NULL,
        CONSTRAINT [PK_Cases] PRIMARY KEY ([Id]),
        CONSTRAINT [FK_Cases_Campaigns_CampaignId] FOREIGN KEY ([CampaignId]) REFERENCES [Campaigns] ([Id]) ON DELETE CASCADE
    );
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261023125823_InitialSchema'
)
BEGIN
    CREATE TABLE [UrlScores] (
        [Id] bigint NOT NULL IDENTITY,
        [UrlId] bigint NOT NULL,
        [ModelVersion] nvarchar(50) NOT NULL,
        [Probability] real NOT NULL,
        [Band] nvarchar(40) NOT NULL,
        [CampaignId] bigint NULL,
        [ScoredAtUtc] datetime2 NOT NULL,
        CONSTRAINT [PK_UrlScores] PRIMARY KEY ([Id]),
        CONSTRAINT [FK_UrlScores_Campaigns_CampaignId] FOREIGN KEY ([CampaignId]) REFERENCES [Campaigns] ([Id]) ON DELETE SET NULL,
        CONSTRAINT [FK_UrlScores_Urls_UrlId] FOREIGN KEY ([UrlId]) REFERENCES [Urls] ([Id]) ON DELETE CASCADE
    );
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261023125823_InitialSchema'
)
BEGIN
    CREATE TABLE [AgentRuns] (
        [Id] bigint NOT NULL IDENTITY,
        [CaseId] bigint NOT NULL,
        [AgentName] nvarchar(100) NOT NULL,
        [Input] nvarchar(max) NULL,
        [Output] nvarchar(max) NULL,
        [TokensUsed] int NULL,
        [StartedAtUtc] datetime2 NOT NULL,
        [CompletedAtUtc] datetime2 NULL,
        CONSTRAINT [PK_AgentRuns] PRIMARY KEY ([Id]),
        CONSTRAINT [FK_AgentRuns_Cases_CaseId] FOREIGN KEY ([CaseId]) REFERENCES [Cases] ([Id]) ON DELETE CASCADE
    );
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261023125823_InitialSchema'
)
BEGIN
    CREATE TABLE [AnalystDecisions] (
        [Id] bigint NOT NULL IDENTITY,
        [CaseId] bigint NOT NULL,
        [Analyst] nvarchar(100) NOT NULL,
        [Decision] nvarchar(40) NOT NULL,
        [Notes] nvarchar(max) NULL,
        [DecidedAtUtc] datetime2 NOT NULL,
        CONSTRAINT [PK_AnalystDecisions] PRIMARY KEY ([Id]),
        CONSTRAINT [FK_AnalystDecisions_Cases_CaseId] FOREIGN KEY ([CaseId]) REFERENCES [Cases] ([Id]) ON DELETE CASCADE
    );
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261023125823_InitialSchema'
)
BEGIN
    CREATE TABLE [EnforcementNotices] (
        [Id] bigint NOT NULL IDENTITY,
        [CaseId] bigint NOT NULL,
        [Route] nvarchar(40) NOT NULL,
        [Recipient] nvarchar(254) NOT NULL,
        [Subject] nvarchar(300) NOT NULL,
        [Body] nvarchar(max) NOT NULL,
        [Status] nvarchar(40) NOT NULL,
        [CreatedAtUtc] datetime2 NOT NULL,
        [FiledAtUtc] datetime2 NULL,
        CONSTRAINT [PK_EnforcementNotices] PRIMARY KEY ([Id]),
        CONSTRAINT [FK_EnforcementNotices_Cases_CaseId] FOREIGN KEY ([CaseId]) REFERENCES [Cases] ([Id]) ON DELETE CASCADE
    );
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261023125823_InitialSchema'
)
BEGIN
    CREATE TABLE [MonitorChecks] (
        [Id] bigint NOT NULL IDENTITY,
        [CaseId] bigint NOT NULL,
        [CheckedAtUtc] datetime2 NOT NULL,
        [IsActive] bit NOT NULL,
        [Notes] nvarchar(max) NULL,
        CONSTRAINT [PK_MonitorChecks] PRIMARY KEY ([Id]),
        CONSTRAINT [FK_MonitorChecks_Cases_CaseId] FOREIGN KEY ([CaseId]) REFERENCES [Cases] ([Id]) ON DELETE CASCADE
    );
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261023125823_InitialSchema'
)
BEGIN
    IF EXISTS (SELECT * FROM [sys].[identity_columns] WHERE [name] IN (N'Id', N'Keywords', N'Name', N'OwnedDomains', N'PrimaryDomain') AND [object_id] = OBJECT_ID(N'[Brands]'))
        SET IDENTITY_INSERT [Brands] ON;
    EXEC(N'INSERT INTO [Brands] ([Id], [Keywords], [Name], [OwnedDomains], [PrimaryDomain])
    VALUES (1, N''paypal'', N''PayPal'', N''paypal.com,paypal.me,paypalobjects.com'', N''paypal.com''),
    (2, N''microsoft,office365,outlook,onedrive,msft'', N''Microsoft'', N''microsoft.com,microsoftonline.com,office.com,live.com,outlook.com,sharepoint.com,onedrive.live.com'', N''microsoft.com''),
    (3, N''apple,icloud,appleid'', N''Apple'', N''apple.com,icloud.com'', N''apple.com''),
    (4, N''amazon,aws,prime'', N''Amazon'', N''amazon.com,amazonaws.com,media-amazon.com'', N''amazon.com''),
    (5, N''netflix'', N''Netflix'', N''netflix.com,nflxext.com'', N''netflix.com''),
    (6, N''google,gmail'', N''Google'', N''google.com,gmail.com,googleusercontent.com,gstatic.com'', N''google.com''),
    (7, N''chase,jpmorgan'', N''Chase'', N''chase.com,jpmorganchase.com'', N''chase.com''),
    (8, N''wellsfargo,wells-fargo'', N''Wells Fargo'', N''wellsfargo.com'', N''wellsfargo.com''),
    (9, N''bankofamerica,bofa'', N''Bank of America'', N''bankofamerica.com'', N''bankofamerica.com''),
    (10, N''dhl'', N''DHL'', N''dhl.com'', N''dhl.com''),
    (11, N''fedex'', N''FedEx'', N''fedex.com'', N''fedex.com''),
    (12, N''linkedin'', N''LinkedIn'', N''linkedin.com,licdn.com'', N''linkedin.com''),
    (13, N''facebook,meta,instagram'', N''Facebook'', N''facebook.com,fb.com,instagram.com,meta.com'', N''facebook.com''),
    (14, N''dropbox'', N''Dropbox'', N''dropbox.com'', N''dropbox.com''),
    (15, N''docusign'', N''DocuSign'', N''docusign.com,docusign.net'', N''docusign.com''),
    (16, N''adobe'', N''Adobe'', N''adobe.com'', N''adobe.com''),
    (17, N''coinbase'', N''Coinbase'', N''coinbase.com'', N''coinbase.com''),
    (18, N''usps'', N''USPS'', N''usps.com'', N''usps.com'')');
    IF EXISTS (SELECT * FROM [sys].[identity_columns] WHERE [name] IN (N'Id', N'Keywords', N'Name', N'OwnedDomains', N'PrimaryDomain') AND [object_id] = OBJECT_ID(N'[Brands]'))
        SET IDENTITY_INSERT [Brands] OFF;
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261023125823_InitialSchema'
)
BEGIN
    CREATE INDEX [IX_AgentRuns_CaseId] ON [AgentRuns] ([CaseId]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261023125823_InitialSchema'
)
BEGIN
    CREATE INDEX [IX_AnalystDecisions_CaseId] ON [AnalystDecisions] ([CaseId]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261023125823_InitialSchema'
)
BEGIN
    CREATE INDEX [IX_Campaigns_BrandId] ON [Campaigns] ([BrandId]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261023125823_InitialSchema'
)
BEGIN
    CREATE UNIQUE INDEX [IX_Campaigns_GroupingKey] ON [Campaigns] ([GroupingKey]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261023125823_InitialSchema'
)
BEGIN
    CREATE INDEX [IX_Cases_CampaignId] ON [Cases] ([CampaignId]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261023125823_InitialSchema'
)
BEGIN
    CREATE INDEX [IX_Cases_Status] ON [Cases] ([Status]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261023125823_InitialSchema'
)
BEGIN
    CREATE INDEX [IX_EnforcementNotices_CaseId] ON [EnforcementNotices] ([CaseId]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261023125823_InitialSchema'
)
BEGIN
    CREATE INDEX [IX_MonitorChecks_CaseId] ON [MonitorChecks] ([CaseId]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261023125823_InitialSchema'
)
BEGIN
    CREATE INDEX [IX_Urls_Category_IsPhishing] ON [Urls] ([Category], [IsPhishing]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261023125823_InitialSchema'
)
BEGIN
    CREATE INDEX [IX_Urls_RegisteredDomain] ON [Urls] ([RegisteredDomain]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261023125823_InitialSchema'
)
BEGIN
    CREATE INDEX [IX_Urls_TargetBrandId] ON [Urls] ([TargetBrandId]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261023125823_InitialSchema'
)
BEGIN
    CREATE INDEX [IX_UrlScores_CampaignId] ON [UrlScores] ([CampaignId]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261023125823_InitialSchema'
)
BEGIN
    CREATE INDEX [IX_UrlScores_ModelVersion_Band] ON [UrlScores] ([ModelVersion], [Band]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261023125823_InitialSchema'
)
BEGIN
    CREATE INDEX [IX_UrlScores_UrlId] ON [UrlScores] ([UrlId]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261023125823_InitialSchema'
)
BEGIN
    INSERT INTO [__EFMigrationsHistory] ([MigrationId], [ProductVersion])
    VALUES (N'20261023125823_InitialSchema', N'9.0.21');
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261023154553_UniqueScorePerModel'
)
BEGIN
    DROP INDEX [IX_UrlScores_UrlId] ON [UrlScores];
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261023154553_UniqueScorePerModel'
)
BEGIN
    CREATE UNIQUE INDEX [IX_UrlScores_UrlId_ModelVersion] ON [UrlScores] ([UrlId], [ModelVersion]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261023154553_UniqueScorePerModel'
)
BEGIN
    INSERT INTO [__EFMigrationsHistory] ([MigrationId], [ProductVersion])
    VALUES (N'20261023154553_UniqueScorePerModel', N'9.0.21');
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261027144342_AddShortLinks'
)
BEGIN
    CREATE TABLE [ShortLinks] (
        [ShortUrl] nvarchar(400) NOT NULL,
        [ExpandedUrl] nvarchar(2048) NOT NULL,
        [ExpandedAtUtc] datetime2 NOT NULL,
        CONSTRAINT [PK_ShortLinks] PRIMARY KEY ([ShortUrl])
    );
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261027144342_AddShortLinks'
)
BEGIN
    INSERT INTO [__EFMigrationsHistory] ([MigrationId], [ProductVersion])
    VALUES (N'20261027144342_AddShortLinks', N'9.0.21');
END;

COMMIT;
