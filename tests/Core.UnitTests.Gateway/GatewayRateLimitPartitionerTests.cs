using System.Net;
using System.Security.Claims;
using Core.Gateway;
using FluentAssertions;
using Microsoft.AspNetCore.Http;
using Xunit;

namespace Core.UnitTests.Gateway;

public class GatewayRateLimitPartitionerTests
{
    private static readonly RateLimitOptions Options = new()
    {
        PerUser = new RateLimitWindowOptions { PermitLimit = 100, Window = TimeSpan.FromMinutes(1) },
        PerIpAddress = new RateLimitWindowOptions { PermitLimit = 20, Window = TimeSpan.FromMinutes(5) },
    };

    [Fact]
    public void Resolve_ShouldPartitionByUserId_WhenRequestIsAuthenticated()
    {
        // Arrange
        var httpContext = new DefaultHttpContext();
        httpContext.User = new ClaimsPrincipal(
            new ClaimsIdentity([new Claim(ClaimTypes.NameIdentifier, "user-1")], "test"));
        httpContext.Connection.RemoteIpAddress = IPAddress.Parse("10.0.0.1");

        // Act
        var partition = GatewayRateLimitPartitioner.Resolve(httpContext, Options);

        // Assert
        partition.Key.Should().Be("user:user-1");
        partition.Window.Should().BeSameAs(Options.PerUser);
    }

    [Fact]
    public void Resolve_ShouldGiveDifferentUsersDifferentPartitions()
    {
        // Arrange
        var firstContext = new DefaultHttpContext();
        firstContext.User = new ClaimsPrincipal(
            new ClaimsIdentity([new Claim(ClaimTypes.NameIdentifier, "user-1")], "test"));

        var secondContext = new DefaultHttpContext();
        secondContext.User = new ClaimsPrincipal(
            new ClaimsIdentity([new Claim(ClaimTypes.NameIdentifier, "user-2")], "test"));

        // Act
        var firstPartition = GatewayRateLimitPartitioner.Resolve(firstContext, Options);
        var secondPartition = GatewayRateLimitPartitioner.Resolve(secondContext, Options);

        // Assert
        firstPartition.Key.Should().NotBe(secondPartition.Key);
    }

    [Fact]
    public void Resolve_ShouldPartitionByIpAddress_WhenRequestIsAnonymous()
    {
        // Arrange
        var httpContext = new DefaultHttpContext();
        httpContext.Connection.RemoteIpAddress = IPAddress.Parse("10.0.0.1");

        // Act
        var partition = GatewayRateLimitPartitioner.Resolve(httpContext, Options);

        // Assert
        partition.Key.Should().Be("ip:10.0.0.1");
        partition.Window.Should().BeSameAs(Options.PerIpAddress);
    }

    [Fact]
    public void Resolve_ShouldThrow_WhenAnonymousRequestHasNoRemoteIpAddress()
    {
        // Arrange
        var httpContext = new DefaultHttpContext();

        // Act
        var resolve = () => GatewayRateLimitPartitioner.Resolve(httpContext, Options);

        // Assert
        resolve.Should().Throw<InvalidOperationException>();
    }

    [Fact]
    public void Resolve_ShouldNotThrow_WhenAuthenticatedRequestHasNoRemoteIpAddress()
    {
        // Arrange
        var httpContext = new DefaultHttpContext();
        httpContext.User = new ClaimsPrincipal(
            new ClaimsIdentity([new Claim(ClaimTypes.NameIdentifier, "user-1")], "test"));

        // Act
        var partition = GatewayRateLimitPartitioner.Resolve(httpContext, Options);

        // Assert
        partition.Key.Should().Be("user:user-1");
    }
}
