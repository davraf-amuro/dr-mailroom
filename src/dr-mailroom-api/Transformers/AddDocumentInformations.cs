using Microsoft.AspNetCore.OpenApi;
using Microsoft.OpenApi;

namespace dr_mailroom_api.Transformers;

/// <summary>Informazioni del documento OpenAPI consumato da Scalar e dal client del frontend.</summary>
public class AddDocumentInformations : IOpenApiDocumentTransformer
{
    /// <summary>Titolo, descrizione e versione del documento.</summary>
    public Task TransformAsync(OpenApiDocument document, OpenApiDocumentTransformerContext context, CancellationToken cancellationToken)
    {
        document.Info.Title = "dr-mailroom API";
        document.Info.Description = "Lettura in sola lettura di caselle Gmail: caselle, filtri, giri, statistiche per mittente e allerte.";
        document.Info.Version = "v1";
        document.Info.Contact = new OpenApiContact { Name = "davraf" };
        return Task.CompletedTask;
    }
}
