import type { ApiResponse, PaginatedResult } from '../../types/common.types';
import type {
  AdminProfileLinkingOverviewDto,
  AdminUserDto,
  AdminUserQuery,
  CreateUserPayload,
  LunchCutoffDayDto,
  NotificationTemplateDto,
  ReferenceItemDto,
  RoleOptionDto,
  RolePermissionMatrixDto,
  SchoolCalendarDayDto,
  UpdateCalendarDayPayload,
  UpdateLunchCutoffPayload,
  UpdateNotificationTemplatePayload,
  UpsertReferenceItemPayload,
} from '../../types/admin.types';

export interface IAdminApi {
  getUsers(query: AdminUserQuery): Promise<ApiResponse<PaginatedResult<AdminUserDto>>>;
  getStaff(query: AdminUserQuery): Promise<ApiResponse<PaginatedResult<AdminUserDto>>>;
  getRoles(): Promise<ApiResponse<RoleOptionDto[]>>;
  createUser(payload: CreateUserPayload): Promise<ApiResponse<AdminUserDto>>;
  updateUserRoles(userId: number, roles: string[]): Promise<ApiResponse<AdminUserDto>>;
  updateUserStatus(userId: number, isActive: boolean): Promise<ApiResponse<boolean>>;
  deleteUser(userId: number): Promise<ApiResponse<boolean>>;
  resendActivationLink(userId: number): Promise<ApiResponse<boolean>>;
  changeUserEmail(userId: number, newEmail: string): Promise<ApiResponse<boolean>>;
  resetUserPassword(userId: number, newPassword: string): Promise<ApiResponse<boolean>>;
  importStaff(file: File): Promise<ApiResponse<number>>;
  getCalendarDays(month?: string, schoolYearId?: number): Promise<ApiResponse<SchoolCalendarDayDto[]>>;
  upsertCalendarDay(payload: UpdateCalendarDayPayload): Promise<ApiResponse<SchoolCalendarDayDto>>;
  getLunchCutoffDays(month?: string): Promise<ApiResponse<LunchCutoffDayDto[]>>;
  upsertLunchCutoff(payload: UpdateLunchCutoffPayload): Promise<ApiResponse<LunchCutoffDayDto>>;

  // GAP-05: UC-54..UC-57
  getPermissionsMatrix(): Promise<ApiResponse<RolePermissionMatrixDto>>;
  updateRolePermissions(roleId: number, grantedPermissionCodes: string[]): Promise<ApiResponse<boolean>>;
  getUserProfileLinks(userId: number, search?: string): Promise<ApiResponse<AdminProfileLinkingOverviewDto>>;
  updateUserProfileLink(userId: number, profileType: 'Staff' | 'Guardian', profileId: number | null): Promise<ApiResponse<boolean>>;
  getReferenceItems(category?: string, activeOnly?: boolean): Promise<ApiResponse<ReferenceItemDto[]>>;
  createReferenceItem(payload: UpsertReferenceItemPayload): Promise<ApiResponse<ReferenceItemDto>>;
  updateReferenceItem(id: number, payload: UpsertReferenceItemPayload): Promise<ApiResponse<ReferenceItemDto>>;
  getNotificationTemplates(): Promise<ApiResponse<NotificationTemplateDto[]>>;
  updateNotificationTemplate(id: number, payload: UpdateNotificationTemplatePayload): Promise<ApiResponse<NotificationTemplateDto>>;
}
