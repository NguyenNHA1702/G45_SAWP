using Core.DTOs;

namespace Services.Interfaces;

public interface IRequestWorkflowEngine
{
    Task<ApiResponse<string>> SubmitClassTransferRequestAsync(int requesterUserId, ClassTransferRequestDto request, CancellationToken cancellationToken = default);
    Task<ApiResponse<string>> SubmitDocumentRequestAsync(int requesterUserId, DocumentIssuanceRequestDto request, CancellationToken cancellationToken = default);
    Task<ApiResponse<bool>> ProcessRequestAsync(int requestId, int reviewerUserId, ProcessRequestDto request, CancellationToken cancellationToken = default);
}
