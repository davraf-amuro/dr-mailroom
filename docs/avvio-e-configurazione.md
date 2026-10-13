# Come configurare e avviare dr-mailroom

Questa guida porta dr-mailroom da zero a un servizio funzionante, in locale o in container. Per la parte Google Cloud c'è una guida dedicata: [setup-google-oauth.md](setup-google-oauth.md).

---

## Prerequisiti

| Strumento | Versione | Serve per |
|---|---|---|
| .NET SDK | 10.0 (vedi `global.json`) | build, test, avvio locale |
| dotnet-ef | 10.x (`dotnet tool install -g dotnet-ef`) | migration del database |
| SQL Server | esistente, raggiungibile in TCP | database `dr-mailroom` |
| Docker Desktop | recente | avvio in container |
| Progetto Google Cloud | — | client OAuth: [setup-google-oauth.md](setup-google-oauth.md) |

Il database deve esistere già, con un login che abbia `db_owner` su di esso. dr-mailroom non crea né database né login.

---

## Cosa si configura e dove

I valori **non sensibili** stanno in `src/dr-mailroom-api/appsettings.json`. I **segreti** non entrano mai in file tracciati da git.

| Chiave | Esempio / default | Sensibile | Note |
|---|---|---|---|
| `ConnectionStrings:MailroomDb` | `Server=…;Database=dr-mailroom;User Id=…;Password=…;TrustServerCertificate=True` | **sì** | |
| `Mailroom:Gmail:ClientId` | `…apps.googleusercontent.com` | **sì** | dal JSON di Google |
| `Mailroom:Gmail:ClientSecret` | — | **sì** | dal JSON di Google |
| `Mailroom:Gmail:RedirectUri` | `http://localhost:5180/api/v1/mailboxes/connect/callback` | no | identico a quello in Google Console |
| `Mailroom:Gmail:QuotaUnitsPerMinute` | `12000` | no | unità di quota Gmail consumabili al minuto per casella (limite Google: 15000 per utente); oltre, le chiamate attendono |
| `Authentication:ApiKey:ApiKeyValue` | stringa lunga e casuale | **sì** | obbligatoria: senza, l'API non parte |
| `Authentication:ApiKey:HeaderName` | `x-api-key` | no | |
| `Mailroom:Schedule:Cron` | `0 0/15 * * * ?` | no | formato Quartz, **con i secondi** (questo: ogni 15 minuti) |
| `Mailroom:Retention:TrashedMessagesDays` | `180` | no | giorni dopo i quali le mail spostate nel cestino con "Elimina tutte" si eliminano dal DB (da 1 a 3650) |
| `Mailroom:Retention:PurgeCron` | `0 30 3 * * ?` | no | quando gira la purga delle mail cestinate (questo: ogni giorno alle 03:30) |
| `Mailroom:Processing:MaxParallelMailboxes` | `2` | no | caselle lavorate in parallelo dal cron |
| `Mailroom:Processing:MaxParallelMessages` | `4` | no | consumatori della coda di elaborazione (filtri e processi) per casella; Gmail si legge sempre una mail per volta |
| `Mailroom:Processing:IncludeSpamTrash` | `false` | no | Spam e Cestino esclusi |
| `Mailroom:Filtering:RegexTimeoutMs` | `250` | no | oltre il timeout, la regex non corrisponde |
| `Mailroom:Attachments:RootPath` | `data/attachments` | no | radice degli allegati estratti |
| `Mailroom:Attachments:MaxSizeBytes` | `26214400` (25 MB) | no | gli allegati più grandi si saltano |
| `Mailroom:DataProtection:KeyRingPath` | `data/keys` | no | chiavi che cifrano i refresh token: **non perderle** |
| `Mailroom:Frontend:BaseUrl` | `http://localhost:5173` | no | CORS e redirect dopo il collegamento |

Ogni valore obbligatorio mancante o non valido **ferma l'avvio** con un messaggio esplicito (per esempio un cron non valido o un `RootPath` vuoto).

### Dove mettere i segreti

| Ambiente | File | Formato chiavi |
|---|---|---|
| Sviluppo locale | `src/dr-mailroom-api/appsettings.local.json` | JSON annidato, come `appsettings.json` |
| Container | `.env` nella radice (copia di `.env.example`) | doppio underscore: `Mailroom__Gmail__ClientId` |

Entrambi i file sono in `.gitignore`. `appsettings.local.json` viene caricato **solo** con `ASPNETCORE_ENVIRONMENT=Development`.

Esempio di `appsettings.local.json`, con placeholder al posto dei valori veri:

```json
{
  "ConnectionStrings": {
    "MailroomDb": "Server=CHISSADOVE;Database=dr-mailroom;User Id=CHISSACHI;Password=CHISSAQUALE;TrustServerCertificate=True"
  },
  "Mailroom": {
    "Gmail": { "ClientId": "CHISSAQUALE.apps.googleusercontent.com", "ClientSecret": "CHISSAQUALE" }
  },
  "Authentication": {
    "ApiKey": { "ApiKeyValue": "CHISSAQUALE" }
  }
}
```

Per generare una chiave API casuale in PowerShell:

```powershell
[Convert]::ToBase64String([Security.Cryptography.RandomNumberGenerator]::GetBytes(32))
```

