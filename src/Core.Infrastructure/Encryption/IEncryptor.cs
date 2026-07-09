namespace Core.Infrastructure;

public interface IEncryptor
{
    string Encrypt(string text);
    string Decrypt(string text);
}