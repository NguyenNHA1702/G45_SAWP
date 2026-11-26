import { useCallback, useEffect, useState } from 'react';
import {
  ChevronLeft,
  ChevronRight,
  ChevronsLeft,
  ChevronsRight,
  Clock,
  Code2,
  Eye,
  FileText,
  RefreshCw,
  Search,
  User,
} from 'lucide-react';
import toast from 'react-hot-toast';
import { systemApi } from '../../services/implementations/systemApi';
import type { AuditLogDto } from '../../types/system.types';
import { getSystemMessage } from '../../constants/messages';
import { GeometricModal } from '../../components/common/GeometricModal';

const ENTITY_LABELS: Record<string, string> = {
  User: 'Tài khoản người dùng',
  SchoolYear: 'Năm học',
  SchoolCalendarDay: 'Lịch năm học',
  Class: 'Lớp học',
  PlacementPlan: 'Phương án xếp lớp',
  Student: 'Học sinh',
  AttendanceRecord: 'Phiếu điểm danh',
  AdministrativeRequest: 'Đơn hành chính',
  LunchServiceDay: 'Lịch bán trú',
  LunchRegistration: 'Đăng ký ăn trưa',
  PostComment: 'Bình luận sổ liên lạc',
  LeaveRequest: 'Đơn xin nghỉ học',
  AccountRequest: 'Yêu cầu cấp tài khoản',
};

const FIELD_LABELS: Record<string, string> = {
  Status: 'Trạng thái',
  Reason: 'Lý do',
  CorrectionReason: 'Lý do điều chỉnh',
  Remarks: 'Ghi chú',
  ClassId: 'Mã lớp',
  StudentId: 'Mã học sinh',
  Date: 'Ngày',
  Session: 'Buổi',
  IsActive: 'Hoạt động',
  IsServing: 'Phục vụ ăn trưa',
  CutoffTime: 'Giờ chốt suất',
  FullName: 'Họ và tên',
  Roles: 'Vai trò',
  Role: 'Vai trò',
  IsSchoolDay: 'Ngày học chính thức',
  Note: 'Ghi chú',
  ParentsNotified: 'Số phụ huynh đã báo',
};

