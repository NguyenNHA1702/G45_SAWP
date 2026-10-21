namespace Core.Interfaces;

public interface IUnitOfWork : IDisposable
{
    IUserRepository Users { get; }
    IStudentRepository Students { get; }
    IClassRepository Classes { get; }
    IAttendanceRepository Attendance { get; }
    ILunchRepository Lunch { get; }
    IClassScheduleRepository Schedules { get; }
    INotificationRepository Notifications { get; }
    IRepository<T> Repository<T>() where T : class;
    Task<int> SaveChangesAsync(CancellationToken cancellationToken = default);
    Task BeginTransactionAsync(CancellationToken cancellationToken = default);
    Task CommitTransactionAsync(CancellationToken cancellationToken = default);
    Task RollbackTransactionAsync(CancellationToken cancellationToken = default);
}
