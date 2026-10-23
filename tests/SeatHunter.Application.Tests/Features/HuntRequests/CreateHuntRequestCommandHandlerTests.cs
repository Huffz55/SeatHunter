using FluentAssertions;
using Moq;
using SeatHunter.Application.Features.HuntRequests.Commands.CreateHuntRequest;
using SeatHunter.Application.Interfaces;
using SeatHunter.Domain.Entities;
using SeatHunter.Domain.Interfaces;

namespace SeatHunter.Application.Tests.Features.HuntRequests;

public class CreateHuntRequestCommandHandlerTests
{
    [Fact]
    public async Task Handle_Should_SaveToDatabase_And_PublishMessage()
    {
        // Arrange (Hazırlık): Veritabanı, RabbitMQ ve Factory bağımlılıklarını taklit ediyoruz (Mock)
        var mockRepo = new Mock<IHuntRequestRepository>();
        var mockPublisher = new Mock<IMessagePublisher>();
        var mockFactory = new Mock<ITicketProviderFactory>();
        var mockProvider = new Mock<ITicketProvider>();
        var mockPlanService = new Mock<IUserPlanService>();
        var mockScheduler = new Mock<IDistributedHuntScheduler>();

        // Sahte provider ayarları
        mockProvider.Setup(p => p.ProviderName).Returns("MockProvider");
        mockProvider.Setup(p => p.ExtractEventNameAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync("Mock Event Name");

        mockFactory.Setup(f => f.GetProvider(It.IsAny<string>())).Returns(mockProvider.Object);
        
        var handler = new CreateHuntRequestCommandHandler(mockRepo.Object, mockPublisher.Object, mockFactory.Object, mockPlanService.Object, mockScheduler.Object);
        var command = new CreateHuntRequestCommand(
            UserId: "user123", 
            Url: "https://biletix.com/mock-event", 
            TargetDate: DateTime.UtcNow, 
            Language: "tr", 
            TargetEventName: "Tarkan Konseri"
        );

        // Act (Eylem): Handler'ı çalıştırıyoruz
        var result = await handler.Handle(command, CancellationToken.None);

        // Assert (Doğrulama): Sonuçları kontrol ediyoruz
        result.Should().NotBeEmpty(); // Geriye geçerli bir Guid dönmeli
        
        // Repository'nin AddAsync metodunun tam olarak 1 kez çağrıldığını doğrula
        mockRepo.Verify(r => r.AddAsync(It.IsAny<HuntRequest>(), It.IsAny<CancellationToken>()), Times.Once);
        
        // Publisher'ın mesajı kuyruğa tam olarak 1 kez gönderdiğini doğrula
        mockPublisher.Verify(p => p.Publish(It.IsAny<object>(), "hunt_requests_queue"), Times.Once);

        // Scheduler'ın görevi zamanladığını doğrula
        mockScheduler.Verify(s => s.ScheduleHuntAsync(It.IsAny<Guid>(), It.IsAny<DateTime>(), It.IsAny<CancellationToken>()), Times.Once);
    }

    [Theory]
    [InlineData("http://localhost:8080")]
    [InlineData("http://127.0.0.1/admin")]
    [InlineData("http://169.254.169.254/latest/meta-data")]
    [InlineData("http://postgres:5432")]
    [InlineData("ftp://biletix.com/test")]
    [InlineData("")]
    public async Task Handle_Should_ThrowArgumentException_WhenUrlIsSuspicious(string invalidUrl)
    {
        var mockRepo = new Mock<IHuntRequestRepository>();
        var mockPublisher = new Mock<IMessagePublisher>();
        var mockFactory = new Mock<ITicketProviderFactory>();
        var mockPlanService = new Mock<IUserPlanService>();
        var mockScheduler = new Mock<IDistributedHuntScheduler>();

        var handler = new CreateHuntRequestCommandHandler(mockRepo.Object, mockPublisher.Object, mockFactory.Object, mockPlanService.Object, mockScheduler.Object);
        var command = new CreateHuntRequestCommand(
            UserId: "user123",
            Url: invalidUrl,
            TargetDate: DateTime.UtcNow,
            Language: "tr",
            TargetEventName: "Tarkan Konseri"
        );

        Func<Task> act = async () => await handler.Handle(command, CancellationToken.None);

        await act.Should().ThrowAsync<ArgumentException>();
    }
}
