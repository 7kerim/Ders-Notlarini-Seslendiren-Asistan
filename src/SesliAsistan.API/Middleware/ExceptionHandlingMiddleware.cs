using Microsoft.AspNetCore.Mvc;
using SesliAsistan.Business.Exceptions;

namespace SesliAsistan.API.Middleware;

/// <summary>Tüm hataları RFC 7807 ProblemDetails biçiminde, tutarlı JSON olarak döndürür.</summary>
public class ExceptionHandlingMiddleware
{
    private readonly RequestDelegate _next;
    private readonly ILogger<ExceptionHandlingMiddleware> _logger;
    private readonly IHostEnvironment _env;

    public ExceptionHandlingMiddleware(RequestDelegate next, ILogger<ExceptionHandlingMiddleware> logger, IHostEnvironment env)
    {
        _next = next;
        _logger = logger;
        _env = env;
    }

    public async Task InvokeAsync(HttpContext context)
    {
        try
        {
            await _next(context);
        }
        catch (Exception ex)
        {
            await HandleAsync(context, ex);
        }
    }

    private async Task HandleAsync(HttpContext context, Exception ex)
    {
        ProblemDetails problem = ex switch
        {
            BusinessValidationException v => new ValidationProblemDetails(v.Errors.ToDictionary(k => k.Key, k => k.Value))
            {
                Status = v.StatusCode, Title = v.Title, Detail = v.Message
            },
            AppException app => new ProblemDetails { Status = app.StatusCode, Title = app.Title, Detail = app.Message },
            UnauthorizedAccessException => new ProblemDetails { Status = 401, Title = "Kimlik doğrulama hatası", Detail = ex.Message },
            OperationCanceledException => new ProblemDetails { Status = 499, Title = "İstek iptal edildi" },
            _ => new ProblemDetails
            {
                Status = 500,
                Title = "Sunucu hatası",
                Detail = _env.IsDevelopment() ? ex.ToString() : "Beklenmeyen bir hata oluştu."
            }
        };

        if (problem.Status >= 500)
            _logger.LogError(ex, "İşlenmeyen hata: {Path}", context.Request.Path);

        problem.Instance = context.Request.Path;
        problem.Extensions["traceId"] = context.TraceIdentifier;

        context.Response.StatusCode = problem.Status ?? 500;
        // Çalışma zamanı tipiyle serileştir (ValidationProblemDetails.Errors alanı kaybolmasın)
        await context.Response.WriteAsJsonAsync(problem, problem.GetType(), options: null,
            contentType: "application/problem+json");
    }
}
