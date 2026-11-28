using Core.Common;
using Core.Constants;
using Core.DTOs;
using Core.Entities;
using Core.Enums;
using Core.Interfaces;
using Microsoft.EntityFrameworkCore;
using Services.Interfaces;

namespace Services.Implementations;

public class StudentService : IStudentService
{
    private readonly IUnitOfWork _unitOfWork;
    private readonly IAuditService _auditService;

    public StudentService(IUnitOfWork unitOfWork, IAuditService auditService)
    {
        _unitOfWork = unitOfWork;
        _auditService = auditService;
    }

    public async Task<ApiResponse<StudentProfileDto>> GetStudentProfileAsync(
        int studentId,
        int? requestingUserId = null,
        string? requestingUserRole = null,
        CancellationToken cancellationToken = default)
    {
        var isDeniedRole = string.Equals(requestingUserRole, UserRoles.ServiceStaff.ToString(), StringComparison.OrdinalIgnoreCase)
            || string.Equals(requestingUserRole, UserRoles.Admin.ToString(), StringComparison.OrdinalIgnoreCase);
        if (isDeniedRole)
        {
            return ApiResponse<StudentProfileDto>.Fail(SystemMessages.MSG10);
        }

        var fastLookup = await _unitOfWork.Students.GetStudentProfileDtoAsync(studentId, cancellationToken);
        if (fastLookup != null)
        {
            if (!string.IsNullOrEmpty(requestingUserRole) && requestingUserId.HasValue)
            {
                if (string.Equals(requestingUserRole, UserRoles.Parent.ToString(), StringComparison.OrdinalIgnoreCase))
                {
                    if (!fastLookup.GuardianUserIds.Contains(requestingUserId.Value))
                    {
                        return ApiResponse<StudentProfileDto>.Fail(SystemMessages.MSG10);
                    }
                }
                else if (string.Equals(requestingUserRole, UserRoles.Teacher.ToString(), StringComparison.OrdinalIgnoreCase))
                {
                    if (!fastLookup.HomeroomTeacherUserIds.Contains(requestingUserId.Value))
                    {
                        return ApiResponse<StudentProfileDto>.Fail(SystemMessages.MSG10);
                    }
                }
            }

            return ApiResponse<StudentProfileDto>.Ok(fastLookup.Profile);
        }

        var student = await _unitOfWork.Students.GetStudentWithProfileAsync(studentId, cancellationToken);
        if (student == null)
        {
            return ApiResponse<StudentProfileDto>.Fail(SystemMessages.MSG11);
        }

        // BR-06, BR-07, BR-51: Data Scope Authorization
        if (!string.IsNullOrEmpty(requestingUserRole) && requestingUserId.HasValue)
        {
            if (string.Equals(requestingUserRole, UserRoles.Parent.ToString(), StringComparison.OrdinalIgnoreCase))
            {
                var isLinked = student.StudentGuardians.Any(sg => sg.Guardian.UserId == requestingUserId.Value);
                if (!isLinked)
                {
                    return ApiResponse<StudentProfileDto>.Fail(SystemMessages.MSG10);
                }
            }
            else if (string.Equals(requestingUserRole, UserRoles.Teacher.ToString(), StringComparison.OrdinalIgnoreCase))
            {
                var isHomeroom = student.ClassAssignments
                    .Any(ca => ca.IsActive && ca.Class.HomeroomTeacher != null && ca.Class.HomeroomTeacher.UserId == requestingUserId.Value);
                if (!isHomeroom)
                {
                    return ApiResponse<StudentProfileDto>.Fail(SystemMessages.MSG10);
                }
            }
        }

        var profileDto = MapToStudentProfileDto(student);
        return ApiResponse<StudentProfileDto>.Ok(profileDto);
    }

    public async Task<ApiResponse<PaginatedResult<StudentProfileDto>>> GetStudentsPagedAsync(
        StudentFilterDto filter,
        CancellationToken cancellationToken = default)
    {
        var baseQuery = _unitOfWork.Repository<Student>()
            .Query()
            .AsNoTracking();

        var searchTerm = !string.IsNullOrWhiteSpace(filter.SearchTerm) ? filter.SearchTerm : filter.Keyword;
        if (!string.IsNullOrWhiteSpace(searchTerm))
        {
            var term = System.Text.RegularExpressions.Regex.Replace(searchTerm.Trim(), @"\s+", " ").ToLower();
            baseQuery = baseQuery.Where(s => s.StudentCode.ToLower().Contains(term)
                || s.FullName.ToLower().Contains(term)
                || (s.ResidentialArea != null && s.ResidentialArea.ToLower().Contains(term))
                || s.StudentGuardians.Any(sg => sg.Guardian.FullName.ToLower().Contains(term)));
        }

        if (!string.IsNullOrWhiteSpace(filter.Status))
        {
            baseQuery = baseQuery.Where(s => s.Status == filter.Status);
        }

        if (filter.ClassId.HasValue)
        {
            baseQuery = baseQuery.Where(s => s.ClassAssignments.Any(ca => ca.IsActive && ca.ClassId == filter.ClassId.Value));
        }

        if (filter.Grade.HasValue)
        {
            baseQuery = baseQuery.Where(s => s.ClassAssignments.Any(ca => ca.IsActive && ca.Class.Grade == filter.Grade.Value));
        }

        if (filter.SchoolYearId.HasValue)
        {
            baseQuery = baseQuery.Where(s => s.ClassAssignments.Any(ca => ca.IsActive && ca.Class.SchoolYearId == filter.SchoolYearId.Value));
        }

        var pageIndex = filter.PageIndex <= 0 ? 1 : filter.PageIndex;
        var pageSize = filter.PageSize <= 0 ? 20 : filter.PageSize;

        var totalItems = await baseQuery.CountAsync(cancellationToken);
        if (totalItems == 0 || (pageIndex - 1) * pageSize >= totalItems)
        {
            return ApiResponse<PaginatedResult<StudentProfileDto>>.Ok(
                new PaginatedResult<StudentProfileDto>(new List<StudentProfileDto>(), totalItems, pageIndex, pageSize));
        }

        // Two-step pageIds fetch avoids 4x split-query re-evaluation and keeps sort grants minimal
        var pageIds = await baseQuery
            .OrderByDescending(s => s.CreatedAt)
            .ThenByDescending(s => s.Id)
            .Skip((pageIndex - 1) * pageSize)
            .Take(pageSize)
            .Select(s => s.Id)
            .ToListAsync(cancellationToken);

        var rows = await _unitOfWork.Repository<Student>()
            .Query()
            .AsNoTracking()
            .Where(s => pageIds.Contains(s.Id))
            .Select(s => new
            {
                s.Id,
                s.StudentCode,
                s.FullName,
                s.DateOfBirth,
                s.Gender,
                s.EnrollmentDate,
                s.Status,
                s.ResidentialArea,
                s.IsSpecialNeeds,
                s.SpecialNeedsNotes,
                s.CreatedAt,
                ActiveClass = s.ClassAssignments
                    .Where(ca => ca.IsActive && (ca.Class.SchoolYear == null || ca.Class.SchoolYear.IsActive))
                    .Select(ca => new
                    {
                        ca.Class.Id,
                        ca.Class.ClassName,
                        ca.Class.Grade,
                        ca.Class.RoomNumber,
                        HomeroomTeacherName = ca.Class.HomeroomTeacher != null && ca.Class.HomeroomTeacher.User != null
                            ? ca.Class.HomeroomTeacher.User.FullName
                            : null
                    })
                    .FirstOrDefault(),
                Guardians = s.StudentGuardians
                    .Select(sg => new
                    {
                        sg.GuardianId,
                        sg.Guardian.UserId,
                        sg.Guardian.FullName,
                        sg.Guardian.PhoneNumber,
                        sg.Guardian.Email,
                        sg.Guardian.IdentityCardNumber,
                        sg.Guardian.Occupation,
                        sg.Guardian.Address,
                        Relationship = (int)sg.Relationship,
                        RelationshipName = sg.Relationship.ToString(),
                        sg.IsPrimaryContact
                    })
                    .ToList()
            })
            .ToListAsync(cancellationToken);

        var orderMap = pageIds
            .Select((id, idx) => new { id, idx })
            .ToDictionary(x => x.id, x => x.idx);

        var dtoList = rows
            .OrderBy(r => orderMap[r.Id])
            .Select(r =>
            {
                var primaryGuardian = r.Guardians.FirstOrDefault(g => g.IsPrimaryContact) ?? r.Guardians.FirstOrDefault();
                return new StudentProfileDto
                {
                    Id = r.Id,
                    StudentCode = r.StudentCode,
                    FullName = r.FullName,
                    DateOfBirth = r.DateOfBirth,
                    Gender = r.Gender,
                    EnrollmentDate = r.EnrollmentDate,
                    Status = r.Status,
                    Address = r.ResidentialArea ?? primaryGuardian?.Address,
                    ResidentialArea = r.ResidentialArea,
                    IsSpecialNeeds = r.IsSpecialNeeds,
                    SpecialNeedsNotes = r.SpecialNeedsNotes,
                    CurrentClassId = r.ActiveClass?.Id,
                    CurrentClassName = r.ActiveClass?.ClassName,
                    CurrentGrade = r.ActiveClass?.Grade,
                    CurrentRoomNumber = r.ActiveClass?.RoomNumber,
                    HomeroomTeacherName = r.ActiveClass?.HomeroomTeacherName,
                    PrimaryGuardianName = primaryGuardian?.FullName,
                    PrimaryGuardianPhone = primaryGuardian?.PhoneNumber,
                    GuardiansCount = r.Guardians.Count,
                    CreatedAt = r.CreatedAt,
                    Guardians = r.Guardians.Select(g => new StudentGuardianItemDto
                    {
                        GuardianId = g.GuardianId,
                        UserId = g.UserId,
                        FullName = g.FullName,
                        PhoneNumber = g.PhoneNumber,
                        Email = g.Email,
                        IdentityCardNumber = g.IdentityCardNumber,
                        Occupation = g.Occupation,
                        Address = g.Address,
                        Relationship = g.Relationship,
                        RelationshipName = g.RelationshipName,
                        IsPrimaryContact = g.IsPrimaryContact
                    }).ToList()
                };
            })
            .ToList();

        var paginated = new PaginatedResult<StudentProfileDto>(dtoList, totalItems, pageIndex, pageSize);
        return ApiResponse<PaginatedResult<StudentProfileDto>>.Ok(paginated);
    }

