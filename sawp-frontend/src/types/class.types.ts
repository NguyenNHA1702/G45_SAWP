export interface SchoolYearDto {
  id: number;
  yearName: string;
  startDate: string;
  endDate: string;
  isActive: boolean;
  totalClasses: number;
}

export interface CreateSchoolYearDto {
  yearName: string;
  startDate: string;
  endDate: string;
  isActive?: boolean;
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
  isPlacementPublished: boolean;
  schoolYearId: number;
  schoolYearName: string;
  homeroomTeacherStaffId?: number;
  homeroomTeacherUserId?: number;
  homeroomTeacherName?: string;
  homeroomTeacherEmail?: string;
  homeroomTeacherPhone?: string;
}

export interface CreateClassDto {
  className: string;
  grade: number;
  capacity: number;
  schoolYearId?: number;
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
  staffId: number;
  fullName: string;
  email: string;
  phone?: string;
  currentAssignedClass?: string;
  isAvailable: boolean;
}

export interface AssignTeacherDto {
  teacherUserId: number;
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
  totalStudents: number;
  isPlacementPublished: boolean;
  schoolYearName: string;
  homeroomTeacherName?: string;
  homeroomTeacherPhone?: string;
  students: RosterStudentItemDto[];
}

export interface PublishPlacementResultDto {
  classId: number;
  className: string;
  isPlacementPublished: boolean;
  publishedStudentsCount: number;
  message: string;
}
