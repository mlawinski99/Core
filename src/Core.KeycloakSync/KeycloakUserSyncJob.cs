using Core.BackgroundJobs;
using Core.DataAccessTypes;
using Core.Identity.Context;

namespace Core.KeycloakSync;

[DisallowConcurrentExecution]
public class KeycloakUserSyncJob<TContext> : IBackgroundJob
    where TContext : BaseDbContext, IUserContext, IKeycloakEventsContext
{
    public const string JobId = "keycloak-user-sync";

    private readonly KeycloakEventImporter<TContext> _importer;
    private readonly KeycloakEventProcessor<TContext> _processor;

    public KeycloakUserSyncJob(KeycloakEventImporter<TContext> importer, KeycloakEventProcessor<TContext> processor)
    {
        _importer = importer;
        _processor = processor;
    }

    public async Task Run(CancellationToken cancellationToken)
    {
        await _importer.ImportEventsAsync();
        await _processor.Run();
    }
}