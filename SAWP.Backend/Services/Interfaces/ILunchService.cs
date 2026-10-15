using Core.DTOs;

namespace Services.Interfaces;

public interface ILunchService
{
    Task<ApiResponse<bool>> RegisterLunchAsync(LunchRegistrationDto request, CancellationToken cancellationToken = default);
    Task<ApiResponse<DailyMealCountReportDto>> GetDailyMealCountAsync(DateTime date, CancellationToken cancellationToken = default);
}