const ACTION_LABELS: Record<string, string> = {
  USERCREATE: 'Tạo tài khoản mới',
  USERUPDATEROLES: 'Cập nhật phân quyền',
  USERACTIVATE: 'Kích hoạt tài khoản',
  USERDEACTIVATE: 'Tạm khóa tài khoản',
  USERRESETPASSWORD: 'Đặt lại mật khẩu',
  USERCHANGEPASSWORD: 'Đổi mật khẩu',
  USERRECOVERPASSWORD: 'Khôi phục mật khẩu',
  USERACTIVATEBYLINK: 'Kích hoạt qua email',
  USERACTIVATIONLINKSENT: 'Gửi link kích hoạt',
  SCHOOLCALENDARDAYCONFIGURE: 'Cập nhật lịch năm học',
  SCHOOLYEARCREATE: 'Tạo năm học mới',
  SCHOOLYEARACTIVATE: 'Kích hoạt năm học',
  SCHOOLYEARDELETE: 'Xóa năm học',
  SCHOOLYEARPUBLISHPLACEMENTS: 'Công bố kết quả xếp lớp',
  CLASSCREATE: 'Tạo lớp học mới',
  CLASSINITIALIZEBATCH: 'Khởi tạo hàng loạt lớp học',
  CLASSUPDATE: 'Cập nhật thông tin lớp',
  CLASSASSIGNHOMEROOMTEACHER: 'Phân công GVCN',
  CLASSUNASSIGNHOMEROOMTEACHER: 'Gỡ phân công GVCN',
  CLASSASSIGNSTUDENTS: 'Xếp học sinh vào lớp',
  CLASSREMOVESTUDENT: 'Rút học sinh khỏi lớp',
  CLASSDELETE: 'Xóa lớp học',
  CLASSPUBLISHPLACEMENT: 'Công bố danh sách lớp',
  PLACEMENTPLANGENERATEDRAFT: 'Tạo phương án xếp lớp nháp',
  PLACEMENTPLANMOVESTUDENT: 'Chuyển học sinh trong phương án',
  PLACEMENTPLANSWAPSTUDENTS: 'Hoán đổi học sinh giữa 2 lớp',
  PLACEMENTPLANSUBMIT: 'Trình phương án xếp lớp',
  PLACEMENTPLANAPPROVE: 'Phê duyệt phương án xếp lớp',
  PLACEMENTPLANREJECT: 'Từ chối phương án xếp lớp',
  STUDENTCREATE: 'Thêm hồ sơ học sinh',
  STUDENTUPDATE: 'Cập nhật hồ sơ học sinh',
  STUDENTACTIVATE: 'Kích hoạt hồ sơ học sinh',
  STUDENTRESOLVEDUPLICATE: 'Xử lý trùng hồ sơ học sinh',
  STUDENTBULKIMPORT: 'Nhập học sinh từ Excel',
  STUDENTGUARDIANLINK: 'Liên kết phụ huynh – học sinh',
  STUDENTGUARDIANUPDATE: 'Cập nhật thông tin phụ huynh',
  STUDENTGUARDIANUNLINK: 'Hủy liên kết phụ huynh',
  ACCOUNTREQUESTSAVEDRAFT: 'Lưu nháp yêu cầu cấp tài khoản',
  ACCOUNTREQUESTSUBMIT: 'Gửi yêu cầu cấp tài khoản',
  ACCOUNTREQUESTDECIDE: 'Duyệt yêu cầu cấp tài khoản',
  ACCOUNTREQUESTCLOSEROWS: 'Đóng dòng yêu cầu tài khoản',
  ACCOUNTREQUESTRESUBMIT: 'Gửi lại yêu cầu tài khoản',
  ACCOUNTREQUESTRESENDACTIVATION: 'Gửi lại email kích hoạt',
  ATTENDANCESUBMIT: 'Lưu bảng điểm danh',
  ATTENDANCEUPDATE: 'Cập nhật điểm danh',
  ATTENDANCECORRECT: 'Điều chỉnh điểm danh',
  ATTENDANCEABSENCENOTICESEND: 'Gửi thông báo vắng mặt',
  TEACHERREQUESTREATTENDANCE: 'GV yêu cầu điểm danh lại',
  REQUESTREATTENDANCE: 'Yêu cầu điểm danh lại',
  LEAVEREQUESTSUBMIT: 'Nộp đơn xin nghỉ học',
  PARENTSUBMITLEAVEREQUEST: 'Phụ huynh nộp đơn xin nghỉ',
  LEAVEREQUESTFORWARD: 'Chuyển đơn nghỉ lên BGH',
  LEAVEREQUESTAPPROVE: 'Duyệt đơn xin nghỉ học',
  LEAVEREQUESTREJECT: 'Từ chối đơn xin nghỉ học',
  LEAVEREQUESTCANCEL: 'Hủy đơn xin nghỉ học',
  LEAVEREQUESTREQUESTMOREINFO: 'Yêu cầu bổ sung đơn nghỉ',
  LEAVEREQUESTRESUBMIT: 'Gửi lại đơn xin nghỉ học',
  LEAVEREQUESTWITHDRAW: 'Rút đơn xin nghỉ học',
  CLASSTRANSFERREQUESTEXECUTE: 'Thực hiện chuyển lớp',
  ADMINISTRATIVEREQUESTSUBMIT: 'Gửi đơn hành chính',
  ADMINISTRATIVEREQUESTAMENDMENT: 'Yêu cầu bổ sung đơn hành chính',
  ADMINISTRATIVEREQUESTRESUBMIT: 'Gửi lại đơn hành chính',
  ADMINISTRATIVEREQUESTENDORSE: 'Giáo vụ trình đơn lên BGH',
  ADMINISTRATIVEREQUESTDIRECTISSUE: 'Cấp giấy tờ trực tiếp',
  ADMINISTRATIVEREQUESTBOARDRETURN: 'BGH trả đơn về Giáo vụ',
  ADMINISTRATIVEREQUESTAPPROVE: 'BGH phê duyệt đơn hành chính',
  ADMINISTRATIVEREQUESTREJECT: 'BGH từ chối đơn hành chính',
  ADMINISTRATIVEREQUESTCANCEL: 'Hủy đơn hành chính',
  CANCELREQUEST: 'Hủy đơn yêu cầu',
  LUNCHSERVICEDAYCONFIGURE: 'Cấu hình ngày phục vụ bán trú',
  LUNCHSERVICEDAYCONFIGURECUTOFF: 'Cập nhật giờ chốt suất ăn',
  LUNCHSERVICEDAYLOCK: 'Khóa sổ suất ăn bán trú',
  LUNCHSERVICEDAYUNLOCK: 'Mở khóa sổ suất ăn bán trú',
  LUNCHMEALRESTORE: 'Khôi phục suất ăn bán trú',
  LUNCHREGISTRATIONWAITLIST: 'Đăng ký ăn trưa (chờ)',
  LUNCHREGISTRATIONUPDATEMEALTYPE: 'Đổi chế độ ăn bán trú',
  LUNCHMONTHLYQUOTACONFIGURE: 'Cấu hình định mức ăn tháng',
  LUNCHMONTHLYQUOTACLEAR: 'Xóa định mức ăn tháng',
  BOARDINGINCIDENTCREATE: 'Ghi nhận sự cố bán trú',
  POSTCOMMENTDELETE: 'Xóa bình luận sổ liên lạc',
};

