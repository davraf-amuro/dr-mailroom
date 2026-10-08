namespace dr_mailroom_api.Dto;

/// <summary>Body della riapplica: sottoinsieme dei processi del filtro; null = tutti (D13).</summary>
public record ReapplyRequest(List<string>? Processes);
