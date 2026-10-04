# 🎟️ SeatHunter SaaS

![Tech Stack](https://img.shields.io/badge/.NET-10.0-512BD4?logo=dotnet)
![Architecture](https://img.shields.io/badge/Architecture-Clean%20%7C%20CQRS-blue)
![Docker](https://img.shields.io/badge/Docker-Production%20Ready-2496ED?logo=docker)
![Playwright](https://img.shields.io/badge/Web_Scraping-Playwright-2EAD33?logo=playwright)
![RabbitMQ](https://img.shields.io/badge/Message_Broker-RabbitMQ-FF6600?logo=rabbitmq)
![PostgreSQL](https://img.shields.io/badge/Database-PostgreSQL-4169E1?logo=postgresql)
![Redis](https://img.shields.io/badge/Cache-Redis-DC382D?logo=redis)

## 📌 Project Overview
**SeatHunter** is a highly scalable, advanced B2C ticket-hunting SaaS platform built entirely on **Clean Architecture** and **CQRS (MediatR)** principles. 

Instead of relying on fragile, hardcoded scraper logics, SeatHunter leverages an advanced **Universal Ticket Provider** system. It proactively monitors high-demand events (concerts, buses, airplanes) across global platforms and alerts the user instantaneously via **SignalR**, **Email**, or **Telegram** the millisecond a seat becomes available.

## ✨ Key Features
- 🕷️ **Hybrid Web Scraping Engine:** 
  - Extremely fast *HttpClient* mechanism for static websites.
  - Deep integrated **Microsoft Playwright (Headless Chromium)** implementation for fully rendering dynamic, JavaScript-heavy SPA platforms (e.g., *ingresso.com*, *biletix*). Features a custom **Deep Script Scanner** that intercepts JSON state payloads right inside the DOM to bypass anti-bot protections.
- ⚡ **Async Event-Driven Architecture:** Decoupled background services utilize **RabbitMQ** to handle high-latency operations (like sending SMTP emails or Telegram alerts) asynchronously ("Fire and Forget"). This prevents API thread starvation and ensures an ultra-responsive user experience.
- 🚀 **High Performance & Resilience:** Utilizes **Redis** for distributed caching and active hunt session storage, backed by robust **PostgreSQL** for data persistence. Incorporates **Polly** for exponential backoff and circuit-breaking against unstable external ticket providers.
- 🐳 **Production-Ready & Secure:** Fully containerized utilizing multi-stage Docker builds. The application is published as a `self-contained` Linux binary to guarantee 100% compatibility across environments without requiring .NET runtimes on the host machine. Secrets are strictly managed via environment variables (`.env`).

## 🛠️ Tech Stack
* **Framework:** .NET 10 (Web API)
* **Architecture:** Clean Architecture, CQRS (MediatR), Strategy & Factory Patterns
* **Web Scraping:** Microsoft Playwright, HtmlAgilityPack
* **Database & ORM:** PostgreSQL, Entity Framework Core 10
* **Caching:** Redis (Distributed Cache)
* **Message Broker:** RabbitMQ
* **Containerization:** Docker, Docker Compose

---

## 🚀 Getting Started

Deploying SeatHunter is remarkably straightforward, as the entire infrastructure (Database, Cache, Message Queue, and the Headless Browser API) is bundled in a single Docker Compose network.

### Step 1: Configure Environment Variables
We use environment variables to keep your credentials perfectly secure. First, create your local configuration file:
```bash
# Clone the repository
git clone https://github.com/Huffz55/SeatHunter.git
cd SeatHunter

# Copy the example environment file
cp .env.example .env
```
Open the `.env` file and fill in your actual database passwords, JWT keys, and Telegram/Email credentials.

### Step 2: Build and Run with Docker
No manual `dotnet run` or local SDK installations are required. To spin up the entire ecosystem, simply run:
```bash
docker-compose up -d --build
```
*This command will pull the necessary PostgreSQL, Redis, and RabbitMQ images, build the self-contained .NET 10 API, and install the Playwright Chromium dependencies automatically.*

### Step 3: Access the Application
Once the containers are successfully running, the application and its interactive API documentation will be available at:
👉 **http://localhost:5179/swagger**

From the Swagger UI, you can register a new user, acquire your JWT token, and immediately start scheduling asynchronous ticket hunts globally.

---
*Developed with clean code practices, performance, and scalability in mind.*
