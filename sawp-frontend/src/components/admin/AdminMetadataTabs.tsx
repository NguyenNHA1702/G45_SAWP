import { useCallback, useEffect, useState } from 'react';
import { Bell, BookOpen, Plus, Save, ShieldCheck } from 'lucide-react';
import toast from 'react-hot-toast';
import { adminApi } from '../../services/implementations/adminApi';
import { asApiError } from '../../utils/apiError';
import type { NotificationTemplateDto, ReferenceItemDto, RolePermissionMatrixDto } from '../../types/admin.types';

export function PermissionsMatrixTab() {
  const [matrix, setMatrix] = useState<RolePermissionMatrixDto | null>(null);
  const [selectedRoleId, setSelectedRoleId] = useState<number>(0);
  const [grantedCodes, setGrantedCodes] = useState<string[]>([]);
  const [loading, setLoading] = useState(true);
  const [saving, setSaving] = useState(false);

  const loadMatrix = useCallback(async () => {
    setLoading(true);
    try {
      const res = await adminApi.getPermissionsMatrix();
      if (res.success && res.data) {
        setMatrix(res.data);
        const firstRole = res.data.roles[0];
        if (firstRole) {
          setSelectedRoleId((prev) => (prev && res.data!.roles.some((r) => r.roleId === prev) ? prev : firstRole.roleId));
        }
      }
    } catch {
      toast.error('Không thể tải ma trận phân quyền.');
    } finally {
      setLoading(false);
    }
  }, []);

  useEffect(() => {
    loadMatrix();
  }, [loadMatrix]);

  useEffect(() => {
    if (!matrix || !selectedRoleId) return;
    const role = matrix.roles.find((r) => r.roleId === selectedRoleId);
    setGrantedCodes(role ? [...role.grantedPermissionCodes] : []);
  }, [matrix, selectedRoleId]);

  const toggleCode = (code: string) => {
    setGrantedCodes((prev) => (prev.includes(code) ? prev.filter((c) => c !== code) : [...prev, code]));
  };

  const handleSave = async () => {
    if (!selectedRoleId) return;
    try {
      setSaving(true);
      const res = await adminApi.updateRolePermissions(selectedRoleId, grantedCodes);
      if (res.success) {
        toast.success(res.message || 'Đã cập nhật quyền cho vai trò.');
        await loadMatrix();
      } else {
        toast.error(res.errors?.[0] || res.message || 'Cập nhật phân quyền thất bại.');
      }
    } catch (err) {
      const apiErr = asApiError(err);
      toast.error(apiErr.errors?.[0] || apiErr.message || 'Cập nhật phân quyền thất bại.');
    } finally {
      setSaving(false);
    }
  };

  if (loading || !matrix) {
    return <div className="card" style={{ padding: 24, textAlign: 'center' }}>Đang tải ma trận phân quyền...</div>;
  }

  return (
    <div className="card" style={{ display: 'flex', flexDirection: 'column', gap: 16 }}>
      <div style={{ display: 'flex', justifyContent: 'space-between', alignItems: 'center', flexWrap: 'wrap', gap: 12 }}>
        <div>
          <h3 style={{ margin: 0, display: 'flex', alignItems: 'center', gap: 8 }}>
            <ShieldCheck size={18} color="var(--color-accent)" /> Cấu hình Phân quyền theo Vai trò (UC-54)
          </h3>
          <p style={{ margin: '4px 0 0', fontSize: 'var(--fs-xs)', color: 'var(--color-muted-fg)' }}>
            Chọn vai trò và bật/tắt các quyền thao tác chi tiết trong hệ thống.
          </p>
        </div>
        <div style={{ display: 'flex', gap: 8, alignItems: 'center' }}>
          <select className="input" value={selectedRoleId} onChange={(e) => setSelectedRoleId(Number(e.target.value))}>
            {matrix.roles.map((r) => (
              <option key={r.roleId} value={r.roleId}>
                {r.roleName} ({r.grantedPermissionCodes.length} quyền)
              </option>
            ))}
          </select>
          <button type="button" className="btn btn--primary btn--sm" disabled={saving} onClick={handleSave}>
            <Save size={15} /> {saving ? 'Đang lưu...' : 'Lưu phân quyền'}
          </button>
        </div>
      </div>

      <div style={{ display: 'grid', gridTemplateColumns: 'repeat(auto-fill, minmax(260px, 1fr))', gap: 10 }}>
        {matrix.permissions.map((perm) => {
          const checked = grantedCodes.includes(perm.code);
          return (
            <label
              key={perm.id}
              style={{
                display: 'flex',
                alignItems: 'flex-start',
                gap: 10,
                padding: '10px 12px',
                borderRadius: 10,
                border: checked ? '2px solid var(--color-accent)' : '1.5px solid var(--color-border)',
                background: checked ? '#F5F3FF' : '#FFFFFF',
                cursor: 'pointer',
              }}
            >
              <input type="checkbox" checked={checked} onChange={() => toggleCode(perm.code)} style={{ marginTop: 3 }} />
              <div>
                <div style={{ fontWeight: 700, fontSize: 'var(--fs-sm)' }}>{perm.name}</div>
                <div style={{ fontSize: 11, color: 'var(--color-muted-fg)' }}>
                  <code>{perm.module}</code> • <code>{perm.code}</code>
                </div>
                {perm.description && <div style={{ fontSize: 12, color: '#475569', marginTop: 2 }}>{perm.description}</div>}
              </div>
            </label>
          );
        })}
      </div>
    </div>
  );
}

