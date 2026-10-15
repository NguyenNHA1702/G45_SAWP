using Core.DTOs;
using Core.Interfaces;
using Services.Interfaces;

namespace Services.Implementations;

public class LunchService : ILunchService
{
    private readonly IUnitOfWork _unitOfWork;

    public LunchService(IUnitOfWork unitOfWork)
    {
        _unitOfWork = unitOfWork;
    }

    public async Task<ApiResponse<bool>> RegisterLunchAsync(LunchRegistrationDto request, CancellationToken cancellationToken = default)
    {
        await Task.CompletedTask;
        return ApiResponse<bool>.Ok(true);
    }

    public async Task<ApiResponse<DailyMealCountReportDto>> GetDailyMealCountAsync(DateTime date, CancellationToken cancellationToken = default)
    {
        await Task.CompletedTask;
        return ApiResponse<DailyMealCountReportDto>.Ok(new DailyMealCountReportDto { Date = date });
    }
}
