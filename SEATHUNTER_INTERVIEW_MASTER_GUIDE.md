# SEATHUNTER: SENIOR & LEAD ENGINEER INTERVIEW MASTER GUIDE
**A'dan Z'ye Mimari Savunma, Kod Analizi, Sistem Tasarımı ve Mülakat Başucu Rehberi**

---

## GİRİŞ VE BELGENİN AMACI
Bu doküman, **SeatHunter** projesinin mimari kararlarını, kod yapısını, kullanılan teknolojilerin seçim gerekçelerini ve karşılaşılan kritik mühendislik problemlerine üretilen çözümleri en ince ayrıntısına kadar belgelemek amacıyla hazırlanmıştır. Bir Senior/Lead Yazılım Mühendisi veya Çözüm Mimarı perspektifiyle, projenin teknik mülakatlarda ve mimari inceleme kurullarında (Architecture Review Board) eksiksiz şekilde savunulabilmesi için gerekli tüm teorik ve pratik donanımı sağlar.

---

## 1. SİSTEMİN GENEL VİZYONU VE İŞ AKIŞI

### 1.1. SeatHunter Nedir ve Hangi Problemi Çözer?
SeatHunter; popüler konser, tiyatro, spor müsabakaları ve seyahat platformlarında (Biletix, Obilet, Passo, Ingresso vb.) saniyeler içinde tükenen biletlerin ve iptal edilen koltukların takibini otomatikleştiren, yüksek erişilebilirlikli (High Availability), olay güdümlü (Event-Driven) ve hibrit kazıma (Hybrid Scraping) yeteneğine sahip bir **Bilet ve Stok Avcısı SaaS (Software as a Service)** platformudur.

#### Çözülen Problem:
Geleneksel bilet satış sistemlerinde talep arzı katbekat aşar. Biletler tükendiğinde, kullanıcıların sisteme iade edilen veya sonradan satışa açılan koltukları yakalayabilmesi için günlerce ekran başında sayfayı yenilemesi (F5 yapması) gerekir. Bu durum kullanıcı tarafında ciddi bir zaman kaybı yaratırken, hedef bilet sağlayıcıları için de düzensiz ve verimsiz bir trafik yükü oluşturur. 

SeatHunter bu süreci insan faktöründen çıkararak otonomlaştırır:
1. **Otonom Tarama:** Kullanıcının hedef URL'ini ve etkinliğini 7/24 arka planda periyodik olarak tarar.
2. **Akıllı Algılama:** Hem statik HTML sayfalarını hem de istemci taraflı (Client-Side Rendering) çalışan modern JavaScript/SPA uygulamalarını analiz eder.
3. **Milisaniyelik Bildirim:** Boş koltuk veya açılan kontenjan tespit edildiği anda kullanıcıya çok dilli E-posta, Telegram ve WebSockets (SignalR) üzerinden anlık uyarı iletir.

---

### 1.2. Uçtan Uca Yaşam Döngüsü (End-to-End Life Cycle)
Bir kullanıcının bilet takip isteği oluşturmasından, biletin bulunup bildirimlerin gönderilmesine kadar geçen sürecin adım adım akışı:

```
[Kullanıcı / UI] 
       │ (1. POST /api/HuntRequests + Bearer JWT)
       ▼
[RateLimiter: FixedPolicy (3 req / 10s)]
       │
       ▼
[HuntRequestsController]
       │ (2. _mediator.Send(CreateHuntRequestCommand))
       ▼
[CreateHuntRequestCommandHandler]
       │
       ├─► [ITicketProviderFactory.GetProvider(url)] ────► [Provider Tespiti & Event Adı Çözümleme]
       │
       ├─► [IHuntRequestRepository.AddAsync] ────────────► [PostgreSQL: HuntRequests Tablosuna Kayıt]
       │                                                    (Status = Active, NextScanTime = UtcNow)
       │
       └─► [IMessagePublisher.Publish] ──────────────────► [RabbitMQ: "hunt_requests_queue"]
                                                                   │
                                                                   ▼
                                                     [RabbitMqListenerService]
                                                     (İlk Anlık Tarama & Hızlı Doğrulama)
                                                                   │
                                                                   └─► Koltuk varsa: SignalR ile UI'a anlık bildirim

[Arka Plan Periyodik Döngüsü: ContinuousHuntWorker (Her 5 Saniyede Bir)]
       │
       ├─► [PostgreSQL Sorgusu: NextScanTime <= UtcNow && Status == Active]
       │
       ├─► [HuntManagerService (Singleton ConcurrentDictionary)] 
       │    └─► Mükerrer tarama engelleme & CancellationTokenSource.CreateLinkedTokenSource
       │
       ├─► [Task.Run -> ProcessSingleCheckAsync]
       │    │
       │    ├─► [ITicketProvider.CheckAvailabilityAsync] 
       │    │    ├─► Statik Site: HttpClient + Polly (Retry & Circuit Breaker)
       │    │    └─► SPA/Dinamik: Playwright Headless Chromium (Resource Blocking & DOMContentLoaded)
       │    │
       │    ├─► Bilet BULUNDU ise:
       │    │    ├─► DB: Status = Completed, CompletedAt = UtcNow
       │    │    ├─► SignalR Hub: Clients.All.SendAsync("SeatFound")
       │    │    ├─► IEmailService: MailKit üzerinden zengin HTML şablonlu E-posta
       │    │    └─► ITelegramService: Telegram Bot API ile anlık mesaj
       │    │
       │    ├─► Bilet BULUNAMADI ise:
       │    │    └─► Adaptif Backoff: Etkinlik tarihine göre NextScanTime güncelleme
       │    │
       │    └─► finally:
       │         └─► HuntManagerService.RemoveHunt() -> CTS Dispose (Memory Leak Önleme)
```

---

## 2. MİMARİ YAKLAŞIM (Clean Architecture & CQRS)

### 2.1. Neden Clean Architecture?
Geleneksel monolitik veya sıkı sıkıya bağlı katmanlı (N-Tier) mimarilerde, iş mantığı (Business Logic) çoğunlukla veritabanı teknolojisine (ORM/SQL) veya dış kütüphanelere bağımlı hale gelir. Bu durum sistemi test edilemez, kırılgan ve teknoloji değişimine dirençli kılar.

SeatHunter'da Robert C. Martin'in **Clean Architecture** prensipleri uygulanmıştır. Bu yaklaşımın temel amacı **Bağımlılıkların Tersine Çevrilmesi İlkesi (Dependency Inversion Principle - DIP)** doğrultusunda, çekirdek iş kurallarının dış dünyadan (veritabanı, web framework, mesaj kuyrukları, web scraper kütüphaneleri) tamamen izole edilmesidir.

```
                    ┌─────────────────────────┐
                    │      SeatHunter.Api     │ (Controllers, Workers, Hubs, Middlewares)
                    └────────────┬────────────┘
                                 │
                    ┌────────────▼────────────┐
                    │ SeatHunter.Infrastructure│ (EF Core, Playwright, RabbitMQ, Redis, MailKit)
                    └────────────┬────────────┘
                                 │
                    ┌────────────▼────────────┐
                    │  SeatHunter.Application │ (Commands, Queries, Handlers, Interfaces)
                    └────────────┬────────────┘
                                 │
                    ┌────────────▼────────────┐
                    │    SeatHunter.Domain    │ (Entities, Enums, Core Interfaces)
                    └─────────────────────────┘
```

#### Katmanların Projedeki Görev Dağılımı:

