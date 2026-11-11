using System.Text;
using System.Text.Json;
using RabbitMQ.Client;
using SeatHunter.Application.Interfaces;
using SeatHunter.Domain.Constants;

namespace SeatHunter.Infrastructure.Messaging;

public class RabbitMqPublisher : IMessagePublisher
{
    private readonly IRabbitMqConnectionManager _connectionManager;

    public RabbitMqPublisher(IRabbitMqConnectionManager connectionManager)
    {
        _connectionManager = connectionManager;
    }

    public void Publish<T>(T message, string queueName)
    {
        var connection = _connectionManager.GetConnection();
        using var channel = connection.CreateModel();

        IDictionary<string, object>? queueArgs = null;
        if (queueName == AppConstants.Queues.HuntRequestsQueue)
        {
            queueArgs = new Dictionary<string, object>
            {
                { "x-dead-letter-exchange", AppConstants.Exchanges.DeadLetterExchange },
                { "x-dead-letter-routing-key", AppConstants.Queues.HuntRequestsQueue }
            };
        }

        try
        {
            channel.QueueDeclare(queue: queueName,
                                 durable: true,
                                 exclusive: false,
                                 autoDelete: false,
                                 arguments: queueArgs);
        }
        catch (RabbitMQ.Client.Exceptions.OperationInterruptedException oie) when (oie.ShutdownReason?.ReplyCode == 406)
        {
            using var fallbackChannel = connection.CreateModel();
            fallbackChannel.QueueDeclare(queue: queueName,
                                         durable: true,
                                         exclusive: false,
                                         autoDelete: false,
                                         arguments: null);

            var jsonFallback = JsonSerializer.Serialize(message);
            var bodyFallback = Encoding.UTF8.GetBytes(jsonFallback);
            fallbackChannel.BasicPublish(exchange: "",
                                         routingKey: queueName,
                                         basicProperties: null,
                                         body: bodyFallback);
            return;
        }

        var json = JsonSerializer.Serialize(message);
        var body = Encoding.UTF8.GetBytes(json);

        channel.BasicPublish(exchange: "",
                             routingKey: queueName,
                             basicProperties: null,
                             body: body);
    }
}
