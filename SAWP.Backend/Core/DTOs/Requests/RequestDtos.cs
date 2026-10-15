using Core.Enums;

namespace Core.DTOs;

public class ClassTransferRequestDto
{
    public int StudentId { get; set; }
    public int CurrentClassId { get; set; }
    public int TargetClassId { get; set; }
    public string TransferReason { get; set; } = string.Empty;
}

public class DocumentIssuanceRequestDto
{
    public int StudentId { get; set; }
    public string DocumentType { get; set; } = string.Empty;
    public int NumberOfCopies { get; set; } = 1;
    public string Purpose { get; set; } = string.Empty;
}

public class ProcessRequestDto
{
    public RequestStatus NewStatus { get; set; }
    public string? ReviewerNotes { get; set; }
}
