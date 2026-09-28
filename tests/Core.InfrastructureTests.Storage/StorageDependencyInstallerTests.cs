using Core.IntegrationTests.Shared.Infrastructure;
using Core.Storage;
using FluentAssertions;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;
using Xunit;

namespace Core.InfrastructureTests.Storage;

public class StorageDependencyInstallerTests
{
    private readonly Dictionary<string, string?> _s3Settings = new()
    {
        ["S3:ServiceUrl"] = "http://localhost:3900",
        ["S3:Region"] = "garage",
        ["S3:AccessKey"] = "access-key",
        ["S3:SecretKey"] = "secret-key",
        ["S3:Bucket"] = "bucket"
    };

    [Fact]
    public void AddStorage_WithoutConfiguredContentTypes_ShouldAllowDefaultTypes()
    {
        // Arrange
        var configuration = new ConfigurationBuilder().AddInMemoryCollection(_s3Settings).Build();
        using var provider = new ServiceCollection()
            .AddStorage<TestDbContext>(configuration)
            .BuildServiceProvider();

        // Act
        var options = provider.GetRequiredService<IOptions<StoredFileOptions>>().Value;

        // Assert
        options.AllowedContentTypes.Should().BeEquivalentTo("image/jpeg", "image/png", "image/webp", "application/pdf");
    }

    [Fact]
    public void AddStorage_WithConfiguredContentTypes_ShouldReplaceDefaultTypes()
    {
        // Arrange
        _s3Settings["StoredFiles:AllowedContentTypes:0"] = "text/csv";
        var configuration = new ConfigurationBuilder().AddInMemoryCollection(_s3Settings).Build();
        using var provider = new ServiceCollection()
            .AddStorage<TestDbContext>(configuration)
            .BuildServiceProvider();

        // Act
        var options = provider.GetRequiredService<IOptions<StoredFileOptions>>().Value;

        // Assert
        options.AllowedContentTypes.Should().BeEquivalentTo("text/csv");
    }

    [Fact]
    public void AddStorage_WithMarkUploadedCronRunningLessOftenThanPendingExpiration_ShouldFailValidation()
    {
        // Arrange
        _s3Settings["StoredFiles:MarkUploadedCron"] = "0 3 * * *";
        _s3Settings["StoredFiles:PendingExpiration"] = "12:00:00";
        var configuration = new ConfigurationBuilder().AddInMemoryCollection(_s3Settings).Build();
        using var provider = new ServiceCollection()
            .AddStorage<TestDbContext>(configuration)
            .BuildServiceProvider();

        // Act
        var resolve = () => provider.GetRequiredService<IOptions<StoredFileOptions>>().Value;

        // Assert
        resolve.Should().Throw<OptionsValidationException>()
            .WithMessage("*StoredFiles:MarkUploadedCron must run more often*");
    }
}