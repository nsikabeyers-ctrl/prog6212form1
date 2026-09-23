/* =========================================================
   RaceDay Database Script
   PROG6212/w - Part 1 - Section C
   Run in SQL Server Management Studio (SSMS) on a clean instance.
   ========================================================= */

IF DB_ID('RaceDayDB') IS NOT NULL
BEGIN
    ALTER DATABASE RaceDayDB SET SINGLE_USER WITH ROLLBACK IMMEDIATE;
    DROP DATABASE RaceDayDB;
END
GO

CREATE DATABASE RaceDayDB;
GO

USE RaceDayDB;
GO

/* ---------------------------------------------------------
   Table: Organisers
   --------------------------------------------------------- */
CREATE TABLE Organisers (
    OrganiserId     INT IDENTITY(1,1) PRIMARY KEY,
    FullName        NVARCHAR(150)   NOT NULL,
    Email           NVARCHAR(150)   NOT NULL UNIQUE,
    PasswordHash    NVARCHAR(255)   NOT NULL,
    PhoneNumber     NVARCHAR(20)    NULL,
    CreatedAt       DATETIME2       NOT NULL DEFAULT SYSDATETIME()
);
GO

/* ---------------------------------------------------------
   Table: Participants
   --------------------------------------------------------- */
CREATE TABLE Participants (
    ParticipantId     INT IDENTITY(1,1) PRIMARY KEY,
    FullName          NVARCHAR(150)   NOT NULL,
    Email             NVARCHAR(150)   NOT NULL UNIQUE,
    PasswordHash      NVARCHAR(255)   NOT NULL,
    DateOfBirth       DATE            NULL,
    ProfilePictureUrl NVARCHAR(500)   NULL,
    CreatedAt         DATETIME2       NOT NULL DEFAULT SYSDATETIME()
);
GO

/* ---------------------------------------------------------
   Table: Events
   --------------------------------------------------------- */
CREATE TABLE Events (
    EventId         INT IDENTITY(1,1) PRIMARY KEY,
    OrganiserId     INT             NOT NULL,
    Name            NVARCHAR(150)   NOT NULL,
    Description     NVARCHAR(1000)  NULL,
    EventDate       DATETIME2       NOT NULL,
    Location        NVARCHAR(200)   NOT NULL,
    DistanceKm      DECIMAL(6,2)    NOT NULL,
    EventType       NVARCHAR(20)    NOT NULL CHECK (EventType IN ('Run', 'Walk', 'Cycle')),
    BannerImageUrl  NVARCHAR(500)   NULL,
    CreatedAt       DATETIME2       NOT NULL DEFAULT SYSDATETIME(),
    CONSTRAINT FK_Events_Organisers FOREIGN KEY (OrganiserId)
        REFERENCES Organisers(OrganiserId)
);
GO

/* ---------------------------------------------------------
   Table: Categories
   --------------------------------------------------------- */
CREATE TABLE Categories (
    CategoryId      INT IDENTITY(1,1) PRIMARY KEY,
    EventId         INT             NOT NULL,
    Name            NVARCHAR(100)   NOT NULL,
    MinAge          INT             NULL,
    MaxAge          INT             NULL,
    DistanceKm      DECIMAL(6,2)    NULL,
    CONSTRAINT FK_Categories_Events FOREIGN KEY (EventId)
        REFERENCES Events(EventId) ON DELETE CASCADE
);
GO

/* ---------------------------------------------------------
   Table: Enrolments
   --------------------------------------------------------- */
CREATE TABLE Enrolments (
    EnrolmentId     INT IDENTITY(1,1) PRIMARY KEY,
    ParticipantId   INT             NOT NULL,
    EventId         INT             NOT NULL,
    CategoryId      INT             NOT NULL,
    EnrolmentDate   DATETIME2       NOT NULL DEFAULT SYSDATETIME(),
    Status          NVARCHAR(20)    NOT NULL DEFAULT 'Pending'
                        CHECK (Status IN ('Pending', 'Confirmed', 'Cancelled')),
    CONSTRAINT FK_Enrolments_Participants FOREIGN KEY (ParticipantId)
        REFERENCES Participants(ParticipantId),
    CONSTRAINT FK_Enrolments_Events FOREIGN KEY (EventId)
        REFERENCES Events(EventId),
    CONSTRAINT FK_Enrolments_Categories FOREIGN KEY (CategoryId)
        REFERENCES Categories(CategoryId),
    CONSTRAINT UQ_Enrolments_Participant_Event UNIQUE (ParticipantId, EventId)
);
GO

/* ---------------------------------------------------------
   Table: Results
   --------------------------------------------------------- */
CREATE TABLE Results (
    ResultId                INT IDENTITY(1,1) PRIMARY KEY,
    EnrolmentId             INT             NOT NULL UNIQUE,
    FinishTime              TIME(0)         NOT NULL,
    FinishPosition          INT             NOT NULL,
    RecordedByOrganiserId   INT             NOT NULL,
    RecordedAt              DATETIME2       NOT NULL DEFAULT SYSDATETIME(),
    CONSTRAINT FK_Results_Enrolments FOREIGN KEY (EnrolmentId)
        REFERENCES Enrolments(EnrolmentId),
    CONSTRAINT FK_Results_Organisers FOREIGN KEY (RecordedByOrganiserId)
        REFERENCES Organisers(OrganiserId)
);
GO

/* =========================================================
   Seed Data
   ========================================================= */

INSERT INTO Organisers (FullName, Email, PasswordHash, PhoneNumber) VALUES
('Thabo Nkosi',   'thabo.nkosi@raceday.co.za',   'HASHED_PASSWORD_1', '0821234567'),
('Lindiwe Dube',  'lindiwe.dube@raceday.co.za',  'HASHED_PASSWORD_2', '0837654321');
GO

INSERT INTO Participants (FullName, Email, PasswordHash, DateOfBirth) VALUES
('Sipho Mokoena', 'sipho.mokoena@example.com', 'HASHED_PASSWORD_3', '1998-04-12'),
('Amahle Zulu',   'amahle.zulu@example.com',   'HASHED_PASSWORD_4', '2001-09-30');
GO

INSERT INTO Events (OrganiserId, Name, Description, EventDate, Location, DistanceKm, EventType) VALUES
(1, 'Pretoria Park Run Classic',   'A community fun run through the Pretoria botanical gardens.', '2026-11-14 07:00', 'Pretoria',      10.0, 'Run'),
(1, 'Tembisa Charity Walk',        'A charity walk supporting local youth programmes.',           '2026-11-21 08:00', 'Tembisa',        5.0, 'Walk'),
(2, 'Highveld Cycle Challenge',    'A scenic cycling route across the Highveld plains.',           '2026-12-05 06:30', 'Johannesburg',  42.0, 'Cycle');
GO

INSERT INTO Categories (EventId, Name, MinAge, MaxAge, DistanceKm) VALUES
(1, 'Under 20',  10, 19, 10.0),
(1, 'Senior',    20, 59, 10.0),
(2, 'Open Walk',  0, 99,  5.0),
(3, '21km Half',  16, 99, 21.0),
(3, '42km Full',  18, 99, 42.0);
GO

INSERT INTO Enrolments (ParticipantId, EventId, CategoryId, Status) VALUES
(1, 1, 2, 'Confirmed'),
(2, 1, 1, 'Confirmed'),
(1, 3, 4, 'Pending');
GO

INSERT INTO Results (EnrolmentId, FinishTime, FinishPosition, RecordedByOrganiserId) VALUES
(1, '00:48:32', 12, 1),
(2, '00:52:10', 15, 1);
GO
