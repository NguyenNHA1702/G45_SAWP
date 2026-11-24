import { useEffect, useMemo, useState } from 'react';
import {
  AlertTriangle,
  Ban,
  Calendar,
  RefreshCw,
  Search,
  ShieldAlert,
  Soup,
  Users,
} from 'lucide-react';
import toast from 'react-hot-toast';
import { lunchApi } from '../../services/implementations/lunchApi';
import { formatDateDDMMYYYY } from '../../constants/lunch';
import type {
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

function formatCancelSource(src: StudentMealOperationItem['cancellationSource']): string {
  switch (src) {
    case 'Parent':
    case 1:
      return 'Phụ huynh hủy';
    case 'Leave':
    case 2:
      return 'Đơn xin nghỉ';
    case 'Attendance':
    case 3:
      return 'Điểm danh vắng';
    case 'Staff':
    case 4:
      return 'Giáo vụ / Bếp';
    default:
      return 'Khác';
  }
}

function formatDateTimeDDMMYYYY(iso?: string | null): string {
  if (!iso) return '—';
  const d = new Date(iso);
  if (Number.isNaN(d.getTime())) return formatDateDDMMYYYY(iso);
  const hh = String(d.getHours()).padStart(2, '0');
  const mm = String(d.getMinutes()).padStart(2, '0');
  return `${formatDateDDMMYYYY(d)} ${hh}:${mm}`;
}

export default function TeacherBoardingPage() {
  const [selectedDate, setSelectedDate] = useState<string>(getTodayIso);
  const [selectedClassId, setSelectedClassId] = useState<number | undefined>(undefined);
  const [board, setBoard] = useState<DailyMealBoard | null>(null);
  const [loading, setLoading] = useState<boolean>(false);

  const [searchQuery, setSearchQuery] = useState<string>('');
  const [onlyAllergies, setOnlyAllergies] = useState<boolean>(false);

  const fetchData = async (date: string, classId?: number) => {
    try {
      setLoading(true);
      const boardData = await lunchApi.getDailyMealBoard(date, classId);
      setBoard(boardData);
    } catch {
      toast.error('Không thể tải danh sách bán trú của lớp.');
    } finally {
      setLoading(false);
    }
  };

  useEffect(() => {
    fetchData(selectedDate, selectedClassId);
  }, [selectedDate, selectedClassId]);

  const studentMeals = board?.studentMeals ?? [];

  const cancelledStudents = useMemo(
    () => studentMeals.filter((s) => !s.isActive),
    [studentMeals]
  );

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
                <Soup size={13} /> Bán trú lớp {classLabel}
              </span>
            </div>
            <h1 className="page-title" style={{ margin: 0 }}>
              Danh Sách Bán Trú & <span className="squiggle">Cảnh Báo Dị Ứng</span>
            </h1>
            <p className="page-subtitle" style={{ marginTop: 6 }}>
              Theo dõi khẩu phần ăn và cảnh báo dị ứng thực phẩm của học sinh trong lớp.
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
          </div>
        </div>
      </header>

      {/* KPI Cards */}
      <div
        style={{
          display: 'grid',
          gridTemplateColumns: 'repeat(auto-fit, minmax(190px, 1fr))',
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
        <div className="card" style={{ padding: 18, background: '#FFFBEB' }}>
          <div style={{ fontSize: '0.75rem', fontWeight: 800, color: '#92400E' }}>HỦY SUẤT TRONG NGÀY</div>
          <div style={{ fontSize: '1.6rem', fontWeight: 900, color: '#B45309', marginTop: 4 }}>
            {board?.totalAutoCancelled ?? cancelledStudents.length}
          </div>
        </div>
        <div className="card" style={{ padding: 18, background: '#FEF2F2' }}>
          <div style={{ fontSize: '0.75rem', fontWeight: 800, color: '#991B1B' }}>CẢNH BÁO DỊ ỨNG</div>
          <div style={{ fontSize: '1.6rem', fontWeight: 900, color: '#DC2626', marginTop: 4 }}>{allergyCount}</div>
        </div>
      </div>

      {/* Class Boarding Roster Table (TC_BDS_35) */}
      <section className="card" style={{ padding: 20 }}>
        <div style={{ display: 'flex', justifyContent: 'space-between', alignItems: 'center', flexWrap: 'wrap', gap: 12, marginBottom: 16 }}>
          <div style={{ display: 'flex', alignItems: 'center', gap: 10 }}>
            <Users size={18} />
            <h2 style={{ margin: 0, fontSize: '1.05rem', fontWeight: 900 }}>
              Danh sách bán trú lớp ({formatDateDDMMYYYY(selectedDate)})
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
              </tr>
            </thead>
            <tbody>
              {filteredStudents.length === 0 ? (
                <tr>
                  <td colSpan={6} style={{ padding: 24, textAlign: 'center', color: 'var(--color-muted-fg)' }}>
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
                    </tr>
                  );
                })
              )}
            </tbody>
          </table>
        </div>
      </section>

      {/* A12: Danh sách học sinh hủy suất ăn trong ngày */}
      <section className="card" style={{ padding: 20, marginTop: 24 }}>
        <div style={{ display: 'flex', justifyContent: 'space-between', alignItems: 'center', flexWrap: 'wrap', gap: 12, marginBottom: 16 }}>
          <div style={{ display: 'flex', alignItems: 'center', gap: 10 }}>
            <Ban size={18} color="#B45309" />
            <h2 style={{ margin: 0, fontSize: '1.05rem', fontWeight: 900 }}>
              Số lượng học sinh hủy suất ăn trong ngày ({formatDateDDMMYYYY(selectedDate)})
            </h2>
          </div>
          <span className="badge badge--amber" style={{ border: '1.5px solid var(--color-fg)', fontWeight: 800 }}>
            {cancelledStudents.length} học sinh đã hủy suất
          </span>
        </div>

        <div style={{ overflowX: 'auto' }}>
          <table style={{ width: '100%', borderCollapse: 'collapse', fontSize: 'var(--fs-sm)' }}>
            <thead>
              <tr style={{ borderBottom: '2px solid var(--color-fg)', textAlign: 'left', background: '#FFFBEB' }}>
                <th style={{ padding: '10px 12px', fontWeight: 800 }}>Mã HS</th>
                <th style={{ padding: '10px 12px', fontWeight: 800 }}>Họ và tên</th>
                <th style={{ padding: '10px 12px', fontWeight: 800 }}>Lớp</th>
                <th style={{ padding: '10px 12px', fontWeight: 800 }}>Nguồn hủy</th>
                <th style={{ padding: '10px 12px', fontWeight: 800 }}>Lý do hủy</th>
                <th style={{ padding: '10px 12px', fontWeight: 800 }}>Thời gian hủy</th>
              </tr>
            </thead>
            <tbody>
              {cancelledStudents.length === 0 ? (
                <tr>
                  <td colSpan={6} style={{ padding: 20, textAlign: 'center', color: 'var(--color-muted-fg)' }}>
                    {loading ? 'Đang tải...' : 'Chưa có học sinh nào hủy suất ăn trong ngày này.'}
                  </td>
                </tr>
              ) : (
                cancelledStudents.map((s) => (
                  <tr key={`cancelled-${s.studentId}-${s.registrationId}`} style={{ borderBottom: '1px solid var(--color-border)' }}>
                    <td style={{ padding: '10px 12px', fontWeight: 800 }}>{s.studentCode}</td>
                    <td style={{ padding: '10px 12px', fontWeight: 700 }}>{s.studentName}</td>
                    <td style={{ padding: '10px 12px' }}>{s.className}</td>
                    <td style={{ padding: '10px 12px' }}>
                      <span className="badge badge--pink" style={{ border: '1px solid var(--color-fg)', fontWeight: 700 }}>
                        {formatCancelSource(s.cancellationSource)}
                      </span>
                    </td>
                    <td style={{ padding: '10px 12px', color: 'var(--color-fg)' }}>
                      {s.cancellationReason || 'Không ghi chú'}
                    </td>
                    <td style={{ padding: '10px 12px', color: 'var(--color-muted-fg)', fontWeight: 600 }}>
                      {formatDateTimeDDMMYYYY(s.cancelledAt)}
                    </td>
                  </tr>
                ))
              )}
            </tbody>
          </table>
        </div>
      </section>
    </div>
  );
}
