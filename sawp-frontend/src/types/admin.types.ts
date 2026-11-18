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
  createdAt: string;
  roles: string[];
  staffCode: string | null;
  position: string | null;
  department: string | null;
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
  password: string;
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
