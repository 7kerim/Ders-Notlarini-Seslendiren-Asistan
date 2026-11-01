using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using SesliAsistan.Business.DTOs.Auth;
using SesliAsistan.Business.Exceptions;
using SesliAsistan.Entities.Entities;

namespace SesliAsistan.Tests;

public class PasswordResetTests
{
    private static readonly RegisterRequest ValidRegister = new("ogrenci@firat.edu.tr", "EskiSifre123", "Test Öğrenci");

    [Fact]
    public async Task ForgotPassword_ValidUser_GeneratesHashedCodeAndSendsEmail()
    {
        await using var db = TestHelpers.CreateDb();
        var emailSender = new TestEmailSender();
        var sut = TestHelpers.CreateAuthService(db, emailSender);

        await sut.RegisterAsync(ValidRegister, null);
        await sut.ForgotPasswordAsync(new ForgotPasswordRequest(ValidRegister.Email));

        emailSender.LastRecipient.Should().Be(ValidRegister.Email);
        emailSender.LastCode.Should().NotBeNullOrWhiteSpace();
        emailSender.LastCode!.Length.Should().Be(6);

        var codeRecord = await db.PasswordResetCodes.SingleOrDefaultAsync();
        codeRecord.Should().NotBeNull();
        codeRecord!.CodeHash.Should().NotBe(emailSender.LastCode); // Düz metin değil hash saklanır
        codeRecord.FailedAttempts.Should().Be(0);
        codeRecord.IsUsed.Should().BeFalse();
    }

    [Fact]
    public async Task ForgotPassword_NonExistentEmail_DoesNotThrowAndDoesNotSendEmail()
    {
        await using var db = TestHelpers.CreateDb();
        var emailSender = new TestEmailSender();
        var sut = TestHelpers.CreateAuthService(db, emailSender);

        // Kayıtlı olmayan e-posta ile istek yapıldığında hata fırlatılmamalı (user enumeration engeli)
        var act = () => sut.ForgotPasswordAsync(new ForgotPasswordRequest("kayitsiz@firat.edu.tr"));

        await act.Should().NotThrowAsync();
        emailSender.LastCode.Should().BeNull();
    }

    [Fact]
    public async Task ResetPassword_ExpiredCode_ThrowsUnauthorized()
    {
        await using var db = TestHelpers.CreateDb();
        var emailSender = new TestEmailSender();
        var sut = TestHelpers.CreateAuthService(db, emailSender);

        await sut.RegisterAsync(ValidRegister, null);
        await sut.ForgotPasswordAsync(new ForgotPasswordRequest(ValidRegister.Email));

        // Kodu geçmiş zamana çekerek süresini doldur
        var codeRecord = await db.PasswordResetCodes.SingleAsync();
        codeRecord.ExpiresAt = DateTime.UtcNow.AddMinutes(-1);
        await db.SaveChangesAsync();

        var act = () => sut.ResetPasswordAsync(new ResetPasswordRequest(
            ValidRegister.Email, emailSender.LastCode!, "YeniGucluSifre123"));

        await act.Should().ThrowAsync<UnauthorizedAppException>();
    }

    [Fact]
    public async Task ResetPassword_FiveFailedAttempts_InvalidatesCode()
    {
        await using var db = TestHelpers.CreateDb();
        var emailSender = new TestEmailSender();
        var sut = TestHelpers.CreateAuthService(db, emailSender);

        await sut.RegisterAsync(ValidRegister, null);
        await sut.ForgotPasswordAsync(new ForgotPasswordRequest(ValidRegister.Email));

        // 5 kez hatalı kod dene
        for (int i = 0; i < 5; i++)
        {
            var act = () => sut.ResetPasswordAsync(new ResetPasswordRequest(
                ValidRegister.Email, "999999", "YeniGucluSifre123"));
            await act.Should().ThrowAsync<UnauthorizedAppException>();
        }

        // Artık doğru kod bile girilse kod iptal edilmiş olmalı
        var correctAct = () => sut.ResetPasswordAsync(new ResetPasswordRequest(
            ValidRegister.Email, emailSender.LastCode!, "YeniGucluSifre123"));

        await correctAct.Should().ThrowAsync<UnauthorizedAppException>();

        var codeRecord = await db.PasswordResetCodes.SingleAsync();
        codeRecord.IsUsed.Should().BeTrue();
    }

    [Fact]
    public async Task ResetPassword_Success_UpdatesPasswordAndRevokesAllRefreshTokens()
    {
        await using var db = TestHelpers.CreateDb();
        var emailSender = new TestEmailSender();
        var sut = TestHelpers.CreateAuthService(db, emailSender);

        var auth = await sut.RegisterAsync(ValidRegister, null);
        await sut.ForgotPasswordAsync(new ForgotPasswordRequest(ValidRegister.Email));

        var newPassword = "YeniSuperSifre456";
        await sut.ResetPasswordAsync(new ResetPasswordRequest(ValidRegister.Email, emailSender.LastCode!, newPassword));

        // Kullanıcı yeni şifresiyle giriş yapabilmeli
        var newLogin = await sut.LoginAsync(new LoginRequest(ValidRegister.Email, newPassword), null);
        newLogin.AccessToken.Should().NotBeNullOrWhiteSpace();

        // Eski açık oturumlar kapatılmış olmalı
        var activeTokens = await db.RefreshTokens.Where(t => t.UserId == auth.User.Id && t.RevokedAt == null).ToListAsync();
        // Yeni login sonrası sadece 1 yeni token aktif olmalı (öncekiler revoked)
        activeTokens.Should().HaveCount(1);
    }

    [Fact]
    public async Task ResetPassword_WeakPassword_ThrowsValidationException()
    {
        await using var db = TestHelpers.CreateDb();
        var emailSender = new TestEmailSender();
        var sut = TestHelpers.CreateAuthService(db, emailSender);

        await sut.RegisterAsync(ValidRegister, null);
        await sut.ForgotPasswordAsync(new ForgotPasswordRequest(ValidRegister.Email));

        var act = () => sut.ResetPasswordAsync(new ResetPasswordRequest(
            ValidRegister.Email, emailSender.LastCode!, "kisa1"));

        await act.Should().ThrowAsync<BusinessValidationException>();
    }
}
