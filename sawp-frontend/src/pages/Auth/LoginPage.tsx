import { useEffect, useState } from 'react';
import type { FormEvent } from 'react';
import { Link, useNavigate, useSearchParams } from 'react-router';
import toast from 'react-hot-toast';
import { LogIn, School, ShieldAlert, Sparkles, UserCheck } from 'lucide-react';
import { getAuthToken, getAuthUser, clearAuthSession } from '../../services/apiClient';
import { authApi } from '../../services/implementations/authApi';
import { asApiError } from '../../utils/apiError';
import { SYSTEM_MESSAGES } from '../../constants/messages';
import './LoginPage.css';

/* =====================================================================
 * [DEMO ACCOUNTS CONFIG]
 * Phần này dùng để điền nhanh tài khoản mẫu khi test / demo (chỉ dùng Email theo BR-01).
 * ===================================================================== */
interface DemoAccount {
  roleName: string;
  badgeClass: string;
  email: string;
  password: string;
  description: string;
}

const DEMO_ACCOUNTS: DemoAccount[] = [
  {
    roleName: 'Quản trị viên',
    badgeClass: 'badge--violet',
    email: 'admin@sawp.edu.vn',
    password: 'Password@123',
    description: 'Admin hệ thống',
  },
  {
    roleName: 'Ban Giám Hiệu',
    badgeClass: 'badge--amber',
    email: 'hieutruong@sawp.edu.vn',
    password: 'Password@123',
    description: 'Hiệu trưởng, duyệt đơn',
  },
  {
    roleName: 'Giáo vụ',
    badgeClass: 'badge--mint',
    email: 'giaovu@sawp.edu.vn',
    password: 'Password@123',
    description: 'Hồ sơ, phân lớp, sửa điểm danh',
  },
  {
    roleName: 'Giáo viên',
    badgeClass: 'badge--violet',
    email: 'teacher1@sawp.edu.vn',
    password: 'Password@123',
    description: 'GVCN lớp 1A, điểm danh',
  },
  {
    roleName: 'Nhân viên bếp',
    badgeClass: 'badge--amber',
    email: 'kitchen1@sawp.edu.vn',
    password: 'Password@123',
    description: 'Tổng hợp suất ăn bán trú',
  },
  {
    roleName: 'Phụ huynh',
    badgeClass: 'badge--pink',
    email: 'parent1@gmail.com',
    password: 'Password@123',
    description: 'Theo dõi con, chuyên cần',
  },
];
/* ===================================================================== */

function isJwtValid(token: string): boolean {
  try {
    const [, payload] = token.split('.');
    if (!payload) return false;
    const base64 = payload.replace(/-/g, '+').replace(/_/g, '/');
    const decoded = JSON.parse(atob(base64));
    if (typeof decoded.exp === 'number' && decoded.exp * 1000 <= Date.now()) {
      return false;
    }
    return true;
  } catch {
    return false;
  }
}

