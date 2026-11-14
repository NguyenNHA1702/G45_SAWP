import { useEffect, useMemo, useState } from 'react';
import {
  AlertTriangle,
  Calendar,
  Plus,
  RefreshCw,
  Search,
  ShieldAlert,
  Soup,
  Users,
} from 'lucide-react';
import toast from 'react-hot-toast';
import { lunchApi } from '../../services/implementations/lunchApi';
import { GeometricModal } from '../../components/common/GeometricModal';
import type {
  BoardingIncident,
  DailyMealBoard,
  StudentMealOperationItem,
} from '../../types/lunch.types';

const MEAL_TYPE_BADGE: Record<string, { label: string; cls: string }> = {
  StandardLunch: { label: '🍱 Tiêu chuẩn', cls: 'badge--mint' },
  VegetarianLunch: { label: '🥗 Ăn chay', cls: 'badge--amber' },
  SpecialDiet: { label: '⚕️ Đặc biệt', cls: 'badge--pink' },
  '1': { label: '🍱 Tiêu chuẩn', cls: 'badge--mint' },
  '2': { label: '🥗 Ăn chay', cls: 'badge--amber' },
  '3': { label: '⚕️ Đặc biệt', cls: 'badge--pink' },
};

const INCIDENT_TYPE_LABELS: Record<string, string> = {
  General: 'Chung / Giám sát',
  Dietary: 'Ăn uống / Dị ứng',
  Nap: 'Giờ ngủ trưa',
  Health: 'Sức khỏe / Phòng y tế',
};

function getTodayIso(): string {
  const d = new Date();
  return `${d.getFullYear()}-${String(d.getMonth() + 1).padStart(2, '0')}-${String(d.getDate()).padStart(2, '0')}`;
}

function getAllergyText(item: StudentMealOperationItem): string | null {
  if (item.allergyWarning && item.allergyWarning.trim()) {
    return item.allergyWarning.trim();
  }
  const raw = (item.remarks || '').trim();
  if (!raw) return null;
  if (raw.includes('|')) {
    const [, notePart] = raw.split('|');
    const clean = (notePart || '').replace(/^Ghi chú:\s*/i, '').trim();
    if (clean.toLowerCase().includes('dị ứng') || clean.toLowerCase().includes('allergy')) {
      return clean;
    }
  }
  if (raw.toLowerCase().includes('dị ứng') || raw.toLowerCase().includes('allergy')) {
    return raw;
  }
  return null;
}

