namespace dr_mailroom_core.Enums;

/// <summary>Processo di lavorazione applicabile a una mail filtrata.</summary>
public enum ProcessType
{
    AttachmentExtraction,
    Alert,

    /// <summary>Salva su disco il contenuto originale della mail (.eml) per scaricarlo o leggerlo in dr-mailroom.</summary>
    ContentArchive,
}
