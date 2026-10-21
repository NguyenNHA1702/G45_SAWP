export interface ChildSummary {
  studentId: number;
  studentCode: string;
  fullName: string;
  avatarUrl?: string | null;
  gender: string;
  dateOfBirth: string;
  classId: number;
  className: string;
  relationship: string;
  isPrimaryContact: boolean;
}
