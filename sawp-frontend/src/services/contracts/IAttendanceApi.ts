import type { ApiResponse } from '../../types/common.types';
import type {
  AbsenceContactNote,
  AbsenceWithoutContactNote,
  AttendanceSession,
  AttendanceSheet,
  AttendanceSheetEntry,
  AttendanceSubmitResult,
  ClassAttendanceMatrix,
  ClassAttendanceSummary,
  CorrectAttendancePayload,
  CreateContactNotePayload,
  ReattendanceRequest,
  StudentAttendanceHistory,
  SubmitAttendanceSheetPayload,
  TeacherClassOption,
} from '../../types/attendance.types';

export interface IAttendanceApi {
  getMyClasses(date: string, session: AttendanceSession): Promise<TeacherClassOption[]>;
  getSheet(classId: number, date: string, session: AttendanceSession): Promise<AttendanceSheet>;
  submitSheet(classId: number, payload: SubmitAttendanceSheetPayload): Promise<ApiResponse<AttendanceSubmitResult>>;
  correctRecord(recordId: number, payload: CorrectAttendancePayload): Promise<ApiResponse<AttendanceSheetEntry>>;
  getContactNotes(recordId: number): Promise<AbsenceContactNote[]>;
  addContactNote(recordId: number, payload: CreateContactNotePayload): Promise<ApiResponse<AbsenceContactNote>>;
  supersedeContactNote(noteId: number, reason: string): Promise<ApiResponse<AbsenceContactNote>>;
  getAbsencesWithoutContactNote(from: string, to: string, classId?: number | null): Promise<AbsenceWithoutContactNote[]>;
  getClassSummary(classId: number, from: string, to: string): Promise<ClassAttendanceSummary>;
  getClassMatrix(classId: number, from: string, to: string): Promise<ClassAttendanceMatrix>;
  getStudentHistory(studentId: number, from: string, to: string): Promise<StudentAttendanceHistory>;
  requestReattendance(
    classId: number,
    payload: { date: string; session: AttendanceSession; reason?: string }
  ): Promise<ApiResponse<boolean>>;
  getReattendanceRequests(status?: string): Promise<ReattendanceRequest[]>;
  unlockSheet(
    classId: number,
    payload: { date: string; session: AttendanceSession; note?: string; requestId?: number }
  ): Promise<ApiResponse<ReattendanceRequest>>;
  lockSheet(
    classId: number,
    payload: { date: string; session: AttendanceSession; note?: string }
  ): Promise<ApiResponse<boolean>>;
  rejectReattendanceRequest(
    requestId: number,
    payload: { note?: string }
  ): Promise<ApiResponse<ReattendanceRequest>>;
}
