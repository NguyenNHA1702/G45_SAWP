import { useState } from 'react';
import { ArrowRight, HeartHandshake, Sparkles } from 'lucide-react';
import toast from 'react-hot-toast';
import { GeometricModal } from '../common/GeometricModal';
import { guardianApi } from '../../services/implementations/guardianApi';
import type { SubmitChildLinkDto } from '../../types/guardian.types';

interface LinkChildModalProps {
  open: boolean;
  onClose: () => void;
  onSubmitted: () => void;
}

export const LinkChildModal: React.FC<LinkChildModalProps> = ({
  open,
  onClose,
  onSubmitted,
}) => {
  const [formData, setFormData] = useState<SubmitChildLinkDto>({
    studentCode: '',
    studentFullName: '',
    studentDateOfBirth: '2020-01-01',
    relationship: 'Bố',
    notes: '',
  });

  const [loading, setLoading] = useState(false);

  const handleSubmit = async (e: React.FormEvent) => {
    e.preventDefault();
    if (!formData.studentCode.trim() || !formData.studentFullName.trim()) {
      toast.error('Vui lòng điền đầy đủ Mã học sinh và Họ tên của con!');
      return;
    }

    setLoading(true);
    try {
      const res = await guardianApi.submitChildLink({
        ...formData,
        studentCode: formData.studentCode.trim().toUpperCase(),
        studentFullName: formData.studentFullName.trim(),
      });

      if (res.success && res.data) {
        if (res.data.isAutoVerified) {
          toast.success(
            `Đã tự động xác thực và liên kết thành công với con ${res.data.studentFullName}!`,
            { icon: '🎉' }
          );
        } else {
          toast.success(
            'Yêu cầu liên kết đã được gửi tới Ban Giáo vụ và đang chờ duyệt!',
            { icon: '⏳' }
          );
        }
        onSubmitted();
        onClose();
      } else {
        toast.error(res.message || 'Gửi yêu cầu liên kết thất bại.');
      }
    } catch (err: any) {
      toast.error(err.response?.data?.message || 'Lỗi khi gửi yêu cầu liên kết.');
    } finally {
      setLoading(false);
    }
  };

  return (
    <GeometricModal
      open={open}
      onClose={onClose}
      title="Liên kết hồ sơ học sinh"
      badgeText="Cổng Phụ Huynh"
      badgeColor="pink"
      icon={<HeartHandshake size={24} color="#1E293B" strokeWidth={2.5} />}
      iconBg="var(--color-secondary)"
      maxWidth="md"
    >
      <form onSubmit={handleSubmit} style={{ display: 'flex', flexDirection: 'column', gap: 16 }}>
        <div className="banner banner--info" style={{ fontSize: 'var(--fs-sm)' }}>
          <Sparkles size={18} color="var(--color-accent)" style={{ flexShrink: 0, marginTop: 2 }} />
          <div>
            Hệ thống sẽ <strong>tự động đối chiếu thông tin</strong> (Mã học sinh, Họ tên, Ngày sinh và
            Số điện thoại của bạn). Nếu trùng khớp, hồ sơ sẽ được liên kết ngay lập tức!
          </div>
        </div>

        <div className="field">
          <label className="label">Mã học sinh (*)</label>
          <input
            type="text"
            className="input"
            placeholder="VD: HS2026001"
            required
            value={formData.studentCode}
            onChange={(e) =>
              setFormData({ ...formData, studentCode: e.target.value.toUpperCase() })
            }
          />
        </div>

        <div className="field">
          <label className="label">Họ và tên học sinh (*)</label>
          <input
            type="text"
            className="input"
            placeholder="Nguyễn Văn An"
            required
            value={formData.studentFullName}
            onChange={(e) =>
              setFormData({ ...formData, studentFullName: e.target.value })
            }
          />
        </div>

        <div className="field">
          <label className="label">Ngày sinh của con (*)</label>
          <input
            type="date"
            className="input"
            required
            value={formData.studentDateOfBirth}
            onChange={(e) =>
              setFormData({ ...formData, studentDateOfBirth: e.target.value })
            }
          />
        </div>

        <div className="field">
          <label className="label">Mối quan hệ với học sinh</label>
          <select
            className="select"
            value={formData.relationship}
            onChange={(e) => setFormData({ ...formData, relationship: e.target.value })}
          >
            <option value="Bố">Bố (Cha)</option>
            <option value="Mẹ">Mẹ</option>
            <option value="Người giám hộ">Người giám hộ</option>
          </select>
        </div>

        <div className="field">
          <label className="label">Ghi chú bổ sung (Tùy chọn)</label>
          <textarea
            className="input"
            rows={2}
            placeholder="Thông tin liên hệ thêm hoặc yêu cầu đặc biệt..."
            value={formData.notes || ''}
            onChange={(e) => setFormData({ ...formData, notes: e.target.value })}
          />
        </div>

        <div style={{ display: 'flex', justifyContent: 'flex-end', gap: 10, marginTop: 10 }}>
          <button type="button" className="btn btn--secondary" onClick={onClose} disabled={loading}>
            Hủy
          </button>
          <button type="submit" className="btn btn--primary" disabled={loading}>
            {loading ? 'Đang gửi...' : 'Gửi yêu cầu liên kết'}
            <ArrowRight size={18} />
          </button>
        </div>
      </form>
    </GeometricModal>
  );
};
