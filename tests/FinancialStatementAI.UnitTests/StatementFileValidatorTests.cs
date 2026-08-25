using System.Text;
using FinancialStatementAI.Infrastructure.Documents;

namespace FinancialStatementAI.UnitTests;

public class StatementFileValidatorTests
{
    private readonly StatementFileValidator _validator = new();

    // A minimal, well-formed single-page PDF with a correctly byte-offset xref table (built
    // programmatically rather than hardcoded, since every offset must be exact for a PDF reader
    // to accept the file without needing to fall back to repair/recovery scanning).
    private static readonly byte[] MinimalValidPdf = BuildMinimalPdf();

    private static byte[] BuildMinimalPdf()
    {
        string[] objects =
        [
            "1 0 obj\n<< /Type /Catalog /Pages 2 0 R >>\nendobj\n",
            "2 0 obj\n<< /Type /Pages /Kids [3 0 R] /Count 1 >>\nendobj\n",
            "3 0 obj\n<< /Type /Page /Parent 2 0 R /MediaBox [0 0 200 200] >>\nendobj\n"
        ];

        var body = new StringBuilder("%PDF-1.4\n");
        var offsets = new List<int>();
        foreach (var obj in objects)
        {
            offsets.Add(Encoding.ASCII.GetByteCount(body.ToString()));
            body.Append(obj);
        }

        var xrefOffset = Encoding.ASCII.GetByteCount(body.ToString());
        body.Append("xref\n");
        body.Append($"0 {objects.Length + 1}\n");
        body.Append("0000000000 65535 f \n");
        foreach (var offset in offsets)
        {
            body.Append($"{offset:D10} 00000 n \n");
        }
        body.Append("trailer\n");
        body.Append($"<< /Size {objects.Length + 1} /Root 1 0 R >>\n");
        body.Append("startxref\n");
        body.Append($"{xrefOffset}\n");
        body.Append("%%EOF");

        return Encoding.ASCII.GetBytes(body.ToString());
    }

    private static readonly byte[] MinimalValidPng =
    [
        0x89, 0x50, 0x4E, 0x47, 0x0D, 0x0A, 0x1A, 0x0A, // PNG signature
        0x00, 0x00, 0x00, 0x0D, 0x49, 0x48, 0x44, 0x52 // (rest of a real PNG isn't needed — see note below)
    ];

    private static readonly byte[] MinimalValidJpeg = [0xFF, 0xD8, 0xFF, 0xE0, 0x00, 0x10];

    [Fact]
    public void Validate_Accepts_A_WellFormed_Pdf()
    {
        var result = _validator.Validate(MinimalValidPdf, "statement.pdf", MinimalValidPdf.Length);

        Assert.True(result.IsValid);
        Assert.Equal("application/pdf", result.ConfirmedContentType);
    }

    [Fact]
    public void Validate_Rejects_A_Pdf_That_Is_Not_Actually_Openable()
    {
        var garbage = Encoding.ASCII.GetBytes("%PDF-1.4\nthis is not a real pdf body at all");

        var result = _validator.Validate(garbage, "statement.pdf", garbage.Length);

        Assert.False(result.IsValid);
        Assert.Contains("corrupted", result.ErrorMessage, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void Validate_Accepts_A_Png_By_Its_Magic_Bytes()
    {
        var result = _validator.Validate(MinimalValidPng, "statement.png", MinimalValidPng.Length);

        Assert.True(result.IsValid);
        Assert.Equal("image/png", result.ConfirmedContentType);
    }

    [Fact]
    public void Validate_Accepts_A_Jpeg_By_Its_Magic_Bytes()
    {
        var result = _validator.Validate(MinimalValidJpeg, "statement.jpg", MinimalValidJpeg.Length);

        Assert.True(result.IsValid);
        Assert.Equal("image/jpeg", result.ConfirmedContentType);
    }

    [Fact]
    public void Validate_Rejects_Content_That_Does_Not_Match_Its_Extension()
    {
        // PNG bytes, but claiming to be a .pdf — should not pass just because the extension is allowed.
        var result = _validator.Validate(MinimalValidPng, "statement.pdf", MinimalValidPng.Length);

        Assert.False(result.IsValid);
        Assert.Contains("does not match", result.ErrorMessage, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void Validate_Rejects_Unsupported_Extensions()
    {
        var result = _validator.Validate([1, 2, 3], "statement.exe", 3);

        Assert.False(result.IsValid);
        Assert.Contains("Unsupported file type", result.ErrorMessage);
    }

    [Fact]
    public void Validate_Rejects_Empty_Files()
    {
        var result = _validator.Validate([], "statement.pdf", 0);

        Assert.False(result.IsValid);
        Assert.Contains("empty", result.ErrorMessage, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void Validate_Rejects_Files_Over_The_Size_Limit()
    {
        var result = _validator.Validate(MinimalValidPdf, "statement.pdf", 21 * 1024 * 1024);

        Assert.False(result.IsValid);
        Assert.Contains("maximum allowed size", result.ErrorMessage);
    }
}
