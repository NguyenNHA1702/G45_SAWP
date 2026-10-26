import { useEffect, useState } from 'react';
import { useSearchParams } from 'react-router';
import {
  Calendar,
  CalendarPlus,
  CheckCircle2,
  GraduationCap,
  Layers,
  Megaphone,
  RefreshCw,
  Sparkles,
  UserCheck,
  UserPlus,
  Users,
} from 'lucide-react';
import toast from 'react-hot-toast';
import { classApi } from '../../services/implementations/classApi';
import { placementPlanApi } from '../../services/implementations/placementPlanApi';
import { CreateSchoolYearModal } from '../../components/classes/CreateSchoolYearModal';
import { BatchInitClassesModal } from '../../components/classes/BatchInitClassesModal';
import { AssignTeacherModal } from '../../components/classes/AssignTeacherModal';
import { AssignStudentsModal } from '../../components/classes/AssignStudentsModal';
import { ClassDetailModal } from '../../components/classes/ClassDetailModal';
import { GeneratePlacementModal } from '../../components/placement/GeneratePlacementModal';
import { DraftPlacementPreview } from '../../components/placement/DraftPlacementPreview';
import { PlacementPlanHistoryList } from '../../components/placement/PlacementPlanHistoryList';
import { USER_KEY } from '../../services/apiClient';
import type { ClassSummaryDto, SchoolYearDto } from '../../types/class.types';
import type {
  PlacementPlanDetailDto,
  PlacementPlanSummaryDto,
} from '../../types/placement.types';