    public async Task<ApiResponse<StudentProfileDto>> CreateStudentAsync(
        CreateStudentDto request,
        int? actorUserId = null,
        CancellationToken cancellationToken = default)
    {
        // 1. Validation
        if (string.IsNullOrWhiteSpace(request.StudentCode) || string.IsNullOrWhiteSpace(request.FullName))
        {
            return ApiResponse<StudentProfileDto>.Fail(SystemMessages.MSG01, new List<string> { "Mã học sinh và Họ tên là bắt buộc." });
        }

        if (!System.Text.RegularExpressions.Regex.IsMatch(request.StudentCode.Trim(), @"^[a-zA-Z0-9_\-]{3,30}$"))
        {
            return ApiResponse<StudentProfileDto>.Fail(
                SystemMessages.MSG01,
                new List<string> { "Mã học sinh chỉ được chứa chữ cái, chữ số, dấu gạch ngang (-) và gạch dưới (_) từ 3 đến 30 ký tự." });
        }

        var nameErr = StudentValidationHelper.ValidateFullName(request.FullName, "họ và tên học sinh");
        if (nameErr != null)
        {
            return ApiResponse<StudentProfileDto>.Fail(SystemMessages.MSG01, new List<string> { nameErr });
        }

        var validGenders = new[] { "Nam", "Nữ", "Male", "Female" };
        if (!string.IsNullOrWhiteSpace(request.Gender) && !validGenders.Contains(request.Gender.Trim(), StringComparer.OrdinalIgnoreCase))
        {
            return ApiResponse<StudentProfileDto>.Fail(
                SystemMessages.MSG01,
                new List<string> { "Giới tính không hợp lệ. Vui lòng chọn 'Nam' hoặc 'Nữ'." });
        }

        // 2. Duplicate Student Code Check (BR-09, MSG12)
        var exists = await _unitOfWork.Students.ExistsByStudentCodeAsync(request.StudentCode.Trim(), null, cancellationToken);
        if (exists)
        {
            return ApiResponse<StudentProfileDto>.Fail(SystemMessages.MSG12);
        }

        // 3. Date of birth & Age validation (BR-01, 5 to 15 years old + over-age exception)
        if (request.DateOfBirth == default || request.DateOfBirth.Year < 1900)
        {
            return ApiResponse<StudentProfileDto>.Fail(
                SystemMessages.MSG01,
                new List<string> { "Vui lòng nhập ngày sinh hợp lệ của học sinh." });
        }

        var dobErr = StudentValidationHelper.ValidateBirthDateAndAge(request.DateOfBirth, null, request.AllowAgeException);
        if (dobErr != null)
        {
            return ApiResponse<StudentProfileDto>.Fail(SystemMessages.MSG01, new List<string> { dobErr });
        }

        // Guardian checked before anything is saved: a conflicting identity card number / email stops the creation
        GuardianMatch? guardianMatch = null;
        if (request.InitialGuardian != null)
        {
            var hasGuardianContact = !string.IsNullOrWhiteSpace(request.InitialGuardian.PhoneNumber)
                || !string.IsNullOrWhiteSpace(request.InitialGuardian.Email)
                || !string.IsNullOrWhiteSpace(request.InitialGuardian.IdentityCardNumber);
            if (hasGuardianContact && string.IsNullOrWhiteSpace(request.InitialGuardian.FullName))
            {
                return ApiResponse<StudentProfileDto>.Fail(
                    SystemMessages.MSG01,
                    new List<string> { "Vui lòng nhập Họ tên phụ huynh khi cung cấp thông tin liên hệ (SĐT / Email / CCCD)." });
            }

            if (!string.IsNullOrWhiteSpace(request.InitialGuardian.FullName))
            {
                var guardianNameErr = StudentValidationHelper.ValidateFullName(request.InitialGuardian.FullName, "họ tên phụ huynh");
                if (guardianNameErr != null)
                {
                    return ApiResponse<StudentProfileDto>.Fail(SystemMessages.MSG01, new List<string> { guardianNameErr });
                }

                var (match, error) = await MatchGuardianAsync(request.InitialGuardian, cancellationToken);
                if (error != null)
                {
                    return ApiResponse<StudentProfileDto>.Fail(SystemMessages.MSG01, new List<string> { error });
                }
                guardianMatch = match;
            }
        }

        // Validate InitialClassId (existence, school year status, capacity & age-grade compatibility) BEFORE saving student (SC-03, SC-10)
        Class? initialTargetClass = null;
        var isDraftYearClass = false;
        if (request.InitialClassId.HasValue)
        {
            initialTargetClass = await _unitOfWork.Classes.GetClassWithDetailsAsync(request.InitialClassId.Value, cancellationToken);
            if (initialTargetClass == null)
            {
                return ApiResponse<StudentProfileDto>.Fail(SystemMessages.MSG01, new List<string> { "Lớp học được chọn không tồn tại." });
            }

            if (string.Equals(initialTargetClass.SchoolYear?.Status, SchoolYearStatuses.Archived, StringComparison.OrdinalIgnoreCase))
            {
                return ApiResponse<StudentProfileDto>.Fail(SystemMessages.MSG01, new List<string> { "Không thể xếp học sinh vào lớp thuộc năm học đã lưu trữ (Archived)." });
            }

            isDraftYearClass = string.Equals(initialTargetClass.SchoolYear?.Status, SchoolYearStatuses.Draft, StringComparison.OrdinalIgnoreCase);

            var gradeAgeError = StudentValidationHelper.ValidateAgeForGrade(
                request.DateOfBirth,
                initialTargetClass.Grade,
                initialTargetClass.SchoolYear?.StartDate.Year,
                request.IsSpecialNeeds,
                request.AllowAgeException);
            if (gradeAgeError != null)
            {
                return ApiResponse<StudentProfileDto>.Fail(SystemMessages.MSG01, new List<string> { gradeAgeError });
            }

            var enrolledCount = initialTargetClass.ClassAssignments.Count(ca => ca.IsActive || ca.IsDraft);
            if (initialTargetClass.Capacity > 0 && enrolledCount >= initialTargetClass.Capacity)
            {
                return ApiResponse<StudentProfileDto>.Fail(
                    SystemMessages.MSG15,
                    new List<string> { $"Lớp {initialTargetClass.ClassName} đã đủ sĩ số ({enrolledCount}/{initialTargetClass.Capacity}). Vui lòng chọn lớp khác hoặc chọn 'Chưa xếp lớp'." });
            }
        }

        // 3. Create Student
        var student = new Student
        {
            StudentCode = request.StudentCode.Trim().ToUpperInvariant(),
            FullName = request.FullName.Trim(),
            DateOfBirth = request.DateOfBirth,
            Gender = request.Gender.Trim(),
            EnrollmentDate = DateTime.UtcNow,
            Status = string.IsNullOrWhiteSpace(request.Status) ? "Active" : request.Status,
            AvatarUrl = request.AvatarUrl,
            ResidentialArea = request.ResidentialArea?.Trim(),
            IsSpecialNeeds = request.IsSpecialNeeds,
            SpecialNeedsNotes = request.SpecialNeedsNotes?.Trim(),
            CreatedAt = DateTime.UtcNow
        };

        await _unitOfWork.Students.AddAsync(student, cancellationToken);
        await _unitOfWork.SaveChangesAsync(cancellationToken);

        // 4. Initial Class Assignment if specified
        if (initialTargetClass != null)
        {
            var assignment = new ClassAssignment
            {
                ClassId = initialTargetClass.Id,
                StudentId = student.Id,
                AcademicYear = initialTargetClass.SchoolYear?.YearName ?? DateTime.UtcNow.Year.ToString(),
                StartDate = DateTime.UtcNow.Date,
                IsActive = !isDraftYearClass,
                IsDraft = isDraftYearClass
            };
            await _unitOfWork.Repository<ClassAssignment>().AddAsync(assignment, cancellationToken);
        }

        // 5. Initial guardian: a guardian record, never a user account (BR-64, BR-66)
        if (guardianMatch != null)
        {
            var guardian = await CreateOrReuseGuardianAsync(request.InitialGuardian!, guardianMatch, cancellationToken);
            // The first guardian of a new student is its primary contact (BR-13)
            await LinkGuardianRecordAsync(student.Id, guardian, request.InitialGuardian!.Relationship, true, cancellationToken);
        }

        await _unitOfWork.SaveChangesAsync(cancellationToken);

        await _auditService.LogActionAsync(
            actorUserId,
            AuditActions.StudentCreate,
            nameof(Student),
            student.Id.ToString(),
            null,
            new { student.StudentCode, student.FullName, student.DateOfBirth, student.Gender, student.Status, request.AllowAgeException, request.AgeExceptionReason },
            null, // TODO: pass the client IP address; it is not recorded yet.
            cancellationToken);
        await _unitOfWork.SaveChangesAsync(cancellationToken);

        var reloaded = await _unitOfWork.Students.GetStudentWithProfileAsync(student.Id, cancellationToken);
        return ApiResponse<StudentProfileDto>.Ok(MapToStudentProfileDto(reloaded ?? student), SystemMessages.MSG08);
    }

