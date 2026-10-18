-- Frozen schema of a profile created by release 1.0.0 (migrations up to 20261017090216_VendorRevisionAndDecisions).
-- Generated once with `dotnet ef migrations script 0 VendorRevisionAndDecisions`; never regenerate it,
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

BEGIN TRANSACTION;
CREATE TABLE "Vendors" (
    "Id" TEXT NOT NULL CONSTRAINT "PK_Vendors" PRIMARY KEY,
    "EventId" TEXT NOT NULL,
    "Category" TEXT NOT NULL,
    "Name" TEXT NOT NULL,
    "Contact" TEXT NULL,
    "Notes" TEXT NULL,
    CONSTRAINT "FK_Vendors_EventProjects_EventId" FOREIGN KEY ("EventId") REFERENCES "EventProjects" ("Id") ON DELETE CASCADE
);

CREATE TABLE "Quotes" (
    "Id" TEXT NOT NULL CONSTRAINT "PK_Quotes" PRIMARY KEY,
    "VendorId" TEXT NOT NULL,
    "Description" TEXT NOT NULL,
    "AmountMinor" INTEGER NOT NULL,
    "Currency" TEXT NOT NULL,
    "ScopeText" TEXT NOT NULL,
    "ExclusionsText" TEXT NULL,
    "ValidUntilDate" TEXT NULL,
    "Transport" TEXT NOT NULL,
    "TransportMinor" INTEGER NULL,
    "PeopleCount" INTEGER NULL,
    "Status" TEXT NOT NULL,
    "Revision" INTEGER NOT NULL,
    CONSTRAINT "FK_Quotes_Vendors_VendorId" FOREIGN KEY ("VendorId") REFERENCES "Vendors" ("Id") ON DELETE CASCADE
);

CREATE TABLE "BudgetItems" (
    "Id" TEXT NOT NULL CONSTRAINT "PK_BudgetItems" PRIMARY KEY,
    "EventId" TEXT NOT NULL,
    "Category" TEXT NOT NULL,
    "Title" TEXT NOT NULL,
    "EstimatedMinor" INTEGER NULL,
    "CommittedMinor" INTEGER NULL,
    "Currency" TEXT NOT NULL,
    "SelectedQuoteId" TEXT NULL,
    "Revision" INTEGER NOT NULL,
    CONSTRAINT "FK_BudgetItems_EventProjects_EventId" FOREIGN KEY ("EventId") REFERENCES "EventProjects" ("Id") ON DELETE CASCADE,
    CONSTRAINT "FK_BudgetItems_Quotes_SelectedQuoteId" FOREIGN KEY ("SelectedQuoteId") REFERENCES "Quotes" ("Id") ON DELETE SET NULL
);

CREATE TABLE "Payments" (
    "Id" TEXT NOT NULL CONSTRAINT "PK_Payments" PRIMARY KEY,
    "BudgetItemId" TEXT NOT NULL,
    "OperationId" TEXT NOT NULL,
    "Kind" TEXT NOT NULL,
    "AmountMinor" INTEGER NOT NULL,
    "Status" TEXT NOT NULL,
    "PaidLocalDate" TEXT NULL,
    "DueLocalDate" TEXT NULL,
    "Reference" TEXT NULL,
    "Revision" INTEGER NOT NULL,
    CONSTRAINT "FK_Payments_BudgetItems_BudgetItemId" FOREIGN KEY ("BudgetItemId") REFERENCES "BudgetItems" ("Id") ON DELETE CASCADE
);

CREATE INDEX "IX_BudgetItems_EventId" ON "BudgetItems" ("EventId");

CREATE INDEX "IX_BudgetItems_SelectedQuoteId" ON "BudgetItems" ("SelectedQuoteId");

CREATE INDEX "IX_Payments_BudgetItemId" ON "Payments" ("BudgetItemId");

CREATE UNIQUE INDEX "IX_Payments_OperationId" ON "Payments" ("OperationId");

CREATE INDEX "IX_Payments_Status_DueLocalDate" ON "Payments" ("Status", "DueLocalDate");

CREATE INDEX "IX_Quotes_VendorId_Status" ON "Quotes" ("VendorId", "Status");

CREATE INDEX "IX_Vendors_EventId_Category" ON "Vendors" ("EventId", "Category");

INSERT INTO "__EFMigrationsHistory" ("MigrationId", "ProductVersion")
VALUES ('20261017082808_VendorsAndBudget', '10.0.13');

COMMIT;

BEGIN TRANSACTION;
ALTER TABLE "Vendors" ADD "Revision" INTEGER NOT NULL DEFAULT 1;

CREATE TABLE "QuoteDecisions" (
    "Id" TEXT NOT NULL CONSTRAINT "PK_QuoteDecisions" PRIMARY KEY,
    "QuoteId" TEXT NOT NULL,
    "Kind" TEXT NOT NULL,
    "DecidedUtc" TEXT NOT NULL,
    "AmountMinor" INTEGER NOT NULL,
    "KnownTotalMinor" INTEGER NULL,
    "Currency" TEXT NOT NULL,
    "BudgetImpact" TEXT NULL,
    "Note" TEXT NULL,
    CONSTRAINT "FK_QuoteDecisions_Quotes_QuoteId" FOREIGN KEY ("QuoteId") REFERENCES "Quotes" ("Id") ON DELETE CASCADE
);

CREATE INDEX "IX_QuoteDecisions_QuoteId" ON "QuoteDecisions" ("QuoteId");

INSERT INTO "__EFMigrationsHistory" ("MigrationId", "ProductVersion")
VALUES ('20261017090216_VendorRevisionAndDecisions', '10.0.13');

COMMIT;

