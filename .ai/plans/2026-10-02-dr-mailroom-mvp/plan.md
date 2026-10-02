# Piano: dr-mailroom MVP — lettore Gmail con filtri, processi e statistiche
Brief: [20261001-01.md](../../../briefs/20261001-01.md)
Data: 2026-10-02
Stato: SUPERATO — sostituito da [2026-10-02-mailroom-gmail-reader](../2026-10-02-mailroom-gmail-reader/plan.md) (revisione R1)

## Obiettivo
Servizio C# autonomo e schedulato che indicizza in sola lettura una casella Gmail personale, calcola le statistiche per mittente, applica i filtri utente (estrazione allegati, allerta), con API REST e frontend Vue, distribuito con Docker sulla LAN.

## Decisioni (warroom 2026-10-01 + risposte utente)

| Tema | Decisione |
|------|-----------|
| Account | Gmail personale (`d.raffagli@gmail.com`). Modello multi-casella per **un solo utente**; per un altro utente si crea un'istanza separata di servizio e DB |
| Accesso | Gmail API REST con scope `gmail.readonly`. Lo stato letto/non letto non può cambiare per costruzione. Niente IMAP |
| Autenticazione | OAuth2 con consenso da browser su `http://localhost`. App Google Cloud in stato **"In production"** (non verificata, uso personale): in stato "Testing" il refresh token scade dopo 7 giorni |
| Checkpoint | Primo giro: sincronizzazione completa dello **storico**, ripresa dal `pageToken` salvato. Giri successivi: incrementali con `history.list` da `LastHistoryId`. Con HTTP 404 (history scaduta) → sincronizzazione completa, i duplicati sono assorbiti dal vincolo UNIQUE |
| Perimetro | Statistiche su **tutte** le mail; processi solo sulle mail che corrispondono ai filtri. Spam e Cestino **esclusi**, includibili da impostazione |
| Filtro | Mittente obbligatorio + **una sola** condizione opzionale sull'oggetto (`StartsWith` / `Contains` / `Regex`, scelta dall'utente) + uno o più processi. Più casi = più filtri. Se più filtri corrispondono alla stessa mail, si applica l'unione dei processi, ciascuno una sola volta |
| Applicazione filtri | Solo sulle mail nuove. Il comando **"riapplica"** lavora lo storico già indicizzato |
| Allegati | Un'unica cartella fissa (configurabile, volume Docker). Nome deterministico `yyyyMMdd-HHmmss_<mittente>_<nomefile>`, con suffisso numerico se esiste già. Collegamento mail↔file nel DB |
| Allerta | Riga in tabella `Alert` (mittente, oggetto, data ricezione). La legge il software esterno (fuori scope) dal DB o da `GET /api/alerts` |
| Database | SQL Server esistente, EF Core con migrazioni. Vincoli UNIQUE per l'idempotenza, viste per le statistiche, colonna calcolata `YearMonth`. Nessun'altra tecnologia di persistenza |
| Hosting | **Un solo host** ASP.NET Core: Minimal API + scheduler Quartz.NET nello stesso processo. Un'istanza per utente, quindi niente coda nel DB (`usp_ClaimWork` scartata); `sp_getapplock` + `[DisallowConcurrentExecution]` impediscono giri sovrapposti (schedulato, manuale, riapplica) |
| Parallelismo | `Parallel.ForEachAsync` con `MaxDegreeOfParallelism` configurabile per il recupero dei metadati e la lavorazione; retry con Polly e backoff sui 429/5xx |
| Cron | Espressione cron in configurazione (appsettings / variabile d'ambiente) |
| Frontend | Vue 3 + Vite + TypeScript, servito da nginx; proxy `/api` verso l'host .NET |
| Sicurezza | Refresh token cifrato con ASP.NET Data Protection, chiavi su volume dedicato. Client ID/secret Google solo in user-secrets / `.env` non committato. API protetta da una API key (header) configurata da ambiente |

## Scope

### File da creare (percorsi indicativi — confermati in Fase 2 secondo le istruzioni dei pacchetti installati)
- [ ] `dr-mailroom.slnx` — solution
- [ ] `src/dr-mailroom.core/` — dominio, DbContext EF, client Gmail, sync, matcher dei filtri, processori
- [ ] `src/dr-mailroom.api/` — Minimal API, endpoint OAuth, hosting Quartz, composition root
- [ ] `test/dr-mailroom.tests/` — test xUnit
- [ ] `dr-mailroom-fe/` — frontend Vue
- [ ] `docker/` o radice: `Dockerfile` (api, fe), `docker-compose.yml`, `.env.example`
- [ ] `docs/setup-google-oauth.md` — procedura Google Cloud passo passo
- [ ] `README.md`
- [ ] `.github/instructions/*`, `.ai/dr-guidelines-packages.json` — aggiornati dagli installer dei pacchetti

### Perimetro negativo
- Non toccherò: `briefs/` (file dell'utente), `CLAUDE.md`, `.github/copilot-instructions.md` (a parte quanto scrivono gli installer dei pacchetti), `.mcp.json`, `.claude/`
- Niente notifiche all'utente (software esterno), niente scrittura su Gmail (label, stato, invio)
- Nessun `git push`: commit locali solo su richiesta
- Nessuna creazione di DB o login sull'istanza SQL Server senza conferma esplicita della connection string

## Modello dati (SQL Server)

| Tabella | Colonne chiave | Vincoli e indici |
|---------|----------------|------------------|
| `Mailbox` | EmailAddress, EncryptedRefreshToken, LastHistoryId, FullSyncPageToken, FullSyncCompletedAtUtc, IsEnabled, LastError | UNIQUE(EmailAddress) |
| `MailMessage` | MailboxId, GmailMessageId, ThreadId, SenderAddress, SenderName, Subject, ReceivedAtUtc, `YearMonth` (calcolata persistita), HasAttachments | UNIQUE(MailboxId, GmailMessageId); IX(MailboxId, SenderAddress, YearMonth) |
| `Filter` | Name, SenderAddress, SubjectMatchType (null / StartsWith / Contains / Regex), SubjectPattern, IsEnabled | IX(SenderAddress) |
| `FilterProcess` | FilterId, ProcessType (AttachmentExtraction / Alert) | PK(FilterId, ProcessType) |
| `MessageProcessing` | MailMessageId, ProcessType, FilterId, Status, Error, ProcessedAtUtc | UNIQUE(MailMessageId, ProcessType) |
| `Alert` | MailMessageId, SenderAddress, Subject, ReceivedAtUtc, CreatedAtUtc, ConsumedAtUtc (per il software esterno) | UNIQUE(MailMessageId) |
| `ExtractedAttachment` | MailMessageId, PartId, OriginalFileName, StoredFileName, SizeBytes, Sha256 | UNIQUE(MailMessageId, PartId) |
| `SyncRun` | Trigger (Scheduled / Manual / Reapply), StartedAtUtc, EndedAtUtc, Status, MessagesIndexed, MessagesProcessed, ErrorCount, ErrorSummary | IX(StartedAtUtc) |

Viste: `vw_SenderTotals(MailboxId, SenderAddress, MailCount)`, `vw_SenderMonthly(MailboxId, SenderAddress, YearMonth, MailCount)`, mappate come keyless entity.

## API REST (bozza)

| Metodo | Percorso | Scopo |
|--------|----------|-------|
| GET | `/api/oauth/start` · `/api/oauth/callback` | Collegamento della casella |
| GET / DELETE | `/api/mailboxes` · `/api/mailboxes/{id}` | Stato della casella (ultimo sync, errore, token valido), scollegamento |
| GET / POST / PUT / DELETE | `/api/filters` | CRUD dei filtri, con validatore esplicito (regex compilata con timeout) |
| GET | `/api/filters/preview` | Anteprima delle mail già indicizzate che corrispondono al filtro (solo DB) |
| POST | `/api/filters/{id}/reapply` | Riapplica il filtro allo storico → 202 + runId |
| POST / GET | `/api/runs` · `/api/runs/{id}` | Giro manuale → 202 + runId; cronologia ed esito |
| GET | `/api/stats/senders` · `/api/stats/senders/monthly` | Le due statistiche |
| GET | `/api/alerts` | Allerte per il software esterno |
| GET | `/api/messages/{id}/attachments` | File estratti di una mail |

## Fasi

### Fase 0: Prerequisiti
- **Stato**: [ ]
- **Precondizione**: nessuna
- **File**: nessuno
- **Operazione**: verifica
- **Azione**: verificare SDK .NET 10, node/npm, Docker, PowerShell 7 e raggiungibilità dell'istanza SQL Server (connection string fornita dall'utente, mai scritta in file committati). L'utente crea il progetto Google Cloud seguendo la procedura che fornirò (può farlo in parallelo, serve dalla Fase 4)
- **Tool ammessi**: Bash/PowerShell in sola lettura
- **Verifica passo**: tutti i comandi `--version` rispondono; connessione SQL riuscita
- **Su divergenza**: STOP — `⚠️ Divergenza Fase 0`

### Fase 1: Scaffolding solution e pacchetti dr-*
- **Stato**: [ ]
- **Precondizione**: Fase 0 completata
- **File**: solution, progetti `src/`, `test/`, `dr-mailroom-fe/`, istruzioni dei pacchetti
- **Operazione**: CREATE
- **Azione**: `/dr-scaffold-solution` con `dr-mailroom.core` (classlib), `dr-mailroom.api` (minimal-api), `dr-mailroom.tests` (xunit), `dr-mailroom-fe` (vue-fe); pacchetti `dr-dotnet-backend`, `dr-minimalapi`, `dr-efdb`, `dr-fe`, `dr-devops`. Il frontend Vue va creato prima dell'install del core (nota del catalogo)
- **Tool ammessi**: skill dr-scaffold-solution
- **Verifica passo**: `dotnet build` ok; `.ai/dr-guidelines-packages.json` elenca i 5 pacchetti
- **Su divergenza**: STOP

### Fase 2: Checkpoint convenzioni
- **Stato**: [ ]
- **Precondizione**: Fase 1 completata
- **File**: questo `plan.md`
- **Operazione**: EDIT
- **Azione**: leggere le istruzioni dei pacchetti installati e sostituire i percorsi indicativi dello Scope con quelli esatti (struttura Endpoints/, convenzioni EF, struttura FE). Mostrare la differenza all'utente
- **Tool ammessi**: nessuno
- **Verifica passo**: utente approva lo Scope aggiornato
- **Su divergenza**: STOP

### Fase 3: Modello dati e migrazione
- **Stato**: [ ]
- **Precondizione**: Fase 2 approvata
- **File**: entità, `DbContext`, configurazioni EF, migrazione iniziale (tabelle, UNIQUE, indici, colonna calcolata, viste)
- **Operazione**: CREATE
- **Azione**: implementare il modello dati della sezione omonima
- **Tool ammessi**: dotnet ef
- **Verifica passo**: `dotnet ef migrations script` genera lo script con tutti i vincoli; applicazione su DB di sviluppo confermata dall'utente
- **Su divergenza**: STOP

### Fase 4: OAuth e archivio dei token
- **Stato**: [ ]
- **Precondizione**: Fase 3; progetto Google Cloud pronto
- **File**: servizio token (Data Protection), endpoint `oauth/start` e `oauth/callback`, opzioni `GoogleOAuthOptions`
- **Operazione**: CREATE
- **Azione**: flusso di consenso con `access_type=offline`, `prompt=consent`, scope `gmail.readonly`; refresh token cifrato su `Mailbox`; errore `invalid_grant` → `Mailbox.LastError` visibile
- **Tool ammessi**: nessuno
- **Verifica passo**: collegamento reale della casella riuscito; token nel DB non leggibile in chiaro
- **Su divergenza**: STOP

### Fase 5: Sincronizzazione Gmail (indicizzazione)
- **Stato**: [ ]
- **Precondizione**: Fase 4
- **File**: astrazione `IGmailClient` + implementazione, `MailboxSyncService`
- **Operazione**: CREATE
- **Azione**: sincronizzazione completa paginata con `pageToken` persistito per la ripresa; metadati (`From`, `Subject`, `internalDate`, label, allegati sì/no) con `format=metadata` in parallelo limitato; sincronizzazione incrementale con `history.list`; fallback su 404; esclusione Spam/Cestino secondo impostazione; insert idempotente (errori 2601/2627 = già presente)
- **Tool ammessi**: nessuno
- **Verifica passo**: test con client Gmail finto (completa, ripresa, incrementale, 404); a due giri consecutivi il secondo non indicizza nulla di nuovo
- **Su divergenza**: STOP

### Fase 6: Filtri e processi
- **Stato**: [ ]
- **Precondizione**: Fase 5
- **File**: `FilterMatcher`, `AlertProcessor`, `AttachmentExtractionProcessor`, `FileNameSanitizer`
- **Operazione**: CREATE
- **Azione**: match del mittente case-insensitive esatto + condizione sull'oggetto (regex con timeout); unione dei processi; `MessageProcessing` per l'idempotenza; allegati scaricati con `format=full` + `attachments.get`, nome sanificato (niente path traversal), limite di dimensione configurabile, SHA-256, suffisso sulle collisioni
- **Tool ammessi**: nessuno
- **Verifica passo**: test unitari su matcher (3 tipi, regex non valida o lenta), sanitizer, rilavorazione idempotente
- **Su divergenza**: STOP

### Fase 7: Scheduler, giri ed esiti
- **Stato**: [ ]
- **Precondizione**: Fase 6
- **File**: job Quartz, `RunCoordinator` (`sp_getapplock`), opzioni `SchedulerOptions`
- **Operazione**: CREATE
- **Azione**: job cron con `[DisallowConcurrentExecution]`; giro manuale e riapplica passano dallo stesso coordinatore; `SyncRun` registra trigger, conteggi ed errori; parallelismo per casella e per mail configurabile; Polly sui 429/5xx
- **Tool ammessi**: nessuno
- **Verifica passo**: giro manuale avviato durante un giro schedulato → rifiutato con 409 o accodato (comportamento documentato)
- **Su divergenza**: STOP

### Fase 8: API REST
- **Stato**: [ ]
- **Precondizione**: Fase 7
- **File**: endpoint della sezione "API REST", validatori, middleware API key
- **Operazione**: CREATE
- **Azione**: implementare gli endpoint con un validatore esplicito per ogni body e OpenAPI
- **Tool ammessi**: nessuno
- **Verifica passo**: test di integrazione sugli endpoint principali; richiesta senza API key → 401
- **Su divergenza**: STOP

### Fase 9: Frontend Vue
- **Stato**: [ ]
- **Precondizione**: Fase 8
- **File**: `dr-mailroom-fe/src/**`
- **Operazione**: CREATE
- **Azione**: pagine Account (collega, stato, errore token), Filtri (CRUD, anteprima, riapplica), Esecuzioni (cronologia, "Esegui ora" con stato `aria-live` e pulsante disabilitato durante il giro), Statistiche (tabelle ordinabili: totali, mensili per mittente)
- **Tool ammessi**: nessuno
- **Verifica passo**: `npm run build` ok; verifica di lint secondo `dr-fe` pulita
- **Su divergenza**: STOP

### Fase 10: Docker
- **Stato**: [ ]
- **Precondizione**: Fase 9
- **File**: `Dockerfile` api e fe, `docker-compose.yml`, `.env.example`, `.gitignore` (`.env`)
- **Operazione**: CREATE
- **Azione**: compose con api + fe (nginx con proxy `/api`), volumi `attachments` e `dpkeys`, SQL Server esterno via variabile d'ambiente, porta api su localhost per il redirect OAuth
- **Tool ammessi**: docker
- **Verifica passo**: `docker compose up` → FE raggiungibile, consenso OAuth completato dal container
- **Su divergenza**: STOP

### Fase 11: Documentazione
- **Stato**: [ ]
- **Precondizione**: Fase 10
- **File**: `README.md`, `docs/setup-google-oauth.md`
- **Operazione**: CREATE
- **Azione**: README secondo `readme-structure`; guida Google Cloud (progetto, abilitazione Gmail API, schermata di consenso, pubblicazione "In production", client OAuth, redirect URI)
- **Tool ammessi**: skill dr-professor
- **Verifica passo**: rilettura; nessun segreto nei file
- **Su divergenza**: STOP

### Fase 12: Verifica end-to-end
- **Stato**: [ ]
- **Precondizione**: Fasi 0–11
- **File**: nessuno
- **Operazione**: verifica
- **Azione**: giro reale sulla casella; controllo che le mail non lette restino non lette; statistiche coerenti con Gmail su un mittente campione; un filtro di test produce allerta e allegato
- **Tool ammessi**: dr-verify-plan
- **Verifica passo**: tutti i criteri finali soddisfatti
- **Su divergenza**: STOP

## Criteri di verifica finale
- [ ] `dotnet build` e `dotnet test` verdi; `dotnet format --verify-no-changes` pulito su ogni `.csproj`
- [ ] Lint FE pulito, `npm run build` ok
- [ ] Nessuna credenziale in file tracciati (`git grep` su secret, connection string, client secret)
- [ ] Lo stato letto/non letto di un campione di mail non lette è invariato dopo il giro
- [ ] Il secondo giro consecutivo è incrementale (nessuna reindicizzazione completa)
- [ ] Statistica totale e mensile corrette su un mittente campione
- [ ] Filtro di test: allerta creata una sola volta; allegato salvato con nome deterministico e collegato alla mail
- [ ] Riapplica: lavora lo storico senza duplicare allerte o file
- [ ] Giro manuale visibile in cronologia con esito
- [ ] `docker compose up` funzionante con volumi persistenti
- [ ] Verifica indipendente `dr-verify-plan` superata
