using Chatter.IntegrationTests.Shared.Infrastructure;
using Chatter.MessagesDataAccess.DbContexts;
using Chatter.MessagesDomain;
using Chatter.Shared.Domain;
using Core.DomainTypes;
using Microsoft.EntityFrameworkCore;

namespace Chatter.IntegrationTests.Messages.Infrastructure;

public static class MessagesDbSeeder
{
    public static readonly Guid TestUser1Id = Guid.Parse(KeycloakTestUsersData.TestUserId);
    public static readonly Guid TestUser2Id = Guid.Parse("550e8400-e29b-41d4-a716-446655440002");

    public static readonly Guid PrivateChat1Id = Guid.Parse("11111111-1111-1111-1111-111111111111");
    public static readonly Guid PrivateChat2Id = Guid.Parse("22222222-2222-2222-2222-222222222222");
    public static readonly Guid GroupChatId = Guid.Parse("33333333-3333-3333-3333-333333333333");
    public static readonly Guid OtherUsersChatId = Guid.Parse("44444444-4444-4444-4444-444444444444");

    public static readonly Guid Message1Id = Guid.Parse("aaaaaaaa-aaaa-aaaa-aaaa-aaaaaaaaaaaa");
    public static readonly Guid Message2Id = Guid.Parse("bbbbbbbb-bbbb-bbbb-bbbb-bbbbbbbbbbbb");
    public static readonly Guid Message3Id = Guid.Parse("cccccccc-cccc-cccc-cccc-cccccccccccc");
    public static readonly Guid Message4Id = Guid.Parse("dddddddd-dddd-dddd-dddd-dddddddddddd");

    public static void Seed(ChatDbContext db)
    {
        if (db.Users.Any())
            return;

        var user1 = new User
        {
            Id = TestUser1Id,
            KeycloakId = Guid.Parse(KeycloakTestUsersData.TestUserId),
            UserName = KeycloakTestUsersData.TestUsername,
            Email = KeycloakTestUsersData.TestEmail
        };

        var user2 = new User
        {
            Id = TestUser2Id,
            KeycloakId = TestUser2Id,
            UserName = "testuser2",
            Email = "testuser2@test.com"
        };

        db.Users.AddRange(user1, user2);
        db.SaveChanges();

        var privateChat1 = Chat.Create(ChatType.Private);
        privateChat1.Id = PrivateChat1Id;
        privateChat1.AddMember(user1);
        privateChat1.AddMember(user2);
        db.Chats.Add(privateChat1);

        var privateChat2 = Chat.Create(ChatType.Private);
        privateChat2.Id = PrivateChat2Id;
        privateChat2.AddMember(user1);
        db.Chats.Add(privateChat2);

        var groupChat = Chat.Create(ChatType.Group);
        groupChat.Id = GroupChatId;
        groupChat.AddMember(user1);
        groupChat.AddMember(user2);
        db.Chats.Add(groupChat);
        
        var otherUsersChat = Chat.Create(ChatType.Private);
        otherUsersChat.Id = OtherUsersChatId;
        otherUsersChat.AddMember(user2);
        db.Chats.Add(otherUsersChat);

        db.SaveChanges();

        var message1 = Message.Create(
            MessageContent.Create("Hello from user1"),
            TestUser1Id,
            PrivateChat1Id);
        message1.Id = Message1Id;
        message1.CreatedBy = TestUser1Id;

        var message2 = Message.Create(
            MessageContent.Create("Reply from user2"),
            TestUser2Id,
            PrivateChat1Id);
        message2.Id = Message2Id;
        message2.CreatedBy = TestUser2Id;

        var message3 = Message.Create(
            MessageContent.Create("Group message from user1"),
            TestUser1Id,
            GroupChatId);
        message3.Id = Message3Id;
        message3.CreatedBy = TestUser1Id;

        var message4 = Message.Create(
            MessageContent.Create("Group message from user2"),
            TestUser2Id,
            GroupChatId);
        message4.Id = Message4Id;
        message4.CreatedBy = TestUser2Id;

        db.Messages.AddRange(message1, message2, message3, message4);
        db.SaveChanges();

        // AuditableInterceptor overwrites CreatedBy during SaveChanges,
        // tests ownership fix
        db.Database.ExecuteSqlRaw(
            """
            UPDATE chat."Messages" SET "CreatedBy" = "SenderId"
            """);
    }
}
