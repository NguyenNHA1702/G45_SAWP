import { useEffect, useState } from 'react';
import {
  Calendar,
  CalendarPlus,
  CheckCircle2,
  ClipboardCheck,
  FileSpreadsheet,
  Power,
  GraduationCap,
  PlusCircle,
  RefreshCw,
  Sparkles,
  Trash2,
  UserCheck,
  UserPlus,
  Users,
} from 'lucide-react';
import toast from 'react-hot-toast';
import { classApi } from '../../services/implementations/classApi';
import { CreateSchoolYearModal } from '../../components/classes/CreateSchoolYearModal';
import { CreateClassModal } from '../../components/classes/CreateClassModal';
import { AssignTeacherModal } from '../../components/classes/AssignTeacherModal';
import { AssignStudentsModal } from '../../components/classes/AssignStudentsModal';
import { ClassDetailModal } from '../../components/classes/ClassDetailModal';
import { ImportClassAssignmentsModal } from '../../components/classes/ImportClassAssignmentsModal';
import { SYSTEM_MESSAGES, getSystemMessage } from '../../constants/messages';
import { ConfirmDialog } from '../../components/common/ConfirmDialog';
import { getAuthUser } from '../../services/apiClient';
import type { ClassSummaryDto, SchoolYearDto } from '../../types/class.types';

