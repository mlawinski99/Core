using Core.BackgroundJobs;
using Core.BackgroundJobs.Attributes;
using Core.DataAccessTypes;
using Core.Identity.Context;

namespace Core.KeycloakSync;

[DisallowConcurrentExecution]
// no retries, the next scheduled run picks up whatever failed
[Retry(0)]
public class KeycloakUserSyncJob<TContext>(
    KeycloakEventImporter<TContext> importer,
    KeycloakEventProcessor<TContext> processor)
    : IBackgroundJob
    where TContext : BaseDbContext, IUserContext, IKeycloakEventsContext
{
    public const string JobId = "keycloak-user-sync";

    public async Task Run(CancellationToken cancellationToken)
    {
        await importer.ImportEventsAsync();
        await processor.Run();
    }
}
