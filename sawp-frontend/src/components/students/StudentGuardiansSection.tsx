import React, { useState } from 'react';
import { Link2, Plus, Search, Star, Unlink, Users } from 'lucide-react';
import toast from 'react-hot-toast';
import { useNavigate } from 'react-router';
import { studentApi } from '../../services/implementations/studentApi';
import { asApiError } from '../../utils/apiError';
import { SYSTEM_MESSAGES } from '../../constants/messages';
import type { GuardianRecordDto, StudentGuardianItemDto, StudentProfileDto } from '../../types/student.types';

const RELATIONSHIPS: { value: number; label: string }[] = [
  { value: 1, label: 'Bố (Cha)' },
  { value: 2, label: 'Mẹ' },
  { value: 3, label: 'Người giám hộ' },
  { value: 4, label: 'Người thân khác' },
];

interface Props {
  student: StudentProfileDto;
  /** Office Staff / Admin may add, change and remove links (UC-10); others only see the list. */
  canManage: boolean;
  onChanged: (student: StudentProfileDto) => void;
}

/**
 * UC-10 Link or Unlink Guardian to Student. Guardian records need no user account (BR-64); a new record is matched to
 * an existing one only by identity card number or the record's email, never by phone (BR-66).
 */
export const StudentGuardiansSection: React.FC<Props> = ({ student, canManage, onChanged }) => {
  const guardians = student.guardians ?? [];
  const navigate = useNavigate();
  const [busy, setBusy] = useState(false);
  const [adding, setAdding] = useState(false);
  const [mode, setMode] = useState<'search' | 'new'>('search');
  const [search, setSearch] = useState('');
  const [results, setResults] = useState<GuardianRecordDto[]>([]);
  const [selected, setSelected] = useState<GuardianRecordDto | null>(null);
  const [newGuardian, setNewGuardian] = useState({ fullName: '', phoneNumber: '', email: '', identityCardNumber: '' });
  const [relationship, setRelationship] = useState(3);
  const [isPrimary, setIsPrimary] = useState(false);

  const run = async (action: () => Promise<{ data?: StudentProfileDto | null; message?: string }>, done?: () => void) => {
    setBusy(true);
    try {
      const res = await action();
      if (res.data) onChanged(res.data);
      toast.success(res.message || SYSTEM_MESSAGES.MSG08.content);
      done?.();
    } catch (err) {
      const apiErr = asApiError(err);
      toast.error(apiErr.errors?.[0] || apiErr.message);
    } finally {
      setBusy(false);
    }
  };

  const resetForm = () => {
    setAdding(false);
    setSearch('');
    setResults([]);
    setSelected(null);
    setNewGuardian({ fullName: '', phoneNumber: '', email: '', identityCardNumber: '' });
    setRelationship(3);
    setIsPrimary(false);
  };

  const handleSearch = async () => {
    if (search.trim().length < 2) {
      toast.error('Nhập ít nhất 2 ký tự để tìm.');
      return;
    }
    try {
      const res = await studentApi.searchGuardianRecords(search.trim());
      setResults(res.data ?? []);
      if ((res.data ?? []).length === 0) toast('Không tìm thấy hồ sơ phù hợp.');
    } catch (err) {
      toast.error(asApiError(err).message);
    }
  };

  const handleLink = () => {
    if (mode === 'search' && !selected) {
      toast.error('Vui lòng chọn một hồ sơ người giám hộ.');
      return;
    }
    if (mode === 'new' && !newGuardian.fullName.trim()) {
      toast.error('Vui lòng nhập họ tên người giám hộ.');
      return;
    }
    void run(
      () =>
        studentApi.linkGuardian(student.id, {
          guardianId: mode === 'search' ? selected!.id : undefined,
          newGuardian:
            mode === 'new'
              ? {
                  fullName: newGuardian.fullName.trim(),
                  phoneNumber: newGuardian.phoneNumber.trim() || undefined,
                  email: newGuardian.email.trim() || undefined,
                  identityCardNumber: newGuardian.identityCardNumber.trim() || undefined,
                  relationship,
                }
              : undefined,
          relationship,
          isPrimaryContact: isPrimary,
        }),
      resetForm,
    );
  };

  const handleMakePrimary = (g: StudentGuardianItemDto) =>
    run(() => studentApi.updateGuardianLink(student.id, g.guardianId, { relationship: g.relationship, isPrimaryContact: true }));

  const handleChangeRelationship = (g: StudentGuardianItemDto, value: number) =>
    run(() => studentApi.updateGuardianLink(student.id, g.guardianId, { relationship: value, isPrimaryContact: g.isPrimaryContact }));

  const handleUnlink = (g: StudentGuardianItemDto) => {
    if (!window.confirm(`${SYSTEM_MESSAGES.MSG45.content}\n(Gỡ liên kết ${g.fullName} khỏi học sinh ${student.fullName})`)) return;
    void run(() => studentApi.unlinkGuardian(student.id, g.guardianId));
  };

  return (
    <div className="card" style={{ padding: 12 }} aria-label="Người giám hộ">
      <div style={{ display: 'flex', justifyContent: 'space-between', alignItems: 'center', marginBottom: 8 }}>
        <div style={{ display: 'flex', alignItems: 'center', gap: 8, color: 'var(--color-muted-fg)', fontSize: 'var(--fs-xs)', textTransform: 'uppercase', fontWeight: 700 }}>
          <Users size={14} /> Người giám hộ ({guardians.length})
        </div>
        {canManage && !adding && (
          <button id="btn-add-guardian" type="button" className="btn btn--secondary btn--sm" onClick={() => setAdding(true)} disabled={busy}>
            <Plus size={14} /> Thêm người giám hộ
          </button>
        )}
      </div>

      {guardians.length === 0 && <div style={{ color: 'var(--color-muted-fg)', fontSize: 'var(--fs-sm)' }}>Chưa có người giám hộ.</div>}

      <ul style={{ listStyle: 'none', margin: 0, padding: 0, display: 'flex', flexDirection: 'column', gap: 8 }}>
        {guardians.map((g) => (
          <li key={g.guardianId} className="guardian-item" style={{ border: '1px solid var(--color-border)', borderRadius: 8, padding: 8 }}>
            <div style={{ display: 'flex', justifyContent: 'space-between', gap: 8, flexWrap: 'wrap' }}>
              <div>
                <strong>{g.fullName}</strong>{' '}
                {g.isPrimaryContact && (
                  <span className="badge badge--mint">
                    <Star size={12} /> Liên hệ chính
                  </span>
                )}{' '}
                <span className="badge badge--violet">{g.userId ? 'Có tài khoản' : 'Chưa có tài khoản'}</span>
                <div style={{ fontSize: 'var(--fs-xs)', color: 'var(--color-muted-fg)' }}>
                  {[g.phoneNumber, g.email || 'Chưa có email', g.identityCardNumber && `CCCD ${g.identityCardNumber}`].filter(Boolean).join(' • ')}
                </div>
              </div>
              {canManage ? (
                <div style={{ display: 'flex', gap: 6, alignItems: 'center', flexWrap: 'wrap' }}>
                  <select
                    className="select"
                    aria-label={`Quan hệ của ${g.fullName}`}
                    value={g.relationship}
                    disabled={busy}
                    onChange={(e) => handleChangeRelationship(g, Number(e.target.value))}
                  >
                    {RELATIONSHIPS.map((r) => (
                      <option key={r.value} value={r.value}>
                        {r.label}
                      </option>
                    ))}
                  </select>
                  {!g.userId && g.email && (
                    <button
                      type="button"
                      className="btn btn--secondary btn--sm"
                      onClick={() => navigate(`/office/account-requests?guardianIds=${g.guardianId}`)}
                    >
                      Yêu cầu cấp tài khoản
                    </button>
                  )}
                  {!g.isPrimaryContact && (
                    <button type="button" className="btn btn--secondary btn--sm" disabled={busy} onClick={() => handleMakePrimary(g)}>
                      <Star size={14} /> Đặt làm liên hệ chính
                    </button>
                  )}
                  <button
                    type="button"
                    className="btn btn--secondary btn--sm"
                    disabled={busy}
                    onClick={() => handleUnlink(g)}
                    aria-label={`Gỡ liên kết ${g.fullName}`}
                  >
                    <Unlink size={14} /> Gỡ liên kết
                  </button>
                </div>
              ) : (
                <span style={{ fontSize: 'var(--fs-sm)' }}>{g.relationshipName}</span>
              )}
            </div>
          </li>
        ))}
      </ul>

      {canManage && adding && (
        <div style={{ marginTop: 12, borderTop: '2px dashed var(--color-border)', paddingTop: 12, display: 'flex', flexDirection: 'column', gap: 10 }}>
          <div style={{ display: 'flex', gap: 8 }}>
            <button type="button" className={`btn btn--sm ${mode === 'search' ? 'btn--primary' : 'btn--secondary'}`} onClick={() => setMode('search')}>
              <Search size={14} /> Chọn hồ sơ có sẵn
            </button>
            <button type="button" className={`btn btn--sm ${mode === 'new' ? 'btn--primary' : 'btn--secondary'}`} onClick={() => setMode('new')}>
              <Plus size={14} /> Tạo hồ sơ mới
            </button>
          </div>

          {mode === 'search' ? (
            <>
              <div style={{ display: 'flex', gap: 8 }}>
                <input
                  id="guardian-search"
                  className="input"
                  placeholder="Họ tên, email, CCCD hoặc SĐT"
                  value={search}
                  onChange={(e) => setSearch(e.target.value)}
                  onKeyDown={(e) => {
                    if (e.key === 'Enter') {
                      e.preventDefault();
                      void handleSearch();
                    }
                  }}
                />
                <button type="button" className="btn btn--secondary btn--sm" onClick={handleSearch}>
                  Tìm
                </button>
              </div>
              {results.map((r) => (
                <label key={r.id} style={{ display: 'flex', gap: 8, alignItems: 'center', fontSize: 'var(--fs-sm)', cursor: 'pointer' }}>
                  <input type="radio" name="guardian-pick" checked={selected?.id === r.id} onChange={() => setSelected(r)} />
                  <span>
                    <strong>{r.fullName}</strong> — {[r.email, r.identityCardNumber && `CCCD ${r.identityCardNumber}`, r.phoneNumber].filter(Boolean).join(' • ')}{' '}
                    ({r.linkedStudentCount} học sinh{r.hasAccount ? ', có tài khoản' : ''})
                  </span>
                </label>
              ))}
            </>
          ) : (
            <div style={{ display: 'grid', gridTemplateColumns: '1fr 1fr', gap: 8 }}>
              <input id="new-guardian-name" className="input" placeholder="Họ tên *" value={newGuardian.fullName} onChange={(e) => setNewGuardian({ ...newGuardian, fullName: e.target.value })} />
              <input id="new-guardian-phone" className="input" placeholder="Số điện thoại" value={newGuardian.phoneNumber} onChange={(e) => setNewGuardian({ ...newGuardian, phoneNumber: e.target.value })} />
              <input id="new-guardian-email" className="input" type="email" placeholder="Email (cần để cấp tài khoản)" value={newGuardian.email} onChange={(e) => setNewGuardian({ ...newGuardian, email: e.target.value })} />
              <input id="new-guardian-idcard" className="input" placeholder="Số CCCD" value={newGuardian.identityCardNumber} onChange={(e) => setNewGuardian({ ...newGuardian, identityCardNumber: e.target.value })} />
            </div>
          )}

          <div style={{ display: 'flex', gap: 12, alignItems: 'center', flexWrap: 'wrap' }}>
            <select className="select" aria-label="Quan hệ với học sinh" value={relationship} onChange={(e) => setRelationship(Number(e.target.value))}>
              {RELATIONSHIPS.map((r) => (
                <option key={r.value} value={r.value}>
                  {r.label}
                </option>
              ))}
            </select>
            <label style={{ display: 'flex', gap: 6, alignItems: 'center', fontSize: 'var(--fs-sm)' }}>
              <input type="checkbox" checked={isPrimary} onChange={(e) => setIsPrimary(e.target.checked)} /> Liên hệ chính
            </label>
          </div>
          <div style={{ display: 'flex', justifyContent: 'flex-end', gap: 8 }}>
            <button type="button" className="btn btn--secondary btn--sm" onClick={resetForm} disabled={busy}>
              Hủy
            </button>
            <button id="btn-link-guardian" type="button" className="btn btn--primary btn--sm" onClick={handleLink} disabled={busy}>
              <Link2 size={14} /> Liên kết
            </button>
          </div>
        </div>
      )}
    </div>
  );
};
