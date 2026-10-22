using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using SesliAsistan.DataAccess.Context;
using SesliAsistan.DataAccess.Repositories;

namespace SesliAsistan.DataAccess;

public static class DataAccessServiceRegistration
{
    public static IServiceCollection AddDataAccess(this IServiceCollection services, IConfiguration configuration)
    {
        services.AddDbContext<AppDbContext>(opt =>
            opt.UseNpgsql(configuration.GetConnectionString("Postgres")));

        services.AddScoped(typeof(IGenericRepository<>), typeof(GenericRepository<>));
        services.AddScoped<IUserRepository, UserRepository>();
        services.AddScoped<IRefreshTokenRepository, RefreshTokenRepository>();
        services.AddScoped<IDocumentRepository, DocumentRepository>();
        services.AddScoped<IUnitOfWork, UnitOfWork>();
        return services;
    }
}
