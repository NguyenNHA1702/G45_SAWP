import { apiClient } from '../apiClient';
import type { ApiResponse } from '../../types/common.types';
import type {
  AdministrativeRequestSummary,
  BoardDecisionRequest,
  ClassTransferRequest,
  DocumentIssuanceRequest,
  OtherRequestPayload,
  PagedAdministrativeRequests,
  RequestHistoryItem,
  RequestStatistics,
  ResubmitRequest,
  StaffAmendmentRequest,
  StaffDirectIssueRequest,
  StaffEndorseDocumentRequest,
  StaffEndorseTransferRequest,
} from '../../types/request.types';
import type { IAdministrativeRequestApi } from '../contracts/IAdministrativeRequestApi';

class AdministrativeRequestApi implements IAdministrativeRequestApi {
  // Parent operations
  async submitClassTransfer(req: ClassTransferRequest): Promise<ApiResponse<string>> {
    const res = await apiClient.post<ApiResponse<string>>('/requests/class-transfer', req);
    return res.data;
  }

  async submitDocument(req: DocumentIssuanceRequest): Promise<ApiResponse<string>> {
    const res = await apiClient.post<ApiResponse<string>>('/requests/document', req);
    return res.data;
  }

  async submitOther(req: OtherRequestPayload): Promise<ApiResponse<string>> {
    const res = await apiClient.post<ApiResponse<string>>('/requests/other', req);
    return res.data;
  }

  async getMyRequests(studentId?: number): Promise<ApiResponse<AdministrativeRequestSummary[]>> {
    const res = await apiClient.get<ApiResponse<AdministrativeRequestSummary[]>>('/requests/my-requests', {
      params: studentId ? { studentId } : {},
    });
    return res.data;
  }

  async getMyRequestsPaged(params?: {
    studentId?: number;
    requestType?: string;
    status?: string;
    pageIndex?: number;
    pageSize?: number;
  }): Promise<ApiResponse<PagedAdministrativeRequests>> {
    const res = await apiClient.get<ApiResponse<PagedAdministrativeRequests>>('/requests/my-requests/paged', {
      params: {
        ...(params?.studentId ? { studentId: params.studentId } : {}),
        ...(params?.requestType && params.requestType !== 'ALL' ? { requestType: params.requestType } : {}),
        ...(params?.status && params.status !== 'ALL' ? { status: params.status } : {}),
        pageIndex: params?.pageIndex ?? 1,
        pageSize: params?.pageSize ?? 15,
      },
    });
    return res.data;
  }

  async getRequestDetail(id: number): Promise<ApiResponse<AdministrativeRequestSummary>> {
    const res = await apiClient.get<ApiResponse<AdministrativeRequestSummary>>(`/requests/${id}`);
    return res.data;
  }

  async resubmitRequest(id: number, req: ResubmitRequest): Promise<ApiResponse<boolean>> {
    const res = await apiClient.put<ApiResponse<boolean>>(`/requests/${id}/resubmit`, req);
    return res.data;
  }

  async cancelRequest(id: number): Promise<ApiResponse<boolean>> {
    const res = await apiClient.post<ApiResponse<boolean>>(`/requests/${id}/cancel`, {});
    return res.data;
  }

  async downloadIssuedDocument(id: number, fileName: string): Promise<void> {
    const res = await apiClient.get(`/requests/${id}/download-document`, {
      responseType: 'blob',
    });
    const blobType = typeof res.headers?.['content-type'] === 'string' ? res.headers['content-type'] : 'application/msword';
    const url = window.URL.createObjectURL(new Blob([res.data], { type: blobType }));
    const link = document.createElement('a');
    link.href = url;
    // Đảm bảo đuôi file là .doc thay vì .txt
    const safeFileName = fileName.endsWith('.doc') ? fileName : fileName.replace(/\.txt$/, '') + '.doc';
    link.setAttribute('download', safeFileName);
    document.body.appendChild(link);
    link.click();
    link.parentNode?.removeChild(link);
    window.URL.revokeObjectURL(url);
  }

  async uploadAttachment(file: File): Promise<ApiResponse<{ url: string; fileName: string; fileSize: number }>> {
    const formData = new FormData();
    formData.append('file', file);
    const res = await apiClient.post<ApiResponse<{ url: string; fileName: string; fileSize: number }>>(
      '/requests/upload-attachment',
      formData,
      {
        headers: { 'Content-Type': 'multipart/form-data' },
      }
    );
    return res.data;
  }

