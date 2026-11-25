import { apiClient } from '../apiClient';
import type { LeaveSession, RequestStatus } from '../../types/attendance.types';

export interface CreateLeaveRequestPayload {
  studentId: number;
  startDate: string;
  endDate: string;
  session: number; // 1: Morning, 2: Afternoon, 3: FullDay
  keepLunchService: boolean;
  reason: string;
  doctorNoteAttachmentUrl?: string;
}

export interface UpdateLeaveRequestPayload {
  startDate: string;
  endDate: string;
  session: number;
  keepLunchService: boolean;
  reason: string;
  doctorNoteAttachmentUrl?: string;
}

export interface LeaveStatusHistoryItem {
  action: string;
  actionLabel: string;
  actorName?: string | null;
  notes?: string | null;
  timestamp: string;
}

export interface LeaveRequestItem {
  id: number;
  requestNumber: string;
  studentId: number;
  studentName: string;
  studentCode: string;
  classId?: number;
  className: string;
  hasHomeroomTeacher?: boolean;
  requesterUserId: number;
  requesterName: string;
  requesterPhone?: string;
  startDate: string;
  endDate: string;
  session: LeaveSession;
  sessionName: string;
  keepLunchService: boolean;
  isLunchCancelled: boolean;
  reason: string;
  doctorNoteAttachmentUrl?: string;
  totalDays: number;
  isEscalated: boolean;
  canEdit?: boolean;
  canWithdraw?: boolean;
  isWithdrawn?: boolean;
  status: RequestStatus;
  statusName: string;
  submissionDate: string;
  reviewerUserId?: number;
  reviewerName?: string;
  processedDate?: string;
  reviewerNotes?: string;
  statusHistory?: LeaveStatusHistoryItem[];
}

export interface ReviewLeaveRequestPayload {
  isApproved: boolean;
  isForwardToSchoolBoard?: boolean;
  isRequestMoreInfo?: boolean;
  reviewerNotes?: string;
}

export interface SyncLeaveResult {
  requestId: number;
  status: RequestStatus;
  isAttendanceSyncSuggested: boolean;
  cancelledMealDates: string[];
  mealSyncMessage: string;
  attendanceWarnings?: string[];
}

export interface LeaveRequestStats {
  totalCount: number;
  submittedCount: number;
  needMoreInfoCount: number;
  underReviewCount: number;
  approvedCount: number;
  rejectedCount: number;
  cancelledCount: number;
  escalatedCount: number;
}

export interface PagedLeaveRequests {
  items: LeaveRequestItem[];
  totalCount: number;
  pageIndex: number;
  pageSize: number;
  totalPages: number;
  stats: LeaveRequestStats;
}

export const leaveRequestApi = {
  submit: async (payload: CreateLeaveRequestPayload) => {
    const res = await apiClient.post<{ success: boolean; data: LeaveRequestItem; message: string }>('/leaverequests', payload);
    return res.data;
  },

  update: async (id: number, payload: UpdateLeaveRequestPayload) => {
    const res = await apiClient.put<{ success: boolean; data: LeaveRequestItem; message: string }>(`/leaverequests/${id}`, payload);
    return res.data;
  },

  getMyRequests: async () => {
    const res = await apiClient.get<{ success: boolean; data: LeaveRequestItem[] }>('/leaverequests/my');
    return res.data;
  },

  getMyRequestsPaged: async (params?: { studentId?: number; status?: string; pageIndex?: number; pageSize?: number }) => {
    const res = await apiClient.get<{ success: boolean; data: PagedLeaveRequests }>('/leaverequests/my/paged', {
      params: {
        ...(params?.studentId ? { studentId: params.studentId } : {}),
        ...(params?.status && params.status !== 'ALL' ? { status: params.status } : {}),
        pageIndex: params?.pageIndex ?? 1,
        pageSize: params?.pageSize ?? 15,
      },
    });
    return res.data;
  },

  getClassRequests: async (params?: { classId?: number; status?: string; date?: string }) => {
    const res = await apiClient.get<{ success: boolean; data: LeaveRequestItem[] }>('/leaverequests/class', { params });
    return res.data;
  },

  getClassRequestsPaged: async (params?: { classId?: number; status?: string; date?: string; search?: string; pageIndex?: number; pageSize?: number }) => {
    const res = await apiClient.get<{ success: boolean; data: PagedLeaveRequests }>('/leaverequests/class/paged', {
      params: {
        ...(params?.classId ? { classId: params.classId } : {}),
        ...(params?.status && params.status !== 'ALL' ? { status: params.status } : {}),
        ...(params?.date ? { date: params.date } : {}),
        ...(params?.search ? { search: params.search } : {}),
        pageIndex: params?.pageIndex ?? 1,
        pageSize: params?.pageSize ?? 15,
      },
    });
    return res.data;
  },

  getDetails: async (id: number) => {
    const res = await apiClient.get<{ success: boolean; data: LeaveRequestItem }>(`/leaverequests/${id}`);
    return res.data;
  },

  review: async (id: number, payload: ReviewLeaveRequestPayload) => {
    const res = await apiClient.put<{ success: boolean; data: SyncLeaveResult; message: string }>(`/leaverequests/${id}/review`, payload);
    return res.data;
  },

  cancel: async (id: number) => {
    const res = await apiClient.post<{ success: boolean; message: string }>(`/leaverequests/${id}/cancel`);
    return res.data;
  },

  withdraw: async (id: number, reason?: string) => {
    const res = await apiClient.post<{ success: boolean; message: string }>(`/leaverequests/${id}/withdraw`, { reason });
    return res.data;
  },

  uploadAttachment: async (file: File) => {
    const formData = new FormData();
    formData.append('file', file);
    const res = await apiClient.post<{
      success: boolean;
      data: { url: string; fileName: string; fileSize: number; savedPath: string };
      message: string;
    }>('/leaverequests/upload-attachment', formData, {
      headers: { 'Content-Type': 'multipart/form-data' },
    });
    return res.data;
  },

  openAttachment: async (url: string) => {
    const endpoint = url.startsWith('/api/') ? url.slice(4) : url;
    const res = await apiClient.get<Blob>(endpoint, { responseType: 'blob' });
    const objectUrl = URL.createObjectURL(res.data);
    window.open(objectUrl, '_blank', 'noopener,noreferrer');
    window.setTimeout(() => URL.revokeObjectURL(objectUrl), 60_000);
  },
};
