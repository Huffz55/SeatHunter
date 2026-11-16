# 🎟️ SeatHunter SaaS

![.NET](https://img.shields.io/badge/.NET-10.0-512BD4?logo=dotnet)
![React](https://img.shields.io/badge/Frontend-React%2018%20%2B%20Vite-61DAFB?logo=react)
![Architecture](https://img.shields.io/badge/Architecture-Clean%20%7C%20CQRS-blue)
![OpenTelemetry](https://img.shields.io/badge/Observability-OpenTelemetry%20%2B%20Prometheus-E6522C?logo=prometheus)
![Docker](https://img.shields.io/badge/Docker-Production%20Ready-2496ED?logo=docker)
![Playwright](https://img.shields.io/badge/Scraping-Playwright-2EAD33?logo=playwright)
![RabbitMQ](https://img.shields.io/badge/Broker-RabbitMQ-FF6600?logo=rabbitmq)
![PostgreSQL](https://img.shields.io/badge/Database-PostgreSQL-4169E1?logo=postgresql)
![Redis](https://img.shields.io/badge/Cache-Redis-DC382D?logo=redis)

## 📌 Project Overview

**SeatHunter** is a multi-tier SaaS ticket-monitoring and seat-discovery platform built with **.NET 10** (**Clean Architecture** + **CQRS / MediatR**) and a modular **React + Vite + Tailwind CSS v4** frontend.

The platform continuously monitors ticket availability and pricing snapshots across static and JavaScript-rendered event pages, enforces tier-based quotas (`Free`, `Pro`, `Vip`), and delivers real-time alerts via **SignalR**, **Email**, **Telegram**, and **Discord**. Administrators manage system infrastructure, Dead Letter Queues (DLQ), and user subscriptions through an isolated **Admin Portal**.

---

## ✨ Key Features

- **Hybrid Scraping & VIP Auto-Cart Engine**
  - Fast `HttpClient` pipeline (`HttpScraperProviderBase`) backed by **Microsoft Playwright** headless Chromium rendering (`UniversalProvider`, `BiletixScraperProvider`, `ObiletScraperProvider`) and HTML/JSON DOM evaluation (`TicketHtmlEvaluator`).
  - Proxy rotation with cooldown tracking, Polly resilience policies, and optional **VIP Auto-Cart** reservation link creation (`PlaywrightTicketReservationEngine`).
- **Tiered SaaS Billing & Subscriptions**
  - Database-backed `Subscription` and `PaymentTransaction` entities with `PlanCatalog` (`Free`, `Pro`, `Vip`, monthly & yearly cycles).
  - Idempotent checkout sessions (`/api/billing/checkout`), HMAC-SHA256 webhook signature verification (`/api/billing/webhook`), sandbox confirmation flow outside production, period-end cancellation, and automated expiry checks (`SubscriptionExpirationWorker`).
- **Multi-Channel Real-Time Notifications (SSRF-Safe)**
  - Real-time browser push updates over SignalR (`/notificationHub` & `/adminHub`) with automatic reconnection banner handling.
  - Per-user channel preferences (`Email`, `Telegram`, personal `TelegramChatId`, personal `DiscordWebhookUrl`) validated against strict host allowlists (`NotificationChannelValidator`), dispatched asynchronously via **RabbitMQ**.
- **Pro/VIP Price Analytics & Admin Operations Portal**
  - Historical price snapshots and event min/max/average summaries scoped cleanly by subscription tier (`/api/price-analytics`).
  - Separate **Admin Portal** featuring live system telemetry, RabbitMQ DLQ inspection/replay/purge (`/api/admin/dlq`), and user tier/status administration (`/api/admin/users`).
- **Production Observability & Hardening**
  - **OpenTelemetry** metrics & tracing (`SeatHunter.Engine` activity spans + ASP.NET Core / `HttpClient` telemetry) exposed at `/metrics` for **Prometheus** and configurable OTLP exporters (`OTEL_EXPORTER_OTLP_ENDPOINT`).
  - Correlation ID propagation (`X-Correlation-ID` via `CorrelationIdMiddleware`) enriched into structured **Serilog** logs, reverse-proxy `ForwardedHeaders`, and separate readiness (`/health`) vs. liveness (`/health/live`) probes.

---

## 🧱 Architecture & Repository Layout

```text
SeatHunter/
├── src/
│   ├── SeatHunter.Domain/           # Entities (User, HuntRequest, Subscription, PaymentTransaction), enums, interfaces
│   ├── SeatHunter.Application/      # CQRS commands/queries (MediatR), BillingService, HuntExecutionService, validators
│   ├── SeatHunter.Infrastructure/   # EF Core (PostgreSQL), Redis scheduler, RabbitMQ, Playwright providers, telemetry
│   └── SeatHunter.Api/              # ASP.NET Core API controllers, SignalR hubs, middlewares, workers, compiled wwwroot
├── frontend/                        # Modular React 18 + Vite 6 + Tailwind CSS v4 SPA (TR/EN i18n, User & Admin portals)
└── tests/
    ├── SeatHunter.Application.Tests/
    ├── SeatHunter.Infrastructure.Tests/
    └── SeatHunter.Api.Tests/        # WebApplicationFactory integration tests (Auth, Hunts, Billing, Admin, Observability)
```

---

## 🛠️ Tech Stack

- **Backend:** .NET 10, ASP.NET Core Web API, MediatR, FluentValidation, Serilog, Polly
- **Frontend:** React 18, Vite 6, Tailwind CSS v4, `@microsoft/signalr`, Font Awesome
- **Data & Messaging:** PostgreSQL (EF Core 10), Redis, RabbitMQ
- **Scraping & Browser Automation:** Microsoft Playwright, HtmlAgilityPack
- **Observability:** OpenTelemetry (Metrics & Tracing), Prometheus Exporter (`/metrics`)
- **Containers & CI:** Multi-stage Docker (`node:22-alpine` -> `.NET 10 SDK` -> `playwright/dotnet`), Docker Compose, GitHub Actions

---

## 🚀 Getting Started

### Option 1: Docker Compose (Recommended)

Docker builds the frontend bundle automatically and runs PostgreSQL, Redis, RabbitMQ, and the self-contained .NET 10 API together.

1. **Clone and configure environment variables:**
   ```bash
   git clone https://github.com/Huffz55/SeatHunter.git
   cd SeatHunter
   cp .env.example .env
   ```
   Fill in required secrets in `.env` (PostgreSQL credentials, `JWT_KEY`, `BILLING_WEBHOOK_SECRET`, and optional Telegram/Email/Discord/Proxy/OTEL variables).

2. **Build and launch:**
   ```bash
   docker compose up -d --build
   ```

3. **Access the endpoints:**
   - **Web App:** `http://localhost:5179`
   - **Swagger (Development):** `http://localhost:5179/swagger`
   - **Liveness / Readiness:** `http://localhost:5179/health/live` · `http://localhost:5179/health`
   - **Prometheus Metrics:** `http://localhost:5179/metrics`

---

### Option 2: Local Development (Frontend + Backend)

Requires **.NET 10 SDK** and **Node.js 22+**.

1. **Build or run the frontend:**
   ```bash
   cd frontend
   npm ci
   npm run dev      # Vite dev server with /api and SignalR proxies to localhost:5179
   # or: npm run build  (outputs static assets into src/SeatHunter.Api/wwwroot)
   ```

2. **Run the API:**
   ```bash
   cd src/SeatHunter.Api
   dotnet run
   ```

---

## 🧪 Running Tests

Run the full build and automated test suite across Application, Infrastructure, and API Integration layers:

```bash
dotnet build SeatHunter.slnx --configuration Release
dotnet test SeatHunter.slnx --configuration Release --no-build
```

Currently verified with **155 tests passing** (`75` Application, `63` Infrastructure, `17` API Integration).

---

*Built with Clean Architecture, strict tier isolation, observability, and automated verification.*
