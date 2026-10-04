using System.Text;
using System.Text.Json;
using Microsoft.AspNetCore.SignalR;
using RabbitMQ.Client;
using RabbitMQ.Client.Events;
using SeatHunter.Api.Hubs;
using SeatHunter.Domain.Interfaces;

namespace SeatHunter.Api.BackgroundServices;

public class RabbitMqListenerService : BackgroundService
{
    private readonly IServiceProvider _serviceProvider;
    private readonly IHubContext<NotificationHub> _hubContext;
    private IConnection _connection;
    private IModel _channel;

    public RabbitMqListenerService(IServiceProvider serviceProvider, IHubContext<NotificationHub> hubContext)
    {
        _serviceProvider = serviceProvider;
        _hubContext = hubContext;
        InitRabbitMQ();
    }

        private void InitRabbitMQ()
    {
        var configuration = _serviceProvider.GetRequiredService<IConfiguration>();
        var hostName = configuration.GetConnectionString("RabbitMQ") ?? "localhost";
        var factory = new ConnectionFactory { HostName = hostName };
        _connection = factory.CreateConnection();
        _channel = _connection.CreateModel();
        _channel.QueueDeclare(queue: "hunt_requests_queue", durable: true, exclusive: false, autoDelete: false, arguments: null);
    }

    protected override Task ExecuteAsync(CancellationToken stoppingToken)
    {
        var consumer = new EventingBasicConsumer(_channel);
                consumer.Received += async (model, ea) =>
        {
            try
            {
                var body = ea.Body.ToArray();
                var message = Encoding.UTF8.GetString(body);
                var payload = JsonSerializer.Deserialize<JsonElement>(message);

                if (!payload.TryGetProperty("RouteOrEventId", out var routeProp) || string.IsNullOrWhiteSpace(routeProp.GetString()))
                {
                    _channel.BasicAck(ea.DeliveryTag, false);
                    return;
                }
                var routeOrEventId = routeProp.GetString()!;

                var requestId = payload.TryGetProperty("RequestId", out var reqProp) ? reqProp.GetString() : "UNKNOWN";
                var providerName = payload.TryGetProperty("ProviderName", out var prop) ? prop.GetString() : "MockProvider";

                using var scope = _serviceProvider.CreateScope();
                var ticketProviderFactory = scope.ServiceProvider.GetRequiredService<ITicketProviderFactory>();
                var provider = ticketProviderFactory.GetProvider(routeOrEventId);

                var isAvailable = await provider.CheckAvailabilityAsync(routeOrEventId, stoppingToken);

                if (isAvailable)
                {
                    await _hubContext.Clients.All.SendAsync("SeatFound", new 
                    { 
                        RequestId = requestId, 
                        Message = "Boş koltuk bulundu! Hemen biletinizi alın." 
                    }, cancellationToken: stoppingToken);
                }
            }
            finally
            {
                if (_channel.IsOpen)
                {
                    _channel.BasicAck(ea.DeliveryTag, false);
                }
            }
        };

        _channel.BasicConsume(queue: "hunt_requests_queue", autoAck: false, consumer: consumer);

        return Task.CompletedTask;
    }

        public override void Dispose()
    {
        if (_channel != null && _channel.IsOpen) _channel.Dispose();
        if (_connection != null && _connection.IsOpen) _connection.Dispose();
        base.Dispose();
    }
}
