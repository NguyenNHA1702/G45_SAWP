/** BR-02: at least 8 characters with uppercase, lowercase, a digit and a special character (same rule as the API). */
export const meetsPasswordPolicy = (password: string): boolean =>
  password.length >= 8 &&
  /[A-Z]/.test(password) &&
  /[a-z]/.test(password) &&
  /[0-9]/.test(password) &&
  /[!@#$%^&*()_+\-=[\]{};':"\\|,.<>/?]/.test(password);

export const PASSWORD_POLICY_HINT = 'Tối thiểu 8 ký tự, gồm chữ hoa, chữ thường, chữ số và ký tự đặc biệt.';
