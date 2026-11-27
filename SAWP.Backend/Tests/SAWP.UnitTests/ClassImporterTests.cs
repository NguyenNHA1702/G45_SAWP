using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using ClosedXML.Excel;
using Core.Constants;
using Core.DTOs;
using Core.Entities;
using Core.Enums;
using FluentAssertions;
using Moq;
using Services.Implementations;
using Xunit;

namespace SAWP.UnitTests;

/// <summary>UC-69 Import Class Assignments (BR-09, BR-10, BR-15, BR-16, BR-17, BR-70, BR-72; MSG41, MSG48, MSG49, MSG62).</summary>
public class ClassImporterTests : TestBase
{
    private const int YearId = 5;

    private readonly ClassImporter _importer;
    private readonly SchoolYear _year;
    private readonly List<Class> _classes = new();
    private readonly List<ClassAssignment> _assignments = new();
    private readonly List<Student> _students = new();
    private readonly List<StaffProfile> _staff = new();

    public ClassImporterTests()
    {
        _importer = new ClassImporter(UnitOfWorkMock.Object, AuditServiceMock.Object);
        _year = new SchoolYear
        {
            Id = YearId,
            YearName = "2027-2028",
            StartDate = new DateTime(2027, 9, 1),
            EndDate = new DateTime(2028, 5, 31),
            IsActive = false,
            Status = SchoolYearStatuses.Draft
        };

        SetupRepository(new List<SchoolYear> { _year });
        SetupRepository(_classes);
        SetupRepository(_assignments);
        SetupRepository(_students);
        SetupRepository(_staff);
        SetupRepository(new List<ClassSchedule>());

        for (var i = 1; i <= 6; i++)
        {
            _students.Add(new Student { Id = i, StudentCode = $"HS00{i}", FullName = $"Nguyễn Văn {(char)('A' + i - 1)}", Status = "Active" });
        }
        _students.Add(new Student { Id = 7, StudentCode = "HS007", FullName = "Trần Thị G", Status = "Inactive" });

        _staff.Add(Staff(1, "GV001", "Lê Thị Hoa", UserRoles.Teacher));
        _staff.Add(Staff(2, "GV002", "Phạm Văn Minh", UserRoles.Teacher));
        _staff.Add(Staff(3, "NV001", "Đỗ Văn Bảo", UserRoles.ServiceStaff));
        _staff.Add(Staff(4, "GV004", "Hoàng Lan", UserRoles.Teacher, AccountStatus.Deactivated));
    }

    #region Helpers

    private static StaffProfile Staff(int id, string code, string name, UserRoles role, AccountStatus status = AccountStatus.Active)
    {
        var user = new User { Id = 100 + id, FullName = name, Status = status };
        user.UserRoles.Add(new UserRole { UserId = user.Id, Role = new Role { Id = (int)role, Name = role.ToString() } });
        return new StaffProfile { Id = id, UserId = user.Id, User = user, StaffCode = code };
    }

    private Class ExistingClass(int id, string name, int grade, StaffProfile? teacher = null, bool reviewed = false, int capacity = 35)
    {
        var cls = new Class
        {
            Id = id,
            SchoolYearId = YearId,
            SchoolYear = _year,
            ClassName = name,
            Grade = grade,
            Capacity = capacity,
            HomeroomTeacherId = teacher?.Id,
            HomeroomTeacher = teacher,
            IsReviewed = reviewed,
            ReviewedAt = reviewed ? DateTime.UtcNow : null
        };
        _classes.Add(cls);
        return cls;
    }

    private ClassAssignment DraftAssignment(int id, Class cls, int studentId)
    {
        var ca = new ClassAssignment
        {
            Id = id,
            ClassId = cls.Id,
            Class = cls,
            StudentId = studentId,
            Student = _students.First(s => s.Id == studentId),
            IsActive = false,
            IsDraft = true
        };
        _assignments.Add(ca);
        return ca;
    }

    private static ClassImportRowsDto Rows(string mode, (string Name, string Grade, string? Teacher)[] classes, (string Code, string? Name, string Class)[] students)
        => new()
        {
            Mode = mode,
            Classes = classes.Select((c, i) => new ClassImportClassRowDto { RowNumber = i + 2, ClassName = c.Name, Grade = c.Grade, HomeroomTeacherCode = c.Teacher }).ToList(),
            Students = students.Select((s, i) => new ClassImportStudentRowDto { RowNumber = i + 2, StudentCode = s.Code, FullName = s.Name, ClassName = s.Class }).ToList()
        };

