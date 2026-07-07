using Chatter.MessagesDataAccess.DbEntitiesConfigurations;
using Chatter.MessagesDomain;
using Chatter.Shared.Context;
using Core.DataAccessTypes;
using Chatter.Shared.Domain;
using Core.Infrastructure.Json;
using Core.Outbox;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;

namespace Chatter.MessagesDataAccess.DbContexts;

public class ChatDbContext : BaseDbContext, IUserContext, IConfigurationContext, IOutbox
{
    public DbSet<ConfigurationData> ConfigurationData { get; set; }
    public DbSet<Message> Messages { get; set; }
    public DbSet<ChatMember> ChatMembers { get; set; }
    public DbSet<Chat> Chats { get; set; }
    public DbSet<KeycloakAdminEvent> KeycloakAdminEvents { get; set; }
    public DbSet<User> Users { get; set; }
    public DbSet<OutboxMessage> OutboxMessages { get; set; }

    public ChatDbContext(DbContextOptions<ChatDbContext> options,
        IJsonSerializer jsonSerializer,
        IEnumerable<IInterceptor> interceptors) : base(options, jsonSerializer, interceptors)
    {
    }

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        base.OnModelCreating(modelBuilder);
        modelBuilder.ApplyConfiguration(new ChatConfiguration());
        modelBuilder.ApplyConfiguration(new ChatMemberConfiguration());
        modelBuilder.ApplyConfiguration(new MessageConfiguration());
        modelBuilder.ApplyConfiguration(new UserConfiguration());
        modelBuilder.ApplyConfiguration(new KeycloakAdminEventConfiguration());
        modelBuilder.ApplyConfiguration(new ConfigurationDataConfiguration());
        modelBuilder.ApplyConfiguration(new OutboxMessageConfiguration());
    }
}