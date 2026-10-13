using Asp.Versioning.Builder;
using dr_mailroom_api.Dto;
using dr_mailroom_api.Properties;
using dr_mailroom_api.Services;
using dr_mailroom_api.Validators;
using dr_mailroom_core.Infrastructure.Mailroom.Filters;
using Microsoft.AspNetCore.Mvc;

namespace dr_mailroom_api.Endpoints;

/// <summary>Endpoint dei messaggi indicizzati: griglia delle mail di un mittente e allegati salvati collegati alla mail.</summary>
public static class MessagesMapping
{
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
            .WithDescription("Griglia delle mail di un mittente (mailboxId e senderAddress obbligatori, mittente confrontato per uguaglianza): oggetto, data di ricezione, label Gmail separate da virgola, presenza di allegati. Paginata (pageSize 1–100, default 25), ordinata per data (sortDir Asc|Desc, default Desc), ricerca facoltativa nell'oggetto. 404 se la casella non esiste; nessuna mail → 200 con totalCount 0.");

        group.MapPost("/trash-sender", TrashSenderHandlerAsync)
            .Produces<TrashSenderStartedDto>(StatusCodes.Status202Accepted)
            .Produces<HttpValidationProblemDetails>(StatusCodes.Status400BadRequest)
            .Produces<ProblemDetails>(StatusCodes.Status404NotFound)
            .Produces<ProblemDetails>(StatusCodes.Status409Conflict)
            .WithName("PostTrashSender")
            .WithSummary("Sposta nel cestino tutte le mail di un mittente")
            .WithDescription("\"Elimina tutte\": avvia un giro (trigger TrashSender) che sposta nel cestino di Gmail le mail ricevute dal mittente nella casella e non ancora cestinate, a blocchi da 1.000 (batchModify con label TRASH). Usa il token del cestino (scope gmail.modify, D-T1 bis): la lettura resta in sola lettura. Le mail restano recuperabili da Gmail per 30 giorni; in dr-mailroom spariscono da elenchi e statistiche e il DB le elimina dopo la ritenzione configurata. 404 se la casella non esiste; 409 se non è attiva, il cestino non è autorizzato (GET /mailboxes/{id}/trash-authorization), non c'è nulla da cestinare o la casella ha già un giro in corso. L'avanzamento si segue con GET /runs/{runId}.");

        group.MapGet("/{id:long}/attachments", GetAttachmentsHandlerAsync)
            .Produces<List<AttachmentDto>>(StatusCodes.Status200OK)
            .Produces<ProblemDetails>(StatusCodes.Status404NotFound)
            .WithName("GetMessageAttachmentList")
            .WithSummary("Allegati salvati di una mail")
            .WithDescription("Allegati estratti dalla mail con percorso relativo e SHA-256. 404 se la mail non esiste o non ha allegati salvati.");

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

    /// <summary>Allegati del messaggio o 404.</summary>
    private static async Task<IResult> GetAttachmentsHandlerAsync(long id, MessageService service, CancellationToken cancellationToken)
    {
        var result = await service.GetAttachmentsAsync(id, cancellationToken);
        return result.Outcome == ServiceOutcome.Ok ? EndpointResults.ListOrNoData(result.Value!) : EndpointResults.Failure(result);
    }
}
