export interface LinkedChildSummary {
  studentId: number;
  studentCode: string;
  fullName: string;
  gender: string;
  dateOfBirth: string;
  className?: string;
  relationship: string;
  isPrimaryContact: boolean;
}

export interface StaffProfileDetail {
  staffCode: string;
  department: string;
  position: string;
  isHomeroomTeacher: boolean;
  homeroomClassName?: string;
}

export interface GuardianProfileDetail {
  identityCardNumber: string;
  occupation?: string;
  address?: string;
  children: LinkedChildSummary[];
}

export interface UserProfile {
  userId: number;
  username: string;
  fullName: string;
  email: string;
  phoneNumber?: string;
  isActive: boolean;
  createdAt: string;
  roles: string[];
  staffProfile?: StaffProfileDetail;
  guardianProfile?: GuardianProfileDetail;
}

export interface UpdateProfilePayload {
  fullName: string;
  phoneNumber?: string;
  email?: string;
  address?: string;
  occupation?: string;
}
