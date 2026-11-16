using Microsoft.Extensions.DependencyInjection;

namespace SeatHunter.Application;

public static class DependencyInjection
{
    public static IServiceCollection AddApplicationServices(this IServiceCollection services)
    {
        services.AddMediatR(cfg => cfg.RegisterServicesFromAssembly(typeof(DependencyInjection).Assembly));
        services.AddSingleton<SeatHunter.Application.Interfaces.IHuntManagerService, SeatHunter.Application.Services.HuntManagerService>();
        services.AddScoped<SeatHunter.Application.Interfaces.IUserPlanService, SeatHunter.Application.Services.UserPlanService>();
        services.AddScoped<SeatHunter.Application.Interfaces.IHuntExecutionService, SeatHunter.Application.Services.HuntExecutionService>();
        services.AddScoped<SeatHunter.Application.Interfaces.IBillingService, SeatHunter.Application.Services.BillingService>();
        return services;
    }
}
