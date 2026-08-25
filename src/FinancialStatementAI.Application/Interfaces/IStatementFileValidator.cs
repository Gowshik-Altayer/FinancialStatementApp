using FinancialStatementAI.Application.DTOs.Statements;

namespace FinancialStatementAI.Application.Interfaces;

public interface IStatementFileValidator
{
    /// <summary>Validates extension, size, and actual file content (magic-byte sniffing; for
    /// PDFs, that the file opens and isn't password-protected). Never trusts the client-supplied
    /// file name/Content-Type alone.</summary>
    FileValidationResult Validate(byte[] content, string fileName, long fileSizeBytes);
}
