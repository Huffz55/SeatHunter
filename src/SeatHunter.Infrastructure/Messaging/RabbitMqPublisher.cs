using System.Text;
using System.Text.Json;
using RabbitMQ.Client;
using SeatHunter.Application.Interfaces;

using Microsoft.Extensions.Configuration;

namespace SeatHunter.Infrastructure.Messaging;

public class RabbitMqPublisher : IMessagePublisher
{
    private readonly string _hostName;

    public RabbitMqPublisher(IConfiguration configuration)
    {
        _hostName = configuration.GetConnectionString("RabbitMQ") ?? "localhost";
    }

    public void Publish<T>(T message, string queueName)
    {
        var factory = new ConnectionFactory { HostName = _hostName };
        using var connection = factory.CreateConnection();
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
