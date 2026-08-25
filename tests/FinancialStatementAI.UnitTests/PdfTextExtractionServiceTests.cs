using System.Text;
using FinancialStatementAI.Infrastructure.Documents;

namespace FinancialStatementAI.UnitTests;

public class PdfTextExtractionServiceTests
{
    private readonly PdfTextExtractionService _service = new();

    // A minimal PDF whose page has an actual content stream drawing text (BT/Tj/ET), so PdfPig
    // extracts real characters — as opposed to StatementFileValidatorTests' fixture, which only
    // has empty Page objects (no content stream at all), simulating a scanned page with no text layer.
    private static byte[] BuildPdfWithText(string pageText)
    {
        var contentStream = $"BT /F1 12 Tf 72 700 Td ({pageText}) Tj ET";
        var contentBytes = Encoding.ASCII.GetByteCount(contentStream);

        string[] objects =
        [
            "1 0 obj\n<< /Type /Catalog /Pages 2 0 R >>\nendobj\n",
            "2 0 obj\n<< /Type /Pages /Kids [3 0 R] /Count 1 >>\nendobj\n",
            "3 0 obj\n<< /Type /Page /Parent 2 0 R /MediaBox [0 0 612 792] /Resources << /Font << /F1 4 0 R >> >> /Contents 5 0 R >>\nendobj\n",
            "4 0 obj\n<< /Type /Font /Subtype /Type1 /BaseFont /Helvetica >>\nendobj\n",
            $"5 0 obj\n<< /Length {contentBytes} >>\nstream\n{contentStream}\nendstream\nendobj\n"
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

    // Same shape as StatementFileValidatorTests' fixture: valid PDF structure, but the page has
    // no /Contents at all — nothing for PdfPig to extract text from.
    private static byte[] BuildPdfWithNoContentStream()
    {
        string[] objects =
        [
            "1 0 obj\n<< /Type /Catalog /Pages 2 0 R >>\nendobj\n",
            "2 0 obj\n<< /Type /Pages /Kids [3 0 R] /Count 1 >>\nendobj\n",
            "3 0 obj\n<< /Type /Page /Parent 2 0 R /MediaBox [0 0 612 792] >>\nendobj\n"
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

    [Fact]
    public void Extract_A_Page_With_Substantial_Text_Is_Marked_Usable()
    {
        var pdfBytes = BuildPdfWithText(
            "01/08 AMAZON WEB SERVICES 129.45 02/08 UBER TRIP 18.20 03/08 WHOLE FOODS MARKET 64.02");

        var result = _service.Extract(new MemoryStream(pdfBytes));

        Assert.Equal(1, result.PageCount);
        Assert.True(result.HasUsableText);
        Assert.True(result.CharacterCount > 0);
        Assert.Contains("AMAZON", result.RawText);
    }

    [Fact]
    public void Extract_A_Page_With_No_Content_Stream_Is_Marked_Not_Usable()
    {
        var pdfBytes = BuildPdfWithNoContentStream();

        var result = _service.Extract(new MemoryStream(pdfBytes));

        Assert.Equal(1, result.PageCount);
        Assert.False(result.HasUsableText);
        Assert.Equal(0, result.CharacterCount);
    }
}
