# Piano: scheda casella — avanzamento reale del backfill e contesto dell'ultimo errore
Data: 2026-10-09
Stato: COMPLETATO

## Obiettivo
Durante la lettura dello storico la scheda casella mostra una barra determinata ("13.000 / ~48.200 mail, 27%") che si aggiorna da sola, il giro in corso al posto di un "Ultimo giro" fermo a ieri, e l'ultimo errore con data e numero del giro che l'ha prodotto.

## Contesto
- La barra è una `<progress>` indeterminata con `appearance: none`: in Chrome appare vuota e ferma. Nessun numero (`valueText` non passato).
- `MailboxDto` non porta avanzamento: `messagesRead` vive sul giro; la lista caselle non si aggiorna da sola.
- `LastRunAtUtc`/`LastError` si scrivono solo a fine giro (Completed/Failed): i giri 4–8 (annullati/interrotti) e il 9 (in corso) non li toccano, la scheda mostra il giro 3 dell'8 ottobre.
- Scelte dell'utente (2026-10-09): opzione "BE+FE, barra con %" e "Ultimo errore con data e giro".
- Il piano FE `2026-10-08-dr-mailroom-fe` è `IN CORSO` (Fase 11): questo piano è separato, tocca solo la vista Caselle già consegnata in Fase 10.

