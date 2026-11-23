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

  getClassRequests: async (params?: { classId?: number; status?: string; date?: string }) => {
    const res = await apiClient.get<{ success: boolean; data: LeaveRequestItem[] }>('/leaverequests/class', { params });
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