---

## Come creare o aggiornare il database

La migration crea 9 tabelle, gli indici UNIQUE, il vincolo `CK_Filter_Subject` e le viste `vw_SenderStats` e `vw_SenderMonthlyStats`.

```powershell
dotnet ef database update --project src/dr-mailroom-core --startup-project src/dr-mailroom-api
```

`dotnet ef` avvia l'host in ambiente Development, quindi legge la connection string da `appsettings.local.json`.

Per annullare tutto (cancella tabelle e dati):

```powershell
dotnet ef database update 0 --project src/dr-mailroom-core --startup-project src/dr-mailroom-api
```

---

## Come avviarlo in locale

```powershell
dotnet run --project src/dr-mailroom-api --launch-profile http
```

| Indirizzo | Cosa c'è |
|---|---|
| `http://localhost:5180/scalar/v1` | documentazione interattiva delle API |
| `http://localhost:5180/openapi/v1.json` | documento OpenAPI |
| `http://localhost:5180/health` | salute del processo |
| `http://localhost:5180/api/v1/status` | stato del database (anonimo) |

In VS Code ci sono i profili `Debug dr-mailroom-api (http)` e `(https)` in `.vscode/launch.json`. Richieste di esempio pronte: `src/dr-mailroom-api/dr-mailroom-api.http`.

Se il database non risponde, l'API parte comunque in **stato degradato**: `status` riporta `databaseReady: false` e `POST /api/v1/status/retry-database` ritenta la connessione.

All'avvio i giri rimasti `Running` dopo un arresto brusco vengono chiusi come `Failed`, così la casella non resta bloccata.

---

## Come avviarlo in container

1. Copia `.env.example` in `.env` e sostituisci i placeholder.
2. Se SQL Server gira **sulla stessa macchina di Docker**, nella connection string usa `Server=host.docker.internal`: dal container `localhost` è il container stesso. SQL Server deve accettare connessioni TCP sulla porta 1433.
3. Avvia:

```powershell
docker compose up -d --build
docker compose logs -f
```

| Cartella sull'host | Nel container | Contenuto |
|---|---|---|
| `./data/attachments` | `/data/attachments` | allegati estratti, divisi per sottocartella del filtro |
| `./data/keys` | `/data/keys` | key ring Data Protection |
| `./data/logs` | `/app/logs` | log Serilog giornalieri |

Le cartelle si possono spostare con `ATTACHMENTS_HOST_PATH`, `KEYS_HOST_PATH` e `LOGS_HOST_PATH` nel file `.env`.

> ⚠️ **Il key ring è indispensabile.** I refresh token salvati nel database si decifrano solo con le chiavi di `./data/keys`. Se perdi la cartella devi ricollegare tutte le caselle. Includila nei backup, insieme al database.

La porta host resta **5180**: è quella del redirect URI registrato su Google.

Il container gira come utente non root (`app`) e ascolta sulla porta 8080 interna. Il binario è lo stesso dell'avvio locale.

---

## Come collegare le caselle e verificare il funzionamento

1. Collega la casella seguendo [setup-google-oauth.md](setup-google-oauth.md), passo 7.
2. Il primo giro (al prossimo scatto del cron, oppure subito con `POST /api/v1/runs`) legge **tutto lo storico** e lo indicizza. In questo giro **i filtri non si applicano**.
3. Controlla l'avanzamento con `GET /api/v1/runs/{id}` (`messagesRead`).
4. Crea i filtri (`POST /api/v1/filters`). Valgono sulle mail nuove. Per lo storico usa `POST /api/v1/filters/{id}/reapply`, anche con i soli processi che ti servono.

Tutte le richieste, tranne `status` e `health`, vogliono l'header `x-api-key`.

---

## Come eseguire i test

```powershell
dotnet test dr-mailroom.slnx
```

I test non toccano Gmail né SQL Server: usano un client Gmail finto e un database in memoria.

Un solo test, quello del lock `sp_getapplock`, gira su SQL Server reale. Si attiva impostando la connection string nella variabile `MAILROOM_TEST_SQL`; senza variabile risulta *saltato*:

```powershell
$env:MAILROOM_TEST_SQL = "Server=…;Database=dr-mailroom;User Id=…;Password=…;TrustServerCertificate=True"
dotnet test dr-mailroom.slnx
```

---

## Prima di un push

È il gate del progetto: ogni progetto deve uscire con codice 0.

```powershell
dotnet format src/dr-mailroom-api/dr-mailroom-api.csproj --verify-no-changes
dotnet format src/dr-mailroom-core/dr-mailroom-core.csproj --verify-no-changes
dotnet format test/dr-mailroom-tests/dr-mailroom-tests.csproj --verify-no-changes
```

---

## Fonti

- Configurazione: `src/dr-mailroom-api/appsettings.json`, `src/dr-mailroom-core/Configuration/`, `src/dr-mailroom-api/Program.cs`
- Container: `Dockerfile`, `docker-compose.yml`, `.env.example`
- Piano: `.ai/plans/2026-10-02-mailroom-gmail-reader/plan.md` (C7, C10, C11, D12, T6)

*Revisione v1.2 — 2026-10-13 21:35 — claude-opus-5-5*
