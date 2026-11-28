import { useEffect, useState } from 'react';
import toast from 'react-hot-toast';
import { Ban, MessageSquareText, Save } from 'lucide-react';
import { GeometricModal } from '../common/GeometricModal';
import { attendanceApi } from '../../services/implementations/attendanceApi';
import { asApiError } from '../../utils/apiError';
import { formatViDateTime } from '../../utils/date';
import { SYSTEM_MESSAGES } from '../../constants/messages';
import type { AbsenceContactNote, ContactChannel, ContactOutcome } from '../../types/attendance.types';

const CONTACT_CHANNEL_LABELS: Record<ContactChannel, string> = {
  Phone: 'Điện thoại',
  Zalo: 'Zalo',
  InPerson: 'Gặp trực tiếp',
  Other: 'Khác',
};

const CONTACT_OUTCOME_LABELS: Record<ContactOutcome, string> = {
  Reached: 'Liên hệ được',
  NotReached: 'Không liên hệ được',
};

/** Current local time as the value of an <input type="datetime-local">. */
const nowLocalInput = (): string => {
  const d = new Date();
  d.setSeconds(0, 0);
  const offset = d.getTimezoneOffset() * 60000;
  return new Date(d.getTime() - offset).toISOString().slice(0, 16);
};

interface Props {
  open: boolean;
  recordId: number | null;
  studentName: string;
  /** e.g. "Buổi sáng · 28-11-2026" */
  absenceLabel: string;

  /** The record is an unexcused absence and the user may record a note (homeroom Teacher or Office Staff). */
  canRecord: boolean;
  onClose: () => void;
  /** Called after a note was saved or marked superseded. */
  onChanged: () => void;
}

/**
 * OVR-10 (UC-23 A1, BR-62): the contact notes of one unexcused absence. A saved note is never edited;
 * a wrong note is marked superseded with a reason and stays visible.
 */
