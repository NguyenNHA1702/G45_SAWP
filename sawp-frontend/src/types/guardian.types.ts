export interface SubmitChildLinkDto {
  studentCode: string;
  studentFullName: string;
  studentDateOfBirth: string;
  relationship: string;
  notes?: string;
}

export interface ChildLinkRequestResultDto {
  requestId: number;
  studentCode: string;
  studentFullName: string;
  isAutoVerified: boolean;
  status: string;
  message: string;
}

export interface ChildLinkRequestItemDto {
  requestId: number;
  requestDate: string;
  status: string;
  parentUserId: number;
  parentFullName: string;
  parentPhone?: string;
  studentId?: number;
  studentCode: string;
  studentFullName: string;
  studentDateOfBirth: string;
  relationship: string;
  isAutoMatched: boolean;
  systemCheckNote?: string;
  reviewedByName?: string;
  reviewedAt?: string;
  rejectionReason?: string;
}

export interface VerifyChildLinkDto {
  isApproved: boolean;
  rejectionReason?: string;
}

export interface LinkedChildSummaryDto {
  studentId: number;
  studentCode: string;
  fullName: string;
  dateOfBirth: string;
  gender: string;
  avatarUrl?: string | null;
  status: string;
  currentClassId?: number | null;
  currentClassName?: string | null;
  grade?: number | null;
  isPlacementPublished: boolean;
  relationshipName: string;
  isPrimaryContact: boolean;
}

export interface ChildDashboardDto {
  studentId: number;
  studentCode: string;
  fullName: string;
  dateOfBirth: string;
  gender: string;
  status: string;
  classId?: number;
  className?: string;
  grade?: number;
  isPlacementPublished: boolean;
  homeroomTeacherName?: string;
  homeroomTeacherEmail?: string;
  homeroomTeacherPhone?: string;
  schoolYearName?: string;
  relationship: string;
  isPrimaryContact: boolean;
}
