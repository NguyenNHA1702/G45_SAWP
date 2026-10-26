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

export interface IAdministrativeRequestApi {
  // Parent
  submitClassTransfer(req: ClassTransferRequest): Promise<ApiResponse<string>>;
  submitDocument(req: DocumentIssuanceRequest): Promise<ApiResponse<string>>;
  getMyRequests(studentId?: number): Promise<ApiResponse<AdministrativeRequestSummary[]>>;
  getRequestDetail(id: number): Promise<ApiResponse<AdministrativeRequestSummary>>;
  resubmitRequest(id: number, req: ResubmitRequest): Promise<ApiResponse<boolean>>;
  downloadIssuedDocument(id: number, fileName: string): Promise<void>;
  uploadAttachment(file: File): Promise<ApiResponse<{ url: string; fileName: string; fileSize: number }>>;

  // Office Staff
  getOfficeRequests(requestType?: string, status?: string): Promise<ApiResponse<AdministrativeRequestSummary[]>>;
  requestAmendment(id: number, req: StaffAmendmentRequest): Promise<ApiResponse<boolean>>;
  endorseTransfer(id: number, req: StaffEndorseTransferRequest): Promise<ApiResponse<boolean>>;
  endorseDocument(id: number, req: StaffEndorseDocumentRequest): Promise<ApiResponse<boolean>>;
  executeClassTransfer(id: number): Promise<ApiResponse<boolean>>;

  // School Board
  getBoardRequests(requestType?: string): Promise<ApiResponse<AdministrativeRequestSummary[]>>;
  processBoardDecision(id: number, req: BoardDecisionRequest): Promise<ApiResponse<boolean>>;
}
