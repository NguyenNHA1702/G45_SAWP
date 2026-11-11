using Core.DTOs;
using Core.DTOs.ContactBook;

namespace Services.Interfaces;

public interface IEContactBookService
{
    // --- Bảng tin lớp học (UC-61, UC-62) ---
    Task<ApiResponse<List<ClassPostDto>>> GetClassPostsAsync(int currentUserId, int classId, CancellationToken cancellationToken = default);
    Task<ApiResponse<ClassPostDto>> CreateClassPostAsync(int teacherUserId, CreateClassPostDto request, CancellationToken cancellationToken = default);
    Task<ApiResponse<bool>> TogglePinPostAsync(int teacherUserId, int postId, CancellationToken cancellationToken = default);
    Task<ApiResponse<bool>> DeleteClassPostAsync(int teacherUserId, int postId, CancellationToken cancellationToken = default);
    Task<ApiResponse<PostCommentDto>> AddCommentAsync(int userId, int postId, CreatePostCommentDto request, CancellationToken cancellationToken = default);
    Task<ApiResponse<bool>> DeleteCommentAsync(int currentUserId, int commentId, CancellationToken cancellationToken = default);
    Task<ApiResponse<bool>> ToggleReactionAsync(int userId, int postId, string reactionType, CancellationToken cancellationToken = default);

    // --- Sổ nhận xét học sinh & Ký nhận 2 chiều (UC-63, UC-64) ---
    Task<ApiResponse<List<StudentEvaluationRosterItemDto>>> GetClassEvaluationsRosterAsync(int teacherUserId, int classId, int weekNumber, CancellationToken cancellationToken = default);
    Task<ApiResponse<List<StudentEvaluationDto>>> GetStudentEvaluationsAsync(int currentUserId, int studentId, CancellationToken cancellationToken = default);
    Task<ApiResponse<StudentEvaluationDto>> CreateOrUpdateEvaluationAsync(int teacherUserId, CreateStudentEvaluationDto request, CancellationToken cancellationToken = default);
    Task<ApiResponse<StudentEvaluationDto>> AcknowledgeEvaluationAsync(int guardianUserId, int evaluationId, AcknowledgeEvaluationDto request, CancellationToken cancellationToken = default);
}