export function ContactNotesModal({ open, recordId, studentName, absenceLabel, canRecord, onClose, onChanged }: Props) {
  const [notes, setNotes] = useState<AbsenceContactNote[]>([]);
  const [loading, setLoading] = useState(false);
  const [contactedAt, setContactedAt] = useState(nowLocalInput);
  const [channel, setChannel] = useState<ContactChannel | ''>('');
  const [outcome, setOutcome] = useState<ContactOutcome | ''>('');
  const [noteText, setNoteText] = useState('');
  const [saving, setSaving] = useState(false);
  const [supersedingId, setSupersedingId] = useState<number | null>(null);
  const [supersedeReason, setSupersedeReason] = useState('');
  const [superseding, setSuperseding] = useState(false);

  const load = async (id: number) => {
    setLoading(true);
    try {
      setNotes(await attendanceApi.getContactNotes(id));
    } catch (e) {
      toast.error(asApiError(e).message);
      setNotes([]);
    } finally {
      setLoading(false);
    }
  };

  useEffect(() => {
    if (!open || recordId === null) return;
    setContactedAt(nowLocalInput());
    setChannel('');
    setOutcome('');
    setNoteText('');
    setSupersedingId(null);
    setSupersedeReason('');
    void load(recordId);
  }, [open, recordId]);

  const showErrors = (e: unknown) => {
    const err = asApiError(e);
    toast.error(err.message);
    err.errors?.forEach((m) => toast.error(m));
  };

  const handleSave = async () => {
    if (recordId === null) return;
    if (!contactedAt || !channel || !outcome || !noteText.trim()) {
      toast.error(SYSTEM_MESSAGES.MSG01.content);
      return;
    }
    setSaving(true);
    try {
      const res = await attendanceApi.addContactNote(recordId, {
        contactedAt: new Date(contactedAt).toISOString(),
        channel,
        outcome,
        note: noteText.trim(),
      });
      toast.success(res.message || SYSTEM_MESSAGES.MSG55.content);
      setChannel('');
      setOutcome('');
      setNoteText('');
      setContactedAt(nowLocalInput());
      await load(recordId);
      onChanged();
    } catch (e) {
      showErrors(e);
    } finally {
      setSaving(false);
    }
  };

  const handleSupersede = async (noteId: number) => {
    if (recordId === null) return;
    const reason = supersedeReason.trim();
    if (reason.length < 5 || reason.length > 500) {
      toast.error(SYSTEM_MESSAGES.MSG01.content);
      return;
    }
    setSuperseding(true);
    try {
      const res = await attendanceApi.supersedeContactNote(noteId, reason);
      toast.success(res.message || SYSTEM_MESSAGES.MSG319.content);
      setSupersedingId(null);
      setSupersedeReason('');
      await load(recordId);
      onChanged();
    } catch (e) {
      showErrors(e);
    } finally {
      setSuperseding(false);
    }
  };

  return (
    <GeometricModal
      open={open}
      onClose={() => !saving && !superseding && onClose()}
      title="Ghi chú liên hệ gia đình"
      subtitle={`${studentName} · Vắng không phép · ${absenceLabel}`}
      badgeText="Vắng không phép"
      badgeColor="pink"
      icon={<MessageSquareText size={20} color="var(--color-fg)" />}
      iconBg="var(--color-tertiary)"
      maxWidth="lg"
    >
      <div style={{ display: 'flex', flexDirection: 'column', gap: 16 }} data-testid="contact-notes-modal">
        <section aria-label="Danh sách ghi chú liên hệ">
          {loading ? (
            <div aria-busy="true" className="skeleton" />
          ) : notes.length === 0 ? (
            <p style={{ margin: 0, color: 'var(--color-muted-fg)' }} data-testid="contact-notes-empty">
              Chưa có ghi chú liên hệ nào.
            </p>
          ) : (
            <ul style={{ listStyle: 'none', margin: 0, padding: 0, display: 'grid', gap: 10 }}>
              {notes.map((n) => (
                <li
                  key={n.id}
                  data-testid="contact-note"
                  data-note-id={n.id}
                  data-superseded={n.isSuperseded ? 'true' : 'false'}
                  style={{
                    border: '1.5px solid var(--color-fg)',
                    borderRadius: 'var(--radius-md)',
                    padding: '10px 14px',
                    background: n.isSuperseded ? '#F1F5F9' : 'var(--color-card)',
                  }}
                >
                  <div style={{ display: 'flex', flexWrap: 'wrap', gap: 8, alignItems: 'center', marginBottom: 6 }}>
                    <strong>{formatViDateTime(n.contactedAt)}</strong>
                    <span className="badge">{CONTACT_CHANNEL_LABELS[n.channel]}</span>
                    <span className={`badge ${n.outcome === 'Reached' ? 'badge--mint' : 'badge--pink'}`}>
                      {CONTACT_OUTCOME_LABELS[n.outcome]}
                    </span>
                    {n.isSuperseded && (
                      <span className="badge" style={{ border: '1.5px solid var(--color-fg)', fontWeight: 700 }}>
                        Không còn hiệu lực
                      </span>
                    )}
                  </div>
                  <p
                    style={{
                      margin: '0 0 6px',
                      whiteSpace: 'pre-wrap',
                      textDecoration: n.isSuperseded ? 'line-through' : 'none',
                    }}
                  >
                    {n.note}
                  </p>
                  <p style={{ margin: 0, fontSize: 'var(--fs-xs)', color: 'var(--color-muted-fg)' }}>
                    Ghi bởi {n.createdByName ?? '—'} lúc {formatViDateTime(n.createdAt)}
                  </p>
                  {n.isSuperseded && n.supersededAt && (
                    <p style={{ margin: '4px 0 0', fontSize: 'var(--fs-xs)' }} data-testid="contact-note-superseded-info">
                      Đánh dấu không còn hiệu lực bởi {n.supersededByName ?? '—'} lúc {formatViDateTime(n.supersededAt)}. Lý do:{' '}
                      {n.supersedeReason}
                    </p>
                  )}
                  {n.canSupersede && supersedingId !== n.id && (
                    <button
                      type="button"
                      className="btn btn--secondary btn--sm"
                      style={{ marginTop: 8, display: 'inline-flex', alignItems: 'center', gap: 6 }}
                      onClick={() => {
                        setSupersedingId(n.id);
                        setSupersedeReason('');
                      }}
                    >
                      <Ban size={13} aria-hidden="true" /> Đánh dấu không còn hiệu lực
                    </button>
                  )}
                  {supersedingId === n.id && (
                    <div style={{ marginTop: 8, display: 'grid', gap: 8 }}>
                      <label className="label" htmlFor={`supersede-reason-${n.id}`}>
                        Lý do đánh dấu không còn hiệu lực * (5–500 ký tự)
                      </label>
                      <textarea
                        id={`supersede-reason-${n.id}`}
                        className="input"
                        rows={2}
                        maxLength={500}
                        value={supersedeReason}
                        onChange={(e) => setSupersedeReason(e.target.value)}
                      />
                      <div style={{ display: 'flex', gap: 8, justifyContent: 'flex-end' }}>
                        <button
                          type="button"
                          className="btn btn--secondary btn--sm"
                          onClick={() => setSupersedingId(null)}
                          disabled={superseding}
                        >
                          Hủy
                        </button>
                        <button
                          type="button"
                          className="btn btn--primary btn--sm"
                          onClick={() => handleSupersede(n.id)}
                          disabled={superseding}
                        >
                          Xác nhận
                        </button>
                      </div>
                    </div>
                  )}
                </li>
              ))}
            </ul>
          )}
        </section>

        {canRecord && (
          <section
            aria-label="Thêm ghi chú liên hệ"
            style={{ borderTop: '2px dashed var(--color-border)', paddingTop: 14, display: 'grid', gap: 12 }}
          >
            <div style={{ display: 'grid', gap: 12, gridTemplateColumns: 'repeat(auto-fit, minmax(180px, 1fr))' }}>
              <div className="field">
                <label className="label" htmlFor="contact-note-at">
                  Thời điểm liên hệ *
                </label>
                <input
                  id="contact-note-at"
                  type="datetime-local"
                  className="input"
                  value={contactedAt}
                  max={nowLocalInput()}
                  onChange={(e) => setContactedAt(e.target.value)}
                />
              </div>
              <div className="field">
                <label className="label" htmlFor="contact-note-channel">
                  Hình thức liên hệ *
                </label>
                <select
                  id="contact-note-channel"
                  className="select"
                  value={channel}
                  onChange={(e) => setChannel(e.target.value as ContactChannel | '')}
                >
                  <option value="">-- Chọn --</option>
                  {(Object.keys(CONTACT_CHANNEL_LABELS) as ContactChannel[]).map((c) => (
                    <option key={c} value={c}>
                      {CONTACT_CHANNEL_LABELS[c]}
                    </option>
                  ))}
                </select>
              </div>
              <div className="field">
                <label className="label" htmlFor="contact-note-outcome">
                  Kết quả *
                </label>
                <select
                  id="contact-note-outcome"
                  className="select"
                  value={outcome}
                  onChange={(e) => setOutcome(e.target.value as ContactOutcome | '')}
                >
                  <option value="">-- Chọn --</option>
                  {(Object.keys(CONTACT_OUTCOME_LABELS) as ContactOutcome[]).map((o) => (
                    <option key={o} value={o}>
                      {CONTACT_OUTCOME_LABELS[o]}
                    </option>
                  ))}
                </select>
              </div>
            </div>
            <div className="field">
              <label className="label" htmlFor="contact-note-text">
                Nội dung ghi chú * (tối đa 500 ký tự)
              </label>
              <textarea
                id="contact-note-text"
                className="input"
                rows={3}
                maxLength={500}
                value={noteText}
                onChange={(e) => setNoteText(e.target.value)}
              />
            </div>
            <div style={{ display: 'flex', justifyContent: 'flex-end', gap: 10 }}>
              <button type="button" className="btn btn--secondary" onClick={onClose} disabled={saving}>
                Đóng
              </button>
              <button
                type="button"
                className="btn btn--primary"
                onClick={handleSave}
                disabled={saving}
                style={{ display: 'inline-flex', alignItems: 'center', gap: 6 }}
              >
                <Save size={15} aria-hidden="true" /> {saving ? 'Đang lưu...' : 'Lưu ghi chú'}
              </button>
            </div>
          </section>
        )}
      </div>
    </GeometricModal>
  );
}
