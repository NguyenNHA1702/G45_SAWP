using System.Globalization;
using System.Text;
using System.Text.RegularExpressions;
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

/// <summary>
/// UC-69 Import Class Assignments: creates the classes of a Draft school year, their homeroom teachers and the
/// students' class assignments from a school-provided Excel file (sheets "Classes" and "Students"), with a preview
/// and the Replace / Merge re-import of A2. Placement itself is decided by the school (BR-70).
/// </summary>
public class ClassImporter : IClassImporter
{
    public const string ClassesSheet = "Classes";
    public const string StudentsSheet = "Students";
    public const string InstructionsSheet = "Instructions";

    public const string ClassNameHeader = "Class name";
    public const string GradeHeader = "Grade";
    public const string TeacherCodeHeader = "Homeroom teacher code";
    public const string StudentCodeHeader = "Student code";
    public const string FullNameHeader = "Full name";

    /// <summary>Capacity of a class created by the import; Office Staff change it during review (UC-14).</summary>
    public const int DefaultCapacity = 35;

    private const int MaxRowsPerSheet = 5000;

    private readonly IUnitOfWork _unitOfWork;
    private readonly IAuditService _auditService;

    public ClassImporter(IUnitOfWork unitOfWork, IAuditService auditService)
    {
        _unitOfWork = unitOfWork;
        _auditService = auditService;
    }

    #region Parse (UC-69 step 2, E1)

    public ApiResponse<ClassImportRowsDto> ParseWorkbook(Stream fileStream)
    {
        XLWorkbook workbook;
        try
        {
            workbook = new XLWorkbook(fileStream);
        }
        catch (Exception)
        {
            return ApiResponse<ClassImportRowsDto>.Fail(SystemMessages.MSG41, new List<string> { "Không đọc được tệp Excel (.xlsx)." });
        }

        using (workbook)
        {
            var errors = new List<string>();
            var classesSheet = FindSheet(workbook, ClassesSheet);
            var studentsSheet = FindSheet(workbook, StudentsSheet);
            if (classesSheet == null) errors.Add($"Tệp thiếu trang tính '{ClassesSheet}'.");
            if (studentsSheet == null) errors.Add($"Tệp thiếu trang tính '{StudentsSheet}'.");
            if (errors.Count > 0)
            {
                return ApiResponse<ClassImportRowsDto>.Fail(SystemMessages.MSG41, errors);
            }

            var classColumns = ReadHeader(classesSheet!, new[] { ClassNameHeader, GradeHeader }, new[] { TeacherCodeHeader }, errors);
            var studentColumns = ReadHeader(studentsSheet!, new[] { StudentCodeHeader, ClassNameHeader }, new[] { FullNameHeader }, errors);
            if (errors.Count > 0)
            {
                return ApiResponse<ClassImportRowsDto>.Fail(SystemMessages.MSG41, errors);
            }

            var result = new ClassImportRowsDto();
            foreach (var (rowNumber, cells) in ReadRows(classesSheet!, classColumns))
            {
                result.Classes.Add(new ClassImportClassRowDto
                {
                    RowNumber = rowNumber,
                    ClassName = cells[ClassNameHeader],
                    Grade = cells[GradeHeader],
                    HomeroomTeacherCode = NullIfEmpty(cells.GetValueOrDefault(TeacherCodeHeader))
                });
            }

            foreach (var (rowNumber, cells) in ReadRows(studentsSheet!, studentColumns))
            {
                result.Students.Add(new ClassImportStudentRowDto
                {
                    RowNumber = rowNumber,
                    StudentCode = cells[StudentCodeHeader],
                    FullName = NullIfEmpty(cells.GetValueOrDefault(FullNameHeader)),
                    ClassName = cells[ClassNameHeader]
                });
            }

            if (result.Classes.Count > MaxRowsPerSheet || result.Students.Count > MaxRowsPerSheet)
            {
                return ApiResponse<ClassImportRowsDto>.Fail(SystemMessages.MSG41, new List<string> { $"Mỗi trang tính được phép có tối đa {MaxRowsPerSheet} dòng dữ liệu." });
            }

            return ApiResponse<ClassImportRowsDto>.Ok(result);
        }
    }

    private static IXLWorksheet? FindSheet(XLWorkbook workbook, string name)
        => workbook.Worksheets.FirstOrDefault(ws => NormalizeKey(ws.Name) == NormalizeKey(name));

    private static Dictionary<string, int> ReadHeader(IXLWorksheet sheet, string[] required, string[] optional, List<string> errors)
    {
        var map = new Dictionary<string, int>();
        var lastColumn = sheet.LastColumnUsed()?.ColumnNumber() ?? 0;
        var wanted = required.Concat(optional).ToDictionary(NormalizeKey, h => h);
        for (var col = 1; col <= lastColumn; col++)
        {
            var key = NormalizeKey(sheet.Cell(1, col).GetFormattedString());
            if (wanted.TryGetValue(key, out var header) && !map.ContainsKey(header))
            {
                map[header] = col;
            }
        }

        var missing = required.Where(h => !map.ContainsKey(h)).ToList();
        if (missing.Count > 0)
        {
            errors.Add($"Trang {sheet.Name} thiếu cột bắt buộc: {string.Join(", ", missing)}.");
        }

        return map;
    }

