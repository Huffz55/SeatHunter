using Microsoft.AspNetCore.RateLimiting;
using System.Threading.RateLimiting;
using Serilog;
using SeatHunter.Application;
using SeatHunter.Infrastructure;
using SeatHunter.Api.BackgroundServices;
using Microsoft.EntityFrameworkCore;

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
        });

    builder.Services.AddSignalR();

    builder.Services.AddRateLimiter(options =>
    {
        options.RejectionStatusCode = StatusCodes.Status429TooManyRequests;
        options.AddFixedWindowLimiter("FixedPolicy", opt =>
        {
            opt.PermitLimit = 3;
            opt.Window = TimeSpan.FromSeconds(10);
            opt.QueueProcessingOrder = QueueProcessingOrder.OldestFirst;
            opt.QueueLimit = 0;
        });
    });

    builder.Services.AddHealthChecks()
        .AddNpgSql(builder.Configuration.GetConnectionString("DefaultConnection")!, name: "PostgreSQL-Database")
        .AddRedis("localhost:6379", name: "Redis-Cache")
        .AddRabbitMQ(rabbitConnectionString: "amqp://localhost:5672", name: "RabbitMQ-MessageBroker");
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
