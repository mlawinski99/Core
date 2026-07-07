using System.Net;
using System.Net.Http.Json;
using Chatter.IntegrationTests.Messages.Infrastructure;
using Chatter.IntegrationTests.Shared.Infrastructure;
using Chatter.Messages.Application.Message.Errors;
using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Xunit;

namespace Chatter.IntegrationTests.Messages;

[Collection("MessagesApi")]
public class SendMessageTests
{
    private readonly MessagesTestFixture _fixture;

    public SendMessageTests(MessagesTestFixture fixture)
    {
        _fixture = fixture;
    }

    [Fact]
    public async Task SendMessage_ValidRequest_Returns200AndPersistsMessage()
    {
        var client = _fixture.Api.CreateAuthenticatedClient();

        var content = $"Test message {Guid.NewGuid()}";
        var response = await client.PostAsJsonAsync("/Messages", new
        {
            ChatId = MessagesDbSeeder.PrivateChat1Id,
            Content = content
        });

        var result = await response.ReadResult();
        response.StatusCode.Should().Be(HttpStatusCode.OK);
        result.IsSuccess.Should().BeTrue();

        using var db = _fixture.CreateDbContext();
        var message = await db.Messages
            .AsNoTracking()
            .FirstOrDefaultAsync(m => m.Content.Text == content);

        message.Should().NotBeNull();
        message!.ChatId.Should().Be(MessagesDbSeeder.PrivateChat1Id);
        message.SenderId.Should().Be(MessagesDbSeeder.TestUser1Id);
    }

    [Fact]
    public async Task SendMessage_EmptyContent_ReturnsBadRequest()
    {
        var client = _fixture.Api.CreateAuthenticatedClient();

        var response = await client.PostAsJsonAsync("/Messages", new
        {
            ChatId = MessagesDbSeeder.PrivateChat1Id,
            Content = ""
        });

        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
    }

    [Fact]
    public async Task SendMessage_NonExistentChat_Returns404()
    {
        var client = _fixture.Api.CreateAuthenticatedClient();

        var response = await client.PostAsJsonAsync("/Messages", new
        {
            ChatId = Guid.NewGuid(),
            Content = "Message to non-existent chat"
        });

        var result = await response.ReadResult();
        response.StatusCode.Should().Be(HttpStatusCode.NotFound);
        result.IsSuccess.Should().BeFalse();
        result.Error.Should().Contain(ErrorMessages.ChatNotFound);
    }

    [Fact]
    public async Task SendMessage_NotChatMember_Returns403()
    {
        var client = _fixture.Api.CreateAuthenticatedClient();

        var response = await client.PostAsJsonAsync("/Messages", new
        {
            ChatId = MessagesDbSeeder.OtherUsersChatId,
            Content = "test"
        });

        var result = await response.ReadResult();
        response.StatusCode.Should().Be(HttpStatusCode.Forbidden);
        result.IsSuccess.Should().BeFalse();
        result.Error.Should().Contain(ErrorMessages.NotChatMember);
    }

    [Fact]
    public async Task SendMessage_Unauthenticated_Returns401()
    {
        var client = _fixture.Api.CreateClient();

        var response = await client.PostAsJsonAsync("/Messages", new
        {
            ChatId = MessagesDbSeeder.PrivateChat1Id,
            Content = "Unauthorized message"
        });

        response.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }
}