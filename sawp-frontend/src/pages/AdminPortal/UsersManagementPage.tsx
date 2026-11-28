import { useCallback, useEffect, useRef, useState } from 'react';
import {
  KeyRound,
  Lock,
  Mail,
  Plus,
  RefreshCw,
  Search,
  Shield,
  Trash2,
  Unlock,
  Upload,
  UserCog,
  X,
} from 'lucide-react';
import toast from 'react-hot-toast';
import { adminApi } from '../../services/implementations/adminApi';
import { UserRole, type AdminUserDto, type RoleOptionDto } from '../../types/admin.types';
import { getSystemMessage } from '../../constants/messages';
import {
  validateEmailAddress,
  validatePersonFullName,
  validatePhoneNumber,
  validateUsername,
} from '../../utils/validation';

/** Account state (BR-63): Pending Activation is neither active nor locked. */
const STATUS_BADGE: Record<AdminUserDto['status'], { label: string; className: string }> = {
  Active: { label: 'Hoạt động', className: 'badge badge--mint' },
  PendingActivation: { label: 'Chờ kích hoạt', className: 'badge badge--amber' },
  Deactivated: { label: 'Đã khóa', className: 'badge badge--pink' },
};

export default function UsersManagementPage() {
  const [users, setUsers] = useState<AdminUserDto[]>([]);
  const [roles, setRoles] = useState<RoleOptionDto[]>([]);
  const [loading, setLoading] = useState(true);
  const [pageIndex, setPageIndex] = useState(1);
  const [totalPages, setTotalPages] = useState(0);
  const [totalCount, setTotalCount] = useState(0);

  const [searchInput, setSearchInput] = useState('');
  const [searchQuery, setSearchQuery] = useState('');
  const [roleFilter, setRoleFilter] = useState('');
  const [statusFilter, setStatusFilter] = useState<string>('');

  // Create modal
  const [showCreateModal, setShowCreateModal] = useState(false);
  const [newUsername, setNewUsername] = useState('');
  const [newFullName, setNewFullName] = useState('');
  const [newEmail, setNewEmail] = useState('');
  const [newPhone, setNewPhone] = useState('');
  // pub:UC-53 step 3: one initial role per new account
  const [newRole, setNewRole] = useState<string>(UserRole.Teacher);

  // Edit roles modal
  const [editingRolesUser, setEditingRolesUser] = useState<AdminUserDto | null>(null);
  const [selectedRoles, setSelectedRoles] = useState<string[]>([]);

  // Reset password modal
  const [resettingUser, setResettingUser] = useState<AdminUserDto | null>(null);
  const [resetPasswordVal, setResetPasswordVal] = useState('');

  const fileInputRef = useRef<HTMLInputElement>(null);

  const loadData = useCallback(async () => {
    setLoading(true);
    try {
      const [usersRes, rolesRes] = await Promise.all([
        adminApi.getUsers({
          pageIndex,
          pageSize: 15,
          role: roleFilter || undefined,
          isActive: statusFilter === '' ? undefined : statusFilter === 'true',
          search: searchQuery || undefined,
        }),
        adminApi.getRoles(),
      ]);

      if (usersRes.success && usersRes.data) {
        setUsers(usersRes.data.items);
        setTotalPages(usersRes.data.totalPages);
        setTotalCount(usersRes.data.totalCount);
      }
      if (rolesRes.success && rolesRes.data) {
        setRoles(rolesRes.data);
      }
    } catch {
      toast.error(getSystemMessage('MSG50')?.content || 'Đã xảy ra lỗi hệ thống.');
    } finally {
      setLoading(false);
    }
  }, [pageIndex, roleFilter, statusFilter, searchQuery]);

  useEffect(() => {
    loadData();
  }, [loadData]);

  const showApiError = (code: string | null, fallback: string, errors?: string[] | null) => {
    const sysMsg = code ? getSystemMessage(code)?.content : undefined;
    toast.error(errors?.[0] || sysMsg || fallback);
  };

  const handleCreateUser = async (e: React.FormEvent) => {
    e.preventDefault();
    const validationErr =
      validateUsername(newUsername) ||
      validatePersonFullName(newFullName) ||
      validateEmailAddress(newEmail, true) ||
      validatePhoneNumber(newPhone);
    if (validationErr) {
      toast.error(validationErr);
      return;
    }

    try {
      const res = await adminApi.createUser({
        username: newUsername.trim(),
        fullName: newFullName.trim().replace(/\s+/g, ' '),
        email: newEmail.trim(),
        phoneNumber: newPhone.trim() || undefined,
        roles: [newRole],
      });
      if (res.success) {
        toast.success(getSystemMessage('MSG38')?.content || res.message);
        // The account exists, but the activation email could not be sent (MSG59)
        res.errors?.forEach((m) => toast.error(m, { duration: 8000 }));
        setShowCreateModal(false);
        setNewUsername('');
        setNewFullName('');
        setNewEmail('');
        setNewPhone('');
        setNewRole(UserRole.Teacher);
        loadData();
      } else {
        showApiError(res.code, res.message, res.errors);
      }
    } catch (err: unknown) {
      const msg = (err as { response?: { data?: { message?: string; errors?: string[] } } })?.response?.data;
      toast.error(msg?.errors?.[0] || msg?.message || getSystemMessage('MSG01')?.content || 'Lỗi tạo tài khoản.');
    }
  };

  const handleSaveRoles = async () => {
    if (!editingRolesUser) return;
    try {
      const res = await adminApi.updateUserRoles(editingRolesUser.id, selectedRoles);
      if (res.success) {
        toast.success(getSystemMessage('MSG40')?.content || res.message);
        setEditingRolesUser(null);
        loadData();
      } else {
        showApiError(res.code, res.message, res.errors);
      }
    } catch (err: unknown) {
      const msg = (err as { response?: { data?: { message?: string; errors?: string[] } } })?.response?.data;
      toast.error(msg?.errors?.[0] || msg?.message || 'Không thể cập nhật vai trò.');
    }
  };

  const handleToggleStatus = async (user: AdminUserDto) => {
    const targetIsActive = user.status === 'Deactivated';
    const confirmText = getSystemMessage('MSG45')?.content || 'Bạn có chắc chắn muốn thực hiện thao tác này?';
    if (!window.confirm(`${confirmText}\n(${targetIsActive ? 'Mở khóa' : 'Khóa'} tài khoản ${user.username})`)) {
      return;
    }
    try {
      const res = await adminApi.updateUserStatus(user.id, targetIsActive);
      if (res.success) {
        toast.success(
          targetIsActive
            ? getSystemMessage('MSG08')?.content || 'Đã kích hoạt tài khoản.'
            : getSystemMessage('MSG39')?.content || 'Đã vô hiệu hóa tài khoản người dùng.'
        );
        loadData();
      } else {
        showApiError(res.code, res.message, res.errors);
      }
    } catch (err: unknown) {
      const msg = (err as { response?: { data?: { message?: string; errors?: string[] } } })?.response?.data;
      toast.error(msg?.errors?.[0] || msg?.message || 'Không thể đổi trạng thái tài khoản.');
    }
  };

  const handleResendActivation = async (user: AdminUserDto) => {
    try {
      const res = await adminApi.resendActivationLink(user.id);
      if (res.success) {
        toast.success(`Đã gửi lại liên kết kích hoạt tới ${user.email}.`);
      } else {
        showApiError(res.code, res.message, res.errors);
      }
    } catch (err: unknown) {
      const msg = (err as { response?: { data?: { message?: string; errors?: string[] } } })?.response?.data;
      toast.error(msg?.errors?.[0] || msg?.message || getSystemMessage('MSG59')?.content || 'Không gửi được email kích hoạt.');
    }
  };

  const handleDeleteUser = async (user: AdminUserDto) => {
    const confirmText = getSystemMessage('MSG45')?.content || 'Bạn có chắc chắn muốn thực hiện thao tác này?';
    if (!window.confirm(`${confirmText}\n(Xóa vĩnh viễn tài khoản @${user.username})`)) {
      return;
    }
    try {
      const res = await adminApi.deleteUser(user.id);
      if (res.success) {
        toast.success(`Đã xóa tài khoản @${user.username}.`);
        loadData();
      } else {
        showApiError(res.code, res.message, res.errors);
      }
    } catch (err: unknown) {
      const msg = (err as { response?: { data?: { message?: string; errors?: string[] } } })?.response?.data;
      toast.error(msg?.errors?.[0] || msg?.message || 'Không thể xóa tài khoản.');
    }
  };

  const handleResetPassword = async (e: React.FormEvent) => {
    e.preventDefault();
    if (!resettingUser) return;
    try {
      const res = await adminApi.resetUserPassword(resettingUser.id, resetPasswordVal);
      if (res.success) {
        toast.success(getSystemMessage('MSG46')?.content || 'Đổi mật khẩu thành công.');
        setResettingUser(null);
        setResetPasswordVal('');
      } else {
        showApiError(res.code, res.message, res.errors);
      }
    } catch (err: unknown) {
      const msg = (err as { response?: { data?: { message?: string; errors?: string[] } } })?.response?.data;
      toast.error(msg?.errors?.[0] || msg?.message || getSystemMessage('MSG04')?.content || 'Đặt lại mật khẩu thất bại.');
    }
  };

  const handleImportStaff = async (e: React.ChangeEvent<HTMLInputElement>) => {
    const file = e.target.files?.[0];
    if (!file) return;
    try {
      const res = await adminApi.importStaff(file);
      if (res.success) {
        toast.success(`${getSystemMessage('MSG48')?.content || 'Nhập dữ liệu hoàn tất.'} (${res.data ?? 0} bản ghi)`);
        loadData();
      } else {
        showApiError(res.code, res.message, res.errors);
      }
    } catch {
      toast.error(getSystemMessage('MSG41')?.content || 'Tệp không hợp lệ hoặc vượt quá dung lượng cho phép.');
    } finally {
      if (fileInputRef.current) fileInputRef.current.value = '';
    }
  };

  const toggleRoleInList = (list: string[], roleName: string, setter: (v: string[]) => void) => {
    if (list.includes(roleName)) {
      setter(list.filter((r) => r !== roleName));
    } else {
      setter([...list, roleName]);
    }
  };

  return (
    <div style={{ display: 'flex', flexDirection: 'column', gap: 20 }}>
      {/* Header */}
      <div className="card" style={{ display: 'flex', justifyContent: 'space-between', alignItems: 'center', flexWrap: 'wrap', gap: 12 }}>
        <div>
          <span className="badge badge--violet" style={{ marginBottom: 8 }}>
            <UserCog size={13} /> Quản trị hệ thống (UC-53 – UC-57)
          </span>
          <h1 style={{ margin: '4px 0 4px' }}>Quản lý Tài khoản & Phân quyền</h1>
          <p style={{ margin: 0, color: 'var(--color-muted-fg)', fontSize: 'var(--fs-sm)' }}>
            Cấp tài khoản mới, phân vai trò RBAC, khóa/mở khóa, đặt lại mật khẩu và nhập nhân sự Excel.
          </p>
        </div>

        <div style={{ display: 'flex', gap: 10, flexWrap: 'wrap' }}>
          <input ref={fileInputRef} type="file" accept=".xlsx" hidden onChange={handleImportStaff} />
          <button type="button" className="btn btn--secondary btn--sm" onClick={() => fileInputRef.current?.click()}>
            <Upload size={15} /> Nhập Nhân sự (.xlsx)
          </button>
          <button type="button" className="btn btn--primary btn--sm" onClick={() => setShowCreateModal(true)}>
            <Plus size={15} /> Tạo tài khoản
          </button>
        </div>
      </div>

      {/* Filters */}
      <div className="card">
        <form
          onSubmit={(e) => {
            e.preventDefault();
            setPageIndex(1);
            setSearchQuery(searchInput.trim());
          }}
          style={{ display: 'flex', gap: 12, flexWrap: 'wrap', alignItems: 'center' }}
        >
          <div style={{ position: 'relative', flex: '1 1 240px' }}>
            <Search size={16} style={{ position: 'absolute', left: 12, top: '50%', transform: 'translateY(-50%)', color: 'var(--color-muted-fg)' }} />
            <input
              type="text"
              className="input"
              style={{ paddingLeft: 36, width: '100%' }}
              placeholder="Tìm theo username, họ tên, email, SĐT..."
              value={searchInput}
              onChange={(e) => setSearchInput(e.target.value)}
            />
          </div>

          <select
            className="input"
            style={{ width: 170 }}
            value={roleFilter}
            onChange={(e) => {
              setPageIndex(1);
              setRoleFilter(e.target.value);
            }}
          >
            <option value="">Tất cả vai trò</option>
            {roles.map((r) => (
              <option key={r.id} value={r.name}>{r.name}</option>
            ))}
          </select>

          <select
            className="input"
            style={{ width: 160 }}
            value={statusFilter}
            onChange={(e) => {
              setPageIndex(1);
              setStatusFilter(e.target.value);
            }}
          >
            <option value="">Mọi trạng thái</option>
            <option value="true">Đang hoạt động</option>
            <option value="false">Chưa hoạt động (chờ kích hoạt / đã khóa)</option>
          </select>

          <button type="submit" className="btn btn--primary btn--sm">Lọc</button>
          <button type="button" className="btn btn--secondary btn--sm" onClick={loadData}>
            <RefreshCw size={14} />
          </button>
        </form>
      </div>

      {/* Table */}
      <div className="card" style={{ padding: 0, overflow: 'hidden' }}>
        {loading ? (
          <div style={{ padding: 40, textAlign: 'center', color: 'var(--color-muted-fg)' }}>Đang tải danh sách tài khoản...</div>
        ) : users.length === 0 ? (
          <div style={{ padding: 40, textAlign: 'center', fontWeight: 700 }}>
            {getSystemMessage('MSG43')?.content || 'Không có dữ liệu phù hợp để hiển thị.'}
          </div>
        ) : (
          <div style={{ overflowX: 'auto' }}>
            <table style={{ width: '100%', borderCollapse: 'collapse', fontSize: 'var(--fs-sm)' }}>
              <thead>
                <tr style={{ background: 'var(--color-muted)', borderBottom: 'var(--border)' }}>
                  <th style={{ padding: '12px 14px', textAlign: 'left', fontWeight: 800 }}>Tài khoản</th>
                  <th style={{ padding: '12px 14px', textAlign: 'left', fontWeight: 800 }}>Liên hệ</th>
                  <th style={{ padding: '12px 14px', textAlign: 'left', fontWeight: 800 }}>Vai trò</th>
                  <th style={{ padding: '12px 14px', textAlign: 'center', fontWeight: 800 }}>Trạng thái</th>
                  <th style={{ padding: '12px 14px', textAlign: 'center', fontWeight: 800 }}>Thao tác</th>
                </tr>
              </thead>
              <tbody>
                {users.map((u) => (
                  <tr key={u.id} style={{ borderBottom: '1px solid var(--color-border)' }}>
                    <td style={{ padding: '10px 14px' }}>
                      <div style={{ fontWeight: 700 }}>{u.fullName}</div>
                      <div style={{ fontSize: 12, color: 'var(--color-muted-fg)' }}>@{u.username}</div>
                    </td>
                    <td style={{ padding: '10px 14px' }}>
                      <div>{u.email}</div>
                      {u.phoneNumber && <div style={{ fontSize: 12, color: 'var(--color-muted-fg)' }}>{u.phoneNumber}</div>}
                    </td>
                    <td style={{ padding: '10px 14px' }}>
                      <div style={{ display: 'flex', gap: 6, flexWrap: 'wrap' }}>
                        {u.roles.map((r) => (
                          <span key={r} className="badge badge--violet">{r}</span>
                        ))}
                      </div>
                    </td>
                    <td style={{ padding: '10px 14px', textAlign: 'center' }}>
                      <span className={STATUS_BADGE[u.status].className}>{STATUS_BADGE[u.status].label}</span>
                    </td>
                    <td style={{ padding: '10px 14px' }}>
                      <div style={{ display: 'flex', gap: 6, justifyContent: 'center', flexWrap: 'wrap' }}>
                        <button
                          type="button"
                          className="btn btn--secondary btn--sm"
                          title="Phân quyền vai trò"
                          onClick={() => {
                            setEditingRolesUser(u);
                            setSelectedRoles(u.roles);
                          }}
                        >
                          <Shield size={14} /> Quyền
                        </button>
                        <button
                          type="button"
                          className="btn btn--secondary btn--sm"
                          title={u.status === 'Active' ? 'Đặt lại mật khẩu' : 'Chỉ đặt lại được mật khẩu của tài khoản đang hoạt động'}
                          disabled={u.status !== 'Active' || u.roles.includes('Admin')}
                          onClick={() => {
                            setResettingUser(u);
                            setResetPasswordVal('');
                          }}
                        >
                          <KeyRound size={14} /> MK
                        </button>
                        {u.status === 'PendingActivation' && (
                          <button
                            type="button"
                            className="btn btn--secondary btn--sm"
                            title="Gửi lại liên kết kích hoạt qua email"
                            onClick={() => handleResendActivation(u)}
                          >
                            <Mail size={14} /> Gửi lại link
                          </button>
                        )}
                        <button
                          type="button"
                          className="btn btn--secondary btn--sm"
                          title={
                            u.status === 'Deactivated'
                              ? 'Mở khóa tài khoản'
                              : 'Khóa tài khoản'
                          }
                          onClick={() => handleToggleStatus(u)}
                        >
                          {u.status === 'Deactivated' ? <Unlock size={14} /> : <Lock size={14} />}
                          {u.status === 'Deactivated' ? 'Mở' : 'Khóa'}
                        </button>
                        {(u.status === 'PendingActivation' || u.canDelete) && (
                          <button
                            type="button"
                            className="btn btn--secondary btn--sm"
                            style={{ color: '#DC2626', borderColor: '#FCA5A5' }}
                            title="Xóa tài khoản chưa kích hoạt"
                            onClick={() => handleDeleteUser(u)}
                          >
                            <Trash2 size={14} /> Xóa
                          </button>
                        )}
                      </div>
                    </td>
                  </tr>
                ))}
              </tbody>
            </table>
          </div>
        )}

        <div style={{ display: 'flex', justifyContent: 'space-between', alignItems: 'center', padding: '12px 16px', borderTop: 'var(--border)' }}>
          <span style={{ fontSize: 'var(--fs-sm)', color: 'var(--color-muted-fg)' }}>
            Tổng {totalCount} tài khoản • Trang {pageIndex}/{Math.max(1, totalPages)}
          </span>
          <div style={{ display: 'flex', gap: 8 }}>
            <button type="button" className="btn btn--secondary btn--sm" disabled={pageIndex <= 1} onClick={() => setPageIndex(pageIndex - 1)}>
              Trước
            </button>
            <button type="button" className="btn btn--secondary btn--sm" disabled={pageIndex >= totalPages} onClick={() => setPageIndex(pageIndex + 1)}>
              Sau
            </button>
          </div>
        </div>
      </div>

      {/* Create User Modal */}
      {showCreateModal && (
        <div style={{ position: 'fixed', inset: 0, background: 'rgba(15, 23, 42, 0.55)', display: 'grid', placeItems: 'center', zIndex: 1000, padding: 16 }}>
          <form onSubmit={handleCreateUser} noValidate className="card" style={{ width: '100%', maxWidth: 520, display: 'flex', flexDirection: 'column', gap: 12 }}>
            <div style={{ display: 'flex', justifyContent: 'space-between', alignItems: 'center' }}>
              <h3 style={{ margin: 0 }}>Tạo tài khoản mới</h3>
              <button type="button" className="btn btn--secondary btn--sm" onClick={() => setShowCreateModal(false)}><X size={15} /></button>
            </div>
            <input className="input" placeholder="Tên đăng nhập *" value={newUsername} onChange={(e) => setNewUsername(e.target.value)} />
            <input className="input" placeholder="Họ và tên *" value={newFullName} onChange={(e) => setNewFullName(e.target.value)} />
            <input className="input" type="email" placeholder="Email *" value={newEmail} onChange={(e) => setNewEmail(e.target.value)} />
            <input className="input" placeholder="Số điện thoại" value={newPhone} onChange={(e) => setNewPhone(e.target.value)} />
            <p style={{ margin: 0, fontSize: 'var(--fs-sm)', color: 'var(--color-muted-fg)' }}>
              Tài khoản được tạo ở trạng thái chờ kích hoạt. Hệ thống gửi đường liên kết kích hoạt đến email để người dùng tự đặt mật khẩu.
            </p>
            <div>
              <div style={{ fontWeight: 700, fontSize: 'var(--fs-sm)', marginBottom: 6 }}>Vai trò:</div>
              <div style={{ display: 'flex', gap: 8, flexWrap: 'wrap' }}>
                {roles.filter((r) => r.name !== 'Parent').map((r) => (
                  <label key={r.id} style={{ display: 'flex', alignItems: 'center', gap: 6, fontSize: 'var(--fs-sm)', cursor: 'pointer' }}>
                    <input
                      type="radio"
                      name="newRole"
                      value={r.name}
                      checked={newRole === r.name}
                      onChange={() => setNewRole(r.name)}
                    />
                    {r.name}
                  </label>
                ))}
              </div>
            </div>
            <div style={{ display: 'flex', justifyContent: 'flex-end', gap: 10, marginTop: 6 }}>
              <button type="button" className="btn btn--secondary btn--sm" onClick={() => setShowCreateModal(false)}>Hủy</button>
              <button type="submit" className="btn btn--primary btn--sm">Tạo tài khoản</button>
            </div>
          </form>
        </div>
      )}

      {/* Edit Roles Modal */}
      {editingRolesUser && (
        <div style={{ position: 'fixed', inset: 0, background: 'rgba(15, 23, 42, 0.55)', display: 'grid', placeItems: 'center', zIndex: 1000, padding: 16 }}>
          <div className="card" style={{ width: '100%', maxWidth: 440, display: 'flex', flexDirection: 'column', gap: 14 }}>
            <div style={{ display: 'flex', justifyContent: 'space-between', alignItems: 'center' }}>
              <h3 style={{ margin: 0 }}>Phân quyền: @{editingRolesUser.username}</h3>
              <button type="button" className="btn btn--secondary btn--sm" onClick={() => setEditingRolesUser(null)}><X size={15} /></button>
            </div>
            <div style={{ display: 'flex', flexDirection: 'column', gap: 8 }}>
              {roles.map((r) => (
                <label key={r.id} style={{ display: 'flex', alignItems: 'center', gap: 8, cursor: 'pointer' }}>
                  <input
                    type="checkbox"
                    checked={selectedRoles.includes(r.name)}
                    onChange={() => toggleRoleInList(selectedRoles, r.name, setSelectedRoles)}
                    // OVR-04 / BR-68: Parent is never combined with another role
                    disabled={
                      !selectedRoles.includes(r.name) &&
                      selectedRoles.length > 0 &&
                      (r.name === UserRole.Parent || selectedRoles.includes(UserRole.Parent))
                    }
                  />
                  <strong>{r.name}</strong>
                  {r.description && <span style={{ fontSize: 12, color: 'var(--color-muted-fg)' }}>— {r.description}</span>}
                </label>
              ))}
            </div>
            <div style={{ display: 'flex', justifyContent: 'flex-end', gap: 10 }}>
              <button type="button" className="btn btn--secondary btn--sm" onClick={() => setEditingRolesUser(null)}>Hủy</button>
              <button type="button" className="btn btn--primary btn--sm" onClick={handleSaveRoles}>Lưu vai trò</button>
            </div>
          </div>
        </div>
      )}

      {/* Reset Password Modal */}
      {resettingUser && (
        <div style={{ position: 'fixed', inset: 0, background: 'rgba(15, 23, 42, 0.55)', display: 'grid', placeItems: 'center', zIndex: 1000, padding: 16 }}>
          <form onSubmit={handleResetPassword} noValidate className="card" style={{ width: '100%', maxWidth: 420, display: 'flex', flexDirection: 'column', gap: 14 }}>
            <div style={{ display: 'flex', justifyContent: 'space-between', alignItems: 'center' }}>
              <h3 style={{ margin: 0 }}>Đặt lại mật khẩu: @{resettingUser.username}</h3>
              <button type="button" className="btn btn--secondary btn--sm" onClick={() => setResettingUser(null)}><X size={15} /></button>
            </div>
            <p style={{ margin: 0, fontSize: 'var(--fs-sm)', color: 'var(--color-muted-fg)' }}>
              Nhập mật khẩu tạm thời và chuyển cho người dùng qua kênh của nhà trường. Người dùng phải đổi mật khẩu ở lần đăng nhập tiếp theo; các phiên đăng nhập hiện tại sẽ kết thúc.
            </p>
            <input
              type="password"
              className="input"
              placeholder="Mật khẩu tạm thời (≥ 8 ký tự, hoa/thường/số/đặc biệt)"
              value={resetPasswordVal}
              onChange={(e) => setResetPasswordVal(e.target.value)}
            />
            <div style={{ display: 'flex', justifyContent: 'flex-end', gap: 10 }}>
              <button type="button" className="btn btn--secondary btn--sm" onClick={() => setResettingUser(null)}>Hủy</button>
              <button type="submit" className="btn btn--primary btn--sm">Xác nhận đặt lại</button>
            </div>
          </form>
        </div>
      )}
    </div>
  );
}
