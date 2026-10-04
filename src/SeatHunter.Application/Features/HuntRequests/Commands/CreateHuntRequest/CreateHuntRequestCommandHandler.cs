using MediatR;
using SeatHunter.Domain.Entities;
using SeatHunter.Application.Interfaces;
using SeatHunter.Domain.Interfaces;

namespace SeatHunter.Application.Features.HuntRequests.Commands.CreateHuntRequest;

public class CreateHuntRequestCommandHandler : IRequestHandler<CreateHuntRequestCommand, Guid>
{
    private readonly IHuntRequestRepository _repository;
    private readonly IMessagePublisher _messagePublisher;
    private readonly ITicketProviderFactory _factory;

    public CreateHuntRequestCommandHandler(IHuntRequestRepository repository, IMessagePublisher messagePublisher, ITicketProviderFactory factory)
    {
        _repository = repository;
        _messagePublisher = messagePublisher;
        _factory = factory;
    }

        public async Task<Guid> Handle(CreateHuntRequestCommand request, CancellationToken cancellationToken)
    {
        var provider = _factory.GetProvider(request.Url);
        
        string eventName = request.TargetEventName;
        if (string.IsNullOrWhiteSpace(eventName))
        {
            eventName = await provider.ExtractEventNameAsync(request.Url, cancellationToken);
            if (string.IsNullOrWhiteSpace(eventName))
            {
                try
                {
                    var uri = new Uri(request.Url);
                    eventName = uri.Host.Replace("www.", "");
                }
                catch
                {
                    eventName = "Unknown Event";
                }
            }
        }

        var huntRequest = new HuntRequest
        {
            UserId = request.UserId,
            ProviderName = provider.ProviderName,
            RouteOrEventId = request.Url,
            TargetDate = request.TargetDate,
            Language = request.Language ?? "tr",
            EventName = eventName
        };

        await _repository.AddAsync(huntRequest, cancellationToken);

        var eventMessage = new { RequestId = huntRequest.Id, ProviderName = huntRequest.ProviderName, RouteOrEventId = huntRequest.RouteOrEventId, Language = huntRequest.Language };
        _messagePublisher.Publish(eventMessage, "hunt_requests_queue");

        return huntRequest.Id;
    }
}
