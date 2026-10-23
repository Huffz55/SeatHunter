using System.Text;
using System.Text.Json;
using Microsoft.AspNetCore.SignalR;
using RabbitMQ.Client;
using RabbitMQ.Client.Events;
using SeatHunter.Api.Hubs;
using SeatHunter.Application.Interfaces;
using SeatHunter.Domain.Interfaces;
using SeatHunter.Domain.Constants;

namespace SeatHunter.Api.BackgroundServices;

public class RabbitMqListenerService : BackgroundService
{
    private readonly IServiceProvider _serviceProvider;
    private readonly ILogger<RabbitMqListenerService> _logger;
    private readonly IConfiguration _configuration;
    private IConnection? _connection;
    private IModel? _channel;

    public RabbitMqListenerService(
        IServiceProvider serviceProvider, 
        ILogger<RabbitMqListenerService> logger,
        IConfiguration configuration)
    {
        _serviceProvider = serviceProvider;
        _logger = logger;
        _configuration = configuration;
    }

    private async Task ConnectWithRetryAsync(CancellationToken stoppingToken)
    {
        var hostName = _configuration.GetConnectionString("RabbitMQ") ?? "localhost";
        var factory = new ConnectionFactory { HostName = hostName };

        int retryCount = 0;
        while (!stoppingToken.IsCancellationRequested && (_connection == null || !_connection.IsOpen))
        {
            try
            {
                _connection = factory.CreateConnection();
                _channel = _connection.CreateModel();

                // 1. Dead Letter Exchange (DLX) tanımı
                _channel.ExchangeDeclare(
                    exchange: AppConstants.Exchanges.DeadLetterExchange,
                    type: ExchangeType.Direct,
                    durable: true,
                    autoDelete: false);

                // 2. Dead Letter Queue (DLQ) tanımı
                _channel.QueueDeclare(
                    queue: AppConstants.Queues.HuntRequestsDeadLetterQueue,
                    durable: true,
                    exclusive: false,
                    autoDelete: false,
                    arguments: null);

                // 3. DLQ -> DLX bağlama
                _channel.QueueBind(
                    queue: AppConstants.Queues.HuntRequestsDeadLetterQueue,
                    exchange: AppConstants.Exchanges.DeadLetterExchange,
                    routingKey: AppConstants.Queues.HuntRequestsQueue);

                // 4. Ana kuyruğu DLX argümanları ile tanımlama (Mevcut kuyruk 406 verirse koruyarak devam eder)
                try
                {
                    var queueArgs = new Dictionary<string, object>
                    {
                        { "x-dead-letter-exchange", AppConstants.Exchanges.DeadLetterExchange },
                        { "x-dead-letter-routing-key", AppConstants.Queues.HuntRequestsQueue }
                    };

                    _channel.QueueDeclare(
                        queue: AppConstants.Queues.HuntRequestsQueue,
                        durable: true,
                        exclusive: false,
                        autoDelete: false,
                        arguments: queueArgs);
                }
                catch (RabbitMQ.Client.Exceptions.OperationInterruptedException oie) when (oie.ShutdownReason?.ReplyCode == 406)
                {
                    _logger.LogWarning("Mevcut ana kuyruk ({Queue}) DLX argümanları olmadan oluşturulmuş (406). Mevcut kuyruk yapısı korunuyor.", AppConstants.Queues.HuntRequestsQueue);
                    _channel?.Dispose();
                    _channel = _connection.CreateModel();
                    _channel.QueueDeclare(
                        queue: AppConstants.Queues.HuntRequestsQueue,
                        durable: true,
                        exclusive: false,
                        autoDelete: false,
                        arguments: null);
                }

                _logger.LogInformation("RabbitMQ bağlantısı başarıyla kuruldu, DLX/DLQ ve ana kuyruk hazırlandı.");
                return;
            }
            catch (Exception ex)
            {
                retryCount++;
                _logger.LogWarning("RabbitMQ bağlantısı kurulamadı ({HostName}). 5 saniye sonra tekrar denenecek... (Deneme: {RetryCount}, Hata: {Message})", hostName, retryCount, ex.Message);
                try
                {
                    await Task.Delay(5000, stoppingToken);
                }
                catch (OperationCanceledException)
                {
                    return;
                }
            }
        }
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        await ConnectWithRetryAsync(stoppingToken);

        if (_channel == null || stoppingToken.IsCancellationRequested)
        {
            _logger.LogWarning("RabbitMQ kanalı oluşturulamadı veya servis durduruldu.");
            return;
        }

        var consumer = new EventingBasicConsumer(_channel);
        consumer.Received += async (model, ea) =>
        {
            bool isHandled = false;
            try
            {
                var body = ea.Body.ToArray();
                var message = Encoding.UTF8.GetString(body);
                var payload = JsonSerializer.Deserialize<JsonElement>(message);

                if (!payload.TryGetProperty("RouteOrEventId", out var routeProp) || string.IsNullOrWhiteSpace(routeProp.GetString()))
                {
                    _logger.LogWarning("Geçersiz kuyruk mesajı: RouteOrEventId eksik veya boş. Mesaj DLQ'ya yönlendiriliyor.");
                    if (_channel != null && _channel.IsOpen && !isHandled)
                    {
                        _channel.BasicReject(ea.DeliveryTag, requeue: false);
                        isHandled = true;
                    }
                    return;
                }
                var routeOrEventId = routeProp.GetString()!;

                var requestId = payload.TryGetProperty("RequestId", out var reqProp) ? reqProp.GetString() : "UNKNOWN";
                var providerName = payload.TryGetProperty("ProviderName", out var prop) ? prop.GetString() : "MockProvider";

                using var scope = _serviceProvider.CreateScope();
                var ticketProviderFactory = scope.ServiceProvider.GetRequiredService<ITicketProviderFactory>();
                var scraperMetrics = scope.ServiceProvider.GetService<IScraperMetrics>();
                var provider = ticketProviderFactory.GetProvider(routeOrEventId);

                var sw = System.Diagnostics.Stopwatch.StartNew();
                bool isAvailable;
                try
                {
                    isAvailable = await provider.CheckAvailabilityAsync(routeOrEventId, stoppingToken);
                    sw.Stop();
                    scraperMetrics?.RecordScrapeAttempt(providerName ?? "RabbitMqConsumer", isAvailable ? "found" : "not_found", sw.Elapsed.TotalMilliseconds);
                }
                catch (Exception)
                {
                    sw.Stop();
                    scraperMetrics?.RecordScrapeAttempt(providerName ?? "RabbitMqConsumer", "error", sw.Elapsed.TotalMilliseconds);
                    throw;
                }

                if (isAvailable)
                {
                    var userId = payload.TryGetProperty("UserId", out var userProp) ? userProp.GetString() : null;
                    if (!string.IsNullOrEmpty(userId))
                    {
                        var dbContext = scope.ServiceProvider.GetRequiredService<SeatHunter.Infrastructure.Persistence.SeatHunterDbContext>();
                        var notificationDispatcher = scope.ServiceProvider.GetRequiredService<SeatHunter.Application.Interfaces.INotificationDispatcher>();

                        var user = await dbContext.Users.FindAsync(new object[] { userId }, stoppingToken);

                        bool isAutoReserve = payload.TryGetProperty("AutoReserveEnabled", out var autoProp) && autoProp.GetBoolean();
                        bool isReserved = false;
                        string? cartUrl = null;
                        DateTime? reservedUntil = null;

                        if (isAutoReserve && user?.Tier == SeatHunter.Domain.Enums.UserTier.Vip)
                        {
                            var reservationEngine = scope.ServiceProvider.GetService<ITicketReservationEngine>();
                            if (reservationEngine != null)
                            {
                                Guid.TryParse(requestId, out var reqGuid);
                                var mockHunt = new SeatHunter.Domain.Entities.HuntRequest
                                {
                                    Id = reqGuid,
                                    UserId = userId,
                                    ProviderName = providerName ?? "UnknownProvider",
                                    RouteOrEventId = routeOrEventId,
                                    AutoReserveEnabled = true
                                };
                                var reserveResult = await reservationEngine.AttemptReserveAsync(mockHunt, stoppingToken);
                                if (reserveResult.IsSuccess)
                                {
                                    isReserved = true;
                                    cartUrl = reserveResult.CartUrl;
                                    reservedUntil = reserveResult.ExpiresAt;
                                }
                            }
                        }

                        Guid.TryParse(requestId, out var parsedReqGuid);
                        var notification = new SeatHunter.Application.Interfaces.TicketFoundNotification(
                            RequestId: parsedReqGuid,
                            UserId: userId,
                            UserEmail: user?.Email,
                            ProviderName: providerName ?? "UnknownProvider",
                            RouteOrEventId: routeOrEventId,
                            EventName: routeOrEventId,
                            Language: "tr",
                            IsAutoReserved: isReserved,
                            ReservationCartUrl: cartUrl,
                            ReservedUntil: reservedUntil
                        );

                        await notificationDispatcher.DispatchTicketFoundAsync(notification, stoppingToken);
                    }
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "RabbitMQ kuyruk mesajı işlenirken hata oluştu. Mesaj Dead Letter Queue (DLQ)'ya yönlendiriliyor.");
                try
                {
                    if (_channel != null && _channel.IsOpen && !isHandled)
                    {
                        _channel.BasicReject(ea.DeliveryTag, requeue: false);
                        isHandled = true;
                    }
                }
                catch (Exception rejectEx)
                {
                    _logger.LogWarning(rejectEx, "RabbitMQ BasicReject gönderilirken hata oluştu.");
                }
            }
            finally
            {
                try
                {
                    if (_channel != null && _channel.IsOpen && !isHandled)
                    {
                        _channel.BasicAck(ea.DeliveryTag, false);
                        isHandled = true;
                    }
                }
                catch (Exception ackEx)
                {
                    _logger.LogWarning(ackEx, "RabbitMQ BasicAck gönderilirken hata oluştu.");
                }
            }
        };

        _channel.BasicConsume(queue: AppConstants.Queues.HuntRequestsQueue, autoAck: false, consumer: consumer);

        // Servisi durdurma token'ı gelene kadar canlı tut
        try
        {
            await Task.Delay(Timeout.Infinite, stoppingToken);
        }
        catch (OperationCanceledException)
        {
            _logger.LogInformation("RabbitMQ Listener servisi durduruluyor.");
        }
    }

    public override void Dispose()
    {
        if (_channel != null && _channel.IsOpen) _channel.Dispose();
        if (_connection != null && _connection.IsOpen) _connection.Dispose();
        base.Dispose();
    }
}
