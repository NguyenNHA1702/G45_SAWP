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

export interface IAdministrativeRequestApi {
  // Parent
  submitClassTransfer(req: ClassTransferRequest): Promise<ApiResponse<string>>;
  submitDocument(req: DocumentIssuanceRequest): Promise<ApiResponse<string>>;
  submitOther(req: OtherRequestPayload): Promise<ApiResponse<string>>;
  getMyRequests(studentId?: number): Promise<ApiResponse<AdministrativeRequestSummary[]>>;
  getMyRequestsPaged(params?: { studentId?: number; requestType?: string; status?: string; pageIndex?: number; pageSize?: number }): Promise<ApiResponse<PagedAdministrativeRequests>>;
  getRequestDetail(id: number): Promise<ApiResponse<AdministrativeRequestSummary>>;
  resubmitRequest(id: number, req: ResubmitRequest): Promise<ApiResponse<boolean>>;
  cancelRequest(id: number): Promise<ApiResponse<boolean>>;
  downloadIssuedDocument(id: number, fileName: string): Promise<void>;
  uploadAttachment(file: File): Promise<ApiResponse<{ url: string; fileName: string; fileSize: number }>>;

  // Office Staff
  getOfficeRequests(requestType?: string, status?: string): Promise<ApiResponse<AdministrativeRequestSummary[]>>;
  getOfficeRequestsPaged(params?: { requestType?: string; status?: string; search?: string; pageIndex?: number; pageSize?: number }): Promise<ApiResponse<PagedAdministrativeRequests>>;
  requestAmendment(id: number, req: StaffAmendmentRequest): Promise<ApiResponse<boolean>>;
  endorseTransfer(id: number, req: StaffEndorseTransferRequest): Promise<ApiResponse<boolean>>;
  endorseDocument(id: number, req: StaffEndorseDocumentRequest): Promise<ApiResponse<boolean>>;
  directIssue(id: number, req: StaffDirectIssueRequest): Promise<ApiResponse<boolean>>;
  staffReject(id: number, reason: string): Promise<ApiResponse<boolean>>;
  executeClassTransfer(id: number, confirmEarly?: boolean): Promise<ApiResponse<boolean>>;

  // School Board
  getBoardRequests(requestType?: string, status?: string): Promise<ApiResponse<AdministrativeRequestSummary[]>>;
  getBoardRequestsPaged(params?: { requestType?: string; status?: string; search?: string; pageIndex?: number; pageSize?: number }): Promise<ApiResponse<PagedAdministrativeRequests>>;
  processBoardDecision(id: number, req: BoardDecisionRequest): Promise<ApiResponse<boolean>>;

  // History & Statistics
  getRequestHistory(id: number): Promise<ApiResponse<RequestHistoryItem[]>>;
  getStatistics(): Promise<ApiResponse<RequestStatistics>>;
}
