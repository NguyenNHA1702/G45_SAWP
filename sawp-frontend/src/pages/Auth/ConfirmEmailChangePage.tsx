import { useEffect, useState } from 'react';
import { Link, useNavigate, useSearchParams } from 'react-router';
import toast from 'react-hot-toast';
import { CheckCircle2, MailCheck, ShieldAlert } from 'lucide-react';
import { authApi } from '../../services/implementations/authApi';
import { clearAuthSession } from '../../services/apiClient';
import { asApiError } from '../../utils/apiError';
import { SYSTEM_MESSAGES } from '../../constants/messages';
import type { EmailChangeLinkCheckResult } from '../../types/auth.types';
import './LoginPage.css';

type LinkState = 'checking' | 'valid' | 'confirmed' | 'invalid';

/**
 * BR-69: Opened from the emailed confirmation link (/confirm-email-change?token=...).
 * Needs no sign-in; confirming replaces the old email with the new address.
 */
export default function ConfirmEmailChangePage() {
  const [searchParams] = useSearchParams();
  const navigate = useNavigate();
  const token = searchParams.get('token') ?? '';

  const [state, setState] = useState<LinkState>(token ? 'checking' : 'invalid');
  const [info, setInfo] = useState<EmailChangeLinkCheckResult | null>(null);
  const [confirming, setConfirming] = useState(false);

  useEffect(() => {
    let cancelled = false;
    if (!token) return;
    authApi
      .checkEmailChangeLink(token)
      .then((res) => {
        if (cancelled) return;
        setInfo(res.data ?? null);
        setState('valid');
      })
      .catch(() => {
        if (!cancelled) setState('invalid');
      });
    return () => {
      cancelled = true;
    };
  }, [token]);

  const handleConfirm = async () => {
    setConfirming(true);
    try {
      const res = await authApi.confirmEmailChange(token);
      clearAuthSession();
      toast.success(res.message || 'Đã xác nhận thay đổi email đăng nhập.');
      setState('confirmed');
    } catch (err) {
      const apiErr = asApiError(err);
      toast.error(apiErr.message);
      if (apiErr.code === 'MSG57') setState('invalid');
    } finally {
      setConfirming(false);
    }
  };

  return (
    <main className="login-page">
      <span className="decor decor--circle login-decor-circle" aria-hidden="true" />
      <span className="decor decor--triangle login-decor-triangle" aria-hidden="true" />
      <div className="login-card card card--featured pop-in">
        <div className="card__float-icon">
          <MailCheck size={22} color="#1E293B" />
        </div>
        <header className="login-header">
          <h1 className="login-title">
            Xác nhận <span className="squiggle">đổi email</span>
          </h1>
          <p className="login-desc">Cập nhật địa chỉ email đăng nhập hệ thống SAWP (BR-69).</p>
        </header>

        {state === 'checking' && <p className="login-desc">Đang kiểm tra liên kết…</p>}

        {state === 'invalid' && (
          <>
            <div className="banner banner--danger login-banner" role="alert">
              <ShieldAlert size={18} />
              <span>{SYSTEM_MESSAGES.MSG57.content}</span>
            </div>
            <Link to="/login" className="btn btn--primary btn--full">
              Về trang đăng nhập
            </Link>
          </>
        )}

        {state === 'valid' && info && (
          <div style={{ display: 'flex', flexDirection: 'column', gap: 14 }}>
            <div className="banner" style={{ background: '#F8FAFC', border: '2px solid var(--color-fg)', padding: '12px 14px', borderRadius: 12 }}>
              <div style={{ fontSize: 'var(--fs-sm)', lineHeight: 1.5 }}>
                <div><strong>Tài khoản:</strong> {info.fullName}</div>
                <div><strong>Email cũ:</strong> {info.currentEmail}</div>
                <div><strong>Email mới:</strong> <span style={{ color: '#6D28D9', fontWeight: 700 }}>{info.newEmail}</span></div>
              </div>
            </div>
            <button
              id="btn-confirm-email-change"
              type="button"
              className="btn btn--primary btn--full"
              disabled={confirming}
              onClick={handleConfirm}
            >
              <CheckCircle2 size={16} />
              {confirming ? 'Đang xác nhận…' : 'Xác nhận đổi sang email mới'}
            </button>
          </div>
        )}

        {state === 'confirmed' && (
          <div style={{ display: 'flex', flexDirection: 'column', gap: 14 }}>
            <div className="banner" style={{ background: '#DCFCE7', border: '2px solid var(--color-fg)', padding: '12px 14px', borderRadius: 12 }}>
              <CheckCircle2 size={18} color="#15803D" />
              <span>
                Email đăng nhập đã được đổi thành <strong>{info?.newEmail}</strong>. Vui lòng đăng nhập lại bằng email mới.
              </span>
            </div>
            <button type="button" className="btn btn--primary btn--full" onClick={() => navigate('/login', { replace: true })}>
              Đăng nhập ngay
            </button>
          </div>
        )}
      </div>
    </main>
  );
}
