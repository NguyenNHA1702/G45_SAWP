import { useCallback, useEffect, useState } from 'react';
import { useNavigate } from 'react-router';
import {
  Briefcase,
  Building2,
  Mail,
  Phone,
  RefreshCw,
  Search,
  UserCog,
  Users,
} from 'lucide-react';
import toast from 'react-hot-toast';
import { adminApi } from '../../services/implementations/adminApi';
import { getAuthUser } from '../../services/apiClient';
import { PaginationBar } from '../../components/common/PaginationBar';
import { getSystemMessage } from '../../constants/messages';
import type { AdminUserDto } from '../../types/admin.types';

const STAFF_ROLE_OPTIONS = [
  { value: 'Teacher', label: 'Giáo viên' },
  { value: 'OfficeStaff', label: 'Giáo vụ / Văn phòng' },
  { value: 'ServiceStaff', label: 'Nhân viên Bếp / Bán trú' },
  { value: 'SchoolBoard', label: 'Ban Giám hiệu' },
  { value: 'Admin', label: 'Quản trị hệ thống' },
];

const ROLE_BADGE_MAP: Record<string, { label: string; cls: string }> = {
  Teacher: { label: 'Giáo viên', cls: 'badge--mint' },
  OfficeStaff: { label: 'Giáo vụ', cls: 'badge--violet' },
  ServiceStaff: { label: 'Bếp / Bán trú', cls: 'badge--amber' },
  SchoolBoard: { label: 'Ban Giám hiệu', cls: 'badge--pink' },
  Admin: { label: 'Admin', cls: 'badge--violet' },
};

function renderStatusBadge(u: AdminUserDto) {
  if (u.status === 'PendingActivation') {
    return (
      <span className="badge badge--amber" style={{ border: '1.5px solid var(--color-fg)', fontWeight: 700 }}>
        Chờ kích hoạt
      </span>
    );
  }
  if (u.isActive) {
    return (
      <span className="badge badge--mint" style={{ border: '1.5px solid var(--color-fg)', fontWeight: 700 }}>
        Đang công tác
      </span>
    );
  }
  return (
    <span className="badge badge--pink" style={{ border: '1.5px solid var(--color-fg)', fontWeight: 700 }}>
      Đã khóa
    </span>
  );
}

