import type { AttendanceStatus, LunchCancellationSource, RequestStatus } from '../types/attendance.types';

// Enum values exchanged with the API. The API serializes enums by name (JsonStringEnumConverter in
// SAWP.Backend/API/Program.cs), so every value here must match the member name of the backend enum it maps to.

/** Maps to `Core.Enums.AttendanceStatus` (SAWP.Backend/Core/Enums/AttendanceStatus.cs). */
export const ATTENDANCE_STATUS = {
  Present: 'Present',
  ExcusedAbsence: 'ExcusedAbsence',
  UnexcusedAbsence: 'UnexcusedAbsence',
  Late: 'Late',
} as const satisfies Record<string, AttendanceStatus>;

/**
 * Maps to `Core.Enums.RequestStatus` (SAWP.Backend/Core/Enums/RequestStatus.cs); only the values the attendance
 * screens use for the leave indicator.
 */
export const LEAVE_REQUEST_STATUS = {
  Submitted: 'Submitted',
  UnderReview: 'UnderReview',
  Approved: 'Approved',
} as const satisfies Record<string, RequestStatus>;

/** Maps to `Core.Enums.LunchCancellationSource` (SAWP.Backend/Core/Enums/LunchCancellationSource.cs). */
export const LUNCH_CANCELLATION_SOURCE = {
  Parent: 'Parent',
  Leave: 'Leave',
  Attendance: 'Attendance',
  Staff: 'Staff',
} as const satisfies Record<string, LunchCancellationSource>;
