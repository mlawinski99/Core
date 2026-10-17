using System.Net;
using Core.IntegrationTests.Permissions.Infrastructure;
using Core.Permissions.Entities;
using FluentAssertions;
using Xunit;

namespace Core.IntegrationTests.Permissions;

[Collection("PermissionsApi")]
public class HasPermissionTests(PermissionsApiFixture fixture)
{
    private const string ReadKey = "Core.IntegrationTests.Permissions.Infrastructure.TestPermission.Read";

    [Fact]
    public async Task HasPermission_WithUserPermission_ShouldReturn200()
    {
        // Arrange
        var userId = Guid.NewGuid();
        await using var db = fixture.CreateDbContext();
        db.UserPermissions.Add(new UserPermission { UserId = userId, Permission = ReadKey });
        await db.SaveChangesAsync();

        // Act
        var response = await fixture.CreateAuthenticatedClient(userId).GetAsync(PermissionsApiFixture.ReadEndpoint);

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.OK);
    }

    [Fact]
    public async Task HasPermission_WithRolePermission_ShouldReturn200()
    {
        // Arrange
        var userId = Guid.NewGuid();
        var role = new Role { Id = Guid.NewGuid(), Name = $"reader-{Guid.NewGuid()}" };
        await using var db = fixture.CreateDbContext();
        db.Roles.Add(role);
        db.RolePermissions.Add(new RolePermission { RoleId = role.Id, Permission = ReadKey });
        db.UserRoles.Add(new UserRole { UserId = userId, RoleId = role.Id });
        await db.SaveChangesAsync();

        // Act
        var response = await fixture.CreateAuthenticatedClient(userId).GetAsync(PermissionsApiFixture.ReadEndpoint);

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.OK);
    }

    [Fact]
    public async Task HasPermission_WithRoleWithoutPermission_ShouldReturn403()
    {
        // Arrange
        var userId = Guid.NewGuid();
        var role = new Role { Id = Guid.NewGuid(), Name = $"{Guid.NewGuid()}" };
        await using var db = fixture.CreateDbContext();
        db.Roles.Add(role);
        db.UserRoles.Add(new UserRole { UserId = userId, RoleId = role.Id });
        await db.SaveChangesAsync();

        // Act
        var response = await fixture.CreateAuthenticatedClient(userId).GetAsync(PermissionsApiFixture.ReadEndpoint);

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.Forbidden);
    }

    [Fact]
    public async Task HasPermission_WithPermissionOfOtherUser_ShouldReturn403()
    {
        // Arrange
        var userId = Guid.NewGuid();
        await using var db = fixture.CreateDbContext();
        db.UserPermissions.Add(new UserPermission { UserId = Guid.NewGuid(), Permission = ReadKey });
        await db.SaveChangesAsync();

        // Act
        var response = await fixture.CreateAuthenticatedClient(userId).GetAsync(PermissionsApiFixture.ReadEndpoint);

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.Forbidden);
    }

    [Fact]
    public async Task HasPermission_WithoutAnyPermission_ShouldReturn403()
    {
        // Arrange
        var client = fixture.CreateAuthenticatedClient(Guid.NewGuid());

        // Act
        var response = await client.GetAsync(PermissionsApiFixture.ReadEndpoint);

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.Forbidden);
    }

    [Fact]
    public async Task HasPermission_WithUnauthenticatedUser_ShouldReturn401()
    {
        // Arrange
        var client = fixture.CreateClient();

        // Act
        var response = await client.GetAsync(PermissionsApiFixture.ReadEndpoint);

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }
}
