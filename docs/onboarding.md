# Onboarding su dr-mailroom

Per uno sviluppatore senior che entra nel progetto. Spiega cosa ha di specifico dr-mailroom: per .NET, EF Core e Minimal API in generale valgono le basi che già conosci.

---

## 1. Il progetto in tre righe

- Servizio .NET 10 che legge una o più caselle Gmail **in sola lettura** (scope `gmail.readonly`). Lo stato letto/non letto non cambia mai.
- Conta le mail per mittente (totale e mensile) e, sulle mail che corrispondono a un filtro, salva gli allegati e registra allerte.
- Le allerte le notifica un software esterno, che le legge via API. Il servizio gira da solo con un cron, in locale o in Docker.

---

## 2. Stack e scelte tecniche

| Tecnologia | Versione | Perché |
|---|---|---|
| .NET / C# | 10 / 14 (`global.json`: SDK `10.0.100`) | Standard di progetto |
| ASP.NET Core Minimal API | 10 | Standard di progetto (`dr-minimalapi`): niente controller MVC |
| Asp.Versioning | 10.2.1 | Versione nell'URL: `api/v1/...` |
| OpenAPI + Scalar | 10.0.12 / 2.17.13 | Documentazione su `/scalar/v1`. Swagger UI è vietato dalle istruzioni |
| EF Core + SQL Server | 10.0.12 | Standard di progetto (`dr-efdb`). Provider con proiezioni `Expression`, niente repository |
| Quartz | 4.3.0 | Cron dei giri e job manuali nello stesso host dell'API |
| Google.Apis.Gmail.v1 | 1.75.0.4225 | Client Gmail e flusso OAuth |
| Data Protection | 10.0.12 | Cifra i refresh token. Il key ring sta su file, fuori dal DB |
| System.Threading.RateLimiting | 10.0.12 | Rispetta la quota Gmail per casella (`QuotaUnitsPerMinute`) |
| SimpleAuthenticationTools | 3.1.27 | Chiave API nell'header `x-api-key` |
| Serilog | 10.0.0 | Log strutturati con `ThreadId` e `TaskId` |
| xUnit + EF InMemory + WebApplicationFactory | 2.9.3 / 10.0.12 | Test senza Gmail e senza SQL Server |

Le decisioni sono numerate (D1–D20, T1–T12, C1–C12) e spiegate in [architettura.md](architettura.md). Nel codice i commenti le citano, per esempio `(D18)`.

---

## 3. Come avviare il progetto

Prerequisiti: .NET SDK 10, `dotnet-ef` 10.x, un SQL Server raggiungibile con un database già creato, un client OAuth Google ([setup-google-oauth.md](setup-google-oauth.md)).

