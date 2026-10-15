using Core.DTOs;
using Services.Interfaces;

namespace Services.Implementations;

public class StudentImporter : IStudentImporter
{
    public async Task<ApiResponse<List<ImportStudentRowDto>>> ParseExcelAsync(Stream fileStream, CancellationToken cancellationToken = default)
    {
        await Task.CompletedTask;
        return ApiResponse<List<ImportStudentRowDto>>.Ok(new List<ImportStudentRowDto>());
    }

    public async Task<ApiResponse<int>> ImportStudentsAsync(List<ImportStudentRowDto> validRows, CancellationToken cancellationToken = default)
    {
        await Task.CompletedTask;
        return ApiResponse<int>.Ok(validRows.Count);
    }
}
