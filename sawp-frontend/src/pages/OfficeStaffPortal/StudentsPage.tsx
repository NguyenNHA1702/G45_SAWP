import React, { useEffect, useState } from 'react';
import {
  Download,
  Eye,
  FileSpreadsheet,
  Filter,
  GraduationCap,
  Plus,
  RefreshCw,
  Search,
  Sparkles,
  UserCheck,
  UserPlus,
  Users,
} from 'lucide-react';
import toast from 'react-hot-toast';
import { studentApi } from '../../services/implementations/studentApi';
import { classApi } from '../../services/implementations/classApi';
import { CreateStudentModal } from '../../components/students/CreateStudentModal';
import { DuplicateResolveModal } from '../../components/students/DuplicateResolveModal';
import { ExcelImportModal } from '../../components/students/ExcelImportModal';
import { StudentDetailModal } from '../../components/students/StudentDetailModal';
import type {
  CreateStudentDto,
  StudentFilterDto,
  StudentProfileDto,
} from '../../types/student.types';
import type { ClassSummaryDto } from '../../types/class.types';

export default function StudentsPage() {
  const [students, setStudents] = useState<StudentProfileDto[]>([]);
  const [classes, setClasses] = useState<ClassSummaryDto[]>([]);
  const [totalCount, setTotalCount] = useState(0);
  const [pageIndex, setPageIndex] = useState(1);
  const [pageSize] = useState(10);
  const [keyword, setKeyword] = useState('');
  const [selectedClassId, setSelectedClassId] = useState<number | undefined>(undefined);
  const [selectedStatus, setSelectedStatus] = useState<string | undefined>(undefined);
  const [loading, setLoading] = useState(false);

  // Modals state
  const [createModalOpen, setCreateModalOpen] = useState(false);
  const [importModalOpen, setImportModalOpen] = useState(false);
  const [detailModalOpen, setDetailModalOpen] = useState(false);
  const [selectedStudent, setSelectedStudent] = useState<StudentProfileDto | null>(null);

  // Duplicate resolution state
  const [duplicateModalOpen, setDuplicateModalOpen] = useState(false);
  const [pendingStudent, setPendingStudent] = useState<CreateStudentDto | null>(null);
  const [existingStudent, setExistingStudent] = useState<StudentProfileDto | null>(null);

  useEffect(() => {
    loadClasses();
  }, []);

  useEffect(() => {
    loadStudents();
  }, [pageIndex, selectedClassId, selectedStatus]);

  const loadClasses = async () => {
    try {
      const res = await classApi.getClasses();
      if (res.success && res.data) {
        setClasses(res.data);
      }
    } catch {
      // ignore
    }
  };

  const loadStudents = async () => {
    setLoading(true);
    try {
      const filter: StudentFilterDto = {
        pageIndex,
        pageSize,
        keyword: keyword.trim() || undefined,
        classId: selectedClassId,
        status: selectedStatus,
      };
      const res = await studentApi.getStudents(filter);
      if (res.success && res.data) {
        setStudents(res.data.items);
        setTotalCount(res.data.totalCount);
      }
    } catch {
      toast.error('Lỗi khi tải danh sách học sinh.');
    } finally {
      setLoading(false);
    }
  };

  const handleSearchSubmit = (e: React.FormEvent) => {
    e.preventDefault();
    setPageIndex(1);
    loadStudents();
  };

  const handleDownloadTemplate = async () => {
    try {
      const blob = await studentApi.downloadTemplate();
      const url = window.URL.createObjectURL(blob);
      const a = document.createElement('a');
      a.href = url;
      a.download = 'Mau_Danh_Sach_Hoc_Sinh_SAWP.xlsx';
      document.body.appendChild(a);
      a.click();
      a.remove();
      window.URL.revokeObjectURL(url);
      toast.success('Đã tải tệp mẫu Excel thành công!');
    } catch {
      toast.error('Lỗi khi tải tệp mẫu Excel.');
    }
  };

  // Stats calculation
  const activeCount = students.filter((s) => s.status === 'Active').length;
  const unassignedCount = students.filter((s) => !s.currentClassId).length;

  return (
    <div className="container pop-in">
      {/* Decorative Memphis Shapes */}
      <span className="decor decor--circle" style={{ top: -20, right: 30 }} aria-hidden="true" />
      <span className="decor decor--triangle" style={{ bottom: 30, left: 10 }} aria-hidden="true" />

      {/* Page Header */}
      <header className="page-header" style={{ marginBottom: 24 }}>
        <div style={{ display: 'flex', justifyContent: 'space-between', alignItems: 'flex-start', flexWrap: 'wrap', gap: 16 }}>
          <div>
            <span className="badge badge--mint" style={{ marginBottom: 8 }}>
              <Sparkles size={14} /> Quản lý Giáo vụ (Workflow 1)
            </span>
            <h1 style={{ margin: '4px 0 8px' }}>
              Hồ sơ <span className="squiggle">học sinh</span>
            </h1>
            <p style={{ color: 'var(--color-muted-fg)', margin: 0 }}>
              Quản lý hồ sơ định danh học sinh, nhập danh sách Excel hàng loạt, xử lý trùng lặp và phân lớp.
            </p>
          </div>

          {/* Quick Action Candy Buttons */}
          <div style={{ display: 'flex', gap: 10, flexWrap: 'wrap' }}>
            <button
              type="button"
              className="btn btn--secondary btn--sm"
              onClick={handleDownloadTemplate}
              title="Tải mẫu Excel chuẩn SAWP"
            >
              <Download size={16} /> Mẫu Excel
            </button>
            <button
              type="button"
              className="btn btn--secondary btn--sm"
              onClick={() => setImportModalOpen(true)}
            >
              <FileSpreadsheet size={16} color="var(--color-tertiary)" /> Nhập Excel
            </button>
            <button
              type="button"
              className="btn btn--primary"
              onClick={() => setCreateModalOpen(true)}
            >
              <UserPlus size={18} /> Thêm học sinh mới
            </button>
          </div>
        </div>
      </header>

      {/* Top Stat Cards */}
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
            <Users size={22} color="var(--color-accent)" strokeWidth={2.5} />
          </div>
          <div>
            <div style={{ fontSize: 'var(--fs-2xl)', fontFamily: 'var(--font-heading)', fontWeight: 800 }}>
              {totalCount}
            </div>
            <div style={{ fontSize: 'var(--fs-xs)', textTransform: 'uppercase', color: 'var(--color-muted-fg)', fontWeight: 700 }}>
              Tổng số học sinh
            </div>
          </div>
        </div>

        <div className="stat-card">
          <div className="stat-icon" style={{ background: 'var(--color-quaternary-soft)' }}>
            <UserCheck size={22} color="var(--color-quaternary)" strokeWidth={2.5} />
          </div>
          <div>
            <div style={{ fontSize: 'var(--fs-2xl)', fontFamily: 'var(--font-heading)', fontWeight: 800 }}>
              {activeCount}
            </div>
            <div style={{ fontSize: 'var(--fs-xs)', textTransform: 'uppercase', color: 'var(--color-muted-fg)', fontWeight: 700 }}>
              Hồ sơ kích hoạt (Active)
            </div>
          </div>
        </div>

        <div className="stat-card">
          <div className="stat-icon" style={{ background: 'var(--color-secondary-soft)' }}>
            <GraduationCap size={22} color="var(--color-secondary)" strokeWidth={2.5} />
          </div>
          <div>
            <div style={{ fontSize: 'var(--fs-2xl)', fontFamily: 'var(--font-heading)', fontWeight: 800 }}>
              {unassignedCount}
            </div>
            <div style={{ fontSize: 'var(--fs-xs)', textTransform: 'uppercase', color: 'var(--color-muted-fg)', fontWeight: 700 }}>
              Chưa phân lớp
            </div>
          </div>
        </div>
      </div>

      {/* Filter and Search Bar */}
      <div className="card" style={{ marginBottom: 20, padding: 16 }}>
        <form onSubmit={handleSearchSubmit} style={{ display: 'flex', gap: 12, alignItems: 'center', flexWrap: 'wrap' }}>
          <div style={{ position: 'relative', flex: 1, minWidth: 240 }}>
            <Search
              size={18}
              color="var(--color-muted-fg)"
              style={{ position: 'absolute', top: 13, left: 14 }}
            />
            <input
              type="text"
              className="input"
              style={{ paddingLeft: 40 }}
              placeholder="Tìm theo Mã học sinh hoặc Họ tên..."
              value={keyword}
              onChange={(e) => setKeyword(e.target.value)}
            />
          </div>

          <div style={{ minWidth: 160 }}>
            <select
              className="select"
              value={selectedClassId ?? ''}
              onChange={(e) => {
                setSelectedClassId(e.target.value ? Number(e.target.value) : undefined);
                setPageIndex(1);
              }}
            >
              <option value="">Tất cả các lớp</option>
              {classes.map((c) => (
                <option key={c.id} value={c.id}>
                  Lớp {c.className} (Khối {c.grade})
                </option>
              ))}
            </select>
          </div>

          <div style={{ minWidth: 140 }}>
            <select
              className="select"
              value={selectedStatus ?? ''}
              onChange={(e) => {
                setSelectedStatus(e.target.value || undefined);
                setPageIndex(1);
              }}
            >
              <option value="">Mọi trạng thái</option>
              <option value="Active">Đang học (Active)</option>
              <option value="Inactive">Tạm ngưng (Inactive)</option>
            </select>
          </div>

          <button type="submit" className="btn btn--primary btn--sm">
            <Filter size={15} /> Lọc kết quả
          </button>

          <button
            type="button"
            className="btn btn--secondary btn--sm"
            onClick={() => {
              setKeyword('');
              setSelectedClassId(undefined);
              setSelectedStatus(undefined);
              setPageIndex(1);
              loadStudents();
            }}
            title="Làm mới"
          >
            <RefreshCw size={15} />
          </button>
        </form>
      </div>

      {/* Main Students Data Table */}
      {loading ? (
        <div className="card" style={{ textAlign: 'center', padding: 48 }}>
          <RefreshCw size={32} className="spin" color="var(--color-accent)" style={{ margin: '0 auto 12px' }} />
          <div style={{ fontWeight: 700 }}>Đang tải danh sách học sinh...</div>
        </div>
      ) : students.length === 0 ? (
        <div className="card empty-state" style={{ textAlign: 'center', padding: 48 }}>
          <Users size={48} color="var(--color-muted-fg)" style={{ margin: '0 auto 12px' }} />
          <h3>Chưa có học sinh nào phù hợp</h3>
          <p style={{ color: 'var(--color-muted-fg)', maxWidth: 460, margin: '0 auto 16px' }}>
            Không tìm thấy học sinh theo điều kiện lọc. Bạn có thể thêm học sinh mới hoặc nhập dữ liệu hàng loạt từ Excel.
          </p>
          <button
            type="button"
            className="btn btn--primary btn--sm"
            onClick={() => setCreateModalOpen(true)}
          >
            <Plus size={16} /> Thêm học sinh đầu tiên
          </button>
        </div>
      ) : (
        <div className="data-table-wrapper">
          <table className="data-table">
            <thead>
              <tr>
                <th>Mã HS</th>
                <th>Họ và tên</th>
                <th>Ngày sinh</th>
                <th>Giới tính</th>
                <th>Lớp học</th>
                <th>GV Chủ nhiệm</th>
                <th>Phụ huynh liên hệ</th>
                <th>Trạng thái</th>
                <th style={{ textAlign: 'right' }}>Thao tác</th>
              </tr>
            </thead>
            <tbody>
              {students.map((s) => (
                <tr key={s.id}>
                  <td>
                    <span className="badge badge--violet" style={{ fontWeight: 700 }}>
                      {s.studentCode}
                    </span>
                  </td>
                  <td>
                    <strong style={{ fontSize: 'var(--fs-base)' }}>{s.fullName}</strong>
                  </td>
                  <td>{new Date(s.dateOfBirth).toLocaleDateString('vi-VN')}</td>
                  <td>{s.gender}</td>
                  <td>
                    {s.currentClassName ? (
                      <span className="badge badge--mint">Lớp {s.currentClassName}</span>
                    ) : (
                      <span className="badge badge--muted">Chưa xếp lớp</span>
                    )}
                  </td>
                  <td>{s.homeroomTeacherName || '-'}</td>
                  <td>
                    {s.primaryGuardianName ? (
                      <div>
                        <div>{s.primaryGuardianName}</div>
                        {s.primaryGuardianPhone && (
                          <small style={{ color: 'var(--color-muted-fg)' }}>
                            {s.primaryGuardianPhone}
                          </small>
                        )}
                      </div>
                    ) : (
                      <small style={{ color: 'var(--color-muted-fg)' }}>Chưa có</small>
                    )}
                  </td>
                  <td>
                    <span className={`badge ${s.status === 'Active' ? 'badge--mint' : 'badge--amber'}`}>
                      {s.status}
                    </span>
                  </td>
                  <td style={{ textAlign: 'right' }}>
                    <button
                      type="button"
                      className="btn btn--secondary btn--sm"
                      style={{ padding: '6px 12px' }}
                      onClick={() => {
                        setSelectedStudent(s);
                        setDetailModalOpen(true);
                      }}
                      title="Xem chi tiết hồ sơ"
                    >
                      <Eye size={15} /> Chi tiết
                    </button>
                  </td>
                </tr>
              ))}
            </tbody>
          </table>

          {/* Pagination bar */}
          <div
            style={{
              display: 'flex',
              alignItems: 'center',
              justifyContent: 'space-between',
              padding: '12px 18px',
              borderTop: 'var(--border)',
              background: 'var(--color-muted)',
              fontSize: 'var(--fs-sm)',
            }}
          >
            <div>
              Hiển thị <strong>{students.length}</strong> / <strong>{totalCount}</strong> học sinh
            </div>
            <div style={{ display: 'flex', gap: 8 }}>
              <button
                type="button"
                className="btn btn--secondary btn--sm"
                disabled={pageIndex <= 1}
                onClick={() => setPageIndex((p) => Math.max(1, p - 1))}
              >
                ← Trang trước
              </button>
              <span className="badge badge--violet" style={{ alignSelf: 'center' }}>
                Trang {pageIndex}
              </span>
              <button
                type="button"
                className="btn btn--secondary btn--sm"
                disabled={students.length < pageSize}
                onClick={() => setPageIndex((p) => p + 1)}
              >
                Trang sau →
              </button>
            </div>
          </div>
        </div>
      )}

      {/* Integrated Modals */}
      <CreateStudentModal
        open={createModalOpen}
        onClose={() => setCreateModalOpen(false)}
        classes={classes}
        onCreated={() => {
          loadStudents();
        }}
        onDuplicateDetected={(pending, existing) => {
          setPendingStudent(pending);
          setExistingStudent(existing);
          setDuplicateModalOpen(true);
        }}
      />

      <DuplicateResolveModal
        open={duplicateModalOpen}
        onClose={() => setDuplicateModalOpen(false)}
        pendingStudent={pendingStudent}
        existingStudent={existingStudent}
        onResolved={() => {
          loadStudents();
        }}
      />

      <ExcelImportModal
        open={importModalOpen}
        onClose={() => setImportModalOpen(false)}
        onImportCompleted={() => {
          loadStudents();
          loadClasses();
        }}
      />

      <StudentDetailModal
        open={detailModalOpen}
        onClose={() => setDetailModalOpen(false)}
        student={selectedStudent}
        onUpdated={() => {
          loadStudents();
        }}
      />
    </div>
  );
}
