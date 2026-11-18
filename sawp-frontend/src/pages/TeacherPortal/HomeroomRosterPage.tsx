import { useEffect, useMemo, useState } from 'react';
import { Link } from 'react-router';
import {
  Calendar,
  CalendarCheck2,
  CheckCircle2,
  ChevronLeft,
  ChevronRight,
  Download,
  Eye,
  Filter,
  GraduationCap,
  Phone,
  Printer,
  RefreshCw,
  Search,
  Soup,
  UtensilsCrossed,
  Users,
} from 'lucide-react';
import toast from 'react-hot-toast';
import { classApi } from '../../services/implementations/classApi';
import { StudentDetailModal } from '../../components/students/StudentDetailModal';
import { lunchApi } from '../../services/implementations/lunchApi';
import type { ClassRosterDto, RosterStudentItemDto } from '../../types/class.types';
import type { StudentMealOperationItem } from '../../types/lunch.types';
import { GeometricModal } from '../../components/common/GeometricModal';

const MEAL_TYPE_BADGE: Record<string, { label: string; cls: string; bg: string; desc: string }> = {
  StandardLunch: { label: '🍱 Tiêu chuẩn', cls: 'badge--mint', bg: '#ECFDF5', desc: 'Suất ăn tiêu chuẩn đầy đủ dinh dưỡng theo thực đơn bếp nhà trường' },
  VegetarianLunch: { label: '🥗 Ăn chay', cls: 'badge--amber', bg: '#FEF3C7', desc: 'Suất ăn chay thanh đạm từ rau củ, nấm và đậu phụ tươi' },
  SpecialDiet: { label: '⚕️ Đặc biệt', cls: 'badge--pink', bg: '#FCE7F3', desc: 'Chế độ ăn kiêng / dị ứng hoặc thức ăn mềm được nhà bếp chế biến riêng' },
  '1': { label: '🍱 Tiêu chuẩn', cls: 'badge--mint', bg: '#ECFDF5', desc: 'Suất ăn tiêu chuẩn đầy đủ dinh dưỡng theo thực đơn bếp nhà trường' },
  '2': { label: '🥗 Ăn chay', cls: 'badge--amber', bg: '#FEF3C7', desc: 'Suất ăn chay thanh đạm từ rau củ, nấm và đậu phụ tươi' },
  '3': { label: '⚕️ Đặc biệt', cls: 'badge--pink', bg: '#FCE7F3', desc: 'Chế độ ăn kiêng / dị ứng hoặc thức ăn mềm được nhà bếp chế biến riêng' },
};

const COURSE_SLOTS = [
  { role: 'Món chính • Tinh bột', emoji: '🍚', bg: '#FEF3C7', accent: '#D97706' },
  { role: 'Món mặn • Giàu đạm', emoji: '🍗', bg: '#FCE7F3', accent: '#DB2777' },
  { role: 'Món xào • Rau củ', emoji: '🥦', bg: '#ECFDF5', accent: '#059669' },
  { role: 'Món canh • Thanh mát', emoji: '🍲', bg: '#EDE9FE', accent: '#7C3AED' },
  { role: 'Tráng miệng • Vitamin', emoji: '🍌', bg: '#FFFDF5', accent: '#EA580C' },
];

const DEFAULT_STANDARD_DISHES = [
  'Cơm trắng dẻo',
  'Thịt gà kho nấm hương',
  'Rau cải ngọt xào tỏi',
  'Canh bí đỏ nấu tôm tươi',
  'Chuối tiêu tráng miệng',
];

