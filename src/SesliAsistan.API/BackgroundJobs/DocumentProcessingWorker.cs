using SesliAsistan.Business.Interfaces;

namespace SesliAsistan.API.BackgroundJobs;

/// <summary>Kuyruktaki belgeleri arka planda sırayla işleyen servis (HTTP isteğini bekletmez).</summary>
public class DocumentProcessingWorker : BackgroundService
{
    private readonly IDocumentProcessingQueue _queue;
    private readonly IServiceScopeFactory _scopeFactory;
    private readonly ILogger<DocumentProcessingWorker> _logger;

    public DocumentProcessingWorker(
        IDocumentProcessingQueue queue,
        IServiceScopeFactory scopeFactory,
        ILogger<DocumentProcessingWorker> logger)
    {
        _queue = queue;
        _scopeFactory = scopeFactory;
        _logger = logger;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        _logger.LogInformation("Belge işleme servisi başladı.");

        await foreach (var documentId in _queue.DequeueAllAsync(stoppingToken))
        {
            try
            {
                using var scope = _scopeFactory.CreateScope();
                var processor = scope.ServiceProvider.GetRequiredService<IDocumentProcessor>();
                await processor.ProcessAsync(documentId, stoppingToken);
            }
            catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
            {
                break;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Belge işleme kuyruğunda hata: {DocumentId}", documentId);
            }
        }
    }
}
