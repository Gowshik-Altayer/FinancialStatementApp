using ClosedXML.Excel;
using FinancialStatementAI.Application.DTOs.Statements;
using FinancialStatementAI.Application.Interfaces;
using FinancialStatementAI.Domain.Constants;
using UglyToad.PdfPig;
using UglyToad.PdfPig.Exceptions;

namespace FinancialStatementAI.Infrastructure.Documents;

/// <summary>Validates a candidate statement upload using the file's actual bytes, not just the
/// client-supplied extension/Content-Type header — see requirement #45 (MIME validation as a
/// real security control) and #1/#14 (corrupted PDFs, password-protected PDFs).</summary>
public class StatementFileValidator : IStatementFileValidator
{
    public FileValidationResult Validate(byte[] content, string fileName, long fileSizeBytes)
    {
        if (content.Length == 0)
        {
            return FileValidationResult.Failure("The uploaded file is empty.");
        }

        if (fileSizeBytes > FileUploadLimits.MaxFileSizeBytes)
        {
            return FileValidationResult.Failure(
                $"File exceeds the maximum allowed size of {FileUploadLimits.MaxFileSizeBytes / (1024 * 1024)} MB.");
        }

        var extension = Path.GetExtension(fileName);
        if (string.IsNullOrEmpty(extension) || !FileUploadLimits.AllowedExtensionsToContentType.TryGetValue(extension, out var expectedContentType))
        {
            return FileValidationResult.Failure(
                "Unsupported file type. Allowed types: PDF, JPG, JPEG, PNG, XLSX.");
        }

        var sniffedContentType = SniffContentType(content);
        if (sniffedContentType is null || sniffedContentType != expectedContentType)
        {
            return FileValidationResult.Failure(
                "The file's content does not match its extension. The file may be corrupted or mislabeled.");
        }

        if (sniffedContentType == "application/pdf")
        {
            var pdfCheck = ValidatePdf(content);
            if (pdfCheck is not null)
            {
                return FileValidationResult.Failure(pdfCheck);
            }
        }

        if (sniffedContentType == FileUploadLimits.SpreadsheetContentType)
        {
            var xlsxCheck = ValidateSpreadsheet(content);
            if (xlsxCheck is not null)
            {
                return FileValidationResult.Failure(xlsxCheck);
            }
        }

        return FileValidationResult.Success(sniffedContentType);
    }

    private static string? SniffContentType(byte[] content)
    {
        if (content.Length >= 5 && content[0] == 0x25 && content[1] == 0x50 && content[2] == 0x44 && content[3] == 0x46 && content[4] == 0x2D)
        {
            return "application/pdf"; // %PDF-
        }

        if (content.Length >= 3 && content[0] == 0xFF && content[1] == 0xD8 && content[2] == 0xFF)
        {
            return "image/jpeg";
        }

        if (content.Length >= 8 && content[0] == 0x89 && content[1] == 0x50 && content[2] == 0x4E && content[3] == 0x47
            && content[4] == 0x0D && content[5] == 0x0A && content[6] == 0x1A && content[7] == 0x0A)
        {
            return "image/png";
        }

        // .xlsx (like every OOXML format — .docx, .pptx too) is a ZIP archive, so this magic byte
        // alone is ambiguous with any other ZIP-based format or a plain .zip renamed to .xlsx.
        // ValidateSpreadsheet below is the actual disambiguator: only a file ClosedXML can open as
        // a real workbook passes, exactly mirroring how ValidatePdf both confirms the format AND
        // rejects a corrupted/encrypted file.
        if (content.Length >= 4 && content[0] == 0x50 && content[1] == 0x4B && content[2] == 0x03 && content[3] == 0x04)
        {
            return FileUploadLimits.SpreadsheetContentType;
        }

        return null;
    }

    /// <returns>An error message if the PDF is corrupted or password-protected; otherwise null.</returns>
    private static string? ValidatePdf(byte[] content)
    {
        try
        {
            using var document = PdfDocument.Open(content);
            _ = document.NumberOfPages;
            return null;
        }
        catch (PdfDocumentEncryptedException)
        {
            return "This PDF is password-protected. Please upload an unprotected copy.";
        }
        catch (Exception)
        {
            return "This PDF appears to be corrupted and could not be opened.";
        }
    }

    /// <returns>An error message if the file isn't actually a valid Excel workbook (corrupted, or
    /// a different ZIP-based format renamed to .xlsx); otherwise null.</returns>
    private static string? ValidateSpreadsheet(byte[] content)
    {
        try
        {
            using var stream = new MemoryStream(content);
            using var workbook = new XLWorkbook(stream);
            _ = workbook.Worksheets.Count;
            return null;
        }
        catch (Exception)
        {
            return "This file does not appear to be a valid Excel (.xlsx) workbook, or it is corrupted.";
        }
    }
}
