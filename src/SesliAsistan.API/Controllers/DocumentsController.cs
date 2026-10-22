using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using SesliAsistan.API.Extensions;
using SesliAsistan.Business.DTOs.Documents;
using SesliAsistan.Business.Interfaces;

namespace SesliAsistan.API.Controllers;

/// <summary>FG-2 ve FG-5: Belge yükleme, listeleme, konu ağacı ve silme.</summary>
[ApiController]
[Route("api/v1/documents")]
[Authorize(Policy = "StudentOrAdmin")]
[Produces("application/json")]
public class DocumentsController : ControllerBase
{
    private readonly IDocumentService _documents;

    public DocumentsController(IDocumentService documents) => _documents = documents;

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
