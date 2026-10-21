using Core.Entities;

namespace Core.Interfaces;

public interface IStudentRepository : IRepository<Student>
{
    Task<Student?> GetStudentWithProfileAsync(int studentId, CancellationToken cancellationToken = default);
    Task<IReadOnlyList<Student>> GetStudentsByClassIdAsync(int classId, CancellationToken cancellationToken = default);
    Task<Student?> GetByStudentCodeAsync(string studentCode, CancellationToken cancellationToken = default);
    Task<bool> ExistsByStudentCodeAsync(string studentCode, int? excludeStudentId = null, CancellationToken cancellationToken = default);
    Task<IReadOnlyList<Student>> GetLinkedStudentsByParentUserIdAsync(int parentUserId, CancellationToken cancellationToken = default);
}

