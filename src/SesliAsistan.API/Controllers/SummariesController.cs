using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using SesliAsistan.API.Extensions;
using SesliAsistan.Business.DTOs.Learning;
using SesliAsistan.Business.Interfaces;

namespace SesliAsistan.API.Controllers;

/// <summary>FG-4: Sesli özet yönetimi ve özet seslendirme uç noktaları.</summary>
[ApiController]
[Route("api/v1/summaries")]
[Authorize(Policy = "StudentOrAdmin")]
[Produces("application/json")]
public class SummariesController : ControllerBase
{
    private readonly ISummaryService _summaries;
    private readonly ITtsService _tts;

    public SummariesController(ISummaryService summaries, ITtsService tts)
    {
        _summaries = summaries;
        _tts = tts;
    }

    /// <summary>Özet metnini getirir.</summary>
    [HttpGet("{summaryId:guid}")]
    [ProducesResponseType<SummaryDto>(StatusCodes.Status200OK)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status404NotFound)]
    public async Task<ActionResult<SummaryDto>> GetSummary(Guid summaryId, CancellationToken ct) =>
        Ok(await _summaries.GetSummaryAsync(User.GetUserId(), summaryId, ct));

    /// <summary>FG-4: Üretilen özetin seslendirilmiş MP3 hâlini döner (önbellek destekli).</summary>
    [HttpGet("{summaryId:guid}/audio")]
    [ProducesResponseType<AudioResultDto>(StatusCodes.Status200OK)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status404NotFound)]
    public async Task<ActionResult<AudioResultDto>> GetSummaryAudio(
        Guid summaryId, [FromQuery] string? voice = null, [FromQuery] decimal? speed = null, CancellationToken ct = default) =>
        Ok(await _tts.SynthesizeSummaryAudioAsync(User.GetUserId(), summaryId, voice, speed, ct));
}
