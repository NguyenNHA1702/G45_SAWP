import { useEffect, useMemo, useState } from 'react';
import { AlertTriangle, BookOpen, Check, Edit3, Plus, PlusCircle, Search, Trash2, UtensilsCrossed } from 'lucide-react';
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
      <div className="responsive-split-sidebar">
        {/* Form Thêm / Sửa món */}
        <form
          onSubmit={handleSubmit}
          style={{
            background: '#FFFDF5',
            border: '2px solid var(--color-fg)',
            borderRadius: 'var(--radius-md)',
            boxShadow: '4px 4px 0 0 var(--color-fg)',
            padding: 16,
            display: 'flex',
            flexDirection: 'column',
            gap: 14,
            alignSelf: 'start',
          }}
        >
          <div style={{ display: 'flex', justifyContent: 'space-between', alignItems: 'center', paddingBottom: 8, borderBottom: '1.5px dashed var(--color-border)' }}>
            <strong style={{ fontSize: 'var(--fs-sm)', display: 'inline-flex', alignItems: 'center', gap: 6, fontFamily: 'var(--font-heading)' }}>
              {editingId ? <Edit3 size={15} color="var(--color-accent)" /> : <PlusCircle size={15} color="var(--color-accent)" />}
              {editingId ? 'Chỉnh sửa món ăn' : 'Thêm món mới'}
            </strong>
            {editingId && (
              <button
                type="button"
                onClick={resetForm}
                style={{
                  background: '#FFF1F2',
                  border: '1.5px solid var(--color-fg)',
                  borderRadius: 'var(--radius-full)',
                  padding: '2px 10px',
                  color: '#E11D48',
                  fontWeight: 800,
                  fontSize: 'var(--fs-xs)',
                  cursor: 'pointer',
                }}
              >
                Hủy sửa
              </button>
            )}
          </div>

          <div>
            <label className="label" style={{ fontSize: 'var(--fs-xs)', marginBottom: 4 }}>TÊN MÓN ĂN *</label>
            <input
              type="text"
              className="input"
              maxLength={120}
              value={name}
              onChange={(e) => setName(e.target.value)}
              placeholder="VD: Cá hồi áp chảo sốt cam"
              style={{ border: '2px solid var(--color-fg)', fontWeight: 600 }}
            />
          </div>

          <div>
            <label className="label" style={{ fontSize: 'var(--fs-xs)', marginBottom: 4 }}>NHÓM MÓN</label>
            <select
              className="select"
              value={category}
              onChange={(e) => setCategory(Number(e.target.value))}
              style={{ border: '2px solid var(--color-fg)', fontWeight: 700 }}
            >
              {DISH_CATEGORY_OPTIONS.map((opt) => (
                <option key={opt.value} value={opt.value}>
                  {opt.label}
                </option>
              ))}
            </select>
          </div>

          <div>
            <label className="label" style={{ fontSize: 'var(--fs-xs)', marginBottom: 6 }}>PHÙ HỢP CHẾ ĐỘ ĂN</label>
            <div style={{ display: 'flex', gap: 8, flexWrap: 'wrap' }}>
              {MEAL_TYPE_OPTIONS.map((mt) => {
                const active = suitableMealTypes.includes(mt.value);
                return (
                  <button
                    key={mt.value}
                    type="button"
                    onClick={() => toggleMealType(mt.value)}
                    style={{
                      display: 'inline-flex',
                      alignItems: 'center',
                      gap: 5,
                      padding: '5px 12px',
                      borderRadius: 'var(--radius-full)',
                      border: '2px solid var(--color-fg)',
                      background: active ? '#DCFCE7' : '#FFFFFF',
                      boxShadow: active ? '2px 2px 0 0 var(--color-fg)' : 'none',
                      fontWeight: 800,
                      fontSize: 'var(--fs-xs)',
                      color: active ? '#065F46' : 'var(--color-fg)',
                      cursor: 'pointer',
                      transition: 'transform 0.12s ease, box-shadow 0.12s ease',
                    }}
                  >
                    {active && <Check size={12} strokeWidth={3} />}
                    {mt.label}
                  </button>
                );
              })}
            </div>
          </div>

          <div>
            <label className="label" style={{ fontSize: 'var(--fs-xs)', marginBottom: 6 }}>NHÃN CẢNH BÁO DỊ ỨNG</label>
            <div style={{ display: 'flex', gap: 6, flexWrap: 'wrap' }}>
              {ALLERGEN_OPTIONS.map((al) => {
                const checked = allergenTags.includes(al.code);
                return (
                  <button
                    key={al.code}
                    type="button"
                    onClick={() => toggleAllergen(al.code)}
                    style={{
                      display: 'inline-flex',
                      alignItems: 'center',
                      gap: 4,
                      padding: '4px 10px',
                      borderRadius: 'var(--radius-full)',
                      border: checked ? '2px solid var(--color-fg)' : '1.5px dashed var(--color-muted-fg)',
                      background: checked ? '#FEE2E2' : '#FFFFFF',
                      boxShadow: checked ? '2px 2px 0 0 var(--color-fg)' : 'none',
                      color: checked ? '#991B1B' : 'var(--color-fg)',
                      fontWeight: 800,
                      fontSize: 'var(--fs-xs)',
                      cursor: 'pointer',
                    }}
                  >
                    {checked ? <AlertTriangle size={11} /> : <Plus size={11} />}
                    {al.shortLabel}
                  </button>
                );
              })}
            </div>
          </div>

          <button
            type="button"
            onClick={() => setIsActive(!isActive)}
            style={{
              display: 'flex',
              alignItems: 'center',
              gap: 10,
              padding: '8px 12px',
              borderRadius: 'var(--radius-sm)',
              border: '2px solid var(--color-fg)',
              background: isActive ? '#ECFDF5' : '#F8FAFC',
              boxShadow: '2px 2px 0 0 var(--color-fg)',
              cursor: 'pointer',
              textAlign: 'left',
            }}
          >
            <span
              style={{
                width: 20,
                height: 20,
                borderRadius: 6,
                border: '2px solid var(--color-fg)',
                background: isActive ? 'var(--color-accent)' : '#FFFFFF',
                color: '#FFFFFF',
                display: 'grid',
                placeItems: 'center',
                flexShrink: 0,
              }}
            >
              {isActive && <Check size={13} strokeWidth={3} />}
            </span>
            <span style={{ fontSize: 'var(--fs-xs)', fontWeight: 800, color: 'var(--color-fg)' }}>
              Đang sử dụng (hiển thị khi lên thực đơn)
            </span>
          </button>

          <button type="submit" className="btn btn--primary" disabled={saving} style={{ justifyContent: 'center' }}>
            <Plus size={15} />
            {saving ? 'Đang lưu...' : editingId ? 'Lưu cập nhật' : 'Thêm vào thư viện'}
          </button>
        </form>

        {/* Danh sách món */}
        <div style={{ display: 'flex', flexDirection: 'column', gap: 12, minWidth: 0 }}>
          <div style={{ display: 'flex', gap: 8, flexWrap: 'wrap', alignItems: 'center' }}>
            <div style={{ position: 'relative', flex: '1 1 180px' }}>
              <Search size={15} style={{ position: 'absolute', left: 12, top: '50%', transform: 'translateY(-50%)', color: 'var(--color-muted-fg)' }} />
              <input
                type="text"
                className="input"
                placeholder="Tìm tên món..."
                value={search}
                onChange={(e) => setSearch(e.target.value)}
                style={{ paddingLeft: 36, border: '2px solid var(--color-fg)', fontWeight: 600 }}
              />
            </div>
            <select
              className="select"
              value={filterCategory}
              onChange={(e) => setFilterCategory(e.target.value === 'ALL' ? 'ALL' : Number(e.target.value))}
              style={{ width: 'auto', border: '2px solid var(--color-fg)', fontWeight: 700 }}
            >
              <option value="ALL">Tất cả nhóm món</option>
              {DISH_CATEGORY_OPTIONS.map((opt) => (
                <option key={opt.value} value={opt.value}>
                  {opt.label}
                </option>
              ))}
            </select>
            <button
              type="button"
              onClick={() => setShowInactive(!showInactive)}
              style={{
                display: 'inline-flex',
                alignItems: 'center',
                gap: 7,
                height: 44,
                padding: '0 12px',
                borderRadius: 'var(--radius-sm)',
                border: '2px solid var(--color-fg)',
                background: showInactive ? '#FEF3C7' : '#FFFFFF',
                boxShadow: '2px 2px 0 0 var(--color-fg)',
                fontSize: 'var(--fs-xs)',
                fontWeight: 800,
                cursor: 'pointer',
                whiteSpace: 'nowrap',
              }}
            >
              <span
                style={{
                  width: 18,
                  height: 18,
                  borderRadius: 5,
                  border: '2px solid var(--color-fg)',
                  background: showInactive ? 'var(--color-accent)' : '#FFFFFF',
                  color: '#FFFFFF',
                  display: 'grid',
                  placeItems: 'center',
                }}
              >
                {showInactive && <Check size={12} strokeWidth={3} />}
              </span>
              Hiện món ngưng dùng
            </button>
          </div>

          <div
            style={{
              border: '2px solid var(--color-fg)',
              borderRadius: 'var(--radius-md)',
              boxShadow: '4px 4px 0 0 var(--color-fg)',
              maxHeight: 430,
              overflowY: 'auto',
              background: '#FFFFFF',
            }}
          >
            {loading ? (
              <div style={{ padding: 28, textAlign: 'center', color: 'var(--color-muted-fg)', fontWeight: 700 }}>Đang tải danh mục món ăn...</div>
            ) : filteredDishes.length === 0 ? (
              <div style={{ padding: 28, textAlign: 'center', color: 'var(--color-muted-fg)', fontWeight: 700 }}>Không có món ăn phù hợp.</div>
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
                      gap: 12,
                      padding: '12px 14px',
                      borderBottom: '1.5px solid var(--color-border)',
                      background: editingId === d.id ? '#EDE9FE' : '#FFFFFF',
                      opacity: d.isActive ? 1 : 0.65,
                    }}
                  >
                    <div style={{ minWidth: 0 }}>
                      <div style={{ display: 'flex', alignItems: 'center', gap: 8, flexWrap: 'wrap' }}>
                        <UtensilsCrossed size={14} color="var(--color-accent)" style={{ flexShrink: 0 }} />
                        <strong style={{ fontSize: 'var(--fs-sm)', color: 'var(--color-fg)' }}>{d.name}</strong>
                        {!d.isActive && (
                          <span className="badge badge--pink" style={{ fontSize: 10 }}>Ngưng dùng</span>
                        )}
                      </div>
                      <div style={{ display: 'flex', gap: 6, flexWrap: 'wrap', marginTop: 6, alignItems: 'center' }}>
                        <span
                          style={{
                            fontSize: 11,
                            fontWeight: 700,
                            padding: '2px 8px',
                            borderRadius: 'var(--radius-full)',
                            background: '#F1F5F9',
                            border: '1.5px solid var(--color-fg)',
                            color: 'var(--color-fg)',
                          }}
                        >
                          {cat.label}
                        </span>
                        {(d.allergenTags ?? []).map((tag) => (
                          <span
                            key={tag}
                            style={{
                              display: 'inline-flex',
                              alignItems: 'center',
                              gap: 4,
                              fontSize: 11,
                              fontWeight: 800,
                              padding: '2px 8px',
                              borderRadius: 'var(--radius-full)',
                              background: '#FEE2E2',
                              color: '#991B1B',
                              border: '1.5px solid var(--color-fg)',
                            }}
                          >
                            <AlertTriangle size={11} /> {getAllergenShortLabel(tag)}
                          </span>
                        ))}
                      </div>
                    </div>

                    <div style={{ display: 'flex', gap: 8, flexShrink: 0 }}>
                      <button
                        type="button"
                        className="btn btn--secondary btn--sm"
                        onClick={() => startEdit(d)}
                        title="Sửa món"
                        style={{
                          padding: '6px 10px',
                          border: '2px solid var(--color-fg)',
                          boxShadow: '2px 2px 0 0 var(--color-fg)',
                          background: '#FFFFFF',
                        }}
                      >
                        <Edit3 size={14} />
                      </button>
                      <button
                        type="button"
                        className="btn btn--secondary btn--sm"
                        onClick={() => handleDelete(d)}
                        title="Xóa / Ngưng dùng"
                        style={{
                          padding: '6px 10px',
                          border: '2px solid var(--color-fg)',
                          boxShadow: '2px 2px 0 0 var(--color-fg)',
                          background: '#FFF1F2',
                          color: '#DC2626',
                        }}
                      >
                        <Trash2 size={14} />
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
