import { useCallback, useEffect, useState } from 'react';
import { BellRing, CheckCircle2, Megaphone, Send, Users } from 'lucide-react';
import toast from 'react-hot-toast';
import { notificationApi } from '../../services/implementations/notificationApi';
import { classApi } from '../../services/implementations/classApi';
import { asApiError } from '../../utils/apiError';
import { formatDDMMYYYYHHMM } from '../../utils/date';
import type { BroadcastHistoryItem } from '../../types/notification.types';
import type { ClassSummaryDto } from '../../types/class.types';

const ROLE_OPTIONS = [
  { value: 'Parent', label: 'Phụ huynh' },
  { value: 'Teacher', label: 'Giáo viên chủ nhiệm' },
  { value: 'OfficeStaff', label: 'Nhân viên Giáo vụ' },
  { value: 'ServiceStaff', label: 'Nhân viên Bán trú' },
  { value: 'SchoolBoard', label: 'Ban Giám hiệu' },
];

export default function BroadcastNotificationsPage() {
  const [targetScope, setTargetScope] = useState<'All' | 'Role' | 'Grade' | 'Class'>('All');
  const [targetRole, setTargetRole] = useState('Parent');
  const [grade, setGrade] = useState(1);
  const [classId, setClassId] = useState<number | ''>('');
  const [title, setTitle] = useState('');
  const [content, setContent] = useState('');
  const [linkUrl, setLinkUrl] = useState('/dashboard');
  const [sending, setSending] = useState(false);

  const [classes, setClasses] = useState<ClassSummaryDto[]>([]);
  const [history, setHistory] = useState<BroadcastHistoryItem[]>([]);
  const [loadingHistory, setLoadingHistory] = useState(true);

  const loadData = useCallback(async () => {
    setLoadingHistory(true);
    try {
      const [clsRes, hist] = await Promise.all([
        classApi.getClasses(undefined, undefined, 'Active'),
        notificationApi.getBroadcastHistory(25),
      ]);
      if (clsRes.success && clsRes.data) {
        setClasses(clsRes.data);
        if (clsRes.data.length > 0 && classId === '') {
          setClassId(clsRes.data[0].id);
        }
      }
      setHistory(hist);
    } catch {
      toast.error('Không thể tải dữ liệu thông báo nhà trường.');
    } finally {
      setLoadingHistory(false);
    }
  }, [classId]);

  useEffect(() => {
    loadData();
    // eslint-disable-next-line react-hooks/exhaustive-deps
  }, []);

  const handleSubmit = async (e: React.FormEvent) => {
    e.preventDefault();
    const t = title.trim();
    const c = content.trim();
    if (t.length < 5 || t.length > 150) {
      toast.error('Tiêu đề thông báo phải từ 5 đến 150 ký tự.');
      return;
    }
    if (c.length < 5 || c.length > 450) {
      toast.error('Nội dung thông báo phải từ 5 đến 450 ký tự.');
      return;
    }
    if (targetScope === 'Class' && !classId) {
      toast.error('Vui lòng chọn lớp nhận thông báo.');
      return;
    }

    try {
      setSending(true);
      const { result, message } = await notificationApi.broadcast({
        targetScope,
        targetRole: targetScope === 'Role' ? targetRole : undefined,
        grade: targetScope === 'Grade' ? grade : undefined,
        classId: targetScope === 'Class' ? Number(classId) : undefined,
        title: t,
        content: c,
        linkUrl: linkUrl.trim() || '/dashboard',
      });
      toast.success(message || `Đã phát thông báo tới ${result.recipientCount} người nhận.`);
      setTitle('');
      setContent('');
      await loadData();
    } catch (err) {
      const apiErr = asApiError(err);
      toast.error(apiErr.errors?.[0] || apiErr.message || 'Gửi thông báo thất bại.');
    } finally {
      setSending(false);
    }
  };

  return (
    <div style={{ display: 'flex', flexDirection: 'column', gap: 20 }}>
      <div className="card" style={{ display: 'flex', justifyContent: 'space-between', alignItems: 'center', flexWrap: 'wrap', gap: 12 }}>
        <div>
          <span className="badge badge--violet" style={{ marginBottom: 8 }}>
            <Megaphone size={13} /> Thông báo nhà trường (UC-47 / UC-65)
          </span>
          <h1 style={{ margin: '4px 0' }}>Phát Thông báo Chung</h1>
          <p style={{ margin: 0, color: 'var(--color-muted-fg)', fontSize: 'var(--fs-sm)' }}>
            Gửi thông báo trực tiếp tới chuông thông báo của toàn trường, theo vai trò, khối hoặc lớp học.
          </p>
        </div>
      </div>

      <div className="responsive-split">
        <form onSubmit={handleSubmit} className="card" style={{ display: 'flex', flexDirection: 'column', gap: 14 }}>
          <h3 style={{ margin: 0, display: 'flex', alignItems: 'center', gap: 8 }}>
            <BellRing size={18} color="var(--color-accent)" /> Soạn thông báo mới
          </h3>

          <label style={{ fontSize: 'var(--fs-sm)', fontWeight: 700 }}>
            Đối tượng nhận
            <select
              className="input"
              style={{ width: '100%', marginTop: 4 }}
              value={targetScope}
              onChange={(e) => setTargetScope(e.target.value as 'All' | 'Role' | 'Grade' | 'Class')}
            >
              <option value="All">Toàn trường (Tất cả tài khoản đang hoạt động)</option>
              <option value="Role">Theo vai trò (Phụ huynh / GVCN / Nhân viên...)</option>
              <option value="Grade">Theo khối lớp (GVCN & Phụ huynh khối)</option>
              <option value="Class">Theo lớp học cụ thể (GVCN & Phụ huynh lớp)</option>
            </select>
          </label>

          {targetScope === 'Role' && (
            <label style={{ fontSize: 'var(--fs-sm)', fontWeight: 700 }}>
              Chọn vai trò
              <select className="input" style={{ width: '100%', marginTop: 4 }} value={targetRole} onChange={(e) => setTargetRole(e.target.value)}>
                {ROLE_OPTIONS.map((r) => (
                  <option key={r.value} value={r.value}>{r.label}</option>
                ))}
              </select>
            </label>
          )}

          {targetScope === 'Grade' && (
            <label style={{ fontSize: 'var(--fs-sm)', fontWeight: 700 }}>
              Chọn khối học
              <select className="input" style={{ width: '100%', marginTop: 4 }} value={grade} onChange={(e) => setGrade(Number(e.target.value))}>
                {[1, 2, 3, 4, 5].map((g) => (
                  <option key={g} value={g}>Khối {g}</option>
                ))}
              </select>
            </label>
          )}

          {targetScope === 'Class' && (
            <label style={{ fontSize: 'var(--fs-sm)', fontWeight: 700 }}>
              Chọn lớp học
              <select
                className="input"
                style={{ width: '100%', marginTop: 4 }}
                value={classId}
                onChange={(e) => setClassId(e.target.value ? Number(e.target.value) : '')}
              >
                {classes.map((c) => (
                  <option key={c.id} value={c.id}>
                    Lớp {c.className} (Khối {c.grade})
                  </option>
                ))}
              </select>
            </label>
          )}

          <label style={{ fontSize: 'var(--fs-sm)', fontWeight: 700 }}>
            Tiêu đề thông báo *
            <input
              type="text"
              className="input"
              maxLength={150}
              style={{ width: '100%', marginTop: 4 }}
              placeholder="Ví dụ: Lịch nghỉ Lễ & Họp phụ huynh học kỳ I"
              value={title}
              onChange={(e) => setTitle(e.target.value)}
              required
            />
          </label>

          <label style={{ fontSize: 'var(--fs-sm)', fontWeight: 700 }}>
            Nội dung thông báo (5–450 ký tự) *
            <textarea
              className="input"
              rows={4}
              maxLength={450}
              style={{ width: '100%', marginTop: 4 }}
              placeholder="Nhập nội dung ngắn gọn, rõ ràng gửi tới phụ huynh/cán bộ..."
              value={content}
              onChange={(e) => setContent(e.target.value)}
              required
            />
            <div style={{ fontSize: 'var(--fs-xs)', color: 'var(--color-muted-fg)', marginTop: 4 }}>
              {content.length}/450 ký tự
            </div>
          </label>

          <label style={{ fontSize: 'var(--fs-sm)', fontWeight: 700 }}>
            Đường dẫn điều hướng khi bấm vào thông báo
            <input
              type="text"
              className="input"
              style={{ width: '100%', marginTop: 4 }}
              value={linkUrl}
              onChange={(e) => setLinkUrl(e.target.value)}
            />
          </label>

          <button type="submit" className="btn btn--primary" disabled={sending}>
            <Send size={16} /> {sending ? 'Đang gửi thông báo...' : 'Phát thông báo ngay'}
          </button>
        </form>

        <div className="card" style={{ display: 'flex', flexDirection: 'column', gap: 12 }}>
          <h3 style={{ margin: 0, display: 'flex', alignItems: 'center', gap: 8 }}>
            <Users size={18} color="var(--color-accent)" /> Lịch sử phát thông báo gần đây
          </h3>

          {loadingHistory ? (
            <div style={{ padding: 24, textAlign: 'center' }}>Đang tải lịch sử...</div>
          ) : history.length === 0 ? (
            <div style={{ padding: 24, textAlign: 'center', color: 'var(--color-muted-fg)' }}>
              Chưa có thông báo chung nào được phát từ cổng nhà trường.
            </div>
          ) : (
            <div style={{ display: 'flex', flexDirection: 'column', gap: 10 }}>
              {history.map((h) => (
                <div
                  key={h.referenceKey}
                  style={{
                    padding: 12,
                    borderRadius: 12,
                    border: '1.5px solid var(--color-border)',
                    background: '#F8FAFC',
                  }}
                >
                  <div style={{ display: 'flex', justifyContent: 'space-between', alignItems: 'center', gap: 8, marginBottom: 4 }}>
                    <strong style={{ fontSize: 'var(--fs-sm)' }}>{h.title}</strong>
                    <span className="badge badge--mint" style={{ whiteSpace: 'nowrap' }}>
                      <CheckCircle2 size={12} /> Đã đọc: {h.readCount}/{h.recipientCount}
                    </span>
                  </div>
                  <p style={{ margin: '0 0 6px', fontSize: 'var(--fs-xs)', color: '#475569' }}>{h.content}</p>
                  <div style={{ fontSize: 11, color: 'var(--color-muted-fg)' }}>
                    Thời gian phát: {formatDDMMYYYYHHMM(h.sentAt)}
                  </div>
                </div>
              ))}
            </div>
          )}
        </div>
      </div>
    </div>
  );
}
