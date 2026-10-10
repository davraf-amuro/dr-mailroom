# Volumi host su E:\Davide\VM\docker\dr-mailroom

Stato: COMPLETATO

## Obiettivo
Log Serilog e allegati del container `dr-mailroom-api` su `E:\Davide\VM\docker\dr-mailroom\{logs,attachments}`.

## Scope
- `.env` (non committato): `LOGS_HOST_PATH`, `ATTACHMENTS_HOST_PATH`
- Ricreazione container (`docker compose up -d`)

## Fuori scope
- `docker-compose.yml`, `Dockerfile`, codice: già parametrizzati via variabili
- `KEYS_HOST_PATH`: resta `./data/keys` (non richiesto)
- Log vecchi in `./data/logs`: restano dove sono
- `./data/attachments`: vuota, niente da migrare (percorsi in DB relativi a RootPath)

## Fasi
- [x] 1. Aggiungere le due variabili a `.env`
- [x] 2. `docker compose up -d` per ricreare il container
- [x] 3. Verifica: `docker inspect` mostra i bind, nuovo file log in `E:\Davide\VM\docker\dr-mailroom\logs`

## Criteri di verifica
- Mount `/app/logs` → `E:\Davide\VM\docker\dr-mailroom\logs`
- Mount `/data/attachments` → `E:\Davide\VM\docker\dr-mailroom\attachments`
- Container `Up`, file `app-YYYYMMDD.log` scritto nella nuova cartella
