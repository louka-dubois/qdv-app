-- =============================================
-- QDVapp - Expanded Database Schema
-- =============================================

-- =============================================
-- EXISTING TABLES (with fixes)
-- =============================================

CREATE TABLE [dbo].[Clients] (
    [IdClient]          int NOT NULL IDENTITY(1,1),
    [NomClient]         varchar(100),
    [Courriel]          varchar(255),
    [Telephone]         varchar(30),
    [Adresse]           varchar(255),
    [Ville]             varchar(100),
    [Province]          varchar(50),
    [CodePostal]        varchar(10),
    [ContactPrincipal]  varchar(150),
    [DateCreation]      datetime DEFAULT GETDATE(),
    [Actif]             bit DEFAULT 1,
    PRIMARY KEY ([IdClient])
);

CREATE TABLE [dbo].[Projets] (
    [IdProjet]          int NOT NULL IDENTITY(1,1),
    [IdClient]          int NOT NULL,
    [Vendeur]           varchar(50),
    [DescriptionProjet] varchar(500),
    [DateRequise]       date,
    [DateCreation]      datetime DEFAULT GETDATE(),
    [DateFinPrevue]     date,
    [DateFinReelle]     date,
    [StatutCommande]    varchar(30),
    [Priorite]          varchar(20) DEFAULT 'Normale',
    [Budget]            decimal(12,2),
    [CommentaireProjet] text,
    PRIMARY KEY ([IdProjet])
);

CREATE TABLE [dbo].[OrdresFabrication] (
    [RefOF]             varchar(20) NOT NULL,
    [IdProjet]          int,
    [DescriptionArticle] varchar(255) NOT NULL,
    [StatutOF]          varchar(30) NOT NULL,
    [Priorite]          varchar(20) NOT NULL,
    [Quantite]          int DEFAULT 1,
    [Unite]             varchar(20) DEFAULT 'pcs',
    [CommentaireOF]     text,
    [DateRequiseFab]    date,
    [DateRequiseFinale] date,
    [DateCreation]      datetime DEFAULT GETDATE(),
    [DateDebut]         date,
    [DateFin]           date,
    [MachineAssignee]   varchar(100),
    PRIMARY KEY ([RefOF])
);

CREATE TABLE [dbo].[Phases] (
    [IdPhase]           int NOT NULL IDENTITY(1,1),
    [RefOF]             varchar(20),
    [DescriptionPhase]  varchar(255),
    [StatutPhase]       varchar(30),
    [OrdreSequence]     int DEFAULT 0,
    [TempsEstime]       decimal(6,2),
    [TempsDeclare]      decimal(6,2),
    [Ecart]             AS ([TempsDeclare] - [TempsEstime]),
    [CommentairePhase]  text,
    [Avancement]        int,
    [DateDebut]         datetime,
    [DateFin]           datetime,
    [Operateur]         varchar(100),
    [Machine]           varchar(100),
    PRIMARY KEY ([IdPhase]),
    CONSTRAINT [CK_Phases_Avancement] CHECK ([Avancement] >= 0 AND [Avancement] <= 100)
);

-- =============================================
-- FOREIGN KEY CONSTRAINTS
-- =============================================

ALTER TABLE [dbo].[Projets]
ADD CONSTRAINT [FK_Clients]
FOREIGN KEY ([IdClient]) 
REFERENCES [dbo].[Clients]([IdClient])
ON DELETE NO ACTION
ON UPDATE NO ACTION;

ALTER TABLE [dbo].[OrdresFabrication]
ADD CONSTRAINT [FK_Projets]
FOREIGN KEY ([IdProjet]) 
REFERENCES [dbo].[Projets]([IdProjet])
ON DELETE NO ACTION
ON UPDATE NO ACTION;

ALTER TABLE [dbo].[Phases]
ADD CONSTRAINT [FK_OrdresFabrication]
FOREIGN KEY ([RefOF]) 
REFERENCES [dbo].[OrdresFabrication]([RefOF])
ON DELETE CASCADE
ON UPDATE NO ACTION;

-- =============================================
-- NEW TABLES
-- =============================================

CREATE TABLE [dbo].[Employes] (
    [IdEmploye]     int NOT NULL IDENTITY(1,1),
    [Nom]           varchar(100) NOT NULL,
    [Prenom]        varchar(100) NOT NULL,
    [Poste]         varchar(100),
    [Departement]   varchar(100),
    [Courriel]      varchar(255),
    [Telephone]     varchar(30),
    [Actif]         bit DEFAULT 1,
    PRIMARY KEY ([IdEmploye])
);

CREATE TABLE [dbo].[Machines] (
    [IdMachine]      int NOT NULL IDENTITY(1,1),
    [NomMachine]     varchar(100) NOT NULL,
    [TypeMachine]    varchar(100),
    [Localisation]   varchar(100),
    [Statut]         varchar(30) DEFAULT 'Disponible',
    [CapaciteHebdo]  decimal(6,2),
    PRIMARY KEY ([IdMachine])
);

