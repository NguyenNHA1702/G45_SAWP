import { useState } from 'react';
import type { FormEvent } from 'react';
import { useNavigate } from 'react-router';
import toast from 'react-hot-toast';
import { KeyRound, Lock, LogOut } from 'lucide-react';
import { authApi } from '../../services/implementations/authApi';
import { clearAuthSession } from '../../services/apiClient';
import { asApiError } from '../../utils/apiError';
import { SYSTEM_MESSAGES } from '../../constants/messages';
import { meetsPasswordPolicy, PASSWORD_POLICY_HINT } from '../../utils/passwordPolicy';
import './LoginPage.css';

/**
 * UC-60 A1, BR-02: after an administrator-assisted reset the temporary password must be changed before the
 * user can do anything else (the API refuses other requests until then).
 */
export default function ChangeTemporaryPasswordPage() {
  const navigate = useNavigate();
  const [currentPassword, setCurrentPassword] = useState('');
  const [newPassword, setNewPassword] = useState('');
  const [confirmPassword, setConfirmPassword] = useState('');
  const [saving, setSaving] = useState(false);

  const handleSubmit = async (e: FormEvent) => {
    e.preventDefault();
    if (!currentPassword || !newPassword || !confirmPassword) {
      toast.error(SYSTEM_MESSAGES.MSG01.content);
      return;
    }
    if (!meetsPasswordPolicy(newPassword) || newPassword !== confirmPassword || newPassword === currentPassword) {
      toast.error(SYSTEM_MESSAGES.MSG04.content);
      return;
    }
    setSaving(true);
    try {
      const res = await authApi.changePassword({ currentPassword, newPassword });
      toast.success(res.message || SYSTEM_MESSAGES.MSG46.content);
      navigate('/dashboard', { replace: true });
    } catch (err) {
      toast.error(asApiError(err).message);
    } finally {
      setSaving(false);
    }
  };

  const handleSignOut = () => {
    clearAuthSession();
    navigate('/login', { replace: true });
  };

  return (
    <main className="login-page">
      <span className="decor decor--circle login-decor-circle" aria-hidden="true" />
      <div className="login-card card card--featured pop-in">
        <div className="card__float-icon">
          <KeyRound size={22} color="#1E293B" />
        </div>
        <header className="login-header">
          <h1 className="login-title">
            Đổi <span className="squiggle">mật khẩu</span>
          </h1>
          <p className="login-desc">
            Tài khoản của bạn đang dùng mật khẩu tạm thời do quản trị viên cấp. Vui lòng đặt mật khẩu mới để tiếp tục.
          </p>
        </header>
        <form onSubmit={handleSubmit} className="login-form" noValidate>
          <div className="field">
            <label className="label" htmlFor="temp-current">
              Mật khẩu tạm thời
            </label>
            <input id="temp-current" type="password" className="input" value={currentPassword}
              onChange={(e) => setCurrentPassword(e.target.value)} disabled={saving} autoComplete="current-password" />
          </div>
          <div className="field">
            <label className="label" htmlFor="temp-new">
              Mật khẩu mới
            </label>
            <input id="temp-new" type="password" className="input" value={newPassword}
              onChange={(e) => setNewPassword(e.target.value)} disabled={saving} autoComplete="new-password"
              aria-describedby="temp-new-hint" />
            <small id="temp-new-hint" className="login-desc">
              {PASSWORD_POLICY_HINT}
            </small>
          </div>
          <div className="field">
            <label className="label" htmlFor="temp-confirm">
              Xác nhận mật khẩu mới
            </label>
            <input id="temp-confirm" type="password" className="input" value={confirmPassword}
              onChange={(e) => setConfirmPassword(e.target.value)} disabled={saving} autoComplete="new-password" />
          </div>
          <button id="btn-change-temp-password" type="submit" className="btn btn--primary btn--full login-submit" disabled={saving}>
            {saving ? 'Đang lưu…' : 'Đổi mật khẩu'}
            <span className="btn__icon">
              <Lock size={15} />
            </span>
          </button>
        </form>
        <footer className="login-footer">
          <button type="button" className="btn btn--secondary btn--sm" onClick={handleSignOut}>
            <LogOut size={14} /> Đăng xuất
          </button>
        </footer>
      </div>
    </main>
  );
}