1. Crea il file locale di configurazione dello sviluppo, con connessione al database, credenziali OAuth e chiave API. Nome, formato e chiavi sono in [avvio-e-configurazione.md](avvio-e-configurazione.md#dove-mettere-i-segreti).
2. Applica le migration:
   ```powershell
   dotnet ef database update --project src/dr-mailroom-core --startup-project src/dr-mailroom-api
   ```
3. Avvia:
   ```powershell
   dotnet run --project src/dr-mailroom-api --launch-profile http
   ```
4. Apri `http://localhost:5180/scalar/v1`.

Cose da sapere al primo avvio:

- Senza chiave API configurata l'host **non parte**. Lo stesso vale per un'opzione `Mailroom:*` obbligatoria mancante o non valida.
- Con il database giù l'host parte in **stato degradato**: `GET /api/v1/status` lo dice, `POST /api/v1/status/retry-database` ritenta.
- In VS Code ci sono già i profili di debug in `.vscode/launch.json`.

Avvio in container: `docker compose up -d --build`. Dettagli in [avvio-e-configurazione.md](avvio-e-configurazione.md#come-avviarlo-in-container).

---

## 4. Struttura del codice

```
dr-mailroom.slnx
├── src/dr-mailroom-api/        host: API REST + Quartz
├── src/dr-mailroom-core/       dominio, EF Core, Gmail, giri
└── test/dr-mailroom-tests/     xUnit
```

### dr-mailroom-api

| Cartella | Cosa contiene |
|---|---|
| `Endpoints/` | Un `<Gruppo>Mapping.cs` per MapGroup. `EndpointResults.cs` traduce gli esiti in ProblemDetails |
| `Services/` | Un servizio per risorsa, tra handler e provider. `ServiceResult` / `ServiceOutcome` per esiti Ok, Invalid, NotFound, Conflict |
| `Validators/` | Un `IValidator<T>` per ogni body e filtro di query. `ValidationRules.cs` raccoglie le regole comuni |
| `Dto/` | Un record per file, con `static Projection` EF-traducibile |
| `Scheduling/` | Job Quartz (`ScheduledRunJob`, `OnDemandRunJob`) e `QuartzRunDispatcher` |
| `Infrastructure/` | Avvio resiliente del DB e `TaskIdEnricher` di Serilog |
| `Transformers/` | `AddDocumentInformations` per il documento OpenAPI |
| `Program.cs` | Composizione dell'host |

### dr-mailroom-core

| Cartella | Cosa contiene |
|---|---|
| `Infrastructure/Mailroom/` | `MailroomDbContext`, provider, `Entities/`, `Configurations/`, `Filters/` (con `ToExpression()`), `Migrations/` |
| `Gmail/` | `GmailReader`, `GmailSession`, `GmailAuthorizer`, eccezioni (`TokenRevokedException`, `HistoryExpiredException`) |
| `Ingestion/` | `MailboxIngestionService`: lettura dei messaggi e statistiche |
| `Filtering/` | `FilterMatcher`: mittente + condizione sull'oggetto, regex con timeout |
| `Processes/` | `IMessageProcess` e le due implementazioni: `AttachmentExtractionProcess`, `AlertProcess` |
| `Runs/` | `RunOrchestrator`, `MessageProcessingQueue`, `MailboxLock`, `RunCancellationRegistry`, `ScheduledRunner` |
| `Security/` | Cifratura dei token e firma dello `state` OAuth |
| `Configuration/` | Opzioni tipizzate `Mailroom:*` validate all'avvio |

### Dove si tocca più spesso

| Cosa vuoi fare | Dove |
|---|---|
| Aggiungere un endpoint | `Endpoints/<Gruppo>Mapping.cs` → `Services/<Risorsa>Service.cs` → provider in `dr-mailroom-core` |
| Cambiare il modello dati | `Entities/` + `Configurations/`, poi una nuova migration |
| Aggiungere un processo sulle mail | Nuova classe in `Processes/` che implementa `IMessageProcess`, nuovo valore in `ProcessType` |
| Cambiare il comportamento di un giro | `Runs/RunOrchestrator.cs` |
| Aggiungere un'opzione di configurazione | `Configuration/` + `appsettings.json` + tabella in [avvio-e-configurazione.md](avvio-e-configurazione.md) |

> ⚠️ Esiste una cartella anomala `src/dr-mailroom-core/dr_mailroom_core/Infrastructure/Mailroom/Migrations/` con un `MailroomDbContextModelSnapshot.cs`. Da verificare con il team se è un residuo di `dotnet ef` da rimuovere.

---

## 5. Convenzioni obbligatorie

Le regole vengono dai pacchetti `dr-*` installati (`.ai/dr-guidelines-packages.json`: `dr-guidelines`, `dr-dotnet-backend`, `dr-minimalapi`, `dr-efdb`). Ecco quelle che incidono sul lavoro di ogni giorno.

| Regola | Fonte |
|---|---|
| Endpoint solo come extension method in `Endpoints/*Mapping.cs`, route `api/v{version:apiVersion}/{gruppo}` | `minimal-api-architecture` |
| Ogni endpoint ha `WithName`, `WithSummary`, `WithDescription`, `WithTags` e un `Produces` per ogni codice restituito | `minimal-api-architecture` |
| L'handler inietta **solo** il servizio, mai il provider o le Projection | `minimal-api-architecture` |
| Ogni body e ogni filtro di query passa da un `IValidator<T>` prima di qualsiasi query → 400 | `input-validation` |
| Liste: `GET /` con il DTO completo e `GET /summary` con quello ridotto. Lista vuota → 404 ProblemDetails | `minimal-api-architecture` |
| Proiezioni solo con new-initializer su membri semplici: niente `e.ToDto()`, che EF non sa tradurre | `minimal-api-architecture`, `database-provider` |
| DbContext in NoTracking. `AsTracking()` esplicito su update e delete | `database-provider` |
| Una classe (o record) per file. Cartella = ruolo | `code-organization` |
| `///` su ogni metodo pubblico di handler, servizio, provider e validatore, più un commento sulle operazioni DB | `code-organization` |
| Log strutturati con placeholder, mai interpolazione | `logging` |
| Letterali con significato centralizzati (costanti, opzioni, enum) | `no-hardcoded-values` |
| Vietati: controller MVC, Swagger UI, repository, AutoMapper, MediatR | `minimal-api-architecture` |

Tutte le istruzioni sono in `.github/instructions/`.

---

## 6. Flusso di lavoro

- **Piano prima del codice.** Ogni task con almeno 2 operazioni ha un piano in `.ai/plans/<YYYY-MM-DD>-<slug>/plan.md`, da far approvare prima di eseguire. Regole in `plan-tracking.instructions.md` e `dev-cycle.instructions.md`.
- **Brief.** Le richieste scritte dall'utente stanno in `briefs/`. Un brief genera sempre un piano.
- **Branch.** Il branch principale è `main`. Il repository al momento **non ha un remote**. Convenzioni di naming dei branch e di PR: da verificare con il team.
- **Test:**
  ```powershell
  dotnet test dr-mailroom.slnx
  ```
  Un solo test (il lock `sp_getapplock`) gira su SQL Server reale. Si attiva con la variabile `MAILROOM_TEST_SQL`.
- **Prima di ogni push** (gate obbligatorio, codice di uscita 0 su tutti e tre):
  ```powershell
  dotnet format src/dr-mailroom-api/dr-mailroom-api.csproj --verify-no-changes
  dotnet format src/dr-mailroom-core/dr-mailroom-core.csproj --verify-no-changes
  dotnet format test/dr-mailroom-tests/dr-mailroom-tests.csproj --verify-no-changes
  ```
- **CI/CD:** nessuna pipeline nel repository. Il rilascio è `docker compose up -d --build` su Docker locale.

---

## 7. Dati sensibili e configurazione locale

Regola completa: `.github/instructions/sensitive-data.instructions.md`. In sintesi:

- I file committati contengono **solo placeholder** (`CHISSADOVE`, `CHISSAQUALE`).
- I valori reali stanno in file locali esclusi da git: uno per lo sviluppo, caricato solo in `Development`, e uno per il container. Quali sono e come si compilano: [avvio-e-configurazione.md](avvio-e-configurazione.md#dove-mettere-i-segreti).
- Anche il JSON del client OAuth scaricato da Google resta fuori da git.
- Le schede operative con i valori reali sono `docs/*-wiki.md`: escluse da git, da condividere solo in privato.

> ⚠️ **Il key ring Data Protection va salvato insieme al database.** Senza, i refresh token salvati non si decifrano più e tutte le caselle vanno ricollegate.

---

## 8. Cosa leggere dopo

| File | Perché |
|---|---|
| [architettura.md](architettura.md) | Componenti, decisioni, come funzionano giro, backfill e riapplica, modello dati |
| [avvio-e-configurazione.md](avvio-e-configurazione.md) | Tutte le chiavi di configurazione, migration, container, test |
| [setup-google-oauth.md](setup-google-oauth.md) | Google Cloud e collegamento delle caselle |
| [card-dr-mailroom-api.md](card-dr-mailroom-api.md) | Scheda dell'API: gruppi, dipendenze, hosting |
| `docs/endpoint-*.md` | Un documento per ogni gruppo di endpoint |
| [frontend-specifiche.md](frontend-specifiche.md) | Specifica del frontend `dr-mailroom-fe` |
| `DECISIONI.md`, `HANDOFF.md` | Decisioni prese e stato del lavoro, nella radice |
| `src/dr-mailroom-api/dr-mailroom-api.http` | Richieste di esempio pronte |
| `.ai/plans/` | Storia dei piani eseguiti e in corso |

Per i dubbi non coperti da questi file: Davide Raffagli.

---
*Revisione v1.1 — 2026-10-11 17:25 — claude-opus-5-5*
