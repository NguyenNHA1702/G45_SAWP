import { useEffect, useMemo, useState } from 'react';
import {
  AlertTriangle,
  Ban,
  CheckCircle2,
  RefreshCw,
  Search,
  ShieldAlert,
  Soup,
  Users,
  UtensilsCrossed,
} from 'lucide-react';
import toast from 'react-hot-toast';
import { lunchApi } from '../../services/implementations/lunchApi';
import { formatDateDDMMYYYY } from '../../constants/lunch';
import {
  detectLiveDishConflicts,
  getAllergenFullLabel,
  normalizeMealTypeNumber,
} from '../../constants/allergens';
import { GeometricModal } from '../../components/common/GeometricModal';
import { DatePickerPill } from '../../components/common/DatePickerPill';
import type {
  DailyMealBoard,
  DailyMenusByMealType,
  DishSummary,
  StudentMealOperationItem,
} from '../../types/lunch.types';

const MEAL_TYPE_BADGE: Record<string, { label: string; cls: string; color: 'mint' | 'amber' | 'pink' }> = {
  StandardLunch: { label: 'Tiêu chuẩn', cls: 'badge--mint', color: 'mint' },
  VegetarianLunch: { label: 'Ăn chay', cls: 'badge--amber', color: 'amber' },
  SpecialDiet: { label: 'Đặc biệt', cls: 'badge--pink', color: 'pink' },
  '1': { label: 'Tiêu chuẩn', cls: 'badge--mint', color: 'mint' },
  '2': { label: 'Ăn chay', cls: 'badge--amber', color: 'amber' },
  '3': { label: 'Đặc biệt', cls: 'badge--pink', color: 'pink' },
};

const COURSE_SLOTS = [
  { role: 'Món chính • Tinh bột', emoji: '🍚', bg: '#FEF3C7', accent: '#D97706' },
  { role: 'Món mặn • Giàu đạm', emoji: '🍗', bg: '#FCE7F3', accent: '#DB2777' },
  { role: 'Món xào • Rau củ', emoji: '🥦', bg: '#ECFDF5', accent: '#059669' },
  { role: 'Món canh • Thanh mát', emoji: '🍲', bg: '#EDE9FE', accent: '#7C3AED' },
  { role: 'Tráng miệng • Vitamin', emoji: '🍌', bg: '#FFFDF5', accent: '#EA580C' },
];

function formatCleanRemark(remarks?: string | null): string {
  const raw = (remarks || '').trim();
  if (!raw || raw === 'Suất ăn bán trú tiêu chuẩn') return '';
  if (raw.includes('|')) {
    const [, notePart] = raw.split('|');
    return (notePart || '').replace(/^Ghi chú:\s*/i, '').trim();
  }
  return raw.replace(/^Ghi chú:\s*/i, '').trim();
}

function isBoilerplateNonAllergyNote(text?: string | null): boolean {
  const clean = (text || '').replace(/^Ghi chú:\s*/gi, '').trim().toLowerCase();
  return (
    !clean ||
    clean === 'suất ăn chay thanh đạm' ||
    clean === 'suất ăn bán trú tiêu chuẩn' ||
    clean === 'chế độ ăn mềm dễ tiêu hóa'
  );
}

function getStudentTrayDishes(
  mealType: string | number,
  dailyMenus?: DailyMenusByMealType | null,
  dailyMenuRemarks?: string | null,
  studentRemarks?: string | null
): string[] {
  const rawRemarks = (studentRemarks || '').trim();
  if (rawRemarks.includes('|')) {
    const [menuPart] = rawRemarks.split('|');
    const customDishes = menuPart.split(',').map((d) => d.trim()).filter(Boolean);
    if (customDishes.length >= 2) {
      return customDishes;
    }
  }

  const mtNum = normalizeMealTypeNumber(mealType);
  if (dailyMenus) {
    const structured =
      mtNum === 2
        ? dailyMenus.vegetarian
        : mtNum === 3
          ? dailyMenus.special
          : dailyMenus.standard;
    if (structured && structured.length > 0) {
      return structured.map((d) => d.name);
    }
    if (dailyMenus.standard && dailyMenus.standard.length > 0) {
      return dailyMenus.standard.map((d) => d.name);
    }
  }

  const cleanMenu = (dailyMenuRemarks || '')
    .replace(/^Thực đơn:\s*/i, '')
    .replace(/^Thực đơn Tân Lập A:\s*/i, '')
    .trim();
  return cleanMenu
    .split(',')
    .map((d) => d.trim())
    .filter(Boolean);
}

