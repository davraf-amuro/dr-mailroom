# Progetto

dr-mailroom — repository `E:\Davide\Progetti\dr-mailroom`, branch `tentativo-1` (main: `main`).

---

# Obiettivo

Servizio .NET 10 che legge una o più caselle Gmail in sola lettura e:
- salva le statistiche per mittente;
- applica filtri (mittente + al più una condizione sull'oggetto);
- esegue i processi sulle mail filtrate (estrazione allegati, allerta);
- gira da solo con un cron Quartz e on-demand via API REST;
- per il frontend esiste solo la specifica.

Piano di riferimento: `.ai/plans/2026-10-02-mailroom-gmail-reader/plan.md` (`Stato: IN CORSO`). Brief: `briefs/20261001-01.md`.

---

# Stato corrente

- Fasi 1–18 del piano marcate `[x]`. Fase 19 (verifica end-to-end) `[ ]`.
- Codice completo in `src/dr-mailroom-api`, `src/dr-mailroom-core` e `test/dr-mailroom-tests`.
- Migration `InitialCreate` applicata al DB di sviluppo `dr-mailroom` su SQL Server `spectre` (macchina locale).
- Immagine Docker `dr-mailroom-api:local` costruita; container `dr-mailroom-api` in esecuzione, porta host 5180.
- Nessuna casella Gmail ancora collegata; nessun test con Gmail reale.
- Nessun commit dalla `45be932`: tutto il lavoro è nella working tree, non committato.

---

# Componenti completati

- Configurazione tipizzata `Mailroom:*` con validazione all'avvio (`src/dr-mailroom-core/Configuration/`).
- Modello EF Core: 9 tabelle, 2 viste statistiche, vincoli UNIQUE e CHECK, migration (`src/dr-mailroom-core/Infrastructure/Mailroom/`).
- Cifratura dei refresh token con Data Protection e key ring su file (`src/dr-mailroom-core/Security/`).
- Client Gmail in sola lettura: retry su 429/5xx, `HistoryExpiredException`, `TokenRevokedException` (`src/dr-mailroom-core/Gmail/`).
- Flusso OAuth di collegamento con state firmato (`GmailAuthorizer`, `OAuthStateProtector`).
- Ingestione: backfill riprendibile e incrementale da `historyId`, insert idempotente (`src/dr-mailroom-core/Ingestion/`).
- Motore dei filtri `StartsWith`/`Contains`/`Regex` con timeout (`src/dr-mailroom-core/Filtering/`).
- Processi `AttachmentExtractionProcess` e `AlertProcess` (`src/dr-mailroom-core/Processes/`).
- Orchestrazione dei giri, lock `sp_getapplock`, giro schedulato parallelo (`src/dr-mailroom-core/Runs/`).
- Scheduler Quartz 4.3: job cron e job on-demand (`src/dr-mailroom-api/Scheduling/`).
- API REST: 21 path sotto `/api/v1` più `/health`, chiave API, OpenAPI e Scalar (`src/dr-mailroom-api/Endpoints/`).
- Container: `Dockerfile`, `docker-compose.yml`, `.env.example`.
- Documentazione: `README.md`, `docs/architettura.md` (v1.2), `docs/frontend-specifiche.md`, `docs/setup-google-oauth.md`, `docs/avvio-e-configurazione.md`.

---

# Componenti in lavorazione

- Fase 19 del piano (verifica end-to-end): non iniziata, in attesa delle informazioni dall'utente (vedi "Informazioni mancanti").

---

# Componenti mancanti

- Frontend `dr-mailroom-fe`: fuori scope, esiste solo la specifica.
- Software esterno che notifica le allerte: fuori scope.

---

# File modificati

| Percorso | Motivo |
|---|---|
| `.ai/plans/2026-10-02-mailroom-gmail-reader/plan.md` | stato, esiti di fase, decisioni D15–D18 e C1–C12, divergenze |
| `.ai/dr-guidelines-packages.json` | installazione dei pacchetti dr-dotnet-backend, dr-minimalapi, dr-efdb |
| `.github/instructions/*`, `.github/prompts/*`, `.claude/skills/dr-audit-api/` | portati dagli installer dei pacchetti |
| `.gitignore` | esclusi `Gmail/`, `client_secret*.json`, `logs/`, `src/dr-mailroom-api/data/`, `.env`, `/data/` |
| `Directory.Build.props`, `global.json` | portati da dr-dotnet-backend; sostituiti i placeholder Authors/Company/Product |
| `dr-mailroom.slnx` | solution con 3 progetti |
| `src/dr-mailroom-core/**` | dominio, persistenza, Gmail, ingestione, filtri, processi, giri |
| `src/dr-mailroom-api/**` | host: Program, endpoint, servizi, DTO, validatori, scheduling, appsettings, `.http`, launchSettings |
| `test/dr-mailroom-tests/**` | test unitari, test di integrazione degli endpoint, test SQL del lock |
| `.vscode/launch.json`, `.vscode/tasks.json` | profili di debug `coreclr` |
| `Dockerfile`, `docker-compose.yml`, `.dockerignore`, `.env.example` | container |
| `README.md`, `docs/*.md` | documentazione |
| `src/dr-mailroom-api/appsettings.local.json`, `.env` | segreti locali (ignorati da git, mai committati) |

---

# Decisioni progettuali

- Le decisioni complete sono in `DECISIONI.md` e nel piano (D1–D18, T1–T12, C1–C12).

---

# Problemi aperti

- **Limitazione:** le mail arrivate durante il backfill e già lette da esso non passano dai filtri (rimedio: riapplica).
- **Limitazione:** un arresto tra il salvataggio dei messaggi e i processi lascia quei messaggi senza processi (rimedio: riapplica).
- **Limitazione:** `HasAttachments` è dedotto da `multipart/mixed`, quindi può essere impreciso.
- **Limitazione:** la chiave API in una SPA è visibile nel browser; documentato in `docs/frontend-specifiche.md`.
- **Debito tecnico:** `EnableSensitiveDataLogging` attivo (scelta dell'utente, C4): i log EF possono contenere il refresh token cifrato.
- **Attività incompleta:** Fase 19 del piano.
- **Attività incompleta:** redirect URI in Google Console da aggiornare a `/api/v1/mailboxes/connect/callback` (l'utente lo farà più tardi).
- **Issue esterne aperte su `davraf-amuro/dr-guidelines`:**
  - #9 `readme-structure` non applicabile agli host;
  - #10 standard per il documento di architettura;
  - #11 riallineamento del documento di architettura al codice.

---

# TODO

1. Ottenere dall'utente la conferma del redirect URI in Google Console e il nome della casella e del mittente campione.
2. Eseguire la Fase 19 del piano:
   - collegamento della casella;
   - backfill;
   - verifica dello stato letto/non letto;
   - confronto delle statistiche con Gmail;
   - filtro di test (anche `Regex`) su una mail nuova;
   - riapplica con solo `AttachmentExtraction` eseguita due volte;
   - doppio giro manuale → 409;
   - `HistoryId` scaduto.
3. Verificare tutti i "Criteri di verifica finale" del piano.
4. Eseguire `/dr-verify-plan`; se passa, impostare `Stato: COMPLETATO` nel piano.
5. Commit locale del lavoro, solo su richiesta dell'utente. Nessun push senza il gate `dotnet format --verify-no-changes`.

---

# Validazione

- **Build:** `dotnet build dr-mailroom.slnx` → 0 avvisi, 0 errori (ultima esecuzione in sessione, Fase 15).
- **Test:** `dotnet test dr-mailroom.slnx` → 87 superati, 1 ignorato (test SQL senza `MAILROOM_TEST_SQL`). Con `MAILROOM_TEST_SQL` impostata: 69/69 nell'esecuzione della Fase 14, prima dei test degli endpoint.
- **Lint:** `dotnet format dr-mailroom.slnx --verify-no-changes` → exit 0 (Fase 15).
- **Container:** `docker build` exit 0; `/health` 200; `/api/v1/status` `databaseReady: true`; 401 senza chiave.
- Dopo la Fase 15 il codice non è stato toccato; solo documentazione e file Docker. Build e test non sono stati rieseguiti dopo le modifiche al Dockerfile.

---

# Rischi

- Tutto il lavoro è non committato sul branch `tentativo-1`: una perdita della working tree cancella il progetto.
- La cartella `data/keys` (key ring, ignorata da git): se va persa, i refresh token salvati non si decifrano più.
- Il container occupa la porta 5180: va fermato prima di un `dotnet run` locale sulla stessa porta.
- La password SQL è stata scritta in chiaro nella conversazione: rotazione consigliata.
- Il redirect URI in Google Console non è ancora allineato alla callback versionata: il collegamento OAuth fallirebbe con `redirect_uri_mismatch`.

---

# Prossimo passo consigliato

Chiedere all'utente la conferma del redirect URI `http://localhost:5180/api/v1/mailboxes/connect/callback` in Google Console, la casella da collegare e il mittente campione, poi avviare la Fase 19.

---

# Informazioni mancanti

- Stato del redirect URI in Google Console: sconosciuto.
- Casella Gmail da usare per la Fase 19: sconosciuta.
- Mittente campione e conteggio atteso: sconosciuti.
- Se l'utente vuole un commit locale prima della Fase 19: sconosciuto.
