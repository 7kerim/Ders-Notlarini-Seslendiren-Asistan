using FluentValidation;
using SesliAsistan.Business.DTOs.Auth;

namespace SesliAsistan.Business.Validation;

public class RegisterRequestValidator : AbstractValidator<RegisterRequest>
{
    public RegisterRequestValidator()
    {
        RuleFor(x => x.Email)
            .NotEmpty().WithMessage("E-posta zorunludur.")
            .EmailAddress().WithMessage("Geçerli bir e-posta adresi giriniz.")
            .MaximumLength(256);

        RuleFor(x => x.FullName)
            .NotEmpty().WithMessage("Ad soyad zorunludur.")
            .Length(2, 120);

        RuleFor(x => x.Password).SetValidator(new PasswordPolicyValidator());
    }
}

public class LoginRequestValidator : AbstractValidator<LoginRequest>
{
    public LoginRequestValidator()
    {
        RuleFor(x => x.Email).NotEmpty().EmailAddress();
        RuleFor(x => x.Password).NotEmpty();
    }
}

public class UpdateProfileRequestValidator : AbstractValidator<UpdateProfileRequest>
{
    public UpdateProfileRequestValidator()
    {
        RuleFor(x => x.FullName).NotEmpty().Length(2, 120);
        RuleFor(x => x.PreferredVoice).NotEmpty().MaximumLength(64);
        // FG-3: oynatma hızı 0.5x – 2x
        RuleFor(x => x.PlaybackSpeed).InclusiveBetween(0.5m, 2.0m)
            .WithMessage("Oynatma hızı 0.5 ile 2.0 arasında olmalıdır.");
    }
}

public class ChangePasswordRequestValidator : AbstractValidator<ChangePasswordRequest>
{
    public ChangePasswordRequestValidator()
    {
        RuleFor(x => x.CurrentPassword).NotEmpty();
        RuleFor(x => x.NewPassword).SetValidator(new PasswordPolicyValidator());
    }
}

/// <summary>Şifre politikası: en az 8 karakter, büyük harf, küçük harf ve rakam.</summary>
public class PasswordPolicyValidator : AbstractValidator<string>
{
    public PasswordPolicyValidator()
    {
        RuleFor(x => x)
            .NotEmpty().WithMessage("Şifre zorunludur.")
            .MinimumLength(8).WithMessage("Şifre en az 8 karakter olmalıdır.")
            .MaximumLength(72).WithMessage("Şifre en fazla 72 karakter olabilir.") // BCrypt sınırı
            .Matches("[A-ZÇĞİÖŞÜ]").WithMessage("Şifre en az bir büyük harf içermelidir.")
            .Matches("[a-zçğıöşü]").WithMessage("Şifre en az bir küçük harf içermelidir.")
            .Matches("[0-9]").WithMessage("Şifre en az bir rakam içermelidir.")
            .OverridePropertyName("Password");
    }
}

public static class ValidationExtensions
{
    /// <summary>FluentValidation sonucunu, API katmanında 400 ProblemDetails'e çevrilen istisnaya dönüştürür.</summary>
    public static async Task ValidateOrThrowAsync<T>(this IValidator<T> validator, T instance, CancellationToken ct = default)
    {
        var result = await validator.ValidateAsync(instance, ct);
        if (result.IsValid) return;

        var errors = result.Errors
            .GroupBy(e => e.PropertyName)
            .ToDictionary(g => g.Key, g => g.Select(e => e.ErrorMessage).Distinct().ToArray());
        throw new Exceptions.BusinessValidationException(errors);
    }
}
