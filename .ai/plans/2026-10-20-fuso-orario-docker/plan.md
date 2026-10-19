# Piano: container dr-mailroom con fuso orario del PC (Europe/Rome)
Data: 2026-10-20
Stato: COMPLETATO

## Obiettivo
Log, nome dei file di log e cron Quartz del container seguono l'ora italiana del PC, non l'UTC.

## Analisi
- L'orologio del container è già sincronizzato con Windows (VM WSL2 di Docker Desktop): differisce solo il fuso, UTC di default in ogni container.
- Docker Desktop non ha un'impostazione globale del fuso: si imposta per immagine o per container (`TZ`). "Tutto docker" = questo container; gli altri progetti (nginx-proxy, CondominioPiaveIII) sono fuori scope.
- L'immagine `mcr.microsoft.com/dotnet/aspnet:10.0` contiene già `tzdata` (`/usr/share/zoneinfo/Europe/Rome` presente): basta `TZ`, nessun pacchetto da installare.
- Serilog (`Program.cs`) usa l'ora locale per `{Timestamp}` e per il nome del file giornaliero → con `TZ` diventano ora italiana.
- Quartz: i trigger cron (`SchedulingExtensions.cs`) non fissano un fuso → usano quello locale. Effetto: `PurgeCron` `0 30 3 * * ?` passa da 03:30 UTC (05:30 italiane) a 03:30 italiane, come già dichiarato in `docs/avvio-e-configurazione.md`. Il cron ogni 15 minuti non cambia.
- Il codice non usa `DateTime.Now` né `TimeZoneInfo`: DB e API restano in UTC (`*Utc`), nessun impatto sui dati.

## Scope
### File da modificare
- [x] `Dockerfile` — `TZ=Europe/Rome` nel blocco `ENV` dello stage `final` (default dell'immagine, vale anche senza compose)
- [x] `.env.example` — voce facoltativa commentata `# TZ=Europe/Rome` per cambiare fuso
- [x] `docs/avvio-e-configurazione.md` — nota nella sezione Docker: container in ora italiana (log, file giornalieri, cron), dati in UTC; footer v1.3

### Perimetro negativo
- Non toccherò: `docker-compose.yml` (il `.env` arriva già al container via `env_file`), codice C#, configurazione Serilog e Quartz, `.env` reale, altri progetti Docker.
- Nessun push.

## Fasi

### Fase 1: Fuso nell'immagine
- **Stato**: [x]
- **Precondizione**: `Dockerfile` senza `TZ`
- **File**: `Dockerfile`
- **Operazione**: EDIT
- **Azione**: aggiungi `TZ=Europe/Rome` al blocco `ENV` dello stage `final`, con commento sul perché
- **Tool ammessi**: nessuno
- **Verifica passo**: file riletto, una sola occorrenza di `TZ=`
- **Su divergenza**: STOP — scrivi `⚠️ Divergenza Fase 1: <cosa>` in plan.md, non procedere

### Fase 2: Esempio di configurazione
- **Stato**: [x]
- **Precondizione**: Fase 1 `[x]`
- **File**: `.env.example`
- **Operazione**: EDIT
- **Azione**: aggiungi blocco commentato "Facoltativo: fuso orario del container (default Europe/Rome)" con `# TZ=Europe/Rome`
- **Tool ammessi**: nessuno
- **Verifica passo**: file riletto
- **Su divergenza**: STOP — scrivi `⚠️ Divergenza Fase 2: <cosa>` in plan.md, non procedere

### Fase 3: Riallinea il documento al codice
- **Stato**: [x]
- **Precondizione**: Fase 2 `[x]`
- **File**: `docs/avvio-e-configurazione.md`
- **Operazione**: EDIT
- **Azione**: dopo la tabella dei volumi, nota sul fuso: container in `Europe/Rome` (orari dei log, nome dei file giornalieri, cron Quartz), `TZ` nel `.env` per cambiarlo, date su DB e API in UTC; footer `v1.3` secondo `doc-versioning`
- **Tool ammessi**: nessuno
- **Verifica passo**: affermazioni coerenti con Fasi 1–2 e con il codice
- **Su divergenza**: STOP — scrivi `⚠️ Divergenza Fase 3: <cosa>` in plan.md, non procedere

### Fase 4: Verifica, commit e deploy
- **Stato**: [x]
- **Precondizione**: Fasi 1–3 `[x]`
- **File**: nessuno
- **Operazione**: nessuna
- **Azione**: `/dr-verify-plan`; commit su `main`; `docker compose up -d --build`; `docker exec dr-mailroom-api date` mostra CEST/CET; nuova riga di log con ora italiana in `LOGS_HOST_PATH`
- **Tool ammessi**: docker, git (no push)
- **Verifica passo**: ora del container = ora del PC; container `Up`
- **Su divergenza**: STOP — scrivi `⚠️ Divergenza Fase 4: <cosa>` in plan.md, non procedere

## Criteri di verifica finale
- [x] `docker exec dr-mailroom-api date` mostra l'ora italiana (fuso CEST/CET)
- [x] Le righe nuove del log hanno l'ora italiana; il file del giorno porta la data italiana
- [x] Container avviato e API che risponde
- [x] `docs/avvio-e-configurazione.md` allineato
