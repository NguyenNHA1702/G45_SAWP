using Core.Constants;
using Core.DTOs;
using Core.Entities;
using Core.Enums;
using Core.Interfaces;
using Microsoft.EntityFrameworkCore;
using Services.Interfaces;

namespace Services.Implementations;

public class AttendanceService : IAttendanceService
{
    private readonly IUnitOfWork _unitOfWork;
    private readonly ILunchService _lunchService;

    public AttendanceService(IUnitOfWork unitOfWork, ILunchService lunchService)
    {
        _unitOfWork = unitOfWork;
        _lunchService = lunchService;
    }

    public async Task<ApiResponse<ClassDailySheetDto>> GetClassDailySheetAsync(int classId, DateTime date, AttendanceSession session, int requestingUserId, CancellationToken cancellationToken = default)
    {
        var targetDate = date.Date;

        // 1. Get Class details
        var classObj = await _unitOfWork.Repository<Class>()
            .Query()
            .FirstOrDefaultAsync(c => c.Id == classId, cancellationToken);

        if (classObj == null)
        {
            return ApiResponse<ClassDailySheetDto>.Fail("Không tìm thấy thông tin lớp học.");
        }

        // 2. Get active students in class
        var studentAssignments = await _unitOfWork.Repository<ClassAssignment>()
            .Query()
            .Include(ca => ca.Student)
            .Where(ca => ca.ClassId == classId && ca.IsActive)
            .OrderBy(ca => ca.Student.FullName)
            .ToListAsync(cancellationToken);

        // 3. Check existing saved attendance records for this date & session
        var existingRecords = await _unitOfWork.Repository<AttendanceRecord>()
            .Query()
            .Include(ar => ar.RecordedByUser)
            .Where(ar => ar.ClassId == classId && ar.Date.Date == targetDate && ar.Session == session)
            .ToListAsync(cancellationToken);

        var isSaved = existingRecords.Any();
        var savedRecord = existingRecords.FirstOrDefault();

        // 4. Query all approved leave requests covering targetDate
        var studentIds = studentAssignments.Select(ca => ca.StudentId).ToList();
        var approvedLeaves = await _unitOfWork.Repository<AdministrativeRequest>()
            .Query()
            .Include(ar => ar.LeaveRequest)
            .Where(ar => ar.StudentId.HasValue && studentIds.Contains(ar.StudentId.Value))
            .Where(ar => ar.RequestType == "LeaveRequest" && ar.Status == RequestStatus.Approved && ar.LeaveRequest != null)
            .Where(ar => ar.LeaveRequest!.StartDate.Date <= targetDate && ar.LeaveRequest.EndDate.Date >= targetDate)
            .ToListAsync(cancellationToken);

        var leaveMap = approvedLeaves.ToDictionary(ar => ar.StudentId!.Value, ar => ar.LeaveRequest!);

        // 5. Construct sheet rows
        var rows = new List<AttendanceRowDto>();
        foreach (var sa in studentAssignments)
        {
            var existing = existingRecords.FirstOrDefault(r => r.StudentId == sa.StudentId);
            var hasLeave = leaveMap.TryGetValue(sa.StudentId, out var leaveReq);

            // Check if leave covers this session
            var leaveCoversSession = false;
            if (hasLeave && leaveReq != null)
            {
                leaveCoversSession = leaveReq.Session == LeaveSession.FullDay ||
                                     (session == AttendanceSession.Morning && leaveReq.Session == LeaveSession.Morning) ||
                                     (session == AttendanceSession.Afternoon && leaveReq.Session == LeaveSession.Afternoon);
            }

            var row = new AttendanceRowDto
            {
                StudentId = sa.StudentId,
                StudentCode = sa.Student.StudentCode,
                FullName = sa.Student.FullName,
                Gender = sa.Student.Gender,
                HasApprovedLeave = leaveCoversSession,
                LeaveReason = leaveCoversSession ? leaveReq?.Reason : null
            };

            if (isSaved && existing != null)
            {
                row.CurrentStatus = existing.Status;
                row.Remarks = existing.Remarks;
                row.IsPreFilled = false;
            }
            else
            {
                // Not saved yet: Pre-fill
                if (leaveCoversSession)
                {
                    row.CurrentStatus = AttendanceStatus.ExcusedAbsence;
                    row.IsPreFilled = true;
                    row.Remarks = "Gợi ý từ đơn xin nghỉ học đã duyệt";
                }
                else
                {
                    row.CurrentStatus = AttendanceStatus.Present;
                    row.IsPreFilled = false;
                }
            }

            rows.Add(row);
        }

        var result = new ClassDailySheetDto
        {
            ClassId = classId,
            ClassName = classObj.ClassName,
            Date = targetDate,
            Session = session,
            IsSaved = isSaved,
            SavedAt = savedRecord?.CreatedAt,
            SavedByName = savedRecord?.RecordedByUser?.FullName,
            Students = rows
        };

        return ApiResponse<ClassDailySheetDto>.Ok(result);
    }