export default function ClassesPage() {
  const [searchParams, setSearchParams] = useSearchParams();

  // Tab State: 'classes' (Lớp học chính thức) or 'placement' (Phương án xếp lớp đầu năm)
  const [activeTab, setActiveTab] = useState<'classes' | 'placement'>(
    (searchParams.get('tab') as 'classes' | 'placement') || 'classes'
  );

  const [classes, setClasses] = useState<ClassSummaryDto[]>([]);
  const [schoolYears, setSchoolYears] = useState<SchoolYearDto[]>([]);
  const [selectedYearId, setSelectedYearId] = useState<number | undefined>(undefined);
  const [activeSchoolYear, setActiveSchoolYear] = useState<SchoolYearDto | null>(null);
  const [selectedGrade, setSelectedGrade] = useState<number | undefined>(undefined);
  const [loading, setLoading] = useState(false);
  const [publishingAll, setPublishingAll] = useState(false);

  // Modals state for Classes & School Year
  const [createYearModalOpen, setCreateYearModalOpen] = useState(false);
  const [batchModalOpen, setBatchModalOpen] = useState(false);
  const [teacherModalOpen, setTeacherModalOpen] = useState(false);
  const [studentsModalOpen, setStudentsModalOpen] = useState(false);
  const [detailModalOpen, setDetailModalOpen] = useState(false);
  const [selectedClass, setSelectedClass] = useState<ClassSummaryDto | null>(null);

  // Placement Plan states (WF-Placement)
  const [placementPlans, setPlacementPlans] = useState<PlacementPlanSummaryDto[]>([]);
  const [loadingPlans, setLoadingPlans] = useState(false);
  const [selectedPlanDetail, setSelectedPlanDetail] = useState<PlacementPlanDetailDto | null>(null);
  const [generatePlacementOpen, setGeneratePlacementOpen] = useState(false);

  // User roles
  const userJson = localStorage.getItem(USER_KEY);
  const currentUser = userJson ? JSON.parse(userJson) : null;
  const userRoles: string[] = currentUser?.roles ?? [];

  useEffect(() => {
    loadSchoolYears();
  }, []);

  useEffect(() => {
    loadClasses();
    if (activeTab === 'placement') {
      loadPlacementPlans();
    }
  }, [selectedYearId, selectedGrade, activeTab]);

  useEffect(() => {
    const planIdParam = searchParams.get('planId');
    if (planIdParam) {
      setActiveTab('placement');
      loadPlanDetail(Number(planIdParam));
    }
  }, [searchParams]);

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

  const loadPlacementPlans = async () => {
    setLoadingPlans(true);
    try {
      const res = await placementPlanApi.getPlanHistory(selectedYearId, selectedGrade);
      if (res.success && res.data) {
        setPlacementPlans(res.data);
      }
    } catch {
      // ignore
    } finally {
      setLoadingPlans(false);
    }
  };

  const loadPlanDetail = async (planId: number) => {
    try {
      const res = await placementPlanApi.getPlanById(planId);
      if (res.success && res.data) {
        setSelectedPlanDetail(res.data);
      }
    } catch {
      toast.error('Không tìm thấy phương án xếp lớp được yêu cầu.');
    }
  };

  const handleSelectPlan = (planId: number) => {
    setSearchParams({ tab: 'placement', planId: planId.toString() });
    loadPlanDetail(planId);
  };

  const handleBackToPlanHistory = () => {
    setSelectedPlanDetail(null);
    setSearchParams({ tab: 'placement' });
    loadPlacementPlans();
  };

  const handlePublishAll = async () => {
    if (!activeSchoolYear) {
      toast.error('Chưa có năm học hiện tại!');
      return;
    }

    if (totalEnrolled === 0) {
      toast.error('Chưa có học sinh nào được xếp vào lớp trong năm học này. Vui lòng lập và duyệt phương án xếp lớp trước khi công bố!');
      return;
    }

    if (
      !window.confirm(
        `Bạn có chắc chắn muốn công bố kết quả xếp lớp cho các lớp đã có học sinh trong Năm học ${activeSchoolYear.yearName}? Phụ huynh và giáo viên sẽ xem được danh sách lớp chính thức.`
      )
    ) {
      return;
    }

    setPublishingAll(true);
    try {
      const res = await classApi.publishAllPlacements(activeSchoolYear.id);
      if (res.success && res.data) {
        toast.success(
          `Đã công bố danh sách xếp lớp thành công cho ${res.data.length} lớp học!`
        );
        loadClasses();
      } else {
        toast.error(res.message || 'Lỗi khi công bố danh sách lớp.');
      }
    } catch {
      toast.error('Có lỗi xảy ra khi công bố danh sách lớp.');
    } finally {
      setPublishingAll(false);
    }
  };

  // KPI metrics
  const totalClasses = classes.length;
  const totalEnrolled = classes.reduce((sum, c) => sum + (c.enrolledCount ?? c.enrolledStudentsCount ?? 0), 0);
  const totalCapacity = classes.reduce((sum, c) => sum + c.capacity, 0);
  const fillRate = totalCapacity > 0 ? Math.round((totalEnrolled / totalCapacity) * 100) : 0;
  const publishedClassesCount = classes.filter((c) => c.isPlacementPublished).length;

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
                      Năm học {y.yearName} {y.isActive ? '(Hiện tại)' : ''}
                    </option>
                  ))}
                </select>
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
              </div>
            </div>
            <h1 style={{ margin: '4px 0 8px' }}>
              Lớp học & <span className="squiggle">Xếp lớp đầu năm</span>
            </h1>
            <p style={{ color: 'var(--color-muted-fg)', margin: 0 }}>
              Khởi tạo danh sách lớp, lập phương án thuật toán phân lớp cân bằng và trình Ban Giám hiệu phê duyệt.
            </p>
          </div>

          {/* Action Buttons */}
          <div style={{ display: 'flex', gap: 10, flexWrap: 'wrap' }}>
            {activeTab === 'classes' ? (
              <>
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
              </>
            ) : (
              <button
                type="button"
                className="btn btn--primary"
                onClick={() => setGeneratePlacementOpen(true)}
              >
                <Sparkles size={18} /> Lập phương án mới
              </button>
            )}
          </div>
        </div>

        {/* Tab Switcher: Lớp học chính thức vs Phương án xếp lớp đầu năm */}
        <div
          style={{
            display: 'flex',
            gap: 12,
            marginTop: 20,
            borderBottom: '2px solid #E2E8F0',
            paddingBottom: 2,
          }}
        >
          <button
            type="button"
            onClick={() => {
              setActiveTab('classes');
              setSearchParams({});
              setSelectedPlanDetail(null);
            }}
            style={{
              display: 'flex',
              alignItems: 'center',
              gap: 8,
              padding: '10px 18px',
              borderRadius: '14px 14px 0 0',
              border: activeTab === 'classes' ? '2.5px solid #1E293B' : '2px solid transparent',
              borderBottom: activeTab === 'classes' ? 'none' : undefined,
              backgroundColor: activeTab === 'classes' ? '#FFFFFF' : 'transparent',
              fontWeight: 800,
              color: activeTab === 'classes' ? '#0F172A' : '#64748B',
              cursor: 'pointer',
              fontSize: '0.95rem',
            }}
          >
            <GraduationCap size={18} color={activeTab === 'classes' ? '#0284C7' : '#64748B'} />
            Danh sách lớp & Phân công GVCN
          </button>

          <button
            type="button"
            onClick={() => {
              setActiveTab('placement');
              setSearchParams({ tab: 'placement' });
            }}
            style={{
              display: 'flex',
              alignItems: 'center',
              gap: 8,
              padding: '10px 18px',
              borderRadius: '14px 14px 0 0',
              border: activeTab === 'placement' ? '2.5px solid #1E293B' : '2px solid transparent',
              borderBottom: activeTab === 'placement' ? 'none' : undefined,
              backgroundColor: activeTab === 'placement' ? '#FFFFFF' : 'transparent',
              fontWeight: 800,
              color: activeTab === 'placement' ? '#0F172A' : '#64748B',
              cursor: 'pointer',
              fontSize: '0.95rem',
            }}
          >
            <Sparkles size={18} color={activeTab === 'placement' ? '#D97706' : '#64748B'} />
            Phương án xếp lớp đầu năm (SAWP Smart Placement)
            {placementPlans.filter((p) => p.status === 'Submitted').length > 0 && (
              <span
                style={{
                  backgroundColor: '#EF4444',
                  color: '#FFFFFF',
                  fontSize: '0.72rem',
                  fontWeight: 900,
                  padding: '1px 7px',
                  borderRadius: 12,
                }}
              >
                {placementPlans.filter((p) => p.status === 'Submitted').length} Chờ duyệt
              </span>
            )}
          </button>
        </div>
      </header>

      {/* TAB 1: DANH SÁCH LỚP HỌC CHÍNH THỨC */}
      {activeTab === 'classes' && (
        <>
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
                Năm học này chưa có lớp học nào cho khối đã chọn. Bạn có thể khởi tạo hàng loạt bằng nút bên dưới.
              </p>
              <button
                type="button"
                className="btn btn--primary"
                onClick={() => setBatchModalOpen(true)}
              >
                <Layers size={18} /> Khởi tạo lớp hàng loạt
              </button>
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
                  <div key={c.id} className="class-card">
                    {/* Header */}
                    <div style={{ display: 'flex', justifyContent: 'space-between', alignItems: 'flex-start', marginBottom: 12 }}>
                      <div>
                        <div style={{ display: 'flex', gap: 6, alignItems: 'center' }}>
                          <span
                            className="class-badge"
                            style={{
                              background: 'var(--color-accent)',
                              color: 'var(--color-ink)',
                              fontFamily: 'var(--font-heading)',
                              fontWeight: 900,
                              fontSize: 'var(--fs-lg)',
                              padding: '2px 10px',
                            }}
                          >
                            Lớp {c.className}
                          </span>
                          <span className="badge badge--yellow" style={{ fontSize: 'var(--fs-xs)' }}>
                            Khối {c.grade}
                          </span>
                        </div>
                        <div style={{ fontSize: 'var(--fs-xs)', color: 'var(--color-muted-fg)', marginTop: 4 }}>
                          Phòng: <strong>{c.roomNumber || 'Chưa xếp'}</strong>
                        </div>
                      </div>

                      {c.isPlacementPublished ? (
                        <span className="badge badge--green" style={{ fontSize: '0.7rem' }}>
                          <CheckCircle2 size={12} /> Đã công bố
                        </span>
                      ) : (
                        <span className="badge badge--orange" style={{ fontSize: '0.7rem' }}>
                          Chờ công bố
                        </span>
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
                  </div>
                );
              })}
            </div>
          )}
        </>
      )}

      {/* TAB 2: PHƯƠNG ÁN XẾP LỚP ĐẦU NĂM (SAWP Smart Placement) */}
      {activeTab === 'placement' && (
        <>
          {selectedPlanDetail ? (
            <DraftPlacementPreview
              plan={selectedPlanDetail}
              userRoles={userRoles}
              onPlanUpdated={(updated) => {
                setSelectedPlanDetail(updated);
                loadPlacementPlans();
                loadClasses();
              }}
              onBackToHistory={handleBackToPlanHistory}
            />
          ) : (
            <PlacementPlanHistoryList
              plans={placementPlans}
              loading={loadingPlans}
              userRoles={userRoles}
              onSelectPlan={handleSelectPlan}
              onOpenGenerateModal={() => setGeneratePlacementOpen(true)}
            />
          )}
        </>
      )}

      {/* Integrated Modals */}
      <CreateSchoolYearModal
        open={createYearModalOpen}
        onClose={() => setCreateYearModalOpen(false)}
        onYearCreated={(newYear) => {
          loadSchoolYears();
          setSelectedYearId(newYear.id);
          setActiveSchoolYear(newYear);
          loadClasses();
        }}
      />

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

      <GeneratePlacementModal
        isOpen={generatePlacementOpen}
        onClose={() => setGeneratePlacementOpen(false)}
        schoolYears={schoolYears}
        activeSchoolYearId={selectedYearId}
        availableClasses={classes}
        onGenerated={(newPlan) => {
          setSelectedPlanDetail(newPlan);
          setActiveTab('placement');
          setSearchParams({ tab: 'placement', planId: newPlan.id.toString() });
          loadPlacementPlans();
        }}
      />
    </div>
  );
}