    private static MemoryStream Workbook(Action<XLWorkbook> build)
    {
        var wb = new XLWorkbook();
        build(wb);
        var ms = new MemoryStream();
        wb.SaveAs(ms);
        ms.Position = 0;
        return ms;
    }

    private static void Fill(IXLWorksheet ws, params string[][] rows)
    {
        for (var r = 0; r < rows.Length; r++)
            for (var c = 0; c < rows[r].Length; c++)
                ws.Cell(r + 1, c + 1).SetValue(rows[r][c]);
    }

    #endregion

    #region Method 01: ParseWorkbook (UTCID01 - UTCID05)

    [Fact]
    public void UTCID01_ParseWorkbook_ValidFile_ReadsBothSheets()
    {
        // Arrange: headers in another order and case, an empty row in between
        using var stream = Workbook(wb =>
        {
            Fill(wb.Worksheets.Add("classes"),
                new[] { "Homeroom teacher code", "GRADE", "class name" },
                new[] { "GV001", "1", "1a" },
                new[] { "", "", "" },
                new[] { "", "2", "2A" });
            Fill(wb.Worksheets.Add("Students"),
                new[] { "Student code", "Full name", "Class name" },
                new[] { "HS001", "Nguyễn Văn A", "1A" },
                new[] { "HS002", "", "2A" });
        });

        // Act
        var result = _importer.ParseWorkbook(stream);

        // Assert
        result.Success.Should().BeTrue();
        result.Data!.Classes.Should().HaveCount(2);
        result.Data.Classes[0].ClassName.Should().Be("1a");
        result.Data.Classes[0].HomeroomTeacherCode.Should().Be("GV001");
        result.Data.Classes[1].RowNumber.Should().Be(4);
        result.Data.Classes[1].HomeroomTeacherCode.Should().BeNull();
        result.Data.Students.Should().HaveCount(2);
        result.Data.Students[1].FullName.Should().BeNull();
    }

    [Fact]
    public void UTCID02_ParseWorkbook_MissingSheet_ReturnsMSG41()
    {
        // Arrange
        using var stream = Workbook(wb => Fill(wb.Worksheets.Add("Classes"), new[] { "Class name", "Grade" }));

        // Act
        var result = _importer.ParseWorkbook(stream);

        // Assert: E1
        result.Success.Should().BeFalse();
        result.Code.Should().Be(SystemMessages.MSG41.Code);
        result.Errors.Should().ContainSingle(e => e.Contains("Students"));
    }

    [Fact]
    public void UTCID03_ParseWorkbook_MissingRequiredColumn_ReturnsMSG41()
    {
        // Arrange
        using var stream = Workbook(wb =>
        {
            Fill(wb.Worksheets.Add("Classes"), new[] { "Class name" });
            Fill(wb.Worksheets.Add("Students"), new[] { "Student code", "Class name" });
        });

        // Act
        var result = _importer.ParseWorkbook(stream);

        // Assert
        result.Success.Should().BeFalse();
        result.Code.Should().Be(SystemMessages.MSG41.Code);
        result.Errors.Should().ContainSingle(e => e.Contains("Grade"));
    }

    [Fact]
    public void UTCID04_ParseWorkbook_CorruptedFile_ReturnsMSG41()
    {
        // Arrange
        using var stream = new MemoryStream(new byte[] { 1, 2, 3, 4, 5 });

        // Act
        var result = _importer.ParseWorkbook(stream);

        // Assert
        result.Success.Should().BeFalse();
        result.Code.Should().Be(SystemMessages.MSG41.Code);
    }

    [Fact]
    public void UTCID05_GenerateTemplate_ParsesBackWithSampleRows()
    {
        // Act
        using var stream = new MemoryStream(_importer.GenerateTemplate());
        var result = _importer.ParseWorkbook(stream);

        // Assert
        result.Success.Should().BeTrue();
        result.Data!.Classes.Select(c => c.ClassName).Should().Equal("1A", "2A", "2B");
        result.Data.Students.Should().HaveCount(4);
        new XLWorkbook(new MemoryStream(_importer.GenerateTemplate())).Worksheets.Select(w => w.Name)
            .Should().Equal("Classes", "Students", "Instructions");
    }

