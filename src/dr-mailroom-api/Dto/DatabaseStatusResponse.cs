namespace dr_mailroom_api.Dto;

/// <summary>Stato del database esposto da GET status (sempre 200).</summary>
public record DatabaseStatusResponse(bool DatabaseReady, string? LastError, DateTimeOffset? LastCheckedUtc);
