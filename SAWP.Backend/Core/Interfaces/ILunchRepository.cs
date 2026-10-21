using Core.Entities;
using Core.Enums;

namespace Core.Interfaces;

public record HeadcountRow(int StudentId, int ClassId, string ClassName, MealType MealType, bool IsActive, LunchCancellationSource? Source);

public interface ILunchRepository : IRepository<LunchRegistration>
{
    Task<IReadOnlyList<LunchRegistration>> GetDailyRegistrationsAsync(DateTime date, CancellationToken cancellationToken = default);
    Task<LunchRegistration?> GetStudentRegistrationForDateAsync(int studentId, DateTime date, CancellationToken cancellationToken = default);
    Task<Dictionary<int, LunchRegistration>> GetRegistrationsForStudentsAsync(IEnumerable<int> studentIds, DateTime date, bool track = false, CancellationToken ct = default);
    Task<IReadOnlyList<HeadcountRow>> GetHeadcountRowsAsync(DateTime date, CancellationToken ct = default);
}

