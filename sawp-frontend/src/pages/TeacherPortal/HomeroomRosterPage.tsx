import { useEffect, useMemo, useState } from 'react';
import { Link } from 'react-router';
import {
  Calendar,
  CalendarCheck2,
  ChevronLeft,
  ChevronRight,
  Eye,
  Filter,
  GraduationCap,
  Phone,
  Printer,
  RefreshCw,
  Search,
  Soup,
  Users,
  UtensilsCrossed,
} from 'lucide-react';
import toast from 'react-hot-toast';
import { classApi } from '../../services/implementations/classApi';
import { lunchApi } from '../../services/implementations/lunchApi';
import { StudentDetailModal } from '../../components/students/StudentDetailModal';
import { GeometricModal } from '../../components/common/GeometricModal';
import type { ClassRosterDto } from '../../types/class.types';
import type { DailyMealBoard, StudentMealOperationItem } from '../../types/lunch.types';
import { formatDDMMYYYY, todayIso } from '../../utils/date';
import { normalizeMealTypeNumber } from '../../constants/allergens';

const MEAL_BADGE_MAP: Record<string, { label: string; cls: string }> = {
  StandardLunch: { label: 'Tiêu chuẩn', cls: 'badge--mint' },
  VegetarianLunch: { label: 'Ăn chay', cls: 'badge--amber' },
  SpecialDiet: { label: 'Đặc biệt', cls: 'badge--pink' },
  '1': { label: 'Tiêu chuẩn', cls: 'badge--mint' },
  '2': { label: 'Ăn chay', cls: 'badge--amber' },
  '3': { label: 'Đặc biệt', cls: 'badge--pink' },
};

