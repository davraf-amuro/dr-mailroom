namespace dr_mailroom_api.Dto;

/// <summary>Giri avviati e caselle saltate perché già occupate (D16, D17).</summary>
public record RunsStartedDto(IReadOnlyList<long> RunIds, IReadOnlyList<int> SkippedMailboxIds);
