using ClosedXML.Excel;
using Core.Constants;
using Core.DTOs;
using Core.Entities;
using Core.Enums;
using Core.Interfaces;
using Microsoft.EntityFrameworkCore;
using Services.Interfaces;

namespace Services.Implementations;

public class StudentImporter : IStudentImporter
{
    private readonly IUnitOfWork _unitOfWork;
    private readonly IAuditService _auditService;

    public StudentImporter(IUnitOfWork unitOfWork, IAuditService auditService)
    {
        _unitOfWork = unitOfWork;
        _auditService = auditService;
    }

    public async Task<ApiResponse<List<ImportStudentRowDto>>> ParseExcelAsync(Stream fileStream, CancellationToken cancellationToken = default)
    {
        try
        {
            var rows = new List<ImportStudentRowDto>();
            using var workbook = new XLWorkbook(fileStream);
            var worksheet = workbook.Worksheets.FirstOrDefault();

            if (worksheet == null)
            {
                return ApiResponse<List<ImportStudentRowDto>>.Fail(SystemMessages.MSG41, new List<string> { "Tệp Excel không chứa trang tính (sheet) nào." });
            }

            var range = worksheet.RangeUsed();
            if (range == null || range.RowCount() < 2)
            {
                return ApiResponse<List<ImportStudentRowDto>>.Fail(SystemMessages.MSG49, new List<string> { "Tệp Excel không có dòng dữ liệu hợp lệ nào." });
            }

            var existingCodesInDb = (await _unitOfWork.Repository<Student>().GetAllAsync(cancellationToken))
                .Select(s => s.StudentCode.ToUpperInvariant())
                .ToHashSet();

            var seenCodesInFile = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

            for (int r = 2; r <= range.RowCount(); r++)
            {
                var row = worksheet.Row(r);
                var studentCode = row.Cell(1).GetString()?.Trim().ToUpperInvariant() ?? string.Empty;
                var fullName = CapitalizeWords(row.Cell(2).GetString()?.Trim());
                var dobRaw = row.Cell(3).GetString()?.Trim() ?? string.Empty;
                var gender = row.Cell(4).GetString()?.Trim() ?? "Nam";
                var className = row.Cell(5).GetString()?.Trim() ?? string.Empty;
                var guardianName = CapitalizeWords(row.Cell(6).GetString()?.Trim());
                var guardianPhone = row.Cell(7).GetString()?.Trim() ?? string.Empty;
                var guardianIdCard = row.Cell(8).GetString()?.Trim() ?? string.Empty;
                var relationship = row.Cell(9).GetString()?.Trim() ?? "Phụ huynh";

                // Check if row is entirely empty
                if (string.IsNullOrWhiteSpace(studentCode) && string.IsNullOrWhiteSpace(fullName))
                {
                    continue;
                }

                var item = new ImportStudentRowDto
                {
                    RowNumber = r,
                    StudentCode = studentCode,
                    FullName = fullName,
                    DateOfBirth = dobRaw,
                    Gender = gender,
                    ClassName = className,
                    GuardianName = guardianName,
                    GuardianPhone = guardianPhone,
                    GuardianIdentityCard = guardianIdCard,
                    Relationship = relationship,
                    IsValid = true,
                    IsDuplicate = false
                };

                // Validate Required Fields
                if (string.IsNullOrWhiteSpace(studentCode))
                {
                    item.IsValid = false;
                    item.ErrorMessage = "Thiếu mã học sinh.";
                }
                else if (string.IsNullOrWhiteSpace(fullName))
                {
                    item.IsValid = false;
                    item.ErrorMessage = "Thiếu họ và tên học sinh.";
                }
                else if (seenCodesInFile.Contains(studentCode))
                {
                    // Duplicate in the file
                    item.IsValid = false;
                    item.IsDuplicate = true;
                    item.ErrorMessage = $"Mã học sinh '{studentCode}' bị trùng lặp trong tệp Excel.";
                }
                else if (existingCodesInDb.Contains(studentCode))
                {
                    // Duplicate in DB (Workflow: Duplicate Student Identifier Detected)
                    item.IsDuplicate = true;
                    item.ErrorMessage = $"Mã học sinh '{studentCode}' đã tồn tại trong cơ sở dữ liệu hệ thống.";
                }
                else
                {
                    seenCodesInFile.Add(studentCode);
                }

                rows.Add(item);
            }

            if (!rows.Any())
            {
                return ApiResponse<List<ImportStudentRowDto>>.Fail(SystemMessages.MSG49, new List<string> { "Tệp Excel không có dòng dữ liệu hợp lệ nào." });
            }

            return ApiResponse<List<ImportStudentRowDto>>.Ok(rows);
        }
        catch (Exception ex)
        {
            return ApiResponse<List<ImportStudentRowDto>>.Fail(SystemMessages.MSG41, new List<string> { $"Không thể xử lý tệp Excel: {ex.Message}" });
        }
    }

