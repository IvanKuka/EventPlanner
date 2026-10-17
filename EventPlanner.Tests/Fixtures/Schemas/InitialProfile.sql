-- Frozen schema of a profile created by Etapa 1 (migration 20261017073256_InitialProfile).
-- Generated once with `dotnet ef migrations script 0 InitialProfile`; never regenerate it,
-- it must keep representing what old user profiles actually contain.

CREATE TABLE IF NOT EXISTS "__EFMigrationsHistory" (
    "MigrationId" TEXT NOT NULL CONSTRAINT "PK___EFMigrationsHistory" PRIMARY KEY,
    "ProductVersion" TEXT NOT NULL
);

BEGIN TRANSACTION;
CREATE TABLE "EventProjects" (
    "Id" TEXT NOT NULL CONSTRAINT "PK_EventProjects" PRIMARY KEY,
    "Name" TEXT NOT NULL,
    "EventType" TEXT NOT NULL,
    "LocalStartDate" TEXT NOT NULL,
    "LocalEndDate" TEXT NOT NULL,
    "TimeZoneId" TEXT NOT NULL,
    "VenueText" TEXT NULL,
    "Currency" TEXT NOT NULL,
    "Status" TEXT NOT NULL,
    "CreatedUtc" TEXT NOT NULL,
    "Revision" INTEGER NOT NULL
);

CREATE TABLE "ParticipantGroups" (
    "Id" TEXT NOT NULL CONSTRAINT "PK_ParticipantGroups" PRIMARY KEY,
    "EventId" TEXT NOT NULL,
    "Label" TEXT NOT NULL,
    "ContactName" TEXT NULL,
    "ContactEmail" TEXT NULL,
    "Phone" TEXT NULL,
    "Notes" TEXT NULL,
    CONSTRAINT "FK_ParticipantGroups_EventProjects_EventId" FOREIGN KEY ("EventId") REFERENCES "EventProjects" ("Id") ON DELETE CASCADE
);

CREATE TABLE "ProgramParts" (
    "Id" TEXT NOT NULL CONSTRAINT "PK_ProgramParts" PRIMARY KEY,
    "EventId" TEXT NOT NULL,
    "Label" TEXT NOT NULL,
    "Order" INTEGER NOT NULL,
    CONSTRAINT "FK_ProgramParts_EventProjects_EventId" FOREIGN KEY ("EventId") REFERENCES "EventProjects" ("Id") ON DELETE CASCADE
);

CREATE TABLE "Guests" (
    "Id" TEXT NOT NULL CONSTRAINT "PK_Guests" PRIMARY KEY,
    "EventId" TEXT NOT NULL,
    "GroupId" TEXT NULL,
    "DisplayName" TEXT NOT NULL,
    "Kind" TEXT NOT NULL,
    "AgeCategory" TEXT NULL,
    "RsvpState" TEXT NOT NULL,
    "PrivateNote" TEXT NULL,
    "Revision" INTEGER NOT NULL,
    CONSTRAINT "FK_Guests_EventProjects_EventId" FOREIGN KEY ("EventId") REFERENCES "EventProjects" ("Id") ON DELETE CASCADE,
    CONSTRAINT "FK_Guests_ParticipantGroups_GroupId" FOREIGN KEY ("GroupId") REFERENCES "ParticipantGroups" ("Id") ON DELETE SET NULL
);

CREATE TABLE "GuestAttendances" (
    "GuestId" TEXT NOT NULL,
    "ProgramPartId" TEXT NOT NULL,
    CONSTRAINT "PK_GuestAttendances" PRIMARY KEY ("GuestId", "ProgramPartId"),
    CONSTRAINT "FK_GuestAttendances_Guests_GuestId" FOREIGN KEY ("GuestId") REFERENCES "Guests" ("Id") ON DELETE CASCADE,
    CONSTRAINT "FK_GuestAttendances_ProgramParts_ProgramPartId" FOREIGN KEY ("ProgramPartId") REFERENCES "ProgramParts" ("Id") ON DELETE CASCADE
);

CREATE INDEX "IX_GuestAttendances_ProgramPartId" ON "GuestAttendances" ("ProgramPartId");

CREATE INDEX "IX_Guests_EventId_RsvpState" ON "Guests" ("EventId", "RsvpState");

CREATE INDEX "IX_Guests_GroupId" ON "Guests" ("GroupId");

CREATE INDEX "IX_ParticipantGroups_EventId" ON "ParticipantGroups" ("EventId");

CREATE INDEX "IX_ProgramParts_EventId_Order" ON "ProgramParts" ("EventId", "Order");

INSERT INTO "__EFMigrationsHistory" ("MigrationId", "ProductVersion")
VALUES ('20261017073256_InitialProfile', '10.0.13');

COMMIT;

