import { GraduationCap, Sparkles } from 'lucide-react';

export default function ClassesPage() {
  return (
    <div className="container pop-in">
      <header className="page-header" style={{ marginBottom: 24 }}>
        <span className="badge badge--violet" style={{ marginBottom: 8 }}>
          <Sparkles size={14} /> Quản trị lớp học
        </span>
        <h1>
          Lớp học & <span className="squiggle">Phân công</span>
        </h1>
        <p style={{ color: 'var(--color-muted-fg)' }}>
          Quản lý danh sách lớp, giáo viên chủ nhiệm và thời khóa biểu giảng dạy.
        </p>
      </header>

      <div className="card empty-state">
        <GraduationCap size={48} color="var(--color-tertiary)" />
        <h3>Phân hệ Lớp học & Thời khóa biểu</h3>
        <p style={{ maxWidth: 500 }}>
          Trang này dành cho Giáo vụ phân lớp học sinh và phân công giáo viên giảng dạy theo năm học.
        </p>
      </div>
    </div>
  );
}
