import { apiClient } from '../apiClient';
import { unwrap, unwrapData } from '../../utils/apiError';
import type { IAttendanceApi } from '../contracts/IAttendanceApi';

export const attendanceApi: IAttendanceApi = {
  getMyClasses: (date) =>
    unwrapData(apiClient.get('/attendance/my-classes', { params: { date } })),

  getSheet: (classId, date) =>
    unwrapData(apiClient.get(`/attendance/classes/${classId}/sheet`, { params: { date } })),

  submitSheet: (classId, payload) =>
    unwrap(apiClient.put(`/attendance/classes/${classId}/sheet`, payload)),

  correctRecord: (recordId, payload) =>
    unwrap(apiClient.patch(`/attendance/records/${recordId}/correction`, payload)),

  getClassSummary: (classId, from, to) =>
    unwrapData(apiClient.get(`/attendance/classes/${classId}/summary`, { params: { from, to } })),

  getStudentHistory: (studentId, from, to) =>
    unwrapData(apiClient.get(`/attendance/students/${studentId}/history`, { params: { from, to } })),
};
