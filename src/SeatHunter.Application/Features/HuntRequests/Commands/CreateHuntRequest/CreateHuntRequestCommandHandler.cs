using MediatR;
using SeatHunter.Domain.Entities;
using SeatHunter.Application.Interfaces;
using SeatHunter.Domain.Interfaces;
using SeatHunter.Domain.Constants;

namespace SeatHunter.Application.Features.HuntRequests.Commands.CreateHuntRequest;

public class CreateHuntRequestCommandHandler : IRequestHandler<CreateHuntRequestCommand, Guid>
{
    private readonly IHuntRequestRepository _repository;
    private readonly IMessagePublisher _messagePublisher;
    private readonly ITicketProviderFactory _factory;
    private readonly IUserPlanService _planService;
    private readonly IDistributedHuntScheduler _scheduler;

    public CreateHuntRequestCommandHandler(
        IHuntRequestRepository repository, 
        IMessagePublisher messagePublisher, 
        ITicketProviderFactory factory,
        IUserPlanService planService,
        IDistributedHuntScheduler scheduler)
    {
        _repository = repository;
        _messagePublisher = messagePublisher;
        _factory = factory;
        _planService = planService;
        _scheduler = scheduler;
    }

    public async Task<Guid> Handle(CreateHuntRequestCommand request, CancellationToken cancellationToken)
    {
        ValidateUrlSecurity(request.Url);

        await _planService.ValidateHuntCreationAsync(request.UserId, request, cancellationToken);

        var provider = _factory.GetProvider(request.Url);
        
        string? eventName = request.TargetEventName;
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
            EventName = eventName,
            MaxPrice = request.MaxPrice,
            PreferredCategory = request.PreferredCategory,
            AutoReserveEnabled = request.AutoReserveEnabled
        };

        await _repository.AddAsync(huntRequest, cancellationToken);
        await _scheduler.ScheduleHuntAsync(huntRequest.Id, huntRequest.NextScanTime, cancellationToken);

        var eventMessage = new 
        { 
            RequestId = huntRequest.Id, 
            UserId = huntRequest.UserId,
            ProviderName = huntRequest.ProviderName, 
            RouteOrEventId = huntRequest.RouteOrEventId, 
            Language = huntRequest.Language,
            MaxPrice = huntRequest.MaxPrice,
            PreferredCategory = huntRequest.PreferredCategory,
            AutoReserveEnabled = huntRequest.AutoReserveEnabled
        };
        _messagePublisher.Publish(eventMessage, AppConstants.Queues.HuntRequestsQueue);

        return huntRequest.Id;
    }

    private static void ValidateUrlSecurity(string url)
    {
        if (string.IsNullOrWhiteSpace(url))
            throw new ArgumentException("Hedef URL boş olamaz.");

        if (!Uri.TryCreate(url, UriKind.Absolute, out var uri))
            throw new ArgumentException("Geçersiz URL formatı.");

        if (uri.Scheme != Uri.UriSchemeHttp && uri.Scheme != Uri.UriSchemeHttps)
            throw new ArgumentException("Sadece HTTP ve HTTPS protokolleri desteklenmektedir.");

        var host = uri.Host.ToLowerInvariant();

        // Yerel ağ, loopback ve dahili servis koruması (SSRF Koruması)
        if (host == "localhost" || host == "127.0.0.1" || host == "0.0.0.0" || host == "::1" ||
            host == "169.254.169.254" || host == "postgres" || host == "redis" || host == "rabbitmq" ||
            host.EndsWith(".internal") || host.EndsWith(".local"))
        {
            throw new ArgumentException("Dahili ağ adresleri ve yerel bağlantılar taranamaz.");
        }

        // Özel IP blokları kontrolü (RFC 1918 ve Link-local)
        if (System.Net.IPAddress.TryParse(host, out var ip))
        {
            if (System.Net.IPAddress.IsLoopback(ip) || ip.ToString().StartsWith("10.") ||
                ip.ToString().StartsWith("192.168.") || ip.ToString().StartsWith("169.254."))
            {
                throw new ArgumentException("Özel IP adresleri taranamaz.");
            }
        }
    }
}
