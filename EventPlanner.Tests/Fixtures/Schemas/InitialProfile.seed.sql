-- Sample data of an Etapa 1 profile, in the value formats EF Core 10 wrote for that schema
-- (upper-case GUID text, ISO dates, string enums). Fictional data only.
INSERT INTO "EventProjects" ("Id", "Name", "EventType", "LocalStartDate", "LocalEndDate", "TimeZoneId", "VenueText", "Currency", "Status", "CreatedUtc", "Revision")
VALUES ('0192A000-0000-7000-8000-000000000001', 'Stará svadba', 'Wedding', '2027-06-12', '2027-06-13', 'Europe/Bratislava', 'Sála', 'EUR', 'Planning', '2026-10-17 07:00:00+00:00', 3);

INSERT INTO "ProgramParts" ("Id", "EventId", "Label", "Order")
VALUES ('0192A000-0000-7000-8000-000000000002', '0192A000-0000-7000-8000-000000000001', 'Obrad', 0);

INSERT INTO "ParticipantGroups" ("Id", "EventId", "Label", "ContactName", "ContactEmail", "Phone", "Notes")
VALUES ('0192A000-0000-7000-8000-000000000003', '0192A000-0000-7000-8000-000000000001', 'Rodina', NULL, 'rodina@example.invalid', NULL, NULL);

INSERT INTO "Guests" ("Id", "EventId", "GroupId", "DisplayName", "Kind", "AgeCategory", "RsvpState", "PrivateNote", "Revision")
VALUES ('0192A000-0000-7000-8000-000000000004', '0192A000-0000-7000-8000-000000000001', '0192A000-0000-7000-8000-000000000003', 'Hosť Prvý', 'Named', NULL, 'Accepted', 'bezlepková', 2);

INSERT INTO "GuestAttendances" ("GuestId", "ProgramPartId")
VALUES ('0192A000-0000-7000-8000-000000000004', '0192A000-0000-7000-8000-000000000002');
