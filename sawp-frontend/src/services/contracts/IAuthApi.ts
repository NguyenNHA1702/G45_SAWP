import type { ApiResponse } from '../../types/common.types';
import type { ChangePasswordRequest, CurrentUser, LoginRequest, LoginResponse } from '../../types/auth.types';

export interface IAuthApi {
  login(payload: LoginRequest): Promise<ApiResponse<LoginResponse>>;
  changePassword(payload: ChangePasswordRequest): Promise<ApiResponse<boolean>>;
  getMe(): Promise<ApiResponse<CurrentUser>>;
}
