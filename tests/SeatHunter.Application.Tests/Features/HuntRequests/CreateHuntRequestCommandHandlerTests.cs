using FluentAssertions;
using Moq;
using SeatHunter.Application.Features.HuntRequests.Commands.CreateHuntRequest;
using SeatHunter.Application.Interfaces;
using SeatHunter.Domain.Entities;

namespace SeatHunter.Application.Tests.Features.HuntRequests;

public class CreateHuntRequestCommandHandlerTests
{
    [Fact]
    public async Task Handle_Should_SaveToDatabase_And_PublishMessage()
    {
        // Arrange (Hazırlık): Veritabanı ve RabbitMQ bağımlılıklarını taklit ediyoruz (Mock)
        var mockRepo = new Mock<IHuntRequestRepository>();
        var mockPublisher = new Mock<IMessagePublisher>();
        
        var handler = new CreateHuntRequestCommandHandler(mockRepo.Object, mockPublisher.Object);
        var command = new CreateHuntRequestCommand("user123", "Obilet", "IST-ANK", DateTime.UtcNow);

        // Act (Eylem): Handler'ı çalıştırıyoruz
        var result = await handler.Handle(command, CancellationToken.None);

        // Assert (Doğrulama): Sonuçları kontrol ediyoruz
        result.Should().NotBeEmpty(); // Geriye geçerli bir Guid dönmeli
        
        // Repository'nin AddAsync metodunun tam olarak 1 kez çağrıldığını doğrula
        mockRepo.Verify(r => r.AddAsync(It.IsAny<HuntRequest>(), It.IsAny<CancellationToken>()), Times.Once);
        
        // Publisher'ın mesajı kuyruğa tam olarak 1 kez gönderdiğini doğrula
        mockPublisher.Verify(p => p.Publish(It.IsAny<object>(), "hunt_requests_queue"), Times.Once);
    }
}
