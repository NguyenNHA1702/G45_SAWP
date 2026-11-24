import { apiClient, TOKEN_KEY, USER_KEY } from '../apiClient';
import { unwrap } from '../../utils/apiError';
import type { IAuthApi } from '../contracts/IAuthApi';
import type {
  ChangePasswordRequest,
  CurrentUser,
  ForgotPasswordRequest,
  LoginRequest,
  LoginResponse,
  PasswordLinkCheckResult,
  SetPasswordWithLinkRequest,
} from '../../types/auth.types';
import type { ApiResponse } from '../../types/common.types';

/** Keeps the signed-in session in sessionStorage (one tab = one session). */
const storeSession = (data: LoginResponse) => {
  sessionStorage.setItem(TOKEN_KEY, data.token);
  sessionStorage.setItem(
    USER_KEY,
    JSON.stringify({
      username: data.username,
      fullName: data.fullName,
      roles: data.roles,
      mustChangePassword: data.mustChangePassword,
    }),
  );
  localStorage.removeItem(TOKEN_KEY);
  localStorage.removeItem(USER_KEY);
};

export const authApi: IAuthApi = {
  login: async (payload: LoginRequest): Promise<ApiResponse<LoginResponse>> => {
    const res = await unwrap(apiClient.post<ApiResponse<LoginResponse>>('/auth/login', payload));
    if (res.data?.token) storeSession(res.data);
    return res;
  },

  changePassword: async (payload: ChangePasswordRequest): Promise<ApiResponse<LoginResponse>> => {
    const res = await unwrap(apiClient.post<ApiResponse<LoginResponse>>('/auth/change-password', payload));
    if (res.data?.token) storeSession(res.data);
    return res;
  },

  forgotPassword: (payload: ForgotPasswordRequest): Promise<ApiResponse<boolean>> =>
    unwrap(apiClient.post<ApiResponse<boolean>>('/auth/forgot-password', payload)),

  checkPasswordLink: (token: string): Promise<ApiResponse<PasswordLinkCheckResult>> =>
    unwrap(apiClient.post<ApiResponse<PasswordLinkCheckResult>>('/auth/password-link/check', { token })),

  setPasswordWithLink: (payload: SetPasswordWithLinkRequest): Promise<ApiResponse<boolean>> =>
    unwrap(apiClient.post<ApiResponse<boolean>>('/auth/password-link/set-password', payload)),

  getMe: (): Promise<ApiResponse<CurrentUser>> => {
    return unwrap(apiClient.get<ApiResponse<CurrentUser>>('/auth/me'));
  },
};
