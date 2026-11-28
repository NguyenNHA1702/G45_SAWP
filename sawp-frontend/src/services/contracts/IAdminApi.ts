import type { ApiResponse, PaginatedResult } from '../../types/common.types';
import type {
  AdminUserDto,
  AdminUserQuery,
  CreateUserPayload,
  LunchCutoffDayDto,
  RoleOptionDto,
  SchoolCalendarDayDto,
  UpdateCalendarDayPayload,
  UpdateLunchCutoffPayload,
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
  resetUserPassword(userId: number, newPassword: string): Promise<ApiResponse<boolean>>;
  importStaff(file: File): Promise<ApiResponse<number>>;
  getCalendarDays(month?: string, schoolYearId?: number): Promise<ApiResponse<SchoolCalendarDayDto[]>>;
  upsertCalendarDay(payload: UpdateCalendarDayPayload): Promise<ApiResponse<SchoolCalendarDayDto>>;
  getLunchCutoffDays(month?: string): Promise<ApiResponse<LunchCutoffDayDto[]>>;
  upsertLunchCutoff(payload: UpdateLunchCutoffPayload): Promise<ApiResponse<LunchCutoffDayDto>>;
}
