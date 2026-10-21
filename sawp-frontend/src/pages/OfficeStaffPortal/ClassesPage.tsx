import { useEffect, useState } from 'react';
import {
  Calendar,
  CheckCircle2,
  GraduationCap,
  Layers,
  Megaphone,
  Plus,
  RefreshCw,
  Sparkles,
  UserCheck,
  UserPlus,
  Users,
} from 'lucide-react';
import toast from 'react-hot-toast';
import { classApi } from '../../services/implementations/classApi';
import { BatchInitClassesModal } from '../../components/classes/BatchInitClassesModal';
import { AssignTeacherModal } from '../../components/classes/AssignTeacherModal';
import { AssignStudentsModal } from '../../components/classes/AssignStudentsModal';
import { ClassDetailModal } from '../../components/classes/ClassDetailModal';
import type { ClassSummaryDto, SchoolYearDto } from '../../types/class.types';

export default function ClassesPage() {
  const [classes, setClasses] = useState<ClassSummaryDto[]>([]);
  const [schoolYears, setSchoolYears] = useState<SchoolYearDto[]>([]);
  const [selectedYearId, setSelectedYearId] = useState<number | undefined>(undefined);
  const [activeSchoolYear, setActiveSchoolYear] = useState<SchoolYearDto | null>(null);
  const [selectedGrade, setSelectedGrade] = useState<number | undefined>(undefined);
  const [loading, setLoading] = useState(false);
  const [publishingAll, setPublishingAll] = useState(false);

  // Modals state
  const [batchModalOpen, setBatchModalOpen] = useState(false);
  const [teacherModalOpen, setTeacherModalOpen] = useState(false);
  const [studentsModalOpen, setStudentsModalOpen] = useState(false);
  const [detailModalOpen, setDetailModalOpen] = useState(false);
  const [selectedClass, setSelectedClass] = useState<ClassSummaryDto | null>(null);

  useEffect(() => {
    loadSchoolYears();
  }, []);

  useEffect(() => {
    loadClasses();
  }, [selectedYearId, selectedGrade]);

  const loadSchoolYears = async () => {
    try {
      const [activeRes, allRes] = await Promise.all([
        classApi.getActiveSchoolYear(),
        classApi.getSchoolYears(),
      ]);

      if (allRes.success && allRes.data) {
        setSchoolYears(allRes.data);
      }

      if (activeRes.success && activeRes.data) {
        setActiveSchoolYear(activeRes.data);
        setSelectedYearId(activeRes.data.id);
      } else if (allRes.data && allRes.data.length > 0) {
        setActiveSchoolYear(allRes.data[0]);
        setSelectedYearId(allRes.data[0].id);
      }
    } catch {
      // ignore
    }
  };

  const loadClasses = async () => {
    setLoading(true);
    try {
      const res = await classApi.getClasses(selectedYearId, selectedGrade);
      if (res.success && res.data) {
        setClasses(res.data);
      }
    } catch {
      toast.error('Lỗi khi tải danh sách lớp học.');
    } finally {
      setLoading(false);
    }
  };

  const handlePublishAll = async () => {
    if (!activeSchoolYear) {
      toast.error('Chưa có năm học hiện tại!');
      return;
    }

    if (
      !window.confirm(
        `Bạn có chắc chắn muốn công bố kết quả xếp lớp cho toàn bộ các lớp trong Năm học ${activeSchoolYear.yearName}? Phụ huynh và giáo viên sẽ xem được danh sách lớp chính thức.`
      )
    ) {
      return;
    }

    setPublishingAll(true);
    try {
      const res = await classApi.publishAllPlacements(activeSchoolYear.id);
      if (res.success && res.data) {
        toast.success(
          `Đã công bố kết quả xếp lớp cho ${res.data.length} lớp học thành công!`,
          { icon: '📢' }
        );
        loadClasses();
      } else {
        toast.error(res.message || 'Công bố thất bại.');
      }
    } catch (err: any) {
      toast.error(err.response?.data?.message || 'Lỗi khi công bố xếp lớp.');
    } finally {
      setPublishingAll(false);
    }
  };

  // Calculations
  const totalClasses = classes.length;
  const totalEnrolled = classes.reduce((sum, c) => sum + c.enrolledStudentsCount, 0);
  const totalCapacity = classes.reduce((sum, c) => sum + c.capacity, 0);
  const fillRate = totalCapacity > 0 ? Math.round((totalEnrolled / totalCapacity) * 100) : 0;
  const publishedClassesCount = classes.filter((c) => c.isPlacementPublished).length;

  return (
    <div className="container pop-in">
      {/* Decorative Memphis elements */}
      <span className="decor decor--circle" style={{ top: -20, right: 30 }} aria-hidden="true" />
      <span className="decor decor--square" style={{ bottom: 20, left: 10 }} aria-hidden="true" />

      {/* Page Header */}
      <header className="page-header" style={{ marginBottom: 24 }}>
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
                      Năm học {y.yearName} {y.isActive ? '(Hiện tại)' : ''}
                    </option>
                  ))}
                </select>
              </div>
            </div>
            <h1 style={{ margin: '4px 0 8px' }}>
              Lớp học & <span className="squiggle">Phân công</span>
            </h1>
            <p style={{ color: 'var(--color-muted-fg)', margin: 0 }}>
              Khởi tạo danh sách lớp hàng loạt, phân công giáo viên chủ nhiệm và xếp lớp học sinh theo quy tắc nghiệp vụ.
            </p>
          </div>

          {/* Action Candy Buttons */}
          <div style={{ display: 'flex', gap: 10, flexWrap: 'wrap' }}>
            <button
              type="button"
              className="btn btn--secondary btn--sm"
              onClick={handlePublishAll}
              disabled={publishingAll || classes.length === 0}
              title="Công bố kết quả xếp lớp cho toàn trường"
            >
              <Megaphone size={16} color="var(--color-secondary)" />
              {publishingAll ? 'Đang công bố...' : 'Công bố toàn trường'}
            </button>
            <button
              type="button"
              className="btn btn--primary"
              onClick={() => setBatchModalOpen(true)}
            >
              <Layers size={18} /> Khởi tạo lớp hàng loạt
            </button>
          </div>
        </div>
      </header>

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
              Học sinh đã xếp lớp ({fillRate}%)
            </div>
            <div className="progress-track" style={{ height: 8 }}>
              <div
                className="progress-fill"
                style={{ width: `${Math.min(fillRate, 100)}%` }}
              />
            </div>
          </div>
        </div>

        <div className="stat-card">
          <div className="stat-icon" style={{ background: 'var(--color-tertiary-soft)' }}>
            <Megaphone size={22} color="var(--color-tertiary)" strokeWidth={2.5} />
          </div>
          <div>
            <div style={{ fontSize: 'var(--fs-2xl)', fontFamily: 'var(--font-heading)', fontWeight: 800 }}>
              {publishedClassesCount} / {totalClasses}
            </div>
            <div style={{ fontSize: 'var(--fs-xs)', textTransform: 'uppercase', color: 'var(--color-muted-fg)', fontWeight: 700 }}>
              Lớp đã công bố xếp lớp
            </div>
          </div>
        </div>
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
          onClick={loadClasses}
          title="Làm mới danh sách"
        >
          <RefreshCw size={15} /> Làm mới
        </button>
      </div>

      {/* Grid of Class Sticker Cards */}
      {loading ? (
        <div className="card" style={{ textAlign: 'center', padding: 48 }}>
          <RefreshCw size={32} className="spin" color="var(--color-accent)" style={{ margin: '0 auto 12px' }} />
          <div style={{ fontWeight: 700 }}>Đang tải danh sách lớp học...</div>
        </div>
      ) : classes.length === 0 ? (
        <div className="card empty-state" style={{ textAlign: 'center', padding: 48 }}>
          <GraduationCap size={48} color="var(--color-muted-fg)" style={{ margin: '0 auto 12px' }} />
          <h3>Chưa có lớp học nào được khởi tạo</h3>
          <p style={{ color: 'var(--color-muted-fg)', maxWidth: 460, margin: '0 auto 16px' }}>
            Hãy bấm nút <strong>"Khởi tạo lớp hàng loạt"</strong> để tạo nhanh các lớp mục tiêu (1A, 1B, 1C...) cho năm học mới.
          </p>
          <button
            type="button"
            className="btn btn--primary btn--sm"
            onClick={() => setBatchModalOpen(true)}
          >
            <Plus size={16} /> Khởi tạo lớp học ngay
          </button>
        </div>
      ) : (
        <div
          style={{
            display: 'grid',
            gridTemplateColumns: 'repeat(auto-fill, minmax(320px, 1fr))',
            gap: 20,
          }}
        >
          {classes.map((c) => {
            const classFillRate = c.capacity > 0 ? Math.round((c.enrolledStudentsCount / c.capacity) * 100) : 0;
            return (
              <div
                key={c.id}
                className="card card--hover"
                style={{
                  display: 'flex',
                  flexDirection: 'column',
                  gap: 14,
                  borderColor: c.isPlacementPublished ? 'var(--color-fg)' : undefined,
                }}
              >
                {/* Card Top: Class Name & Badges */}
                <div style={{ display: 'flex', justifyContent: 'space-between', alignItems: 'flex-start' }}>
                  <div style={{ display: 'flex', alignItems: 'center', gap: 10 }}>
                    <div
                      style={{
                        width: 44,
                        height: 44,
                        borderRadius: 'var(--radius-md)',
                        background: 'var(--color-accent)',
                        color: '#ffffff',
                        display: 'grid',
                        placeItems: 'center',
                        fontSize: 'var(--fs-lg)',
                        fontFamily: 'var(--font-heading)',
                        fontWeight: 800,
                        border: 'var(--border)',
                        boxShadow: 'var(--shadow-pop-active)',
                      }}
                    >
                      {c.className}
                    </div>
                    <div>
                      <div style={{ fontSize: 'var(--fs-lg)', fontWeight: 800, fontFamily: 'var(--font-heading)' }}>
                        Lớp {c.className}
                      </div>
                      <span className="badge badge--violet badge--sm">Khối {c.grade}</span>
                    </div>
                  </div>

                  <div>
                    {c.isPlacementPublished ? (
                      <span className="badge badge--mint">
                        <CheckCircle2 size={12} /> Đã công bố
                      </span>
                    ) : (
                      <span className="badge badge--amber">Chưa công bố</span>
                    )}
                  </div>
                </div>

                {/* Sĩ số & Progress bar */}
                <div>
                  <div style={{ display: 'flex', justifyContent: 'space-between', fontSize: 'var(--fs-xs)', textTransform: 'uppercase', fontWeight: 700, color: 'var(--color-muted-fg)', marginBottom: 4 }}>
                    <span>Sĩ số: {c.enrolledStudentsCount}/{c.capacity} học sinh</span>
                    <span>{classFillRate}%</span>
                  </div>
                  <div className="progress-track">
                    <div
                      className={`progress-fill ${
                        classFillRate >= 100
                          ? 'progress-fill--danger'
                          : classFillRate >= 80
                          ? 'progress-fill--warning'
                          : ''
                      }`}
                      style={{ width: `${Math.min(classFillRate, 100)}%` }}
                    />
                  </div>
                </div>

                {/* Homeroom Teacher */}
                <div
                  style={{
                    background: 'var(--color-bg)',
                    padding: '10px 12px',
                    borderRadius: 'var(--radius-sm)',
                    border: '1px solid var(--color-border)',
                    display: 'flex',
                    alignItems: 'center',
                    justifyContent: 'space-between',
                  }}
                >
                  <div style={{ display: 'flex', alignItems: 'center', gap: 8 }}>
                    <UserCheck size={18} color="var(--color-accent)" />
                    <div>
                      <div style={{ fontSize: 'var(--fs-xs)', textTransform: 'uppercase', color: 'var(--color-muted-fg)', fontWeight: 700 }}>
                        GVCN
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
                    disabled={c.availableCapacity <= 0}
                  >
                    <UserPlus size={14} /> Xếp HS
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
              </div>
            );
          })}
        </div>
      )}

      {/* Integrated Modals */}
      <BatchInitClassesModal
        open={batchModalOpen}
        onClose={() => setBatchModalOpen(false)}
        activeSchoolYear={activeSchoolYear}
        onClassesCreated={() => loadClasses()}
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
    </div>
  );
}
