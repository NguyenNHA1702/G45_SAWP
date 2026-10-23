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
        var query = _unitOfWork.Repository<Student>()
            .Query()
            .Include(s => s.StudentGuardians)

                .ThenInclude(sg => sg.Guardian)
                    .ThenInclude(g => g.User)
            .Include(s => s.ClassAssignments.Where(ca => ca.IsActive))
                .ThenInclude(ca => ca.Class)
                    .ThenInclude(c => c.HomeroomTeacher)
                        .ThenInclude(t => t!.User)
            .AsQueryable();

        if (!string.IsNullOrWhiteSpace(filter.SearchTerm))
        {
            var term = filter.SearchTerm.Trim().ToLower();
            query = query.Where(s => s.StudentCode.ToLower().Contains(term) || s.FullName.ToLower().Contains(term));
        }

        if (!string.IsNullOrWhiteSpace(filter.Status))
        {
            query = query.Where(s => s.Status == filter.Status);
        }

        if (filter.ClassId.HasValue)
        {
            query = query.Where(s => s.ClassAssignments.Any(ca => ca.IsActive && ca.ClassId == filter.ClassId.Value));
        }

        if (filter.Grade.HasValue)
        {
            query = query.Where(s => s.ClassAssignments.Any(ca => ca.IsActive && ca.Class.Grade == filter.Grade.Value));
        }

        if (filter.SchoolYearId.HasValue)
        {
            query = query.Where(s => s.ClassAssignments.Any(ca => ca.IsActive && ca.Class.SchoolYearId == filter.SchoolYearId.Value));
        }

        var totalItems = await query.CountAsync(cancellationToken);

        var pageIndex = filter.PageIndex <= 0 ? 1 : filter.PageIndex;
        var pageSize = filter.PageSize <= 0 ? 20 : filter.PageSize;

        var items = await query
            .OrderByDescending(s => s.CreatedAt)
            .Skip((pageIndex - 1) * pageSize)
            .Take(pageSize)
            .ToListAsync(cancellationToken);

        var dtoList = items.Select(MapToStudentProfileDto).ToList();

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

        // 2. Duplicate Student Code Check (BR-09, MSG12)
        var exists = await _unitOfWork.Students.ExistsByStudentCodeAsync(request.StudentCode.Trim(), null, cancellationToken);
        if (exists)
        {
            return ApiResponse<StudentProfileDto>.Fail(SystemMessages.MSG12);
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
        if (request.InitialClassId.HasValue)
        {
            var targetClass = await _unitOfWork.Classes.GetClassWithDetailsAsync(request.InitialClassId.Value, cancellationToken);
            if (targetClass != null)
            {
                var enrolledCount = targetClass.ClassAssignments.Count(ca => ca.IsActive);
                if (enrolledCount < targetClass.Capacity)
                {
                    var assignment = new ClassAssignment
                    {
                        ClassId = targetClass.Id,
                        StudentId = student.Id,
                        AcademicYear = targetClass.SchoolYear?.YearName ?? DateTime.UtcNow.Year.ToString(),
                        StartDate = DateTime.UtcNow,
                        IsActive = true
                    };
                    await _unitOfWork.Repository<ClassAssignment>().AddAsync(assignment, cancellationToken);
                }
            }
        }

        // 5. Initial Guardian if specified
        if (request.InitialGuardian != null && !string.IsNullOrWhiteSpace(request.InitialGuardian.FullName))
        {
            await CreateOrLinkGuardianAsync(student.Id, request.InitialGuardian, cancellationToken);
        }

        await _unitOfWork.SaveChangesAsync(cancellationToken);

        await _auditService.LogActionAsync(
            actorUserId,
            "CreateStudent",
            nameof(Student),
            student.Id.ToString(),
            null,
            $"Created Master Profile: {student.StudentCode} - {student.FullName}",
            null,
            cancellationToken);

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

        var oldValues = $"Name: {student.FullName}, Status: {student.Status}, Gender: {student.Gender}";

        student.FullName = request.FullName.Trim();
        student.DateOfBirth = request.DateOfBirth;
        student.Gender = request.Gender.Trim();
        student.Status = request.Status;
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
            var currentActiveAssignment = student.ClassAssignments.FirstOrDefault(ca => ca.IsActive);
            if (currentActiveAssignment == null || currentActiveAssignment.ClassId != request.CurrentClassId.Value)
            {
                if (currentActiveAssignment != null)
                {
                    currentActiveAssignment.IsActive = false;
                    currentActiveAssignment.EndDate = DateTime.UtcNow;
                    await _unitOfWork.Repository<ClassAssignment>().UpdateAsync(currentActiveAssignment, cancellationToken);
                }

                var targetClass = await _unitOfWork.Classes.GetClassWithDetailsAsync(request.CurrentClassId.Value, cancellationToken);
                if (targetClass != null)
                {
                    var newAssignment = new ClassAssignment
                    {
                        ClassId = targetClass.Id,
                        StudentId = student.Id,
                        AcademicYear = targetClass.SchoolYear?.YearName ?? DateTime.UtcNow.Year.ToString(),
                        StartDate = DateTime.UtcNow,
                        IsActive = true
                    };
                    await _unitOfWork.Repository<ClassAssignment>().AddAsync(newAssignment, cancellationToken);
                }
            }
        }

        await _unitOfWork.Students.UpdateAsync(student, cancellationToken);
        await _unitOfWork.SaveChangesAsync(cancellationToken);

        await _auditService.LogActionAsync(
            actorUserId,
            "UpdateStudent",
            nameof(Student),
            student.Id.ToString(),
            oldValues,
            $"Name: {student.FullName}, Status: {student.Status}, Gender: {student.Gender}",
            null,
            cancellationToken);

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
        if (string.IsNullOrWhiteSpace(request.StudentCode))
        {
            return ApiResponse<StudentProfileDto>.Fail(SystemMessages.MSG01, new List<string> { "Mã học sinh là bắt buộc." });
        }

        var normalizedCode = request.StudentCode.Trim().ToUpperInvariant();
        var existing = await _unitOfWork.Students.GetByStudentCodeAsync(normalizedCode, cancellationToken);

        if (string.Equals(request.Action, "Rename", StringComparison.OrdinalIgnoreCase))
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
            var createDto = request.ProfileData ?? new CreateStudentDto();
            createDto.StudentCode = newNormalizedCode;
            return await CreateStudentAsync(createDto, actorUserId, cancellationToken);
        }

        if (existing == null)
        {
            // No conflict in DB, can create fresh
            var createDto = request.ProfileData ?? new CreateStudentDto();
            createDto.StudentCode = normalizedCode;
            return await CreateStudentAsync(createDto, actorUserId, cancellationToken);
        }

        // Action: "Merge" or "Overwrite"
        var oldValues = $"Name: {existing.FullName}, DOB: {existing.DateOfBirth:yyyy-MM-dd}, Gender: {existing.Gender}, Status: {existing.Status}";

        if (request.ProfileData != null)
        {
            if (!string.IsNullOrWhiteSpace(request.ProfileData.FullName))
            {
                existing.FullName = request.ProfileData.FullName.Trim();
            }

            if (request.ProfileData.DateOfBirth != default)
            {
                existing.DateOfBirth = request.ProfileData.DateOfBirth;
            }

            if (!string.IsNullOrWhiteSpace(request.ProfileData.Gender))
            {
                existing.Gender = request.ProfileData.Gender.Trim();
            }

            if (!string.IsNullOrWhiteSpace(request.ProfileData.AvatarUrl))
            {
                existing.AvatarUrl = request.ProfileData.AvatarUrl;
            }

            // Set Student Status = 'Active' per workflow diagram
            existing.Status = "Active";

            // If guardian info provided, merge guardian
            if (request.ProfileData.InitialGuardian != null && !string.IsNullOrWhiteSpace(request.ProfileData.InitialGuardian.FullName))
            {
                await CreateOrLinkGuardianAsync(existing.Id, request.ProfileData.InitialGuardian, cancellationToken);
            }

            // If initialClassId provided, assign class if not already assigned
            if (request.ProfileData.InitialClassId.HasValue)
            {
                var activeClass = existing.ClassAssignments.FirstOrDefault(ca => ca.IsActive);
                if (activeClass == null)
                {
                    var targetClass = await _unitOfWork.Classes.GetClassWithDetailsAsync(request.ProfileData.InitialClassId.Value, cancellationToken);
                    if (targetClass != null)
                    {
                        var assignment = new ClassAssignment
                        {
                            ClassId = targetClass.Id,
                            StudentId = existing.Id,
                            AcademicYear = targetClass.SchoolYear?.YearName ?? DateTime.UtcNow.Year.ToString(),
                            StartDate = DateTime.UtcNow,
                            IsActive = true
                        };
                        await _unitOfWork.Repository<ClassAssignment>().AddAsync(assignment, cancellationToken);
                    }
                }
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
            $"ResolveDuplicate_{request.Action}",
            nameof(Student),
            existing.Id.ToString(),
            oldValues,
            $"Resolved duplicate profile for code {normalizedCode} via {request.Action}",
            null,
            cancellationToken);

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
            "ActivateStudent",
            nameof(Student),
            student.Id.ToString(),
            null,
            $"Set status = 'Active' for {student.StudentCode}",
            null,
            cancellationToken);

        return ApiResponse<bool>.Ok(true, SystemMessages.MSG08);
    }

    #region Helper Methods

    private async Task CreateOrLinkGuardianAsync(int studentId, GuardianInputDto input, CancellationToken cancellationToken)
    {
        // Check if user account or guardian profile exists by phone number or username
        var phone = input.PhoneNumber.Trim();
        var user = await _unitOfWork.Repository<User>().FindAsync(u => u.PhoneNumber == phone || u.Username == phone, cancellationToken);
        var existingUser = user.FirstOrDefault();

        Guardian guardian;
        if (existingUser != null)
        {
            var guardianProfile = (await _unitOfWork.Repository<Guardian>().FindAsync(g => g.UserId == existingUser.Id, cancellationToken)).FirstOrDefault();
            if (guardianProfile != null)
            {
                guardian = guardianProfile;
            }
            else
            {
                guardian = new Guardian
                {
                    UserId = existingUser.Id,
                    IdentityCardNumber = input.IdentityCardNumber?.Trim() ?? string.Empty,
                    Occupation = input.Occupation?.Trim(),
                    Address = input.Address?.Trim(),
                    CreatedAt = DateTime.UtcNow
                };
                await _unitOfWork.Repository<Guardian>().AddAsync(guardian, cancellationToken);
                await _unitOfWork.SaveChangesAsync(cancellationToken);
            }
        }
        else
        {
            // Create user for parent with default password
            var passwordHash = BCrypt.Net.BCrypt.HashPassword("Password@123");
            var newUser = new User
            {
                Username = !string.IsNullOrWhiteSpace(phone) ? phone : $"parent_{Guid.NewGuid().ToString("N")[..8]}",
                Email = $"{phone}@parent.sawp.edu.vn",
                FullName = input.FullName.Trim(),
                PhoneNumber = phone,
                PasswordHash = passwordHash,
                IsActive = true,
                CreatedAt = DateTime.UtcNow
            };
            await _unitOfWork.Repository<User>().AddAsync(newUser, cancellationToken);
            await _unitOfWork.SaveChangesAsync(cancellationToken);

            var parentRoleId = (await _unitOfWork.Repository<Role>().FindAsync(r => r.Name == UserRoles.Parent.ToString(), cancellationToken))
                .Select(r => r.Id).FirstOrDefault();

            if (parentRoleId > 0)
            {
                await _unitOfWork.Repository<UserRole>().AddAsync(new UserRole
                {
                    UserId = newUser.Id,
                    RoleId = parentRoleId
                }, cancellationToken);
            }

            guardian = new Guardian
            {
                UserId = newUser.Id,
                IdentityCardNumber = input.IdentityCardNumber?.Trim() ?? string.Empty,
                Occupation = input.Occupation?.Trim(),
                Address = input.Address?.Trim(),
                CreatedAt = DateTime.UtcNow
            };
            await _unitOfWork.Repository<Guardian>().AddAsync(guardian, cancellationToken);
            await _unitOfWork.SaveChangesAsync(cancellationToken);
        }

        // Link student to guardian
        var existingLink = (await _unitOfWork.Repository<StudentGuardian>()
            .FindAsync(sg => sg.StudentId == studentId && sg.GuardianId == guardian.Id, cancellationToken)).FirstOrDefault();

        if (existingLink == null)
        {
            var relType = Enum.IsDefined(typeof(RelationshipTypes), input.Relationship)
                ? (RelationshipTypes)input.Relationship
                : RelationshipTypes.Guardian;

            var link = new StudentGuardian
            {
                StudentId = studentId,
                GuardianId = guardian.Id,
                Relationship = relType,
                IsPrimaryContact = input.IsPrimaryContact
            };
            await _unitOfWork.Repository<StudentGuardian>().AddAsync(link, cancellationToken);
        }
    }

    private static StudentProfileDto MapToStudentProfileDto(Student student)
    {
        var activeAssignment = student.ClassAssignments.FirstOrDefault(ca => ca.IsActive);
        var activeClass = activeAssignment?.Class;

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
            ResidentialArea = student.ResidentialArea,
            IsSpecialNeeds = student.IsSpecialNeeds,
            SpecialNeedsNotes = student.SpecialNeedsNotes,
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
                FullName = sg.Guardian.User?.FullName ?? string.Empty,
                PhoneNumber = sg.Guardian.User?.PhoneNumber,
                Email = sg.Guardian.User?.Email,
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
