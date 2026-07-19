using Core.DataAccessTypes;
using Core.DateTimeProvider;
using Core.Identity.Context;
using Core.Identity.Domain;
using Core.Infrastructure;
using Core.Keycloak;
using Core.Logger;
using Microsoft.EntityFrameworkCore;

namespace Core.KeycloakSync;

public class KeycloakEventProcessor<TContext>(
    TContext db,
    IKeycloakService keycloakService,
    IEncryptor encryptor,
    IDateTimeProvider dateTimeProvider,
    IAppLogger<KeycloakEventProcessor<TContext>> logger)
    where TContext : BaseDbContext, IUserContext, IKeycloakEventsContext
{
    private const string AnonymizedValue = "User Deleted";
    private readonly string _encryptedAnonymizedValue = encryptor.Encrypt(AnonymizedValue);

    public async Task Run()
    {
        var events = await db.KeycloakAdminEvents
            .Where(e => !e.IsProcessed && e.ResourceType == "USER")
            .OrderBy(e => e.Time)
            .ToListAsync();

        var token = await keycloakService.GetToken();
        // @TODO batch process
        foreach (var @event in events)
        {
            try
            {
                await ProcessEvent(@event, token);
                @event.IsProcessed = true;
                await db.SaveChangesAsync();
            }
            catch (Exception ex)
            {
                logger.LogError(ex.Message, ex);
                continue;
            }
        }
    }

    private async Task ProcessEvent(KeycloakAdminEvent ev, string token)
    {
        var userId = ExtractUserId(ev.ResourcePath);
        if (userId == null)
            return;

        switch (ev.OperationType)
        {
            case "CREATE" or "UPDATE":
                await SyncUser(userId, token);
                break;
            case "DELETE":
                await DeleteUser(userId);
                break;
        }
    }

    private async Task SyncUser(string keycloakUserId, string token)
    {
        var keycloakUser = await keycloakService.GetUser(token, keycloakUserId);
        if (keycloakUser == null)
            return;

        var user = await db.Users.FirstOrDefaultAsync(u => u.KeycloakId == Guid.Parse(keycloakUserId));

        if (user == null)
        {
            user = new User()
            {
                KeycloakId = Guid.Parse(keycloakUser.Id),
                UserName = keycloakUser.Username,
                Email = keycloakUser.Email
            };

            db.Users.Add(user);
        }
        else
        {
            user.UserName = keycloakUser.Username;
            user.Email = keycloakUser.Email;
        }
    }

    private async Task DeleteUser(string keycloakUserId)
    {
        // @TODO if batch process implemented we can preload userlist and update it without ExecuteUpdate async, temporary to prevent additional query
        await db.Users
            .Where(x => x.KeycloakId == Guid.Parse(keycloakUserId))
            .ExecuteUpdateAsync(s => s
                .SetProperty(u => u.IsDeleted, true)
                .SetProperty(u => u.DateDeletedUtc, dateTimeProvider.UtcNow)
                .SetProperty(u => u.DateModifiedUtc, dateTimeProvider.UtcNow)
                .SetProperty(u => u.UserName, _encryptedAnonymizedValue)
                .SetProperty(u => u.Email, _encryptedAnonymizedValue));
    }

    private string ExtractUserId(string resourcePath)
    {
        var parts = resourcePath.Split('/', StringSplitOptions.RemoveEmptyEntries);

        if (parts.Length >= 2 && parts[0] == "users")
            return parts[1];

        return null;
    }
}