function getHomeroomStudentDishes(
  mealType: string | number,
  dailyMenu?: string | null,
  studentRemarks?: string | null
): { dishes: string[]; note: string } {
  const rawRemarks = (studentRemarks || '').trim();
  // Nếu trong DB cột Remarks của học sinh có lưu cụ thể thực đơn + ghi chú (dạng "Món 1, Món 2... | Ghi chú: ...")
  if (rawRemarks.includes('|')) {
    const [menuPart, notePart] = rawRemarks.split('|');
    const customDishes = menuPart.split(',').map((x) => x.trim()).filter(Boolean);
    const customNote = (notePart || '').replace(/^Ghi chú:\s*/i, '').trim();
    if (customDishes.length >= 2) {
      return { dishes: customDishes, note: customNote };
    }
  }

  const cleanNote =
    rawRemarks && rawRemarks !== 'Suất ăn bán trú tiêu chuẩn' && rawRemarks !== 'Suất ăn chay dinh dưỡng'
      ? rawRemarks
      : '';

  if (mealType === 'VegetarianLunch' || mealType === 2 || mealType === '2') {
    return {
      dishes: ['Cơm trắng dẻo', 'Đậu phụ sốt cà chua', 'Nấm đùi gà kho xì dầu', 'Rau củ quả luộc', 'Canh nấm hạt sen'],
      note: cleanNote || 'Suất ăn chay thanh đạm (Không hành tỏi)',
    };
  }
  if (mealType === 'SpecialDiet' || mealType === 3 || mealType === '3') {
    return {
      dishes: ['Cơm mềm / Cháo dinh dưỡng', 'Thịt gà phi lê hấp mềm', 'Rau củ hấp tươi', 'Canh rau ngót thịt băm', 'Sữa chua men sống'],
      note: cleanNote || 'Chế độ ăn kiêng / thức ăn mềm theo dặn dò phụ huynh',
    };
  }

  const cleanMenu = (dailyMenu || '')
    .replace(/^Thực đơn:\s*/i, '')
    .replace(/^Thực đơn Tân Lập A:\s*/i, '')
    .trim();
  const parsed = cleanMenu.split(',').map((x) => x.trim()).filter(Boolean);
  if (parsed.length < 2 || cleanMenu.toLowerCase().includes('phục vụ bán trú bình thường')) {
    return { dishes: DEFAULT_STANDARD_DISHES, note: cleanNote };
  }
  return { dishes: parsed, note: cleanNote };
}

