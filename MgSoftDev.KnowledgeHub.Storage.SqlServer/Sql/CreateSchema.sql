-- ============================================================================
-- MgSoftDev.KnowledgeHub - SQL Server schema (idempotent).
-- Placeholders {{SCHEMA}} / {{PREFIX}} are replaced at runtime by
-- KnowledgeHubSqlSchema.GetCreateScript(). Safe to run repeatedly.
-- DB defaults (NEWSEQUENTIALID/GETDATE) are a safety net for manual inserts:
-- the library always sends client-generated values.
-- ============================================================================

IF NOT EXISTS (SELECT 1 FROM sys.schemas WHERE name = N'{{SCHEMA}}')
    EXEC (N'CREATE SCHEMA [{{SCHEMA}}]');

-- ---------------------------------------------------------------- DocPages
IF OBJECT_ID(N'[{{SCHEMA}}].[{{PREFIX}}DocPages]', N'U') IS NULL
BEGIN
    CREATE TABLE [{{SCHEMA}}].[{{PREFIX}}DocPages]
    (
        Pk                          UNIQUEIDENTIFIER NOT NULL CONSTRAINT DF_{{PREFIX}}DocPages_Pk DEFAULT NEWSEQUENTIALID(),
        Fk_DocPageParent            UNIQUEIDENTIFIER NULL,
        Fk_DocPageVersionPublished  UNIQUEIDENTIFIER NULL,
        Slug                        NVARCHAR(200) NOT NULL,
        Title                       NVARCHAR(300) NOT NULL,
        SortOrder                   INT NOT NULL CONSTRAINT DF_{{PREFIX}}DocPages_SortOrder DEFAULT (0),
        IsPublic                    BIT NOT NULL CONSTRAINT DF_{{PREFIX}}DocPages_IsPublic DEFAULT (0),
        RowIsActive                 BIT NOT NULL CONSTRAINT DF_{{PREFIX}}DocPages_RowIsActive DEFAULT (1),
        RowCreateDate               DATETIME2 NOT NULL CONSTRAINT DF_{{PREFIX}}DocPages_RowCreateDate DEFAULT (GETDATE()),
        RowUpdateDate               DATETIME2 NOT NULL CONSTRAINT DF_{{PREFIX}}DocPages_RowUpdateDate DEFAULT (GETDATE()),
        RowUserCreate               NVARCHAR(64) NULL,
        RowUserUpdate               NVARCHAR(64) NULL,
        CONSTRAINT PK_{{PREFIX}}DocPages PRIMARY KEY (Pk)
    );

    CREATE INDEX IX_{{PREFIX}}DocPages_Fk_DocPageParent
        ON [{{SCHEMA}}].[{{PREFIX}}DocPages] (Fk_DocPageParent);
    CREATE UNIQUE INDEX UQ_{{PREFIX}}DocPages_Slug
        ON [{{SCHEMA}}].[{{PREFIX}}DocPages] (Slug);
END;

