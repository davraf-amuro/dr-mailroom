using Asp.Versioning.Builder;
using dr_mailroom_api.Dto;
using dr_mailroom_api.Properties;
using dr_mailroom_api.Services;
using dr_mailroom_api.Validators;
using dr_mailroom_core.Infrastructure.Mailroom.Filters;
using Microsoft.AspNetCore.Mvc;

namespace dr_mailroom_api.Endpoints;

/// <summary>Endpoint dei messaggi indicizzati: griglia delle mail di un mittente, cestino, allegati salvati e contenuto archiviato.</summary>
public static class MessagesMapping
{
    /// <summary>Valore di X-Content-Type-Options: il browser non deduce il tipo dal contenuto.</summary>
    public const string NoSniff = "nosniff";

    /// <summary>Gruppo api/v1/messages.</summary>
    public static IEndpointRouteBuilder MapMessagesEndpoints(this IEndpointRouteBuilder routes, ApiVersionSet versionSet)
    {
        var group = routes.MapGroup("api/v{version:apiVersion}/messages")
            .WithTags("Messages")
            .WithApiVersionSet(versionSet)
            .MapToApiVersion(ApiVersionFactory.Version1)
            .RequireAuthorization();

        group.MapGet("/", GetSenderMessagesHandlerAsync)
            .Produces<SenderMessagePageDto>(StatusCodes.Status200OK)
            .Produces<HttpValidationProblemDetails>(StatusCodes.Status400BadRequest)
            .Produces<ProblemDetails>(StatusCodes.Status404NotFound)
            .WithName("GetSenderMessageList")
            .WithSummary("Mail di un mittente in una casella")
            .WithDescription("Griglia delle mail di un mittente (mailboxId e senderAddress obbligatori, mittente confrontato per uguaglianza): oggetto, data di ricezione, label Gmail separate da virgola, presenza di allegati. Paginata (pageSize 1–100, default 25), ordinata per data (sortDir Asc|Desc, default Desc), ricerca facoltativa nell'oggetto, receivedBeforeUtc facoltativo (solo le mail ricevute prima di quell'istante, estremo escluso: il conteggio di \"Elimina tutte\" fino a una data). 404 se la casella non esiste; nessuna mail → 200 con totalCount 0.");

        group.MapPost("/trash-sender", TrashSenderHandlerAsync)
            .Produces<TrashSenderStartedDto>(StatusCodes.Status202Accepted)
            .Produces<HttpValidationProblemDetails>(StatusCodes.Status400BadRequest)
            .Produces<ProblemDetails>(StatusCodes.Status404NotFound)
            .Produces<ProblemDetails>(StatusCodes.Status409Conflict)
            .WithName("PostTrashSender")
            .WithSummary("Sposta nel cestino tutte le mail di un mittente")
            .WithDescription("\"Elimina tutte\": avvia un giro (trigger TrashSender) che sposta nel cestino di Gmail le mail ricevute dal mittente nella casella e non ancora cestinate (con receivedBeforeUtc facoltativo solo quelle ricevute prima di quell'istante, estremo escluso: per \"fino al giorno X compreso\" il client invia l'inizio del giorno dopo), a blocchi da 1.000 (batchModify con label TRASH). Usa il token del cestino (scope gmail.modify, D-T1 bis): la lettura resta in sola lettura. Le mail restano recuperabili da Gmail per 30 giorni; in dr-mailroom spariscono da elenchi e statistiche e il DB le elimina dopo la ritenzione configurata. 404 se la casella non esiste; 409 se non è attiva, il cestino non è autorizzato (GET /mailboxes/{id}/trash-authorization), non c'è nulla da cestinare o la casella ha già un giro in corso. L'avanzamento si segue con GET /runs/{runId}.");

        group.MapGet("/{id:long}/attachments", GetAttachmentsHandlerAsync)
            .Produces<List<AttachmentDto>>(StatusCodes.Status200OK)
            .Produces<ProblemDetails>(StatusCodes.Status404NotFound)
            .WithName("GetMessageAttachmentList")
            .WithSummary("Allegati salvati di una mail")
            .WithDescription("Allegati estratti dalla mail con percorso relativo e SHA-256. 404 se la mail non esiste o non ha allegati salvati.");

        group.MapGet("/{id:long}/content", GetContentHandlerAsync)
            .Produces(StatusCodes.Status200OK, contentType: MessageService.EmlContentType)
            .Produces(StatusCodes.Status200OK, contentType: MessageService.TxtContentType)
            .Produces<HttpValidationProblemDetails>(StatusCodes.Status400BadRequest)
            .Produces<ProblemDetails>(StatusCodes.Status404NotFound)
            .Produces<ProblemDetails>(StatusCodes.Status409Conflict)
            .WithName("GetMessageContent")
            .WithSummary("Contenuto archiviato di una mail")
            .WithDescription("Scarica il contenuto salvato dal processo ContentArchive: format=eml restituisce il messaggio originale (application/octet-stream, in streaming dal disco), format=txt il testo leggibile ricavato dal .eml (text/plain UTF-8: intestazioni Da/Oggetto/Data, poi il corpo text/plain o l'HTML convertito). Sempre Content-Disposition attachment e X-Content-Type-Options nosniff. 400 se format manca o non è valido; 404 se la mail non esiste, è cestinata, non ha contenuto archiviato o il file non è più sul disco; 409 se il .eml non è leggibile.");

        return routes;
    }