export default function HomeroomRosterPage() {
  const [roster, setRoster] = useState<ClassRosterDto | null>(null);
  const [mealMap, setMealMap] = useState<Record<number, StudentMealOperationItem>>({});
  const [dailyMenuText, setDailyMenuText] = useState<string>('');
  const [loading, setLoading] = useState(false);
  const [detailModalOpen, setDetailModalOpen] = useState(false);
  const [selectedStudentId, setSelectedStudentId] = useState<number | null>(null);
  const [selectedTrayStudent, setSelectedTrayStudent] = useState<{
    student: RosterStudentItemDto;
    meal: StudentMealOperationItem;
  } | null>(null);

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
        const now = new Date();
        const todayStr = `${now.getFullYear()}-${String(now.getMonth() + 1).padStart(2, '0')}-${String(now.getDate()).padStart(2, '0')}`;
        lunchApi
          .getDailyMealBoard(todayStr, res.data.classId)
          .then((board) => {
            const map: Record<number, StudentMealOperationItem> = {};
            (board.studentMeals ?? []).forEach((m) => {
              map[m.studentId] = m;
            });
            setMealMap(map);
            setDailyMenuText(board.serviceRemarks || '');
          })
          .catch(() => {});
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

  const handlePrint = () => {
    window.print();
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

  const handleExportCsv = () => {
    if (!roster) return;
    const rows: string[][] = [
      ['STT', 'Ma hoc sinh', 'Ho va ten', 'Ngay sinh', 'Gioi tinh', 'Phu huynh lien he', 'SDT Phu huynh', 'SDT Khan cap', 'Trang thai'],
    ];

    filteredStudents.forEach((s, idx) => {
      rows.push([
        String(idx + 1),
        s.studentCode,
        s.fullName,
        new Date(s.dateOfBirth).toLocaleDateString('vi-VN'),
        s.gender,
        s.primaryGuardianName || 'Chưa cập nhật',
        s.primaryGuardianPhone || '',
        s.emergencyContactPhone || '',
        s.status,
      ]);
    });

    const csvContent =
      'data:text/csv;charset=utf-8,\uFEFF' +
      rows.map((r) => r.map((c) => `"${(c || '').replace(/"/g, '""')}"`).join(',')).join('\n');
    const encodedUri = encodeURI(csvContent);
    const link = document.createElement('a');
    link.setAttribute('href', encodedUri);
    link.setAttribute('download', `Danh_sach_lop_${roster.className}.csv`);
    document.body.appendChild(link);
    link.click();
    document.body.removeChild(link);
    toast.success('Đã tải xuống file danh sách học sinh!');
  };

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
            <div style={{ display: 'flex', gap: 10, flexWrap: 'wrap', alignItems: 'center' }}>
              <button
                type="button"
                className="btn btn--secondary btn--sm"
                onClick={handlePrint}
                style={{
                  borderRadius: 'var(--radius-full)',
                  border: '2px solid var(--color-fg)',
                  boxShadow: '2px 2px 0 0 var(--color-fg)',
                  fontWeight: 700,
                  padding: '8px 18px',
                }}
              >
                <Printer size={16} /> In danh sách
              </button>
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

            {/* Card 3: Trạng thái xếp lớp */}
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
                  background: '#FEF3C7',
                  border: '2px solid var(--color-fg)',
                  boxShadow: '2px 2px 0 0 var(--color-fg)',
                  display: 'grid',
                  placeItems: 'center',
                  flexShrink: 0,
                }}
              >
                <CheckCircle2 size={24} color="#B45309" strokeWidth={2.5} />
              </div>
              <div>
                <div style={{ fontSize: 'var(--fs-base)', fontFamily: 'var(--font-heading)', fontWeight: 800, color: 'var(--color-fg)' }}>
                  {roster.isPlacementPublished ? 'Đã công bố chính thức' : 'Chưa công bố chính thức'}
                </div>
                <div style={{ fontSize: 'var(--fs-xs)', textTransform: 'uppercase', color: 'var(--color-muted-fg)', fontWeight: 700, marginTop: 2 }}>
                  Trạng thái danh sách lớp
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

            {/* Export CSV button */}
            <button
              type="button"
              className="btn btn--secondary btn--sm"
              onClick={handleExportCsv}
              title="Tải file CSV danh sách học sinh của lớp"
              style={{
                borderRadius: 'var(--radius-full)',
                border: '2px solid var(--color-fg)',
                boxShadow: '2px 2px 0 0 var(--color-fg)',
                fontWeight: 700,
                padding: '6px 14px',
              }}
            >
              <Download size={14} /> Xuất CSV
            </button>
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
            <div style={{ overflowX: 'auto' }}>
              <table style={{ width: '100%', borderCollapse: 'collapse', fontSize: 'var(--fs-sm)' }}>
                <thead>
                  <tr style={{ borderBottom: '2px solid var(--color-fg)', background: '#F8FAFC' }}>
                    <th style={{ padding: '12px 14px', textAlign: 'center', width: 48, fontWeight: 800 }}>STT</th>
                    <th style={{ padding: '12px 14px', textAlign: 'left', width: 110, fontWeight: 800 }}>Mã học sinh</th>
                    <th style={{ padding: '12px 16px', textAlign: 'left', minWidth: 180, fontWeight: 800 }}>Họ và tên</th>
                    <th style={{ padding: '12px 14px', textAlign: 'center', width: 110, fontWeight: 800 }}>Ngày sinh</th>
                    <th style={{ padding: '12px 14px', textAlign: 'center', width: 90, fontWeight: 800 }}>Giới tính</th>
                    <th style={{ padding: '12px 16px', textAlign: 'left', minWidth: 160, fontWeight: 800 }}>Phụ huynh liên hệ</th>
                    <th style={{ padding: '12px 16px', textAlign: 'left', minWidth: 150, fontWeight: 800 }}>SĐT Phụ huynh</th>
                    <th style={{ padding: '12px 16px', textAlign: 'left', minWidth: 160, fontWeight: 800 }}>Chế độ ăn bán trú</th>
                    <th style={{ padding: '12px 14px', textAlign: 'center', width: 120, fontWeight: 800 }}>Trạng thái</th>
                    <th style={{ padding: '12px 14px', textAlign: 'center', width: 110, fontWeight: 800 }}>Hành động</th>
                  </tr>
                </thead>
                <tbody>
                  {pagedStudents.length > 0 ? (
                    pagedStudents.map((s, idx) => {
                      const mealInfo = mealMap[s.studentId];
                      const badgeCfg = mealInfo ? MEAL_TYPE_BADGE[String(mealInfo.mealType)] : null;
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
                        <td style={{ padding: '12px 14px', textAlign: 'center', color: 'var(--color-muted-fg)', fontWeight: 600 }}>
                          {new Date(s.dateOfBirth).toLocaleDateString('vi-VN')}
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
                        <td style={{ padding: '12px 16px' }}>
                          {mealInfo && mealInfo.isActive && badgeCfg ? (
                            <div style={{ display: 'inline-flex', alignItems: 'center', gap: 6, flexWrap: 'nowrap' }}>
                              <span
                                className={`badge ${badgeCfg.cls}`}
                                style={{ border: '1.5px solid var(--color-fg)', fontWeight: 800, whiteSpace: 'nowrap' }}
                              >
                                {badgeCfg.label}
                              </span>
                              <button
                                type="button"
                                onClick={() => setSelectedTrayStudent({ student: s, meal: mealInfo })}
                                title="Xem chi tiết các món trong khay ăn của học sinh"
                                style={{
                                  display: 'inline-flex',
                                  alignItems: 'center',
                                  gap: 5,
                                  padding: '4px 10px',
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
                            </div>
                          ) : mealInfo && !mealInfo.isActive ? (
                            <span className="badge badge--muted" style={{ border: '1px solid var(--color-border)' }}>
                              Đã hủy ăn hôm nay
                            </span>
                          ) : (
                            <span style={{ color: 'var(--color-muted-fg)', fontSize: 'var(--fs-xs)' }}>Không ăn bán trú</span>
                          )}
                        </td>
                        <td style={{ padding: '12px 14px', textAlign: 'center' }}>
                          <span className="badge badge--mint" style={{ border: '1.5px solid var(--color-fg)', fontWeight: 700 }}>
                            {s.status === 'Active' ? 'Đang học' : s.status}
                          </span>
                        </td>
                        <td style={{ padding: '12px 14px', textAlign: 'center' }}>
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

          {/* =============================================================== */}
          {/* MODAL KHAY ĂN BÁN TRÚ HỌC SINH (PLAYFUL GEOMETRIC BENTO TRAY)   */}
          {/* =============================================================== */}
          <GeometricModal
            open={Boolean(selectedTrayStudent)}
            onClose={() => setSelectedTrayStudent(null)}
            title={`Khay Ăn Bán Trú • ${selectedTrayStudent?.student.fullName ?? ''}`}
            subtitle={`Lớp ${roster.className} • Mã HS: ${selectedTrayStudent?.student.studentCode ?? ''} • Thực đơn ngày hôm nay`}
            badgeText={
              selectedTrayStudent
                ? (MEAL_TYPE_BADGE[String(selectedTrayStudent.meal.mealType)]?.label ?? '🍱 Tiêu chuẩn')
                : ''
            }
            badgeColor="mint"
            icon={<Soup size={24} color="var(--color-fg)" strokeWidth={2.5} />}
            iconBg="var(--color-tertiary)"
            maxWidth="md"
          >
            {selectedTrayStudent && (() => {
              const { student, meal } = selectedTrayStudent;
              const badgeCfg = MEAL_TYPE_BADGE[String(meal.mealType)] ?? MEAL_TYPE_BADGE.StandardLunch;
              const { dishes, note } = getHomeroomStudentDishes(meal.mealType, dailyMenuText, meal.remarks);
              return (
                <div style={{ display: 'flex', flexDirection: 'column', gap: 18 }}>
                  {/* 1. Student & Diet Sticker Banner */}
                  <div
                    style={{
                      display: 'flex',
                      alignItems: 'center',
                      justifyContent: 'space-between',
                      flexWrap: 'wrap',
                      gap: 14,
                      padding: '16px 20px',
                      background: badgeCfg.bg,
                      border: '2px solid var(--color-fg)',
                      borderRadius: 'var(--radius-lg)',
                      boxShadow: '4px 4px 0 0 var(--color-fg)',
                    }}
                  >
                    <div style={{ display: 'flex', alignItems: 'center', gap: 14 }}>
                      <div
                        style={{
                          width: 48,
                          height: 48,
                          borderRadius: '50%',
                          background: '#FFFFFF',
                          border: '2px solid var(--color-fg)',
                          boxShadow: '2px 2px 0 0 var(--color-fg)',
                          display: 'grid',
                          placeItems: 'center',
                          fontSize: 22,
                          flexShrink: 0,
                        }}
                      >
                        🍱
                      </div>
                      <div>
                        <div style={{ display: 'flex', alignItems: 'center', gap: 8, flexWrap: 'wrap' }}>
                          <span style={{ fontFamily: 'var(--font-heading)', fontWeight: 800, fontSize: 'var(--fs-base)', color: 'var(--color-fg)' }}>
                            {student.fullName}
                          </span>
                          <span className="badge badge--violet" style={{ border: '1.5px solid var(--color-fg)', fontWeight: 800 }}>
                            {student.studentCode}
                          </span>
                        </div>
                        <div style={{ fontSize: 'var(--fs-xs)', color: 'var(--color-muted-fg)', fontWeight: 600, marginTop: 3 }}>
                          {badgeCfg.desc}
                        </div>
                      </div>
                    </div>
                    <span className={`badge ${badgeCfg.cls}`} style={{ border: '2px solid var(--color-fg)', fontWeight: 800, padding: '6px 14px', fontSize: 'var(--fs-xs)' }}>
                      {badgeCfg.label}
                    </span>
                  </div>

                  {/* 2. Visual Bento Meal Tray (Grid of Dish Cards) */}
                  <div
                    style={{
                      background: '#FFFDF5',
                      border: '2.5px solid var(--color-fg)',
                      borderRadius: 'var(--radius-lg)',
                      boxShadow: '6px 6px 0 0 #E2E8F0',
                      padding: '18px',
                    }}
                  >
                    <div style={{ display: 'flex', alignItems: 'center', justifyContent: 'space-between', marginBottom: 14 }}>
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
                        <UtensilsCrossed size={16} color="var(--color-accent)" strokeWidth={2.5} />
                        Chi tiết khẩu phần trong khay ({dishes.length} món)
                      </span>
                      <span className="badge badge--amber" style={{ border: '1.5px solid var(--color-fg)', fontWeight: 800, fontSize: '11px' }}>
                        Định lượng chuẩn Tiểu học
                      </span>
                    </div>

                    <div
                      style={{
                        display: 'grid',
                        gridTemplateColumns: 'repeat(auto-fit, minmax(195px, 1fr))',
                        gap: 12,
                      }}
                    >
                      {dishes.map((dish, idx) => {
                        const slot = COURSE_SLOTS[idx % COURSE_SLOTS.length];
                        return (
                          <div
                            key={idx}
                            style={{
                              display: 'flex',
                              alignItems: 'center',
                              gap: 12,
                              padding: '12px 14px',
                              background: '#FFFFFF',
                              border: '2px solid var(--color-fg)',
                              borderRadius: 'var(--radius-md)',
                              boxShadow: '3px 3px 0 0 var(--color-fg)',
                              transition: 'transform 0.15s ease',
                            }}
                          >
                            <div
                              style={{
                                width: 42,
                                height: 42,
                                borderRadius: '50%',
                                background: slot.bg,
                                border: '2px solid var(--color-fg)',
                                display: 'grid',
                                placeItems: 'center',
                                fontSize: 20,
                                flexShrink: 0,
                              }}
                            >
                              {slot.emoji}
                            </div>
                            <div style={{ minWidth: 0 }}>
                              <div
                                style={{
                                  fontSize: '10.5px',
                                  fontWeight: 800,
                                  textTransform: 'uppercase',
                                  letterSpacing: '0.04em',
                                  color: slot.accent,
                                }}
                              >
                                {slot.role}
                              </div>
                              <div
                                style={{
                                  fontFamily: 'var(--font-heading)',
                                  fontWeight: 800,
                                  fontSize: 'var(--fs-sm)',
                                  color: 'var(--color-fg)',
                                  marginTop: 2,
                                  lineHeight: 1.3,
                                }}
                              >
                                {dish}
                              </div>
                            </div>
                          </div>
                        );
                      })}
                    </div>
                  </div>

                  {/* 3. Allergy / Parent Note Box (if any) */}
                  <div
                    style={{
                      display: 'flex',
                      alignItems: 'center',
                      justifyContent: 'space-between',
                      padding: '12px 16px',
                      borderRadius: 'var(--radius-md)',
                      border: '2px dashed var(--color-fg)',
                      background: note ? '#FFF1F2' : '#F8FAFC',
                      fontSize: 'var(--fs-xs)',
                    }}
                  >
                    <div>
                      <strong style={{ color: note ? '#BE123C' : 'var(--color-fg)' }}>
                        📝 Ghi chú dị ứng / Dặn dò từ phụ huynh:{' '}
                      </strong>
                      <span style={{ fontWeight: 600, color: note ? '#9F1239' : 'var(--color-muted-fg)' }}>
                        {note || 'Không có ghi chú dị ứng đặc biệt.'}
                      </span>
                    </div>
                  </div>

                  <div style={{ display: 'flex', justifyContent: 'flex-end' }}>
                    <button
                      type="button"
                      className="btn btn--primary"
                      onClick={() => setSelectedTrayStudent(null)}
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
        </>
      )}

      {selectedStudentId && (
        <StudentDetailModal
          open={detailModalOpen}
          studentId={selectedStudentId}
          onClose={() => setDetailModalOpen(false)}
        />
      )}
    </div>
  );
}
