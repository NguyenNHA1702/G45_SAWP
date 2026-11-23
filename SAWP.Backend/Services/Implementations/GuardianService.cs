using Core.Constants;
using Core.DTOs;
using Core.Entities;
using Core.Enums;
using Core.Interfaces;
using Microsoft.EntityFrameworkCore;
using Services.Interfaces;

namespace Services.Implementations;

public class GuardianService : IGuardianService
{
    private readonly IUnitOfWork _unitOfWork;

    public GuardianService(IUnitOfWork unitOfWork)
    {
        _unitOfWork = unitOfWork;
    }

    public async Task<ApiResponse<List<LinkedChildSummaryDto>>> GetMyLinkedChildrenAsync(int parentUserId, CancellationToken cancellationToken = default)
    {
        var linkedStudents = await _unitOfWork.Students.GetLinkedStudentsByParentUserIdAsync(parentUserId, cancellationToken);

        var guardian = (await _unitOfWork.Repository<Guardian>().FindAsync(g => g.UserId == parentUserId, cancellationToken)).FirstOrDefault();
        int guardianId = guardian?.Id ?? 0;

        var result = linkedStudents.Select(s =>
        {
            var activeAssignment = s.ClassAssignments.FirstOrDefault(ca => ca.IsActive);
            var myLink = s.StudentGuardians.FirstOrDefault(sg => sg.GuardianId == guardianId);

            return new LinkedChildSummaryDto
            {
                StudentId = s.Id,
                StudentCode = s.StudentCode,
                FullName = s.FullName,
                DateOfBirth = s.DateOfBirth,
                Gender = s.Gender,
                AvatarUrl = s.AvatarUrl,
                Status = s.Status,
                CurrentClassId = activeAssignment?.ClassId,
                CurrentClassName = activeAssignment?.Class?.ClassName,
                Grade = activeAssignment?.Class?.Grade,
                IsPlacementPublished = activeAssignment?.Class?.IsPlacementPublished ?? false,
                RelationshipName = myLink?.Relationship.ToVietnamese() ?? "Phụ huynh",
                IsPrimaryContact = myLink?.IsPrimaryContact ?? false
            };
        }).ToList();

        return ApiResponse<List<LinkedChildSummaryDto>>.Ok(result);
    }

