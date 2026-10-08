---
applyTo: "**"
---

# Minimal API Design Rules (AI Agent)

Scopo: regole obbligatorie per progetti Minimal API .NET 10. Segui sempre. Testo ottimizzato per token.

## Stack (obbligatorio)
- .NET 10, ASP.NET Core Minimal API (no Controllers)
- Tinyhelpers.AspNetCore (installare sempre)
- Scalar per docs (no Swagger UI)
- Asp.Versioning.Mvc.ApiExplorer (obbligatorio per Scalar)
- Serilog (opzionale — chiedi in fase di raccolta informazioni; se confermato, segui sezione "Serilog — configurazione completa")
- Entity Framework Core 10: se già presente nel `.csproj`, dichiaralo e prosegui senza chiedere. Chiedi solo se assente.
- Autenticazione/autorizzazione: **solo se esplicitamente richiesta dal developer** — nessun pattern auth di default. Se richiesta, segui la sezione "Autenticazione": lo schema dipende dal tipo di client, non è una scelta libera
- Aggiungi sempre il file launchSettings.json con configurazione per IIS Express e Kestrel
- Aggiungi sempre il file appsettings.local.json, aggiungi la chiamata in program.cs, e ignora il file in .gitignore
- Aggiungi sempre `.vscode/launch.json` e `.vscode/tasks.json` con profili debug `coreclr`
- Dati sensibili: segui sempre `sensitive-data.instructions.md`

## Raccolta informazioni iniziale (gate obbligatorio)

Prima di creare qualsiasi file, raccogli tutto in **un unico messaggio**, nell'ordine:

1. **Nome entità + campi** — se il task coinvolge un'entità e non sono specificati
2. **Nome progetto** — se non ricavabile dal contesto: proponi `<nomecartella>.api` in lowercase come default (es. cartella `test-test/` → proponi `test-test.api`) e attendi conferma. **Non inventare e non procedere senza risposta.**
3. **Serilog** — chiedi se aggiungere Serilog. Se sì: configura scrittura su file + console (vedi sezione "Serilog — configurazione completa"). Se no: usa `ILogger` built-in di ASP.NET Core.
4. **Autenticazione** — chiedi se il progetto prevede **utenti umani con login** (registrazione, profilo, cambio password). Se sì: applica la sezione "Autenticazione", che determina lo schema in base ai client. Se no: non proporre auth — nessun pacchetto, nessun middleware.
5. **Connessione DB** — solo se MCP db-schema attivo e sono disponibili più connessioni

Regola assoluta: se il nome progetto non è ricavabile né confermato, **fermati**. Non inferire, non usare placeholder come `MyApi` o `WebApi`.

## Vietato
- MVC Controllers
- Swagger UI
- IRepository pattern
- AutoMapper
- MediatR
- `AddJwtBearer` per utenti umani, salvo richiesta esplicita e motivata del developer — il JWT non è revocabile prima della scadenza: dopo logout o cambio password il token già emesso resta valido

## Struttura progetto
- src/<project>/
  - Dto/
  - Endpoints/
  - Infrastructure/Provider/{Entities,Filters,*DbContext.cs,*Provider.cs}
  - Services/
  - Transformers/
  - Validators/
  - Properties/
  - Program.cs
- test/
- docs/

## ⚠️ Vincoli critici (leggere prima dei pattern)

**Regola 13 — Commenti obbligatori**: tutti i `public` method di provider/service/handler/validator richiedono `///` XML doc + commento inline su ogni operazione DB. Esclusi: getter/setter banali, wrapper di una riga. Non generare codice senza questi commenti.

**EF traducibilità**: le `Projection` usano **esclusivamente** new-initializer con accesso a membri primitivi (`e.Id`, `e.Nome`). Metodi extension (`e.ToDto()`) e qualsiasi chiamata a funzione non sono EF-traducibili → causano full table scan silente. Se in dubbio, usa solo `e.Campo`.

---

