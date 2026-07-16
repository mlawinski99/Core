using System.Net;
using Core.CQRS;
using Core.Keycloak;
using Core.Logger;
using Core.ResultPattern;

using static Core.Identity.Users.Errors.ErrorMessages;

namespace Core.Identity.Users.Commands;

public class RegisterUser : ICommandHandler<RegisterUser.RegisterUserCommand, Result>
{
    public record RegisterUserCommand(string Username, string Password, string ConfirmPassword, string Email) : ICommand<Result>;

    private readonly IKeycloakService _keycloakService;
    private readonly IAppLogger<RegisterUser> _logger;

    public RegisterUser(IKeycloakService keycloakService, IAppLogger<RegisterUser> logger)
    {
        _keycloakService = keycloakService;
        _logger = logger;
    }

    public async Task<Result> Handle(RegisterUserCommand command, CancellationToken cancellationToken)
    {
        try
        {
            var token = await _keycloakService.GetToken();
            await _keycloakService.CreateUser(token, command.Username, command.Email, command.Password);
            return Result.Success;
        }
        catch (KeycloakException ex) when (ex.StatusCode == HttpStatusCode.Conflict)
        {
            _logger.LogWarning("Registration conflict for {Username}: {Message}", command.Username, ex.Message);
            return Result.Conflict(UserAlreadyExists);
        }
    }
}
