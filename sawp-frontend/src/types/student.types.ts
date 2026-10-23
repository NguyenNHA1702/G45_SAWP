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
}

export interface CreateStudentDto {
  studentCode: string;
  fullName: string;
  dateOfBirth: string;
  gender: string;
  address?: string;
  residentialArea?: string;
  isSpecialNeeds?: boolean;
  specialNeedsNotes?: string;
  classId?: number;
  parentFullName?: string;
  parentPhone?: string;
  parentIdentityCard?: string;
  parentRelationship?: string;
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
  parentFullName?: string;
  parentPhone?: string;
  parentIdentityCard?: string;
  parentRelationship?: string;
}

export interface StudentFilterDto {
  pageIndex?: number;
  pageSize?: number;
  keyword?: string;
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
  resolutionAction: 'Merge' | 'Overwrite' | 'Rename';
  existingStudentId: number;
  newStudentCode?: string;
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
  relationship?: string;
  isValid: boolean;
  isDuplicate: boolean;
  errorMessage?: string;
}

export interface ImportStudentsConfirmDto {
  schoolYearId?: number;
  validRows: ImportStudentRowDto[];
}

export interface ImportStudentsResultDto {
  totalRows: number;
  successCount: number;
  skippedCount: number;
  errorCount: number;
  importedStudentCodes: string[];
  errors?: string[];
}