function translateEntity(entityName: string): string {
  return ENTITY_LABELS[entityName] || entityName;
}

function formatEntityId(entityId: string | null): string {
  if (!entityId) return '';
  return entityId
    .replace(/:Morning\b/gi, ' · Buổi sáng')
    .replace(/:Afternoon\b/gi, ' · Buổi chiều')
    .replace(/:FullDay\b/gi, ' · Cả ngày');
}

function translateAction(action: string): string {
  const normalized = action.replace(/[\s_-]+/g, '').toUpperCase();
  if (ACTION_LABELS[normalized]) return ACTION_LABELS[normalized];

  if (normalized.includes('REATTENDANCE')) return 'Yêu cầu điểm danh lại';
  if (normalized.includes('ABSENCENOTICE')) return 'Gửi thông báo vắng mặt';
  if (normalized.includes('CORRECT') && normalized.includes('ATTENDANCE')) return 'Điều chỉnh điểm danh';
  if (normalized.includes('ATTENDANCE')) return 'Cập nhật điểm danh';
  if (normalized.includes('IMPORT')) return 'Nhập dữ liệu từ Excel';
  if (normalized.includes('APPROVE')) return 'Phê duyệt';
  if (normalized.includes('REJECT')) return 'Từ chối';
  if (normalized.includes('RETURN')) return 'Trả lại chỉnh sửa';
  if (normalized.includes('DEACTIVATE')) return 'Tạm khóa';
  if (normalized.includes('ACTIVATE')) return 'Kích hoạt';
  if (normalized.includes('CREATE')) return 'Tạo mới';
  if (normalized.includes('UPDATE') || normalized.includes('UPSERT') || normalized.includes('CONFIG')) return 'Cập nhật thông tin';
  if (normalized.includes('DELETE') || normalized.includes('REMOVE')) return 'Xóa dữ liệu';
  if (normalized.includes('LOGIN')) return 'Đăng nhập hệ thống';
  return action.replace(/([A-Z])/g, ' $1').replace(/_/g, ' ').trim();
}

function formatFriendlyValue(val: unknown): string {
  if (val === null || val === undefined || val === '') return '—';
  if (typeof val === 'boolean') return val ? 'Có' : 'Không';
  if (Array.isArray(val)) return val.join(', ');
  if (typeof val === 'object') return JSON.stringify(val);
  const str = String(val);
  if (str === 'Morning') return 'Buổi sáng';
  if (str === 'Afternoon') return 'Buổi chiều';
  if (str === 'Present') return 'Có mặt';
  if (str === 'Late') return 'Đi muộn';
  if (str === 'ExcusedAbsence') return 'Vắng có phép';
  if (str === 'UnexcusedAbsence') return 'Vắng không phép';
  return str;
}

