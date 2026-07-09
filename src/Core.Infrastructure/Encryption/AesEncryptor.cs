using System.Security.Cryptography;
using System.Text;
using Microsoft.Extensions.Options;

namespace Core.Infrastructure;

public class AesEncryptor : IEncryptor
{
    private readonly byte[] _key;

    public AesEncryptor(IOptions<AesEncryptorOptions> options)
    {
        _key = Convert.FromBase64String(options.Value.Key);
    }

    public string Encrypt(string plainText)
    {
        using var aes = Aes.Create();
        aes.Key = _key;
        aes.IV = DeriveIv(plainText);

        using var encryptor = aes.CreateEncryptor(aes.Key, aes.IV);
        var plainBytes = Encoding.UTF8.GetBytes(plainText);
        var encrypted = encryptor.TransformFinalBlock(plainBytes, 0, plainBytes.Length);

        var result = aes.IV.Concat(encrypted).ToArray();
        return Convert.ToBase64String(result);
    }

    public string Decrypt(string cipherText)
    {
        var data = Convert.FromBase64String(cipherText);
        var iv = data[..16]; // first 16 bytes
        var cipher = data[16..]; // rest of bytes

        using var aes = Aes.Create();
        aes.Key = _key;
        aes.IV = iv;

        using var decryptor = aes.CreateDecryptor(aes.Key, aes.IV);
        var decrypted = decryptor.TransformFinalBlock(cipher, 0, cipher.Length);

        return Encoding.UTF8.GetString(decrypted);
    }

    private byte[] DeriveIv(string plainText)
    {
        return SHA256.HashData(Encoding.UTF8.GetBytes(plainText))[..16];
    }
}