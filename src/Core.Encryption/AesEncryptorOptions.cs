namespace Core.Encryption;

public class AesEncryptorOptions
{
    public const string SectionName = "Encryption";

    public required string Key { get; init; }
}