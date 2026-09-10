-- =============================================
-- QDVapp - Schéma de la base de données
-- Modèle SQL Server (documentation)
-- =============================================

-- =============================================
-- MIGRATIONS EF CORE
-- =============================================

CREATE TABLE [dbo].[__EFMigrationsHistory] (
    [MigrationId]    nvarchar(150) NOT NULL,
    [ProductVersion] nvarchar(32)  NOT NULL,
    PRIMARY KEY ([MigrationId])
);

-- =============================================
-- ASP.NET IDENTITY - ROLES
-- =============================================

CREATE TABLE [dbo].[AspNetRoles] (
    [Id]               nvarchar(450) NOT NULL,
    [Name]             nvarchar(256),
    [NormalizedName]   nvarchar(256),
    [ConcurrencyStamp] nvarchar(max),
    PRIMARY KEY ([Id])
);

CREATE TABLE [dbo].[AspNetRoleClaims] (
    [Id]         int NOT NULL IDENTITY(1,1),
    [RoleId]     nvarchar(450) NOT NULL,
    [ClaimType]  nvarchar(max),
    [ClaimValue] nvarchar(max),
    PRIMARY KEY ([Id]),
    CONSTRAINT [FK_AspNetRoleClaims_AspNetRoles_RoleId] FOREIGN KEY ([RoleId]) REFERENCES [dbo].[AspNetRoles]([Id]) ON DELETE CASCADE
);

-- =============================================
-- ASP.NET IDENTITY - UTILISATEURS
-- =============================================

CREATE TABLE [dbo].[AspNetUsers] (
    [Id]                   nvarchar(450) NOT NULL,
    [AccessFailedCount]    int NOT NULL,
    [ConcurrencyStamp]     nvarchar(max),
    [Email]                nvarchar(256),
    [EmailConfirmed]       bit NOT NULL,
    [LockoutEnabled]       bit NOT NULL,
    [LockoutEnd]           datetimeoffset,
    [NormalizedEmail]      nvarchar(256),
    [NormalizedUserName]   nvarchar(256),
    [PasswordHash]         nvarchar(max),
    [PhoneNumber]          nvarchar(max),
    [PhoneNumberConfirmed] bit NOT NULL,
    [SecurityStamp]        nvarchar(max),
    [TwoFactorEnabled]     bit NOT NULL,
    [UserName]             nvarchar(256),
    [Theme]                bit,
    PRIMARY KEY ([Id])
);

CREATE TABLE [dbo].[AspNetUserClaims] (
    [Id]         int NOT NULL IDENTITY(1,1),
    [UserId]     nvarchar(450) NOT NULL,
    [ClaimType]  nvarchar(max),
    [ClaimValue] nvarchar(max),
    PRIMARY KEY ([Id]),
    CONSTRAINT [FK_AspNetUserClaims_AspNetUsers_UserId] FOREIGN KEY ([UserId]) REFERENCES [dbo].[AspNetUsers]([Id]) ON DELETE CASCADE
);

CREATE TABLE [dbo].[AspNetUserLogins] (
    [LoginProvider]       nvarchar(450) NOT NULL,
    [ProviderKey]         nvarchar(450) NOT NULL,
    [ProviderDisplayName] nvarchar(max),
    [UserId]              nvarchar(450) NOT NULL,
    PRIMARY KEY ([LoginProvider], [ProviderKey]),
    CONSTRAINT [FK_AspNetUserLogins_AspNetUsers_UserId] FOREIGN KEY ([UserId]) REFERENCES [dbo].[AspNetUsers]([Id]) ON DELETE CASCADE
);

CREATE TABLE [dbo].[AspNetUserRoles] (
    [UserId] nvarchar(450) NOT NULL,
    [RoleId] nvarchar(450) NOT NULL,
    PRIMARY KEY ([UserId], [RoleId]),
    CONSTRAINT [FK_AspNetUserRoles_AspNetRoles_RoleId] FOREIGN KEY ([RoleId]) REFERENCES [dbo].[AspNetRoles]([Id]) ON DELETE CASCADE,
    CONSTRAINT [FK_AspNetUserRoles_AspNetUsers_UserId] FOREIGN KEY ([UserId]) REFERENCES [dbo].[AspNetUsers]([Id]) ON DELETE CASCADE
);

