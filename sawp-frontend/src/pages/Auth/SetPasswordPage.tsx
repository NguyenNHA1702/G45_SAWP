import { useEffect, useState } from 'react';
import type { FormEvent } from 'react';
import { Link, useNavigate, useSearchParams } from 'react-router';
import toast from 'react-hot-toast';
import { ArrowLeft, Lock, ShieldAlert, ShieldCheck } from 'lucide-react';
import { authApi } from '../../services/implementations/authApi';
import { asApiError } from '../../utils/apiError';
import { SYSTEM_MESSAGES } from '../../constants/messages';
import { meetsPasswordPolicy, PASSWORD_POLICY_HINT } from '../../utils/passwordPolicy';
import type { PasswordLinkPurpose } from '../../types/auth.types';
import './LoginPage.css';

type LinkState = 'checking' | 'valid' | 'invalid';

/**
 * UC-66 steps 6-10 (Set New Password) and A3 (first activation). Opened from the emailed link
 * (/reset-password?token=… or /activate?token=…). Opening the page only checks the link; it is used on submit.
 */
export default function SetPasswordPage() {
  const [searchParams] = useSearchParams();
  const navigate = useNavigate();
  const token = searchParams.get('token') ?? '';

  const [linkState, setLinkState] = useState<LinkState>(token ? 'checking' : 'invalid');
  const [purpose, setPurpose] = useState<PasswordLinkPurpose | null>(null);
  const [newPassword, setNewPassword] = useState('');
  const [confirmPassword, setConfirmPassword] = useState('');
  const [saving, setSaving] = useState(false);

  useEffect(() => {
    let cancelled = false;
    if (!token) return;
    authApi
      .checkPasswordLink(token)
      .then((res) => {
        if (cancelled) return;
        setPurpose(res.data?.purpose ?? null);
        setLinkState('valid');
      })
      .catch(() => {
        if (!cancelled) setLinkState('invalid'); // E3: MSG57
      });
    return () => {
      cancelled = true;
    };
  }, [token]);

  const activating = purpose === 'Activation';

  const handleSubmit = async (e: FormEvent) => {
    e.preventDefault();
    if (!meetsPasswordPolicy(newPassword) || newPassword !== confirmPassword) {
      toast.error(SYSTEM_MESSAGES.MSG04.content); // E4
      return;
    }
    setSaving(true);
    try {
      const res = await authApi.setPasswordWithLink({ token, newPassword, confirmPassword });
      toast.success(res.message || SYSTEM_MESSAGES.MSG46.content);
      navigate('/login', { replace: true });
    } catch (err) {
      const apiErr = asApiError(err);
      toast.error(apiErr.message);
      if (apiErr.code === 'MSG57') setLinkState('invalid');
    } finally {
      setSaving(false);
    }
  };

  return (
    <main className="login-page">
      <span className="decor decor--circle login-decor-circle" aria-hidden="true" />
      <span className="decor decor--triangle login-decor-triangle" aria-hidden="true" />
      <div className="login-card card card--featured pop-in">
        <div className="card__float-icon">
          <ShieldCheck size={22} color="#1E293B" />
        </div>
        <header className="login-header">
          <h1 className="login-title">
            {activating ? 'Kích hoạt ' : 'Đặt '}
            <span className="squiggle">{activating ? 'tài khoản' : 'mật khẩu mới'}</span>
          </h1>
          <p className="login-desc">
            {activating ? 'Đặt mật khẩu đầu tiên để bắt đầu sử dụng tài khoản.' : 'Nhập mật khẩu mới cho tài khoản của bạn.'}
          </p>
        </header>

        {linkState === 'checking' && <p className="login-desc">Đang kiểm tra liên kết…</p>}

        {linkState === 'invalid' && (
          <>
            <div className="banner banner--danger login-banner" role="alert">
              <ShieldAlert size={18} />
              <span>{SYSTEM_MESSAGES.MSG57.content}</span>
            </div>
            <Link to="/forgot-password" className="btn btn--primary btn--full">
              Yêu cầu liên kết mới
            </Link>
          </>
        )}

        {linkState === 'valid' && (
          <form onSubmit={handleSubmit} className="login-form" noValidate>
            <div className="field">
              <label className="label" htmlFor="new-password">
                Mật khẩu mới
              </label>
              <input
                id="new-password"
                type="password"
                className="input"
                value={newPassword}
                onChange={(e) => setNewPassword(e.target.value)}
                disabled={saving}
                autoComplete="new-password"
                aria-describedby="new-password-hint"
              />
              <small id="new-password-hint" className="login-desc">
                {PASSWORD_POLICY_HINT}
              </small>
            </div>
            <div className="field">
              <label className="label" htmlFor="confirm-password">
                Xác nhận mật khẩu mới
              </label>
              <input
                id="confirm-password"
                type="password"
                className="input"
                value={confirmPassword}
                onChange={(e) => setConfirmPassword(e.target.value)}
                disabled={saving}
                autoComplete="new-password"
              />
            </div>
            <button id="btn-set-password" type="submit" className="btn btn--primary btn--full login-submit" disabled={saving}>
              {saving ? 'Đang lưu…' : activating ? 'Kích hoạt tài khoản' : 'Đặt mật khẩu'}
              <span className="btn__icon">
                <Lock size={15} />
              </span>
            </button>
          </form>
        )}

        <footer className="login-footer">
          <p className="login-note">
            <Link to="/login">
              <ArrowLeft size={14} style={{ verticalAlign: 'middle' }} /> Quay lại đăng nhập
            </Link>
          </p>
        </footer>
      </div>
    </main>
  );
}
