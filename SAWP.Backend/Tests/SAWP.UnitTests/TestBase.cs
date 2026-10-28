using System;
using System.Collections.Generic;
using System.Linq;
using System.Linq.Expressions;
using System.Reflection;
using System.Threading;
using System.Threading.Tasks;
using Core.Entities;
using Core.Interfaces;
using MockQueryable;
using MockQueryable.Moq;
using Moq;
using Services.Interfaces;

namespace SAWP.UnitTests;

public abstract class TestBase
{
    protected readonly Mock<IUnitOfWork> UnitOfWorkMock;
    protected readonly Mock<IAuditService> AuditServiceMock;
    protected readonly Mock<IStudentRepository> StudentRepoMock;
    protected readonly Mock<IClassRepository> ClassRepoMock;
    protected readonly Mock<IUserRepository> UserRepoMock;
    protected readonly Mock<ILunchRepository> LunchRepoMock;

    protected TestBase()
    {
        UnitOfWorkMock = new Mock<IUnitOfWork>();
        AuditServiceMock = new Mock<IAuditService>();
        StudentRepoMock = new Mock<IStudentRepository>();
        ClassRepoMock = new Mock<IClassRepository>();
        UserRepoMock = new Mock<IUserRepository>();
        LunchRepoMock = new Mock<ILunchRepository>();

        UnitOfWorkMock.Setup(u => u.Students).Returns(StudentRepoMock.Object);
        UnitOfWorkMock.Setup(u => u.Classes).Returns(ClassRepoMock.Object);
        UnitOfWorkMock.Setup(u => u.Users).Returns(UserRepoMock.Object);
        UnitOfWorkMock.Setup(u => u.Lunch).Returns(LunchRepoMock.Object);
        UnitOfWorkMock.Setup(u => u.SaveChangesAsync(It.IsAny<CancellationToken>())).ReturnsAsync(1);
        UnitOfWorkMock.Setup(u => u.BeginTransactionAsync(It.IsAny<CancellationToken>())).Returns(Task.CompletedTask);
        UnitOfWorkMock.Setup(u => u.CommitTransactionAsync(It.IsAny<CancellationToken>())).Returns(Task.CompletedTask);
        UnitOfWorkMock.Setup(u => u.RollbackTransactionAsync(It.IsAny<CancellationToken>())).Returns(Task.CompletedTask);
    }

    protected Mock<IRepository<T>> SetupRepository<T>(List<T>? data = null) where T : class
    {
        var list = data ?? new List<T>();
        var mockRepo = new Mock<IRepository<T>>();

        mockRepo.Setup(r => r.Query()).Returns(() => list.BuildMock());

        
        mockRepo.Setup(r => r.GetAllAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(() => list.ToList());

        mockRepo.Setup(r => r.FindAsync(It.IsAny<Expression<Func<T, bool>>>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((Expression<Func<T, bool>> predicate, CancellationToken _) =>
            {
                var func = predicate.Compile();
                return list.Where(func).ToList();
            });

        mockRepo.Setup(r => r.GetByIdAsync(It.IsAny<int>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((int id, CancellationToken _) =>
            {
                var idProp = typeof(T).GetProperty("Id");
                if (idProp == null) return null;
                return list.FirstOrDefault(x =>
                {
                    var val = idProp.GetValue(x);
                    return val != null && Convert.ToInt32(val) == id;
                });
            });

        mockRepo.Setup(r => r.AddAsync(It.IsAny<T>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((T entity, CancellationToken _) =>
            {
                var idProp = typeof(T).GetProperty("Id");
                if (idProp != null)
                {
                    var currentId = idProp.GetValue(entity);
                    if (currentId == null || Convert.ToInt32(currentId) <= 0)
                    {
                        var maxId = list.Count > 0
                            ? list.Max(x => Convert.ToInt32(idProp.GetValue(x) ?? 0))
                            : 0;
                        idProp.SetValue(entity, maxId + 1);
                    }
                }
                list.Add(entity);
                return entity;
            });

        mockRepo.Setup(r => r.UpdateAsync(It.IsAny<T>(), It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask);

        mockRepo.Setup(r => r.DeleteAsync(It.IsAny<T>(), It.IsAny<CancellationToken>()))
            .Returns((T entity, CancellationToken _) =>
            {
                list.Remove(entity);
                return Task.CompletedTask;
            });

        UnitOfWorkMock.Setup(u => u.Repository<T>()).Returns(mockRepo.Object);
        return mockRepo;
    }
}
