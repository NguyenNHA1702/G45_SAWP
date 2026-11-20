import { useCallback, useEffect, useState } from 'react';
import {
  ChevronLeft,
  ChevronRight,
  ChevronsLeft,
  ChevronsRight,
  Eye,
  RefreshCw,
  Search,
  ShieldAlert,
  X,
} from 'lucide-react';
import toast from 'react-hot-toast';
import { systemApi } from '../../services/implementations/systemApi';
import type { AuditLogDto } from '../../types/system.types';
import { getSystemMessage } from '../../constants/messages';

function formatJsonPretty(raw: string | null): string {
  if (!raw) return '—';
  try {
    return JSON.stringify(JSON.parse(raw), null, 2);
  } catch {
    return raw;
  }
}

export default function AuditLogsPage() {
  const [items, setItems] = useState<AuditLogDto[]>([]);
  const [loading, setLoading] = useState<boolean>(true);
  const [pageIndex, setPageIndex] = useState<number>(1);
  const [pageSize, setPageSize] = useState<number>(20);
  const [totalCount, setTotalCount] = useState<number>(0);
  const [totalPages, setTotalPages] = useState<number>(0);

  const [searchInput, setSearchInput] = useState<string>('');
  const [searchQuery, setSearchQuery] = useState<string>('');
  const [entityFilter, setEntityFilter] = useState<string>('');
  const [jumpPageInput, setJumpPageInput] = useState<string>('1');
  const [selectedLog, setSelectedLog] = useState<AuditLogDto | null>(null);

  const loadLogs = useCallback(async () => {
    setLoading(true);
    try {
      const res = await systemApi.getAuditLogs({
        pageIndex,
        pageSize,
        search: searchQuery || undefined,
        entityName: entityFilter || undefined,
      });

      if (res.success && res.data) {
        setItems(res.data.items);
        setTotalCount(res.data.totalCount);
        setTotalPages(res.data.totalPages);
        setJumpPageInput(String(res.data.pageIndex));
      } else {
        const msg = res.code ? getSystemMessage(res.code)?.content : undefined;
        toast.error(msg || res.message || 'Không thể tải nhật ký hệ thống.');
      }
    } catch {
      toast.error(getSystemMessage('MSG42')?.content || 'Bạn không có quyền xem nhật ký hệ thống.');
    } finally {
      setLoading(false);
    }
  }, [pageIndex, pageSize, searchQuery, entityFilter]);

  useEffect(() => {
    loadLogs();
  }, [loadLogs]);

  const handleSearch = (e: React.FormEvent) => {
    e.preventDefault();
    setPageIndex(1);
    setSearchQuery(searchInput.trim());
  };

  const handleGoToPage = (targetPage: number) => {
    if (targetPage < 1) {
      setPageIndex(1);
      return;
    }
    setPageIndex(targetPage);
  };

  const handleJumpSubmit = (e: React.FormEvent) => {
    e.preventDefault();
    const parsed = Number(jumpPageInput);
    if (!Number.isFinite(parsed) || Number.isNaN(parsed)) {
      toast.error(getSystemMessage('MSG01')?.content || 'Vui lòng nhập số trang hợp lệ.');
      return;
    }
    setPageIndex(Math.trunc(parsed));
  };

  const getActionBadgeClass = (action: string) => {
    const upper = action.toUpperCase();
    if (upper.includes('DELETE') || upper.includes('DEACTIVATE') || upper.includes('REJECT')) return 'badge badge--pink';
    if (upper.includes('CREATE') || upper.includes('APPROVE') || upper.includes('ACTIVATE')) return 'badge badge--mint';
    if (upper.includes('CORRECT') || upper.includes('CONFIG') || upper.includes('RESET')) return 'badge badge--amber';
    return 'badge badge--violet';
  };

  const isOutOfRange = totalPages > 0 && pageIndex > totalPages;
  const effectiveLastPage = Math.max(1, totalPages);

  return (
    <div style={{ display: 'flex', flexDirection: 'column', gap: 20 }}>
      {/* Header */}
      <div className="card" style={{ display: 'flex', justifyContent: 'space-between', alignItems: 'center', flexWrap: 'wrap', gap: 12 }}>
        <div>
          <span className="badge badge--violet" style={{ marginBottom: 8 }}>
            <ShieldAlert size={13} /> Giám sát & Bảo mật
          </span>
          <h1 style={{ margin: '4px 0 4px' }}>Nhật ký hệ thống (Audit Logs)</h1>
          <p style={{ margin: 0, color: 'var(--color-muted-fg)', fontSize: 'var(--fs-sm)' }}>
            Theo dõi toàn bộ thay đổi dữ liệu, điều chỉnh điểm danh, phân quyền và cấu hình hệ thống.
          </p>
        </div>
        <button type="button" className="btn btn--secondary btn--sm" onClick={loadLogs} disabled={loading}>
          <RefreshCw size={15} className={loading ? 'animate-spin' : ''} /> Tải lại
        </button>
      </div>

      {/* Filters & Search */}
      <div className="card">
        <form onSubmit={handleSearch} style={{ display: 'flex', gap: 12, flexWrap: 'wrap', alignItems: 'center' }}>
          <div style={{ position: 'relative', flex: '1 1 260px' }}>
            <Search size={16} style={{ position: 'absolute', left: 12, top: '50%', transform: 'translateY(-50%)', color: 'var(--color-muted-fg)' }} />
            <input
              type="text"
              className="input"
              style={{ paddingLeft: 36, width: '100%' }}
              placeholder="Tìm theo tài khoản, hành động, đối tượng, dữ liệu..."
              value={searchInput}
              onChange={(e) => setSearchInput(e.target.value)}
            />
          </div>

          <select
            className="input"
            style={{ width: 200 }}
            value={entityFilter}
            onChange={(e) => {
              setPageIndex(1);
              setEntityFilter(e.target.value);
            }}
          >
            <option value="">Tất cả đối tượng</option>
            <option value="User">Tài khoản (User)</option>
            <option value="SchoolYear">Năm học (SchoolYear)</option>
            <option value="SchoolCalendarDay">Lịch năm học (SchoolCalendarDay)</option>
            <option value="Class">Lớp học (Class)</option>
            <option value="PlacementPlan">Phương án xếp lớp (PlacementPlan)</option>
            <option value="Student">Học sinh (Student)</option>
            <option value="AttendanceRecord">Điểm danh (AttendanceRecord)</option>
            <option value="AdministrativeRequest">Đơn từ, yêu cầu (AdministrativeRequest)</option>
            <option value="LunchServiceDay">Ngày phục vụ bán trú (LunchServiceDay)</option>
            <option value="LunchRegistration">Đăng ký bán trú (LunchRegistration)</option>
            <option value="LunchMonthlyQuota">Chỉ tiêu bán trú (LunchMonthlyQuota)</option>
            <option value="BoardingIncident">Sự cố bán trú (BoardingIncident)</option>
            <option value="PostComment">Bình luận sổ liên lạc (PostComment)</option>
          </select>

          <select
            className="input"
            style={{ width: 150 }}
            value={pageSize}
            onChange={(e) => {
              setPageIndex(1);
              setPageSize(Number(e.target.value));
            }}
          >
            <option value={10}>10 dòng / trang</option>
            <option value={20}>20 dòng / trang</option>
            <option value={50}>50 dòng / trang</option>
          </select>

          <button type="submit" className="btn btn--primary btn--sm">
            Tìm kiếm
          </button>
        </form>
      </div>

      {/* Table */}
      <div className="card" style={{ padding: 0, overflow: 'hidden' }}>
        {loading ? (
          <div style={{ padding: 40, textAlign: 'center', color: 'var(--color-muted-fg)' }}>
            Đang tải dữ liệu nhật ký hệ thống...
          </div>
        ) : items.length === 0 ? (
          <div style={{ padding: 40, textAlign: 'center' }}>
            <p style={{ fontWeight: 700, marginBottom: 8 }}>
              {getSystemMessage('MSG43')?.content || 'Không có dữ liệu phù hợp để hiển thị.'}
            </p>
            {isOutOfRange && (
              <p style={{ color: 'var(--color-muted-fg)', fontSize: 'var(--fs-sm)', marginBottom: 14 }}>
                Trang yêu cầu ({pageIndex}) vượt quá tổng số trang hiện có ({totalPages}).
              </p>
            )}
            {(isOutOfRange || searchQuery || entityFilter) && (
              <button
                type="button"
                className="btn btn--secondary btn--sm"
                onClick={() => {
                  setSearchInput('');
                  setSearchQuery('');
                  setEntityFilter('');
                  setPageIndex(1);
                }}
              >
                Quay về trang 1
              </button>
            )}
          </div>
        ) : (
          <div style={{ overflowX: 'auto' }}>
            <table style={{ width: '100%', borderCollapse: 'collapse', fontSize: 'var(--fs-sm)' }}>
              <thead>
                <tr style={{ background: 'var(--color-muted)', borderBottom: 'var(--border)' }}>
                  <th style={{ padding: '12px 14px', textAlign: 'left', fontWeight: 800 }}>Thời gian</th>
                  <th style={{ padding: '12px 14px', textAlign: 'left', fontWeight: 800 }}>Người thực hiện</th>
                  <th style={{ padding: '12px 14px', textAlign: 'left', fontWeight: 800 }}>Hành động</th>
                  <th style={{ padding: '12px 14px', textAlign: 'left', fontWeight: 800 }}>Đối tượng</th>
                  <th style={{ padding: '12px 14px', textAlign: 'center', fontWeight: 800 }}>Chi tiết</th>
                </tr>
              </thead>
              <tbody>
                {items.map((log) => (
                  <tr key={log.id} style={{ borderBottom: '1px solid var(--color-border)' }}>
                    <td style={{ padding: '10px 14px', whiteSpace: 'nowrap' }}>
                      {new Date(log.timestamp).toLocaleString('vi-VN')}
                    </td>
                    <td style={{ padding: '10px 14px' }}>
                      <div style={{ fontWeight: 700 }}>{log.userFullName || 'Hệ thống'}</div>
                      {log.username && (
                        <div style={{ fontSize: 12, color: 'var(--color-muted-fg)' }}>@{log.username}</div>
                      )}
                    </td>
                    <td style={{ padding: '10px 14px' }}>
                      <span className={getActionBadgeClass(log.action)}>{log.action}</span>
                    </td>
                    <td style={{ padding: '10px 14px' }}>
                      <span style={{ fontWeight: 600 }}>{log.entityName}</span>
                      {log.entityId && <span style={{ color: 'var(--color-muted-fg)' }}> #{log.entityId}</span>}
                    </td>
                    <td style={{ padding: '10px 14px', textAlign: 'center' }}>
                      <button
                        type="button"
                        className="btn btn--secondary btn--sm"
                        onClick={() => setSelectedLog(log)}
                      >
                        <Eye size={14} /> Xem
                      </button>
                    </td>
                  </tr>
                ))}
              </tbody>
            </table>
          </div>
        )}

        {/* Pagination Bar */}
        <div
          style={{
            display: 'flex',
            justifyContent: 'space-between',
            alignItems: 'center',
            flexWrap: 'wrap',
            gap: 12,
            padding: '12px 16px',
            borderTop: 'var(--border)',
            background: 'var(--color-bg)',
          }}
        >
          <div style={{ fontSize: 'var(--fs-sm)', color: 'var(--color-muted-fg)' }}>
            Tổng cộng <strong>{totalCount}</strong> bản ghi • Trang <strong>{pageIndex}</strong> /{' '}
            <strong>{effectiveLastPage}</strong>
          </div>

          <div style={{ display: 'flex', alignItems: 'center', gap: 6, flexWrap: 'wrap' }}>
            <button
              type="button"
              className="btn btn--secondary btn--sm"
              disabled={pageIndex <= 1 || loading}
              onClick={() => handleGoToPage(1)}
              title="Trang đầu"
            >
              <ChevronsLeft size={14} /> Đầu
            </button>
            <button
              type="button"
              className="btn btn--secondary btn--sm"
              disabled={pageIndex <= 1 || loading}
              onClick={() => handleGoToPage(pageIndex - 1)}
              title="Trang trước"
            >
              <ChevronLeft size={14} /> Trước
            </button>

            <form onSubmit={handleJumpSubmit} style={{ display: 'flex', alignItems: 'center', gap: 4 }}>
              <input
                type="number"
                className="input"
                style={{ width: 68, textAlign: 'center', padding: '4px 8px' }}
                value={jumpPageInput}
                onChange={(e) => setJumpPageInput(e.target.value)}
                aria-label="Số trang"
              />
              <button type="submit" className="btn btn--secondary btn--sm">
                Đi tới
              </button>
            </form>

            <button
              type="button"
              className="btn btn--secondary btn--sm"
              disabled={totalPages === 0 || pageIndex >= totalPages || loading}
              onClick={() => handleGoToPage(pageIndex + 1)}
              title="Trang sau"
            >
              Sau <ChevronRight size={14} />
            </button>
            <button
              type="button"
              className="btn btn--secondary btn--sm"
              disabled={totalPages === 0 || pageIndex >= totalPages || loading}
              onClick={() => handleGoToPage(effectiveLastPage)}
              title="Trang cuối"
            >
              Cuối <ChevronsRight size={14} />
            </button>
          </div>
        </div>
      </div>

      {/* Detail Modal */}
      {selectedLog && (
        <div
          style={{
            position: 'fixed',
            inset: 0,
            background: 'rgba(15, 23, 42, 0.55)',
            display: 'grid',
            placeItems: 'center',
            zIndex: 1000,
            padding: 16,
          }}
          onClick={() => setSelectedLog(null)}
        >
          <div
            className="card"
            style={{ width: '100%', maxWidth: 680, maxHeight: '85vh', overflowY: 'auto' }}
            onClick={(e) => e.stopPropagation()}
          >
            <div style={{ display: 'flex', justifyContent: 'space-between', alignItems: 'center', marginBottom: 14 }}>
              <h3 style={{ margin: 0 }}>Chi tiết Audit Log #{selectedLog.id}</h3>
              <button type="button" className="btn btn--secondary btn--sm" onClick={() => setSelectedLog(null)}>
                <X size={15} /> Đóng
              </button>
            </div>

            <div style={{ display: 'grid', gridTemplateColumns: '1fr 1fr', gap: 10, fontSize: 'var(--fs-sm)', marginBottom: 14 }}>
              <div><strong>Thời gian:</strong> {new Date(selectedLog.timestamp).toLocaleString('vi-VN')}</div>
              <div><strong>Người thao tác:</strong> {selectedLog.userFullName || 'Hệ thống'} ({selectedLog.username || 'N/A'})</div>
              <div><strong>Hành động:</strong> {selectedLog.action}</div>
              <div><strong>Đối tượng:</strong> {selectedLog.entityName} #{selectedLog.entityId || '—'}</div>
            </div>

            <div style={{ display: 'grid', gap: 12 }}>
              <div>
                <div style={{ fontWeight: 700, fontSize: 'var(--fs-sm)', marginBottom: 4 }}>Giá trị cũ (OldValues)</div>
                <pre style={{ margin: 0, padding: 12, background: 'var(--color-muted)', border: 'var(--border)', borderRadius: 12, overflowX: 'auto', fontSize: 12 }}>
                  {formatJsonPretty(selectedLog.oldValues)}
                </pre>
              </div>
              <div>
                <div style={{ fontWeight: 700, fontSize: 'var(--fs-sm)', marginBottom: 4 }}>Giá trị mới (NewValues)</div>
                <pre style={{ margin: 0, padding: 12, background: 'var(--color-muted)', border: 'var(--border)', borderRadius: 12, overflowX: 'auto', fontSize: 12 }}>
                  {formatJsonPretty(selectedLog.newValues)}
                </pre>
              </div>
            </div>
          </div>
        </div>
      )}
    </div>
  );
}
