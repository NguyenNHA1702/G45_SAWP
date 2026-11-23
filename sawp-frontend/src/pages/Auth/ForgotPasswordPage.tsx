import { useState } from 'react';
import type { FormEvent } from 'react';
import { Link } from 'react-router';
import toast from 'react-hot-toast';
import { ArrowLeft, KeyRound, Mail, Send } from 'lucide-react';
import { authApi } from '../../services/implementations/authApi';
import { asApiError } from '../../utils/apiError';
import { SYSTEM_MESSAGES } from '../../constants/messages';
import './LoginPage.css';

const EMAIL_PATTERN = /^[^\s@]+@[^\s@]+\.[^\s@]+$/;

/** UC-66 steps 1-5: request a password recovery link (or a new activation link). */
export default function ForgotPasswordPage() {
  const [email, setEmail] = useState('');
  const [loading, setLoading] = useState(false);
  const [sent, setSent] = useState(false);

  const handleSubmit = async (e: FormEvent) => {
    e.preventDefault();
    const value = email.trim();
    if (!EMAIL_PATTERN.test(value)) {
      toast.error(SYSTEM_MESSAGES.MSG01.content); // E1
      return;
    }
    setLoading(true);
    try {
      const res = await authApi.forgotPassword({ email: value });
      // Same answer whether or not the address is registered (MSG56)
      toast.success(res.message || SYSTEM_MESSAGES.MSG56.content);
      setSent(true);
    } catch (err) {
      toast.error(asApiError(err).message); // MSG58 / MSG59
    } finally {
      setLoading(false);
    }
  };

  return (
    <main className="login-page">
      <span className="decor decor--circle login-decor-circle" aria-hidden="true" />
      <span className="decor decor--square login-decor-square" aria-hidden="true" />
      <div className="login-card card card--featured pop-in">
        <div className="card__float-icon">
          <KeyRound size={22} color="#1E293B" />
        </div>
        <header className="login-header">
          <h1 className="login-title">
            Quên <span className="squiggle">mật khẩu</span>
          </h1>
          <p className="login-desc">
            Nhập địa chỉ email đã đăng ký. Hệ thống sẽ gửi một đường liên kết để đặt mật khẩu mới (hoặc kích hoạt tài khoản).
          </p>
        </header>

        {sent && (
          <div className="banner banner--info login-banner" role="status">
            <Mail size={18} />
            <span>{SYSTEM_MESSAGES.MSG56.content}</span>
          </div>
        )}

        <form onSubmit={handleSubmit} className="login-form" noValidate>
          <div className="field">
            <label className="label" htmlFor="forgot-email">
              Email
            </label>
            <input
              id="forgot-email"
              type="email"
              className="input"
              placeholder="vd: ten@truong.edu.vn"
              value={email}
              onChange={(e) => setEmail(e.target.value)}
              disabled={loading}
              autoComplete="email"
              required
            />
          </div>
          <button id="btn-forgot-submit" type="submit" className="btn btn--primary btn--full login-submit" disabled={loading}>
            {loading ? 'Đang gửi…' : 'Gửi liên kết'}
            <span className="btn__icon">
              <Send size={15} />
            </span>
          </button>
        </form>

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