export default function LoginPage() {
  const [email, setEmail] = useState('');
  const [password, setPassword] = useState('');
  const [loading, setLoading] = useState(false);
  const [searchParams] = useSearchParams();
  const navigate = useNavigate();

  const isExpired = searchParams.get('expired') === '1';

  // UC-01 A1 (BUG-AUTH-05): already signed-in user opening /login goes directly to dashboard / change-password
  useEffect(() => {
    if (isExpired) return;
    const token = getAuthToken();
    if (!token) return;
    if (!isJwtValid(token)) {
      clearAuthSession();
      return;
    }
    try {
      const user = JSON.parse(getAuthUser() ?? '{}');
      navigate(user?.mustChangePassword ? '/change-password' : '/dashboard', { replace: true });
    } catch {
      navigate('/dashboard', { replace: true });
    }
  }, [isExpired, navigate]);

  const handleLogin = async (e: FormEvent) => {
    e.preventDefault();
    if (!email.trim() || !password) {
      toast.error(SYSTEM_MESSAGES.MSG01.content);
      return;
    }

    setLoading(true);
    try {
      const res = await authApi.login({ username: email.trim(), password });
      toast.success(res.message || SYSTEM_MESSAGES.MSG05.content);

      // A temporary password from an administrator must be changed first (UC-60 A1); otherwise every role
      // goes to the Dashboard
      navigate(res.data?.mustChangePassword ? '/change-password' : '/dashboard');
    } catch (err) {
      const apiErr = asApiError(err);
      toast.error(apiErr.message);
    } finally {
      setLoading(false);
    }
  };

  /* Helper điền nhanh tài khoản demo */
  const handleSelectDemoAccount = (acc: DemoAccount) => {
    setEmail(acc.email);
    setPassword(acc.password);
    toast.success(`Đã điền sẵn tài khoản: ${acc.roleName}`);
  };

  return (
    <main className="login-page">
      {/* Memphis decorative elements */}
      <span className="decor decor--circle login-decor-circle" aria-hidden="true" />
      <span className="decor decor--triangle login-decor-triangle" aria-hidden="true" />
      <span className="decor decor--square login-decor-square" aria-hidden="true" />

      <div className="login-card card card--featured pop-in">
        <div className="card__float-icon">
          <School size={22} color="#1E293B" />
        </div>

        <header className="login-header">
          <span className="login-badge badge badge--violet">
            <Sparkles size={14} /> SAWP Platform
          </span>
          <h1 className="login-title">
            Đăng <span className="squiggle">nhập</span>
          </h1>
          <p className="login-desc">Hệ thống Quản lý Hành chính & Bán trú Tiểu học</p>
        </header>

        {isExpired && (
          <div className="banner banner--warning login-banner" role="alert">
            <ShieldAlert size={18} />
            <span>{SYSTEM_MESSAGES.MSG07.content}</span>
          </div>
        )}

        <form onSubmit={handleLogin} className="login-form" noValidate>
          <div className="field">
            <label className="label" htmlFor="email">
              Email
            </label>
            <input
              id="email"
              type="email"
              className="input"
              placeholder="vd: teacher1@sawp.edu.vn"
              value={email}
              onChange={(e) => setEmail(e.target.value)}
              disabled={loading}
              autoComplete="email"
              required
            />
          </div>

          <div className="field">
            <label className="label" htmlFor="password">
              Mật khẩu
            </label>
            <input
              id="password"
              type="password"
              className="input"
              placeholder="••••••••"
              value={password}
              onChange={(e) => setPassword(e.target.value)}
              disabled={loading}
              autoComplete="current-password"
              required
            />
          </div>

          <button id="btn-login" type="submit" className="btn btn--primary btn--full login-submit" disabled={loading}>
            {loading ? 'Đang xác thực…' : 'Đăng nhập ngay'}
            <span className="btn__icon">
              <LogIn size={15} />
            </span>
          </button>
        </form>

        {/* =====================================================================
         * [DEMO ACCOUNTS UI]
         * Khi bỏ phần demo, hãy xóa toàn bộ thẻ <div className="demo-accounts-box">...</div> này
         * ===================================================================== */}
        <section className="demo-accounts-box" aria-label="Tài khoản dùng thử demo">
          <div className="demo-accounts-header">
            <UserCheck size={16} />
            <span>Tài khoản mẫu (Bấm để điền nhanh)</span>
          </div>
          <div className="demo-accounts-grid">
            {DEMO_ACCOUNTS.map((acc) => (
              <button
                key={acc.email}
                type="button"
                className="demo-account-chip"
                onClick={() => handleSelectDemoAccount(acc)}
                title={`Bấm để điền: ${acc.email} (${acc.description})`}
              >
                <span className={`badge ${acc.badgeClass} badge--sm`}>{acc.roleName}</span>
                <span className="demo-username">{acc.email}</span>
              </button>
            ))}
          </div>
        </section>
        {/* ===================================================================== */}

        <footer className="login-footer">
          <p className="login-note">
            <Link id="link-forgot-password" to="/forgot-password">
              Quên mật khẩu?
            </Link>
          </p>
        </footer>
      </div>
    </main>
  );
}
