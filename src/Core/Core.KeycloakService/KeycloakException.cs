using System.Net;

namespace Core.KeycloakService;

public class KeycloakException : Exception
{
    public HttpStatusCode? StatusCode { get; }

    public KeycloakException(string message, HttpStatusCode? statusCode = null) : base(message)
    {
        StatusCode = statusCode;
    }
}
