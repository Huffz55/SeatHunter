# 1. Build aşaması: Resmi .NET 10 SDK imajını kullan
FROM mcr.microsoft.com/dotnet/sdk:10.0 AS build
WORKDIR /src

# Proje dosyalarını kopyala ve restore işlemini gerçekleştir
COPY ["src/SeatHunter.Api/SeatHunter.Api.csproj", "src/SeatHunter.Api/"]
COPY ["src/SeatHunter.Application/SeatHunter.Application.csproj", "src/SeatHunter.Application/"]
COPY ["src/SeatHunter.Domain/SeatHunter.Domain.csproj", "src/SeatHunter.Domain/"]
COPY ["src/SeatHunter.Infrastructure/SeatHunter.Infrastructure.csproj", "src/SeatHunter.Infrastructure/"]

RUN dotnet restore "src/SeatHunter.Api/SeatHunter.Api.csproj"

# Tüm kaynak kodları kopyala ve publish et (self-contained olarak, linux-x64 mimarisine göre)
COPY . .
WORKDIR "/src/src/SeatHunter.Api"
RUN dotnet publish "SeatHunter.Api.csproj" -c Release -r linux-x64 --self-contained true -o /app/publish /p:UseAppHost=true

# 2. Çalışma Zamanı (Runtime) aşaması: Microsoft Playwright dotnet imajını kullan (Playwright v1.63.0 ile tam uyumlu)
FROM mcr.microsoft.com/playwright/dotnet:v1.63.0-noble AS final
WORKDIR /app

# Yayınlanan self-contained binary dosyalarını ve web kök klasörünü (wwwroot) kopyala
COPY --from=build /app/publish .

# İkili çalıştırma izinlerini güvenceye al
RUN chmod +x ./SeatHunter.Api

# Portları dışa aç
EXPOSE 8080
ENV ASPNETCORE_URLS=http://+:8080

# Kapsayıcı ayağa kalktığında doğrudan uygulamayı başlat
ENTRYPOINT ["./SeatHunter.Api"]