const REFERENCE_CATEGORIES = [
  { value: '', label: 'Tất cả danh mục' },
  { value: 'DocumentType', label: 'Loại giấy tờ hành chính' },
  { value: 'Relationship', label: 'Quan hệ gia đình' },
  { value: 'Allergen', label: 'Chất dị ứng thực phẩm' },
];

export function ReferenceItemsTab() {
  const [items, setItems] = useState<ReferenceItemDto[]>([]);
  const [categoryFilter, setCategoryFilter] = useState('');
  const [loading, setLoading] = useState(true);
  const [editingId, setEditingId] = useState<number | null>(null);
  const [formCategory, setFormCategory] = useState('DocumentType');
  const [formCode, setFormCode] = useState('');
  const [formLabel, setFormLabel] = useState('');
  const [formSort, setFormSort] = useState(1);
  const [formActive, setFormActive] = useState(true);
  const [saving, setSaving] = useState(false);

  const loadItems = useCallback(async () => {
    setLoading(true);
    try {
      const res = await adminApi.getReferenceItems(categoryFilter || undefined, false);
      if (res.success && res.data) setItems(res.data);
    } catch {
      toast.error('Không thể tải danh mục tham chiếu.');
    } finally {
      setLoading(false);
    }
  }, [categoryFilter]);

  useEffect(() => {
    loadItems();
  }, [loadItems]);

  const resetForm = () => {
    setEditingId(null);
    setFormCategory(categoryFilter || 'DocumentType');
    setFormCode('');
    setFormLabel('');
    setFormSort(items.length + 1);
    setFormActive(true);
  };

  const handleSave = async (e: React.FormEvent) => {
    e.preventDefault();
    try {
      setSaving(true);
      const payload = {
        category: formCategory,
        code: formCode.trim(),
        label: formLabel.trim(),
        sortOrder: Number(formSort) || 0,
        isActive: formActive,
      };
      const res = editingId
        ? await adminApi.updateReferenceItem(editingId, payload)
        : await adminApi.createReferenceItem(payload);
      if (res.success) {
        toast.success(res.message || 'Đã lưu danh mục.');
        resetForm();
        await loadItems();
      } else {
        toast.error(res.errors?.[0] || res.message || 'Lưu danh mục thất bại.');
      }
    } catch (err) {
      const apiErr = asApiError(err);
      toast.error(apiErr.errors?.[0] || apiErr.message || 'Lưu danh mục thất bại.');
    } finally {
      setSaving(false);
    }
  };

  return (
    <div className="responsive-split">
      <form onSubmit={handleSave} className="card" style={{ display: 'flex', flexDirection: 'column', gap: 12 }}>
        <h3 style={{ margin: 0, display: 'flex', alignItems: 'center', gap: 8 }}>
          <BookOpen size={18} color="var(--color-accent)" /> {editingId ? 'Sửa mục danh mục' : 'Thêm mục danh mục (UC-56)'}
        </h3>
        <label style={{ fontSize: 'var(--fs-sm)', fontWeight: 700 }}>
          Nhóm danh mục
          <select className="input" style={{ width: '100%', marginTop: 4 }} value={formCategory} onChange={(e) => setFormCategory(e.target.value)}>
            <option value="DocumentType">Loại giấy tờ (DocumentType)</option>
            <option value="Relationship">Quan hệ gia đình (Relationship)</option>
            <option value="Allergen">Chất dị ứng (Allergen)</option>
          </select>
        </label>
        <label style={{ fontSize: 'var(--fs-sm)', fontWeight: 700 }}>
          Mã tham chiếu *
          <input className="input" style={{ width: '100%', marginTop: 4 }} placeholder="Ví dụ: DOC_GIAY_KHAI_SINH" value={formCode} onChange={(e) => setFormCode(e.target.value)} required />
        </label>
        <label style={{ fontSize: 'var(--fs-sm)', fontWeight: 700 }}>
          Nhãn hiển thị *
          <input className="input" style={{ width: '100%', marginTop: 4 }} placeholder="Ví dụ: Giấy xác nhận chuyển lớp" value={formLabel} onChange={(e) => setFormLabel(e.target.value)} required />
        </label>
        <label style={{ fontSize: 'var(--fs-sm)', fontWeight: 700 }}>
          Thứ tự sắp xếp
          <input type="number" className="input" style={{ width: '100%', marginTop: 4 }} value={formSort} onChange={(e) => setFormSort(Number(e.target.value))} />
        </label>
        <label style={{ display: 'flex', alignItems: 'center', gap: 8, fontSize: 'var(--fs-sm)', fontWeight: 700 }}>
          <input type="checkbox" checked={formActive} onChange={(e) => setFormActive(e.target.checked)} />
          Đang sử dụng (Hiển thị trên dropdown)
        </label>
        <div style={{ display: 'flex', gap: 8 }}>
          <button type="submit" className="btn btn--primary btn--sm" disabled={saving}>
            {editingId ? <Save size={15} /> : <Plus size={15} />} {saving ? 'Đang lưu...' : editingId ? 'Cập nhật' : 'Thêm mục'}
          </button>
          {editingId && (
            <button type="button" className="btn btn--secondary btn--sm" onClick={resetForm}>
              Hủy sửa
            </button>
          )}
        </div>
      </form>

      <div className="card" style={{ padding: 0, overflowX: 'auto' }}>
        <div style={{ padding: '12px 14px', borderBottom: 'var(--border)', display: 'flex', justifyContent: 'space-between', alignItems: 'center', gap: 8 }}>
          <strong>Danh sách mục tham chiếu</strong>
          <select className="input" style={{ width: 220 }} value={categoryFilter} onChange={(e) => setCategoryFilter(e.target.value)}>
            {REFERENCE_CATEGORIES.map((c) => (
              <option key={c.value} value={c.value}>{c.label}</option>
            ))}
          </select>
        </div>
        {loading ? (
          <div style={{ padding: 24, textAlign: 'center' }}>Đang tải danh mục...</div>
        ) : (
          <table style={{ width: '100%', borderCollapse: 'collapse', fontSize: 'var(--fs-sm)' }}>
            <thead>
              <tr style={{ background: 'var(--color-muted)', borderBottom: 'var(--border)' }}>
                <th style={{ padding: '10px 12px', textAlign: 'left' }}>Nhóm</th>
                <th style={{ padding: '10px 12px', textAlign: 'left' }}>Mã</th>
                <th style={{ padding: '10px 12px', textAlign: 'left' }}>Nhãn</th>
                <th style={{ padding: '10px 12px', textAlign: 'center' }}>Trạng thái</th>
              </tr>
            </thead>
            <tbody>
              {items.map((item) => (
                <tr
                  key={item.id}
                  style={{ borderBottom: '1px solid var(--color-border)', cursor: 'pointer' }}
                  onClick={() => {
                    setEditingId(item.id);
                    setFormCategory(item.category);
                    setFormCode(item.code);
                    setFormLabel(item.label);
                    setFormSort(item.sortOrder);
                    setFormActive(item.isActive);
                  }}
                >
                  <td style={{ padding: '10px 12px' }}><code>{item.category}</code></td>
                  <td style={{ padding: '10px 12px' }}><code>{item.code}</code></td>
                  <td style={{ padding: '10px 12px', fontWeight: 700 }}>{item.label}</td>
                  <td style={{ padding: '10px 12px', textAlign: 'center' }}>
                    <span className={item.isActive ? 'badge badge--mint' : 'badge badge--pink'}>
                      {item.isActive ? 'Hiển thị' : 'Đang ẩn'}
                    </span>
                  </td>
                </tr>
              ))}
            </tbody>
          </table>
        )}
      </div>
    </div>
  );
}

