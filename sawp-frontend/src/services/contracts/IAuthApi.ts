import type { ApiResponse } from '../../types/common.types';
import type {
  ChangePasswordRequest,
  CurrentUser,
  EmailChangeLinkCheckResult,
  ForgotPasswordRequest,
  LoginRequest,
  LoginResponse,
  PasswordLinkCheckResult,
  SetPasswordWithLinkRequest,
} from '../../types/auth.types';

export interface IAuthApi {
  login(payload: LoginRequest): Promise<ApiResponse<LoginResponse>>;
  logout(): Promise<ApiResponse<boolean>>;
  /** Stores the new token: a password change ends the other sessions (BR-03). */
  changePassword(payload: ChangePasswordRequest): Promise<ApiResponse<LoginResponse>>;
  forgotPassword(payload: ForgotPasswordRequest): Promise<ApiResponse<boolean>>;
  checkPasswordLink(token: string): Promise<ApiResponse<PasswordLinkCheckResult>>;
  setPasswordWithLink(payload: SetPasswordWithLinkRequest): Promise<ApiResponse<boolean>>;
  checkEmailChangeLink(token: string): Promise<ApiResponse<EmailChangeLinkCheckResult>>;
  confirmEmailChange(token: string): Promise<ApiResponse<boolean>>;
  getMe(): Promise<ApiResponse<CurrentUser>>;
}