  // Office Staff operations
  async getOfficeRequests(requestType?: string, status?: string): Promise<ApiResponse<AdministrativeRequestSummary[]>> {
    const params: Record<string, string> = {};
    if (requestType) params.requestType = requestType;
    if (status) params.status = status;
    const res = await apiClient.get<ApiResponse<AdministrativeRequestSummary[]>>('/requests/office', { params });
    return res.data;
  }

  async getOfficeRequestsPaged(params?: {
    requestType?: string;
    status?: string;
    search?: string;
    pageIndex?: number;
    pageSize?: number;
  }): Promise<ApiResponse<PagedAdministrativeRequests>> {
    const res = await apiClient.get<ApiResponse<PagedAdministrativeRequests>>('/requests/office/paged', {
      params: {
        ...(params?.requestType && params.requestType !== 'ALL' ? { requestType: params.requestType } : {}),
        ...(params?.status && params.status !== 'ALL' ? { status: params.status } : {}),
        ...(params?.search ? { search: params.search } : {}),
        pageIndex: params?.pageIndex ?? 1,
        pageSize: params?.pageSize ?? 15,
      },
    });
    return res.data;
  }

  async requestAmendment(id: number, req: StaffAmendmentRequest): Promise<ApiResponse<boolean>> {
    const res = await apiClient.post<ApiResponse<boolean>>(`/requests/${id}/request-amendment`, req);
    return res.data;
  }

  async endorseTransfer(id: number, req: StaffEndorseTransferRequest): Promise<ApiResponse<boolean>> {
    const res = await apiClient.post<ApiResponse<boolean>>(`/requests/${id}/endorse-transfer`, req);
    return res.data;
  }

  async endorseDocument(id: number, req: StaffEndorseDocumentRequest): Promise<ApiResponse<boolean>> {
    const res = await apiClient.post<ApiResponse<boolean>>(`/requests/${id}/endorse-document`, req);
    return res.data;
  }

  async directIssue(id: number, req: StaffDirectIssueRequest): Promise<ApiResponse<boolean>> {
    const res = await apiClient.post<ApiResponse<boolean>>(`/requests/${id}/direct-issue`, req);
    return res.data;
  }

  async staffReject(id: number, reason: string): Promise<ApiResponse<boolean>> {
    const res = await apiClient.post<ApiResponse<boolean>>(`/requests/${id}/staff-reject`, { reason });
    return res.data;
  }

  async executeClassTransfer(id: number, confirmEarly = false): Promise<ApiResponse<boolean>> {
    const res = await apiClient.post<ApiResponse<boolean>>(
      `/requests/${id}/execute-transfer`,
      {},
      { params: confirmEarly ? { confirmEarly: true } : {} }
    );
    return res.data;
  }

  // School Board operations
  async getBoardRequests(requestType?: string, status?: string): Promise<ApiResponse<AdministrativeRequestSummary[]>> {
    const params: Record<string, string> = {};
    if (requestType && requestType !== 'ALL') params.requestType = requestType;
    if (status && status !== 'ALL') params.status = status;
    const res = await apiClient.get<ApiResponse<AdministrativeRequestSummary[]>>('/requests/board', { params });
    return res.data;
  }

  async getBoardRequestsPaged(params?: {
    requestType?: string;
    status?: string;
    search?: string;
    pageIndex?: number;
    pageSize?: number;
  }): Promise<ApiResponse<PagedAdministrativeRequests>> {
    const res = await apiClient.get<ApiResponse<PagedAdministrativeRequests>>('/requests/board/paged', {
      params: {
        ...(params?.requestType && params.requestType !== 'ALL' ? { requestType: params.requestType } : {}),
        ...(params?.status && params.status !== 'ALL' ? { status: params.status } : {}),
        ...(params?.search ? { search: params.search } : {}),
        pageIndex: params?.pageIndex ?? 1,
        pageSize: params?.pageSize ?? 15,
      },
    });
    return res.data;
  }

  async processBoardDecision(id: number, req: BoardDecisionRequest): Promise<ApiResponse<boolean>> {
    const res = await apiClient.post<ApiResponse<boolean>>(`/requests/${id}/board-decision`, req);
    return res.data;
  }

  // History & Statistics
  async getRequestHistory(id: number): Promise<ApiResponse<RequestHistoryItem[]>> {
    const res = await apiClient.get<ApiResponse<RequestHistoryItem[]>>(`/requests/${id}/history`);
    return res.data;
  }

  async getStatistics(): Promise<ApiResponse<RequestStatistics>> {
    const res = await apiClient.get<ApiResponse<RequestStatistics>>('/requests/statistics');
    return res.data;
  }
}

export const administrativeRequestApi = new AdministrativeRequestApi();
