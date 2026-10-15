using Core.DTOs;

namespace Services.Interfaces;

public interface IStudentImporter
{
    Task<ApiResponse<List<ImportStudentRowDto>>> ParseExcelAsync(Stream fileStream, CancellationToken cancellationToken = default);
    Task<ApiResponse<int>> ImportStudentsAsync(List<ImportStudentRowDto> validRows, CancellationToken cancellationToken = default);
}
