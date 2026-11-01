using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using SesliAsistan.Business.DTOs.Learning;
using SesliAsistan.Business.Interfaces;

namespace SesliAsistan.API.Controllers;

/// <summary>FG-3: Seslendirme (TTS) ses seçenekleri ve meta verileri.</summary>
[ApiController]
[Route("api/v1/tts")]
[Authorize(Policy = "StudentOrAdmin")]
[Produces("application/json")]
public class TtsController : ControllerBase
{
    private readonly ITtsService _tts;

    public TtsController(ITtsService tts) => _tts = tts;

    /// <summary>Kullanılabilir Türkçe nöral seslerin listesini döner.</summary>
    [HttpGet("voices")]
    [ProducesResponseType<IReadOnlyList<VoiceDto>>(StatusCodes.Status200OK)]
    public ActionResult<IReadOnlyList<VoiceDto>> GetVoices() =>
        Ok(_tts.GetAvailableVoices());
}