CREATE TABLE [dbo].[PostesTravail] (
    [IdPoste]       int NOT NULL IDENTITY(1,1),
    [NomPoste]      varchar(100) NOT NULL,
    [OrdrePassage]  int DEFAULT 0,
    [Actif]         bit DEFAULT 1,
    PRIMARY KEY ([IdPoste])
);

CREATE TABLE [dbo].[AvancementPoste] (
    [IdAvancement]      int NOT NULL IDENTITY(1,1),
    [RefOF]             varchar(20) NOT NULL,
    [IdPoste]           int NOT NULL,
    [Statut]            varchar(30) DEFAULT 'En attente',
    [QuantitePlanifiee] int DEFAULT 0,
    [QuantiteDeclaree]  int DEFAULT 0,
    [DateDebut]         datetime,
    [DateFin]           datetime,
    [Operateur]         varchar(100),
    [Commentaire]       text,
    PRIMARY KEY ([IdAvancement])
);

CREATE TABLE [dbo].[Historique] (
    [IdHistorique]      int NOT NULL IDENTITY(1,1),
    [TableSource]       varchar(50) NOT NULL,
    [IdEnregistrement]  varchar(50) NOT NULL,
    [Action]            varchar(20) NOT NULL,
    [AncienneValeur]    text,
    [NouvelleValeur]    text,
    [Utilisateur]       varchar(100),
    [DateAction]        datetime DEFAULT GETDATE(),
    PRIMARY KEY ([IdHistorique])
);

CREATE TABLE [dbo].[Documents] (
    [IdDocument]    int NOT NULL IDENTITY(1,1),
    [NomFichier]    varchar(255) NOT NULL,
    [CheminFichier] varchar(500) NOT NULL,
    [TypeFichier]   varchar(50),
    [TailleOctets]  bigint,
    [IdProjet]      int NULL,
    [RefOF]         varchar(20) NULL,
    [IdPhase]       int NULL,
    [DateUpload]    datetime DEFAULT GETDATE(),
    [UploadePar]    varchar(100),
    PRIMARY KEY ([IdDocument])
);

-- =============================================
-- FOREIGN KEYS FOR NEW TABLES
-- =============================================

ALTER TABLE [dbo].[AvancementPoste]
ADD CONSTRAINT [FK_AvancementPoste_OF]
FOREIGN KEY ([RefOF]) REFERENCES [dbo].[OrdresFabrication]([RefOF]);

ALTER TABLE [dbo].[AvancementPoste]
ADD CONSTRAINT [FK_AvancementPoste_Poste]
FOREIGN KEY ([IdPoste]) REFERENCES [dbo].[PostesTravail]([IdPoste]);

ALTER TABLE [dbo].[Documents]
ADD CONSTRAINT [FK_Documents_Projets]
FOREIGN KEY ([IdProjet]) REFERENCES [dbo].[Projets]([IdProjet]);

ALTER TABLE [dbo].[Documents]
ADD CONSTRAINT [FK_Documents_OF]
FOREIGN KEY ([RefOF]) REFERENCES [dbo].[OrdresFabrication]([RefOF]);

ALTER TABLE [dbo].[Documents]
ADD CONSTRAINT [FK_Documents_Phases]
FOREIGN KEY ([IdPhase]) REFERENCES [dbo].[Phases]([IdPhase]);

-- =============================================
-- INDEXES
-- =============================================

CREATE INDEX [IX_Projets_IdClient]       ON [dbo].[Projets]([IdClient]);
CREATE INDEX [IX_Projets_Statut]         ON [dbo].[Projets]([StatutCommande]);
CREATE INDEX [IX_Projets_DateRequise]    ON [dbo].[Projets]([DateRequise]);

CREATE INDEX [IX_OF_IdProjet]            ON [dbo].[OrdresFabrication]([IdProjet]);
CREATE INDEX [IX_OF_Statut]              ON [dbo].[OrdresFabrication]([StatutOF]);
CREATE INDEX [IX_OF_DateRequise]         ON [dbo].[OrdresFabrication]([DateRequiseFinale]);

CREATE INDEX [IX_Phases_RefOF]           ON [dbo].[Phases]([RefOF]);
CREATE INDEX [IX_Phases_Statut]          ON [dbo].[Phases]([StatutPhase]);

CREATE INDEX [IX_AvancementPoste_RefOF]  ON [dbo].[AvancementPoste]([RefOF]);
CREATE INDEX [IX_AvancementPoste_Poste]  ON [dbo].[AvancementPoste]([IdPoste]);

CREATE INDEX [IX_Documents_Projet]       ON [dbo].[Documents]([IdProjet]);
CREATE INDEX [IX_Documents_OF]           ON [dbo].[Documents]([RefOF]);