    #endregion

    #region Method 02: PreviewAsync (UTCID01 - UTCID08)

    [Fact]
    public async Task UTCID01_Preview_YearNotDraft_ReturnsMSG62()
    {
        // Arrange
        _year.Status = SchoolYearStatuses.Active;
        _year.IsActive = true;

        // Act
        var result = await _importer.PreviewAsync(YearId, Rows(ClassImportModes.Merge, new[] { ("1A", "1", (string?)null) }, new[] { ("HS001", (string?)null, "1A") }));

        // Assert: E2
        result.Success.Should().BeFalse();
        result.Code.Should().Be(SystemMessages.MSG62.Code);
    }

    [Fact]
    public async Task UTCID02_Preview_FirstImport_ShowsClassesAndStudents()
    {
        // Arrange
        var rows = Rows(ClassImportModes.Merge,
            new[] { ("1a", "1", (string?)"gv001"), ("2A", "2", (string?)null) },
            new[] { ("hs001", (string?)"nguyễn  văn a", "1A"), ("HS002", (string?)null, "1A"), ("HS003", (string?)null, "2a") });

        // Act
        var result = await _importer.PreviewAsync(YearId, rows);

        // Assert
        result.Success.Should().BeTrue();
        var preview = result.Data!;
        preview.IsReimport.Should().BeFalse();
        preview.InvalidClassRows.Should().Be(0);
        preview.InvalidStudentRows.Should().Be(0);
        preview.ClassSummaries.Should().HaveCount(2);
        var c1A = preview.ClassSummaries.Single(c => c.ClassName == "1A");
        c1A.IsNew.Should().BeTrue();
        c1A.StudentCount.Should().Be(2);
        c1A.HomeroomTeacherCode.Should().Be("GV001");
        c1A.Capacity.Should().Be(ClassImporter.DefaultCapacity);
        preview.Diff.ClassesAdded.Should().BeEquivalentTo("1A", "2A");
        preview.Diff.StudentsAdded.Should().HaveCount(3);
        preview.Warnings.Should().BeEmpty();
    }

    [Fact]
    public async Task UTCID03_Preview_InvalidRows_ListedWithReasons()
    {
        // Arrange
        var rows = Rows(ClassImportModes.Merge,
            new[] { ("1A", "1", (string?)null), ("2B", "3", (string?)null), ("3A", "x", (string?)null), ("4A", "4", (string?)"GV999"), ("5A", "5", (string?)"NV001"), ("5B", "5", (string?)"GV004") },
            new[]
            {
                ("HS999", (string?)null, "1A"),         // unknown student (BR-10)
                ("HS007", (string?)null, "1A"),         // inactive student
                ("HS001", (string?)null, "1A"),         // repeated (BR-17)
                ("HS001", (string?)null, "1A"),
                ("HS002", (string?)"Trần Văn Z", "1A"), // name does not match the record
                ("HS003", (string?)null, "9Z"),         // class not in the file nor in the year
                ("", (string?)null, "1A")               // missing code
            });

        // Act
        var result = await _importer.PreviewAsync(YearId, rows);

        // Assert
        result.Success.Should().BeTrue();
        var p = result.Data!;
        p.Classes[0].IsValid.Should().BeTrue();
        p.Classes[1].Errors.Should().ContainSingle(e => e.Contains("không hợp lệ với Khối 3")); // name 2B, grade 3 (BR-15)
        p.Classes[2].Errors.Should().ContainSingle(e => e.Contains("Khối lớp 'x'"));
        p.Classes[3].Errors.Should().ContainSingle(e => e.Contains("GV999"));
        p.Classes[4].Errors.Should().ContainSingle(e => e.Contains("không phải tài khoản Giáo viên"));
        p.Classes[5].Errors.Should().ContainSingle(e => e.Contains("chưa được kích hoạt hoặc đã bị khóa"));

        p.Students.Select(s => s.IsValid).Should().AllBeEquivalentTo(false);
        p.Students[0].Errors.Should().ContainSingle(e => e.Contains("Không tìm thấy học sinh"));
        p.Students[1].Errors.Should().ContainSingle(e => e.Contains("không ở trạng thái đang học"));
        p.Students[2].Errors.Should().ContainSingle(e => e.Contains("xuất hiện nhiều lần"));
        p.Students[3].Errors.Should().ContainSingle(e => e.Contains("xuất hiện nhiều lần"));
        p.Students[4].Errors.Should().ContainSingle(e => e.Contains("không khớp"));
        p.Students[5].Errors.Should().ContainSingle(e => e.Contains("chưa có trong năm học"));
        p.Students[6].Errors.Should().ContainSingle(e => e.Contains("Thiếu mã học sinh"));
        p.ValidClassRows.Should().Be(1);
        p.InvalidStudentRows.Should().Be(7);
    }

