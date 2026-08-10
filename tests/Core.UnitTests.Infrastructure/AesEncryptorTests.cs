using System.Security.Cryptography;
using Core.Infrastructure;
using FluentAssertions;
using Microsoft.Extensions.Options;
using Xunit;

namespace Core.UnitTests.Infrastructure;

public class AesEncryptorTests
{
    private readonly AesEncryptor _encryptor;

    public AesEncryptorTests()
    {
        var key = Convert.ToBase64String(RandomNumberGenerator.GetBytes(32));
        var options = Options.Create(new AesEncryptorOptions { Key = key });
        _encryptor = new AesEncryptor(options);
    }

    [Fact]
    public void EncryptAndDecrypt_ShouldReturnOriginalText()
    {
        // Arrange
        var plainText = "Test";
        var encrypted = _encryptor.Encrypt(plainText);

        // Act
        var decrypted = _encryptor.Decrypt(encrypted);

        // Assert
        decrypted.Should().Be(plainText);
    }

    [Fact]
    public void Encrypt_SamePlainText_ShouldProduceSameEncryptedText()
    {
        // Arrange
        var plainText = "Test";

        // Act
        var encrypted1 = _encryptor.Encrypt(plainText);
        var encrypted2 = _encryptor.Encrypt(plainText);

        // Assert
        encrypted1.Should().Be(encrypted2);
    }

    [Fact]
    public void Decrypt_WithInvalidEncryptedText_ShouldThrow()
    {
        // Arrange
        var invalidEncryptedText = "test";

        // Act
        var act = () => _encryptor.Decrypt(invalidEncryptedText);

        // Assert
        act.Should().Throw<Exception>();
    }
}
