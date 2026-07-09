using Core.CQRS;
using Core.Keycloak;
using Core.ResultPattern;
using Core.Web;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

using static Core.Identity.Users.Commands.LoginUser;
using static Core.Identity.Users.Commands.LogoutUser;
using static Core.Identity.Users.Commands.RegisterUser;

namespace Core.Identity.Web;

[Route("api/[controller]")]
public class UsersController(IRequestDispatcher requestDispatcher) : BaseController(requestDispatcher)
{
    [HttpPost("register")]
    public async Task<Result> RegisterUser(RegisterUserCommand command)
    {
        return await _requestDispatcher.Dispatch(command);
    }

    [HttpPost("login")]
    public async Task<Result<KeycloakTokenResponse>> LoginUser(LoginUserCommand command)
    {
        return await _requestDispatcher.Dispatch(command);
    }

    [HttpPost("logout")]
    [Authorize]
    public async Task<Result> LogoutUser(LogoutUserCommand command)
    {
        return await _requestDispatcher.Dispatch(command);
    }
}