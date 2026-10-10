# Progetto

dr-mailroom: backend .NET 10 (Minimal API + EF Core su SQL Server) in `E:\Davide\Progetti\dr-mailroom-workspace\dr-mailroom` (branch `main`) e frontend Vue 3 in `E:\Davide\Progetti\dr-mailroom-workspace\dr-mailroom-fe` (branch `main`). Workspace VS Code: `E:\Davide\Progetti\dr-mailroom-workspace\dr-mailroom.code-workspace`.

---

# Obiettivo

Eseguire il brief `briefs/20261009-01.md` ("completamento del FE"): CRUD filtri, lista esecuzioni, statistiche per casella con, per mittente (indirizzo email), numero di mail e data+oggetto della più recente e della più vecchia.

- Piano coordinatore: `.ai/plans/2026-10-09-completamento-fe/plan.md` — `Stato: IN CORSO`, approvato dall'utente
- Piano FE: `.ai/plans/2026-10-08-dr-mailroom-fe/plan.md` — revisione v1.3 (Fasi 11 e 13 aggiornate dal brief)
- Piano `2026-10-09-scheda-avanzamento-backfill` — `COMPLETATO`

---

# Stato corrente

- Piano coordinatore: Fasi 1, 2, 3, 3b, 4, 5b `[x]`; Fasi 5, 6, 7, 8, 9 `[ ]`
- Piano FE: Checkpoint C1 `[x]`, Fase 11 `[x]`; Fasi 12–18 `[ ]`
- **Nessun commit** nella sessione: entrambi i repo hanno modifiche non committate (vedi "File modificati")
- Container in esecuzione: `dr-mailroom-api` (porta 5180, ricreato con le modifiche CORS e date) e `dr-mailroom-fe` (dietro `http://mailroom.spectre.local`, ricreato con le modifiche del piano backfill; **non** contiene la vista Filtri)
- Immagini di rollback: `dr-mailroom-api:pre-cors`, `dr-mailroom-api:pre-date-fix`, `dr-mailroom-fe:pre-backfill-close`
- Casella 1: backfill completato, 38.439 mail indicizzate

---

# Componenti completati

- Checkpoint C1: vista Caselle approvata dall'utente (2026-10-10)
- CORS di sviluppo: opzione `Mailroom:Frontend:ExtraCorsOrigins` (vuota di default), `http://localhost:5173` solo nel `.env` locale
- Date 1970: `ReceivedDateResolver` (internalDate > 0 → header `Date` → ora di lettura), header `Date` richiesto a Gmail, warning strutturato, riparazione a inizio giro delle mail con data = 1970-01-01 (max 50 per giro); le 10 righe esistenti riparate (giro 25)
- Vista Filtri FE (Fase 11): elenco a card con ricerca, costruttore a frase e form classico, anteprima, riapplica, elimina, allegati, precompilazione `?sender=`
- Validazione regex del filtro: solo lato server (motore .NET)

---

# Componenti in lavorazione

Nessuno

---

# Componenti mancanti

- Fase 5 (= Fase 12 piano FE): vista Esecuzioni, compresa la lettura di `/runs?runIds=<id,…>&skippedMailboxIds=<id,…>` prodotta da "Riapplica"
- Fase 6: BE `GET /api/v1/stats/senders/overview` (paginato, ordinabile, ricerca, `year` facoltativo) e `GET /api/v1/stats/years` (`mailboxId` obbligatorio, `senderAddress` facoltativo con confronto esatto)
- Fase 7: misura della query su ~48.000 mail; indice `(MailboxId, SenderAddress, ReceivedAtUtc) INCLUDE (Subject)` solo se > 300 ms
- Fase 8: documentazione BE del nuovo endpoint (`/dr-professor`)
- Fase 9 (= Fase 13 piano FE): vista Statistiche (selettore casella e anno, tabella paginata, barre top 20, dettaglio mittente per anno, "Crea filtro da questo mittente")
- Fasi 14–18 del piano FE (e2e, container, debug combinato, documentazione, chiusura)

---

# File modificati

Repo `dr-mailroom` (in questa sessione):
- `src/dr-mailroom-core/Configuration/FrontendOptions.cs` — `ExtraCorsOrigins`
- `src/dr-mailroom-api/Program.cs` — origini CORS = `BaseUrl` + `ExtraCorsOrigins`
- `.env.example` — voce commentata `Mailroom__Frontend__ExtraCorsOrigins__0`
- `.env` (non tracciato) — `Mailroom__Frontend__ExtraCorsOrigins__0=http://localhost:5173`
- `src/dr-mailroom-core/Gmail/ReceivedDateResolver.cs`, `ReceivedDateSource.cs` (nuovi) — data di ricezione senza ripiego su 0
- `src/dr-mailroom-core/Gmail/GmailMessageMetadata.cs` — `ReceivedDateSource`, `RawInternalDate`
- `src/dr-mailroom-core/Gmail/GmailSession.cs` — header `Date`, uso del resolver, warning
- `src/dr-mailroom-core/Gmail/GmailConstants.cs` — `DateHeader`
- `src/dr-mailroom-core/Infrastructure/Mailroom/MessageProvider.cs` — `GetUndatedMessagesAsync` (con `AsTracking`), `SaveChangesAsync`
- `src/dr-mailroom-core/Ingestion/MailboxIngestionService.cs` — `RepairUndatedMessagesAsync`, `MaxUndatedRepairsPerRun = 50`
- `test/dr-mailroom-tests/Api/CorsTests.cs` (nuovo) — 3 test CORS
- `test/dr-mailroom-tests/Gmail/ReceivedDateResolverTests.cs` (nuovo), `Gmail/GmailSessionTests.cs` — test del resolver e del ripiego su header `Date`
- `test/dr-mailroom-tests/Ingestion/MailboxIngestionServiceTests.cs` — test di riparazione
- `test/dr-mailroom-tests/Infrastructure/InMemoryMailroomDb.cs` — `NoTracking` come in produzione
- `test/dr-mailroom-tests/Runs/RunOrchestratorTests.cs` — `AsTracking()` nella preparazione di `PausedMailbox_RunIsSkipped_AndNothingIsReadAsync`
- `.ai/plans/2026-10-09-completamento-fe/plan.md` (nuovo), `.ai/plans/2026-10-08-dr-mailroom-fe/plan.md` (v1.3), `.ai/permessi-richiesti.md`

