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

public class StudentImporterTests : TestBase
{
    private readonly StudentImporter _importer;

    public StudentImporterTests()
    {
        _importer = new StudentImporter(UnitOfWorkMock.Object, AuditServiceMock.Object);
        SetupRepository(new List<Guardian>()); // parse and import read the existing guardian records
    }

    private static MemoryStream CreateExcelStream(Action<IXLWorksheet> populate)
    {
        var workbook = new XLWorkbook();
        var ws = workbook.Worksheets.Add("Students");
        ws.Cell(1, 1).Value = "Mã học sinh";
        ws.Cell(1, 2).Value = "Họ và tên";
        ws.Cell(1, 3).Value = "Ngày sinh";
        ws.Cell(1, 4).Value = "Giới tính";
        ws.Cell(1, 5).Value = "Lớp";
        ws.Cell(1, 6).Value = "Tên phụ huynh";
        ws.Cell(1, 7).Value = "SĐT phụ huynh";
        ws.Cell(1, 8).Value = "CCCD phụ huynh";
        ws.Cell(1, 9).Value = "Quan hệ";
        ws.Cell(1, 10).Value = "Email phụ huynh";

        populate(ws);

        var ms = new MemoryStream();
        workbook.SaveAs(ms);
        ms.Position = 0;
        return ms;
    }

    #region Method 08: ParseExcelAsync (UTCID01 - UTCID06)

    [Fact]
    public async Task UTCID01_ParseExcel_ValidFile_ReturnsParsedRows()
    {
        // Arrange
        SetupRepository(new List<Student>());
        using var stream = CreateExcelStream(ws =>
        {
            ws.Cell(2, 1).Value = "HS001";
            ws.Cell(2, 2).Value = "Nguyen Van A";
            ws.Cell(2, 3).Value = "2018-05-15";
            ws.Cell(2, 4).Value = "Nam";
            ws.Cell(2, 5).Value = "1A";
            ws.Cell(2, 6).Value = "Nguyen Van Bo";
            ws.Cell(2, 7).Value = "0901234567";
            ws.Cell(2, 8).Value = "001200000001";
            ws.Cell(2, 9).Value = "Bố";
        });

        // Act
        var result = await _importer.ParseExcelAsync(stream);

        // Assert
        result.Should().NotBeNull();
        result.Success.Should().BeTrue();
        result.Data.Should().HaveCount(1);
        result.Data![0].StudentCode.Should().Be("HS001");
        result.Data[0].IsValid.Should().BeTrue();
        result.Data[0].IsDuplicate.Should().BeFalse();
    }

    [Fact]
    public async Task UTCID02_ParseExcel_MissingStudentCode_MarksInvalid()
    {
        // Arrange
        SetupRepository(new List<Student>());
        using var stream = CreateExcelStream(ws =>
        {
            ws.Cell(2, 1).Value = "";
            ws.Cell(2, 2).Value = "Nguyen Van A";
        });

        // Act
        var result = await _importer.ParseExcelAsync(stream);

        // Assert
        result.Should().NotBeNull();
        result.Success.Should().BeTrue();
        result.Data.Should().HaveCount(1);
        result.Data![0].IsValid.Should().BeFalse();
        result.Data[0].ErrorMessage.Should().Contain("mã học sinh");
    }

    [Fact]
    public async Task UTCID03_ParseExcel_MissingFullName_MarksInvalid()
    {
        // Arrange
        SetupRepository(new List<Student>());
        using var stream = CreateExcelStream(ws =>
        {
            ws.Cell(2, 1).Value = "HS002";
            ws.Cell(2, 2).Value = "";
        });

        // Act
        var result = await _importer.ParseExcelAsync(stream);

        // Assert
        result.Should().NotBeNull();
        result.Success.Should().BeTrue();
        result.Data.Should().HaveCount(1);
        result.Data![0].IsValid.Should().BeFalse();
        result.Data[0].ErrorMessage.Should().Contain("họ và tên");
    }

