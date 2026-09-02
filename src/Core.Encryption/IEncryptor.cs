namespace Core.Encryption;

public interface IEncryptor
{
    string Encrypt(string text);
    string Decrypt(string text);
}