    private static IEnumerable<(int RowNumber, Dictionary<string, string> Cells)> ReadRows(IXLWorksheet sheet, Dictionary<string, int> columns)
    {
        var lastRow = sheet.LastRowUsed()?.RowNumber() ?? 1;
        for (var r = 2; r <= lastRow; r++)
        {
            var cells = columns.ToDictionary(c => c.Key, c => sheet.Cell(r, c.Value).GetFormattedString().Trim());
            if (cells.Values.All(string.IsNullOrWhiteSpace))
            {
                continue;
            }

            yield return (r, cells);
        }
    }

    #endregion

    #region Preview (UC-69 step 3, A2)

    public async Task<ApiResponse<ClassImportPreviewDto>> PreviewAsync(int schoolYearId, ClassImportRowsDto rows, CancellationToken cancellationToken = default)
    {
        var (plan, failure) = await BuildPlanAsync(schoolYearId, rows, isPreview: true, cancellationToken);
        if (failure != null)
        {
            return ApiResponse<ClassImportPreviewDto>.Fail(failure.Value.Message, failure.Value.Errors);
        }

        return ApiResponse<ClassImportPreviewDto>.Ok(ToPreview(plan!));
    }

    #endregion

    #region Import (UC-69 steps 4-6)

    public async Task<ApiResponse<ClassImportResultDto>> ImportAsync(int schoolYearId, ClassImportRowsDto rows, int? actorUserId = null, CancellationToken cancellationToken = default)
    {
        var (plan, failure) = await BuildPlanAsync(schoolYearId, rows, isPreview: false, cancellationToken);
        if (failure != null)
        {
            return ApiResponse<ClassImportResultDto>.Fail(failure.Value.Message, failure.Value.Errors);
        }

        var p = plan!;
        if (p.ValidClassRows.Count == 0 && p.ValidStudentRows.Count == 0)
        {
            // E3: no valid rows -> import nothing
            return ApiResponse<ClassImportResultDto>.Fail(SystemMessages.MSG49, RowErrors(p));
        }

        var year = p.Year;
        var classRepo = _unitOfWork.Repository<Class>();
        var assignmentRepo = _unitOfWork.Repository<ClassAssignment>();
        var result = new ClassImportResultDto
        {
            SkippedRows = p.Rows.Classes.Count(r => !r.IsValid) + p.Rows.Students.Count(r => !r.IsValid),
            Errors = RowErrors(p),
            Warnings = p.Warnings.ToList()
        };

        await _unitOfWork.BeginTransactionAsync(cancellationToken);
        try
        {
            // Classes: create new ones, update homeroom teachers (UC-15 A2) and reset changed ones to Not reviewed
            var classByName = new Dictionary<string, Class>(p.ExistingClasses, StringComparer.OrdinalIgnoreCase);
            foreach (var target in p.FinalClasses.Values.Where(c => c.Existing == null))
            {
                var cls = new Class
                {
                    SchoolYearId = year.Id,
                    ClassName = target.Name,
                    Grade = target.Grade,
                    Capacity = DefaultCapacity,
                    Status = "Active",
                    HomeroomTeacherId = target.Teacher?.Id,
                    IsReviewed = false
                };
                await classRepo.AddAsync(cls, cancellationToken);
                classByName[target.Name] = cls;
                result.ClassesCreated++;
            }

            foreach (var target in p.FinalClasses.Values.Where(c => c.Existing != null))
            {
                var cls = target.Existing!;
                if (cls.HomeroomTeacherId != target.Teacher?.Id)
                {
                    cls.HomeroomTeacherId = target.Teacher?.Id;
                }

                if (target.IsChanged)
                {
                    ClassService.ResetReview(cls);
                }

                await classRepo.UpdateAsync(cls, cancellationToken);
            }

            var finalTeacherIds = p.FinalClasses.Values
                .Where(c => c.Teacher != null)
                .Select(c => c.Teacher!.Id)
                .ToHashSet();

            foreach (var teacher in p.FinalClasses.Values.Select(c => c.Teacher).Where(t => t != null && !t.IsHomeroomTeacher))
            {
                teacher!.IsHomeroomTeacher = true;
                await _unitOfWork.Repository<StaffProfile>().UpdateAsync(teacher, cancellationToken);
            }

            foreach (var oldTeacher in p.ExistingClasses.Values
                .Select(c => c.HomeroomTeacher)
                .Where(t => t != null && t.IsHomeroomTeacher && !finalTeacherIds.Contains(t.Id))
                .DistinctBy(t => t!.Id))
            {
                oldTeacher!.IsHomeroomTeacher = false;
                await _unitOfWork.Repository<StaffProfile>().UpdateAsync(oldTeacher, cancellationToken);
            }

            result.TeacherChanges = p.Diff.TeacherChanges.Count;

            // New classes need their ids before assignments refer to them
            await _unitOfWork.SaveChangesAsync(cancellationToken);

            // Students: a moved or removed Draft assignment is deleted; nothing refers to it yet (BR-72)
            foreach (var current in p.AssignmentsToDelete)
            {
                await assignmentRepo.DeleteAsync(current, cancellationToken);
            }

            foreach (var (student, className) in p.AssignmentsToAdd)
            {
                await assignmentRepo.AddAsync(new ClassAssignment
                {
                    ClassId = classByName[className].Id,
                    StudentId = student.Id,
                    AcademicYear = year.YearName,
                    // Step 5: the school-year start date is the effective date
                    StartDate = year.StartDate.Date,
                    IsActive = false,
                    IsDraft = true
                }, cancellationToken);
            }

            result.StudentsAdded = p.Diff.StudentsAdded.Count;
            result.StudentsMoved = p.Diff.StudentsMoved.Count;
            result.StudentsRemoved = p.Diff.StudentsRemoved.Count;

            // Replace: classes not in the file are removed with what belongs to them
            foreach (var removed in p.ClassesToRemove)
            {
                var leftovers = (await assignmentRepo.FindAsync(ca => ca.ClassId == removed.Id, cancellationToken))
                    .Where(ca => !p.AssignmentsToDelete.Contains(ca))
                    .ToList();
                foreach (var leftover in leftovers)
                {
                    await assignmentRepo.DeleteAsync(leftover, cancellationToken);
                }

                foreach (var schedule in await _unitOfWork.Repository<ClassSchedule>().FindAsync(s => s.ClassId == removed.Id, cancellationToken))
                {
                    await _unitOfWork.Repository<ClassSchedule>().DeleteAsync(schedule, cancellationToken);
                }

                await classRepo.DeleteAsync(removed, cancellationToken);
                result.ClassesRemoved++;
            }

            result.ClassesToReview = p.FinalClasses.Values.Count(c => c.IsChanged);
            await _unitOfWork.SaveChangesAsync(cancellationToken);

            await _auditService.LogActionAsync(
                actorUserId,
                AuditActions.ClassImportAssignments,
                nameof(SchoolYear),
                year.Id.ToString(),
                null,
                new
                {
                    year.YearName,
                    p.Mode,
                    result.ClassesCreated,
                    result.ClassesRemoved,
                    result.StudentsAdded,
                    result.StudentsMoved,
                    result.StudentsRemoved,
                    result.TeacherChanges,
                    result.SkippedRows
                },
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

        // Step 6: totals with MSG48; skipped rows are listed with MSG49
        return ApiResponse<ClassImportResultDto>.Ok(result, result.SkippedRows == 0 ? SystemMessages.MSG48 : SystemMessages.MSG49);
    }

    #endregion

    #region Template

    public byte[] GenerateTemplate()
    {
        using var workbook = new XLWorkbook();

        var classes = workbook.Worksheets.Add(ClassesSheet);
        WriteSheet(classes, new[] { ClassNameHeader, GradeHeader, TeacherCodeHeader }, new[]
        {
            new[] { "1A", "1", "GV001" },
            new[] { "2A", "2", "GV002" },
            new[] { "2B", "2", "" }
        });

        var students = workbook.Worksheets.Add(StudentsSheet);
        WriteSheet(students, new[] { StudentCodeHeader, FullNameHeader, ClassNameHeader }, new[]
        {
            new[] { "HS001", "Trần Gia Bảo", "1A" },
            new[] { "HS002", "Nguyễn Minh Anh", "1A" },
            new[] { "HS010", "Lê Hoàng Nam", "2A" },
            new[] { "HS011", "", "2B" }
        });

        var help = workbook.Worksheets.Add(InstructionsSheet);
        var lines = new[]
        {
            "HƯỚNG DẪN NHẬP DỮ LIỆU XẾP LỚP (UC-69)",
            "Tệp gồm 2 trang tính: Classes (danh sách lớp và giáo viên chủ nhiệm) và Students (học sinh và lớp được xếp).",
            "Dòng 1 của mỗi trang là tên cột, giữ nguyên tên cột tiếng Anh; thứ tự cột không bắt buộc.",
            "Trang Classes – Class name (bắt buộc): tên lớp gồm số khối và một chữ cái, ví dụ 1A, 2B; mỗi lớp một dòng.",
            "Trang Classes – Grade (bắt buộc): khối lớp, số nguyên từ 1 đến 5, khớp với tên lớp.",
            "Trang Classes – Homeroom teacher code (không bắt buộc): mã nhân viên của giáo viên chủ nhiệm, ví dụ GV001. Mỗi giáo viên chủ nhiệm một lớp.",
            "Trang Students – Student code (bắt buộc): mã học sinh đã có trong SAWP và đang học; mỗi học sinh chỉ một dòng.",
            "Trang Students – Full name (không bắt buộc): họ và tên để đối chiếu, phải khớp với hồ sơ; không cập nhật hồ sơ học sinh.",
            "Trang Students – Class name (bắt buộc): lớp có ở trang Classes (hoặc đã có trong năm học khi nhập bổ sung).",
            $"Lớp mới được tạo với sĩ số tối đa {DefaultCapacity}; có thể chỉnh khi rà soát lớp. Lớp vượt sĩ số chỉ được cảnh báo.",
            "Dòng có lỗi được bỏ qua; các dòng hợp lệ được nhập vào năm học ở trạng thái Bản nháp."
        };
        for (var i = 0; i < lines.Length; i++)
        {
            help.Cell(i + 1, 1).Value = lines[i];
        }
        help.Cell(1, 1).Style.Font.Bold = true;
        help.Column(1).Width = 120;

        using var ms = new MemoryStream();
        workbook.SaveAs(ms);
        return ms.ToArray();
    }

    private static void WriteSheet(IXLWorksheet sheet, string[] headers, string[][] rows)
    {
        for (var c = 0; c < headers.Length; c++)
        {
            var cell = sheet.Cell(1, c + 1);
            cell.Value = headers[c];
            cell.Style.Font.Bold = true;
        }

        for (var r = 0; r < rows.Length; r++)
        {
            for (var c = 0; c < rows[r].Length; c++)
            {
                // Text cells, so codes like "001" keep their leading zeros
                sheet.Cell(r + 2, c + 1).SetValue(rows[r][c]);
            }
        }

        sheet.Columns(1, headers.Length).Width = 24;
        sheet.Column(1).Style.NumberFormat.Format = "@";
        sheet.SheetView.FreezeRows(1);
    }

    #endregion

    #region Validation and plan

    private sealed class TargetClass
    {
        public string Name { get; init; } = string.Empty;
        public int Grade { get; init; }
        public Class? Existing { get; init; }
        public StaffProfile? Teacher { get; set; }
        public bool IsChanged { get; set; }
        public bool FromFile { get; init; }
        public List<string> StudentCodes { get; } = new();
    }

    private sealed class Plan
    {
        public SchoolYear Year { get; init; } = null!;
        public string Mode { get; init; } = ClassImportModes.Merge;
        public ClassImportRowsDto Rows { get; init; } = new();
        public bool IsReimport { get; init; }
        public Dictionary<string, Class> ExistingClasses { get; init; } = new(StringComparer.OrdinalIgnoreCase);
        public Dictionary<string, StaffProfile> TeachersByCode { get; set; } = new(StringComparer.OrdinalIgnoreCase);
        public List<ClassImportClassRowDto> ValidClassRows { get; init; } = new();
        public List<ClassImportStudentRowDto> ValidStudentRows { get; init; } = new();
        public Dictionary<string, TargetClass> FinalClasses { get; } = new(StringComparer.OrdinalIgnoreCase);
        public List<Class> ClassesToRemove { get; } = new();
        public List<ClassAssignment> AssignmentsToDelete { get; } = new();
        public List<(Student Student, string ClassName)> AssignmentsToAdd { get; } = new();
        public ClassImportDiffDto Diff { get; } = new();
        public List<string> Warnings { get; } = new();
    }

    private async Task<(Plan? Plan, (SystemMessage Message, List<string> Errors)? Failure)> BuildPlanAsync(
        int schoolYearId, ClassImportRowsDto rows, bool isPreview, CancellationToken ct)
    {
        var year = await _unitOfWork.Repository<SchoolYear>().GetByIdAsync(schoolYearId, ct);
        if (year == null)
        {
            return (null, (SystemMessages.MSG01, new List<string> { "Không tìm thấy năm học." }));
        }

        // E2: only a Draft school year
        if (year.Status != SchoolYearStatuses.Draft)
        {
            return (null, (SystemMessages.MSG62, new List<string>()));
        }

        var mode = string.Equals(rows.Mode, ClassImportModes.Replace, StringComparison.OrdinalIgnoreCase)
            ? ClassImportModes.Replace
            : ClassImportModes.Merge;

        foreach (var row in rows.Classes)
        {
            row.ClassName = (row.ClassName ?? string.Empty).Trim().ToUpperInvariant();
            row.Grade = (row.Grade ?? string.Empty).Trim();
            row.HomeroomTeacherCode = NullIfEmpty(row.HomeroomTeacherCode?.Trim().ToUpperInvariant());
            row.Errors = new List<string>();
        }

        foreach (var row in rows.Students)
        {
            row.StudentCode = (row.StudentCode ?? string.Empty).Trim().ToUpperInvariant();
            row.FullName = NullIfEmpty(row.FullName?.Trim());
            row.ClassName = (row.ClassName ?? string.Empty).Trim().ToUpperInvariant();
            row.Errors = new List<string>();
        }

        // Current Draft: classes of the year and their Draft assignments
        var existingClasses = await _unitOfWork.Repository<Class>().Query()
            .Include(c => c.HomeroomTeacher).ThenInclude(t => t!.User)
            .Where(c => c.SchoolYearId == year.Id)
            .ToListAsync(ct);
        var existingById = existingClasses.ToDictionary(c => c.Id);
        var existingClassIds = existingById.Keys.ToList();
        var currentAssignments = await _unitOfWork.Repository<ClassAssignment>().Query()
            .Include(ca => ca.Student)
            .Where(ca => ca.IsDraft && existingClassIds.Contains(ca.ClassId))
            .ToListAsync(ct);

        var plan = new Plan
        {
            Year = year,
            Mode = mode,
            Rows = rows,
            IsReimport = existingClasses.Count > 0,
            ExistingClasses = existingClasses
                .GroupBy(c => c.ClassName, StringComparer.OrdinalIgnoreCase)
                .ToDictionary(g => g.Key, g => g.First(), StringComparer.OrdinalIgnoreCase)
        };

        // Students and teachers named in the file
        var studentCodes = rows.Students.Select(r => r.StudentCode).Where(c => c.Length > 0).Distinct().ToList();
        var students = (await _unitOfWork.Repository<Student>().Query()
                .Where(s => studentCodes.Contains(s.StudentCode))
                .ToListAsync(ct))
            .GroupBy(s => s.StudentCode, StringComparer.OrdinalIgnoreCase)
            .ToDictionary(g => g.Key, g => g.First(), StringComparer.OrdinalIgnoreCase);
        var teacherCodes = rows.Classes.Select(r => r.HomeroomTeacherCode).OfType<string>().Distinct().ToList();
        var staff = (await _unitOfWork.Repository<StaffProfile>().Query()
                .Include(sp => sp.User).ThenInclude(u => u.UserRoles).ThenInclude(ur => ur.Role)
                .Where(sp => teacherCodes.Contains(sp.StaffCode))
                .ToListAsync(ct))
            .GroupBy(sp => sp.StaffCode, StringComparer.OrdinalIgnoreCase)
            .ToDictionary(g => g.Key, g => g.First(), StringComparer.OrdinalIgnoreCase);

        plan.TeachersByCode = staff;
        ValidateClassRows(plan, staff);
        ResolveTeachers(plan, staff);
        ValidateStudentRows(plan, students, isPreview);

        plan.ValidClassRows.AddRange(rows.Classes.Where(r => r.IsValid));
        plan.ValidStudentRows.AddRange(rows.Students.Where(r => r.IsValid));

        BuildFinalState(plan, students, currentAssignments, existingById);
        return (plan, null);
    }

    private static void ValidateClassRows(Plan plan, Dictionary<string, StaffProfile> staff)
    {
        // A class name or a teacher code on more than one row: none of these rows is imported
        var rowsByName = RowsByKey(plan.Rows.Classes, r => r.ClassName, r => r.RowNumber);
        var rowsByTeacher = RowsByKey(plan.Rows.Classes, r => r.HomeroomTeacherCode, r => r.RowNumber);
        foreach (var row in plan.Rows.Classes)
        {
            var gradeOk = int.TryParse(row.Grade, NumberStyles.Integer, CultureInfo.InvariantCulture, out var grade) && grade >= 1 && grade <= 5;
            if (string.IsNullOrEmpty(row.ClassName))
            {
                row.Errors.Add("Thiếu tên lớp (Class name).");
            }
            else if (rowsByName[row.ClassName].Count > 1)
            {
                row.Errors.Add($"Lớp {row.ClassName} xuất hiện nhiều lần ở trang {ClassesSheet} (dòng {string.Join(", ", rowsByName[row.ClassName])}).");
            }

            if (row.HomeroomTeacherCode != null && rowsByTeacher[row.HomeroomTeacherCode].Count > 1)
            {
                // BR-16: one class per homeroom teacher
                row.Errors.Add($"Mã giáo viên {row.HomeroomTeacherCode} xuất hiện nhiều lần ở trang {ClassesSheet} (dòng {string.Join(", ", rowsByTeacher[row.HomeroomTeacherCode])}).");
            }

            if (string.IsNullOrEmpty(row.Grade))
            {
                row.Errors.Add("Thiếu khối lớp (Grade).");
            }
            else if (!gradeOk)
            {
                row.Errors.Add($"Khối lớp '{row.Grade}' không hợp lệ: phải là số nguyên từ 1 đến 5 (BR-15).");
            }

            if (gradeOk && !string.IsNullOrEmpty(row.ClassName))
            {
                // BR-15: one class name carries one grade (the app's naming rule: grade digit + one letter)
                var nameError = StudentValidationHelper.ValidateClassNameForGrade(row.ClassName, grade);
                if (nameError != null)
                {
                    row.Errors.Add(nameError);
                }
                else if (plan.ExistingClasses.TryGetValue(row.ClassName, out var existing) && existing.Grade != grade)
                {
                    row.Errors.Add($"Lớp {row.ClassName} đã có trong năm học với Khối {existing.Grade}.");
                }
            }

            if (row.HomeroomTeacherCode != null && rowsByTeacher[row.HomeroomTeacherCode].Count == 1)
            {
                if (!staff.TryGetValue(row.HomeroomTeacherCode, out var teacher))
                {
                    row.Errors.Add($"Không tìm thấy giáo viên có mã nhân viên {row.HomeroomTeacherCode}.");
                }
                else if (!IsTeacher(teacher))
                {
                    row.Errors.Add($"Mã nhân viên {row.HomeroomTeacherCode} không phải tài khoản Giáo viên.");
                }
                else if (teacher.User?.Status != AccountStatus.Active)
                {
                    row.Errors.Add($"Tài khoản của giáo viên {row.HomeroomTeacherCode} chưa được kích hoạt hoặc đã bị khóa.");
                }
            }

            row.IsValid = row.Errors.Count == 0;
        }
    }

    /// <summary>BR-16: a teacher is homeroom teacher of one class of the year, in the state after the import.</summary>
    private static void ResolveTeachers(Plan plan, Dictionary<string, StaffProfile> staff)
    {
        var validNames = plan.Rows.Classes.Where(r => r.IsValid).Select(r => r.ClassName).ToHashSet(StringComparer.OrdinalIgnoreCase);
        var namedNames = plan.Rows.Classes.Select(r => r.ClassName).ToHashSet(StringComparer.OrdinalIgnoreCase);
        var claims = new Dictionary<int, string>();

        // Classes the import leaves alone keep their teacher: in Merge every class without a valid row, and valid rows
        // without a teacher code; in Replace the classes named only on rejected rows
        foreach (var cls in plan.ExistingClasses.Values.Where(c => c.HomeroomTeacherId.HasValue))
        {
            var row = plan.Rows.Classes.FirstOrDefault(r => r.IsValid && r.ClassName.Equals(cls.ClassName, StringComparison.OrdinalIgnoreCase));
            var keeps = plan.Mode == ClassImportModes.Merge
                ? !validNames.Contains(cls.ClassName) || row?.HomeroomTeacherCode == null
                : !validNames.Contains(cls.ClassName) && namedNames.Contains(cls.ClassName);
            if (keeps)
            {
                claims.TryAdd(cls.HomeroomTeacherId!.Value, cls.ClassName);
            }
        }

        foreach (var row in plan.Rows.Classes.Where(r => r.IsValid && r.HomeroomTeacherCode != null))
        {
            var teacher = staff[row.HomeroomTeacherCode!];
            if (claims.TryGetValue(teacher.Id, out var other) && !other.Equals(row.ClassName, StringComparison.OrdinalIgnoreCase))
            {
                row.Errors.Add($"Giáo viên {row.HomeroomTeacherCode} đã là giáo viên chủ nhiệm lớp {other} trong năm học này.");
                row.IsValid = false;
                continue;
            }

            claims[teacher.Id] = row.ClassName;
        }
    }

    /// <summary>Row numbers per non-empty key (case-insensitive), to find values given on more than one row.</summary>
    private static Dictionary<string, List<int>> RowsByKey<T>(IEnumerable<T> rows, Func<T, string?> key, Func<T, int> rowNumber)
        => rows
            .Select(r => (Key: key(r), Row: rowNumber(r)))
            .Where(x => !string.IsNullOrEmpty(x.Key))
            .GroupBy(x => x.Key!, StringComparer.OrdinalIgnoreCase)
            .ToDictionary(g => g.Key, g => g.Select(x => x.Row).ToList(), StringComparer.OrdinalIgnoreCase);

    private static void ValidateStudentRows(Plan plan, Dictionary<string, Student> students, bool isPreview)
    {
        var rowsByCode = RowsByKey(plan.Rows.Students, r => r.StudentCode, r => r.RowNumber);
        var classRows = plan.Rows.Classes
            .Where(r => r.ClassName.Length > 0)
            .GroupBy(r => r.ClassName, StringComparer.OrdinalIgnoreCase)
            .ToDictionary(g => g.Key, g => g.Any(r => r.IsValid), StringComparer.OrdinalIgnoreCase);

        var classGrades = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase);
        foreach (var c in plan.ExistingClasses.Values)
        {
            classGrades[c.ClassName] = c.Grade;
        }
        foreach (var cr in plan.Rows.Classes.Where(r => r.IsValid))
        {
            if (int.TryParse(cr.Grade, NumberStyles.Integer, CultureInfo.InvariantCulture, out var g))
            {
                classGrades[cr.ClassName] = g;
            }
        }

        foreach (var row in plan.Rows.Students)
        {
            row.RequiresAgeException = false;
            Student? matchedStudent = null;
            if (row.StudentCode.Length == 0)
            {
                row.Errors.Add("Thiếu mã học sinh (Student code).");
            }
            else if (rowsByCode[row.StudentCode].Count > 1)
            {
                // BR-17: a student appears once in the file; every repeated row is skipped
                row.Errors.Add($"Mã học sinh {row.StudentCode} xuất hiện nhiều lần ở trang {StudentsSheet} (dòng {string.Join(", ", rowsByCode[row.StudentCode])}).");
            }
            else if (!students.TryGetValue(row.StudentCode, out var student))
            {
                // BR-10: students must already exist in SAWP (UC-07, UC-09)
                row.Errors.Add($"Không tìm thấy học sinh có mã {row.StudentCode}.");
            }
            else if (!string.Equals(student.Status, "Active", StringComparison.OrdinalIgnoreCase))
            {
                row.Errors.Add($"Học sinh {row.StudentCode} không ở trạng thái đang học.");
            }
            else if (row.FullName != null && NormalizeName(row.FullName) != NormalizeName(student.FullName))
            {
                row.Errors.Add($"Họ và tên '{row.FullName}' không khớp với hồ sơ học sinh {row.StudentCode} ({student.FullName}).");
            }
            else
            {
                matchedStudent = student;
            }

            if (row.ClassName.Length == 0)
            {
                row.Errors.Add("Thiếu tên lớp (Class name).");
            }
            else if (classRows.TryGetValue(row.ClassName, out var classRowValid))
            {
                if (!classRowValid)
                {
                    row.Errors.Add($"Lớp {row.ClassName} không hợp lệ ở trang {ClassesSheet}.");
                }
            }
            else if (!plan.ExistingClasses.ContainsKey(row.ClassName))
            {
                row.Errors.Add($"Lớp {row.ClassName} không có ở trang {ClassesSheet} và chưa có trong năm học.");
            }
            else if (plan.Mode == ClassImportModes.Replace)
            {
                row.Errors.Add($"Lớp {row.ClassName} không có ở trang {ClassesSheet}; khi Thay thế, lớp không có trong tệp sẽ bị xóa.");
            }

            if (matchedStudent != null
                && matchedStudent.DateOfBirth != default
                && classGrades.TryGetValue(row.ClassName, out var targetGrade)
                && targetGrade >= 1 && targetGrade <= 5)
            {
                var gradeAgeErr = StudentValidationHelper.ValidateAgeForGrade(
                    matchedStudent.DateOfBirth,
                    targetGrade,
                    plan.Year.StartDate.Year,
                    matchedStudent.IsSpecialNeeds,
                    allowAgeException: plan.Rows.AllowAgeException);
                if (gradeAgeErr != null)
                {
                    if (StudentValidationHelper.IsOverPrimaryAge(matchedStudent.DateOfBirth, plan.Year.StartDate.Year))
                    {
                        row.RequiresAgeException = true;
                        if (isPreview)
                        {
                            plan.Warnings.Add($"Cảnh báo ngoại lệ quá tuổi: Học sinh {matchedStudent.StudentCode} ({matchedStudent.FullName}) vượt quá 15 tuổi khi xếp vào Khối {targetGrade} (cần xác nhận ngoại lệ trước khi nhập).");
                        }
                        else if (!plan.Rows.AllowAgeException)
                        {
                            row.Errors.Add(gradeAgeErr);
                        }
                    }
                    else
                    {
                        row.Errors.Add(gradeAgeErr);
                    }
                }
            }

            row.IsValid = row.Errors.Count == 0;
        }
    }

    private static void BuildFinalState(Plan plan, Dictionary<string, Student> students, List<ClassAssignment> currentAssignments, Dictionary<int, Class> existingById)
    {
        var replace = plan.Mode == ClassImportModes.Replace;
        // Classes after the import
        foreach (var row in plan.ValidClassRows)
        {
            plan.ExistingClasses.TryGetValue(row.ClassName, out var existing);
            plan.FinalClasses[row.ClassName] = new TargetClass
            {
                Name = row.ClassName,
                Grade = int.Parse(row.Grade, CultureInfo.InvariantCulture),
                Existing = existing,
                FromFile = true,
                IsChanged = existing == null
            };
        }

        // Replace: a class named only on rejected rows is kept as it is (a rejected row changes nothing)
        var namedClasses = plan.Rows.Classes.Select(r => r.ClassName).ToHashSet(StringComparer.OrdinalIgnoreCase);
        foreach (var existing in plan.ExistingClasses.Values)
        {
            if (plan.FinalClasses.ContainsKey(existing.ClassName)) continue;
            if (replace && !namedClasses.Contains(existing.ClassName))
            {
                plan.ClassesToRemove.Add(existing);
                plan.Diff.ClassesRemoved.Add(existing.ClassName);
                continue;
            }

            plan.FinalClasses[existing.ClassName] = new TargetClass { Name = existing.ClassName, Grade = existing.Grade, Existing = existing };
        }

        plan.Diff.ClassesAdded.AddRange(plan.FinalClasses.Values.Where(c => c.Existing == null).Select(c => c.Name));

        // Homeroom teachers
        var teacherRows = plan.ValidClassRows.ToDictionary(r => r.ClassName, r => r.HomeroomTeacherCode, StringComparer.OrdinalIgnoreCase);
        foreach (var target in plan.FinalClasses.Values)
        {
            var current = target.Existing?.HomeroomTeacher;
            StaffProfile? wanted;
            if (target.FromFile && teacherRows[target.Name] is { } code)
            {
                wanted = plan.TeachersByCode[code];
            }
            else
            {
                // A file row without a teacher code: Replace clears the teacher, Merge keeps it
                wanted = target.FromFile && replace ? null : current;
            }

            target.Teacher = wanted;
            if (target.Existing != null && target.Existing.HomeroomTeacherId != wanted?.Id)
            {
                target.IsChanged = true;
                plan.Diff.TeacherChanges.Add(new ClassImportTeacherChangeDto
                {
                    ClassName = target.Name,
                    FromTeacher = TeacherLabel(current),
                    ToTeacher = TeacherLabel(wanted)
                });
            }
            else if (target.Existing == null && wanted != null)
            {
                plan.Diff.TeacherChanges.Add(new ClassImportTeacherChangeDto { ClassName = target.Name, ToTeacher = TeacherLabel(wanted) });
            }
        }

        // Students after the import
        var currentByStudent = currentAssignments
            .GroupBy(ca => ca.StudentId)
            .ToDictionary(g => g.Key, g => g.First());
        var desired = plan.ValidStudentRows.ToDictionary(r => students[r.StudentCode].Id, r => r);

        foreach (var row in plan.ValidStudentRows)
        {
            var student = students[row.StudentCode];
            var target = plan.FinalClasses[row.ClassName];
            target.StudentCodes.Add(student.StudentCode);

            if (!currentByStudent.TryGetValue(student.Id, out var current))
            {
                plan.AssignmentsToAdd.Add((student, target.Name));
                plan.Diff.StudentsAdded.Add(Change(student, null, target.Name));
                target.IsChanged = true;
                continue;
            }

            var fromName = existingById.TryGetValue(current.ClassId, out var fromClass) ? fromClass.ClassName : null;
            if (fromName != null && fromName.Equals(target.Name, StringComparison.OrdinalIgnoreCase))
            {
                continue;
            }

            plan.AssignmentsToDelete.Add(current);
            plan.AssignmentsToAdd.Add((student, target.Name));
            plan.Diff.StudentsMoved.Add(Change(student, fromName, target.Name));
            target.IsChanged = true;
            if (fromName != null && plan.FinalClasses.TryGetValue(fromName, out var source))
            {
                source.IsChanged = true;
            }
        }

        var namedStudents = plan.Rows.Students.Select(r => r.StudentCode).ToHashSet(StringComparer.OrdinalIgnoreCase);
        foreach (var current in currentAssignments.Where(ca => !desired.ContainsKey(ca.StudentId)))
        {
            var className = existingById.TryGetValue(current.ClassId, out var cls) ? cls.ClassName : null;
            var classStays = className != null && plan.FinalClasses.TryGetValue(className, out var stays) ? stays : null;
            // Merge: students not in the file stay where they are. Replace: a student named only on rejected rows, or in a
            // class kept as it is, stays too
            var keep = classStays != null
                       && (!replace || namedStudents.Contains(current.Student?.StudentCode ?? string.Empty) || !classStays.FromFile);
            if (keep && classStays != null)
            {
                classStays.StudentCodes.Add(current.Student?.StudentCode ?? string.Empty);
                continue;
            }

            plan.AssignmentsToDelete.Add(current);
            plan.Diff.StudentsRemoved.Add(new ClassImportStudentChangeDto
            {
                StudentCode = current.Student?.StudentCode ?? string.Empty,
                FullName = current.Student?.FullName ?? string.Empty,
                FromClass = className
            });
            if (classStays != null)
            {
                classStays.IsChanged = true;
            }
        }

        // Warnings: class larger than its capacity (step 2), class without students
        foreach (var target in plan.FinalClasses.Values.OrderBy(c => c.Grade).ThenBy(c => c.Name))
        {
            var capacity = target.Existing?.Capacity ?? DefaultCapacity;
            if (target.StudentCodes.Count > capacity)
            {
                plan.Warnings.Add($"Lớp {target.Name} có {target.StudentCodes.Count} học sinh, vượt sĩ số tối đa {capacity}.");
            }

            if (target.FromFile && target.StudentCodes.Count == 0)
            {
                plan.Warnings.Add($"Lớp {target.Name} chưa có học sinh nào.");
            }
        }
    }

    #endregion

    #region Helpers

    private static ClassImportPreviewDto ToPreview(Plan p) => new()
    {
        SchoolYearId = p.Year.Id,
        SchoolYearName = p.Year.YearName,
        Mode = p.Mode,
        IsReimport = p.IsReimport,
        Classes = p.Rows.Classes,
        Students = p.Rows.Students,
        ClassSummaries = p.FinalClasses.Values
            .Select(c => new ClassImportClassSummaryDto
            {
                ClassName = c.Name,
                Grade = c.Grade,
                IsNew = c.Existing == null,
                StudentCount = c.StudentCodes.Count,
                Capacity = c.Existing?.Capacity ?? DefaultCapacity,
                HomeroomTeacherCode = c.Teacher?.StaffCode,
                HomeroomTeacherName = c.Teacher?.User?.FullName,
                IsChanged = c.IsChanged,
                StudentCodes = c.StudentCodes.OrderBy(s => s).ToList()
            })
            .Concat(p.ClassesToRemove.Select(c => new ClassImportClassSummaryDto
            {
                ClassName = c.ClassName,
                Grade = c.Grade,
                Capacity = c.Capacity,
                HomeroomTeacherCode = c.HomeroomTeacher?.StaffCode,
                HomeroomTeacherName = c.HomeroomTeacher?.User?.FullName,
                IsRemoved = true
            }))
            .OrderBy(c => c.Grade).ThenBy(c => c.ClassName)
            .ToList(),
        Diff = p.Diff,
        Warnings = p.Warnings,
        ValidClassRows = p.Rows.Classes.Count(r => r.IsValid),
        InvalidClassRows = p.Rows.Classes.Count(r => !r.IsValid),
        ValidStudentRows = p.Rows.Students.Count(r => r.IsValid),
        InvalidStudentRows = p.Rows.Students.Count(r => !r.IsValid)
    };

    private static List<string> RowErrors(Plan p)
        => p.Rows.Classes.Where(r => !r.IsValid)
            .Select(r => $"Trang {ClassesSheet}, dòng {r.RowNumber}: {string.Join(" ", r.Errors)}")
            .Concat(p.Rows.Students.Where(r => !r.IsValid)
                .Select(r => $"Trang {StudentsSheet}, dòng {r.RowNumber}: {string.Join(" ", r.Errors)}"))
            .ToList();

    private static ClassImportStudentChangeDto Change(Student s, string? from, string? to) => new()
    {
        StudentCode = s.StudentCode,
        FullName = s.FullName,
        FromClass = from,
        ToClass = to
    };

    private static string? TeacherLabel(StaffProfile? t)
        => t == null ? null : string.IsNullOrEmpty(t.User?.FullName) ? t.StaffCode : $"{t.User!.FullName} ({t.StaffCode})";

    private static bool IsTeacher(StaffProfile sp)
        => sp.User?.UserRoles?.Any(ur => ur.Role != null && ur.Role.Name == UserRoles.Teacher.ToString()) == true;

    private static string? NullIfEmpty(string? value) => string.IsNullOrWhiteSpace(value) ? null : value;

    /// <summary>Lower case, no accents, single spaces: header and sheet names are matched this way.</summary>
    internal static string NormalizeKey(string? value)
    {
        if (string.IsNullOrWhiteSpace(value)) return string.Empty;
        var decomposed = value.Trim().Normalize(NormalizationForm.FormD);
        var sb = new StringBuilder(decomposed.Length);
        foreach (var ch in decomposed)
        {
            if (CharUnicodeInfo.GetUnicodeCategory(ch) != UnicodeCategory.NonSpacingMark)
            {
                sb.Append(ch);
            }
        }

        return Regex.Replace(sb.ToString().Normalize(NormalizationForm.FormC).Replace('đ', 'd').Replace('Đ', 'D'), @"\s+", " ").ToLowerInvariant();
    }

    /// <summary>Names are compared ignoring case and extra spaces, but not accents (different accents = different name).</summary>
    private static string NormalizeName(string? value)
        => Regex.Replace((value ?? string.Empty).Normalize(NormalizationForm.FormC).Trim(), @"\s+", " ").ToLowerInvariant();

    #endregion
}
