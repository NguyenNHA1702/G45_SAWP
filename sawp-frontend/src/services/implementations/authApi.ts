import { apiClient, TOKEN_KEY, USER_KEY } from '../apiClient';
import { unwrap } from '../../utils/apiError';
import type { IAuthApi } from '../contracts/IAuthApi';
import type { ChangePasswordRequest, CurrentUser, LoginRequest, LoginResponse } from '../../types/auth.types';
import type { ApiResponse } from '../../types/common.types';

export const authApi: IAuthApi = {
  login: async (payload: LoginRequest): Promise<ApiResponse<LoginResponse>> => {
    const res = await unwrap(apiClient.post<ApiResponse<LoginResponse>>('/auth/login', payload));
    if (res.data?.token) {
      localStorage.setItem(TOKEN_KEY, res.data.token);
      localStorage.setItem(
        USER_KEY,
        JSON.stringify({
          username: res.data.username,
          fullName: res.data.fullName,
          roles: res.data.roles,
        }),
      );
    }
    return res;
  },

  changePassword: (payload: ChangePasswordRequest): Promise<ApiResponse<boolean>> => {
    return unwrap(apiClient.post<ApiResponse<boolean>>('/auth/change-password', payload));
  },

  getMe: (): Promise<ApiResponse<CurrentUser>> => {
    return unwrap(apiClient.get<ApiResponse<CurrentUser>>('/auth/me'));
  },
};
