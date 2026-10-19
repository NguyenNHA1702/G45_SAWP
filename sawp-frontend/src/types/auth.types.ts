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
}

export interface CurrentUser {
  userId: number;
  username: string;
  fullName: string;
  email: string;
  roles: string[];
}