function parseFriendlyPairs(raw: string | null): Array<{ label: string; value: string }> {
  if (!raw || !raw.trim()) return [];
  try {
    const parsed = JSON.parse(raw);
    if (parsed && typeof parsed === 'object' && !Array.isArray(parsed)) {
      return Object.entries(parsed as Record<string, unknown>).map(([k, v]) => ({
        label: FIELD_LABELS[k] || k,
        value: formatFriendlyValue(v),
      }));
    }
  } catch {
    // Fallback for semicolon-separated key=value strings
    if (raw.includes('=')) {
      return raw
        .split(';')
        .map((part) => part.trim())
        .filter(Boolean)
        .map((part) => {
          const idx = part.indexOf('=');
          if (idx === -1) return { label: 'Thông tin', value: part };
          const k = part.slice(0, idx).trim();
          const v = part.slice(idx + 1).trim();
          return { label: FIELD_LABELS[k] || k, value: formatFriendlyValue(v) };
        });
    }
  }
  return [{ label: 'Nội dung', value: raw }];
}

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
  const [showRawTech, setShowRawTech] = useState<boolean>(false);

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
        toast.error(msg || res.message || 'Không thể tải nhật ký hoạt động.');
      }
    } catch {
      toast.error(getSystemMessage('MSG42')?.content || 'Bạn không có quyền xem nhật ký hoạt động.');
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
    setPageIndex(targetPage < 1 ? 1 : targetPage);
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
    if (upper.includes('CORRECT') || upper.includes('CONFIG') || upper.includes('RESET') || upper.includes('REATTENDANCE')) return 'badge badge--amber';
    return 'badge badge--violet';
  };

  const isOutOfRange = totalPages > 0 && pageIndex > totalPages;
  const effectiveLastPage = Math.max(1, totalPages);

  return (
    <div className="container" style={{ display: 'flex', flexDirection: 'column', gap: 20 }}>
      {/* Header */}
      <div className="card" style={{ display: 'flex', justifyContent: 'space-between', alignItems: 'center', flexWrap: 'wrap', gap: 12 }}>
        <div>
          <span className="badge badge--violet" style={{ marginBottom: 8 }}>
            <FileText size={13} /> Theo dõi hoạt động nhà trường
          </span>
          <h1 style={{ margin: '4px 0 4px' }}>Nhật ký hoạt động</h1>
          <p style={{ margin: 0, color: 'var(--color-muted-fg)', fontSize: 'var(--fs-sm)' }}>
            Xem lại lịch sử các thao tác quan trọng của giáo viên, giáo vụ và ban giám hiệu theo thời gian thực.
          </p>
        </div>
        <button type="button" className="btn btn--secondary btn--sm" onClick={loadLogs} disabled={loading}>
          <RefreshCw size={15} className={loading ? 'animate-spin' : ''} /> Tải lại
        </button>
      </div>

      {/* Filters & Search */}
      <div className="card">
        <form onSubmit={handleSearch} style={{ display: 'flex', gap: 12, flexWrap: 'wrap', alignItems: 'center' }}>
          <div style={{ position: 'relative', flex: '1 1 240px' }}>
            <Search size={16} style={{ position: 'absolute', left: 12, top: '50%', transform: 'translateY(-50%)', color: 'var(--color-muted-fg)' }} />
            <input
              type="text"
              className="input"
              style={{ paddingLeft: 36, width: '100%' }}
              placeholder="Tìm theo tên người thực hiện, nội dung..."
              value={searchInput}
              onChange={(e) => setSearchInput(e.target.value)}
            />
          </div>

          <select
            className="select"
            style={{ width: 'auto', minWidth: 190, flex: '0 1 220px' }}
            value={entityFilter}
            onChange={(e) => {
              setPageIndex(1);
              setEntityFilter(e.target.value);
            }}
          >
            <option value="">Tất cả mục quản lý</option>
            <option value="AttendanceRecord">Điểm danh học sinh</option>
            <option value="AdministrativeRequest">Đơn từ & Giấy tờ</option>
            <option value="LeaveRequest">Đơn xin nghỉ học</option>
            <option value="LunchServiceDay">Lịch bán trú</option>
            <option value="LunchRegistration">Đăng ký ăn trưa</option>
            <option value="Student">Hồ sơ học sinh</option>
            <option value="Class">Lớp học</option>
            <option value="PlacementPlan">Phương án xếp lớp</option>
            <option value="SchoolCalendarDay">Lịch năm học</option>
            <option value="User">Tài khoản người dùng</option>
          </select>

          <select
            className="select"
            style={{ width: 'auto', minWidth: 140, flex: '0 1 160px' }}
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

      {/* Content */}
      <div className="card" style={{ padding: 0, overflow: 'hidden' }}>
        {loading ? (
          <div style={{ padding: 40, textAlign: 'center', color: 'var(--color-muted-fg)' }}>
            Đang tải nhật ký hoạt động...
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
          <>
            {/* Desktop Table */}
            <div className="desktop-table-wrap" style={{ overflowX: 'auto' }}>
              <table className="data-table">
                <thead>
                  <tr>
                    <th>Thời gian</th>
                    <th>Người thực hiện</th>
                    <th>Hoạt động</th>
                    <th>Mục liên quan</th>
                    <th style={{ textAlign: 'center' }}>Chi tiết</th>
                  </tr>
                </thead>
                <tbody>
                  {items.map((log) => (
                    <tr key={log.id}>
                      <td style={{ whiteSpace: 'nowrap', fontWeight: 600 }}>
                        {new Date(log.timestamp).toLocaleString('vi-VN')}
                      </td>
                      <td>
                        <div style={{ fontWeight: 700 }}>{log.userFullName || 'Hệ thống tự động'}</div>
                        {log.username && (
                          <div style={{ fontSize: 'var(--fs-xs)', color: 'var(--color-muted-fg)' }}>@{log.username}</div>
                        )}
                      </td>
                      <td>
                        <span className={getActionBadgeClass(log.action)}>{translateAction(log.action)}</span>
                      </td>
                      <td>
                        <span style={{ fontWeight: 700 }}>{translateEntity(log.entityName)}</span>
                        {log.entityId && <span style={{ color: 'var(--color-muted-fg)' }}> (Mã #{formatEntityId(log.entityId)})</span>}
                      </td>
                      <td style={{ textAlign: 'center' }}>
                        <button
                          type="button"
                          className="btn btn--secondary btn--sm"
                          onClick={() => {
                            setShowRawTech(false);
                            setSelectedLog(log);
                          }}
                        >
                          <Eye size={14} /> Xem chi tiết
                        </button>
                      </td>
                    </tr>
                  ))}
                </tbody>
              </table>
            </div>

            {/* Mobile Card List */}
            <div className="mobile-card-list" style={{ padding: 12 }}>
              {items.map((log) => (
                <div key={`m-${log.id}`} className="mobile-item-card">
                  <div className="mobile-item-card__head">
                    <div>
                      <strong style={{ fontSize: 'var(--fs-sm)', color: 'var(--color-fg)' }}>
                        {log.userFullName || 'Hệ thống tự động'}
                      </strong>
                      <div className="mobile-item-card__meta" style={{ marginTop: 4 }}>
                        <Clock size={12} />
                        <span>{new Date(log.timestamp).toLocaleString('vi-VN')}</span>
                      </div>
                    </div>
                    <span className={getActionBadgeClass(log.action)}>{translateAction(log.action)}</span>
                  </div>

                  <div style={{ fontSize: 'var(--fs-xs)', color: 'var(--color-fg)' }}>
                    Mục: <strong>{translateEntity(log.entityName)}</strong>
                    {log.entityId && <span style={{ color: 'var(--color-muted-fg)' }}> (Mã #{formatEntityId(log.entityId)})</span>}
                  </div>

                  <div className="mobile-item-card__foot">
                    <span style={{ fontSize: 'var(--fs-xs)', color: 'var(--color-muted-fg)' }}>
                      {log.username ? `@${log.username}` : 'Tự động'}
                    </span>
                    <button
                      type="button"
                      className="btn btn--secondary btn--sm"
                      onClick={() => {
                        setShowRawTech(false);
                        setSelectedLog(log);
                      }}
                    >
                      <Eye size={13} /> Xem chi tiết
                    </button>
                  </div>
                </div>
              ))}
            </div>
          </>
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
            Tổng cộng <strong>{totalCount}</strong> hoạt động • Trang <strong>{pageIndex}</strong> /{' '}
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

      {/* Friendly Detail Modal */}
      <GeometricModal
        open={selectedLog !== null}
        onClose={() => setSelectedLog(null)}
        title={selectedLog ? translateAction(selectedLog.action) : 'Chi tiết hoạt động'}
        subtitle={
          selectedLog
            ? `${translateEntity(selectedLog.entityName)}${selectedLog.entityId ? ` (Mã #${formatEntityId(selectedLog.entityId)})` : ''} • ${new Date(selectedLog.timestamp).toLocaleString('vi-VN')}`
            : ''
        }
        badgeText="Chi tiết hoạt động"
        badgeColor="violet"
        icon={<FileText size={20} color="var(--color-fg)" />}
        iconBg="var(--color-tertiary)"
        maxWidth="lg"
      >
        {selectedLog && (
          <div style={{ display: 'flex', flexDirection: 'column', gap: 16 }}>
            <div
              className="modal-grid-2col"
              style={{
                padding: 14,
                background: '#FFFDF5',
                border: 'var(--border)',
                borderRadius: 'var(--radius-md)',
                fontSize: 'var(--fs-sm)',
              }}
            >
              <div style={{ display: 'flex', alignItems: 'center', gap: 8 }}>
                <User size={16} color="var(--color-accent)" />
                <div>
                  <div style={{ fontSize: 'var(--fs-xs)', color: 'var(--color-muted-fg)', fontWeight: 700 }}>NGƯỜI THỰC HIỆN</div>
                  <strong>{selectedLog.userFullName || 'Hệ thống tự động'}</strong>
                </div>
              </div>
              <div style={{ display: 'flex', alignItems: 'center', gap: 8 }}>
                <Clock size={16} color="var(--color-accent)" />
                <div>
                  <div style={{ fontSize: 'var(--fs-xs)', color: 'var(--color-muted-fg)', fontWeight: 700 }}>THỜI GIAN</div>
                  <strong>{new Date(selectedLog.timestamp).toLocaleString('vi-VN')}</strong>
                </div>
              </div>
            </div>

            {parseFriendlyPairs(selectedLog.oldValues).length > 0 && (
              <div>
                <div style={{ fontWeight: 800, fontSize: 'var(--fs-sm)', marginBottom: 6 }}>Thông tin trước khi thay đổi</div>
                <div style={{ display: 'grid', gap: 6, padding: 12, background: 'var(--color-muted)', border: 'var(--border)', borderRadius: 'var(--radius-sm)' }}>
                  {parseFriendlyPairs(selectedLog.oldValues).map((p, idx) => (
                    <div key={idx} style={{ display: 'flex', justifyContent: 'space-between', gap: 12, fontSize: 'var(--fs-xs)', flexWrap: 'wrap' }}>
                      <span style={{ color: 'var(--color-muted-fg)', fontWeight: 700 }}>{p.label}:</span>
                      <span style={{ fontWeight: 700, color: 'var(--color-fg)' }}>{p.value}</span>
                    </div>
                  ))}
                </div>
              </div>
            )}

            {parseFriendlyPairs(selectedLog.newValues).length > 0 && (
              <div>
                <div style={{ fontWeight: 800, fontSize: 'var(--fs-sm)', marginBottom: 6 }}>Nội dung thực hiện / Giá trị mới</div>
                <div style={{ display: 'grid', gap: 6, padding: 12, background: '#ECFDF5', border: 'var(--border)', borderRadius: 'var(--radius-sm)' }}>
                  {parseFriendlyPairs(selectedLog.newValues).map((p, idx) => (
                    <div key={idx} style={{ display: 'flex', justifyContent: 'space-between', gap: 12, fontSize: 'var(--fs-xs)', flexWrap: 'wrap' }}>
                      <span style={{ color: '#065F46', fontWeight: 700 }}>{p.label}:</span>
                      <span style={{ fontWeight: 800, color: '#064E3B' }}>{p.value}</span>
                    </div>
                  ))}
                </div>
              </div>
            )}

            <div style={{ display: 'flex', justifyContent: 'space-between', alignItems: 'center', flexWrap: 'wrap', gap: 10, paddingTop: 4 }}>
              <button
                type="button"
                className="btn btn--secondary btn--sm"
                onClick={() => setShowRawTech((v) => !v)}
              >
                <Code2 size={14} /> {showRawTech ? 'Ẩn dữ liệu kỹ thuật' : 'Xem dữ liệu kỹ thuật (JSON)'}
              </button>
              <button type="button" className="btn btn--primary btn--sm" onClick={() => setSelectedLog(null)}>
                Đóng
              </button>
            </div>

            {showRawTech && (
              <div style={{ display: 'grid', gap: 10 }}>
                <div>
                  <div style={{ fontWeight: 700, fontSize: 'var(--fs-xs)', marginBottom: 4 }}>OldValues</div>
                  <pre style={{ margin: 0, padding: 10, background: 'var(--color-muted)', border: 'var(--border)', borderRadius: 10, overflowX: 'auto', fontSize: 11 }}>
                    {formatJsonPretty(selectedLog.oldValues)}
                  </pre>
                </div>
                <div>
                  <div style={{ fontWeight: 700, fontSize: 'var(--fs-xs)', marginBottom: 4 }}>NewValues</div>
                  <pre style={{ margin: 0, padding: 10, background: 'var(--color-muted)', border: 'var(--border)', borderRadius: 10, overflowX: 'auto', fontSize: 11 }}>
                    {formatJsonPretty(selectedLog.newValues)}
                  </pre>
                </div>
              </div>
            )}
          </div>
        )}
      </GeometricModal>
    </div>
  );
}
