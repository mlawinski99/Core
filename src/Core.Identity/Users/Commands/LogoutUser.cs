using System.Net;
using Core.CQRS;
using Core.Infrastructure;
using Core.Keycloak;
using Core.Logger;
using Core.ResultPattern;

using static Core.Identity.Users.Errors.ErrorMessages;

namespace Core.Identity.Users.Commands;

public class LogoutUser : ICommandHandler<LogoutUser.LogoutUserCommand, Result>
{
    public record LogoutUserCommand(string RefreshToken) : ICommand<Result>;

    private readonly IKeycloakService _keycloakService;
    private readonly IAppLogger<LogoutUser> _logger;
    private readonly IUserProvider _userProvider;

    public LogoutUser(IKeycloakService keycloakService, IAppLogger<LogoutUser> logger, IUserProvider userProvider)
    {
        _keycloakService = keycloakService;
        _logger = logger;
        _userProvider = userProvider;
    }

    public async Task<Result> Handle(LogoutUserCommand command, CancellationToken cancellationToken)
    {
        try
        {
            await _keycloakService.LogoutUser(command.RefreshToken);
            return Result.Success;
        }
        catch (KeycloakException ex) when (ex.StatusCode is >= HttpStatusCode.BadRequest and < HttpStatusCode.InternalServerError)
        {
            _logger.LogWarning("Failed to logout user {UserId}: {Message}", _userProvider.UserId ?? Guid.Empty, ex.Message);
            return Result.BadRequest(FailedToLogout);
        }
    }
}