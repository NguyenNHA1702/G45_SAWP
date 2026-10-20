import { apiClient } from '../apiClient';

export interface AttendanceRow {
  studentId: number;
  studentCode: string;
  fullName: string;
  gender: string;
  currentStatus: number; // 1: Present, 2: ExcusedAbsence, 3: UnexcusedAbsence, 4: Late
  hasApprovedLeave: boolean;
  leaveReason?: string;
  isPreFilled: boolean;
  remarks?: string;
}

export interface ClassDailySheet {
  classId: number;
  className: string;
  date: string;
  session: number; // 1: Morning, 2: Afternoon
  sessionName: string;
  isSaved: boolean;
  savedAt?: string;
  savedByName?: string;
  totalStudents: number;
  presentCount: number;
  excusedAbsenceCount: number;
  unexcusedAbsenceCount: number;
  lateCount: number;
  students: AttendanceRow[];
}

export interface SaveClassAttendancePayload {
  classId: number;
  date: string;
  session: number;
  entries: {
    studentId: number;
    status: number;
    remarks?: string;
  }[];
}

export interface AttendanceSaveResult {
  success: boolean;
  message: string;
  savedCount: number;
  mealAlerts: string[];
}

export const attendanceApi = {
  getClassDailySheet: async (classId: number, date?: string, session: number = 1) => {
    const res = await apiClient.get<{ success: boolean; data: ClassDailySheet; message: string }>(`/attendance/class/${classId}/sheet`, {
      params: { date, session },
    });
    return res.data;
  },

  saveAttendance: async (payload: SaveClassAttendancePayload) => {
    const res = await apiClient.post<{ success: boolean; data: AttendanceSaveResult; message: string }>('/attendance/class/save', payload);
    return res.data;
  },
};