    public async Task<ApiResponse<ChildDashboardDto>> GetChildDashboardAsync(
        int parentUserId,
        int studentId,
        CancellationToken cancellationToken = default)
    {
        // BR-06, BR-34, BR-47: Security check - Verify parent is linked to this child
        var guardian = (await _unitOfWork.Repository<Guardian>().FindAsync(g => g.UserId == parentUserId, cancellationToken)).FirstOrDefault();
        if (guardian == null)
        {
            return ApiResponse<ChildDashboardDto>.Fail(SystemMessages.MSG10);
        }

        var isLinked = await _unitOfWork.Repository<StudentGuardian>()
            .FindAsync(sg => sg.StudentId == studentId && sg.GuardianId == guardian.Id, cancellationToken);

        if (!isLinked.Any())
        {
            return ApiResponse<ChildDashboardDto>.Fail(SystemMessages.MSG10);
        }

        var student = await _unitOfWork.Students.GetStudentWithProfileAsync(studentId, cancellationToken);
        if (student == null)
        {
            return ApiResponse<ChildDashboardDto>.Fail(SystemMessages.MSG11);
        }

        var activeAssignment = student.ClassAssignments.FirstOrDefault(ca => ca.IsActive);
        var activeClass = activeAssignment?.Class;

        ClassSummaryDto? classInfoDto = null;
        HomeroomTeacherContactDto? teacherContactDto = null;

        if (activeClass != null)
        {
            classInfoDto = new ClassSummaryDto
            {
                Id = activeClass.Id,
                SchoolYearId = activeClass.SchoolYearId,
                SchoolYearName = activeClass.SchoolYear?.YearName ?? string.Empty,
                ClassName = activeClass.ClassName,
                Grade = activeClass.Grade,
                RoomNumber = activeClass.RoomNumber,
                Capacity = activeClass.Capacity,
                Status = activeClass.Status,
                IsPlacementPublished = activeClass.IsPlacementPublished,
                HomeroomTeacherId = activeClass.HomeroomTeacherId,
                HomeroomTeacherName = activeClass.HomeroomTeacher?.User?.FullName,
                HomeroomTeacherStaffCode = activeClass.HomeroomTeacher?.StaffCode
            };

            if (activeClass.HomeroomTeacher?.User != null)
            {
                teacherContactDto = new HomeroomTeacherContactDto
                {
                    TeacherName = activeClass.HomeroomTeacher.User.FullName,
                    PhoneNumber = activeClass.HomeroomTeacher.User.PhoneNumber,
                    Email = activeClass.HomeroomTeacher.User.Email,
                    StaffCode = activeClass.HomeroomTeacher.StaffCode
                };
            }
        }

        // Attendance Mini Summary
        var attendanceRecords = await _unitOfWork.Repository<AttendanceRecord>()
            .FindAsync(ar => ar.StudentId == studentId, cancellationToken);

        int presentCount = attendanceRecords.Count(a => a.Status == AttendanceStatus.Present);
        int excusedCount = attendanceRecords.Count(a => a.Status == AttendanceStatus.ExcusedAbsence);
        int unexcusedCount = attendanceRecords.Count(a => a.Status == AttendanceStatus.UnexcusedAbsence);
        int lateCount = attendanceRecords.Count(a => a.Status == AttendanceStatus.Late);
        int totalDays = attendanceRecords.Count;
        double rate = totalDays > 0 ? Math.Round((double)(presentCount + lateCount) / totalDays * 100, 1) : 100.0;

        var attendanceSummary = new AttendanceMiniSummaryDto
        {
            TotalTrackedDays = totalDays,
            PresentDays = presentCount,
            ExcusedAbsenceDays = excusedCount,
            UnexcusedAbsenceDays = unexcusedCount,
            LateDays = lateCount,
            AttendanceRatePercentage = rate
        };

        // Today's Lunch Status
        var today = DateTime.UtcNow.Date;
        var lunchReg = await _unitOfWork.Lunch.GetStudentRegistrationForDateAsync(studentId, today, cancellationToken);
        var lunchServiceDay = (await _unitOfWork.Repository<LunchServiceDay>().FindAsync(ls => ls.Date.Date == today, cancellationToken)).FirstOrDefault();

        var lunchStatus = new LunchStatusTodayDto
        {
            IsServiceAvailable = lunchServiceDay != null && lunchServiceDay.IsServing,
            IsRegistered = lunchReg != null && lunchReg.IsActive,
            MealType = lunchReg?.MealType.ToString(),
            CutoffTimeMessage = lunchServiceDay != null ? $"Hạn đăng ký/hủy: {lunchServiceDay.CutoffTime:hh\\:mm}" : "Không có lịch phục vụ"
        };

        var studentProfileDto = new StudentProfileDto
        {
            Id = student.Id,
            StudentCode = student.StudentCode,
            FullName = student.FullName,
            DateOfBirth = student.DateOfBirth,
            Gender = student.Gender,
            EnrollmentDate = student.EnrollmentDate,
            Status = student.Status,
            AvatarUrl = student.AvatarUrl,
            CurrentClassId = activeClass?.Id,
            CurrentClassName = activeClass?.ClassName,
            CurrentGrade = activeClass?.Grade,
            CurrentRoomNumber = activeClass?.RoomNumber,
            HomeroomTeacherName = activeClass?.HomeroomTeacher?.User?.FullName,
            CreatedAt = student.CreatedAt,
            Guardians = student.StudentGuardians.Select(sg => new StudentGuardianItemDto
            {
                GuardianId = sg.GuardianId,
                UserId = sg.Guardian.UserId,
                FullName = sg.Guardian.FullName,
                PhoneNumber = sg.Guardian.PhoneNumber,
                Email = sg.Guardian.Email,
                IdentityCardNumber = sg.Guardian.IdentityCardNumber,
                Occupation = sg.Guardian.Occupation,
                Address = sg.Guardian.Address,
                Relationship = (int)sg.Relationship,
                RelationshipName = sg.Relationship.ToVietnamese(),
                IsPrimaryContact = sg.IsPrimaryContact
            }).ToList()
        };

        var dashboard = new ChildDashboardDto
        {
            Student = studentProfileDto,
            ClassInfo = classInfoDto,
            HomeroomTeacher = teacherContactDto,
            AttendanceSummary = attendanceSummary,
            LunchStatusToday = lunchStatus,
            Guardians = studentProfileDto.Guardians
        };

        return ApiResponse<ChildDashboardDto>.Ok(dashboard);
    }
}
