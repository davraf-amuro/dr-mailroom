# Card: dr-mailroom-core

## Identificazione
- **Progetto:** dr-mailroom-core
- **Solution:** dr-mailroom.slnx
- **Workspace:** dr-mailroom.code-workspace (cartella superiore al repository)
- **Repository:**
- **Tipo Applicazione:** Class library .NET 10 (consumata da `dr-mailroom-api`)
- **Pattern Architetturale:** Provider EF Core + servizi di dominio (ingestione, filtri, processi, giri)
- **Versione Corrente:**
- **Owner/Team:** davraf
- **Referente:** Davide Raffagli
- **Contatto Supporto:**

## Stack Tecnologico
- **Linguaggio Principale:** C# 14
- **Framework:** .NET 10
- **Target Framework:** net10.0
- **SDK Version:** Microsoft.NET.Sdk

## Dipendenze

### Progetti Interni
- Nessuno. È referenziato da `dr-mailroom-api` e `dr-mailroom-tests`.

### Pacchetti Esterni
| Pacchetto | Versione | Scopo |
|-----------|----------|-------|
| Google.Apis.Gmail.v1 | 1.75.0.4225 | Client Gmail API e flusso OAuth |
| Microsoft.AspNetCore.DataProtection | 10.0.12 | Cifratura dei refresh token |
| Microsoft.AspNetCore.DataProtection.Extensions | 10.0.12 | Key ring su file |
| Microsoft.EntityFrameworkCore.SqlServer | 10.0.12 | Accesso a SQL Server |
| Microsoft.Extensions.Options.ConfigurationExtensions | 10.0.12 | Opzioni tipizzate `Mailroom:*` |
| Microsoft.Extensions.Options.DataAnnotations | 10.0.12 | Validazione delle opzioni all'avvio |
| Quartz | 4.3.0 | Job dei giri |
| System.Threading.RateLimiting | 10.0.12 | Limite di quota Gmail per casella |

## Database
| Connection String Key | Nome Database | Tipo | Server/Host | Username | Provider/ORM |
|-----------------------|---------------|------|-------------|----------|--------------|
| `ConnectionStrings:MailroomDb` | da `appsettings.local.json` | SQL Server | da `appsettings.local.json` | da `appsettings.local.json` | EF Core 10 (`MailroomDbContext`, migration in `Infrastructure/Mailroom/Migrations`) |

## Servizi Esterni
| Tipo | Nome/Endpoint | Protocollo | Autenticazione | Scopo/Descrizione |
|------|---------------|------------|----------------|-------------------|
| API REST | Gmail API v1 | HTTPS | OAuth 2.0, scope `gmail.readonly` | Lettura di mail, cronologia e allegati |

## Configurazione e Hosting
- **Entrypoint:** nessuno, è una libreria. Registrazione DI in `src/dr-mailroom-core/MailroomCoreExtensions.cs`
- **Ambiente Test:** Docker locale (dentro il container `dr-mailroom-api`)
- **Ambiente Produzione:** Docker locale (dentro il container `dr-mailroom-api`)

---
*Revisione v1.0 — 2026-10-08 22:49 — claude-opus-5-5*
