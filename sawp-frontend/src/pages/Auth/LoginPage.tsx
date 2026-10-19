import { useState } from 'react';
import type { FormEvent } from 'react';
import { useNavigate, useSearchParams } from 'react-router';
import toast from 'react-hot-toast';
import { LogIn, School, ShieldAlert, Sparkles, UserCheck } from 'lucide-react';
import { authApi } from '../../services/implementations/authApi';
import { asApiError } from '../../utils/apiError';
import { SYSTEM_MESSAGES } from '../../constants/messages';
import './LoginPage.css';

/* =====================================================================
 * [DEMO ACCOUNTS CONFIG]
 * Phần này dùng để điền nhanh tài khoản mẫu khi test / demo.
 * Sau này khi deploy thật hoặc xong nghiệm thu, chỉ cần XÓA block này
 * và đoạn <div className="demo-accounts-box">...</div> ở phía dưới JSX.
 * ===================================================================== */
interface DemoAccount {
  roleName: string;
  badgeClass: string;
  username: string;
  password: string;
  description: string;
}

const DEMO_ACCOUNTS: DemoAccount[] = [
  {
    roleName: 'Quản trị viên',
    badgeClass: 'badge--violet',
    username: 'admin',
    password: 'Password@123',
    description: 'Admin hệ thống',
  },
  {
    roleName: 'Ban Giám Hiệu',
    badgeClass: 'badge--amber',
    username: 'board1',
    password: 'Password@123',
    description: 'Hiệu trưởng, duyệt đơn',
  },
  {
    roleName: 'Giáo vụ',
    badgeClass: 'badge--mint',
    username: 'office1',
    password: 'Password@123',
    description: 'Hồ sơ, phân lớp, sửa điểm danh',
  },
  {
    roleName: 'Giáo viên',
    badgeClass: 'badge--violet',
    username: 'teacher1',
    password: 'Password@123',
    description: 'GVCN lớp 1A, điểm danh',
  },
  {
    roleName: 'Nhân viên bếp',
    badgeClass: 'badge--amber',
    username: 'kitchen1',
    password: 'Password@123',
    description: 'Tổng hợp suất ăn bán trú',
  },
  {
    roleName: 'Phụ huynh',
    badgeClass: 'badge--pink',
    username: 'parent1',
    password: 'Password@123',
    description: 'Theo dõi con, chuyên cần',
  },
];
/* ===================================================================== */

export default function LoginPage() {
  const [username, setUsername] = useState('');
  const [password, setPassword] = useState('');
  const [loading, setLoading] = useState(false);
  const [searchParams] = useSearchParams();
  const navigate = useNavigate();

  const isExpired = searchParams.get('expired') === '1';

  const handleLogin = async (e: FormEvent) => {
    e.preventDefault();
    if (!username.trim() || !password) {
      toast.error(SYSTEM_MESSAGES.MSG01.content);
      return;
    }

    setLoading(true);
    try {
      const res = await authApi.login({ username: username.trim(), password });
      toast.success(res.message || SYSTEM_MESSAGES.MSG05.content);

      // Sau khi đăng nhập thành công, tất cả các role đều vào trang chủ Dashboard
      navigate('/dashboard');
    } catch (err) {
      const apiErr = asApiError(err);
      toast.error(apiErr.message);
    } finally {
      setLoading(false);
    }
  };

  /* Helper điền nhanh tài khoản demo */
  const handleSelectDemoAccount = (acc: DemoAccount) => {
    setUsername(acc.username);
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

        <form onSubmit={handleLogin} className="login-form">
          <div className="field">
            <label className="label" htmlFor="username">
              Tên đăng nhập hoặc Email
            </label>
            <input
              id="username"
              type="text"
              className="input"
              placeholder="vd: teacher1 / admin"
              value={username}
              onChange={(e) => setUsername(e.target.value)}
              disabled={loading}
              autoComplete="username"
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
                key={acc.username}
                type="button"
                className="demo-account-chip"
                onClick={() => handleSelectDemoAccount(acc)}
                title={`Bấm để điền: ${acc.username} (${acc.description})`}
              >
                <span className={`badge ${acc.badgeClass} badge--sm`}>{acc.roleName}</span>
                <span className="demo-username">{acc.username}</span>
              </button>
            ))}
          </div>
        </section>
        {/* ===================================================================== */}

        <footer className="login-footer">
          <p className="login-note">
            Quên mật khẩu? Vui lòng liên hệ <strong>Quản trị viên nhà trường</strong> (SRS BR-46) để được cấp lại mật khẩu.
          </p>
        </footer>
      </div>
    </main>
  );
}