Repo `dr-mailroom` (non committati, da sessioni/piani precedenti): file del piano backfill (`Mailbox.cs`, `MailboxProvider.cs`, `RunOrchestrator.cs`, `MailboxDto.cs`, `MailboxRunDto.cs`, migration `20261009182050_MailboxProgress`, `FakeGmail.cs`, `MailboxDtoTests.cs`, `docs/*`), `.gitignore` (regola `Gmail/` ancorata alla radice)

Repo `dr-mailroom-fe` (in questa sessione, Fase 11):
- `src/services/filtersService.ts`, `src/services/messagesService.ts`, `src/stores/filters.ts` (nuovi)
- `src/composables/filters/*`, `src/components/filters/*` (nuovi)
- `src/views/FiltersView.vue`
- `src/services/api/problem.ts` (+ test) — 201 trattato come successo
- `src/composables/filters/filterForm.ts` (+ test) — rimossa `isCompilableRegex`

Repo `dr-mailroom-fe` (non committati, piano backfill): `MailboxCard.vue`, `MailboxDetailDialog.vue`, `MailboxTable.vue`, `ProgressMeter.vue`, `mailboxPresentation.ts`, `useMailboxesView.ts`, `useMailboxPolling.ts`, `schema.d.ts`, `stores/mailboxes.ts`, `utils/formatNumber.ts` e test

---

# Decisioni progettuali

Vedi `DECISIONI.md`.

---

# Problemi aperti

- **Attività incompleta**: nessun commit. Le cartelle `src/dr-mailroom-core/Gmail/` e `test/dr-mailroom-tests/Gmail/` risultano interamente non tracciate (prima escluse da `.gitignore`), quindi non sono mai state committate
- **Attività incompleta**: il container `dr-mailroom-fe` non contiene la vista Filtri (non ricostruito dopo la Fase 11)
- **Limitazione**: 10 mail di spam con `internalDate = 0` da Gmail restano datate come lette (9 al 2026-10-10, 1 dall'header `Date`); l'utente le elimina da Gmail
- **Limitazione**: il filtro `senderAddress` di `/stats/senders/monthly` usa `Contains`, non l'uguaglianza
- **Debito tecnico**: il DbContext è `NoTracking` di default (`MailroomProviderExtensions.cs:22`); ogni aggiornamento richiede `AsTracking()`
- **Attività incompleta**: in `briefs/` c'è un nuovo brief `20261011-01.md`, non letto in questa sessione

---

# TODO

1. Fase 5: eseguire la Fase 12 del piano FE (vista Esecuzioni, compresa la query `runIds`/`skippedMailboxIds`)
2. Fase 6: implementare `stats/senders/overview` e `stats/years` con validator, test e `ToQueryString`; ricreare il container API (tag `pre-overview`)
3. Fase 7: misurare la query sulla casella 1; indice solo se > 300 ms
4. Fase 8: `/dr-professor` per la documentazione del nuovo endpoint
5. Fase 9: `npm run gen:api` ed esecuzione della Fase 13 riscritta del piano FE
6. Ricostruire il container FE dopo le viste nuove
7. Chiedere all'utente se e come committare (due repo)

---

# Validazione

- Build BE: `dotnet build` 0 avvisi, 0 errori (dopo le ultime modifiche)
- Test BE: 131 superati, 1 ignorato, 0 falliti
- Lint BE: `dotnet format --verify-no-changes` exit 0
- FE: `type-check`, `lint:check`, `format:check`, `build` exit 0; vitest 139/139
- Verifica in esecuzione: preflight CORS (5173 e `mailroom.spectre.local` ammesse, `evil.local` rifiutata); giro 25 `Completed`; `stats/senders/monthly?mailboxId=1` → 0 righe con `YearMonth < 200001`

---

# Rischi

- Modifiche consistenti non committate in due repo, comprese cartelle mai tracciate
- Il contesto `NoTracking`: un aggiornamento senza `AsTracking()` non viene salvato e non dà errori
- Ricreare il container API interrompe un giro in corso: controllare prima `currentRun` su `GET /api/v1/mailboxes`
- La Fase 13 dipende dal contratto delle Fasi 6–8: non va iniziata prima

---

# Prossimo passo consigliato

Eseguire la Fase 12 del piano FE (vista Esecuzioni) come Fase 5 del piano coordinatore.

---

# Informazioni mancanti

- Strategia di commit desiderata dall'utente per i due repo: Sconosciuto
- Contenuto e priorità di `briefs/20261011-01.md`: Sconosciuto
