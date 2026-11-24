using Core.Interfaces;

namespace API.Common;

/// <summary>Reads the candidate origins of the current HTTP request (see <see cref="IRequestOriginSource"/>).</summary>
public class HttpRequestOriginSource : IRequestOriginSource
{
    private readonly IHttpContextAccessor _accessor;

    public HttpRequestOriginSource(IHttpContextAccessor accessor)
    {
        _accessor = accessor;
    }

    public IReadOnlyList<string> GetCandidateOrigins()
    {
        var request = _accessor.HttpContext?.Request;
        if (request == null)
        {
            return Array.Empty<string>();
        }

        var candidates = new List<string>();

        // Sent by the browser with every POST; the Vite dev proxy keeps it while it rewrites Host
        var origin = request.Headers.Origin.ToString();
        if (!string.IsNullOrWhiteSpace(origin))
        {
            candidates.Add(origin);
        }

        var forwardedHost = request.Headers["X-Forwarded-Host"].ToString().Split(',')[0].Trim();
        if (!string.IsNullOrEmpty(forwardedHost))
        {
            var forwardedProto = request.Headers["X-Forwarded-Proto"].ToString().Split(',')[0].Trim();
            candidates.Add($"{(string.IsNullOrEmpty(forwardedProto) ? request.Scheme : forwardedProto)}://{forwardedHost}");
        }

        if (request.Host.HasValue)
        {
            candidates.Add($"{request.Scheme}://{request.Host.Value}");
        }

        return candidates;
    }
}
