// UC-67 Request Parent Accounts / UC-68 Review Account Requests (SSAWP-153)

export type AccountRequestReason = 'NewSchoolYearIntake' | 'AdditionalGuardian' | 'Correction';
export type AccountRequestStatus = 'Draft' | 'Submitted' | 'InReview' | 'Completed' | 'PartlyCompleted' | 'Rejected' | 'Closed';
export type AccountRequestItemStatus = 'Waiting' | 'AccountCreated' | 'Active' | 'EmailFailed' | 'Rejected' | 'Failed' | 'Closed';

export interface GuardianWithoutAccountDto {
  guardianId: number;
  fullName: string;
  email?: string;
  phoneNumber?: string;
  students: string[];
  checkResult: string;
  checkMessage?: string;
  canRequest: boolean;
}

export interface AccountRequestRowCheckDto {
  guardianId: number;
  fullName: string;
  email?: string;
  checkResult: string;
  message?: string;
  passed: boolean;
}

export interface SaveAccountRequestDto {
  title: string;
  reason: AccountRequestReason;
  note?: string;
  guardianIds: number[];
  source: 'Manual' | 'Import';
  sourceReference?: string;
  submit: boolean;
}

export interface AccountRequestItemDto {
  id: number;
  guardianId: number;
  guardianName: string;
  email?: string;
  currentEmail?: string;
  students: string[];
  status: AccountRequestItemStatus;
  checkResult?: string;
  checkMessage?: string;
  reason?: string;
  createdUserId?: number;
  decidedByName?: string;
  decidedAt?: string;
}

export interface AccountRequestDto {
  id: number;
  title: string;
  reason: AccountRequestReason;
  note?: string;
  source: string;
  sourceReference?: string;
  status: AccountRequestStatus;
  requestedByUserId: number;
  requestedByName: string;
  followsUpRequestId?: number;
  createdAt: string;
  submittedAt?: string;
  completedAt?: string;
  totalRows: number;
  waitingRows: number;
  createdRows: number;
  rejectedRows: number;
  failedRows: number;
  closedRows: number;
  items?: AccountRequestItemDto[];
}

export interface SaveAccountRequestResultDto {
  request: AccountRequestDto;
  excludedRows: AccountRequestRowCheckDto[];
}

export interface AccountRequestDecisionDto {
  approveItemIds: number[];
  rejectItemIds: number[];
  rejectReason?: string;
  approveAllValid: boolean;
  rejectAll: boolean;
}

export interface AccountRequestDecisionResultDto {
  request: AccountRequestDto;
  accountsCreated: number;
  emailFailed: number;
  failed: number;
  rejected: number;
  notApproved: AccountRequestRowCheckDto[];
}

export const REASON_LABEL: Record<AccountRequestReason, string> = {
  NewSchoolYearIntake: 'Tiếp nhận đầu năm học',
  AdditionalGuardian: 'Bổ sung người giám hộ',
  Correction: 'Điều chỉnh, sửa sai',
};

export const REQUEST_STATUS: Record<AccountRequestStatus, { label: string; badge: string }> = {
  Draft: { label: 'Nháp', badge: 'badge' },
  Submitted: { label: 'Đã gửi', badge: 'badge badge--violet' },
  InReview: { label: 'Đang xét duyệt', badge: 'badge badge--amber' },
  Completed: { label: 'Hoàn tất', badge: 'badge badge--mint' },
  PartlyCompleted: { label: 'Hoàn tất một phần', badge: 'badge badge--amber' },
  Rejected: { label: 'Bị từ chối', badge: 'badge badge--pink' },
  Closed: { label: 'Đã đóng', badge: 'badge' },
};

export const ITEM_STATUS: Record<AccountRequestItemStatus, { label: string; badge: string }> = {
  Waiting: { label: 'Chờ duyệt', badge: 'badge badge--violet' },
  AccountCreated: { label: 'Đã tạo tài khoản, chờ kích hoạt', badge: 'badge badge--amber' },
  Active: { label: 'Đã kích hoạt', badge: 'badge badge--mint' },
  EmailFailed: { label: 'Lỗi gửi email kích hoạt', badge: 'badge badge--pink' },
  Rejected: { label: 'Bị từ chối', badge: 'badge badge--pink' },
  Failed: { label: 'Lỗi', badge: 'badge badge--pink' },
  Closed: { label: 'Đã đóng', badge: 'badge' },
};
