export type RequestStatus = 'Submitted' | 'UnderReview' | 'Approved' | 'Rejected' | 'Cancelled';

export interface ClassTransferRequest {
  studentId: number;
  currentClassId?: number;
  targetClassId: number;
  transferReason: string;
  attachmentUrl?: string;
}

export interface DocumentIssuanceRequest {
  studentId: number;
  documentType: string;
  numberOfCopies: number;
  purpose: string;
  attachmentUrl?: string;
}

export interface ResubmitRequest {
  transferReason?: string;
  purpose?: string;
  attachmentUrl?: string;
}

export interface StaffAmendmentRequest {
  amendmentNotes: string;
}

export interface StaffEndorseTransferRequest {
  staffRecommendation: string;
}

export interface StaffEndorseDocumentRequest {
  staffEndorsement: string;
  documentContentDraft?: string;
}

export interface BoardDecisionRequest {
  isApproved: boolean;
  decisionNotes?: string;
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
}

export interface AdministrativeRequestSummary {
  id: number;
  requestNumber: string;
  requestType: 'ClassTransfer' | 'Document' | string;
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