    public async Task<ApiResponse<ImportStudentsResultDto>> ImportStudentsAsync(
        ImportStudentsConfirmDto request,
        int? actorUserId = null,
        CancellationToken cancellationToken = default)
    {
        if (request.ValidRows == null || !request.ValidRows.Any())
        {
            return ApiResponse<ImportStudentsResultDto>.Fail(SystemMessages.MSG49, new List<string> { "Không có bản ghi hợp lệ nào để nhập." });
        }

        var result = new ImportStudentsResultDto
        {
            TotalRows = request.ValidRows.Count
        };

        var schoolYear = request.SchoolYearId.HasValue
            ? await _unitOfWork.Repository<SchoolYear>().GetByIdAsync(request.SchoolYearId.Value, cancellationToken)
            : await _unitOfWork.Classes.GetActiveSchoolYearAsync(cancellationToken);

        var schoolYearId = schoolYear?.Id ?? 0;
        var schoolYearName = schoolYear?.YearName ?? DateTime.UtcNow.Year.ToString();

        var classesInYear = schoolYearId > 0
            ? await _unitOfWork.Classes.GetClassesBySchoolYearAsync(schoolYearId, null, null, cancellationToken)
            : new List<Class>();

        var classMap = classesInYear.ToDictionary(c => c.ClassName, c => c, StringComparer.OrdinalIgnoreCase);
        var enrolledCountByClassId = classesInYear.ToDictionary(c => c.Id, c => c.ClassAssignments?.Count(ca => ca.IsActive) ?? 0);

        var parentRole = (await _unitOfWork.Repository<Role>().FindAsync(r => r.Name == UserRoles.Parent.ToString(), cancellationToken)).FirstOrDefault();
        var defaultPasswordHash = BCrypt.Net.BCrypt.HashPassword("Password@123");

        // Tối ưu hóa hiệu năng: Cache dữ liệu kiểm tra trùng lặp để xử lý mượt mà lên tới 5,000 học sinh
        var studentRepo = _unitOfWork.Repository<Student>();
        var existingStudents = studentRepo != null ? await studentRepo.GetAllAsync(cancellationToken) : null;
        var existingCodesInDb = existingStudents != null
            ? existingStudents.Select(s => s.StudentCode.ToUpperInvariant()).ToHashSet()
            : new HashSet<string>();
        bool checkCodeWithRepo = existingStudents == null;

        var userRepo = _unitOfWork.Repository<User>();
        var existingParents = userRepo != null ? await userRepo.GetAllAsync(cancellationToken) : null;
        var userByPhone = existingParents != null
            ? existingParents.Where(u => !string.IsNullOrEmpty(u.PhoneNumber)).ToDictionary(u => u.PhoneNumber!, u => u, StringComparer.OrdinalIgnoreCase)
            : new Dictionary<string, User>(StringComparer.OrdinalIgnoreCase);
        var userByUsername = existingParents != null
            ? existingParents.ToDictionary(u => u.Username, u => u, StringComparer.OrdinalIgnoreCase)
            : new Dictionary<string, User>(StringComparer.OrdinalIgnoreCase);

        var guardianRepo = _unitOfWork.Repository<Guardian>();
        var allGuardians = guardianRepo != null ? await guardianRepo.GetAllAsync(cancellationToken) : null;
        var guardianByUserId = allGuardians != null
            ? allGuardians.ToDictionary(g => g.UserId, g => g)
            : new Dictionary<int, Guardian>();

        await _unitOfWork.BeginTransactionAsync(cancellationToken);
        try
        {
            const int chunkSize = 500;
            var validRowsList = request.ValidRows.Where(r => r.IsValid && !r.IsDuplicate).ToList();

            for (int i = 0; i < validRowsList.Count; i += chunkSize)
            {
                var chunk = validRowsList.Skip(i).Take(chunkSize).ToList();
                var createdStudents = new List<(ImportStudentRowDto Row, Student Student)>();

                // 1. Thêm danh sách Student trong chunk
                foreach (var row in chunk)
                {
                    var code = row.StudentCode.Trim().ToUpperInvariant();
                    var isCodeExisting = existingCodesInDb.Contains(code) || (checkCodeWithRepo && await _unitOfWork.Students.ExistsByStudentCodeAsync(code, null, cancellationToken));
                    if (isCodeExisting)
                    {
                        result.SkippedCount++;
                        result.Errors.Add($"Dòng {row.RowNumber}: Mã {code} đã tồn tại trong hệ thống, bỏ qua.");
                        continue;
                    }
                    existingCodesInDb.Add(code);

                    DateTime dob = DateTime.UtcNow.AddYears(-6);
                    if (!string.IsNullOrWhiteSpace(row.DateOfBirth) && DateTime.TryParse(row.DateOfBirth, out var parsedDob))
                    {
                        dob = parsedDob;
                    }

                    var student = new Student
                    {
                        StudentCode = code,
                        FullName = CapitalizeWords(row.FullName),
                        DateOfBirth = dob,
                        Gender = string.IsNullOrWhiteSpace(row.Gender) ? "Nam" : row.Gender.Trim(),
                        EnrollmentDate = DateTime.UtcNow,
                        Status = "Active", // Workflow step: Save Profiles & Set Student Status = 'Active'
                        ResidentialArea = row.ResidentialArea?.Trim(),
                        IsSpecialNeeds = row.IsSpecialNeeds,
                        SpecialNeedsNotes = row.SpecialNeedsNotes?.Trim(),
                        CreatedAt = DateTime.UtcNow
                    };

                    await _unitOfWork.Students.AddAsync(student, cancellationToken);
                    createdStudents.Add((row, student));
                }

                if (!createdStudents.Any())
                {
                    continue;
                }

                // Lưu các Student để nhận về student.Id từ DB
                await _unitOfWork.SaveChangesAsync(cancellationToken);

                // 2. Tạo User cho phụ huynh mới trong chunk
                var newUsersToCreate = new List<(User User, string IdCard)>();
                foreach (var (row, student) in createdStudents)
                {
                    if (string.IsNullOrWhiteSpace(row.GuardianName)) continue;
                    var phone = row.GuardianPhone?.Trim() ?? string.Empty;
                    var code = student.StudentCode;
                    var defaultUsername = !string.IsNullOrWhiteSpace(phone) ? phone : $"parent_{code.ToLowerInvariant()}";

                    bool userExists = (!string.IsNullOrWhiteSpace(phone) && userByPhone.ContainsKey(phone))
                                   || userByUsername.ContainsKey(defaultUsername);

                    if (!userExists)
                    {
                        var newUser = new User
                        {
                            Username = defaultUsername,
                            Email = !string.IsNullOrWhiteSpace(phone) ? $"{phone}@parent.sawp.edu.vn" : $"{code.ToLowerInvariant()}@parent.sawp.edu.vn",
                            FullName = CapitalizeWords(row.GuardianName),
                            PhoneNumber = phone,
                            PasswordHash = defaultPasswordHash,
                            IsActive = true,
                            CreatedAt = DateTime.UtcNow
                        };

                        await _unitOfWork.Repository<User>().AddAsync(newUser, cancellationToken);
                        userByUsername[defaultUsername] = newUser;
                        if (!string.IsNullOrWhiteSpace(phone))
                        {
                            userByPhone[phone] = newUser;
                        }
                        newUsersToCreate.Add((newUser, row.GuardianIdentityCard?.Trim() ?? string.Empty));
                    }
                }

                // Lưu các User mới để nhận về newUser.Id
                if (newUsersToCreate.Any())
                {
                    await _unitOfWork.SaveChangesAsync(cancellationToken);

                    foreach (var (newUser, idCard) in newUsersToCreate)
                    {
                        if (parentRole != null)
                        {
                            await _unitOfWork.Repository<UserRole>().AddAsync(new UserRole
                            {
                                UserId = newUser.Id,
                                RoleId = parentRole.Id
                            }, cancellationToken);
                        }

                        var g = new Guardian
                        {
                            UserId = newUser.Id,
                            IdentityCardNumber = idCard,
                            CreatedAt = DateTime.UtcNow
                        };
                        await _unitOfWork.Repository<Guardian>().AddAsync(g, cancellationToken);
                        guardianByUserId[newUser.Id] = g;
                    }

                    // Lưu các Guardian và UserRole mới để nhận về guardian.Id
                    await _unitOfWork.SaveChangesAsync(cancellationToken);
                }

                // 3. Phân lớp (ClassAssignment) và liên kết phụ huynh (StudentGuardian) với ID thực tế
                foreach (var (row, student) in createdStudents)
                {
                    // ClassAssignment
                    if (!string.IsNullOrWhiteSpace(row.ClassName) && classMap.TryGetValue(row.ClassName.Trim(), out var targetClass))
                    {
                        enrolledCountByClassId.TryGetValue(targetClass.Id, out var currentEnrolled);
                        if (currentEnrolled < targetClass.Capacity)
                        {
                            var assignment = new ClassAssignment
                            {
                                ClassId = targetClass.Id,
                                StudentId = student.Id,
                                AcademicYear = schoolYearName,
                                StartDate = DateTime.UtcNow,
                                IsActive = true
                            };
                            await _unitOfWork.Repository<ClassAssignment>().AddAsync(assignment, cancellationToken);
                            enrolledCountByClassId[targetClass.Id] = currentEnrolled + 1;
                        }
                    }

                    // StudentGuardian
                    if (!string.IsNullOrWhiteSpace(row.GuardianName))
                    {
                        var phone = row.GuardianPhone?.Trim() ?? string.Empty;
                        Guardian? guardian = null;

                        if (!string.IsNullOrWhiteSpace(phone) && userByPhone.TryGetValue(phone, out var user))
                        {
                            guardianByUserId.TryGetValue(user.Id, out guardian);
                        }
                        else if (userByUsername.TryGetValue($"parent_{student.StudentCode.ToLowerInvariant()}", out var userByName))
                        {
                            guardianByUserId.TryGetValue(userByName.Id, out guardian);
                        }

                        if (guardian == null && userRepo != null)
                        {
                            var u = (await userRepo.FindAsync(x => x.Username == $"parent_{student.StudentCode.ToLowerInvariant()}", cancellationToken)).FirstOrDefault();
                            if (u != null)
                            {
                                guardianByUserId.TryGetValue(u.Id, out guardian);
                            }
                        }

                        if (guardian != null)
                        {
                            if (!string.IsNullOrWhiteSpace(row.GuardianIdentityCard) && string.IsNullOrEmpty(guardian.IdentityCardNumber))
                            {
                                guardian.IdentityCardNumber = row.GuardianIdentityCard.Trim();
                            }

                            var link = new StudentGuardian
                            {
                                StudentId = student.Id,
                                GuardianId = guardian.Id,
                                Relationship = ParseRelationship(row.Relationship),
                                IsPrimaryContact = true
                            };
                            await _unitOfWork.Repository<StudentGuardian>().AddAsync(link, cancellationToken);
                        }
                    }

                    result.SuccessCount++;
                    result.ImportedStudentCodes.Add(student.StudentCode);
                }

                // Lưu các Assignment và StudentGuardian của chunk
                await _unitOfWork.SaveChangesAsync(cancellationToken);
            }

            await _auditService.LogActionAsync(
                actorUserId,
                AuditActions.StudentBulkImport,
                nameof(Student),
                null,
                null,
                new { result.SuccessCount, result.SkippedCount },
                null, // TODO: pass the client IP address; it is not recorded yet.
                cancellationToken);

            await _unitOfWork.SaveChangesAsync(cancellationToken);
            await _unitOfWork.CommitTransactionAsync(cancellationToken);

            return ApiResponse<ImportStudentsResultDto>.Ok(result, SystemMessages.MSG48);
        }
        catch (Exception ex)
        {
            await _unitOfWork.RollbackTransactionAsync(CancellationToken.None);
            return ApiResponse<ImportStudentsResultDto>.Fail(SystemMessages.MSG50, new List<string> { $"Lỗi trong quá trình nhập dữ liệu: {ex.Message}" });
        }
    }

