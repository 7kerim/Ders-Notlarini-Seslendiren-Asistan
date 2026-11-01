using FluentValidation;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using SesliAsistan.Business.Interfaces;
using SesliAsistan.Business.Options;
using SesliAsistan.Business.Services;
using SesliAsistan.Business.TextExtraction;

namespace SesliAsistan.Business;

public static class BusinessServiceRegistration
{
    public static IServiceCollection AddBusiness(this IServiceCollection services, IConfiguration configuration)
    {
        services.Configure<JwtOptions>(configuration.GetSection(JwtOptions.SectionName));
        services.Configure<FileStorageOptions>(configuration.GetSection(FileStorageOptions.SectionName));
        services.Configure<TtsOptions>(configuration.GetSection(TtsOptions.SectionName));

        services.AddValidatorsFromAssembly(typeof(BusinessServiceRegistration).Assembly);

        services.AddSingleton<IPasswordHasher, BCryptPasswordHasher>();
        services.AddSingleton<ITokenService, JwtTokenService>();
        services.AddSingleton<IFileValidator, FileValidator>();
        services.AddSingleton<IFileStorage, LocalFileStorage>();
        services.AddSingleton<IDocumentProcessingQueue, DocumentProcessingQueue>();
        services.AddSingleton<IEmailSender, LoggerEmailSender>();
        services.AddSingleton<ITextRankSummarizer, SesliAsistan.Business.Speech.TextRankSummarizer>();

        services.AddHttpClient<ITtsProvider, SesliAsistan.Business.Speech.AzureSpeechTtsProvider>();

        services.AddSingleton<ITextExtractor, PdfTextExtractor>();
        services.AddSingleton<ITextExtractor, DocxTextExtractor>();

        services.AddScoped<IAuthService, AuthService>();
        services.AddScoped<IUserService, UserService>();
        services.AddScoped<IDocumentService, DocumentService>();
        services.AddScoped<IDocumentProcessor, DocumentProcessor>();
        services.AddScoped<ITtsService, TtsService>();
        services.AddScoped<IListeningProgressService, ListeningProgressService>();
        services.AddScoped<ISummaryService, SummaryService>();
        return services;
    }
}
