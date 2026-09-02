using Microsoft.AspNetCore.Http;

namespace Core.RequestContext;

public class ExpectedVersionProvider(IHttpContextAccessor httpContextAccessor) : IExpectedVersionProvider
{
    private const string VersionHeader = "X-Version";

    public int? ExpectedVersion
    {
        get
        {
            var value = httpContextAccessor.HttpContext?.Request.Headers[VersionHeader].ToString();

            if (string.IsNullOrEmpty(value))
                return null;

            return int.TryParse(value, out var version)
                ? version
                : throw new FormatException($"Invalid {VersionHeader} header value '{value}'");
        }
    }
}