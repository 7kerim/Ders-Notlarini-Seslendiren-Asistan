using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using SesliAsistan.API.Extensions;
using SesliAsistan.Business.DTOs.Documents;
using SesliAsistan.Business.DTOs.Learning;
using SesliAsistan.Business.Interfaces;

namespace SesliAsistan.API.Controllers;

/// <summary>FG-2, FG-3, FG-4, FG-5: Belge yönetimi, konu ağacı, seslendirme, özet ve dinleme konumu.</summary>
[ApiController]
[Route("api/v1/documents")]
[Authorize(Policy = "StudentOrAdmin")]
[Produces("application/json")]
public class DocumentsController : ControllerBase
{
    private readonly IDocumentService _documents;
    private readonly ITtsService _tts;
    private readonly ISummaryService _summaries;
    private readonly IListeningProgressService _progress;

    public DocumentsController(
        IDocumentService documents,
        ITtsService tts,
        ISummaryService summaries,
        IListeningProgressService progress)
    {
        _documents = documents;
        _tts = tts;
        _summaries = summaries;
        _progress = progress;
    }

    /// <summary>PDF veya .docx yükler (≤ 20 MB). Metin çıkarma arka planda yapılır; yanıt 202 Accepted döner.</summary>
    [HttpPost]
    [Consumes("multipart/form-data")]
    [RequestSizeLimit(21 * 1024 * 1024)]
    [ProducesResponseType<DocumentDetailDto>(StatusCodes.Status202Accepted)]
    [ProducesResponseType<ValidationProblemDetails>(StatusCodes.Status400BadRequest)]
    public async Task<IActionResult> Upload([FromForm] UploadDocumentForm form, CancellationToken ct)
    {
        if (form.File is null)
            return ValidationProblem(new ValidationProblemDetails(
                new Dictionary<string, string[]> { ["File"] = ["Dosya seçilmedi."] }));

        await using var stream = form.File.OpenReadStream();
        var result = await _documents.UploadAsync(
            new UploadDocumentCommand(User.GetUserId(), form.Title, form.File.FileName, form.File.Length, stream), ct);

        return AcceptedAtAction(nameof(GetById), new { id = result.Id }, result);
    }

    /// <summary>Kullanıcının belgelerini sayfalı olarak listeler; başlığa göre arama yapılabilir.</summary>
    [HttpGet]
    [ProducesResponseType<PagedResult<DocumentListItemDto>>(StatusCodes.Status200OK)]
    public async Task<ActionResult<PagedResult<DocumentListItemDto>>> GetList(
        [FromQuery] int page = 1, [FromQuery] int pageSize = 20, [FromQuery] string? search = null,
        CancellationToken ct = default) =>
        Ok(await _documents.GetListAsync(User.GetUserId(), page, pageSize, search, ct));

    /// <summary>Belge ayrıntısı, işleme durumu ve konu ağacı (FG-5).</summary>
    [HttpGet("{id:guid}")]
    [ProducesResponseType<DocumentDetailDto>(StatusCodes.Status200OK)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status404NotFound)]
    public async Task<ActionResult<DocumentDetailDto>> GetById(Guid id, CancellationToken ct) =>
        Ok(await _documents.GetDetailAsync(User.GetUserId(), id, ct));

    /// <summary>Konu ağacındaki tek bir bölümün metni (seçilen bölümü dinleme için TTS girdisi).</summary>
    [HttpGet("{id:guid}/sections/{sectionId:guid}")]
    [ProducesResponseType<SectionContentDto>(StatusCodes.Status200OK)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status404NotFound)]
    public async Task<ActionResult<SectionContentDto>> GetSection(Guid id, Guid sectionId, CancellationToken ct) =>
        Ok(await _documents.GetSectionAsync(User.GetUserId(), id, sectionId, ct));

    /// <summary>FG-3: Konu ağacından seçilen bölümün sesini üretir veya önbellekten döner.</summary>
    [HttpGet("{id:guid}/sections/{sectionId:guid}/audio")]
    [ProducesResponseType<AudioResultDto>(StatusCodes.Status200OK)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status404NotFound)]
    public async Task<ActionResult<AudioResultDto>> GetSectionAudio(
        Guid id, Guid sectionId, [FromQuery] string? voice = null, [FromQuery] decimal? speed = null, CancellationToken ct = default) =>
        Ok(await _tts.SynthesizeSectionAudioAsync(User.GetUserId(), id, sectionId, voice, speed, ct));

    /// <summary>FG-4: Bölüm için TextRank algoritmasıyla kısa/orta/detaylı sesli özet üretir.</summary>
    [HttpPost("{id:guid}/sections/{sectionId:guid}/summary")]
    [ProducesResponseType<SummaryDto>(StatusCodes.Status200OK)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status404NotFound)]
    public async Task<ActionResult<SummaryDto>> CreateSectionSummary(
        Guid id, Guid sectionId, [FromBody] CreateSummaryRequest request, CancellationToken ct = default) =>
        Ok(await _summaries.CreateSummaryAsync(User.GetUserId(), id, sectionId, request.Length, ct));

    /// <summary>FG-3: Belgenin dinleme konumunu (bölüm ve saniye) kaydeder.</summary>
    [HttpPut("{id:guid}/progress")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    public async Task<IActionResult> SaveProgress(Guid id, [FromBody] SaveProgressRequest request, CancellationToken ct = default)
    {
        await _progress.SaveProgressAsync(User.GetUserId(), id, request, ct);
        return Ok(new { message = "Dinleme konumu kaydedildi." });
    }

    /// <summary>FG-3: Belgenin son dinleme konumunu ("kaldığı yerden devam") getirir.</summary>
    [HttpGet("{id:guid}/progress")]
    [ProducesResponseType<ListeningProgressDto>(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    public async Task<ActionResult<ListeningProgressDto?>> GetProgress(Guid id, CancellationToken ct = default)
    {
        var result = await _progress.GetProgressAsync(User.GetUserId(), id, ct);
        return result is null ? NoContent() : Ok(result);
    }

    /// <summary>İşlenemeyen (Failed) belgeyi tekrar kuyruğa alır.</summary>
    [HttpPost("{id:guid}/reprocess")]
    [ProducesResponseType(StatusCodes.Status202Accepted)]
    public async Task<IActionResult> Reprocess(Guid id, CancellationToken ct)
    {
        await _documents.ReprocessAsync(User.GetUserId(), id, ct);
        return Accepted();
    }

    [HttpDelete("{id:guid}")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> Delete(Guid id, CancellationToken ct)
    {
        await _documents.DeleteAsync(User.GetUserId(), id, ct);
        return NoContent();
    }
}

public class UploadDocumentForm
{
    public IFormFile? File { get; set; }
    public string? Title { get; set; }
}
