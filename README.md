# Ders Notlarını Seslendiren Öğrenme Asistanı — Backend

C# / .NET 9 Web API, N-Tier (katmanlı) mimari, PostgreSQL, Redis.

## Katmanlar

| Proje | Sorumluluk |
|---|---|
| `SesliAsistan.Entities` | Varlık (entity) sınıfları ve enum'lar — hiçbir katmana bağımlı değil |
| `SesliAsistan.DataAccess` | EF Core `AppDbContext`, Fluent API konfigürasyonları, repository'ler, Unit of Work |
| `SesliAsistan.Business` | İş kuralları, DTO'lar, doğrulama (FluentValidation), JWT/BCrypt, metin çıkarma (PdfPig, OpenXML) |
| `SesliAsistan.API` | Controller'lar, kimlik doğrulama, hata yönetimi, Swagger, arka plan işleyicisi |
| `SesliAsistan.Tests` | xUnit birim testleri |

Bağımlılık yönü: `API → Business → DataAccess → Entities`

## Çalıştırma

```bash
docker compose up -d                       # PostgreSQL + Redis
dotnet tool install --global dotnet-ef     # bir kez
dotnet ef migrations add InitialCreate -p src/SesliAsistan.DataAccess -s src/SesliAsistan.API
dotnet run --project src/SesliAsistan.API  # Swagger: https://localhost:7180/swagger
dotnet test                                # birim testleri
```

Redis kullanmak için `appsettings.json` → `ConnectionStrings:Redis` alanına `localhost:6379` yazın;
boş bırakılırsa bellek içi önbellek kullanılır.

Üretim ortamında `Jwt:Secret` değeri **asla** dosyaya yazılmamalı; ortam değişkeni
(`Jwt__Secret`) veya `dotnet user-secrets` ile verilmelidir.

## Hazır uç noktalar (v1)

| Yöntem | Uç nokta | Açıklama | Gereksinim |
|---|---|---|---|
| POST | `/api/v1/auth/register` | Kayıt | FG-1 |
| POST | `/api/v1/auth/login` | Giriş (JWT + refresh token) | FG-1 |
| POST | `/api/v1/auth/refresh` | Token yenileme (rotasyon) | FG-1 |
| POST | `/api/v1/auth/logout` | Çıkış | FG-1 |
| GET/PUT/DELETE | `/api/v1/users/me` | Profil görüntüle / güncelle / hesabı sil (KVKK) | FG-1 |
| PUT | `/api/v1/users/me/password` | Şifre değiştir | FG-1 |
| POST | `/api/v1/documents` | PDF/.docx yükleme (≤ 20 MB, 202 Accepted) | FG-2 |
| GET | `/api/v1/documents` | Sayfalı listeleme + arama | FG-2 |
| GET | `/api/v1/documents/{id}` | Ayrıntı + işleme durumu + konu ağacı | FG-2, FG-5 |
| GET | `/api/v1/documents/{id}/sections/{sectionId}` | Bölüm metni | FG-5 |
| POST | `/api/v1/documents/{id}/reprocess` | Hatalı belgeyi yeniden işle | FG-2 |
| DELETE | `/api/v1/documents/{id}` | Belge silme | FG-2 |
| GET | `/health` | Sağlık kontrolü | — |
