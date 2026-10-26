import { apiClient } from '../apiClient';
import type { ApiResponse } from '../../types/common.types';
import type {
  AdministrativeRequestSummary,
  BoardDecisionRequest,
  ClassTransferRequest,
  DocumentIssuanceRequest,
  ResubmitRequest,
  StaffAmendmentRequest,
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

  async getMyRequests(studentId?: number): Promise<ApiResponse<AdministrativeRequestSummary[]>> {
    const res = await apiClient.get<ApiResponse<AdministrativeRequestSummary[]>>('/requests/my-requests', {
      params: studentId ? { studentId } : {},
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

  async downloadIssuedDocument(id: number, fileName: string): Promise<void> {
    const res = await apiClient.get(`/requests/${id}/download-document`, {
      responseType: 'blob',
    });
    const url = window.URL.createObjectURL(new Blob([res.data], { type: 'text/plain;charset=utf-8' }));
    const link = document.createElement('a');
    link.href = url;
    link.setAttribute('download', fileName);
    document.body.appendChild(link);
    link.click();
    link.parentNode?.removeChild(link);
    window.URL.revokeObjectURL(url);
  }

  // Office Staff operations
  async getOfficeRequests(requestType?: string, status?: string): Promise<ApiResponse<AdministrativeRequestSummary[]>> {
    const params: Record<string, string> = {};
    if (requestType) params.requestType = requestType;
    if (status) params.status = status;
    const res = await apiClient.get<ApiResponse<AdministrativeRequestSummary[]>>('/requests/office', { params });
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

  async executeClassTransfer(id: number): Promise<ApiResponse<boolean>> {
    const res = await apiClient.post<ApiResponse<boolean>>(`/requests/${id}/execute-transfer`, {});
    return res.data;
  }

  // School Board operations
  async getBoardRequests(requestType?: string): Promise<ApiResponse<AdministrativeRequestSummary[]>> {
    const params: Record<string, string> = {};
    if (requestType) params.requestType = requestType;
    const res = await apiClient.get<ApiResponse<AdministrativeRequestSummary[]>>('/requests/board', { params });
    return res.data;
  }

  async processBoardDecision(id: number, req: BoardDecisionRequest): Promise<ApiResponse<boolean>> {
    const res = await apiClient.post<ApiResponse<boolean>>(`/requests/${id}/board-decision`, req);
    return res.data;
  }
}

export const administrativeRequestApi = new AdministrativeRequestApi();
