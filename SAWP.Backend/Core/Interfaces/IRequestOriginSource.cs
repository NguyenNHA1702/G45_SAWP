namespace Core.Interfaces;

/// <summary>
/// The web addresses (scheme://host[:port]) the current request says it came from, most specific first:
/// the browser's Origin header, then the reverse proxy's X-Forwarded-Host, then Host. None of them is trusted
/// on its own; callers check them against a configured list.
/// </summary>
public interface IRequestOriginSource
{
    IReadOnlyList<string> GetCandidateOrigins();
}