export default function TeacherBoardingPage() {
  const [selectedDate, setSelectedDate] = useState<string>(getTodayIso);
  const [selectedClassId, setSelectedClassId] = useState<number | undefined>(undefined);
  const [board, setBoard] = useState<DailyMealBoard | null>(null);
  const [incidents, setIncidents] = useState<BoardingIncident[]>([]);
  const [loading, setLoading] = useState<boolean>(false);

  const [searchQuery, setSearchQuery] = useState<string>('');
  const [onlyAllergies, setOnlyAllergies] = useState<boolean>(false);

  // Khoảng thời gian xem nhật ký sự cố
  const [incidentRange, setIncidentRange] = useState<'month' | 'week' | 'day' | 'custom'>('month');
  const [incidentFrom, setIncidentFrom] = useState<string>(() => getTodayIso().slice(0, 7) + '-01');
  const [incidentTo, setIncidentTo] = useState<string>(getTodayIso);

  // Modal ghi nhận sự cố bán trú (TC_BDS_36)
  const [showIncidentModal, setShowIncidentModal] = useState<boolean>(false);
  const [incidentStudentId, setIncidentStudentId] = useState<number>(0);
  const [incidentType, setIncidentType] = useState<string>('Health');
  const [incidentNote, setIncidentNote] = useState<string>('');
  const [submittingIncident, setSubmittingIncident] = useState<boolean>(false);

  const fetchIncidents = async (date: string, classId?: number, range = incidentRange, from = incidentFrom, to = incidentTo) => {
    try {
      if (range === 'day') {
        return await lunchApi.getBoardingIncidents(date, classId);
      }
      if (range === 'month') {
        return await lunchApi.getBoardingIncidents(undefined, classId, date.slice(0, 7));
      }
      if (range === 'week') {
        const d = new Date(date);
        const start = new Date(d);
        start.setDate(d.getDate() - 6);
        const startIso = `${start.getFullYear()}-${String(start.getMonth() + 1).padStart(2, '0')}-${String(start.getDate()).padStart(2, '0')}`;
        return await lunchApi.getBoardingIncidents(undefined, classId, undefined, undefined, startIso, date);
      }
      return await lunchApi.getBoardingIncidents(undefined, classId, undefined, undefined, from || undefined, to || undefined);
    } catch {
      return [];
    }
  };

  const fetchData = async (date: string, classId?: number) => {
    try {
      setLoading(true);
      const [boardData, incidentList] = await Promise.all([
        lunchApi.getDailyMealBoard(date, classId),
        fetchIncidents(date, classId),
      ]);
      setBoard(boardData);
      setIncidents(incidentList);
    } catch {
      toast.error('Không thể tải danh sách bán trú của lớp.');
    } finally {
      setLoading(false);
    }
  };

  useEffect(() => {
    fetchData(selectedDate, selectedClassId);
  }, [selectedDate, selectedClassId, incidentRange, incidentFrom, incidentTo]);

  const studentMeals = board?.studentMeals ?? [];

  const allergyCount = useMemo(
    () => studentMeals.filter((s) => getAllergyText(s) !== null).length,
    [studentMeals]
  );

  const filteredStudents = useMemo(() => {
    const q = searchQuery.trim().toLowerCase();
    return studentMeals.filter((s) => {
      if (onlyAllergies && getAllergyText(s) === null) return false;
      if (!q) return true;
      return (
        s.studentName.toLowerCase().includes(q) ||
        s.studentCode.toLowerCase().includes(q) ||
        (getAllergyText(s) || '').toLowerCase().includes(q)
      );
    });
  }, [studentMeals, onlyAllergies, searchQuery]);

  const openIncidentModal = (studentId?: number) => {
    const fallbackId = studentMeals[0]?.studentId ?? 0;
    setIncidentStudentId(studentId || fallbackId);
    setIncidentType('Health');
    setIncidentNote('');
    setShowIncidentModal(true);
  };

  const handleSubmitIncident = async () => {
    if (!incidentStudentId || !incidentNote.trim()) {
      toast.error('Vui lòng chọn học sinh và nhập nội dung sự cố bán trú.');
      return;
    }
    try {
      setSubmittingIncident(true);
      await lunchApi.createBoardingIncident({
        studentId: incidentStudentId,
        date: selectedDate,
        incidentType,
        note: incidentNote.trim(),
      });
      toast.success('Đã ghi nhận sự cố bán trú và gửi cảnh báo đến phụ huynh.');
      setShowIncidentModal(false);
      setIncidentNote('');
      await fetchData(selectedDate, selectedClassId);
    } catch {
      toast.error('Không thể ghi nhận sự cố bán trú.');
    } finally {
      setSubmittingIncident(false);
    }
  };

  const classLabel =
    board?.classBreakdowns && board.classBreakdowns.length > 0
      ? board.classBreakdowns.map((c) => c.className).join(', ')
      : 'Lớp chủ nhiệm';

  return (
    <div className="container pop-in" style={{ paddingBottom: 60 }}>
      {/* Header */}
      <header className="page-header" style={{ marginBottom: 20 }}>
        <div style={{ display: 'flex', justifyContent: 'space-between', alignItems: 'flex-start', flexWrap: 'wrap', gap: 16 }}>
          <div>
            <div style={{ display: 'inline-flex', alignItems: 'center', gap: 8, marginBottom: 6 }}>
              <span className="badge badge--amber" style={{ border: '1.5px solid var(--color-fg)', fontWeight: 800 }}>
                <Soup size={13} /> Bán trú & Giám sát lớp {classLabel}
              </span>
            </div>
            <h1 className="page-title" style={{ margin: 0 }}>
              Danh Sách Bán Trú & <span className="squiggle">Cảnh Báo Dị Ứng</span>
            </h1>
            <p className="page-subtitle" style={{ marginTop: 6 }}>
              Theo dõi khẩu phần ăn, cảnh báo dị ứng thực phẩm và ghi nhận sự cố giờ ăn/ngủ trưa gửi phụ huynh.
            </p>
          </div>

          <div style={{ display: 'flex', alignItems: 'center', gap: 10, flexWrap: 'wrap' }}>
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
                type="date"
                aria-label="Chọn ngày bán trú"
                value={selectedDate}
                onChange={(e) => setSelectedDate(e.target.value)}
                style={{
                  border: 'none',
                  outline: 'none',
                  fontWeight: 800,
                  fontSize: 'var(--fs-sm)',
                  background: 'transparent',
                }}
              />
            </div>

            {board && board.classBreakdowns.length > 1 && (
              <select
                className="input"
                value={selectedClassId ?? ''}
                onChange={(e) => setSelectedClassId(e.target.value ? Number(e.target.value) : undefined)}
                style={{ width: 'auto', minWidth: 130 }}
              >
                <option value="">Tất cả lớp</option>
                {board.classBreakdowns.map((c) => (
                  <option key={c.classId} value={c.classId}>
                    Lớp {c.className}
                  </option>
                ))}
              </select>
            )}

            <button
              type="button"
              className="btn btn--secondary"
              onClick={() => fetchData(selectedDate, selectedClassId)}
              disabled={loading}
            >
              <RefreshCw size={15} className={loading ? 'spin' : ''} /> Làm mới
            </button>

            <button
              type="button"
              className="btn btn--primary"
              onClick={() => openIncidentModal()}
              disabled={studentMeals.length === 0}
            >
              <Plus size={15} /> Ghi nhận sự cố
            </button>
          </div>
        </div>
      </header>

      {/* KPI Cards */}
      <div
        style={{
          display: 'grid',
          gridTemplateColumns: 'repeat(auto-fit, minmax(200px, 1fr))',
          gap: 16,
          marginBottom: 24,
        }}
      >
        <div className="card" style={{ padding: 18 }}>
          <div style={{ fontSize: '0.75rem', fontWeight: 800, color: 'var(--color-muted-fg)' }}>ĐĂNG KÝ BÁN TRÚ</div>
          <div style={{ fontSize: '1.6rem', fontWeight: 900, marginTop: 4 }}>{board?.totalInitialRegistered ?? 0}</div>
        </div>
        <div className="card" style={{ padding: 18, background: '#ECFDF5' }}>
          <div style={{ fontSize: '0.75rem', fontWeight: 800, color: '#065F46' }}>SUẤT THỰC ĂN</div>
          <div style={{ fontSize: '1.6rem', fontWeight: 900, color: '#047857', marginTop: 4 }}>
            {board?.totalActiveToCook ?? 0}
          </div>
        </div>
        <div className="card" style={{ padding: 18, background: '#FEF2F2' }}>
          <div style={{ fontSize: '0.75rem', fontWeight: 800, color: '#991B1B' }}>CẢNH BÁO DỊ ỨNG</div>
          <div style={{ fontSize: '1.6rem', fontWeight: 900, color: '#DC2626', marginTop: 4 }}>{allergyCount}</div>
        </div>
        <div className="card" style={{ padding: 18, background: '#FEF3C7' }}>
          <div style={{ fontSize: '0.75rem', fontWeight: 800, color: '#92400E' }}>SỰ CỐ TRONG NGÀY</div>
          <div style={{ fontSize: '1.6rem', fontWeight: 900, color: '#B45309', marginTop: 4 }}>{incidents.length}</div>
        </div>
      </div>

      {/* Class Boarding Roster Table (TC_BDS_35) */}
      <section className="card" style={{ padding: 20, marginBottom: 24 }}>
        <div style={{ display: 'flex', justifyContent: 'space-between', alignItems: 'center', flexWrap: 'wrap', gap: 12, marginBottom: 16 }}>
          <div style={{ display: 'flex', alignItems: 'center', gap: 10 }}>
            <Users size={18} />
            <h2 style={{ margin: 0, fontSize: '1.05rem', fontWeight: 900 }}>
              Danh sách bán trú lớp ({selectedDate})
            </h2>
          </div>

          <div style={{ display: 'flex', alignItems: 'center', gap: 10, flexWrap: 'wrap' }}>
            <div style={{ position: 'relative' }}>
              <Search size={14} style={{ position: 'absolute', left: 12, top: '50%', transform: 'translateY(-50%)', color: 'var(--color-muted-fg)' }} />
              <input
                type="text"
                className="input"
                placeholder="Tìm mã HS, tên, dị ứng..."
                value={searchQuery}
                onChange={(e) => setSearchQuery(e.target.value)}
                style={{ paddingLeft: 34, minWidth: 220 }}
              />
            </div>

            <button
              type="button"
              className={onlyAllergies ? 'btn btn--primary btn--sm' : 'btn btn--secondary btn--sm'}
              onClick={() => setOnlyAllergies((v) => !v)}
            >
              <ShieldAlert size={14} /> Chỉ xem dị ứng ({allergyCount})
            </button>
          </div>
        </div>

        <div style={{ overflowX: 'auto' }}>
          <table style={{ width: '100%', borderCollapse: 'collapse', fontSize: 'var(--fs-sm)' }}>
            <thead>
              <tr style={{ borderBottom: '2px solid var(--color-fg)', textAlign: 'left', background: '#F8FAFC' }}>
                <th style={{ padding: '10px 12px', fontWeight: 800 }}>Mã HS</th>
                <th style={{ padding: '10px 12px', fontWeight: 800 }}>Họ và tên</th>
                <th style={{ padding: '10px 12px', fontWeight: 800 }}>Lớp</th>
                <th style={{ padding: '10px 12px', fontWeight: 800 }}>Loại suất</th>
                <th style={{ padding: '10px 12px', fontWeight: 800 }}>Trạng thái</th>
                <th style={{ padding: '10px 12px', fontWeight: 800 }}>Cảnh báo dị ứng / Ghi chú</th>
                <th style={{ padding: '10px 12px', fontWeight: 800, textAlign: 'right' }}>Thao tác</th>
              </tr>
            </thead>
            <tbody>
              {filteredStudents.length === 0 ? (
                <tr>
                  <td colSpan={7} style={{ padding: 24, textAlign: 'center', color: 'var(--color-muted-fg)' }}>
                    {loading ? 'Đang tải danh sách bán trú...' : 'Không có dữ liệu bán trú phù hợp.'}
                  </td>
                </tr>
              ) : (
                filteredStudents.map((s) => {
                  const allergy = getAllergyText(s);
                  const badgeCfg = MEAL_TYPE_BADGE[String(s.mealType)] ?? MEAL_TYPE_BADGE.StandardLunch;
                  return (
                    <tr
                      key={`${s.studentId}-${s.registrationId}`}
                      style={{
                        borderBottom: '1px solid var(--color-border)',
                        background: allergy ? '#FFF1F2' : undefined,
                      }}
                    >
                      <td style={{ padding: '10px 12px', fontWeight: 800 }}>{s.studentCode}</td>
                      <td style={{ padding: '10px 12px', fontWeight: 700 }}>{s.studentName}</td>
                      <td style={{ padding: '10px 12px' }}>{s.className}</td>
                      <td style={{ padding: '10px 12px' }}>
                        <span className={`badge ${badgeCfg.cls}`} style={{ border: '1px solid var(--color-fg)', fontWeight: 700 }}>
                          {badgeCfg.label}
                        </span>
                      </td>
                      <td style={{ padding: '10px 12px' }}>
                        {s.isActive ? (
                          <span className="badge badge--mint" style={{ border: '1px solid var(--color-fg)', fontWeight: 700 }}>
                            Đang ăn bán trú
                          </span>
                        ) : (
                          <span className="badge badge--amber" style={{ border: '1px solid var(--color-fg)', fontWeight: 700 }}>
                            Đã hủy
                          </span>
                        )}
                      </td>
                      <td style={{ padding: '10px 12px' }}>
                        {allergy ? (
                          <span
                            style={{
                              display: 'inline-flex',
                              alignItems: 'center',
                              gap: 6,
                              padding: '4px 10px',
                              borderRadius: 10,
                              background: '#FEE2E2',
                              border: '1.5px solid #DC2626',
                              color: '#991B1B',
                              fontWeight: 800,
                              fontSize: 'var(--fs-xs)',
                            }}
                          >
                            <AlertTriangle size={14} /> {allergy}
                          </span>
                        ) : (
                          <span style={{ color: 'var(--color-muted-fg)', fontSize: 'var(--fs-xs)' }}>
                            {s.cancellationReason || 'Không có'}
                          </span>
                        )}
                      </td>
                      <td style={{ padding: '10px 12px', textAlign: 'right' }}>
                        <button
                          type="button"
                          className="btn btn--secondary btn--sm"
                          onClick={() => openIncidentModal(s.studentId)}
                        >
                          <AlertTriangle size={13} /> Ghi nhận sự cố
                        </button>
                      </td>
                    </tr>
                  );
                })
              )}
            </tbody>
          </table>
        </div>
      </section>

      {/* Daily Boarding Incident Log (TC_BDS_36) */}
      <section className="card" style={{ padding: 20 }}>
        <div style={{ display: 'flex', alignItems: 'center', justifyContent: 'space-between', flexWrap: 'wrap', gap: 12, marginBottom: 14 }}>
          <div style={{ display: 'flex', alignItems: 'center', gap: 8 }}>
            <AlertTriangle size={18} color="#D97706" />
            <h2 style={{ margin: 0, fontSize: '1.05rem', fontWeight: 900 }}>
              Nhật ký sự cố bán trú ({incidents.length})
            </h2>
          </div>
          <div style={{ display: 'flex', alignItems: 'center', gap: 8, flexWrap: 'wrap' }}>
            <select
              className="input"
              value={incidentRange}
              onChange={(e) => setIncidentRange(e.target.value as 'month' | 'week' | 'day' | 'custom')}
              style={{ width: 'auto' }}
            >
              <option value="month">Theo tháng ({selectedDate.slice(0, 7)})</option>
              <option value="week">7 ngày gần nhất</option>
              <option value="day">Chỉ ngày {selectedDate}</option>
              <option value="custom">Tùy chọn khoảng ngày</option>
            </select>
            {incidentRange === 'custom' && (
              <>
                <input
                  type="date"
                  className="input"
                  value={incidentFrom}
                  onChange={(e) => setIncidentFrom(e.target.value)}
                  style={{ width: 'auto' }}
                />
                <span>→</span>
                <input
                  type="date"
                  className="input"
                  value={incidentTo}
                  onChange={(e) => setIncidentTo(e.target.value)}
                  style={{ width: 'auto' }}
                />
              </>
            )}
          </div>
        </div>

        {incidents.length === 0 ? (
          <div style={{ padding: 20, textAlign: 'center', color: 'var(--color-muted-fg)', fontSize: 'var(--fs-sm)' }}>
            Chưa có sự cố bán trú nào được ghi nhận trong ngày này.
          </div>
        ) : (
          <div style={{ display: 'flex', flexDirection: 'column', gap: 10 }}>
            {incidents.map((inc) => (
              <div
                key={inc.id}
                style={{
                  padding: 14,
                  borderRadius: 14,
                  border: '2px solid var(--color-fg)',
                  background: '#FFFBEB',
                  display: 'flex',
                  justifyContent: 'space-between',
                  alignItems: 'flex-start',
                  gap: 12,
                  flexWrap: 'wrap',
                }}
              >
                <div>
                  <div style={{ display: 'flex', alignItems: 'center', gap: 8, marginBottom: 4, flexWrap: 'wrap' }}>
                    <span style={{ fontWeight: 900 }}>
                      {inc.studentCode} - {inc.studentName}
                    </span>
                    <span className="badge badge--amber" style={{ border: '1px solid var(--color-fg)', fontSize: '0.7rem' }}>
                      {new Date(inc.date).toLocaleDateString('vi-VN')}
                    </span>
                    <span className="badge badge--pink" style={{ border: '1px solid var(--color-fg)', fontSize: '0.7rem' }}>
                      {INCIDENT_TYPE_LABELS[inc.incidentType] ?? inc.incidentType}
                    </span>
                    <span className="badge badge--mint" style={{ border: '1px solid var(--color-fg)', fontSize: '0.7rem' }}>
                      Đã gửi cảnh báo phụ huynh
                    </span>
                  </div>
                  <div style={{ fontSize: 'var(--fs-sm)', fontWeight: 600 }}>{inc.note}</div>
                </div>
                <div style={{ fontSize: 'var(--fs-xs)', color: 'var(--color-muted-fg)', textAlign: 'right' }}>
                  <div>Ghi nhận: {inc.recordedByName}</div>
                  <div>{new Date(inc.createdAt).toLocaleString('vi-VN')}</div>
                </div>
              </div>
            ))}
          </div>
        )}
      </section>

      {/* Modal Ghi nhận sự cố bán trú */}
      <GeometricModal
        open={showIncidentModal}
        onClose={() => setShowIncidentModal(false)}
        title="Ghi nhận Sự cố Bán trú"
        maxWidth="sm"
      >
        <div style={{ display: 'flex', flexDirection: 'column', gap: 14 }}>
          <div style={{ padding: 10, borderRadius: 10, background: '#FEF3C7', border: '1.5px solid #F59E0B', fontSize: 'var(--fs-xs)', fontWeight: 700, color: '#92400E' }}>
            Hệ thống sẽ lưu vào nhật ký bán trú ngày {selectedDate} và gửi thông báo <strong>Boarding Incident</strong> ngay lập tức cho phụ huynh.
          </div>

          <div>
            <label style={{ display: 'block', fontWeight: 800, fontSize: 'var(--fs-xs)', marginBottom: 6 }}>
              Học sinh
            </label>
            <select
              className="input"
              value={incidentStudentId}
              onChange={(e) => setIncidentStudentId(Number(e.target.value))}
              style={{ width: '100%' }}
            >
              {studentMeals.map((s) => (
                <option key={s.studentId} value={s.studentId}>
                  {s.studentCode} - {s.studentName} ({s.className})
                </option>
              ))}
            </select>
          </div>

          <div>
            <label style={{ display: 'block', fontWeight: 800, fontSize: 'var(--fs-xs)', marginBottom: 6 }}>
              Loại sự cố
            </label>
            <select
              className="input"
              value={incidentType}
              onChange={(e) => setIncidentType(e.target.value)}
              style={{ width: '100%' }}
            >
              <option value="Health">Sức khỏe / Phòng y tế</option>
              <option value="Dietary">Ăn uống / Dị ứng</option>
              <option value="Nap">Giờ ngủ trưa</option>
              <option value="General">Chung / Giám sát</option>
            </select>
          </div>

          <div>
            <label style={{ display: 'block', fontWeight: 800, fontSize: 'var(--fs-xs)', marginBottom: 6 }}>
              Nội dung sự cố
            </label>
            <textarea
              className="input"
              rows={3}
              value={incidentNote}
              onChange={(e) => setIncidentNote(e.target.value)}
              placeholder="Ví dụ: Cháu bị nôn nhẹ sau bữa trưa, đã cho nghỉ tại phòng y tế"
              style={{ width: '100%' }}
            />
          </div>

          <div style={{ display: 'flex', justifyContent: 'flex-end', gap: 10, marginTop: 6 }}>
            <button type="button" className="btn btn--secondary" onClick={() => setShowIncidentModal(false)}>
              Hủy
            </button>
            <button
              type="button"
              className="btn btn--primary"
              onClick={handleSubmitIncident}
              disabled={submittingIncident}
            >
              {submittingIncident ? 'Đang gửi...' : 'Lưu & Cảnh báo phụ huynh'}
            </button>
          </div>
        </div>
      </GeometricModal>
    </div>
  );
}
