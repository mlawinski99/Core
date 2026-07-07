using System.Net;
using Chatter.IntegrationTests.Messages.Infrastructure;
using Chatter.IntegrationTests.Shared.Infrastructure;
using Chatter.Messages.Application.Message.Errors;
using Chatter.Messages.Application.Message.Queries;
using Core.Pager;
using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Xunit;

namespace Chatter.IntegrationTests.Messages;

[Collection("MessagesApi")]
public class LoadMessagesTests
{
    private readonly MessagesTestFixture _fixture;

    public LoadMessagesTests(MessagesTestFixture fixture)
    {
        _fixture = fixture;
    }

    [Fact]
    public async Task LoadMessages_ValidChatId_Returns200WithMessages()
    {
        var client = _fixture.Api.CreateAuthenticatedClient();

        var response = await client.GetAsync(
            $"/Messages?chatId={MessagesDbSeeder.PrivateChat1Id}&pageSize=10");

        var result = await response.ReadResult<CursorPagedResult<LoadMessages.MessageDto>>();
        response.StatusCode.Should().Be(HttpStatusCode.OK);
        result.IsSuccess.Should().BeTrue();
        result.Data.Should().NotBeNull();
        result.Data!.Items.Should().NotBeEmpty();
    }

    [Fact]
    public async Task LoadMessages_NotChatMember_Returns403()
    {
        var client = _fixture.Api.CreateAuthenticatedClient();

        var response = await client.GetAsync(
            $"/Messages?chatId={MessagesDbSeeder.OtherUsersChatId}&pageSize=10");

        var result = await response.ReadResult();
        response.StatusCode.Should().Be(HttpStatusCode.Forbidden);
        result.IsSuccess.Should().BeFalse();
        result.Error.Should().Contain(ErrorMessages.NotChatMember);
    }

    [Fact]
    public async Task LoadMessages_WithCursor_ReturnsOlderMessages()
    {
        var client = _fixture.Api.CreateAuthenticatedClient();

        var response = await client.GetAsync(
            $"/Messages?chatId={MessagesDbSeeder.PrivateChat1Id}&lastMessageId={MessagesDbSeeder.Message2Id}&pageSize=10");

        var result = await response.ReadResult<CursorPagedResult<LoadMessages.MessageDto>>();
        response.StatusCode.Should().Be(HttpStatusCode.OK);
        result.IsSuccess.Should().BeTrue();
        result.Data.Should().NotBeNull();
        result.Data!.Items.Should().NotContain(m => m.Id == MessagesDbSeeder.Message2Id);
    }

    [Fact]
    public async Task LoadMessages_NonExistentChat_Returns404()
    {
        var client = _fixture.Api.CreateAuthenticatedClient();

        var response = await client.GetAsync(
            $"/Messages?chatId={Guid.NewGuid()}&pageSize=10");

        var result = await response.ReadResult();
        response.StatusCode.Should().Be(HttpStatusCode.NotFound);
        result.IsSuccess.Should().BeFalse();
        result.Error.Should().Contain(ErrorMessages.ChatNotFound);
    }

    [Fact]
    public async Task LoadMessages_EmptyChat_Returns200WithEmptyList()
    {
        var client = _fixture.Api.CreateAuthenticatedClient();

        var response = await client.GetAsync(
            $"/Messages?chatId={MessagesDbSeeder.PrivateChat2Id}&pageSize=10");

        var result = await response.ReadResult<CursorPagedResult<LoadMessages.MessageDto>>();
        response.StatusCode.Should().Be(HttpStatusCode.OK);
        result.IsSuccess.Should().BeTrue();
        result.Data.Should().NotBeNull();
        result.Data!.Items.Should().BeEmpty();
    }

    [Fact]
    public async Task LoadMessages_Unauthenticated_Returns401()
    {
        var client = _fixture.Api.CreateClient();

        var response = await client.GetAsync(
            $"/Messages?chatId={MessagesDbSeeder.PrivateChat1Id}&pageSize=10");

        response.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }
}