    /// <summary>Pagina delle mail di un mittente: 400 su filtro non valido, 404 se la casella non esiste.</summary>
    private static async Task<IResult> GetSenderMessagesHandlerAsync(
        [AsParameters] SenderMessageFilter filter, IValidator<SenderMessageFilter> validator, MessageService service, CancellationToken cancellationToken)
    {
        var validation = validator.Validate(filter);
        if (!validation.IsValid)
        {
            return TypedResults.ValidationProblem(validation.Errors);
        }

        var result = await service.GetSenderMessagesAsync(filter, cancellationToken);
        return result.Outcome == ServiceOutcome.Ok ? TypedResults.Ok(result.Value) : EndpointResults.Failure(result);
    }

    /// <summary>"Elimina tutte": 400 su body non valido, poi gli esiti del servizio; 202 con l'Id del giro.</summary>
    private static async Task<IResult> TrashSenderHandlerAsync(
        TrashSenderRequest request, IValidator<TrashSenderRequest> validator, RunService service, CancellationToken cancellationToken)
    {
        var validation = validator.Validate(request);
        if (!validation.IsValid)
        {
            return TypedResults.ValidationProblem(validation.Errors);
        }

        var result = await service.StartTrashSenderAsync(request, cancellationToken);
        return result.Outcome == ServiceOutcome.Ok ? TypedResults.Accepted((string?)null, result.Value) : EndpointResults.Failure(result);
    }

    /// <summary>Download del contenuto: 400 sul formato, poi gli esiti del servizio; il browser non deve interpretare il file.</summary>
    private static async Task<IResult> GetContentHandlerAsync(
        long id, [AsParameters] MessageContentQuery query, IValidator<MessageContentQuery> validator, MessageService service,
        HttpContext httpContext, CancellationToken cancellationToken)
    {
        var validation = validator.Validate(query);
        if (!validation.IsValid)
        {
            return TypedResults.ValidationProblem(validation.Errors);
        }

        MessageContentQueryValidator.TryParse(query.Format!, out var format);
        var result = await service.GetContentAsync(id, format, cancellationToken);
        if (result.Outcome != ServiceOutcome.Ok)
        {
            return EndpointResults.Failure(result);
        }

        var file = result.Value!;
        httpContext.Response.Headers.XContentTypeOptions = NoSniff;
        return file.FilePath is not null
            ? TypedResults.PhysicalFile(file.FilePath, file.ContentType, file.FileName)
            : TypedResults.File(file.Content!, file.ContentType, file.FileName);
    }

    /// <summary>Allegati del messaggio o 404.</summary>
    private static async Task<IResult> GetAttachmentsHandlerAsync(long id, MessageService service, CancellationToken cancellationToken)
    {
        var result = await service.GetAttachmentsAsync(id, cancellationToken);
        return result.Outcome == ServiceOutcome.Ok ? EndpointResults.ListOrNoData(result.Value!) : EndpointResults.Failure(result);
    }
}
