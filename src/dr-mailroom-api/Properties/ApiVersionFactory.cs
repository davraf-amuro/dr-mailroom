using Asp.Versioning;

namespace dr_mailroom_api.Properties;

/// <summary>Versioni dell'API esposte nell'URL (api/v{version}).</summary>
public static class ApiVersionFactory
{
    public static readonly ApiVersion Version1 = new(1, 0);
}
