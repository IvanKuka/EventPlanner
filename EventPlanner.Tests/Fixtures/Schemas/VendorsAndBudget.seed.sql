-- Sample vendor and budget data of an Etapa 2 profile, applied after InitialProfile.seed.sql, in the
-- value formats EF Core 10 wrote for that schema (upper-case GUID text, ISO dates, string enums). Fictional data only.
INSERT INTO "Vendors" ("Id", "EventId", "Category", "Name", "Contact", "Notes")
VALUES ('0192A000-0000-7000-8000-000000000010', '0192A000-0000-7000-8000-000000000001', 'Výzdoba', 'Stará výzdoba', 'info@example.invalid', NULL);

INSERT INTO "Quotes" ("Id", "VendorId", "Description", "AmountMinor", "Currency", "ScopeText", "ExclusionsText", "ValidUntilDate", "Transport", "TransportMinor", "PeopleCount", "Status", "Revision")
VALUES ('0192A000-0000-7000-8000-000000000011', '0192A000-0000-7000-8000-000000000010', 'Kvety', 30000, 'EUR', 'kvety, stuhy', NULL, '2027-05-01', 'Separate', 2500, 80, 'Selected', 2);

INSERT INTO "BudgetItems" ("Id", "EventId", "Category", "Title", "EstimatedMinor", "CommittedMinor", "Currency", "SelectedQuoteId", "Revision")
VALUES ('0192A000-0000-7000-8000-000000000012', '0192A000-0000-7000-8000-000000000001', 'Výzdoba', 'Kvety', 35000, 32500, 'EUR', '0192A000-0000-7000-8000-000000000011', 2);

INSERT INTO "Payments" ("Id", "BudgetItemId", "OperationId", "Kind", "AmountMinor", "Status", "PaidLocalDate", "DueLocalDate", "Reference", "Revision")
VALUES ('0192A000-0000-7000-8000-000000000013', '0192A000-0000-7000-8000-000000000012', '0192A000-0000-7000-8000-000000000014', 'Deposit', 10000, 'Posted', '2027-01-15', NULL, 'VS 1', 1);
