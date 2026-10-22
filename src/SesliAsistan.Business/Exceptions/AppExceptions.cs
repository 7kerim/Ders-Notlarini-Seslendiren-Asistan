namespace SesliAsistan.Business.Exceptions;

/// <summary>İş kuralı ihlallerinde fırlatılan, HTTP durum koduna eşlenen temel istisna.</summary>
public abstract class AppException : Exception
{
    public int StatusCode { get; }
    public string Title { get; }

    protected AppException(int statusCode, string title, string message) : base(message)
    {
        StatusCode = statusCode;
        Title = title;
    }
}

public sealed class NotFoundException(string message)
    : AppException(404, "Kayıt bulunamadı", message);

public sealed class ConflictException(string message)
    : AppException(409, "Çakışma", message);

public sealed class UnauthorizedAppException(string message)
    : AppException(401, "Kimlik doğrulama hatası", message);

public sealed class ForbiddenAppException(string message)
    : AppException(403, "Yetkisiz işlem", message);

public sealed class BusinessValidationException : AppException
{
    public IReadOnlyDictionary<string, string[]> Errors { get; }

    public BusinessValidationException(IReadOnlyDictionary<string, string[]> errors)
        : base(400, "Doğrulama hatası", "Gönderilen veriler geçersiz.")
        => Errors = errors;

    public BusinessValidationException(string field, string error)
        : this(new Dictionary<string, string[]> { [field] = new[] { error } }) { }
}
