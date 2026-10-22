using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using SesliAsistan.Business.DTOs.Auth;
using SesliAsistan.Business.Exceptions;

namespace SesliAsistan.Tests;

public class AuthServiceTests
{
    private static readonly RegisterRequest ValidRegister = new("Ogrenci@Firat.edu.tr", "Guclu1234", "Test Öğrenci");

    [Fact]
    public async Task Register_ValidRequest_CreatesUserWithHashedPassword()
    {
        await using var db = TestHelpers.CreateDb();
        var sut = TestHelpers.CreateAuthService(db);

        var result = await sut.RegisterAsync(ValidRegister, "127.0.0.1");

        result.AccessToken.Should().NotBeNullOrWhiteSpace();
        result.RefreshToken.Should().NotBeNullOrWhiteSpace();
        result.User.Email.Should().Be("ogrenci@firat.edu.tr"); // e-posta normalize edilir

        var user = await db.Users.SingleAsync();
        user.PasswordHash.Should().NotBe(ValidRegister.Password);
        user.PasswordHash.Should().StartWith("$2"); // BCrypt özeti
        (await db.RefreshTokens.CountAsync()).Should().Be(1);
    }

    [Fact]
    public async Task Register_DuplicateEmail_ThrowsConflict()
    {
        await using var db = TestHelpers.CreateDb();
        var sut = TestHelpers.CreateAuthService(db);
        await sut.RegisterAsync(ValidRegister, null);

        var act = () => sut.RegisterAsync(ValidRegister with { Email = "OGRENCI@firat.edu.tr" }, null);

        await act.Should().ThrowAsync<ConflictException>();
    }

    [Theory]
    [InlineData("kisa1A")]        // 8 karakterden kısa
    [InlineData("sadecekucuk1")]  // büyük harf yok
    [InlineData("SadeceHarf")]    // rakam yok
    public async Task Register_WeakPassword_ThrowsValidation(string password)
    {
        await using var db = TestHelpers.CreateDb();
        var sut = TestHelpers.CreateAuthService(db);

        var act = () => sut.RegisterAsync(ValidRegister with { Password = password }, null);

        await act.Should().ThrowAsync<BusinessValidationException>();
    }

    [Fact]
    public async Task Login_WrongPassword_ThrowsUnauthorized()
    {
        await using var db = TestHelpers.CreateDb();
        var sut = TestHelpers.CreateAuthService(db);
        await sut.RegisterAsync(ValidRegister, null);

        var act = () => sut.LoginAsync(new LoginRequest(ValidRegister.Email, "YanlisSifre1"), null);

        await act.Should().ThrowAsync<UnauthorizedAppException>();
    }

    [Fact]
    public async Task Refresh_RotatesToken_AndOldTokenCannotBeReused()
    {
        await using var db = TestHelpers.CreateDb();
        var sut = TestHelpers.CreateAuthService(db);
        var first = await sut.RegisterAsync(ValidRegister, null);

        var second = await sut.RefreshAsync(first.RefreshToken, null);
        second.RefreshToken.Should().NotBe(first.RefreshToken);

        var reuse = () => sut.RefreshAsync(first.RefreshToken, null);
        await reuse.Should().ThrowAsync<UnauthorizedAppException>();
    }
}
