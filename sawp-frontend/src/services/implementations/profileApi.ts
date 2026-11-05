import { apiClient } from '../apiClient';
import type { ApiResponse } from '../../types/common.types';
import type { UserProfile, UpdateProfilePayload } from '../../types/profile.types';

export const profileApi = {
  getProfile: async (): Promise<UserProfile> => {
    const res = await apiClient.get<ApiResponse<UserProfile>>('/auth/profile');
    if (!res.data.success || !res.data.data) {
      throw new Error(res.data.message || 'Không thể tải hồ sơ cá nhân.');
    }
    return res.data.data;
  },

  updateProfile: async (payload: UpdateProfilePayload): Promise<UserProfile> => {
    const res = await apiClient.put<ApiResponse<UserProfile>>('/auth/profile', payload);
    if (!res.data.success || !res.data.data) {
      throw new Error(res.data.message || 'Cập nhật hồ sơ thất bại.');
    }
    return res.data.data;
  },
};
