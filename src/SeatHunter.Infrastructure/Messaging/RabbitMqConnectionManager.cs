using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using RabbitMQ.Client;

namespace SeatHunter.Infrastructure.Messaging;

public class RabbitMqConnectionManager : IRabbitMqConnectionManager
{
    private readonly IConfiguration _configuration;
    private readonly ILogger<RabbitMqConnectionManager> _logger;
    private IConnection? _connection;
    private readonly object _lock = new();

    public RabbitMqConnectionManager(IConfiguration configuration, ILogger<RabbitMqConnectionManager> logger)
    {
        _configuration = configuration;
        _logger = logger;
    }

    public IConnection GetConnection()
    {
        if (_connection != null && _connection.IsOpen)
            return _connection;

        lock (_lock)
        {
            if (_connection != null && _connection.IsOpen)
                return _connection;

            var hostName = _configuration.GetConnectionString("RabbitMQ");
            if (string.IsNullOrWhiteSpace(hostName)) hostName = "localhost";
            var factory = new ConnectionFactory
            {
                HostName = hostName,
                AutomaticRecoveryEnabled = true,
                NetworkRecoveryInterval = TimeSpan.FromSeconds(5)
            };

            _connection = factory.CreateConnection();
            _logger.LogInformation("RabbitMQ kalıcı (long-lived) TCP bağlantısı başarıyla oluşturuldu.");
            return _connection;
        }
    }

    public void Dispose()
    {
        if (_connection != null && _connection.IsOpen)
        {
            _connection.Dispose();
        }
    }
}
