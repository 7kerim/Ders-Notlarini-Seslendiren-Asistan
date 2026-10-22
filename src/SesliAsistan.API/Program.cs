using SesliAsistan.API.BackgroundJobs;
using SesliAsistan.API.Extensions;
using SesliAsistan.API.Middleware;
using SesliAsistan.Business;
using SesliAsistan.DataAccess;
using SesliAsistan.DataAccess.Context;
using Microsoft.EntityFrameworkCore;

var builder = WebApplication.CreateBuilder(args);

// ---- Katmanlar (N-Tier) ----
builder.Services.AddDataAccess(builder.Configuration);   // Veri Erişim Katmanı
builder.Services.AddBusiness(builder.Configuration);     // İş Katmanı

// ---- Sunum Katmanı altyapısı ----
builder.Services.AddControllers();
builder.Services.AddProblemDetails();
builder.Services.AddJwtAuthentication(builder.Configuration);
builder.Services.AddAppRateLimiting();
builder.Services.AddSwaggerWithJwt();
builder.Services.AddAppCaching(builder.Configuration);
builder.Services.AddHealthChecks().AddDbContextCheck<AppDbContext>("postgres");
builder.Services.AddHostedService<DocumentProcessingWorker>();

builder.Services.AddCors(o => o.AddPolicy("mobile", p => p
    .WithOrigins(builder.Configuration.GetSection("Cors:Origins").Get<string[]>() ?? [])
    .AllowAnyHeader()
    .AllowAnyMethod()));

var app = builder.Build();

app.UseMiddleware<ExceptionHandlingMiddleware>();

if (app.Environment.IsDevelopment())
{
    app.UseSwagger();
    app.UseSwaggerUI(o => o.DocumentTitle = "Sesli Öğrenme Asistanı API");

    // Geliştirme ortamında bekleyen migration'ları otomatik uygula
    using var scope = app.Services.CreateScope();
    var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
    if (db.Database.IsRelational()) await db.Database.MigrateAsync();
}
else
{
    app.UseHsts();
}

app.UseHttpsRedirection();
app.UseCors("mobile");
app.UseRateLimiter();
app.UseAuthentication();
app.UseAuthorization();

app.MapControllers();
app.MapHealthChecks("/health");

app.Run();

// Entegrasyon testlerinde WebApplicationFactory<Program> için
public partial class Program { }
