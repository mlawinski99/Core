using Core.Infrastructure;

namespace Core.IntegrationTests.Shared.Infrastructure;

public class TestEncryptor : IEncryptor
{
    public string Encrypt(string text) => $"encrypted{text}";
    public string Decrypt(string text) => text.Replace("encrypted", "");
}
