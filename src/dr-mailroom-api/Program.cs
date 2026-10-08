using System.Text.Json.Serialization;
using Asp.Versioning;
using dr_mailroom_api.Endpoints;
using dr_mailroom_api.Infrastructure.Database;
using dr_mailroom_api.Infrastructure.Logging;
using dr_mailroom_api.Properties;
using dr_mailroom_api.Scheduling;
using dr_mailroom_api.Services;
using dr_mailroom_api.Transformers;
using dr_mailroom_core;
using dr_mailroom_core.Configuration;
using dr_mailroom_core.Gmail;
using dr_mailroom_core.Infrastructure.Mailroom;
using dr_mailroom_core.Runs;
using dr_mailroom_core.Security;
using Scalar.AspNetCore;
using Serilog;
using Serilog.Events;
using SimpleAuthentication;
using TinyHelpers.AspNetCore.Extensions;

var builder = WebApplication.CreateBuilder(args);

// Segreti locali (connection string, client secret, chiave API): solo in sviluppo; in container arrivano dall'ambiente
if (builder.Environment.IsDevelopment())
{
    builder.Configuration.AddJsonFile("appsettings.local.json", optional: true, reloadOnChange: true);
}

builder.Services.AddSerilog((_, lc) => lc
    .MinimumLevel.Information()
    .MinimumLevel.Override("Microsoft.EntityFrameworkCore", LogEventLevel.Warning)
    .MinimumLevel.Override("Microsoft.AspNetCore", LogEventLevel.Warning)
    .Enrich.WithThreadId()
    .Enrich.With<TaskIdEnricher>()
    .WriteTo.Console(outputTemplate:
        "{Timestamp:HH:mm:ss} [{Level:u3}] T:{ThreadId} A:{TaskId} {Message:lj}{NewLine}{Exception}")
    .WriteTo.File(
        path: "logs/app-.log",
        rollingInterval: RollingInterval.Day,
        outputTemplate:
        "{Timestamp:yyyy-MM-dd HH:mm:ss} [{Level:u3}] T:{ThreadId} A:{TaskId} {Message:lj}{NewLine}{Exception}"));

// Opzioni tipizzate "Mailroom:*" validate all'avvio: un valore obbligatorio mancante ferma l'host
builder.Services.AddMailroomOptions(builder.Configuration);

// Chiave API obbligatoria: senza, l'API sarebbe aperta o inutilizzabile (C8)
if (string.IsNullOrWhiteSpace(builder.Configuration["Authentication:ApiKey:ApiKeyValue"]))
{
    throw new InvalidOperationException("Authentication:ApiKey:ApiKeyValue è obbligatorio (appsettings.local.json o variabile d'ambiente)");
}

// Database dr-mailroom: factory del DbContext (singleton e job) + DbContext scoped
builder.Services.AddMailroomProvider(builder.Configuration);

// Cifratura dei refresh token Gmail: key ring su file, fuori dal database
builder.Services.AddMailroomTokenProtection(builder.Configuration);

// Client Gmail in sola lettura (scope gmail.readonly) e flusso OAuth di collegamento
builder.Services.AddMailroomGmail();

// Logica di dominio (provider, ingestione, filtri, processi, giri) e scheduler Quartz
builder.Services.AddMailroomCore();
builder.Services.AddMailroomScheduling(builder.Configuration);

// API: servizi applicativi e validatori, JSON con enum testuali, ProblemDetails, chiave API, CORS
builder.Services.AddMailroomApiServices();
builder.Services.ConfigureHttpJsonOptions(options => options.SerializerOptions.Converters.Add(new JsonStringEnumConverter()));
builder.Services.AddExceptionHandler<DatabaseExceptionHandler>();
builder.Services.AddDefaultProblemDetails();
builder.Services.AddDefaultExceptionHandler();
builder.Services.AddSimpleAuthentication(builder.Configuration);
builder.Services.AddAuthorization();
builder.Services.AddSingleton<DatabaseStartupService>();
builder.Services.AddHealthChecks();

var frontendBaseUrl = builder.Configuration[$"{FrontendOptions.SectionName}:{nameof(FrontendOptions.BaseUrl)}"] ?? string.Empty;
builder.Services.AddCors(options => options.AddDefaultPolicy(policy => policy
    .WithOrigins(frontendBaseUrl.TrimEnd('/'))
    .AllowAnyHeader()
    .AllowAnyMethod()));

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
    options.AddSimpleAuthentication(builder.Configuration);
});

var app = builder.Build();

app.UseExceptionHandler();
app.UseStatusCodePages();
app.UseCors();
app.UseAuthentication();
app.UseAuthorization();

var versionSet = app.NewApiVersionSet()
    .HasApiVersion(ApiVersionFactory.Version1)
    .Build();

app.MapOpenApi();
app.MapScalarApiReference();
app.MapHealthEndpoints(versionSet);
app.MapMailboxesEndpoints(versionSet);
app.MapFiltersEndpoints(versionSet);
app.MapRunsEndpoints(versionSet);
app.MapStatsEndpoints(versionSet);
app.MapAlertsEndpoints(versionSet);
app.MapMessagesEndpoints(versionSet);

// Arresto: annullo i giri in corso prima che Quartz li attenda, così si chiudono Cancelled invece di essere uccisi
var cancellationRegistry = app.Services.GetRequiredService<RunCancellationRegistry>();
app.Lifetime.ApplicationStopping.Register(cancellationRegistry.CancelAll);

// Inizializzazione resiliente: DB giù → l'API parte in stato degradato (status e retry restano raggiungibili)
var databaseStartup = app.Services.GetRequiredService<DatabaseStartupService>();
if (!await databaseStartup.TryInitializeAsync(CancellationToken.None))
{
    app.Logger.LogWarning("Database non raggiungibile all'avvio: API in stato degradato. Stato su /api/v1/status, retry su /api/v1/status/retry-database");
}

await app.RunAsync();

/// <summary>Esposto ai test di integrazione (WebApplicationFactory).</summary>
public partial class Program;
