export type RequestStatus = 'Submitted' | 'UnderReview' | 'Approved' | 'Rejected' | 'Cancelled' | 'Completed' | 'NeedMoreInfo' | 'ReturnedToStaff';

export interface ClassTransferRequest {
  studentId: number;
  currentClassId?: number;
  targetClassId: number;
  transferReason: string;
  attachmentUrl?: string;
  effectiveDate?: string;
}

export interface DocumentIssuanceRequest {
  studentId: number;
  documentType: string;
  numberOfCopies: number;
  purpose: string;
  attachmentUrl?: string;
  isPaperCopyRequested?: boolean;
}

export interface OtherRequestPayload {
  studentId: number;
  title: string;
  content: string;
  attachmentUrl?: string;
}

export interface ResubmitRequest {
  transferReason?: string;
  purpose?: string;
  numberOfCopies?: number;
  additionalNotes?: string;
  attachmentUrl?: string;
}

export interface StaffAmendmentRequest {
  amendmentNotes: string;
}

export interface StaffRejectRequest {
  reason: string;
}

export interface StaffDirectIssueRequest {
  certificateNumber?: string;
  issuedDate?: string;
  documentContent?: string;
  documentFileUrl?: string;
  digitalSignatureInfo?: string;
  isPaperCopy?: boolean;
  staffNotes?: string;
}

export interface StaffEndorseTransferRequest {
  staffRecommendation: string;
  effectiveDate?: string;
}

export interface StaffEndorseDocumentRequest {
  staffEndorsement: string;
  documentContentDraft?: string;
  documentFileUrl?: string;
  certificateNumber?: string;
  issuedDate?: string;
  isPaperCopyRequested?: boolean;
}

export interface BoardDecisionRequest {
  isApproved: boolean;
  returnToStaff?: boolean;
  decisionNotes?: string;
  signedDocumentUrl?: string;
  effectiveDate?: string;
}

export interface RequestHistoryItem {
  timestamp: string;
  action: string;
  description: string;
  performedBy?: string;
  performedByRole?: string;
  status: RequestStatus;
}

export interface RequestStatistics {
  totalRequests: number;
  submittedCount: number;
  underReviewCount: number;
  approvedCount: number;
  completedCount: number;
  rejectedCount: number;
  cancelledCount: number;
  classTransferCount: number;
  documentCount: number;
  requiringAmendmentCount: number;
}

export interface ClassTransferSummary {
  fromClassId: number;
  fromClassName: string;
  toClassId: number;
  toClassName: string;
  transferReason: string;
  staffRecommendation?: string;
  targetClassCapacity?: number;
  targetClassMaxCapacity?: number;
  isExecuted: boolean;
  executedAt?: string;
  effectiveDate?: string;
}

export interface DocumentRequestSummary {
  documentType: string;
  numberOfCopies: number;
  purpose: string;
  staffEndorsement?: string;
  documentContentDraft?: string;
  issuedDate?: string;
  issuedDocumentUrl?: string;
  certificateNumber?: string;
  digitalSignatureInfo?: string;
  isPaperCopyRequested?: boolean;
}

export interface AdministrativeRequestSummary {
  id: number;
  requestNumber: string;
  requestType: 'ClassTransfer' | 'Document' | 'Other' | string;
  status: RequestStatus;
  submissionDate: string;
  processedDate?: string;
  requesterUserId: number;
  requesterName: string;
  studentId?: number;
  studentCode?: string;
  studentName?: string;
  currentClassName?: string;
  requiresAmendment: boolean;
  attachmentUrl?: string;
  amendmentNotes?: string;
  reviewerNotes?: string;
  schoolBoardNotes?: string;
  schoolBoardApprovedAt?: string;
  classTransfer?: ClassTransferSummary;
  documentRequest?: DocumentRequestSummary;
}

export interface AdministrativeRequestStats {
  totalCount: number;
  submittedCount: number;
  needMoreInfoCount: number;
  returnedToStaffCount: number;
  underReviewCount: number;
  approvedCount: number;
  completedCount: number;
  rejectedCount: number;
  cancelledCount: number;
  pendingExecutionCount: number;
}

export interface PagedAdministrativeRequests {
  items: AdministrativeRequestSummary[];
  totalCount: number;
  pageIndex: number;
  pageSize: number;
  totalPages: number;
  stats: AdministrativeRequestStats;
  hasPendingTransfer?: boolean;
  pendingTransferRequestNumber?: string;
  pendingDocumentTypes?: string[];
}
