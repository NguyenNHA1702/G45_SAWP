export interface LoginRequest {
  username: string;
  password: string;
}

export interface ChangePasswordRequest {
  currentPassword: string;
  newPassword: string;
}

export interface LoginResponse {
  token: string;
  username: string;
  fullName: string;
  roles: string[];
  expiresAt: string;
  /** True after an administrator-assisted reset: Change Password must come first (UC-60 A1). */
  mustChangePassword: boolean;
}

/** UC-66: request a recovery or activation link. */
export interface ForgotPasswordRequest {
  email: string;
}

export type PasswordLinkPurpose = 'Activation' | 'PasswordRecovery';

export interface PasswordLinkCheckResult {
  purpose: PasswordLinkPurpose;
}

export interface SetPasswordWithLinkRequest {
  token: string;
  newPassword: string;
  confirmPassword: string;
}

export interface CurrentUser {
  userId: number;
  username: string;
  fullName: string;
  email: string;
  roles: string[];
}

export interface EmailChangeLinkCheckResult {
  currentEmail: string;
  newEmail: string;
  fullName: string;
}
