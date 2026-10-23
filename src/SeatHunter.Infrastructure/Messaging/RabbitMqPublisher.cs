using System.Text;
using System.Text.Json;
using RabbitMQ.Client;
using SeatHunter.Application.Interfaces;

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

        channel.QueueDeclare(queue: queueName,
                             durable: true,
                             exclusive: false,
                             autoDelete: false,
                             arguments: null);

        var json = JsonSerializer.Serialize(message);
        var body = Encoding.UTF8.GetBytes(json);

        channel.BasicPublish(exchange: "",
                             routingKey: queueName,
                             basicProperties: null,
                             body: body);
    }
}
