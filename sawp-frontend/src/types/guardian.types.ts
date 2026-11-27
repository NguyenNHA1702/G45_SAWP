export interface LinkedChildSummaryDto {
  studentId: number;
  studentCode: string;
  fullName: string;
  dateOfBirth: string;
  gender: string;
  avatarUrl?: string | null;
  status: string;
  currentClassId?: number | null;
  currentClassName?: string | null;
  grade?: number | null;
  homeroomTeacherName?: string | null;
  homeroomTeacherPhone?: string | null;
  relationshipName: string;
  isPrimaryContact: boolean;
}

export interface ChildDashboardDto {
  studentId: number;
  studentCode: string;
  fullName: string;
  dateOfBirth: string;
  gender: string;
  status: string;
  classId?: number;
  className?: string;
  grade?: number;
  homeroomTeacherName?: string;
  homeroomTeacherEmail?: string;
  homeroomTeacherPhone?: string;
  schoolYearName?: string;
  relationship: string;
  isPrimaryContact: boolean;
}
