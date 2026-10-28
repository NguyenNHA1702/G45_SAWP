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
}
