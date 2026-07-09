namespace Core.Infrastructure;

public class AesEncryptorOptions
{
    public const string SectionName = "Encryption";

    public string Key { get; set; }
}