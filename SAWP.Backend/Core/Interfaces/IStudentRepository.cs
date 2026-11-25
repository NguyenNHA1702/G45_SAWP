using Core.DTOs;
using Core.Entities;

namespace Core.Interfaces;

public class StudentProfileLookupResult
{
    public StudentProfileDto Profile { get; set; } = new();
    public List<int> GuardianUserIds { get; set; } = new();
    public List<int> HomeroomTeacherUserIds { get; set; } = new();
}

public interface IStudentRepository : IRepository<Student>
{
    Task<Student?> GetStudentWithProfileAsync(int studentId, CancellationToken cancellationToken = default);
    Task<IReadOnlyList<Student>> GetStudentsByClassIdAsync(int classId, CancellationToken cancellationToken = default);
    Task<Student?> GetByStudentCodeAsync(string studentCode, CancellationToken cancellationToken = default);
    
    // Workflow 2 additions
    Task<IReadOnlyList<Student>> GetActiveRosterAsync(int classId, DateTime date, CancellationToken ct = default);
    Task<Dictionary<int, List<int>>> GetGuardianUserIdsAsync(IEnumerable<int> studentIds, CancellationToken ct = default);
    Task<bool> IsGuardianOfAsync(int studentId, int userId, CancellationToken ct = default);
    Task<bool> ExistsByStudentCodeAsync(string studentCode, int? excludeStudentId = null, CancellationToken cancellationToken = default);
    Task<IReadOnlyList<Student>> GetLinkedStudentsByParentUserIdAsync(int parentUserId, CancellationToken cancellationToken = default);

    // Performance projection read paths (default implementations preserve existing test mocks)
    Task<StudentProfileLookupResult?> GetStudentProfileDtoAsync(int studentId, CancellationToken cancellationToken = default)
        => Task.FromResult<StudentProfileLookupResult?>(null);

    Task<List<LinkedChildSummaryDto>?> GetLinkedChildDtosAsync(int parentUserId, CancellationToken cancellationToken = default)
        => Task.FromResult<List<LinkedChildSummaryDto>?>(null);
}

