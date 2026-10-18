# Navrhované kontrakty a rozšírenia

Služby EventProjectService, GuestService, QuoteComparisonService, BudgetService, TaskScheduleService, SeatingService a EventExportService; transakcie podľa konkrétnych príkazov. Implementované: EventProjectService, GuestService (Etapa 1), VendorService (CRUD dodávateľov a ponúk), QuoteComparisonService (CompareQuotes, SelectQuote) a BudgetService (BudgetTotals ako `GetOverviewAsync`, RecordPayment, PostPayment, CancelPayment) v Etape 2; TaskService (časť plánovaného TaskScheduleService: `ListAsync` = UpcomingTasks s termínmi podľa časovej zóny projektu, Create/Update/SetState ako CompleteTask, Delete) po vydaní 1.0.0.

Commands: UpdateGuest(id,expectedRevision,...), SetGroupRsvp(groupId,explicitGuestChanges), SelectQuote(id,budgetImpactPolicy), RecordPayment(itemId,operationId,...), MoveGuestToTable(guestId,tableId,expectedRevision), CompleteTask(id,...), PreviewExport(eventId,profile,fields), ExportApprovedPreview(previewHash,target), ImportProjectAsCopy(path).

Queries: EventOverview, GuestCountsByAttendanceScope, CompareQuotes(category), BudgetTotals(currency), UpcomingTasks(asOfDate), TimelineForDay(date), SeatingCapacityReport. Súhrny sa počítajú zo zdrojových záznamov, nie z ručne udržiavaných nesúvislých čísel.

Žiadny platobný gateway, verejný RSVP web, SMTP ani kalendárová synchronizácia v MVP. Manuálny ICS export ako budúce rozšírenie musí mať stabilné UID a definovaný update postup. Finance Atlas integrácia iba cez budúci dokumentovaný export/import, nikdy cez spoločnú databázu. Nepredpokladať hotové cudzie endpointy.

Prílohy zmlúv/dokladov kopírovať do blob store. Otvorenie v predvolenej OS aplikácii, aktívne dokumenty nespúšťať v privilegovanom hoste. Uložené kontakty sa nepovažujú za autorizáciu appky kohokoľvek osloviť.
