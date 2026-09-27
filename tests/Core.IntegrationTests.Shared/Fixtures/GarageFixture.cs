using Core.IntegrationTests.Shared.Settings;
using Core.Storage;
using DotNet.Testcontainers.Builders;
using DotNet.Testcontainers.Containers;
using Xunit;

namespace Core.IntegrationTests.Shared.Fixtures;

public class GarageFixture : IAsyncLifetime
{
    private const string KeyName = "test-key";
    private const string AccessKey = "GK0123456789abcdef01234567";
    private const string SecretKey = "0123456789abcdef0123456789abcdef0123456789abcdef0123456789abcdef";
    public const string Bucket = "test-bucket";

    private readonly IContainer _container;

    public GarageFixture()
    {
        var configPath = Path.Combine(AppContext.BaseDirectory, "TestData", "garage.toml");

        if (!File.Exists(configPath))
            throw new FileNotFoundException($"Garage config file not found at: {configPath}");

        // the image has no shell, so readiness is probed over HTTP; any S3 response means it is up
        _container = new ContainerBuilder()
            .WithImage(ContainerImages.Garage)
            .WithPortBinding(3900, true)
            .WithResourceMapping(configPath, "/etc/")
            .WithWaitStrategy(Wait.ForUnixContainer()
                .UntilHttpRequestIsSucceeded(r => r
                    .ForPort(3900)
                    .ForStatusCodeMatching(_ => true)))
            .Build();
    }

    public async Task InitializeAsync()
    {
        await _container.StartAsync();

        var nodeId = (await Garage("node", "id", "-q")).Split('@')[0];

        await Garage("layout", "assign", "-z", "dc1", "-c", "1G", nodeId);
        await Garage("layout", "apply", "--version", "1");
        await Garage("key", "import", "--yes", "-n", KeyName, AccessKey, SecretKey);
        await Garage("bucket", "create", Bucket);
        await Garage("bucket", "allow", "--read", "--write", "--owner", Bucket, "--key", KeyName);
    }

    public async Task DisposeAsync() => await _container.DisposeAsync();

    public S3Options CreateS3Options() => new()
    {
        ServiceUrl = $"http://{_container.Hostname}:{_container.GetMappedPublicPort(3900)}",
        Region = "garage",
        AccessKey = AccessKey,
        SecretKey = SecretKey,
        Bucket = Bucket
    };

    private async Task<string> Garage(params string[] args)
    {
        var result = await _container.ExecAsync(["/garage", .. args]);

        if (result.ExitCode != 0)
            throw new InvalidOperationException($"garage {string.Join(' ', args)} failed: {result.Stderr}");

        return result.Stdout.Trim();
    }
}