    public async Task<ApiResponse<StudentProfileDto>> UpdateStudentAsync(
        int studentId,
        UpdateStudentDto request,
        int? actorUserId = null,
        CancellationToken cancellationToken = default)
    {
        var student = await _unitOfWork.Students.GetStudentWithProfileAsync(studentId, cancellationToken);
        if (student == null)
        {
            return ApiResponse<StudentProfileDto>.Fail(SystemMessages.MSG11);
        }

        if (request.DateOfBirth == default || request.DateOfBirth.Year < 1900)
        {
            return ApiResponse<StudentProfileDto>.Fail(
                SystemMessages.MSG01,
                new List<string> { "Vui lòng nhập ngày sinh hợp lệ của học sinh." });
        }

        var updateDobErr = StudentValidationHelper.ValidateBirthDateAndAge(request.DateOfBirth, null, request.AllowAgeException);
        if (updateDobErr != null)
        {
            return ApiResponse<StudentProfileDto>.Fail(SystemMessages.MSG01, new List<string> { updateDobErr });
        }

        var nameErr = StudentValidationHelper.ValidateFullName(request.FullName, "họ và tên học sinh");
        if (nameErr != null)
        {
            return ApiResponse<StudentProfileDto>.Fail(SystemMessages.MSG01, new List<string> { nameErr });
        }

        var validUpdateGenders = new[] { "Nam", "Nữ", "Male", "Female" };
        if (!string.IsNullOrWhiteSpace(request.Gender) && !validUpdateGenders.Contains(request.Gender.Trim(), StringComparer.OrdinalIgnoreCase))
        {
            return ApiResponse<StudentProfileDto>.Fail(
                SystemMessages.MSG01,
                new List<string> { "Giới tính không hợp lệ. Vui lòng chọn 'Nam' hoặc 'Nữ'." });
        }

        var validStatuses = new[] { "Active", "Inactive", "Archived", "Draft" };
        var normalizedStatus = string.IsNullOrWhiteSpace(request.Status) ? student.Status : request.Status.Trim();
        var matchedStatus = validStatuses.FirstOrDefault(s => string.Equals(s, normalizedStatus, StringComparison.OrdinalIgnoreCase));
        if (matchedStatus == null)
        {
            return ApiResponse<StudentProfileDto>.Fail(
                SystemMessages.MSG01,
                new List<string> { "Trạng thái học sinh không hợp lệ (Active, Inactive, Archived)." });
        }

        // SC-09: Validate age-grade compatibility even when student remains in current class
        var currentActiveAssignments = student.ClassAssignments?.Where(ca => ca.IsActive).ToList() ?? new List<ClassAssignment>();
        if (!request.CurrentClassId.HasValue || currentActiveAssignments.Any(ca => ca.ClassId == request.CurrentClassId.Value))
        {
            var currentClass = currentActiveAssignments.FirstOrDefault()?.Class;
            if (currentClass == null && currentActiveAssignments.Count > 0)
            {
                currentClass = await _unitOfWork.Classes.GetClassWithDetailsAsync(currentActiveAssignments[0].ClassId, cancellationToken);
            }
            if (currentClass != null)
            {
                var gradeAgeError = StudentValidationHelper.ValidateAgeForGrade(
                    request.DateOfBirth,
                    currentClass.Grade,
                    currentClass.SchoolYear?.StartDate.Year,
                    request.IsSpecialNeeds,
                    request.AllowAgeException);
                if (gradeAgeError != null)
                {
                    return ApiResponse<StudentProfileDto>.Fail(SystemMessages.MSG01, new List<string> { gradeAgeError });
                }
            }
        }

        var oldValues = new { student.FullName, student.DateOfBirth, student.Gender, student.Status };

        student.FullName = request.FullName.Trim();
        student.DateOfBirth = request.DateOfBirth;
        student.Gender = request.Gender.Trim();
        student.Status = matchedStatus;
        student.ResidentialArea = request.ResidentialArea?.Trim();
        student.IsSpecialNeeds = request.IsSpecialNeeds;
        student.SpecialNeedsNotes = request.SpecialNeedsNotes?.Trim();
        if (!string.IsNullOrWhiteSpace(request.AvatarUrl))
        {
            student.AvatarUrl = request.AvatarUrl;
        }

        // If CurrentClassId changed
        if (request.CurrentClassId.HasValue)
        {
            var activeAssignments = currentActiveAssignments;
            if (activeAssignments.Count == 0 || activeAssignments.Any(ca => ca.ClassId != request.CurrentClassId.Value))
            {
                var targetClass = await _unitOfWork.Classes.GetClassWithDetailsAsync(request.CurrentClassId.Value, cancellationToken);
                if (targetClass != null)
                {
                    if (string.Equals(targetClass.SchoolYear?.Status, SchoolYearStatuses.Archived, StringComparison.OrdinalIgnoreCase))
                    {
                        return ApiResponse<StudentProfileDto>.Fail(SystemMessages.MSG01, new List<string> { "Không thể chuyển học sinh vào lớp thuộc năm học đã lưu trữ (Archived)." });
                    }

                    var isDraftYearClass = string.Equals(targetClass.SchoolYear?.Status, SchoolYearStatuses.Draft, StringComparison.OrdinalIgnoreCase);

                    var gradeAgeError = StudentValidationHelper.ValidateAgeForGrade(
                        request.DateOfBirth,
                        targetClass.Grade,
                        targetClass.SchoolYear?.StartDate.Year,
                        request.IsSpecialNeeds,
                        request.AllowAgeException);
                    if (gradeAgeError != null)
                    {
                        return ApiResponse<StudentProfileDto>.Fail(SystemMessages.MSG01, new List<string> { gradeAgeError });
                    }

                    var enrolledCount = targetClass.ClassAssignments.Count(ca => ca.IsActive || ca.IsDraft);
                    if (targetClass.Capacity > 0 && enrolledCount >= targetClass.Capacity)
                    {
                        return ApiResponse<StudentProfileDto>.Fail(
                            SystemMessages.MSG01,
                            new List<string> { $"Lớp {targetClass.ClassName} đã đủ sĩ số tối đa ({targetClass.Capacity} học sinh). Không thể chuyển học sinh vào lớp này." });
                    }

                    if (!isDraftYearClass)
                    {
                        foreach (var oldCa in activeAssignments)
                        {
                            oldCa.IsActive = false;
                            oldCa.EndDate = DateTime.UtcNow.Date;
                            await _unitOfWork.Repository<ClassAssignment>().UpdateAsync(oldCa, cancellationToken);
                        }
                    }
                    else
                    {
                        var existingDraftAssignments = (student.ClassAssignments ?? new List<ClassAssignment>())
                            .Where(ca => ca.IsDraft && ca.Class?.SchoolYearId == targetClass.SchoolYearId)
                            .ToList();
                        foreach (var oldDraft in existingDraftAssignments)
                        {
                            await _unitOfWork.Repository<ClassAssignment>().DeleteAsync(oldDraft, cancellationToken);
                        }
                    }

                    var newAssignment = new ClassAssignment
                    {
                        ClassId = targetClass.Id,
                        StudentId = student.Id,
                        AcademicYear = targetClass.SchoolYear?.YearName ?? DateTime.UtcNow.Year.ToString(),
                        StartDate = DateTime.UtcNow.Date,
                        IsActive = !isDraftYearClass,
                        IsDraft = isDraftYearClass
                    };
                    await _unitOfWork.Repository<ClassAssignment>().AddAsync(newAssignment, cancellationToken);
                }
            }
        }

        await _unitOfWork.Students.UpdateAsync(student, cancellationToken);
        await _unitOfWork.SaveChangesAsync(cancellationToken);

        await _auditService.LogActionAsync(
            actorUserId,
            AuditActions.StudentUpdate,
            nameof(Student),
            student.Id.ToString(),
            oldValues,
            new { student.FullName, student.DateOfBirth, student.Gender, student.Status, request.AllowAgeException, request.AgeExceptionReason },
            null, // TODO: pass the client IP address; it is not recorded yet.
            cancellationToken);
        await _unitOfWork.SaveChangesAsync(cancellationToken);

        var reloaded = await _unitOfWork.Students.GetStudentWithProfileAsync(student.Id, cancellationToken);
        return ApiResponse<StudentProfileDto>.Ok(MapToStudentProfileDto(reloaded ?? student), SystemMessages.MSG08);
    }

