namespace Core.Interfaces;

/// <summary>Provides the current request's client IP address to backend services (e.g. AuditService).</summary>
public interface IClientIpSource
{
    string? GetClientIp();
}