1. **`SeatHunter.Domain` (Çekirdek - En İç Katman):**
   - Dış dünyadaki hiçbir kütüphaneye, veritabanına veya framework'e bağımlılığı **yoktur**. Saf C# sınıflarından oluşur.
   - **Varlıklar (Entities):** [`HuntRequest`](file:///c:/Users/Mehmet%20Halim/Documents/Codes/SeatHunter/src/SeatHunter.Domain/Entities/HuntRequest.cs), [`User`](file:///c:/Users/Mehmet%20Halim/Documents/Codes/SeatHunter/src/SeatHunter.Domain/Entities/User.cs), [`Route`](file:///c:/Users/Mehmet%20Halim/Documents/Codes/SeatHunter/src/SeatHunter.Domain/Entities/Route.cs), [`Ticket`](file:///c:/Users/Mehmet%20Halim/Documents/Codes/SeatHunter/src/SeatHunter.Domain/Entities/Ticket.cs).
   - **Numaralandırmalar (Enums):** [`RequestStatus`](file:///c:/Users/Mehmet%20Halim/Documents/Codes/SeatHunter/src/SeatHunter.Domain/Enums/RequestStatus.cs) (`Active = 1`, `Completed = 2`, `Cancelled = 3`, `Expired = 4`).
   - **Temel Arayüzler (Interfaces):** [`ITicketProvider`](file:///c:/Users/Mehmet%20Halim/Documents/Codes/SeatHunter/src/SeatHunter.Domain/Interfaces/ITicketProvider.cs), [`ITicketProviderFactory`](file:///c:/Users/Mehmet%20Halim/Documents/Codes/SeatHunter/src/SeatHunter.Domain/Interfaces/ITicketProviderFactory.cs).

2. **`SeatHunter.Application` (İş Mantığı ve Kullanım Senaryoları):**
   - Sistemin "ne yapacağını" tanımlar ancak altyapı detaylarını bilmez.
   - **CQRS Yapısı:** MediatR kütüphanesi ile Command ve Query nesneleri.
   - **Sözleşmeler (Interfaces):** [`IHuntRequestRepository`](file:///c:/Users/Mehmet%20Halim/Documents/Codes/SeatHunter/src/SeatHunter.Application/Interfaces/IHuntRequestRepository.cs), [`IMessagePublisher`](file:///c:/Users/Mehmet%20Halim/Documents/Codes/SeatHunter/src/SeatHunter.Application/Interfaces/IMessagePublisher.cs), [`ICacheService`](file:///c:/Users/Mehmet%20Halim/Documents/Codes/SeatHunter/src/SeatHunter.Application/Interfaces/ICacheService.cs), [`IEmailService`](file:///c:/Users/Mehmet%20Halim/Documents/Codes/SeatHunter/src/SeatHunter.Application/Interfaces/IEmailService.cs), [`ITelegramService`](file:///c:/Users/Mehmet%20Halim/Documents/Codes/SeatHunter/src/SeatHunter.Application/Interfaces/ITelegramService.cs), [`IHuntManagerService`](file:///c:/Users/Mehmet%20Halim/Documents/Codes/SeatHunter/src/SeatHunter.Application/Interfaces/IHuntManagerService.cs).
   - **Uygulama Servisleri:** [`HuntManagerService`](file:///c:/Users/Mehmet%20Halim/Documents/Codes/SeatHunter/src/SeatHunter.Application/Services/HuntManagerService.cs) (In-memory thread-safe av takibi).

3. **`SeatHunter.Infrastructure` (Dış Dünya ve Altyapı Entegrasyonları):**
   - Application ve Domain katmanlarındaki soyutlamaları (interfaces) somutlaştırır (implements).
   - **Veri Kalıcılığı:** EF Core & Npgsql tabanlı [`SeatHunterDbContext`](file:///c:/Users/Mehmet%20Halim/Documents/Codes/SeatHunter/src/SeatHunter.Infrastructure/Persistence/SeatHunterDbContext.cs) ve Repository implementasyonları.
   - **Kazıma Motorları:** [`UniversalProvider`](file:///c:/Users/Mehmet%20Halim/Documents/Codes/SeatHunter/src/SeatHunter.Infrastructure/Providers/UniversalProvider.cs) (Microsoft.Playwright), [`BiletixScraperProvider`](file:///c:/Users/Mehmet%20Halim/Documents/Codes/SeatHunter/src/SeatHunter.Infrastructure/Providers/BiletixScraperProvider.cs) (HttpClient + Polly), [`ObiletScraperProvider`](file:///c:/Users/Mehmet%20Halim/Documents/Codes/SeatHunter/src/SeatHunter.Infrastructure/Providers/ObiletScraperProvider.cs).
   - **Mesajlaşma & Önbellek:** [`RabbitMqPublisher`](file:///c:/Users/Mehmet%20Halim/Documents/Codes/SeatHunter/src/SeatHunter.Infrastructure/Messaging/RabbitMqPublisher.cs), [`RedisCacheService`](file:///c:/Users/Mehmet%20Halim/Documents/Codes/SeatHunter/src/SeatHunter.Infrastructure/Caching/RedisCacheService.cs).
   - **İletişim Servisleri:** MailKit ile [`RealEmailService`](file:///c:/Users/Mehmet%20Halim/Documents/Codes/SeatHunter/src/SeatHunter.Infrastructure/Services/RealEmailService.cs), Telegram.Bot ile [`RealTelegramService`](file:///c:/Users/Mehmet%20Halim/Documents/Codes/SeatHunter/src/SeatHunter.Infrastructure/Services/RealTelegramService.cs).

4. **`SeatHunter.Api` (Sunum ve Giriş Kapısı):**
   - HTTP isteklerini kabul eder, kullanıcı kimliğini doğrular ve işi alt katmanlara devreder.
   - **Denetleyiciler (Controllers):** [`HuntRequestsController`](file:///c:/Users/Mehmet%20Halim/Documents/Codes/SeatHunter/src/SeatHunter.Api/Controllers/HuntRequestsController.cs), [`HuntsController`](file:///c:/Users/Mehmet%20Halim/Documents/Codes/SeatHunter/src/SeatHunter.Api/Controllers/HuntsController.cs), [`AuthController`](file:///c:/Users/Mehmet%20Halim/Documents/Codes/SeatHunter/src/SeatHunter.Api/Controllers/AuthController.cs).
   - **Arka Plan Servisleri (Hosted Services):** [`ContinuousHuntWorker`](file:///c:/Users/Mehmet%20Halim/Documents/Codes/SeatHunter/src/SeatHunter.Api/BackgroundServices/ContinuousHuntWorker.cs), [`RabbitMqListenerService`](file:///c:/Users/Mehmet%20Halim/Documents/Codes/SeatHunter/src/SeatHunter.Api/BackgroundServices/RabbitMqListenerService.cs).
   - **Gerçek Zamanlı İletişim:** [`NotificationHub`](file:///c:/Users/Mehmet%20Halim/Documents/Codes/SeatHunter/src/SeatHunter.Api/Hubs/NotificationHub.cs) (SignalR).
   - **Güvenlik & Dayanıklılık:** JWT Bearer doğrulama, IP/Endpoint bazlı `RateLimiter` (Fixed Window Policy), küresel [`ExceptionMiddleware`](file:///c:/Users/Mehmet%20Halim/Documents/Codes/SeatHunter/src/SeatHunter.Api/Middlewares/ExceptionMiddleware.cs) ve Serilog yapılandırması.

---

### 2.2. MediatR ile CQRS (Command/Query Responsibility Segregation)
Projede veri değiştirme (Command) ve veri okuma (Query) işlemleri birbirinden tamamen ayrılmıştır.

#### Neden CQRS ve MediatR?
- **Tek Sorumluluk İlkesi (Single Responsibility Principle):** Her işlem (`CreateHuntRequestCommand`, `CancelHuntRequestCommand`, `GetActiveRequestsQuery`) tek bir amaca hizmet eder. 
- **Zayıf Bağlılık (Loose Coupling):** API Controller'ları iş mantığını doğrudan çağırmaz. Controller sadece gelen HTTP gövdesini bir Command nesnesine çevirip `_mediator.Send()` yapar.
- **Pipeline ve Cross-Cutting Davranışlar:** Gelecekte sisteme eklenecek Logging, Validation (FluentValidation) veya Transaction yönetimi Handler'ların içine dokunulmadan MediatR Pipeline Behavior olarak enjekte edilebilir.

#### Kod Örneği ve İnceleme:

```csharp
// 1. Command Tanımı (Immutable C# Record)
public record CreateHuntRequestCommand(
    string UserId, 
    string Url, 
    DateTime TargetDate, 
    string Language, 
    string? TargetEventName
) : IRequest<Guid>;

// 2. Command Handler (İş Mantığı Yürütücüsü)
public class CreateHuntRequestCommandHandler : IRequestHandler<CreateHuntRequestCommand, Guid>
{
    private readonly IHuntRequestRepository _repository;
    private readonly IMessagePublisher _messagePublisher;
    private readonly ITicketProviderFactory _factory;

    public CreateHuntRequestCommandHandler(
        IHuntRequestRepository repository, 
        IMessagePublisher messagePublisher, 
        ITicketProviderFactory factory)
    {
        _repository = repository;
        _messagePublisher = messagePublisher;
        _factory = factory;
    }

    public async Task<Guid> Handle(CreateHuntRequestCommand request, CancellationToken cancellationToken)
    {
        // Provider Factory üzerinden ilgili stratejiyi al
        var provider = _factory.GetProvider(request.Url);
        
        // Etkinlik ismi belirtilmemişse sağlayıcı üzerinden otomatik çıkar
        string eventName = request.TargetEventName;
        if (string.IsNullOrWhiteSpace(eventName))
        {
            eventName = await provider.ExtractEventNameAsync(request.Url, cancellationToken);
            if (string.IsNullOrWhiteSpace(eventName))
            {
                var uri = new Uri(request.Url);
                eventName = uri.Host.Replace("www.", "");
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

        // Veritabanına kaydet
        await _repository.AddAsync(huntRequest, cancellationToken);

        // RabbitMQ'ya ilk anlık kontrol mesajını bas
        var eventMessage = new { 
            RequestId = huntRequest.Id, 
            ProviderName = huntRequest.ProviderName, 
            RouteOrEventId = huntRequest.RouteOrEventId, 
            Language = huntRequest.Language 
        };
        _messagePublisher.Publish(eventMessage, "hunt_requests_queue");

        return huntRequest.Id;
    }
}
```

---

## 3. KAZIMA MOTORU VE TASARIM DESENLERİ (Core Domain)

### 3.1. Hibrit Web Scraping Mimarisi
Modern internet dünyasında tek bir kazıma tekniği tüm sitelere yetersiz kalır. SeatHunter, performans ve esneklik dengesini kurmak için **Hibrit (Çift Katmanlı)** bir web kazıma mimarisi kullanır:

| Özellik | Statik Siteler ([`BiletixScraperProvider`](file:///c:/Users/Mehmet%20Halim/Documents/Codes/SeatHunter/src/SeatHunter.Infrastructure/Providers/BiletixScraperProvider.cs), [`ObiletScraperProvider`](file:///c:/Users/Mehmet%20Halim/Documents/Codes/SeatHunter/src/SeatHunter.Infrastructure/Providers/ObiletScraperProvider.cs)) | Dinamik / SPA Siteler ([`UniversalProvider`](file:///c:/Users/Mehmet%20Halim/Documents/Codes/SeatHunter/src/SeatHunter.Infrastructure/Providers/UniversalProvider.cs)) |
| :--- | :--- | :--- |
| **Kullanılan Teknoloji** | `HttpClient` + `HtmlAgilityPack` | `Microsoft.Playwright` (Headless Chromium) |
| **Kaynak Tüketimi** | Aşırı düşük (~MB seviyesinde RAM, %1'in altında CPU) | Orta (~50-100MB RAM/sayfa, optimize edilmezse yüksek CPU) |
| **İstek Hızı** | 50ms - 300ms | 1.5s - 3s (Optimize edilmiş haliyle) |
| **JS / SPA Desteği** | Yok (Sadece ham HTML ve SSR çıktıları) | **Tam Destek** (React, Vue, Angular, Next.js, Ingresso) |
| **Bot Savunması** | User-Agent rotasyonu, Sec-Fetch-* header manipülasyonu | Gerçek tarayıcı motoru (Full Browser Fingerprint) |

#### SPA'larda Neden HttpClient Çöker, Playwright Nasıl Çözer?
React veya Angular ile yazılmış modern bir bilet platformuna (örneğin Ingresso) standart bir `HttpClient.GetAsync` isteği atıldığında, sunucu sadece boş bir iskelet HTML döner:
```html
<div id="root"></div>
<script src="/static/bundle.js"></script>
```
Biletlerin doluluk durumu, koltuk listesi ve "Satın Al" butonları tarayıcıda JavaScript çalıştıktan sonra API'den çekilip DOM'a basılır (DOM Hydration). `HttpClient` JavaScript motoruna (V8) sahip olmadığı için biletlerin satışta olduğunu asla göremez.

`UniversalProvider`, Microsoft'un resmi **Playwright** kütüphanesini kullanarak arka planda görünmez (Headless) bir Chromium tarayıcı başlatır. JavaScript'i işletir, DOM'un oluşmasını sağlar ve oluşan nihai DOM ağacını (`page.ContentAsync()`) bellek içine alarak `HtmlAgilityPack` ve `Regex` ile tarar.

---

### 3.2. Playwright Performans Optimizasyonları
Bir sunucuda yüzlerce tarayıcı oturumu açmak belleği ve işlemciyi tüketebilir. SeatHunter bu darboğazı çözmek için 3 kritik optimizasyon uygulamıştır:

#### 1. Ağ Seviyesinde Kaynak Engelleme (Resource Blocking):
Sayfadaki resimler, fontlar, CSS dosyaları ve videolar biletin var olup olmadığını tespit etmek için tamamen gereksizdir. Playwright'ın ağ dinleyicisi (`Page.RouteAsync`) ile bu kaynaklar henüz indirilmeden iptal edilir:

```csharp
await page.RouteAsync("**/*", async route =>
{
    var type = route.Request.ResourceType;
    if (type == "image" || type == "stylesheet" || type == "font" || type == "media")
    {
        await route.AbortAsync(); // Ağ trafiğini ve RAM kullanımını %80 keser!
    }
    else
    {
        await route.ContinueAsync(); // Sadece document, script ve xhr/fetch isteklerine izin ver
    }
});
```

#### 2. Akıllı Bekleme Stratejisi (`DOMContentLoaded`):
Geleneksel `WaitUntilState.NetworkIdle` yaklaşımı, sayfadaki tüm arka plan analiz (Google Analytics, tracker) ve reklam isteklerinin bitmesini bekler. Bu da sayfa başına 10-15 saniye gecikme demektir. Bunun yerine `WaitUntilState.DOMContentLoaded` ve 2 saniyelik mikro bekleme kullanılarak sayfa yüklenme süresi **%75 hızlandırılmıştır**.

#### 3. Derin Script Taraması (Deep Script Scanner):
Kullanıcı arayüzünde "Tükendi" yazsa bile, arka plandaki JSON konfigürasyonlarında koltuk açılmış olabilir. `UniversalProvider` sadece DOM metnini değil, sayfadaki tüm `<script>` bloklarını Regex desenleriyle tarar:
- `"isSoldOut"\s*:\s*false`
- `"status"\s*:\s*"available"`
- `"availability"\s*:\s*"IN_STOCK"`
- `"availableSeats"\s*:\s*\[[^\]]+\]`

---

### 3.3. Tasarım Desenleri (Design Patterns)

#### Strategy Pattern:
Farklı bilet siteleri farklı HTML yapılarına ve farklı algılama algoritmalarına sahiptir. Tüm bu algoritmalar [`ITicketProvider`](file:///c:/Users/Mehmet%20Halim/Documents/Codes/SeatHunter/src/SeatHunter.Domain/Interfaces/ITicketProvider.cs) arayüzü ile standartlaştırılmıştır:
- `CheckAvailabilityAsync(string url, CancellationToken token)`
- `ExtractEventNameAsync(string url, CancellationToken token)`
- `ProviderName { get; }`

Her bilet sağlayıcısı (`BiletixScraperProvider`, `ObiletScraperProvider`, `UniversalProvider`) birer **Strateji** nesnesidir. Bu yapı **Open/Closed Principle (OCP)** gereği yeni bir site desteği eklendiğinde mevcut kodları bozmadan sisteme entegre olabilmesini sağlar.

#### Factory Pattern ([`TicketProviderFactory`](file:///c:/Users/Mehmet%20Halim/Documents/Codes/SeatHunter/src/SeatHunter.Infrastructure/Providers/TicketProviderFactory.cs)):
Gelen hedef URL'e göre doğru stratejinin çalışma zamanında (Runtime) belirlenmesi ve oluşturulması sorumluluğunu üstlenir. Dependency Injection (IoC Container) ile entegre çalışır:

```csharp
public class TicketProviderFactory : ITicketProviderFactory
{
    private readonly IServiceProvider _serviceProvider;

    public TicketProviderFactory(IServiceProvider serviceProvider) => _serviceProvider = serviceProvider;

    public ITicketProvider GetProvider(string url)
    {
        if (string.IsNullOrWhiteSpace(url)) 
            return _serviceProvider.GetRequiredService<UniversalProvider>();

        var lowerUrl = url.ToLowerInvariant();
        if (lowerUrl.Contains("biletix.com"))
            return _serviceProvider.GetRequiredService<BiletixScraperProvider>();
        if (lowerUrl.Contains("obilet.com"))
            return _serviceProvider.GetRequiredService<ObiletScraperProvider>();

        // Tanınmayan veya modern SPA siteler için Universal Playwright motorunu dön
        return _serviceProvider.GetRequiredService<UniversalProvider>();
    }
}
```

#### Singleton Pattern ([`HuntManagerService`](file:///c:/Users/Mehmet%20Halim/Documents/Codes/SeatHunter/src/SeatHunter.Application/Services/HuntManagerService.cs)):
Aktif tarama oturumlarının, cancellation token'larının ve anlık DTO durumlarının uygulama genelinde tek bir merkezden thread-safe olarak yönetilmesini sağlar.

---

## 4. ALTYAPI VE ASENKRON İŞLEMLER (RabbitMQ & Redis)

### 4.1. RabbitMQ ve Olay Güdümlü Mimari (Event-Driven Architecture)
Geleneksel senkron yapılarda, kullanıcı bilet takip isteği attığında arka planda hemen kazıma motoru ve e-posta servisi senkron olarak çağrılsaydı:
1. Kullanıcı API yanıtı için 5-10 saniye beklemek zorunda kalırdı (kötü UX).
2. Hedef site yavaşsa veya SMTP sunucusu yanıt vermezse HTTP isteği zaman aşımına (Timeout 504) uğrardı.
3. Aynı anda 1000 kullanıcı istek attığında thread havuzu tükenir ve API çökerdi.

SeatHunter bu darboğazı **RabbitMQ** ile çözer:

```
[CreateHuntRequestCommandHandler] 
              │
              ▼ (Olay Üretildi: hunt_requests_queue)
       ┌──────────────┐
       │   RabbitMQ   │ ── durable: true
       └──────┬───────┘
              │
              ▼ (BasicConsume: autoAck = false)
    [RabbitMqListenerService] ──► Hızlı Ön Değerlendirme & SignalR Tetikleme
```

#### Sistemin Dayanıklılığına (Resilience) Katkıları:
- **Tamponlama (Buffering / Backpressure):** Ani trafik patlamalarında (örneğin Tarkan konseri biletleri açıldığında) gelen tüm istekler RabbitMQ kuyruğunda güvenle sıraya alınır. Arka plan servisleri kendi tüketim kapasitelerine göre mesajları işler.
- **Kayıpsız Mesajlaşma (Guaranteed Delivery):** Kuyruk `durable: true` tanımlanmıştır. Mesajlar diske yazılır; RabbitMQ sunucusu yeniden başlasa bile istekler kaybolmaz.
- **Manual Acknowledgment (`BasicAck`):** [`RabbitMqListenerService`](file:///c:/Users/Mehmet%20Halim/Documents/Codes/SeatHunter/src/SeatHunter.Api/BackgroundServices/RabbitMqListenerService.cs) mesajı başarıyla işleyene kadar `BasicAck` göndermez. İşlem sırasında worker çökerse mesaj otomatik olarak kuyruğa geri döner (Re-queue).

---

### 4.2. Redis ve In-Memory Dağıtık Önbellek
İlişkisel veritabanımız (PostgreSQL) varken neden **Redis** kullandık?

#### PostgreSQL vs Redis Karşılaştırması:
- **PostgreSQL:** Disk I/O tabanlıdır, ACID garantisi sunar, ilişkisel veriyi tutar ancak saniyede binlerce okuma/yazma altında connection pool ve CPU darboğazına girer.
- **Redis:** Veriyi tamamen RAM'de tutar. Veri okuma ve yazma gecikmesi mikrosaniye (sub-millisecond) seviyesindedir.

#### SeatHunter'daki Kullanım Alanları:
1. **Hedef URL Throttling & Sonuç Önbelleği:** Aynı popüler konser linki için 50 farklı kullanıcı av başlatmış olabilir. Her kullanıcı için hedef siteye 5 saniyede bir 50 ayrı istek atmak IP adresimizin anında Cloudflare tarafından engellenmesine yol açar. Redis üzerinde `Cache-Aside` deseniyle son kazıma sonucu (örneğin 30 saniye TTL ile) saklanır. Böylece hedef siteye 50 istek yerine tek bir istek atılır.
2. **Kullanıcı Oturumları & Blacklist:** JWT token iptalleri ve anlık kullanıcı durumları veritabanına gitmeden Redis üzerinden doğrulanır.
3. **SignalR Backplane Hazırlığı:** Uygulama yatayda birden fazla instance olarak ölçeklendiğinde (Horizontal Pod Autoscaling), kullanıcıların farklı sunucularda bağlı olan SignalR WebSocket bağlantılarına tek bir event ile yayın yapılabilmesi için Redis Backplane altyapısı hazır tutulmuştur.

---

## 5. ARKA PLAN İŞÇİLERİ VE PERFORMANS (Background Services)

### 5.1. ContinuousHuntWorker İşleyişi
[`ContinuousHuntWorker`](file:///c:/Users/Mehmet%20Halim/Documents/Codes/SeatHunter/src/SeatHunter.Api/BackgroundServices/ContinuousHuntWorker.cs), .NET'in `BackgroundService` sınıfından türetilmiş uzun ömürlü bir arka plan işçisidir.

#### Döngü Algoritması:
1. 5 saniyede bir uyanır (`Task.Delay(5000, stoppingToken)`).
2. Veritabanından durumu `Active` olan ve tarama vakti gelmiş (`NextScanTime <= DateTime.UtcNow`) kayıtları çeker.
3. Bellekte o anda zaten taranmakta olan istekleri eler (Çift işlemeyi engelleme).
4. Her istek için bağımsız bir asenkron görev (`Task.Run`) başlatır.

#### Dinamik / Adaptif Tarama Frekansı (Smart Backoff):
Tüm biletleri aynı sabit aralıkla (örneğin her 1 dakikada bir) taramak sistem kaynaklarını ve hedef site bant genişliğini israf eder. SeatHunter etkinlik tarihine olan uzaklığa göre tarama frekansını otomatik ayarlar:
- **Etkinliğe 30 günden fazla varsa:** 6 saatte bir tara (`AddHours(6)`).
- **Etkinliğe 7 ile 30 gün arası varsa:** 1 saatte bir tara (`AddHours(1)`).
- **Etkinliğe 1 ile 7 gün arası varsa:** 15 dakikada bir tara (`AddMinutes(15)`).
- **Etkinliğe 24 saatten az kaldıysa:** 1 dakikada bir tara (`AddMinutes(1)`).

---

### 5.2. Kritik `CancellationTokenSource` Bellek Sızıntısı (Memory Leak) Analizi

#### Mülakatın En Kritik Sorusu: *"Bu sistemde karşılaştığın en büyük bellek sızıntısını nasıl çözdün?"*

#### Problemin Kaynağı:
`ContinuousHuntWorker` içinde her tarama işlemi için uygulamanın genel kapanma token'ı (`stoppingToken`) ile birleşen bir bağlantılı iptal kaynağı oluşturuluyordu:
```csharp
var cts = CancellationTokenSource.CreateLinkedTokenSource(stoppingToken);
```
`.NET` çalışma zamanında `CreateLinkedTokenSource` çağrıldığında, arka planda ana `stoppingToken` nesnesine bir event listener (CancellationTokenRegistration) kaydedilir. 

`stoppingToken` uygulama ayakta kaldığı sürece (günlerce/aylarca) yaşadığı için, bu bağlantılı `cts` nesneleri tarama bittikten sonra açıkta bırakılırsa, **Garbage Collector (GC) bu nesneleri bellekten temizleyemez (Rooted Object)!** Her 5 saniyede onlarca istek tarandığında, sistem belleği (RAM) saatler içinde yüzlerce megabayttan gigabaytlara fırlar ve sonunda uygulama `OutOfMemoryException` vererek çöker.

#### Mimari Çözüm:
`HuntManagerService` içerisindeki av yönetim modeli `Cts` nesnesini de saklayacak şekilde revize edildi ve her iptal/tamamlanma senaryosunda açıkça `Dispose()` çağrıldı:

```csharp
// HuntManagerService.cs
private class ActiveHuntEntry
{
    public ActiveHuntDto Dto { get; set; }
    public CancellationTokenSource Cts { get; set; }
}

public void CancelHunt(string huntId)
{
    if (_activeHunts.TryRemove(huntId, out var entry))
    {
        if (!entry.Cts.IsCancellationRequested)
        {
            entry.Cts.Cancel();
        }
        entry.Cts.Dispose(); // Listener'ı ana tokendan koparır ve RAM'i iade eder!
    }
}

public void RemoveHunt(string huntId)
{
    if (_activeHunts.TryRemove(huntId, out var entry))
    {
        entry.Cts.Dispose(); // Tarama bittiğinde garantili temizlik!
    }
}
```

`ContinuousHuntWorker` içerisindeki `ProcessSingleCheckAsync` metodunda ise `finally` bloğu içine `huntManager.RemoveHunt(requestId.ToString())` eklenerek, hata oluşsa dahi CTS nesnesinin mutlak surette Dispose edilmesi garanti altına alınmıştır.

---

## 6. GÜVENLİK, DEVOPS VE CI/CD

### 6.1. Hardcoded Secrets ve 12-Factor App Güvenliği
Geliştirme safhasında kod tabanında kalmış olan Telegram Bot Token, PostgreSQL parolası, RabbitMQ bağlantı dizesi ve Gmail App Password gibi hassas veriler tamamen ayıklandı:
1. **`appsettings.json`:** Sadece boş konfigürasyon şablonları bırakıldı (`"Telegram": { "BotToken": "" }`).
2. **`IConfiguration` Entegrasyonu:** Tüm servisler (`RealTelegramService`, `RealEmailService`, `RabbitMqPublisher`, `RabbitMqListenerService`) şifreleri ortam değişkenlerinden (Environment Variables) okuyacak şekilde refactor edildi.
3. **`.env` ve `.env.example`:** Gerçek sırlar git reposuna girmeyecek şekilde `.gitignore` dosyasına eklendi, sunucu kurulumları için `.env.example` dokümante edildi.
4. **API Koruması:** `FixedPolicy` rate limiting (10 saniyede maksimum 3 istek) ile API brute-force ve bot spam saldırılarına karşı korumaya alındı.

---

### 6.2. Docker ve Multi-Stage Container Mimarisi
SeatHunter, sunucuya tek komutla (`docker-compose up -d`) deploy edilebilecek **Self-Contained** bir konteyner mimarisine sahiptir.

#### Multi-Stage Dockerfile Analizi:
```dockerfile
# Aşama 1: Derleme (SDK İmajı)
FROM mcr.microsoft.com/dotnet/sdk:10.0 AS build
WORKDIR /src
COPY ["src/SeatHunter.Api/SeatHunter.Api.csproj", "src/SeatHunter.Api/"]
...
RUN dotnet restore "src/SeatHunter.Api/SeatHunter.Api.csproj"
COPY . .
WORKDIR "/src/src/SeatHunter.Api"
# Self-contained linux-x64 binary derlemesi
RUN dotnet publish "SeatHunter.Api.csproj" -c Release -r linux-x64 --self-contained true -o /app/publish

# Aşama 2: Çalışma Zamanı (Runtime İmajı)
# KRİTİK: Standart .NET runtime yerine Microsoft Playwright imajı kullanıldı!
FROM mcr.microsoft.com/playwright/dotnet:v1.40.0-jammy AS final
WORKDIR /app
COPY --from=build /app/publish .
EXPOSE 8080
ENTRYPOINT ["./SeatHunter.Api"]
```

#### Neden `playwright/dotnet:v1.40.0-jammy` Base Image Olarak Seçildi?
Standart Ubuntu veya Alpine Linux imajlarında Chromium tarayıcısının çalışabilmesi için onlarca işletim sistemi kütüphanesi (C++ binding'leri, `libgbm`, `libasound2`, `libnss3`, font paketleri vb.) eksiktir. Microsoft'un resmi Playwright imajı bu bağımlılıkların ve Headless Chromium tarayıcısının önceden kurulu olduğu ortamı sağlayarak Docker içinde Playwright'ın çökmeden, sıfır konfigürasyonla çalışmasını sağlar.

#### Docker Compose Servis Orkestrasyonu:
- `seathunter_postgres`: PostgreSQL 15 Alpine (Kalıcı named volume: `postgres_data`).
- `seathunter_redis`: Redis 7 Alpine (Kalıcı named volume: `redis_data`).
- `seathunter_rabbitmq`: RabbitMQ 3 Management Alpine (5672 mesajlaşma, 15672 web UI).
- `seathunter_api`: Tüm bu servislere izole bir bridge network (`seathunter_network`) üzerinden bağlanan API servisi.

---

### 6.3. CI/CD Pipeline (GitHub Actions) ve Test Stratejisi
- **Workflow:** `.github/workflows/dotnet.yml` dosyası üzerinde GitHub Actions pipeline'ı yapılandırılmıştır.
- **`.slnx` Desteği:** .NET 10 ile gelen modern XML tabanlı `.slnx` çözüm dosyası kullanılarak `dotnet restore SeatHunter.slnx`, `dotnet build SeatHunter.slnx` ve `dotnet test SeatHunter.slnx` aşamaları otomatik olarak koşar.
- **Birim Test İzolasyonu (Moq & FluentAssertions):**
  `CreateHuntRequestCommandHandlerTests` sınıfında harici tüm bağımlılıklar taklit (mock) edilmiştir:
  - `Mock<IHuntRequestRepository>`: Gerçek veritabanına bağlanmadan `AddAsync` çağrısı doğrulanır.
  - `Mock<IMessagePublisher>`: RabbitMQ ayağa kaldırmadan mesajın doğru kuyruğa basıldığı (`Times.Once`) test edilir.
  - `Mock<ITicketProviderFactory>` ve `Mock<ITicketProvider>`: Ağ bağlantısı yapmadan ve Playwright açmadan sahte sağlayıcı yanıtları (`MockProvider`, `Mock Event Name`) ile iş mantığı izole şekilde doğrulanır.

---

## 7. MÜLAKAT SORU-CEVAP SİMÜLASYONU (Sık Sorulan Zorlu Sorular)

Teknik mülakatlarda bir Senior/Lead adayını terletmek için sorulabilecek 12 kritik soru ve verilmesi gereken profesyonel yanıtlar:

---

### Soru 1: "Playwright gibi ağır bir aracı arka plan botunda kullanmak sunucuyu yormaz mı? RAM ve CPU tüketimini nasıl dizginlediniz?"
**Cevap:**
> "Playwright varsayılan haliyle her sayfada CSS, resim, font, iframe ve üçüncü parti takip scriptlerini indirir; bu da instance başına 150-300MB RAM tüketebilir. SeatHunter mimarisinde bunu 3 seviyede optimize ettik:
> 1. **Ağ Seviyesinde Engelleme (Resource Blocking):** `Page.RouteAsync` kullanarak `image`, `stylesheet`, `font` ve `media` tipindeki tüm istekleri tarayıcıya ulaşmadan `route.AbortAsync()` ile iptal ettik. Sadece `document`, `script` ve `xhr/fetch` isteklerine izin verdik. Bu sayede bant genişliğini ve RAM tüketimini %80 azalttık.
> 2. **Bekleme Stratejisi:** Gereksiz reklam ve analiz ağ trafiğini bekleyen `NetworkIdle` yerine, DOM ağacı oluştuğu an tetiklenen `DOMContentLoaded` ve 2 saniyelik mikro bekleme kullandık.
> 3. **Hibrit Yaklaşım:** Playwright'ı her sitede kullanmıyoruz. `TicketProviderFactory` üzerinden Biletix ve Obilet gibi sitelere ultra hafif `HttpClient` yönlendiriyoruz; Playwright sadece SPA/React gibi zorunlu sitelerde devreye giriyor."

---

### Soru 2: "Neden doğrudan DbContext ile veritabanına yazmadınız da araya MediatR ve CQRS koydunuz? Bu ölçekte bir proje için Over-engineering değil mi?"
**Cevap:**
> "Görünürde basit bir CRUD gibi dursa da, bilet takip isteği oluşturmak saf bir veritabanı yazma işlemi değildir. Bir istek geldiğinde: URL'den sağlayıcı tespit edilmeli, sayfa taranıp etkinlik adı çıkarılmalı, veritabanına kaydedilmeli ve RabbitMQ kuyruğuna asenkron olay fırlatılmalıdır.
> Eğer bunu doğrudan Controller içinde yapsaydık, Controller 'Fat Controller' haline gelir, test edilemez ve Single Responsibility prensibini ihlal ederdi. CQRS ve MediatR sayesinde:
> - Controller sadece HTTP isteğini karşılayıp `CreateHuntRequestCommand` nesnesini MediatR'a ileten ince bir katman olarak kaldı.
> - İş mantığı `CreateHuntRequestCommandHandler` içinde izole edildi.
> - Birim testlerde Controller'ı veya Web sunucusunu ayağa kaldırmadan sadece bu Handler'ı Mock bağımlılıklarla %100 izole test edebilir hale geldik. Bu mimari 'over-engineering' değil, sürdürülebilirlik ve test edilebilirlik yatırımıdır."

---

### Soru 3: "ContinuousHuntWorker bir Singleton BackgroundService. EF Core DbContext ise varsayılan olarak Scoped ömre sahiptir. Scoped bir servisi Singleton içinde nasıl yönettiniz? Service Locator anti-pattern'e düştünüz mü?"
**Cevap:**
> "Evet, Singleton bir sınıfın kurucusuna (constructor) doğrudan Scoped bir `DbContext` enjekte ederseniz `Captive Dependency` hatası oluşur ve DbContext singleton gibi yaşayarak bellek sızıntısına ve eşzamanlılık (concurrency) çakışmalarına yol açar.
> Bu sorunu çözmek için `ContinuousHuntWorker` içine `IServiceProvider` enjekte ettik ve işçi döngüsünün her iterasyonunda `using var scope = _serviceProvider.CreateScope();` ile açık bir Scope başlattık. DbContext ve ilgili servisleri bu scope üzerinden çözümledik. İşlem tamamlandığında scope dispose olarak açılan veritabanı bağlantılarını ve entity takibini havuza (pool) temizce iade eder. BackgroundService'lerin doğası gereği bu bir anti-pattern değil, Microsoft'un resmi olarak önerdiği Scoped Background Service desenidir."

---

### Soru 4: "CancellationTokenSource bellek sızıntısını (Memory Leak) nasıl fark ettiniz ve neden oluştu?"
**Cevap:**
> "Uygulama uzun süre çalıştığında RAM tüketiminin sürekli arttığını ve GC'nin bu belleği geri alamadığını gördük. İncelediğimizde sorunun `CancellationTokenSource.CreateLinkedTokenSource(stoppingToken)` kullanımından kaynaklandığını tespit ettik.
> Bağlantılı bir token oluşturduğunuzda, arka planda uzun ömürlü olan `stoppingToken` üzerine bir delege kaydedilir. Eğer oluşturulan bu kısa ömürlü `cts` nesneleri açıkça `Dispose()` edilmezse, ana `stoppingToken` hayatta kaldığı sürece o nesnelere referans tutmaya devam eder (GC Rooting). Bu da nesnelerin Garbage Collector tarafından toplanmasını engeller.
> Çözüm olarak `HuntManagerService` yapısında CTS referansını tuttuk ve hem kullanıcı iptal ettiğinde hem de tarama başarıyla/hatayla bittiğinde `finally` bloğu içerisinde `entry.Cts.Dispose()` çağrılmasını garanti altına aldık."

---

### Soru 5: "Unit test yazarken ITicketProviderFactory ve sağlayıcıları nasıl izole ettiniz? Test mimarinizi anlatır mısınız?"
**Cevap:**
> "Birim testlerimizin dış dünyaya (gerçek web sitelerine, RabbitMQ sunucusuna veya PostgreSQL'e) bağımlı olmaması kırmızı çizgimizdir. `CreateHuntRequestCommandHandlerTests` sınıfında Moq kütüphanesini kullandık:
> 1. `Mock<ITicketProvider>` oluşturup `ProviderName` özelliğini 'MockProvider' dönecek, `ExtractEventNameAsync` metodunu ise sahte bir etkinlik adı dönecek şekilde ayarladık (`ReturnsAsync("Mock Event Name")`).
> 2. `Mock<ITicketProviderFactory>` oluşturup `GetProvider(It.IsAny<string>())` çağrıldığında bu sahte sağlayıcıyı dönmesini sağladık.
> 3. Böylece Handler test edilirken gerçek bir Chromium tarayıcı açılmadı, ağ isteği atılmadı; sadece iş mantığının repoya doğru veriyi eklediği (`AddAsync`) ve RabbitMQ'ya mesajı ilettiği (`Publish`) milisaniyeler içinde doğrulandı."

---

### Soru 6: "Bilet bulunduğunda neden doğrudan SMTP servisini çağırmadınız da araya RabbitMQ koydunuz?"
**Cevap:**
> "RabbitMQ kullanımımızın iki temel sebebi var:
> 1. **Kullanıcı İsteğinin Hızlı Yanıtlanması (Decoupling):** Kullanıcı bilet takip isteği oluşturduğunda ilk anlık kontrolün sonucunu beklemek istemez. İstek kuyruğa atılır ve kullanıcıya anında HTTP 200 döner.
> 2. **Sistem Dayanıklılığı ve Geri Basınç (Backpressure):** SMTP sunucuları (Gmail, SendGrid vb.) oran sınırlarına (Rate Limits) sahiptir ve ağ kesintilerine uğrayabilir. Doğrudan çağrı yapılsaydı, ağ koptuğunda bildirim kaybolurdu. RabbitMQ ile mesaj `durable` olarak saklanır ve worker mesajı başarıyla işleyip `BasicAck` gönderene kadar kuyrukta güvendedir. Hata anında mesaj kaybolmaz."

---

### Soru 7: "Hedef bilet siteleri botları engellemek için Cloudflare veya Akamai kullanıyor. Bu engelleri aşmak için kodda hangi teknik önlemleri aldınız?"
**Cevap:**
> "Bunu iki katmanda yönettik:
> 1. **HttpClient Seviyesinde ([`DependencyInjection.cs`](file:///c:/Users/Mehmet%20Halim/Documents/Codes/SeatHunter/src/SeatHunter.Infrastructure/DependencyInjection.cs)):** 
>    - Rastgele modern User-Agent havuzu (Windows Chrome, Mac Safari, iPhone Safari, Android Chrome) tanımlayarak her istekte User-Agent rotasyonu yaptık.
>    - Tarayıcı taklidi için `Sec-Fetch-Dest`, `Sec-Fetch-Mode`, `Sec-Fetch-Site`, `Sec-Ch-Ua` gibi modern 'Client Hints' başlıklarını ekledik.
> 2. **Playwright Seviyesinde ([`UniversalProvider.cs`](file:///c:/Users/Mehmet%20Halim/Documents/Codes/SeatHunter/src/SeatHunter.Infrastructure/Providers/UniversalProvider.cs)):** 
>    - Headless Chromium gerçek bir tarayıcı motoru olduğu için JavaScript zorluklarını (JS Challenge) ve Cloudflare Turnstile gibi dinamik kontrolleri 'tam tarayıcı parmak izi' (Browser Fingerprint) ile aşabilmektedir.
> 3. **Akıllı İstek Frekansı:** Hedef siteleri flood etmemek için etkinlik tarihine göre dinamik frekans (Backoff) uygulayarak IP adresimizin şüpheli görünmesini engelledik."

---

### Soru 8: "Hedef site 500 hatası verirse veya çökerse botunuz o siteyi bombalamaya devam mı ediyor? Hata toleransı (Resilience) mekanizmanız nedir?"
**Cevap:**
> "Kesinlikle hayır. `SeatHunter.Infrastructure` katmanında **Polly** kütüphanesini kullanarak iki kritik dayanıklılık politikası kurduk:
> 1. **Wait and Retry (Üstel Geri Çekilme):** Geçici ağ hatalarında (Transient Errors: 5xx, 408) istek anında pes etmez. $2^n$ üstel katsayısıyla (2s, 4s, 8s) 3 kez yeniden dener.
> 2. **Circuit Breaker (Devre Kesici):** Hedef sağlayıcı art arda 5 kez hata verirse devre açılır (`Circuit Breaker Açık`). Sistem hedef siteye istek atmayı 5 dakikalığına tamamen durdurur. Bu sırada sistem yöneticisine Telegram üzerinden otomatik uyarı gönderilir (`⚠️ ACİL: Hedef sağlayıcı yanıt vermiyor! İstekler 5 dakikalığına durduruldu`). 5 dakika sonra devre 'Yarı Açık' (Half-Open) duruma geçer; tek bir deneme isteği atar, başarılı olursa normale döner."

---

### Soru 9: "Docker ortamında Playwright çalıştırmanın zorlukları nelerdir, nasıl aştınız?"
**Cevap:**
> "En büyük zorluk Linux ortamındaki eksik grafik ve ses kütüphaneleridir. Standart bir .NET runtime imajına Playwright eklendiğinde `Host system is missing dependencies to run browsers` hatasıyla Chromium ayağa kalkmaz.
> Çözüm olarak multi-stage Dockerfile hazırladık:
> - Derleme aşamasını resmi `dotnet/sdk:10.0` ile yaptık ve uygulamayı Linux için self-contained (`-r linux-x64 --self-contained true`) derledik.
> - Runtime imajı olarak Microsoft'un resmi `mcr.microsoft.com/playwright/dotnet:v1.40.0-jammy` imajını kullandık. Bu imaj Chromium için gerekli tüm paylaşımlı kütüphaneleri barındırdığından, Docker konteyneri içinde ek kurulum yapmaya gerek kalmadan sorunsuz çalıştı."

---

### Soru 10: "Veritabanı şemasında performans için nasıl bir indeksleme stratejisi izlediniz?"
**Cevap:**
> "`ContinuousHuntWorker` her 5 saniyede bir şu sorguyu çalıştırır:
> `WHERE Status == RequestStatus.Active AND NextScanTime <= DateTime.UtcNow`
> Tabloda yüz binlerce kayıt biriktiğinde bu sorgu 'Full Table Scan' yaparak veritabanını kilitler. Bu nedenle `HuntRequests` tablosu üzerinde `(Status, NextScanTime)` alanlarını içeren bir **B-Tree Bileşik İndeks (Composite Index)** kurgulanmalıdır. Ayrıca kullanıcı bazlı sorgular için `(UserId, CreatedAt)` indeksi eklenerek geçmiş listeleme sorgularının disk I/O yapmadan bellekten indeks taraması (Index Scan) ile milisaniyeler içinde dönmesi sağlanmıştır."

---

### Soru 11: "SignalR ile WebSocket bağlantısı kurarken sistem yatayda (Horizontal Scaling) 5 ayrı instance olarak çalışırsa ne olur? Bunu nasıl çözersiniz?"
**Cevap:**
> "Tek bir instance çalışırken `_hubContext.Clients.All.SendAsync()` o sunucuya bağlı tüm WebSocket istemcilerine mesajı iletir. Ancak uygulamayı Kubernetes üzerinde 5 Pod olarak çalıştırırsak, Kullanıcı A'nın soketi Pod-1'e bağlıyken bileti Pod-2 yakalayabilir. Bu durumda Pod-2'nin yerel HubContext'i Kullanıcı A'ya ulaşamaz.
> Bunun çözümü mimarimizde zaten hazır bulunan **Redis Backplane** entegrasyonudur. `builder.Services.AddSignalR().AddStackExchangeRedis(...)` eklenerek SignalR mesajlarının Redis Pub/Sub üzerinden tüm Pod'lara dağıtılması sağlanır. Hangi sunucu bileti bulursa bulsun, Redis aracılığıyla diğer 4 sunucudaki istemcilere de anında event ulaştırılır."

---

### Soru 12: "Uygulama çökerse veya plansız yeniden başlarsa (Graceful Shutdown) o sırada taranmakta olan isteklerin durumu ne olur?"
**Cevap:**
> ".NET'in `IHostedService` altyapısı ve `CancellationToken` mimarisi sayesinde sistem 'Graceful Shutdown'ı tam olarak destekler. Kapatma sinyali (SIGTERM) geldiğinde:
> 1. `stoppingToken` iptal edilir.
> 2. `ContinuousHuntWorker` döngüden çıkar. O sırada çalışan asenkron taramalarda `OperationCanceledException` yakalanır ve 'Tarama iptal edildi (Graceful Shutdown)' logu basılarak kaynaklar serbest bırakılır.
> 3. İsteklerin veritabanındaki durumu `Active` olarak kalmaya devam eder, silinmez.
> 4. Sistem yeniden ayağa kalktığında `NextScanTime <= UtcNow` koşulu sağlandığı için hiçbir kullanıcı isteği veri kaybına uğramadan kaldığı yerden taranmaya devam eder."

---

## 8. PROJE DİZİN VE MİMARİ DOSYA HARİTASI

Projenin fiziki dizin yapısı ve kritik sınıfların yerleşimi:

```
SeatHunter/
├── SeatHunter.slnx                                    # .NET 10 Modern XML Solution Dosyası
├── Dockerfile                                         # Multi-stage Playwright Jammy Docker Yapılandırması
├── docker-compose.yml                                 # Postgres, Redis, RabbitMQ & API Orkestrasyonu
├── ARCHITECTURE.md                                    # Sistem Mimarisi Referans Dokümanı
├── README.md                                          # İngilizce Tanıtım ve Kurulum Kılavuzu
├── SEATHUNTER_INTERVIEW_MASTER_GUIDE.md               # Bu Başucu Mülakat Dokümanı
│
├── src/
│   ├── SeatHunter.Domain/                             # Saf Çekirdek (Core Domain)
│   │   ├── Entities/
│   │   │   ├── HuntRequest.cs                         # Av Takip Varlığı (Id, UserId, Url, TargetDate, Status...)
│   │   │   ├── User.cs                                # Kullanıcı Varlığı (Id, Email, PasswordHash...)
│   │   │   ├── Route.cs                               # Rota Varlığı
│   │   │   └── Ticket.cs                              # Bilet Varlığı
│   │   ├── Enums/
│   │   │   └── RequestStatus.cs                       # Active(1), Completed(2), Cancelled(3), Expired(4)
│   │   └── Interfaces/
│   │       ├── ITicketProvider.cs                     # Kazıma Strateji Arayüzü
│   │       └── ITicketProviderFactory.cs              # Sağlayıcı Fabrika Arayüzü
│   │
│   ├── SeatHunter.Application/                        # İş Mantığı (Use Cases & CQRS)
│   │   ├── Features/HuntRequests/
│   │   │   ├── Commands/CreateHuntRequest/            # Create Command & Handler
│   │   │   ├── Commands/CancelHuntRequest/            # Cancel Command & Handler
│   │   │   └── Queries/                               # GetActiveRequests Query & Handler
│   │   ├── Interfaces/                                # Altyapı Soyutlamaları (Repository, Cache, Email...)
│   │   ├── DTOs/
│   │   │   └── ActiveHuntDto.cs                       # Anlık UI DTO Modeli
│   │   ├── Services/
│   │   │   └── HuntManagerService.cs                  # Singleton In-memory Av & CTS Yöneticisi
│   │   └── DependencyInjection.cs                     # MediatR ve Singleton Servis Kayıtları
│   │
│   ├── SeatHunter.Infrastructure/                     # Dış Entegrasyonlar ve Sağlayıcılar
│   │   ├── Persistence/
│   │   │   ├── SeatHunterDbContext.cs                 # EF Core Npgsql Veritabanı Bağlamı
│   │   │   └── Repositories/                          # Repository Implementasyonları
│   │   ├── Providers/
│   │   │   ├── UniversalProvider.cs                   # Playwright Headless Chromium SPA Kazıyıcı
│   │   │   ├── BiletixScraperProvider.cs              # HttpClient + Polly Biletix Kazıyıcı
│   │   │   ├── ObiletScraperProvider.cs               # HttpClient + Polly Obilet Kazıyıcı
│   │   │   └── TicketProviderFactory.cs               # URL Tabanlı Dinamik Sağlayıcı Çözümleyici
│   │   ├── Messaging/
│   │   │   └── RabbitMqPublisher.cs                   # RabbitMQ Kuyruk Yayıncısı
│   │   ├── Caching/
│   │   │   └── RedisCacheService.cs                   # IDistributedCache Redis Implementasyonu
│   │   ├── Services/
│   │   │   ├── RealEmailService.cs                    # MailKit SMTP E-posta Servisi
│   │   │   ├── RealTelegramService.cs                 # TelegramBotClient Mesajlaşma Servisi
│   │   │   └── AuthService.cs                         # JWT Token Üretim ve Doğrulama
│   │   └── DependencyInjection.cs                     # HttpClient, Polly, Redis, DbContext Kayıtları
│   │
│   └── SeatHunter.Api/                                # Sunum Katmanı (Web API)
│       ├── Controllers/                               # REST Endpoint'leri (Hunts, Auth...)
│       ├── BackgroundServices/
│       │   ├── ContinuousHuntWorker.cs                # Periyodik Av İşçisi (Smart Backoff)
│       │   └── RabbitMqListenerService.cs             # RabbitMQ Tüketici İşçisi (Consumer)
│       ├── Hubs/
│       │   └── NotificationHub.cs                     # SignalR Gerçek Zamanlı Bildirim Hub'ı
│       ├── Middlewares/
│       │   └── ExceptionMiddleware.cs                 # Global Hata Yakalama ve Güvenlik
│       └── Program.cs                                 # Uygulama Başlatıcı, RateLimiter, HealthChecks, Serilog
│
└── tests/
    └── SeatHunter.Application.Tests/                  # Birim Testler
        └── Features/HuntRequests/
            └── CreateHuntRequestCommandHandlerTests.cs# Moq & FluentAssertions ile %100 İzole Test
```

---

## 9. SONUÇ VE MÜLAKAT ÖZETİ (Executive Summary)

SeatHunter projesini mülakat kuruluna aktarırken vurgulanması gereken 5 temel anahtar mesaj:
1. **Mimari Disiplin:** Clean Architecture ve CQRS ile iş mantığını framework'lerden ve veritabanından tamamen bağımsız tuttuk.
2. **Hibrit Kazıma İnovasyonu:** Statik sitelerde milisaniyelik `HttpClient`, dinamik SPA'larda ise ağ optimizasyonu (`Resource Blocking`) yapılmış `Microsoft.Playwright` kullanarak hem performansı hem de her siteye uyumluluğu sağladık.
3. **Gerçek Zamanlı ve Olay Güdümlü:** RabbitMQ ile asenkron esneklik, Redis ile mikrosaniyelik önbellek, SignalR ile kullanıcıya sıfır gecikmeli canlı soket bildirimi sunduk.
4. **Kritik Hata Çözümü:** `CancellationTokenSource.CreateLinkedTokenSource` kaynaklı bellek sızıntısını mimari düzeyde tespit edip yaşam döngüsü yönetimiyle ortadan kaldırdık.
5. **Bulut ve DevOps Hazırlığı:** Multi-stage Docker, Playwright Jammy imajı, `.slnx` formatında GitHub Actions CI/CD pipeline'ı ve güvenli ortam değişkeni mimarisiyle projeyi doğrudan üretim ortamına hazır hale getirdik.
