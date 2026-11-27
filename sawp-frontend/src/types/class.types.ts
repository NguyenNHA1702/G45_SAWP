export interface SchoolYearDto {
  id: number;
  yearName: string;
  startDate: string;
  endDate: string;
  isActive: boolean;
  /** Draft, Active or Archived (UC-13, BR-72). */
  status: SchoolYearStatus;
  totalClasses: number;
}

export type SchoolYearStatus = 'Draft' | 'Active' | 'Archived';

export interface CreateSchoolYearDto {
  yearName: string;
  startDate: string;
  endDate: string;
}

export interface ClassSummaryDto {
  id: number;
  className: string;
  grade: number;
  capacity: number;
  enrolledStudentsCount: number;
  enrolledCount?: number;
  availableCapacity: number;
  roomNumber?: string;
  status: string;
  schoolYearId: number;
  schoolYearName: string;
  homeroomTeacherStaffId?: number;
  homeroomTeacherUserId?: number;
  homeroomTeacherName?: string;
  homeroomTeacherEmail?: string;
  homeroomTeacherPhone?: string;
  homeroomTeacherStaffCode?: string;
  /** Review mark of a class of a Draft school year (UC-14 A2, BR-72). */
  isReviewed?: boolean;
  reviewedAt?: string;
}

export interface CreateClassDto {
  className: string;
  grade: number;
  capacity: number;
  schoolYearId?: number;
  roomNumber?: string;
  homeroomTeacherUserId?: number;
}

export interface InitializeBatchClassesDto {
  schoolYearId?: number;
  targetGrade?: number;
  grades?: number[];
  sectionNames?: string[];
  sectionSuffixes?: string[];
  capacityPerClass?: number;
  defaultCapacity?: number;
}

export interface UpdateClassDto {
  className: string;
  capacity: number;
  status?: string;
}

export interface TeacherOptionDto {
  userId: number;
  staffProfileId?: number;
  staffId?: number;
  fullName: string;
  staffCode?: string;
  department?: string;
  email?: string;
  phone?: string;
  currentHomeroomClassName?: string;
  currentAssignedClass?: string;
  hasActiveHomeroom?: boolean;
  isAvailable?: boolean;
}

export interface AssignTeacherDto {
  staffProfileId?: number;
  teacherUserId?: number;
  effectiveDate?: string;
}

export interface AssignStudentsDto {
  studentIds: number[];
}

export interface RosterStudentItemDto {
  studentId: number;
  studentCode: string;
  fullName: string;
  dateOfBirth: string;
  gender: string;
  enrollmentDate: string;
  status: string;
  primaryGuardianName?: string;
  primaryGuardianPhone?: string;
  emergencyContactPhone?: string;
}

export interface ClassRosterDto {
  classId: number;
  className: string;
  grade: number;
  capacity: number;
  academicYear?: string;
  schoolYearName?: string;
  schoolYearStartDate?: string;
  schoolYearEndDate?: string;
  roomNumber?: string;
  totalEnrolled?: number;
  totalStudents?: number;
  homeroomTeacherName?: string;
  homeroomTeacherPhone?: string;
  homeroomTeacherEmail?: string;
  students: RosterStudentItemDto[];
}

