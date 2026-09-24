using CashMovement.Application.Abstractions;
using CashMovement.Application.Services;
using CashMovement.Infrastructure.Repositories;
using Microsoft.Extensions.DependencyInjection;

namespace CashMovement.Infrastructure;

public static class DependencyInjection
{
    public static IServiceCollection AddCashMovementInfrastructure(
        this IServiceCollection services, string connectionString)
    {
        services.AddSingleton<ISqlConnectionFactory>(_ => new SqlConnectionFactory(connectionString));
        services.AddScoped<ICashMovementRepository, SqlCashMovementRepository>();
        services.AddScoped<CashMovementService>();
        return services;
    }
}
