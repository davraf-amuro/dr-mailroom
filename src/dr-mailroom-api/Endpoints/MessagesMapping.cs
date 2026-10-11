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

    /// <summary>Allegati del messaggio o 404.</summary>
    private static async Task<IResult> GetAttachmentsHandlerAsync(long id, MessageService service, CancellationToken cancellationToken)
    {
        var result = await service.GetAttachmentsAsync(id, cancellationToken);
        return result.Outcome == ServiceOutcome.Ok ? EndpointResults.ListOrNoData(result.Value!) : EndpointResults.Failure(result);
    }
}
