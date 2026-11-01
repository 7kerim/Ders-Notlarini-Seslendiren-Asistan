using System.Net.Http.Headers;
using System.Text;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using SesliAsistan.Business.Exceptions;
using SesliAsistan.Business.Interfaces;
using SesliAsistan.Business.Options;

namespace SesliAsistan.Business.Speech;

public class MockTtsProvider : ITtsProvider
{
    // Minimal geçerli bir MP3 frame'i (test ve sahte ses için)
    private static readonly byte[] MockMp3Bytes = [
        0xFF, 0xFB, 0x90, 0x64, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00,
        0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00,
        0x54, 0x41, 0x47, 0x53, 0x65, 0x73, 0x6C, 0x69, 0x41, 0x73, 0x69, 0x73
    ];

    public Task<byte[]> SynthesizeAsync(string ssml, string voice, decimal speed, CancellationToken ct = default)
    {
        // Metin uzunluğuna göre simüle edilmiş ses baytları döner
        var textLength = ssml.Length;
        var result = new byte[Math.Max(MockMp3Bytes.Length, textLength * 10)];
        Buffer.BlockCopy(MockMp3Bytes, 0, result, 0, MockMp3Bytes.Length);
        return Task.FromResult(result);
    }
}

public class AzureSpeechTtsProvider : ITtsProvider
{
    private readonly HttpClient _httpClient;
    private readonly TtsOptions _options;
    private readonly ILogger<AzureSpeechTtsProvider> _logger;

    public AzureSpeechTtsProvider(
        HttpClient httpClient,
        IOptions<TtsOptions> options,
        ILogger<AzureSpeechTtsProvider> logger)
    {
        _httpClient = httpClient;
        _options = options.Value;
        _logger = logger;
    }

    public async Task<byte[]> SynthesizeAsync(string ssml, string voice, decimal speed, CancellationToken ct = default)
    {
        // Eğer Azure API anahtarı yapılandırılmamışsa doğrudan Mock sağlayıcı gibi davranır
        if (string.IsNullOrWhiteSpace(_options.AzureApiKey))
        {
            _logger.LogInformation("Azure API anahtarı tanımlı değil, Mock TTS kullanılıyor.");
            return await new MockTtsProvider().SynthesizeAsync(ssml, voice, speed, ct);
        }

        var endpoint = $"https://{_options.AzureRegion}.tts.speech.microsoft.com/cognitiveservices/v1";

        int maxRetries = 3;
        int delayMs = 1000;

        for (int attempt = 1; attempt <= maxRetries; attempt++)
        {
            try
            {
                using var request = new HttpRequestMessage(HttpMethod.Post, endpoint);
                request.Headers.Add("Ocp-Apim-Subscription-Key", _options.AzureApiKey);
                request.Headers.Add("X-Microsoft-OutputFormat", "audio-24khz-48kbitrate-mono-mp3");
                request.Headers.Add("User-Agent", "SesliAsistan");
                request.Content = new StringContent(ssml, Encoding.UTF8, "application/ssml+xml");

                var response = await _httpClient.SendAsync(request, ct);

                if (response.IsSuccessStatusCode)
                {
                    return await response.Content.ReadAsByteArrayAsync(ct);
                }

                if ((int)response.StatusCode == 429 && attempt < maxRetries)
                {
                    _logger.LogWarning("Azure TTS 429 (Kota/Hız Sınırı) alındı. Deneme {Attempt}/{MaxRetries}", attempt, maxRetries);
                    await Task.Delay(delayMs, ct);
                    delayMs *= 2;
                    continue;
                }

                var errorBody = await response.Content.ReadAsStringAsync(ct);
                _logger.LogError("Azure TTS hatası {StatusCode}: {ErrorBody}", response.StatusCode, errorBody);
                throw new BusinessValidationException(new Dictionary<string, string[]>
                {
                    ["TTS"] = [$"Ses sentezleme servisi hata verdi (Kod: {response.StatusCode})."]
                });
            }
            catch (Exception ex) when (attempt < maxRetries && ex is not BusinessValidationException)
            {
                _logger.LogWarning(ex, "Azure TTS bağlantı hatası. Yeniden deneniyor... Deneme: {Attempt}", attempt);
                await Task.Delay(delayMs, ct);
                delayMs *= 2;
            }
        }

        throw new BusinessValidationException(new Dictionary<string, string[]>
        {
            ["TTS"] = ["Ses servisine ulaşılamadı. Lütfen daha sonra tekrar deneyin."]
        });
    }
}