    [Fact]
    public async Task UTCID04_ParseExcel_DuplicateCodeInFile_MarksDuplicate()
    {
        // Arrange
        SetupRepository(new List<Student>());
        using var stream = CreateExcelStream(ws =>
        {
            ws.Cell(2, 1).Value = "HS001";
            ws.Cell(2, 2).Value = "Nguyen Van A";
            ws.Cell(3, 1).Value = "HS001";
            ws.Cell(3, 2).Value = "Nguyen Van Duplicate";
        });

        // Act
        var result = await _importer.ParseExcelAsync(stream);

        // Assert
        result.Should().NotBeNull();
        result.Success.Should().BeTrue();
        result.Data.Should().HaveCount(2);
        result.Data![0].IsValid.Should().BeTrue();
        result.Data[1].IsValid.Should().BeFalse();
        result.Data[1].IsDuplicate.Should().BeTrue();
    }

    [Fact]
    public async Task UTCID05_ParseExcel_EmptyWorksheet_Fails()
    {
        // Arrange
        var workbook = new XLWorkbook();
        workbook.Worksheets.Add("EmptySheet");
        using var ms = new MemoryStream();
        workbook.SaveAs(ms);
        ms.Position = 0;

        // Act
        var result = await _importer.ParseExcelAsync(ms);

        // Assert
        result.Should().NotBeNull();
        result.Success.Should().BeFalse();
        result.Code.Should().Be(SystemMessages.MSG49.Code);
    }

    [Fact]
    public async Task UTCID06_ParseExcel_InvalidStream_Fails()
    {
        // Arrange
        using var invalidStream = new MemoryStream(new byte[] { 0x01, 0x02, 0x03, 0x04 });

        // Act
        var result = await _importer.ParseExcelAsync(invalidStream);

        // Assert
        result.Should().NotBeNull();
        result.Success.Should().BeFalse();
        result.Code.Should().Be(SystemMessages.MSG41.Code);
    }

    #endregion

    #region Method 09: ImportStudentsAsync (UTCID01 - UTCID02)

