import { Sparkles, Users } from 'lucide-react';

export default function StudentsPage() {
  return (
    <div className="container pop-in">
      <header className="page-header" style={{ marginBottom: 24 }}>
        <span className="badge badge--mint" style={{ marginBottom: 8 }}>
          <Sparkles size={14} /> Quản lý học sinh
        </span>
        <h1>
          Hồ sơ <span className="squiggle">học sinh</span>
        </h1>
        <p style={{ color: 'var(--color-muted-fg)' }}>
          Danh sách hồ sơ học sinh, thông tin phụ huynh và phân bổ lớp học.
        </p>
      </header>

      <div className="card empty-state">
        <Users size={48} color="var(--color-accent)" />
        <h3>Phân hệ Quản lý Học sinh</h3>
        <p style={{ maxWidth: 500 }}>
          Trang này dành cho bộ phận Giáo vụ & Quản trị viên quản lý danh sách học sinh toàn trường.
        </p>
      </div>
    </div>
  );
}
