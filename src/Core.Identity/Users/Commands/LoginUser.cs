using Core.CQRS;
using Core.Keycloak;
using Core.ResultPattern;

using static Core.Identity.Users.Errors.ErrorMessages;

namespace Core.Identity.Users.Commands;

public class LoginUser : ICommandHandler<LoginUser.LoginUserCommand, Result<KeycloakTokenResponse>>
{
    public record LoginUserCommand(string Username, string Password) : ICommand<Result<KeycloakTokenResponse>>;

    private readonly IKeycloakService _keycloakService;

    public LoginUser(IKeycloakService keycloakService)
    {
        _keycloakService = keycloakService;
    }

    public async Task<Result<KeycloakTokenResponse>> Handle(LoginUserCommand command, CancellationToken cancellationToken)
    {
        var tokenResponse = await _keycloakService.LoginUser(command.Username, command.Password);

        if (tokenResponse is null)
            return Result<KeycloakTokenResponse>.Unauthorized(InvalidUsernameOrPassword);

        return Result<KeycloakTokenResponse>.Success(tokenResponse);
    }
}
