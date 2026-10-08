using System.Linq.Expressions;
using dr_mailroom_core.Infrastructure.Mailroom.Entities;

namespace dr_mailroom_api.Dto;

/// <summary>Allegato salvato su disco; RelativePath è relativo alla cartella radice configurata.</summary>
public record AttachmentDto(long Id, string PartId, string OriginalFileName, string RelativePath, long SizeBytes, string Sha256, DateTime SavedAtUtc)
{
    /// <summary>Proiezione EF-traducibile: new-initializer con accesso diretto ai membri.</summary>
    public static Expression<Func<ExtractedAttachment, AttachmentDto>> Projection =>
        e => new(e.Id, e.PartId, e.OriginalFileName, e.RelativePath, e.SizeBytes, e.Sha256, e.SavedAtUtc);
}
