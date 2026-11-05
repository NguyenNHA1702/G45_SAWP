import { useState } from 'react';
import type { FormEvent } from 'react';
import toast from 'react-hot-toast';
import { Eye, EyeOff, KeyRound, Lock, ShieldCheck } from 'lucide-react';
import { authApi } from '../../services/implementations/authApi';
import { asApiError } from '../../utils/apiError';
import { SYSTEM_MESSAGES } from '../../constants/messages';
import { GeometricModal } from '../common/GeometricModal';

interface Props {
  open: boolean;
  onClose: () => void;
}

export function ChangePasswordModal({ open, onClose }: Props) {
  const [currentPassword, setCurrentPassword] = useState('');
  const [newPassword, setNewPassword] = useState('');
  const [confirmPassword, setConfirmPassword] = useState('');
  const [loading, setLoading] = useState(false);

  // Show/Hide visibility states
  const [showCurrent, setShowCurrent] = useState(false);
  const [showNew, setShowNew] = useState(false);
  const [showConfirm, setShowConfirm] = useState(false);

  // Password rules validation
  const hasMinLength = newPassword.length >= 8;
  const hasLetter = /[a-zA-Z]/.test(newPassword);
  const hasNumberOrSymbol = /[\d!@#$%^&*(),.?":{}|<>]/.test(newPassword);
  const isMatch = confirmPassword.length > 0 && newPassword === confirmPassword;

  const handleResetAndClose = () => {
    setCurrentPassword('');
    setNewPassword('');
    setConfirmPassword('');
    setShowCurrent(false);
    setShowNew(false);
    setShowConfirm(false);
    onClose();
  };

  const handleSubmit = async (e: FormEvent) => {
    e.preventDefault();

    if (!currentPassword || !newPassword || !confirmPassword) {
      toast.error(SYSTEM_MESSAGES.MSG01.content);
      return;
    }

    if (newPassword.length < 8) {
      toast.error('Mật khẩu mới phải có tối thiểu 8 ký tự.');
      return;
    }

    if (newPassword !== confirmPassword) {
      toast.error(SYSTEM_MESSAGES.MSG04.content);
      return;
    }

    setLoading(true);
    try {
      const res = await authApi.changePassword({ currentPassword, newPassword });
      toast.success(res.message || SYSTEM_MESSAGES.MSG46.content);
      handleResetAndClose();
    } catch (err) {
      const apiErr = asApiError(err);
      toast.error(apiErr.message);
    } finally {
      setLoading(false);
    }
  };

  return (
    <GeometricModal
      open={open}
      onClose={handleResetAndClose}
      title="Đổi mật khẩu tài khoản"
      subtitle="Cập nhật mật khẩu định kỳ để bảo vệ tài khoản công tác và dữ liệu học sinh"
      badgeText="Bảo mật tài khoản"
      badgeColor="violet"
      icon={<KeyRound size={22} color="var(--color-fg)" />}
      iconBg="var(--color-tertiary)"
      maxWidth="md"
    >
      {/* Policy Tip Box */}
      <div
        style={{
          display: 'flex',
          alignItems: 'flex-start',
          gap: 12,
          padding: '12px 16px',
          background: '#FFFDF5',
          border: '2px solid var(--color-fg)',
          boxShadow: '2px 2px 0 0 var(--color-fg)',
          borderRadius: 'var(--radius-md)',
          marginBottom: 18,
          fontSize: 'var(--fs-xs)',
          lineHeight: 1.5,
        }}
      >
        <ShieldCheck size={18} color="var(--color-accent)" style={{ flexShrink: 0, marginTop: 2 }} />
        <div>
          <strong style={{ color: 'var(--color-fg)' }}>Yêu cầu độ mạnh mật khẩu:</strong>{' '}
          Tối thiểu 8 ký tự, khuyến nghị kết hợp chữ in hoa, chữ thường, số và ký tự đặc biệt để đảm bảo an toàn.
        </div>
      </div>

      <form onSubmit={handleSubmit} style={{ display: 'flex', flexDirection: 'column', gap: 16 }}>
        {/* 1. Mật khẩu hiện tại */}
        <div className="field" style={{ display: 'flex', flexDirection: 'column', gap: 6 }}>
          <label className="label" htmlFor="cur-pass" style={{ fontSize: 'var(--fs-xs)', fontWeight: 800, letterSpacing: '0.04em' }}>
            MẬT KHẨU HIỆN TẠI <span style={{ color: 'var(--color-secondary)' }}>*</span>
          </label>
          <div style={{ position: 'relative', width: '100%' }}>
            <input
              id="cur-pass"
              type={showCurrent ? 'text' : 'password'}
              className="input"
              value={currentPassword}
              onChange={(e) => setCurrentPassword(e.target.value)}
              disabled={loading}
              placeholder="Nhập mật khẩu bạn đang sử dụng"
              style={{
                width: '100%',
                boxSizing: 'border-box',
                borderRadius: 'var(--radius-full)',
                border: '2px solid var(--color-fg)',
                padding: '10px 46px 10px 18px',
                fontWeight: 600,
                fontSize: 'var(--fs-sm)',
              }}
              required
            />
            <button
              type="button"
              onClick={() => setShowCurrent(!showCurrent)}
              aria-label={showCurrent ? 'Ẩn mật khẩu' : 'Hiện mật khẩu'}
              style={{
                position: 'absolute',
                right: 14,
                top: '50%',
                transform: 'translateY(-50%)',
                background: 'none',
                border: 'none',
                cursor: 'pointer',
                color: 'var(--color-muted-fg)',
                display: 'grid',
                placeItems: 'center',
                padding: 4,
              }}
            >
              {showCurrent ? <EyeOff size={18} /> : <Eye size={18} />}
            </button>
          </div>
        </div>

        {/* 2. Mật khẩu mới */}
        <div className="field" style={{ display: 'flex', flexDirection: 'column', gap: 6 }}>
          <label className="label" htmlFor="new-pass" style={{ fontSize: 'var(--fs-xs)', fontWeight: 800, letterSpacing: '0.04em' }}>
            MẬT KHẨU MỚI <span style={{ color: 'var(--color-secondary)' }}>*</span>
          </label>
          <div style={{ position: 'relative', width: '100%' }}>
            <input
              id="new-pass"
              type={showNew ? 'text' : 'password'}
              className="input"
              value={newPassword}
              onChange={(e) => setNewPassword(e.target.value)}
              disabled={loading}
              placeholder="Tối thiểu 8 ký tự, gồm chữ hoa, thường, số, ký tự đặc biệt"
              style={{
                width: '100%',
                boxSizing: 'border-box',
                borderRadius: 'var(--radius-full)',
                border: '2px solid var(--color-fg)',
                padding: '10px 46px 10px 18px',
                fontWeight: 600,
                fontSize: 'var(--fs-sm)',
              }}
              required
            />
            <button
              type="button"
              onClick={() => setShowNew(!showNew)}
              aria-label={showNew ? 'Ẩn mật khẩu' : 'Hiện mật khẩu'}
              style={{
                position: 'absolute',
                right: 14,
                top: '50%',
                transform: 'translateY(-50%)',
                background: 'none',
                border: 'none',
                cursor: 'pointer',
                color: 'var(--color-muted-fg)',
                display: 'grid',
                placeItems: 'center',
                padding: 4,
              }}
            >
              {showNew ? <EyeOff size={18} /> : <Eye size={18} />}
            </button>
          </div>

          {/* Validation Chips */}
          {newPassword.length > 0 && (
            <div style={{ display: 'flex', gap: 6, flexWrap: 'wrap', marginTop: 4 }}>
              <span
                className={`badge ${hasMinLength ? 'badge--mint' : 'badge--muted'}`}
                style={{ fontSize: 11, padding: '2px 8px', border: hasMinLength ? '1px solid #059669' : '1px solid var(--color-border)' }}
              >
                {hasMinLength ? '✓' : '○'} ≥ 8 ký tự
              </span>
              <span
                className={`badge ${hasLetter ? 'badge--mint' : 'badge--muted'}`}
                style={{ fontSize: 11, padding: '2px 8px', border: hasLetter ? '1px solid #059669' : '1px solid var(--color-border)' }}
              >
                {hasLetter ? '✓' : '○'} Có chữ cái
              </span>
              <span
                className={`badge ${hasNumberOrSymbol ? 'badge--mint' : 'badge--muted'}`}
                style={{ fontSize: 11, padding: '2px 8px', border: hasNumberOrSymbol ? '1px solid #059669' : '1px solid var(--color-border)' }}
              >
                {hasNumberOrSymbol ? '✓' : '○'} Số hoặc ký tự đặc biệt
              </span>
            </div>
          )}
        </div>

        {/* 3. Xác nhận mật khẩu mới */}
        <div className="field" style={{ display: 'flex', flexDirection: 'column', gap: 6 }}>
          <label className="label" htmlFor="cfm-pass" style={{ fontSize: 'var(--fs-xs)', fontWeight: 800, letterSpacing: '0.04em' }}>
            XÁC NHẬN MẬT KHẨU MỚI <span style={{ color: 'var(--color-secondary)' }}>*</span>
          </label>
          <div style={{ position: 'relative', width: '100%' }}>
            <input
              id="cfm-pass"
              type={showConfirm ? 'text' : 'password'}
              className="input"
              value={confirmPassword}
              onChange={(e) => setConfirmPassword(e.target.value)}
              disabled={loading}
              placeholder="Nhập lại chính xác mật khẩu mới"
              style={{
                width: '100%',
                boxSizing: 'border-box',
                borderRadius: 'var(--radius-full)',
                border: '2px solid var(--color-fg)',
                padding: '10px 46px 10px 18px',
                fontWeight: 600,
                fontSize: 'var(--fs-sm)',
              }}
              required
            />
            <button
              type="button"
              onClick={() => setShowConfirm(!showConfirm)}
              aria-label={showConfirm ? 'Ẩn mật khẩu' : 'Hiện mật khẩu'}
              style={{
                position: 'absolute',
                right: 14,
                top: '50%',
                transform: 'translateY(-50%)',
                background: 'none',
                border: 'none',
                cursor: 'pointer',
                color: 'var(--color-muted-fg)',
                display: 'grid',
                placeItems: 'center',
                padding: 4,
              }}
            >
              {showConfirm ? <EyeOff size={18} /> : <Eye size={18} />}
            </button>
          </div>

          {confirmPassword.length > 0 && (
            <div style={{ marginTop: 2 }}>
              {isMatch ? (
                <span className="badge badge--mint" style={{ fontSize: 11, padding: '2px 8px', border: '1px solid #059669' }}>
                  ✓ Mật khẩu xác nhận trùng khớp
                </span>
              ) : (
                <span className="badge badge--pink" style={{ fontSize: 11, padding: '2px 8px', border: '1px solid #E11D48' }}>
                  ✕ Mật khẩu xác nhận chưa khớp
                </span>
              )}
            </div>
          )}
        </div>

        {/* Buttons */}
        <div style={{ display: 'flex', justifyContent: 'flex-end', gap: 12, marginTop: 12, paddingTop: 14, borderTop: '1.5px dashed var(--color-border)' }}>
          <button
            type="button"
            className="btn btn--secondary"
            onClick={handleResetAndClose}
            disabled={loading}
            style={{
              borderRadius: 'var(--radius-full)',
              border: '2px solid var(--color-fg)',
              boxShadow: '2px 2px 0 0 var(--color-fg)',
              fontWeight: 700,
              padding: '10px 22px',
            }}
          >
            Hủy bỏ
          </button>
          <button
            type="submit"
            className="btn btn--primary"
            disabled={loading}
            style={{
              borderRadius: 'var(--radius-full)',
              border: '2px solid var(--color-fg)',
              boxShadow: '4px 4px 0 0 var(--color-fg)',
              fontWeight: 800,
              padding: '10px 24px',
              background: 'var(--color-accent)',
              color: '#FFFFFF',
              display: 'inline-flex',
              alignItems: 'center',
              gap: 8,
            }}
          >
            {loading ? (
              <span>Đang lưu...</span>
            ) : (
              <>
                <Lock size={16} />
                <span>Cập nhật mật khẩu</span>
              </>
            )}
          </button>
        </div>
      </form>
    </GeometricModal>
  );
}