## Disegno
- **Totale casella**: `users.getProfile` restituisce già `messagesTotal` (stessa chiamata dell'historyId, costo 1 unità). Si salva in `Mailbox.MessagesTotal` a ogni avvio o ripresa del backfill. È un totale approssimato (include Spam/Cestino): la UI lo mostra con "~".
- **Indicizzati**: `COUNT` dei `Message` della casella, calcolato nella proiezione del DTO (vale anche tra giri interrotti, a differenza di `Run.MessagesRead`).
- **Giro in corso**: sottoquery nella proiezione — giro `Running` più recente della casella (`Id`, `StartedAtUtc`, `MessagesRead`), null se nessuno.
- **Errore con contesto**: nuova colonna `Mailbox.LastErrorRunId` (giro che ha scritto `LastError`), scritta da `RecordRunOutcomeAsync`; la data è `LastRunAtUtc`, già scritta insieme all'errore.
- **FE**: barra determinata se `messagesTotal` noto, indeterminata animata altrimenti (con ramo `prefers-reduced-motion`); polling della lista ogni 15 s solo finché almeno una casella ha un giro in corso.

## Scope
### File da modificare — backend (`dr-mailroom`)
- [x] `src/dr-mailroom-core/Gmail/IGmailSession.cs`, `GmailSession.cs` — `GetProfileHistoryIdAsync` → `GetProfileAsync` che restituisce `GmailProfile(HistoryId, MessagesTotal)`
- [x] `src/dr-mailroom-core/Gmail/GmailProfile.cs` (nuovo) — record del profilo
- [x] `src/dr-mailroom-core/Ingestion/MailboxIngestionService.cs` — salva `MessagesTotal` all'avvio e alla ripresa del backfill
- [x] `src/dr-mailroom-core/Infrastructure/Mailroom/Entities/Mailbox.cs` — `MessagesTotal int?`, `LastErrorRunId long?`
- [x] `src/dr-mailroom-core/Infrastructure/Mailroom/MailboxProvider.cs` — `RecordRunOutcomeAsync` riceve `runId`
- [x] `src/dr-mailroom-core/Runs/RunOrchestrator.cs` — passa `runId` a `RecordRunOutcomeAsync`
- [x] `src/dr-mailroom-core/Infrastructure/Mailroom/Migrations/*` — migration `MailboxProgress` (generata con `dotnet ef`)
- [x] `src/dr-mailroom-api/Dto/MailboxDto.cs` (+ nuovo `MailboxRunDto.cs` se serve) — `MessagesTotal`, `MessagesIndexed`, `CurrentRun`, `LastErrorRunId`
- [x] `test/dr-mailroom-tests/**` — `FakeGmail`, test di ingestione, orchestratore e DTO adeguati; nuovi test per `MessagesTotal` e `LastErrorRunId`
- [x] `docs/architettura.md`, `docs/endpoint*.md` (se descrivono `MailboxDto`) — nuovi campi

### File da modificare — frontend (`dr-mailroom-fe`)
- [x] `src/services/api/schema.d.ts` — rigenerato con `npm run gen:api`
- [x] `src/components/shared/ProgressMeter.vue` — stato indeterminato visibile e animato, ramo `prefers-reduced-motion`
- [x] `src/composables/mailboxes/mailboxPresentation.ts` — funzione pura per testo e valori della barra (`backfillProgress`)
- [x] `src/components/mailboxes/MailboxCard.vue` — barra determinata, giro in corso, errore con "giro N, data"
- [x] `src/components/mailboxes/MailboxDetailDialog.vue`, `MailboxTable.vue` — stessi dati (giro in corso, contesto errore)
- [x] `src/composables/mailboxes/useMailboxesView.ts` — polling 15 s attivo solo con giri in corso, fermato all'uscita dalla vista
- [x] `src/**/__tests__/*` — test di `backfillProgress`, polling e scheda
- [x] Aggiunti in esecuzione: `src/composables/mailboxes/useMailboxPolling.ts`, `src/utils/formatNumber.ts`, `src/stores/mailboxes.ts` (`refreshSilently`); backend: `src/dr-mailroom-api/Dto/MailboxRunDto.cs`, `docs/frontend-specifiche.md`, `docs/endpoint-mailboxes.md`

### Perimetro negativo
- Non toccherò: logica di ingestione oltre al salvataggio di `MessagesTotal`, retry/limitatore di quota, processi, filtri, scheduler, endpoint diversi da quelli caselle
- Nessun nuovo endpoint: i dati arrivano dal `MailboxDto` esistente
- Non toccherò le altre viste FE né il piano `2026-10-08-dr-mailroom-fe`
- Nessun commit né push senza richiesta esplicita

## Fasi

### Fase 1: profilo Gmail con totale messaggi
- **Stato**: [x] — `GetProfileAsync` → `GmailProfile`; test `GetProfile_ReturnsHistoryIdAndMessagesTotalAsync` aggiunto
- **Precondizione**: build verde
- **File**: `GmailProfile.cs`, `IGmailSession.cs`, `GmailSession.cs`, `FakeGmail.cs`
- **Operazione**: CREATE / EDIT
- **Azione**: `GetProfileAsync` restituisce `GmailProfile(ulong HistoryId, int? MessagesTotal)`; i chiamanti usano `.HistoryId`
- **Tool ammessi**: dotnet
- **Verifica passo**: `dotnet build` 0 errori
- **Su divergenza**: STOP — scrivi `⚠️ Divergenza Fase 1: <cosa>` in plan.md, non procedere

### Fase 2: colonne e migration
- **Stato**: [x] — migration `20261009182050_MailboxProgress`: due `AddColumn` nullable (`LastErrorRunId bigint`, `MessagesTotal int`). Nota fuori perimetro: `.gitignore:449` `Gmail/` esclude anche `src/dr-mailroom-core/Gmail/` e `test/dr-mailroom-tests/Gmail/` (mai committati) — da segnalare all'utente
- **Precondizione**: Fase 1 completata
- **File**: `Mailbox.cs`, `Migrations/*`
- **Operazione**: EDIT / CREATE
- **Azione**: aggiunge `MessagesTotal int?` e `LastErrorRunId long?` (nullable, nessun default); `dotnet ef migrations add MailboxProgress`
- **Tool ammessi**: dotnet, dotnet-ef
- **Verifica passo**: migration con sole due `AddColumn` nullable; build verde
- **Su divergenza**: STOP — scrivi `⚠️ Divergenza Fase 2: <cosa>` in plan.md, non procedere

### Fase 3: scrittura dei nuovi dati
- **Stato**: [x] — `BackfillAsync` legge sempre il profilo: `MessagesTotal` ad avvio e ripresa, `HistoryId` solo se assente; `LastErrorRunId` = runId solo con errore. Migration portata a namespace file-scoped come `InitialCreate` (IDE0161)
- **Precondizione**: Fase 2 completata
- **File**: `MailboxIngestionService.cs`, `MailboxProvider.cs`, `RunOrchestrator.cs`
- **Operazione**: EDIT
- **Azione**: backfill (avvio e ripresa) salva `MessagesTotal`; `RecordRunOutcomeAsync(mailboxId, runId, lastError, newStatus)` scrive `LastErrorRunId` (null se nessun errore)
- **Tool ammessi**: dotnet
- **Verifica passo**: build verde
- **Su divergenza**: STOP — scrivi `⚠️ Divergenza Fase 3: <cosa>` in plan.md, non procedere

### Fase 4: DTO casella
- **Stato**: [x] — `MailboxDto` + `MailboxRunDto(Id, StartedAtUtc, MessagesRead)`; traduzione SQL Server verificata con `ToQueryString` (test `Projection_TranslatesToSqlServer_WithoutClientEvaluation`)
- **Precondizione**: Fase 3 completata
- **File**: `MailboxDto.cs`, eventuale `MailboxRunDto.cs`
- **Operazione**: EDIT / CREATE
- **Azione**: proiezione EF-traducibile con `MessagesTotal`, `MessagesIndexed` (`e.Messages.Count()`), `CurrentRun` (giro Running più recente o null), `LastErrorRunId`
- **Tool ammessi**: dotnet
- **Verifica passo**: build verde; SQL generato senza valutazione lato client (log EF o test)
- **Su divergenza**: STOP — scrivi `⚠️ Divergenza Fase 4: <cosa>` in plan.md, non procedere

### Fase 5: test backend
- **Stato**: [x] — 114 superati, 1 ignorato (invariato); nuovi: `MailboxDtoTests` (3), `CompletedRun_ClearsPreviousErrorAndItsRunAsync`, asserzioni su `MessagesTotal` (avvio/ripresa) e `LastErrorRunId`; build 0 avvisi; `dotnet format --verify-no-changes` exit 0
- **Precondizione**: Fase 4 completata
- **File**: `test/dr-mailroom-tests/**`
- **Operazione**: EDIT
- **Azione**: adegua i test alle nuove firme; aggiunge: `MessagesTotal` salvato all'avvio e alla ripresa; `LastErrorRunId` scritto su Failed e azzerato su Completed; `CurrentRun` valorizzato solo con giro Running
- **Tool ammessi**: dotnet
- **Verifica passo**: `dotnet test dr-mailroom.slnx` exit 0; `dotnet format --verify-no-changes` exit 0
- **Su divergenza**: STOP — scrivi `⚠️ Divergenza Fase 5: <cosa>` in plan.md, non procedere

### Fase 6: rilascio API
- **Stato**: [x] — ⚠️ Divergenza Fase 6 (risolta): la migration **non** si applica all'avvio; applicata con `dotnet ef database update` (stesso SQL Server del container: `spectre` = `host.docker.internal`), colonne nullable compatibili col container vecchio. Rebuild container: giro 9 chiuso Cancelled a 15.500 letti. `GET /mailboxes/1` espone i nuovi campi (`messagesIndexed` 26.500). `messagesTotal` atteso dal giro delle 18:30 UTC
- **Precondizione**: Fase 5 completata
- **Azione**: `docker compose up -d --build` del container API (interrompe il giro 9; il backfill riprende dalla pagina salvata al giro schedulato successivo); migration applicata all'avvio
- **Tool ammessi**: docker, curl
- **Verifica passo**: `GET /api/v1/mailboxes/1` contiene i nuovi campi; dopo il primo giro `messagesTotal` valorizzato
- **Su divergenza**: STOP — scrivi `⚠️ Divergenza Fase 6: <cosa>` in plan.md, non procedere

### Fase 7: tipi FE e barra di avanzamento
- **Stato**: [x] — `gen:api`; `ProgressMeter` indeterminata con fascia animata (reduced-motion: trama fissa); `backfillProgress` (max 99% in corso) + `utils/formatNumber.ts`; fixture di `mailboxes.spec.ts` allineata ai nuovi campi; `type-check` exit 0
- **Precondizione**: Fase 6 completata
- **File**: `schema.d.ts`, `ProgressMeter.vue`, `mailboxPresentation.ts`
- **Operazione**: EDIT
- **Azione**: `npm run gen:api`; stato indeterminato animato (ramo reduced-motion); `backfillProgress(mailbox)` → `{ value, max?, valueText }`
- **Tool ammessi**: npm
- **Verifica passo**: `type-check` exit 0
- **Su divergenza**: STOP — scrivi `⚠️ Divergenza Fase 7: <cosa>` in plan.md, non procedere

### Fase 8: scheda, dettaglio, tabella
- **Stato**: [x] — card: barra con valori, blocco "Giro in corso", "Ultimo giro concluso", errore con riga di contesto; tabella e dettaglio con gli stessi dati (testo); `currentRunText`/`lastErrorContext` in `mailboxPresentation.ts`; `type-check` e `lint:check` exit 0
- **Precondizione**: Fase 7 completata
- **File**: `MailboxCard.vue`, `MailboxDetailDialog.vue`, `MailboxTable.vue`
- **Operazione**: EDIT
- **Azione**: barra con "N / ~T mail · P%"; "Giro in corso — dalle HH:mm" al posto di "Ultimo giro" quando `currentRun` presente; errore con "giro N · data"
- **Tool ammessi**: npm
- **Verifica passo**: `type-check` e `lint:check` exit 0
- **Su divergenza**: STOP — scrivi `⚠️ Divergenza Fase 8: <cosa>` in plan.md, non procedere

### Fase 9: aggiornamento automatico
- **Stato**: [x] — composable `useMailboxPolling` (testabile senza montare la vista) + `store.refreshSilently`. ⚠️ Divergenza minore: oltre ai 15 s con giro in corso, rilettura ogni 60 s se una casella attiva ha lo storico in sospeso senza giro (così la scheda si accorge del giro schedulato successivo). `formatCount` con raggruppamento esplicito ("1.500"): Intl it-IT non separa sotto 10.000 e i tipi TS non ammettono `useGrouping: 'always'`. 69/69 test
- **Precondizione**: Fase 8 completata
- **File**: `useMailboxesView.ts`
- **Operazione**: EDIT
- **Azione**: polling `MAILBOX_RUN_POLL_INTERVAL_MS = 15_000` attivo solo se almeno una casella ha `currentRun`; nessun cambio di `loadState` durante il refresh silenzioso; stop all'unmount
- **Tool ammessi**: npm
- **Verifica passo**: test unitario con timer finti: parte con giro in corso, si ferma senza
- **Su divergenza**: STOP — scrivi `⚠️ Divergenza Fase 9: <cosa>` in plan.md, non procedere

### Fase 10: test FE e rilascio
- **Stato**: [x] — gate FE tutti exit 0 (69/69 test); container FE ricostruito; Playwright sulla stessa pagina senza reload: 27.000 → 27.500 / ~38.413 mail, 70% → 71% in 130 s. Correzione dopo screenshot: valore della barra non spezzato (`flex-wrap` + `whitespace-nowrap` in `ProgressMeter`). Documenti aggiornati: `architettura.md` v1.6, `frontend-specifiche.md` v1.2, `endpoint-mailboxes.md` v1.2
- **Precondizione**: Fase 9 completata
- **Azione**: `test:unit --run`, `lint:check`, `format:check`, `build`; rebuild container FE; verifica visiva della scheda durante un giro (screenshot)
- **Tool ammessi**: npm, docker, playwright
- **Verifica passo**: tutti exit 0; nello screenshot la barra mostra valore e percentuale e il numero cresce tra due aggiornamenti
- **Su divergenza**: STOP — scrivi `⚠️ Divergenza Fase 10: <cosa>` in plan.md, non procedere

## Criteri di verifica finale
- [x] `dotnet build` 0 avvisi, `dotnet test` exit 0, `dotnet format --verify-no-changes` exit 0
- [x] Migration con sole colonne nullable aggiunte
- [x] `GET /api/v1/mailboxes` restituisce `messagesTotal`, `messagesIndexed`, `currentRun`, `lastErrorRunId`
- [x] Scheda: barra determinata con numero e percentuale durante il backfill; si aggiorna senza ricaricare la pagina
- [x] "Giro in corso" mostrato quando c'è un giro Running; "Ultimo errore" con giro e data
- [x] FE: `type-check`, `lint:check`, `format:check`, `test:unit`, `build` exit 0
- [x] Animazione con ramo `prefers-reduced-motion`; stato non espresso solo col colore
- [x] Logging strutturato con placeholder nei nuovi log

## Esito
Piano 2026-10-09-scheda-avanzamento-backfill verificato. Tutti i criteri soddisfatti.
- Backend: 114 superati, 1 ignorato; build 0 avvisi; `dotnet format` exit 0
- Nessun commit né push (due repository: `dr-mailroom`, `dr-mailroom-fe`)
- Segnalazione fuori perimetro: `.gitignore:449` `Gmail/` esclude i sorgenti `src/dr-mailroom-core/Gmail/` e `test/dr-mailroom-tests/Gmail/`
