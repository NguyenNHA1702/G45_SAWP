using Core.Interfaces;
using Infrastructure.Data;
using Microsoft.EntityFrameworkCore.Storage;
using System.Collections.Concurrent;

namespace Infrastructure.Repositories;

public class UnitOfWork : IUnitOfWork
{
    private readonly ApplicationDbContext _context;
    private IDbContextTransaction? _currentTransaction;
    private readonly ConcurrentDictionary<string, object> _repositories = new();

    public IUserRepository Users { get; }
    public IStudentRepository Students { get; }
    public IClassRepository Classes { get; }
    public IAttendanceRepository Attendance { get; }
    public ILunchRepository Lunch { get; }
    public IClassScheduleRepository Schedules { get; }
    public INotificationRepository Notifications { get; }

    public UnitOfWork(
        ApplicationDbContext context,
        IUserRepository users,
        IStudentRepository students,
        IClassRepository classes,
        IAttendanceRepository attendance,
        ILunchRepository lunch,
        IClassScheduleRepository schedules,
        INotificationRepository notifications)
    {
        _context = context;
        Users = users;
        Students = students;
        Classes = classes;
        Attendance = attendance;
        Lunch = lunch;
        Schedules = schedules;
        Notifications = notifications;
    }


    public IRepository<T> Repository<T>() where T : class
    {
        var typeName = typeof(T).Name;
        return (IRepository<T>)_repositories.GetOrAdd(typeName, _ => new Repository<T>(_context));
    }

    public async Task<int> SaveChangesAsync(CancellationToken cancellationToken = default)
    {
        return await _context.SaveChangesAsync(cancellationToken);
    }

    public async Task BeginTransactionAsync(CancellationToken cancellationToken = default)
    {
        if (_currentTransaction != null) return;
        _currentTransaction = await _context.Database.BeginTransactionAsync(cancellationToken);
    }

    public async Task CommitTransactionAsync(CancellationToken cancellationToken = default)
    {
        try
        {
            await _context.SaveChangesAsync(cancellationToken);
            if (_currentTransaction != null)
            {
                await _currentTransaction.CommitAsync(cancellationToken);
            }
        }
        catch
        {
            await RollbackTransactionAsync(cancellationToken);
            throw;
        }
        finally
        {
            if (_currentTransaction != null)
            {
                await _currentTransaction.DisposeAsync();
                _currentTransaction = null;
            }
        }
    }

    public async Task RollbackTransactionAsync(CancellationToken cancellationToken = default)
    {
        if (_currentTransaction != null)
        {
            await _currentTransaction.RollbackAsync(cancellationToken);
            await _currentTransaction.DisposeAsync();
            _currentTransaction = null;
        }
    }

    public void Dispose()
    {
        _context.Dispose();
    }
}