export function NotificationTemplatesTab() {
  const [templates, setTemplates] = useState<NotificationTemplateDto[]>([]);
  const [editing, setEditing] = useState<NotificationTemplateDto | null>(null);
  const [title, setTitle] = useState('');
  const [body, setBody] = useState('');
  const [isActive, setIsActive] = useState(true);
  const [loading, setLoading] = useState(true);
  const [saving, setSaving] = useState(false);

  const loadTemplates = useCallback(async () => {
    setLoading(true);
    try {
      const res = await adminApi.getNotificationTemplates();
      if (res.success && res.data) {
        setTemplates(res.data);
        if (res.data.length > 0 && !editing) {
          setEditing(res.data[0]);
          setTitle(res.data[0].titleTemplate);
          setBody(res.data[0].bodyTemplate);
          setIsActive(res.data[0].isActive);
        }
      }
    } catch {
      toast.error('Không thể tải mẫu thông báo.');
    } finally {
      setLoading(false);
    }
  }, [editing]);

  useEffect(() => {
    loadTemplates();
    // eslint-disable-next-line react-hooks/exhaustive-deps
  }, []);

  const handleSave = async (e: React.FormEvent) => {
    e.preventDefault();
    if (!editing) return;
    try {
      setSaving(true);
      const res = await adminApi.updateNotificationTemplate(editing.id, {
        titleTemplate: title.trim(),
        bodyTemplate: body.trim(),
        isActive,
      });
      if (res.success && res.data) {
        toast.success(res.message || 'Đã cập nhật mẫu thông báo.');
        setEditing(res.data);
        await loadTemplates();
      } else {
        toast.error(res.errors?.[0] || res.message || 'Lưu mẫu thông báo thất bại.');
      }
    } catch (err) {
      const apiErr = asApiError(err);
      toast.error(apiErr.errors?.[0] || apiErr.message || 'Lưu mẫu thông báo thất bại.');
    } finally {
      setSaving(false);
    }
  };

  if (loading) {
    return <div className="card" style={{ padding: 24, textAlign: 'center' }}>Đang tải mẫu thông báo...</div>;
  }

  return (
    <div className="responsive-split">
      <div className="card" style={{ display: 'flex', flexDirection: 'column', gap: 10 }}>
        <h3 style={{ margin: 0, display: 'flex', alignItems: 'center', gap: 8 }}>
          <Bell size={18} color="var(--color-accent)" /> Danh sách Mẫu thông báo (UC-57)
        </h3>
        {templates.map((tpl) => {
          const isSelected = editing?.id === tpl.id;
          return (
            <button
              key={tpl.id}
              type="button"
              className={isSelected ? 'btn btn--primary btn--sm' : 'btn btn--secondary btn--sm'}
              style={{
                width: '100%',
                justifyContent: 'space-between',
                alignItems: 'center',
                gap: 10,
                textAlign: 'left',
                whiteSpace: 'normal',
                height: 'auto',
                minHeight: 44,
                padding: '10px 12px',
                lineHeight: 1.35,
              }}
              onClick={() => {
                setEditing(tpl);
                setTitle(tpl.titleTemplate);
                setBody(tpl.bodyTemplate);
                setIsActive(tpl.isActive);
              }}
            >
              <span style={{ minWidth: 0, flex: 1, overflowWrap: 'anywhere' }}>
                <code style={{ fontWeight: 800 }}>{tpl.code}</code> — {tpl.titleTemplate}
              </span>
              <span
                className={tpl.isActive ? 'badge badge--mint' : 'badge badge--pink'}
                style={{ flexShrink: 0, whiteSpace: 'nowrap' }}
              >
                {tpl.isActive ? 'Bật' : 'Tắt'}
              </span>
            </button>
          );
        })}
      </div>

      {editing && (
        <form onSubmit={handleSave} className="card" style={{ display: 'flex', flexDirection: 'column', gap: 12 }}>
          <h3 style={{ margin: 0 }}>Chỉnh sửa mẫu: <code>{editing.code}</code></h3>
          <label style={{ fontSize: 'var(--fs-sm)', fontWeight: 700 }}>
            Tiêu đề mẫu *
            <input className="input" style={{ width: '100%', marginTop: 4 }} value={title} onChange={(e) => setTitle(e.target.value)} required />
          </label>
          <label style={{ fontSize: 'var(--fs-sm)', fontWeight: 700 }}>
            Nội dung mẫu (hỗ trợ biến {'{StudentName}'}, {'{RequestNumber}'}, ...) *
            <textarea className="input" rows={4} style={{ width: '100%', marginTop: 4 }} value={body} onChange={(e) => setBody(e.target.value)} required />
          </label>
          <label style={{ display: 'flex', alignItems: 'center', gap: 8, fontSize: 'var(--fs-sm)', fontWeight: 700 }}>
            <input type="checkbox" checked={isActive} onChange={(e) => setIsActive(e.target.checked)} />
            Kích hoạt mẫu thông báo
          </label>
          <button type="submit" className="btn btn--primary btn--sm" disabled={saving}>
            <Save size={15} /> {saving ? 'Đang lưu...' : 'Lưu mẫu thông báo'}
          </button>
        </form>
      )}
    </div>
  );
}
