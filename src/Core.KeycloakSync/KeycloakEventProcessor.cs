using Core.DataAccessTypes;
using Core.Identity.Context;
using Core.Identity.Domain;
using Core.Keycloak;
using Core.Logger;
using Microsoft.EntityFrameworkCore;

namespace Core.KeycloakSync;

public class KeycloakEventProcessor<TContext> where TContext : BaseDbContext, IUserContext, IKeycloakEventsContext
{
    private readonly TContext _db;
    private readonly IKeycloakService _keycloakService;
    private readonly IAppLogger<KeycloakEventProcessor<TContext>> _logger;

    public KeycloakEventProcessor(TContext db, IKeycloakService keycloakService,
        IAppLogger<KeycloakEventProcessor<TContext>> logger)
    {
        _db = db;
        _keycloakService = keycloakService;
        _logger = logger;
    }

    public async Task Run()
    {
        var events = await _db.KeycloakAdminEvents
            .Where(e => !e.IsProcessed && e.ResourceType == "USER")
            .OrderBy(e => e.Time)
            .ToListAsync();

        var token = await _keycloakService.GetToken();
        // @TODO batch process
        foreach (var @event in events)
        {
            try
            {
                await ProcessEvent(@event, token);
                @event.IsProcessed = true;
                await _db.SaveChangesAsync();
            }
            catch (Exception ex)
            {
                _logger.LogError(ex.Message, ex);
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
        var keycloakUser = await _keycloakService.GetUser(token, keycloakUserId);
        if (keycloakUser == null)
            return;

        var user = await _db.Users.FirstOrDefaultAsync(u => u.KeycloakId == Guid.Parse(keycloakUserId));

        if (user == null)
        {
            user = new User()
            {
                KeycloakId = Guid.Parse(keycloakUser.Id),
                UserName = keycloakUser.Username,
                Email = keycloakUser.Email
            };

            _db.Users.Add(user);
        }
        else
        {
            user.UserName = keycloakUser.Username;
            user.Email = keycloakUser.Email;
        }
    }

    private async Task DeleteUser(string keycloakUserId)
    {
        await _db.Users
            .Where(x => x.KeycloakId == Guid.Parse(keycloakUserId))
            .ExecuteDeleteAsync();
    }

    private string ExtractUserId(string resourcePath)
    {
        var parts = resourcePath.Split('/', StringSplitOptions.RemoveEmptyEntries);

        if (parts.Length >= 2 && parts[0] == "users")
            return parts[1];

        return null;
    }
}
