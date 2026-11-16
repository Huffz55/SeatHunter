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
    private readonly INotificationRepository? _notificationRepository;

    public CreateHuntRequestCommandHandler(
        IHuntRequestRepository repository,
        IMessagePublisher messagePublisher,
        ITicketProviderFactory factory,
        IUserPlanService planService,
        IDistributedHuntScheduler scheduler,
        INotificationRepository? notificationRepository = null)
    {
        _repository = repository;
        _messagePublisher = messagePublisher;
        _factory = factory;
        _planService = planService;
        _scheduler = scheduler;
        _notificationRepository = notificationRepository;
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
            TargetDate = (request.TargetDate == default || request.TargetDate <= DateTime.UtcNow)
                ? DateTime.UtcNow.AddDays(30) 
                : (request.TargetDate.Kind == DateTimeKind.Utc ? request.TargetDate : request.TargetDate.ToUniversalTime()),
            // İlk anlık kontrol RabbitMQ kuyruğuna devredildiği için worker ile yarışmaması adına ilk zamanlamayı 20sn sonraya kur
            NextScanTime = DateTime.UtcNow.AddSeconds(20),
            Language = request.Language ?? "tr",
            EventName = eventName,
            MaxPrice = request.MaxPrice,
            PreferredCategory = request.PreferredCategory,
            AutoReserveEnabled = request.AutoReserveEnabled
        };

        await _repository.AddAsync(huntRequest, cancellationToken);
        await _scheduler.ScheduleHuntAsync(huntRequest.Id, huntRequest.NextScanTime, cancellationToken);

        if (_notificationRepository != null)
        {
            await _notificationRepository.AddAsync(new UserNotification
            {
                UserId = huntRequest.UserId,
                Title = "Tarama Başlatıldı",
                Message = $"\"{eventName}\" için gerçek zamanlı bilet takibi başlatıldı.",
                Type = "info",
                RelatedUrl = huntRequest.RouteOrEventId
            }, cancellationToken);
        }

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

        // Özel IP blokları kontrolü (IP literal ve IPv6 dahil)
        if (System.Net.IPAddress.TryParse(host, out var literalIp))
        {
            if (IsPrivateOrReservedIp(literalIp))
            {
                throw new ArgumentException("Özel IP adresleri taranamaz.");
            }
            return;
        }

        // DNS üzerinden yerel/özel IP'ye çözümlenen alan adlarını engelle (SSRF koruması)
        try
        {
            var addresses = System.Net.Dns.GetHostAddresses(host);
            if (addresses.Any(IsPrivateOrReservedIp))
            {
                throw new ArgumentException("Dahili ağ adreslerine çözümlenen alan adları taranamaz.");
            }
        }
        catch (System.Net.Sockets.SocketException)
        {
            // Çevrimdışı veya test ortamında çözümlenemeyen dış alan adı için HTTP katmanının başarısız olmasına izin ver
        }
    }

    private static bool IsPrivateOrReservedIp(System.Net.IPAddress ip)
    {
        if (ip.IsIPv4MappedToIPv6)
        {
            ip = ip.MapToIPv4();
        }

        if (System.Net.IPAddress.IsLoopback(ip) || ip.Equals(System.Net.IPAddress.Any) || ip.Equals(System.Net.IPAddress.IPv6Any))
        {
            return true;
        }

        var bytes = ip.GetAddressBytes();
        if (ip.AddressFamily == System.Net.Sockets.AddressFamily.InterNetwork && bytes.Length == 4)
        {
            return bytes[0] == 0 ||                                      // 0.0.0.0/8
                   bytes[0] == 10 ||                                     // 10.0.0.0/8
                   bytes[0] == 127 ||                                    // 127.0.0.0/8
                   (bytes[0] == 100 && bytes[1] >= 64 && bytes[1] <= 127) || // 100.64.0.0/10 CGNAT
                   (bytes[0] == 169 && bytes[1] == 254) ||               // 169.254.0.0/16 Link-local / Metadata
                   (bytes[0] == 172 && bytes[1] >= 16 && bytes[1] <= 31) || // 172.16.0.0/12
                   (bytes[0] == 192 && bytes[1] == 168);                 // 192.168.0.0/16
        }

        if (ip.AddressFamily == System.Net.Sockets.AddressFamily.InterNetworkV6 && bytes.Length == 16)
        {
            bool isUniqueLocal = (bytes[0] & 0xFE) == 0xFC;              // fc00::/7
            bool isLinkLocal = bytes[0] == 0xFE && (bytes[1] & 0xC0) == 0x80; // fe80::/10
            return isUniqueLocal || isLinkLocal || ip.IsIPv6LinkLocal || ip.IsIPv6SiteLocal;
        }

        return false;
    }
}
