namespace SeatHunter.Application.Interfaces;

public record ProxyDefinition(string Server, string? Username = null, string? Password = null);

public interface IProxyRotationService
{
    ProxyDefinition? GetNextProxy();
    void ReportFailure(string server);
    void ReportSuccess(string server);
    bool HasProxies { get; }
    int TotalProxies { get; }
    int CoolingDownProxiesCount { get; }
}
