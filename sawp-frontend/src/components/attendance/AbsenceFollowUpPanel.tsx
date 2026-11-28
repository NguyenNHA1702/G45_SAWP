import { useCallback, useEffect, useMemo, useState } from 'react';
import { MessageSquareText, PhoneOff } from 'lucide-react';
import { attendanceApi } from '../../services/implementations/attendanceApi';
import { asApiError } from '../../utils/apiError';
import type { AbsenceWithoutContactNote, TeacherClassOption } from '../../types/attendance.types';
import { ContactNotesModal } from './ContactNotesModal';
import { DateInput } from '../common/DateInput';

const formatDashDate = (iso: string): string => {
  const [y, m, d] = iso.slice(0, 10).split('-');
  return d && m && y ? `${d}-${m}-${y}` : iso;
};

interface Props {
  classes: TeacherClassOption[];
  defaultFrom: string;
  defaultTo: string;
}

/**
 * OVR-10 (UC-23 A2): Office Staff list the unexcused absences of a date range that have no contact note
 * in effect (superseded notes do not count), by class, and may record a note themselves.
 */
export function AbsenceFollowUpPanel({ classes, defaultFrom, defaultTo }: Props) {
  const [from, setFrom] = useState(defaultFrom);
  const [to, setTo] = useState(defaultTo);
  const [classFilter, setClassFilter] = useState<number | ''>('');
  const [rows, setRows] = useState<AbsenceWithoutContactNote[]>([]);
  const [loading, setLoading] = useState(false);
  const [error, setError] = useState<string | null>(null);
  const [selected, setSelected] = useState<AbsenceWithoutContactNote | null>(null);

  const load = useCallback(async () => {
    if (!from || !to) return;
    if (from > to) {
      setRows([]);
      setError('Từ ngày phải trước hoặc bằng Đến ngày.');
      return;
    }
    setLoading(true);
    setError(null);
    try {
      setRows(await attendanceApi.getAbsencesWithoutContactNote(from, to, classFilter === '' ? null : classFilter));
    } catch (e) {
      setRows([]);
      setError(asApiError(e).message);
    } finally {
      setLoading(false);
    }
  }, [from, to, classFilter]);

  useEffect(() => {
    void load();
  }, [load]);

  const groups = useMemo(() => {
    const map = new Map<number, { className: string; teacher: string | null; items: AbsenceWithoutContactNote[] }>();
    for (const r of rows) {
      const g = map.get(r.classId) ?? { className: r.className, teacher: r.homeroomTeacherName, items: [] };
      g.items.push(r);
      map.set(r.classId, g);
    }
    return [...map.entries()];
  }, [rows]);

  return (
    <section aria-label="Vắng không phép chưa có ghi chú liên hệ" data-testid="absence-follow-up">
      <div
        className="card card--toolbar pop-in"
        style={{
          display: 'flex',
          flexWrap: 'wrap',
          alignItems: 'flex-end',
          gap: 16,
          padding: '18px 24px',
          background: '#FFFDF5',
          border: '2px solid var(--color-fg)',
          boxShadow: '4px 4px 0 0 var(--color-fg)',
          borderRadius: 'var(--radius-lg)',
          marginBottom: 20,
        }}
      >
        <div className="field" style={{ minWidth: 200, flex: 1 }}>
          <label className="label" htmlFor="follow-up-class">
            LỚP HỌC
          </label>
          <select
            id="follow-up-class"
            className="select"
            value={classFilter}
            onChange={(e) => setClassFilter(e.target.value === '' ? '' : Number(e.target.value))}
          >
            <option value="">Tất cả lớp</option>
            {classes.map((c) => (
              <option key={c.classId} value={c.classId}>
                Lớp {c.className}
              </option>
            ))}
          </select>
        </div>
        <div className="field" style={{ minWidth: 160 }}>
          <label className="label" htmlFor="follow-up-from">
            TỪ NGÀY
          </label>
          <DateInput id="follow-up-from" value={from} max={to || defaultTo} onChange={setFrom} ariaLabel="Từ ngày" />
        </div>
        <div className="field" style={{ minWidth: 160 }}>
          <label className="label" htmlFor="follow-up-to">
            ĐẾN NGÀY
          </label>
          <DateInput id="follow-up-to" value={to} min={from} max={defaultTo} onChange={setTo} ariaLabel="Đến ngày" />
        </div>
      </div>

      {loading ? (
        <div aria-busy="true" style={{ display: 'grid', gap: 8 }}>
          {Array.from({ length: 3 }).map((_, i) => (
            <div key={i} className="skeleton" />
          ))}
        </div>
      ) : error ? (
        <div className="card empty-state" style={{ border: '2px solid var(--color-fg)', borderRadius: 'var(--radius-lg)' }}>
          <p>{error}</p>
        </div>
      ) : rows.length === 0 ? (
        <div
          className="card empty-state"
          style={{ border: '2px solid var(--color-fg)', borderRadius: 'var(--radius-lg)' }}
          data-testid="follow-up-empty"
        >
          <p style={{ margin: 0, fontWeight: 700 }}>Không có buổi vắng không phép nào chưa có ghi chú liên hệ.</p>
        </div>
      ) : (
        groups.map(([classId, g]) => (
          <div
            key={classId}
            className="card pop-in"
            style={{
              border: '2px solid var(--color-fg)',
              boxShadow: '4px 4px 0 0 var(--color-fg)',
              borderRadius: 'var(--radius-lg)',
              padding: 0,
              overflow: 'hidden',
              marginBottom: 20,
            }}
          >
            <div style={{ padding: '12px 16px', borderBottom: '2px solid var(--color-fg)', background: '#F8FAFC', display: 'flex', gap: 10, alignItems: 'center' }}>
              <PhoneOff size={16} aria-hidden="true" />
              <strong>Lớp {g.className}</strong>
              <span style={{ color: 'var(--color-muted-fg)', fontSize: 'var(--fs-sm)' }}>
                GVCN: {g.teacher ?? '—'} · {g.items.length} buổi vắng
              </span>
            </div>
            <div style={{ overflowX: 'auto' }}>
              <table className="table" style={{ width: '100%', borderCollapse: 'collapse', fontSize: 'var(--fs-sm)' }}>
                <thead>
                  <tr style={{ borderBottom: '1.5px solid var(--color-fg)' }}>
                    <th scope="col" style={{ padding: '10px 14px', textAlign: 'left' }}>Ngày</th>
                    <th scope="col" style={{ padding: '10px 14px', textAlign: 'left' }}>Buổi</th>
                    <th scope="col" style={{ padding: '10px 14px', textAlign: 'left' }}>Học sinh</th>
                    <th scope="col" style={{ padding: '10px 14px', textAlign: 'left' }}>Ghi chú điểm danh</th>
                    <th scope="col" style={{ padding: '10px 14px', textAlign: 'center' }}>Liên hệ gia đình</th>
                  </tr>
                </thead>
                <tbody>
                  {g.items.map((r) => (
                    <tr key={r.recordId} data-testid={`follow-up-row-${r.recordId}`} style={{ borderBottom: '1px solid var(--color-border)' }}>
                      <td style={{ padding: '10px 14px' }}>{formatDashDate(r.date)}</td>
                      <td style={{ padding: '10px 14px' }}>{r.session === 'Morning' ? 'Buổi sáng' : 'Buổi chiều'}</td>
                      <td style={{ padding: '10px 14px' }}>
                        <strong>{r.fullName}</strong>{' '}
                        <span style={{ color: 'var(--color-muted-fg)', fontSize: 'var(--fs-xs)' }}>{r.studentCode}</span>
                      </td>
                      <td style={{ padding: '10px 14px' }}>
                        {r.remarks || '—'}
                        {r.supersededNoteCount > 0 && (
                          <div style={{ fontSize: 'var(--fs-xs)', color: 'var(--color-muted-fg)' }}>
                            {r.supersededNoteCount} ghi chú không còn hiệu lực
                          </div>
                        )}
                      </td>
                      <td style={{ padding: '10px 14px', textAlign: 'center' }}>
                        <button
                          type="button"
                          className="btn btn--secondary btn--sm"
                          onClick={() => setSelected(r)}
                          aria-label={`Ghi chú liên hệ của ${r.fullName}`}
                          style={{ display: 'inline-flex', alignItems: 'center', gap: 6 }}
                        >
                          <MessageSquareText size={14} aria-hidden="true" /> Ghi chú liên hệ
                        </button>
                      </td>
                    </tr>
                  ))}
                </tbody>
              </table>
            </div>
          </div>
        ))
      )}

      <ContactNotesModal
        open={selected !== null}
        recordId={selected?.recordId ?? null}
        studentName={selected?.fullName ?? ''}
        absenceLabel={
          selected ? `${selected.session === 'Morning' ? 'Buổi sáng' : 'Buổi chiều'} · ${formatDashDate(selected.date)}` : ''
        }
        canRecord
        onClose={() => {
          setSelected(null);
          void load();
        }}
        onChanged={() => undefined}
      />
    </section>
  );
}
