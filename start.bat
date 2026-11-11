@echo off
setlocal
cd /d "%~dp0"
title SeatHunter SaaS Runner

echo ===================================================
echo             SeatHunter SaaS Runner
echo ===================================================
echo.

echo [1/3] Docker kontrol ediliyor...
docker info >nul 2>&1
if %errorlevel% equ 0 goto docker_ready

echo Docker Desktop henuz acik degil, baslatiliyor...
if exist "C:\Program Files\Docker\Docker\Docker Desktop.exe" (
    start "" "C:\Program Files\Docker\Docker\Docker Desktop.exe"
) else (
    echo [HATA] Docker Desktop bulunamadi! Lutfen Docker Desktop'i manuel baslatin.
    pause
    exit /b 1
)

echo Docker motorunun hazir olmasi bekleniyor...
:wait_docker
timeout /t 3 /nobreak >nul
docker info >nul 2>&1
if %errorlevel% neq 0 goto wait_docker

:docker_ready
echo [OK] Docker hazir!
echo.

echo [2/3] Altyapi servisleri (Postgres, Redis, RabbitMQ) baslatiliyor...
docker compose up -d postgres redis rabbitmq
if %errorlevel% neq 0 (
    echo [HATA] docker compose up basarisiz oldu!
    pause
    exit /b 1
)
echo [OK] Altyapi servisleri baslatildi!
echo.

echo [3/3] SeatHunter API ve Web Arayuzu baslatiliyor...
echo.
echo ===================================================
echo Web Arayuzu: http://localhost:5179
echo Swagger API: http://localhost:5179/swagger
echo RabbitMQ:    http://localhost:15672 (guest/guest)
echo Demo Hesap:  demo@seathunter.com / Password123!
echo ===================================================
echo.

timeout /t 2 /nobreak >nul
start http://localhost:5179
dotnet run --project src/SeatHunter.Api --launch-profile http

echo.
echo Uygulama sonlandi.
pause
