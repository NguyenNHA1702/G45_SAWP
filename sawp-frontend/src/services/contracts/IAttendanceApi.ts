import type { ApiResponse } from '../../types/common.types';
import type {
  AttendanceSession,
  AttendanceSheet,
  AttendanceSheetEntry,
  AttendanceSubmitResult,
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
  getClassSummary(classId: number, from: string, to: string): Promise<ClassAttendanceSummary>;
  getStudentHistory(studentId: number, from: string, to: string): Promise<StudentAttendanceHistory>;
}
