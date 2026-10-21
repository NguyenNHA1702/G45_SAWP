using Core.Entities;

namespace Core.Interfaces;

public interface IClassScheduleRepository : IRepository<ClassSchedule>
{
    Task<int?> GetFirstPeriodTeacherIdAsync(int classId, DateTime date, CancellationToken ct = default);
    Task<IReadOnlyList<int>> GetClassesWhereTeacherHasFirstPeriodAsync(int staffId, DateTime date, CancellationToken ct = default);

}
