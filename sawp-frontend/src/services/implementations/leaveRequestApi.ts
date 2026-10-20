import { apiClient } from '../apiClient';

export interface CreateLeaveRequestPayload {
  studentId: number;
  startDate: string;
  endDate: string;
  session: number; // 1: Morning, 2: Afternoon, 3: FullDay
  keepLunchService: boolean;
  reason: string;
  doctorNoteAttachmentUrl?: string;
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
  session: number;
  sessionName: string;
  keepLunchService: boolean;
  isLunchCancelled: boolean;
  reason: string;
  doctorNoteAttachmentUrl?: string;
  totalDays: number;
  isEscalated: boolean;
  status: number;
  statusName: string;
  submissionDate: string;
  reviewerUserId?: number;
  reviewerName?: string;
  processedDate?: string;
  reviewerNotes?: string;
}

export interface ReviewLeaveRequestPayload {
  isApproved: boolean;
  isForwardToSchoolBoard?: boolean;
  reviewerNotes?: string;
}

export interface SyncLeaveResult {
  requestId: number;
  status: number;
  isAttendanceSyncSuggested: boolean;
  cancelledMealDates: string[];
  mealSyncMessage: string;
}

export const leaveRequestApi = {
  submit: async (payload: CreateLeaveRequestPayload) => {
    const res = await apiClient.post<{ success: boolean; data: LeaveRequestItem; message: string }>('/leaverequests', payload);
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
};
