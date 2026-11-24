import type { ApiResponse } from '../../types/common.types';
import type {
  ChangePasswordRequest,
  CurrentUser,
  ForgotPasswordRequest,
  LoginRequest,
  LoginResponse,
  PasswordLinkCheckResult,
  SetPasswordWithLinkRequest,
} from '../../types/auth.types';

export interface IAuthApi {
  login(payload: LoginRequest): Promise<ApiResponse<LoginResponse>>;
  /** Stores the new token: a password change ends the other sessions (BR-03). */
  changePassword(payload: ChangePasswordRequest): Promise<ApiResponse<LoginResponse>>;
  forgotPassword(payload: ForgotPasswordRequest): Promise<ApiResponse<boolean>>;
  checkPasswordLink(token: string): Promise<ApiResponse<PasswordLinkCheckResult>>;
  setPasswordWithLink(payload: SetPasswordWithLinkRequest): Promise<ApiResponse<boolean>>;
  getMe(): Promise<ApiResponse<CurrentUser>>;
}
