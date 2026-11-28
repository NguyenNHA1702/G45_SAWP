// UC-69 Import Class Assignments

export type ClassImportMode = 'Replace' | 'Merge';

export interface ClassImportClassRowDto {
  rowNumber: number;
  className: string;
  grade: string;
  homeroomTeacherCode?: string | null;
  isValid: boolean;
  errors: string[];
}

export interface ClassImportStudentRowDto {
  rowNumber: number;
  studentCode: string;
  fullName?: string | null;
  className: string;
  requiresAgeException?: boolean;
  isValid: boolean;
  errors: string[];
}

export interface ClassImportRowsDto {
  mode: ClassImportMode;
  allowAgeException?: boolean;
  classes: ClassImportClassRowDto[];
  students: ClassImportStudentRowDto[];
}

export interface ClassImportClassSummaryDto {
  className: string;
  grade: number;
  isNew: boolean;
  studentCount: number;
  capacity: number;
  homeroomTeacherCode?: string | null;
  homeroomTeacherName?: string | null;
  isChanged: boolean;
  isRemoved: boolean;
  studentCodes: string[];
}

export interface ClassImportStudentChangeDto {
  studentCode: string;
  fullName: string;
  fromClass?: string | null;
  toClass?: string | null;
}

export interface ClassImportTeacherChangeDto {
  className: string;
  fromTeacher?: string | null;
  toTeacher?: string | null;
}

export interface ClassImportDiffDto {
  classesAdded: string[];
  classesRemoved: string[];
  studentsAdded: ClassImportStudentChangeDto[];
  studentsRemoved: ClassImportStudentChangeDto[];
  studentsMoved: ClassImportStudentChangeDto[];
  teacherChanges: ClassImportTeacherChangeDto[];
}

export interface ClassImportPreviewDto {
  schoolYearId: number;
  schoolYearName: string;
  mode: ClassImportMode;
  isReimport: boolean;
  classes: ClassImportClassRowDto[];
  students: ClassImportStudentRowDto[];
  classSummaries: ClassImportClassSummaryDto[];
  diff: ClassImportDiffDto;
  warnings: string[];
  validClassRows: number;
  invalidClassRows: number;
  validStudentRows: number;
  invalidStudentRows: number;
}

export interface ClassImportResultDto {
  classesCreated: number;
  classesRemoved: number;
  studentsAdded: number;
  studentsMoved: number;
  studentsRemoved: number;
  teacherChanges: number;
  classesToReview: number;
  skippedRows: number;
  errors: string[];
  warnings: string[];
}
