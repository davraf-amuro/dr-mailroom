using Asp.Versioning.Builder;
using dr_mailroom_api.Dto;
using dr_mailroom_api.Properties;
using dr_mailroom_api.Services;
using dr_mailroom_api.Validators;
using Microsoft.AspNetCore.Mvc;

namespace dr_mailroom_api.Endpoints;

/// <summary>Endpoint dei messaggi indicizzati: allegati salvati collegati alla mail.</summary>
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

        group.MapGet("/{id:long}/attachments", GetAttachmentsHandlerAsync)
            .Produces<List<AttachmentDto>>(StatusCodes.Status200OK)
            .Produces<ProblemDetails>(StatusCodes.Status404NotFound)
            .WithName("GetMessageAttachmentList")
            .WithSummary("Allegati salvati di una mail")
            .WithDescription("Allegati estratti dalla mail con percorso relativo e SHA-256. 404 se la mail non esiste o non ha allegati salvati.");

        return routes;
    }

    /// <summary>Allegati del messaggio o 404.</summary>
    private static async Task<IResult> GetAttachmentsHandlerAsync(long id, MessageService service, CancellationToken cancellationToken)
    {
        var result = await service.GetAttachmentsAsync(id, cancellationToken);
        return result.Outcome == ServiceOutcome.Ok ? EndpointResults.ListOrNoData(result.Value!) : EndpointResults.Failure(result);
    }
}