    [Fact]
    public async Task UTCID04_Preview_TeacherCodeTwice_BothClassRowsAndTheirStudentsRejected()
    {
        // Arrange
        var rows = Rows(ClassImportModes.Merge,
            new[] { ("1A", "1", (string?)"GV001"), ("1B", "1", (string?)"GV001") },
            new[] { ("HS001", (string?)null, "1A"), ("HS002", (string?)null, "1B") });

        // Act
        var result = await _importer.PreviewAsync(YearId, rows);

        // Assert: BR-16; neither row is imported, and nor are the students of these classes
        var p = result.Data!;
        p.Classes.Should().OnlyContain(r => !r.IsValid && r.Errors.Single() == "Mã giáo viên GV001 xuất hiện nhiều lần ở trang Classes (dòng 2, 3).");
        p.Students[0].Errors.Should().ContainSingle(e => e.Contains("Lớp 1A không hợp lệ ở trang Classes"));
        p.Students[1].Errors.Should().ContainSingle(e => e.Contains("Lớp 1B không hợp lệ ở trang Classes"));
        p.ClassSummaries.Should().BeEmpty();
    }

    [Fact]
    public async Task UTCID08_Preview_ClassNameTwice_BothRowsAndTheirStudentsRejected()
    {
        // Arrange: 1A twice (once lower case, with another teacher); 2A once
        var rows = Rows(ClassImportModes.Merge,
            new[] { ("1A", "1", (string?)"GV001"), ("2A", "2", (string?)null), ("1a", "1", (string?)"GV002") },
            new[] { ("HS001", (string?)null, "1A"), ("HS002", (string?)null, "2A") });

        // Act
        var result = await _importer.PreviewAsync(YearId, rows);

        // Assert
        var p = result.Data!;
        p.Classes[0].Errors.Should().ContainSingle(e => e == "Lớp 1A xuất hiện nhiều lần ở trang Classes (dòng 2, 4).");
        p.Classes[2].Errors.Should().ContainSingle(e => e == "Lớp 1A xuất hiện nhiều lần ở trang Classes (dòng 2, 4).");
        p.Classes[1].IsValid.Should().BeTrue();
        p.Students[0].Errors.Should().ContainSingle(e => e.Contains("Lớp 1A không hợp lệ ở trang Classes"));
        p.Students[1].IsValid.Should().BeTrue();
        p.ClassSummaries.Select(c => c.ClassName).Should().Equal("2A");
    }

    [Fact]
    public async Task UTCID05_Preview_OverCapacityAndEmptyClass_AreWarnings()
    {
        // Arrange: existing class 1A has capacity 26 and 25 Draft students; the file adds two more
        var c1A = ExistingClass(1, "1A", 1, capacity: 26);
        for (var i = 0; i < 25; i++)
        {
            var s = new Student { Id = 200 + i, StudentCode = $"HSX{i:D2}", FullName = $"Học sinh {i}", Status = "Active" };
            _students.Add(s);
            DraftAssignment(300 + i, c1A, s.Id);
        }

        var rows = Rows(ClassImportModes.Merge,
            new[] { ("1A", "1", (string?)null), ("2A", "2", (string?)null) },
            new[] { ("HS001", (string?)null, "1A"), ("HS002", (string?)null, "1A") });

        // Act
        var result = await _importer.PreviewAsync(YearId, rows);

        // Assert: the rows stay valid
        var p = result.Data!;
        p.IsReimport.Should().BeTrue();
        p.InvalidStudentRows.Should().Be(0);
        p.Warnings.Should().Contain(w => w.Contains("Lớp 1A có 27 học sinh, vượt sĩ số tối đa 26"));
        p.Warnings.Should().Contain(w => w.Contains("Lớp 2A chưa có học sinh nào"));
    }

