using Amazon.Runtime;
using Amazon.S3;
using Core.BackgroundJobs;
using Core.DateTimeProvider;
using Core.Extensions;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;

namespace Core.Storage;

public static class StorageDependencyInstaller
{
    private static readonly string[] DefaultAllowedContentTypes = ["image/jpeg", "image/png", "image/webp", "application/pdf"];
    private static readonly TimeSpan MaxUrlExpiration = TimeSpan.FromDays(7);

    public static IServiceCollection AddStorage<TContext>(
        this IServiceCollection services, IConfiguration configuration) where TContext : DbContext, IFileStore
    {
        var maxUrlExpiration = MaxUrlExpiration.TotalDays >= 1
            ? $"{MaxUrlExpiration.TotalDays} days"
            : MaxUrlExpiration.TotalHours >= 1
                ? $"{MaxUrlExpiration.TotalHours} hours"
                : $"{MaxUrlExpiration.TotalMinutes} minutes";

        var s3Section = configuration.GetSection(S3Options.SectionName);
        var storedFilesSection = configuration.GetSection(StoredFileOptions.SectionName);

        services.AddOptions<S3Options>()
            .Bind(s3Section)
            .Validate(o => Uri.TryCreate(o.ServiceUrl, UriKind.Absolute, out _), "S3:ServiceUrl must be an absolute URL")
            .Validate(o => !string.IsNullOrWhiteSpace(o.Region), "S3:Region must not be empty")
            .Validate(o => !string.IsNullOrWhiteSpace(o.AccessKey), "S3:AccessKey must not be empty")
            .Validate(o => !string.IsNullOrWhiteSpace(o.SecretKey), "S3:SecretKey must not be empty")
            .Validate(o => !string.IsNullOrWhiteSpace(o.Bucket), "S3:Bucket must not be empty")
            .Validate(_ => s3Section[nameof(S3Options.DefaultUrlExpiration)] is not { } value || value.IsTimeSpan(),
                "S3:DefaultUrlExpiration must be written as a TimeSpan, for example \"00:05:00\" for five minutes")
            .Validate(o => o.DefaultUrlExpiration > TimeSpan.Zero && o.DefaultUrlExpiration <= MaxUrlExpiration,
                $"S3:DefaultUrlExpiration must be greater than zero and at most {maxUrlExpiration}")
            .ValidateOnStart();

        services.AddOptions<StoredFileOptions>()
            .Bind(storedFilesSection)
            // if config is empty we set defaults
            .PostConfigure(o =>
            {
                if (o.AllowedContentTypes.Length == 0)
                    o.AllowedContentTypes = DefaultAllowedContentTypes;
            })
            .Validate(o => CronValidator.IsValid(o.MarkUploadedCron), "StoredFiles:MarkUploadedCron must be a valid cron expression")
            .Validate(o => CronValidator.IsValid(o.CleanupCron), "StoredFiles:CleanupCron must be a valid cron expression")
            // 1000 is the S3 DeleteObjects limit
            .Validate(o => o.BatchSize is > 0 and <= 1000, "StoredFiles:BatchSize must be between 1 and 1000")
            .Validate(o => o.MaxFileSizeInMb > 0, "StoredFiles:MaxFileSizeInMb must be greater than 0")
            .Validate(_ => storedFilesSection[nameof(StoredFileOptions.PendingExpiration)] is not { } value || value.IsTimeSpan(),
                "StoredFiles:PendingExpiration must be written as a TimeSpan, for example \"1.00:00:00\" for one day")
            .Validate<IOptions<S3Options>>((o, s3) => o.PendingExpiration > s3.Value.DefaultUrlExpiration,
                "StoredFiles:PendingExpiration must be greater than S3:DefaultUrlExpiration")
            .ValidateOnStart();

        services.AddSingleton<IAmazonS3>(sp => CreateClient(sp.GetRequiredService<IOptions<S3Options>>().Value));

        services.AddDateProvider();
        services.AddScoped<IStorageService, S3StorageService>();
        services.AddScoped<IFileStore>(sp => sp.GetRequiredService<TContext>());
        services.AddScoped<FileService>();
        services.AddScoped<IFileService>(sp => sp.GetRequiredService<FileService>());

        services.AddRecurringJob<MarkUploadedFilesJob<TContext>>(
            MarkUploadedFilesJob<TContext>.JobId, sp => sp.GetRequiredService<IOptions<StoredFileOptions>>().Value.MarkUploadedCron);
        services.AddRecurringJob<CleanupStoredFilesJob<TContext>>(
            CleanupStoredFilesJob<TContext>.JobId, sp => sp.GetRequiredService<IOptions<StoredFileOptions>>().Value.CleanupCron);

        return services;
    }

    internal static AmazonS3Client CreateClient(S3Options options) =>
        new(new BasicAWSCredentials(options.AccessKey, options.SecretKey), new AmazonS3Config
        {
            ServiceURL = options.ServiceUrl,
            AuthenticationRegion = options.Region,
            ForcePathStyle = options.ForcePathStyle,
            RequestChecksumCalculation = RequestChecksumCalculation.WHEN_REQUIRED,
            ResponseChecksumValidation = ResponseChecksumValidation.WHEN_REQUIRED
        });
}
