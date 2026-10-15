using Core.Entities;

namespace Core.Interfaces;

public interface ILunchRepository : IRepository<LunchRegistration>
{
    Task<IReadOnlyList<LunchRegistration>> GetDailyRegistrationsAsync(DateTime date, CancellationToken cancellationToken = default);
    Task<LunchRegistration?> GetStudentRegistrationForDateAsync(int studentId, DateTime date, CancellationToken cancellationToken = default);
}
