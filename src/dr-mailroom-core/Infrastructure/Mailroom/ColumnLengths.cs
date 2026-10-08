namespace dr_mailroom_core.Infrastructure.Mailroom;

/// <summary>Lunghezze massime delle colonne testuali: fonte unica per entità, configurazioni EF e validatori.</summary>
public static class ColumnLengths
{
    /// <summary>Lunghezza massima di un indirizzo email (RFC 5321).</summary>
    public const int EmailAddress = 320;
    public const int DisplayName = 320;
    public const int GmailId = 64;
    public const int PageToken = 500;
    public const int ErrorText = 2000;
    public const int Subject = 1000;
    public const int Labels = 1000;
    public const int FilterName = 100;
    public const int SubjectValue = 500;
    public const int Subfolder = 200;
    public const int EnumText = 30;
    public const int RequestedProcesses = 200;
    public const int PartId = 50;
    public const int FileName = 260;
    public const int RelativePath = 500;
    public const int Sha256Hex = 64;
}
