using Core.DTOs;
using Core.Interfaces;
using Services.Interfaces;

namespace Services.Implementations;

public class RequestWorkflowEngine : IRequestWorkflowEngine
{
    private readonly IUnitOfWork _unitOfWork;

    public RequestWorkflowEngine(IUnitOfWork unitOfWork)
    {
        _unitOfWork = unitOfWork;
    }

    public async Task<ApiResponse<string>> SubmitClassTransferRequestAsync(int requesterUserId, ClassTransferRequestDto request, CancellationToken cancellationToken = default)
    {
        await Task.CompletedTask;
        return ApiResponse<string>.Ok("REQ-TRANSFER-001");
    }

    public async Task<ApiResponse<string>> SubmitDocumentRequestAsync(int requesterUserId, DocumentIssuanceRequestDto request, CancellationToken cancellationToken = default)
    {
        await Task.CompletedTask;
        return ApiResponse<string>.Ok("REQ-DOC-001");
    }

    public async Task<ApiResponse<bool>> ProcessRequestAsync(int requestId, int reviewerUserId, ProcessRequestDto request, CancellationToken cancellationToken = default)
    {
        await Task.CompletedTask;
        return ApiResponse<bool>.Ok(true);
    }
}
