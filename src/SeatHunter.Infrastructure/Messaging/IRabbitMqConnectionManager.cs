using RabbitMQ.Client;

namespace SeatHunter.Infrastructure.Messaging;

public interface IRabbitMqConnectionManager : IDisposable
{
    IConnection GetConnection();
}
