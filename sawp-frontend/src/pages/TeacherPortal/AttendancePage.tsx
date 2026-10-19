import { useState } from 'react';
import { CalendarCheck2, Clock, Sparkles } from 'lucide-react';

export default function AttendancePage() {
  const [selectedClass, setSelectedClass] = useState('1A');

  return (
    <div className="container pop-in">
      <header className="page-header" style={{ marginBottom: 24 }}>
        <span className="badge badge--mint" style={{ marginBottom: 8 }}>
          <Sparkles size={14} /> Workflow 2 - Daily Attendance
        </span>
        <h1>
          Điểm danh <span className="squiggle">lớp học</span>
        </h1>
        <p style={{ color: 'var(--color-muted-fg)' }}>
          Ghi nhận chuyên cần hàng ngày cho học sinh. Vắng không phép sẽ tự động trừ suất ăn bán trú và gửi thông báo cho phụ huynh.
        </p>
      </header>

      <div className="card" style={{ marginBottom: 24 }}>
        <div style={{ display: 'flex', alignItems: 'center', gap: 16, flexWrap: 'wrap' }}>
          <div className="field" style={{ minWidth: 200 }}>
            <label className="label">Chọn Lớp học</label>
            <select
              className="select"
              value={selectedClass}
              onChange={(e) => setSelectedClass(e.target.value)}
            >
              <option value="1A">Lớp 1A (Chủ nhiệm)</option>
              <option value="1B">Lớp 1B (Tiết đầu)</option>
            </select>
          </div>

          <div className="field">
            <label className="label">Ngày điểm danh</label>
            <input type="date" className="input" defaultValue={new Date().toISOString().slice(0, 10)} />
          </div>

          <div style={{ marginLeft: 'auto', display: 'flex', alignItems: 'center', gap: 8 }}>
            <span className="badge badge--amber">
              <Clock size={14} /> Giờ chốt bếp: 10:30
            </span>
          </div>
        </div>
      </div>

      <div className="card empty-state">
        <CalendarCheck2 size={48} color="var(--color-accent)" />
        <h3>Sẵn sàng kết nối API Điểm danh</h3>
        <p style={{ maxWidth: 500 }}>
          Giao diện và API Điểm danh chi tiết đã được viết sẵn trong tài liệu <code>Documents/WORKFLOW2_ATTENDANCE_FE.md</code>.
          Khi bạn hoàn thành phần Backend hoặc muốn tích hợp bảng điểm danh đầy đủ, mình sẽ cập nhật bảng học sinh ngay lập tức!
        </p>
      </div>
    </div>
  );
}
