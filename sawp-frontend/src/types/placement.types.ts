export interface GeneratePlacementPlanRequestDto {
  schoolYearId: number;
  grade: number;
  planName?: string;
  targetClassIds?: number[];
  targetCapacity?: number;
  maxSpecialNeedsPerClass?: number;
  avoidDuplicateNames?: boolean;
  prioritizeResidentialArea?: boolean;
  balanceGender?: boolean;
  autoCreateClassesIfMissing?: boolean;
  autoCreateClassCount?: number;
  autoCreateSectionSuffixes?: string[];
}

export interface PlacementStudentItemDto {
  studentId: number;
  studentCode: string;
  fullName: string;
  gender: string;
  dateOfBirth: string;
  residentialArea?: string;
  isSpecialNeeds: boolean;
  specialNeedsNotes?: string;
  isManualOverride: boolean;
  notes?: string;
}

export interface PlacementPlanClassPreviewDto {
  classId: number;
  className: string;
  grade: number;
  capacity: number;
  totalStudents: number;
  maleCount: number;
  femaleCount: number;
  specialNeedsCount: number;
  residentialAreaCounts: Record<string, number>;
  students: PlacementStudentItemDto[];
}

export interface PlacementCriteriaWarningDto {
  severity: 'Warning' | 'Violation' | 'Info';
  classId?: number;
  className?: string;
  ruleName: string;
  message: string;
}

export interface PlacementPlanSummaryDto {
  id: number;
  schoolYearId: number;
  schoolYearName: string;
  grade: number;
  planName: string;
  version: number;
  status: 'Draft' | 'Submitted' | 'Approved' | 'Rejected';
  targetClassCount: number;
  targetCapacity: number;
  totalStudents: number;
  createdByUserId: number;
  createdByName: string;
  createdAt: string;
  submittedAt?: string;
  reviewedByUserId?: number;
  reviewedByName?: string;
  reviewedAt?: string;
  reviewNotes?: string;
  hasWarnings: boolean;
  violationCount: number;
}

export interface PlacementPlanDetailDto extends PlacementPlanSummaryDto {
  classes: PlacementPlanClassPreviewDto[];
  criteriaWarnings: PlacementCriteriaWarningDto[];
}

export interface MoveStudentInPlanDto {
  studentId: number;
  fromClassId: number;
  toClassId: number;
  notes?: string;
}

export interface SwapStudentsInPlanDto {
  student1Id: number;
  class1Id: number;
  student2Id: number;
  class2Id: number;
  notes?: string;
}

export interface ReviewPlacementPlanDto {
  isApproved: boolean;
  reviewNotes?: string;
}