function getTodayIso(): string {
  const d = new Date();
  return `${d.getFullYear()}-${String(d.getMonth() + 1).padStart(2, '0')}-${String(d.getDate()).padStart(2, '0')}`;
}

function getAllergyText(item: StudentMealOperationItem): string | null {
  const conflictsPart =
    item.allergyConflicts && item.allergyConflicts.length > 0
      ? `Xung đột món hôm nay: ${item.allergyConflicts.join(' • ')}`
      : null;

  const tagLabels = (item.studentAllergenTags ?? []).map(getAllergenFullLabel).filter(Boolean);
  const cleanAllergyNote = !isBoilerplateNonAllergyNote(item.studentAllergyNotes)
    ? (item.studentAllergyNotes || '').replace(/^Ghi chú:\s*/gi, '').trim()
    : '';

  const structuredParts: string[] = [];
  if (tagLabels.length > 0) structuredParts.push(tagLabels.join(', '));
  if (cleanAllergyNote) structuredParts.push(`Ghi chú: ${cleanAllergyNote}`);

  let profilePart: string | null = structuredParts.length > 0 ? structuredParts.join(' • ') : null;
  if (!profilePart && item.allergyWarning && !isBoilerplateNonAllergyNote(item.allergyWarning)) {
    profilePart = item.allergyWarning.replace(/^Ghi chú:\s*Ghi chú:\s*/i, 'Ghi chú: ').trim();
  }

  if (conflictsPart && profilePart) {
    return `${conflictsPart} (Hồ sơ: ${profilePart})`;
  }
  if (conflictsPart) return conflictsPart;
  if (profilePart) return profilePart;

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
  const [selectedStudentTray, setSelectedStudentTray] = useState<StudentMealOperationItem | null>(null);

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

          <div className="mobile-header-actions">
            <DatePickerPill
              value={selectedDate}
              onChange={setSelectedDate}
              ariaLabel="Chọn ngày bán trú"
            />

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

          <div className="responsive-filter-bar">
            <div style={{ position: 'relative', flex: '1 1 200px', minWidth: 0 }}>
              <Search size={14} style={{ position: 'absolute', left: 12, top: '50%', transform: 'translateY(-50%)', color: 'var(--color-muted-fg)' }} />
              <input
                type="text"
                className="input"
                placeholder="Tìm mã HS, tên, dị ứng..."
                value={searchQuery}
                onChange={(e) => setSearchQuery(e.target.value)}
                style={{ paddingLeft: 34 }}
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
                <th style={{ padding: '10px 12px', fontWeight: 800, textAlign: 'center' }}>Khay ăn</th>
                <th style={{ padding: '10px 12px', fontWeight: 800 }}>Trạng thái</th>
                <th style={{ padding: '10px 12px', fontWeight: 800 }}>Cảnh báo dị ứng / Ghi chú</th>
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
                      <td style={{ padding: '10px 12px', textAlign: 'center' }}>
                        {s.isActive ? (
                          <button
                            type="button"
                            onClick={() => setSelectedStudentTray(s)}
                            style={{
                              display: 'inline-flex',
                              alignItems: 'center',
                              gap: 5,
                              padding: '5px 12px',
                              borderRadius: 'var(--radius-full)',
                              border: '1.5px solid var(--color-fg)',
                              background: '#FFFDF5',
                              boxShadow: '2px 2px 0 0 var(--color-fg)',
                              fontWeight: 800,
                              fontSize: '11.5px',
                              color: 'var(--color-fg)',
                              cursor: 'pointer',
                              whiteSpace: 'nowrap',
                              transition: 'all 0.15s cubic-bezier(0.34,1.56,0.64,1)',
                            }}
                            onMouseEnter={(e) => {
                              e.currentTarget.style.background = 'var(--color-tertiary)';
                              e.currentTarget.style.transform = 'translate(-1px, -1px)';
                              e.currentTarget.style.boxShadow = '3px 3px 0 0 var(--color-fg)';
                            }}
                            onMouseLeave={(e) => {
                              e.currentTarget.style.background = '#FFFDF5';
                              e.currentTarget.style.transform = 'none';
                              e.currentTarget.style.boxShadow = '2px 2px 0 0 var(--color-fg)';
                            }}
                          >
                            <UtensilsCrossed size={12} color="var(--color-accent)" strokeWidth={2.5} />
                            Xem khay ăn
                          </button>
                        ) : (
                          <span style={{ fontSize: 'var(--fs-xs)', color: 'var(--color-muted-fg)', fontStyle: 'italic' }}>
                            Không chia khay
                          </span>
                        )}
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

      {/* Modal Xem Khay Ăn Bán Trú dành cho Giáo viên chủ nhiệm */}
      <GeometricModal
        open={Boolean(selectedStudentTray)}
        onClose={() => setSelectedStudentTray(null)}
        title={`Khay Ăn Bán Trú • ${selectedStudentTray?.studentName ?? ''}`}
        subtitle={
          selectedStudentTray
            ? `Lớp ${selectedStudentTray.className} • Mã HS: ${selectedStudentTray.studentCode} • Ngày ${formatDateDDMMYYYY(selectedDate)}`
            : ''
        }
        badgeText={
          selectedStudentTray
            ? (MEAL_TYPE_BADGE[String(selectedStudentTray.mealType)]?.label ?? 'Tiêu chuẩn')
            : ''
        }
        badgeColor={
          selectedStudentTray
            ? (MEAL_TYPE_BADGE[String(selectedStudentTray.mealType)]?.color ?? 'mint')
            : 'mint'
        }
        icon={<Soup size={22} color="var(--color-fg)" strokeWidth={2.5} />}
        iconBg="var(--color-tertiary)"
        maxWidth="md"
      >
        {selectedStudentTray && (() => {
          const mealTypeNum = normalizeMealTypeNumber(selectedStudentTray.mealType);
          const badgeCfg = MEAL_TYPE_BADGE[String(selectedStudentTray.mealType)] ?? MEAL_TYPE_BADGE.StandardLunch;
          const dishes = getStudentTrayDishes(
            selectedStudentTray.mealType,
            board?.menus,
            board?.serviceRemarks,
            selectedStudentTray.remarks
          );

          const structuredDishes: DishSummary[] = board?.menus
            ? ((mealTypeNum === 2
                ? board.menus.vegetarian
                : mealTypeNum === 3
                  ? board.menus.special
                  : board.menus.standard) ?? [])
            : [];
          const effectiveStructured =
            structuredDishes.length > 0 ? structuredDishes : (board?.menus?.standard ?? []);

          const dishObjectsForCheck =
            (selectedStudentTray.remarks || '').includes('|')
              ? dishes.map((name) => {
                  const matched = effectiveStructured.find((sd) => sd.name.toLowerCase() === name.toLowerCase());
                  return matched ?? { name, allergenTags: [] };
                })
              : effectiveStructured.length > 0
                ? effectiveStructured
                : dishes.map((name) => ({ name, allergenTags: [] }));

          const cleanAllergyNotes = !isBoilerplateNonAllergyNote(selectedStudentTray.studentAllergyNotes)
            ? (selectedStudentTray.studentAllergyNotes || '').replace(/^Ghi chú:\s*/gi, '').trim()
            : '';

          const { conflicts: liveConflicts, conflictingDishNames } = detectLiveDishConflicts(
            selectedStudentTray.studentAllergenTags,
            cleanAllergyNotes || undefined,
            dishObjectsForCheck,
            selectedStudentTray.allergyWarning || selectedStudentTray.remarks
          );
          const conflictingSet = new Set(conflictingDishNames);

          const declaredTags = selectedStudentTray.studentAllergenTags ?? [];
          const cleanRegRemark = formatCleanRemark(selectedStudentTray.remarks);
          const kitchenRemark =
            cleanRegRemark &&
            cleanRegRemark.toLowerCase() !== cleanAllergyNotes.toLowerCase() &&
            !isBoilerplateNonAllergyNote(cleanRegRemark)
              ? cleanRegRemark
              : '';
          const hasDeclaredAllergy =
            declaredTags.length > 0 || Boolean(cleanAllergyNotes) || liveConflicts.length > 0;

          return (
            <div style={{ display: 'flex', flexDirection: 'column', gap: 16 }}>
              {liveConflicts.length > 0 ? (
                <div
                  style={{
                    padding: '10px 14px',
                    borderRadius: 'var(--radius-md)',
                    border: '2px solid #DC2626',
                    background: '#FEF2F2',
                    color: '#991B1B',
                    fontSize: 'var(--fs-xs)',
                    fontWeight: 800,
                    display: 'flex',
                    alignItems: 'flex-start',
                    gap: 8,
                  }}
                >
                  <AlertTriangle size={16} style={{ flexShrink: 0, marginTop: 1 }} />
                  <div>
                    <div>Cảnh báo trùng thành phần dị ứng trong khay ăn:</div>
                    <div style={{ fontWeight: 700, marginTop: 2 }}>{liveConflicts.join(' • ')}</div>
                  </div>
                </div>
              ) : hasDeclaredAllergy && dishes.length > 0 ? (
                <div
                  style={{
                    padding: '10px 14px',
                    borderRadius: 'var(--radius-md)',
                    border: '2px solid #059669',
                    background: '#ECFDF5',
                    color: '#065F46',
                    fontSize: 'var(--fs-xs)',
                    fontWeight: 800,
                    display: 'flex',
                    alignItems: 'center',
                    gap: 8,
                  }}
                >
                  <CheckCircle2 size={16} color="#059669" style={{ flexShrink: 0 }} />
                  <div>Khẩu phần hiện tại của bé đã an toàn, không chứa thành phần gây dị ứng đã khai báo.</div>
                </div>
              ) : null}

              <div
                style={{
                  background: '#FFFDF5',
                  border: '2.5px solid var(--color-fg)',
                  borderRadius: 'var(--radius-lg)',
                  boxShadow: '5px 5px 0 0 #E2E8F0',
                  padding: '16px',
                }}
              >
                <div style={{ display: 'flex', alignItems: 'center', justifyContent: 'space-between', marginBottom: 12, flexWrap: 'wrap', gap: 8 }}>
                  <span
                    style={{
                      fontFamily: 'var(--font-heading)',
                      fontWeight: 800,
                      fontSize: 'var(--fs-sm)',
                      textTransform: 'uppercase',
                      letterSpacing: '0.04em',
                      color: 'var(--color-fg)',
                      display: 'inline-flex',
                      alignItems: 'center',
                      gap: 6,
                    }}
                  >
                    <UtensilsCrossed size={15} color="var(--color-accent)" strokeWidth={2.5} />
                    Các món trong khay ({dishes.length} món)
                  </span>
                  <span className={`badge ${badgeCfg.cls}`} style={{ border: '1.5px solid var(--color-fg)', fontWeight: 800, fontSize: '11px' }}>
                    {badgeCfg.label}
                  </span>
                </div>

                <div style={{ display: 'grid', gridTemplateColumns: 'repeat(auto-fit, minmax(190px, 1fr))', gap: 10 }}>
                  {dishes.length === 0 ? (
                    <div style={{ gridColumn: '1 / -1', padding: '14px 12px', textAlign: 'center', fontSize: 'var(--fs-xs)', color: 'var(--color-muted-fg)' }}>
                      Tổ Bán trú chưa cấu hình thực đơn chi tiết cho ngày này.
                    </div>
                  ) : (
                    dishes.map((dish, idx) => {
                      const slot = COURSE_SLOTS[idx % COURSE_SLOTS.length];
                      const isDishAllergic = conflictingSet.has(dish.toLowerCase());
                      return (
                        <div
                          key={idx}
                          style={{
                            display: 'flex',
                            alignItems: 'center',
                            gap: 10,
                            padding: '10px 12px',
                            background: isDishAllergic ? '#FEF2F2' : '#FFFFFF',
                            border: isDishAllergic ? '2px solid #DC2626' : '2px solid var(--color-fg)',
                            borderRadius: 'var(--radius-md)',
                            boxShadow: isDishAllergic ? '3px 3px 0 0 #DC2626' : '3px 3px 0 0 var(--color-fg)',
                          }}
                        >
                          <div
                            style={{
                              width: 38,
                              height: 38,
                              borderRadius: '50%',
                              background: isDishAllergic ? '#FEE2E2' : slot.bg,
                              border: isDishAllergic ? '2px solid #DC2626' : '2px solid var(--color-fg)',
                              display: 'grid',
                              placeItems: 'center',
                              fontSize: 18,
                              flexShrink: 0,
                            }}
                          >
                            {isDishAllergic ? '⚠️' : slot.emoji}
                          </div>
                          <div style={{ minWidth: 0 }}>
                            <div style={{ fontSize: '10px', fontWeight: 800, textTransform: 'uppercase', color: isDishAllergic ? '#DC2626' : slot.accent }}>
                              {isDishAllergic ? 'TRÙNG DỊ ỨNG' : slot.role}
                            </div>
                            <div style={{ fontFamily: 'var(--font-heading)', fontWeight: 800, fontSize: 'var(--fs-sm)', color: isDishAllergic ? '#991B1B' : 'var(--color-fg)', marginTop: 2 }}>
                              {dish}
                            </div>
                          </div>
                        </div>
                      );
                    })
                  )}
                </div>
              </div>

              <div
                style={{
                  padding: '12px 14px',
                  borderRadius: 'var(--radius-md)',
                  border: hasDeclaredAllergy ? '2px solid #FB7185' : '2px dashed var(--color-fg)',
                  background: hasDeclaredAllergy ? '#FFF1F2' : '#F8FAFC',
                  fontSize: 'var(--fs-xs)',
                  display: 'flex',
                  flexDirection: 'column',
                  gap: 6,
                }}
              >
                <div style={{ fontWeight: 800, color: hasDeclaredAllergy ? '#BE123C' : 'var(--color-fg)', display: 'flex', alignItems: 'center', gap: 6 }}>
                  <ShieldAlert size={14} /> Hồ sơ dị ứng phụ huynh khai báo:
                </div>

                {declaredTags.length > 0 && (
                  <div style={{ display: 'flex', gap: 6, flexWrap: 'wrap' }}>
                    {declaredTags.map((tag) => (
                      <span
                        key={tag}
                        style={{
                          padding: '3px 9px',
                          borderRadius: '999px',
                          border: '1.5px solid #DC2626',
                          background: '#FEE2E2',
                          color: '#991B1B',
                          fontWeight: 800,
                          fontSize: '11px',
                        }}
                      >
                        {getAllergenFullLabel(tag)}
                      </span>
                    ))}
                  </div>
                )}

                {cleanAllergyNotes && (
                  <div style={{ fontWeight: 700, color: '#9F1239' }}>
                    Ghi chú dị ứng tự do: <span style={{ fontWeight: 600 }}>{cleanAllergyNotes}</span>
                  </div>
                )}

                {declaredTags.length === 0 && !cleanAllergyNotes && (
                  <div style={{ fontWeight: 600, color: 'var(--color-muted-fg)' }}>
                    Phụ huynh không khai báo dị ứng thực phẩm nào cho học sinh.
                  </div>
                )}

                {kitchenRemark && (
                  <div style={{ marginTop: 2, paddingTop: 6, borderTop: '1px dashed #FDA4AF', color: '#475569', fontWeight: 700 }}>
                    📝 Ghi chú chế biến bếp: <span style={{ fontWeight: 600 }}>{kitchenRemark}</span>
                  </div>
                )}
              </div>

              <div style={{ display: 'flex', justifyContent: 'flex-end' }}>
                <button
                  type="button"
                  className="btn btn--primary"
                  onClick={() => setSelectedStudentTray(null)}
                  style={{
                    borderRadius: 'var(--radius-full)',
                    border: '2px solid var(--color-fg)',
                    boxShadow: '3px 3px 0 0 var(--color-fg)',
                    fontWeight: 800,
                    padding: '8px 24px',
                    background: 'var(--color-accent)',
                    color: '#FFFFFF',
                  }}
                >
                  Đóng khay ăn
                </button>
              </div>
            </div>
          );
        })()}
      </GeometricModal>
    </div>
  );
}
