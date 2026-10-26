import { useState } from 'react';
import type { FormEvent } from 'react';
import toast from 'react-hot-toast';
import { KeyRound, Lock } from 'lucide-react';
import { authApi } from '../../services/implementations/authApi';
import { asApiError } from '../../utils/apiError';
import { SYSTEM_MESSAGES } from '../../constants/messages';

interface Props {
  open: boolean;
  onClose: () => void;
}

export function ChangePasswordModal({ open, onClose }: Props) {
  const [currentPassword, setCurrentPassword] = useState('');
  const [newPassword, setNewPassword] = useState('');
  const [confirmPassword, setConfirmPassword] = useState('');
  const [loading, setLoading] = useState(false);

  if (!open) return null;

  const handleSubmit = async (e: FormEvent) => {
    e.preventDefault();

    if (!currentPassword || !newPassword || !confirmPassword) {
      toast.error(SYSTEM_MESSAGES.MSG01.content);
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
      onClose();
    } catch (err) {
      const apiErr = asApiError(err);
      toast.error(apiErr.message);
    } finally {
      setLoading(false);
    }
  };

  return (
    <dialog open className="dialog">
      <h2 className="dialog__title" style={{ display: 'flex', alignItems: 'center', gap: 8 }}>
        <KeyRound size={22} color="var(--color-accent)" />
        Đổi mật khẩu
      </h2>
      <p style={{ fontSize: 'var(--fs-sm)', color: 'var(--color-muted-fg)' }}>
        Mật khẩu mới phải có tối thiểu 8 ký tự, bao gồm chữ hoa, chữ thường, số và ký tự đặc biệt.
      </p>

      <form onSubmit={handleSubmit} className="dialog__body" style={{ display: 'flex', flexDirection: 'column', gap: 14 }}>
        <div className="field">
          <label className="label" htmlFor="cur-pass">Mật khẩu hiện tại</label>
          <input
            id="cur-pass"
            type="password"
            className="input"
            value={currentPassword}
            onChange={(e) => setCurrentPassword(e.target.value)}
            disabled={loading}
            required
          />
        </div>

        <div className="field">
          <label className="label" htmlFor="new-pass">Mật khẩu mới</label>
          <input
            id="new-pass"
            type="password"
            className="input"
            value={newPassword}
            onChange={(e) => setNewPassword(e.target.value)}
            disabled={loading}
            required
          />
        </div>

        <div className="field">
          <label className="label" htmlFor="cfm-pass">Xác nhận mật khẩu mới</label>
          <input
            id="cfm-pass"
            type="password"
            className="input"
            value={confirmPassword}
            onChange={(e) => setConfirmPassword(e.target.value)}
            disabled={loading}
            required
          />
        </div>

        <div className="dialog__actions" style={{ marginTop: 12 }}>
          <button type="button" className="btn btn--secondary" onClick={onClose} disabled={loading}>
            Hủy
          </button>
          <button type="submit" className="btn btn--primary" disabled={loading}>
            {loading ? 'Đang lưu…' : 'Cập nhật'}
            <span className="btn__icon"><Lock size={14} /></span>
          </button>
        </div>
      </form>
    </dialog>
  );
}