export default function ClassesPage() {
  const [classes, setClasses] = useState<ClassSummaryDto[]>([]);
  const [schoolYears, setSchoolYears] = useState<SchoolYearDto[]>([]);
  const [selectedYearId, setSelectedYearId] = useState<number | undefined>(undefined);
  const [activeSchoolYear, setActiveSchoolYear] = useState<SchoolYearDto | null>(null);
  const [selectedGrade, setSelectedGrade] = useState<number | undefined>(undefined);
  const [loading, setLoading] = useState(false);

  // Modals state for Classes & School Year
  const [createYearModalOpen, setCreateYearModalOpen] = useState(false);
  const [createClassModalOpen, setCreateClassModalOpen] = useState(false);
  const [teacherModalOpen, setTeacherModalOpen] = useState(false);
  const [studentsModalOpen, setStudentsModalOpen] = useState(false);
  const [detailModalOpen, setDetailModalOpen] = useState(false);
  const [selectedClass, setSelectedClass] = useState<ClassSummaryDto | null>(null);
  const [confirmDeleteClass, setConfirmDeleteClass] = useState<ClassSummaryDto | null>(null);
  const [deletingClass, setDeletingClass] = useState(false);
  const [confirmDeleteYear, setConfirmDeleteYear] = useState(false);
  const [deletingYear, setDeletingYear] = useState(false);

  // Draft school year (UC-13, UC-14 A2, UC-69, BR-72)
  const [importModalOpen, setImportModalOpen] = useState(false);
  const [confirmActivateYear, setConfirmActivateYear] = useState(false);
  const [activatingYear, setActivatingYear] = useState(false);
  const [reviewingClassId, setReviewingClassId] = useState<number | null>(null);

  // User roles (robust against session/localStorage format, case-insensitivity, single role vs roles array)
  const getUserRoles = (): string[] => {
    const raw = getAuthUser();
    if (!raw) return [];
    try {
      const parsed = JSON.parse(raw);
      const roles = parsed?.roles ?? (parsed?.role ? [parsed.role] : []);
      return Array.isArray(roles) ? roles : [String(roles)];
    } catch {
      return [];
    }
  };

  const userRoles = getUserRoles();
  const canManage = userRoles.some(
    (r) => typeof r === 'string' && (r.toLowerCase() === 'officestaff' || r.toLowerCase() === 'admin')
  );

  useEffect(() => {
    loadSchoolYears();
  }, []);

  useEffect(() => {
    if (selectedYearId === undefined && schoolYears.length === 0) return;
    loadClasses();
  }, [selectedYearId, selectedGrade]);

  const loadSchoolYears = async (keepYearId?: number) => {
    try {
      const allRes = await classApi.getSchoolYears();

      if (allRes.success && allRes.data && allRes.data.length > 0) {
        // Sanitize names by stripping HTML and sort descending by start date
        const sanitizedYears = allRes.data
          .map((y) => ({
            ...y,
            yearName: y.yearName ? y.yearName.replace(/<[^>]*>?/gm, '').trim() : '',
          }))
          .sort((a, b) => new Date(b.startDate).getTime() - new Date(a.startDate).getTime());

        setSchoolYears(sanitizedYears);
        const activeYear =
          sanitizedYears.find((y) => y.id === keepYearId) ?? sanitizedYears.find((y) => y.isActive) ?? sanitizedYears[0];
        setActiveSchoolYear(activeYear);
        setSelectedYearId(activeYear.id);
      } else {
        loadClasses();
      }
    } catch {
      loadClasses();
    }
  };

  const loadClasses = async (targetYearId?: number, targetGrade?: number) => {
    setLoading(true);
    try {
      const yearToUse = targetYearId !== undefined ? targetYearId : selectedYearId;
      const gradeToUse = targetGrade !== undefined ? targetGrade : selectedGrade;
      const res = await classApi.getClasses(yearToUse, gradeToUse);
      if (res.success && res.data) {
        setClasses(res.data);
      }
    } catch {
      toast.error('Lỗi khi tải danh sách lớp học.');
    } finally {
      setLoading(false);
    }
  };

  const handleDeleteClass = (c: ClassSummaryDto) => {
    const enrolled = c.enrolledCount ?? c.enrolledStudentsCount ?? 0;
    if (enrolled > 0) {
      toast.error(`Không thể xóa lớp học vì lớp đang có ${enrolled} học sinh. Chỉ có thể xóa lớp học khi sĩ số bằng 0.`);
      return;
    }

    if (c.homeroomTeacherStaffId || c.homeroomTeacherUserId || c.homeroomTeacherName) {
      toast.error(`Lớp ${c.className} đang có Giáo viên chủ nhiệm phân công. Vui lòng gỡ GVCN trước khi xóa lớp.`);
      return;
    }

    setConfirmDeleteClass(c);
  };

  const executeDeleteClass = async () => {
    if (!confirmDeleteClass) return;
    setDeletingClass(true);
    try {
      const res = await classApi.deleteClass(confirmDeleteClass.id);
      if (res.success) {
        toast.success(`Đã xóa lớp ${confirmDeleteClass.className} thành công!`);
        setConfirmDeleteClass(null);
        loadClasses();
      } else {
        const errorMsg = res.errors?.[0] || res.message || 'Xóa lớp học thất bại.';
        toast.error(errorMsg);
      }
    } catch (err: any) {
      const msg = err.response?.data?.errors?.[0] || err.response?.data?.message || 'Có lỗi xảy ra khi xóa lớp học.';
      toast.error(msg);
    } finally {
      setDeletingClass(false);
    }
  };

  const handleDeleteSchoolYear = () => {
    if (!selectedYearId || !activeSchoolYear) return;
    // UC-13 A2: only a Draft school year can be discarded, with its classes and class assignments
    if (activeSchoolYear.status !== 'Draft') {
      toast.error('Chỉ có thể xóa năm học ở trạng thái Bản nháp.');
      return;
    }
    setConfirmDeleteYear(true);
  };

  const executeDeleteSchoolYear = async () => {
    if (!selectedYearId || !activeSchoolYear) return;
    setDeletingYear(true);
    try {
      const res = await classApi.deleteSchoolYear(selectedYearId, true);
      if (res.success) {
        toast.success(`Đã xóa năm học "${activeSchoolYear.yearName}" thành công!`);
        setConfirmDeleteYear(false);
        await loadSchoolYears();
      } else {
        toast.error(res.errors?.[0] || res.message || 'Không thể xóa năm học.');
      }
    } catch (err: any) {
      toast.error(err.response?.data?.errors?.[0] || err.response?.data?.message || 'Lỗi khi xóa năm học.');
    } finally {
      setDeletingYear(false);
    }
  };

  const isDraftYear = activeSchoolYear?.status === 'Draft';
  const isArchivedYear = activeSchoolYear?.status === 'Archived';

  const handleMarkReviewed = async (c: ClassSummaryDto) => {
    setReviewingClassId(c.id);
    try {
      const res = await classApi.markClassReviewed(c.id);
      if (res.success) {
        toast.success(`Đã đánh dấu Lớp ${c.className} là Đã rà soát.`);
        loadClasses();
      } else {
        toast.error(res.errors?.[0] || res.message || 'Không thể đánh dấu rà soát.');
      }
    } catch (err: any) {
      toast.error(err.response?.data?.errors?.[0] || err.response?.data?.message || 'Không thể đánh dấu rà soát.');
    } finally {
      setReviewingClassId(null);
    }
  };

  const executeActivateYear = async () => {
    if (!activeSchoolYear) return;
    setActivatingYear(true);
    try {
      const res = await classApi.activateSchoolYear(activeSchoolYear.id);
      if (res.success) {
        toast.success(`Đã kích hoạt năm học ${activeSchoolYear.yearName}.`);
        setConfirmActivateYear(false);
        await loadSchoolYears(activeSchoolYear.id);
        loadClasses(activeSchoolYear.id);
      } else {
        toast.error([SYSTEM_MESSAGES.MSG63.content, ...(res.errors ?? [])].join(' '));
      }
    } catch (err: any) {
      // MSG63 (UC-13 E2) with the reason, e.g. the classes that are not reviewed yet
      const data = err.response?.data;
      const srs = data?.code ? getSystemMessage(data.code)?.content : undefined;
      toast.error([srs ?? data?.message ?? SYSTEM_MESSAGES.MSG63.content, ...(srs ? data?.errors ?? [] : [])].join(' '));
      setConfirmActivateYear(false);
    } finally {
      setActivatingYear(false);
    }
  };

  // KPI metrics
  const totalClasses = classes.length;
  const totalEnrolled = classes.reduce((sum, c) => sum + (c.enrolledCount ?? c.enrolledStudentsCount ?? 0), 0);
  const totalCapacity = classes.reduce((sum, c) => sum + c.capacity, 0);
  const fillRate = totalCapacity > 0 ? Math.round((totalEnrolled / totalCapacity) * 100) : 0;
  const reviewedClassesCount = classes.filter((c) => c.isReviewed).length;

  return (
    <div className="container pop-in">
      {/* Decorative Memphis elements */}
      <span className="decor decor--circle" style={{ top: -20, right: 30 }} aria-hidden="true" />
      <span className="decor decor--square" style={{ bottom: 20, left: 10 }} aria-hidden="true" />

      {/* Page Header */}
      <header className="page-header" style={{ marginBottom: 20 }}>
        <div style={{ display: 'flex', justifyContent: 'space-between', alignItems: 'flex-start', flexWrap: 'wrap', gap: 16 }}>
          <div>
            <div style={{ display: 'flex', gap: 10, alignItems: 'center', marginBottom: 8, flexWrap: 'wrap' }}>
              <span className="badge badge--violet">
                <Sparkles size={14} /> Quản trị Lớp học & Xếp lớp
              </span>
              <div style={{ display: 'flex', alignItems: 'center', gap: 6 }}>
                <Calendar size={14} color="var(--color-tertiary)" />
                <select
                  className="select"
                  style={{ minHeight: 32, padding: '2px 10px', fontSize: 'var(--fs-xs)', fontWeight: 700 }}
                  value={selectedYearId ?? ''}
                  onChange={(e) => {
                    const val = e.target.value ? Number(e.target.value) : undefined;
                    setSelectedYearId(val);
                    const matched = schoolYears.find((y) => y.id === val);
                    if (matched) setActiveSchoolYear(matched);
                  }}
                >
                  {schoolYears.map((y) => (
                    <option key={y.id} value={y.id}>
                      Năm học {y.yearName}{' '}
                      {y.isActive ? '(Hiện tại)' : y.status === 'Draft' ? '(Bản nháp)' : y.status === 'Archived' ? '(Đã lưu trữ)' : ''}
                    </option>
                  ))}
                </select>
                {canManage && (
                  <>
                    <button
                      type="button"
                      className="btn btn--secondary btn--sm"
                      style={{ minHeight: 32, padding: '2px 10px', fontSize: 'var(--fs-xs)', gap: 4 }}
                      onClick={() => setCreateYearModalOpen(true)}
                      title="Khởi tạo năm học mới"
                    >
                      <CalendarPlus size={14} color="var(--color-tertiary)" />
                      + Năm học mới
                    </button>
                    <button
                      type="button"
                      className="btn btn--secondary btn--sm"
                      style={{
                        minHeight: 32,
                        padding: '2px 10px',
                        fontSize: 'var(--fs-xs)',
                        gap: 4,
                        color: !isDraftYear ? '#94A3B8' : '#DC2626',
                        borderColor: !isDraftYear ? '#CBD5E1' : '#FECACA',
                        backgroundColor: !isDraftYear ? '#F1F5F9' : '#FEF2F2',
                        cursor: !isDraftYear ? 'not-allowed' : 'pointer',
                        opacity: !isDraftYear ? 0.6 : 1,
                      }}
                      disabled={!isDraftYear}
                      onClick={handleDeleteSchoolYear}
                      title={
                        !activeSchoolYear
                          ? 'Chưa chọn năm học'
                          : !isDraftYear
                          ? 'Chỉ có thể xóa năm học ở trạng thái Bản nháp'
                          : 'Xóa năm học Bản nháp này cùng các lớp và dữ liệu xếp lớp'
                      }
                    >
                      <Trash2 size={13} /> Xóa năm học
                    </button>
                  </>
                )}
              </div>
            </div>
            <h1 style={{ margin: '4px 0 8px' }}>
              Lớp học & <span className="squiggle">Xếp lớp đầu năm</span>
            </h1>
            <p style={{ color: 'var(--color-muted-fg)', margin: 0 }}>
              Khởi tạo danh sách lớp, phân công giáo viên chủ nhiệm và nhập kết quả xếp lớp do nhà trường quyết định vào năm học Bản nháp.
            </p>
          </div>

          {/* Action Buttons */}
          {canManage && (
            <div style={{ display: 'flex', gap: 10, flexWrap: 'wrap' }}>
              {isDraftYear && (
                <>
                  <button
                    type="button"
                    className="btn btn--secondary btn--sm"
                    onClick={() => setConfirmActivateYear(true)}
                    title="Kích hoạt năm học khi tất cả các lớp đã được rà soát"
                  >
                    <Power size={16} /> Kích hoạt năm học
                  </button>
                  <button
                    type="button"
                    className="btn btn--primary"
                    onClick={() => setImportModalOpen(true)}
                    title="Nhập danh sách lớp, giáo viên chủ nhiệm và xếp lớp học sinh từ tệp Excel"
                  >
                    <FileSpreadsheet size={18} /> Nhập xếp lớp từ tệp
                  </button>
                </>
              )}
              {!isArchivedYear && (
                  <button
                    type="button"
                    className="btn btn--primary"
                    onClick={() => setCreateClassModalOpen(true)}
                    title="Thêm một lớp học đơn lẻ"
                  >
                    <PlusCircle size={18} /> + Thêm lớp học
                  </button>
              )}
            </div>
          )}
        </div>

      </header>

      <>
        {isDraftYear && (
          <div
            className="card"
            data-testid="draft-year-banner"
            style={{ padding: '12px 16px', marginBottom: 16, borderStyle: 'dashed', display: 'flex', gap: 10, alignItems: 'center', flexWrap: 'wrap' }}
          >
            <ClipboardCheck size={20} />
            <span style={{ fontSize: 'var(--fs-sm)' }}>
              Năm học <strong>{activeSchoolYear?.yearName}</strong> đang ở trạng thái <strong>Bản nháp</strong>: chưa hiển thị với Giáo
              viên và Phụ huynh. Đã rà soát <strong>{reviewedClassesCount} / {totalClasses}</strong> lớp; chỉ kích hoạt được khi tất cả
              các lớp đã được rà soát.
            </span>
          </div>
        )}

        {/* Top Stats Cards */}
        <div
          style={{
            display: 'grid',
            gridTemplateColumns: 'repeat(auto-fit, minmax(220px, 1fr))',
            gap: 16,
            marginBottom: 24,
          }}
        >
          <div className="stat-card">
            <div className="stat-icon" style={{ background: 'var(--color-accent-soft)' }}>
              <GraduationCap size={22} color="var(--color-accent)" strokeWidth={2.5} />
            </div>
            <div>
              <div style={{ fontSize: 'var(--fs-2xl)', fontFamily: 'var(--font-heading)', fontWeight: 800 }}>
                {totalClasses}
              </div>
              <div style={{ fontSize: 'var(--fs-xs)', textTransform: 'uppercase', color: 'var(--color-muted-fg)', fontWeight: 700 }}>
                Tổng số lớp học
              </div>
            </div>
          </div>

          <div className="stat-card">
            <div className="stat-icon" style={{ background: 'var(--color-quaternary-soft)' }}>
              <Users size={22} color="var(--color-quaternary)" strokeWidth={2.5} />
            </div>
            <div style={{ flex: 1 }}>
              <div style={{ fontSize: 'var(--fs-2xl)', fontFamily: 'var(--font-heading)', fontWeight: 800 }}>
                {totalEnrolled} / {totalCapacity}
              </div>
              <div style={{ fontSize: 'var(--fs-xs)', textTransform: 'uppercase', color: 'var(--color-muted-fg)', fontWeight: 700, marginBottom: 4 }}>
                Sĩ số / Chỉ tiêu lớp học ({fillRate}%)
              </div>
              <div className="progress-track" style={{ height: 8 }}>
                <div
                  className="progress-fill"
                  style={{ width: `${Math.min(fillRate, 100)}%` }}
                />
              </div>
            </div>
          </div>

          {isDraftYear && (
            <div className="stat-card">
              <div className="stat-icon" style={{ background: 'var(--color-tertiary-soft)' }}>
                <ClipboardCheck size={22} color="var(--color-tertiary)" strokeWidth={2.5} />
              </div>
              <div>
                <div style={{ fontSize: 'var(--fs-2xl)', fontFamily: 'var(--font-heading)', fontWeight: 800 }}>
                  {reviewedClassesCount} / {totalClasses}
                </div>
                <div style={{ fontSize: 'var(--fs-xs)', textTransform: 'uppercase', color: 'var(--color-muted-fg)', fontWeight: 700 }}>
                  Lớp đã rà soát
                </div>
              </div>
            </div>
          )}
        </div>

        {/* Grade Selector Pill Tabs */}
        <div style={{ display: 'flex', justifyContent: 'space-between', alignItems: 'center', marginBottom: 20, flexWrap: 'wrap', gap: 12 }}>
          <div className="pill-tabs">
            <button
              type="button"
              className={`pill-tab ${selectedGrade === undefined ? 'pill-tab--active' : ''}`}
              onClick={() => setSelectedGrade(undefined)}
            >
              Tất cả khối lớp
            </button>
            {[1, 2, 3, 4, 5].map((g) => (
              <button
                key={g}
                type="button"
                className={`pill-tab ${selectedGrade === g ? 'pill-tab--active' : ''}`}
                onClick={() => setSelectedGrade(g)}
              >
                Khối {g}
              </button>
            ))}
          </div>

          <button
            type="button"
            className="btn btn--secondary btn--sm"
            onClick={() => loadClasses()}
            title="Tải lại danh sách"
          >
            <RefreshCw size={14} className={loading ? 'spin' : ''} />
            Làm mới
          </button>
        </div>

        {/* Classes Cards Grid */}
        {loading ? (
          <div className="card" style={{ padding: 48, textAlign: 'center' }}>
            <RefreshCw size={32} className="spin" color="var(--color-muted-fg)" style={{ margin: '0 auto 12px' }} />
            <div style={{ fontWeight: 700, color: 'var(--color-muted-fg)' }}>
              Đang tải danh sách lớp học...
            </div>
          </div>
        ) : classes.length === 0 ? (
          <div className="card" style={{ padding: 48, textAlign: 'center', borderStyle: 'dashed' }}>
            <GraduationCap size={48} color="var(--color-muted-fg)" style={{ margin: '0 auto 12px' }} />
            <h3 style={{ margin: '0 0 6px' }}>Chưa có lớp học nào</h3>
            <p style={{ color: 'var(--color-muted-fg)', margin: '0 0 16px' }}>
              Năm học này chưa có lớp học nào cho khối đã chọn. Bạn có thể thêm lớp học mới hoặc nhập xếp lớp từ tệp Excel.
            </p>
            <div style={{ display: 'flex', gap: 12, justifyContent: 'center', flexWrap: 'wrap' }}>
              {isDraftYear && (
                <button
                  type="button"
                  className="btn btn--secondary"
                  onClick={() => setImportModalOpen(true)}
                >
                  <FileSpreadsheet size={18} /> Nhập xếp lớp từ tệp
                </button>
              )}
              {canManage && !isArchivedYear && (
                <button
                  type="button"
                  className="btn btn--primary"
                  onClick={() => setCreateClassModalOpen(true)}
                >
                  <PlusCircle size={18} /> + Thêm lớp học
                </button>
              )}
            </div>
          </div>
        ) : (
          <div
            style={{
              display: 'grid',
              gridTemplateColumns: 'repeat(auto-fill, minmax(290px, 1fr))',
              gap: 20,
            }}
          >
            {classes.map((c) => {
              const enrolled = c.enrolledCount ?? c.enrolledStudentsCount ?? 0;
              const availableSlots = c.capacity - enrolled;
              const isFull = availableSlots <= 0;

              return (
                <div
                  key={c.id}
                  className="class-card card"
                  style={{
                    background: '#FFFFFF',
                    border: '2px solid #1E293B',
                    borderRadius: 16,
                    boxShadow: '4px 4px 0 0 #1E293B',
                    padding: '18px 20px',
                    display: 'flex',
                    flexDirection: 'column',
                    justifyContent: 'space-between',
                  }}
                >
                  {/* Header row 1: class name, grade and review status (UC-14 A2) */}
                  <div style={{ display: 'flex', justifyContent: 'space-between', alignItems: 'center', marginBottom: 8, gap: 8 }}>
                    <div style={{ display: 'flex', gap: 6, alignItems: 'center', flexWrap: 'nowrap' }}>
                      <span
                        style={{
                          background: 'var(--color-accent)',
                          color: 'var(--color-ink)',
                          fontFamily: 'var(--font-heading)',
                          fontWeight: 900,
                          fontSize: 'var(--fs-base)',
                          padding: '3px 10px',
                          borderRadius: 'var(--radius-sm)',
                          border: '1.5px solid var(--color-ink)',
                          whiteSpace: 'nowrap',
                          display: 'inline-block',
                        }}
                      >
                        Lớp {c.className}
                      </span>
                      <span
                        className="badge badge--yellow"
                        style={{ fontSize: 'var(--fs-xs)', whiteSpace: 'nowrap', height: 'fit-content' }}
                      >
                        Khối {c.grade}
                      </span>
                    </div>

                    {isDraftYear ? (
                      c.isReviewed ? (
                        <span className="badge badge--green" style={{ fontSize: '0.72rem', whiteSpace: 'nowrap' }}>
                          <CheckCircle2 size={12} /> Đã rà soát
                        </span>
                      ) : (
                        <span className="badge badge--orange" style={{ fontSize: '0.72rem', whiteSpace: 'nowrap' }}>
                          Chưa rà soát
                        </span>
                      )
                    ) : null}
                  </div>

                  {/* Header Row 2: Phòng học & Nút Xóa lớp học */}
                  <div style={{ display: 'flex', justifyContent: 'space-between', alignItems: 'center', marginBottom: 12, minHeight: 28 }}>
                    <div style={{ fontSize: 'var(--fs-xs)', color: 'var(--color-muted-fg)' }}>
                      Phòng: <strong>{c.roomNumber || 'Chưa xếp'}</strong>
                    </div>

                    {canManage && (
                      <button
                        type="button"
                        className="btn btn--secondary btn--sm"
                        style={{
                          padding: '3px 8px',
                          minHeight: 24,
                          color: enrolled > 0 ? '#94A3B8' : '#DC2626',
                          borderColor: enrolled > 0 ? '#E2E8F0' : '#FECACA',
                          backgroundColor: enrolled > 0 ? '#F1F5F9' : '#FEF2F2',
                          fontSize: '0.72rem',
                          fontWeight: 700,
                          gap: 4,
                          borderRadius: 6,
                          cursor: enrolled > 0 ? 'not-allowed' : 'pointer',
                          opacity: enrolled > 0 ? 0.6 : 1,
                        }}
                        disabled={enrolled > 0}
                        onClick={() => handleDeleteClass(c)}
                        title={
                          enrolled > 0
                            ? `Không thể xóa vì lớp đang có ${enrolled} học sinh`
                            : (c.homeroomTeacherStaffId || c.homeroomTeacherUserId || c.homeroomTeacherName)
                            ? 'Vui lòng gỡ GVCN trước khi xóa lớp'
                            : 'Xóa lớp học (chỉ khi sĩ số = 0)'
                        }
                      >
                        <Trash2 size={12} /> Xóa lớp học
                      </button>
                    )}
                  </div>

                  {/* Capacity and Enrollment Progress */}
                  <div style={{ marginBottom: 14 }}>
                    <div style={{ display: 'flex', justifyContent: 'space-between', fontSize: 'var(--fs-xs)', marginBottom: 4, fontWeight: 700 }}>
                      <span>Sĩ số:</span>
                      <span style={{ color: isFull ? 'var(--color-quaternary)' : 'inherit' }}>
                        {enrolled} / {c.capacity} em {isFull ? '(Đầy)' : `(Còn ${availableSlots} chỗ)`}
                      </span>
                    </div>
                    <div className="progress-track">
                      <div
                        className="progress-fill"
                        style={{
                          width: `${Math.min((enrolled / c.capacity) * 100, 100)}%`,
                          background: isFull ? 'var(--color-quaternary)' : 'var(--color-tertiary)',
                        }}
                      />
                    </div>
                  </div>

                  {/* Homeroom Teacher */}
                  <div
                    style={{
                      background: 'var(--color-bg)',
                      border: '1.5px solid var(--color-ink)',
                      borderRadius: 'var(--radius-md)',
                      padding: '8px 10px',
                      marginBottom: 14,
                      display: 'flex',
                      alignItems: 'center',
                      justifyContent: 'space-between',
                    }}
                  >
                    <div style={{ display: 'flex', alignItems: 'center', gap: 8 }}>
                      <div
                        style={{
                          width: 28,
                          height: 28,
                          borderRadius: 'var(--radius-sm)',
                          background: c.homeroomTeacherName ? 'var(--color-tertiary-soft)' : 'var(--color-muted)',
                          border: '1px solid var(--color-ink)',
                          display: 'flex',
                          alignItems: 'center',
                          justifyContent: 'center',
                        }}
                      >
                        <UserCheck size={16} color={c.homeroomTeacherName ? 'var(--color-tertiary)' : 'var(--color-muted-fg)'} />
                      </div>
                      <div>
                        <div style={{ fontSize: '0.7rem', textTransform: 'uppercase', color: 'var(--color-muted-fg)', fontWeight: 800 }}>
                          GV Chủ nhiệm
                        </div>
                        <div style={{ fontSize: 'var(--fs-sm)', fontWeight: 700 }}>
                          {c.homeroomTeacherName || 'Chưa phân công'}
                        </div>
                      </div>
                    </div>
                    <button
                      type="button"
                      className="btn btn--secondary btn--sm"
                      style={{ padding: '2px 8px', fontSize: 'var(--fs-xs)' }}
                      onClick={() => {
                        setSelectedClass(c);
                        setTeacherModalOpen(true);
                      }}
                    >
                      {c.homeroomTeacherName ? 'Đổi' : '+ Gán'}
                    </button>
                  </div>

                  {/* Card Action Buttons */}
                  <div style={{ display: 'grid', gridTemplateColumns: '1fr 1fr', gap: 8, marginTop: 'auto' }}>
                    <button
                      type="button"
                      className="btn btn--secondary btn--sm"
                      onClick={() => {
                        setSelectedClass(c);
                        setStudentsModalOpen(true);
                      }}
                      disabled={isFull}
                      title="Tiếp nhận học sinh chuyển trường hoặc bổ sung giữa kỳ"
                    >
                      <UserPlus size={14} /> Bổ sung HS
                    </button>
                    <button
                      type="button"
                      className="btn btn--primary btn--sm"
                      onClick={() => {
                        setSelectedClass(c);
                        setDetailModalOpen(true);
                      }}
                    >
                      <Users size={14} /> Chi tiết lớp
                    </button>
                  </div>

                  {isDraftYear && canManage && !c.isReviewed && (
                    <button
                      type="button"
                      className="btn btn--secondary btn--sm"
                      style={{ marginTop: 8 }}
                      onClick={() => handleMarkReviewed(c)}
                      disabled={reviewingClassId === c.id}
                      title="Đã kiểm tra danh sách học sinh, sĩ số và giáo viên chủ nhiệm của lớp"
                    >
                      <ClipboardCheck size={14} /> {reviewingClassId === c.id ? 'Đang lưu...' : 'Đánh dấu đã rà soát'}
                    </button>
                  )}
                </div>
              );
            })}
          </div>
        )}
      </>

      {/* Integrated Modals */}
      <CreateSchoolYearModal
        open={createYearModalOpen}
        onClose={() => setCreateYearModalOpen(false)}
        existingYears={schoolYears}
        onYearCreated={(newYear) => {
          loadSchoolYears();
          setSelectedYearId(newYear.id);
          setActiveSchoolYear(newYear);
          loadClasses(newYear.id);
        }}
      />

      <CreateClassModal
        open={createClassModalOpen}
        onClose={() => setCreateClassModalOpen(false)}
        activeSchoolYear={activeSchoolYear}
        initialGrade={selectedGrade || 1}
        onClassCreated={(_newClass, grade) => {
          setSelectedGrade(grade);
          loadClasses(selectedYearId, grade);
        }}
      />

      <AssignTeacherModal
        open={teacherModalOpen}
        onClose={() => setTeacherModalOpen(false)}
        targetClass={selectedClass}
        onAssigned={() => loadClasses()}
      />

      <AssignStudentsModal
        open={studentsModalOpen}
        onClose={() => setStudentsModalOpen(false)}
        targetClass={selectedClass}
        onAssigned={() => loadClasses()}
      />

      <ClassDetailModal
        open={detailModalOpen}
        onClose={() => setDetailModalOpen(false)}
        targetClass={selectedClass}
        onOpenAssignTeacher={(c) => {
          setSelectedClass(c);
          setTeacherModalOpen(true);
        }}
        onOpenAssignStudents={(c) => {
          setSelectedClass(c);
          setStudentsModalOpen(true);
        }}
        onRosterUpdated={() => loadClasses()}
      />

      <ConfirmDialog
        open={!!confirmDeleteClass}
        title={`Xác nhận xóa Lớp ${confirmDeleteClass?.className ?? ''}`}
        confirmText={deletingClass ? 'Đang xóa...' : 'Xác nhận xóa'}
        cancelText="Hủy"
        busy={deletingClass}
        onConfirm={executeDeleteClass}
        onCancel={() => !deletingClass && setConfirmDeleteClass(null)}
      >
        <p style={{ margin: 0 }}>
          Bạn có chắc chắn muốn xóa <strong>Lớp {confirmDeleteClass?.className}</strong> (Khối {confirmDeleteClass?.grade}) khỏi danh sách năm học? Thao tác này không thể hoàn tác.
        </p>
      </ConfirmDialog>

      <ImportClassAssignmentsModal
        open={importModalOpen}
        onClose={() => setImportModalOpen(false)}
        schoolYear={activeSchoolYear}
        onImported={() => loadClasses()}
      />

      <ConfirmDialog
        open={confirmActivateYear}
        title={`Kích hoạt năm học "${activeSchoolYear?.yearName ?? ''}"`}
        confirmText={activatingYear ? 'Đang kích hoạt...' : 'Kích hoạt'}
        cancelText="Hủy"
        busy={activatingYear}
        onConfirm={executeActivateYear}
        onCancel={() => !activatingYear && setConfirmActivateYear(false)}
      >
        <p style={{ margin: 0 }}>
          Khi kích hoạt, lớp được xếp trong năm học <strong>{activeSchoolYear?.yearName}</strong> trở thành lớp hiện tại của từng học
          sinh, giáo viên chủ nhiệm nhận thông báo và năm học hiện tại chuyển sang trạng thái lưu trữ.
        </p>
      </ConfirmDialog>

      <ConfirmDialog
        open={confirmDeleteYear}
        title={`Xác nhận xóa năm học "${activeSchoolYear?.yearName ?? ''}"`}
        confirmText={deletingYear ? 'Đang xóa...' : 'Xác nhận xóa'}
        cancelText="Hủy"
        busy={deletingYear}
        onConfirm={executeDeleteSchoolYear}
        onCancel={() => !deletingYear && setConfirmDeleteYear(false)}
      >
        <p style={{ margin: 0 }}>
          {classes.length > 0 ? (
            <>
              <strong style={{ color: '#DC2626' }}>Cảnh báo: Năm học {activeSchoolYear?.yearName} hiện đang có {classes.length} lớp học!</strong>{' '}
              Xóa năm học sẽ xóa toàn bộ {classes.length} lớp học nháp và dữ liệu xếp lớp nháp bên trong. Thao tác này không thể hoàn tác.
            </>
          ) : (
            <>
              Bạn có chắc chắn muốn xóa năm học <strong>{activeSchoolYear?.yearName}</strong>? Thao tác này không thể hoàn tác.
            </>
          )}
        </p>
      </ConfirmDialog>
    </div>
  );
}