-- ---------------------------------------------------------------- DocPageVersions
IF OBJECT_ID(N'[{{SCHEMA}}].[{{PREFIX}}DocPageVersions]', N'U') IS NULL
BEGIN
    CREATE TABLE [{{SCHEMA}}].[{{PREFIX}}DocPageVersions]
    (
        Pk              UNIQUEIDENTIFIER NOT NULL CONSTRAINT DF_{{PREFIX}}DocPageVersions_Pk DEFAULT NEWSEQUENTIALID(),
        Fk_DocPage      UNIQUEIDENTIFIER NOT NULL,
        VersionNumber   INT NOT NULL,
        Title           NVARCHAR(300) NOT NULL,
        ContentHtml     NVARCHAR(MAX) NOT NULL,
        Status          INT NOT NULL,
        ChangeNote      NVARCHAR(500) NULL,
        PublishedAt     DATETIME2 NULL,
        RowIsActive     BIT NOT NULL CONSTRAINT DF_{{PREFIX}}DocPageVersions_RowIsActive DEFAULT (1),
        RowCreateDate   DATETIME2 NOT NULL CONSTRAINT DF_{{PREFIX}}DocPageVersions_RowCreateDate DEFAULT (GETDATE()),
        RowUpdateDate   DATETIME2 NOT NULL CONSTRAINT DF_{{PREFIX}}DocPageVersions_RowUpdateDate DEFAULT (GETDATE()),
        RowUserCreate   NVARCHAR(64) NULL,
        RowUserUpdate   NVARCHAR(64) NULL,
        CONSTRAINT PK_{{PREFIX}}DocPageVersions PRIMARY KEY (Pk)
    );

    CREATE INDEX IX_{{PREFIX}}DocPageVersions_Fk_DocPage
        ON [{{SCHEMA}}].[{{PREFIX}}DocPageVersions] (Fk_DocPage);
    CREATE UNIQUE INDEX UQ_{{PREFIX}}DocPageVersions_Page_Version
        ON [{{SCHEMA}}].[{{PREFIX}}DocPageVersions] (Fk_DocPage, VersionNumber);
END;

-- ---------------------------------------------------------------- DocImages
IF OBJECT_ID(N'[{{SCHEMA}}].[{{PREFIX}}DocImages]', N'U') IS NULL
BEGIN
    CREATE TABLE [{{SCHEMA}}].[{{PREFIX}}DocImages]
    (
        Pk              UNIQUEIDENTIFIER NOT NULL CONSTRAINT DF_{{PREFIX}}DocImages_Pk DEFAULT NEWSEQUENTIALID(),
        FileName        NVARCHAR(300) NOT NULL,
        ContentHash     CHAR(64) NOT NULL,
        ContentType     NVARCHAR(100) NOT NULL,
        SizeBytes       BIGINT NOT NULL,
        Width           INT NOT NULL,
        Height          INT NOT NULL,
        RowIsActive     BIT NOT NULL CONSTRAINT DF_{{PREFIX}}DocImages_RowIsActive DEFAULT (1),
        RowCreateDate   DATETIME2 NOT NULL CONSTRAINT DF_{{PREFIX}}DocImages_RowCreateDate DEFAULT (GETDATE()),
        RowUpdateDate   DATETIME2 NOT NULL CONSTRAINT DF_{{PREFIX}}DocImages_RowUpdateDate DEFAULT (GETDATE()),
        RowUserCreate   NVARCHAR(64) NULL,
        RowUserUpdate   NVARCHAR(64) NULL,
        CONSTRAINT PK_{{PREFIX}}DocImages PRIMARY KEY (Pk)
    );

    CREATE INDEX IX_{{PREFIX}}DocImages_ContentHash
        ON [{{SCHEMA}}].[{{PREFIX}}DocImages] (ContentHash);
END;

-- ---------------------------------------------------------------- DocImageContents
IF OBJECT_ID(N'[{{SCHEMA}}].[{{PREFIX}}DocImageContents]', N'U') IS NULL
BEGIN
    CREATE TABLE [{{SCHEMA}}].[{{PREFIX}}DocImageContents]
    (
        Pk              UNIQUEIDENTIFIER NOT NULL CONSTRAINT DF_{{PREFIX}}DocImageContents_Pk DEFAULT NEWSEQUENTIALID(),
        Fk_DocImage     UNIQUEIDENTIFIER NOT NULL,
        Content         VARBINARY(MAX) NOT NULL,
        RowIsActive     BIT NOT NULL CONSTRAINT DF_{{PREFIX}}DocImageContents_RowIsActive DEFAULT (1),
        RowCreateDate   DATETIME2 NOT NULL CONSTRAINT DF_{{PREFIX}}DocImageContents_RowCreateDate DEFAULT (GETDATE()),
        RowUpdateDate   DATETIME2 NOT NULL CONSTRAINT DF_{{PREFIX}}DocImageContents_RowUpdateDate DEFAULT (GETDATE()),
        RowUserCreate   NVARCHAR(64) NULL,
        RowUserUpdate   NVARCHAR(64) NULL,
        CONSTRAINT PK_{{PREFIX}}DocImageContents PRIMARY KEY (Pk)
    );

    CREATE UNIQUE INDEX UQ_{{PREFIX}}DocImageContents_Fk_DocImage
        ON [{{SCHEMA}}].[{{PREFIX}}DocImageContents] (Fk_DocImage);