    [Fact]
    public async Task UTCID06_Preview_ExistingClass_SameGradeReused_OtherGradeRejected()
    {
        // Arrange
        ExistingClass(1, "1A", 1);
        ExistingClass(2, "2A", 2);
        var rows = Rows(ClassImportModes.Merge,
            new[] { ("1A", "1", (string?)null) },
            new[] { ("HS001", (string?)null, "1A"), ("HS002", (string?)null, "2A") });

        // Act
        var result = await _importer.PreviewAsync(YearId, rows);

        // Assert: A3 – students go into the existing classes (Merge allows a class only in the year)
        var p = result.Data!;
        p.Classes[0].IsValid.Should().BeTrue();
        p.ClassSummaries.Single(c => c.ClassName == "1A").IsNew.Should().BeFalse();
        p.Students.Should().OnlyContain(s => s.IsValid);
        p.Diff.ClassesAdded.Should().BeEmpty();
    }

    [Fact]
    public async Task UTCID07_Preview_ClassExistsWithOtherGrade_RowInvalid()
    {
        // Arrange: the existing class is named "1A" but filed under grade 2 (legacy data)
        ExistingClass(1, "1A", 2);
        var rows = Rows(ClassImportModes.Merge, new[] { ("1A", "1", (string?)null) }, new[] { ("HS001", (string?)null, "1A") });

        // Act
        var result = await _importer.PreviewAsync(YearId, rows);

        // Assert
        result.Data!.Classes[0].Errors.Should().ContainSingle(e => e.Contains("đã có trong năm học với Khối 2"));
    }

    #endregion

    #region Method 03: ImportAsync (UTCID01 - UTCID09)

