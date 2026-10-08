# Piano: dr-mailroom — servizio C# di lettura Gmail, filtri, processi e statistiche
Brief: [20261001-01.md](../../../briefs/20261001-01.md)
Data: 2026-10-02
Stato: INTERROTTO
Sostituisce: [2026-10-02-dr-mailroom-mvp](../2026-10-02-dr-mailroom-mvp/plan.md) (SUPERATO)

> **Nota 2026-10-08** — Da rivedere quando il frontend ([2026-10-08-dr-mailroom-fe](../2026-10-08-dr-mailroom-fe/plan.md)) è pronto: verificare cosa resta aperto (criteri di verifica finali non spuntati) e decidere se completare, chiudere o archiviare. Fino ad allora non blocca altri piani.

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
| D15 | Validazione body | Regole approvate il 2026-10-05: filtro (name obbl. ≤100; senderAddress email valida ≤320; mailboxId facoltativo esistente; subjectMatchType+subjectValue insieme, valore ≤500, regex compilabile; attachmentSubfolder ≤200 relativo senza `..`, obbligatorio con AttachmentExtraction; processes ≥1 senza duplicati; isEnabled default true); anteprima = stesse regole senza name/processes/subfolder; riapplica processes facoltativo ⊆ processi del filtro; giro mailboxId facoltativo, esistente e attiva |
| D16 | Riapplica filtro globale | Un giro per ogni casella attiva; 202 con lista RunId; caselle occupate segnalate come saltate; 409 solo se tutte occupate |
| D17 | Giro su tutte le caselle | `POST /runs` senza mailboxId → un giro per casella attiva, stessa semantica di D16 |
| D18 | Rimozione casella | Nessuna cancellazione fisica: `Status = Deleted`, refresh token rimosso; nessun giro, nessuna modifica a filtri/dati della casella (409); statistiche conservate; ricollegare via OAuth un indirizzo `Deleted` è rifiutato |

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