    public async Task<byte[]> GenerateExcelTemplateAsync(CancellationToken cancellationToken = default)
    {
        await Task.CompletedTask;
        using var workbook = new XLWorkbook();
        var worksheet = workbook.Worksheets.Add("DanhSachHocSinh");

        // Headers
        var headers = new[]
        {
            "Mã học sinh (*)",
            "Họ và tên (*)",
            "Ngày sinh (YYYY-MM-DD)",
            "Giới tính (Nam/Nữ)",
            "Lớp dự kiến",
            "Họ tên phụ huynh",
            "SĐT phụ huynh",
            "CCCD phụ huynh",
            "Quan hệ (Bố/Mẹ/Người giám hộ)"
        };

        for (int i = 0; i < headers.Length; i++)
        {
            var cell = worksheet.Cell(1, i + 1);
            cell.Value = headers[i];
            cell.Style.Font.Bold = true;
            cell.Style.Fill.BackgroundColor = XLColor.FromHtml("#4F46E5");
            cell.Style.Font.FontColor = XLColor.White;
            cell.Style.Alignment.Horizontal = XLAlignmentHorizontalValues.Center;
        }

        // Sample Data Rows
        var sampleRows = new[]
        {
            new[] { "HS2026001", "Nguyễn Văn An", "2020-05-15", "Nam", "1A", "Nguyễn Văn Nam", "0912345678", "001200123456", "Bố" },
            new[] { "HS2026002", "Trần Thị Mai", "2020-08-22", "Nữ", "1A", "Trần Đình Khang", "0987654321", "001200987654", "Bố" },
            new[] { "HS2026003", "Lê Gia Bảo", "2020-03-10", "Nam", "1B", "Phạm Thu Hương", "0944556677", "001200334455", "Mẹ" }
        };

        for (int r = 0; r < sampleRows.Length; r++)
        {
            for (int c = 0; c < sampleRows[r].Length; c++)
            {
                worksheet.Cell(r + 2, c + 1).Value = sampleRows[r][c];
            }
        }

        worksheet.Columns().AdjustToContents();

        using var memoryStream = new MemoryStream();
        workbook.SaveAs(memoryStream);
        return memoryStream.ToArray();
    }

    private static RelationshipTypes ParseRelationship(string? text)
    {
        if (string.IsNullOrWhiteSpace(text)) return RelationshipTypes.Guardian;
        var lower = text.Trim().ToLowerInvariant();
        if (lower.Contains("bố") || lower.Contains("cha") || lower.Contains("father")) return RelationshipTypes.Father;
        if (lower.Contains("mẹ") || lower.Contains("mother")) return RelationshipTypes.Mother;
        return RelationshipTypes.Guardian;
    }

    private static string CapitalizeWords(string? input)
    {
        if (string.IsNullOrWhiteSpace(input)) return string.Empty;
        var words = input.Trim().Split(' ', StringSplitOptions.RemoveEmptyEntries);
        for (int i = 0; i < words.Length; i++)
        {
            var w = words[i].ToLower();
            if (w.Length > 0)
            {
                words[i] = char.ToUpper(w[0]) + (w.Length > 1 ? w.Substring(1) : string.Empty);
            }
        }
        return string.Join(" ", words);
    }
}
