using System.ComponentModel.DataAnnotations;

namespace dr_mailroom_core.Configuration;

/// <summary>Indirizzo del frontend: origine CORS ammessa e destinazione del redirect post-OAuth.</summary>
public sealed class FrontendOptions
{
    public const string SectionName = "Mailroom:Frontend";

    /// <summary>URL base della SPA dr-mailroom-fe, es. http://localhost:5173.</summary>
    [Required, Url]
    public string BaseUrl { get; init; } = string.Empty;
}
