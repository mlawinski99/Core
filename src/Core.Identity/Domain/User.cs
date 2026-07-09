using Core.Infrastructure;
﻿

using Core.DomainTypes;

namespace Core.Identity.Domain;

//@TODO publish event after keycloak change user outbox, sync with keycloak
public class User : Entity, IAuditable, ISoftDeletable
{
    public Guid Id { get; set; }
    public Guid KeycloakId { get; set; }
    [Encryptable]
    public string UserName { get; set; }
    [Encryptable]
    public string Email { get; set; }
    public DateTime DateCreatedUtc { get; set; }
    public DateTime? DateModifiedUtc { get; set; }
    public DateTime? DateDeletedUtc { get; set; }
    public bool IsDeleted { get; set; }
}