# Navrhovaný dátový model

| Entita | Polia a pravidlá |
|---|---|
| EventProject | id, name, eventType, localStartDate, localEndDate, timezoneId, venueText?, currency, revision |
| ProgramPart | id, eventId, label, order – časti programu definované projektom (implementované) |
| ParticipantGroup | id, eventId, label, contactName?, contactEmail?, phone?, notes? |
| Guest | id, eventId, groupId?, displayName, kind (Named / PlusOnePlaceholder), ageCategory?, rsvpState, privateNote?, revision |
| GuestAttendance | (guestId, programPartId) – explicitný attendanceScope hosťa; cascade pri zmazaní hosťa aj časti programu |
| MealRequirement | id, guestId, requirementText, cateringShareAllowed; citlivá informácia sa nezahrnie do všeobecného exportu |
| Vendor | id, eventId, category, name, contact?, notes?, revision |
| Quote | id, vendorId, description, amountMinor, currency, scopeText, exclusionsText?, validUntilDate?, transport (Unknown / Included / Separate / NotNeeded), transportMinor? (len pri Separate), peopleCount?, status (Received / Selected / Declined), revision |
| QuoteDecision | id, quoteId (CASCADE), kind (Selected / Replaced / Declined), decidedUtc, amountMinor, knownTotalMinor?, currency, budgetImpact? (len Selected), note? |
| BudgetItem | id, eventId, category, title, estimatedMinor?, committedMinor?, currency (nemenná po vytvorení), selectedQuoteId? (SET NULL pri zmazaní ponuky), revision |
| Payment | id, budgetItemId, operationId (unikátny), kind (Deposit / Balance / Refund), amountMinor > 0, paidLocalDate? (povinný pri Posted), dueLocalDate?, status (Planned / Posted / Cancelled), reference?, revision |
| Task | id, eventId, title, ownerLabel?, dueLocalDate?, state, priority, predecessorId?, revision |
| TimelineEntry | id, eventId, title, startLocalDateTime, endLocalDateTime?, ownerLabel?, location?, visibility |
| SeatingTable | id, eventId, label, capacity, order |
| SeatAssignment | guestId unique v evente, tableId; obsadenosť <= kapacita |
| Attachment | id, eventId, ownerType, ownerId, blobId, kind, originalName, sha256 |

RSVP = Unknown, Invited, Accepted, Declined, Tentative. attendanceScope má explicitné položky definované projektom, napr. obrad/večera; neodvodzovať z jediného RSVP, že hosť príde na všetky časti. Skupina nesmie automaticky označiť všetkých za potvrdených bez možnosti výnimky.

Peniaze v minor units, žiadny double. Jedna mena projektu v MVP; cudzia ponuka sa uchová s vlastnou menou, ale bez konverzie sa nevloží do spoločného súčtu. Uhradené = iba posted payment, plánovaná záloha sa nesčíta s už vykonanou druhýkrát. Refund je samostatný typ, záporná ľubovoľná suma nie je univerzálny workaround.

Stav implementácie: Etapa 1 – EventProject, ProgramPart, ParticipantGroup, Guest a GuestAttendance (migrácia `InitialProfile`); Etapa 2 – Vendor, Quote, BudgetItem a Payment (migrácia `VendorsAndBudget`, indexy Quote(vendorId,status), Payment(status,dueLocalDate), unikátny Payment(operationId), Vendor(eventId,category)). Ostatné entity sú plánované. Polia transport, transportMinor a peopleCount ponuky dopĺňajú porovnanie podľa 03-processing (doprava, počet osôb); revision na BudgetItem chráni súbeh platieb a výberu ponuky; migrácia `VendorRevisionAndDecisions` pridala revision na Vendor (existujúci dodávatelia 1) a append-only QuoteDecision (index quoteId) s cenou zapísanou v čase rozhodnutia. Sumy sú `long` v minor units podľa ISO 4217 (`MinorUnits`), 0 … 10^12; null znamená neznáme. Enumy sa ukladajú ako stabilné anglické reťazce. Zmazanie skupiny hostí zachová a len zruší ich väzbu.

Indexy Guest(eventId,rsvpState), Quote(vendorId,status), Payment(status,dueLocalDate), Task(eventId,state,dueLocalDate), TimelineEntry(eventId,startLocalDateTime). Cross-event väzby sú zakázané. Prílohy vlastnené jednou entitou podľa validovaného ownerType, nie voľný používateľský názov tabuľky.
