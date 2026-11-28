using Core.Common;
using Core.Constants;
using Core.DTOs;
using Core.Entities;
using Core.Enums;
using Core.Interfaces;
using Microsoft.EntityFrameworkCore;
using Services.Interfaces;

namespace Services.Implementations;

public class ClassService : IClassService
{
    private readonly IUnitOfWork _unitOfWork;
    private readonly IAuditService _auditService;

    public ClassService(IUnitOfWork unitOfWork, IAuditService auditService)
    {
        _unitOfWork = unitOfWork;
        _auditService = auditService;
    }

    #region Academic Year

    public async Task<ApiResponse<SchoolYearDto>> CreateSchoolYearAsync(CreateSchoolYearDto request, int? actorUserId = null, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(request.YearName))
        {
            return ApiResponse<SchoolYearDto>.Fail(SystemMessages.MSG01, new List<string> { "Tên năm học không được để trống." });
        }

        if (request.StartDate >= request.EndDate)
        {
            return ApiResponse<SchoolYearDto>.Fail(SystemMessages.MSG01, new List<string> { "Ngày bắt đầu phải trước ngày kết thúc năm học (BR-14)." });
        }

        var durationDays = (request.EndDate - request.StartDate).TotalDays;
        if (durationDays < 60)
        {
            return ApiResponse<SchoolYearDto>.Fail(SystemMessages.MSG01, new List<string> { "Thời lượng năm học quá ngắn (tối thiểu 60 ngày). Vui lòng kiểm tra lại khoảng thời gian." });
        }

        if (durationDays > 450)
        {
            return ApiResponse<SchoolYearDto>.Fail(SystemMessages.MSG01, new List<string> { "Thời lượng năm học không được vượt quá 15 tháng (tối đa 450 ngày). Vui lòng kiểm tra lại năm bắt đầu và kết thúc." });
        }

        // Validate tên năm học phải đúng định dạng YYYY-YYYY và năm kết thúc = năm bắt đầu + 1
        var yearMatch = System.Text.RegularExpressions.Regex.Match(request.YearName.Trim(), @"^(\d{4})-(\d{4})$");
        if (!yearMatch.Success)
        {
            return ApiResponse<SchoolYearDto>.Fail(
                SystemMessages.MSG01,
                new List<string> { "Tên năm học phải có định dạng YYYY-YYYY (ví dụ: 2026-2027)." });
        }

        var expectedStartYear = int.Parse(yearMatch.Groups[1].Value);
        var expectedEndYear = int.Parse(yearMatch.Groups[2].Value);
        if (expectedEndYear != expectedStartYear + 1)
        {
            return ApiResponse<SchoolYearDto>.Fail(
                SystemMessages.MSG01,
                new List<string> { $"Năm kết thúc ({expectedEndYear}) phải bằng năm bắt đầu ({expectedStartYear}) + 1. Ví dụ: {expectedStartYear}-{expectedStartYear + 1}." });
        }

        if (request.StartDate.Year != expectedStartYear || request.EndDate.Year != expectedEndYear)
        {
            return ApiResponse<SchoolYearDto>.Fail(
                SystemMessages.MSG01,
                new List<string> { $"Năm của ngày bắt đầu ({request.StartDate.Year}) và ngày kết thúc ({request.EndDate.Year}) phải trùng khớp với tên năm học ({expectedStartYear}-{expectedEndYear})." });
        }

        var existingYears = await _unitOfWork.Classes.GetAllSchoolYearsAsync(cancellationToken);
        if (existingYears != null && existingYears.Any(y => y.YearName.Trim().Equals(request.YearName.Trim(), StringComparison.OrdinalIgnoreCase)))
        {
            return ApiResponse<SchoolYearDto>.Fail(SystemMessages.MSG01, new List<string> { $"Năm học '{request.YearName.Trim()}' đã tồn tại trong hệ thống." });
        }

        // UC-13 step 2, BR-72: a new school year is saved in Draft state; it does not affect the active year (BR-14)
        var schoolYear = new SchoolYear
        {
            YearName = request.YearName.Trim(),
            StartDate = request.StartDate,
            EndDate = request.EndDate,
            IsActive = false,
            Status = SchoolYearStatuses.Draft
        };

        await _unitOfWork.Repository<SchoolYear>().AddAsync(schoolYear, cancellationToken);
        await _unitOfWork.SaveChangesAsync(cancellationToken);

        await _auditService.LogActionAsync(
            actorUserId,
            AuditActions.SchoolYearCreate,
            nameof(SchoolYear),
            schoolYear.Id.ToString(),
            null,
            new { schoolYear.YearName, schoolYear.StartDate, schoolYear.EndDate, schoolYear.Status },
            null, // TODO: pass the client IP address; it is not recorded yet.
            cancellationToken);
        await _unitOfWork.SaveChangesAsync(cancellationToken);

