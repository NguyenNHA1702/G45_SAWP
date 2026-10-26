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

        var existingYears = await _unitOfWork.Classes.GetAllSchoolYearsAsync(cancellationToken);
        if (existingYears.Any(y => y.YearName.Trim().Equals(request.YearName.Trim(), StringComparison.OrdinalIgnoreCase)))
        {
            return ApiResponse<SchoolYearDto>.Fail(SystemMessages.MSG01, new List<string> { $"Năm học '{request.YearName.Trim()}' đã tồn tại trong hệ thống." });
        }

        // BR-14: Exactly one school year may be designated as the current active school year at a time
        if (request.IsActive)
        {
            foreach (var sy in existingYears.Where(y => y.IsActive))
            {
                sy.IsActive = false;
                await _unitOfWork.Repository<SchoolYear>().UpdateAsync(sy, cancellationToken);
            }
        }

        var schoolYear = new SchoolYear
        {
            YearName = request.YearName.Trim(),
            StartDate = request.StartDate,
            EndDate = request.EndDate,
            IsActive = request.IsActive
        };

        await _unitOfWork.Repository<SchoolYear>().AddAsync(schoolYear, cancellationToken);
        await _unitOfWork.SaveChangesAsync(cancellationToken);

        await _auditService.LogActionAsync(
            actorUserId,
            "CreateSchoolYear",
            nameof(SchoolYear),
            schoolYear.Id.ToString(),
            null,
            $"Year: {schoolYear.YearName}, Active: {schoolYear.IsActive}",
            null,
            cancellationToken);

        return ApiResponse<SchoolYearDto>.Ok(new SchoolYearDto
        {
            Id = schoolYear.Id,
            YearName = schoolYear.YearName,
            StartDate = schoolYear.StartDate,
            EndDate = schoolYear.EndDate,
            IsActive = schoolYear.IsActive,
            ClassCount = 0
        }, SystemMessages.MSG08);
    }

    public async Task<ApiResponse<List<SchoolYearDto>>> GetSchoolYearsAsync(CancellationToken cancellationToken = default)
    {
        var years = await _unitOfWork.Classes.GetAllSchoolYearsAsync(cancellationToken);
        var result = years.Select(y => new SchoolYearDto
        {
            Id = y.Id,
            YearName = y.YearName,
            StartDate = y.StartDate,
            EndDate = y.EndDate,
            IsActive = y.IsActive,
            ClassCount = y.Classes.Count
        }).ToList();

        return ApiResponse<List<SchoolYearDto>>.Ok(result);
    }

    public async Task<ApiResponse<SchoolYearDto>> GetActiveSchoolYearAsync(CancellationToken cancellationToken = default)
    {
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

        var allYears = await _unitOfWork.Classes.GetAllSchoolYearsAsync(cancellationToken);
        foreach (var sy in allYears)
        {
            sy.IsActive = (sy.Id == schoolYearId);
            await _unitOfWork.Repository<SchoolYear>().UpdateAsync(sy, cancellationToken);
        }

        await _unitOfWork.SaveChangesAsync(cancellationToken);

        await _auditService.LogActionAsync(
            actorUserId,
            "ActivateSchoolYear",
            nameof(SchoolYear),
            schoolYearId.ToString(),
            null,
            $"Activated Year: {target.YearName}",
            null,
            cancellationToken);

        return ApiResponse<bool>.Ok(true, SystemMessages.MSG08);
    }

    #endregion

    #region Classes Management

    public async Task<ApiResponse<ClassSummaryDto>> CreateClassAsync(CreateClassDto request, int? actorUserId = null, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(request.ClassName) || request.Grade < 1 || request.Grade > 5 || request.Capacity <= 0)
        {
            return ApiResponse<ClassSummaryDto>.Fail(SystemMessages.MSG01, new List<string> { "Khối học phải từ 1 đến 5 và sĩ số tối đa phải lớn hơn 0." });
        }

        var schoolYear = await _unitOfWork.Repository<SchoolYear>().GetByIdAsync(request.SchoolYearId, cancellationToken);
        if (schoolYear == null)
        {
            return ApiResponse<ClassSummaryDto>.Fail(SystemMessages.MSG01, new List<string> { "Năm học không hợp lệ." });
        }

        var existingClasses = await _unitOfWork.Classes.GetClassesBySchoolYearAsync(request.SchoolYearId, null, null, cancellationToken);
        if (existingClasses.Any(c => c.ClassName.Equals(request.ClassName.Trim(), StringComparison.OrdinalIgnoreCase)))
        {
            return ApiResponse<ClassSummaryDto>.Fail(SystemMessages.MSG01, new List<string> { $"Lớp {request.ClassName} đã tồn tại trong năm học {schoolYear.YearName}." });
        }

        var newClass = new Class
        {
            SchoolYearId = request.SchoolYearId,
            ClassName = request.ClassName.Trim(),
            Grade = request.Grade,
            RoomNumber = request.RoomNumber?.Trim(),
            Capacity = request.Capacity,
            Status = "Active",
            IsPlacementPublished = false
        };

        await _unitOfWork.Classes.AddAsync(newClass, cancellationToken);
        await _unitOfWork.SaveChangesAsync(cancellationToken);

        await _auditService.LogActionAsync(
            actorUserId,
            "CreateClass",
            nameof(Class),
            newClass.Id.ToString(),
            null,
            $"Class: {newClass.ClassName}, Grade: {newClass.Grade}, Capacity: {newClass.Capacity}",
            null,
            cancellationToken);

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
            IsPlacementPublished = newClass.IsPlacementPublished
        }, SystemMessages.MSG08);
    }

    public async Task<ApiResponse<List<ClassSummaryDto>>> InitializeBatchClassesAsync(InitializeBatchClassesDto request, int? actorUserId = null, CancellationToken cancellationToken = default)
    {
        SchoolYear? schoolYear = null;
        if (request.SchoolYearId > 0)
        {
            schoolYear = await _unitOfWork.Repository<SchoolYear>().GetByIdAsync(request.SchoolYearId, cancellationToken);
        }
        if (schoolYear == null)
        {
            schoolYear = await _unitOfWork.Classes.GetActiveSchoolYearAsync(cancellationToken);
        }
        if (schoolYear == null)
        {
            return ApiResponse<List<ClassSummaryDto>>.Fail(SystemMessages.MSG01, new List<string> { "Không tìm thấy năm học hợp lệ." });
        }

        var targetSchoolYearId = schoolYear.Id;
        var existingClasses = await _unitOfWork.Classes.GetClassesBySchoolYearAsync(targetSchoolYearId, null, null, cancellationToken);
        var existingDict = existingClasses.ToDictionary(c => c.ClassName, StringComparer.OrdinalIgnoreCase);

        // Resolve grades: Nếu có TargetGrade thì chỉ tạo/cập nhật cho khối đó
        List<int> grades;
        if (request.TargetGrade.HasValue && request.TargetGrade.Value >= 1 && request.TargetGrade.Value <= 5)
        {
            grades = new List<int> { request.TargetGrade.Value };
        }
        else if (request.Grades != null && request.Grades.Count > 0)
        {
            grades = request.Grades.Where(g => g >= 1 && g <= 5).Distinct().ToList();
        }
        else
        {
            grades = new List<int> { 1, 2, 3, 4, 5 };
        }

        // Resolve section suffixes (phân lớp: A, B, C, D, E...)
        var rawSuffixes = (request.SectionNames != null && request.SectionNames.Count > 0)
            ? request.SectionNames
            : (request.SectionSuffixes != null && request.SectionSuffixes.Count > 0 ? request.SectionSuffixes : new List<string> { "A", "B", "C" });

        var suffixes = rawSuffixes
            .Select(s => s.Trim().ToUpperInvariant())
            .Where(s => !string.IsNullOrEmpty(s))
            .Distinct()
            .ToList();

        // Resolve capacity (Sĩ số tối đa)
        var capacity = request.CapacityPerClass ?? (request.DefaultCapacity > 0 ? request.DefaultCapacity : 35);

        var affectedClasses = new List<ClassSummaryDto>();
        int createdCount = 0;
        int updatedCount = 0;

        foreach (var grade in grades)
        {
            foreach (var suffix in suffixes)
            {
                var className = $"{grade}{suffix}";

                if (existingDict.TryGetValue(className, out var existingClass))
                {
                    // Lớp đã tồn tại: Cập nhật sĩ số tối đa (Capacity) nếu có thay đổi
                    if (existingClass.Capacity != capacity)
                    {
                        existingClass.Capacity = capacity;
                        await _unitOfWork.Classes.UpdateAsync(existingClass, cancellationToken);
                        updatedCount++;
                    }

                    affectedClasses.Add(new ClassSummaryDto
                    {
                        Id = existingClass.Id,
                        SchoolYearId = existingClass.SchoolYearId,
                        SchoolYearName = schoolYear.YearName,
                        ClassName = existingClass.ClassName,
                        Grade = existingClass.Grade,
                        RoomNumber = existingClass.RoomNumber,
                        Capacity = existingClass.Capacity,
                        EnrolledCount = existingClass.ClassAssignments?.Count(a => a.IsActive) ?? 0,
                        Status = existingClass.Status,
                        HomeroomTeacherId = existingClass.HomeroomTeacherId,
                        HomeroomTeacherName = existingClass.HomeroomTeacher?.User?.FullName,
                        HomeroomTeacherStaffCode = existingClass.HomeroomTeacher?.StaffCode
                    });
                }
                else
                {
                    // Lớp chưa tồn tại: Tạo mới vào danh sách lớp mục tiêu
                    var newClass = new Class
                    {
                        SchoolYearId = targetSchoolYearId,
                        ClassName = className,
                        Grade = grade,
                        RoomNumber = $"Phòng {grade}0{suffix}",
                        Capacity = capacity,
                        Status = "Active",
                        IsPlacementPublished = false
                    };

                    await _unitOfWork.Classes.AddAsync(newClass, cancellationToken);
                    existingDict[className] = newClass;
                    createdCount++;

                    affectedClasses.Add(new ClassSummaryDto
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
                        IsPlacementPublished = newClass.IsPlacementPublished
                    });
                }
            }
        }

        await _unitOfWork.SaveChangesAsync(cancellationToken);

        await _auditService.LogActionAsync(
            actorUserId,
            "InitializeBatchClasses",
            nameof(Class),
            null,
            null,
            $"Khởi tạo/cập nhật {affectedClasses.Count} lớp (Tạo mới: {createdCount}, Cập nhật: {updatedCount}) cho năm học {schoolYear.YearName}",
            null,
            cancellationToken);

        var message = createdCount > 0 || updatedCount > 0
            ? $"Đã xử lý danh mục lớp học: Tạo mới {createdCount} lớp, Cập nhật sĩ số {updatedCount} lớp."
            : "Danh mục lớp học đã được đồng bộ đầy đủ.";

        return ApiResponse<List<ClassSummaryDto>>.Ok(affectedClasses, message);
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
            var activeYear = await _unitOfWork.Classes.GetActiveSchoolYearAsync(cancellationToken);
            if (activeYear == null)
            {
                return ApiResponse<List<ClassSummaryDto>>.Fail(SystemMessages.MSG14);
            }
            targetYearId = activeYear.Id;
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
            EnrolledCount = c.ClassAssignments?.Count(ca => ca.IsActive) ?? 0,
            Status = c.Status,
            IsPlacementPublished = c.IsPlacementPublished,
            HomeroomTeacherId = c.HomeroomTeacherId,
            HomeroomTeacherName = c.HomeroomTeacher?.User?.FullName,
            HomeroomTeacherStaffCode = c.HomeroomTeacher?.StaffCode
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
            EnrolledCount = c.ClassAssignments?.Count(ca => ca.IsActive) ?? 0,
            Status = c.Status,
            IsPlacementPublished = c.IsPlacementPublished,
            HomeroomTeacherId = c.HomeroomTeacherId,
            HomeroomTeacherName = c.HomeroomTeacher?.User?.FullName,
            HomeroomTeacherStaffCode = c.HomeroomTeacher?.StaffCode
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

        var enrolledCount = c.ClassAssignments?.Count(ca => ca.IsActive) ?? 0;
        if (request.Capacity < enrolledCount)
        {
            return ApiResponse<ClassSummaryDto>.Fail(SystemMessages.MSG15, new List<string> { $"Không thể giảm sĩ số tối đa ({request.Capacity}) nhỏ hơn số học sinh hiện tại đang có ({enrolledCount})." });
        }

        var oldInfo = $"Name: {c.ClassName}, Capacity: {c.Capacity}, Status: {c.Status}";

        c.ClassName = request.ClassName.Trim();
        c.Grade = request.Grade;
        c.RoomNumber = request.RoomNumber?.Trim();
        c.Capacity = request.Capacity;
        c.Status = request.Status;

        await _unitOfWork.Classes.UpdateAsync(c, cancellationToken);
        await _unitOfWork.SaveChangesAsync(cancellationToken);

        await _auditService.LogActionAsync(
            actorUserId,
            "UpdateClass",
            nameof(Class),
            c.Id.ToString(),
            oldInfo,
            $"Name: {c.ClassName}, Capacity: {c.Capacity}, Status: {c.Status}",
            null,
            cancellationToken);

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
            IsPlacementPublished = c.IsPlacementPublished,
            HomeroomTeacherId = c.HomeroomTeacherId,
            HomeroomTeacherName = c.HomeroomTeacher?.User?.FullName,
            HomeroomTeacherStaffCode = c.HomeroomTeacher?.StaffCode
        }, SystemMessages.MSG08);
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
            var activeYear = await _unitOfWork.Classes.GetActiveSchoolYearAsync(cancellationToken);
            if (activeYear == null)
            {
                return ApiResponse<List<TeacherOptionDto>>.Fail(SystemMessages.MSG14);
            }
            targetYearId = activeYear.Id;
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

        var teacherProfile = await _unitOfWork.Repository<StaffProfile>().GetByIdAsync(request.StaffProfileId, cancellationToken);
        if (teacherProfile == null)
        {
            return ApiResponse<bool>.Fail(SystemMessages.MSG01, new List<string> { "Không tìm thấy hồ sơ giáo viên." });
        }

        // BR-16: A class may have at most one active primary Teacher assignment for the same effective period.
        // Check if this teacher is already assigned to another class in this school year
        var hasConflict = await _unitOfWork.Classes.IsTeacherAssignedToActiveHomeroomAsync(
            request.StaffProfileId,
            targetClass.SchoolYearId,
            classId,
            cancellationToken);

        if (hasConflict)
        {
            return ApiResponse<bool>.Fail(SystemMessages.MSG01, new List<string> { "Giáo viên này đã được phân công chủ nhiệm cho một lớp khác trong cùng năm học." });
        }

        var priorTeacherId = targetClass.HomeroomTeacherId;
        targetClass.HomeroomTeacherId = request.StaffProfileId;
        teacherProfile.IsHomeroomTeacher = true;

        await _unitOfWork.Classes.UpdateAsync(targetClass, cancellationToken);
        await _unitOfWork.Repository<StaffProfile>().UpdateAsync(teacherProfile, cancellationToken);
        await _unitOfWork.SaveChangesAsync(cancellationToken);

        // Notify teacher of homeroom assignment (BR-42)
        var notification = new Notification
        {
            UserId = teacherProfile.UserId,
            Title = "Phân công giáo viên chủ nhiệm",
            Content = $"Bạn đã được phân công làm giáo viên chủ nhiệm lớp {targetClass.ClassName} năm học {targetClass.SchoolYear?.YearName}.",
            LinkUrl = $"/teacher/classes/{targetClass.Id}",
            CreatedAt = DateTime.UtcNow,
            IsRead = false
        };
        await _unitOfWork.Repository<Notification>().AddAsync(notification, cancellationToken);
        await _unitOfWork.SaveChangesAsync(cancellationToken);

        await _auditService.LogActionAsync(
            actorUserId,
            "AssignHomeroomTeacher",
            nameof(Class),
            targetClass.Id.ToString(),
            $"PriorTeacherId: {priorTeacherId}",
            $"NewTeacherProfileId: {request.StaffProfileId}",
            null,
            cancellationToken);

        return ApiResponse<bool>.Ok(true, SystemMessages.MSG08);
    }

    #endregion

    #region Student Placement / Class Assignment (UC-16)

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
                PrimaryGuardianRelationship = s.StudentGuardians.FirstOrDefault(sg => sg.IsPrimaryContact)?.Relationship.ToString()
            };
        }).ToList();

        return ApiResponse<List<RosterStudentItemDto>>.Ok(result);
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
        var currentEnrolled = targetClass.ClassAssignments.Count(ca => ca.IsActive);
        if (currentEnrolled + request.StudentIds.Count > targetClass.Capacity)
        {
            return ApiResponse<bool>.Fail(SystemMessages.MSG15, new List<string> { $"Lớp {targetClass.ClassName} đã đạt hoặc vượt quá sĩ số tối đa ({targetClass.Capacity}). Hiện có {currentEnrolled} học sinh." });
        }

        // Check if any student already has an active class assignment for this school year (BR-17)
        var activeAssignmentsInYear = await _unitOfWork.Repository<ClassAssignment>()
            .FindAsync(ca => ca.IsActive && ca.Class.SchoolYearId == targetClass.SchoolYearId && request.StudentIds.Contains(ca.StudentId), cancellationToken);

        if (activeAssignmentsInYear.Any())
        {
            return ApiResponse<bool>.Fail(SystemMessages.MSG15, new List<string> { "Một hoặc nhiều học sinh đã được phân vào lớp khác trong năm học này." });
        }

        var academicYearName = targetClass.SchoolYear?.YearName ?? DateTime.UtcNow.Year.ToString();

        foreach (var studentId in request.StudentIds.Distinct())
        {
            var assignment = new ClassAssignment
            {
                ClassId = classId,
                StudentId = studentId,
                AcademicYear = academicYearName,
                StartDate = DateTime.UtcNow,
                IsActive = true
            };
            await _unitOfWork.Repository<ClassAssignment>().AddAsync(assignment, cancellationToken);
        }

        await _unitOfWork.SaveChangesAsync(cancellationToken);

        await _auditService.LogActionAsync(
            actorUserId,
            "AssignStudentsToClass",
            nameof(ClassAssignment),
            classId.ToString(),
            null,
            $"Assigned {request.StudentIds.Count} students to Class {targetClass.ClassName}",
            null,
            cancellationToken);

        return ApiResponse<bool>.Ok(true, SystemMessages.MSG16);
    }

    public async Task<ApiResponse<bool>> RemoveStudentFromClassAsync(int classId, int studentId, int? actorUserId = null, CancellationToken cancellationToken = default)
    {
        var assignment = (await _unitOfWork.Repository<ClassAssignment>()
            .FindAsync(ca => ca.ClassId == classId && ca.StudentId == studentId && ca.IsActive, cancellationToken))
            .FirstOrDefault();

        if (assignment == null)
        {
            return ApiResponse<bool>.Fail(SystemMessages.MSG01, new List<string> { "Học sinh không có trong danh sách lớp này." });
        }

        // BR-18: Class reassignment/transfer must preserve prior class-assignment history
        assignment.IsActive = false;
        assignment.EndDate = DateTime.UtcNow;

        await _unitOfWork.Repository<ClassAssignment>().UpdateAsync(assignment, cancellationToken);
        await _unitOfWork.SaveChangesAsync(cancellationToken);

        await _auditService.LogActionAsync(
            actorUserId,
            "RemoveStudentFromClass",
            nameof(ClassAssignment),
            assignment.Id.ToString(),
            $"ClassId: {classId}, StudentId: {studentId}",
            "Deactivated assignment",
            null,
            cancellationToken);

        return ApiResponse<bool>.Ok(true, SystemMessages.MSG08);
    }

    #endregion

    #region Placement Publishing

    public async Task<ApiResponse<PublishPlacementResultDto>> PublishPlacementAsync(int classId, int? actorUserId = null, CancellationToken cancellationToken = default)
    {
        var targetClass = await _unitOfWork.Classes.GetClassWithDetailsAsync(classId, cancellationToken);
        if (targetClass == null)
        {
            return ApiResponse<PublishPlacementResultDto>.Fail(SystemMessages.MSG01, new List<string> { "Không tìm thấy lớp học." });
        }

        var enrolledCount = targetClass.ClassAssignments.Count(ca => ca.IsActive);
        if (enrolledCount == 0)
        {
            return ApiResponse<PublishPlacementResultDto>.Fail(
                SystemMessages.MSG01,
                new List<string> { $"Lớp {targetClass.ClassName} chưa có học sinh nào. Vui lòng lập và duyệt phương án xếp lớp trước khi công bố." }
            );
        }

        targetClass.IsPlacementPublished = true;
        targetClass.Status = "Active";

        await _unitOfWork.Classes.UpdateAsync(targetClass, cancellationToken);
        await _unitOfWork.SaveChangesAsync(cancellationToken);

        // Notify homeroom teacher if designated (BR-42)
        if (targetClass.HomeroomTeacher != null)
        {
            var notif = new Notification
            {
                UserId = targetClass.HomeroomTeacher.UserId,
                Title = "Danh sách phân lớp đã được công bố",
                Content = $"Danh sách học sinh lớp {targetClass.ClassName} ({enrolledCount} học sinh) đã được Giáo vụ công bố chính thức. Thầy/Cô có thể vào xem danh sách lớp.",
                LinkUrl = $"/teacher/classes/{targetClass.Id}/roster",
                CreatedAt = DateTime.UtcNow,
                IsRead = false
            };
            await _unitOfWork.Repository<Notification>().AddAsync(notif, cancellationToken);
            await _unitOfWork.SaveChangesAsync(cancellationToken);
        }

        await _auditService.LogActionAsync(
            actorUserId,
            "PublishPlacement",
            nameof(Class),
            targetClass.Id.ToString(),
            null,
            $"Published placement for Class {targetClass.ClassName} with {enrolledCount} students",
            null,
            cancellationToken);

        var result = new PublishPlacementResultDto
        {
            ClassId = targetClass.Id,
            ClassName = targetClass.ClassName,
            EnrolledCount = enrolledCount,
            HomeroomTeacherName = targetClass.HomeroomTeacher?.User?.FullName,
            PublishedAt = DateTime.UtcNow,
            Message = $"Đã công bố danh sách phân lớp thành công cho lớp {targetClass.ClassName} ({enrolledCount} học sinh)."
        };

        return ApiResponse<PublishPlacementResultDto>.Ok(result, SystemMessages.MSG08);
    }

    public async Task<ApiResponse<List<PublishPlacementResultDto>>> PublishAllPlacementsAsync(int schoolYearId, int? actorUserId = null, CancellationToken cancellationToken = default)
    {
        var classes = await _unitOfWork.Classes.GetClassesBySchoolYearAsync(schoolYearId, null, null, cancellationToken);
        var eligibleClasses = classes.Where(c => (c.ClassAssignments?.Count(ca => ca.IsActive) ?? 0) > 0).ToList();

        if (!eligibleClasses.Any())
        {
            return ApiResponse<List<PublishPlacementResultDto>>.Fail(
                SystemMessages.MSG01,
                new List<string> { "Chưa có lớp nào có học sinh để công bố. Vui lòng lập và duyệt phương án xếp lớp trước khi công bố toàn trường." }
            );
        }

        var results = new List<PublishPlacementResultDto>();

        foreach (var c in eligibleClasses)
        {
            c.IsPlacementPublished = true;
            c.Status = "Active";
            await _unitOfWork.Classes.UpdateAsync(c, cancellationToken);

            var count = c.ClassAssignments?.Count(ca => ca.IsActive) ?? 0;

            if (c.HomeroomTeacher != null)
            {
                var notif = new Notification
                {
                    UserId = c.HomeroomTeacher.UserId,
                    Title = "Công bố danh sách lớp học",
                    Content = $"Danh sách học sinh lớp {c.ClassName} năm học {c.SchoolYear?.YearName} đã được công bố.",
                    LinkUrl = $"/teacher/classes/{c.Id}/roster",
                    CreatedAt = DateTime.UtcNow,
                    IsRead = false
                };
                await _unitOfWork.Repository<Notification>().AddAsync(notif, cancellationToken);
            }

            results.Add(new PublishPlacementResultDto
            {
                ClassId = c.Id,
                ClassName = c.ClassName,
                EnrolledCount = count,
                HomeroomTeacherName = c.HomeroomTeacher?.User?.FullName,
                PublishedAt = DateTime.UtcNow,
                Message = $"Đã công bố lớp {c.ClassName}"
            });
        }

        await _unitOfWork.SaveChangesAsync(cancellationToken);

        await _auditService.LogActionAsync(
            actorUserId,
            "PublishAllPlacements",
            nameof(SchoolYear),
            schoolYearId.ToString(),
            null,
            $"Published {results.Count} class placements for SchoolYearId {schoolYearId}",
            null,
            cancellationToken);

        return ApiResponse<List<PublishPlacementResultDto>>.Ok(results, SystemMessages.MSG08);
    }

    #endregion

    #region Class Roster (UC-17)

    public async Task<ApiResponse<ClassRosterDto>> GetClassRosterAsync(int classId, int? requestingUserId = null, string? requestingUserRole = null, CancellationToken cancellationToken = default)
    {
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

        var activeAssignments = targetClass.ClassAssignments.Where(ca => ca.IsActive).ToList();

        var roster = new ClassRosterDto
        {
            ClassId = targetClass.Id,
            ClassName = targetClass.ClassName,
            Grade = targetClass.Grade,
            AcademicYear = targetClass.SchoolYear?.YearName ?? string.Empty,
            RoomNumber = targetClass.RoomNumber,
            Capacity = targetClass.Capacity,
            TotalEnrolled = activeAssignments.Count,
            IsPlacementPublished = targetClass.IsPlacementPublished,
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
                    PrimaryGuardianRelationship = s.StudentGuardians.FirstOrDefault(sg => sg.IsPrimaryContact)?.Relationship.ToString()
                };
            }).OrderBy(s => s.FullName).ToList()
        };

        return ApiResponse<ClassRosterDto>.Ok(roster);
    }

    public async Task<ApiResponse<ClassRosterDto>> GetMyHomeroomClassAsync(int teacherUserId, CancellationToken cancellationToken = default)
    {
        var homeroomClass = await _unitOfWork.Classes.GetHomeroomClassByTeacherUserIdAsync(teacherUserId, null, cancellationToken);
        if (homeroomClass == null)
        {
            return ApiResponse<ClassRosterDto>.Fail(SystemMessages.MSG43); // MSG43: Không có dữ liệu phù hợp để hiển thị.
        }

        return await GetClassRosterAsync(homeroomClass.Id, teacherUserId, UserRoles.Teacher.ToString(), cancellationToken);
    }

    #endregion
}
