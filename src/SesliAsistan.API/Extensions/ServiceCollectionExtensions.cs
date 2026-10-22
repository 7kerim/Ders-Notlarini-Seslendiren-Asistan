using System.Text;
using System.Threading.RateLimiting;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.IdentityModel.Tokens;
using Microsoft.OpenApi.Models;
using SesliAsistan.Business.Options;

namespace SesliAsistan.API.Extensions;

public static class ServiceCollectionExtensions
{
    public const string AuthRateLimitPolicy = "auth";

    public static IServiceCollection AddJwtAuthentication(this IServiceCollection services, IConfiguration config)
    {
        var jwt = config.GetSection(JwtOptions.SectionName).Get<JwtOptions>()
                  ?? throw new InvalidOperationException("Jwt yapılandırması eksik.");

        if (string.IsNullOrWhiteSpace(jwt.Secret) || jwt.Secret.Length < 32)
            throw new InvalidOperationException("Jwt:Secret en az 32 karakter olmalıdır.");

        services.AddAuthentication(JwtBearerDefaults.AuthenticationScheme)
            .AddJwtBearer(o =>
            {
                o.MapInboundClaims = false;
                o.TokenValidationParameters = new TokenValidationParameters
                {
                    ValidateIssuer = true,
                    ValidIssuer = jwt.Issuer,
                    ValidateAudience = true,
                    ValidAudience = jwt.Audience,
                    ValidateIssuerSigningKey = true,
                    IssuerSigningKey = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(jwt.Secret)),
                    ValidateLifetime = true,
                    ClockSkew = TimeSpan.FromSeconds(30),
                    NameClaimType = System.Security.Claims.ClaimTypes.Name,
                    RoleClaimType = System.Security.Claims.ClaimTypes.Role
                };
            });

        // RBAC: Öğrenci ve Yönetici rolleri
        services.AddAuthorizationBuilder()
            .AddPolicy("AdminOnly", p => p.RequireRole("Admin"))
            .AddPolicy("StudentOrAdmin", p => p.RequireRole("Student", "Admin"));

        return services;
    }

    /// <summary>Kaba kuvvet (brute-force) saldırılarına karşı giriş/kayıt uçlarına IP başına sınır.</summary>
    public static IServiceCollection AddAppRateLimiting(this IServiceCollection services)
    {
        services.AddRateLimiter(o =>
        {
            o.RejectionStatusCode = StatusCodes.Status429TooManyRequests;
            o.AddPolicy(AuthRateLimitPolicy, ctx => RateLimitPartition.GetFixedWindowLimiter(
                ctx.Connection.RemoteIpAddress?.ToString() ?? "unknown",
                _ => new FixedWindowRateLimiterOptions { PermitLimit = 10, Window = TimeSpan.FromMinutes(1) }));
        });
        return services;
    }

    public static IServiceCollection AddSwaggerWithJwt(this IServiceCollection services)
    {
        services.AddEndpointsApiExplorer();
        services.AddSwaggerGen(o =>
        {
            o.SwaggerDoc("v1", new OpenApiInfo
            {
                Title = "Ders Notlarını Seslendiren Öğrenme Asistanı API",
                Version = "v1",
                Description = "Fırat Üniversitesi Bilgisayar Mühendisliği Tasarım Dersi projesi"
            });

            var scheme = new OpenApiSecurityScheme
            {
                Name = "Authorization",
                Type = SecuritySchemeType.Http,
                Scheme = "bearer",
                BearerFormat = "JWT",
                In = ParameterLocation.Header,
                Description = "Giriş yanıtındaki accessToken değerini yapıştırın.",
                Reference = new OpenApiReference { Type = ReferenceType.SecurityScheme, Id = "Bearer" }
            };
            o.AddSecurityDefinition("Bearer", scheme);
            o.AddSecurityRequirement(new OpenApiSecurityRequirement { [scheme] = Array.Empty<string>() });

            var xml = Path.Combine(AppContext.BaseDirectory, "SesliAsistan.API.xml");
            if (File.Exists(xml)) o.IncludeXmlComments(xml);
        });
        return services;
    }

    /// <summary>Redis bağlantısı tanımlıysa Redis, değilse bellek içi dağıtık önbellek kullanılır.</summary>
    public static IServiceCollection AddAppCaching(this IServiceCollection services, IConfiguration config)
    {
        var redis = config.GetConnectionString("Redis");
        if (!string.IsNullOrWhiteSpace(redis))
            services.AddStackExchangeRedisCache(o => { o.Configuration = redis; o.InstanceName = "sesli:"; });
        else
            services.AddDistributedMemoryCache();
        return services;
    }
}
