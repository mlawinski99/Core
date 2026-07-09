using Microsoft.EntityFrameworkCore;

namespace Core.KeycloakSync;

public interface IKeycloakEventsContext
{
    DbSet<KeycloakAdminEvent> KeycloakAdminEvents { get; set; }
}