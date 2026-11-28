export interface StudentProfileDto {
  id: number;
  studentCode: string;
  fullName: string;
  dateOfBirth: string;
  gender: string;
  address?: string;
  residentialArea?: string;
  isSpecialNeeds?: boolean;
  specialNeedsNotes?: string;
  avatarUrl?: string;
  status: string;
  enrollmentDate: string;
  currentClassId?: number;
  currentClassName?: string;
  homeroomTeacherName?: string;
  primaryGuardianName?: string;
  primaryGuardianPhone?: string;
  guardiansCount: number;
  guardians?: StudentGuardianItemDto[];
}

/** A guardian record entered on a form (no user account is created, BR-64 / BR-66). */
export interface GuardianInputDto {
  fullName: string;
  phoneNumber?: string;
  email?: string;
  identityCardNumber?: string;
  occupation?: string;
  address?: string;
  /** RelationshipTypes: 1 Father, 2 Mother, 3 Guardian, 4 Other */
  relationship: number;
  isPrimaryContact?: boolean;
}

/** Request body of POST /students (same field names as the backend CreateStudentDto). */
export interface CreateStudentDto {
  studentCode: string;
  fullName: string;
  dateOfBirth: string;
  gender: string;
  residentialArea?: string;
  isSpecialNeeds?: boolean;
  specialNeedsNotes?: string;
  initialClassId?: number;
  allowAgeException?: boolean;
  ageExceptionReason?: string;
  initialGuardian?: GuardianInputDto;
}

export interface UpdateStudentDto {
  fullName: string;
  dateOfBirth: string;
  gender: string;
  address?: string;
  residentialArea?: string;
  isSpecialNeeds?: boolean;
  specialNeedsNotes?: string;
  avatarUrl?: string;
  status?: string;
  allowAgeException?: boolean;
  ageExceptionReason?: string;
  parentFullName?: string;
  parentPhone?: string;
  parentIdentityCard?: string;
  parentRelationship?: string;
}

export interface StudentFilterDto {
  pageIndex?: number;
  pageSize?: number;
  keyword?: string;
  searchTerm?: string;
  classId?: number;
  grade?: number;
  status?: string;
}

export interface DuplicateCheckResultDto {
  isDuplicate: boolean;
  message?: string;
  existingStudent?: StudentProfileDto;
}

export interface ResolveDuplicateStudentDto {
  studentCode?: string;
  action?: 'Merge' | 'Overwrite' | 'Rename';
  newStudentCode?: string;
  profileData?: CreateStudentDto;
  resolutionAction?: 'Merge' | 'Overwrite' | 'Rename';
  existingStudentId?: number;
  newStudentData?: CreateStudentDto;
}

export interface ImportStudentRowDto {
  rowNumber: number;
  studentCode: string;
  fullName: string;
  dateOfBirth: string;
  gender: string;
  className?: string;
  guardianName?: string;
  guardianPhone?: string;
  guardianIdentityCard?: string;
  guardianEmail?: string;
  relationship?: string;
  /** Preview (UC-09 step 3): new guardian record, existing one reused, or a conflict to fix in the file. */
  guardianMatch?: 'None' | 'New' | 'Reused' | 'Conflict';
  guardianMatchNote?: string;
  /** Has an email address and no account: an account can be requested later (BR-64). */
  guardianEligibleForAccount?: boolean;
  allowAgeException?: boolean;
  isValid: boolean;
  isDuplicate: boolean;
  errorMessage?: string;
}

export interface ImportStudentsConfirmDto {
  schoolYearId?: number;
  allowAgeException?: boolean;
  validRows: ImportStudentRowDto[];
}

export interface ImportStudentsResultDto {
  totalRows: number;
  successCount: number;
  skippedCount: number;
  errorCount: number;
  importedStudentCodes: string[];
  errors?: string[];
  guardiansCreated: number;
  guardiansReused: number;
  guardiansWithoutEmail: number;
  guardiansEligibleForAccountIds: number[];
}

/** UC-10: a guardian record found by search. */
export interface GuardianRecordDto {
  id: number;
  fullName: string;
  phoneNumber?: string;
  email?: string;
  identityCardNumber?: string;
  hasAccount: boolean;
  linkedStudentCount: number;
}

export interface StudentGuardianItemDto {
  guardianId: number;
  userId: number | null;
  fullName: string;
  phoneNumber?: string;
  email?: string;
  identityCardNumber?: string;
  occupation?: string;
  address?: string;
  relationship: number;
  relationshipName: string;
  isPrimaryContact: boolean;
}

/** UC-10: link an existing record (guardianId) or a new one (newGuardian). */
export interface LinkGuardianRequestDto {
  guardianId?: number;
  newGuardian?: GuardianInputDto;
  relationship: number;
  isPrimaryContact: boolean;
}

export interface StudentEnrollmentHistoryItemDto {
  assignmentId: number;
  academicYear: string;
  classId: number;
  className: string;
  grade: number;
  homeroomTeacherName?: string;
  startDate: string;
  endDate?: string;
  isActive: boolean;
  isDraft: boolean;
}

export interface StudentAttendanceHistoryItemDto {
  date: string;
  session: string;
  status: string;
  className?: string;
  remarks?: string;
}

export interface StudentAttendanceHistorySummaryDto {
  totalDays: number;
  presentCount: number;
  absentExcusedCount: number;
  absentUnexcusedCount: number;
  lateCount: number;
  recentRecords: StudentAttendanceHistoryItemDto[];
}

export interface StudentRequestHistoryItemDto {
  requestId: number;
  requestNumber: string;
  requestType: string;
  status: string;
  submissionDate: string;
  processedDate?: string;
  summary?: string;
}

export interface StudentLunchHistoryItemDto {
  date: string;
  mealType: string;
  isActive: boolean;
  isWaitlisted: boolean;
  cancellationSource?: string;
}

export interface StudentLunchHistorySummaryDto {
  activeRegistrationsCount: number;
  cancelledCount: number;
  waitlistedCount: number;
  recentRegistrations: StudentLunchHistoryItemDto[];
}

export interface StudentAuditHistoryItemDto {
  timestamp: string;
  action: string;
  actorName?: string;
  oldValues?: string;
  newValues?: string;
}

export interface StudentHistoryDto {
  studentId: number;
  studentCode: string;
  fullName: string;
  status: string;
  enrollmentDate: string;
  enrollmentHistory: StudentEnrollmentHistoryItemDto[];
  attendanceSummary: StudentAttendanceHistorySummaryDto;
  requests: StudentRequestHistoryItemDto[];
  lunchSummary: StudentLunchHistorySummaryDto;
  auditLogs: StudentAuditHistoryItemDto[];
}