END;

-- ---------------------------------------------------------------- DocPages_DocImages
IF OBJECT_ID(N'[{{SCHEMA}}].[{{PREFIX}}DocPages_DocImages]', N'U') IS NULL
BEGIN
    CREATE TABLE [{{SCHEMA}}].[{{PREFIX}}DocPages_DocImages]
    (
        Pk              UNIQUEIDENTIFIER NOT NULL CONSTRAINT DF_{{PREFIX}}DocPages_DocImages_Pk DEFAULT NEWSEQUENTIALID(),
        Fk_DocPage      UNIQUEIDENTIFIER NOT NULL,
        Fk_DocImage     UNIQUEIDENTIFIER NOT NULL,
        RowIsActive     BIT NOT NULL CONSTRAINT DF_{{PREFIX}}DocPages_DocImages_RowIsActive DEFAULT (1),
        RowCreateDate   DATETIME2 NOT NULL CONSTRAINT DF_{{PREFIX}}DocPages_DocImages_RowCreateDate DEFAULT (GETDATE()),
        RowUpdateDate   DATETIME2 NOT NULL CONSTRAINT DF_{{PREFIX}}DocPages_DocImages_RowUpdateDate DEFAULT (GETDATE()),
        RowUserCreate   NVARCHAR(64) NULL,
        RowUserUpdate   NVARCHAR(64) NULL,
        CONSTRAINT PK_{{PREFIX}}DocPages_DocImages PRIMARY KEY (Pk)
    );

    CREATE UNIQUE INDEX UQ_{{PREFIX}}DocPages_DocImages_Page_Image
        ON [{{SCHEMA}}].[{{PREFIX}}DocPages_DocImages] (Fk_DocPage, Fk_DocImage);
END;

-- ---------------------------------------------------------------- DocPages_Permissions
IF OBJECT_ID(N'[{{SCHEMA}}].[{{PREFIX}}DocPages_Permissions]', N'U') IS NULL
BEGIN
    CREATE TABLE [{{SCHEMA}}].[{{PREFIX}}DocPages_Permissions]
    (
        Pk              UNIQUEIDENTIFIER NOT NULL CONSTRAINT DF_{{PREFIX}}DocPages_Permissions_Pk DEFAULT NEWSEQUENTIALID(),
        Fk_DocPage      UNIQUEIDENTIFIER NOT NULL,
        Permission      NVARCHAR(128) NOT NULL,
        RowIsActive     BIT NOT NULL CONSTRAINT DF_{{PREFIX}}DocPages_Permissions_RowIsActive DEFAULT (1),
        RowCreateDate   DATETIME2 NOT NULL CONSTRAINT DF_{{PREFIX}}DocPages_Permissions_RowCreateDate DEFAULT (GETDATE()),
        RowUpdateDate   DATETIME2 NOT NULL CONSTRAINT DF_{{PREFIX}}DocPages_Permissions_RowUpdateDate DEFAULT (GETDATE()),
        RowUserCreate   NVARCHAR(64) NULL,
        RowUserUpdate   NVARCHAR(64) NULL,
        CONSTRAINT PK_{{PREFIX}}DocPages_Permissions PRIMARY KEY (Pk)
    );

    CREATE UNIQUE INDEX UQ_{{PREFIX}}DocPages_Permissions_Page_Permission
        ON [{{SCHEMA}}].[{{PREFIX}}DocPages_Permissions] (Fk_DocPage, Permission);
END;

-- ---------------------------------------------------------------- Foreign keys
-- Added after all tables exist (DocPages <-> DocPageVersions is circular).

