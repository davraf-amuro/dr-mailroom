namespace dr_mailroom_api.Dto;

/// <summary>Esito dell'anteprima: totale delle corrispondenze e le più recenti.</summary>
public record FilterPreviewDto(int TotalMatches, IReadOnlyList<MessageSummaryDto> Messages);
