using System.Text;
using FluentAssertions;
using SesliAsistan.Business.Exceptions;
using SesliAsistan.Business.Options;
using SesliAsistan.Business.Services;
using SesliAsistan.Entities.Enums;
using MsOptions = Microsoft.Extensions.Options.Options;

namespace SesliAsistan.Tests;

public class FileValidatorTests
{
    private readonly FileValidator _sut = new(MsOptions.Create(new FileStorageOptions { MaxFileSizeBytes = 1024 }));

    private static MemoryStream Bytes(string s) => new(Encoding.ASCII.GetBytes(s));

    [Fact]
    public void ValidPdf_ReturnsPdfType()
    {
        using var stream = Bytes("%PDF-1.7 ...");
        _sut.Validate("notlar.pdf", stream.Length, stream).Should().Be(DocumentFileType.Pdf);
        stream.Position.Should().Be(0); // akış başa sarılmalı
    }

    [Fact]
    public void RenamedExecutable_IsRejected()
    {
        using var stream = Bytes("MZ\u0090\u0000 sahte");
        var act = () => _sut.Validate("notlar.pdf", stream.Length, stream);
        act.Should().Throw<BusinessValidationException>();
    }

    [Fact]
    public void UnsupportedExtension_IsRejected()
    {
        using var stream = Bytes("%PDF");
        var act = () => _sut.Validate("resim.png", stream.Length, stream);
        act.Should().Throw<BusinessValidationException>();
    }

    [Fact]
    public void TooLargeFile_IsRejected()
    {
        using var stream = Bytes("%PDF");
        var act = () => _sut.Validate("buyuk.pdf", 5000, stream);
        act.Should().Throw<BusinessValidationException>();
    }
}