CREATE TABLE [dbo].[AspNetUserTokens] (
    [UserId]        nvarchar(450) NOT NULL,
    [LoginProvider] nvarchar(450) NOT NULL,
    [Name]          nvarchar(450) NOT NULL,
    [Value]         nvarchar(max),
    PRIMARY KEY ([UserId], [LoginProvider], [Name]),
    CONSTRAINT [FK_AspNetUserTokens_AspNetUsers_UserId] FOREIGN KEY ([UserId]) REFERENCES [dbo].[AspNetUsers]([Id]) ON DELETE CASCADE
);

-- =============================================
-- PROJETS
-- =============================================

CREATE TABLE [dbo].[Projets] (
    [Id]                  uniqueidentifier NOT NULL,
    [Statut]              varchar(30),
    [NoCmd]               varchar(50),
    [RefOF]               varchar(50),
    [CodeArticle]         varchar(50),
    [Description]         varchar(500),
    [CommentaireOF]       text,
    [Vendeur]             varchar(100),
    [DateRequise]         date,
    [JrsSTPlanifie]       decimal(6,2),
    [Planifie]            decimal(12,2),
    [Fait]                decimal(12,2),
    [DeclAv]              decimal(12,2),
    [Restant]             decimal(12,2),
    [TempsTotalFinEstime] decimal(12,2),
    [Diff]                varchar(50),
    [PctDiff]             varchar(50),
    [NoteFab]             text,
    [Manuel]              bit NOT NULL DEFAULT (0),
    PRIMARY KEY ([Id])
);

-- =============================================
-- ORDRES DE FABRICATION (saisie manuelle)
-- =============================================

CREATE TABLE [dbo].[ManuelOFs] (
    [Id]                    uniqueidentifier NOT NULL,
    [Projet]                varchar(50),
    [RefOF]                 varchar(50),
    [StatutOF]              varchar(30),
    [DescriptionArticle]    varchar(255),
    [Priorite]              int,
    [RequisFab]             date,
    [RequisFinal]           date,
    [Scie]                  decimal(12,2),
    [RobotPlasma]           decimal(12,2),
    [LaserTube]             decimal(12,2),
    [TablePlasma]           decimal(12,2),
    [TableLaser]            decimal(12,2),
    [Pliage]                decimal(12,2),
    [Roulage]               decimal(12,2),
    [Machinage]             decimal(12,2),
    [STMachine]             decimal(12,2),
    [Montage]               decimal(12,2),
    [TuyauterieMontage]     decimal(12,2),
    [Soudage]               decimal(12,2),
    [TuyauterieSoudage]     decimal(12,2),
    [SoudageRobot]          decimal(12,2),
    [STMontageSoudage]      decimal(12,2),
    [Inspection]            decimal(12,2),
    [STInspection]          decimal(12,2),
    [Reparation]            decimal(12,2),
    [Peinture]              decimal(12,2),
    [STPeinture]            decimal(12,2),
    [Emballage]             decimal(12,2),
    [CommentaireInspection] text,
    [CommentaireOF]         text,
    [Manuel]                bit NOT NULL DEFAULT (1),
    PRIMARY KEY ([Id])
);

-- =============================================
-- CORRECTIONS MANUELLES
-- =============================================

CREATE TABLE [dbo].[Corrections] (
    [Id]         int NOT NULL IDENTITY(1,1),
    [Page]       varchar(20)  NOT NULL,
    [ProjectKey] varchar(50)  NOT NULL,
    [Field]      varchar(50)  NOT NULL,
    [Value]      varchar(100) NOT NULL,
    PRIMARY KEY ([Id]),
    CONSTRAINT [UX_Corrections] UNIQUE ([Page], [ProjectKey], [Field])
);

-- =============================================
-- INDEXES
-- =============================================

CREATE UNIQUE INDEX [RoleNameIndex]               ON [dbo].[AspNetRoles]([NormalizedName]);
CREATE INDEX [IX_AspNetRoleClaims_RoleId]         ON [dbo].[AspNetRoleClaims]([RoleId]);

CREATE INDEX [EmailIndex]                         ON [dbo].[AspNetUsers]([NormalizedEmail]);
CREATE UNIQUE INDEX [UserNameIndex]               ON [dbo].[AspNetUsers]([NormalizedUserName]);
CREATE INDEX [IX_AspNetUserClaims_UserId]         ON [dbo].[AspNetUserClaims]([UserId]);
CREATE INDEX [IX_AspNetUserLogins_UserId]         ON [dbo].[AspNetUserLogins]([UserId]);
CREATE INDEX [IX_AspNetUserRoles_RoleId]          ON [dbo].[AspNetUserRoles]([RoleId]);