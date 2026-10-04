# Seat Hunter API
Clean Architecture, CQRS (MediatR), asenkron mesajlaşma (RabbitMQ) ve gerçek zamanlı bildirim (SignalR) kullanılarak geliştirilmiş B2C bilet/koltuk yakalama sistemi simülasyonu.

## Kurulum ve Çalıştırma
1. Altyapıyı ayağa kaldırmak için ana dizinde terminali açın ve `docker-compose up -d` komutunu çalıştırın. (RabbitMQ ve yönetim paneli başlayacaktır).
2. API'yi başlatmak için `dotnet run --project src/SeatHunter.Api/SeatHunter.Api.csproj` komutunu kullanın.
3. Tarayıcınızdan `http://localhost:5000/swagger` veya `https://localhost:5001/swagger` adresine giderek API'yi test edebilirsiniz. RabbitMQ yönetim paneline ise `http://localhost:15672` (Kullanıcı adı: guest, Şifre: guest) adresinden ulaşabilirsiniz.
