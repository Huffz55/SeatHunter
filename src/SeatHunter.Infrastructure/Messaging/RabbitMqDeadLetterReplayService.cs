using Microsoft.Extensions.Logging;
using RabbitMQ.Client;
using SeatHunter.Application.Interfaces;
using SeatHunter.Domain.Constants;

namespace SeatHunter.Infrastructure.Messaging;

public class RabbitMqDeadLetterReplayService : IDeadLetterReplayService
{
    private readonly IRabbitMqConnectionManager _connectionManager;
    private readonly ILogger<RabbitMqDeadLetterReplayService> _logger;

    public RabbitMqDeadLetterReplayService(
        IRabbitMqConnectionManager connectionManager, 
        ILogger<RabbitMqDeadLetterReplayService> logger)
    {
        _connectionManager = connectionManager;
        _logger = logger;
    }

    public Task<DeadLetterQueueStatus> GetStatusAsync(CancellationToken cancellationToken = default)
    {
        try
        {
            var connection = _connectionManager.GetConnection();
            using var channel = connection.CreateModel();

            var queueResult = channel.QueueDeclarePassive(AppConstants.Queues.HuntRequestsDeadLetterQueue);

            return Task.FromResult(new DeadLetterQueueStatus(
                QueueName: queueResult.QueueName,
                MessageCount: queueResult.MessageCount,
                ConsumerCount: queueResult.ConsumerCount,
                CheckedAt: DateTime.UtcNow
            ));
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "DLQ durumu sorgulanırken hata oluştu (kuyruk henüz oluşmamış olabilir).");
            return Task.FromResult(new DeadLetterQueueStatus(
                QueueName: AppConstants.Queues.HuntRequestsDeadLetterQueue,
                MessageCount: 0,
                ConsumerCount: 0,
                CheckedAt: DateTime.UtcNow
            ));
        }
    }

    public Task<int> ReplayMessagesAsync(int maxCount = 100, CancellationToken cancellationToken = default)
    {
        int replayedCount = 0;

        try
        {
            var connection = _connectionManager.GetConnection();
            using var channel = connection.CreateModel();

            for (int i = 0; i < maxCount; i++)
            {
                var result = channel.BasicGet(AppConstants.Queues.HuntRequestsDeadLetterQueue, autoAck: false);
                if (result == null) break;

                // Mesajı ana kuyruğa geri ilet
                var props = channel.CreateBasicProperties();
                props.Persistent = true;

                channel.BasicPublish(
                    exchange: string.Empty,
                    routingKey: AppConstants.Queues.HuntRequestsQueue,
                    basicProperties: props,
                    body: result.Body
                );

                // DLQ'daki mesajı onayla ve kaldır
                channel.BasicAck(result.DeliveryTag, multiple: false);
                replayedCount++;
            }

            _logger.LogInformation("♻️ {Count} adet poison mesaj DLQ'dan ana kuyruğa ({MainQueue}) başarıyla yeniden yönlendirildi.",
                replayedCount, AppConstants.Queues.HuntRequestsQueue);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Poison mesajlar yeniden kuyruğa alınırken hata oluştu.");
        }

        return Task.FromResult(replayedCount);
    }

    public Task<uint> PurgeQueueAsync(CancellationToken cancellationToken = default)
    {
        uint purgedCount = 0;

        try
        {
            var connection = _connectionManager.GetConnection();
            using var channel = connection.CreateModel();

            purgedCount = channel.QueuePurge(AppConstants.Queues.HuntRequestsDeadLetterQueue);
            _logger.LogInformation("🧹 {Count} adet mesaj DLQ kuyruğundan temizlendi.", purgedCount);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "DLQ temizleme işlemi sırasında hata oluştu.");
        }

        return Task.FromResult(purgedCount);
    }
}
