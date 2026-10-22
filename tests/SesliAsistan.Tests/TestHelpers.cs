using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;
using SesliAsistan.Business.Options;
using SesliAsistan.Business.Services;
using SesliAsistan.Business.Validation;
using SesliAsistan.DataAccess.Context;
using SesliAsistan.DataAccess.Repositories;
using MsOptions = Microsoft.Extensions.Options.Options;

namespace SesliAsistan.Tests;

internal static class TestHelpers
{
    public static AppDbContext CreateDb() =>
        new(new DbContextOptionsBuilder<AppDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .Options);

    public static readonly JwtOptions Jwt = new()
    {
        Secret = "TEST-ICIN-EN-AZ-32-KARAKTERLIK-GIZLI-ANAHTAR!!",
        AccessTokenMinutes = 30,
        RefreshTokenDays = 14
    };

    public static AuthService CreateAuthService(AppDbContext db) =>
        new(new UserRepository(db),
            new RefreshTokenRepository(db),
            new UnitOfWork(db),
            new BCryptPasswordHasher(),
            new JwtTokenService(MsOptions.Create(Jwt)),
            new RegisterRequestValidator(),
            new LoginRequestValidator(),
            MsOptions.Create(Jwt),
            NullLogger<AuthService>.Instance);
}
