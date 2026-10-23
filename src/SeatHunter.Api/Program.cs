using Microsoft.AspNetCore.RateLimiting;
using System.Threading.RateLimiting;
using Serilog;
using SeatHunter.Application;
using SeatHunter.Infrastructure;
using SeatHunter.Api.BackgroundServices;
using Microsoft.EntityFrameworkCore;
using SeatHunter.Domain.Constants;

Log.Logger = new LoggerConfiguration()
    .MinimumLevel.Information()
    .WriteTo.Console()
    .WriteTo.File("Logs/seathunter-log-.txt", rollingInterval: RollingInterval.Day)
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
    builder.Services.AddAuthentication(Microsoft.AspNetCore.Authentication.JwtBearer.JwtBearerDefaults.AuthenticationScheme)
        .AddJwtBearer(options =>
        {
            options.TokenValidationParameters = new Microsoft.IdentityModel.Tokens.TokenValidationParameters
            {
                ValidateIssuer = true,
                ValidateAudience = true,
                ValidateLifetime = true,
                ValidateIssuerSigningKey = true,
                ValidIssuer = jwtSettings["Issuer"],
                ValidAudience = jwtSettings["Audience"],
                IssuerSigningKey = new Microsoft.IdentityModel.Tokens.SymmetricSecurityKey(System.Text.Encoding.UTF8.GetBytes(jwtSettings["Key"]!))
            };

            options.Events = new Microsoft.AspNetCore.Authentication.JwtBearer.JwtBearerEvents
            {
                OnMessageReceived = context =>
                {
                    var accessToken = context.Request.Query["access_token"];
                    var path = context.HttpContext.Request.Path;
                    if (!string.IsNullOrEmpty(accessToken) && path.StartsWithSegments("/notificationHub"))
                    {
                        context.Token = accessToken;
                    }
                    return Task.CompletedTask;
                }
            };
        });

    builder.Services.AddCors(options =>
    {
        options.AddPolicy("CorsPolicy", policy =>
        {
            policy.SetIsOriginAllowed(_ => true)
                  .AllowAnyMethod()
                  .AllowAnyHeader()
                  .AllowCredentials();
        });
    });

    builder.Services.AddSignalR();
    builder.Services.AddScoped<SeatHunter.Application.Interfaces.ISignalRNotifier, SeatHunter.Api.Services.SignalRNotifier>();

    builder.Services.AddRateLimiter(options =>
    {
        options.RejectionStatusCode = StatusCodes.Status429TooManyRequests;
        options.AddFixedWindowLimiter(AppConstants.RateLimiting.FixedPolicy, opt =>
        {
            opt.PermitLimit = 3;
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

    builder.Services.AddHealthChecks()
        .AddNpgSql(builder.Configuration.GetConnectionString("DefaultConnection")!, name: "PostgreSQL-Database")
        .AddRedis(redisConn, name: "Redis-Cache")
        .AddRabbitMQ(rabbitConnectionString: rabbitConn, name: "RabbitMQ-MessageBroker")
        .AddCheck<SeatHunter.Infrastructure.HealthChecks.ProxyPoolHealthCheck>("Proxy-Pool");

    builder.Services.AddHostedService<RabbitMqListenerService>();
    builder.Services.AddHostedService<ContinuousHuntWorker>();

    var app = builder.Build();

    using (var scope = app.Services.CreateScope())
    {
        var db = scope.ServiceProvider.GetRequiredService<SeatHunter.Infrastructure.Persistence.SeatHunterDbContext>();
        db.Database.Migrate();
    }
    
    app.UseStaticFiles();
    app.UseMiddleware<SeatHunter.Api.Middlewares.ExceptionMiddleware>();
    if (app.Environment.IsDevelopment()) 
    { 
        app.UseSwagger(); 
        app.UseSwaggerUI(); 
    }
    app.UseHttpsRedirection();
    app.UseCors("CorsPolicy");
    app.UseAuthentication();
    app.UseAuthorization();
    app.UseRateLimiter();
    app.MapControllers();

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
