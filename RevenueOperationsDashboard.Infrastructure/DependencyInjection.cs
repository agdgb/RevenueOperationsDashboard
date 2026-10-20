using Microsoft.Extensions.DependencyInjection;
using RevenueOperationsDashboard.Infrastructure.Time;

namespace RevenueOperationsDashboard.Infrastructure;

public static class DependencyInjection
{
    public static IServiceCollection AddTimeInfrastructure(this IServiceCollection services)
    {
        services.AddSingleton(TimeProvider.System);
        services.AddSingleton<IEthiopianClock, EthiopianCalendarClock>();
        services.AddSingleton<IEthiopianDateMapper, EthiopianDateMapper>();

        return services;
    }
}
