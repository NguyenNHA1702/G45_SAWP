import React from 'react';
import { ChevronLeft, ChevronRight } from 'lucide-react';

interface PaginationBarProps {
  pageIndex: number;
  pageSize: number;
  totalCount: number;
  onPageChange: (page: number) => void;
  onPageSizeChange?: (size: number) => void;
  pageSizeOptions?: number[];
  itemLabel?: string;
}

export const PaginationBar: React.FC<PaginationBarProps> = ({
  pageIndex,
  pageSize,
  totalCount,
  onPageChange,
  onPageSizeChange,
  pageSizeOptions = [10, 20, 50],
  itemLabel = 'mục',
}) => {
  const totalPages = Math.max(1, Math.ceil(totalCount / pageSize));
  if (totalCount === 0) return null;

  const from = (pageIndex - 1) * pageSize + 1;
  const to = Math.min(pageIndex * pageSize, totalCount);

  return (
    <div
      style={{
        display: 'flex',
        justifyContent: 'space-between',
        alignItems: 'center',
        flexWrap: 'wrap',
        gap: 12,
        padding: '12px 16px',
        marginTop: 12,
        backgroundColor: '#FFFFFF',
        border: '2px solid #1E293B',
        borderRadius: 12,
      }}
    >
      <div style={{ display: 'flex', alignItems: 'center', gap: 12, flexWrap: 'wrap', fontSize: 13, color: '#475569', fontWeight: 600 }}>
        <span>
          Hiển thị <strong>{from}–{to}</strong> / <strong>{totalCount.toLocaleString('vi-VN')}</strong> {itemLabel}
        </span>
        {onPageSizeChange && (
          <label style={{ display: 'inline-flex', alignItems: 'center', gap: 6 }}>
            <span>Mỗi trang:</span>
            <select
              className="input"
              value={pageSize}
              onChange={(e) => onPageSizeChange(Number(e.target.value))}
              style={{ width: 'auto', padding: '4px 8px', fontSize: 13, borderRadius: 8 }}
            >
              {pageSizeOptions.map((opt) => (
                <option key={opt} value={opt}>
                  {opt}
                </option>
              ))}
            </select>
          </label>
        )}
      </div>

      <div style={{ display: 'flex', alignItems: 'center', gap: 8 }}>
        <button
          type="button"
          className="btn btn--outline btn--sm"
          disabled={pageIndex <= 1}
          onClick={() => onPageChange(Math.max(1, pageIndex - 1))}
          style={{ display: 'inline-flex', alignItems: 'center', gap: 4, borderRadius: 8 }}
        >
          <ChevronLeft size={15} /> Trước
        </button>
        <span style={{ fontSize: 13, fontWeight: 700, color: '#1E293B', padding: '0 6px' }}>
          Trang {pageIndex} / {totalPages}
        </span>
        <button
          type="button"
          className="btn btn--outline btn--sm"
          disabled={pageIndex >= totalPages}
          onClick={() => onPageChange(Math.min(totalPages, pageIndex + 1))}
          style={{ display: 'inline-flex', alignItems: 'center', gap: 4, borderRadius: 8 }}
        >
          Sau <ChevronRight size={15} />
        </button>
      </div>
    </div>
  );
};
