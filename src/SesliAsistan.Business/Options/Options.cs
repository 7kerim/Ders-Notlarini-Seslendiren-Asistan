namespace SesliAsistan.Business.Options;

public class JwtOptions
{
    public const string SectionName = "Jwt";
    public string Issuer { get; set; } = "SesliAsistan";
    public string Audience { get; set; } = "SesliAsistan.Mobile";
    /// <summary>En az 32 karakter. Üretimde ortam değişkeni / user-secrets ile verilir.</summary>
    public string Secret { get; set; } = null!;
    public int AccessTokenMinutes { get; set; } = 30;
    public int RefreshTokenDays { get; set; } = 14;
}

public class FileStorageOptions
{
    public const string SectionName = "FileStorage";
    public string RootPath { get; set; } = "storage";
    /// <summary>Fonksiyonel olmayan gereksinim: dosya boyutu ≤ 20 MB.</summary>
    public long MaxFileSizeBytes { get; set; } = 20 * 1024 * 1024;
}