        return ApiResponse<SchoolYearDto>.Ok(new SchoolYearDto
        {
            Id = schoolYear.Id,
            YearName = schoolYear.YearName,
            StartDate = schoolYear.StartDate,
            EndDate = schoolYear.EndDate,
            IsActive = schoolYear.IsActive,
            Status = schoolYear.Status,
            ClassCount = 0
        }, SystemMessages.MSG08);
    }

    public async Task<ApiResponse<List<SchoolYearDto>>> GetSchoolYearsAsync(CancellationToken cancellationToken = default)
    {
        var fast = await _unitOfWork.Classes.GetSchoolYearDtosAsync(cancellationToken);
        if (fast != null)
        {
            return ApiResponse<List<SchoolYearDto>>.Ok(fast);
        }

        var years = await _unitOfWork.Classes.GetAllSchoolYearsAsync(cancellationToken);
        var result = years.Select(y => new SchoolYearDto
        {
            Id = y.Id,
            YearName = y.YearName,
            StartDate = y.StartDate,
            EndDate = y.EndDate,
            IsActive = y.IsActive,
            Status = y.Status,
            ClassCount = y.Classes.Count
        }).ToList();

        return ApiResponse<List<SchoolYearDto>>.Ok(result);
    }

    public async Task<ApiResponse<SchoolYearDto>> GetActiveSchoolYearAsync(CancellationToken cancellationToken = default)
    {
        var fast = await _unitOfWork.Classes.GetActiveSchoolYearDtoAsync(cancellationToken);
        if (fast != null)
        {
            return ApiResponse<SchoolYearDto>.Ok(fast);
        }

        var activeYear = await _unitOfWork.Classes.GetActiveSchoolYearAsync(cancellationToken);
        if (activeYear == null)
        {
            return ApiResponse<SchoolYearDto>.Fail(SystemMessages.MSG14);
        }

        return ApiResponse<SchoolYearDto>.Ok(new SchoolYearDto
        {
            Id = activeYear.Id,
            YearName = activeYear.YearName,
            StartDate = activeYear.StartDate,
            EndDate = activeYear.EndDate,
            IsActive = activeYear.IsActive,
            Status = activeYear.Status,
            ClassCount = activeYear.Classes.Count
        });
    }

    public async Task<ApiResponse<bool>> ActivateSchoolYearAsync(int schoolYearId, int? actorUserId = null, CancellationToken cancellationToken = default)
    {
        var target = await _unitOfWork.Repository<SchoolYear>().GetByIdAsync(schoolYearId, cancellationToken);
        if (target == null)
        {
            return ApiResponse<bool>.Fail(SystemMessages.MSG01, new List<string> { "Không tìm thấy năm học." });
        }

        // UC-13 step 4 / E2, BR-72: only a Draft school year all of whose classes are Reviewed can be activated
        var targetClasses = await _unitOfWork.Classes.GetClassesBySchoolYearAsync(schoolYearId, null, null, cancellationToken)
                            ?? new List<Class>();
        if (target.Status != SchoolYearStatuses.Draft)
        {
            return ApiResponse<bool>.Fail(SystemMessages.MSG63, new List<string> { $"Năm học {target.YearName} không ở trạng thái Bản nháp." });
        }

        var notReviewed = targetClasses.Where(c => !c.IsReviewed).Select(c => c.ClassName).ToList();
        if (notReviewed.Count > 0)
        {
            return ApiResponse<bool>.Fail(SystemMessages.MSG63, new List<string> { $"Các lớp chưa được rà soát: {string.Join(", ", notReviewed)}." });
        }

        var targetClassIds = targetClasses.Select(c => c.Id).ToList();
        var now = DateTime.UtcNow;
        var allYears = await _unitOfWork.Classes.GetAllSchoolYearsAsync(cancellationToken);

        await _unitOfWork.BeginTransactionAsync(cancellationToken);
        try
        {
            // BR-14: the previously active year ends
            foreach (var sy in allYears.Where(y => y.Id != schoolYearId && y.IsActive))
            {
                sy.IsActive = false;
                sy.Status = SchoolYearStatuses.Archived;
                await _unitOfWork.Repository<SchoolYear>().UpdateAsync(sy, cancellationToken);
            }

            // BR-17, BR-18: current assignments outside the new year end, so each student keeps one current class;
            // the records stay as history (BR-55)
            var endedAssignments = await _unitOfWork.Repository<ClassAssignment>()
                .FindAsync(ca => ca.IsActive && !targetClassIds.Contains(ca.ClassId), cancellationToken);
            foreach (var ca in endedAssignments)
            {
                ca.IsActive = false;
                ca.EndDate = now;
                await _unitOfWork.Repository<ClassAssignment>().UpdateAsync(ca, cancellationToken);
            }

            // The Draft class assignments become each student's current class
            var draftAssignments = await _unitOfWork.Repository<ClassAssignment>()
                .FindAsync(ca => ca.IsDraft && targetClassIds.Contains(ca.ClassId), cancellationToken);
            foreach (var ca in draftAssignments)
            {
                ca.IsDraft = false;
                ca.IsActive = true;
                await _unitOfWork.Repository<ClassAssignment>().UpdateAsync(ca, cancellationToken);
            }

            target.IsActive = true;
            target.Status = SchoolYearStatuses.Active;
            await _unitOfWork.Repository<SchoolYear>().UpdateAsync(target, cancellationToken);

            // UC-15 step 5: homeroom teachers of a Draft year are notified at activation
            foreach (var cls in targetClasses.Where(c => c.HomeroomTeacherId.HasValue))
            {
                var teacher = cls.HomeroomTeacher
                              ?? await _unitOfWork.Repository<StaffProfile>().GetByIdAsync(cls.HomeroomTeacherId!.Value, cancellationToken);
                if (teacher == null) continue;
                await _unitOfWork.Repository<Notification>().AddAsync(BuildHomeroomNotification(teacher.UserId, cls, target.YearName), cancellationToken);
            }

            await _unitOfWork.SaveChangesAsync(cancellationToken);

            await _auditService.LogActionAsync(
                actorUserId,
                AuditActions.SchoolYearActivate,
                nameof(SchoolYear),
                schoolYearId.ToString(),
                new { target.YearName, Status = SchoolYearStatuses.Draft },
                new { target.YearName, target.Status, ClassCount = targetClasses.Count, ActivatedAssignments = draftAssignments.Count(), EndedAssignments = endedAssignments.Count() },
                null, // TODO: pass the client IP address; it is not recorded yet.
                cancellationToken);
            await _unitOfWork.SaveChangesAsync(cancellationToken);
            await _unitOfWork.CommitTransactionAsync(cancellationToken);
        }
        catch
        {
            await _unitOfWork.RollbackTransactionAsync(cancellationToken);
            throw;
        }

        return ApiResponse<bool>.Ok(true, SystemMessages.MSG08);
    }

    public async Task<ApiResponse<bool>> DeleteSchoolYearAsync(int schoolYearId, int? actorUserId = null, CancellationToken cancellationToken = default)
    {
        var schoolYear = await _unitOfWork.Repository<SchoolYear>().GetByIdAsync(schoolYearId, cancellationToken);
        if (schoolYear == null)
        {
            return ApiResponse<bool>.Fail(SystemMessages.MSG14, new List<string> { "Năm học không tồn tại." });
        }

        if (schoolYear.IsActive)
        {
            return ApiResponse<bool>.Fail(SystemMessages.MSG09, new List<string> { "Không thể xóa năm học đang hoạt động (Active). Vui lòng chuyển trạng thái hoạt động sang năm học khác trước khi xóa." });
        }

        // UC-13 A2, BR-52: only a Draft school year can be discarded; an archived year stays for history
        if (schoolYear.Status != SchoolYearStatuses.Draft)
        {
            return ApiResponse<bool>.Fail(SystemMessages.MSG09, new List<string> { "Chỉ có thể xóa năm học ở trạng thái Bản nháp. Năm học đã lưu trữ được giữ lại để tra cứu." });
        }

        var classesInYear = await _unitOfWork.Classes.GetClassesBySchoolYearAsync(schoolYearId, null, null, cancellationToken);

        // Check if any class has enrolled students
        if (classesInYear.Any(c => c.ClassAssignments != null && c.ClassAssignments.Any(ca => ca.IsActive)))
        {
            return ApiResponse<bool>.Fail(SystemMessages.MSG09, new List<string> { "Không thể xóa năm học vì đang có học sinh phân công vào các lớp trong năm học này." });
        }

        // Check if any attendance records exist
        var classIds = classesInYear.Select(c => c.Id).ToList();
        var hasAttendance = await AnyRecordAsync<AttendanceRecord>(a => classIds.Contains(a.ClassId), cancellationToken);
        if (hasAttendance)
        {
            return ApiResponse<bool>.Fail(SystemMessages.MSG09, new List<string> { "Không thể xóa năm học vì đã có dữ liệu điểm danh phát sinh." });
        }

        // Safe to delete: remove assignments, schedules, classes, and then the school year
        foreach (var cls in classesInYear)
        {
            var assignments = await _unitOfWork.Repository<ClassAssignment>().FindAsync(ca => ca.ClassId == cls.Id, cancellationToken);
            foreach (var ca in assignments)
            {
                await _unitOfWork.Repository<ClassAssignment>().DeleteAsync(ca, cancellationToken);
            }

            var schedules = await _unitOfWork.Repository<ClassSchedule>().FindAsync(s => s.ClassId == cls.Id, cancellationToken);
            foreach (var s in schedules)
            {
                await _unitOfWork.Repository<ClassSchedule>().DeleteAsync(s, cancellationToken);
            }

            await _unitOfWork.Classes.DeleteAsync(cls, cancellationToken);
        }

        await _unitOfWork.Repository<SchoolYear>().DeleteAsync(schoolYear, cancellationToken);
        await _unitOfWork.SaveChangesAsync(cancellationToken);

        await _auditService.LogActionAsync(
            actorUserId,
            AuditActions.SchoolYearDelete,
            nameof(SchoolYear),
            schoolYearId.ToString(),
            new { schoolYear.YearName, schoolYear.Status, DeletedClassCount = classesInYear.Count },
            null,
            null,
            cancellationToken);

        await _unitOfWork.SaveChangesAsync(cancellationToken);

        return ApiResponse<bool>.Ok(true, "Đã xóa năm học thành công.");
    }

    #endregion

    #region Classes Management

    public async Task<ApiResponse<ClassSummaryDto>> CreateClassAsync(CreateClassDto request, int? actorUserId = null, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(request.ClassName) || request.Grade < 1 || request.Grade > 5)
        {
            return ApiResponse<ClassSummaryDto>.Fail(SystemMessages.MSG01, new List<string> { "Khối học phải từ 1 đến 5." });
        }

        var classNameError = StudentValidationHelper.ValidateClassNameForGrade(request.ClassName, request.Grade);
        if (classNameError != null)
        {
            return ApiResponse<ClassSummaryDto>.Fail(SystemMessages.MSG01, new List<string> { classNameError });
        }

        if (StudentValidationHelper.ContainsHtmlTags(request.RoomNumber))
        {
            return ApiResponse<ClassSummaryDto>.Fail(SystemMessages.MSG01, new List<string> { "Tên phòng học không được chứa thẻ HTML hoặc ký tự '<', '>'." });
        }

        var normalizedClassName = request.ClassName.Trim().ToUpperInvariant();

        if (request.Capacity < 25 || request.Capacity > 40)
        {
            return ApiResponse<ClassSummaryDto>.Fail(SystemMessages.MSG01, new List<string> { "Sĩ số tối đa mỗi lớp phải từ 25 đến 40 học sinh (BR-15)." });
        }

        var schoolYear = await _unitOfWork.Repository<SchoolYear>().GetByIdAsync(request.SchoolYearId, cancellationToken);
        if (schoolYear == null)
        {
            return ApiResponse<ClassSummaryDto>.Fail(SystemMessages.MSG01, new List<string> { "Năm học không hợp lệ." });
        }

        if (string.Equals(schoolYear.Status, SchoolYearStatuses.Archived, StringComparison.OrdinalIgnoreCase))
        {
            return ApiResponse<ClassSummaryDto>.Fail(SystemMessages.MSG01, new List<string> { $"Năm học {schoolYear.YearName} đã lưu trữ (Archived), không thể thêm lớp học mới." });
        }

        var existingClasses = await _unitOfWork.Classes.GetClassesBySchoolYearAsync(request.SchoolYearId, null, null, cancellationToken);
        if (existingClasses != null && existingClasses.Any(c => c.ClassName.Equals(normalizedClassName, StringComparison.OrdinalIgnoreCase)))
        {
            return ApiResponse<ClassSummaryDto>.Fail(SystemMessages.MSG01, new List<string> { $"Lớp {normalizedClassName} đã tồn tại trong năm học {schoolYear.YearName}." });
        }

        var newClass = new Class
        {
            SchoolYearId = request.SchoolYearId,
            ClassName = normalizedClassName,
            Grade = request.Grade,
            RoomNumber = request.RoomNumber?.Trim(),
            Capacity = request.Capacity,
            Status = "Active",
            IsReviewed = string.Equals(schoolYear.Status, SchoolYearStatuses.Active, StringComparison.OrdinalIgnoreCase),
        };

        await _unitOfWork.Classes.AddAsync(newClass, cancellationToken);
        await _unitOfWork.SaveChangesAsync(cancellationToken);

        await _auditService.LogActionAsync(
            actorUserId,
            AuditActions.ClassCreate,
            nameof(Class),
            newClass.Id.ToString(),
            null,
            new { newClass.SchoolYearId, newClass.ClassName, newClass.Grade, newClass.RoomNumber, newClass.Capacity },
            null, // TODO: pass the client IP address; it is not recorded yet.
            cancellationToken);
        await _unitOfWork.SaveChangesAsync(cancellationToken);

        return ApiResponse<ClassSummaryDto>.Ok(new ClassSummaryDto
        {
            Id = newClass.Id,
            SchoolYearId = newClass.SchoolYearId,
            SchoolYearName = schoolYear.YearName,
            ClassName = newClass.ClassName,
            Grade = newClass.Grade,
            RoomNumber = newClass.RoomNumber,
            Capacity = newClass.Capacity,
            EnrolledCount = 0,
            Status = newClass.Status,
        }, SystemMessages.MSG08);
    }

    public async Task<ApiResponse<List<ClassSummaryDto>>> GetClassesAsync(int? schoolYearId = null, int? grade = null, string? status = null, CancellationToken cancellationToken = default)
    {
        int targetYearId;
        if (schoolYearId.HasValue)
        {
            targetYearId = schoolYearId.Value;
        }
        else
        {
            var activeYearDto = await _unitOfWork.Classes.GetActiveSchoolYearDtoAsync(cancellationToken);
            if (activeYearDto != null)
            {
                targetYearId = activeYearDto.Id;
            }
            else
            {
                var activeYear = await _unitOfWork.Classes.GetActiveSchoolYearAsync(cancellationToken);
                if (activeYear == null)
                {
                    return ApiResponse<List<ClassSummaryDto>>.Fail(SystemMessages.MSG14);
                }
                targetYearId = activeYear.Id;
            }
        }

        var fastClasses = await _unitOfWork.Classes.GetClassSummaryDtosAsync(targetYearId, grade, status, cancellationToken);
        if (fastClasses != null)
        {
            return ApiResponse<List<ClassSummaryDto>>.Ok(fastClasses);
        }

        var classes = await _unitOfWork.Classes.GetClassesBySchoolYearAsync(targetYearId, grade, status, cancellationToken);

        var result = classes.Select(c => new ClassSummaryDto
        {
            Id = c.Id,
            SchoolYearId = c.SchoolYearId,
            SchoolYearName = c.SchoolYear?.YearName ?? string.Empty,
            ClassName = c.ClassName,
            Grade = c.Grade,
            RoomNumber = c.RoomNumber,
            Capacity = c.Capacity,
            EnrolledCount = c.ClassAssignments?.Count(IsMember) ?? 0,
            Status = c.Status,
            HomeroomTeacherId = c.HomeroomTeacherId,
            HomeroomTeacherName = c.HomeroomTeacher?.User?.FullName,
            HomeroomTeacherStaffCode = c.HomeroomTeacher?.StaffCode,
            IsReviewed = c.IsReviewed,
            ReviewedAt = c.ReviewedAt
        }).ToList();

        return ApiResponse<List<ClassSummaryDto>>.Ok(result);
    }

    public async Task<ApiResponse<ClassSummaryDto>> GetClassDetailsAsync(int classId, CancellationToken cancellationToken = default)
    {
        var c = await _unitOfWork.Classes.GetClassWithDetailsAsync(classId, cancellationToken);
        if (c == null)
        {
            return ApiResponse<ClassSummaryDto>.Fail(SystemMessages.MSG01, new List<string> { "Không tìm thấy lớp học." });
        }

        var dto = new ClassSummaryDto
        {
            Id = c.Id,
            SchoolYearId = c.SchoolYearId,
            SchoolYearName = c.SchoolYear?.YearName ?? string.Empty,
            ClassName = c.ClassName,
            Grade = c.Grade,
            RoomNumber = c.RoomNumber,
            Capacity = c.Capacity,
            EnrolledCount = c.ClassAssignments?.Count(IsMember) ?? 0,
            Status = c.Status,
            HomeroomTeacherId = c.HomeroomTeacherId,
            HomeroomTeacherName = c.HomeroomTeacher?.User?.FullName,
            HomeroomTeacherStaffCode = c.HomeroomTeacher?.StaffCode,
            IsReviewed = c.IsReviewed,
            ReviewedAt = c.ReviewedAt
        };

        return ApiResponse<ClassSummaryDto>.Ok(dto);
    }

    public async Task<ApiResponse<ClassSummaryDto>> UpdateClassAsync(int classId, UpdateClassDto request, int? actorUserId = null, CancellationToken cancellationToken = default)
    {
        var c = await _unitOfWork.Classes.GetClassWithDetailsAsync(classId, cancellationToken);
        if (c == null)
        {
            return ApiResponse<ClassSummaryDto>.Fail(SystemMessages.MSG01, new List<string> { "Không tìm thấy lớp học." });
        }

        if (request.Grade < 1 || request.Grade > 5 || request.Capacity <= 0)
        {
            return ApiResponse<ClassSummaryDto>.Fail(SystemMessages.MSG01, new List<string> { "Khối học phải từ 1 đến 5 và sĩ số tối đa phải lớn hơn 0." });
        }

        var classNameError = StudentValidationHelper.ValidateClassNameForGrade(request.ClassName, request.Grade);
        if (classNameError != null)
        {
            return ApiResponse<ClassSummaryDto>.Fail(SystemMessages.MSG01, new List<string> { classNameError });
        }

        if (StudentValidationHelper.ContainsHtmlTags(request.RoomNumber))
        {
            return ApiResponse<ClassSummaryDto>.Fail(SystemMessages.MSG01, new List<string> { "Tên phòng học không được chứa thẻ HTML hoặc ký tự '<', '>'." });
        }

        var normalizedName = request.ClassName.Trim().ToUpperInvariant();
        var enrolledCount = c.ClassAssignments?.Count(IsMember) ?? 0;
        if (request.Capacity < enrolledCount)
        {
            return ApiResponse<ClassSummaryDto>.Fail(SystemMessages.MSG15, new List<string> { $"Không thể giảm sĩ số tối đa ({request.Capacity}) nhỏ hơn số học sinh hiện tại đang có ({enrolledCount})." });
        }

        var oldValues = new { c.ClassName, c.Grade, c.RoomNumber, c.Capacity, c.Status };

        var changed = c.ClassName != normalizedName || c.Grade != request.Grade || c.RoomNumber != request.RoomNumber?.Trim()
                      || c.Capacity != request.Capacity || c.Status != request.Status;

        c.ClassName = normalizedName;
        c.Grade = request.Grade;
        c.RoomNumber = request.RoomNumber?.Trim();
        c.Capacity = request.Capacity;
        c.Status = request.Status;
        if (changed)
        {
            ResetReview(c);
        }

        await _unitOfWork.Classes.UpdateAsync(c, cancellationToken);
        await _unitOfWork.SaveChangesAsync(cancellationToken);

        await _auditService.LogActionAsync(
            actorUserId,
            AuditActions.ClassUpdate,
            nameof(Class),
            c.Id.ToString(),
            oldValues,
            new { c.ClassName, c.Grade, c.RoomNumber, c.Capacity, c.Status },
            null, // TODO: pass the client IP address; it is not recorded yet.
            cancellationToken);
        await _unitOfWork.SaveChangesAsync(cancellationToken);

        return ApiResponse<ClassSummaryDto>.Ok(new ClassSummaryDto
        {
            Id = c.Id,
            SchoolYearId = c.SchoolYearId,
            SchoolYearName = c.SchoolYear?.YearName ?? string.Empty,
            ClassName = c.ClassName,
            Grade = c.Grade,
            RoomNumber = c.RoomNumber,
            Capacity = c.Capacity,
            EnrolledCount = enrolledCount,
            Status = c.Status,
            HomeroomTeacherId = c.HomeroomTeacherId,
            HomeroomTeacherName = c.HomeroomTeacher?.User?.FullName,
            HomeroomTeacherStaffCode = c.HomeroomTeacher?.StaffCode,
            IsReviewed = c.IsReviewed,
            ReviewedAt = c.ReviewedAt
        }, SystemMessages.MSG08);
    }

    public async Task<ApiResponse<bool>> DeleteClassAsync(int classId, int? actorUserId = null, CancellationToken cancellationToken = default)
    {
        var targetClass = await _unitOfWork.Classes.GetClassWithDetailsAsync(classId, cancellationToken);
        if (targetClass == null)
        {
            return ApiResponse<bool>.Fail(SystemMessages.MSG01, new List<string> { "Lớp học không tồn tại." });
        }

        var activeEnrolledCount = targetClass.ClassAssignments?.Count(IsMember) ?? 0;
        if (activeEnrolledCount > 0)
        {
            return ApiResponse<bool>.Fail(
                SystemMessages.MSG09,
                new List<string> { $"Không thể xóa lớp học vì lớp đang có {activeEnrolledCount} học sinh. Chỉ có thể xóa lớp học khi sĩ số bằng 0." });
        }

        if (targetClass.HomeroomTeacherId.HasValue || targetClass.HomeroomTeacher != null)
        {
            return ApiResponse<bool>.Fail(
                SystemMessages.MSG09,
                new List<string> { "Không thể xóa lớp học vì lớp đang có Giáo viên chủ nhiệm phân công. Vui lòng gỡ GVCN trước khi xóa lớp." });
        }

        var hasAttendance = await AnyRecordAsync<AttendanceRecord>(a => a.ClassId == classId, cancellationToken);
        if (hasAttendance)
        {
            return ApiResponse<bool>.Fail(
                SystemMessages.MSG09,
                new List<string> { "Không thể xóa lớp học vì lớp đã phát sinh dữ liệu điểm danh." });
        }

        var hasSchedule = await AnyRecordAsync<ClassSchedule>(s => s.ClassId == classId, cancellationToken);
        if (hasSchedule)
        {
            return ApiResponse<bool>.Fail(
                SystemMessages.MSG09,
                new List<string> { "Không thể xóa lớp học vì lớp đã có thời khóa biểu." });
        }

        var pastAssignments = await _unitOfWork.Repository<ClassAssignment>().FindAsync(ca => ca.ClassId == classId, cancellationToken);
        foreach (var ca in pastAssignments)
        {
            await _unitOfWork.Repository<ClassAssignment>().DeleteAsync(ca, cancellationToken);
        }

        await _unitOfWork.Classes.DeleteAsync(targetClass, cancellationToken);
        await _unitOfWork.SaveChangesAsync(cancellationToken);

        await _auditService.LogActionAsync(
            actorUserId,
            AuditActions.ClassDelete,
            nameof(Class),
            targetClass.Id.ToString(),
            new { targetClass.ClassName, targetClass.Grade, targetClass.SchoolYearId },
            null,
            null,
            cancellationToken);

        return ApiResponse<bool>.Ok(true, "Đã xóa lớp học thành công.");
    }

    #endregion

    #region Homeroom Teacher Assignment (UC-15)

    public async Task<ApiResponse<List<TeacherOptionDto>>> GetEligibleTeachersAsync(int? schoolYearId = null, CancellationToken cancellationToken = default)
    {
        int targetYearId;
        if (schoolYearId.HasValue)
        {
            targetYearId = schoolYearId.Value;
        }
        else
        {
            var activeYearDto = await _unitOfWork.Classes.GetActiveSchoolYearDtoAsync(cancellationToken);
            if (activeYearDto != null)
            {
                targetYearId = activeYearDto.Id;
            }
            else
            {
                var activeYear = await _unitOfWork.Classes.GetActiveSchoolYearAsync(cancellationToken);
                if (activeYear == null)
                {
                    return ApiResponse<List<TeacherOptionDto>>.Fail(SystemMessages.MSG14);
                }
                targetYearId = activeYear.Id;
            }
        }

        var fastTeachers = await _unitOfWork.Classes.GetEligibleTeacherDtosAsync(targetYearId, cancellationToken);
        if (fastTeachers != null)
        {
            return ApiResponse<List<TeacherOptionDto>>.Ok(fastTeachers);
        }

        var teachers = await _unitOfWork.Classes.GetEligibleTeachersAsync(cancellationToken);
        var activeClassesInYear = await _unitOfWork.Classes.GetClassesBySchoolYearAsync(targetYearId, null, null, cancellationToken);
        var teacherClassMap = activeClassesInYear
            .Where(c => c.HomeroomTeacherId.HasValue)
            .ToDictionary(c => c.HomeroomTeacherId!.Value, c => c.ClassName);

        var result = teachers.Select(t => new TeacherOptionDto
        {
            StaffProfileId = t.Id,
            UserId = t.UserId,
            FullName = t.User.FullName,
            StaffCode = t.StaffCode,
            Department = t.Department,
            CurrentHomeroomClassName = teacherClassMap.TryGetValue(t.Id, out var cName) ? cName : null,
            HasActiveHomeroom = teacherClassMap.ContainsKey(t.Id)
        }).ToList();

        return ApiResponse<List<TeacherOptionDto>>.Ok(result);
    }

    public async Task<ApiResponse<bool>> AssignHomeroomTeacherAsync(int classId, AssignTeacherDto request, int? actorUserId = null, CancellationToken cancellationToken = default)
    {
        var targetClass = await _unitOfWork.Classes.GetClassWithDetailsAsync(classId, cancellationToken);
        if (targetClass == null)
        {
            return ApiResponse<bool>.Fail(SystemMessages.MSG01, new List<string> { "Không tìm thấy lớp học." });
        }

        int targetStaffProfileId = request.StaffProfileId;
        if (targetStaffProfileId <= 0 && request.TeacherUserId.HasValue)
        {
            var staffByUserId = (await _unitOfWork.Repository<StaffProfile>().FindAsync(s => s.UserId == request.TeacherUserId.Value, cancellationToken)).FirstOrDefault();
            if (staffByUserId != null)
            {
                targetStaffProfileId = staffByUserId.Id;
            }
        }

        var teacherProfile = await _unitOfWork.Repository<StaffProfile>().GetByIdAsync(targetStaffProfileId, cancellationToken);
        if (teacherProfile == null)
        {
            return ApiResponse<bool>.Fail(SystemMessages.MSG01, new List<string> { "Không tìm thấy hồ sơ giáo viên." });
        }

        // BR-16: A class may have at most one active primary Teacher assignment for the same effective period.
        // Check if this teacher is already assigned to another class in this school year
        var hasConflict = await _unitOfWork.Classes.IsTeacherAssignedToActiveHomeroomAsync(
            targetStaffProfileId,
            targetClass.SchoolYearId,
            classId,
            cancellationToken);

        if (hasConflict)
        {
            return ApiResponse<bool>.Fail(SystemMessages.MSG01, new List<string> { "Giáo viên này đã được phân công chủ nhiệm cho một lớp khác trong cùng năm học." });
        }

        var priorTeacherId = targetClass.HomeroomTeacherId;
        if (priorTeacherId != targetStaffProfileId)
        {
            ResetReview(targetClass);
        }
        targetClass.HomeroomTeacherId = targetStaffProfileId;
        teacherProfile.IsHomeroomTeacher = true;

        await _unitOfWork.Classes.UpdateAsync(targetClass, cancellationToken);
        await _unitOfWork.Repository<StaffProfile>().UpdateAsync(teacherProfile, cancellationToken);
        await _unitOfWork.SaveChangesAsync(cancellationToken);

        // Notify teacher of homeroom assignment (BR-42); for a Draft school year at activation (UC-15 step 5)
        if (!IsDraftYear(targetClass.SchoolYear))
        {
            var notification = BuildHomeroomNotification(teacherProfile.UserId, targetClass, targetClass.SchoolYear?.YearName);
            await _unitOfWork.Repository<Notification>().AddAsync(notification, cancellationToken);
            await _unitOfWork.SaveChangesAsync(cancellationToken);
        }

        await _auditService.LogActionAsync(
            actorUserId,
            AuditActions.ClassAssignHomeroomTeacher,
            nameof(Class),
            targetClass.Id.ToString(),
            new { HomeroomTeacherId = priorTeacherId },
            new { HomeroomTeacherId = request.StaffProfileId },
            null, // TODO: pass the client IP address; it is not recorded yet.
            cancellationToken);
        await _unitOfWork.SaveChangesAsync(cancellationToken);

        return ApiResponse<bool>.Ok(true, SystemMessages.MSG08);
    }

    public async Task<ApiResponse<bool>> UnassignHomeroomTeacherAsync(int classId, int? actorUserId = null, CancellationToken cancellationToken = default)
    {
        var targetClass = await _unitOfWork.Classes.GetClassWithDetailsAsync(classId, cancellationToken);
        if (targetClass == null)
        {
            return ApiResponse<bool>.Fail(SystemMessages.MSG01, new List<string> { "Không tìm thấy lớp học." });
        }

        if (!targetClass.HomeroomTeacherId.HasValue)
        {
            return ApiResponse<bool>.Ok(true, "Lớp này hiện chưa có Giáo viên chủ nhiệm.");
        }

        var priorTeacherId = targetClass.HomeroomTeacherId.Value;
        var teacherProfile = await _unitOfWork.Repository<StaffProfile>().GetByIdAsync(priorTeacherId, cancellationToken);

        targetClass.HomeroomTeacherId = null;
        ResetReview(targetClass);
        if (teacherProfile != null)
        {
            // Check if teacher is assigned to any other classes in the school year
            var otherClasses = await _unitOfWork.Classes.GetClassesBySchoolYearAsync(targetClass.SchoolYearId, null, null, cancellationToken);
            var isCNAnother = otherClasses != null && otherClasses.Any(c => c.Id != classId && c.HomeroomTeacherId == priorTeacherId);
            if (!isCNAnother)
            {
                teacherProfile.IsHomeroomTeacher = false;
                await _unitOfWork.Repository<StaffProfile>().UpdateAsync(teacherProfile, cancellationToken);
            }
        }

        await _unitOfWork.Classes.UpdateAsync(targetClass, cancellationToken);
        await _unitOfWork.SaveChangesAsync(cancellationToken);

        await _auditService.LogActionAsync(
            actorUserId,
            AuditActions.ClassAssignHomeroomTeacher,
            nameof(Class),
            targetClass.Id.ToString(),
            new { HomeroomTeacherId = priorTeacherId },
            new { HomeroomTeacherId = (int?)null },
            null,
            cancellationToken);

        await _unitOfWork.SaveChangesAsync(cancellationToken);

        return ApiResponse<bool>.Ok(true, "Đã gỡ Giáo viên chủ nhiệm khỏi lớp thành công.");
    }

    #endregion

    #region Student Class Assignment (UC-16)

    public async Task<ApiResponse<List<RosterStudentItemDto>>> GetEligibleStudentsForAssignmentAsync(int classId, CancellationToken cancellationToken = default)
    {
        var targetClass = await _unitOfWork.Classes.GetClassWithDetailsAsync(classId, cancellationToken);
        if (targetClass == null)
        {
            return ApiResponse<List<RosterStudentItemDto>>.Fail(SystemMessages.MSG01, new List<string> { "Không tìm thấy lớp học." });
        }

        var unassigned = await _unitOfWork.Classes.GetEligibleStudentsForAssignmentAsync(targetClass.SchoolYearId, targetClass.Grade, cancellationToken);

        var result = unassigned.Select(s =>
        {
            var primaryGuardian = s.StudentGuardians.FirstOrDefault(sg => sg.IsPrimaryContact)?.Guardian;
            return new RosterStudentItemDto
            {
                StudentId = s.Id,
                StudentCode = s.StudentCode,
                FullName = s.FullName,
                DateOfBirth = s.DateOfBirth,
                Gender = s.Gender,
                Status = s.Status,
                AvatarUrl = s.AvatarUrl,
                ResidentialArea = s.ResidentialArea,
                IsSpecialNeeds = s.IsSpecialNeeds,
                SpecialNeedsNotes = s.SpecialNeedsNotes,
                EnrolledDate = s.EnrollmentDate,
                PrimaryGuardianName = primaryGuardian?.User?.FullName,
                PrimaryGuardianPhone = primaryGuardian?.User?.PhoneNumber,
                PrimaryGuardianRelationship = s.StudentGuardians.FirstOrDefault(sg => sg.IsPrimaryContact)?.Relationship.ToString(),
                EmergencyContactPhone = s.StudentGuardians.FirstOrDefault(sg => !sg.IsPrimaryContact)?.Guardian?.User?.PhoneNumber ?? primaryGuardian?.User?.PhoneNumber
            };
        }).ToList();

        return ApiResponse<List<RosterStudentItemDto>>.Ok(result);
    }

    public async Task<ApiResponse<PaginatedResult<RosterStudentItemDto>>> GetEligibleStudentsForAssignmentPagedAsync(
        int classId,
        string? search = null,
        int pageIndex = 1,
        int pageSize = 25,
        CancellationToken cancellationToken = default)
    {
        if (pageIndex < 1) pageIndex = 1;
        if (pageSize < 1 || pageSize > 100) pageSize = 25;

        int? schoolYearId = null;
        var classQuery = _unitOfWork.Repository<Class>()?.Query();
        if (classQuery != null)
        {
            schoolYearId = await Microsoft.EntityFrameworkCore.EntityFrameworkQueryableExtensions.FirstOrDefaultAsync(
                Microsoft.EntityFrameworkCore.EntityFrameworkQueryableExtensions.AsNoTracking(classQuery)
                    .Where(c => c.Id == classId)
                    .Select(c => (int?)c.SchoolYearId),
                cancellationToken);
        }
        else
        {
            var targetClass = await _unitOfWork.Classes.GetClassWithDetailsAsync(classId, cancellationToken);
            schoolYearId = targetClass?.SchoolYearId;
        }

        if (!schoolYearId.HasValue)
        {
            return ApiResponse<PaginatedResult<RosterStudentItemDto>>.Fail(SystemMessages.MSG01, new List<string> { "Không tìm thấy lớp học." });
        }

        var fastPaged = await _unitOfWork.Classes.GetEligibleStudentsPagedAsync(schoolYearId.Value, search, pageIndex, pageSize, cancellationToken);
        if (fastPaged != null)
        {
            return ApiResponse<PaginatedResult<RosterStudentItemDto>>.Ok(fastPaged);
        }

        var unpaged = await GetEligibleStudentsForAssignmentAsync(classId, cancellationToken);
        if (!unpaged.Success)
        {
            return ApiResponse<PaginatedResult<RosterStudentItemDto>>.Fail(SystemMessages.MSG01, unpaged.Errors);
        }

        var items = unpaged.Data ?? new List<RosterStudentItemDto>();
        if (!string.IsNullOrWhiteSpace(search))
        {
            var term = search.Trim().ToLowerInvariant();
            items = items.Where(s =>
                s.FullName.ToLowerInvariant().Contains(term) ||
                s.StudentCode.ToLowerInvariant().Contains(term)).ToList();
        }

        var totalCount = items.Count;
        var paged = items.Skip((pageIndex - 1) * pageSize).Take(pageSize).ToList();
        return ApiResponse<PaginatedResult<RosterStudentItemDto>>.Ok(
            new PaginatedResult<RosterStudentItemDto>(paged, totalCount, pageIndex, pageSize));
    }

    public async Task<ApiResponse<bool>> AssignStudentsToClassAsync(int classId, AssignStudentsDto request, int? actorUserId = null, CancellationToken cancellationToken = default)
    {
        if (request.StudentIds == null || !request.StudentIds.Any())
        {
            return ApiResponse<bool>.Fail(SystemMessages.MSG01, new List<string> { "Vui lòng chọn ít nhất một học sinh để phân lớp." });
        }

        var targetClass = await _unitOfWork.Classes.GetClassWithDetailsAsync(classId, cancellationToken);
        if (targetClass == null)
        {
            return ApiResponse<bool>.Fail(SystemMessages.MSG01, new List<string> { "Không tìm thấy lớp học." });
        }

        // BR-15 & BR-17: Check capacity
        var currentEnrolled = targetClass.ClassAssignments.Count(IsMember);
        if (currentEnrolled + request.StudentIds.Count > targetClass.Capacity)
        {
            return ApiResponse<bool>.Fail(SystemMessages.MSG15, new List<string> { $"Lớp {targetClass.ClassName} đã đạt hoặc vượt quá sĩ số tối đa ({targetClass.Capacity}). Hiện có {currentEnrolled} học sinh." });
        }

        // Check if any student already has an active class assignment for this school year (BR-17)
        var activeAssignmentsInYear = await _unitOfWork.Repository<ClassAssignment>()
            .FindAsync(ca => (ca.IsActive || ca.IsDraft) && ca.Class.SchoolYearId == targetClass.SchoolYearId && request.StudentIds.Contains(ca.StudentId), cancellationToken);

        if (activeAssignmentsInYear.Any())
        {
            return ApiResponse<bool>.Fail(SystemMessages.MSG15, new List<string> { "Một hoặc nhiều học sinh đã được phân vào lớp khác trong năm học này." });
        }

        // SC-03: Check student age vs target class grade compatibility
        var distinctStudentIds = request.StudentIds.Distinct().ToList();
        var studentRepo = _unitOfWork.Repository<Student>();
        if (studentRepo != null)
        {
            var studentsToAssign = await studentRepo.FindAsync(s => distinctStudentIds.Contains(s.Id), cancellationToken);
            if (studentsToAssign != null)
            {
                var syStartYear = targetClass.SchoolYear?.StartDate.Year;
                foreach (var st in studentsToAssign)
                {
                    if (st.DateOfBirth != default)
                    {
                        var gradeAgeError = StudentValidationHelper.ValidateAgeForGrade(st.DateOfBirth, targetClass.Grade, syStartYear, st.IsSpecialNeeds);
                        if (gradeAgeError != null)
                        {
                            return ApiResponse<bool>.Fail(
                                SystemMessages.MSG01,
                                new List<string> { $"Học sinh {st.FullName} ({st.StudentCode}): {gradeAgeError}" });
                        }
                    }
                }
            }
        }

        var academicYearName = targetClass.SchoolYear?.YearName ?? DateTime.UtcNow.Year.ToString();
        // UC-16 A1, BR-72: in a Draft school year the assignment does not change the student's current class
        var isDraftYear = IsDraftYear(targetClass.SchoolYear);

        foreach (var studentId in distinctStudentIds)
        {
            var assignment = new ClassAssignment
            {
                ClassId = classId,
                StudentId = studentId,
                AcademicYear = academicYearName,
                StartDate = isDraftYear ? targetClass.SchoolYear!.StartDate.Date : DateTime.UtcNow.Date,
                IsActive = !isDraftYear,
                IsDraft = isDraftYear
            };
            await _unitOfWork.Repository<ClassAssignment>().AddAsync(assignment, cancellationToken);
        }

        if (isDraftYear)
        {
            ResetReview(targetClass);
            await _unitOfWork.Classes.UpdateAsync(targetClass, cancellationToken);
        }

        await _unitOfWork.SaveChangesAsync(cancellationToken);

        await _auditService.LogActionAsync(
            actorUserId,
            AuditActions.ClassAssignStudents,
            nameof(Class),
            classId.ToString(),
            null,
            new { targetClass.ClassName, request.StudentIds },
            null, // TODO: pass the client IP address; it is not recorded yet.
            cancellationToken);
        await _unitOfWork.SaveChangesAsync(cancellationToken);

        return ApiResponse<bool>.Ok(true, SystemMessages.MSG16);
    }

    public async Task<ApiResponse<bool>> RemoveStudentFromClassAsync(int classId, int studentId, int? actorUserId = null, CancellationToken cancellationToken = default)
    {
        var assignment = (await _unitOfWork.Repository<ClassAssignment>()
            .FindAsync(ca => ca.ClassId == classId && ca.StudentId == studentId && (ca.IsActive || ca.IsDraft), cancellationToken))
            .FirstOrDefault();

        if (assignment == null)
        {
            return ApiResponse<bool>.Fail(SystemMessages.MSG01, new List<string> { "Học sinh không có trong danh sách lớp này." });
        }

        await EndOrDeleteAssignmentAsync(assignment, DateTime.UtcNow, cancellationToken);
        await ResetReviewIfDraftAsync(classId, assignment.IsDraft, cancellationToken);
        await _unitOfWork.SaveChangesAsync(cancellationToken);

        await _auditService.LogActionAsync(
            actorUserId,
            AuditActions.ClassRemoveStudent,
            nameof(Class),
            classId.ToString(),
            new { StudentId = studentId, AssignmentId = assignment.Id, IsActive = true },
            new { StudentId = studentId, AssignmentId = assignment.Id, assignment.IsActive, assignment.EndDate },
            null, // TODO: pass the client IP address; it is not recorded yet.
            cancellationToken);
        await _unitOfWork.SaveChangesAsync(cancellationToken);

        return ApiResponse<bool>.Ok(true, SystemMessages.MSG08);
    }

    public async Task<ApiResponse<bool>> RemoveStudentsBatchFromClassAsync(int classId, List<int> studentIds, int? actorUserId = null, CancellationToken cancellationToken = default)
    {
        if (studentIds == null || !studentIds.Any())
        {
            return ApiResponse<bool>.Fail(SystemMessages.MSG01, new List<string> { "Vui lòng chọn ít nhất một học sinh để gỡ khỏi lớp." });
        }

        var distinctIds = studentIds.Distinct().ToList();
        var assignments = await _unitOfWork.Repository<ClassAssignment>()
            .FindAsync(ca => ca.ClassId == classId && distinctIds.Contains(ca.StudentId) && (ca.IsActive || ca.IsDraft), cancellationToken);

        var list = assignments.ToList();
        if (!list.Any())
        {
            return ApiResponse<bool>.Fail(SystemMessages.MSG01, new List<string> { "Không tìm thấy học sinh nào đang phân công trong lớp này." });
        }

        var now = DateTime.UtcNow;
        foreach (var assignment in list)
        {
            await EndOrDeleteAssignmentAsync(assignment, now, cancellationToken);
        }
        await ResetReviewIfDraftAsync(classId, list.Any(a => a.IsDraft), cancellationToken);

        await _unitOfWork.SaveChangesAsync(cancellationToken);

        await _auditService.LogActionAsync(
            actorUserId,
            AuditActions.ClassRemoveStudent,
            nameof(Class),
            classId.ToString(),
            new { RemovedCount = list.Count, StudentIds = distinctIds },
            new { IsActive = false, EndDate = now },
            null,
            cancellationToken);

        await _unitOfWork.SaveChangesAsync(cancellationToken);

        return ApiResponse<bool>.Ok(true, $"Đã gỡ thành công {list.Count} học sinh khỏi lớp học.");
    }

    #endregion

    #region Class Roster (UC-17)

    public async Task<ApiResponse<ClassRosterDto>> GetClassRosterAsync(int classId, int? requestingUserId = null, string? requestingUserRole = null, CancellationToken cancellationToken = default)
    {
        var fastRoster = await _unitOfWork.Classes.GetClassRosterWithAccessCheckAsync(classId, cancellationToken);
        if (fastRoster.HasValue)
        {
            if (string.Equals(requestingUserRole, UserRoles.Teacher.ToString(), StringComparison.OrdinalIgnoreCase))
            {
                if (!fastRoster.Value.HomeroomTeacherUserId.HasValue || fastRoster.Value.HomeroomTeacherUserId.Value != requestingUserId)
                {
                    return ApiResponse<ClassRosterDto>.Fail(SystemMessages.MSG20); // MSG20: Bạn không được phân công phụ trách lớp này.
                }
            }

            return ApiResponse<ClassRosterDto>.Ok(fastRoster.Value.Roster);
        }

        var targetClass = await _unitOfWork.Classes.GetClassWithDetailsAsync(classId, cancellationToken);
        if (targetClass == null)
        {
            return ApiResponse<ClassRosterDto>.Fail(SystemMessages.MSG01, new List<string> { "Không tìm thấy lớp học." });
        }

        // BR-07, BR-20: If Teacher role, strictly verify homeroom assignment
        if (string.Equals(requestingUserRole, UserRoles.Teacher.ToString(), StringComparison.OrdinalIgnoreCase))
        {
            if (targetClass.HomeroomTeacher == null || targetClass.HomeroomTeacher.UserId != requestingUserId)
            {
                return ApiResponse<ClassRosterDto>.Fail(SystemMessages.MSG20); // MSG20: Bạn không được phân công phụ trách lớp này.
            }
        }

        var activeAssignments = targetClass.ClassAssignments.Where(IsMember).ToList();

        var roster = new ClassRosterDto
        {
            ClassId = targetClass.Id,
            ClassName = targetClass.ClassName,
            Grade = targetClass.Grade,
            AcademicYear = targetClass.SchoolYear?.YearName ?? string.Empty,
            SchoolYearStartDate = targetClass.SchoolYear?.StartDate,
            SchoolYearEndDate = targetClass.SchoolYear?.EndDate,
            RoomNumber = targetClass.RoomNumber,
            Capacity = targetClass.Capacity,
            TotalEnrolled = activeAssignments.Count,
            HomeroomTeacherName = targetClass.HomeroomTeacher?.User?.FullName,
            HomeroomTeacherPhone = targetClass.HomeroomTeacher?.User?.PhoneNumber,
            HomeroomTeacherEmail = targetClass.HomeroomTeacher?.User?.Email,
            Students = activeAssignments.Select(ca =>
            {
                var s = ca.Student;
                var primaryGuardian = s.StudentGuardians.FirstOrDefault(sg => sg.IsPrimaryContact)?.Guardian;
                return new RosterStudentItemDto
                {
                    StudentId = s.Id,
                    StudentCode = s.StudentCode,
                    FullName = s.FullName,
                    DateOfBirth = s.DateOfBirth,
                    Gender = s.Gender,
                    Status = s.Status,
                    AvatarUrl = s.AvatarUrl,
                    ResidentialArea = s.ResidentialArea,
                    IsSpecialNeeds = s.IsSpecialNeeds,
                    SpecialNeedsNotes = s.SpecialNeedsNotes,
                    EnrolledDate = s.EnrollmentDate,
                    PrimaryGuardianName = primaryGuardian?.User?.FullName,
                    PrimaryGuardianPhone = primaryGuardian?.User?.PhoneNumber,
                    PrimaryGuardianRelationship = s.StudentGuardians.FirstOrDefault(sg => sg.IsPrimaryContact)?.Relationship.ToString(),
                    EmergencyContactPhone = s.StudentGuardians.FirstOrDefault(sg => !sg.IsPrimaryContact)?.Guardian?.User?.PhoneNumber ?? primaryGuardian?.User?.PhoneNumber
                };
            }).OrderBy(s => s.FullName).ToList()
        };

        return ApiResponse<ClassRosterDto>.Ok(roster);
    }

    public async Task<ApiResponse<ClassRosterDto>> GetMyHomeroomClassAsync(int teacherUserId, CancellationToken cancellationToken = default)
    {
        var fastClassId = await _unitOfWork.Classes.GetHomeroomClassIdByTeacherUserIdAsync(teacherUserId, null, cancellationToken);
        if (fastClassId.HasValue)
        {
            return await GetClassRosterAsync(fastClassId.Value, teacherUserId, UserRoles.Teacher.ToString(), cancellationToken);
        }

        var homeroomClass = await _unitOfWork.Classes.GetHomeroomClassByTeacherUserIdAsync(teacherUserId, null, cancellationToken);
        if (homeroomClass == null)
        {
            return ApiResponse<ClassRosterDto>.Fail(SystemMessages.MSG43); // MSG43: Không có dữ liệu phù hợp để hiển thị.
        }

        return await GetClassRosterAsync(homeroomClass.Id, teacherUserId, UserRoles.Teacher.ToString(), cancellationToken);
    }

    #region Draft School Year Review (UC-14 A2, BR-72)

    public async Task<ApiResponse<ClassSummaryDto>> MarkClassReviewedAsync(int classId, int? actorUserId = null, CancellationToken cancellationToken = default)
    {
        var targetClass = await _unitOfWork.Classes.GetClassWithDetailsAsync(classId, cancellationToken);
        if (targetClass == null)
        {
            return ApiResponse<ClassSummaryDto>.Fail(SystemMessages.MSG01, new List<string> { "Không tìm thấy lớp học." });
        }

        if (!IsDraftYear(targetClass.SchoolYear))
        {
            return ApiResponse<ClassSummaryDto>.Fail(SystemMessages.MSG01, new List<string> { "Chỉ rà soát lớp của năm học ở trạng thái Bản nháp." });
        }

        if (!targetClass.IsReviewed)
        {
            targetClass.IsReviewed = true;
            targetClass.ReviewedAt = DateTime.UtcNow;
            targetClass.ReviewedByUserId = actorUserId;
            await _unitOfWork.Classes.UpdateAsync(targetClass, cancellationToken);
            await _unitOfWork.SaveChangesAsync(cancellationToken);

            await _auditService.LogActionAsync(
                actorUserId,
                AuditActions.ClassMarkReviewed,
                nameof(Class),
                targetClass.Id.ToString(),
                new { IsReviewed = false },
                new { targetClass.ClassName, targetClass.IsReviewed, targetClass.ReviewedAt, EnrolledCount = targetClass.ClassAssignments?.Count(IsMember) ?? 0 },
                null, // TODO: pass the client IP address; it is not recorded yet.
                cancellationToken);
            await _unitOfWork.SaveChangesAsync(cancellationToken);
        }

        return ApiResponse<ClassSummaryDto>.Ok(new ClassSummaryDto
        {
            Id = targetClass.Id,
            SchoolYearId = targetClass.SchoolYearId,
            SchoolYearName = targetClass.SchoolYear?.YearName ?? string.Empty,
            ClassName = targetClass.ClassName,
            Grade = targetClass.Grade,
            RoomNumber = targetClass.RoomNumber,
            Capacity = targetClass.Capacity,
            EnrolledCount = targetClass.ClassAssignments?.Count(IsMember) ?? 0,
            Status = targetClass.Status,
            HomeroomTeacherId = targetClass.HomeroomTeacherId,
            HomeroomTeacherName = targetClass.HomeroomTeacher?.User?.FullName,
            HomeroomTeacherStaffCode = targetClass.HomeroomTeacher?.StaffCode,
            IsReviewed = targetClass.IsReviewed,
            ReviewedAt = targetClass.ReviewedAt
        }, SystemMessages.MSG08);
    }

    #endregion

    /// <summary>A Draft assignment counts as a member of its (Draft) class (BR-72).</summary>
    private static bool IsMember(ClassAssignment ca) => ca.IsActive || ca.IsDraft;

    private static bool IsDraftYear(SchoolYear? year) => year?.Status == SchoolYearStatuses.Draft;

    /// <summary>BR-72: any change to a Reviewed class resets it to Not reviewed.</summary>
    internal static void ResetReview(Class c)
    {
        c.IsReviewed = false;
        c.ReviewedAt = null;
        c.ReviewedByUserId = null;
    }

    private async Task ResetReviewIfDraftAsync(int classId, bool changedDraftAssignment, CancellationToken cancellationToken)
    {
        if (!changedDraftAssignment) return;
        var cls = await _unitOfWork.Repository<Class>().GetByIdAsync(classId, cancellationToken);
        if (cls == null || !cls.IsReviewed) return;
        ResetReview(cls);
        await _unitOfWork.Classes.UpdateAsync(cls, cancellationToken);
    }

    /// <summary>
    /// BR-18: a current assignment is ended and kept as history; a Draft assignment is deleted, since it was never
    /// anyone's current class.
    /// </summary>
    private async Task EndOrDeleteAssignmentAsync(ClassAssignment assignment, DateTime endDate, CancellationToken cancellationToken)
    {
        if (assignment.IsDraft)
        {
            await _unitOfWork.Repository<ClassAssignment>().DeleteAsync(assignment, cancellationToken);
            return;
        }

        assignment.IsActive = false;
        assignment.EndDate = endDate;
        await _unitOfWork.Repository<ClassAssignment>().UpdateAsync(assignment, cancellationToken);
    }

    private static Notification BuildHomeroomNotification(int teacherUserId, Class cls, string? yearName) => new()
    {
        UserId = teacherUserId,
        Title = "Phân công giáo viên chủ nhiệm",
        Content = $"Bạn đã được phân công làm giáo viên chủ nhiệm lớp {cls.ClassName} năm học {yearName}.",
        LinkUrl = $"/teacher/classes/{cls.Id}",
        CreatedAt = DateTime.UtcNow,
        IsRead = false
    };

    private async Task<bool> AnyRecordAsync<T>(System.Linq.Expressions.Expression<Func<T, bool>> predicate, CancellationToken cancellationToken) where T : class
    {
        var repo = _unitOfWork.Repository<T>();
        var query = repo.Query();
        if (query != null)
        {
            return await Microsoft.EntityFrameworkCore.EntityFrameworkQueryableExtensions.AnyAsync(
                Microsoft.EntityFrameworkCore.EntityFrameworkQueryableExtensions.AsNoTracking(query),
                predicate,
                cancellationToken);
        }

        var results = await repo.FindAsync(predicate, cancellationToken);
        return results != null && results.Any();
    }

    #endregion
}