## Regole core (sempre)
1) Endpoint solo in extension methods in Endpoints/*Mapping.cs
2) URL standard: api/v{version:apiVersion}/{gruppo}/{comando?}
3) Usa route group con WithTags + WithApiVersionSet + MapToApiVersion
4) Versioning: UrlSegmentApiVersionReader + ApiExplorer GroupNameFormat='v'VVV + SubstituteApiVersionInUrl=true
5) Parametri handler: route -> query (o [AsParameters] se >2) -> body -> servizi DI -> CancellationToken ultimo
6) OpenAPI metadata completo — **ogni endpoint** richiede obbligatoriamente:
   - `WithSummary("...")` — descrizione breve (max 10 parole)
   - `WithDescription("...")` — descrizione estesa per i consumer
   - `WithTags("...")` — gruppo logico (uguale per tutti gli endpoint della stessa entità)
   - `WithName("<Verbo><Risorsa>")` — convention: `Get<Entity>List`, `Get<Entity>By<Key>`, `Post<Entity>`, `Put<Entity>`, `Delete<Entity>`
   - `Produces<T>(StatusCode)` per ogni `TypedResults` restituito — inclusi 400 e 404 ove applicabile
   - Endpoint senza questi metadata = non accettabile (Scalar muto per i consumer)
7) Program.cs deve chiamare gli extension methods dopo MapOpenApi
8) Transformer OpenAPI: classe AddDocumentInformations in Transformers/ + registrazione AddOpenApi
9) GET list con provider: filtro dedicato obbligatorio su tutti i campi entità; proiezione EF-traducibile via `Expression<Func<TEntity, TDto>>`; ProblemDetails 404 se vuoto.
   - Deriva filtro dall'entità senza chiedere nulla all'utente — applica regole tipo e genera subito
   - Regole tipo → campo filter:
     - string       → string?  — `(Descrizione == null || e.Descrizione.Contains(Descrizione))`
     - int / int?   → int?     — `(Marca == null || e.Marca == Marca)`
     - DateTime / DateTime? → due param NomeFrom? + NomeTo? — range >= / <=
     - bool / bool? → bool?    — `(Flag == null || e.Flag == Flag)`
   - Tutti i campi filtro nullable — nessun campo richiesto
   - Filter class: `Infrastructure/Provider/Filters/<Entity>Filter.cs` — espone `ToExpression()` che ritorna `Expression<Func<TEntity, bool>>`
   - Ogni DTO record espone `static Expression<Func<TEntity, TDto>> Projection => e => new(...)` — EF-traducibile
   - **DTO multipli obbligatori**: per ogni entity genera almeno due DTO record con Projection — `<Entity>Dto` completo (tutti i campi) e `<Entity>SummaryDto` ridotto (chiave + campi identificativi). Esponi `GET /` con il DTO completo e `GET /summary` con il ridotto — stesso filter, stesso provider, selector diverso
   - **Ogni DTO record in file separato**: `Dto/<Entity>Dto.cs` e `Dto/<Entity>SummaryDto.cs` — mai due record nello stesso file, anche se correlati (segui `code-organization.instructions.md` Regola 1, vale anche per i record)
   - Handler usa `[AsParameters]` se filtro ha ≥ 2 campi
   - Il filtro ha sempre un validator `<Entity>FilterValidator : IValidator<<Entity>Filter>` con regole **ereditate dai metadati dell'entità** (es. `varchar(50)` → maxLength 50); l'handler valida prima della query → 400; `Produces(400)` anche sui GET con filtro. Vedi `input-validation.instructions.md`
   - Provider: `Get<Entity>Async<TDto>(<Entity>Filter filter, Expression<Func<TEntity, TDto>> selector, CancellationToken ct)` — mai GetAllAsync senza filtro
   - Chiamata handler: tramite Service (vedi regola 12) — il Service passa `MyDto.Projection` al provider
10) Ogni input esterno (body POST/PUT/PATCH **e filtri query dei GET**): valida con `IValidator<T>` prima di processare; segui `input-validation.instructions.md`
11) Ogni nuovo progetto include `HealthMapping.cs` con: `MapHealthChecks("/health")` (infrastruttura, non in Scalar) + `GET /api/v1/status` versioned (consumer-facing, in Scalar). Se il progetto dipende dal DB con seed/inizializzazione all'avvio, segui `database-startup-resilience.instructions.md` (avvio resiliente, stato DB, retry, 503 a runtime)
12) **Service layer obbligatorio per il CRUD di entità**: classe `Services/<Entity>Service.cs` tra handler e provider. Gli handler iniettano **solo il Service** — mai il provider, mai le Projection. Il Service riceve il provider via primary constructor (DI). Il Service: sceglie la Projection per ogni caso d'uso, mappa request→entity (metodo privato `ToEntity`) ed entity→DTO, espone `GetAllAsync`, `GetSummariesAsync`, `GetByIdAsync`, `CreateAsync`, `UpdateAsync`, `DeleteAsync`
13) **Commenti obbligatori** (provider, service, handler, validator): `///` XML doc di una riga che spieghi il ruolo nel flusso a un dev senior + commento inline su ogni operazione DB — segui `code-organization.instructions.md` Regola 6. Casi obbligatori: (a) tutti i `public` method; (b) provider/service/handler/validator indipendentemente dallo scope; (c) logica con predicati composti o query composition. Esclusi: getter/setter banali, wrapper di una riga.

## Scoperta automatica struttura DB via MCP

Se nel progetto è configurato un MCP server `db-schema` (verifica in `.claude/settings.json` o `~/.claude/settings.json`, chiave `mcpServers`):

1. **Non chiedere i campi all'utente** — leggi la struttura dalla tabella:
   - Usa `mcp__db-schema__use_connection` per selezionare la connessione
   - Usa `mcp__db-schema__get_view_columns` o strumento equivalente per ottenere colonne e tipi
2. **Presenta il piano di lavoro** con la struttura letta:
   - Elenca colonne rilevate, tipo C# mappato, se nullable
   - Dichiara: "Struttura rilevata via MCP db-schema. Procedo con piano."
3. **Genera il validator immediatamente** — non chiedere nulla all'utente:
   - Regole inferite dal metadato (applica sempre):
     - `NOT NULL` → `required`
     - `varchar(N)` / `nvarchar(N)` → `maxLength = N`
     - colonna nullable → campo opzionale (nessun required)
     - tipi numerici (`int`, `decimal`) → validazione di tipo garantita da C#
   - Campi con regole non determinabili dallo schema → nessun errore per quel campo + commento `// TODO: validazione`
   - Non chiedere regole all'utente: genera subito, codice compilabile e funzionante.

Se MCP db-schema non è configurato: comportamento standard (chiedi i campi all'utente prima di procedere).

---

## Serilog — configurazione completa

Applica solo se confermato nella raccolta informazioni iniziale.

**Pacchetti NuGet da aggiungere:**
```
Serilog.AspNetCore
Serilog.Sinks.File
Serilog.Sinks.Console
```

**Configurazione (Program.cs, template di output, enrichers ThreadId/TaskId):** segui `logging.instructions.md` — è la **fonte unica** della configurazione Serilog. Non duplicare qui varianti alternative: due configurazioni divergenti per lo stesso stack generano codice incoerente.

**Cartella logs — aggiungere a `.gitignore`:**
```
logs/
```

---

## Autenticazione

Applica solo se confermato nella raccolta informazioni iniziale. Nessun pattern auth di default: se il developer non l'ha chiesta, non proporla.

Lo schema **non è una scelta libera**: dipende da chi sono i client.

### Matrice di scelta

| Caso | Schema | Pacchetto |
|---|---|---|
| Utenti umani, frontend browser sullo stesso dominio dell'API | ASP.NET Identity + cookie `HttpOnly` | `Microsoft.AspNetCore.Identity.EntityFrameworkCore` |
| Utenti umani, domini diversi oppure client non-browser previsto (app mobile, desktop) | Identity + `AddBearerToken` — token opachi, refresh incluso | `Microsoft.AspNetCore.Identity.EntityFrameworkCore` |
| Entrambi i tipi di client | Stessi endpoint, si aggiunge solo il secondo schema | `Microsoft.AspNetCore.Identity.EntityFrameworkCore` |
| Nessun utente umano — sola comunicazione servizio-a-servizio | `SimpleAuthenticationTools` (API Key) | `SimpleAuthenticationTools` |
| Auth non richiesta | Nessuna | — |

Se il developer non ha detto se ci saranno client non-browser, chiediglielo: è la sola informazione che discrimina tra la prima e la seconda riga.

### Perché Identity

Copre registrazione, login, profilo, cambio password, reset e lockout senza codice a mano. `MapIdentityApi<TUser>()` espone gli endpoint già pronti, incluso `/manage/info` per email e password.

Non scrivere a mano una tabella utenti con hash della password: il codice risparmiato è poco, quello dimenticato — lockout, reset, conferma email, rotazione dei token — è molto.

### Cookie e bearer convivono

`MapIdentityApi<TUser>()` serve entrambi dagli stessi endpoint, sulla stessa anagrafica utenti:

| Chiamata | Risposta | Client |
|---|---|---|
| `POST /login?useCookies=true` | `Set-Cookie` `HttpOnly` | frontend browser |
| `POST /login` | `accessToken` + `refreshToken` opachi | app mobile, client non-browser |

Conseguenza operativa: partire dal cookie non è irreversibile. Quando arriva un client non-browser si aggiunge lo schema bearer senza toccare l'anagrafica utenti e senza migrazioni.

### Cosa evitare, e perché

- **Token in `localStorage`** — leggibile da JavaScript, quindi rubabile con un XSS. Il cookie `HttpOnly` non lo è. Se il bearer è inevitabile, il token sta in memoria e si rinnova col refresh: vedi `frontend-organization.instructions.md`
- **JWT per utenti umani** — il token è autoconsistente e l'API non lo confronta con nessun archivio. Non esiste revoca: dopo un logout o un cambio password il token già emesso resta valido fino alla scadenza. Vale la pena solo quando il lookup a database è un collo di bottiglia **misurato**, non ipotizzato

### Rilascio in container

Due regole obbligatorie appena un'applicazione containerizzata ha un'auth con cookie o bearer. Entrambe le omissioni si manifestano come guasti intermittenti, non come errori: senza queste righe vengono diagnosticate a lungo come bug applicativi.

**1. Portachiavi Data Protection persistito e condiviso.**

I cookie di Identity e i token opachi di `AddBearerToken` sono cifrati con le chiavi Data Protection. Senza configurazione esplicita finiscono nel filesystem del container, con due conseguenze: ricreare il container invalida ogni sessione (tutti gli utenti sloggati a ogni deploy), e con `replicas > 1` ogni replica ha il proprio portachiavi (il login su una replica non vale sull'altra, quindi 401 a intermittenza).

- Default: `PersistKeysToDbContext<TDbContext>()` — pacchetto `Microsoft.AspNetCore.DataProtection.EntityFrameworkCore`, sfrutta il database già presente per Identity
- Alternativa: `PersistKeysToFileSystem` su un volume montato, quando il database non è disponibile all'avvio
- In entrambi i casi `SetApplicationName("<nome-applicazione>")` esplicito: le repliche devono condividere lo stesso spazio di chiavi

**2. `UseForwardedHeaders` come primo middleware della pipeline.**

Con TLS terminato sul reverse proxy l'applicazione riceve `http` e considera la connessione non sicura: il cookie `Secure` non viene emesso e i redirect nascono con lo schema sbagliato.

- Inoltra `XForwardedProto` e `XForwardedFor`
- **Trappola Docker**: di default vengono accettate solo le intestazioni provenienti dal loopback, mentre il reverse proxy arriva dall'indirizzo IP di un altro container. Vanno configurati `KnownNetworks`/`KnownProxies` per la rete Docker, altrimenti le intestazioni sono ignorate senza alcun errore visibile e il rimedio sembra non avere effetto

Identity richiede il database all'avvio: vale anche `database-startup-resilience.instructions.md`. Per il lato stack — volumi, variabili d'ambiente, repliche — vedi `docker-swarm-compose.instructions.md`.

---

## Pattern richiesti (copiabili)

### ApiVersionFactory

```csharp
// Properties/ApiVersionFactory.cs
public static class ApiVersionFactory
{
    public static readonly ApiVersion Version1 = new(1, 0);
}
```

### Extension method + group (pattern starter: HealthMapping)
```csharp
using Asp.Versioning;
using Asp.Versioning.Builder;   // ApiVersionSet è qui (v10.0.0+)

namespace <Project>.Endpoints;

public static class HealthMapping
{
    public static IEndpointRouteBuilder MapHealthEndpoints(this IEndpointRouteBuilder routes, ApiVersionSet versionSet)
    {
        routes.MapHealthChecks("/health");

        var group = routes.MapGroup("api/v{version:apiVersion}/status")
            .WithTags("Status")
            .WithApiVersionSet(versionSet)
            .MapToApiVersion(ApiVersionFactory.Version1);

        group.MapGet("/", () => TypedResults.Ok(new { status = "ok" }))
            .Produces<object>(StatusCodes.Status200OK)
            .WithName("GetStatus")
            .WithSummary("API status")
            .WithDescription("Returns ok when the API is running");

        return routes;
    }
}
```

### Program.cs (versioning + openapi + mapping)
```csharp
builder.Services.AddApiVersioning(options =>
{
    options.ApiVersionReader = new UrlSegmentApiVersionReader();
    options.DefaultApiVersion = ApiVersionFactory.Version1;
    options.AssumeDefaultVersionWhenUnspecified = true;
    options.ReportApiVersions = true;
})
.AddApiExplorer(options =>
{
    options.GroupNameFormat = "'v'VVV";
    options.SubstituteApiVersionInUrl = true;
});

builder.Services.AddOpenApi(options =>
{
    options.AddDocumentTransformer<AddDocumentInformations>();
});

builder.Services.AddHealthChecks();

var versionSet = app.NewApiVersionSet()
    .HasApiVersion(ApiVersionFactory.Version1)
    .Build();

app.MapOpenApi();
app.MapScalarApiReference();
app.MapHealthEndpoints(versionSet);
```

### OpenAPI transformer
```csharp
using Microsoft.AspNetCore.OpenApi;
using Microsoft.OpenApi;   // non Microsoft.OpenApi.Models (v2.0.0+)

public class AddDocumentInformations : IOpenApiDocumentTransformer
{
    public Task TransformAsync(OpenApiDocument document, OpenApiDocumentTransformerContext context, CancellationToken cancellationToken)
    {
        document.Info.Title = "<SolutionName> API";
        document.Info.Description = "<Short description>";
        document.Info.Version = "v1";
        document.Info.Contact = new OpenApiContact
        {
            Name = "<Organizzazione>, @ <Year>",
            Url = new Uri("https://<dominio>/"),
            Email = "<email-contatto>"
        };

        return Task.CompletedTask;
    }
}
```

### GET list con provider, filtro e proiezione Expression

```csharp
// DTO — Dto/<Entity>Dto.cs
// Ogni DTO espone una Projection EF-traducibile (new-initializer con member access primitivi)
public record ModelKitDto(int Id, string Descrizione, int? Marca, DateTime DataRegistrazione)
{
    public static Expression<Func<ModelKit, ModelKitDto>> Projection =>
        e => new(e.Id, e.Descrizione, e.Marca, e.DataRegistrazione);
}

// DTO parziale — stessa entità, campi ridotti → SELECT ottimizzato
public record ModelKitSummaryDto(int Id, int? Marca)
{
    public static Expression<Func<ModelKit, ModelKitSummaryDto>> Projection =>
        e => new(e.Id, e.Marca);
}

// Filter — Infrastructure/Provider/Filters/<Entity>Filter.cs
// ToExpression() incapsula tutta la logica WHERE in un'unica Expression EF-traducibile
public class ModelKitFilter
{
    public string? Descrizione { get; set; }
    public int? Marca { get; set; }
    public DateTime? DataRegistrazioneFrom { get; set; }
    public DateTime? DataRegistrazioneTo { get; set; }

    public Expression<Func<ModelKit, bool>> ToExpression() =>
        e => (Descrizione == null || e.Descrizione.Contains(Descrizione))
          && (Marca == null || e.Marca == Marca)
          && (DataRegistrazioneFrom == null || e.DataRegistrazione >= DataRegistrazioneFrom)
          && (DataRegistrazioneTo == null || e.DataRegistrazione <= DataRegistrazioneTo);
}

// Provider — generico sul tipo di ritorno, mai GetAllAsync senza filtro
public async Task<IEnumerable<TDto>> GetAsync<TDto>(
    ModelKitFilter filter,
    Expression<Func<ModelKit, TDto>> selector,
    CancellationToken ct) =>
    await db.ModelKits
        .Where(filter.ToExpression())
        .Select(selector)
        .ToListAsync(ct);

// Handler — in *Mapping.cs
// L'handler inietta il Service (regola 12) — non il provider, non le Projection
private static async Task<IResult> GetHandler(
    [AsParameters] ModelKitFilter filter,
    ModelKitService service,
    CancellationToken ct)
{
    var result = await service.GetAllAsync(filter, ct);
    if (!result.Any())
        return TypedResults.Problem(new ProblemDetails
        {
            Title = "Data Not Found",
            Status = StatusCodes.Status404NotFound,
            Detail = "No data for specified filters."
        });

    return TypedResults.Ok(result);
}

// Handler con DTO parziale — stesso service, metodo diverso
private static async Task<IResult> GetSummaryHandler(
    [AsParameters] ModelKitFilter filter,
    ModelKitService service,
    CancellationToken ct)
{
    var result = await service.GetSummariesAsync(filter, ct);
    // ...
    return TypedResults.Ok(result);
}
```

> **Regola critica — EF traducibilità:** `Projection` deve usare esclusivamente new-initializer con accesso a membri primitivi (`e.Id`, `e.Nome`, ecc.). Chiamate a metodi extension (es. `e.ToDto()`) NON sono EF-traducibili e causano valutazione client-side silente (full table scan in memoria).

### Ottimizzazione EF (obbligatoria)

Il pattern Filter + Projection esiste per generare SQL minimo. Verifica sempre:

- **SELECT ridotta**: la `Projection` determina le colonne — il SQL generato contiene solo i campi del DTO richiesto. `<Entity>SummaryDto.Projection` → SELECT delle sole colonne del Summary. Mai materializzare l'entity per poi mappare in memoria.
- **WHERE solo sui filtri valorizzati**: ogni predicato in `ToExpression()` segue il pattern `(Campo == null || e.Campo <op> Campo)` — EF parametrizza e il query optimizer scarta i rami con filtro null. Nessun `if` di composizione query nel provider.
- **Mai GetAll senza filtro**: il provider accetta sempre `<Entity>Filter`; filtro vuoto = tutti i predicati null = nessuna restrizione, ma la firma resta filtrata.
- **Tracking**: `UseQueryTrackingBehavior(NoTracking)` come default nel DbContext; le letture restano no-tracking; `Update`/`Delete` usano `.AsTracking()` esplicito — senza tracking `SaveChanges` non rileva le modifiche.

### Service layer (obbligatorio per CRUD di entità)

Il `Service` (`Services/<Entity>Service.cs`) si interpone tra endpoint e provider. L'handler conosce solo il Service — non il provider, non le Projection. Il Service sceglie la Projection per caso d'uso e centralizza il mapping request→entity ed entity→DTO.

```csharp
// Services/<Entity>Service.cs
/// <summary>Application service for ModelKits: handlers depend on this, never on the provider.</summary>
public class ModelKitService(ModelKitsProvider provider)
{
    /// <summary>Returns the ModelKits matching the filter, projected to the full DTO.</summary>
    public Task<List<ModelKitDto>> GetAllAsync(ModelKitFilter filter, CancellationToken cancellationToken) =>
        provider.GetModelKitAsync(filter, ModelKitDto.Projection, cancellationToken);

    /// <summary>Returns the ModelKits matching the filter, projected to the reduced DTO (optimized SELECT).</summary>
    public Task<List<ModelKitSummaryDto>> GetSummariesAsync(ModelKitFilter filter, CancellationToken cancellationToken) =>
        provider.GetModelKitAsync(filter, ModelKitSummaryDto.Projection, cancellationToken);

    /// <summary>Returns the ModelKit with the given Id, or null if not found.</summary>
    public Task<ModelKitDto?> GetByIdAsync(int id, CancellationToken cancellationToken) =>
        provider.GetModelKitByIdAsync(id, ModelKitDto.Projection, cancellationToken);

    /// <summary>Creates a new ModelKit from the validated request and returns the persisted DTO.</summary>
    public async Task<ModelKitDto> CreateAsync(ModelKitRequest request, CancellationToken cancellationToken)
    {
        var created = await provider.CreateModelKitAsync(ToEntity(request), cancellationToken);
        return new ModelKitDto(created.Id, created.Descrizione, created.Marca, created.DataRegistrazione);
    }

    /// <summary>Updates the ModelKit with the given Id. False if it does not exist.</summary>
    public Task<bool> UpdateAsync(int id, ModelKitRequest request, CancellationToken cancellationToken)
    {
        var entity = ToEntity(request);
        entity.Id = id;
        return provider.UpdateModelKitAsync(entity, cancellationToken);
    }

    /// <summary>Deletes the ModelKit with the given Id. False if it does not exist.</summary>
    public Task<bool> DeleteAsync(int id, CancellationToken cancellationToken) =>
        provider.DeleteModelKitAsync(id, cancellationToken);

    /// <summary>Maps a validated request to the entity (request fields are guaranteed by the validator).</summary>
    private static ModelKit ToEntity(ModelKitRequest request) => new()
    {
        Descrizione = request.Descrizione!,
        Marca = request.Marca,
        DataRegistrazione = request.DataRegistrazione!.Value
    };
}

// Handler — inietta Service, non Provider
/// <summary>Returns the ModelKits matching the optional filters; 404 ProblemDetails when none.</summary>
private static async Task<IResult> GetListHandler(
    [AsParameters] ModelKitFilter filter,
    ModelKitService service,
    CancellationToken cancellationToken)
{
    var result = await service.GetAllAsync(filter, cancellationToken);
    // ...
}

// Program.cs — registra entrambi
builder.Services.AddScoped<ModelKitsProvider>();   // o tramite Add<Provider>Provider(configuration)
builder.Services.AddScoped<ModelKitService>();
```

> **Implementazione di riferimento:** `src/test-guideline.api/` — `Infrastructure/ModelKits/` (entity, DbContext, filter, provider), `Services/ModelKitService.cs`, `Dto/ModelKitDto.cs`, `Endpoints/ModelKitsMapping.cs`.

---

### VS Code debug (.vscode/launch.json + tasks.json)

**launch.json** — `"type": "coreclr"` (portabile, senza C# Dev Kit):
```json
{
  "version": "0.2.0",
  "configurations": [
    {
      "name": "Debug <project> (http)",
      "type": "coreclr",
      "request": "launch",
      "preLaunchTask": "build",
      "program": "${workspaceFolder}/src/<project>/bin/Debug/net10.0/<project>.dll",
      "args": [],
      "cwd": "${workspaceFolder}/src/<project>",
      "stopAtEntry": false,
      "env": {
        "ASPNETCORE_ENVIRONMENT": "Development",
        "ASPNETCORE_URLS": "http://localhost:5000"
      }
    },
    {
      "name": "Debug <project> (https)",
      "type": "coreclr",
      "request": "launch",
      "preLaunchTask": "build",
      "program": "${workspaceFolder}/src/<project>/bin/Debug/net10.0/<project>.dll",
      "args": [],
      "cwd": "${workspaceFolder}/src/<project>",
      "stopAtEntry": false,
      "env": {
        "ASPNETCORE_ENVIRONMENT": "Development",
        "ASPNETCORE_URLS": "https://localhost:5001;http://localhost:5000"
      }
    }
  ]
}
```

> **Nota:** Il percorso di `program` dipende da `OutputPath`: default standard .NET = `bin/Debug/<tfm>/`. Se `Directory.Build.props` o il csproj sovrascrivono `<OutputPath>`, leggi il valore reale e calcola il percorso di conseguenza. Con Claude Code la generazione guidata di questi file è disponibile via skill `dr-CreateLaunchProfiles` (stesso percorso di default).

**tasks.json**:
```json
{
  "version": "2.0.0",
  "tasks": [
    {
      "label": "build",
      "command": "dotnet",
      "type": "process",
      "args": [
        "build",
        "${workspaceFolder}/src/<project>/<project>.csproj",
        "/property:GenerateFullPaths=true",
        "/consoleloggerparameters:NoSummary;ForceNoAlign"
      ],
      "problemMatcher": "$msCompile",
      "group": "build"
    }
  ]
}
```

## Errori comuni (rapidi)
- Model binding: path/query parameter non convertibile al tipo atteso (es. stringa su campo `int`) ritorna HTTP 400 automaticamente — nessuna validazione custom richiesta per il tipo
- Version reader non UrlSegmentApiVersionReader => errore su MapToApiVersion
- Route senza api/v{version:apiVersion}/... => 404 o no route match
- Mancata MapToApiVersion => versione richiesta ma non specificata
- Scalar senza ApiExplorer config => nessun endpoint
- Provider non registrato in DI => Cannot resolve service
- Date query non ISO 8601 => DateTime conversion error
- `ApiVersionSet` CS0246 => manca `using Asp.Versioning.Builder` (Asp.Versioning v10.0.0+)
- `Microsoft.OpenApi.Models` CS0234 => usare `using Microsoft.OpenApi` (Microsoft.OpenApi v2.0.0+)

## ✅ Checklist Post-Generazione
- [ ] Endpoint solo in extension methods in Endpoints/*Mapping.cs
- [ ] Route group usa WithTags + WithApiVersionSet + MapToApiVersion
- [ ] URL formato api/v{version:apiVersion}/{gruppo}/{comando?}
- [ ] Versioning configurato con UrlSegmentApiVersionReader + ApiExplorer
- [ ] Metadata OpenAPI completi per ogni endpoint: `WithSummary`, `WithDescription`, `WithTags`, `WithName(<Verbo><Risorsa>)`, tutti i `TypedResults` in `Produces`
- [ ] Transformer AddDocumentInformations creato e registrato
- [ ] Program.cs chiama MapOpenApi prima dei Map*Endpoints
- [ ] GET list: `<Entity>Filter.cs` in `Infrastructure/Provider/Filters/` con `ToExpression()`, ogni DTO ha `static Projection`, provider usa `Get<Entity>Async<TDto>(filter, selector, ct)`
- [ ] DTO multipli: `<Entity>Dto` completo + `<Entity>SummaryDto` ridotto, endpoint `GET /` e `GET /summary`, ciascuno nel proprio file (`Dto/<Entity>Dto.cs` + `Dto/<Entity>SummaryDto.cs`)
- [ ] Service layer: `Services/<Entity>Service.cs` creato e registrato; handler iniettano solo il Service
- [ ] EF: SELECT con sole colonne del DTO (Projection), WHERE con soli filtri valorizzati (ToExpression), `AsTracking()` su Update/Delete
- [ ] Commenti: `///` su provider/service/handler/validator + inline su operazioni DB (code-organization Regola 6)
- [ ] POST/PUT/PATCH: validator creato in `Validators/`, registrato in DI, chiamato nel handler prima della logica
- [ ] HealthMapping.cs creato con `/health` (MapHealthChecks) e `GET /api/v1/status` (versioned, in Scalar)
- [ ] File .http aggiunto per endpoint nuovi
- [ ] `.vscode/launch.json` e `tasks.json` creati con `type: coreclr`
- [ ] `appsettings.json` contiene solo valori fake/placeholder per dati sensibili, mai credenziali reali
- [ ] Se Serilog confermato: configurazione conforme a `logging.instructions.md` (fonte unica), `logs/` in .gitignore

## 🎯 Criteri di successo (verificare prima di iniziare)

Prima di iniziare, chiediti:
- [ ] So esattamente quali file creerò/modificherò?
- [ ] Ho letto le istruzioni modulari pertinenti al task?
- [ ] Ho verificato che l'endpoint o il componente non esista già?

Se una risposta è NO → chiedi chiarimenti all'utente prima di procedere.

## Test
- Aggiungi sempre un file .http per endpoint nuovi

*Template v2.3 - .NET 10 - Token-optimized for AI agents* - Last Update 2026-08-12 — claude-opus-5

