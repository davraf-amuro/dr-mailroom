# Card: dr-mailroom-api

**Minimal API** che legge caselle Gmail in sola lettura, conta le mail per mittente e lavora quelle filtrate (allegati e allerte), con giri pianificati da Quartz.
Espone 7 endpoint group: `Status`, `Mailboxes`, `Filters`, `Runs`, `Stats`, `Alerts`, `Messages`.

## Identificazione
- **Progetto:** dr-mailroom-api
- **Solution:** dr-mailroom.slnx
- **Workspace:** dr-mailroom.code-workspace (cartella superiore al repository)
- **Repository:**
- **Tipo Applicazione:** Minimal API (.NET 10) + scheduler Quartz nello stesso host
- **Pattern Architetturale:** Minimal API + Service + Provider + Scalar
- **Versione Corrente:**
- **Owner/Team:** davraf
- **Referente:** Davide Raffagli
- **Contatto Supporto:**

## Stack Tecnologico
- **Linguaggio Principale:** C# 14
- **Framework:** .NET 10 ASP.NET Core Minimal API
- **Target Framework:** net10.0
- **SDK Version:** Microsoft.NET.Sdk.Web (SDK `10.0.100`, `rollForward: latestMinor`)

## Endpoint Groups

| Group | File Mapping | Route Base | Tag Scalar |
|-------|-------------|------------|------------|
| `Status` | `Endpoints/HealthMapping.cs` | `api/v1/status` (+ `/health` fuori da Scalar) | Status |
| `Mailboxes` | `Endpoints/MailboxesMapping.cs` | `api/v1/mailboxes` | Mailboxes |
| `Filters` | `Endpoints/FiltersMapping.cs` | `api/v1/filters` | Filters |
| `Runs` | `Endpoints/RunsMapping.cs` | `api/v1/runs` | Runs |
| `Stats` | `Endpoints/StatsMapping.cs` | `api/v1/stats` | Stats |
| `Alerts` | `Endpoints/AlertsMapping.cs` | `api/v1/alerts` | Alerts |
| `Messages` | `Endpoints/MessagesMapping.cs` | `api/v1/messages` | Messages |

## Dipendenze

### Progetti Interni
- `dr-mailroom-core` — dominio, accesso al database, client Gmail, giri ([card-dr-mailroom-core.md](card-dr-mailroom-core.md))

### Pacchetti Esterni
| Pacchetto | Versione | Scopo |
|-----------|----------|-------|
| Asp.Versioning.Mvc.ApiExplorer | 10.2.1 | Versioning nell'URL (`api/v1/...`) |
| Microsoft.AspNetCore.OpenApi | 10.0.12 | Documento OpenAPI |
| Scalar.AspNetCore | 2.17.13 | Documentazione interattiva |
| Microsoft.EntityFrameworkCore.Design | 10.0.12 | Strumenti `dotnet ef` (solo design time) |
| Quartz.Extensions.Hosting | 4.3.0 | Scheduler dei giri nell'host |
| Serilog.AspNetCore | 10.0.0 | Logging strutturato |
| Serilog.Enrichers.Thread | 4.0.0 | `ThreadId` nei log |
| Serilog.Sinks.Console | 6.1.1 | Log su console |
| Serilog.Sinks.File | 7.0.0 | Log su file giornaliero |
| SimpleAuthenticationTools | 3.1.27 | Autenticazione con chiave API |
| TinyHelpers.AspNetCore | 4.2.18 | ProblemDetails ed exception handler di default |

## Database
| Connection String Key | Nome Database | Tipo | Server/Host | Username | Provider/ORM |
|-----------------------|---------------|------|-------------|----------|--------------|
| `ConnectionStrings:MailroomDb` | da `appsettings.local.json` | SQL Server | da `appsettings.local.json` | da `appsettings.local.json` | EF Core 10 |

## Servizi Esterni
| Tipo | Nome/Endpoint | Protocollo | Autenticazione | Scopo/Descrizione |
|------|---------------|------------|----------------|-------------------|
| API REST | Gmail API v1 | HTTPS | OAuth 2.0 (`Mailroom:Gmail:ClientId`, `Mailroom:Gmail:ClientSecret`), scope `gmail.readonly` | Lettura di elenco, metadati e allegati delle mail |
| Identity provider | Google OAuth 2.0 | HTTPS (redirect browser) | Client OAuth; ritorno su `Mailroom:Gmail:RedirectUri` | Consenso dell'utente e refresh token per casella |
| Frontend | `dr-mailroom-fe` (`Mailroom:Frontend:BaseUrl`) | HTTP | Chiave API (`x-api-key`) | CORS e redirect dopo il collegamento della casella |
| Consumer | Software esterno di notifica | HTTP | Chiave API (`x-api-key`) | Legge le allerte da notificare e le marca consumate |

## Configurazione e Hosting
- **Entrypoint:** `src/dr-mailroom-api/Program.cs`
- **Ambiente Test:** Docker locale (`docker-compose.yml`, container `dr-mailroom-api`, porta host 5180)
- **Ambiente Produzione:** Docker locale (`docker-compose.yml`, container `dr-mailroom-api`, porta host 5180)
- **URL Scalar (dev):** `http://localhost:5180/scalar/v1` (profilo `http`; `https://localhost:7143/scalar/v1` con il profilo `https`)

## Documentazione API
- **OpenAPI/Swagger:** Scalar (`/scalar/v1`), documento su `/openapi/v1.json`
- **Versioning API:** UrlSegmentApiVersionReader
- **Versioni Supportate:** v1

---
*Revisione v1.0 — 2026-10-08 22:49 — claude-opus-5-5*
