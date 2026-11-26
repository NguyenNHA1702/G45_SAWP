import type { ApiResponse } from '../../types/common.types';
import type {
  AbsenceNoticeResult,
  AttendanceSession,
  AttendanceSheet,
  AttendanceSheetEntry,
  AttendanceSubmitResult,
  ClassAttendanceMatrix,
  ClassAttendanceSummary,
  CorrectAttendancePayload,
  StudentAttendanceHistory,
  SubmitAttendanceSheetPayload,
  TeacherClassOption,
} from '../../types/attendance.types';

export interface IAttendanceApi {
  getMyClasses(date: string, session: AttendanceSession): Promise<TeacherClassOption[]>;
  getSheet(classId: number, date: string, session: AttendanceSession): Promise<AttendanceSheet>;
  submitSheet(classId: number, payload: SubmitAttendanceSheetPayload): Promise<ApiResponse<AttendanceSubmitResult>>;
  correctRecord(recordId: number, payload: CorrectAttendancePayload): Promise<ApiResponse<AttendanceSheetEntry>>;
  sendAbsenceNotice(recordId: number): Promise<ApiResponse<AbsenceNoticeResult>>;
  getClassSummary(classId: number, from: string, to: string): Promise<ClassAttendanceSummary>;
  getClassMatrix(classId: number, from: string, to: string): Promise<ClassAttendanceMatrix>;
  getStudentHistory(studentId: number, from: string, to: string): Promise<StudentAttendanceHistory>;
  requestReattendance(
    classId: number,
    payload: { date: string; session: AttendanceSession; reason?: string }
  ): Promise<ApiResponse<boolean>>;
}
