using Core.DTOs;

namespace Services.Interfaces;

public interface IStaffImporter
{
    Task<ApiResponse<int>> ImportStaffAsync(Stream fileStream, CancellationToken cancellationToken = default);
}
