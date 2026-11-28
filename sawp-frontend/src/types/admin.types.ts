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
  canDelete?: boolean;
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
  // No password: the account starts Disabled unless enableNow is checked
  roles: string[];
  enableNow?: boolean;
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

// ---- GAP-05: Admin UC-54..UC-57 ----

export interface PermissionDto {
  id: number;
  code: string;
  name: string;
  module: string;
  description?: string | null;
}

export interface RolePermissionMatrixItemDto {
  roleId: number;
  roleName: string;
  roleDescription?: string | null;
  grantedPermissionCodes: string[];
}

export interface RolePermissionMatrixDto {
  permissions: PermissionDto[];
  roles: RolePermissionMatrixItemDto[];
}

export interface AdminProfileLinkOptionDto {
  profileId: number;
  profileType: 'Staff' | 'Guardian' | string;
  codeOrPhone: string;
  fullName: string;
  secondaryInfo?: string | null;
  linkedUserId?: number | null;
  linkedUsername?: string | null;
}

export interface AdminUserProfileLinkInfoDto {
  userId: number;
  username: string;
  fullName: string;
  email: string;
  roles: string[];
  linkedStaffProfileId?: number | null;
  linkedStaffLabel?: string | null;
  linkedGuardianId?: number | null;
  linkedGuardianLabel?: string | null;
}

export interface AdminProfileLinkingOverviewDto {
  user?: AdminUserProfileLinkInfoDto | null;
  staffProfiles: AdminProfileLinkOptionDto[];
  guardians: AdminProfileLinkOptionDto[];
}

export interface ReferenceItemDto {
  id: number;
  category: string;
  code: string;
  label: string;
  sortOrder: number;
  isActive: boolean;
  updatedAt: string;
}

export interface UpsertReferenceItemPayload {
  category: string;
  code: string;
  label: string;
  sortOrder: number;
  isActive: boolean;
}

export interface NotificationTemplateDto {
  id: number;
  code: string;
  titleTemplate: string;
  bodyTemplate: string;
  isActive: boolean;
  updatedAt: string;
}

export interface UpdateNotificationTemplatePayload {
  titleTemplate: string;
  bodyTemplate: string;
  isActive: boolean;
}
