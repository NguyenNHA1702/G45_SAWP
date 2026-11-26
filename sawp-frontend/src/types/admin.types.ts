// Role names as stored by the API; mirrors SAWP.Backend/Core/Enums/UserRoles.cs
export const UserRole = {
  Admin: 'Admin',
  SchoolBoard: 'SchoolBoard',
  OfficeStaff: 'OfficeStaff',
  Teacher: 'Teacher',
  ServiceStaff: 'ServiceStaff',
  Parent: 'Parent',
} as const;
export type UserRole = (typeof UserRole)[keyof typeof UserRole];

export interface RoleOptionDto {
  id: number;
  name: string;
  description: string | null;
}

export interface AdminUserDto {
  id: number;
  username: string;
  email: string;
  fullName: string;
  phoneNumber: string | null;
  isActive: boolean;
  /** Account state (BR-63); isActive is true only for 'Active'. */
  status: 'PendingActivation' | 'Active' | 'Deactivated';
  createdAt: string;
  roles: string[];
  staffCode: string | null;
  position: string | null;
  department: string | null;
  homeroomClassName?: string | null;
}

export interface AdminUserQuery {
  pageIndex?: number;
  pageSize?: number;
  role?: string;
  isActive?: boolean;
  search?: string;
}

export interface CreateUserPayload {
  username: string;
  email: string;
  fullName: string;
  phoneNumber?: string;
  // No password: the account starts Pending Activation and gets an activation link (BR-63)
  roles: string[];
  staffCode?: string;
  position?: string;
  department?: string;
}

export interface SchoolCalendarDayDto {
  id: number;
  schoolYearId: number;
  schoolYearName: string | null;
  date: string;
  isSchoolDay: boolean;
  note: string | null;
}

export interface UpdateCalendarDayPayload {
  schoolYearId?: number;
  date: string;
  isSchoolDay: boolean;
  note?: string;
}

export interface LunchCutoffDayDto {
  id: number;
  date: string;
  isServing: boolean;
  cutoffTime: string;
  isLocked: boolean;
  remarks: string | null;
}

export interface UpdateLunchCutoffPayload {
  date: string;
  isServing: boolean;
  cutoffTime: string;
  remarks?: string;
}
