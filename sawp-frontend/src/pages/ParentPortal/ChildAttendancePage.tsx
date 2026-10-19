import { ClipboardList, Sparkles } from 'lucide-react';

export default function ChildAttendancePage() {
  return (
    <div className="container pop-in">
      <header className="page-header" style={{ marginBottom: 24 }}>
        <span className="badge badge--pink" style={{ marginBottom: 8 }}>
          <Sparkles size={14} /> Cổng phụ huynh
        </span>
        <h1>
          Chuyên cần <span className="squiggle">của con</span>
        </h1>
        <p style={{ color: 'var(--color-muted-fg)' }}>
          Theo dõi lịch sử điểm danh, chuyên cần và suất ăn bán trú theo từng tháng.
        </p>
      </header>

      <div className="card empty-state">
        <ClipboardList size={48} color="var(--color-secondary)" />
        <h3>Lịch chuyên cần học sinh</h3>
        <p style={{ maxWidth: 500 }}>
          Xem chi tiết các ngày có mặt, đi muộn, hoặc vắng có phép/không phép của học sinh được liên kết.
        </p>
      </div>
    </div>
  );
}
