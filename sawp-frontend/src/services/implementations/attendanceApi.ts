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

  // OVR-10 (UC-23 A1, A2): contact notes on unexcused absences
  getContactNotes: (recordId) =>
    unwrapData(apiClient.get(`/attendance/records/${recordId}/contact-notes`)),

  addContactNote: (recordId, payload) =>
    unwrap(apiClient.post(`/attendance/records/${recordId}/contact-notes`, payload)),

  supersedeContactNote: (noteId, reason) =>
    unwrap(apiClient.post(`/attendance/contact-notes/${noteId}/supersede`, { reason })),

  getAbsencesWithoutContactNote: (from, to, classId) =>
    unwrapData(
      apiClient.get('/attendance/absences-without-contact-note', {
        params: { from, to, ...(classId ? { classId } : {}) },
      })
    ),

  getClassSummary: (classId, from, to) =>
    unwrapData(apiClient.get(`/attendance/classes/${classId}/summary`, { params: { from, to } })),

  getClassMatrix: (classId, from, to) =>
    unwrapData(apiClient.get(`/attendance/classes/${classId}/matrix`, { params: { from, to } })),

  getStudentHistory: (studentId, from, to) =>
    unwrapData(apiClient.get(`/attendance/students/${studentId}/history`, { params: { from, to } })),

  requestReattendance: (classId, payload) =>
    unwrap(apiClient.post(`/attendance/classes/${classId}/request-reattendance`, payload)),

  getReattendanceRequests: (status) =>
    unwrapData(apiClient.get('/attendance/reattendance-requests', { params: status ? { status } : undefined })),

  unlockSheet: (classId, payload) =>
    unwrap(apiClient.post(`/attendance/classes/${classId}/unlock`, payload)),

  lockSheet: (classId, payload) =>
    unwrap(apiClient.post(`/attendance/classes/${classId}/lock`, payload)),

  rejectReattendanceRequest: (requestId, payload) =>
    unwrap(apiClient.post(`/attendance/reattendance-requests/${requestId}/reject`, payload)),
};
