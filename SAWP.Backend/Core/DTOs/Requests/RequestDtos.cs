using Core.Enums;

namespace Core.DTOs;

public class ClassTransferRequestDto
{
    public int StudentId { get; set; }
    public int? CurrentClassId { get; set; }
    public int TargetClassId { get; set; }
    public string TransferReason { get; set; } = string.Empty;
    public string? AttachmentUrl { get; set; }
    public DateTime? EffectiveDate { get; set; }
}

public class DocumentIssuanceRequestDto
{
    public int StudentId { get; set; }
    public string DocumentType { get; set; } = string.Empty;
    public int NumberOfCopies { get; set; } = 1;
    public string Purpose { get; set; } = string.Empty;
    public string? AttachmentUrl { get; set; }
    public bool IsPaperCopyRequested { get; set; }
}

public class OtherRequestDto
{
    public int StudentId { get; set; }
    public string Title { get; set; } = string.Empty;
    public string Content { get; set; } = string.Empty;
    public string? AttachmentUrl { get; set; }
}

public class ResubmitRequestDto
{
    public string? TransferReason { get; set; }
    public string? Purpose { get; set; }
    public int? NumberOfCopies { get; set; }
    public string? AdditionalNotes { get; set; }
    public string? AttachmentUrl { get; set; }
}

public class StaffAmendmentDto
{
    public string AmendmentNotes { get; set; } = string.Empty;
}

public class StaffRejectDto
{
    public string Reason { get; set; } = string.Empty;
}

public class StaffDirectIssueDocumentDto
{
    public string? CertificateNumber { get; set; }
    public DateTime? IssuedDate { get; set; }
    public string? DocumentContent { get; set; }
    public string? DocumentFileUrl { get; set; }
    public string? StaffNotes { get; set; }
    public bool IsPaperCopy { get; set; }
}

public class StaffEndorseTransferDto
{
    public string StaffRecommendation { get; set; } = string.Empty;
    public DateTime? EffectiveDate { get; set; }
}

public class StaffEndorseDocumentDto
{
    public string StaffEndorsement { get; set; } = string.Empty;
    public string? DocumentContentDraft { get; set; }
    public string? DocumentFileUrl { get; set; }
    public string? CertificateNumber { get; set; }
    public DateTime? IssuedDate { get; set; }
    public bool IsPaperCopyRequested { get; set; }
}

public class BoardDecisionDto
{
    public bool IsApproved { get; set; }
    public bool ReturnToStaff { get; set; }
    public string? DecisionNotes { get; set; }
    public string? SignedDocumentUrl { get; set; }
    public DateTime? EffectiveDate { get; set; }
}

public class ProcessRequestDto
{
    public RequestStatus NewStatus { get; set; }
    public string? ReviewerNotes { get; set; }
}

public class ClassTransferSummaryDto
{
    public int FromClassId { get; set; }
    public string FromClassName { get; set; } = string.Empty;
    public int ToClassId { get; set; }
    public string ToClassName { get; set; } = string.Empty;
    public string TransferReason { get; set; } = string.Empty;
    public string? StaffRecommendation { get; set; }
    public int? TargetClassCapacity { get; set; }
    public int? TargetClassMaxCapacity { get; set; }
    public bool IsExecuted { get; set; }
    public DateTime? ExecutedAt { get; set; }
    public DateTime? EffectiveDate { get; set; }
}

public class DocumentRequestSummaryDto
{
    public string DocumentType { get; set; } = string.Empty;
    public int NumberOfCopies { get; set; } = 1;
    public string Purpose { get; set; } = string.Empty;
    public string? StaffEndorsement { get; set; }
    public string? DocumentContentDraft { get; set; }
    public DateTime? IssuedDate { get; set; }
    public string? IssuedDocumentUrl { get; set; }
    public string? CertificateNumber { get; set; }
    public string? DigitalSignatureInfo { get; set; }
    public bool IsPaperCopyRequested { get; set; }
}

public class AdministrativeRequestSummaryDto
{
    public int Id { get; set; }
    public string RequestNumber { get; set; } = string.Empty;
    public string RequestType { get; set; } = string.Empty;
    public RequestStatus Status { get; set; }
    public DateTime SubmissionDate { get; set; }
    public DateTime? ProcessedDate { get; set; }
    public int RequesterUserId { get; set; }
    public string RequesterName { get; set; } = string.Empty;
    public int? StudentId { get; set; }
    public string? StudentCode { get; set; }
    public string? StudentName { get; set; }
    public string? CurrentClassName { get; set; }

    public bool RequiresAmendment { get; set; }
    public string? AttachmentUrl { get; set; }
    public string? AmendmentNotes { get; set; }
    public string? ReviewerNotes { get; set; }
    public string? SchoolBoardNotes { get; set; }
    public DateTime? SchoolBoardApprovedAt { get; set; }

    public ClassTransferSummaryDto? ClassTransfer { get; set; }
    public DocumentRequestSummaryDto? DocumentRequest { get; set; }
}

public class RequestHistoryItemDto
{
    public DateTime Timestamp { get; set; }
    public string Action { get; set; } = string.Empty;
    public string Description { get; set; } = string.Empty;
    public string? PerformedBy { get; set; }
    public RequestStatus Status { get; set; }
}

public class RequestStatisticsDto
{
    public int TotalRequests { get; set; }
    public int SubmittedCount { get; set; }
    public int UnderReviewCount { get; set; }
    public int ApprovedCount { get; set; }
    public int CompletedCount { get; set; }
    public int RejectedCount { get; set; }
    public int CancelledCount { get; set; }
    public int ClassTransferCount { get; set; }
    public int DocumentCount { get; set; }
    public int RequiringAmendmentCount { get; set; }
}
