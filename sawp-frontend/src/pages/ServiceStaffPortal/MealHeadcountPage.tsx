import { Sparkles, UtensilsCrossed } from 'lucide-react';

export default function MealHeadcountPage() {
  return (
    <div className="container pop-in">
      <header className="page-header" style={{ marginBottom: 24 }}>
        <span className="badge badge--amber" style={{ marginBottom: 8 }}>
          <Sparkles size={14} /> Tổ Bán trú & Bếp ăn
        </span>
        <h1>
          Báo cáo <span className="squiggle">suất ăn bán trú</span>
        </h1>
        <p style={{ color: 'var(--color-muted-fg)' }}>
          Thống kê số lượng suất ăn theo ngày và số suất tự động trừ do học sinh vắng học.
        </p>
      </header>

      <div className="card empty-state">
        <UtensilsCrossed size={48} color="var(--color-tertiary)" />
        <h3>Tổng hợp khẩu phần ăn trong ngày</h3>
        <p style={{ maxWidth: 500 }}>
          Dữ liệu khẩu phần (Suất thường, Suất chay, Chế độ đặc biệt) chốt số liệu tự động lúc 10:30 mỗi ngày học.
        </p>
      </div>
    </div>
  );
}
