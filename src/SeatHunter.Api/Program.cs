using Microsoft.AspNetCore.HttpOverrides;
using Microsoft.AspNetCore.RateLimiting;
using System.Threading.RateLimiting;
using OpenTelemetry.Metrics;
using OpenTelemetry.Resources;
using OpenTelemetry.Trace;
using Serilog;
using SeatHunter.Application;
using SeatHunter.Infrastructure;
using SeatHunter.Api.BackgroundServices;
using Microsoft.EntityFrameworkCore;
using SeatHunter.Domain.Constants;

Log.Logger = new LoggerConfiguration()
    .MinimumLevel.Information()
    .Enrich.FromLogContext()
    .WriteTo.Console(outputTemplate: "[{Timestamp:HH:mm:ss} {Level:u3}] [{CorrelationId}] {Message:lj}{NewLine}{Exception}")
    .WriteTo.File("Logs/seathunter-log-.txt", rollingInterval: RollingInterval.Day, outputTemplate: "{Timestamp:yyyy-MM-dd HH:mm:ss.fff zzz} [{Level:u3}] [{CorrelationId}] {Message:lj}{NewLine}{Exception}")
    .CreateLogger();

try
{
    Log.Information("Seat Hunter API başlatılıyor...");
    var builder = WebApplication.CreateBuilder(args);
    
    // Serilog'u host'a ekle
    builder.Host.UseSerilog();

    builder.Services.AddApplicationServices();
    builder.Services.AddInfrastructureServices(builder.Configuration);
    builder.Services.AddControllers();
    builder.Services.AddEndpointsApiExplorer();
    builder.Services.AddSwaggerGen();

    var jwtSettings = builder.Configuration.GetSection("Jwt");
    var configuredJwtKey = jwtSettings["Key"];
    if (!builder.Environment.IsDevelopment() &&
        (string.IsNullOrWhiteSpace(configuredJwtKey) || System.Text.Encoding.UTF8.GetByteCount(configuredJwtKey) < 32))
    {
        throw new InvalidOperationException("Production ortamında en az 32 byte uzunluğunda Jwt:Key yapılandırılmalıdır.");
    }

    var jwtKey = !string.IsNullOrWhiteSpace(configuredJwtKey)
        ? configuredJwtKey
        : "SeatHunterSuperSecretDevelopmentKeyMustBeAtLeast32BytesLong!";

    builder.Services.AddAuthentication(Microsoft.AspNetCore.Authentication.JwtBearer.JwtBearerDefaults.AuthenticationScheme)
        .AddJwtBearer(options =>
        {
            options.TokenValidationParameters = new Microsoft.IdentityModel.Tokens.TokenValidationParameters
            {
                ValidateIssuer = true,
                ValidateAudience = true,
                ValidateLifetime = true,
                ValidateIssuerSigningKey = true,
                ValidIssuer = !string.IsNullOrWhiteSpace(jwtSettings["Issuer"]) ? jwtSettings["Issuer"] : "SeatHunterSaaS",
                ValidAudience = !string.IsNullOrWhiteSpace(jwtSettings["Audience"]) ? jwtSettings["Audience"] : "SeatHunterClients",
                IssuerSigningKey = new Microsoft.IdentityModel.Tokens.SymmetricSecurityKey(System.Text.Encoding.UTF8.GetBytes(jwtKey))
            };

            options.Events = new Microsoft.AspNetCore.Authentication.JwtBearer.JwtBearerEvents
            {
                OnMessageReceived = context =>
                {
                    var accessToken = context.Request.Query["access_token"];
                    var path = context.HttpContext.Request.Path;
                    if (!string.IsNullOrEmpty(accessToken) && 
                        (path.StartsWithSegments("/notificationHub") || path.StartsWithSegments("/adminHub")))
                    {
                        context.Token = accessToken;
                    }
                    return Task.CompletedTask;
                },
                OnTokenValidated = async context =>
                {
                    var userId = context.Principal?.FindFirst(System.Security.Claims.ClaimTypes.NameIdentifier)?.Value;
                    if (string.IsNullOrWhiteSpace(userId))
                    {
                        context.Fail("Geçersiz kullanıcı kimliği.");
                        return;
                    }

                    var userRepo = context.HttpContext.RequestServices.GetRequiredService<SeatHunter.Application.Interfaces.IUserRepository>();
                    var user = await userRepo.GetByIdAsync(userId, context.HttpContext.RequestAborted);
                    if (user == null || !user.IsActive)
                    {
                        context.Fail("Hesap askıya alınmış veya bulunamadı.");
                    }
                }
            };
        });

    builder.Services.AddCors(options =>
    {
        options.AddPolicy("CorsPolicy", policy =>
        {
            if (builder.Environment.IsDevelopment())
            {
                policy.SetIsOriginAllowed(_ => true)
                      .AllowAnyMethod()
                      .AllowAnyHeader()
                      .AllowCredentials();
            }
            else
            {
                var allowedOrigins = builder.Configuration.GetSection("Cors:AllowedOrigins").Get<string[]>() 
                    ?? new[] { "https://seathunter.com" };
                policy.WithOrigins(allowedOrigins)
                      .AllowAnyMethod()
                      .AllowAnyHeader()
                      .AllowCredentials();
            }
        });
    });

    builder.Services.AddSignalR();
    builder.Services.AddScoped<SeatHunter.Application.Interfaces.ISignalRNotifier, SeatHunter.Api.Services.SignalRNotifier>();
    builder.Services.AddScoped<SeatHunter.Application.Interfaces.IAdminNotifier, SeatHunter.Api.Services.AdminNotifier>();

    var otlpEndpoint = builder.Configuration["OpenTelemetry:OtlpEndpoint"]
        ?? Environment.GetEnvironmentVariable("OTEL_EXPORTER_OTLP_ENDPOINT");

    builder.Services.AddOpenTelemetry()
        .ConfigureResource(resource => resource.AddService("SeatHunter.Api"))
        .WithMetrics(metrics =>
        {
            metrics
                .AddAspNetCoreInstrumentation()
                .AddHttpClientInstrumentation()
                .AddMeter(AppConstants.Metrics.MeterName)
                .AddPrometheusExporter();
        })
        .WithTracing(tracing =>
        {
            tracing
                .AddAspNetCoreInstrumentation()
                .AddHttpClientInstrumentation()
                .AddSource(AppConstants.Metrics.MeterName);

            if (!string.IsNullOrWhiteSpace(otlpEndpoint) && Uri.TryCreate(otlpEndpoint, UriKind.Absolute, out var otlpUri))
            {
                tracing.AddOtlpExporter(o => o.Endpoint = otlpUri);
            }
        });

    builder.Services.Configure<ForwardedHeadersOptions>(options =>
    {
        options.ForwardedHeaders = ForwardedHeaders.XForwardedFor | ForwardedHeaders.XForwardedProto;
        options.KnownIPNetworks.Clear();
        options.KnownProxies.Clear();
    });

    builder.Services.AddRateLimiter(options =>
    {
        options.RejectionStatusCode = StatusCodes.Status429TooManyRequests;
        options.AddFixedWindowLimiter(AppConstants.RateLimiting.FixedPolicy, opt =>
        {
            opt.PermitLimit = builder.Environment.IsEnvironment("Testing") ? 1000 : 20;
            opt.Window = TimeSpan.FromSeconds(10);
            opt.QueueProcessingOrder = QueueProcessingOrder.OldestFirst;
            opt.QueueLimit = 0;
        });
    });

    var redisConn = builder.Configuration.GetConnectionString("Redis");
    if (string.IsNullOrWhiteSpace(redisConn)) redisConn = "localhost:6379";

    var rabbitHost = builder.Configuration.GetConnectionString("RabbitMQ");
    if (string.IsNullOrWhiteSpace(rabbitHost)) rabbitHost = "localhost";
    var rabbitConn = rabbitHost.StartsWith("amqp://") ? rabbitHost : $"amqp://{rabbitHost}:5672";

    if (!builder.Environment.IsEnvironment("Testing"))
    {
        builder.Services.AddHealthChecks()
            .AddNpgSql(builder.Configuration.GetConnectionString("DefaultConnection")!, name: "PostgreSQL-Database")
            .AddRedis(redisConn, name: "Redis-Cache")
            .AddRabbitMQ(rabbitConnectionString: rabbitConn, name: "RabbitMQ-MessageBroker")
            .AddCheck<SeatHunter.Infrastructure.HealthChecks.ProxyPoolHealthCheck>("Proxy-Pool");

        builder.Services.AddHostedService<RabbitMqListenerService>();
        builder.Services.AddHostedService<ContinuousHuntWorker>();
        builder.Services.AddHostedService<SubscriptionExpirationWorker>();
    }
    else
    {
        builder.Services.AddHealthChecks();
    }

    var app = builder.Build();

    if (!app.Environment.IsEnvironment("Testing"))
    {
        using var scope = app.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<SeatHunter.Infrastructure.Persistence.SeatHunterDbContext>();
        db.Database.Migrate();
    }
    
    app.UseForwardedHeaders();
    app.UseMiddleware<SeatHunter.Api.Middlewares.CorrelationIdMiddleware>();
    app.Use(async (context, next) =>
    {
        context.Response.Headers.Append("X-Content-Type-Options", "nosniff");
        context.Response.Headers.Append("X-Frame-Options", "DENY");
        context.Response.Headers.Append("Referrer-Policy", "strict-origin-when-cross-origin");
        context.Response.Headers.Append("X-XSS-Protection", "1; mode=block");
        await next();
    });

    app.UseDefaultFiles();
    app.UseStaticFiles();
    app.UseMiddleware<SeatHunter.Api.Middlewares.ExceptionMiddleware>();
    if (app.Environment.IsDevelopment()) 
    { 
        app.UseSwagger(); 
        app.UseSwaggerUI(); 
    }
    if (app.Configuration.GetValue<bool>("Http:EnableHttpsRedirection"))
    {
        app.UseHttpsRedirection();
    }
    app.UseCors("CorsPolicy");
    app.UseAuthentication();
    app.UseAuthorization();
    app.UseRateLimiter();
    app.MapControllers();

    app.MapHealthChecks("/health/live", new Microsoft.AspNetCore.Diagnostics.HealthChecks.HealthCheckOptions
    {
        Predicate = _ => false
    });
    app.MapPrometheusScrapingEndpoint();

    app.MapHealthChecks("/health", new Microsoft.AspNetCore.Diagnostics.HealthChecks.HealthCheckOptions
    {
        ResponseWriter = async (context, report) =>
        {
            context.Response.ContentType = "application/json";
            var result = System.Text.Json.JsonSerializer.Serialize(new
            {
                status = report.Status.ToString(),
                checks = report.Entries.Select(e => new { component = e.Key, status = e.Value.Status.ToString() })
            });
            await context.Response.WriteAsync(result);
        }
    });
    app.MapHub<SeatHunter.Api.Hubs.NotificationHub>("/notificationHub");
    app.MapHub<SeatHunter.Api.Hubs.AdminHub>("/adminHub");

    app.Run();
}
catch (Exception ex)
{
    Log.Fatal(ex, "Uygulama başlatılamadı!");
}
finally
{
    Log.CloseAndFlush();
}

public partial class Program { }
