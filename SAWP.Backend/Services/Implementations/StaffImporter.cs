using Core.DTOs;
using Services.Interfaces;

namespace Services.Implementations;

public class StaffImporter : IStaffImporter
{
    public async Task<ApiResponse<int>> ImportStaffAsync(Stream fileStream, CancellationToken cancellationToken = default)
    {
        await Task.CompletedTask;
        return ApiResponse<int>.Ok(0);
    }
}
