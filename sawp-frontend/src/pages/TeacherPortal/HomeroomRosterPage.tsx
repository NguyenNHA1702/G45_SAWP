import { useEffect, useMemo, useState } from 'react';
import { Link } from 'react-router';
import {
  Calendar,
  CalendarCheck2,
  CheckCircle2,
  ChevronLeft,
  ChevronRight,
  Download,
  Filter,
  GraduationCap,
  Phone,
  Printer,
  RefreshCw,
  Search,
  Users,
} from 'lucide-react';
import toast from 'react-hot-toast';
import { classApi } from '../../services/implementations/classApi';
import type { ClassRosterDto } from '../../types/class.types';

export default function HomeroomRosterPage() {
  const [roster, setRoster] = useState<ClassRosterDto | null>(null);
  const [loading, setLoading] = useState(false);

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
                    <th style={{ padding: '12px 16px', textAlign: 'left', minWidth: 130, fontWeight: 800 }}>SĐT Khẩn cấp</th>
                    <th style={{ padding: '12px 14px', textAlign: 'center', width: 120, fontWeight: 800 }}>Trạng thái</th>
                  </tr>
                </thead>
                <tbody>
                  {pagedStudents.length > 0 ? (
                    pagedStudents.map((s, idx) => (
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
                          <span className="badge badge--violet" style={{ border: '1.5px solid var(--color-fg)', fontWeight: 800 }}>
                            {s.studentCode}
                          </span>
                        </td>
                        <td style={{ padding: '12px 16px' }}>
                          <strong style={{ fontSize: 'var(--fs-sm)', color: 'var(--color-fg)' }}>{s.fullName}</strong>
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
                        <td style={{ padding: '12px 16px', color: 'var(--color-fg)', fontWeight: 600 }}>
                          {s.emergencyContactPhone || <span style={{ color: 'var(--color-muted-fg)' }}>—</span>}
                        </td>
                        <td style={{ padding: '12px 14px', textAlign: 'center' }}>
                          <span className="badge badge--mint" style={{ border: '1.5px solid var(--color-fg)', fontWeight: 700 }}>
                            {s.status === 'Active' ? 'Đang học' : s.status}
                          </span>
                        </td>
                      </tr>
                    ))
                  ) : (
                    <tr>
                      <td colSpan={9} style={{ textAlign: 'center', padding: '36px', color: 'var(--color-muted-fg)' }}>
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
        </>
      )}
    </div>
  );
}
