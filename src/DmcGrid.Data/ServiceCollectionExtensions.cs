using DmcGrid.Core.Interfaces;
using DmcGrid.Data.Repositories;
using Microsoft.Extensions.DependencyInjection;

namespace DmcGrid.Data;

public static class ServiceCollectionExtensions
{
    public static IServiceCollection AddDmcGridData(this IServiceCollection services, string defaultConnection, string masterConnection)
    {
        services.AddSingleton(new Db(defaultConnection));
        services.AddSingleton<IUserRepository, UserRepository>();
        services.AddSingleton<IRfqRepository, RfqRepository>();
        services.AddSingleton<IQuoteRepository, QuoteRepository>();
        services.AddSingleton<IProposalRepository, ProposalRepository>();
        services.AddSingleton<IAuditRepository, AuditRepository>();
        services.AddSingleton<DemoSeeder>();
        services.AddSingleton(sp => new DbInitializer(masterConnection, defaultConnection,
            sp.GetRequiredService<Db>(), sp.GetRequiredService<DemoSeeder>()));
        return services;
    }
}
