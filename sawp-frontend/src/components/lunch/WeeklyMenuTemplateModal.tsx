import { useEffect, useMemo, useState } from 'react';
import {
  CalendarCheck2,
  CalendarRange,
  Check,
  Clock,
  Copy,
  Edit3,
  Leaf,
  Plus,
  Search,
  Sparkles,
  Stethoscope,
  Trash2,
  UtensilsCrossed,
} from 'lucide-react';
import toast from 'react-hot-toast';
import { GeometricModal } from '../common/GeometricModal';
import { DatePickerPill } from '../common/DatePickerPill';
import { lunchApi } from '../../services/implementations/lunchApi';
import { formatDateDDMMYYYY } from '../../constants/lunch';
import { normalizeMealTypeNumber } from '../../constants/allergens';
import type { Dish, WeeklyMenuTemplate, WeeklyMenuTemplateItemInput } from '../../types/lunch.types';

interface WeeklyMenuTemplateModalProps {
  open: boolean;
  onClose: () => void;
  defaultDate: string;
  onApplied?: () => void;
}

const WEEKDAYS = [
  { value: 1, label: 'Thứ 2' },
  { value: 2, label: 'Thứ 3' },
  { value: 3, label: 'Thứ 4' },
  { value: 4, label: 'Thứ 5' },
  { value: 5, label: 'Thứ 6' },
  { value: 6, label: 'Thứ 7 (Học bù)' },
];

const MEAL_TABS = [
  { value: 1, label: 'Tiêu chuẩn', icon: UtensilsCrossed },
  { value: 2, label: 'Ăn chay', icon: Leaf },
  { value: 3, label: 'Đặc biệt', icon: Stethoscope },
];

function addDaysIso(iso: string, days: number): string {
  const [y, m, d] = iso.split('-').map(Number);
  const dt = new Date(y, m - 1, d + days);
  return `${dt.getFullYear()}-${String(dt.getMonth() + 1).padStart(2, '0')}-${String(dt.getDate()).padStart(2, '0')}`;
}