### Convenzioni dei pacchetti (Fase 3, approvate il 2026-10-05)
| # | Tema | Decisione | Fonte |
|---|------|-----------|-------|
| C1 | URL | Tutti gli endpoint sotto `api/v{version:apiVersion}/<gruppo>` (v1), in `Endpoints/*Mapping.cs` con `WithTags`/`WithApiVersionSet`/`MapToApiVersion` e metadata OpenAPI completi; Scalar (no Swagger UI) | minimal-api regole 1–8 |
| C2 | Callback OAuth | Versionato: `http://localhost:5180/api/v1/mailboxes/connect/callback` — redirect URI da aggiornare in Google Console | scelta utente |
| C3 | Persistenza | `src/dr-mailroom-core/Infrastructure/Mailroom/`: `MailroomDbContext` (primary ctor con `ILoggerFactory`, NoTracking di default), provider per area (`MailboxProvider`, `FilterProvider`, `MessageProvider`, `RunProvider`, `AlertProvider`, `StatsProvider`), `MailroomProviderExtensions`, `Entities/` con `[Table]`+`[Column]` su ogni proprietà, `Configurations/` (Fluent API per chiavi, indici, CHECK), `Filters/` con `ToExpression()`, `Migrations/` | database-provider |
| C4 | SQL logging | `EnableSensitiveDataLogging` attivo come da istruzione (scelta esplicita dell'utente; deroga a T6 **non** applicata: i valori dei parametri SQL, incluso il refresh token **cifrato**, possono comparire nei log EF). I log applicativi non contengono mai il token | scelta utente |
| C5 | Layer API | Handler → `Services/<Area>Service.cs` → provider; `Dto/` con `<Entity>Dto` + `<Entity>SummaryDto` e `static Projection`; GET di lista con `Filter` + `FilterValidator` + `GET /summary`; lista vuota → 404 ProblemDetails | minimal-api regole 9, 12 |
| C6 | Validazione | `Validators/IValidator.cs` (interfaccia propria, no FluentValidation) + un validatore per body e per filtro; regole dei body chieste all'utente campo per campo in Fase 15 | input-validation |
| C7 | Salute DB | `/health` (MapHealthChecks) + `GET /api/v1/status` + `POST /api/v1/status/retry-database`; `DatabaseStartupService` singleton no-throw, `DatabaseExceptionHandler` → 503; avvio con DB giù = stato degradato | regola 11, database-startup-resilience |
| C8 | Autenticazione | Chiave API con `SimpleAuthenticationTools` (nessun utente con login); status/retry `AllowAnonymous` | minimal-api "Autenticazione" |
| C9 | Stack | `Tinyhelpers.AspNetCore`, `Scalar.AspNetCore`, `Asp.Versioning.Mvc.ApiExplorer`, Serilog (console + file giornaliero `logs/`, enricher ThreadId/TaskId) | minimal-api Stack, logging |
| C10 | Segreti locali | `appsettings.local.json` (già in `.gitignore`) caricato da `Program.cs` con `optional: true`: connection string, client secret Google, chiave API. Container: variabili d'ambiente. Sostituisce gli user-secrets | sensitive-data |
| C11 | Data Protection | `PersistKeysToFileSystem` su volume (alternativa prevista dall'istruzione) per tenere il key ring fuori dal DB (T6) | minimal-api "Rilascio in container" |
| C12 | Debug | `.vscode/launch.json` e `tasks.json` (`coreclr`), profilo IIS Express in `launchSettings.json`, file `.http` | minimal-api Stack |

## Scope
### File da modificare / creare
- [x] `.ai/dr-guidelines-packages.json`, `.github/instructions/*` — aggiornati dagli installer (Fase 2)
- [x] `dr-mailroom.slnx` — solution
- [x] `src/dr-mailroom-api/**` — host: `Endpoints/`, `Services/`, `Dto/`, `Validators/`, `Transformers/`, `Properties/`, `Infrastructure/{Database,Logging}/`, `Scheduling/`, `Program.cs`, `appsettings*.json`, `dr-mailroom-api.http`
- [x] `src/dr-mailroom-core/**` — `Infrastructure/Mailroom/` (persistenza, C3), `Configuration/`, `Enums/`, `Security/`, `Gmail/`, `Ingestion/`, `Filtering/`, `Processes/`, `Runs/`
- [x] `test/dr-mailroom-tests/**` — test
- [x] `docs/architettura.md` — decisioni architetturali
- [x] `docs/frontend-specifiche.md` — specifica del frontend
- [x] `docs/setup-google-oauth.md` — procedura Google Cloud passo passo
- [x] `docs/avvio-e-configurazione.md` — segreti, migration, avvio locale e container, test (Fase 18, divergenza risolta)
- [x] `Dockerfile`, `docker-compose.yml`, `.dockerignore`, `.env.example`, `.gitignore` (`.env`)
- [x] `README.md`
- [x] `.vscode/launch.json`, `.vscode/tasks.json` — debug (C12)
- [x] `Directory.Build.props` — sostituire i placeholder `YourCompany` / `Minimal API Template`

> Percorsi allineati alle istruzioni dei pacchetti in Fase 3 (C1–C12).

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
- **Stato**: [x]
- **Precondizione**: piano approvato
- **File**: nessuno
- **Operazione**: verifica
- **Azione**: verificare SDK .NET 10, Docker, PowerShell 7 e raggiungibilità dell'istanza SQL Server (connection string fornita dall'utente, mai scritta in file committati). L'utente crea il progetto Google Cloud (serve dalla Fase 19; procedura fornita in anticipo)
- **Tool ammessi**: Bash/PowerShell in sola lettura
- **Verifica passo**: tutti i `--version` rispondono; connessione SQL riuscita
- **Su divergenza**: STOP — scrivi `⚠️ Divergenza Fase 1: <cosa>` in plan.md, non procedere
- **Esito 2026-10-02**: SDK 10.0.401 ✓, dotnet-ef 10.0.7 ✓, pwsh 7.6.6 ✓, Docker CLI 29.7.2 ✓ (daemon spento: serve da Fase 16), sqlcmd ✓. SQL Server 2022 Developer su `spectre`, DB `dr-mailroom` esistente, login `dr-mailroom-user` con `db_owner` ✓ (credenziali solo in user-secrets/env, mai su file). Redirect URI OAuth fissato in anticipo: `http://localhost:5180/mailboxes/connect/callback` (porta API 5180 in locale e container)

### Fase 2: Solution, progetti e pacchetti dr-*
- **Stato**: [x]
- **Precondizione**: Fase 1 `[x]`
- **File**: `dr-mailroom.slnx`, `src/dr-mailroom-api/`, `src/dr-mailroom-core/`, `test/dr-mailroom-tests/`, `.ai/dr-guidelines-packages.json`, `.github/instructions/`
- **Operazione**: CREATE
- **Azione**: `/dr-scaffold-solution` con Minimal API `dr-mailroom-api`, classlib `dr-mailroom-core`, xUnit `dr-mailroom-tests`; riferimenti api→core, tests→core; pacchetti `dr-minimalapi` e `dr-efdb` (la skill installa i pacchetti: `dr-dotnet-backend` arriva come dipendenza, `dr-guidelines` è già presente). Nessun progetto frontend
- **Tool ammessi**: skill `dr-scaffold-solution`
- **Verifica passo**: `dotnet build dr-mailroom.slnx` exit 0; il manifest elenca i pacchetti
- **Su divergenza**: STOP — scrivi `⚠️ Divergenza Fase 2: <cosa>` in plan.md, non procedere
- **Esito 2026-10-03**: `dr-mailroom.slnx` con 3 progetti, ref api→core e tests→core; pacchetti dr-dotnet-backend (3c12474), dr-minimalapi (e92bd3e), dr-efdb (fdc9030) nel manifest; porta http 5180 in `launchSettings.json`; `dotnet format` applicato; build 0 avvisi / 0 errori; `--verify-no-changes` exit 0. Nessun commit (non richiesto)

### Fase 3: Checkpoint convenzioni
- **Stato**: [x]
- **Precondizione**: Fase 2 `[x]`
- **File**: questo `plan.md`
- **Operazione**: EDIT
- **Azione**: leggere le istruzioni dei pacchetti installati e sostituire i percorsi indicativi (Scope e Fasi) con quelli imposti (struttura Endpoints/, convenzioni EF, validatori). Mostrare la differenza all'utente
- **Tool ammessi**: nessuno
- **Verifica passo**: utente approva i percorsi aggiornati
- **Su divergenza**: STOP — scrivi `⚠️ Divergenza Fase 3: <cosa>` in plan.md, non procedere
- **Esito 2026-10-05**: mappa approvata dall'utente; decisioni C1–C12 registrate; Scope e Fasi aggiornati

### Fase 4: Documento di architettura
- **Stato**: [x]
- **Precondizione**: Fase 3 `[x]`
- **File**: `docs/architettura.md`
- **Operazione**: CREATE
- **Azione**: decisioni D1–D14 e T1–T12, diagramma dei componenti, flusso di un giro (lock → checkpoint → lettura → ingestione → filtri → processi → checkpoint → rilascio lock), flusso della riapplica
- **Tool ammessi**: skill `dr-professor`
- **Verifica passo**: ogni decisione del piano compare nel documento
- **Su divergenza**: STOP — scrivi `⚠️ Divergenza Fase 4: <cosa>` in plan.md, non procedere
- **Esito 2026-10-05**: `docs/architettura.md` v1.0 creato; grep conferma D1–D14, T1–T12, C1–C12 presenti; 5 diagrammi Mermaid (componenti, giro, backfill, riapplica, ER). **Revisione v1.2 (2026-10-06)**: allineato al codice dopo l'implementazione — diagramma del giro (Run creato da API/scheduler, 409 lato API, guardia `Deleted`, checkpoint nello stesso SaveChanges dei messaggi, esito con contesto dedicato), riapplica (D16, sonda del lock), modello dati (`Deleted`, `RequestedProcesses`, `SavedAtUtc`, `YearMonth` int, enum testuali, regole di cancellazione), nuova sezione "Limiti noti". Proposto come standard su dr-guidelines: [#10](https://github.com/davraf-amuro/dr-guidelines/issues/10) (struttura del documento) e [#11](https://github.com/davraf-amuro/dr-guidelines/issues/11) (riallineamento al codice e verifica)

### Fase 5: Configurazione tipizzata
- **Stato**: [x]
- **Precondizione**: Fase 3 `[x]`
- **File**: `src/dr-mailroom-core/Configuration/*Options.cs` (una classe per file), `src/dr-mailroom-api/appsettings.json`, `src/dr-mailroom-api/appsettings.local.json` (non tracciato), `src/dr-mailroom-api/Program.cs`, `src/dr-mailroom-api/Infrastructure/Logging/TaskIdEnricher.cs`, `Directory.Build.props`, `.gitignore` (`logs/`)
- **Operazione**: CREATE / EDIT
- **Azione**: opzioni con `ValidateOnStart`: `Schedule.Cron`, `Processing.MaxParallelMailboxes`, `Processing.MaxParallelMessages`, `Processing.IncludeSpamTrash`, `Filtering.RegexTimeoutMs`, `Attachments.RootPath`, `Attachments.MaxSizeBytes`, `DataProtection.KeyRingPath`, `Gmail.ClientId`, `Frontend.BaseUrl` (CORS e redirect post-OAuth); client secret, connection string e chiave API **non** in appsettings: in `appsettings.local.json` (C10). Serilog configurato (C9). Placeholder di `Directory.Build.props` sostituiti
- **Tool ammessi**: nessuno
- **Verifica passo**: build ok; avvio con opzione obbligatoria mancante fallisce con messaggio esplicito
- **Su divergenza**: STOP — scrivi `⚠️ Divergenza Fase 5: <cosa>` in plan.md, non procedere
- **Esito 2026-10-05**: 7 classi `*Options` (una per file) + `MailroomOptionsExtensions` con `ValidateDataAnnotations`/`ValidateOnStart` e controllo cron Quartz (`new CronExpression`, Quartz 4 non ha `IsValidExpression`); aggiunto `Gmail.RedirectUri` (C2). Serilog + `TaskIdEnricher`; `appsettings.local.json` (ignorato) con connection string e credenziali Google; `logs/` e `src/dr-mailroom-api/data/` in `.gitignore`; `Directory.Build.props` aggiornato. Verifica: build 0/0, avvio valido resta su, cron non valido → `OptionsValidationException: Mailroom:Schedule:Cron non è un'espressione cron Quartz valida`, RootPath vuoto → `DataAnnotation validation failed for 'AttachmentsOptions'`; nessun segreto nei file tracciabili

### Fase 6: Modello di dominio ed entità EF Core
- **Stato**: [x]
- **Precondizione**: Fase 5 `[x]`
- **File**: `src/dr-mailroom-core/Enums/*.cs`, `src/dr-mailroom-core/Infrastructure/Mailroom/Entities/*.cs`, `src/dr-mailroom-core/Infrastructure/Mailroom/Configurations/*.cs`, `src/dr-mailroom-core/Infrastructure/Mailroom/MailroomDbContext.cs`, `src/dr-mailroom-core/Infrastructure/Mailroom/MailroomProviderExtensions.cs`
- **Operazione**: CREATE
- **Azione**: entità e configurazioni della sezione "Modello dati", con vincoli UNIQUE, CHECK e indici
- **Tool ammessi**: nessuno
- **Verifica passo**: build ok; test che verifica nel modello EF gli indici UNIQUE e il CHECK di `Filter`
- **Su divergenza**: STOP — scrivi `⚠️ Divergenza Fase 6: <cosa>` in plan.md, non procedere
- **Esito 2026-10-05**: 6 enum (salvati come stringa), 11 entità con `[Table]`/`[Column]`, 11 `IEntityTypeConfiguration`, `ColumnLengths`, `MailroomViews`, `MailroomDbContext` (C4), `MailroomProviderExtensions` (`AddDbContextFactory`, NoTracking). Dettagli: `YearMonth` = `int` calcolato persistito `YEAR*100+MONTH`; `HistoryId` = `ulong?`; aggiunta colonna `Run.RequestedProcesses` (sottoinsieme processi D13) e `ExtractedAttachment.SavedAtUtc`; cancellazioni Filter→Mailbox `NoAction`, Run→Filter `SetNull`. Test `MailroomModelTests`: 10/10 verdi (5 UNIQUE, CHECK `CK_Filter_Subject`, colonna calcolata, indice statistiche, 2 viste keyless); build 0/0; format pulito

### Fase 7: Migration iniziale con indici e viste statistiche
- **Stato**: [x]
- **Precondizione**: Fase 6 `[x]`; connection string disponibile fuori dai file committati
- **File**: `src/dr-mailroom-core/Infrastructure/Mailroom/Migrations/*`
- **Operazione**: CREATE
- **Azione**: migration iniziale; viste `vw_SenderStats` e `vw_SenderMonthlyStats` via `migrationBuilder.Sql` con `Down` simmetrico
- **Tool ammessi**: `dotnet ef`; dr-mcp-dbschema (read-only) facoltativo
- **Verifica passo**: `dotnet ef database update` su DB di sviluppo (confermato dall'utente) exit 0; `SELECT` sulle viste senza errori
- **Su divergenza**: STOP — scrivi `⚠️ Divergenza Fase 7: <cosa>` in plan.md, non procedere
- **Esito 2026-10-05**: migration `InitialCreate` con viste `vw_SenderStats`/`vw_SenderMonthlyStats` via `migrationBuilder.Sql` (nomi da `MailroomViews`) e `Down` simmetrico (DROP VIEW prima delle tabelle). Per `dotnet ef`: aggiunti `Microsoft.EntityFrameworkCore.Design` all'API e `AddMailroomProvider` in `Program.cs`. DB vuoto verificato, update confermato dall'utente: exit 0; presenti 9 tabelle + `__EFMigrationsHistory`, 2 viste (`SELECT COUNT(*)` = 0 senza errori), `CK_Filter_Subject`, 5 indici UNIQUE. Nota: dotnet-ef 10.0.7 < runtime 10.0.12 (solo avviso)

### Fase 8: Protezione dei token
- **Stato**: [x]
- **Precondizione**: Fase 6 `[x]`
- **File**: `src/dr-mailroom-core/Security/TokenProtector.cs`, `src/dr-mailroom-api/Program.cs`
- **Operazione**: CREATE / EDIT
- **Azione**: Data Protection con `PersistKeysToFileSystem(KeyRingPath)` e `SetApplicationName("dr-mailroom")`; `ITokenProtector` (Protect/Unprotect) per `EncryptedRefreshToken`; nessun log del token
- **Tool ammessi**: nessuno
- **Verifica passo**: test round-trip; grep: nessun log che contenga il token
- **Su divergenza**: STOP — scrivi `⚠️ Divergenza Fase 8: <cosa>` in plan.md, non procedere
- **Esito 2026-10-05**: `Security/ITokenProtector.cs`, `TokenProtector.cs` (purpose `dr-mailroom.gmail-refresh-token.v1`), `TokenProtectionExtensions.cs` (`PersistKeysToFileSystem(KeyRingPath)` + `SetApplicationName("dr-mailroom")`); registrazione in `Program.cs`. Test round-trip, key ring diverso → `CryptographicException`, token vuoto → eccezione: 3/3 verdi (totale 13/13). Grep: nessun log con token/secret. Avvio reale crea `data/keys/key-*.xml` (ignorata da git); format pulito dopo rinomina campo `_protector` (IDE1006)

### Fase 9: Client Gmail in sola lettura
- **Stato**: [x]
- **Precondizione**: Fase 8 `[x]`
- **File**: `src/dr-mailroom-core/Gmail/IGmailReader.cs`, `src/dr-mailroom-core/Gmail/GmailReader.cs`
- **Operazione**: CREATE
- **Azione**: wrapper su `Google.Apis.Gmail.v1`, scope unico `gmail.readonly`: `GetProfileHistoryIdAsync`, `ListMessageIdsPageAsync(pageToken)` (rispetta `IncludeSpamTrash`), `ListChangesSinceAsync(historyId)` (solo `messageAdded`; 404 → `HistoryExpiredException`), `GetMetadataAsync` (`format=metadata`: From, Subject, `internalDate`, labelIds, threadId, presenza allegati), `GetAttachmentsAsync`; backoff su 429/5xx; `invalid_grant` → `TokenRevokedException`
- **Tool ammessi**: nessuno
- **Verifica passo**: test con handler HTTP finto: 404 → `HistoryExpiredException`; 429 → ritentativo; `invalid_grant` → `TokenRevokedException`
- **Su divergenza**: STOP — scrivi `⚠️ Divergenza Fase 9: <cosa>` in plan.md, non procedere
- **Esito 2026-10-05**: `Gmail/` con `IGmailReader`/`GmailReader` (apre una `IGmailSession` per casella: credenziale da refresh token, scope unico `gmail.readonly`, backoff della libreria disattivato), `GmailSession` (getProfile, messages.list, history.list solo `messageAdded` con filtro Spam/Cestino e dedup, messages.get metadata/full, attachments.get; retry esponenziale su 429/5xx; 404 history → `HistoryExpiredException`; `invalid_grant` → `TokenRevokedException`), modelli record, `GmailRetrySettings`, `GmailConstants`, `SenderParser`, `GmailReaderExtensions` registrato in `Program.cs`. `HasAttachments` dal `mimeType` `multipart/mixed` del payload (format=metadata non elenca le parti). Test con handler HTTP finto: 13 nuovi, totale 26/26 verdi; grep: nessuna chiamata Gmail di modifica, unico scope `gmail.readonly`; format pulito

### Fase 10: Ingestione idempotente e checkpoint
- **Stato**: [x]
- **Precondizione**: Fasi 7 e 9 `[x]`
- **File**: `src/dr-mailroom-core/Ingestion/MailboxIngestionService.cs`
- **Operazione**: CREATE
- **Azione**: backfill se `BackfillCompleted = false`: al primo avvio salvare l'`historyId` del profilo, poi pagine da `BackfillPageToken` salvato dopo ogni pagina (ripresa); a fine backfill `BackfillCompleted = true`. Altrimenti incrementale da `HistoryId`; 404 → scansione completa. Insert idempotente su `Message`. Restituisce gli id dei messaggi **inseriti** (non duplicati) e se il giro era di backfill (D12: niente filtri). `Run.MessagesRead` aggiornato periodicamente
- **Tool ammessi**: nessuno
- **Verifica passo**: test con client finto: doppia ingestione → nessun duplicato; interruzione a metà backfill → ripresa dalla pagina salvata; errore nell'incrementale → `HistoryId` invariato; secondo giro consecutivo incrementale
- **Su divergenza**: STOP — scrivi `⚠️ Divergenza Fase 10: <cosa>` in plan.md, non procedere
- **Esito 2026-10-05**: `Ingestion/MailboxIngestionService.cs` + `IngestionResult.cs`; anticipati da Fase 14 i provider `MailboxProvider`, `RunProvider`, `MessageProvider` (insert idempotente: pre-check esistenti + fallback riga per riga su 2601/2627) e `Helpers/TextLimits.cs`. Ogni pagina/blocco salva messaggi + checkpoint + `Run.MessagesRead` in un unico `SaveChanges`. DbSet resi `=> Set<T>()` (format li aveva resi nullabili). Test InMemory (7): backfill completo con historyId catturato all'inizio, interruzione → ripresa da `p2`, secondo giro incrementale, doppia ingestione senza duplicati, errore incrementale → `HistoryId` invariato e nessun insert, history scaduta → scansione completa, contatore giro; totale 33/33; nessuna modifica pendente al modello. Limiti noti: (1) mail arrivate durante il backfill e già indicizzate da esso non passano dai filtri (D12); (2) crash tra salvataggio e processi → processi persi per quei messaggi, recuperabili con riapplica (D13)

### Fase 11: Motore dei filtri
- **Stato**: [x]
- **Precondizione**: Fase 6 `[x]`
- **File**: `src/dr-mailroom-core/Filtering/FilterMatcher.cs`
- **Operazione**: CREATE
- **Azione**: funzione pura: messaggio + filtri attivi della casella → processi da applicare (unione) e filtri corrispondenti. Mittente: uguaglianza case-insensitive (D7); oggetto: `StartsWith` / `Contains` / `Regex` (D8), regex con timeout e cache delle istanze compilate
- **Tool ammessi**: nessuno
- **Verifica passo**: test: mittente diverso, oggetto assente, `StartsWith` vero/falso, `Contains` vero/falso, `Regex` vero/falso, regex che va in timeout → nessun match senza eccezione, due filtri con processi sovrapposti → processi deduplicati
- **Su divergenza**: STOP — scrivi `⚠️ Divergenza Fase 11: <cosa>` in plan.md, non procedere
- **Esito 2026-10-05**: `Filtering/FilterMatcher.cs` (funzione pura + cache regex compilate con `IgnoreCase|CultureInvariant` e timeout da opzioni; timeout o pattern non valido → nessun match + warning) e `FilterMatchResult.cs` (filtri ordinati per Id, processi deduplicati, sottocartella allegati = primo filtro con estrazione). Considera solo filtri attivi e della casella (o globali). Test: 13 casi (mittente diverso/case-insensitive, oggetto assente, StartsWith/Contains/Regex vero/falso, timeout regex, regex non valida, processi sovrapposti, filtro disattivato/altra casella); totale 46/46; format pulito

### Fase 12: Processo "estrazione allegati"
- **Stato**: [x]
- **Precondizione**: Fasi 9 e 11 `[x]`
- **File**: `src/dr-mailroom-core/Processes/AttachmentExtractionProcess.cs`, `src/dr-mailroom-core/Processes/SafeFileName.cs`
- **Operazione**: CREATE
- **Azione**: per ogni parte allegato: se esiste già `ExtractedAttachment(MessageId, PartId)` → salta; altrimenti scarica, scrive su file temporaneo, sposta in `RootPath/<AttachmentSubfolder>/yyyyMMdd-HHmmss_<mittente>_<nomefile>` (sanificato, dentro la root, suffisso su collisione), registra `ExtractedAttachment` con SHA-256; allegati oltre `MaxSizeBytes` saltati con motivo; esito in `ProcessExecution`
- **Tool ammessi**: nessuno
- **Verifica passo**: test: `..\..\x.exe` resta dentro la root; riesecuzione → nessun file nuovo; crash simulato dopo la scrittura del primo allegato → la ripresa scrive solo i mancanti
- **Su divergenza**: STOP — scrivi `⚠️ Divergenza Fase 12: <cosa>` in plan.md, non procedere
- **Esito 2026-10-05**: `Processes/IMessageProcess.cs` (Strategy, scelta per tipo anche in riapplica), `ProcessContext.cs`, `SafeFileName.cs` (nome `yyyyMMdd-HHmmss_mittente_nome`, sanificazione, nomi riservati Windows, accorciamento, cartella sempre dentro la root, suffisso `_N`), `AttachmentExtractionProcess.cs` (salta parti già registrate, limite dimensione con motivo, file temporaneo + rename, SHA-256, rimozione file se la registrazione fallisce, esito in `ProcessExecution`; errori → Failed senza eccezione, tranne cancellazione e `TokenRevokedException`). Provider `AttachmentProvider` (stacca l'entità su errore) e `ProcessExecutionProvider` (upsert). Test: 10 nuovi (nome deterministico, traversal nel nome, sottocartella fuori root → Failed senza scritture, riesecuzione senza nuovi file, crash dopo il primo allegato → ripresa solo dei mancanti, oltre limite → Skipped, suffisso su collisione, sanificazione); totale 56/56; format pulito

### Fase 13: Processo "allerta"
- **Stato**: [x]
- **Precondizione**: Fase 11 `[x]`
- **File**: `src/dr-mailroom-core/Processes/AlertProcess.cs`
- **Operazione**: CREATE
- **Azione**: una sola `Alert` per messaggio (D3) con mittente, oggetto, data di ricezione; `AlertFilter` per tutti i filtri corrispondenti (aggiunti anche su riapplica se l'allerta esiste già)
- **Tool ammessi**: nessuno
- **Verifica passo**: test: due filtri con Alert sullo stesso messaggio → 1 Alert, 2 AlertFilter; riesecuzione → nessuna nuova Alert
- **Su divergenza**: STOP — scrivi `⚠️ Divergenza Fase 13: <cosa>` in plan.md, non procedere
- **Esito 2026-10-05**: `Processes/AlertProcess.cs` + `Infrastructure/Mailroom/AlertProvider.cs` (`EnsureAlertAsync`: crea l'allerta se manca, collega i soli filtri nuovi che prevedono l'allerta, un unico SaveChanges). Test: 4 nuovi (due filtri → 1 Alert + 2 AlertFilter con mittente/oggetto/data; riesecuzione → nessuna nuova Alert né ProcessExecution; riapplica con nuovo filtro → solo il collegamento; filtro senza Alert non collegato); totale 60/60; format pulito

### Fase 14: Orchestrazione dei giri, lock e scheduler
- **Stato**: [x]
- **Precondizione**: Fasi 10, 12, 13 `[x]`
- **File**: `src/dr-mailroom-core/Runs/RunOrchestrator.cs`, `src/dr-mailroom-core/Runs/MailboxLock.cs`, `src/dr-mailroom-api/Scheduling/ScheduledRunJob.cs`, `src/dr-mailroom-api/Scheduling/OnDemandRunJob.cs`, `src/dr-mailroom-core/Infrastructure/Mailroom/*Provider.cs`, `src/dr-mailroom-api/Program.cs`
- **Operazione**: CREATE / EDIT
- **Azione**: `MailboxLock` su `sp_getapplock` (T8). Job schedulato (cron, `[DisallowConcurrentExecution]`): per ogni casella attiva, in parallelo fino a `MaxParallelMailboxes`, prova il lock; occupata → `Run` Skipped. Job on-demand (manuale e riapplica) avviato con `TriggerJob` e `RunId` nel JobDataMap. Giro: ingestione → filtri sui messaggi inseriti (non nel backfill, D12) → processi. Riapplica: messaggi già indicizzati che corrispondono al filtro → solo i processi richiesti (D13). Errori di una casella non fermano le altre; `TokenRevokedException` → `Mailbox.Status = TokenRevoked`
- **Tool ammessi**: nessuno
- **Verifica passo**: test: lock occupato → seconda richiesta rifiutata; errore su casella A non blocca B; backfill non crea allerte né file; riapplica con solo `AttachmentExtraction` non crea allerte; riapplica ripetuta non duplica nulla
- **Su divergenza**: STOP — scrivi `⚠️ Divergenza Fase 14: <cosa>` in plan.md, non procedere
- **Esito 2026-10-05**: core `Runs/` con `IMailboxLock` + `MailboxLock` (`sp_getapplock` Exclusive/Session/timeout 0 su connessione dedicata, rilascio esplicito), `RunOrchestrator` (giro normale: ingestione → filtri sui soli messaggi nuovi fuori dal backfill → processi; riapplica: messaggi del mittente, condizione oggetto dal motore, sottoinsieme processi ∩ processi del filtro, vale anche su filtro disattivato; `TokenRevokedException` → `Mailbox.Status = TokenRevoked`; non rilancia salvo cancellazione), `ScheduledRunner` (caselle attive in parallelo, uno scope DI per casella), `IRunDispatcher`; `MailroomCoreExtensions.AddMailroomCore`. Esito finale di giro e casella scritto con contesto dedicato (`IDbContextFactory`) per non salvare modifiche pendenti fallite (es. HistoryId avanzato in memoria). Provider: nuovo `FilterProvider`, estesi `MessageProvider`, `MailboxProvider`, `RunProvider` (`CreateRunAsync`, `HasRunningRunAsync`, `FailInterruptedRunsAsync`, `FinishRunAsync`). API `Scheduling/`: `ScheduledRunJob` (`[DisallowConcurrentExecution]`), `OnDemandRunJob` (durevole, `RunId` nel JobDataMap), `QuartzRunDispatcher` (`TriggerJob`), `SchedulingExtensions` — adeguati a Quartz 4.3 (`ValueTask Execute(ctx, ct)`, `CronScheduleBuilder.Create`); rimossi gli stub `NotImplementedException` generati da `dotnet format`. Test: 8 orchestrazione con composizione DI reale (lock occupato → Skipped senza letture; backfill senza allerte/file/esiti; incrementale lavora solo i nuovi; riapplica solo allegati → nessuna allerta, ripetuta senza duplicati; riapplica senza lista → tutti i processi; errore casella A non blocca B; token revocato → TokenRevoked; incrementale fallito non persiste HistoryId) + 1 integrazione `sp_getapplock` su SQL reale (attiva con env `MAILROOM_TEST_SQL`, altrimenti skipped): 69/69 con SQL, 68 + 1 skipped senza. Avvio host con Quartz: ok

### Fase 15: Endpoint API
- **Stato**: [x]
- **Precondizione**: Fase 14 `[x]`
- **File**: `src/dr-mailroom-api/Endpoints/*Mapping.cs`, `src/dr-mailroom-api/Validators/*.cs`, `src/dr-mailroom-api/Services/*Service.cs`, `src/dr-mailroom-api/Dto/*.cs`, `src/dr-mailroom-core/Infrastructure/Mailroom/Filters/*.cs`, `src/dr-mailroom-api/Transformers/AddDocumentInformations.cs`, `src/dr-mailroom-api/Properties/ApiVersionFactory.cs`, `src/dr-mailroom-api/Infrastructure/Database/*.cs`, `src/dr-mailroom-api/dr-mailroom-api.http`, `src/dr-mailroom-api/Properties/launchSettings.json`, `.vscode/launch.json`, `.vscode/tasks.json`
- **Operazione**: CREATE
- **Azione**: endpoint con validatore esplicito per ogni body e filtro, tutti sotto `/api/v1/` (C1, C5–C8):
  - Caselle: lista, stato (ultimo giro, avanzamento backfill, errore, token valido), `GET /mailboxes/connect` e callback OAuth (`access_type=offline`, `prompt=consent`; redirect a `Frontend.BaseUrl` con esito in query string), rimozione
  - Filtri: CRUD; `POST /filters/preview` (dry-run sulle mail già indicizzate); `POST /filters/{id}/reapply` con body `{ processes?: [...] }` (sottoinsieme dei processi del filtro, default tutti) → 202 + `RunId`, 409 se casella occupata
  - Giri: `POST /runs` (casella o tutte) → 202 + `RunId`, 409 se occupata; `GET /runs`, `GET /runs/{id}`
  - Statistiche: `GET /stats/senders`, `GET /stats/senders/monthly`
  - Allerte: `GET /alerts?consumed=false`, `POST /alerts/{id}/consume`
  - Allegati: `GET /messages/{id}/attachments`
  - Salute: `/health`, `GET /status`, `POST /status/retry-database` (C7)
  - Autenticazione: chiave API con `SimpleAuthenticationTools` (C8); CORS limitato a `Frontend.BaseUrl`; OpenAPI per il client Vue; solo REST JSON (D10)
- **Tool ammessi**: nessuno
- **Verifica passo**: test di integrazione: senza chiave API → 401; mittente mancante → 400; `SubjectMatchType` senza `SubjectValue` o viceversa → 400; tipo non tra `StartsWith`/`Contains`/`Regex` → 400; regex non compilabile → 400; nessun processo → 400; reapply con processo non presente nel filtro → 400; doppio avvio → 409
- **Su divergenza**: STOP — scrivi `⚠️ Divergenza Fase 15: <cosa>` in plan.md, non procedere
- **Esito 2026-10-05**: regole dei body approvate dall'utente (D15) e decisioni D16–D18 registrate. Core: `MailboxStatus.Deleted` (stringa, nessuna migration), filtri di query in `Infrastructure/Mailroom/Filters/` (`FilterListFilter`/`AlertListFilter` per evitare collisioni con le entità), query `Get<Entity>Async<TResult>(filter, selector)` sui provider, `StatsProvider`, `GmailAuthorizer` (offline + consent + state), `OAuthStateProtector` (state firmato a 10 minuti), guardia orchestratore su casella `Deleted`. API: 19 DTO con `Projection` (FilterDto/AlertDto proiettano una sotto-lista tradotta da EF), validatori `IValidator<T>` per 4 body e 6 filtri (`ValidationRules` condivise), 6 servizi + `ServiceResult`, 7 `*Mapping.cs` (21 path sotto `/api/v1`, metadata OpenAPI completi, handler `*Async`), `DatabaseStartupService` (chiude i giri rimasti Running all'avvio) + `DatabaseExceptionHandler`, `AddDocumentInformations`, `ApiVersionFactory`, `Program.cs` (TinyHelpers ProblemDetails, SimpleAuthentication API key `x-api-key` obbligatoria all'avvio, CORS su `Frontend.BaseUrl`, enum JSON testuali, Scalar); `appsettings.local.json` caricato solo in Development, chiave API casuale generata lì; `.http`, profilo IIS Express, `.vscode/launch.json` e `tasks.json`. Deroghe: viste statistiche con un solo DTO; avvisi AV0029/AV0030 soppressi nel csproj (pattern dell'istruzione, OpenAPI verificato completo). Callback OAuth anonimo con state firmato; redirect al frontend con `oauth=ok|error`. Test d'integrazione (WebApplicationFactory, DB InMemory): 401 senza chiave, 400 su mittente mancante/non valido, tipo senza valore e viceversa, tipo non ammesso, regex non compilabile, nessun processo, sottocartella con `..`, riapplica con processo non del filtro; 202 + 409 sul doppio avvio e su lock occupato; eliminazione logica + 409 sui filtri della casella eliminata; 404 su allerte vuote; callback con state falsificato → errore; flusso connect completo → casella registrata con token cifrato. Totale 87 verdi + 1 skipped (SQL). Avvio reale in Development: status 200, auth ok, URL di consenso con `gmail.readonly`, `offline`, `consent` e redirect C2

### Fase 16: Container
- **Stato**: [x]
- **Precondizione**: Fase 15 `[x]`
- **File**: `Dockerfile`, `docker-compose.yml`, `.dockerignore`, `.env.example`, `.gitignore`
- **Operazione**: CREATE / EDIT
- **Azione**: immagine multi-stage .NET 10; volumi per `Attachments.RootPath` e `DataProtection.KeyRingPath`; segreti via `.env` non committato; porta API raggiungibile da `localhost` per il redirect OAuth; stesso binario eseguibile anche in locale/servizio
- **Tool ammessi**: skill `dr-tech` (consultiva), docker
- **Verifica passo**: `docker build` exit 0; container risponde a `/health`
- **Su divergenza**: STOP — scrivi `⚠️ Divergenza Fase 16: <cosa>` in plan.md, non procedere
- **Avanzamento 2026-10-05**: creati `Dockerfile` (multi-stage sdk/aspnet 10, utente `app`, volumi `/data/attachments`, `/data/keys`, `/app/logs`, porta 8080), `docker-compose.yml` (porta 5180:8080 per il redirect C2, bind mount su `./data/*` sovrascrivibili, `env_file: .env`), `.dockerignore`, `.env.example` (placeholder), `.env` locale con valori reali (ignorato; SQL via `host.docker.internal`, TCP 1433 verificato); `.gitignore` con `.env` e `/data/`. Verifica 2026-10-05 (Docker 29.7.2): `docker build` exit 0; `docker compose up` → `/health` 200, `/api/v1/status` `databaseReady: true` (SQL via `host.docker.internal`), 401 senza chiave; key ring, log e allegati su `./data/*` dell'host; key ring invariato dopo `--force-recreate`. `ASPNETCORE_URLS` sostituito da `ASPNETCORE_HTTP_PORTS` per eliminare l'avviso di override; unico WRN residuo: sensitive data logging (C4, scelta utente)

### Fase 17: Specifica del frontend
- **Stato**: [x]
- **Precondizione**: Fase 15 `[x]`
- **File**: `docs/frontend-specifiche.md`
- **Operazione**: CREATE
- **Azione**: SPA `dr-mailroom-fe` (D10) con 4 viste: Caselle (OAuth, stati, avanzamento backfill, token revocato), Filtri (mittente, oggetto con tipo `StartsWith`/`Contains`/`Regex`, processi multipli, sottocartella; anteprima dry-run; "Riapplica" con scelta dei processi), Esecuzioni ("Esegui ora" con `aria-busy`, gestione 409, polling 3–5 s, cronologia), Statistiche (tabelle ordinabili); avviso che il primo giro è lungo e non applica i filtri; accessibilità (label esplicite, `aria-live`); mapping di ogni endpoint
- **Tool ammessi**: skill `dr-professor`
- **Verifica passo**: ogni endpoint della Fase 15 è referenziato da almeno una vista (tranne quelli per il software esterno, dichiarati)
- **Su divergenza**: STOP — scrivi `⚠️ Divergenza Fase 17: <cosa>` in plan.md, non procedere
- **Esito 2026-10-05**: `docs/frontend-specifiche.md` v1.0: stack e confini (D10), convenzioni API (x-api-key con rischio SPA dichiarato, 404 su lista = nessun dato, 400 per campo, enum stringa, CORS, OpenAPI), banner di stato DB, 4 viste (Caselle con flusso OAuth e codici `reason`, ricollegamento, eliminazione logica D18; Filtri con form D15, anteprima, allegati salvati, riapplica D13/D16; Esecuzioni con `aria-busy`, 409, polling 3–5 s, avviso D12; Statistiche ordinabili con `aria-sort`), requisiti di accessibilità, mappa endpoint: 22 endpoint usati da almeno una vista, 5 dichiarati come non usati dal frontend (alerts ×3 per il software esterno, callback per il browser, /health per l'infrastruttura). Verifica: grep di tutti i 27 endpoint nel documento

### Fase 18: README e guida Google Cloud
- **Stato**: [x]
- **Precondizione**: Fase 16 `[x]`
- **File**: `README.md`, `docs/setup-google-oauth.md`
- **Operazione**: CREATE / EDIT
- **Azione**: guida Google Cloud (progetto, abilitazione Gmail API, schermata di consenso, **pubblicazione "In production"** con spiegazione dei 7 giorni in "Testing", client OAuth, redirect URI); README con configurazione segreti, migration, avvio locale e container, collegamento caselle
- **Tool ammessi**: skill `dr-professor`
- **Verifica passo**: struttura conforme a `readme-structure.instructions.md`; nessun segreto nei file
- **Su divergenza**: STOP — scrivi `⚠️ Divergenza Fase 18: <cosa>` in plan.md, non procedere
- ⚠️ Divergenza Fase 18: `readme-structure.instructions.md` descrive il README del repo `dr-guidelines` (pacchetti, skill, MCP) e non è applicabile a dr-mailroom; `readme-generator.prompt.md` impone titolo + panoramica + elenco `docs/` con "nessuna sezione extra", in conflitto con l'Azione (segreti, migration, avvio, collegamento nel README). `docs/setup-google-oauth.md` v1.0 creato (nessun conflitto, nessun segreto). **Risolta 2026-10-06** (scelta utente): README snello conforme a `readme-generator.prompt.md`; guida operativa in nuovo `docs/avvio-e-configurazione.md` (aggiunto allo Scope); `readme-structure.instructions.md` non applicato e segnalato su dr-guidelines via `/dr-file-feedback`: [davraf-amuro/dr-guidelines#9](https://github.com/davraf-amuro/dr-guidelines/issues/9)
- **Esito 2026-10-06**: `README.md` (titolo, panoramica, tabella dei 4 documenti di `docs/`, footer) conforme a `readme-generator.prompt.md`; `docs/avvio-e-configurazione.md` v1.0 (tabella configurazione con sensibilità, segreti locali/container, migration, avvio locale e container con volumi e avviso key ring, collegamento caselle, test con `MAILROOM_TEST_SQL`, gate di push); `docs/setup-google-oauth.md` v1.0 (Branding senza logo, scope, pubblicazione In produzione con spiegazione dei 7 giorni, client web + redirect URI, custodia credenziali, primo collegamento, tabella guasti); `docs/architettura.md` → v1.1 con D15–D18. Verifica: ogni `.md` di `docs/` elencato nel README; grep: nessun segreto nei documenti

### Fase 19: Verifica end-to-end
- **Stato**: [ ]
- **Precondizione**: Fasi 1–18 `[x]`; progetto Google Cloud pronto
- **File**: nessuno
- **Operazione**: verifica
- **Azione**: collegamento reale della casella; backfill; controllo stato letto/non letto su un campione; statistiche confrontate con Gmail su un mittente campione; filtro di test su mail nuova → allerta e allegato; riapplica con solo allegati su una mail storica di un fornitore
- **Tool ammessi**: `dr-verify-plan`
- **Verifica passo**: tutti i criteri finali soddisfatti
- **Su divergenza**: STOP — scrivi `⚠️ Divergenza Fase 19: <cosa>` in plan.md, non procedere
- **Esito parziale (2026-10-08, servizio spento su richiesta dell'utente)**:
  - Statici: build 0 avvisi, 109 test superati (1 skip SQL), `dotnet format` exit 0 su 3 progetti; unico scope `gmail.readonly`, nessuna chiamata Gmail di modifica; 4 UNIQUE + `CK_Filter_Subject` nella migration; nessun segreto tracciato (`.env` e `appsettings.local.json` ignorati, `appsettings.json` con valori vuoti); nessun log con interpolazione né token
  - DB (sola lettura): viste interrogabili; 4.500 mail indicizzate (30/06–08/10/2026), 0 allerte, 0 allegati, 0 `ProcessExecution` → backfill senza processi
  - Gmail (connettore, sola lettura): campione di 5 mail `UNREAD` nel DB → ancora `UNREAD` in Gmail; `noreply@subito.it` ottobre 15 = 15, settembre 54 = 54
  - Container: volumi `data/logs` e `data/keys` persistenti; backfill interrotto (giri 4, 6, 7) e ripreso dalla pagina salvata ("ripresa backfill dalla pagina salvata")
  - Anomalia non bloccante: mail `19f426780b8543a1` con `ReceivedAtUtc` 1970-01-01, perché Gmail restituisce `internalDate = 0` (header `Date` malformato, anno 7252). Non è un difetto del servizio
- **Da completare (richiede il servizio acceso)**: casella 1 in pausa (`Disabled`), backfill fermo a 4.500 mail. Servono: `resume` → fine backfill → secondo giro incrementale; filtro di test (anche `Regex`) su una mail nuova → allerta una sola volta + allegato; riapplica solo `AttachmentExtraction` due volte; giro manuale con doppio avvio → 409; `UPDATE Mailbox SET HistoryId = 1` (autorizzato) → scansione completa senza duplicati; `/dr-verify-plan`

## Criteri di verifica finale
- [x] `dotnet build dr-mailroom.slnx` e `dotnet test` exit 0
- [x] `dotnet format <progetto>.csproj --verify-no-changes` exit 0 su ogni progetto
- [x] Scope OAuth unico `gmail.readonly` nel codice; nessuna chiamata Gmail di modifica
- [x] Indici UNIQUE `(MailboxId, GmailMessageId)`, `Alert.MessageId`, `(MessageId, ProcessType)`, `(MessageId, PartId)` e CHECK su `Filter` presenti nella migration
- [x] Viste `vw_SenderStats` e `vw_SenderMonthlyStats` presenti e interrogabili
- [x] Nessun segreto in file tracciati (`git grep` su secret, connection string, client secret, chiave API); token mai loggati
- [x] Logging strutturato con placeholder, nessuna interpolazione nei log
- [x] Stato letto/non letto di un campione di mail non lette invariato dopo il giro
- [ ] Backfill interrotto e ripreso dalla pagina salvata; secondo giro consecutivo incrementale
- [ ] Backfill senza allerte né allegati; statistiche totale e mensile corrette su un mittente campione
- [ ] Filtro di test (anche `Regex`): allerta creata una sola volta, allegato salvato con nome deterministico e collegato alla mail
- [ ] Riapplica con solo `AttachmentExtraction`: scarica gli allegati dello storico, nessuna allerta, nessun duplicato alla seconda esecuzione
- [ ] Giro manuale: parte subito, `RunId` interrogabile, doppio avvio → 409
- [ ] `HistoryId` scaduto gestito con scansione completa senza duplicati
- [x] `docs/architettura.md`, `docs/frontend-specifiche.md`, `docs/setup-google-oauth.md`, `README.md` presenti
- [x] `docker compose up` funzionante con volumi persistenti
- [ ] Verifica a freddo con `/dr-verify-plan`

## Revisioni
### R1 — 2026-10-02: fusione con il piano MVP
- Match oggetto: aggiunto `Regex` (richiesta utente)
- Recuperati dal piano MVP: ripresa del backfill con `pageToken`, `ConsumedAtUtc` sulle allerte, idempotenza allegati (`PartId` UNIQUE, SHA-256, nome deterministico), nota Google "In production", fase prerequisiti, checkpoint convenzioni, verifica E2E e criteri funzionali, `ThreadId`/`SenderName`/`HasAttachments`, endpoint allegati
- Nuove decisioni: D11 (giro manuale immediato + 409, `sp_getapplock`, niente coda nel DB), D12 (backfill solo indicizzazione), D13 (riapplica per filtro con sottoinsieme di processi), D14 (naming kebab-case `dr-mailroom-*`)
- Fasi 1 e 2 del v2 unite: `/dr-scaffold-solution` installa già i pacchetti
