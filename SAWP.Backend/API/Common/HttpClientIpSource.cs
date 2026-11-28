using Core.Interfaces;

namespace API.Common;

public class HttpClientIpSource : IClientIpSource
{
    private readonly IHttpContextAccessor _accessor;

    public HttpClientIpSource(IHttpContextAccessor accessor)
    {
        _accessor = accessor;
    }

    public string? GetClientIp()
    {
        var ctx = _accessor.HttpContext;
        if (ctx == null) return null;

        var forwarded = ctx.Request.Headers["X-Forwarded-For"].FirstOrDefault();
        if (!string.IsNullOrWhiteSpace(forwarded))
        {
            return forwarded.Split(',')[0].Trim();
        }

        return ctx.Connection.RemoteIpAddress?.ToString();
    }
}