IF NOT EXISTS (SELECT 1 FROM sys.foreign_keys WHERE name = N'FK_{{PREFIX}}DocPages_DocPageParent')
    ALTER TABLE [{{SCHEMA}}].[{{PREFIX}}DocPages]
        ADD CONSTRAINT FK_{{PREFIX}}DocPages_DocPageParent
        FOREIGN KEY (Fk_DocPageParent) REFERENCES [{{SCHEMA}}].[{{PREFIX}}DocPages] (Pk) ON DELETE NO ACTION;

IF NOT EXISTS (SELECT 1 FROM sys.foreign_keys WHERE name = N'FK_{{PREFIX}}DocPages_DocPageVersionPublished')
    ALTER TABLE [{{SCHEMA}}].[{{PREFIX}}DocPages]
        ADD CONSTRAINT FK_{{PREFIX}}DocPages_DocPageVersionPublished
        FOREIGN KEY (Fk_DocPageVersionPublished) REFERENCES [{{SCHEMA}}].[{{PREFIX}}DocPageVersions] (Pk) ON DELETE NO ACTION;

IF NOT EXISTS (SELECT 1 FROM sys.foreign_keys WHERE name = N'FK_{{PREFIX}}DocPageVersions_{{PREFIX}}DocPages')
    ALTER TABLE [{{SCHEMA}}].[{{PREFIX}}DocPageVersions]
        ADD CONSTRAINT FK_{{PREFIX}}DocPageVersions_{{PREFIX}}DocPages
        FOREIGN KEY (Fk_DocPage) REFERENCES [{{SCHEMA}}].[{{PREFIX}}DocPages] (Pk) ON DELETE NO ACTION;

IF NOT EXISTS (SELECT 1 FROM sys.foreign_keys WHERE name = N'FK_{{PREFIX}}DocImageContents_{{PREFIX}}DocImages')
    ALTER TABLE [{{SCHEMA}}].[{{PREFIX}}DocImageContents]
        ADD CONSTRAINT FK_{{PREFIX}}DocImageContents_{{PREFIX}}DocImages
        FOREIGN KEY (Fk_DocImage) REFERENCES [{{SCHEMA}}].[{{PREFIX}}DocImages] (Pk) ON DELETE NO ACTION;

IF NOT EXISTS (SELECT 1 FROM sys.foreign_keys WHERE name = N'FK_{{PREFIX}}DocPages_DocImages_{{PREFIX}}DocPages')
    ALTER TABLE [{{SCHEMA}}].[{{PREFIX}}DocPages_DocImages]
        ADD CONSTRAINT FK_{{PREFIX}}DocPages_DocImages_{{PREFIX}}DocPages
        FOREIGN KEY (Fk_DocPage) REFERENCES [{{SCHEMA}}].[{{PREFIX}}DocPages] (Pk) ON DELETE NO ACTION;

IF NOT EXISTS (SELECT 1 FROM sys.foreign_keys WHERE name = N'FK_{{PREFIX}}DocPages_DocImages_{{PREFIX}}DocImages')
    ALTER TABLE [{{SCHEMA}}].[{{PREFIX}}DocPages_DocImages]
        ADD CONSTRAINT FK_{{PREFIX}}DocPages_DocImages_{{PREFIX}}DocImages
        FOREIGN KEY (Fk_DocImage) REFERENCES [{{SCHEMA}}].[{{PREFIX}}DocImages] (Pk) ON DELETE NO ACTION;

IF NOT EXISTS (SELECT 1 FROM sys.foreign_keys WHERE name = N'FK_{{PREFIX}}DocPages_Permissions_{{PREFIX}}DocPages')
    ALTER TABLE [{{SCHEMA}}].[{{PREFIX}}DocPages_Permissions]
        ADD CONSTRAINT FK_{{PREFIX}}DocPages_Permissions_{{PREFIX}}DocPages
        FOREIGN KEY (Fk_DocPage) REFERENCES [{{SCHEMA}}].[{{PREFIX}}DocPages] (Pk) ON DELETE NO ACTION;