    [Fact]
    public async Task UTCID01_Import_FirstImport_CreatesDraftClassesAndAssignments()
    {
        // Arrange
        var rows = Rows(ClassImportModes.Merge,
            new[] { ("1A", "1", (string?)"GV001"), ("2A", "2", (string?)null) },
            new[] { ("HS001", (string?)null, "1A"), ("HS002", (string?)null, "1A"), ("HS003", (string?)null, "2A") });

        // Act
        var result = await _importer.ImportAsync(YearId, rows, 10);

        // Assert: step 5 – classes Not reviewed, Draft assignments with the year start date, teacher set
        result.Success.Should().BeTrue();
        result.Code.Should().Be(SystemMessages.MSG48.Code);
        result.Data!.ClassesCreated.Should().Be(2);
        result.Data.StudentsAdded.Should().Be(3);
        result.Data.SkippedRows.Should().Be(0);

        _classes.Should().HaveCount(2);
        var c1A = _classes.Single(c => c.ClassName == "1A");
        c1A.IsReviewed.Should().BeFalse();
        c1A.HomeroomTeacherId.Should().Be(1);
        c1A.Capacity.Should().Be(ClassImporter.DefaultCapacity);
        _staff.Single(s => s.Id == 1).IsHomeroomTeacher.Should().BeTrue();

        _assignments.Should().HaveCount(3);
        _assignments.Should().OnlyContain(a => a.IsDraft && !a.IsActive && a.StartDate == _year.StartDate && a.AcademicYear == "2027-2028");
        _assignments.Where(a => a.ClassId == c1A.Id).Select(a => a.StudentId).Should().BeEquivalentTo(new[] { 1, 2 });

        UnitOfWorkMock.Verify(u => u.CommitTransactionAsync(It.IsAny<CancellationToken>()), Times.Once);
        AuditServiceMock.Verify(a => a.LogActionAsync(10, AuditActions.ClassImportAssignments, nameof(SchoolYear), YearId.ToString(),
            It.IsAny<object?>(), It.IsAny<object?>(), It.IsAny<string?>(), It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task UTCID02_Import_SomeRowsInvalid_ImportsValidRows_MSG49()
    {
        // Arrange
        var rows = Rows(ClassImportModes.Merge,
            new[] { ("1A", "1", (string?)null) },
            new[] { ("HS001", (string?)null, "1A"), ("HS999", (string?)null, "1A") });

        // Act
        var result = await _importer.ImportAsync(YearId, rows, 10);

        // Assert: E4 – the invalid row is skipped and listed
        result.Success.Should().BeTrue();
        result.Code.Should().Be(SystemMessages.MSG49.Code);
        result.Data!.SkippedRows.Should().Be(1);
        result.Data.Errors.Should().ContainSingle(e => e.StartsWith("Trang Students, dòng 3:"));
        _assignments.Should().ContainSingle(a => a.StudentId == 1);
    }

    [Fact]
    public async Task UTCID03_Import_NoValidRows_ImportsNothing_MSG49()
    {
        // Arrange
        var rows = Rows(ClassImportModes.Merge,
            new[] { ("1A", "9", (string?)null) },
            new[] { ("HS999", (string?)null, "1A") });

        // Act
        var result = await _importer.ImportAsync(YearId, rows, 10);

        // Assert: E3
        result.Success.Should().BeFalse();
        result.Code.Should().Be(SystemMessages.MSG49.Code);
        result.Errors.Should().HaveCount(2);
        _classes.Should().BeEmpty();
        _assignments.Should().BeEmpty();
        UnitOfWorkMock.Verify(u => u.BeginTransactionAsync(It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task UTCID04_Import_YearNotDraft_ReturnsMSG62()
    {
        // Arrange
        _year.Status = SchoolYearStatuses.Archived;

        // Act
        var result = await _importer.ImportAsync(YearId, Rows(ClassImportModes.Merge, new[] { ("1A", "1", (string?)null) }, Array.Empty<(string, string?, string)>()), 10);

        // Assert
        result.Success.Should().BeFalse();
        result.Code.Should().Be(SystemMessages.MSG62.Code);
        _classes.Should().BeEmpty();
    }

    [Fact]
    public async Task UTCID05_Import_Merge_AppliesDifferencesOnly()
    {
        // Arrange: Draft has 1A (Reviewed: HS001, HS002), 1B (Reviewed: HS003), 2A (Reviewed: HS004)
        var c1A = ExistingClass(1, "1A", 1, _staff[0], reviewed: true);
        var c1B = ExistingClass(2, "1B", 1, reviewed: true);
        var c2A = ExistingClass(3, "2A", 2, reviewed: true);
        DraftAssignment(1, c1A, 1);
        var moved = DraftAssignment(2, c1A, 2);
        DraftAssignment(3, c1B, 3);
        DraftAssignment(4, c2A, 4);

        // The file moves HS002 to 1B and adds HS005 to 1B; 1A and 2A are not otherwise touched
        var rows = Rows(ClassImportModes.Merge,
            new[] { ("1B", "1", (string?)null) },
            new[] { ("HS002", (string?)null, "1B"), ("HS005", (string?)null, "1B") });

        // Act
        var preview = await _importer.PreviewAsync(YearId, rows);
        var result = await _importer.ImportAsync(YearId, rows, 10);

        // Assert: A2 Merge – only the changed classes return to Not reviewed
        preview.Data!.Diff.StudentsMoved.Should().ContainSingle(m => m.StudentCode == "HS002" && m.FromClass == "1A" && m.ToClass == "1B");
        preview.Data.Diff.StudentsRemoved.Should().BeEmpty();
        result.Success.Should().BeTrue();
        result.Data!.StudentsMoved.Should().Be(1);
        result.Data.StudentsAdded.Should().Be(1);
        _assignments.Should().NotContain(moved);
        _assignments.Where(a => a.ClassId == c1B.Id).Select(a => a.StudentId).Should().BeEquivalentTo(new[] { 3, 2, 5 });
        _assignments.Should().Contain(a => a.ClassId == c1A.Id && a.StudentId == 1);
        c1A.IsReviewed.Should().BeFalse(); // lost HS002
        c1B.IsReviewed.Should().BeFalse(); // gained students
        c2A.IsReviewed.Should().BeTrue();  // unchanged
        c1A.HomeroomTeacherId.Should().Be(1); // Merge keeps the teacher of a class not in the file
    }

    [Fact]
    public async Task UTCID06_Import_Replace_DraftBecomesTheFile()
    {
        // Arrange: Draft has 1A (teacher GV001, HS001, HS002) and 2A (HS003)
        var c1A = ExistingClass(1, "1A", 1, _staff[0], reviewed: true);
        var c2A = ExistingClass(2, "2A", 2, reviewed: true);
        DraftAssignment(1, c1A, 1);
        DraftAssignment(2, c1A, 2);
        DraftAssignment(3, c2A, 3);

        // The file has only 1A without a teacher, with HS001
        var rows = Rows(ClassImportModes.Replace,
            new[] { ("1A", "1", (string?)null) },
            new[] { ("HS001", (string?)null, "1A") });

        // Act
        var preview = await _importer.PreviewAsync(YearId, rows);
        var result = await _importer.ImportAsync(YearId, rows, 10);

        // Assert: A2 Replace – 2A removed, HS002 removed, teacher cleared
        preview.Data!.Diff.ClassesRemoved.Should().Equal("2A");
        preview.Data.Diff.StudentsRemoved.Select(s => s.StudentCode).Should().BeEquivalentTo("HS002", "HS003");
        preview.Data.ClassSummaries.Single(c => c.ClassName == "2A").IsRemoved.Should().BeTrue();
        result.Success.Should().BeTrue();
        result.Data!.ClassesRemoved.Should().Be(1);
        result.Data.StudentsRemoved.Should().Be(2);
        _classes.Should().ContainSingle().Which.Should().BeSameAs(c1A);
        _assignments.Should().ContainSingle(a => a.StudentId == 1 && a.ClassId == c1A.Id);
        c1A.HomeroomTeacherId.Should().BeNull();
        c1A.IsReviewed.Should().BeFalse();
    }

    [Fact]
    public async Task UTCID07_Import_Merge_TeacherTakenByKeptClass_RowInvalid()
    {
        // Arrange: GV001 is homeroom teacher of 1A, which the file does not mention
        ExistingClass(1, "1A", 1, _staff[0]);
        var rows = Rows(ClassImportModes.Merge,
            new[] { ("2A", "2", (string?)"GV001") },
            new[] { ("HS001", (string?)null, "2A") });

        // Act
        var result = await _importer.PreviewAsync(YearId, rows);

        // Assert: BR-16 in the state after the import
        result.Data!.Classes[0].Errors.Should().ContainSingle(e => e.Contains("đã là giáo viên chủ nhiệm lớp 1A"));
    }

    [Fact]
    public async Task UTCID08_Import_Merge_TeacherChange_ResetsReview()
    {
        // Arrange
        var c1A = ExistingClass(1, "1A", 1, _staff[0], reviewed: true);
        DraftAssignment(1, c1A, 1);
        var rows = Rows(ClassImportModes.Merge,
            new[] { ("1A", "1", (string?)"GV002") },
            new[] { ("HS001", (string?)null, "1A") });

        // Act
        var result = await _importer.ImportAsync(YearId, rows, 10);

        // Assert
        result.Success.Should().BeTrue();
        result.Data!.TeacherChanges.Should().Be(1);
        result.Data.ClassesToReview.Should().Be(1);
        c1A.HomeroomTeacherId.Should().Be(2);
        c1A.IsReviewed.Should().BeFalse();
    }

    [Fact]
    public async Task UTCID09_Import_Replace_RejectedRowsChangeNothing()
    {
        // Arrange: Draft has 1A (HS001, HS002) and 2A (HS003, teacher GV002, Reviewed)
        var c1A = ExistingClass(1, "1A", 1);
        var c2A = ExistingClass(2, "2A", 2, _staff[1], reviewed: true);
        DraftAssignment(1, c1A, 1);
        var repeated = DraftAssignment(2, c1A, 2);
        DraftAssignment(3, c2A, 3);

        // The file: 1A once; 2A twice (rejected); HS002 twice (rejected); HS001 in 1A
        var rows = Rows(ClassImportModes.Replace,
            new[] { ("1A", "1", (string?)null), ("2A", "2", (string?)null), ("2A", "2", (string?)null) },
            new[] { ("HS001", (string?)null, "1A"), ("HS002", (string?)null, "1A"), ("HS002", (string?)null, "1A") });

        // Act
        var result = await _importer.ImportAsync(YearId, rows, 10);

        // Assert: 2A and HS002 stay as they are; nothing is removed
        result.Success.Should().BeTrue();
        result.Code.Should().Be(SystemMessages.MSG49.Code);
        result.Data!.ClassesRemoved.Should().Be(0);
        result.Data.StudentsRemoved.Should().Be(0);
        _classes.Should().Contain(c2A);
        c2A.HomeroomTeacherId.Should().Be(2);
        c2A.IsReviewed.Should().BeTrue();
        _assignments.Should().Contain(repeated);
        _assignments.Should().Contain(a => a.StudentId == 3 && a.ClassId == c2A.Id);
    }

    #endregion
}
