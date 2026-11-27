using Core.DTOs;

namespace Services.Interfaces;

/// <summary>UC-69 Import Class Assignments into a Draft school year.</summary>
public interface IClassImporter
{
    /// <summary>Reads sheets "Classes" and "Students" of an .xlsx file; MSG41 when it cannot (E1).</summary>
    ApiResponse<ClassImportRowsDto> ParseWorkbook(Stream fileStream);

    /// <summary>Validates the rows against the Draft school year and shows what the import would do (step 3, A2).</summary>
    Task<ApiResponse<ClassImportPreviewDto>> PreviewAsync(int schoolYearId, ClassImportRowsDto rows, CancellationToken cancellationToken = default);

    /// <summary>Validates the rows again and imports the valid ones in one transaction (steps 4-6).</summary>
    Task<ApiResponse<ClassImportResultDto>> ImportAsync(int schoolYearId, ClassImportRowsDto rows, int? actorUserId = null, CancellationToken cancellationToken = default);

    /// <summary>The .xlsx template with both sheets, sample rows and instructions.</summary>
    byte[] GenerateTemplate();
}
