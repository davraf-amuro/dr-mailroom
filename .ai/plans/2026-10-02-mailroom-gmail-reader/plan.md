# Piano: dr-mailroom — servizio C# di lettura Gmail, filtri, processi e statistiche
Brief: [20261001-01.md](../../../briefs/20261001-01.md)
Data: 2026-10-02
Stato: PROPOSTO
Sostituisce: [2026-10-02-dr-mailroom-mvp](../2026-10-02-dr-mailroom-mvp/plan.md) (SUPERATO)

## Obiettivo
Servizio .NET autonomo che legge in sola lettura una o più caselle Gmail, salva statistiche per mittente, applica filtri (mittente + condizione opzionale sull'oggetto) e i processi associati (estrazione allegati, allerta), schedulato da cron configurabile ed eseguibile on-demand via API; frontend solo come specifica.

## Origine delle decisioni
Piano nato da sessione `/dr-warroom` (ARCH, BE, UI, UX, DBADMIN) del 2026-10-02, dalle risposte dell'utente e dalla fusione con il piano MVP (revisione R1, vedi in fondo).

### Decisioni confermate dall'utente
| # | Tema | Decisione |
|---|------|-----------|
| D1 | Caselle | Più caselle Gmail: tabella `Mailbox`, token e checkpoint per casella, parallelismo per casella |
| D2 | Storico | Il primo giro su una casella legge **tutto lo storico** (backfill completo, avanzamento visibile, **riprendibile** dopo un'interruzione) |
| D3 | Allerte | **Una allerta per mail**, anche se più filtri la intercettano; l'allerta elenca i filtri che l'hanno generata. Il software esterno la marca come consumata (`ConsumedAtUtc`) |
| D4 | Filtro oggetto | Un filtro ha mittente obbligatorio e **al massimo una** condizione sull'oggetto; più condizioni → più filtri |
| D5 | Topologia | **Host unico** `dr-mailroom-api` (Minimal API + Quartz) + libreria `dr-mailroom-core` + `dr-mailroom-tests`: un solo container. Uno split futuro in API + Worker tocca solo l'host |
| D6 | Spam/Cestino | Esclusi dalla lettura (`includeSpamTrash=false`), configurabile |
| D7 | Match mittente | Uguaglianza case-insensitive sull'indirizzo |
| D8 | Match oggetto | Tipo a scelta tra **`StartsWith`**, **`Contains`** e **`Regex`**; un solo tipo per filtro. `StartsWith`/`Contains` case-insensitive; `Regex` con `IgnoreCase | CultureInvariant` e **timeout** configurabile (timeout → nessun match + log di warning) |
| D9 | Statistiche | Contano **tutte** le mail lette, non solo quelle filtrate |
| D10 | Frontend | **Niente Blazor né Razor**: il backend espone solo API REST JSON; il frontend futuro `dr-mailroom-fe` è una SPA Vue 3 + Vite + TypeScript separata (tipologia `vue-fe`, pacchetto `dr-fe`), servita da nginx in un proprio container. In questo piano: **solo specifica** |
| D11 | Giro manuale | `POST /runs` avvia **subito** il giro; se la casella ha già un giro attivo → **409**. Nessuna coda nel DB |
| D12 | Backfill e filtri | Il backfill iniziale **solo indicizza** (statistiche): nessun processo sullo storico. I filtri valgono sulle mail nuove |
| D13 | Riapplica | `POST /filters/{id}/reapply` lavora le mail **già indicizzate** che corrispondono al filtro, con la possibilità di scegliere un **sottoinsieme dei processi** del filtro (es. "rilancia scaricando solo gli allegati", senza generare allerte sullo storico). Idempotente |
| D14 | Naming | Prefisso del progetto `dr-mailroom` + suffisso kebab-case per ruolo: `dr-mailroom-api`, `dr-mailroom-core`, `dr-mailroom-tests`, `dr-mailroom-fe`. Solution `dr-mailroom.slnx`. Il `RootNamespace` generato da .NET diventa `dr_mailroom_core` ecc. (trattino → underscore), coerente con la regola namespace `snake_case` |

### Decisioni tecniche del tavolo
| # | Tema | Decisione | Fonte |
|---|------|-----------|-------|
| T1 | Accesso | Gmail API (non IMAP), OAuth2, scope `https://www.googleapis.com/auth/gmail.readonly`: lo stato letto/non letto è immodificabile per costruzione | ARCH, BE |
| T2 | Cartelle | In Gmail le "cartelle" sono **label**: si legge per messaggio (`messageId`), non per cartella → nessun doppio conteggio; le label del messaggio si salvano come attributo | ARCH, BE |
| T3 | Checkpoint | Backfill: `pageToken` persistito su `Mailbox.BackfillPageToken` dopo ogni pagina (ripresa); `historyId` del profilo catturato **all'inizio** del backfill e usato come base dell'incrementale. Incrementale: `users.history.list` da `HistoryId`; HTTP 404 (history scaduta) → scansione completa idempotente | ARCH, BE |
| T4 | Idempotenza | UNIQUE `(MailboxId, GmailMessageId)`; insert idempotenti (2601/2627 = già presente); mai chiave sul `Message-ID` dell'header | DBADMIN |
| T5 | Statistiche | Viste SQL (`vw_SenderStats`, `vw_SenderMonthlyStats`) + colonna calcolata persistita `YearMonth` + indice coprente; `ReceivedAtUtc` da `internalDate` Gmail | DBADMIN, BE |
| T6 | Segreti | Refresh token cifrati con ASP.NET Core Data Protection; key ring su percorso configurabile (volume), fuori dal DB; client secret da variabili d'ambiente / user-secrets; token mai nei log | BE |
| T7 | Scheduler | Quartz.NET con cron da configurazione. Job schedulato con `[DisallowConcurrentExecution]`; giro manuale e riapplica via `TriggerJob` immediato su job separato | BE |
| T8 | Lock | Un giro alla volta per casella con `sp_getapplock` (risorsa `mailbox:<id>`, timeout 0, owner `Session` su connessione dedicata tenuta aperta per tutto il giro). Casella occupata: giro schedulato la salta (log), richiesta manuale → 409 | BE, DBADMIN |
| T9 | Resilienza | Parallelismo per casella e per messaggio con grado configurabile; retry con backoff esponenziale su 429/5xx | BE |
| T10 | Allegati | Root configurabile + sottocartella per filtro relativa alla root (no path assoluti dall'utente). Nome deterministico `yyyyMMdd-HHmmss_<mittente>_<nomefile>` sanificato (no path traversal), suffisso numerico su collisione; limite dimensione; SHA-256; UNIQUE `(MessageId, PartId)` → un allegato non viene mai scritto due volte | BE |
| T11 | Database | SQL Server (istanza esistente) con EF Core e migration; nessun trigger, nessuna stored procedure **custom** (`sp_getapplock` è di sistema) | DBADMIN |
| T12 | Google Cloud | App OAuth pubblicata **"In production"** (non verificata, uso personale): in stato "Testing" il refresh token scade dopo 7 giorni. `invalid_grant` → `Mailbox.Status = TokenRevoked` + `LastError` visibile | BE |

## Scope
### File da modificare / creare
- [ ] `.ai/dr-guidelines-packages.json`, `.github/instructions/*` — aggiornati dagli installer (Fase 2)
- [ ] `dr-mailroom.slnx` — solution
- [ ] `src/dr-mailroom-api/**` — host: endpoint, scheduler, configurazione
- [ ] `src/dr-mailroom-core/**` — dominio, persistenza, client Gmail, processi
- [ ] `test/dr-mailroom-tests/**` — test
- [ ] `docs/architettura.md` — decisioni architetturali
- [ ] `docs/frontend-specifiche.md` — specifica del frontend
- [ ] `docs/setup-google-oauth.md` — procedura Google Cloud passo passo
- [ ] `Dockerfile`, `docker-compose.yml`, `.dockerignore`, `.env.example`, `.gitignore` (`.env`)
- [ ] `README.md`

> I percorsi interni a `src/` e `test/` sono indicativi: la Fase 3 li allinea alle istruzioni dei pacchetti installati. Divergenza → `⚠️ Divergenza` e aggiornamento del piano prima di proseguire.

### Perimetro negativo
- Non toccherò: `briefs/`, `CLAUDE.md`, `.github/copilot-instructions.md` (salvo quanto scrivono gli installer), `.claude/skills/**`, `.mcp.json`
- Nessuna implementazione del frontend (solo specifica)
- Nessun sistema di notifica all'utente (lo fa il software esterno leggendo le allerte)
- Nessuna scrittura su Gmail: niente label, niente stato letto, niente cancellazioni
- Nessuna creazione di DB o login su SQL Server senza conferma esplicita della connection string
- Nessun `git push`

## Modello dati (SQL Server)
| Tabella | Colonne chiave | Vincoli e indici |
|---------|----------------|------------------|
| `Mailbox` | EmailAddress, EncryptedRefreshToken, HistoryId, BackfillPageToken, BackfillCompleted, Status (Active / TokenRevoked / Disabled), LastRunAtUtc, LastError | UNIQUE(EmailAddress) |
| `Message` | MailboxId, GmailMessageId, ThreadId, SenderAddress (lowercase), SenderName, Subject, ReceivedAtUtc, `YearMonth` (calcolata persistita), Labels, HasAttachments | UNIQUE(MailboxId, GmailMessageId); IX(MailboxId, SenderAddress, YearMonth) |
| `Filter` | MailboxId (null = tutte), Name, SenderAddress, SubjectMatchType (null / StartsWith / Contains / Regex), SubjectValue, AttachmentSubfolder, IsEnabled | CHECK: SubjectMatchType e SubjectValue entrambi valorizzati o entrambi null; IX(SenderAddress) |
| `FilterProcess` | FilterId, ProcessType (AttachmentExtraction / Alert) | PK(FilterId, ProcessType) |
| `Run` | MailboxId, Trigger (Scheduled / Manual / Reapply), FilterId (solo Reapply), Status (Running / Completed / Failed / Skipped), StartedAtUtc, EndedAtUtc, MessagesRead, MessagesProcessed, ErrorCount, ErrorSummary | IX(MailboxId, StartedAtUtc) |
| `ProcessExecution` | MessageId, ProcessType, Status, Error, ProcessedAtUtc | UNIQUE(MessageId, ProcessType) |
| `Alert` | MessageId, SenderAddress, Subject, ReceivedAtUtc, CreatedAtUtc, ConsumedAtUtc | UNIQUE(MessageId); IX(ConsumedAtUtc) |
| `AlertFilter` | AlertId, FilterId | PK(AlertId, FilterId) |
| `ExtractedAttachment` | MessageId, PartId, OriginalFileName, RelativePath, SizeBytes, Sha256 | UNIQUE(MessageId, PartId) |

Viste: `vw_SenderStats(MailboxId, SenderAddress, MailCount)`, `vw_SenderMonthlyStats(MailboxId, SenderAddress, YearMonth, MailCount)`, mappate come keyless entity.

## Fasi (formato atomico — obbligatorio)

### Fase 1: Prerequisiti
- **Stato**: [ ]
- **Precondizione**: piano approvato
- **File**: nessuno
- **Operazione**: verifica
- **Azione**: verificare SDK .NET 10, Docker, PowerShell 7 e raggiungibilità dell'istanza SQL Server (connection string fornita dall'utente, mai scritta in file committati). L'utente crea il progetto Google Cloud (serve dalla Fase 19; procedura fornita in anticipo)
- **Tool ammessi**: Bash/PowerShell in sola lettura
- **Verifica passo**: tutti i `--version` rispondono; connessione SQL riuscita
- **Su divergenza**: STOP — scrivi `⚠️ Divergenza Fase 1: <cosa>` in plan.md, non procedere

### Fase 2: Solution, progetti e pacchetti dr-*
- **Stato**: [ ]
- **Precondizione**: Fase 1 `[x]`
- **File**: `dr-mailroom.slnx`, `src/dr-mailroom-api/`, `src/dr-mailroom-core/`, `test/dr-mailroom-tests/`, `.ai/dr-guidelines-packages.json`, `.github/instructions/`
- **Operazione**: CREATE
- **Azione**: `/dr-scaffold-solution` con Minimal API `dr-mailroom-api`, classlib `dr-mailroom-core`, xUnit `dr-mailroom-tests`; riferimenti api→core, tests→core; pacchetti `dr-minimalapi` e `dr-efdb` (la skill installa i pacchetti: `dr-dotnet-backend` arriva come dipendenza, `dr-guidelines` è già presente). Nessun progetto frontend
- **Tool ammessi**: skill `dr-scaffold-solution`
- **Verifica passo**: `dotnet build dr-mailroom.slnx` exit 0; il manifest elenca i pacchetti
- **Su divergenza**: STOP — scrivi `⚠️ Divergenza Fase 2: <cosa>` in plan.md, non procedere

### Fase 3: Checkpoint convenzioni
- **Stato**: [ ]
- **Precondizione**: Fase 2 `[x]`
- **File**: questo `plan.md`
- **Operazione**: EDIT
- **Azione**: leggere le istruzioni dei pacchetti installati e sostituire i percorsi indicativi (Scope e Fasi) con quelli imposti (struttura Endpoints/, convenzioni EF, validatori). Mostrare la differenza all'utente
- **Tool ammessi**: nessuno
- **Verifica passo**: utente approva i percorsi aggiornati
- **Su divergenza**: STOP — scrivi `⚠️ Divergenza Fase 3: <cosa>` in plan.md, non procedere

### Fase 4: Documento di architettura
- **Stato**: [ ]
- **Precondizione**: Fase 3 `[x]`
- **File**: `docs/architettura.md`
- **Operazione**: CREATE
- **Azione**: decisioni D1–D14 e T1–T12, diagramma dei componenti, flusso di un giro (lock → checkpoint → lettura → ingestione → filtri → processi → checkpoint → rilascio lock), flusso della riapplica
- **Tool ammessi**: skill `dr-professor`
- **Verifica passo**: ogni decisione del piano compare nel documento
- **Su divergenza**: STOP — scrivi `⚠️ Divergenza Fase 4: <cosa>` in plan.md, non procedere

### Fase 5: Configurazione tipizzata
- **Stato**: [ ]
- **Precondizione**: Fase 3 `[x]`
- **File**: `src/dr-mailroom-core/Configuration/MailroomOptions.cs`, `src/dr-mailroom-api/appsettings.json`
- **Operazione**: CREATE / EDIT
- **Azione**: opzioni con `ValidateOnStart`: `Schedule.Cron`, `Processing.MaxParallelMailboxes`, `Processing.MaxParallelMessages`, `Processing.IncludeSpamTrash`, `Filtering.RegexTimeoutMs`, `Attachments.RootPath`, `Attachments.MaxSizeBytes`, `DataProtection.KeyRingPath`, `Gmail.ClientId`, `Frontend.BaseUrl` (CORS e redirect post-OAuth); client secret, connection string e chiave API **non** in appsettings (env / user-secrets)
- **Tool ammessi**: nessuno
- **Verifica passo**: build ok; avvio con opzione obbligatoria mancante fallisce con messaggio esplicito
- **Su divergenza**: STOP — scrivi `⚠️ Divergenza Fase 5: <cosa>` in plan.md, non procedere

### Fase 6: Modello di dominio ed entità EF Core
- **Stato**: [ ]
- **Precondizione**: Fase 5 `[x]`
- **File**: `src/dr-mailroom-core/Domain/*.cs`, `src/dr-mailroom-core/Persistence/MailroomDbContext.cs`, `src/dr-mailroom-core/Persistence/Configurations/*.cs`
- **Operazione**: CREATE
- **Azione**: entità e configurazioni della sezione "Modello dati", con vincoli UNIQUE, CHECK e indici
- **Tool ammessi**: nessuno
- **Verifica passo**: build ok; test che verifica nel modello EF gli indici UNIQUE e il CHECK di `Filter`
- **Su divergenza**: STOP — scrivi `⚠️ Divergenza Fase 6: <cosa>` in plan.md, non procedere

### Fase 7: Migration iniziale con indici e viste statistiche
- **Stato**: [ ]
- **Precondizione**: Fase 6 `[x]`; connection string disponibile fuori dai file committati
- **File**: `src/dr-mailroom-core/Persistence/Migrations/*`
- **Operazione**: CREATE
- **Azione**: migration iniziale; viste `vw_SenderStats` e `vw_SenderMonthlyStats` via `migrationBuilder.Sql` con `Down` simmetrico
- **Tool ammessi**: `dotnet ef`; dr-mcp-dbschema (read-only) facoltativo
- **Verifica passo**: `dotnet ef database update` su DB di sviluppo (confermato dall'utente) exit 0; `SELECT` sulle viste senza errori
- **Su divergenza**: STOP — scrivi `⚠️ Divergenza Fase 7: <cosa>` in plan.md, non procedere

### Fase 8: Protezione dei token
- **Stato**: [ ]
- **Precondizione**: Fase 6 `[x]`
- **File**: `src/dr-mailroom-core/Security/TokenProtector.cs`, `src/dr-mailroom-api/Program.cs`
- **Operazione**: CREATE / EDIT
- **Azione**: Data Protection con `PersistKeysToFileSystem(KeyRingPath)` e `SetApplicationName("dr-mailroom")`; `ITokenProtector` (Protect/Unprotect) per `EncryptedRefreshToken`; nessun log del token
- **Tool ammessi**: nessuno
- **Verifica passo**: test round-trip; grep: nessun log che contenga il token
- **Su divergenza**: STOP — scrivi `⚠️ Divergenza Fase 8: <cosa>` in plan.md, non procedere

### Fase 9: Client Gmail in sola lettura
- **Stato**: [ ]
- **Precondizione**: Fase 8 `[x]`
- **File**: `src/dr-mailroom-core/Gmail/IGmailReader.cs`, `src/dr-mailroom-core/Gmail/GmailReader.cs`
- **Operazione**: CREATE
- **Azione**: wrapper su `Google.Apis.Gmail.v1`, scope unico `gmail.readonly`: `GetProfileHistoryIdAsync`, `ListMessageIdsPageAsync(pageToken)` (rispetta `IncludeSpamTrash`), `ListChangesSinceAsync(historyId)` (solo `messageAdded`; 404 → `HistoryExpiredException`), `GetMetadataAsync` (`format=metadata`: From, Subject, `internalDate`, labelIds, threadId, presenza allegati), `GetAttachmentsAsync`; backoff su 429/5xx; `invalid_grant` → `TokenRevokedException`
- **Tool ammessi**: nessuno
- **Verifica passo**: test con handler HTTP finto: 404 → `HistoryExpiredException`; 429 → ritentativo; `invalid_grant` → `TokenRevokedException`
- **Su divergenza**: STOP — scrivi `⚠️ Divergenza Fase 9: <cosa>` in plan.md, non procedere

### Fase 10: Ingestione idempotente e checkpoint
- **Stato**: [ ]
- **Precondizione**: Fasi 7 e 9 `[x]`
- **File**: `src/dr-mailroom-core/Ingestion/MailboxIngestionService.cs`
- **Operazione**: CREATE
- **Azione**: backfill se `BackfillCompleted = false`: al primo avvio salvare l'`historyId` del profilo, poi pagine da `BackfillPageToken` salvato dopo ogni pagina (ripresa); a fine backfill `BackfillCompleted = true`. Altrimenti incrementale da `HistoryId`; 404 → scansione completa. Insert idempotente su `Message`. Restituisce gli id dei messaggi **inseriti** (non duplicati) e se il giro era di backfill (D12: niente filtri). `Run.MessagesRead` aggiornato periodicamente
- **Tool ammessi**: nessuno
- **Verifica passo**: test con client finto: doppia ingestione → nessun duplicato; interruzione a metà backfill → ripresa dalla pagina salvata; errore nell'incrementale → `HistoryId` invariato; secondo giro consecutivo incrementale
- **Su divergenza**: STOP — scrivi `⚠️ Divergenza Fase 10: <cosa>` in plan.md, non procedere

### Fase 11: Motore dei filtri
- **Stato**: [ ]
- **Precondizione**: Fase 6 `[x]`
- **File**: `src/dr-mailroom-core/Filtering/FilterMatcher.cs`
- **Operazione**: CREATE
- **Azione**: funzione pura: messaggio + filtri attivi della casella → processi da applicare (unione) e filtri corrispondenti. Mittente: uguaglianza case-insensitive (D7); oggetto: `StartsWith` / `Contains` / `Regex` (D8), regex con timeout e cache delle istanze compilate
- **Tool ammessi**: nessuno
- **Verifica passo**: test: mittente diverso, oggetto assente, `StartsWith` vero/falso, `Contains` vero/falso, `Regex` vero/falso, regex che va in timeout → nessun match senza eccezione, due filtri con processi sovrapposti → processi deduplicati
- **Su divergenza**: STOP — scrivi `⚠️ Divergenza Fase 11: <cosa>` in plan.md, non procedere

### Fase 12: Processo "estrazione allegati"
- **Stato**: [ ]
- **Precondizione**: Fasi 9 e 11 `[x]`
- **File**: `src/dr-mailroom-core/Processes/AttachmentExtractionProcess.cs`, `src/dr-mailroom-core/Processes/SafeFileName.cs`
- **Operazione**: CREATE
- **Azione**: per ogni parte allegato: se esiste già `ExtractedAttachment(MessageId, PartId)` → salta; altrimenti scarica, scrive su file temporaneo, sposta in `RootPath/<AttachmentSubfolder>/yyyyMMdd-HHmmss_<mittente>_<nomefile>` (sanificato, dentro la root, suffisso su collisione), registra `ExtractedAttachment` con SHA-256; allegati oltre `MaxSizeBytes` saltati con motivo; esito in `ProcessExecution`
- **Tool ammessi**: nessuno
- **Verifica passo**: test: `..\..\x.exe` resta dentro la root; riesecuzione → nessun file nuovo; crash simulato dopo la scrittura del primo allegato → la ripresa scrive solo i mancanti
- **Su divergenza**: STOP — scrivi `⚠️ Divergenza Fase 12: <cosa>` in plan.md, non procedere

### Fase 13: Processo "allerta"
- **Stato**: [ ]
- **Precondizione**: Fase 11 `[x]`
- **File**: `src/dr-mailroom-core/Processes/AlertProcess.cs`
- **Operazione**: CREATE
- **Azione**: una sola `Alert` per messaggio (D3) con mittente, oggetto, data di ricezione; `AlertFilter` per tutti i filtri corrispondenti (aggiunti anche su riapplica se l'allerta esiste già)
- **Tool ammessi**: nessuno
- **Verifica passo**: test: due filtri con Alert sullo stesso messaggio → 1 Alert, 2 AlertFilter; riesecuzione → nessuna nuova Alert
- **Su divergenza**: STOP — scrivi `⚠️ Divergenza Fase 13: <cosa>` in plan.md, non procedere

### Fase 14: Orchestrazione dei giri, lock e scheduler
- **Stato**: [ ]
- **Precondizione**: Fasi 10, 12, 13 `[x]`
- **File**: `src/dr-mailroom-core/Runs/RunOrchestrator.cs`, `src/dr-mailroom-core/Runs/MailboxLock.cs`, `src/dr-mailroom-api/Scheduling/ScheduledRunJob.cs`, `src/dr-mailroom-api/Scheduling/OnDemandRunJob.cs`, `Program.cs`
- **Operazione**: CREATE / EDIT
- **Azione**: `MailboxLock` su `sp_getapplock` (T8). Job schedulato (cron, `[DisallowConcurrentExecution]`): per ogni casella attiva, in parallelo fino a `MaxParallelMailboxes`, prova il lock; occupata → `Run` Skipped. Job on-demand (manuale e riapplica) avviato con `TriggerJob` e `RunId` nel JobDataMap. Giro: ingestione → filtri sui messaggi inseriti (non nel backfill, D12) → processi. Riapplica: messaggi già indicizzati che corrispondono al filtro → solo i processi richiesti (D13). Errori di una casella non fermano le altre; `TokenRevokedException` → `Mailbox.Status = TokenRevoked`
- **Tool ammessi**: nessuno
- **Verifica passo**: test: lock occupato → seconda richiesta rifiutata; errore su casella A non blocca B; backfill non crea allerte né file; riapplica con solo `AttachmentExtraction` non crea allerte; riapplica ripetuta non duplica nulla
- **Su divergenza**: STOP — scrivi `⚠️ Divergenza Fase 14: <cosa>` in plan.md, non procedere

### Fase 15: Endpoint API
- **Stato**: [ ]
- **Precondizione**: Fase 14 `[x]`
- **File**: `src/dr-mailroom-api/Endpoints/*.cs`, `src/dr-mailroom-api/Validators/*.cs`
- **Operazione**: CREATE
- **Azione**: endpoint con validatore esplicito per ogni body:
  - Caselle: lista, stato (ultimo giro, avanzamento backfill, errore, token valido), `GET /mailboxes/connect` e callback OAuth (`access_type=offline`, `prompt=consent`; redirect a `Frontend.BaseUrl` con esito in query string), rimozione
  - Filtri: CRUD; `POST /filters/preview` (dry-run sulle mail già indicizzate); `POST /filters/{id}/reapply` con body `{ processes?: [...] }` (sottoinsieme dei processi del filtro, default tutti) → 202 + `RunId`, 409 se casella occupata
  - Giri: `POST /runs` (casella o tutte) → 202 + `RunId`, 409 se occupata; `GET /runs`, `GET /runs/{id}`
  - Statistiche: `GET /stats/senders`, `GET /stats/senders/monthly`
  - Allerte: `GET /alerts?consumed=false`, `POST /alerts/{id}/consume`
  - Allegati: `GET /messages/{id}/attachments`
  - Health: `GET /health`
  - Autenticazione: chiave API in header da ambiente; CORS limitato a `Frontend.BaseUrl`; OpenAPI per il client Vue; solo REST JSON (D10)
- **Tool ammessi**: nessuno
- **Verifica passo**: test di integrazione: senza chiave API → 401; mittente mancante → 400; `SubjectMatchType` senza `SubjectValue` o viceversa → 400; tipo non tra `StartsWith`/`Contains`/`Regex` → 400; regex non compilabile → 400; nessun processo → 400; reapply con processo non presente nel filtro → 400; doppio avvio → 409
- **Su divergenza**: STOP — scrivi `⚠️ Divergenza Fase 15: <cosa>` in plan.md, non procedere

### Fase 16: Container
- **Stato**: [ ]
- **Precondizione**: Fase 15 `[x]`
- **File**: `Dockerfile`, `docker-compose.yml`, `.dockerignore`, `.env.example`, `.gitignore`
- **Operazione**: CREATE / EDIT
- **Azione**: immagine multi-stage .NET 10; volumi per `Attachments.RootPath` e `DataProtection.KeyRingPath`; segreti via `.env` non committato; porta API raggiungibile da `localhost` per il redirect OAuth; stesso binario eseguibile anche in locale/servizio
- **Tool ammessi**: skill `dr-tech` (consultiva), docker
- **Verifica passo**: `docker build` exit 0; container risponde a `/health`
- **Su divergenza**: STOP — scrivi `⚠️ Divergenza Fase 16: <cosa>` in plan.md, non procedere

### Fase 17: Specifica del frontend
- **Stato**: [ ]
- **Precondizione**: Fase 15 `[x]`
- **File**: `docs/frontend-specifiche.md`
- **Operazione**: CREATE
- **Azione**: SPA `dr-mailroom-fe` (D10) con 4 viste: Caselle (OAuth, stati, avanzamento backfill, token revocato), Filtri (mittente, oggetto con tipo `StartsWith`/`Contains`/`Regex`, processi multipli, sottocartella; anteprima dry-run; "Riapplica" con scelta dei processi), Esecuzioni ("Esegui ora" con `aria-busy`, gestione 409, polling 3–5 s, cronologia), Statistiche (tabelle ordinabili); avviso che il primo giro è lungo e non applica i filtri; accessibilità (label esplicite, `aria-live`); mapping di ogni endpoint
- **Tool ammessi**: skill `dr-professor`
- **Verifica passo**: ogni endpoint della Fase 15 è referenziato da almeno una vista (tranne quelli per il software esterno, dichiarati)
- **Su divergenza**: STOP — scrivi `⚠️ Divergenza Fase 17: <cosa>` in plan.md, non procedere

### Fase 18: README e guida Google Cloud
- **Stato**: [ ]
- **Precondizione**: Fase 16 `[x]`
- **File**: `README.md`, `docs/setup-google-oauth.md`
- **Operazione**: CREATE / EDIT
- **Azione**: guida Google Cloud (progetto, abilitazione Gmail API, schermata di consenso, **pubblicazione "In production"** con spiegazione dei 7 giorni in "Testing", client OAuth, redirect URI); README con configurazione segreti, migration, avvio locale e container, collegamento caselle
- **Tool ammessi**: skill `dr-professor`
- **Verifica passo**: struttura conforme a `readme-structure.instructions.md`; nessun segreto nei file
- **Su divergenza**: STOP — scrivi `⚠️ Divergenza Fase 18: <cosa>` in plan.md, non procedere

### Fase 19: Verifica end-to-end
- **Stato**: [ ]
- **Precondizione**: Fasi 1–18 `[x]`; progetto Google Cloud pronto
- **File**: nessuno
- **Operazione**: verifica
- **Azione**: collegamento reale della casella; backfill; controllo stato letto/non letto su un campione; statistiche confrontate con Gmail su un mittente campione; filtro di test su mail nuova → allerta e allegato; riapplica con solo allegati su una mail storica di un fornitore
- **Tool ammessi**: `dr-verify-plan`
- **Verifica passo**: tutti i criteri finali soddisfatti
- **Su divergenza**: STOP — scrivi `⚠️ Divergenza Fase 19: <cosa>` in plan.md, non procedere

## Criteri di verifica finale
- [ ] `dotnet build dr-mailroom.slnx` e `dotnet test` exit 0
- [ ] `dotnet format <progetto>.csproj --verify-no-changes` exit 0 su ogni progetto
- [ ] Scope OAuth unico `gmail.readonly` nel codice; nessuna chiamata Gmail di modifica
- [ ] Indici UNIQUE `(MailboxId, GmailMessageId)`, `Alert.MessageId`, `(MessageId, ProcessType)`, `(MessageId, PartId)` e CHECK su `Filter` presenti nella migration
- [ ] Viste `vw_SenderStats` e `vw_SenderMonthlyStats` presenti e interrogabili
- [ ] Nessun segreto in file tracciati (`git grep` su secret, connection string, client secret, chiave API); token mai loggati
- [ ] Logging strutturato con placeholder, nessuna interpolazione nei log
- [ ] Stato letto/non letto di un campione di mail non lette invariato dopo il giro
- [ ] Backfill interrotto e ripreso dalla pagina salvata; secondo giro consecutivo incrementale
- [ ] Backfill senza allerte né allegati; statistiche totale e mensile corrette su un mittente campione
- [ ] Filtro di test (anche `Regex`): allerta creata una sola volta, allegato salvato con nome deterministico e collegato alla mail
- [ ] Riapplica con solo `AttachmentExtraction`: scarica gli allegati dello storico, nessuna allerta, nessun duplicato alla seconda esecuzione
- [ ] Giro manuale: parte subito, `RunId` interrogabile, doppio avvio → 409
- [ ] `HistoryId` scaduto gestito con scansione completa senza duplicati
- [ ] `docs/architettura.md`, `docs/frontend-specifiche.md`, `docs/setup-google-oauth.md`, `README.md` presenti
- [ ] `docker compose up` funzionante con volumi persistenti
- [ ] Verifica a freddo con `/dr-verify-plan`

## Revisioni
### R1 — 2026-10-02: fusione con il piano MVP
- Match oggetto: aggiunto `Regex` (richiesta utente)
- Recuperati dal piano MVP: ripresa del backfill con `pageToken`, `ConsumedAtUtc` sulle allerte, idempotenza allegati (`PartId` UNIQUE, SHA-256, nome deterministico), nota Google "In production", fase prerequisiti, checkpoint convenzioni, verifica E2E e criteri funzionali, `ThreadId`/`SenderName`/`HasAttachments`, endpoint allegati
- Nuove decisioni: D11 (giro manuale immediato + 409, `sp_getapplock`, niente coda nel DB), D12 (backfill solo indicizzazione), D13 (riapplica per filtro con sottoinsieme di processi), D14 (naming kebab-case `dr-mailroom-*`)
- Fasi 1 e 2 del v2 unite: `/dr-scaffold-solution` installa già i pacchetti
