namespace dr_mailroom_api.Dto;

/// <summary>Giri per cui è stato chiesto l'annullamento: si chiudono Cancelled appena il lavoro in corso si ferma.</summary>
public record RunsCancelledDto(IReadOnlyList<long> RunIds);