    [Fact]
    public async Task UTCID01_ImportStudents_ValidRows_CreatesStudentsAndAssignments()
    {
        // Arrange
        var schoolYear = new SchoolYear { Id = 1, YearName = "2026-2027", IsActive = true };
        SetupRepository(new List<SchoolYear> { schoolYear });
        ClassRepoMock.Setup(r => r.GetActiveSchoolYearAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(schoolYear);

        var targetClass = new Class { Id = 10, ClassName = "1A", SchoolYearId = 1, Capacity = 35 };
        ClassRepoMock.Setup(r => r.GetClassesBySchoolYearAsync(1, null, null, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new List<Class> { targetClass });

        SetupRepository(new List<Role> { new() { Id = 3, Name = UserRoles.Parent.ToString() } });
        SetupRepository(new List<User>());
        SetupRepository(new List<UserRole>());
        SetupRepository(new List<Guardian>());
        SetupRepository(new List<StudentGuardian>());
        SetupRepository(new List<ClassAssignment>());

        StudentRepoMock.Setup(r => r.ExistsByStudentCodeAsync("HS001", null, It.IsAny<CancellationToken>()))
            .ReturnsAsync(false);
        StudentRepoMock.Setup(r => r.AddAsync(It.IsAny<Student>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((Student s, CancellationToken _) => { s.Id = 1; return s; });

        var request = new ImportStudentsConfirmDto
        {
            SchoolYearId = 1,
            ValidRows = new List<ImportStudentRowDto>
            {
                new()
                {
                    RowNumber = 2,
                    StudentCode = "HS001",
                    FullName = "Nguyen Van A",
                    DateOfBirth = "2018-05-15",
                    Gender = "Nam",
                    ClassName = "1A",
                    GuardianName = "Nguyen Van Bo",
                    GuardianPhone = "0901234567",
                    Relationship = "Bố",
                    IsValid = true,
                    IsDuplicate = false
                }
            }
        };

        // Act
        var result = await _importer.ImportStudentsAsync(request, 10);

        // Assert
        result.Should().NotBeNull();
        result.Success.Should().BeTrue();
        result.Data!.SuccessCount.Should().Be(1);
        result.Data.SkippedCount.Should().Be(0);
        StudentRepoMock.Verify(r => r.AddAsync(It.Is<Student>(s => s.StudentCode == "HS001"), It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task UTCID02_ImportStudents_EmptyValidRows_Fails()
    {
        // Arrange
        var request = new ImportStudentsConfirmDto
        {
            ValidRows = new List<ImportStudentRowDto>()
        };

        // Act
        var result = await _importer.ImportStudentsAsync(request, 10);

        // Assert
        result.Should().NotBeNull();
        result.Success.Should().BeFalse();
        result.Code.Should().Be(SystemMessages.MSG49.Code);
    }

    #endregion

    #region SSAWP-152: guardian records only, matched by identity card or email (UC-09 A3, A4, BR-66)

    private (List<User> Users, List<Guardian> Guardians, List<StudentGuardian> Links) SetupImport(params Guardian[] existing)
    {
        var schoolYear = new SchoolYear { Id = 1, YearName = "2026-2027", IsActive = true };
        SetupRepository(new List<SchoolYear> { schoolYear });
        ClassRepoMock.Setup(r => r.GetClassesBySchoolYearAsync(1, null, null, It.IsAny<CancellationToken>())).ReturnsAsync(new List<Class>());
        var users = new List<User>();
        var guardians = existing.ToList();
        var links = new List<StudentGuardian>();
        SetupRepository(users);
        SetupRepository(new List<UserRole>());
        SetupRepository(guardians);
        SetupRepository(links);
        SetupRepository(new List<ClassAssignment>());
        var nextId = 100;
        StudentRepoMock.Setup(r => r.AddAsync(It.IsAny<Student>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((Student st, CancellationToken _) => { st.Id = nextId++; return st; });
        return (users, guardians, links);
    }

    private static ImportStudentRowDto Row(string code, string guardian, string? email = null, string idCard = "", string phone = "") => new()
    {
        RowNumber = 2, StudentCode = code, FullName = "Hoc Sinh " + code, DateOfBirth = "2019-01-01", Gender = "Nam",
        GuardianName = guardian, GuardianEmail = email, GuardianIdentityCard = idCard, GuardianPhone = phone, Relationship = "Mẹ"
    };

    [Fact]
    public async Task UTCID10_Import_CreatesGuardianRecordsAndNoUserAccounts()
    {
        var (users, guardians, links) = SetupImport();

        var result = await _importer.ImportStudentsAsync(new ImportStudentsConfirmDto
        {
            SchoolYearId = 1,
            ValidRows = new List<ImportStudentRowDto> { Row("HS901", "Tran Thi Me", "me@example.com", "001100000001", "0901111111"), Row("HS902", "Le Van Bo") }
        }, 10);

        result.Success.Should().BeTrue();
        users.Should().BeEmpty("UC-09: no user accounts are created");
        guardians.Should().HaveCount(2);
        guardians.Should().OnlyContain(g => g.UserId == null);
        links.Should().HaveCount(2).And.OnlyContain(l => l.IsPrimaryContact);
        result.Data!.GuardiansCreated.Should().Be(2);
        result.Data.GuardiansWithoutEmail.Should().Be(1, "A4: imported, not eligible for an account");
        result.Data.GuardiansEligibleForAccountIds.Should().HaveCount(1);
    }

    [Fact]
    public async Task UTCID11_Import_ReusesRecordByIdentityCardOrEmail_NeverByPhone()
    {
        var byId = new Guardian { Id = 1, FullName = "Me A", IdentityCardNumber = "001100000001" };
        var byEmail = new Guardian { Id = 2, FullName = "Bo B", Email = "bob@example.com" };
        var byPhone = new Guardian { Id = 3, FullName = "Ong C", PhoneNumber = "0903333333" };
        var (_, guardians, links) = SetupImport(byId, byEmail, byPhone);

        var result = await _importer.ImportStudentsAsync(new ImportStudentsConfirmDto
        {
            SchoolYearId = 1,
            ValidRows = new List<ImportStudentRowDto>
            {
                Row("HS911", "Me A", idCard: "0011 0000 0001", email: "mea@example.com"),
                Row("HS912", "Bo B", email: "BOB@example.com"),
                Row("HS913", "Ong C", phone: "0903333333")
            }
        });

        result.Data!.GuardiansReused.Should().Be(2);
        result.Data.GuardiansCreated.Should().Be(1, "the same phone number does not make it the same guardian");
        guardians.Should().HaveCount(4);
        byId.Email.Should().Be("mea@example.com", "an empty field of a reused record is filled");
        links.Select(l => l.GuardianId).Should().Contain(new[] { 1, 2 });
    }

    [Fact]
    public async Task UTCID12_Import_SiblingsInOneFile_ShareOneRecord()
    {
        var (_, guardians, links) = SetupImport();

        await _importer.ImportStudentsAsync(new ImportStudentsConfirmDto
        {
            SchoolYearId = 1,
            ValidRows = new List<ImportStudentRowDto> { Row("HS921", "Tran Thi Me", "me@example.com"), Row("HS922", "Tran Thi Me", "ME@example.com") }
        });

        guardians.Should().ContainSingle();
        links.Should().HaveCount(2);
    }

    [Fact]
    public async Task UTCID13_Import_IdentityCardAndEmailOfDifferentRecords_RowSkipped()
    {
        var (_, guardians, _) = SetupImport(
            new Guardian { Id = 1, FullName = "A", IdentityCardNumber = "001100000001" },
            new Guardian { Id = 2, FullName = "B", Email = "b@example.com" });

        var result = await _importer.ImportStudentsAsync(new ImportStudentsConfirmDto
        {
            SchoolYearId = 1,
            ValidRows = new List<ImportStudentRowDto> { Row("HS931", "A", "b@example.com", "001100000001") }
        });

        result.Data!.SuccessCount.Should().Be(0);
        result.Data.SkippedCount.Should().Be(1);
        result.Data.Errors.Should().ContainSingle().Which.Should().Contain("hai hồ sơ");
        guardians.Should().HaveCount(2);
        StudentRepoMock.Verify(r => r.AddAsync(It.IsAny<Student>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task UTCID14_ParseExcel_PreviewShowsNewReusedConflictAndEligibility()
    {
        SetupRepository(new List<Student>());
        SetupRepository(new List<Guardian>
        {
            new() { Id = 1, FullName = "Co San", Email = "cosan@example.com", UserId = 50 },
            new() { Id = 2, FullName = "Khac", IdentityCardNumber = "001100000009" }
        });
        using var stream = CreateExcelStream(ws =>
        {
            void Put(int r, string code, string guardian, string id, string email)
            {
                ws.Cell(r, 1).Value = code; ws.Cell(r, 2).Value = "Hoc Sinh"; ws.Cell(r, 6).Value = guardian;
                ws.Cell(r, 8).Value = id; ws.Cell(r, 10).Value = email;
            }
            Put(2, "HS941", "Moi Mot", "", "moi@example.com");     // new, eligible
            Put(3, "HS942", "Moi Mot", "", "moi@example.com");     // sibling: reuses the record of row 2
            Put(4, "HS943", "Co San", "", "cosan@example.com");    // reused, already has an account
            Put(5, "HS944", "Xung Dot", "001100000009", "cosan@example.com"); // conflict
            Put(6, "HS945", "Khong Email", "", "");                // new, not eligible
            Put(7, "HS946", "Sai Email", "", "khong-phai-email");  // invalid email
        });

        var rows = (await _importer.ParseExcelAsync(stream)).Data!;

        rows[0].GuardianMatch.Should().Be("New"); rows[0].GuardianEligibleForAccount.Should().BeTrue();
        rows[1].GuardianMatch.Should().Be("Reused");
        rows[2].GuardianMatch.Should().Be("Reused"); rows[2].GuardianEligibleForAccount.Should().BeFalse();
        rows[3].GuardianMatch.Should().Be("Conflict"); rows[3].IsValid.Should().BeFalse();
        rows[4].GuardianMatch.Should().Be("New"); rows[4].GuardianEligibleForAccount.Should().BeFalse();
        rows[5].IsValid.Should().BeFalse(); rows[5].ErrorMessage.Should().Contain("Email");
    }

    #endregion
}