    public async Task<ApiResponse<DuplicateCheckResultDto>> CheckDuplicateAsync(
        string studentCode,
        CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(studentCode))
        {
            return ApiResponse<DuplicateCheckResultDto>.Fail(SystemMessages.MSG01, new List<string> { "Mã học sinh không được để trống." });
        }

        var normalizedCode = studentCode.Trim().ToUpperInvariant();
        var existing = await _unitOfWork.Students.GetByStudentCodeAsync(normalizedCode, cancellationToken);

        if (existing != null)
        {
            return ApiResponse<DuplicateCheckResultDto>.Ok(new DuplicateCheckResultDto
            {
                IsDuplicate = true,
                StudentCode = normalizedCode,
                ExistingStudent = MapToStudentProfileDto(existing),
                Message = $"Mã học sinh '{normalizedCode}' đã tồn tại trong hệ thống (Học sinh: {existing.FullName})."
            }, SystemMessages.MSG12);
        }

        return ApiResponse<DuplicateCheckResultDto>.Ok(new DuplicateCheckResultDto
        {
            IsDuplicate = false,
            StudentCode = normalizedCode,
            ExistingStudent = null,
            Message = $"Mã học sinh '{normalizedCode}' hợp lệ (chưa tồn tại)."
        });
    }

    public async Task<ApiResponse<StudentProfileDto>> ResolveDuplicateAsync(
        ResolveDuplicateStudentDto request,
        int? actorUserId = null,
        CancellationToken cancellationToken = default)
    {
        var studentCode = !string.IsNullOrWhiteSpace(request.StudentCode)
            ? request.StudentCode
            : (request.ProfileData?.StudentCode ?? request.NewStudentData?.StudentCode);

        if (string.IsNullOrWhiteSpace(studentCode) && request.ExistingStudentId.HasValue)
        {
            var existingById = await _unitOfWork.Students.GetByIdAsync(request.ExistingStudentId.Value, cancellationToken);
            if (existingById != null)
            {
                studentCode = existingById.StudentCode;
            }
        }

        if (string.IsNullOrWhiteSpace(studentCode))
        {
            return ApiResponse<StudentProfileDto>.Fail(SystemMessages.MSG01, new List<string> { "Mã học sinh là bắt buộc." });
        }

        var normalizedCode = studentCode.Trim().ToUpperInvariant();
        var existing = await _unitOfWork.Students.GetByStudentCodeAsync(normalizedCode, cancellationToken);

        var action = !string.IsNullOrWhiteSpace(request.Action) ? request.Action : (request.ResolutionAction ?? "Merge");
        var profileData = request.ProfileData ?? request.NewStudentData ?? new CreateStudentDto();

        GuardianMatch? mergeGuardianMatch = null;
        if (profileData.InitialGuardian != null && !string.IsNullOrWhiteSpace(profileData.InitialGuardian.FullName))
        {
            var (match, error) = await MatchGuardianAsync(profileData.InitialGuardian, cancellationToken);
            if (error != null)
            {
                return ApiResponse<StudentProfileDto>.Fail(SystemMessages.MSG01, new List<string> { error });
            }
            mergeGuardianMatch = match;
        }

        if (string.Equals(action, "Rename", StringComparison.OrdinalIgnoreCase))
        {
            if (string.IsNullOrWhiteSpace(request.NewStudentCode))
            {
                return ApiResponse<StudentProfileDto>.Fail(SystemMessages.MSG01, new List<string> { "Vui lòng nhập mã học sinh mới khi chọn hành động Đổi mã (Rename)." });
            }

            var newNormalizedCode = request.NewStudentCode.Trim().ToUpperInvariant();
            var codeExists = await _unitOfWork.Students.ExistsByStudentCodeAsync(newNormalizedCode, null, cancellationToken);
            if (codeExists)
            {
                return ApiResponse<StudentProfileDto>.Fail(SystemMessages.MSG12, new List<string> { $"Mã học sinh mới '{newNormalizedCode}' cũng đã tồn tại." });
            }

            // Create new student with renamed code
            var createDto = profileData;
            createDto.StudentCode = newNormalizedCode;
            return await CreateStudentAsync(createDto, actorUserId, cancellationToken);
        }

        if (existing == null)
        {
            // No conflict in DB, can create fresh
            var createDto = profileData;
            createDto.StudentCode = normalizedCode;
            return await CreateStudentAsync(createDto, actorUserId, cancellationToken);
        }

        // Action: "Merge" or "Overwrite"
        var oldValues = new { existing.FullName, existing.DateOfBirth, existing.Gender, existing.Status };

        if (profileData != null)
        {
            if (!string.IsNullOrWhiteSpace(profileData.FullName))
            {
                var dupNameErr = StudentValidationHelper.ValidateFullName(profileData.FullName, "họ và tên học sinh");
                if (dupNameErr != null)
                {
                    return ApiResponse<StudentProfileDto>.Fail(SystemMessages.MSG01, new List<string> { dupNameErr });
                }
            }

            if (profileData.DateOfBirth != default)
            {
                var dupDobErr = StudentValidationHelper.ValidateBirthDateAndAge(profileData.DateOfBirth, null, profileData.AllowAgeException);
                if (dupDobErr != null)
                {
                    return ApiResponse<StudentProfileDto>.Fail(SystemMessages.MSG01, new List<string> { dupDobErr });
                }
            }

            var validDupGenders = new[] { "Nam", "Nữ", "Male", "Female" };
            if (!string.IsNullOrWhiteSpace(profileData.Gender) && !validDupGenders.Contains(profileData.Gender.Trim(), StringComparer.OrdinalIgnoreCase))
            {
                return ApiResponse<StudentProfileDto>.Fail(
                    SystemMessages.MSG01,
                    new List<string> { "Giới tính không hợp lệ. Vui lòng chọn 'Nam' hoặc 'Nữ'." });
            }

            Class? resolveTargetClass = null;
            var resolveIsDraftYear = false;
            if (profileData.InitialClassId.HasValue)
            {
                var activeClass = existing.ClassAssignments.FirstOrDefault(ca => ca.IsActive);
                if (activeClass == null)
                {
                    resolveTargetClass = await _unitOfWork.Classes.GetClassWithDetailsAsync(profileData.InitialClassId.Value, cancellationToken);
                    if (resolveTargetClass == null)
                    {
                        return ApiResponse<StudentProfileDto>.Fail(SystemMessages.MSG01, new List<string> { "Lớp học được chọn không tồn tại." });
                    }

                    if (string.Equals(resolveTargetClass.SchoolYear?.Status, SchoolYearStatuses.Archived, StringComparison.OrdinalIgnoreCase))
                    {
                        return ApiResponse<StudentProfileDto>.Fail(SystemMessages.MSG01, new List<string> { "Không thể xếp học sinh vào lớp thuộc năm học đã lưu trữ (Archived)." });
                    }

                    resolveIsDraftYear = string.Equals(resolveTargetClass.SchoolYear?.Status, SchoolYearStatuses.Draft, StringComparison.OrdinalIgnoreCase);

                    var dobToCheck = profileData.DateOfBirth != default ? profileData.DateOfBirth : existing.DateOfBirth;
                    if (dobToCheck != default)
                    {
                        var gradeAgeError = StudentValidationHelper.ValidateAgeForGrade(
                            dobToCheck,
                            resolveTargetClass.Grade,
                            resolveTargetClass.SchoolYear?.StartDate.Year,
                            profileData.IsSpecialNeeds,
                            profileData.AllowAgeException);
                        if (gradeAgeError != null)
                        {
                            return ApiResponse<StudentProfileDto>.Fail(SystemMessages.MSG01, new List<string> { gradeAgeError });
                        }
                    }

                    var enrolledCount = resolveTargetClass.ClassAssignments.Count(ca => ca.IsActive || ca.IsDraft);
                    if (resolveTargetClass.Capacity > 0 && enrolledCount >= resolveTargetClass.Capacity)
                    {
                        return ApiResponse<StudentProfileDto>.Fail(
                            SystemMessages.MSG15,
                            new List<string> { $"Lớp {resolveTargetClass.ClassName} đã đủ sĩ số ({enrolledCount}/{resolveTargetClass.Capacity})." });
                    }
                }
            }

            if (!string.IsNullOrWhiteSpace(profileData.FullName))
            {
                existing.FullName = profileData.FullName.Trim();
            }

            if (profileData.DateOfBirth != default)
            {
                existing.DateOfBirth = profileData.DateOfBirth;
            }

            if (!string.IsNullOrWhiteSpace(profileData.Gender))
            {
                existing.Gender = profileData.Gender.Trim();
            }

            if (!string.IsNullOrWhiteSpace(profileData.AvatarUrl))
            {
                existing.AvatarUrl = profileData.AvatarUrl;
            }

            if (!string.IsNullOrWhiteSpace(profileData.ResidentialArea))
            {
                existing.ResidentialArea = profileData.ResidentialArea.Trim();
            }

            // Set Student Status = 'Active' per workflow diagram
            existing.Status = "Active";

            // If guardian info provided, merge guardian
            if (mergeGuardianMatch != null)
            {
                var guardian = await CreateOrReuseGuardianAsync(profileData.InitialGuardian!, mergeGuardianMatch, cancellationToken);
                await LinkGuardianRecordAsync(existing.Id, guardian, profileData.InitialGuardian!.Relationship,
                    profileData.InitialGuardian.IsPrimaryContact ? true : null, cancellationToken);
            }

            if (resolveTargetClass != null)
            {
                var assignment = new ClassAssignment
                {
                    ClassId = resolveTargetClass.Id,
                    StudentId = existing.Id,
                    AcademicYear = resolveTargetClass.SchoolYear?.YearName ?? DateTime.UtcNow.Year.ToString(),
                    StartDate = DateTime.UtcNow.Date,
                    IsActive = !resolveIsDraftYear,
                    IsDraft = resolveIsDraftYear
                };
                await _unitOfWork.Repository<ClassAssignment>().AddAsync(assignment, cancellationToken);
            }
        }
        else
        {
            existing.Status = "Active";
        }

        await _unitOfWork.Students.UpdateAsync(existing, cancellationToken);
        await _unitOfWork.SaveChangesAsync(cancellationToken);

        await _auditService.LogActionAsync(
            actorUserId,
            AuditActions.StudentResolveDuplicate,
            nameof(Student),
            existing.Id.ToString(),
            oldValues,
            new { Resolution = action, StudentCode = normalizedCode, existing.FullName, existing.DateOfBirth, existing.Gender, existing.Status },
            null, // TODO: pass the client IP address; it is not recorded yet.
            cancellationToken);
        await _unitOfWork.SaveChangesAsync(cancellationToken);

        var reloaded = await _unitOfWork.Students.GetStudentWithProfileAsync(existing.Id, cancellationToken);
        return ApiResponse<StudentProfileDto>.Ok(MapToStudentProfileDto(reloaded ?? existing), SystemMessages.MSG08);
    }

    public async Task<ApiResponse<bool>> ActivateStudentAsync(
        int studentId,
        int? actorUserId = null,
        CancellationToken cancellationToken = default)
    {
        var student = await _unitOfWork.Repository<Student>().GetByIdAsync(studentId, cancellationToken);
        if (student == null)
        {
            return ApiResponse<bool>.Fail(SystemMessages.MSG11);
        }

        student.Status = "Active";
        await _unitOfWork.Students.UpdateAsync(student, cancellationToken);
        await _unitOfWork.SaveChangesAsync(cancellationToken);

        await _auditService.LogActionAsync(
            actorUserId,
            AuditActions.StudentActivate,
            nameof(Student),
            student.Id.ToString(),
            null,
            new { student.StudentCode, student.Status },
            null, // TODO: pass the client IP address; it is not recorded yet.
            cancellationToken);
        await _unitOfWork.SaveChangesAsync(cancellationToken);

        return ApiResponse<bool>.Ok(true, SystemMessages.MSG08);
    }

    #region UC-10 Link or Unlink Guardian to Student (SSAWP-152)

    public async Task<ApiResponse<List<GuardianRecordDto>>> SearchGuardianRecordsAsync(string? search, CancellationToken ct = default)
    {
        var q = search?.Trim().ToLower() ?? string.Empty;
        if (q.Length < 2)
        {
            return ApiResponse<List<GuardianRecordDto>>.Ok(new List<GuardianRecordDto>());
        }

        // Office Staff pick the record themselves; searching by phone is fine here (no automatic linking, BR-66)
        var items = await _unitOfWork.Repository<Guardian>().Query()
            .AsNoTracking()
            .Where(g => g.FullName.ToLower().Contains(q)
                        || (g.Email != null && g.Email.ToLower().Contains(q))
                        || (g.IdentityCardNumber != null && g.IdentityCardNumber.Contains(q))
                        || (g.PhoneNumber != null && g.PhoneNumber.Contains(q)))
            .OrderBy(g => g.FullName)
            .Take(20)
            .Select(g => new GuardianRecordDto
            {
                Id = g.Id,
                FullName = g.FullName,
                PhoneNumber = g.PhoneNumber,
                Email = g.Email,
                IdentityCardNumber = g.IdentityCardNumber,
                HasAccount = g.UserId != null,
                LinkedStudentCount = g.StudentGuardians.Count
            })
            .ToListAsync(ct);
        return ApiResponse<List<GuardianRecordDto>>.Ok(items);
    }

    public async Task<ApiResponse<StudentProfileDto>> LinkGuardianAsync(int studentId, LinkGuardianRequestDto request, int? actorUserId = null, CancellationToken ct = default)
    {
        var student = await _unitOfWork.Repository<Student>().GetByIdAsync(studentId, ct);
        if (student == null)
        {
            return ApiResponse<StudentProfileDto>.Fail(SystemMessages.MSG11);
        }
        if (!Enum.IsDefined(typeof(RelationshipTypes), request.Relationship))
        {
            return ApiResponse<StudentProfileDto>.Fail(SystemMessages.MSG01, new List<string> { "Quan hệ với học sinh không hợp lệ." });
        }

        Guardian? guardian;
        var reused = false;
        if (request.GuardianId.HasValue)
        {
            guardian = await _unitOfWork.Repository<Guardian>().GetByIdAsync(request.GuardianId.Value, ct);
            if (guardian == null)
            {
                return ApiResponse<StudentProfileDto>.Fail(SystemMessages.MSG11);
            }
        }
        else if (request.NewGuardian != null && !string.IsNullOrWhiteSpace(request.NewGuardian.FullName))
        {
            var gNameErr = StudentValidationHelper.ValidateFullName(request.NewGuardian.FullName, "họ tên phụ huynh");
            if (gNameErr != null)
            {
                return ApiResponse<StudentProfileDto>.Fail(SystemMessages.MSG01, new List<string> { gNameErr });
            }

            var (match, error) = await MatchGuardianAsync(request.NewGuardian, ct);
            if (error != null)
            {
                return ApiResponse<StudentProfileDto>.Fail(SystemMessages.MSG01, new List<string> { error });
            }
            reused = match.Kind == GuardianMatchKind.Reused;
            guardian = await CreateOrReuseGuardianAsync(request.NewGuardian, match, ct);
        }
        else
        {
            return ApiResponse<StudentProfileDto>.Fail(SystemMessages.MSG01, new List<string> { "Vui lòng chọn hoặc nhập thông tin người giám hộ." });
        }

        var links = (await _unitOfWork.Repository<StudentGuardian>().FindAsync(sg => sg.StudentId == studentId, ct)).ToList();
        if (links.Any(sg => sg.GuardianId == guardian.Id))
        {
            // E1: duplicate relationship
            return ApiResponse<StudentProfileDto>.Fail(SystemMessages.MSG01, new List<string> { "Người giám hộ này đã được liên kết với học sinh." });
        }

        // BR-13: a student without a primary guardian gets this one as primary
        var isPrimary = request.IsPrimaryContact || !links.Any(sg => sg.IsPrimaryContact);
        await LinkGuardianRecordAsync(studentId, guardian, request.Relationship, isPrimary, ct);

        await _auditService.LogActionAsync(actorUserId, AuditActions.StudentGuardianLink, nameof(StudentGuardian), $"{studentId}:{guardian.Id}",
            null, new { StudentId = studentId, GuardianId = guardian.Id, guardian.FullName, Relationship = ToRelationship(request.Relationship).ToString(), IsPrimaryContact = isPrimary, ReusedRecord = reused },
            null, ct);
        await _unitOfWork.SaveChangesAsync(ct);

        return await ReloadProfileAsync(studentId, ct);
    }

    public async Task<ApiResponse<StudentProfileDto>> UpdateGuardianLinkAsync(int studentId, int guardianId, UpdateGuardianLinkRequestDto request, int? actorUserId = null, CancellationToken ct = default)
    {
        var student = await _unitOfWork.Repository<Student>().GetByIdAsync(studentId, ct);
        var links = (await _unitOfWork.Repository<StudentGuardian>().FindAsync(sg => sg.StudentId == studentId, ct)).ToList();
        var link = links.FirstOrDefault(sg => sg.GuardianId == guardianId);
        if (student == null || link == null)
        {
            return ApiResponse<StudentProfileDto>.Fail(SystemMessages.MSG11);
        }
        if (!Enum.IsDefined(typeof(RelationshipTypes), request.Relationship))
        {
            return ApiResponse<StudentProfileDto>.Fail(SystemMessages.MSG01, new List<string> { "Quan hệ với học sinh không hợp lệ." });
        }
        if (link.IsPrimaryContact && !request.IsPrimaryContact && IsActive(student))
        {
            // BR-13: choose another primary guardian instead
            return ApiResponse<StudentProfileDto>.Fail(SystemMessages.MSG01, new List<string> { "Học sinh đang học cần có một người giám hộ chính. Hãy chọn người giám hộ chính khác trước." });
        }

        var old = new { Relationship = link.Relationship.ToString(), link.IsPrimaryContact };
        if (request.IsPrimaryContact)
        {
            foreach (var other in links.Where(sg => sg.GuardianId != guardianId && sg.IsPrimaryContact))
            {
                other.IsPrimaryContact = false;
            }
        }
        link.Relationship = ToRelationship(request.Relationship);
        link.IsPrimaryContact = request.IsPrimaryContact;

        await _auditService.LogActionAsync(actorUserId, AuditActions.StudentGuardianUpdate, nameof(StudentGuardian), $"{studentId}:{guardianId}",
            old, new { Relationship = link.Relationship.ToString(), link.IsPrimaryContact }, null, ct);
        await _unitOfWork.SaveChangesAsync(ct);

        return await ReloadProfileAsync(studentId, ct);
    }

    public async Task<ApiResponse<StudentProfileDto>> UnlinkGuardianAsync(int studentId, int guardianId, int? actorUserId = null, CancellationToken ct = default)
    {
        var student = await _unitOfWork.Repository<Student>().GetByIdAsync(studentId, ct);
        var link = (await _unitOfWork.Repository<StudentGuardian>().FindAsync(sg => sg.StudentId == studentId && sg.GuardianId == guardianId, ct)).FirstOrDefault();
        if (student == null || link == null)
        {
            return ApiResponse<StudentProfileDto>.Fail(SystemMessages.MSG11);
        }

        // E1, BR-13: an active student keeps a primary guardian
        if (link.IsPrimaryContact && IsActive(student))
        {
            return ApiResponse<StudentProfileDto>.Fail(SystemMessages.MSG01, new List<string> { "Không thể gỡ người giám hộ chính của học sinh đang học. Hãy chọn người giám hộ chính khác trước." });
        }

        await _unitOfWork.Repository<StudentGuardian>().DeleteAsync(link, ct);
        // A1: the removed relationship is kept in the audit log; the guardian record itself stays
        await _auditService.LogActionAsync(actorUserId, AuditActions.StudentGuardianUnlink, nameof(StudentGuardian), $"{studentId}:{guardianId}",
            new { StudentId = studentId, GuardianId = guardianId, Relationship = link.Relationship.ToString(), link.IsPrimaryContact }, null, null, ct);
        await _unitOfWork.SaveChangesAsync(ct);

        return await ReloadProfileAsync(studentId, ct);
    }

    public async Task<ApiResponse<GuardianRecordDto>> UpdateGuardianContactAsync(int guardianId, UpdateGuardianContactDto request, int? actorUserId = null, CancellationToken ct = default)
    {
        var guardian = await _unitOfWork.Repository<Guardian>().GetByIdAsync(guardianId, ct);
        if (guardian == null)
        {
            return ApiResponse<GuardianRecordDto>.Fail(SystemMessages.MSG11);
        }

        var gNameErr = StudentValidationHelper.ValidateFullName(request.FullName, "họ tên người giám hộ");
        if (gNameErr != null)
        {
            return ApiResponse<GuardianRecordDto>.Fail(SystemMessages.MSG01, new List<string> { gNameErr });
        }

        if (!StudentValidationHelper.IsValidPhone(request.PhoneNumber))
        {
            return ApiResponse<GuardianRecordDto>.Fail(SystemMessages.MSG01, new List<string> { "Số điện thoại người giám hộ không hợp lệ (bắt buộc đúng 10 chữ số, bắt đầu bằng số 0)." });
        }

        if (!StudentValidationHelper.IsValidIdentityCard(request.IdentityCardNumber))
        {
            return ApiResponse<GuardianRecordDto>.Fail(SystemMessages.MSG01, new List<string> { "Số CCCD/CMND người giám hộ không hợp lệ (bắt buộc 9 hoặc 12 chữ số)." });
        }

        var normalizedId = GuardianMatcher.NormalizeIdentityCard(request.IdentityCardNumber);
        if (normalizedId.Length > 0)
        {
            var dupId = await _unitOfWork.Repository<Guardian>().Query()
                .AsNoTracking()
                .AnyAsync(g => g.Id != guardianId && g.IdentityCardNumber != null && g.IdentityCardNumber.Replace(" ", "").ToUpper() == normalizedId, ct);
            if (dupId)
            {
                return ApiResponse<GuardianRecordDto>.Fail(SystemMessages.MSG01, new List<string> { "Số CCCD/CMND này đã thuộc về một hồ sơ người giám hộ khác." });
            }
        }

        var normalizedMail = GuardianMatcher.NormalizeEmail(request.Email);
        if (guardian.UserId != null)
        {
            var currentMail = GuardianMatcher.NormalizeEmail(guardian.Email);
            if (!string.IsNullOrWhiteSpace(request.Email) && !string.Equals(normalizedMail, currentMail, StringComparison.OrdinalIgnoreCase))
            {
                return ApiResponse<GuardianRecordDto>.Fail(SystemMessages.MSG01, new List<string> { "Người giám hộ đã có tài khoản đăng nhập. Vui lòng gửi Yêu cầu đổi Email (BR-69)." });
            }
        }
        else if (!string.IsNullOrWhiteSpace(request.Email))
        {
            if (!GuardianMatcher.IsValidEmail(request.Email))
            {
                return ApiResponse<GuardianRecordDto>.Fail(SystemMessages.MSG01, new List<string> { "Email người giám hộ không hợp lệ." });
            }
            var dupMail = await _unitOfWork.Repository<Guardian>().Query()
                .AsNoTracking()
                .AnyAsync(g => g.Id != guardianId && g.Email != null && g.Email.ToLower() == normalizedMail, ct);
            if (dupMail)
            {
                return ApiResponse<GuardianRecordDto>.Fail(SystemMessages.MSG01, new List<string> { "Email này đã thuộc về một hồ sơ người giám hộ khác." });
            }
        }

        var oldValues = new { guardian.FullName, guardian.PhoneNumber, guardian.Email, guardian.IdentityCardNumber, guardian.Occupation, guardian.Address };

        guardian.FullName = StudentValidationHelper.NormalizeWhitespace(request.FullName);
        guardian.PhoneNumber = string.IsNullOrWhiteSpace(request.PhoneNumber) ? null : request.PhoneNumber.Trim();
        guardian.IdentityCardNumber = request.IdentityCardNumber?.Trim() ?? string.Empty;
        guardian.Occupation = string.IsNullOrWhiteSpace(request.Occupation) ? null : request.Occupation.Trim();
        guardian.Address = string.IsNullOrWhiteSpace(request.Address) ? null : request.Address.Trim();
        if (guardian.UserId == null)
        {
            guardian.Email = string.IsNullOrWhiteSpace(request.Email) ? null : request.Email.Trim();
        }

        await _unitOfWork.Repository<Guardian>().UpdateAsync(guardian, ct);
        await _auditService.LogActionAsync(
            actorUserId,
            AuditActions.GuardianContactUpdate,
            nameof(Guardian),
            guardian.Id.ToString(),
            oldValues,
            new { guardian.FullName, guardian.PhoneNumber, guardian.Email, guardian.IdentityCardNumber, guardian.Occupation, guardian.Address },
            null,
            ct);
        await _unitOfWork.SaveChangesAsync(ct);

        var linkedCount = await _unitOfWork.Repository<StudentGuardian>().Query()
            .AsNoTracking()
            .CountAsync(sg => sg.GuardianId == guardian.Id, ct);

        return ApiResponse<GuardianRecordDto>.Ok(new GuardianRecordDto
        {
            Id = guardian.Id,
            FullName = guardian.FullName,
            PhoneNumber = guardian.PhoneNumber,
            Email = guardian.Email,
            IdentityCardNumber = guardian.IdentityCardNumber,
            HasAccount = guardian.UserId != null,
            LinkedStudentCount = linkedCount
        }, SystemMessages.MSG08);
    }

    public async Task<ApiResponse<StudentHistoryDto>> GetStudentHistoryAsync(int studentId, int? requestingUserId = null, string? requestingUserRole = null, CancellationToken ct = default)
    {
        var isDeniedRole = string.Equals(requestingUserRole, UserRoles.ServiceStaff.ToString(), StringComparison.OrdinalIgnoreCase)
            || string.Equals(requestingUserRole, UserRoles.Admin.ToString(), StringComparison.OrdinalIgnoreCase);
        if (isDeniedRole)
        {
            return ApiResponse<StudentHistoryDto>.Fail(SystemMessages.MSG10);
        }

        var student = await _unitOfWork.Students.GetStudentWithProfileAsync(studentId, ct);
        if (student == null)
        {
            return ApiResponse<StudentHistoryDto>.Fail(SystemMessages.MSG11);
        }

        if (!string.IsNullOrEmpty(requestingUserRole) && requestingUserId.HasValue)
        {
            if (string.Equals(requestingUserRole, UserRoles.Parent.ToString(), StringComparison.OrdinalIgnoreCase))
            {
                var isLinked = student.StudentGuardians.Any(sg => sg.Guardian.UserId == requestingUserId.Value);
                if (!isLinked) return ApiResponse<StudentHistoryDto>.Fail(SystemMessages.MSG10);
            }
            else if (string.Equals(requestingUserRole, UserRoles.Teacher.ToString(), StringComparison.OrdinalIgnoreCase))
            {
                var isHomeroom = student.ClassAssignments
                    .Any(ca => ca.IsActive && ca.Class.HomeroomTeacher != null && ca.Class.HomeroomTeacher.UserId == requestingUserId.Value);
                if (!isHomeroom) return ApiResponse<StudentHistoryDto>.Fail(SystemMessages.MSG10);
            }
        }

        var enrollmentHistory = student.ClassAssignments
            .OrderByDescending(ca => ca.IsActive)
            .ThenByDescending(ca => ca.StartDate)
            .Select(ca => new StudentEnrollmentHistoryItemDto
            {
                AssignmentId = ca.Id,
                AcademicYear = ca.AcademicYear,
                ClassId = ca.ClassId,
                ClassName = ca.Class?.ClassName ?? string.Empty,
                Grade = ca.Class?.Grade ?? 0,
                HomeroomTeacherName = ca.Class?.HomeroomTeacher?.User?.FullName,
                StartDate = ca.StartDate,
                EndDate = ca.EndDate,
                IsActive = ca.IsActive,
                IsDraft = ca.IsDraft
            })
            .ToList();

        var attendanceList = await _unitOfWork.Repository<AttendanceRecord>().Query()
            .AsNoTracking()
            .Include(a => a.Class)
            .Where(a => a.StudentId == studentId)
            .OrderByDescending(a => a.Date)
            .ThenByDescending(a => a.Session)
            .ToListAsync(ct);

        var attendanceSummary = new StudentAttendanceHistorySummaryDto
        {
            TotalDays = attendanceList.Count,
            PresentCount = attendanceList.Count(a => a.Status == AttendanceStatus.Present),
            AbsentExcusedCount = attendanceList.Count(a => a.Status == AttendanceStatus.ExcusedAbsence),
            AbsentUnexcusedCount = attendanceList.Count(a => a.Status == AttendanceStatus.UnexcusedAbsence),
            LateCount = attendanceList.Count(a => a.Status == AttendanceStatus.Late),
            RecentRecords = attendanceList.Take(20).Select(a => new StudentAttendanceHistoryItemDto
            {
                Date = a.Date,
                Session = a.Session.ToString(),
                Status = a.Status.ToString(),
                ClassName = a.Class?.ClassName,
                Remarks = a.Remarks
            }).ToList()
        };

        var requestsList = await _unitOfWork.Repository<AdministrativeRequest>().Query()
            .AsNoTracking()
            .Include(r => r.LeaveRequest)
            .Include(r => r.ClassTransferDetail).ThenInclude(ctd => ctd!.FromClass)
            .Include(r => r.ClassTransferDetail).ThenInclude(ctd => ctd!.ToClass)
            .Include(r => r.DocumentRequestDetail)
            .Where(r => r.StudentId == studentId)
            .OrderByDescending(r => r.SubmissionDate)
            .Take(30)
            .ToListAsync(ct);

        var requests = requestsList.Select(r =>
        {
            string? summary = null;
            if (r.LeaveRequest != null)
            {
                summary = $"Nghỉ {r.LeaveRequest.StartDate:dd/MM/yyyy} - {r.LeaveRequest.EndDate:dd/MM/yyyy}: {r.LeaveRequest.Reason}";
            }
            else if (r.ClassTransferDetail != null)
            {
                summary = $"Chuyển lớp {r.ClassTransferDetail.FromClass?.ClassName} -> {r.ClassTransferDetail.ToClass?.ClassName}: {r.ClassTransferDetail.TransferReason}";
            }
            else if (r.DocumentRequestDetail != null)
            {
                summary = $"{r.DocumentRequestDetail.DocumentType}: {r.DocumentRequestDetail.Purpose}";
            }
            return new StudentRequestHistoryItemDto
            {
                RequestId = r.Id,
                RequestNumber = r.RequestNumber,
                RequestType = r.RequestType,
                Status = r.Status.ToString(),
                SubmissionDate = r.SubmissionDate,
                ProcessedDate = r.ProcessedDate,
                Summary = summary
            };
        }).ToList();

        var lunchList = await _unitOfWork.Repository<LunchRegistration>().Query()
            .AsNoTracking()
            .Where(l => l.StudentId == studentId)
            .OrderByDescending(l => l.Date)
            .ToListAsync(ct);

        var lunchSummary = new StudentLunchHistorySummaryDto
        {
            ActiveRegistrationsCount = lunchList.Count(l => l.IsActive && !l.IsWaitlisted),
            CancelledCount = lunchList.Count(l => !l.IsActive && l.CancellationSource != null),
            WaitlistedCount = lunchList.Count(l => l.IsWaitlisted),
            RecentRegistrations = lunchList.Take(15).Select(l => new StudentLunchHistoryItemDto
            {
                Date = l.Date,
                MealType = l.MealType.ToString(),
                IsActive = l.IsActive,
                IsWaitlisted = l.IsWaitlisted,
                CancellationSource = l.CancellationSource?.ToString()
            }).ToList()
        };

        var auditLogs = new List<StudentAuditHistoryItemDto>();
        var canSeeAudit = string.Equals(requestingUserRole, UserRoles.OfficeStaff.ToString(), StringComparison.OrdinalIgnoreCase)
            || string.Equals(requestingUserRole, UserRoles.SchoolBoard.ToString(), StringComparison.OrdinalIgnoreCase);
        if (canSeeAudit)
        {
            auditLogs = await _unitOfWork.Repository<AuditLog>().Query()
                .AsNoTracking()
                .Include(a => a.User)
                .Where(a => a.EntityName == nameof(Student) && a.EntityId == studentId.ToString())
                .OrderByDescending(a => a.Timestamp)
                .Take(30)
                .Select(a => new StudentAuditHistoryItemDto
                {
                    Timestamp = a.Timestamp,
                    Action = a.Action,
                    ActorName = a.User != null ? a.User.FullName : null,
                    OldValues = a.OldValues,
                    NewValues = a.NewValues
                })
                .ToListAsync(ct);
        }

        return ApiResponse<StudentHistoryDto>.Ok(new StudentHistoryDto
        {
            StudentId = student.Id,
            StudentCode = student.StudentCode,
            FullName = student.FullName,
            Status = student.Status,
            EnrollmentDate = student.EnrollmentDate,
            EnrollmentHistory = enrollmentHistory,
            AttendanceSummary = attendanceSummary,
            Requests = requests,
            LunchSummary = lunchSummary,
            AuditLogs = auditLogs
        });
    }

    private static bool IsActive(Student student) => string.Equals(student.Status, "Active", StringComparison.OrdinalIgnoreCase);

    private async Task<ApiResponse<StudentProfileDto>> ReloadProfileAsync(int studentId, CancellationToken ct)
    {
        var reloaded = await _unitOfWork.Students.GetStudentWithProfileAsync(studentId, ct);
        return reloaded == null
            ? ApiResponse<StudentProfileDto>.Fail(SystemMessages.MSG11)
            : ApiResponse<StudentProfileDto>.Ok(MapToStudentProfileDto(reloaded), SystemMessages.MSG08);
    }

    #endregion

    #region Helper Methods

    /// <summary>
    /// UC-09 A3 / BR-66 for forms: finds the guardian record to reuse by exact identity card number or guardian-record
    /// email (never phone). Returns an error text for an invalid email or a conflict.
    /// </summary>
    private async Task<(GuardianMatch Match, string? Error)> MatchGuardianAsync(GuardianInputDto input, CancellationToken ct)
    {
        if (!string.IsNullOrWhiteSpace(input.Email) && !GuardianMatcher.IsValidEmail(input.Email))
        {
            return (new GuardianMatch(GuardianMatchKind.None, null, null), "Email người giám hộ không hợp lệ.");
        }

        if (!string.IsNullOrWhiteSpace(input.PhoneNumber))
        {
            var phone = input.PhoneNumber.Trim();
            if (!System.Text.RegularExpressions.Regex.IsMatch(phone, @"^0\d{9}$"))
            {
                return (new GuardianMatch(GuardianMatchKind.None, null, null), "Số điện thoại người giám hộ không hợp lệ. Số điện thoại phải gồm đúng 10 chữ số và bắt đầu bằng số 0 (ví dụ: 0912345678).");
            }
        }

        if (!StudentValidationHelper.IsValidIdentityCard(input.IdentityCardNumber))
        {
            return (new GuardianMatch(GuardianMatchKind.None, null, null), "Số CCCD/CMND người giám hộ không hợp lệ (bắt buộc 9 hoặc 12 chữ số).");
        }

        var id = GuardianMatcher.NormalizeIdentityCard(input.IdentityCardNumber);
        var mail = GuardianMatcher.NormalizeEmail(input.Email);
        var candidates = id.Length == 0 && mail.Length == 0
            ? new List<Guardian>()
            : await _unitOfWork.Repository<Guardian>().Query()
                .Where(g => (id != "" && g.IdentityCardNumber != null && g.IdentityCardNumber.Replace(" ", "").ToUpper() == id)
                            || (mail != "" && g.Email != null && g.Email.ToLower() == mail))
                .ToListAsync(ct);

        var match = GuardianMatcher.Match(input.FullName, input.Email, input.IdentityCardNumber, candidates);
        return match.Kind == GuardianMatchKind.Conflict ? (match, match.Note) : (match, null);
    }

    /// <summary>The reused record (only its empty fields are filled) or a new guardian record; never a user account.</summary>
    private async Task<Guardian> CreateOrReuseGuardianAsync(GuardianInputDto input, GuardianMatch match, CancellationToken ct)
    {
        if (match.Kind == GuardianMatchKind.Reused && match.Record != null)
        {
            var record = match.Record;
            GuardianMatcher.FillEmptyFields(record, input.PhoneNumber, input.Email, input.IdentityCardNumber);
            if (string.IsNullOrWhiteSpace(record.Occupation) && !string.IsNullOrWhiteSpace(input.Occupation)) record.Occupation = input.Occupation.Trim();
            if (string.IsNullOrWhiteSpace(record.Address) && !string.IsNullOrWhiteSpace(input.Address)) record.Address = input.Address.Trim();
            return record;
        }

        var guardian = new Guardian
        {
            FullName = input.FullName.Trim(),
            PhoneNumber = string.IsNullOrWhiteSpace(input.PhoneNumber) ? null : input.PhoneNumber.Trim(),
            Email = string.IsNullOrWhiteSpace(input.Email) ? null : input.Email.Trim(),
            IdentityCardNumber = input.IdentityCardNumber?.Trim() ?? string.Empty,
            Occupation = input.Occupation?.Trim(),
            Address = input.Address?.Trim(),
            CreatedAt = DateTime.UtcNow
        };
        await _unitOfWork.Repository<Guardian>().AddAsync(guardian, ct);
        await _unitOfWork.SaveChangesAsync(ct);
        return guardian;
    }

    /// <summary>
    /// Adds the student-guardian link if missing; a primary link makes the student's other links non-primary (BR-13).
    /// <paramref name="isPrimaryRequest"/> null = primary only when the student has no primary guardian yet.
    /// </summary>
    private async Task LinkGuardianRecordAsync(int studentId, Guardian guardian, int relationship, bool? isPrimaryRequest, CancellationToken ct)
    {
        var repo = _unitOfWork.Repository<StudentGuardian>();
        var links = (await repo.FindAsync(sg => sg.StudentId == studentId, ct)).ToList();
        if (links.Any(sg => sg.GuardianId == guardian.Id))
        {
            return;
        }

        var isPrimary = isPrimaryRequest ?? !links.Any(sg => sg.IsPrimaryContact);

        if (isPrimary)
        {
            foreach (var other in links.Where(sg => sg.IsPrimaryContact))
            {
                other.IsPrimaryContact = false;
            }
        }

        await repo.AddAsync(new StudentGuardian
        {
            StudentId = studentId,
            GuardianId = guardian.Id,
            Relationship = ToRelationship(relationship),
            IsPrimaryContact = isPrimary
        }, ct);
    }

    private static RelationshipTypes ToRelationship(int value) =>
        Enum.IsDefined(typeof(RelationshipTypes), value) ? (RelationshipTypes)value : RelationshipTypes.Guardian;

    private static StudentProfileDto MapToStudentProfileDto(Student student)
    {
        var activeAssignment = student.ClassAssignments.FirstOrDefault(ca => ca.IsActive);
        var activeClass = activeAssignment?.Class;
        var primaryGuardian = student.StudentGuardians.FirstOrDefault(sg => sg.IsPrimaryContact)?.Guardian
                              ?? student.StudentGuardians.FirstOrDefault()?.Guardian;

        return new StudentProfileDto
        {
            Id = student.Id,
            StudentCode = student.StudentCode,
            FullName = student.FullName,
            DateOfBirth = student.DateOfBirth,
            Gender = student.Gender,
            EnrollmentDate = student.EnrollmentDate,
            Status = student.Status,
            AvatarUrl = student.AvatarUrl,
            Address = student.ResidentialArea ?? primaryGuardian?.Address,
            ResidentialArea = student.ResidentialArea,
            IsSpecialNeeds = student.IsSpecialNeeds,
            SpecialNeedsNotes = student.SpecialNeedsNotes,
            CurrentClassId = activeClass?.Id,
            CurrentClassName = activeClass?.ClassName,
            CurrentGrade = activeClass?.Grade,
            CurrentRoomNumber = activeClass?.RoomNumber,
            HomeroomTeacherName = activeClass?.HomeroomTeacher?.User?.FullName,
            PrimaryGuardianName = primaryGuardian?.FullName,
            PrimaryGuardianPhone = primaryGuardian?.PhoneNumber,
            GuardiansCount = student.StudentGuardians.Count,
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
                RelationshipName = sg.Relationship.ToString(),
                IsPrimaryContact = sg.IsPrimaryContact
            }).ToList()
        };
    }

    #endregion
}
