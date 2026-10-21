using Core.Configuration;
using Core.DTOs;
using Core.Entities;
using Core.Enums;
using Core.Interfaces;
using Microsoft.Extensions.Options;
using Services.Interfaces;

namespace Services.Implementations;

public class LunchService : ILunchService
{
    private readonly IUnitOfWork _unitOfWork;
    private readonly ISchoolClock _clock;
    private readonly SchoolOptions _options;

    public LunchService(
        IUnitOfWork unitOfWork,
        ISchoolClock clock,
        IOptions<SchoolOptions> options)
    {
        _unitOfWork = unitOfWork;
        _clock = clock;
        _options = options.Value;
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

    public async Task<ApiResponse<MealHeadcountDto>> GetMealHeadcountAsync(DateTime date, CancellationToken cancellationToken = default)
    {
        var targetDate = date.Date;
        var serviceDays = await _unitOfWork.Repository<LunchServiceDay>()
            .FindAsync(d => d.Date == targetDate, cancellationToken);
        var day = serviceDays.FirstOrDefault();

        bool isServiceDay = day != null 
            ? day.IsServing 
            : (targetDate.DayOfWeek != DayOfWeek.Saturday && targetDate.DayOfWeek != DayOfWeek.Sunday);

        var finalizeTime = TimeSpan.TryParse(_options.KitchenFinalizeTime, out var ft) ? ft : new TimeSpan(10, 30, 0);
        bool isFinalized = (targetDate < _clock.Today) || (targetDate == _clock.Today && _clock.Now.TimeOfDay >= finalizeTime);

        if (!isServiceDay)
        {
            return ApiResponse<MealHeadcountDto>.Ok(new MealHeadcountDto
            {
                Date = targetDate,
                IsServiceDay = false,
                IsFinalized = isFinalized,
                FinalizeTime = finalizeTime
            });
        }

        var rows = await _unitOfWork.Lunch.GetHeadcountRowsAsync(targetDate, cancellationToken);
        var allClassIds = rows.Select(r => r.ClassId).Distinct().ToList();
        var submittedClassIds = await _unitOfWork.Attendance.GetSubmittedClassIdsAsync(allClassIds, targetDate, cancellationToken);

        var activeRows = rows.Where(r => r.IsActive).ToList();
        var dto = new MealHeadcountDto
        {
            Date = targetDate,
            IsServiceDay = true,
            IsFinalized = isFinalized,
            FinalizeTime = finalizeTime,
            TotalMeals = activeRows.Count,
            StandardLunch = activeRows.Count(r => r.MealType == MealType.StandardLunch),
            VegetarianLunch = activeRows.Count(r => r.MealType == MealType.VegetarianLunch),
            SpecialDiet = activeRows.Count(r => r.MealType == MealType.SpecialDiet),
            DeductedByAttendance = rows.Count(r => !r.IsActive && r.Source == LunchCancellationSource.Attendance),
            DeductedByLeave = rows.Count(r => !r.IsActive && r.Source == LunchCancellationSource.Leave),
            Classes = rows.GroupBy(r => new { r.ClassId, r.ClassName })
                .Select(g => new ClassHeadcountDto
                {
                    ClassId = g.Key.ClassId,
                    ClassName = g.Key.ClassName,
                    Meals = g.Count(r => r.IsActive),
                    DeductedByAttendance = g.Count(r => !r.IsActive && r.Source == LunchCancellationSource.Attendance),
                    AttendanceSubmitted = submittedClassIds.Contains(g.Key.ClassId)
                })
                .OrderBy(c => c.ClassName)
                .ToList()
        };

        return ApiResponse<MealHeadcountDto>.Ok(dto);
    }
}

