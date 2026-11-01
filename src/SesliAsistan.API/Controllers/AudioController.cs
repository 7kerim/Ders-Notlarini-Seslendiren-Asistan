using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using SesliAsistan.Business.Interfaces;

namespace SesliAsistan.API.Controllers;

/// <summary>FG-3: Üretilmiş ses dosyalarının akış (stream) olarak istemciye iletilmesi.</summary>
[ApiController]
[Route("api/v1/audio")]
public class AudioController : ControllerBase
{
    private readonly ITtsService _tts;

    public AudioController(ITtsService tts) => _tts = tts;

    /// <summary>
    /// Ses dosyasını akış olarak iletir. HTTP Range desteği ile oynatıcıda ileri-geri sarma yapılabilir.
    /// </summary>
    [HttpGet("{audioId:guid}")]
    [AllowAnonymous]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status206PartialContent)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> StreamAudio(Guid audioId, CancellationToken ct)
    {
        var (stream, contentType, length) = await _tts.GetAudioStreamAsync(audioId, ct);
        return File(stream, contentType, enableRangeProcessing: true);
    }
}