export function WeeklyMenuTemplateModal({
  open,
  onClose,
  defaultDate,
  onApplied,
}: WeeklyMenuTemplateModalProps) {
  const [templates, setTemplates] = useState<WeeklyMenuTemplate[]>([]);
  const [dishes, setDishes] = useState<Dish[]>([]);
  const [loading, setLoading] = useState(false);
  const [saving, setSaving] = useState(false);

  const [selectedTplId, setSelectedTplId] = useState<number | null>(null);
  const [editingMode, setEditingMode] = useState<'apply' | 'edit'>('apply');

  // Form áp dụng mẫu
  const [applyFrom, setApplyFrom] = useState(defaultDate);
  const [applyTo, setApplyTo] = useState(() => addDaysIso(defaultDate, 4));
  const [applyCutoff, setApplyCutoff] = useState('08:15');
  const [overwriteExisting, setOverwriteExisting] = useState(true);

  // Form tạo / sửa mẫu
  const [tplName, setTplName] = useState('');
  const [tplDesc, setTplDesc] = useState('');
  const [activeDay, setActiveDay] = useState<number>(1);
  const [activeMealType, setActiveMealType] = useState<number>(1);
  const [draftItems, setDraftItems] = useState<WeeklyMenuTemplateItemInput[]>([]);
  const [dishSearch, setDishSearch] = useState('');

  const loadAll = async () => {
    try {
      setLoading(true);
      const [tpls, dishList] = await Promise.all([
        lunchApi.getMenuTemplates(false),
        lunchApi.getDishes({ includeInactive: false }),
      ]);
      setTemplates(tpls);
      setDishes(dishList);
      if (tpls.length > 0 && selectedTplId === null) {
        setSelectedTplId(tpls[0].id);
      }
    } catch {
      toast.error('Không thể tải danh sách thực đơn tuần.');
    } finally {
      setLoading(false);
    }
  };

  useEffect(() => {
    if (open) {
      setApplyFrom(defaultDate);
      setApplyTo(addDaysIso(defaultDate, 4));
      setEditingMode('apply');
      loadAll();
    }
  }, [open, defaultDate]);

  const selectedTemplate = useMemo(
    () => templates.find((t) => t.id === selectedTplId) ?? null,
    [templates, selectedTplId]
  );

  const startCreateTemplate = () => {
    setSelectedTplId(null);
    setTplName('');
    setTplDesc('');
    setDraftItems([]);
    setActiveDay(1);
    setActiveMealType(1);
    setEditingMode('edit');
  };

  const startEditTemplate = (tpl: WeeklyMenuTemplate) => {
    setSelectedTplId(tpl.id);
    setTplName(tpl.name);
    setTplDesc(tpl.description || '');
    setDraftItems(
      tpl.items.map((i) => ({
        dayOfWeek: i.dayOfWeek,
        mealType: normalizeMealTypeNumber(i.mealType),
        dishId: i.dishId,
        sortOrder: i.sortOrder,
      }))
    );
    setActiveDay(1);
    setActiveMealType(1);
    setEditingMode('edit');
  };

  const toggleDraftDish = (dishId: number) => {
    setDraftItems((prev) => {
      const exists = prev.some(
        (x) => x.dayOfWeek === activeDay && x.mealType === activeMealType && x.dishId === dishId
      );
      if (exists) {
        return prev.filter(
          (x) => !(x.dayOfWeek === activeDay && x.mealType === activeMealType && x.dishId === dishId)
        );
      }
      const nextSort =
        prev.filter((x) => x.dayOfWeek === activeDay && x.mealType === activeMealType).length + 1;
      return [...prev, { dayOfWeek: activeDay, mealType: activeMealType, dishId, sortOrder: nextSort }];
    });
  };

  const handleCopyDayMealToAllWeekdays = () => {
    const source = draftItems.filter((x) => x.dayOfWeek === activeDay && x.mealType === activeMealType);
    if (source.length === 0) {
      toast.error('Chế độ ăn của ngày đang chọn chưa có món để sao chép.');
      return;
    }
    setDraftItems((prev) => {
      const kept = prev.filter((x) => !(x.mealType === activeMealType && x.dayOfWeek >= 1 && x.dayOfWeek <= 5));
      const cloned: WeeklyMenuTemplateItemInput[] = [];
      for (let d = 1; d <= 5; d++) {
        source.forEach((item, idx) => {
          cloned.push({ dayOfWeek: d, mealType: activeMealType, dishId: item.dishId, sortOrder: idx + 1 });
        });
      }
      return [...kept, ...cloned];
    });
    toast.success('Đã sao chép danh sách món cho Thứ 2 – Thứ 6.');
  };

  const handleSaveTemplate = async () => {
    if (!tplName.trim()) {
      toast.error('Vui lòng nhập tên thực đơn tuần.');
      return;
    }
    if (draftItems.length === 0) {
      toast.error('Vui lòng chọn ít nhất 1 món ăn cho thực đơn tuần.');
      return;
    }
    try {
      setSaving(true);
      const payload = {
        name: tplName.trim(),
        description: tplDesc.trim() || undefined,
        isActive: true,
        items: draftItems,
      };
      if (selectedTplId) {
        const res = await lunchApi.updateMenuTemplate(selectedTplId, payload);
        toast.success(res.message || 'Đã cập nhật thực đơn tuần.');
      } else {
        const res = await lunchApi.createMenuTemplate(payload);
        setSelectedTplId(res.data.id);
        toast.success(res.message || 'Đã tạo thực đơn tuần.');
      }
      setEditingMode('apply');
      await loadAll();
    } catch (err: unknown) {
      const apiErr = (err as { response?: { data?: { message?: string; errors?: string[] } } })?.response?.data;
      toast.error(apiErr?.errors?.[0] || apiErr?.message || 'Lưu thực đơn tuần thất bại.');
    } finally {
      setSaving(false);
    }
  };

  const handleDeleteTemplate = async (tpl: WeeklyMenuTemplate) => {
    try {
      setSaving(true);
      const res = await lunchApi.deleteMenuTemplate(tpl.id);
      toast.success(res.message || 'Đã xóa thực đơn tuần.');
      if (selectedTplId === tpl.id) setSelectedTplId(null);
      await loadAll();
    } catch {
      toast.error('Không thể xóa thực đơn tuần.');
    } finally {
      setSaving(false);
    }
  };

  const handleApplyTemplate = async () => {
    if (!selectedTemplate) {
      toast.error('Vui lòng chọn một thực đơn tuần.');
      return;
    }
    if (!applyFrom || !applyTo || applyTo < applyFrom) {
      toast.error('Khoảng ngày áp dụng không hợp lệ.');
      return;
    }
    try {
      setSaving(true);
      const res = await lunchApi.applyMenuTemplate(selectedTemplate.id, {
        fromDate: applyFrom,
        toDate: applyTo,
        cutoffTime: applyCutoff || undefined,
        overwriteExistingMenus: overwriteExisting,
      });
      const r = res.data;
      const parts = [`Áp dụng ${r.appliedDates.length} ngày`];
      if (r.skippedLockedDates.length > 0) {
        parts.push(`bỏ qua ${r.skippedLockedDates.length} ngày đã khóa (${r.skippedLockedDates.map((d) => formatDateDDMMYYYY(d)).join(', ')})`);
      }
      if (r.skippedNonSchoolDates.length > 0) {
        parts.push(`bỏ qua ${r.skippedNonSchoolDates.length} ngày nghỉ/lễ`);
      }
      toast.success(parts.join(' • '));
      onApplied?.();
      onClose();
    } catch (err: unknown) {
      const apiErr = (err as { response?: { data?: { message?: string; errors?: string[] } } })?.response?.data;
      toast.error(apiErr?.errors?.[0] || apiErr?.message || 'Áp dụng thực đơn tuần thất bại.');
    } finally {
      setSaving(false);
    }
  };

  const filteredDishesForEdit = useMemo(() => {
    const q = dishSearch.trim().toLowerCase();
    return dishes.filter((d) => !q || d.name.toLowerCase().includes(q));
  }, [dishes, dishSearch]);

  const dishById = useMemo(() => {
    const map = new Map<number, Dish>();
    dishes.forEach((d) => map.set(d.id, d));
    return map;
  }, [dishes]);

  return (
    <GeometricModal
      open={open}
      onClose={onClose}
      title="Thực Đơn Tuần"
      subtitle="Thiết kế bộ thực đơn theo tuần cho 3 chế độ ăn (Tiêu chuẩn, Ăn chay, Đặc biệt) và áp dụng nhanh cho lịch phục vụ"
      badgeText={`${templates.length} thực đơn tuần`}
      badgeColor="violet"
      icon={<Sparkles size={22} color="var(--color-fg)" />}
      iconBg="#EDE9FE"
      maxWidth="xl"
    >
      <div className="responsive-split-sidebar">
        {/* Cột trái: Danh sách thực đơn tuần */}
        <div style={{ display: 'flex', flexDirection: 'column', gap: 12 }}>
          <button type="button" className="btn btn--primary btn--sm" onClick={startCreateTemplate} style={{ justifyContent: 'center', height: 40 }}>
            <Plus size={15} /> Tạo thực đơn tuần mới
          </button>

          <div style={{ display: 'flex', flexDirection: 'column', gap: 10, maxHeight: 450, overflowY: 'auto', paddingRight: 4, paddingBottom: 4 }}>
            {loading ? (
              <div style={{ padding: 16, textAlign: 'center', fontSize: 'var(--fs-xs)', color: 'var(--color-muted-fg)', fontWeight: 700 }}>Đang tải...</div>
            ) : templates.length === 0 ? (
              <div style={{ padding: 16, textAlign: 'center', fontSize: 'var(--fs-xs)', color: 'var(--color-muted-fg)', fontWeight: 700 }}>Chưa có thực đơn tuần nào.</div>
            ) : (
              templates.map((tpl) => {
                const isSelected = selectedTplId === tpl.id && editingMode === 'apply';
                return (
                  <div
                    key={tpl.id}
                    onClick={() => {
                      setSelectedTplId(tpl.id);
                      setEditingMode('apply');
                    }}
                    style={{
                      padding: 12,
                      borderRadius: 'var(--radius-md)',
                      border: '2px solid var(--color-fg)',
                      background: isSelected ? '#F5F3FF' : '#FFFFFF',
                      boxShadow: isSelected ? '4px 4px 0 0 var(--color-fg)' : '2px 2px 0 0 var(--color-fg)',
                      cursor: 'pointer',
                      transition: 'transform 0.12s ease, box-shadow 0.12s ease',
                    }}
                  >
                    <div style={{ display: 'flex', justifyContent: 'space-between', alignItems: 'flex-start', gap: 8 }}>
                      <strong style={{ fontSize: 'var(--fs-sm)', color: 'var(--color-fg)' }}>{tpl.name}</strong>
                      <div style={{ display: 'flex', gap: 6, flexShrink: 0 }} onClick={(e) => e.stopPropagation()}>
                        <button
                          type="button"
                          className="btn btn--secondary btn--sm"
                          onClick={() => startEditTemplate(tpl)}
                          title="Sửa thực đơn tuần"
                          style={{ padding: '4px 8px', border: '2px solid var(--color-fg)', boxShadow: '2px 2px 0 0 var(--color-fg)', background: '#FFFFFF' }}
                        >
                          <Edit3 size={12} />
                        </button>
                        <button
                          type="button"
                          className="btn btn--secondary btn--sm"
                          onClick={() => handleDeleteTemplate(tpl)}
                          title="Xóa thực đơn tuần"
                          style={{ padding: '4px 8px', border: '2px solid var(--color-fg)', boxShadow: '2px 2px 0 0 var(--color-fg)', background: '#FFF1F2', color: '#DC2626' }}
                        >
                          <Trash2 size={12} />
                        </button>
                      </div>
                    </div>
                    {tpl.description && (
                      <div style={{ fontSize: 'var(--fs-xs)', color: 'var(--color-muted-fg)', marginTop: 4, lineHeight: 1.4 }}>{tpl.description}</div>
                    )}
                    <div style={{ marginTop: 8 }}>
                      <span className="badge badge--violet" style={{ fontSize: 11, padding: '2px 8px' }}>
                        {tpl.items.length} lượt món cấu hình
                      </span>
                    </div>
                  </div>
                );
              })
            )}
          </div>
        </div>

        {/* Cột phải: Áp dụng mẫu hoặc Chỉnh sửa mẫu */}
        <div
          style={{
            background: '#FFFDF5',
            border: '2px solid var(--color-fg)',
            borderRadius: 'var(--radius-md)',
            boxShadow: '4px 4px 0 0 var(--color-fg)',
            padding: 18,
            display: 'flex',
            flexDirection: 'column',
            gap: 16,
            minWidth: 0,
          }}
        >
          {editingMode === 'apply' ? (
            selectedTemplate ? (
              <>
                <div style={{ paddingBottom: 10, borderBottom: '1.5px dashed var(--color-border)' }}>
                  <h3 style={{ margin: 0, fontSize: '1.05rem', fontWeight: 900, fontFamily: 'var(--font-heading)' }}>{selectedTemplate.name}</h3>
                  <p style={{ margin: '4px 0 0', fontSize: 'var(--fs-xs)', color: 'var(--color-muted-fg)' }}>
                    {selectedTemplate.description || 'Xem trước các món theo thứ trong tuần và áp dụng nhanh cho khoảng ngày.'}
                  </p>
                </div>

                {/* Xem trước T2-T6 */}
                <div style={{ display: 'grid', gridTemplateColumns: 'repeat(auto-fit, minmax(185px, 1fr))', gap: 10, maxHeight: 235, overflowY: 'auto', paddingRight: 4, paddingBottom: 4 }}>
                  {WEEKDAYS.slice(0, 5).map((wd) => {
                    const stdItems = selectedTemplate.items.filter(
                      (i) => i.dayOfWeek === wd.value && normalizeMealTypeNumber(i.mealType) === 1
                    );
                    const vegCount = selectedTemplate.items.filter(
                      (i) => i.dayOfWeek === wd.value && normalizeMealTypeNumber(i.mealType) === 2
                    ).length;
                    const spcCount = selectedTemplate.items.filter(
                      (i) => i.dayOfWeek === wd.value && normalizeMealTypeNumber(i.mealType) === 3
                    ).length;
                    return (
                      <div
                        key={wd.value}
                        style={{
                          background: '#FFFFFF',
                          border: '2px solid var(--color-fg)',
                          borderRadius: 'var(--radius-sm)',
                          boxShadow: '2px 2px 0 0 var(--color-fg)',
                          padding: 10,
                          display: 'flex',
                          flexDirection: 'column',
                          justifyContent: 'space-between',
                          gap: 8,
                        }}
                      >
                        <div>
                          <div style={{ marginBottom: 6 }}>
                            <span className="badge badge--violet" style={{ fontSize: 11, padding: '1px 8px' }}>
                              {wd.label}
                            </span>
                          </div>
                          <div style={{ fontSize: 12, lineHeight: 1.45, color: 'var(--color-fg)', fontWeight: 600 }}>
                            {stdItems.length > 0 ? stdItems.map((i) => i.dishName).join(', ') : 'Chưa có món tiêu chuẩn'}
                          </div>
                        </div>
                        <div style={{ display: 'flex', gap: 6, flexWrap: 'wrap', paddingTop: 6, borderTop: '1px dashed var(--color-border)' }}>
                          <span style={{ display: 'inline-flex', alignItems: 'center', gap: 3, fontSize: 11, fontWeight: 700, color: '#047857' }}>
                            <Leaf size={11} /> Chay: {vegCount}
                          </span>
                          <span style={{ display: 'inline-flex', alignItems: 'center', gap: 3, fontSize: 11, fontWeight: 700, color: '#0369A1' }}>
                            <Stethoscope size={11} /> ĐB: {spcCount}
                          </span>
                        </div>
                      </div>
                    );
                  })}
                </div>

                {/* Form áp dụng khoảng ngày */}
                <div
                  style={{
                    background: '#FFFFFF',
                    border: '2px solid var(--color-fg)',
                    borderRadius: 'var(--radius-md)',
                    boxShadow: '3px 3px 0 0 var(--color-fg)',
                    padding: 14,
                    display: 'flex',
                    flexDirection: 'column',
                    gap: 12,
                  }}
                >
                  <strong style={{ fontSize: 'var(--fs-sm)', display: 'inline-flex', alignItems: 'center', gap: 6, fontFamily: 'var(--font-heading)' }}>
                    <CalendarRange size={16} color="var(--color-accent)" /> Áp dụng thực đơn tuần này cho khoảng ngày
                  </strong>
                  <div style={{ display: 'flex', alignItems: 'flex-end', gap: 12, flexWrap: 'wrap' }}>
                    <div style={{ display: 'flex', flexDirection: 'column', gap: 4 }}>
                      <label className="label" style={{ fontSize: 'var(--fs-xs)' }}>TỪ NGÀY</label>
                      <DatePickerPill
                        value={applyFrom}
                        size="md"
                        background="#FFFDF5"
                        ariaLabel="Chọn từ ngày áp dụng thực đơn"
                        onChange={(val) => {
                          setApplyFrom(val);
                          if (applyTo && val > applyTo) setApplyTo(val);
                        }}
                      />
                    </div>
                    <div style={{ display: 'flex', flexDirection: 'column', gap: 4 }}>
                      <label className="label" style={{ fontSize: 'var(--fs-xs)' }}>ĐẾN NGÀY</label>
                      <DatePickerPill
                        value={applyTo}
                        min={applyFrom}
                        size="md"
                        background="#FFFDF5"
                        ariaLabel="Chọn đến ngày áp dụng thực đơn"
                        onChange={(val) => {
                          setApplyTo(val);
                          if (applyFrom && val < applyFrom) setApplyFrom(val);
                        }}
                      />
                    </div>
                    <div style={{ display: 'flex', flexDirection: 'column', gap: 4, minWidth: 135 }}>
                      <label className="label" style={{ fontSize: 'var(--fs-xs)' }}>GIỜ CHỐT</label>
                      <div style={{ position: 'relative' }}>
                        <Clock size={15} color="var(--color-accent)" style={{ position: 'absolute', left: 12, top: '50%', transform: 'translateY(-50%)', pointerEvents: 'none' }} />
                        <input
                          type="time"
                          min="06:00"
                          max="11:00"
                          className="input"
                          value={applyCutoff}
                          onChange={(e) => setApplyCutoff(e.target.value)}
                          style={{ border: '2px solid var(--color-fg)', fontWeight: 800, paddingLeft: 34, minHeight: 42, height: 42, borderRadius: 'var(--radius-full)' }}
                        />
                      </div>
                    </div>
                  </div>

                  <button
                    type="button"
                    onClick={() => setOverwriteExisting(!overwriteExisting)}
                    style={{
                      display: 'flex',
                      alignItems: 'center',
                      gap: 10,
                      padding: '8px 12px',
                      borderRadius: 'var(--radius-sm)',
                      border: '2px solid var(--color-fg)',
                      background: overwriteExisting ? '#FEF3C7' : '#F8FAFC',
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
                        background: overwriteExisting ? 'var(--color-accent)' : '#FFFFFF',
                        color: '#FFFFFF',
                        display: 'grid',
                        placeItems: 'center',
                        flexShrink: 0,
                      }}
                    >
                      {overwriteExisting && <Check size={13} strokeWidth={3} />}
                    </span>
                    <span style={{ fontSize: 'var(--fs-xs)', fontWeight: 800, color: 'var(--color-fg)' }}>
                      Ghi đè thực đơn những ngày chưa khóa (luôn bỏ qua ngày nghỉ lễ và ngày đã chốt sổ)
                    </span>
                  </button>

                  <div style={{ display: 'flex', justifyContent: 'flex-end', gap: 10, paddingTop: 4 }}>
                    <button
                      type="button"
                      className="btn btn--secondary"
                      onClick={() => startEditTemplate(selectedTemplate)}
                      style={{ border: '2px solid var(--color-fg)', boxShadow: '2px 2px 0 0 var(--color-fg)', background: '#FFFFFF' }}
                    >
                      <Edit3 size={14} /> Chỉnh sửa thực đơn tuần
                    </button>
                    <button type="button" className="btn btn--primary" onClick={handleApplyTemplate} disabled={saving}>
                      <CalendarCheck2 size={15} /> {saving ? 'Đang áp dụng...' : 'Áp dụng thực đơn'}
                    </button>
                  </div>
                </div>
              </>
            ) : (
              <div style={{ padding: 36, textAlign: 'center', color: 'var(--color-muted-fg)', fontWeight: 700 }}>
                Chọn một thực đơn tuần ở bên trái để xem và áp dụng, hoặc bấm <strong>Tạo thực đơn tuần mới</strong>.
              </div>
            )
          ) : (
            /* Chế độ tạo / sửa thực đơn tuần */
            <>
              <div className="responsive-grid-2">
                <div>
                  <label className="label" style={{ fontSize: 'var(--fs-xs)', marginBottom: 4 }}>TÊN THỰC ĐƠN TUẦN *</label>
                  <input
                    type="text"
                    className="input"
                    maxLength={120}
                    value={tplName}
                    onChange={(e) => setTplName(e.target.value)}
                    placeholder="VD: Thực đơn Tuần C – Hải sản & Canh chua"
                    style={{ border: '2px solid var(--color-fg)', fontWeight: 700 }}
                  />
                </div>
                <div>
                  <label className="label" style={{ fontSize: 'var(--fs-xs)', marginBottom: 4 }}>MÔ TẢ NGẮN</label>
                  <input
                    type="text"
                    className="input"
                    maxLength={250}
                    value={tplDesc}
                    onChange={(e) => setTplDesc(e.target.value)}
                    placeholder="Ghi chú phạm vi áp dụng..."
                    style={{ border: '2px solid var(--color-fg)', fontWeight: 600 }}
                  />
                </div>
              </div>

              {/* Chọn thứ */}
              <div style={{ display: 'flex', gap: 8, flexWrap: 'wrap' }}>
                {WEEKDAYS.map((wd) => {
                  const cnt = draftItems.filter((x) => x.dayOfWeek === wd.value).length;
                  const active = activeDay === wd.value;
                  return (
                    <button
                      key={wd.value}
                      type="button"
                      onClick={() => setActiveDay(wd.value)}
                      style={{
                        padding: '5px 12px',
                        borderRadius: 'var(--radius-full)',
                        border: '2px solid var(--color-fg)',
                        background: active ? 'var(--color-tertiary)' : '#FFFFFF',
                        boxShadow: active ? '2px 2px 0 0 var(--color-fg)' : 'none',
                        fontWeight: 800,
                        fontSize: 'var(--fs-xs)',
                        cursor: 'pointer',
                      }}
                    >
                      {wd.label} ({cnt})
                    </button>
                  );
                })}
              </div>

              {/* Chọn chế độ ăn + nút copy */}
              <div style={{ display: 'flex', justifyContent: 'space-between', alignItems: 'center', flexWrap: 'wrap', gap: 8 }}>
                <div style={{ display: 'flex', gap: 8, flexWrap: 'wrap' }}>
                  {MEAL_TABS.map((mt) => {
                    const active = activeMealType === mt.value;
                    const cnt = draftItems.filter((x) => x.dayOfWeek === activeDay && x.mealType === mt.value).length;
                    const Icon = mt.icon;
                    return (
                      <button
                        key={mt.value}
                        type="button"
                        onClick={() => setActiveMealType(mt.value)}
                        style={{
                          display: 'inline-flex',
                          alignItems: 'center',
                          gap: 6,
                          padding: '6px 12px',
                          borderRadius: 'var(--radius-full)',
                          border: '2px solid var(--color-fg)',
                          background: active ? '#DCFCE7' : '#FFFFFF',
                          boxShadow: active ? '2px 2px 0 0 var(--color-fg)' : 'none',
                          color: active ? '#065F46' : 'var(--color-fg)',
                          fontWeight: 800,
                          fontSize: 'var(--fs-xs)',
                          cursor: 'pointer',
                        }}
                      >
                        <Icon size={13} /> {mt.label} ({cnt})
                      </button>
                    );
                  })}
                </div>
                <button
                  type="button"
                  className="btn btn--secondary btn--sm"
                  onClick={handleCopyDayMealToAllWeekdays}
                  style={{ border: '2px solid var(--color-fg)', boxShadow: '2px 2px 0 0 var(--color-fg)', background: '#FFFFFF' }}
                >
                  <Copy size={13} /> Chép chế độ này cho T2–T6
                </button>
              </div>

              {/* Chọn món từ Thư viện */}
              <div style={{ position: 'relative' }}>
                <Search size={15} style={{ position: 'absolute', left: 12, top: '50%', transform: 'translateY(-50%)', color: 'var(--color-muted-fg)' }} />
                <input
                  type="text"
                  className="input"
                  placeholder="Tìm nhanh món trong thư viện để tick chọn..."
                  value={dishSearch}
                  onChange={(e) => setDishSearch(e.target.value)}
                  style={{ paddingLeft: 36, border: '2px solid var(--color-fg)', fontWeight: 600 }}
                />
              </div>

              <div
                style={{
                  display: 'flex',
                  flexWrap: 'wrap',
                  gap: 8,
                  maxHeight: 175,
                  overflowY: 'auto',
                  padding: 12,
                  background: '#FFFFFF',
                  border: '2px solid var(--color-fg)',
                  borderRadius: 'var(--radius-md)',
                  boxShadow: '3px 3px 0 0 var(--color-fg)',
                }}
              >
                {filteredDishesForEdit.map((d) => {
                  const selected = draftItems.some(
                    (x) => x.dayOfWeek === activeDay && x.mealType === activeMealType && x.dishId === d.id
                  );
                  return (
                    <button
                      key={d.id}
                      type="button"
                      onClick={() => toggleDraftDish(d.id)}
                      style={{
                        display: 'inline-flex',
                        alignItems: 'center',
                        gap: 5,
                        padding: '5px 11px',
                        borderRadius: 'var(--radius-full)',
                        border: selected ? '2px solid var(--color-fg)' : '1.5px dashed var(--color-muted-fg)',
                        background: selected ? '#FEF3C7' : '#FFFDF5',
                        boxShadow: selected ? '2px 2px 0 0 var(--color-fg)' : 'none',
                        fontWeight: selected ? 800 : 600,
                        fontSize: 'var(--fs-xs)',
                        cursor: 'pointer',
                      }}
                    >
                      {selected && <Check size={12} strokeWidth={3} />}
                      {d.name}
                    </button>
                  );
                })}
              </div>

              <div
                style={{
                  fontSize: 'var(--fs-xs)',
                  color: 'var(--color-muted-fg)',
                  padding: '8px 12px',
                  background: '#FFFFFF',
                  border: '1.5px dashed var(--color-border)',
                  borderRadius: 'var(--radius-sm)',
                }}
              >
                Đang chọn:{' '}
                <strong style={{ color: 'var(--color-fg)' }}>
                  {draftItems
                    .filter((x) => x.dayOfWeek === activeDay && x.mealType === activeMealType)
                    .map((x) => dishById.get(x.dishId)?.name)
                    .filter(Boolean)
                    .join(', ') || 'Chưa có món nào'}
                </strong>
              </div>

              <div style={{ display: 'flex', justifyContent: 'flex-end', gap: 10 }}>
                <button
                  type="button"
                  className="btn btn--secondary"
                  onClick={() => setEditingMode('apply')}
                  style={{ border: '2px solid var(--color-fg)', boxShadow: '2px 2px 0 0 var(--color-fg)', background: '#FFFFFF' }}
                >
                  Quay lại
                </button>
                <button type="button" className="btn btn--primary" onClick={handleSaveTemplate} disabled={saving}>
                  {saving ? 'Đang lưu...' : 'Lưu thực đơn tuần'}
                </button>
              </div>
            </>
          )}
        </div>
      </div>
    </GeometricModal>
  );
}
