import { useEffect, useMemo, useState } from 'react';
import { BookOpen, Edit3, Plus, Search, Trash2 } from 'lucide-react';
import toast from 'react-hot-toast';
import { GeometricModal } from '../common/GeometricModal';
import { lunchApi } from '../../services/implementations/lunchApi';
import {
  ALLERGEN_OPTIONS,
  DISH_CATEGORY_OPTIONS,
  getAllergenShortLabel,
  getDishCategoryInfo,
  normalizeDishCategory,
  normalizeMealTypeNumber,
} from '../../constants/allergens';
import type { Dish } from '../../types/lunch.types';

interface DishLibraryModalProps {
  open: boolean;
  onClose: () => void;
  onChanged?: () => void;
}

const MEAL_TYPE_OPTIONS = [
  { value: 1, label: 'Tiêu chuẩn' },
  { value: 2, label: 'Ăn chay' },
  { value: 3, label: 'Đặc biệt' },
];

export function DishLibraryModal({ open, onClose, onChanged }: DishLibraryModalProps) {
  const [dishes, setDishes] = useState<Dish[]>([]);
  const [loading, setLoading] = useState(false);
  const [saving, setSaving] = useState(false);

  const [search, setSearch] = useState('');
  const [filterCategory, setFilterCategory] = useState<number | 'ALL'>('ALL');
  const [showInactive, setShowInactive] = useState(true);

  const [editingId, setEditingId] = useState<number | null>(null);
  const [name, setName] = useState('');
  const [category, setCategory] = useState<number>(2);
  const [suitableMealTypes, setSuitableMealTypes] = useState<number[]>([1]);
  const [allergenTags, setAllergenTags] = useState<string[]>([]);
  const [isActive, setIsActive] = useState(true);

  const loadDishes = async () => {
    try {
      setLoading(true);
      const list = await lunchApi.getDishes({ includeInactive: true });
      setDishes(list);
    } catch {
      toast.error('Không thể tải thư viện món ăn.');
    } finally {
      setLoading(false);
    }
  };

  useEffect(() => {
    if (open) {
      loadDishes();
      resetForm();
    }
  }, [open]);

  const resetForm = () => {
    setEditingId(null);
    setName('');
    setCategory(2);
    setSuitableMealTypes([1]);
    setAllergenTags([]);
    setIsActive(true);
  };

  const startEdit = (d: Dish) => {
    setEditingId(d.id);
    setName(d.name);
    setCategory(normalizeDishCategory(d.category));
    const mts = (d.suitableMealTypes ?? []).map((m) => normalizeMealTypeNumber(m));
    setSuitableMealTypes(mts.length > 0 ? Array.from(new Set(mts)) : [1]);
    setAllergenTags(d.allergenTags ?? []);
    setIsActive(d.isActive);
  };

  const toggleMealType = (mt: number) => {
    setSuitableMealTypes((prev) => {
      if (prev.includes(mt)) {
        return prev.length > 1 ? prev.filter((x) => x !== mt) : prev;
      }
      return [...prev, mt];
    });
  };

  const toggleAllergen = (code: string) => {
    setAllergenTags((prev) => (prev.includes(code) ? prev.filter((x) => x !== code) : [...prev, code]));
  };

  const handleSubmit = async (e: React.FormEvent) => {
    e.preventDefault();
    const trimmed = name.trim();
    if (!trimmed) {
      toast.error('Vui lòng nhập tên món ăn.');
      return;
    }
    try {
      setSaving(true);
      const payload = {
        name: trimmed,
        category,
        suitableMealTypes,
        allergenTags,
        isActive,
      };
      if (editingId) {
        const res = await lunchApi.updateDish(editingId, payload);
        toast.success(res.message || 'Đã cập nhật món ăn.');
      } else {
        const res = await lunchApi.createDish(payload);
        toast.success(res.message || 'Đã thêm món ăn mới.');
      }
      resetForm();
      await loadDishes();
      onChanged?.();
    } catch (err: unknown) {
      const apiErr = (err as { response?: { data?: { message?: string; errors?: string[] } } })?.response?.data;
      toast.error(apiErr?.errors?.[0] || apiErr?.message || 'Lưu món ăn thất bại.');
    } finally {
      setSaving(false);
    }
  };

  const handleDelete = async (dish: Dish) => {
    try {
      setSaving(true);
      const res = await lunchApi.deleteDish(dish.id);
      toast.success(res.message || 'Đã xử lý món ăn.');
      if (editingId === dish.id) resetForm();
      await loadDishes();
      onChanged?.();
    } catch (err: unknown) {
      const apiErr = (err as { response?: { data?: { message?: string; errors?: string[] } } })?.response?.data;
      toast.error(apiErr?.errors?.[0] || apiErr?.message || 'Không thể xóa món ăn.');
    } finally {
      setSaving(false);
    }
  };

  const filteredDishes = useMemo(() => {
    const q = search.trim().toLowerCase();
    return dishes.filter((d) => {
      if (!showInactive && !d.isActive) return false;
      if (filterCategory !== 'ALL' && normalizeDishCategory(d.category) !== filterCategory) return false;
      if (q && !d.name.toLowerCase().includes(q)) return false;
      return true;
    });
  }, [dishes, search, filterCategory, showInactive]);

  return (
    <GeometricModal
      open={open}
      onClose={onClose}
      title="Thư Viện Món Ăn Bán Trú"
      subtitle="Quản lý danh mục món ăn, chế độ phù hợp và nhãn dị ứng để ghép thực đơn linh hoạt"
      badgeText={`${dishes.filter((d) => d.isActive).length} món đang dùng`}
      badgeColor="amber"
      icon={<BookOpen size={22} color="var(--color-fg)" />}
      iconBg="#FEF3C7"
      maxWidth="xl"
    >
      <div style={{ display: 'grid', gridTemplateColumns: 'minmax(290px, 340px) 1fr', gap: 18 }}>
        {/* Form Thêm / Sửa món */}
        <form
          onSubmit={handleSubmit}
          style={{
            background: '#FFFDF5',
            border: '2px solid var(--color-fg)',
            borderRadius: 'var(--radius-md)',
            padding: 14,
            display: 'flex',
            flexDirection: 'column',
            gap: 12,
            alignSelf: 'start',
          }}
        >
          <div style={{ display: 'flex', justifyContent: 'space-between', alignItems: 'center' }}>
            <strong style={{ fontSize: 'var(--fs-sm)' }}>
              {editingId ? '✏️ Chỉnh sửa món ăn' : '➕ Thêm món mới'}
            </strong>
            {editingId && (
              <button
                type="button"
                onClick={resetForm}
                style={{ background: 'none', border: 'none', color: '#E11D48', fontWeight: 700, fontSize: 'var(--fs-xs)', cursor: 'pointer' }}
              >
                Hủy sửa
              </button>
            )}
          </div>

          <div>
            <label className="label" style={{ fontSize: 'var(--fs-xs)' }}>TÊN MÓN ĂN *</label>
            <input
              type="text"
              className="input"
              maxLength={120}
              value={name}
              onChange={(e) => setName(e.target.value)}
              placeholder="VD: Cá hồi áp chảo sốt cam"
            />
          </div>

          <div>
            <label className="label" style={{ fontSize: 'var(--fs-xs)' }}>NHÓM MÓN</label>
            <select className="input" value={category} onChange={(e) => setCategory(Number(e.target.value))}>
              {DISH_CATEGORY_OPTIONS.map((opt) => (
                <option key={opt.value} value={opt.value}>
                  {opt.emoji} {opt.label}
                </option>
              ))}
            </select>
          </div>

          <div>
            <label className="label" style={{ fontSize: 'var(--fs-xs)' }}>PHÙ HỢP CHẾ ĐỘ ĂN</label>
            <div style={{ display: 'flex', gap: 6, flexWrap: 'wrap' }}>
              {MEAL_TYPE_OPTIONS.map((mt) => {
                const active = suitableMealTypes.includes(mt.value);
                return (
                  <button
                    key={mt.value}
                    type="button"
                    onClick={() => toggleMealType(mt.value)}
                    style={{
                      padding: '5px 10px',
                      borderRadius: 'var(--radius-full)',
                      border: active ? '2px solid var(--color-fg)' : '1.5px solid var(--color-border)',
                      background: active ? '#DCFCE7' : '#FFFFFF',
                      fontWeight: 700,
                      fontSize: 'var(--fs-xs)',
                      cursor: 'pointer',
                    }}
                  >
                    {mt.label}
                  </button>
                );
              })}
            </div>
          </div>

          <div>
            <label className="label" style={{ fontSize: 'var(--fs-xs)' }}>NHÃN CẢNH BÁO DỊ ỨNG</label>
            <div style={{ display: 'flex', gap: 6, flexWrap: 'wrap' }}>
              {ALLERGEN_OPTIONS.map((al) => {
                const checked = allergenTags.includes(al.code);
                return (
                  <button
                    key={al.code}
                    type="button"
                    onClick={() => toggleAllergen(al.code)}
                    style={{
                      padding: '4px 9px',
                      borderRadius: 'var(--radius-sm)',
                      border: checked ? '2px solid #DC2626' : '1.5px dashed var(--color-border)',
                      background: checked ? '#FEE2E2' : '#FFFFFF',
                      color: checked ? '#991B1B' : 'var(--color-fg)',
                      fontWeight: 700,
                      fontSize: 'var(--fs-xs)',
                      cursor: 'pointer',
                    }}
                  >
                    {checked ? '⚠️ ' : '+ '}
                    {al.shortLabel}
                  </button>
                );
              })}
            </div>
          </div>

          <label style={{ display: 'flex', alignItems: 'center', gap: 8, fontSize: 'var(--fs-xs)', fontWeight: 700, cursor: 'pointer' }}>
            <input type="checkbox" checked={isActive} onChange={(e) => setIsActive(e.target.checked)} />
            Đang sử dụng (hiển thị khi lên thực đơn)
          </label>

          <button type="submit" className="btn btn--primary" disabled={saving} style={{ justifyContent: 'center' }}>
            <Plus size={15} />
            {saving ? 'Đang lưu...' : editingId ? 'Lưu cập nhật' : 'Thêm vào thư viện'}
          </button>
        </form>

        {/* Danh sách món */}
        <div style={{ display: 'flex', flexDirection: 'column', gap: 10, minWidth: 0 }}>
          <div style={{ display: 'flex', gap: 8, flexWrap: 'wrap', alignItems: 'center' }}>
            <div style={{ position: 'relative', flex: '1 1 180px' }}>
              <Search size={14} style={{ position: 'absolute', left: 10, top: '50%', transform: 'translateY(-50%)', color: 'var(--color-muted-fg)' }} />
              <input
                type="text"
                className="input"
                placeholder="Tìm tên món..."
                value={search}
                onChange={(e) => setSearch(e.target.value)}
                style={{ paddingLeft: 32 }}
              />
            </div>
            <select
              className="input"
              value={filterCategory}
              onChange={(e) => setFilterCategory(e.target.value === 'ALL' ? 'ALL' : Number(e.target.value))}
              style={{ width: 'auto' }}
            >
              <option value="ALL">Tất cả nhóm món</option>
              {DISH_CATEGORY_OPTIONS.map((opt) => (
                <option key={opt.value} value={opt.value}>
                  {opt.emoji} {opt.label}
                </option>
              ))}
            </select>
            <label style={{ display: 'inline-flex', alignItems: 'center', gap: 6, fontSize: 'var(--fs-xs)', fontWeight: 700, cursor: 'pointer' }}>
              <input type="checkbox" checked={showInactive} onChange={(e) => setShowInactive(e.target.checked)} />
              Hiện món ngưng dùng
            </label>
          </div>

          <div
            style={{
              border: '2px solid var(--color-fg)',
              borderRadius: 'var(--radius-md)',
              maxHeight: 420,
              overflowY: 'auto',
              background: '#FFFFFF',
            }}
          >
            {loading ? (
              <div style={{ padding: 24, textAlign: 'center', color: 'var(--color-muted-fg)' }}>Đang tải...</div>
            ) : filteredDishes.length === 0 ? (
              <div style={{ padding: 24, textAlign: 'center', color: 'var(--color-muted-fg)' }}>Không có món ăn phù hợp.</div>
            ) : (
              filteredDishes.map((d) => {
                const cat = getDishCategoryInfo(d.category);
                return (
                  <div
                    key={d.id}
                    style={{
                      display: 'flex',
                      alignItems: 'center',
                      justifyContent: 'space-between',
                      gap: 10,
                      padding: '10px 12px',
                      borderBottom: '1px solid var(--color-border)',
                      opacity: d.isActive ? 1 : 0.6,
                    }}
                  >
                    <div style={{ minWidth: 0 }}>
                      <div style={{ display: 'flex', alignItems: 'center', gap: 6, flexWrap: 'wrap' }}>
                        <span>{cat.emoji}</span>
                        <strong style={{ fontSize: 'var(--fs-sm)' }}>{d.name}</strong>
                        {!d.isActive && (
                          <span className="badge badge--pink" style={{ fontSize: 10 }}>Ngưng dùng</span>
                        )}
                      </div>
                      <div style={{ display: 'flex', gap: 6, flexWrap: 'wrap', marginTop: 4 }}>
                        <span style={{ fontSize: 11, color: 'var(--color-muted-fg)', fontWeight: 600 }}>
                          {cat.label}
                        </span>
                        {(d.allergenTags ?? []).map((tag) => (
                          <span
                            key={tag}
                            style={{
                              fontSize: 10,
                              fontWeight: 800,
                              padding: '1px 6px',
                              borderRadius: 6,
                              background: '#FEE2E2',
                              color: '#991B1B',
                              border: '1px solid #FCA5A5',
                            }}
                          >
                            ⚠️ {getAllergenShortLabel(tag)}
                          </span>
                        ))}
                      </div>
                    </div>

                    <div style={{ display: 'flex', gap: 6, flexShrink: 0 }}>
                      <button
                        type="button"
                        className="btn btn--secondary btn--sm"
                        onClick={() => startEdit(d)}
                        title="Sửa món"
                      >
                        <Edit3 size={13} />
                      </button>
                      <button
                        type="button"
                        className="btn btn--secondary btn--sm"
                        onClick={() => handleDelete(d)}
                        title="Xóa / Ngưng dùng"
                        style={{ color: '#DC2626' }}
                      >
                        <Trash2 size={13} />
                      </button>
                    </div>
                  </div>
                );
              })
            )}
          </div>
        </div>
      </div>
    </GeometricModal>
  );
}
