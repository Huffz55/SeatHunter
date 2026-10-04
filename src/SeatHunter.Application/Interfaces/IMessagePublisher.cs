namespace SeatHunter.Application.Interfaces;

public interface IMessagePublisher
{
    void Publish<T>(T message, string queueName);
}