export default function HomeroomRosterPage() {
  const [roster, setRoster] = useState<ClassRosterDto | null>(null);
  const [mealBoard, setMealBoard] = useState<DailyMealBoard | null>(null);
  const [trayModalStudent, setTrayModalStudent] = useState<StudentMealOperationItem | null>(null);
  const [loading, setLoading] = useState(false);
  const [detailModalOpen, setDetailModalOpen] = useState(false);
  const [selectedStudentId, setSelectedStudentId] = useState<number | null>(null);

  // Search & Filter state
  const [searchQuery, setSearchQuery] = useState('');
  const [genderFilter, setGenderFilter] = useState<'ALL' | 'Nam' | 'Nữ'>('ALL');
  const [pageIndex, setPageIndex] = useState(1);
  const [pageSize, setPageSize] = useState(10);

  useEffect(() => {
    loadMyClass();
  }, []);

  const loadMyClass = async () => {
    setLoading(true);
    try {
      const res = await classApi.getMyHomeroomClass();
      if (res.success && res.data) {
        setRoster(res.data);
        try {
          const board = await lunchApi.getDailyMealBoard(todayIso(), res.data.classId);
          setMealBoard(board);
        } catch {
          setMealBoard(null);
        }
      }
    } catch (err: any) {
      if (err.response?.status === 404) {
        setRoster(null);
      } else {
        toast.error('Lỗi khi tải danh sách lớp chủ nhiệm.');
      }
    } finally {
      setLoading(false);
    }
  };

  const mealByStudentId = useMemo(() => {
    const map = new Map<number, StudentMealOperationItem>();
    for (const item of mealBoard?.studentMeals ?? []) {
      map.set(item.studentId, item);
    }
    return map;
  }, [mealBoard]);

  const getTrayDishes = (meal: StudentMealOperationItem): string[] => {
    const mt = normalizeMealTypeNumber(meal.mealType);
    const structured =
      mt === 2
        ? mealBoard?.menus?.vegetarian
        : mt === 3
        ? mealBoard?.menus?.special
        : mealBoard?.menus?.standard;
    if (structured && structured.length > 0) {
      return structured.map((d) => d.name);
    }
    if (mealBoard?.menus?.standard && mealBoard.menus.standard.length > 0) {
      return mealBoard.menus.standard.map((d) => d.name);
    }
    return [];
  };

  const totalCount = roster?.totalEnrolled ?? roster?.totalStudents ?? roster?.students?.length ?? 0;
  const yearLabel = roster?.academicYear || roster?.schoolYearName || '2026 - 2027';
  const maleCount = roster?.students?.filter((s) => s.gender === 'Nam').length ?? 0;
  const femaleCount = roster?.students?.filter((s) => s.gender === 'Nữ').length ?? 0;

  // Filter students
  const filteredStudents = useMemo(() => {
    const list = roster?.students ?? [];
    return list.filter((s) => {
      if (genderFilter !== 'ALL' && s.gender !== genderFilter) return false;
      if (searchQuery.trim()) {
        const q = searchQuery.toLowerCase().trim();
        const matchName = s.fullName.toLowerCase().includes(q);
        const matchCode = s.studentCode.toLowerCase().includes(q);
        const matchGuardian = (s.primaryGuardianName || '').toLowerCase().includes(q);
        const matchPhone = (s.primaryGuardianPhone || '').includes(q);
        const matchEmergency = (s.emergencyContactPhone || '').includes(q);
        if (!matchName && !matchCode && !matchGuardian && !matchPhone && !matchEmergency) return false;
      }
      return true;
    });
  }, [roster?.students, genderFilter, searchQuery]);

  useEffect(() => {
    setPageIndex(1);
  }, [genderFilter, searchQuery, pageSize]);

  const totalPages = Math.max(1, Math.ceil(filteredStudents.length / pageSize));
  const pagedStudents = useMemo(() => {
    const start = (pageIndex - 1) * pageSize;
    return filteredStudents.slice(start, start + pageSize);
  }, [filteredStudents, pageIndex, pageSize]);

  return (
    <div className="container pop-in" style={{ paddingBottom: 60 }}>
      {/* Page Header */}
      <header className="page-header" style={{ marginBottom: 24 }}>
        <div style={{ display: 'flex', justifyContent: 'space-between', alignItems: 'flex-start', flexWrap: 'wrap', gap: 16 }}>
          <div>
            <div style={{ display: 'flex', gap: 8, alignItems: 'center', marginBottom: 8, flexWrap: 'wrap' }}>
              <span className="badge badge--violet" style={{ border: '2px solid var(--color-fg)', padding: '5px 14px', borderRadius: 'var(--radius-full)', fontWeight: 800 }}>
                <GraduationCap size={15} /> Cổng Giáo Viên Chủ Nhiệm
              </span>
              <span className="badge badge--amber" style={{ border: '2px solid var(--color-fg)', padding: '5px 14px', borderRadius: 'var(--radius-full)', fontWeight: 800 }}>
                <Calendar size={14} /> Năm học {yearLabel}
              </span>
            </div>
            <h1 style={{ margin: '4px 0 8px' }}>
              Danh Sách <span className="squiggle">Lớp Chủ Nhiệm</span>
            </h1>
            <p style={{ color: 'var(--color-muted-fg)', margin: 0, fontSize: 'var(--fs-base)' }}>
              Xem danh sách chính thức học sinh của lớp, thông tin liên lạc phụ huynh và hồ sơ chuyên cần.
            </p>
          </div>

          {roster && (
            <div className="no-print" style={{ display: 'flex', gap: 10, flexWrap: 'wrap', alignItems: 'center' }}>
              <button
                type="button"
                className="btn btn--secondary btn--sm"
                onClick={() => window.print()}
                style={{
                  borderRadius: 'var(--radius-full)',
                  border: '2px solid var(--color-fg)',
                  boxShadow: '3px 3px 0 0 var(--color-fg)',
                  fontWeight: 800,
                  padding: '8px 18px',
                  display: 'inline-flex',
                  alignItems: 'center',
                  gap: 6,
                }}
              >
                <Printer size={15} /> In danh sách
              </button>
              <Link
                to="/teacher/boarding"
                className="btn btn--secondary btn--sm"
                style={{
                  textDecoration: 'none',
                  borderRadius: 'var(--radius-full)',
                  border: '2px solid var(--color-fg)',
                  boxShadow: '3px 3px 0 0 var(--color-fg)',
                  fontWeight: 800,
                  padding: '8px 18px',
                  display: 'inline-flex',
                  alignItems: 'center',
                  gap: 6,
                }}
              >
                <Soup size={15} /> Bán trú lớp
              </Link>
              <Link
                to="/teacher/attendance"
                className="btn btn--primary btn--sm"
                style={{
                  textDecoration: 'none',
                  borderRadius: 'var(--radius-full)',
                  border: '2px solid var(--color-fg)',
                  boxShadow: '4px 4px 0 0 var(--color-fg)',
                  fontWeight: 800,
                  padding: '8px 22px',
                  background: 'var(--color-accent)',
                  color: '#FFFFFF',
                  display: 'inline-flex',
                  alignItems: 'center',
                  gap: 8,
                }}
              >
                <CalendarCheck2 size={16} /> Điểm danh ngay
              </Link>
            </div>
          )}
        </div>
      </header>

      {loading ? (
        <div className="card" style={{ textAlign: 'center', padding: 48, border: '2px solid var(--color-fg)', boxShadow: '4px 4px 0 0 var(--color-fg)', borderRadius: 'var(--radius-lg)' }}>
          <RefreshCw size={32} className="spin" color="var(--color-accent)" style={{ margin: '0 auto 12px' }} />
          <div style={{ fontWeight: 700 }}>Đang tải danh sách lớp của bạn...</div>
        </div>
      ) : !roster ? (
        <div className="card empty-state" style={{ textAlign: 'center', padding: 48, border: '2px solid var(--color-fg)', boxShadow: '4px 4px 0 0 var(--color-fg)', borderRadius: 'var(--radius-lg)' }}>
          <GraduationCap size={48} color="var(--color-muted-fg)" style={{ margin: '0 auto 12px' }} />
          <h3>Bạn chưa được phân công làm Chủ nhiệm lớp nào</h3>
          <p style={{ color: 'var(--color-muted-fg)', maxWidth: 460, margin: '0 auto 16px' }}>
            Giáo viên chỉ có quyền truy xuất danh sách của lớp mà mình được phân công chủ nhiệm. Vui lòng liên hệ Ban Giáo vụ để được phân công lớp.
          </p>
          <button type="button" className="btn btn--secondary btn--sm" onClick={loadMyClass} style={{ borderRadius: 'var(--radius-full)' }}>
            <RefreshCw size={15} /> Kiểm tra lại
          </button>
        </div>
      ) : (
        <>
          {/* Top 3 Class KPI Cards */}
          <div
            style={{
              display: 'grid',
              gridTemplateColumns: 'repeat(auto-fit, minmax(260px, 1fr))',
              gap: 16,
              marginBottom: 24,
            }}
          >
            {/* Card 1: Thông tin Lớp */}
            <div
              className="card"
              style={{
                background: '#FFFDF5',
                border: '2px solid var(--color-fg)',
                boxShadow: '4px 4px 0 0 var(--color-fg)',
                borderRadius: 'var(--radius-lg)',
                padding: '20px 22px',
                display: 'flex',
                alignItems: 'center',
                gap: 16,
              }}
            >
              <div
                style={{
                  width: 50,
                  height: 50,
                  borderRadius: '50%',
                  background: '#EDE9FE',
                  border: '2px solid var(--color-fg)',
                  boxShadow: '2px 2px 0 0 var(--color-fg)',
                  display: 'grid',
                  placeItems: 'center',
                  flexShrink: 0,
                }}
              >
                <GraduationCap size={24} color="var(--color-accent)" strokeWidth={2.5} />
              </div>
              <div>
                <div style={{ fontSize: 'var(--fs-2xl)', fontFamily: 'var(--font-heading)', fontWeight: 800, color: 'var(--color-fg)' }}>
                  Lớp {roster.className}
                </div>
                <div style={{ fontSize: 'var(--fs-xs)', textTransform: 'uppercase', color: 'var(--color-muted-fg)', fontWeight: 700, marginTop: 2 }}>
                  Khối {roster.grade} • Sức chứa {roster.capacity} học sinh
                </div>
              </div>
            </div>

            {/* Card 2: Hero Metric - Sĩ số Học sinh */}
            <div
              className="card"
              style={{
                background: '#ECFDF5',
                border: '2.5px solid #059669',
                boxShadow: '4px 4px 0 0 var(--color-fg)',
                borderRadius: 'var(--radius-lg)',
                padding: '20px 22px',
                display: 'flex',
                alignItems: 'center',
                gap: 16,
              }}
            >
              <div
                style={{
                  width: 50,
                  height: 50,
                  borderRadius: '50%',
                  background: '#A7F3D0',
                  border: '2px solid var(--color-fg)',
                  boxShadow: '2px 2px 0 0 var(--color-fg)',
                  display: 'grid',
                  placeItems: 'center',
                  flexShrink: 0,
                }}
              >
                <Users size={24} color="#047857" strokeWidth={2.5} />
              </div>
              <div>
                <div style={{ fontSize: 'var(--fs-2xl)', fontFamily: 'var(--font-heading)', fontWeight: 900, color: '#065F46' }}>
                  {totalCount} học sinh
                </div>
                <div style={{ fontSize: 'var(--fs-xs)', textTransform: 'uppercase', color: '#047857', fontWeight: 800, marginTop: 2 }}>
                  ✓ {maleCount} Nam • {femaleCount} Nữ
                </div>
              </div>
            </div>
          </div>

          {/* Search & Filter Toolbar */}
          <div
            style={{
              display: 'flex',
              gap: 12,
              flexWrap: 'wrap',
              alignItems: 'center',
              padding: '14px 18px',
              background: '#FFFDF5',
              borderRadius: 'var(--radius-md)',
              marginBottom: 20,
              border: '2px solid var(--color-fg)',
              boxShadow: '2px 2px 0 0 var(--color-fg)',
            }}
          >
            {/* Search Input */}
            <div style={{ position: 'relative', flex: 1, minWidth: 220 }}>
              <Search
                size={16}
                color="var(--color-muted-fg)"
                style={{ position: 'absolute', left: 12, top: '50%', transform: 'translateY(-50%)' }}
              />
              <input
                type="text"
                className="input"
                placeholder="Tìm tên, mã học sinh, tên phụ huynh, SĐT..."
                value={searchQuery}
                onChange={(e) => setSearchQuery(e.target.value)}
                style={{
                  paddingLeft: 36,
                  fontSize: 'var(--fs-sm)',
                  borderRadius: 'var(--radius-full)',
                  border: '2px solid var(--color-fg)',
                }}
              />
            </div>

            {/* Gender filter */}
            <div style={{ display: 'flex', alignItems: 'center', gap: 6 }}>
              <Filter size={15} color="var(--color-muted-fg)" />
              <select
                className="select"
                value={genderFilter}
                onChange={(e) => setGenderFilter(e.target.value as 'ALL' | 'Nam' | 'Nữ')}
                style={{
                  minWidth: 150,
                  padding: '6px 14px',
                  fontSize: 'var(--fs-sm)',
                  borderRadius: 'var(--radius-full)',
                  border: '2px solid var(--color-fg)',
                }}
              >
                <option value="ALL">Tất cả giới tính</option>
                <option value="Nam">Chỉ học sinh Nam</option>
                <option value="Nữ">Chỉ học sinh Nữ</option>
              </select>
            </div>

            {/* Page Size */}
            <select
              className="select"
              value={pageSize}
              onChange={(e) => setPageSize(Number(e.target.value))}
              style={{
                minWidth: 130,
                padding: '6px 12px',
                fontSize: 'var(--fs-sm)',
                borderRadius: 'var(--radius-full)',
                border: '2px solid var(--color-fg)',
              }}
            >
              <option value={10}>10 dòng / trang</option>
              <option value={20}>20 dòng / trang</option>
              <option value={50}>50 dòng / trang</option>
              <option value={100}>Tất cả cả lớp</option>
            </select>
          </div>

          {/* Quick summary badges */}
          <div style={{ display: 'flex', gap: 8, marginBottom: 16, alignItems: 'center', flexWrap: 'wrap' }}>
            <span className="badge badge--violet" style={{ border: '1.5px solid var(--color-fg)', fontWeight: 700 }}>
              Tìm thấy: <strong>{filteredStudents.length}</strong> / <strong>{totalCount}</strong> học sinh
            </span>
            <span className="badge badge--mint" style={{ border: '1.5px solid var(--color-fg)', fontWeight: 700 }}>
              Nam: <strong>{filteredStudents.filter((s) => s.gender === 'Nam').length}</strong>
            </span>
            <span className="badge badge--pink" style={{ border: '1.5px solid var(--color-fg)', fontWeight: 700 }}>
              Nữ: <strong>{filteredStudents.filter((s) => s.gender === 'Nữ').length}</strong>
            </span>
            {searchQuery && (
              <span className="badge badge--amber" style={{ border: '1.5px solid var(--color-fg)' }}>
                Từ khóa: &quot;{searchQuery}&quot;
              </span>
            )}
          </div>

          {/* Official Roster Data Table Card */}
          <div
            className="card pop-in"
            style={{
              border: '2px solid var(--color-fg)',
              boxShadow: '4px 4px 0 0 var(--color-fg)',
              borderRadius: 'var(--radius-lg)',
              padding: 0,
              overflow: 'hidden',
              marginBottom: 24,
            }}
          >
            <div className="desktop-table-wrap" style={{ overflowX: 'auto' }}>
              <table style={{ width: '100%', minWidth: 1080, borderCollapse: 'collapse', fontSize: 'var(--fs-sm)' }}>
                <thead>
                  <tr style={{ borderBottom: '2px solid var(--color-fg)', background: '#F8FAFC' }}>
                    <th style={{ padding: '12px 12px', textAlign: 'center', width: 52, whiteSpace: 'nowrap', fontWeight: 800 }}>STT</th>
                    <th style={{ padding: '12px 14px', textAlign: 'left', width: 112, whiteSpace: 'nowrap', fontWeight: 800 }}>Mã học sinh</th>
                    <th style={{ padding: '12px 16px', textAlign: 'left', minWidth: 180, whiteSpace: 'nowrap', fontWeight: 800 }}>Họ và tên</th>
                    <th style={{ padding: '12px 14px', textAlign: 'center', minWidth: 115, whiteSpace: 'nowrap', fontWeight: 800 }}>Ngày sinh</th>
                    <th style={{ padding: '12px 14px', textAlign: 'center', minWidth: 88, whiteSpace: 'nowrap', fontWeight: 800 }}>Giới tính</th>
                    <th style={{ padding: '12px 16px', textAlign: 'left', minWidth: 165, whiteSpace: 'nowrap', fontWeight: 800 }}>Phụ huynh liên hệ</th>
                    <th style={{ padding: '12px 16px', textAlign: 'left', minWidth: 145, whiteSpace: 'nowrap', fontWeight: 800 }}>SĐT Phụ huynh</th>
                    <th style={{ padding: '12px 14px', textAlign: 'center', minWidth: 115, whiteSpace: 'nowrap', fontWeight: 800 }}>Trạng thái</th>
                    <th style={{ padding: '12px 14px', textAlign: 'center', minWidth: 145, whiteSpace: 'nowrap', fontWeight: 800 }}>Bán trú hôm nay</th>
                    <th className="no-print" style={{ padding: '12px 14px', textAlign: 'center', minWidth: 115, whiteSpace: 'nowrap', fontWeight: 800 }}>Hành động</th>
                  </tr>
                </thead>
                <tbody>
                  {pagedStudents.length > 0 ? (
                    pagedStudents.map((s, idx) => {
                      const meal = mealByStudentId.get(s.studentId);
                      const mealBadge = meal ? (MEAL_BADGE_MAP[String(meal.mealType)] ?? MEAL_BADGE_MAP.StandardLunch) : null;
                      return (
                      <tr
                        key={s.studentId}
                        style={{ borderBottom: '1px solid var(--color-border)', transition: 'background 0.15s' }}
                        onMouseEnter={(e) => (e.currentTarget.style.background = '#FFFDF5')}
                        onMouseLeave={(e) => (e.currentTarget.style.background = 'transparent')}
                      >
                        <td style={{ padding: '12px 14px', textAlign: 'center', fontWeight: 600 }}>
                          {(pageIndex - 1) * pageSize + idx + 1}
                        </td>
                        <td style={{ padding: '12px 14px' }}>
                          <button
                            type="button"
                            onClick={() => {
                              setSelectedStudentId(s.studentId);
                              setDetailModalOpen(true);
                            }}
                            style={{ background: 'none', border: 'none', padding: 0, cursor: 'pointer' }}
                            title="Bấm để xem hồ sơ học sinh"
                          >
                            <span className="badge badge--violet" style={{ border: '1.5px solid var(--color-fg)', fontWeight: 800 }}>
                              {s.studentCode}
                            </span>
                          </button>
                        </td>
                        <td style={{ padding: '12px 16px' }}>
                          <button
                            type="button"
                            onClick={() => {
                              setSelectedStudentId(s.studentId);
                              setDetailModalOpen(true);
                            }}
                            style={{
                              background: 'none',
                              border: 'none',
                              padding: 0,
                              cursor: 'pointer',
                              textAlign: 'left',
                            }}
                            title="Bấm để xem hồ sơ học sinh"
                          >
                            <strong style={{ fontSize: 'var(--fs-sm)', color: 'var(--color-fg)' }}>
                              {s.fullName}
                            </strong>
                          </button>
                        </td>
                        <td style={{ padding: '12px 14px', textAlign: 'center', color: 'var(--color-muted-fg)', fontWeight: 600, whiteSpace: 'nowrap' }}>
                          {formatDDMMYYYY(s.dateOfBirth)}
                        </td>
                        <td style={{ padding: '12px 14px', textAlign: 'center' }}>
                          {s.gender === 'Nam' ? (
                            <span className="badge badge--mint" style={{ border: '1px solid #059669', padding: '2px 8px' }}>
                              Nam
                            </span>
                          ) : (
                            <span className="badge badge--pink" style={{ border: '1px solid #E11D48', padding: '2px 8px' }}>
                              Nữ
                            </span>
                          )}
                        </td>
                        <td style={{ padding: '12px 16px', fontWeight: 600 }}>
                          {s.primaryGuardianName || <span style={{ color: 'var(--color-muted-fg)' }}>Chưa cập nhật</span>}
                        </td>
                        <td style={{ padding: '12px 16px' }}>
                          {s.primaryGuardianPhone ? (
                            <a
                              href={`tel:${s.primaryGuardianPhone}`}
                              style={{
                                color: 'var(--color-accent)',
                                textDecoration: 'none',
                                fontWeight: 700,
                                display: 'inline-flex',
                                alignItems: 'center',
                                gap: 4,
                                padding: '3px 8px',
                                background: '#EDE9FE',
                                borderRadius: 'var(--radius-full)',
                                border: '1px solid var(--color-fg)',
                              }}
                            >
                              <Phone size={12} /> {s.primaryGuardianPhone}
                            </a>
                          ) : (
                            <span style={{ color: 'var(--color-muted-fg)' }}>—</span>
                          )}
                        </td>
                        <td style={{ padding: '12px 14px', textAlign: 'center' }}>
                          <span className="badge badge--mint" style={{ border: '1.5px solid var(--color-fg)', fontWeight: 700 }}>
                            {s.status === 'Active' ? 'Đang học' : s.status}
                          </span>
                        </td>
                        <td style={{ padding: '12px 14px', textAlign: 'center' }}>
                          {meal && meal.isActive ? (
                            <div style={{ display: 'inline-flex', flexDirection: 'column', alignItems: 'center', gap: 4 }}>
                              <span className={`badge ${mealBadge?.cls ?? 'badge--mint'}`} style={{ border: '1px solid var(--color-fg)', fontWeight: 700 }}>
                                {mealBadge?.label ?? 'Tiêu chuẩn'}
                              </span>
                              <button
                                type="button"
                                className="btn btn--secondary btn--sm no-print"
                                style={{ padding: '2px 8px', fontSize: 11, minHeight: 22 }}
                                onClick={() => setTrayModalStudent(meal)}
                              >
                                <UtensilsCrossed size={11} /> Xem khay ăn
                              </button>
                            </div>
                          ) : meal ? (
                            <span className="badge badge--pink" style={{ fontSize: 11 }}>Đã hủy suất</span>
                          ) : (
                            <span style={{ color: 'var(--color-muted-fg)', fontSize: 'var(--fs-xs)' }}>Chưa ĐK</span>
                          )}
                        </td>
                        <td className="no-print" style={{ padding: '12px 14px', textAlign: 'center' }}>
                          <button
                            type="button"
                            className="btn btn--secondary btn--sm"
                            style={{
                              padding: '4px 10px',
                              fontSize: 12,
                              borderRadius: 'var(--radius-full)',
                              border: '1.5px solid var(--color-fg)',
                              gap: 4,
                            }}
                            onClick={() => {
                              setSelectedStudentId(s.studentId);
                              setDetailModalOpen(true);
                            }}
                            title="Xem chi tiết hồ sơ học sinh (chế độ chỉ đọc BR-07)"
                          >
                            <Eye size={13} /> Chi tiết
                          </button>
                        </td>
                      </tr>
                    );
                  })
                  ) : (
                    <tr>
                      <td colSpan={10} style={{ textAlign: 'center', padding: '36px', color: 'var(--color-muted-fg)' }}>
                        Không tìm thấy học sinh nào phù hợp với bộ lọc tìm kiếm.
                      </td>
                    </tr>
                  )}
                </tbody>
              </table>
            </div>

            <div className="mobile-card-list" style={{ padding: 12 }}>
              {pagedStudents.length > 0 ? (
                pagedStudents.map((s, idx) => (
                    <div key={`m-${s.studentId}`} className="mobile-item-card">
                      <div className="mobile-item-card__head">
                        <div style={{ minWidth: 0 }}>
                          <div style={{ display: 'flex', alignItems: 'center', gap: 6, flexWrap: 'wrap' }}>
                            <span style={{ fontWeight: 800, fontSize: 'var(--fs-xs)', color: 'var(--color-muted-fg)' }}>
                              #{(pageIndex - 1) * pageSize + idx + 1}
                            </span>
                            <strong style={{ fontSize: 'var(--fs-sm)', color: 'var(--color-fg)' }}>{s.fullName}</strong>
                          </div>
                          <div className="mobile-item-card__meta" style={{ marginTop: 4 }}>
                            <span className="badge badge--violet" style={{ border: '1px solid var(--color-fg)', fontWeight: 700, padding: '1px 6px' }}>
                              {s.studentCode}
                            </span>
                            <span>{formatDDMMYYYY(s.dateOfBirth)}</span>
                            <span>•</span>
                            <span>{s.gender}</span>
                          </div>
                        </div>
                        <span className="badge badge--mint" style={{ border: '1.5px solid var(--color-fg)', fontWeight: 700 }}>
                          {s.status === 'Active' ? 'Đang học' : s.status}
                        </span>
                      </div>

                      <div style={{ fontSize: 'var(--fs-xs)', display: 'flex', flexWrap: 'wrap', gap: 8, alignItems: 'center' }}>
                        <span>PH: <strong>{s.primaryGuardianName || 'Chưa cập nhật'}</strong></span>
                        {s.primaryGuardianPhone && (
                          <a
                            href={`tel:${s.primaryGuardianPhone}`}
                            style={{
                              color: 'var(--color-accent)',
                              textDecoration: 'none',
                              fontWeight: 700,
                              display: 'inline-flex',
                              alignItems: 'center',
                              gap: 4,
                              padding: '2px 8px',
                              background: '#EDE9FE',
                              borderRadius: 'var(--radius-full)',
                              border: '1px solid var(--color-fg)',
                            }}
                          >
                            <Phone size={12} /> {s.primaryGuardianPhone}
                          </a>
                        )}
                      </div>

                      <div className="mobile-item-card__foot" style={{ justifyContent: 'flex-end' }}>
                        <button
                          type="button"
                          className="btn btn--secondary btn--sm"
                          style={{ padding: '4px 10px', fontSize: 12 }}
                          onClick={() => {
                            setSelectedStudentId(s.studentId);
                            setDetailModalOpen(true);
                          }}
                        >
                          <Eye size={13} /> Chi tiết
                        </button>
                      </div>
                    </div>
                ))
              ) : (
                <div style={{ textAlign: 'center', padding: 24, color: 'var(--color-muted-fg)' }}>
                  Không tìm thấy học sinh nào phù hợp với bộ lọc tìm kiếm.
                </div>
              )}
            </div>

            {/* Pagination Controls */}
            {filteredStudents.length > 0 && (
              <div
                style={{
                  display: 'flex',
                  alignItems: 'center',
                  justifyContent: 'space-between',
                  padding: '12px 18px',
                  borderTop: '2px solid var(--color-fg)',
                  background: '#F8FAFC',
                  fontSize: 'var(--fs-sm)',
                  flexWrap: 'wrap',
                  gap: 12,
                }}
              >
                <div>
                  Hiển thị <strong>{pagedStudents.length}</strong> / <strong>{filteredStudents.length}</strong> học sinh
                  <span style={{ color: 'var(--color-muted-fg)', marginLeft: 8 }}>
                    (Bản ghi {(pageIndex - 1) * pageSize + 1} – {Math.min(pageIndex * pageSize, filteredStudents.length)})
                  </span>
                </div>
                <div style={{ display: 'flex', alignItems: 'center', gap: 8 }}>
                  <button
                    type="button"
                    className="btn btn--secondary btn--sm"
                    disabled={pageIndex <= 1}
                    onClick={() => setPageIndex((p) => Math.max(1, p - 1))}
                    style={{
                      borderRadius: 'var(--radius-full)',
                      border: '1.5px solid var(--color-fg)',
                      boxShadow: pageIndex > 1 ? '2px 2px 0 0 var(--color-fg)' : 'none',
                      fontWeight: 700,
                    }}
                  >
                    <ChevronLeft size={14} /> Trang trước
                  </button>
                  <span className="badge badge--violet" style={{ alignSelf: 'center', padding: '4px 12px', border: '1.5px solid var(--color-fg)', fontWeight: 700 }}>
                    Trang {pageIndex} / {totalPages}
                  </span>
                  <button
                    type="button"
                    className="btn btn--secondary btn--sm"
                    disabled={pageIndex >= totalPages}
                    onClick={() => setPageIndex((p) => Math.min(totalPages, p + 1))}
                    style={{
                      borderRadius: 'var(--radius-full)',
                      border: '1.5px solid var(--color-fg)',
                      boxShadow: pageIndex < totalPages ? '2px 2px 0 0 var(--color-fg)' : 'none',
                      fontWeight: 700,
                    }}
                  >
                    Trang sau <ChevronRight size={14} />
                  </button>
                </div>
              </div>
            )}
          </div>
        </>
      )}

      {selectedStudentId && (
        <StudentDetailModal
          open={detailModalOpen}
          studentId={selectedStudentId}
          onClose={() => setDetailModalOpen(false)}
        />
      )}

      {trayModalStudent && (
        <GeometricModal
          open={!!trayModalStudent}
          onClose={() => setTrayModalStudent(null)}
          title={`Khay ăn: ${trayModalStudent.studentName}`}
          badgeText={trayModalStudent.studentCode}
          badgeColor="amber"
          icon={<UtensilsCrossed size={22} color="#1E293B" strokeWidth={2.5} />}
          iconBg="var(--color-tertiary-soft)"
          maxWidth="md"
        >
          <div style={{ display: 'flex', flexDirection: 'column', gap: 12 }}>
            <div style={{ display: 'flex', alignItems: 'center', gap: 8, flexWrap: 'wrap' }}>
              <span className={`badge ${(MEAL_BADGE_MAP[String(trayModalStudent.mealType)] ?? MEAL_BADGE_MAP.StandardLunch).cls}`}>
                {(MEAL_BADGE_MAP[String(trayModalStudent.mealType)] ?? MEAL_BADGE_MAP.StandardLunch).label}
              </span>
              {trayModalStudent.studentAllergyNotes && (
                <span className="badge badge--pink">Dị ứng: {trayModalStudent.studentAllergyNotes}</span>
              )}
            </div>
            <div>
              <div style={{ fontWeight: 800, fontSize: 13, marginBottom: 6 }}>Thực đơn khay ăn hôm nay:</div>
              {getTrayDishes(trayModalStudent).length > 0 ? (
                <ul style={{ margin: 0, paddingLeft: 20, display: 'flex', flexDirection: 'column', gap: 4 }}>
                  {getTrayDishes(trayModalStudent).map((dish, idx) => (
                    <li key={idx}>{dish}</li>
                  ))}
                </ul>
              ) : (
                <div style={{ color: 'var(--color-muted-fg)', fontSize: 'var(--fs-sm)' }}>
                  Chưa có thực đơn chi tiết cho ngày hôm nay.
                </div>
              )}
            </div>
            {trayModalStudent.remarks && (
              <div style={{ fontSize: 'var(--fs-xs)', color: 'var(--color-muted-fg)' }}>
                Ghi chú: {trayModalStudent.remarks}
              </div>
            )}
            <div style={{ display: 'flex', justifyContent: 'flex-end' }}>
              <button type="button" className="btn btn--secondary" onClick={() => setTrayModalStudent(null)}>
                Đóng
              </button>
            </div>
          </div>
        </GeometricModal>
      )}

      <style>{`
        @media print {
          .no-print, .sidebar, .topbar, nav, button.btn { display: none !important; }
          .main-content { margin-left: 0 !important; padding: 0 !important; }
          .card { box-shadow: none !important; border-color: #cbd5e1 !important; }
        }
      `}</style>
    </div>
  );
}
