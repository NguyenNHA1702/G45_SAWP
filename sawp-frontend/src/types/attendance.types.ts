// Không dùng enum (erasableSyntaxOnly) -> const object + union type cùng tên
export const AttendanceStatus = {
  Present: 'Present',
  Late: 'Late',
  ExcusedAbsence: 'ExcusedAbsence',
  UnexcusedAbsence: 'UnexcusedAbsence',
} as const;
export type AttendanceStatus = (typeof AttendanceStatus)[keyof typeof AttendanceStatus];

export type RequestStatus = 'Submitted' | 'UnderReview' | 'Approved' | 'Rejected' | 'Cancelled';
export type AttendanceSession = 'Morning' | 'Afternoon';
export type LeaveSession = 'Morning' | 'Afternoon' | 'FullDay';
export type LunchCancellationSource = 'Parent' | 'Leave' | 'Attendance' | 'Staff';
export type SheetLockReason = 'PastDay' | 'NotAssigned' | 'NonSchoolDay' | 'Future';

export interface TeacherClassOption {
  classId: number;
  className: string;
  grade: number;
  accessReason: 'Homeroom' | 'FirstPeriod' | 'SchoolAdmin';
  isSubmitted: boolean;
}

export interface LeaveIndicator {
  leaveRequestId: number;
  requestNumber: string;
  status: RequestStatus;
  startDate: string;
  endDate: string;
  session: LeaveSession;
  reason: string;
}

export interface AttendanceSheetEntry {
  studentId: number;
  studentCode: string;
  fullName: string;
  avatarUrl: string | null;
  recordId: number | null;
  status: AttendanceStatus | null;
  suggestedStatus: AttendanceStatus;
  remarks: string | null;
  leave: LeaveIndicator | null;
  hasLunch: boolean;
  lunchCancelledBy: LunchCancellationSource | null;
}

export interface AttendanceSummary {
  total: number;
  present: number;
  late: number;
  excusedAbsence: number;
  unexcusedAbsence: number;
  notMarked: number;
}

export interface AttendanceSheet {
  classId: number;
  className: string;
  date: string;
  session: AttendanceSession;
  isSchoolDay: boolean;
  canEdit: boolean;
  lockReason: SheetLockReason | null;
  submittedAt: string | null;
  submittedByName: string | null;
  lastUpdatedAt: string | null;
  entries: AttendanceSheetEntry[];
  summary: AttendanceSummary;
}

export interface DailyAttendanceEntry {
  studentId: number;
  status: AttendanceStatus;
  remarks: string | null;
}

export interface SubmitAttendanceSheetPayload {
  date: string; // yyyy-MM-dd
  session: AttendanceSession;
  entries: DailyAttendanceEntry[];
}

export interface CorrectAttendancePayload {
  status: AttendanceStatus;
  remarks: string | null;
  correctionReason: string;
}

export interface AttendanceSubmitResult {
  summary: AttendanceSummary;
  lunchDeductedCount: number;
  lunchRestoredCount: number;
  lunchRestoreSkippedCount: number;
  parentsNotifiedCount: number;
  warnings: string[];
}

export interface AttendanceDay {
  date: string;
  session: AttendanceSession;
  status: AttendanceStatus;
  remarks: string | null;
  leaveRequestNumber: string | null;
}

export interface StudentAttendanceHistory {
  studentId: number;
  fullName: string;
  className: string | null;
  from: string;
  to: string;
  days: AttendanceDay[];
  summary: AttendanceSummary;
}

export interface StudentAttendanceRow {
  studentId: number;
  studentCode: string;
  fullName: string;
  present: number;
  late: number;
  excusedAbsence: number;
  unexcusedAbsence: number;
}

export interface ClassAttendanceSummary {
  classId: number;
  className: string;
  from: string;
  to: string;
  schoolDays: number;
  totals: AttendanceSummary;
  attendanceRate: number;
  students: StudentAttendanceRow[];
}
