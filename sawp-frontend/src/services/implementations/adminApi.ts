import { apiClient } from '../apiClient';
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
import type { IAdminApi } from '../contracts/IAdminApi';

export const adminApi: IAdminApi = {
  getUsers: async (query: AdminUserQuery): Promise<ApiResponse<PaginatedResult<AdminUserDto>>> => {
    const res = await apiClient.get<ApiResponse<PaginatedResult<AdminUserDto>>>('/admin/users', {
      params: {
        pageIndex: query.pageIndex ?? 1,
        pageSize: query.pageSize ?? 20,
        ...(query.role ? { role: query.role } : {}),
        ...(query.isActive !== undefined ? { isActive: query.isActive } : {}),
        ...(query.search ? { search: query.search } : {}),
      },
    });
    return res.data;
  },

  getStaff: async (query: AdminUserQuery): Promise<ApiResponse<PaginatedResult<AdminUserDto>>> => {
    const res = await apiClient.get<ApiResponse<PaginatedResult<AdminUserDto>>>('/admin/staff', {
      params: {
        pageIndex: query.pageIndex ?? 1,
        pageSize: query.pageSize ?? 20,
        ...(query.role ? { role: query.role } : {}),
        ...(query.isActive !== undefined ? { isActive: query.isActive } : {}),
        ...(query.search ? { search: query.search } : {}),
      },
    });
    return res.data;
  },

  getRoles: async (): Promise<ApiResponse<RoleOptionDto[]>> => {
    const res = await apiClient.get<ApiResponse<RoleOptionDto[]>>('/admin/roles');
    return res.data;
  },

  createUser: async (payload: CreateUserPayload): Promise<ApiResponse<AdminUserDto>> => {
    const res = await apiClient.post<ApiResponse<AdminUserDto>>('/admin/users', payload);
    return res.data;
  },

  updateUserRoles: async (userId: number, roles: string[]): Promise<ApiResponse<AdminUserDto>> => {
    const res = await apiClient.put<ApiResponse<AdminUserDto>>(`/admin/users/${userId}/roles`, { roles });
    return res.data;
  },

  updateUserStatus: async (userId: number, isActive: boolean): Promise<ApiResponse<boolean>> => {
    const res = await apiClient.patch<ApiResponse<boolean>>(`/admin/users/${userId}/status`, { isActive });
    return res.data;
  },

  deleteUser: async (userId: number): Promise<ApiResponse<boolean>> => {
    const res = await apiClient.delete<ApiResponse<boolean>>(`/admin/users/${userId}`);
    return res.data;
  },

  resendActivationLink: async (userId: number): Promise<ApiResponse<boolean>> => {
    const res = await apiClient.post<ApiResponse<boolean>>(`/admin/users/${userId}/resend-activation`);
    return res.data;
  },

  changeUserEmail: async (userId: number, newEmail: string): Promise<ApiResponse<boolean>> => {
    const res = await apiClient.post<ApiResponse<boolean>>(`/admin/users/${userId}/change-email`, { newEmail });
    return res.data;
  },

  resetUserPassword: async (userId: number, newPassword: string): Promise<ApiResponse<boolean>> => {
    const res = await apiClient.post<ApiResponse<boolean>>(`/admin/users/${userId}/reset-password`, { newPassword });
    return res.data;
  },

  importStaff: async (file: File): Promise<ApiResponse<number>> => {
    const formData = new FormData();
    formData.append('file', file);
    const res = await apiClient.post<ApiResponse<number>>('/admin/staff/import', formData, {
      headers: { 'Content-Type': 'multipart/form-data' },
    });
    return res.data;
  },

  getCalendarDays: async (month?: string, schoolYearId?: number): Promise<ApiResponse<SchoolCalendarDayDto[]>> => {
    const res = await apiClient.get<ApiResponse<SchoolCalendarDayDto[]>>('/admin/calendar', {
      params: {
        ...(month ? { month } : {}),
        ...(schoolYearId ? { schoolYearId } : {}),
      },
    });
    return res.data;
  },

  upsertCalendarDay: async (payload: UpdateCalendarDayPayload): Promise<ApiResponse<SchoolCalendarDayDto>> => {
    const res = await apiClient.put<ApiResponse<SchoolCalendarDayDto>>('/admin/calendar/day', payload);
    return res.data;
  },

  getLunchCutoffDays: async (month?: string): Promise<ApiResponse<LunchCutoffDayDto[]>> => {
    const res = await apiClient.get<ApiResponse<LunchCutoffDayDto[]>>('/admin/lunch-cutoff', {
      params: { ...(month ? { month } : {}) },
    });
    return res.data;
  },

  upsertLunchCutoff: async (payload: UpdateLunchCutoffPayload): Promise<ApiResponse<LunchCutoffDayDto>> => {
    const res = await apiClient.put<ApiResponse<LunchCutoffDayDto>>('/admin/lunch-cutoff', payload);
    return res.data;
  },

  // GAP-05: UC-54..UC-57
  getPermissionsMatrix: async () => {
    const res = await apiClient.get('/admin/permissions');
    return res.data;
  },
  updateRolePermissions: async (roleId: number, grantedPermissionCodes: string[]) => {
    const res = await apiClient.put(`/admin/roles/${roleId}/permissions`, { grantedPermissionCodes });
    return res.data;
  },
  getUserProfileLinks: async (userId: number, search?: string) => {
    const res = await apiClient.get(`/admin/users/${userId}/profile-links`, {
      params: search ? { search } : {},
    });
    return res.data;
  },
  updateUserProfileLink: async (userId: number, profileType: 'Staff' | 'Guardian', profileId: number | null) => {
    const res = await apiClient.put(`/admin/users/${userId}/profile-link`, { profileType, profileId });
    return res.data;
  },
  getReferenceItems: async (category?: string, activeOnly = false) => {
    const res = await apiClient.get('/admin/reference-items', {
      params: { ...(category ? { category } : {}), ...(activeOnly ? { activeOnly: true } : {}) },
    });
    return res.data;
  },
  createReferenceItem: async (payload) => {
    const res = await apiClient.post('/admin/reference-items', payload);
    return res.data;
  },
  updateReferenceItem: async (id: number, payload) => {
    const res = await apiClient.put(`/admin/reference-items/${id}`, payload);
    return res.data;
  },
  getNotificationTemplates: async () => {
    const res = await apiClient.get('/admin/notification-templates');
    return res.data;
  },
  updateNotificationTemplate: async (id: number, payload) => {
    const res = await apiClient.put(`/admin/notification-templates/${id}`, payload);
    return res.data;
  },
};
