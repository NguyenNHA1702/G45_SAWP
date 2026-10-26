using Core.Enums;

namespace Core.DTOs;

public class ClassTransferRequestDto
{
    public int StudentId { get; set; }
    public int? CurrentClassId { get; set; }
    public int TargetClassId { get; set; }
    public string TransferReason { get; set; } = string.Empty;
    public string? AttachmentUrl { get; set; }
}

public class DocumentIssuanceRequestDto
{
    public int StudentId { get; set; }
    public string DocumentType { get; set; } = string.Empty;
    public int NumberOfCopies { get; set; } = 1;
    public string Purpose { get; set; } = string.Empty;
    public string? AttachmentUrl { get; set; }
}

public class ResubmitRequestDto
{
    public string? TransferReason { get; set; }
    public string? Purpose { get; set; }
    public string? AttachmentUrl { get; set; }
}

public class StaffAmendmentDto
{
    public string AmendmentNotes { get; set; } = string.Empty;
}

public class StaffEndorseTransferDto
{
    public string StaffRecommendation { get; set; } = string.Empty;
}

public class StaffEndorseDocumentDto
{
    public string StaffEndorsement { get; set; } = string.Empty;
    public string? DocumentContentDraft { get; set; }
}

public class BoardDecisionDto
{
    public bool IsApproved { get; set; }
    public string? DecisionNotes { get; set; }
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
