import { apiClient } from '../apiClient';
import { unwrap, unwrapData } from '../../utils/apiError';
import type { IAttendanceApi } from '../contracts/IAttendanceApi';

export const attendanceApi: IAttendanceApi = {
  getMyClasses: (date, session) =>
    unwrapData(apiClient.get('/attendance/my-classes', { params: { date, session } })),

  getSheet: (classId, date, session) =>
    unwrapData(apiClient.get(`/attendance/classes/${classId}/sheet`, { params: { date, session } })),

  submitSheet: (classId, payload) =>
    unwrap(apiClient.put(`/attendance/classes/${classId}/sheet`, payload)),

  correctRecord: (recordId, payload) =>
    unwrap(apiClient.patch(`/attendance/records/${recordId}/correction`, payload)),

  sendAbsenceNotice: (recordId) =>
    unwrap(apiClient.post(`/attendance/records/${recordId}/absence-notice`)),

  getClassSummary: (classId, from, to) =>
    unwrapData(apiClient.get(`/attendance/classes/${classId}/summary`, { params: { from, to } })),

  getClassMatrix: (classId, from, to) =>
    unwrapData(apiClient.get(`/attendance/classes/${classId}/matrix`, { params: { from, to } })),

  getStudentHistory: (studentId, from, to) =>
    unwrapData(apiClient.get(`/attendance/students/${studentId}/history`, { params: { from, to } })),

  requestReattendance: (classId, payload) =>
    unwrap(apiClient.post(`/attendance/classes/${classId}/request-reattendance`, payload)),
};
