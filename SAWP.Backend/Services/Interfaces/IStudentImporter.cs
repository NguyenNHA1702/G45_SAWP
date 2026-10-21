using Core.DTOs;

namespace Services.Interfaces;

public interface IStudentImporter
{
    Task<ApiResponse<List<ImportStudentRowDto>>> ParseExcelAsync(Stream fileStream, CancellationToken cancellationToken = default);
    Task<ApiResponse<ImportStudentsResultDto>> ImportStudentsAsync(ImportStudentsConfirmDto request, int? actorUserId = null, CancellationToken cancellationToken = default);
    Task<byte[]> GenerateExcelTemplateAsync(CancellationToken cancellationToken = default);
}
