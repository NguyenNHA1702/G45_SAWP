namespace Core.DTOs;

/// <summary>Re-import choices of UC-69 A2.</summary>
public static class ClassImportModes
{
    /// <summary>
    /// The Draft becomes exactly the valid rows of the file; manual changes are lost. Classes and students named only on
    /// rejected rows stay as they are.
    /// </summary>
    public const string Replace = "Replace";

    /// <summary>Only the differences are applied; for students in the file the file wins.</summary>
    public const string Merge = "Merge";
}

/// <summary>A row of sheet "Classes": one class and its optional homeroom teacher (UC-69 step 1).</summary>
public class ClassImportClassRowDto
{
    public int RowNumber { get; set; }
    public string ClassName { get; set; } = string.Empty;

    /// <summary>Kept as text so that an invalid value can be shown back to the user.</summary>
    public string Grade { get; set; } = string.Empty;

    /// <summary>Staff code of the homeroom teacher (optional).</summary>
    public string? HomeroomTeacherCode { get; set; }

    public bool IsValid { get; set; } = true;
    public List<string> Errors { get; set; } = new();
}

/// <summary>A row of sheet "Students": one student and the class the school placed them in (UC-69 step 1).</summary>
public class ClassImportStudentRowDto
{
    public int RowNumber { get; set; }
    public string StudentCode { get; set; } = string.Empty;

    /// <summary>Cross-check only; never written to the student record.</summary>
    public string? FullName { get; set; }

    public string ClassName { get; set; } = string.Empty;
    public bool RequiresAgeException { get; set; }
    public bool IsValid { get; set; } = true;
    public List<string> Errors { get; set; } = new();
}

/// <summary>The parsed file contents, sent back to preview again (another mode) or to confirm.</summary>
public class ClassImportRowsDto
{
    public string Mode { get; set; } = ClassImportModes.Merge;
    public bool AllowAgeException { get; set; }
    public List<ClassImportClassRowDto> Classes { get; set; } = new();
    public List<ClassImportStudentRowDto> Students { get; set; } = new();
}

public class ClassImportClassSummaryDto
{
    public string ClassName { get; set; } = string.Empty;
    public int Grade { get; set; }
    public bool IsNew { get; set; }
    public int StudentCount { get; set; }
    public int Capacity { get; set; }
    public string? HomeroomTeacherCode { get; set; }
    public string? HomeroomTeacherName { get; set; }

    /// <summary>The import changes this class, so it is (or returns to) Not reviewed (UC-69 A2, BR-72).</summary>
    public bool IsChanged { get; set; }

    /// <summary>Replace mode: the class is not in the file and is removed with its assignments.</summary>
    public bool IsRemoved { get; set; }

    public List<string> StudentCodes { get; set; } = new();
}

public class ClassImportStudentChangeDto
{
    public string StudentCode { get; set; } = string.Empty;
    public string FullName { get; set; } = string.Empty;
    public string? FromClass { get; set; }
    public string? ToClass { get; set; }
}

public class ClassImportTeacherChangeDto
{
    public string ClassName { get; set; } = string.Empty;
    public string? FromTeacher { get; set; }
    public string? ToTeacher { get; set; }
}

/// <summary>Differences between the file and the current Draft (UC-69 A2).</summary>
public class ClassImportDiffDto
{
    public List<string> ClassesAdded { get; set; } = new();
    public List<string> ClassesRemoved { get; set; } = new();
    public List<ClassImportStudentChangeDto> StudentsAdded { get; set; } = new();
    public List<ClassImportStudentChangeDto> StudentsRemoved { get; set; } = new();
    public List<ClassImportStudentChangeDto> StudentsMoved { get; set; } = new();
    public List<ClassImportTeacherChangeDto> TeacherChanges { get; set; } = new();
}

/// <summary>Preview of UC-69 step 3: classes, students per class, valid and invalid rows, warnings, differences.</summary>
public class ClassImportPreviewDto
{
    public int SchoolYearId { get; set; }
    public string SchoolYearName { get; set; } = string.Empty;
    public string Mode { get; set; } = ClassImportModes.Merge;

    /// <summary>The Draft already has classes, so the import is a re-import (UC-69 A2).</summary>
    public bool IsReimport { get; set; }

    public List<ClassImportClassRowDto> Classes { get; set; } = new();
    public List<ClassImportStudentRowDto> Students { get; set; } = new();
    public List<ClassImportClassSummaryDto> ClassSummaries { get; set; } = new();
    public ClassImportDiffDto Diff { get; set; } = new();
    public List<string> Warnings { get; set; } = new();

    public int ValidClassRows { get; set; }
    public int InvalidClassRows { get; set; }
    public int ValidStudentRows { get; set; }
    public int InvalidStudentRows { get; set; }
}

/// <summary>Import totals and error list (UC-69 step 6).</summary>
public class ClassImportResultDto
{
    public int ClassesCreated { get; set; }
    public int ClassesRemoved { get; set; }
    public int StudentsAdded { get; set; }
    public int StudentsMoved { get; set; }
    public int StudentsRemoved { get; set; }
    public int TeacherChanges { get; set; }

    /// <summary>Classes that are Not reviewed because of this import.</summary>
    public int ClassesToReview { get; set; }

    public int SkippedRows { get; set; }
    public List<string> Errors { get; set; } = new();
    public List<string> Warnings { get; set; } = new();
}
