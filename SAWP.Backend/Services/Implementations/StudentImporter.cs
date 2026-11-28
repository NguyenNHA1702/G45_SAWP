using ClosedXML.Excel;
using Core.Common;
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

            const int MaxRowsPerSheet = 5000;
            if (range.RowCount() - 1 > MaxRowsPerSheet)
            {
                return ApiResponse<List<ImportStudentRowDto>>.Fail(
                    SystemMessages.MSG01,
                    new List<string> { $"Tệp Excel vượt quá giới hạn tối đa {MaxRowsPerSheet} dòng mỗi lần nhập." });
            }

            var existingCodesInDb = (await _unitOfWork.Repository<Student>().GetAllAsync(cancellationToken))
                .Select(s => s.StudentCode.ToUpperInvariant())
                .ToHashSet();

            var seenCodesInFile = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

            // Guardian preview (UC-09 step 3): existing records plus the ones earlier rows of this file would create
            var knownGuardians = (await _unitOfWork.Repository<Guardian>().GetAllAsync(cancellationToken)).ToList();

            // Load active school year & classes for strict class existence / capacity / age-grade validation
            var activeSchoolYear = _unitOfWork.Classes != null
                ? await _unitOfWork.Classes.GetActiveSchoolYearAsync(cancellationToken)
                : null;
            var schoolYearStartYear = activeSchoolYear?.StartDate.Year ?? DateTime.UtcNow.Year;
            var classesInYear = activeSchoolYear != null
                ? await _unitOfWork.Classes!.GetClassesBySchoolYearAsync(activeSchoolYear.Id, null, null, cancellationToken)
                : new List<Class>();
            var classMap = classesInYear.ToDictionary(c => c.ClassName, c => c, StringComparer.OrdinalIgnoreCase);
            var previewEnrolledByClassId = classesInYear.ToDictionary(c => c.Id, c => c.ClassAssignments?.Count(ca => ca.IsActive) ?? 0);

            for (int r = 2; r <= range.RowCount(); r++)
            {
                var row = worksheet.Row(r);
                var rawCode = row.Cell(1).GetString()?.Trim() ?? string.Empty;
                var studentCode = rawCode.ToUpperInvariant();
                var rawFullName = row.Cell(2).GetString()?.Trim() ?? string.Empty;
                var fullName = CapitalizeWords(rawFullName);
                var dobCell = row.Cell(3);
                var dobRaw = dobCell.GetString()?.Trim() ?? string.Empty;
                var rawGender = row.Cell(4).GetString()?.Trim() ?? string.Empty;
                var className = row.Cell(5).GetString()?.Trim() ?? string.Empty;
                var rawGuardianName = row.Cell(6).GetString()?.Trim() ?? string.Empty;
                var guardianName = CapitalizeWords(rawGuardianName);
                var guardianPhone = row.Cell(7).GetString()?.Trim() ?? string.Empty;
                var guardianIdCard = row.Cell(8).GetString()?.Trim() ?? string.Empty;
                var relationship = row.Cell(9).GetString()?.Trim() ?? string.Empty;
                // Column 10 was added for SSAWP-152; older files without it still import (guardians without email)
                var guardianEmail = row.Cell(10).GetString()?.Trim() ?? string.Empty;

                // Check if row is entirely empty
                if (string.IsNullOrWhiteSpace(studentCode)
                    && string.IsNullOrWhiteSpace(fullName)
                    && string.IsNullOrWhiteSpace(dobRaw)
                    && string.IsNullOrWhiteSpace(rawGender)
                    && string.IsNullOrWhiteSpace(className)
                    && string.IsNullOrWhiteSpace(guardianName)
                    && string.IsNullOrWhiteSpace(guardianPhone))
                {
                    continue;
                }

                var dobParsedOk = TryParseExcelBirthDate(dobCell, out var parsedDob);
                var genderOk = StudentValidationHelper.TryNormalizeGender(rawGender, out var normalizedGender);

                var item = new ImportStudentRowDto
                {
                    RowNumber = r,
                    StudentCode = studentCode,
                    FullName = fullName,
                    DateOfBirth = dobParsedOk ? parsedDob.ToString("yyyy-MM-dd") : dobRaw,
                    Gender = genderOk ? normalizedGender : rawGender,
                    ClassName = className,
                    GuardianName = guardianName,
                    GuardianPhone = guardianPhone,
                    GuardianIdentityCard = guardianIdCard,
                    GuardianEmail = string.IsNullOrWhiteSpace(guardianEmail) ? null : guardianEmail,
                    Relationship = string.IsNullOrWhiteSpace(relationship) ? "Phụ huynh" : relationship,
                    IsValid = true,
                    IsDuplicate = false
                };

                var rowErrors = new List<string>();

                // 1. Validate Student Code
                if (string.IsNullOrWhiteSpace(studentCode))
                {
                    rowErrors.Add("Thiếu mã học sinh.");
                }
                else if (!StudentValidationHelper.IsValidStudentCode(studentCode))
                {
                    rowErrors.Add($"Mã học sinh '{studentCode}' không hợp lệ (chỉ gồm chữ, số, '-', '_', không khoảng trắng, dài 3-30 ký tự).");
                }
                else if (seenCodesInFile.Contains(studentCode))
                {
                    item.IsDuplicate = true;
                    rowErrors.Add($"Mã học sinh '{studentCode}' bị trùng lặp trong tệp Excel.");
                }
                else if (existingCodesInDb.Contains(studentCode))
                {
                    item.IsDuplicate = true;
                    rowErrors.Add($"Mã học sinh '{studentCode}' đã tồn tại trong cơ sở dữ liệu hệ thống.");
                }

                // 2. Validate Full Name
                var nameError = StudentValidationHelper.ValidateFullName(rawFullName, "họ và tên học sinh");
                if (nameError != null)
                {
                    rowErrors.Add(nameError);
                }

                // 3. Validate Date of Birth (strict, no default fallback)
                if (!dobParsedOk)
                {
                    rowErrors.Add(string.IsNullOrWhiteSpace(dobRaw)
                        ? "Thiếu ngày sinh học sinh."
                        : $"Ngày sinh '{dobRaw}' không đúng định dạng hợp lệ (yêu cầu dd/MM/yyyy hoặc yyyy-MM-dd).");
                }
                else
                {
                    var ageError = StudentValidationHelper.ValidateBirthDateAndAge(parsedDob, schoolYearStartYear, allowAgeException: false);
                    if (ageError != null)
                    {
                        if (StudentValidationHelper.IsOverPrimaryAge(parsedDob, schoolYearStartYear))
                        {
                            item.AllowAgeException = true;
                        }
                        else
                        {
                            rowErrors.Add(ageError);
                        }
                    }
                }

                // 4. Validate Gender (strict Nam / Nữ)
                if (!genderOk)
                {
                    rowErrors.Add(string.IsNullOrWhiteSpace(rawGender)
                        ? "Thiếu giới tính (chỉ chấp nhận 'Nam' hoặc 'Nữ')."
                        : $"Giới tính '{rawGender}' không hợp lệ (chỉ chấp nhận 'Nam' hoặc 'Nữ').");
                }

                // 5. Validate ClassName (if provided)
                Class? matchedClass = null;
                if (!string.IsNullOrWhiteSpace(className) && activeSchoolYear != null)
                {
                    if (!classMap.TryGetValue(className, out matchedClass))
                    {
                        rowErrors.Add($"Lớp '{className}' không tồn tại trong năm học {activeSchoolYear.YearName}.");
                    }
                    else
                    {
                        if (dobParsedOk)
                        {
                            var gradeAgeErr = StudentValidationHelper.ValidateAgeForGrade(parsedDob, matchedClass.Grade, schoolYearStartYear, item.IsSpecialNeeds, allowAgeException: item.AllowAgeException);
                            if (gradeAgeErr != null)
                            {
                                rowErrors.Add(gradeAgeErr);
                            }
                        }

                        previewEnrolledByClassId.TryGetValue(matchedClass.Id, out var currentCount);
                        if (matchedClass.Capacity > 0 && currentCount >= matchedClass.Capacity)
                        {
                            rowErrors.Add($"Lớp '{matchedClass.ClassName}' đã đủ sĩ số tối đa ({matchedClass.Capacity} học sinh).");
                        }
                    }
                }

                // 6. Validate Guardian Info (SC-05 + strict fields)
                var hasAnyGuardianContact = !string.IsNullOrWhiteSpace(guardianPhone)
                    || !string.IsNullOrWhiteSpace(guardianEmail)
                    || !string.IsNullOrWhiteSpace(guardianIdCard);

                if (hasAnyGuardianContact && string.IsNullOrWhiteSpace(rawGuardianName))
                {
                    rowErrors.Add("Bắt buộc nhập Họ tên phụ huynh khi có thông tin SĐT / Email / CCCD phụ huynh.");
                }
                else if (!string.IsNullOrWhiteSpace(rawGuardianName))
                {
                    var gNameErr = StudentValidationHelper.ValidateFullName(rawGuardianName, "Họ tên phụ huynh");
                    if (gNameErr != null)
                    {
                        rowErrors.Add(gNameErr);
                    }
                }

                if (!StudentValidationHelper.IsValidPhone(guardianPhone))
                {
                    rowErrors.Add($"SĐT phụ huynh '{guardianPhone}' không hợp lệ (bắt buộc đúng 10 chữ số, bắt đầu bằng 0).");
                }

                if (!StudentValidationHelper.IsValidIdentityCard(guardianIdCard))
                {
                    rowErrors.Add($"Số CCCD/CMND phụ huynh '{guardianIdCard}' không hợp lệ (bắt buộc 9 hoặc 12 chữ số).");
                }

                if (!string.IsNullOrWhiteSpace(guardianEmail) && !GuardianMatcher.IsValidEmail(guardianEmail))
                {
                    rowErrors.Add($"Email phụ huynh '{guardianEmail}' không hợp lệ.");
                }

                if (!StudentValidationHelper.IsValidRelationship(relationship))
                {
                    rowErrors.Add($"Mối quan hệ '{relationship}' không hợp lệ (chấp nhận: Bố, Mẹ, Người giám hộ, Phụ huynh, Ông, Bà).");
                }

                if (rowErrors.Count > 0)
                {
                    // Existing DB duplicate with no other errors keeps IsValid=true, IsDuplicate=true for duplicate resolution UI
                    if (item.IsDuplicate && rowErrors.Count == 1 && existingCodesInDb.Contains(studentCode) && !seenCodesInFile.Contains(studentCode))
                    {
                        item.IsValid = true;
                    }
                    else
                    {
                        item.IsValid = false;
                    }
                    item.ErrorMessage = string.Join(" ", rowErrors);
                }
                else
                {
                    seenCodesInFile.Add(studentCode);
                    if (matchedClass != null)
                    {
                        previewEnrolledByClassId[matchedClass.Id] = previewEnrolledByClassId.GetValueOrDefault(matchedClass.Id) + 1;
                    }
                }

                if (item.IsValid && !item.IsDuplicate)
                {
                    PreviewGuardian(item, knownGuardians);
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

        if (request.ValidRows.Count > 5000)
        {
            return ApiResponse<ImportStudentsResultDto>.Fail(SystemMessages.MSG01, new List<string> { "Số lượng bản ghi nhập tối đa mỗi lần là 5.000 học sinh." });
        }

        var result = new ImportStudentsResultDto
        {
            TotalRows = request.ValidRows.Count
        };

        var schoolYear = request.SchoolYearId.HasValue
            ? await _unitOfWork.Repository<SchoolYear>().GetByIdAsync(request.SchoolYearId.Value, cancellationToken)
            : await _unitOfWork.Classes.GetActiveSchoolYearAsync(cancellationToken);

        if (schoolYear != null && string.Equals(schoolYear.Status, SchoolYearStatuses.Archived, StringComparison.OrdinalIgnoreCase))
        {
            return ApiResponse<ImportStudentsResultDto>.Fail(SystemMessages.MSG01, new List<string> { $"Không thể nhập học sinh vào năm học đã lưu trữ ({schoolYear.YearName})." });
        }

        var isDraftYear = string.Equals(schoolYear?.Status, SchoolYearStatuses.Draft, StringComparison.OrdinalIgnoreCase);
        var schoolYearId = schoolYear?.Id ?? 0;
        var schoolYearName = schoolYear?.YearName ?? DateTime.UtcNow.Year.ToString();

        var classesInYear = schoolYearId > 0
            ? await _unitOfWork.Classes.GetClassesBySchoolYearAsync(schoolYearId, null, null, cancellationToken)
            : new List<Class>();

        var classMap = classesInYear.ToDictionary(c => c.ClassName, c => c, StringComparer.OrdinalIgnoreCase);
        var enrolledCountByClassId = classesInYear.ToDictionary(c => c.Id, c => c.ClassAssignments?.Count(ca => ca.IsActive || ca.IsDraft) ?? 0);

        // Tối ưu hóa hiệu năng: Cache dữ liệu kiểm tra trùng lặp để xử lý mượt mà lên tới 5,000 học sinh
        var studentRepo = _unitOfWork.Repository<Student>();
        var existingStudents = studentRepo != null ? await studentRepo.GetAllAsync(cancellationToken) : null;
        var existingCodesInDb = existingStudents != null
            ? existingStudents.Select(s => s.StudentCode.ToUpperInvariant()).ToHashSet()
            : new HashSet<string>();
        bool checkCodeWithRepo = existingStudents == null;

        // UC-09: guardian records only, never user accounts. Existing records plus the ones created by this import,
        // so that siblings in one file share a record (matched by identity card number or email, never phone).
        var knownGuardians = (await _unitOfWork.Repository<Guardian>().GetAllAsync(cancellationToken)).ToList();
        var eligibleIds = new HashSet<Guardian>(ReferenceEqualityComparer.Instance);

        await _unitOfWork.BeginTransactionAsync(cancellationToken);
        try
        {
            const int chunkSize = 500;
            var validRowsList = request.ValidRows.Where(r => r.IsValid && !r.IsDuplicate).ToList();

            for (int i = 0; i < validRowsList.Count; i += chunkSize)
            {
                var chunk = validRowsList.Skip(i).Take(chunkSize).ToList();
                var createdStudents = new List<(ImportStudentRowDto Row, Student Student)>();
                var guardianOfStudent = new Dictionary<Student, Guardian>(ReferenceEqualityComparer.Instance);

                // 1. Thêm danh sách Student trong chunk
                foreach (var row in chunk)
                {
                    var code = (row.StudentCode ?? string.Empty).Trim().ToUpperInvariant();
                    if (!StudentValidationHelper.IsValidStudentCode(code))
                    {
                        result.SkippedCount++;
                        result.Errors.Add($"Dòng {row.RowNumber}: Mã học sinh '{code}' không hợp lệ (3-30 ký tự, không khoảng trắng).");
                        continue;
                    }

                    var nameErr = StudentValidationHelper.ValidateFullName(row.FullName, "Họ và tên học sinh");
                    if (nameErr != null)
                    {
                        result.SkippedCount++;
                        result.Errors.Add($"Dòng {row.RowNumber}: {nameErr}");
                        continue;
                    }

                    if (!StudentValidationHelper.TryParseStrictDate(row.DateOfBirth, out var parsedDob))
                    {
                        result.SkippedCount++;
                        result.Errors.Add($"Dòng {row.RowNumber}: Ngày sinh '{row.DateOfBirth}' không hợp lệ (định dạng yêu cầu dd/MM/yyyy hoặc yyyy-MM-dd).");
                        continue;
                    }

                    var schoolYearStartYear = schoolYear?.StartDate.Year ?? DateTime.UtcNow.Year;
                    var allowRowAgeException = request.AllowAgeException;
                    var ageErr = StudentValidationHelper.ValidateBirthDateAndAge(parsedDob, schoolYearStartYear, allowRowAgeException);
                    if (ageErr != null)
                    {
                        result.SkippedCount++;
                        result.Errors.Add($"Dòng {row.RowNumber}: {ageErr}");
                        continue;
                    }

                    if (!StudentValidationHelper.TryNormalizeGender(row.Gender, out var normalizedGender))
                    {
                        result.SkippedCount++;
                        result.Errors.Add($"Dòng {row.RowNumber}: Giới tính '{row.Gender}' không hợp lệ (chỉ chấp nhận 'Nam' hoặc 'Nữ').");
                        continue;
                    }

                    var hasAnyGuardianContact = !string.IsNullOrWhiteSpace(row.GuardianPhone)
                        || !string.IsNullOrWhiteSpace(row.GuardianEmail)
                        || !string.IsNullOrWhiteSpace(row.GuardianIdentityCard);
                    if (hasAnyGuardianContact && string.IsNullOrWhiteSpace(row.GuardianName))
                    {
                        result.SkippedCount++;
                        result.Errors.Add($"Dòng {row.RowNumber}: Thiếu Họ tên phụ huynh khi đã nhập SĐT/Email/CCCD.");
                        continue;
                    }

                    if (!string.IsNullOrWhiteSpace(row.GuardianName))
                    {
                        var gNameErr = StudentValidationHelper.ValidateFullName(row.GuardianName, "Họ tên phụ huynh");
                        if (gNameErr != null)
                        {
                            result.SkippedCount++;
                            result.Errors.Add($"Dòng {row.RowNumber}: {gNameErr}");
                            continue;
                        }
                    }

                    if (!StudentValidationHelper.IsValidPhone(row.GuardianPhone))
                    {
                        result.SkippedCount++;
                        result.Errors.Add($"Dòng {row.RowNumber}: SĐT phụ huynh '{row.GuardianPhone}' không hợp lệ (bắt buộc 10 chữ số, bắt đầu bằng 0).");
                        continue;
                    }

                    if (!StudentValidationHelper.IsValidIdentityCard(row.GuardianIdentityCard))
                    {
                        result.SkippedCount++;
                        result.Errors.Add($"Dòng {row.RowNumber}: Số CCCD/CMND phụ huynh '{row.GuardianIdentityCard}' không hợp lệ (9 hoặc 12 chữ số).");
                        continue;
                    }

                    if (!string.IsNullOrWhiteSpace(row.ClassName))
                    {
                        if (!classMap.TryGetValue(row.ClassName.Trim(), out var targetClassCheck))
                        {
                            result.SkippedCount++;
                            result.Errors.Add($"Dòng {row.RowNumber}: Lớp '{row.ClassName.Trim()}' không tồn tại trong năm học {schoolYearName}.");
                            continue;
                        }

                        var gradeAgeErr = StudentValidationHelper.ValidateAgeForGrade(parsedDob, targetClassCheck.Grade, schoolYearStartYear, row.IsSpecialNeeds, allowRowAgeException);
                        if (gradeAgeErr != null)
                        {
                            result.SkippedCount++;
                            result.Errors.Add($"Dòng {row.RowNumber}: {gradeAgeErr}");
                            continue;
                        }

                        enrolledCountByClassId.TryGetValue(targetClassCheck.Id, out var curCountCheck);
                        if (targetClassCheck.Capacity > 0 && curCountCheck >= targetClassCheck.Capacity)
                        {
                            result.SkippedCount++;
                            result.Errors.Add($"Dòng {row.RowNumber}: Lớp '{targetClassCheck.ClassName}' đã đủ sĩ số ({targetClassCheck.Capacity} học sinh).");
                            continue;
                        }
                    }

                    var isCodeExisting = existingCodesInDb.Contains(code) || (checkCodeWithRepo && await _unitOfWork.Students.ExistsByStudentCodeAsync(code, null, cancellationToken));
                    if (isCodeExisting)
                    {
                        result.SkippedCount++;
                        result.Errors.Add($"Dòng {row.RowNumber}: Mã {code} đã tồn tại trong hệ thống, bỏ qua.");
                        continue;
                    }

                    // Matching is recomputed here; the preview sent by the client is not trusted
                    var match = GuardianMatcher.Match(row.GuardianName, row.GuardianEmail, row.GuardianIdentityCard, knownGuardians);
                    if (match.Kind == GuardianMatchKind.Conflict)
                    {
                        result.SkippedCount++;
                        result.Errors.Add($"Dòng {row.RowNumber}: {match.Note} Học sinh {code} chưa được nhập.");
                        continue;
                    }
                    if (!string.IsNullOrWhiteSpace(row.GuardianEmail) && !GuardianMatcher.IsValidEmail(row.GuardianEmail))
                    {
                        result.SkippedCount++;
                        result.Errors.Add($"Dòng {row.RowNumber}: email phụ huynh không hợp lệ. Học sinh {code} chưa được nhập.");
                        continue;
                    }
                    existingCodesInDb.Add(code);

                    var student = new Student
                    {
                        StudentCode = code,
                        FullName = CapitalizeWords(row.FullName),
                        DateOfBirth = parsedDob,
                        Gender = normalizedGender,
                        EnrollmentDate = DateTime.UtcNow,
                        Status = "Active", // Workflow step: Save Profiles & Set Student Status = 'Active'
                        ResidentialArea = row.ResidentialArea?.Trim(),
                        IsSpecialNeeds = row.IsSpecialNeeds,
                        SpecialNeedsNotes = row.SpecialNeedsNotes?.Trim(),
                        CreatedAt = DateTime.UtcNow
                    };

                    await _unitOfWork.Students.AddAsync(student, cancellationToken);
                    createdStudents.Add((row, student));

                    // Guardian record, no account (UC-09 step 5, A3, A4). The match above already includes records
                    // created by earlier rows of this import, so siblings share one record.
                    Guardian? guardian = null;
                    if (match.Kind == GuardianMatchKind.Reused)
                    {
                        guardian = match.Record!;
                        GuardianMatcher.FillEmptyFields(guardian, row.GuardianPhone, row.GuardianEmail, row.GuardianIdentityCard);
                        result.GuardiansReused++;
                    }
                    else if (match.Kind == GuardianMatchKind.New)
                    {
                        guardian = new Guardian
                        {
                            FullName = CapitalizeWords(row.GuardianName),
                            PhoneNumber = string.IsNullOrWhiteSpace(row.GuardianPhone) ? null : row.GuardianPhone.Trim(),
                            Email = string.IsNullOrWhiteSpace(row.GuardianEmail) ? null : row.GuardianEmail.Trim(),
                            IdentityCardNumber = row.GuardianIdentityCard?.Trim() ?? string.Empty,
                            CreatedAt = DateTime.UtcNow
                        };
                        await _unitOfWork.Repository<Guardian>().AddAsync(guardian, cancellationToken);
                        knownGuardians.Add(guardian);
                        result.GuardiansCreated++;
                    }

                    if (guardian != null)
                    {
                        if (string.IsNullOrWhiteSpace(guardian.Email))
                        {
                            result.GuardiansWithoutEmail++; // A4: imported, not eligible for an account
                        }
                        else if (guardian.UserId == null)
                        {
                            eligibleIds.Add(guardian);
                        }
                        guardianOfStudent[student] = guardian;
                    }
                }

                if (!createdStudents.Any())
                {
                    continue;
                }

                // Save the students and new guardian records to get their ids
                await _unitOfWork.SaveChangesAsync(cancellationToken);

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
                                StartDate = DateTime.UtcNow.Date,
                                IsActive = !isDraftYear,
                                IsDraft = isDraftYear
                            };
                            await _unitOfWork.Repository<ClassAssignment>().AddAsync(assignment, cancellationToken);
                            enrolledCountByClassId[targetClass.Id] = currentEnrolled + 1;
                        }
                    }

                    // StudentGuardian: linked as primary guardian (UC-09 step 5)
                    if (guardianOfStudent.TryGetValue(student, out var guardian))
                    {
                        await _unitOfWork.Repository<StudentGuardian>().AddAsync(new StudentGuardian
                        {
                            StudentId = student.Id,
                            GuardianId = guardian.Id,
                            Relationship = ParseRelationship(row.Relationship),
                            IsPrimaryContact = true
                        }, cancellationToken);
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
                new { result.SuccessCount, result.SkippedCount, result.GuardiansCreated, result.GuardiansReused, result.GuardiansWithoutEmail },
                null, // TODO: pass the client IP address; it is not recorded yet.
                cancellationToken);

            await _unitOfWork.SaveChangesAsync(cancellationToken);
            await _unitOfWork.CommitTransactionAsync(cancellationToken);

            result.GuardiansEligibleForAccountIds = eligibleIds.Select(g => g.Id).Distinct().OrderBy(id => id).ToList();
            return ApiResponse<ImportStudentsResultDto>.Ok(result, result.SuccessCount > 0 ? SystemMessages.MSG48 : SystemMessages.MSG49);
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
            "Ngày sinh (dd/MM/yyyy) (*)",
            "Giới tính (Nam/Nữ) (*)",
            "Lớp dự kiến",
            "Họ tên phụ huynh",
            "SĐT phụ huynh (10 số)",
            "CCCD phụ huynh (9/12 số)",
            "Quan hệ (Bố/Mẹ/Người giám hộ)",
            "Email phụ huynh"
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
            new[] { "HS2026001", "Nguyễn Văn An", "15/05/2020", "Nam", "1A", "Nguyễn Văn Nam", "0912345678", "001200123456", "Bố", "nguyenvannam@example.com" },
            new[] { "HS2026002", "Trần Thị Mai", "22/08/2020", "Nữ", "1A", "Trần Đình Khang", "0987654321", "001200987654", "Bố", "" },
            new[] { "HS2026003", "Lê Gia Bảo", "10/03/2020", "Nam", "1B", "Phạm Thu Hương", "0944556677", "001200334455", "Mẹ", "phamthuhuong@example.com" }
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

    /// <summary>
    /// Fills the guardian columns of the preview: new / reused / conflict and account eligibility. A guardian that a
    /// previous row of the same file creates is added to <paramref name="knownGuardians"/>, so siblings show "Reused".
    /// </summary>
    private static void PreviewGuardian(ImportStudentRowDto item, List<Guardian> knownGuardians)
    {
        var match = GuardianMatcher.Match(item.GuardianName, item.GuardianEmail, item.GuardianIdentityCard, knownGuardians);
        item.GuardianMatch = match.Kind.ToString();
        item.GuardianMatchNote = match.Note;
        switch (match.Kind)
        {
            case GuardianMatchKind.Conflict:
                item.IsValid = false;
                item.ErrorMessage = match.Note;
                break;
            case GuardianMatchKind.New:
                knownGuardians.Add(new Guardian
                {
                    FullName = item.GuardianName,
                    Email = item.GuardianEmail,
                    IdentityCardNumber = item.GuardianIdentityCard
                });
                item.GuardianEligibleForAccount = GuardianMatch.IsEligibleForAccount(item.GuardianEmail, null);
                break;
            case GuardianMatchKind.Reused:
                item.GuardianEligibleForAccount = GuardianMatch.IsEligibleForAccount(item.GuardianEmail, match.Record);
                break;
        }
    }

    private static RelationshipTypes ParseRelationship(string? text)
    {
        if (string.IsNullOrWhiteSpace(text)) return RelationshipTypes.Guardian;
        var lower = text.Trim().ToLowerInvariant();
        if (lower.Contains("bố") || lower.Contains("cha") || lower.Contains("father")) return RelationshipTypes.Father;
        if (lower.Contains("mẹ") || lower.Contains("mother")) return RelationshipTypes.Mother;
        return RelationshipTypes.Guardian;
    }

    private static bool TryParseExcelBirthDate(IXLCell cell, out DateTime parsed)
    {
        parsed = default;
        if (cell.IsEmpty()) return false;

        if (cell.DataType == XLDataType.DateTime)
        {
            parsed = cell.GetDateTime().Date;
            return true;
        }

        var raw = cell.GetString()?.Trim();
        return StudentValidationHelper.TryParseStrictDate(raw, out parsed);
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