    public async Task<ApiResponse<AttendanceSaveResultDto>> SaveClassAttendanceAsync(SaveClassAttendanceDto request, int teacherUserId, CancellationToken cancellationToken = default)
    {
        var targetDate = request.Date.Date;

        // 1. Check existing records
        var existingRecords = await _unitOfWork.Repository<AttendanceRecord>()
            .Query()
            .Where(ar => ar.ClassId == request.ClassId && ar.Date.Date == targetDate && ar.Session == request.Session)
            .ToListAsync(cancellationToken);

        var existingMap = existingRecords.ToDictionary(r => r.StudentId);
        var mealAlerts = new List<string>();

        // 2. Check approved leave requests for conflict detection
        var studentIds = request.Entries.Select(e => e.StudentId).ToList();
        var approvedLeaves = await _unitOfWork.Repository<AdministrativeRequest>()
            .Query()
            .Include(ar => ar.LeaveRequest)
            .Include(ar => ar.Student)
            .Where(ar => ar.StudentId.HasValue && studentIds.Contains(ar.StudentId.Value))
            .Where(ar => ar.RequestType == "LeaveRequest" && ar.Status == RequestStatus.Approved && ar.LeaveRequest != null)
            .Where(ar => ar.LeaveRequest!.StartDate.Date <= targetDate && ar.LeaveRequest.EndDate.Date >= targetDate)
            .ToListAsync(cancellationToken);

        var leaveMap = approvedLeaves.ToDictionary(ar => ar.StudentId!.Value, ar => ar);

        // 3. Process entries
        foreach (var entry in request.Entries)
        {
            if (existingMap.TryGetValue(entry.StudentId, out var existing))
            {
                existing.Status = entry.Status;
                existing.Remarks = entry.Remarks;
                existing.RecordedByUserId = teacherUserId;
            }
            else
            {
                var newRec = new AttendanceRecord
                {
                    ClassId = request.ClassId,
                    StudentId = entry.StudentId,
                    Date = targetDate,
                    Session = request.Session,
                    Status = entry.Status,
                    Remarks = entry.Remarks,
                    RecordedByUserId = teacherUserId,
                    CreatedAt = DateTime.UtcNow
                };
                await _unitOfWork.Repository<AttendanceRecord>().AddAsync(newRec, cancellationToken);
            }

            // Anomaly Case 1: Student had approved leave & cancelled meal, but is marked PRESENT / LATE
            if ((entry.Status == AttendanceStatus.Present || entry.Status == AttendanceStatus.Late) &&
                leaveMap.TryGetValue(entry.StudentId, out var adminReq) &&
                adminReq.LeaveRequest != null && adminReq.LeaveRequest.IsLunchCancelled)
            {
                var msg = $"Học sinh {adminReq.Student?.FullName} (Lớp {request.ClassId}) có đơn nghỉ nhưng thực tế ĐI HỌC. Đã yêu cầu kiểm tra/bổ sung suất ăn bán trú.";
                mealAlerts.Add(msg);

                // Try restore meal
                await _lunchService.RestoreMealForStudentAsync(
                    entry.StudentId,
                    targetDate,
                    "Học sinh có mặt thực tế tại lớp dù có đơn xin nghỉ",
                    cancellationToken);
            }

            // Anomaly Case 2: Teacher marks ABSENT (no leave request) BEFORE 08:15 AM
            if ((entry.Status == AttendanceStatus.UnexcusedAbsence || entry.Status == AttendanceStatus.ExcusedAbsence) &&
                request.Session == AttendanceSession.Morning)
            {
                var cutoffTime = targetDate.Add(new TimeSpan(8, 15, 0));
                if (DateTime.Now <= cutoffTime)
                {
                    await _lunchService.CancelMealForLeaveAsync(
                        entry.StudentId,
                        targetDate,
                        "GVCN điểm danh vắng trước 08h15",
                        cancellationToken);
                }
            }
        }

        await _unitOfWork.SaveChangesAsync(cancellationToken);

        return ApiResponse<AttendanceSaveResultDto>.Ok(new AttendanceSaveResultDto
        {
            Success = true,
            Message = SystemMessages.MSG18.Content,
            SavedCount = request.Entries.Count,
            MealAlerts = mealAlerts
        }, SystemMessages.MSG18);
    }
}
