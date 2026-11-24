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

        var items = await baseQuery
            .Include(s => s.StudentGuardians)
                .ThenInclude(sg => sg.Guardian)
            .Include(s => s.ClassAssignments.Where(ca => ca.IsActive))
                .ThenInclude(ca => ca.Class)
                    .ThenInclude(c => c.HomeroomTeacher)
                        .ThenInclude(t => t!.User)
            .AsSplitQuery()
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

        if (!System.Text.RegularExpressions.Regex.IsMatch(request.StudentCode.Trim(), @"^[a-zA-Z0-9_\-]{3,30}$"))
        {
            return ApiResponse<StudentProfileDto>.Fail(
                SystemMessages.MSG01,
                new List<string> { "Mã học sinh chỉ được chứa chữ cái, chữ số, dấu gạch ngang (-) và gạch dưới (_) từ 3 đến 30 ký tự." });
        }

        if (request.FullName.Contains('<') || request.FullName.Contains('>'))
        {
            return ApiResponse<StudentProfileDto>.Fail(
                SystemMessages.MSG01,
                new List<string> { "Họ và tên học sinh không được chứa ký tự đặc biệt hoặc thẻ HTML." });
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

        // 3. Date of birth & Age validation (BR-01, 5 to 15 years old)
        if (request.DateOfBirth != default)
        {
            if (request.DateOfBirth.Year < 1900 || request.DateOfBirth >= DateTime.UtcNow.Date.AddDays(1))
            {
                return ApiResponse<StudentProfileDto>.Fail(
                    SystemMessages.MSG01,
                    new List<string> { "Ngày sinh không hợp lệ (không thể là ngày trong tương lai)." });
            }

            var age = DateTime.UtcNow.Year - request.DateOfBirth.Year;
            if (age < 5 || age > 15)
            {
                return ApiResponse<StudentProfileDto>.Fail(
                    SystemMessages.MSG01,
                    new List<string> { $"Độ tuổi học sinh tiểu học không hợp lệ ({age} tuổi). Tuổi hợp lệ phải từ 5 đến 15 tuổi." });
            }
        }

        // Guardian checked before anything is saved: a conflicting identity card number / email stops the creation
        GuardianMatch? guardianMatch = null;
        if (request.InitialGuardian != null && !string.IsNullOrWhiteSpace(request.InitialGuardian.FullName))
        {
            var (match, error) = await MatchGuardianAsync(request.InitialGuardian, cancellationToken);
            if (error != null)
            {
                return ApiResponse<StudentProfileDto>.Fail(SystemMessages.MSG01, new List<string> { error });
            }
            guardianMatch = match;
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
                        StartDate = DateTime.UtcNow.Date,
                        IsActive = true
                    };
                    await _unitOfWork.Repository<ClassAssignment>().AddAsync(assignment, cancellationToken);
                }
            }
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
            new { student.StudentCode, student.FullName, student.DateOfBirth, student.Gender, student.Status },
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

        if (request.DateOfBirth >= DateTime.UtcNow.Date.AddDays(1))
        {
            return ApiResponse<StudentProfileDto>.Fail(
                SystemMessages.MSG01,
                new List<string> { "Ngày sinh không hợp lệ (không thể là ngày trong tương lai)." });
        }

        var updateAge = DateTime.UtcNow.Year - request.DateOfBirth.Year;
        if (updateAge < 5 || updateAge > 15)
        {
            return ApiResponse<StudentProfileDto>.Fail(
                SystemMessages.MSG01,
                new List<string> { $"Độ tuổi học sinh tiểu học không hợp lệ ({updateAge} tuổi). Tuổi hợp lệ phải từ 5 đến 15 tuổi." });
        }

        if (request.FullName.Contains('<') || request.FullName.Contains('>'))
        {
            return ApiResponse<StudentProfileDto>.Fail(
                SystemMessages.MSG01,
                new List<string> { "Họ và tên học sinh không được chứa ký tự đặc biệt hoặc thẻ HTML." });
        }

        var validUpdateGenders = new[] { "Nam", "Nữ", "Male", "Female" };
        if (!string.IsNullOrWhiteSpace(request.Gender) && !validUpdateGenders.Contains(request.Gender.Trim(), StringComparer.OrdinalIgnoreCase))
        {
            return ApiResponse<StudentProfileDto>.Fail(
                SystemMessages.MSG01,
                new List<string> { "Giới tính không hợp lệ. Vui lòng chọn 'Nam' hoặc 'Nữ'." });
        }

        var oldValues = new { student.FullName, student.DateOfBirth, student.Gender, student.Status };

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
            var activeAssignments = student.ClassAssignments.Where(ca => ca.IsActive).ToList();
            if (activeAssignments.Count == 0 || activeAssignments.Any(ca => ca.ClassId != request.CurrentClassId.Value))
            {
                var targetClass = await _unitOfWork.Classes.GetClassWithDetailsAsync(request.CurrentClassId.Value, cancellationToken);
                if (targetClass != null)
                {
                    var enrolledCount = targetClass.ClassAssignments.Count(ca => ca.IsActive);
                    if (enrolledCount >= targetClass.Capacity)
                    {
                        return ApiResponse<StudentProfileDto>.Fail(
                            SystemMessages.MSG01,
                            new List<string> { $"Lớp {targetClass.ClassName} đã đủ sĩ số tối đa ({targetClass.Capacity} học sinh). Không thể chuyển học sinh vào lớp này." });
                    }

                    foreach (var oldCa in activeAssignments)
                    {
                        oldCa.IsActive = false;
                        oldCa.EndDate = DateTime.UtcNow.Date;
                        await _unitOfWork.Repository<ClassAssignment>().UpdateAsync(oldCa, cancellationToken);
                    }

                    var newAssignment = new ClassAssignment
                    {
                        ClassId = targetClass.Id,
                        StudentId = student.Id,
                        AcademicYear = targetClass.SchoolYear?.YearName ?? DateTime.UtcNow.Year.ToString(),
                        StartDate = DateTime.UtcNow.Date,
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
            AuditActions.StudentUpdate,
            nameof(Student),
            student.Id.ToString(),
            oldValues,
            new { student.FullName, student.DateOfBirth, student.Gender, student.Status },
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

            // If initialClassId provided, assign class if not already assigned
            if (profileData.InitialClassId.HasValue)
            {
                var activeClass = existing.ClassAssignments.FirstOrDefault(ca => ca.IsActive);
                if (activeClass == null)
                {
                    var targetClass = await _unitOfWork.Classes.GetClassWithDetailsAsync(profileData.InitialClassId.Value, cancellationToken);
                    if (targetClass != null)
                    {
                        var assignment = new ClassAssignment
                        {
                            ClassId = targetClass.Id,
                            StudentId = existing.Id,
                            AcademicYear = targetClass.SchoolYear?.YearName ?? DateTime.UtcNow.Year.ToString(),
                            StartDate = DateTime.UtcNow.Date,
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
