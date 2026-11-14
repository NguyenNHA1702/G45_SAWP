import { useEffect, useState } from 'react';
import { useSearchParams } from 'react-router';
import { AlertTriangle, Calendar, RefreshCw, ShieldAlert } from 'lucide-react';
import toast from 'react-hot-toast';
import { useParentChild } from '../../contexts/ParentChildContext';
import { lunchApi } from '../../services/implementations/lunchApi';
import type { BoardingIncident } from '../../types/lunch.types';

const INCIDENT_TYPE_LABELS: Record<string, string> = {
  General: 'Chung / Giám sát',
  Dietary: 'Ăn uống / Dị ứng',
  Nap: 'Giờ ngủ trưa',
  Health: 'Sức khỏe / Phòng y tế',
};

export default function ParentBoardingIncidentsPage() {
  const { children, activeChildId, selectChild } = useParentChild();
  const [params, setParams] = useSearchParams();

  const queryStudentId = Number(params.get('studentId'));
  const queryMonth = params.get('month');
  const studentId =
    (queryStudentId && children.some((c) => c.studentId === queryStudentId) ? queryStudentId : activeChildId) ||
    (children[0]?.studentId ?? 1);

  const [month, setMonth] = useState<string>(() => {
    if (queryMonth && /^\d{4}-\d{2}$/.test(queryMonth)) return queryMonth;
    const now = new Date();
    return `${now.getFullYear()}-${String(now.getMonth() + 1).padStart(2, '0')}`;
  });

  const [incidents, setIncidents] = useState<BoardingIncident[]>([]);
  const [loading, setLoading] = useState<boolean>(false);
  const [rangeMode, setRangeMode] = useState<'month' | 'last30' | 'all' | 'custom'>('month');
  const [fromDate, setFromDate] = useState<string>('');
  const [toDate, setToDate] = useState<string>('');

  useEffect(() => {
    if (queryStudentId && queryStudentId !== activeChildId && children.some((c) => c.studentId === queryStudentId)) {
      selectChild(queryStudentId);
    } else if (activeChildId && !queryStudentId) {
      setParams({ studentId: String(activeChildId), month });
    }
  }, [activeChildId, children, month, queryStudentId, selectChild, setParams]);

  useEffect(() => {
    if (queryMonth && /^\d{4}-\d{2}$/.test(queryMonth) && queryMonth !== month) {
      setMonth(queryMonth);
    }
  }, [queryMonth, month]);

  const fetchIncidents = async () => {
    if (!studentId) return;
    try {
      setLoading(true);
      let data: BoardingIncident[] = [];
      if (rangeMode === 'month') {
        data = await lunchApi.getBoardingIncidents(undefined, undefined, month, studentId);
      } else if (rangeMode === 'last30') {
        const end = new Date();
        const start = new Date();
        start.setDate(end.getDate() - 30);
        const fmt = (d: Date) => `${d.getFullYear()}-${String(d.getMonth() + 1).padStart(2, '0')}-${String(d.getDate()).padStart(2, '0')}`;
        data = await lunchApi.getBoardingIncidents(undefined, undefined, undefined, studentId, fmt(start), fmt(end));
      } else if (rangeMode === 'custom') {
        data = await lunchApi.getBoardingIncidents(undefined, undefined, undefined, studentId, fromDate || undefined, toDate || undefined);
      } else {
        data = await lunchApi.getBoardingIncidents(undefined, undefined, undefined, studentId);
      }
      setIncidents(data);
    } catch {
      toast.error('Không thể tải danh sách sự cố bán trú.');
    } finally {
      setLoading(false);
    }
  };

  useEffect(() => {
    fetchIncidents();
  }, [studentId, month, rangeMode, fromDate, toDate]);

  return (
    <div className="container pop-in" style={{ paddingBottom: 60 }}>
      <header className="page-header" style={{ marginBottom: 20 }}>
        <div style={{ display: 'flex', justifyContent: 'space-between', alignItems: 'flex-start', flexWrap: 'wrap', gap: 16 }}>
          <div>
            <span className="badge badge--amber" style={{ border: '1.5px solid var(--color-fg)', fontWeight: 800, marginBottom: 8 }}>
              <ShieldAlert size={13} /> Theo dõi con & Bán trú
            </span>
            <h1 className="page-title" style={{ margin: 0 }}>
              Sự Cố <span className="squiggle">Bán Trú</span> Của Con
            </h1>
            <p className="page-subtitle" style={{ marginTop: 6 }}>
              Xem chi tiết các sự cố giờ ăn, dị ứng hoặc ngủ trưa do giáo viên ghi nhận.
            </p>
          </div>

          <div style={{ display: 'flex', alignItems: 'center', gap: 10, flexWrap: 'wrap' }}>
            {children.length > 0 && (
              <select
                className="input"
                value={studentId}
                onChange={(e) => {
                  const nextId = Number(e.target.value);
                  selectChild(nextId);
                  setParams({ studentId: String(nextId), month });
                }}
                style={{ width: 'auto', minWidth: 200 }}
              >
                {children.map((c) => (
                  <option key={c.studentId} value={c.studentId}>
                    {c.studentCode} - {c.fullName} ({c.className})
                  </option>
                ))}
              </select>
            )}

            <select
              className="input"
              value={rangeMode}
              onChange={(e) => setRangeMode(e.target.value as 'month' | 'last30' | 'all' | 'custom')}
              style={{ width: 'auto' }}
            >
              <option value="month">Theo tháng</option>
              <option value="last30">30 ngày gần nhất</option>
              <option value="all">Tất cả</option>
              <option value="custom">Tùy chọn khoảng ngày</option>
            </select>

            {rangeMode === 'month' && (
              <div
                style={{
                  display: 'inline-flex',
                  alignItems: 'center',
                  gap: 8,
                  padding: '6px 14px',
                  borderRadius: 'var(--radius-full)',
                  border: '2px solid var(--color-fg)',
                  boxShadow: '3px 3px 0 0 var(--color-fg)',
                  background: '#FFFFFF',
                }}
              >
                <Calendar size={16} />
                <input
                  type="month"
                  aria-label="Chọn tháng"
                  value={month}
                  onChange={(e) => {
                    setMonth(e.target.value);
                    setParams({ studentId: String(studentId), month: e.target.value });
                  }}
                  style={{ border: 'none', outline: 'none', fontWeight: 800, fontSize: 'var(--fs-sm)', background: 'transparent' }}
                />
              </div>
            )}

            {rangeMode === 'custom' && (
              <>
                <input type="date" className="input" value={fromDate} onChange={(e) => setFromDate(e.target.value)} style={{ width: 'auto' }} />
                <span>→</span>
                <input type="date" className="input" value={toDate} onChange={(e) => setToDate(e.target.value)} style={{ width: 'auto' }} />
              </>
            )}

            <button type="button" className="btn btn--secondary" onClick={fetchIncidents} disabled={loading}>
              <RefreshCw size={15} className={loading ? 'spin' : ''} /> Làm mới
            </button>
          </div>
        </div>
      </header>

      <section className="card" style={{ padding: 20 }}>
        {incidents.length === 0 ? (
          <div style={{ padding: 36, textAlign: 'center', color: 'var(--color-muted-fg)' }}>
            <AlertTriangle size={28} style={{ marginBottom: 8, opacity: 0.5 }} />
            <div style={{ fontWeight: 700 }}>
              {loading ? 'Đang tải nhật ký sự cố...' : `Không có sự cố bán trú nào được ghi nhận trong tháng ${month}.`}
            </div>
          </div>
        ) : (
          <div style={{ display: 'flex', flexDirection: 'column', gap: 12 }}>
            {incidents.map((inc) => (
              <div
                key={inc.id}
                style={{
                  padding: 16,
                  borderRadius: 14,
                  border: '2px solid var(--color-fg)',
                  boxShadow: '3px 3px 0 0 var(--color-fg)',
                  background: '#FFFBEB',
                  display: 'flex',
                  justifyContent: 'space-between',
                  alignItems: 'flex-start',
                  gap: 14,
                  flexWrap: 'wrap',
                }}
              >
                <div style={{ flex: 1, minWidth: 240 }}>
                  <div style={{ display: 'flex', alignItems: 'center', gap: 8, marginBottom: 6, flexWrap: 'wrap' }}>
                    <span className="badge badge--amber" style={{ border: '1px solid var(--color-fg)', fontWeight: 800 }}>
                      Ngày {new Date(inc.date).toLocaleDateString('vi-VN')}
                    </span>
                    <span className="badge badge--pink" style={{ border: '1px solid var(--color-fg)', fontWeight: 800 }}>
                      {INCIDENT_TYPE_LABELS[inc.incidentType] ?? inc.incidentType}
                    </span>
                    <span style={{ fontWeight: 900 }}>
                      {inc.studentCode} - {inc.studentName} ({inc.className})
                    </span>
                  </div>
                  <div style={{ fontSize: 'var(--fs-md)', fontWeight: 700, color: 'var(--color-fg)', lineHeight: 1.5 }}>
                    {inc.note}
                  </div>
                </div>
                <div style={{ fontSize: 'var(--fs-xs)', color: 'var(--color-muted-fg)', textAlign: 'right' }}>
                  <div>GV ghi nhận: <strong>{inc.recordedByName}</strong></div>
                  <div>{new Date(inc.createdAt).toLocaleString('vi-VN')}</div>
                </div>
              </div>
            ))}
          </div>
        )}
      </section>
    </div>
  );
}