export default function StaffListPage() {
  const navigate = useNavigate();
  const isAdmin = (() => {
    try {
      const user = JSON.parse(getAuthUser() ?? '{}');
      const rawRoles = user?.roles ?? (user?.role ? [user.role] : []);
      const roles: string[] = Array.isArray(rawRoles) ? rawRoles : [String(rawRoles)];
      return roles.some((r) => typeof r === 'string' && r.toLowerCase() === 'admin');
    } catch {
      return false;
    }
  })();

  const [staff, setStaff] = useState<AdminUserDto[]>([]);
  const [totalCount, setTotalCount] = useState(0);
  const [pageIndex, setPageIndex] = useState(1);
  const [pageSize, setPageSize] = useState(20);
  const [loading, setLoading] = useState(false);

  const [search, setSearch] = useState('');
  const [roleFilter, setRoleFilter] = useState('');
  const [statusFilter, setStatusFilter] = useState('');

  const loadStaff = useCallback(async () => {
    setLoading(true);
    try {
      const res = await adminApi.getStaff({
        pageIndex,
        pageSize,
        role: roleFilter || undefined,
        isActive: statusFilter === '' ? undefined : statusFilter === 'active',
        search: search.trim() || undefined,
      });
      if (res.success && res.data) {
        setStaff(res.data.items);
        setTotalCount(res.data.totalCount);
      }
    } catch {
      toast.error(getSystemMessage('MSG50')?.content || 'Không thể tải danh sách nhân sự.');
    } finally {
      setLoading(false);
    }
  }, [pageIndex, pageSize, roleFilter, statusFilter, search]);

  useEffect(() => {
    loadStaff();
  }, [loadStaff]);

  return (
    <div className="container" style={{ display: 'flex', flexDirection: 'column', gap: 20 }}>
      {/* Header */}
      <div className="card" style={{ display: 'flex', justifyContent: 'space-between', alignItems: 'center', flexWrap: 'wrap', gap: 12 }}>
        <div>
          <span className="badge badge--violet" style={{ marginBottom: 8 }}>
            <Users size={13} /> Danh sách Nhân sự & Giáo viên
          </span>
          <h1 style={{ margin: '4px 0 4px' }}>Danh bạ Cán bộ, Giáo viên & Nhân viên</h1>
          <p style={{ margin: 0, color: 'var(--color-muted-fg)', fontSize: 'var(--fs-sm)' }}>
            Tra cứu thông tin mã nhân viên, chức vụ, phòng ban, lớp chủ nhiệm và liên hệ của toàn trường.
          </p>
        </div>

        <div style={{ display: 'flex', gap: 10, flexWrap: 'wrap', alignItems: 'center' }}>
          <button type="button" className="btn btn--secondary btn--sm" onClick={loadStaff} disabled={loading}>
            <RefreshCw size={15} /> Làm mới
          </button>
          {isAdmin && (
            <button type="button" className="btn btn--primary btn--sm" onClick={() => navigate('/admin/users')}>
              <UserCog size={15} /> Quản lý tài khoản
            </button>
          )}
        </div>
      </div>

      {/* Filters */}
      <div className="card" style={{ display: 'flex', flexWrap: 'wrap', gap: 12, alignItems: 'center' }}>
        <div style={{ position: 'relative', flex: '1 1 240px' }}>
          <Search size={16} style={{ position: 'absolute', left: 12, top: '50%', transform: 'translateY(-50%)', color: 'var(--color-muted-fg)' }} />
          <input
            className="input"
            style={{ width: '100%', paddingLeft: 36 }}
            placeholder="Tìm theo tên, mã NV, phòng ban, SĐT, email..."
            value={search}
            onChange={(e) => {
              setPageIndex(1);
              setSearch(e.target.value);
            }}
          />
        </div>

        <select
          className="select"
          style={{ width: 'auto', minWidth: 180, flex: '0 1 210px' }}
          value={roleFilter}
          onChange={(e) => {
            setPageIndex(1);
            setRoleFilter(e.target.value);
          }}
        >
          <option value="">Tất cả vai trò</option>
          {STAFF_ROLE_OPTIONS.map((r) => (
            <option key={r.value} value={r.value}>
              {r.label}
            </option>
          ))}
        </select>

        <select
          className="select"
          style={{ width: 'auto', minWidth: 160, flex: '0 1 180px' }}
          value={statusFilter}
          onChange={(e) => {
            setPageIndex(1);
            setStatusFilter(e.target.value);
          }}
        >
          <option value="">Tất cả trạng thái</option>
          <option value="active">Đang công tác</option>
          <option value="inactive">Đã khóa / Chờ kích hoạt</option>
        </select>

        <span className="badge badge--amber" style={{ border: '1.5px solid var(--color-fg)', fontWeight: 800 }}>
          Tổng: {totalCount} nhân sự
        </span>
      </div>

      {/* Content */}
      <div className="card" style={{ padding: 0, overflow: 'hidden' }}>
        {loading ? (
          <div style={{ padding: 40, textAlign: 'center', color: 'var(--color-muted-fg)' }}>
            Đang tải danh sách nhân sự...
          </div>
        ) : staff.length === 0 ? (
          <div style={{ padding: 40, textAlign: 'center', fontWeight: 700, color: 'var(--color-muted-fg)' }}>
            {getSystemMessage('MSG43')?.content || 'Không có dữ liệu phù hợp để hiển thị.'}
          </div>
        ) : (
          <>
            {/* Desktop Table */}
            <div className="desktop-table-wrap" style={{ overflowX: 'auto' }}>
              <table className="data-table">
                <thead>
                  <tr>
                    <th>Mã NV</th>
                    <th>Họ và tên</th>
                    <th>Vai trò</th>
                    <th>Chức vụ / Phòng ban</th>
                    <th>Lớp chủ nhiệm</th>
                    <th>Liên hệ</th>
                    <th style={{ textAlign: 'center' }}>Trạng thái</th>
                  </tr>
                </thead>
                <tbody>
                  {staff.map((u) => (
                    <tr key={u.id}>
                      <td style={{ fontWeight: 800, color: 'var(--color-accent)' }}>
                        {u.staffCode || '—'}
                      </td>
                      <td>
                        <div style={{ fontWeight: 700 }}>{u.fullName}</div>
                        <div style={{ fontSize: 'var(--fs-xs)', color: 'var(--color-muted-fg)' }}>@{u.username}</div>
                      </td>
                      <td>
                        <div style={{ display: 'flex', gap: 4, flexWrap: 'wrap' }}>
                          {u.roles.map((r) => {
                            const cfg = ROLE_BADGE_MAP[r];
                            return (
                              <span key={r} className={`badge ${cfg?.cls ?? 'badge--muted'}`} style={{ fontSize: '0.72rem' }}>
                                {cfg?.label ?? r}
                              </span>
                            );
                          })}
                        </div>
                      </td>
                      <td>
                        <div style={{ fontWeight: 600 }}>{u.position || '—'}</div>
                        {u.department && (
                          <div style={{ fontSize: 'var(--fs-xs)', color: 'var(--color-muted-fg)' }}>{u.department}</div>
                        )}
                      </td>
                      <td>
                        {u.homeroomClassName ? (
                          <span className="badge badge--mint" style={{ border: '1.5px solid var(--color-fg)', fontWeight: 800 }}>
                            Lớp {u.homeroomClassName}
                          </span>
                        ) : (
                          <span style={{ color: 'var(--color-muted-fg)' }}>—</span>
                        )}
                      </td>
                      <td>
                        <div style={{ display: 'flex', flexDirection: 'column', gap: 3, fontSize: 'var(--fs-xs)' }}>
                          <span style={{ display: 'inline-flex', alignItems: 'center', gap: 5 }}>
                            <Mail size={12} /> {u.email}
                          </span>
                          {u.phoneNumber && (
                            <a
                              href={`tel:${u.phoneNumber}`}
                              style={{ color: 'var(--color-accent)', fontWeight: 700, textDecoration: 'none', display: 'inline-flex', alignItems: 'center', gap: 5 }}
                            >
                              <Phone size={12} /> {u.phoneNumber}
                            </a>
                          )}
                        </div>
                      </td>
                      <td style={{ textAlign: 'center' }}>{renderStatusBadge(u)}</td>
                    </tr>
                  ))}
                </tbody>
              </table>
            </div>

            {/* Mobile Card List */}
            <div className="mobile-card-list" style={{ padding: 12 }}>
              {staff.map((u) => (
                <div key={`m-${u.id}`} className="mobile-item-card">
                  <div className="mobile-item-card__head">
                    <div style={{ minWidth: 0 }}>
                      <div style={{ display: 'flex', alignItems: 'center', gap: 6, flexWrap: 'wrap' }}>
                        {u.staffCode && (
                          <span className="badge badge--violet" style={{ border: '1px solid var(--color-fg)', fontWeight: 800, padding: '1px 6px', fontSize: '0.7rem' }}>
                            {u.staffCode}
                          </span>
                        )}
                        <strong style={{ fontSize: 'var(--fs-sm)', color: 'var(--color-fg)' }}>{u.fullName}</strong>
                      </div>
                      <div className="mobile-item-card__meta" style={{ marginTop: 4 }}>
                        <span>@{u.username}</span>
                        {u.homeroomClassName && (
                          <>
                            <span>•</span>
                            <span style={{ color: '#059669', fontWeight: 800 }}>CN Lớp {u.homeroomClassName}</span>
                          </>
                        )}
                      </div>
                    </div>
                    {renderStatusBadge(u)}
                  </div>

                  <div style={{ display: 'flex', flexWrap: 'wrap', gap: 6 }}>
                    {u.roles.map((r) => {
                      const cfg = ROLE_BADGE_MAP[r];
                      return (
                        <span key={r} className={`badge ${cfg?.cls ?? 'badge--muted'}`} style={{ fontSize: '0.72rem' }}>
                          {cfg?.label ?? r}
                        </span>
                      );
                    })}
                  </div>

                  {(u.position || u.department) && (
                    <div style={{ display: 'flex', flexWrap: 'wrap', gap: 10, fontSize: 'var(--fs-xs)', color: 'var(--color-fg)' }}>
                      {u.position && (
                        <span style={{ display: 'inline-flex', alignItems: 'center', gap: 4 }}>
                          <Briefcase size={12} /> {u.position}
                        </span>
                      )}
                      {u.department && (
                        <span style={{ display: 'inline-flex', alignItems: 'center', gap: 4, color: 'var(--color-muted-fg)' }}>
                          <Building2 size={12} /> {u.department}
                        </span>
                      )}
                    </div>
                  )}

                  <div className="mobile-item-card__foot">
                    <span style={{ fontSize: 'var(--fs-xs)', color: 'var(--color-muted-fg)', wordBreak: 'break-all' }}>
                      {u.email}
                    </span>
                    {u.phoneNumber && (
                      <a
                        href={`tel:${u.phoneNumber}`}
                        className="btn btn--secondary btn--sm"
                        style={{ padding: '4px 10px', fontSize: 'var(--fs-xs)', textDecoration: 'none' }}
                      >
                        <Phone size={12} /> {u.phoneNumber}
                      </a>
                    )}
                  </div>
                </div>
              ))}
            </div>
          </>
        )}

        <div style={{ padding: '12px 16px', borderTop: 'var(--border)' }}>
          <PaginationBar
            pageIndex={pageIndex}
            totalCount={totalCount}
            pageSize={pageSize}
            onPageChange={setPageIndex}
            onPageSizeChange={(s) => {
              setPageSize(s);
              setPageIndex(1);
            }}
          />
        </div>
      </div>
    </div>
  );
}
