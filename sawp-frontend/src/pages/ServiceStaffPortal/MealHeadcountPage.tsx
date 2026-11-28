import { useEffect, useMemo, useState } from 'react';
import { useSearchParams } from 'react-router';
import {
  UtensilsCrossed,
  Clock,
  CheckCircle2,
  Lock,
  RefreshCw,
  UserMinus,
  Soup,
  ShieldAlert,
  FileSpreadsheet,
  Settings,
  Users,
  Filter,
  Search,
  ArrowRight,
  ChevronLeft,
  ChevronRight,
  CalendarRange,
  BookOpen,
  Sparkles,
  AlertTriangle,
  Bell,
  Download,
} from 'lucide-react';
import toast from 'react-hot-toast';
import { lunchApi } from '../../services/implementations/lunchApi';
import type {
  ClassMealSummary,
  DailyMealBoard,
  DailyMenusByMealType,
  DishSummary,
  LunchPeriodReport,
  StudentMealOperationItem,
  WeeklyMenuTemplate,
} from '../../types/lunch.types';
import { LUNCH_CUTOFF_DEFAULT_TIME, LUNCH_CUTOFF_LABEL, formatDateDDMMYYYY } from '../../constants/lunch';
import { SYSTEM_MESSAGES, getSystemMessage } from '../../constants/messages';
import {
  detectLiveDishConflicts,
  getAllergenFullLabel,
  getAllergenShortLabel,
  normalizeMealTypeNumber,
} from '../../constants/allergens';
import { GeometricModal } from '../../components/common/GeometricModal';
import { DatePickerPill } from '../../components/common/DatePickerPill';
import { DishLibraryModal } from '../../components/lunch/DishLibraryModal';
import { WeeklyMenuTemplateModal } from '../../components/lunch/WeeklyMenuTemplateModal';
import { formatViTime } from '../../utils/date';
import { asApiError } from '../../utils/apiError';

const formatClassLabel = (className: string, classId?: number): string =>
  classId === 0 || className === 'Chưa phân lớp' ? className : `Lớp ${className}`;

const COURSE_SLOTS = [
  { role: 'Món chính • Tinh bột', emoji: '🍚', bg: '#FEF3C7', accent: '#D97706' },
  { role: 'Món mặn • Giàu đạm', emoji: '🍗', bg: '#FCE7F3', accent: '#DB2777' },
  { role: 'Món xào • Rau củ', emoji: '🥦', bg: '#ECFDF5', accent: '#059669' },
  { role: 'Món canh • Thanh mát', emoji: '🍲', bg: '#EDE9FE', accent: '#7C3AED' },
  { role: 'Tráng miệng • Vitamin', emoji: '🍌', bg: '#FFFDF5', accent: '#EA580C' },
];

const MEAL_TYPE_TEXT: Record<string, string> = {
  StandardLunch: 'Tiêu chuẩn',
  VegetarianLunch: 'Ăn chay',
  SpecialDiet: 'Đặc biệt',
  '1': 'Tiêu chuẩn',
  '2': 'Ăn chay',
  '3': 'Đặc biệt',
};

function getMealTypeBadgeClass(mealType: string | number): string {
  const num = normalizeMealTypeNumber(mealType);
  if (num === 2) return 'badge--amber';
  if (num === 3) return 'badge--pink';
  return 'badge--mint';
}

function formatCleanRemark(remarks?: string | null): string {
  const raw = (remarks || '').trim();
  if (!raw || raw === 'Suất ăn bán trú tiêu chuẩn') return '';
  if (raw.includes('|')) {
    const [, notePart] = raw.split('|');
    return (notePart || '').replace(/^Ghi chú:\s*/i, '').trim();
  }
  return raw;
}

function getDistinctKitchenRemark(item: StudentMealOperationItem): string {
  const cleanRegRemark = formatCleanRemark(item.remarks);
  if (!cleanRegRemark) return '';
  const profileNotes = (item.studentAllergyNotes || '').trim();
  if (profileNotes && cleanRegRemark.toLowerCase() === profileNotes.toLowerCase()) {
    return '';
  }
  return cleanRegRemark;
}

function hasStudentAllergyInfo(item: StudentMealOperationItem): boolean {
  return (
    (item.studentAllergenTags?.length ?? 0) > 0 ||
    Boolean(item.studentAllergyNotes?.trim()) ||
    Boolean(item.allergyWarning?.trim()) ||
    (item.allergyConflicts?.length ?? 0) > 0
  );
}

function getStudentDishes(
  mealType: string | number,
  dailyMenus?: DailyMenusByMealType | null,
  dailyMenuRemarks?: string | null,
  studentRemarks?: string | null
): string[] {
  const rawRemarks = (studentRemarks || '').trim();
  if (rawRemarks.includes('|')) {
    const [menuPart] = rawRemarks.split('|');
    const customDishes = menuPart.split(',').map((d) => d.trim()).filter(Boolean);
    if (customDishes.length >= 2) {
      return customDishes;
    }
  }

  const mtNum = normalizeMealTypeNumber(mealType);
  if (dailyMenus) {
    const structured =
      mtNum === 2
        ? dailyMenus.vegetarian
        : mtNum === 3
          ? dailyMenus.special
          : dailyMenus.standard;
    if (structured && structured.length > 0) {
      return structured.map((d) => d.name);
    }
    if (dailyMenus.standard && dailyMenus.standard.length > 0) {
      return dailyMenus.standard.map((d) => d.name);
    }
  }

  const cleanMenu = (dailyMenuRemarks || '')
    .replace(/^Thực đơn:\s*/i, '')
    .replace(/^Thực đơn Tân Lập A:\s*/i, '')
    .trim();
  const parsed = cleanMenu
    .split(',')
    .map((d) => d.trim())
    .filter(Boolean);
  if (parsed.length >= 2 && !cleanMenu.toLowerCase().includes('phục vụ bán trú bình thường')) {
    return parsed;
  }
  return [];
}

function getIsoDayOfWeek(isoDate: string): number {
  const [y, m, d] = isoDate.slice(0, 10).split('-').map(Number);
  const jsDay = new Date(y, m - 1, d).getDay();
  return jsDay === 0 ? 7 : jsDay;
}

function getWeekMonToSatOfDate(isoDate: string): string[] {
  const [y, m, d] = isoDate.slice(0, 10).split('-').map(Number);
  const base = new Date(y, m - 1, d);
  const jsDay = base.getDay();
  const diffToMonday = jsDay === 0 ? -6 : 1 - jsDay;
  const monday = new Date(y, m - 1, d + diffToMonday);

  const days: string[] = [];
  for (let i = 0; i < 6; i++) {
    const cur = new Date(monday.getFullYear(), monday.getMonth(), monday.getDate() + i);
    days.push(
      `${cur.getFullYear()}-${String(cur.getMonth() + 1).padStart(2, '0')}-${String(cur.getDate()).padStart(2, '0')}`
    );
  }
  return days;
}

function previewTemplateForDate(tpl: WeeklyMenuTemplate, isoDate: string) {
  const dow = getIsoDayOfWeek(isoDate);
  let dayItems = tpl.items.filter((i) => i.dayOfWeek === dow);
  if (dayItems.length === 0 && dow >= 6) {
    dayItems = tpl.items.filter((i) => i.dayOfWeek === 1);
  }
  return {
    standard: dayItems.filter((i) => normalizeMealTypeNumber(i.mealType) === 1).map((i) => i.dishName),
    vegetarian: dayItems.filter((i) => normalizeMealTypeNumber(i.mealType) === 2).map((i) => i.dishName),
    special: dayItems.filter((i) => normalizeMealTypeNumber(i.mealType) === 3).map((i) => i.dishName),
  };
}

const KITCHEN_SUBSTITUTION_OPTIONS = [
  'Bỏ món trùng dị ứng, thay bằng thịt nạc hấp & canh rau củ',
  'Bỏ món trùng dị ứng, thay bằng cháo thịt bằm / cháo rau củ',
  'Bỏ món hải sản, thay bằng thịt lợn rim nhạt & canh rau ngót',
  'Bỏ món trứng / sữa, thay bằng thịt gà xé & hoa quả tươi',
  'Bỏ món đậu phụ / đậu nành, thay bằng nấm xào & canh bí',
];

export default function MealHeadcountPage() {
  const [searchParams, setSearchParams] = useSearchParams();
  const activeTab = (searchParams.get('tab') as 'summary' | 'students' | 'logs' | 'report') || 'summary';

  const todayStr = useMemo(() => {
    const d = new Date();
    return `${d.getFullYear()}-${String(d.getMonth() + 1).padStart(2, '0')}-${String(d.getDate()).padStart(2, '0')}`;
  }, []);
  const [selectedDate, setSelectedDate] = useState<string>(todayStr);
  const isFutureSelectedDate = selectedDate > todayStr;
  const [board, setBoard] = useState<DailyMealBoard | null>(null);
  const [loading, setLoading] = useState<boolean>(true);
  const [actionLoading, setActionLoading] = useState<boolean>(false);
  const [message, setMessage] = useState<{ text: string; type: 'success' | 'error' } | null>(null);

  // Bộ lọc & Tìm kiếm danh sách học sinh (UC-33)
  const [searchQuery, setSearchQuery] = useState<string>('');
  const [selectedClassFilter, setSelectedClassFilter] = useState<string>('ALL');
  const [statusFilter, setStatusFilter] = useState<'ALL' | 'ACTIVE' | 'CANCELLED'>('ACTIVE');
  const [dietFilter, setDietFilter] = useState<'ALL' | 'SPECIAL_ALLERGY' | 'CONFLICT_ONLY'>('ALL');

  // Phân trang (Pagination) chuẩn theo phong cách Hồ sơ học sinh
  const [pageIndex, setPageIndex] = useState<number>(1);
  const [pageSize, setPageSize] = useState<number>(10);

  // Bộ lọc cho tab Nhật ký biến động
  const [logSearchQuery, setLogSearchQuery] = useState<string>('');
  const [logFilter, setLogFilter] = useState<'ALL' | 'AutoCancelled' | 'ParentCancelled' | 'Restored'>('ALL');
  const [logPageIndex, setLogPageIndex] = useState<number>(1);
  const [logPageSize, setLogPageSize] = useState<number>(10);

  // Modal Chốt suất ăn (08h15)
  const [showLockModal, setShowLockModal] = useState<boolean>(false);
  const [showUnlockModal, setShowUnlockModal] = useState<boolean>(false);
  const [unlockReason, setUnlockReason] = useState<string>('');
  const [lockNotes, setLockNotes] = useState<string>(
    `Đã kiểm tra đối chiếu danh sách điểm danh và đơn nghỉ lúc ${LUNCH_CUTOFF_LABEL}`
  );

  // Modal UC-32: Cấu hình ngày phục vụ, hạn đăng ký/hủy & chọn Thực đơn tuần
  const [showConfigModal, setShowConfigModal] = useState<boolean>(false);
  const [showDishLibrary, setShowDishLibrary] = useState<boolean>(false);
  const [showTemplateModal, setShowTemplateModal] = useState<boolean>(false);
  const [menuTemplates, setMenuTemplates] = useState<WeeklyMenuTemplate[]>([]);
  const [cfgIsServing, setCfgIsServing] = useState<boolean>(true);
  const [cfgCutoffTime, setCfgCutoffTime] = useState<string>(LUNCH_CUTOFF_DEFAULT_TIME);
  const [cfgSelectedTemplateId, setCfgSelectedTemplateId] = useState<number | ''>('');
  const [cfgRemarks, setCfgRemarks] = useState<string>('');
  const [cfgApplyFullWeek, setCfgApplyFullWeek] = useState<boolean>(false);

  // Modal Xem chi tiết khẩu phần từng lớp & khay ăn học sinh (chỉ chọn phương án đổi món của bếp — không chọn chế độ ăn từng em)
  const [detailClass, setDetailClass] = useState<ClassMealSummary | null>(null);
  const [selectedStudentTray, setSelectedStudentTray] = useState<StudentMealOperationItem | null>(null);
  const [kitchenNotePreset, setKitchenNotePreset] = useState<string>('');
  const [kitchenNote, setKitchenNote] = useState<string>('');
  const [savingKitchenNote, setSavingKitchenNote] = useState<boolean>(false);

  useEffect(() => {
    if (selectedStudentTray) {
      const existing = getDistinctKitchenRemark(selectedStudentTray).replace(/^\[Bếp đổi món\]\s*/i, '').trim();
      if (!existing) {
        setKitchenNotePreset('');
        setKitchenNote('');
      } else if (KITCHEN_SUBSTITUTION_OPTIONS.includes(existing)) {
        setKitchenNotePreset(existing);
        setKitchenNote(existing);
      } else {
        setKitchenNotePreset('__CUSTOM__');
        setKitchenNote(existing);
      }
    }
  }, [selectedStudentTray]);

  const handleSaveKitchenNote = async () => {
    if (!selectedStudentTray) return;
    const rawValue = kitchenNotePreset === '__CUSTOM__' ? kitchenNote.trim() : kitchenNotePreset.trim();
    const formatted = rawValue ? `[Bếp đổi món] ${rawValue}` : undefined;
    if (formatted && formatted.length > 250) {
      toast.error('Ghi chú đổi món của bếp tối đa 235 ký tự.');
      return;
    }
    try {
      setSavingKitchenNote(true);
      await lunchApi.updateRegistrationMealType({
        studentId: selectedStudentTray.studentId,
        date: selectedDate,
        mealType: normalizeMealTypeNumber(selectedStudentTray.mealType),
        remarks: formatted,
      });
      toast.success(
        formatted
          ? 'Đã lưu ghi chú đổi món của Nhà bếp (hiển thị cho GVCN & Phụ huynh).'
          : 'Đã xóa ghi chú đổi món của Nhà bếp.'
      );
      setSelectedStudentTray(null);
      await fetchBoard(selectedDate);
    } catch (err: unknown) {
      const apiErr = asApiError(err);
      toast.error(apiErr.errors?.[0] || apiErr.message || 'Lưu ghi chú đổi món thất bại.');
    } finally {
      setSavingKitchenNote(false);
    }
  };

  // Modal Gửi thông báo mở kỳ đăng ký bán trú (25–28 cuối tháng) tới toàn bộ Phụ huynh
  const [showNotifyWindowModal, setShowNotifyWindowModal] = useState<boolean>(false);
  const [notifyTargetMonth, setNotifyTargetMonth] = useState<string>(() => {
    const d = new Date();
    const next = new Date(d.getFullYear(), d.getMonth() + 1, 1);
    return `${next.getFullYear()}-${String(next.getMonth() + 1).padStart(2, '0')}`;
  });
  const [notifyCustomMessage, setNotifyCustomMessage] = useState<string>('');
  const [sendingWindowNotify, setSendingWindowNotify] = useState<boolean>(false);

  const handleSendRegistrationWindowNotice = async () => {
    if (!notifyTargetMonth || !/^\d{4}-\d{2}$/.test(notifyTargetMonth)) {
      toast.error('Vui lòng chọn tháng áp dụng hợp lệ (YYYY-MM).');
      return;
    }
    if (notifyCustomMessage.trim().length > 300) {
      toast.error('Ghi chú bổ sung tối đa 300 ký tự.');
      return;
    }
    try {
      setSendingWindowNotify(true);
      const res = await lunchApi.notifyRegistrationWindow({
        targetMonth: notifyTargetMonth,
        customMessage: notifyCustomMessage.trim() || undefined,
      });
      toast.success(res.message || `Đã gửi thông báo đăng ký bán trú tới ${res.data.notifiedParentCount} phụ huynh.`);
      setShowNotifyWindowModal(false);
      setNotifyCustomMessage('');
    } catch (err: unknown) {
      const apiErr = asApiError(err);
      toast.error(apiErr.errors?.[0] || apiErr.message || 'Gửi thông báo đăng ký bán trú thất bại.');
    } finally {
      setSendingWindowNotify(false);
    }
  };

  const escapeCsvCell = (val: string | number | null | undefined): string => {
    const s = String(val ?? '');
    if (/[",\r\n]/.test(s)) {
      return `"${s.replace(/"/g, '""')}"`;
    }
    return s;
  };

  const handleExportPeriodReportCsv = () => {
    if (!periodReport) {
      toast.error('Chưa có dữ liệu báo cáo để xuất CSV.');
      return;
    }
    const fromDisplay = formatDateDDMMYYYY(reportFrom);
    const toDisplay = formatDateDDMMYYYY(reportTo);
    const rows: (string | number)[][] = [
      ['BÁO CÁO THỐNG KÊ SUẤT ĂN BÁN TRÚ THEO KỲ'],
      ['Khoảng thời gian', `Từ ${fromDisplay} đến ${toDisplay}`],
      ['Số ngày phục vụ', periodReport.totalServiceDays],
      ['Tổng suất đăng ký', periodReport.totalInitialRegistered],
      ['Tổng đã hủy hợp lệ', periodReport.totalCancelled],
      ['Tổng suất thực nấu', periodReport.totalActualMeals],
      ['Tiêu chuẩn (Mặn)', periodReport.totalStandard],
      ['Ăn chay', periodReport.totalVegetarian],
      ['Đặc biệt', periodReport.totalSpecialDiet],
      [],
      ['Lớp', 'Khối', 'Tổng đăng ký', 'Đã hủy', 'Thực nấu', 'Tiêu chuẩn (Mặn)', 'Ăn chay', 'Đặc biệt'],
      ...periodReport.classSummaries.map((c) => [
        formatClassLabel(c.className, c.classId),
        c.grade > 0 ? `Khối ${c.grade}` : '—',
        c.initialRegistered,
        c.cancelledCount,
        c.actualCount,
        c.standardCount ?? 0,
        c.vegetarianCount ?? 0,
        c.specialDietCount ?? 0,
      ]),
      [
        'TỔNG CỘNG',
        '',
        periodReport.totalInitialRegistered,
        periodReport.totalCancelled,
        periodReport.totalActualMeals,
        periodReport.totalStandard,
        periodReport.totalVegetarian,
        periodReport.totalSpecialDiet,
      ],
    ];

    const csvContent = '\uFEFF' + rows.map((r) => r.map(escapeCsvCell).join(',')).join('\r\n');
    const blob = new Blob([csvContent], { type: 'text/csv;charset=utf-8;' });
    const url = URL.createObjectURL(blob);
    const a = document.createElement('a');
    a.href = url;
    a.download = `bao-cao-thong-ke-suat-an_${reportFrom}_den_${reportTo}.csv`;
    document.body.appendChild(a);
    a.click();
    document.body.removeChild(a);
    URL.revokeObjectURL(url);
    toast.success('Đã xuất file CSV báo cáo thống kê suất ăn.');
  };

  const evaluateTrayConflicts = (studentItem: StudentMealOperationItem) => {
    const mealTypeNum = normalizeMealTypeNumber(studentItem.mealType);
    const dishes = getStudentDishes(
      mealTypeNum,
      board?.menus,
      board?.serviceRemarks,
      studentItem.remarks
    ).filter((d) => !d.startsWith('(Lưu ý'));

    const structuredDishes: DishSummary[] = board?.menus
      ? ((mealTypeNum === 2
          ? board.menus.vegetarian
          : mealTypeNum === 3
            ? board.menus.special
            : board.menus.standard) ?? [])
      : [];
    const effectiveStructured =
      structuredDishes.length > 0 ? structuredDishes : (board?.menus?.standard ?? []);

    const dishObjectsForCheck =
      (studentItem.remarks || '').includes('|')
        ? dishes.map((name) => {
            const matched = effectiveStructured.find((sd) => sd.name.toLowerCase() === name.toLowerCase());
            return matched ?? { name, allergenTags: [] };
          })
        : effectiveStructured.length > 0
          ? effectiveStructured
          : dishes.map((name) => ({ name, allergenTags: [] }));

    const { conflicts: liveConflicts, conflictingDishNames } = detectLiveDishConflicts(
      studentItem.studentAllergenTags,
      studentItem.studentAllergyNotes,
      dishObjectsForCheck,
      studentItem.allergyWarning || studentItem.remarks
    );

    return {
      mealTypeNum,
      dishes,
      liveConflicts,
      conflictingDishNames,
      hasStudentAllergyProfile: hasStudentAllergyInfo(studentItem),
    };
  };

  // Tab 4: Báo cáo thống kê kỳ / tháng (Lunch Report)
  const [reportFrom, setReportFrom] = useState<string>(() => {
    const d = new Date();
    return `${d.getFullYear()}-${String(d.getMonth() + 1).padStart(2, '0')}-01`;
  });
  const [reportTo, setReportTo] = useState<string>(() => {
    const d = new Date();
    const last = new Date(d.getFullYear(), d.getMonth() + 1, 0);
    return `${last.getFullYear()}-${String(last.getMonth() + 1).padStart(2, '0')}-${String(last.getDate()).padStart(2, '0')}`;
  });
  const [periodReport, setPeriodReport] = useState<LunchPeriodReport | null>(null);
  const [loadingReport, setLoadingReport] = useState<boolean>(false);

  const fetchPeriodReport = async (from: string, to: string) => {
    if (!from || !to) {
      toast.error('Vui lòng chọn đầy đủ Từ ngày và Đến ngày.');
      return;
    }
    if (to < from) {
      toast.error('Từ ngày phải trước hoặc bằng Đến ngày.');
      setPeriodReport(null);
      return;
    }
    const [fy, fm, fd] = from.split('-').map(Number);
    const [ty, tm, td] = to.split('-').map(Number);
    const diffDays = Math.round((new Date(ty, tm - 1, td).getTime() - new Date(fy, fm - 1, fd).getTime()) / 86_400_000);
    if (diffDays > 92) {
      toast.error('Khoảng thời gian báo cáo tối đa 92 ngày.');
      setPeriodReport(null);
      return;
    }
    try {
      setLoadingReport(true);
      const data = await lunchApi.getPeriodReport(from, to);
      setPeriodReport(data);
    } catch (err: unknown) {
      const apiErr = asApiError(err);
      toast.error(apiErr.errors?.[0] || apiErr.message || 'Không thể tải báo cáo tổng hợp suất ăn.');
    } finally {
      setLoadingReport(false);
    }
  };

  useEffect(() => {
    if (activeTab === 'report') {
      fetchPeriodReport(reportFrom, reportTo);
    }
  }, [activeTab, reportFrom, reportTo]);

  const loadMenuTemplates = async () => {
    try {
      const list = await lunchApi.getMenuTemplates(false);
      setMenuTemplates(list);
    } catch {
      // ignore if user role has no template access
    }
  };

  useEffect(() => {
    if (showConfigModal) {
      setCfgSelectedTemplateId('');
      loadMenuTemplates();
    }
  }, [showConfigModal]);

  const fetchBoard = async (date: string) => {
    try {
      setLoading(true);
      const data = await lunchApi.getDailyMealBoard(date);
      setBoard(data);
      setCfgIsServing(data.isServing ?? true);
      setCfgCutoffTime(data.cutoffTime || LUNCH_CUTOFF_DEFAULT_TIME);
      setCfgRemarks(data.serviceRemarks || '');
    } catch (err: unknown) {
      const apiErr = asApiError(err);
      setMessage({ text: apiErr.errors?.[0] || apiErr.message || 'Không thể tải bảng tổng hợp suất ăn bán trú.', type: 'error' });
    } finally {
      setLoading(false);
    }
  };

  useEffect(() => {
    fetchBoard(selectedDate);
  }, [selectedDate]);

  const effectiveCutoffLabel = useMemo(() => {
    if (!board?.cutoffTime) return LUNCH_CUTOFF_LABEL;
    return board.cutoffTime.replace(':', 'h');
  }, [board?.cutoffTime]);

  const hasKitchenResolution = (remarks?: string | null): boolean =>
    Boolean(remarks && /^\[Bếp đổi món\]/i.test(remarks.trim()));

  const getCleanKitchenResolutionText = (remarks?: string | null): string =>
    (remarks || '').trim().replace(/^\[Bếp đổi món\]\s*/i, '');

  const mealBreakdownStats = useMemo(() => {
    const active = (board?.studentMeals ?? []).filter((s) => s.isActive);
    return {
      standard: active.filter((s) => normalizeMealTypeNumber(s.mealType) === 1).length,
      vegetarian: active.filter((s) => normalizeMealTypeNumber(s.mealType) === 2).length,
      special: active.filter((s) => normalizeMealTypeNumber(s.mealType) === 3).length,
      unresolvedConflicts: active.filter(
        (s) => (s.allergyConflicts?.length ?? 0) > 0 && !hasKitchenResolution(s.remarks)
      ).length,
      specialOrAllergy: active.filter(
        (s) => normalizeMealTypeNumber(s.mealType) !== 1 || hasStudentAllergyInfo(s)
      ).length,
    };
  }, [board?.studentMeals]);

  // Lọc danh sách học sinh
  const filteredStudentMeals = useMemo(() => {
    const list = board?.studentMeals ?? [];
    return list.filter((item) => {
      if (selectedClassFilter !== 'ALL' && String(item.classId) !== selectedClassFilter) {
        return false;
      }
      if (statusFilter === 'ACTIVE' && !item.isActive) return false;
      if (statusFilter === 'CANCELLED' && item.isActive) return false;

      if (dietFilter === 'CONFLICT_ONLY') {
        if (!item.isActive || (item.allergyConflicts?.length ?? 0) === 0 || hasKitchenResolution(item.remarks)) return false;
      } else if (dietFilter === 'SPECIAL_ALLERGY') {
        const isSpecialOrAllergy =
          normalizeMealTypeNumber(item.mealType) !== 1 || hasStudentAllergyInfo(item);
        if (!item.isActive || !isSpecialOrAllergy) return false;
      }

      if (searchQuery.trim()) {
        const q = searchQuery.toLowerCase().trim();
        const matchName = item.studentName.toLowerCase().includes(q);
        const matchCode = item.studentCode.toLowerCase().includes(q);
        const matchClass = item.className.toLowerCase().includes(q);
        if (!matchName && !matchCode && !matchClass) return false;
      }

      return true;
    });
  }, [board?.studentMeals, selectedClassFilter, statusFilter, dietFilter, searchQuery]);

  // Reset trang về 1 khi đổi bộ lọc
  useEffect(() => {
    setPageIndex(1);
  }, [selectedClassFilter, statusFilter, dietFilter, searchQuery, selectedDate, pageSize]);

  // Phân trang dữ liệu học sinh
  const totalCount = filteredStudentMeals.length;
  const totalPages = Math.max(1, Math.ceil(totalCount / pageSize));
  const pagedStudents = useMemo(() => {
    const start = (pageIndex - 1) * pageSize;
    return filteredStudentMeals.slice(start, start + pageSize);
  }, [filteredStudentMeals, pageIndex, pageSize]);

  // Lọc nhật ký biến động
  const filteredLogs = useMemo(() => {
    const logs = board?.mealChangeLogs ?? [];
    return logs.filter((l) => {
      if (logFilter !== 'ALL' && l.changeType !== logFilter) return false;
      if (logSearchQuery.trim()) {
        const q = logSearchQuery.toLowerCase().trim();
        const matchName = l.studentName.toLowerCase().includes(q);
        const matchCode = l.studentCode.toLowerCase().includes(q);
        const matchClass = l.className.toLowerCase().includes(q);
        const matchReason = l.reason.toLowerCase().includes(q);
        if (!matchName && !matchCode && !matchClass && !matchReason) return false;
      }
      return true;
    });
  }, [board?.mealChangeLogs, logFilter, logSearchQuery]);

  useEffect(() => {
    setLogPageIndex(1);
  }, [logFilter, logSearchQuery, selectedDate, logPageSize]);

  const totalLogCount = filteredLogs.length;
  const totalLogPages = Math.max(1, Math.ceil(totalLogCount / logPageSize));
  const pagedLogs = useMemo(() => {
    const start = (logPageIndex - 1) * logPageSize;
    return filteredLogs.slice(start, start + logPageSize);
  }, [filteredLogs, logPageIndex, logPageSize]);

  const handleTabChange = (tab: 'summary' | 'students' | 'logs' | 'report') => {
    setSearchParams({ tab });
  };

  const selectedConfigTemplate = useMemo(
    () => (cfgSelectedTemplateId !== '' ? menuTemplates.find((t) => t.id === Number(cfgSelectedTemplateId)) ?? null : null),
    [cfgSelectedTemplateId, menuTemplates]
  );

  const handleSaveConfig = async () => {
    if (!cfgCutoffTime || cfgCutoffTime < '06:00' || cfgCutoffTime > '11:00') {
      toast.error('Giờ chốt suất ăn phải nằm trong khoảng 06:00 đến 11:00 (trước giờ ăn trưa).');
      return;
    }
    if (cfgRemarks.trim().length > 250) {
      toast.error('Ghi chú không được vượt quá 250 ký tự.');
      return;
    }

    try {
      setActionLoading(true);
      if (cfgApplyFullWeek) {
        const weekDates = getWeekMonToSatOfDate(selectedDate);
        const monday = weekDates[0];
        const saturday = weekDates[5];

        if (cfgIsServing && cfgSelectedTemplateId !== '') {
          const applyRes = await lunchApi.applyMenuTemplate(Number(cfgSelectedTemplateId), {
            fromDate: monday,
            toDate: saturday,
            cutoffTime: cfgCutoffTime,
            overwriteExistingMenus: true,
          });
          const r = applyRes.data;
          const summaryParts = [`Đã áp dụng thực đơn tuần cho ${r.appliedDates.length} ngày học`];
          if (r.skippedLockedDates.length > 0) {
            summaryParts.push(`bỏ qua ${r.skippedLockedDates.length} ngày đã khóa`);
          }
          const summaryText = summaryParts.join(' • ');
          toast.success(summaryText);
          setMessage({ text: summaryText, type: 'success' });
        } else {
          const existingWeek = await lunchApi.getServiceDays(monday, saturday);
          const targetDates = existingWeek
            .filter((d) => !d.isLocked && (cfgIsServing ? d.isServing : getIsoDayOfWeek(d.date) <= 5 || d.isServing))
            .map((d) => d.date.slice(0, 10));

          if (targetDates.length === 0) {
            toast.error('Không có ngày chưa khóa nào trong tuần để cập nhật.');
            return;
          }

          await lunchApi.upsertServiceDays({
            dates: targetDates,
            isServing: cfgIsServing,
            cutoffTime: cfgCutoffTime,
            remarks: cfgIsServing ? undefined : (cfgRemarks.trim() || undefined),
          });
          toast.success(`Đã cập nhật cấu hình cho ${targetDates.length} ngày trong tuần!`);
          setMessage({ text: `Đã cập nhật cấu hình cho ${targetDates.length} ngày trong tuần.`, type: 'success' });
        }
      } else {
        const useNewTemplate = cfgIsServing && cfgSelectedTemplateId !== '';
        const res = await lunchApi.upsertServiceDays({
          dates: [selectedDate],
          isServing: cfgIsServing,
          cutoffTime: cfgCutoffTime,
          remarks: useNewTemplate ? undefined : (cfgRemarks.trim() || undefined),
          menuTemplateId: useNewTemplate ? Number(cfgSelectedTemplateId) : undefined,
        });
        const msg = (res.code && getSystemMessage(res.code)?.content) || res.message || SYSTEM_MESSAGES.MSG44.content;
        toast.success(msg);
        setMessage({ text: `${msg} (Ngày ${formatDateDDMMYYYY(selectedDate)})`, type: 'success' });
      }
      setShowConfigModal(false);
      await fetchBoard(selectedDate);
    } catch (err: unknown) {
      const apiErr = (err as { response?: { data?: { code?: string; message?: string; errors?: string[] } } })?.response?.data;
      const sysMsg = apiErr?.code ? getSystemMessage(apiErr.code) : undefined;
      const errText = apiErr?.errors?.[0] || sysMsg?.content || apiErr?.message || 'Lưu cấu hình ngày phục vụ thất bại.';
      toast.error(errText);
    } finally {
      setActionLoading(false);
    }
  };

  const handleLockDay = async () => {
    try {
      setActionLoading(true);
      await lunchApi.lockDailyMeal({
        date: selectedDate,
        notes: lockNotes,
      });
      setMessage({
        text: `Đã chốt số liệu suất ăn gửi nhà bếp thành công (Mốc ${effectiveCutoffLabel})!`,
        type: 'success',
      });
      setShowLockModal(false);
      await fetchBoard(selectedDate);
    } catch (err: unknown) {
      const apiErr = (err as { response?: { data?: { message?: string; errors?: string[] } } })?.response?.data;
      const errorMsg = apiErr?.errors?.[0] || apiErr?.message || 'Chốt suất ăn thất bại.';
      toast.error(errorMsg);
      setMessage({ text: errorMsg, type: 'error' });
    } finally {
      setActionLoading(false);
    }
  };

  const handleUnlockDay = async () => {
    if (!unlockReason.trim()) {
      toast.error('Bắt buộc phải nhập lý do mở khóa ngày bán trú.');
      return;
    }
    try {
      setActionLoading(true);
      await lunchApi.unlockDailyMeal({
        date: selectedDate,
        reason: unlockReason.trim(),
      });
      toast.success(`Đã mở khóa ngày bán trú ${formatDateDDMMYYYY(selectedDate)} thành công.`);
      setMessage({
        text: `Đã mở khóa ngày bán trú ${formatDateDDMMYYYY(selectedDate)} thành công.`,
        type: 'success',
      });
      setShowUnlockModal(false);
      setUnlockReason('');
      await fetchBoard(selectedDate);
    } catch (err: unknown) {
      const apiErr = (err as { response?: { data?: { message?: string; errors?: string[] } } })?.response?.data;
      toast.error(apiErr?.errors?.[0] || apiErr?.message || 'Mở khóa ngày bán trú thất bại.');
    } finally {
      setActionLoading(false);
    }
  };

  return (
    <div className="container pop-in" style={{ paddingBottom: 60 }}>
      {/* Page Header */}
      <header className="page-header" style={{ marginBottom: 20 }}>
        <div style={{ display: 'flex', justifyContent: 'space-between', alignItems: 'flex-start', flexWrap: 'wrap', gap: 16 }}>
          <div>
            <div style={{ display: 'flex', gap: 8, alignItems: 'center', marginBottom: 8, flexWrap: 'wrap' }}>
              <span className="badge badge--mint">
                <Soup size={13} /> Tổ Bán Trú & Bếp Ăn
              </span>
              {board && !board.canManage && (
                <span className="badge badge--violet">Chế độ Xem báo cáo</span>
              )}
            </div>
            <h1 style={{ margin: '4px 0 8px' }}>
              Báo Cáo <span className="squiggle">Suất Ăn Bán Trú</span>
            </h1>
            <p style={{ color: 'var(--color-muted-fg)', margin: 0, fontSize: 'var(--fs-base)' }}>
              Theo dõi biến động và chốt khẩu phần nhà bếp hàng ngày theo mốc cắt cơm {effectiveCutoffLabel}.
            </p>
          </div>

          {/* Controls: Capsule Date Picker & Tactile Action Buttons (chỉ áp dụng cho Tab 1-3 theo ngày) */}
          {activeTab !== 'report' && (
          <div className="mobile-header-actions">
            <DatePickerPill
              value={selectedDate}
              onChange={setSelectedDate}
              ariaLabel="Chọn ngày xem suất ăn"
            />

            <button
              type="button"
              className="btn"
              onClick={() => fetchBoard(selectedDate)}
              title="Làm mới dữ liệu"
              disabled={loading}
              style={{
                display: 'inline-flex',
                alignItems: 'center',
                gap: 8,
                borderRadius: 'var(--radius-full)',
                border: '2px solid var(--color-fg)',
                boxShadow: '3px 3px 0 0 var(--color-fg)',
                background: '#FFFFFF',
                color: 'var(--color-fg)',
                fontWeight: 800,
                fontSize: 'var(--fs-sm)',
                padding: '0 18px',
                height: 42,
                boxSizing: 'border-box',
                cursor: loading ? 'not-allowed' : 'pointer',
              }}
            >
              <RefreshCw size={15} className={loading ? 'spin' : ''} />
              Làm mới
            </button>

            {board?.canManage && (
              <>
                <button
                  type="button"
                  className="btn"
                  onClick={() => setShowNotifyWindowModal(true)}
                  title="Gửi thông báo kỳ đăng ký bán trú 25–28 cuối tháng tới toàn bộ Phụ huynh"
                  style={{
                    display: 'inline-flex',
                    alignItems: 'center',
                    gap: 8,
                    borderRadius: 'var(--radius-full)',
                    border: '2px solid var(--color-fg)',
                    boxShadow: '3px 3px 0 0 var(--color-fg)',
                    background: '#FEF3C7',
                    color: 'var(--color-fg)',
                    fontWeight: 800,
                    fontSize: 'var(--fs-sm)',
                    padding: '0 16px',
                    height: 42,
                    boxSizing: 'border-box',
                    cursor: 'pointer',
                  }}
                >
                  <Bell size={15} />
                  Thông báo ĐK tháng (25–28)
                </button>
                <button
                  type="button"
                  className="btn"
                  onClick={() => setShowDishLibrary(true)}
                  title="Quản lý Thư viện món ăn & Nhãn dị ứng"
                  style={{
                    display: 'inline-flex',
                    alignItems: 'center',
                    gap: 8,
                    borderRadius: 'var(--radius-full)',
                    border: '2px solid var(--color-fg)',
                    boxShadow: '3px 3px 0 0 var(--color-fg)',
                    background: '#FFFFFF',
                    color: 'var(--color-fg)',
                    fontWeight: 800,
                    fontSize: 'var(--fs-sm)',
                    padding: '0 16px',
                    height: 42,
                    boxSizing: 'border-box',
                    cursor: 'pointer',
                  }}
                >
                  <BookOpen size={15} />
                  Thư viện món
                </button>
                <button
                  type="button"
                  className="btn"
                  onClick={() => setShowTemplateModal(true)}
                  title="Thiết kế & Áp dụng Thực đơn tuần"
                  style={{
                    display: 'inline-flex',
                    alignItems: 'center',
                    gap: 8,
                    borderRadius: 'var(--radius-full)',
                    border: '2px solid var(--color-fg)',
                    boxShadow: '3px 3px 0 0 var(--color-fg)',
                    background: '#EDE9FE',
                    color: 'var(--color-fg)',
                    fontWeight: 800,
                    fontSize: 'var(--fs-sm)',
                    padding: '0 16px',
                    height: 42,
                    boxSizing: 'border-box',
                    cursor: 'pointer',
                  }}
                >
                  <Sparkles size={15} />
                  Thực đơn tuần
                </button>
                <button
                  type="button"
                  className="btn"
                  onClick={() => setShowConfigModal(true)}
                  title="Cấu hình mở/đóng phục vụ & thực đơn ngày"
                  style={{
                    display: 'inline-flex',
                    alignItems: 'center',
                    gap: 8,
                    borderRadius: 'var(--radius-full)',
                    border: '2px solid var(--color-fg)',
                    boxShadow: '3px 3px 0 0 var(--color-fg)',
                    background: '#FEF9C3',
                    color: 'var(--color-fg)',
                    fontWeight: 800,
                    fontSize: 'var(--fs-sm)',
                    padding: '0 18px',
                    height: 42,
                    boxSizing: 'border-box',
                    cursor: 'pointer',
                  }}
                >
                  <Settings size={15} />
                  Cấu hình ngày
                </button>
              </>
            )}

            {board?.canManage && board.isServing && !board.isLocked ? (
              <button
                type="button"
                className="btn"
                disabled={isFutureSelectedDate}
                title={isFutureSelectedDate ? 'Chỉ được chốt suất cho ngày hôm nay hoặc ngày đã qua' : undefined}
                onClick={() => setShowLockModal(true)}
                style={{
                  borderRadius: 'var(--radius-full)',
                  border: '2px solid var(--color-fg)',
                  boxShadow: isFutureSelectedDate ? 'none' : '3px 3px 0 0 var(--color-fg)',
                  fontWeight: 800,
                  fontSize: 'var(--fs-sm)',
                  padding: '0 22px',
                  height: 42,
                  boxSizing: 'border-box',
                  background: isFutureSelectedDate ? 'var(--color-muted)' : 'var(--color-accent)',
                  color: isFutureSelectedDate ? 'var(--color-muted-fg)' : '#FFFFFF',
                  display: 'inline-flex',
                  alignItems: 'center',
                  gap: 8,
                  cursor: isFutureSelectedDate ? 'not-allowed' : 'pointer',
                }}
              >
                <Lock size={15} />
                Chốt Suất ({effectiveCutoffLabel})
              </button>
            ) : board?.canManage && board.isServing && board.isLocked ? (
              <div
                style={{
                  borderRadius: 'var(--radius-full)',
                  border: '2px solid var(--color-fg)',
                  boxShadow: '3px 3px 0 0 var(--color-fg)',
                  fontWeight: 800,
                  fontSize: 'var(--fs-sm)',
                  padding: '0 20px',
                  height: 42,
                  boxSizing: 'border-box',
                  background: '#D1FAE5',
                  color: '#065F46',
                  display: 'inline-flex',
                  alignItems: 'center',
                  gap: 8,
                }}
                title={board.lockedByName ? `Đã chốt bởi ${board.lockedByName}` : 'Bảng số liệu đã được chốt và gửi sang nhà bếp'}
              >
                <CheckCircle2 size={16} />
                Đã chốt suất ({effectiveCutoffLabel}){board.lockedByName ? ` • ${board.lockedByName}` : ''}
              </div>
            ) : null}
            {board && board.isLocked && board.canManage && (
              <button
                type="button"
                className="btn btn--secondary"
                onClick={() => {
                  setUnlockReason('');
                  setShowUnlockModal(true);
                }}
                style={{
                  borderRadius: 'var(--radius-full)',
                  border: '2px solid var(--color-fg)',
                  boxShadow: '3px 3px 0 0 var(--color-fg)',
                  fontWeight: 800,
                  fontSize: 'var(--fs-sm)',
                  padding: '0 16px',
                  height: 42,
                  background: '#FEF3C7',
                  color: '#92400E',
                }}
              >
                Mở khóa ngày
              </button>
            )}
          </div>
          )}
        </div>
      </header>

      {/* Global alert toast - Clean, un-cluttered notice */}
      {message && (
        <div
          style={{
            marginBottom: 20,
            background: message.type === 'success' ? '#ECFDF5' : '#FFF1F2',
            border: `2px solid ${message.type === 'success' ? 'var(--color-quaternary)' : 'var(--color-secondary)'}`,
            borderRadius: 'var(--radius-md)',
            boxShadow: '3px 3px 0 0 var(--color-fg)',
            padding: '12px 18px',
            display: 'flex',
            alignItems: 'center',
            justifyContent: 'space-between',
            animation: 'pop-in 0.2s ease',
          }}
        >
          <div style={{ display: 'flex', alignItems: 'center', gap: 10, fontWeight: 700 }}>
            {message.type === 'success' ? (
              <CheckCircle2 size={18} color="#047857" />
            ) : (
              <ShieldAlert size={18} color="#e11d48" />
            )}
            <span style={{ color: message.type === 'success' ? '#065f46' : '#9f1239' }}>{message.text}</span>
          </div>
          <button
            type="button"
            className="btn btn--secondary btn--sm"
            onClick={() => setMessage(null)}
            style={{ borderRadius: 'var(--radius-full)' }}
          >
            Đóng
          </button>
        </div>
      )}

      {/* TAB NAVIGATION PILLS - Clean, Sleek, Unified */}
      <div style={{ marginBottom: 24 }}>
        <div
          className="responsive-tabs"
          style={{
            gap: 6,
            background: 'var(--color-muted)',
            padding: '4px',
            borderRadius: 'var(--radius-full)',
            border: '2px solid var(--color-fg)',
            boxShadow: '3px 3px 0 0 var(--color-fg)',
          }}
        >
          <button
            type="button"
            onClick={() => handleTabChange('summary')}
            style={{
              display: 'inline-flex',
              alignItems: 'center',
              gap: 8,
              padding: '8px 18px',
              borderRadius: 'var(--radius-full)',
              border: activeTab === 'summary' ? '2px solid var(--color-fg)' : '2px solid transparent',
              background: activeTab === 'summary' ? 'var(--color-accent)' : 'transparent',
              color: activeTab === 'summary' ? '#FFFFFF' : 'var(--color-fg)',
              fontWeight: 800,
              fontSize: 'var(--fs-sm)',
              cursor: 'pointer',
              boxShadow: activeTab === 'summary' ? '2px 2px 0 0 var(--color-fg)' : 'none',
              transition: 'all 0.15s ease',
            }}
          >
            <FileSpreadsheet size={15} />
            1. Tổng Hợp & Chốt Suất
          </button>

          <button
            type="button"
            onClick={() => handleTabChange('students')}
            style={{
              display: 'inline-flex',
              alignItems: 'center',
              gap: 8,
              padding: '8px 18px',
              borderRadius: 'var(--radius-full)',
              border: activeTab === 'students' ? '2px solid var(--color-fg)' : '2px solid transparent',
              background: activeTab === 'students' ? 'var(--color-accent)' : 'transparent',
              color: activeTab === 'students' ? '#FFFFFF' : 'var(--color-fg)',
              fontWeight: 800,
              fontSize: 'var(--fs-sm)',
              cursor: 'pointer',
              boxShadow: activeTab === 'students' ? '2px 2px 0 0 var(--color-fg)' : 'none',
              transition: 'all 0.15s ease',
            }}
          >
            <Users size={15} />
            2. Danh Sách Học Sinh ({totalCount})
          </button>

          <button
            type="button"
            onClick={() => handleTabChange('logs')}
            style={{
              display: 'inline-flex',
              alignItems: 'center',
              gap: 8,
              padding: '8px 18px',
              borderRadius: 'var(--radius-full)',
              border: activeTab === 'logs' ? '2px solid var(--color-fg)' : '2px solid transparent',
              background: activeTab === 'logs' ? 'var(--color-accent)' : 'transparent',
              color: activeTab === 'logs' ? '#FFFFFF' : 'var(--color-fg)',
              fontWeight: 800,
              fontSize: 'var(--fs-sm)',
              cursor: 'pointer',
              boxShadow: activeTab === 'logs' ? '2px 2px 0 0 var(--color-fg)' : 'none',
              transition: 'all 0.15s ease',
            }}
          >
            <Clock size={15} />
            3. Nhật Ký Biến Động ({board?.mealChangeLogs?.length || 0})
          </button>

          <button
            type="button"
            onClick={() => handleTabChange('report')}
            style={{
              display: 'inline-flex',
              alignItems: 'center',
              gap: 8,
              padding: '8px 18px',
              borderRadius: 'var(--radius-full)',
              border: activeTab === 'report' ? '2px solid var(--color-fg)' : '2px solid transparent',
              background: activeTab === 'report' ? 'var(--color-accent)' : 'transparent',
              color: activeTab === 'report' ? '#FFFFFF' : 'var(--color-fg)',
              fontWeight: 800,
              fontSize: 'var(--fs-sm)',
              cursor: 'pointer',
              boxShadow: activeTab === 'report' ? '2px 2px 0 0 var(--color-fg)' : 'none',
              transition: 'all 0.15s ease',
            }}
          >
            <CalendarRange size={15} />
            4. Báo Cáo Thống Kê
          </button>
        </div>
      </div>

      {/* ===================================================================== */}
      {/* MÀN HÌNH 1: TỔNG HỢP & CHỐT SUẤT BÁN TRÚ                               */}
      {/* ===================================================================== */}
      {activeTab === 'summary' && (
        <div className="pop-in">
          {/* Banner Cutoff Time & Menu (Clean Warm Cream Paper) */}
          {board && (
            <div
              style={{
                marginBottom: 24,
                display: 'flex',
                alignItems: 'center',
                justifyContent: 'space-between',
                flexWrap: 'wrap',
                gap: 16,
                background: '#FFFDF5',
                border: '2px solid var(--color-fg)',
                boxShadow: '3px 3px 0 0 var(--color-fg)',
                borderRadius: 'var(--radius-lg)',
                padding: '16px 20px',
              }}
            >
              <div style={{ display: 'flex', alignItems: 'center', gap: 14 }}>
                <div
                  style={{
                    width: 44,
                    height: 44,
                    borderRadius: '50%',
                    background: !board.isServing
                      ? 'var(--color-secondary)'
                      : board.isLocked
                        ? 'var(--color-quaternary)'
                        : 'var(--color-tertiary)',
                    border: '2px solid var(--color-fg)',
                    boxShadow: '2px 2px 0 0 var(--color-fg)',
                    display: 'grid',
                    placeItems: 'center',
                    flexShrink: 0,
                  }}
                >
                  <Clock size={22} color="var(--color-fg)" />
                </div>
                <div>
                  <div style={{ display: 'flex', alignItems: 'center', gap: 8, flexWrap: 'wrap' }}>
                    <h3 style={{ margin: 0, fontSize: 'var(--fs-base)', fontFamily: 'var(--font-heading)' }}>
                      Quy tắc chốt ăn Tân Lập A: {effectiveCutoffLabel}
                    </h3>
                    <span
                      className={`badge ${
                        !board.isServing ? 'badge--pink' : board.isLocked ? 'badge--mint' : 'badge--amber'
                      }`}
                    >
                      {!board.isServing
                        ? 'Đóng dịch vụ ngày'
                        : board.isLocked
                          ? `Đã khóa số liệu (${effectiveCutoffLabel})`
                          : `Hạn hủy suất: ${effectiveCutoffLabel}`}
                    </span>
                  </div>
                  <p style={{ margin: '4px 0 0', fontSize: 'var(--fs-sm)', color: 'var(--color-muted-fg)' }}>
                    {board.serviceRemarks ? (
                      <>
                        <strong style={{ color: 'var(--color-fg)' }}>
                          {(board.date?.slice(0, 10) || selectedDate) === todayStr
                            ? 'Thực đơn trưa hôm nay:'
                            : `Thực đơn trưa ngày ${formatDateDDMMYYYY(board.date?.slice(0, 10) || selectedDate)}:`}
                        </strong>{' '}
                        {board.serviceRemarks}
                      </>
                    ) : (
                      `Đơn xin nghỉ học có bữa trưa được GVCN duyệt trước ${effectiveCutoffLabel} sẽ tự động giảm trừ suất ăn.`
                    )}
                  </p>
                </div>
              </div>

              <div style={{ display: 'flex', gap: 10 }}>
                <button
                  type="button"
                  className="btn btn--secondary btn--sm"
                  onClick={() => handleTabChange('students')}
                  style={{ borderRadius: 'var(--radius-full)' }}
                >
                  <Users size={14} />
                  Xem danh sách học sinh
                  <ArrowRight size={14} />
                </button>
              </div>
            </div>
          )}

          {/* 4 Live KPI Cards - Harmonious, Clean, Balanced Grid */}
          {board && (
            <div
              className="mobile-stat-grid"
              style={{
                marginBottom: 28,
              }}
            >
              {/* Card 1: Registered Total */}
              <div
                className="card"
                style={{
                  background: 'var(--color-card)',
                  border: '2px solid var(--color-fg)',
                  boxShadow: '4px 4px 0 0 var(--color-fg)',
                  borderRadius: 'var(--radius-lg)',
                  padding: '20px 22px',
                  display: 'flex',
                  flexDirection: 'column',
                  gap: 10,
                }}
              >
                <div style={{ display: 'flex', alignItems: 'center', justifyContent: 'space-between' }}>
                  <div className="label" style={{ marginBottom: 0, fontSize: 'var(--fs-xs)', letterSpacing: '0.04em' }}>
                    TỔNG ĐĂNG KÝ ĐẦU NGÀY
                  </div>
                  <div
                    style={{
                      width: 34,
                      height: 34,
                      borderRadius: '50%',
                      background: 'var(--color-tertiary)',
                      border: '1.5px solid var(--color-fg)',
                      display: 'grid',
                      placeItems: 'center',
                    }}
                  >
                    <UtensilsCrossed size={16} color="var(--color-fg)" />
                  </div>
                </div>

                <div style={{ fontSize: 'var(--fs-2xl)', fontWeight: 800, color: 'var(--color-fg)', fontFamily: 'var(--font-heading)' }}>
                  {board.totalInitialRegistered}{' '}
                  <span style={{ fontSize: 'var(--fs-sm)', fontWeight: 500, color: 'var(--color-muted-fg)' }}>suất</span>
                </div>

                <p style={{ margin: 0, fontSize: 'var(--fs-xs)', color: 'var(--color-muted-fg)' }}>
                  Đăng ký bán trú chính thức trong ngày
                </p>
              </div>

              {/* Card 2: Cancelled */}
              <div
                className="card"
                style={{
                  background: 'var(--color-card)',
                  border: '2px solid var(--color-fg)',
                  boxShadow: '4px 4px 0 0 var(--color-fg)',
                  borderRadius: 'var(--radius-lg)',
                  padding: '20px 22px',
                  display: 'flex',
                  flexDirection: 'column',
                  gap: 10,
                }}
              >
                <div style={{ display: 'flex', alignItems: 'center', justifyContent: 'space-between' }}>
                  <div className="label" style={{ marginBottom: 0, fontSize: 'var(--fs-xs)', letterSpacing: '0.04em', color: '#9F1239' }}>
                    ĐÃ HỦY TRƯỚC {effectiveCutoffLabel}
                  </div>
                  <div
                    style={{
                      width: 34,
                      height: 34,
                      borderRadius: '50%',
                      background: '#FFE4E6',
                      border: '1.5px solid var(--color-fg)',
                      display: 'grid',
                      placeItems: 'center',
                    }}
                  >
                    <UserMinus size={16} color="#E11D48" />
                  </div>
                </div>

                <div style={{ fontSize: 'var(--fs-2xl)', fontWeight: 800, color: '#E11D48', fontFamily: 'var(--font-heading)' }}>
                  {board.totalAutoCancelled > 0 ? `-${board.totalAutoCancelled}` : '0'}{' '}
                  <span style={{ fontSize: 'var(--fs-sm)', fontWeight: 500, color: 'var(--color-muted-fg)' }}>suất</span>
                </div>

                <p style={{ margin: 0, fontSize: 'var(--fs-xs)', color: '#E11D48' }}>
                  Hủy từ đơn xin nghỉ & phụ huynh báo hủy
                </p>
              </div>

              {/* Card 3: Net Active To Cook - HERO METRIC CARD */}
              <div
                className="card"
                style={{
                  background: '#ECFDF5',
                  border: '2.5px solid #059669',
                  boxShadow: '4px 4px 0 0 var(--color-fg)',
                  borderRadius: 'var(--radius-lg)',
                  padding: '20px 22px',
                  display: 'flex',
                  flexDirection: 'column',
                  gap: 10,
                }}
              >
                <div style={{ display: 'flex', alignItems: 'center', justifyContent: 'space-between' }}>
                  <div className="label" style={{ marginBottom: 0, fontSize: 'var(--fs-xs)', letterSpacing: '0.04em', color: '#065F46', fontWeight: 800 }}>
                    THỰC NẤU (BÀN GIAO BẾP)
                  </div>
                  <div
                    style={{
                      width: 34,
                      height: 34,
                      borderRadius: '50%',
                      background: 'var(--color-quaternary)',
                      border: '1.5px solid var(--color-fg)',
                      display: 'grid',
                      placeItems: 'center',
                    }}
                  >
                    <Soup size={18} color="var(--color-fg)" />
                  </div>
                </div>

                <div style={{ fontSize: 'var(--fs-2xl)', fontWeight: 900, color: '#065F46', fontFamily: 'var(--font-heading)' }}>
                  {board.totalActiveToCook}{' '}
                  <span style={{ fontSize: 'var(--fs-md)', fontWeight: 700, color: '#047857' }}>khẩu phần</span>
                </div>

                <div style={{ display: 'flex', gap: 6, flexWrap: 'wrap' }}>
                  <span className="badge badge--mint" style={{ border: '1px solid var(--color-fg)', fontWeight: 800, fontSize: 11 }}>
                    {mealBreakdownStats.standard} Tiêu chuẩn
                  </span>
                  <span className="badge badge--amber" style={{ border: '1px solid var(--color-fg)', fontWeight: 800, fontSize: 11 }}>
                    {mealBreakdownStats.vegetarian} Chay
                  </span>
                  <span className="badge badge--pink" style={{ border: '1px solid var(--color-fg)', fontWeight: 800, fontSize: 11 }}>
                    {mealBreakdownStats.special} Đặc biệt
                  </span>
                </div>
              </div>

              {/* Card 4: Anomalies */}
              <div
                className="card"
                style={{
                  background: 'var(--color-card)',
                  border: '2px solid var(--color-fg)',
                  boxShadow: '4px 4px 0 0 var(--color-fg)',
                  borderRadius: 'var(--radius-lg)',
                  padding: '20px 22px',
                  display: 'flex',
                  flexDirection: 'column',
                  gap: 10,
                }}
              >
                <div style={{ display: 'flex', alignItems: 'center', justifyContent: 'space-between' }}>
                  <div className="label" style={{ marginBottom: 0, fontSize: 'var(--fs-xs)', letterSpacing: '0.04em', color: board.totalAnomalies > 0 ? 'var(--color-accent)' : 'var(--color-muted-fg)' }}>
                    PHÁT SINH CẦN ĐỐI SOÁT
                  </div>
                  <div
                    style={{
                      width: 34,
                      height: 34,
                      borderRadius: '50%',
                      background: '#EDE9FE',
                      border: '1.5px solid var(--color-fg)',
                      display: 'grid',
                      placeItems: 'center',
                    }}
                  >
                    <ShieldAlert size={16} color="var(--color-accent)" />
                  </div>
                </div>

                <div
                  style={{
                    fontSize: 'var(--fs-2xl)',
                    fontWeight: 800,
                    color: board.totalAnomalies > 0 ? 'var(--color-accent)' : 'var(--color-fg)',
                    fontFamily: 'var(--font-heading)',
                  }}
                >
                  {board.totalAnomalies}{' '}
                  <span style={{ fontSize: 'var(--fs-sm)', fontWeight: 500, color: 'var(--color-muted-fg)' }}>em</span>
                </div>

                <p style={{ margin: 0, fontSize: 'var(--fs-xs)', color: board.totalAnomalies > 0 ? 'var(--color-accent)' : 'var(--color-muted-fg)' }}>
                  Hủy suất ăn nhưng có mặt tại lớp
                </p>
              </div>
            </div>
          )}

          {/* Banner cảnh báo học sinh đang trùng món dị ứng chưa xử lý */}
          {board && mealBreakdownStats.unresolvedConflicts > 0 && (
            <div
              style={{
                marginBottom: 22,
                padding: '14px 18px',
                borderRadius: 'var(--radius-lg)',
                border: '2.5px solid #DC2626',
                background: '#FEF2F2',
                boxShadow: '3px 3px 0 0 #991B1B',
                display: 'flex',
                alignItems: 'center',
                justifyContent: 'space-between',
                flexWrap: 'wrap',
                gap: 12,
              }}
            >
              <div style={{ display: 'flex', alignItems: 'center', gap: 10 }}>
                <AlertTriangle size={22} color="#DC2626" style={{ flexShrink: 0 }} />
                <div>
                  <div style={{ fontWeight: 900, color: '#991B1B', fontSize: 'var(--fs-sm)' }}>
                    Phát hiện {mealBreakdownStats.unresolvedConflicts} học sinh có món ăn hôm nay trùng dị ứng thực phẩm!
                  </div>
                  <div style={{ fontSize: 'var(--fs-xs)', color: '#7F1D1D', marginTop: 2, fontWeight: 600 }}>
                    Vui lòng kiểm tra khay ăn và lưu ý tránh món trùng dị ứng khi chế biến/chia suất cho các bé trước khi chốt sổ nấu.
                  </div>
                </div>
              </div>
              <button
                type="button"
                className="btn btn--primary btn--sm"
                onClick={() => {
                  setSelectedClassFilter('ALL');
                  setStatusFilter('ACTIVE');
                  setDietFilter('CONFLICT_ONLY');
                  handleTabChange('students');
                }}
                style={{
                  background: '#DC2626',
                  color: '#FFFFFF',
                  border: '2px solid var(--color-fg)',
                  borderRadius: 'var(--radius-full)',
                  boxShadow: '2px 2px 0 0 var(--color-fg)',
                  fontWeight: 800,
                }}
              >
                Xem &amp; xử lý ngay ({mealBreakdownStats.unresolvedConflicts})
              </button>
            </div>
          )}

          {/* Class Breakdown Table */}
          <div
            className="card"
            style={{
              marginBottom: 28,
              border: '2px solid var(--color-fg)',
              boxShadow: '4px 4px 0 0 var(--color-fg)',
              borderRadius: 'var(--radius-lg)',
              padding: '24px',
            }}
          >
            <div style={{ display: 'flex', justifyContent: 'space-between', alignItems: 'center', marginBottom: 16, flexWrap: 'wrap', gap: 12 }}>
              <div>
                <h3 style={{ margin: 0, fontSize: 'var(--fs-md)', fontFamily: 'var(--font-heading)', display: 'flex', alignItems: 'center', gap: 8 }}>
                  <FileSpreadsheet size={18} color="var(--color-accent)" />
                  Tổng Hợp Khẩu Phần Theo Lớp Học
                </h3>
                <p style={{ margin: '4px 0 0', fontSize: 'var(--fs-xs)', color: 'var(--color-muted-fg)' }}>
                  Bấm vào hàng của một lớp để xem chi tiết danh sách học sinh và khay ăn riêng của lớp đó.
                </p>
              </div>
              <div style={{ display: 'flex', alignItems: 'center', gap: 10, flexWrap: 'wrap' }}>
                <span className="badge badge--mint" style={{ border: '1.5px solid var(--color-fg)', padding: '5px 12px', fontWeight: 700 }}>
                  {board?.classBreakdowns?.length || 0} lớp học • {board?.totalActiveToCook || 0} suất
                </span>
              </div>
            </div>

            <div style={{ overflowX: 'auto', border: '2px solid var(--color-fg)', borderRadius: 'var(--radius-md)' }}>
              <table style={{ width: '100%', borderCollapse: 'collapse', fontSize: 'var(--fs-sm)' }}>
                <thead>
                  <tr style={{ borderBottom: '2px solid var(--color-fg)', background: '#F8FAFC' }}>
                    <th style={{ padding: '12px 14px', textAlign: 'left', fontWeight: 800 }}>Lớp</th>
                    <th style={{ padding: '12px 14px', textAlign: 'center', fontWeight: 800 }}>Khối</th>
                    <th style={{ padding: '12px 14px', textAlign: 'center', fontWeight: 800 }}>Đăng ký</th>
                    <th style={{ padding: '12px 14px', textAlign: 'center', fontWeight: 800 }}>Đã hủy</th>
                    <th style={{ padding: '12px 14px', textAlign: 'center', fontWeight: 800 }}>Thực ăn</th>
                    <th style={{ padding: '12px 14px', textAlign: 'center', fontWeight: 800 }}>Mặn / Chay / ĐB</th>
                    <th style={{ padding: '12px 14px', textAlign: 'left', fontWeight: 800 }}>Tỷ lệ nhận</th>
                    <th style={{ padding: '12px 14px', textAlign: 'center', fontWeight: 800 }}>Thao tác</th>
                  </tr>
                </thead>
                <tbody>
                  {board?.classBreakdowns && board.classBreakdowns.length > 0 ? (
                    board.classBreakdowns.map((cls) => {
                      const percentage = cls.initialRegistered > 0
                        ? Math.round((cls.actualCount / cls.initialRegistered) * 100)
                        : 0;
                      const classActiveMeals = (board.studentMeals ?? []).filter(
                        (s) => s.classId === cls.classId && s.isActive
                      );
                      const stdCnt = cls.standardCount ?? classActiveMeals.filter((s) => s.mealType === 'StandardLunch' || s.mealType === 1).length;
                      const vegCnt = cls.vegetarianCount ?? classActiveMeals.filter((s) => s.mealType === 'VegetarianLunch' || s.mealType === 2).length;
                      const spcCnt = cls.specialDietCount ?? classActiveMeals.filter((s) => s.mealType === 'SpecialDiet' || s.mealType === 3).length;

                      return (
                        <tr
                          key={cls.classId}
                          style={{ borderBottom: '1px solid var(--color-border)', transition: 'background 0.15s', cursor: 'pointer' }}
                          onClick={() => setDetailClass(cls)}
                          onMouseEnter={(e) => (e.currentTarget.style.background = '#FFFDF5')}
                          onMouseLeave={(e) => (e.currentTarget.style.background = 'transparent')}
                        >
                          <td style={{ padding: '12px 14px', fontWeight: 800, color: 'var(--color-accent)' }}>
                            {formatClassLabel(cls.className, cls.classId)}
                          </td>
                          <td style={{ padding: '12px 14px', textAlign: 'center' }}>
                            {cls.grade > 0 ? `Khối ${cls.grade}` : '—'}
                          </td>
                          <td style={{ padding: '12px 14px', textAlign: 'center', fontWeight: 600 }}>
                            {cls.initialRegistered}
                          </td>
                          <td
                            style={{
                              padding: '12px 14px',
                              textAlign: 'center',
                              color: cls.cancelledCount > 0 ? '#E11D48' : 'var(--color-muted-fg)',
                              fontWeight: cls.cancelledCount > 0 ? 800 : 400,
                            }}
                          >
                            {cls.cancelledCount > 0 ? `-${cls.cancelledCount}` : '0'}
                          </td>
                          <td style={{ padding: '12px 14px', textAlign: 'center', fontWeight: 900, color: '#047857', fontSize: 'var(--fs-base)' }}>
                            {cls.actualCount}
                          </td>
                          <td style={{ padding: '12px 14px', textAlign: 'center', fontSize: 'var(--fs-xs)', fontWeight: 700 }}>
                            <span className="badge badge--mint" style={{ marginRight: 4, padding: '2px 6px' }} title="Suất tiêu chuẩn">{stdCnt} Mặn</span>
                            {vegCnt > 0 && <span className="badge badge--amber" style={{ marginRight: 4, padding: '2px 6px' }} title="Suất ăn chay">{vegCnt} Chay</span>}
                            {spcCnt > 0 && <span className="badge badge--pink" style={{ padding: '2px 6px' }} title="Chế độ đặc biệt">{spcCnt} ĐB</span>}
                          </td>
                          <td style={{ padding: '12px 14px' }}>
                            <div style={{ display: 'flex', alignItems: 'center', gap: 8 }}>
                              <div style={{ flex: 1, height: 10, background: 'var(--color-border)', borderRadius: 5, overflow: 'hidden', border: '1px solid var(--color-fg)' }}>
                                <div
                                  style={{
                                    width: `${percentage}%`,
                                    height: '100%',
                                    background: percentage === 100 ? 'var(--color-quaternary)' : 'var(--color-tertiary)',
                                  }}
                                />
                              </div>
                              <span style={{ fontSize: 'var(--fs-xs)', color: 'var(--color-fg)', minWidth: 36, fontWeight: 700 }}>
                                {percentage}%
                              </span>
                            </div>
                          </td>
                          <td style={{ padding: '12px 14px', textAlign: 'center' }} onClick={(e) => e.stopPropagation()}>
                            <div style={{ display: 'inline-flex', gap: 6 }}>
                              <button
                                type="button"
                                className="btn btn--primary btn--sm"
                                onClick={() => setDetailClass(cls)}
                                style={{
                                  borderRadius: 'var(--radius-full)',
                                  border: '1.5px solid var(--color-fg)',
                                  boxShadow: '2px 2px 0 0 var(--color-fg)',
                                  fontWeight: 800,
                                  fontSize: 'var(--fs-xs)',
                                  padding: '4px 12px',
                                }}
                              >
                                Chi tiết suất lớp
                              </button>
                              <button
                                type="button"
                                className="btn btn--secondary btn--sm"
                                onClick={() => {
                                  setSelectedClassFilter(String(cls.classId));
                                  handleTabChange('students');
                                }}
                                title={`Lọc danh sách học sinh lớp ${cls.className}`}
                                style={{
                                  borderRadius: 'var(--radius-full)',
                                  border: '1.5px solid var(--color-fg)',
                                  boxShadow: '2px 2px 0 0 var(--color-fg)',
                                  fontWeight: 700,
                                  fontSize: 'var(--fs-xs)',
                                  padding: '4px 10px',
                                }}
                              >
                                Tab HS →
                              </button>
                            </div>
                          </td>
                        </tr>
                      );
                    })
                  ) : (
                    <tr>
                      <td colSpan={7} style={{ textAlign: 'center', padding: '32px', color: 'var(--color-muted-fg)' }}>
                        Chưa có dữ liệu phân bổ cho ngày này.
                      </td>
                    </tr>
                  )}
                </tbody>
              </table>
            </div>
          </div>
        </div>
      )}

      {/* ===================================================================== */}
      {/* MÀN HÌNH 2: DANH SÁCH HỌC SINH NHẬN SUẤT + PAGINATION                 */}
      {/* ===================================================================== */}
      {activeTab === 'students' && (
        <div
          className="card pop-in"
          style={{
            border: '2px solid var(--color-fg)',
            boxShadow: '4px 4px 0 0 var(--color-fg)',
            borderRadius: 'var(--radius-lg)',
            padding: '24px',
          }}
        >
          {/* Header & Quick Kitchen Filters */}
          <div style={{ display: 'flex', justifyContent: 'space-between', alignItems: 'flex-start', flexWrap: 'wrap', gap: 16, marginBottom: 20 }}>
            <div>
              <h3 style={{ margin: 0, fontSize: 'var(--fs-md)', fontFamily: 'var(--font-heading)', display: 'flex', alignItems: 'center', gap: 8 }}>
                <Users size={18} color="var(--color-accent)" />
                Danh Sách Học Sinh Nhận Suất Ăn
              </h3>
              <p style={{ margin: '4px 0 0', fontSize: 'var(--fs-xs)', color: 'var(--color-muted-fg)' }}>
                Phục vụ nhân viên bán trú chuẩn bị khay riêng (Chay / Đặc biệt) và đối chiếu phát khay ăn theo từng lớp.
              </p>
            </div>

            <div style={{ display: 'flex', gap: 8, flexWrap: 'wrap' }}>
              <button
                type="button"
                className={dietFilter === 'ALL' ? 'btn btn--primary btn--sm' : 'btn btn--secondary btn--sm'}
                onClick={() => setDietFilter('ALL')}
                style={{ borderRadius: 'var(--radius-full)', fontWeight: 800 }}
              >
                Tất cả khẩu phần
              </button>
              <button
                type="button"
                className={dietFilter === 'SPECIAL_ALLERGY' ? 'btn btn--primary btn--sm' : 'btn btn--secondary btn--sm'}
                onClick={() => {
                  setStatusFilter('ACTIVE');
                  setDietFilter((prev) => (prev === 'SPECIAL_ALLERGY' ? 'ALL' : 'SPECIAL_ALLERGY'));
                }}
                style={{
                  borderRadius: 'var(--radius-full)',
                  fontWeight: 800,
                  background: dietFilter === 'SPECIAL_ALLERGY' ? 'var(--color-accent)' : '#FCE7F3',
                  color: dietFilter === 'SPECIAL_ALLERGY' ? '#FFFFFF' : '#9D174D',
                }}
              >
                Khay riêng &amp; Dị ứng ({mealBreakdownStats.specialOrAllergy})
              </button>
              {mealBreakdownStats.unresolvedConflicts > 0 && (
                <button
                  type="button"
                  className="btn btn--sm"
                  onClick={() => {
                    setStatusFilter('ACTIVE');
                    setDietFilter((prev) => (prev === 'CONFLICT_ONLY' ? 'ALL' : 'CONFLICT_ONLY'));
                  }}
                  style={{
                    borderRadius: 'var(--radius-full)',
                    border: '2px solid #DC2626',
                    fontWeight: 800,
                    background: dietFilter === 'CONFLICT_ONLY' ? '#DC2626' : '#FEE2E2',
                    color: dietFilter === 'CONFLICT_ONLY' ? '#FFFFFF' : '#991B1B',
                  }}
                >
                  Trùng món dị ứng ({mealBreakdownStats.unresolvedConflicts})
                </button>
              )}
            </div>
          </div>

          {/* Search & Filter Bar */}
          <div
            style={{
              display: 'flex',
              gap: 12,
              flexWrap: 'wrap',
              alignItems: 'center',
              padding: '14px 18px',
              background: '#FFFDF5',
              borderRadius: 'var(--radius-md)',
              marginBottom: 20,
              border: '2px solid var(--color-fg)',
              boxShadow: '2px 2px 0 0 var(--color-fg)',
            }}
          >
            {/* Search Input */}
            <div style={{ position: 'relative', flex: '1 1 200px', minWidth: 0 }}>
              <Search
                size={16}
                color="var(--color-muted-fg)"
                style={{ position: 'absolute', left: 12, top: '50%', transform: 'translateY(-50%)' }}
              />
              <input
                type="text"
                className="input"
                placeholder="Tìm tên hoặc mã học sinh..."
                value={searchQuery}
                onChange={(e) => setSearchQuery(e.target.value)}
                style={{
                  paddingLeft: 36,
                  fontSize: 'var(--fs-sm)',
                  borderRadius: 'var(--radius-full)',
                  border: '2px solid var(--color-fg)',
                }}
              />
            </div>

            {/* Class Filter */}
            <div style={{ display: 'flex', alignItems: 'center', gap: 6 }}>
              <Filter size={15} color="var(--color-muted-fg)" />
              <select
                className="select"
                value={selectedClassFilter}
                onChange={(e) => setSelectedClassFilter(e.target.value)}
                style={{
                  minWidth: 160,
                  padding: '6px 12px',
                  fontSize: 'var(--fs-sm)',
                  borderRadius: 'var(--radius-full)',
                  border: '2px solid var(--color-fg)',
                }}
              >
                <option value="ALL">Tất cả các lớp</option>
                {board?.classBreakdowns?.map((c) => (
                  <option key={c.classId} value={String(c.classId)}>
                    {c.grade > 0 ? `${formatClassLabel(c.className, c.classId)} (Khối ${c.grade})` : formatClassLabel(c.className, c.classId)}
                  </option>
                ))}
              </select>
            </div>

            {/* Status Filter */}
            <select
              className="select"
              value={statusFilter}
              onChange={(e) => setStatusFilter(e.target.value as 'ALL' | 'ACTIVE' | 'CANCELLED')}
              style={{
                minWidth: 190,
                padding: '6px 12px',
                fontSize: 'var(--fs-sm)',
                borderRadius: 'var(--radius-full)',
                border: '2px solid var(--color-fg)',
              }}
            >
              <option value="ACTIVE">Đang còn hiệu lực (Thực ăn)</option>
              <option value="CANCELLED">Đã hủy trong ngày</option>
              <option value="ALL">Tất cả bản ghi đăng ký</option>
            </select>

            {/* Page Size Select */}
            <select
              className="select"
              value={pageSize}
              onChange={(e) => setPageSize(Number(e.target.value))}
              style={{
                minWidth: 120,
                padding: '6px 12px',
                fontSize: 'var(--fs-sm)',
                borderRadius: 'var(--radius-full)',
                border: '2px solid var(--color-fg)',
              }}
            >
              <option value={10}>10 dòng / trang</option>
              <option value={20}>20 dòng / trang</option>
              <option value={50}>50 dòng / trang</option>
            </select>
          </div>

          {/* Quick status counters */}
          <div style={{ display: 'flex', gap: 8, marginBottom: 16, alignItems: 'center', flexWrap: 'wrap' }}>
            <span className="badge badge--violet" style={{ border: '1.5px solid var(--color-fg)' }}>
              Tổng tìm thấy: <strong>{totalCount}</strong> học sinh
            </span>
            <span className="badge badge--mint" style={{ border: '1.5px solid var(--color-fg)' }}>
              Thực ăn: <strong>{filteredStudentMeals.filter((s) => s.isActive).length}</strong>
            </span>
            <span className="badge badge--pink" style={{ border: '1.5px solid var(--color-fg)' }}>
              Đã hủy: <strong>{filteredStudentMeals.filter((s) => !s.isActive).length}</strong>
            </span>
            {searchQuery && (
              <span className="badge badge--amber" style={{ border: '1.5px solid var(--color-fg)' }}>
                Từ khóa: &quot;{searchQuery}&quot;
              </span>
            )}
          </div>

          {/* Student Table (Desktop) & Cards (Mobile) */}
          <div style={{ border: '2px solid var(--color-fg)', borderRadius: 'var(--radius-md)', overflow: 'hidden', background: '#FFFFFF' }}>
            <div className="desktop-only-table" style={{ overflowX: 'auto' }}>
            <table style={{ width: '100%', borderCollapse: 'collapse', fontSize: 'var(--fs-sm)' }}>
              <thead>
                <tr style={{ borderBottom: '2px solid var(--color-fg)', background: '#F8FAFC' }}>
                  <th style={{ padding: '12px 14px', textAlign: 'center', width: 56, fontWeight: 800 }}>STT</th>
                  <th style={{ padding: '12px 14px', textAlign: 'left', fontWeight: 800 }}>Mã HS</th>
                  <th style={{ padding: '12px 14px', textAlign: 'left', fontWeight: 800 }}>Họ và tên học sinh</th>
                  <th style={{ padding: '12px 14px', textAlign: 'center', fontWeight: 800 }}>Lớp</th>
                  <th style={{ padding: '12px 14px', textAlign: 'center', fontWeight: 800 }}>Chế độ ăn</th>
                  <th style={{ padding: '12px 14px', textAlign: 'center', fontWeight: 800 }}>Khay ăn</th>
                  <th style={{ padding: '12px 14px', textAlign: 'center', fontWeight: 800 }}>Trạng thái</th>
                  <th style={{ padding: '12px 14px', textAlign: 'left', fontWeight: 800 }}>Ghi chú / Lý do hủy</th>
                </tr>
              </thead>
              <tbody>
                {pagedStudents.length > 0 ? (
                  pagedStudents.map((item, idx) => {
                    return (
                    <tr
                      key={item.registrationId}
                      style={{ borderBottom: '1px solid var(--color-border)', transition: 'background 0.15s' }}
                      onMouseEnter={(e) => (e.currentTarget.style.background = '#FFFDF5')}
                      onMouseLeave={(e) => (e.currentTarget.style.background = 'transparent')}
                    >
                      <td style={{ padding: '10px 14px', textAlign: 'center', fontWeight: 600 }}>
                        {(pageIndex - 1) * pageSize + idx + 1}
                      </td>
                      <td style={{ padding: '10px 14px', fontWeight: 800, color: 'var(--color-accent)' }}>
                        {item.studentCode}
                      </td>
                      <td style={{ padding: '10px 14px', fontWeight: 700 }}>{item.studentName}</td>
                      <td style={{ padding: '10px 14px', textAlign: 'center' }}>{formatClassLabel(item.className, item.classId)}</td>
                      <td style={{ padding: '10px 14px', textAlign: 'center' }}>
                        <span className={`badge ${getMealTypeBadgeClass(item.mealType)}`} style={{ border: '1px solid var(--color-fg)', fontWeight: 800 }}>
                          {MEAL_TYPE_TEXT[String(item.mealType)] ?? 'Tiêu chuẩn'}
                        </span>
                      </td>
                      <td style={{ padding: '10px 14px', textAlign: 'center' }}>
                        {item.isActive ? (
                          <button
                            type="button"
                            onClick={() => setSelectedStudentTray(item)}
                            style={{
                              display: 'inline-flex',
                              alignItems: 'center',
                              gap: 5,
                              padding: '5px 12px',
                              borderRadius: 'var(--radius-full)',
                              border: '1.5px solid var(--color-fg)',
                              background: '#FFFDF5',
                              boxShadow: '2px 2px 0 0 var(--color-fg)',
                              fontWeight: 800,
                              fontSize: '11.5px',
                              color: 'var(--color-fg)',
                              cursor: 'pointer',
                              whiteSpace: 'nowrap',
                              transition: 'all 0.15s cubic-bezier(0.34,1.56,0.64,1)',
                            }}
                            onMouseEnter={(e) => {
                              e.currentTarget.style.background = 'var(--color-tertiary)';
                              e.currentTarget.style.transform = 'translate(-1px, -1px)';
                              e.currentTarget.style.boxShadow = '3px 3px 0 0 var(--color-fg)';
                            }}
                            onMouseLeave={(e) => {
                              e.currentTarget.style.background = '#FFFDF5';
                              e.currentTarget.style.transform = 'none';
                              e.currentTarget.style.boxShadow = '2px 2px 0 0 var(--color-fg)';
                            }}
                          >
                            <UtensilsCrossed size={12} color="var(--color-accent)" strokeWidth={2.5} />
                            Xem khay ăn
                          </button>
                        ) : (
                          <span style={{ fontSize: 'var(--fs-xs)', color: 'var(--color-muted-fg)', fontStyle: 'italic' }}>
                            Không chia khay
                          </span>
                        )}
                      </td>
                      <td style={{ padding: '10px 14px', textAlign: 'center' }}>
                        {item.isActive ? (
                          <span className="badge badge--mint" style={{ border: '1px solid var(--color-fg)' }}>Thực ăn</span>
                        ) : (
                          <span className="badge badge--pink" style={{ border: '1px solid var(--color-fg)' }}>Đã hủy</span>
                        )}
                      </td>
                      <td style={{ padding: '10px 14px', color: 'var(--color-muted-fg)', fontSize: 'var(--fs-xs)' }}>
                        {item.isActive && (item.allergyConflicts?.length ?? 0) > 0 && (
                          hasKitchenResolution(item.remarks) ? (
                            <div
                              style={{
                                display: 'inline-flex',
                                alignItems: 'center',
                                gap: 4,
                                padding: '2px 8px',
                                borderRadius: 8,
                                background: '#ECFDF5',
                                border: '1px solid #059669',
                                color: '#065F46',
                                fontWeight: 800,
                                marginBottom: 4,
                              }}
                            >
                              <CheckCircle2 size={12} /> Bếp đã đổi món: {getCleanKitchenResolutionText(item.remarks)}
                            </div>
                          ) : (
                            <div
                              style={{
                                display: 'inline-flex',
                                alignItems: 'center',
                                gap: 4,
                                padding: '2px 8px',
                                borderRadius: 8,
                                background: '#FEE2E2',
                                border: '1px solid #DC2626',
                                color: '#991B1B',
                                fontWeight: 800,
                                marginBottom: 4,
                              }}
                            >
                              <AlertTriangle size={12} /> Xung đột dị ứng: {item.allergyConflicts!.join('; ')}
                            </div>
                          )
                        )}
                        {item.isActive && ((item.studentAllergenTags?.length ?? 0) > 0 || item.studentAllergyNotes) && (
                          <div style={{ display: 'flex', gap: 4, flexWrap: 'wrap', alignItems: 'center', marginBottom: 3 }}>
                            {(item.studentAllergenTags ?? []).map((tag) => (
                              <span
                                key={tag}
                                style={{
                                  fontSize: '10.5px',
                                  fontWeight: 800,
                                  padding: '1px 6px',
                                  borderRadius: 999,
                                  background: '#FFF1F2',
                                  border: '1px solid #FB7185',
                                  color: '#9F1239',
                                }}
                              >
                                {getAllergenShortLabel(tag)}
                              </span>
                            ))}
                            {item.studentAllergyNotes && (
                              <span style={{ color: '#9F1239', fontWeight: 700 }}>
                                Ghi chú dị ứng: {item.studentAllergyNotes}
                              </span>
                            )}
                          </div>
                        )}
                        <div>
                          {(item.isActive
                            ? (getDistinctKitchenRemark(item) || (!(item.studentAllergenTags?.length || item.studentAllergyNotes) ? item.allergyWarning : '') || '')
                            : item.cancellationReason) ||
                            (!(item.allergyConflicts?.length || item.studentAllergenTags?.length || item.studentAllergyNotes) ? '—' : '')}
                        </div>
                      </td>
                    </tr>
                  );
                })
                ) : (
                  <tr>
                    <td colSpan={8} style={{ textAlign: 'center', padding: '36px', color: 'var(--color-muted-fg)' }}>
                      {SYSTEM_MESSAGES.MSG43.content}
                    </td>
                  </tr>
                )}
              </tbody>
            </table>
            </div>

            {/* Mobile Card List for Tab 2 (<= 768px) */}
            <div className="mobile-only-cards" style={{ padding: 12 }}>
              {pagedStudents.length > 0 ? (
                pagedStudents.map((item, idx) => (
                  <div
                    key={item.registrationId}
                    style={{
                      border: '2px solid var(--color-fg)',
                      borderRadius: 'var(--radius-md)',
                      boxShadow: '2px 2px 0 0 var(--color-fg)',
                      background: '#FFFDF5',
                      padding: 12,
                      display: 'flex',
                      flexDirection: 'column',
                      gap: 8,
                    }}
                  >
                    <div style={{ display: 'flex', justifyContent: 'space-between', alignItems: 'flex-start', gap: 8 }}>
                      <div>
                        <div style={{ fontWeight: 800, fontSize: 'var(--fs-sm)', color: 'var(--color-fg)' }}>
                          {(pageIndex - 1) * pageSize + idx + 1}. {item.studentName}
                        </div>
                        <div style={{ fontSize: 'var(--fs-xs)', color: 'var(--color-muted-fg)', fontWeight: 700, marginTop: 2 }}>
                          <span style={{ color: 'var(--color-accent)' }}>{item.studentCode}</span> • {formatClassLabel(item.className, item.classId)}
                        </div>
                      </div>
                      <span
                        className={`badge ${item.isActive ? 'badge--mint' : 'badge--pink'}`}
                        style={{ border: '1.5px solid var(--color-fg)', fontWeight: 800, fontSize: 11, flexShrink: 0 }}
                      >
                        {item.isActive ? 'Thực ăn' : 'Đã hủy'}
                      </span>
                    </div>

                    <div style={{ display: 'flex', alignItems: 'center', gap: 6, flexWrap: 'wrap' }}>
                      <span className={`badge ${getMealTypeBadgeClass(item.mealType)}`} style={{ border: '1.5px solid var(--color-fg)', fontWeight: 800, fontSize: 11 }}>
                        {MEAL_TYPE_TEXT[String(item.mealType)] ?? 'Tiêu chuẩn'}
                      </span>
                      {(item.studentAllergenTags ?? []).map((tag: string) => (
                        <span
                          key={tag}
                          style={{
                            fontSize: 11,
                            fontWeight: 800,
                            padding: '2px 8px',
                            borderRadius: 'var(--radius-full)',
                            background: '#FEE2E2',
                            color: '#991B1B',
                            border: '1.5px solid var(--color-fg)',
                          }}
                        >
                          {getAllergenShortLabel(tag)}
                        </span>
                      ))}
                    </div>

                    {(item.cancellationReason || item.remarks) && (
                      <div style={{ fontSize: 'var(--fs-xs)', color: 'var(--color-muted-fg)', lineHeight: 1.4 }}>
                        {item.cancellationReason || item.remarks}
                      </div>
                    )}

                    {item.isActive && (
                      <button
                        type="button"
                        onClick={() => setSelectedStudentTray(item)}
                        style={{
                          display: 'inline-flex',
                          alignItems: 'center',
                          justifyContent: 'center',
                          gap: 6,
                          padding: '8px 14px',
                          borderRadius: 'var(--radius-full)',
                          border: '2px solid var(--color-fg)',
                          background: '#FFFFFF',
                          boxShadow: '2px 2px 0 0 var(--color-fg)',
                          fontWeight: 800,
                          fontSize: 'var(--fs-xs)',
                          color: 'var(--color-fg)',
                          cursor: 'pointer',
                          width: '100%',
                        }}
                      >
                        <UtensilsCrossed size={13} color="var(--color-accent)" strokeWidth={2.5} />
                        Xem khay ăn
                      </button>
                    )}
                  </div>
                ))
              ) : (
                <div style={{ padding: 28, textAlign: 'center', color: 'var(--color-muted-fg)', fontWeight: 700 }}>
                  Không tìm thấy học sinh nào khớp với điều kiện lọc.
                </div>
              )}
            </div>

            {/* PAGINATION BAR */}
            <div
              style={{
                display: 'flex',
                alignItems: 'center',
                justifyContent: 'space-between',
                padding: '12px 18px',
                borderTop: '2px solid var(--color-fg)',
                background: '#F8FAFC',
                fontSize: 'var(--fs-sm)',
                flexWrap: 'wrap',
                gap: 12,
              }}
            >
              <div>
                Hiển thị <strong>{pagedStudents.length}</strong> / <strong>{totalCount}</strong> học sinh
                {totalCount > 0 && (
                  <span style={{ color: 'var(--color-muted-fg)', marginLeft: 8 }}>
                    (Bản ghi {(pageIndex - 1) * pageSize + 1} – {Math.min(pageIndex * pageSize, totalCount)})
                  </span>
                )}
              </div>
              <div style={{ display: 'flex', alignItems: 'center', gap: 8 }}>
                <button
                  type="button"
                  className="btn btn--secondary btn--sm"
                  disabled={pageIndex <= 1}
                  onClick={() => setPageIndex((p) => Math.max(1, p - 1))}
                  style={{
                    borderRadius: 'var(--radius-full)',
                    border: '1.5px solid var(--color-fg)',
                    boxShadow: pageIndex > 1 ? '2px 2px 0 0 var(--color-fg)' : 'none',
                    fontWeight: 700,
                  }}
                >
                  <ChevronLeft size={14} /> Trang trước
                </button>
                <span className="badge badge--violet" style={{ alignSelf: 'center', padding: '4px 12px', border: '1.5px solid var(--color-fg)', fontWeight: 700 }}>
                  Trang {pageIndex} / {totalPages}
                </span>
                <button
                  type="button"
                  className="btn btn--secondary btn--sm"
                  disabled={pageIndex >= totalPages}
                  onClick={() => setPageIndex((p) => Math.min(totalPages, p + 1))}
                  style={{
                    borderRadius: 'var(--radius-full)',
                    border: '1.5px solid var(--color-fg)',
                    boxShadow: pageIndex < totalPages ? '2px 2px 0 0 var(--color-fg)' : 'none',
                    fontWeight: 700,
                  }}
                >
                  Trang sau <ChevronRight size={14} />
                </button>
              </div>
            </div>
          </div>
        </div>
      )}

      {/* ===================================================================== */}
      {/* MÀN HÌNH 3: NHẬT KÝ BIẾN ĐỘNG SUẤT ĂN                                */}
      {/* ===================================================================== */}
      {activeTab === 'logs' && (
        <div
          className="card pop-in"
          style={{
            border: '2px solid var(--color-fg)',
            boxShadow: '4px 4px 0 0 var(--color-fg)',
            borderRadius: 'var(--radius-lg)',
            padding: '24px',
          }}
        >
          {/* Header */}
          <div style={{ display: 'flex', justifyContent: 'space-between', alignItems: 'flex-start', flexWrap: 'wrap', gap: 16, marginBottom: 20 }}>
            <div>
              <h3 style={{ margin: 0, fontSize: 'var(--fs-md)', fontFamily: 'var(--font-heading)', display: 'flex', alignItems: 'center', gap: 8 }}>
                <Clock size={18} color="var(--color-secondary)" />
                Nhật Ký Biến Động Suất Ăn
              </h3>
              <p style={{ margin: '4px 0 0', fontSize: 'var(--fs-xs)', color: 'var(--color-muted-fg)' }}>
                Truy vết toàn bộ việc tự động hủy suất trước {effectiveCutoffLabel}, phụ huynh báo hủy hoặc khôi phục suất ăn.
              </p>
            </div>

            {/* Quick Status Chips */}
            <div style={{ display: 'flex', gap: 8, alignItems: 'center', flexWrap: 'wrap' }}>
              <span className="badge badge--violet" style={{ border: '1.5px solid var(--color-fg)' }}>
                Tổng biến động: <strong>{board?.mealChangeLogs?.length || 0}</strong>
              </span>
              <span className="badge badge--pink" style={{ border: '1.5px solid var(--color-fg)' }}>
                Hủy suất: <strong>{(board?.mealChangeLogs ?? []).filter((l) => l.changeType !== 'Restored').length}</strong>
              </span>
              <span className="badge badge--mint" style={{ border: '1.5px solid var(--color-fg)' }}>
                Khôi phục: <strong>{(board?.mealChangeLogs ?? []).filter((l) => l.changeType === 'Restored').length}</strong>
              </span>
            </div>
          </div>

          {/* Search & Filter Bar */}
          <div
            style={{
              display: 'flex',
              gap: 12,
              flexWrap: 'wrap',
              alignItems: 'center',
              padding: '14px 18px',
              background: '#FFFDF5',
              borderRadius: 'var(--radius-md)',
              marginBottom: 20,
              border: '2px solid var(--color-fg)',
              boxShadow: '2px 2px 0 0 var(--color-fg)',
            }}
          >
            {/* Search Input */}
            <div style={{ position: 'relative', flex: '1 1 200px', minWidth: 0 }}>
              <Search
                size={16}
                color="var(--color-muted-fg)"
                style={{ position: 'absolute', left: 12, top: '50%', transform: 'translateY(-50%)' }}
              />
              <input
                type="text"
                className="input"
                placeholder="Tìm tên, mã học sinh, lớp hoặc lý do..."
                value={logSearchQuery}
                onChange={(e) => setLogSearchQuery(e.target.value)}
                style={{
                  paddingLeft: 36,
                  fontSize: 'var(--fs-sm)',
                  borderRadius: 'var(--radius-full)',
                  border: '2px solid var(--color-fg)',
                }}
              />
            </div>

            {/* Log Type Filter */}
            <select
              className="select"
              value={logFilter}
              onChange={(e) => setLogFilter(e.target.value as 'ALL' | 'AutoCancelled' | 'ParentCancelled' | 'Restored')}
              style={{
                minWidth: 200,
                padding: '6px 14px',
                fontSize: 'var(--fs-sm)',
                borderRadius: 'var(--radius-full)',
                border: '2px solid var(--color-fg)',
              }}
            >
              <option value="ALL">Tất cả biến động ({board?.mealChangeLogs?.length || 0})</option>
              <option value="AutoCancelled">Tự hủy từ đơn xin nghỉ</option>
              <option value="ParentCancelled">Phụ huynh báo hủy</option>
              <option value="Restored">Khôi phục suất ăn</option>
            </select>

            {/* Page Size Select */}
            <select
              className="select"
              value={logPageSize}
              onChange={(e) => setLogPageSize(Number(e.target.value))}
              style={{
                minWidth: 120,
                padding: '6px 12px',
                fontSize: 'var(--fs-sm)',
                borderRadius: 'var(--radius-full)',
                border: '2px solid var(--color-fg)',
              }}
            >
              <option value={5}>5 dòng / trang</option>
              <option value={10}>10 dòng / trang</option>
              <option value={20}>20 dòng / trang</option>
            </select>
          </div>

          {/* Audit Logs Table */}
          <div style={{ overflowX: 'auto', border: '2px solid var(--color-fg)', borderRadius: 'var(--radius-md)' }}>
            <table style={{ width: '100%', borderCollapse: 'collapse', fontSize: 'var(--fs-sm)' }}>
              <thead>
                <tr style={{ borderBottom: '2px solid var(--color-fg)', background: '#F8FAFC' }}>
                  <th style={{ padding: '12px 14px', textAlign: 'center', width: 50, fontWeight: 800 }}>STT</th>
                  <th style={{ padding: '12px 14px', textAlign: 'center', width: 90, fontWeight: 800 }}>Thời gian</th>
                  <th style={{ padding: '12px 14px', textAlign: 'left', width: 100, fontWeight: 800 }}>Mã HS</th>
                  <th style={{ padding: '12px 14px', textAlign: 'left', minWidth: 150, fontWeight: 800 }}>Học sinh & Lớp</th>
                  <th style={{ padding: '12px 14px', textAlign: 'center', width: 160, fontWeight: 800 }}>Loại biến động</th>
                  <th style={{ padding: '12px 14px', textAlign: 'left', fontWeight: 800 }}>Lý do / Căn cứ</th>
                  <th style={{ padding: '12px 14px', textAlign: 'center', width: 130, fontWeight: 800 }}>Mốc chốt</th>
                </tr>
              </thead>
              <tbody>
                {pagedLogs.length > 0 ? (
                  pagedLogs.map((log, idx) => {
                    const isParent = log.changeType === 'ParentCancelled';
                    const isAuto = log.changeType === 'AutoCancelled';

                    return (
                      <tr
                        key={idx}
                        style={{ borderBottom: '1px solid var(--color-border)', transition: 'background 0.15s' }}
                        onMouseEnter={(e) => (e.currentTarget.style.background = '#FFFDF5')}
                        onMouseLeave={(e) => (e.currentTarget.style.background = 'transparent')}
                      >
                        <td style={{ padding: '11px 14px', textAlign: 'center', fontWeight: 600 }}>
                          {(logPageIndex - 1) * logPageSize + idx + 1}
                        </td>
                        <td style={{ padding: '11px 14px', textAlign: 'center', fontWeight: 700, color: 'var(--color-fg)', fontSize: 'var(--fs-xs)' }}>
                          {formatViTime(log.timestamp)}
                        </td>
                        <td style={{ padding: '11px 14px', fontWeight: 800, color: 'var(--color-accent)' }}>
                          {log.studentCode}
                        </td>
                        <td style={{ padding: '11px 14px' }}>
                          <span style={{ fontWeight: 700, color: 'var(--color-fg)' }}>{log.studentName}</span>
                          <span className="badge badge--muted" style={{ marginLeft: 8, fontSize: 11, padding: '1px 6px' }}>
                            {formatClassLabel(log.className)}
                          </span>
                        </td>
                        <td style={{ padding: '11px 14px', textAlign: 'center' }}>
                          {isParent ? (
                            <span className="badge badge--pink" style={{ border: '1px solid var(--color-fg)', fontWeight: 700 }}>
                              Phụ huynh báo hủy
                            </span>
                          ) : isAuto ? (
                            <span className="badge badge--amber" style={{ border: '1px solid var(--color-fg)', fontWeight: 700 }}>
                              Tự hủy từ đơn xin nghỉ
                            </span>
                          ) : (
                            <span className="badge badge--mint" style={{ border: '1px solid var(--color-fg)', fontWeight: 700 }}>
                              Khôi phục suất
                            </span>
                          )}
                        </td>
                        <td style={{ padding: '11px 14px', color: 'var(--color-fg)', fontSize: 'var(--fs-xs)', fontStyle: 'italic' }}>
                          {log.reason || '—'}
                        </td>
                        <td style={{ padding: '11px 14px', textAlign: 'center' }}>
                          {log.isBeforeCutoff ? (
                            <span style={{ color: '#059669', fontWeight: 800, fontSize: 'var(--fs-xs)' }}>
                              ✓ Trước {effectiveCutoffLabel}
                            </span>
                          ) : (
                            <span style={{ color: '#E11D48', fontWeight: 800, fontSize: 'var(--fs-xs)' }}>
                              ! Sau {effectiveCutoffLabel}
                            </span>
                          )}
                        </td>
                      </tr>
                    );
                  })
                ) : (
                  <tr>
                    <td colSpan={7} style={{ textAlign: 'center', padding: '36px 20px', color: 'var(--color-muted-fg)' }}>
                      <CheckCircle2 size={36} color="#059669" style={{ marginBottom: 8, display: 'inline-block' }} />
                      <div style={{ fontWeight: 700, fontSize: 'var(--fs-sm)', color: 'var(--color-fg)' }}>
                        Không có biến động nào trong ngày
                      </div>
                      <div style={{ fontSize: 'var(--fs-xs)', marginTop: 4 }}>
                        Toàn bộ học sinh dùng bữa theo danh sách đăng ký ban đầu.
                      </div>
                    </td>
                  </tr>
                )}
              </tbody>
            </table>

            {/* Pagination Controls */}
            {totalLogCount > 0 && (
              <div
                style={{
                  display: 'flex',
                  alignItems: 'center',
                  justifyContent: 'space-between',
                  padding: '12px 18px',
                  borderTop: '2px solid var(--color-fg)',
                  background: '#F8FAFC',
                  fontSize: 'var(--fs-sm)',
                  flexWrap: 'wrap',
                  gap: 12,
                }}
              >
                <div>
                  Hiển thị <strong>{pagedLogs.length}</strong> / <strong>{totalLogCount}</strong> biến động
                  <span style={{ color: 'var(--color-muted-fg)', marginLeft: 8 }}>
                    (Bản ghi {(logPageIndex - 1) * logPageSize + 1} – {Math.min(logPageIndex * logPageSize, totalLogCount)})
                  </span>
                </div>
                <div style={{ display: 'flex', alignItems: 'center', gap: 8 }}>
                  <button
                    type="button"
                    className="btn btn--secondary btn--sm"
                    disabled={logPageIndex <= 1}
                    onClick={() => setLogPageIndex((p) => Math.max(1, p - 1))}
                    style={{
                      borderRadius: 'var(--radius-full)',
                      border: '1.5px solid var(--color-fg)',
                      boxShadow: logPageIndex > 1 ? '2px 2px 0 0 var(--color-fg)' : 'none',
                      fontWeight: 700,
                    }}
                  >
                    <ChevronLeft size={14} /> Trang trước
                  </button>
                  <span className="badge badge--violet" style={{ alignSelf: 'center', padding: '4px 12px', border: '1.5px solid var(--color-fg)', fontWeight: 700 }}>
                    Trang {logPageIndex} / {totalLogPages}
                  </span>
                  <button
                    type="button"
                    className="btn btn--secondary btn--sm"
                    disabled={logPageIndex >= totalLogPages}
                    onClick={() => setLogPageIndex((p) => Math.min(totalLogPages, p + 1))}
                    style={{
                      borderRadius: 'var(--radius-full)',
                      border: '1.5px solid var(--color-fg)',
                      boxShadow: logPageIndex < totalLogPages ? '2px 2px 0 0 var(--color-fg)' : 'none',
                      fontWeight: 700,
                    }}
                  >
                    Trang sau <ChevronRight size={14} />
                  </button>
                </div>
              </div>
            )}
          </div>
        </div>
      )}

      {/* ===================================================================== */}
      {/* MÀN HÌNH 4: BÁO CÁO THỐNG KÊ KỲ / THÁNG (LUNCH REPORT)               */}
      {/* ===================================================================== */}
      {activeTab === 'report' && (
        <div
          className="card pop-in"
          style={{
            border: '2px solid var(--color-fg)',
            boxShadow: '4px 4px 0 0 var(--color-fg)',
            borderRadius: 'var(--radius-lg)',
            padding: '24px',
          }}
        >
          <div style={{ display: 'flex', justifyContent: 'space-between', alignItems: 'center', flexWrap: 'wrap', gap: 16, marginBottom: 20 }}>
            <div>
              <h3 style={{ margin: 0, fontSize: 'var(--fs-md)', fontFamily: 'var(--font-heading)', display: 'flex', alignItems: 'center', gap: 8 }}>
                <CalendarRange size={18} color="var(--color-accent)" />
                Báo Cáo Thống Kê Suất Ăn Bán Trú Theo Kỳ
              </h3>
              <p style={{ margin: '4px 0 0', fontSize: 'var(--fs-xs)', color: 'var(--color-muted-fg)' }}>
                Tổng hợp số ngày phục vụ, tổng suất đăng ký, suất hủy và phân loại chế độ ăn (Mặn / Chay / Đặc biệt) theo từng lớp.
              </p>
            </div>

            <div style={{ display: 'flex', alignItems: 'center', gap: 10, flexWrap: 'wrap' }}>
              <DatePickerPill
                label="Từ:"
                size="sm"
                background="#FFFDF5"
                value={reportFrom}
                ariaLabel="Chọn từ ngày báo cáo"
                onChange={(val) => {
                  setReportFrom(val);
                  if (reportTo && val > reportTo) {
                    setReportTo(val);
                  }
                }}
              />
              <DatePickerPill
                label="Đến:"
                size="sm"
                background="#FFFDF5"
                value={reportTo}
                min={reportFrom}
                ariaLabel="Chọn đến ngày báo cáo"
                onChange={(val) => {
                  setReportTo(val);
                  if (reportFrom && val < reportFrom) {
                    setReportFrom(val);
                  }
                }}
              />

              <button
                type="button"
                className="btn btn--secondary btn--sm"
                onClick={() => fetchPeriodReport(reportFrom, reportTo)}
                disabled={loadingReport}
                style={{ borderRadius: 'var(--radius-full)', border: '2px solid var(--color-fg)', boxShadow: '2px 2px 0 0 var(--color-fg)', fontWeight: 700 }}
              >
                <RefreshCw size={14} className={loadingReport ? 'spin' : ''} /> Tải báo cáo
              </button>

              <button
                type="button"
                className="btn btn--primary btn--sm"
                onClick={handleExportPeriodReportCsv}
                disabled={loadingReport || !periodReport}
                style={{
                  borderRadius: 'var(--radius-full)',
                  border: '2px solid var(--color-fg)',
                  boxShadow: '2px 2px 0 0 var(--color-fg)',
                  fontWeight: 800,
                  display: 'inline-flex',
                  alignItems: 'center',
                  gap: 6,
                }}
              >
                <Download size={14} /> Xuất CSV
              </button>

            </div>
          </div>

          {periodReport && (
            <>
              <div className="mobile-stat-grid" style={{ marginBottom: 20 }}>
                <div className="card" style={{ background: '#FFFDF5', border: '2px solid var(--color-fg)', padding: '14px 16px' }}>
                  <div className="label">NGÀY PHỤC VỤ</div>
                  <div style={{ fontSize: 'var(--fs-xl)', fontWeight: 900, fontFamily: 'var(--font-heading)' }}>{periodReport.totalServiceDays} ngày</div>
                </div>
                <div className="card" style={{ background: '#ECFDF5', border: '2px solid var(--color-fg)', padding: '14px 16px' }}>
                  <div className="label" style={{ color: '#047857' }}>TỔNG SUẤT THỰC NẤU</div>
                  <div style={{ fontSize: 'var(--fs-xl)', fontWeight: 900, color: '#065F46', fontFamily: 'var(--font-heading)' }}>{periodReport.totalActualMeals} suất</div>
                </div>
                <div className="card" style={{ background: '#F0F9FF', border: '2px solid var(--color-fg)', padding: '14px 16px' }}>
                  <div className="label">CƠ CẤU CHẾ ĐỘ ĂN</div>
                  <div style={{ display: 'flex', flexWrap: 'wrap', gap: 6, marginTop: 8 }}>
                    <span className="badge badge--mint" style={{ fontSize: 'var(--fs-xs)', fontWeight: 800, padding: '3px 9px' }}>
                      {periodReport.totalStandard} Mặn
                    </span>
                    <span className="badge badge--amber" style={{ fontSize: 'var(--fs-xs)', fontWeight: 800, padding: '3px 9px' }}>
                      {periodReport.totalVegetarian} Chay
                    </span>
                    <span className="badge badge--violet" style={{ fontSize: 'var(--fs-xs)', fontWeight: 800, padding: '3px 9px' }}>
                      {periodReport.totalSpecialDiet} Đặc biệt
                    </span>
                  </div>
                </div>
                <div className="card" style={{ background: '#FFF1F2', border: '2px solid var(--color-fg)', padding: '14px 16px' }}>
                  <div className="label" style={{ color: '#E11D48' }}>TỔNG ĐÃ HỦY HỢP LỆ</div>
                  <div style={{ fontSize: 'var(--fs-xl)', fontWeight: 900, color: '#E11D48', fontFamily: 'var(--font-heading)' }}>
                    {periodReport.totalCancelled > 0 ? `-${periodReport.totalCancelled}` : '0'} suất
                  </div>
                </div>
              </div>

              <div style={{ overflowX: 'auto', border: '2px solid var(--color-fg)', borderRadius: 'var(--radius-md)' }}>
                <table style={{ width: '100%', borderCollapse: 'collapse', fontSize: 'var(--fs-sm)' }}>
                  <thead>
                    <tr style={{ borderBottom: '2px solid var(--color-fg)', background: '#F8FAFC' }}>
                      <th style={{ padding: '12px 14px', textAlign: 'left', fontWeight: 800 }}>Lớp</th>
                      <th style={{ padding: '12px 14px', textAlign: 'center', fontWeight: 800 }}>Khối</th>
                      <th style={{ padding: '12px 14px', textAlign: 'center', fontWeight: 800 }}>Tổng Đăng Ký</th>
                      <th style={{ padding: '12px 14px', textAlign: 'center', fontWeight: 800 }}>Đã Hủy</th>
                      <th style={{ padding: '12px 14px', textAlign: 'center', fontWeight: 800 }}>Thực Nấu</th>
                      <th style={{ padding: '12px 14px', textAlign: 'center', fontWeight: 800 }}>Tiêu Chuẩn (Mặn)</th>
                      <th style={{ padding: '12px 14px', textAlign: 'center', fontWeight: 800 }}>Ăn Chay</th>
                      <th style={{ padding: '12px 14px', textAlign: 'center', fontWeight: 800 }}>Đặc Biệt</th>
                    </tr>
                  </thead>
                  <tbody>
                    {periodReport.classSummaries.map((c) => (
                      <tr key={c.classId} style={{ borderBottom: '1px solid var(--color-border)' }}>
                        <td style={{ padding: '10px 14px', fontWeight: 800, color: 'var(--color-accent)' }}>{formatClassLabel(c.className, c.classId)}</td>
                        <td style={{ padding: '10px 14px', textAlign: 'center' }}>{c.grade > 0 ? `Khối ${c.grade}` : '—'}</td>
                        <td style={{ padding: '10px 14px', textAlign: 'center', fontWeight: 700 }}>{c.initialRegistered}</td>
                        <td style={{ padding: '10px 14px', textAlign: 'center', color: '#E11D48', fontWeight: 700 }}>
                          {c.cancelledCount > 0 ? `-${c.cancelledCount}` : '0'}
                        </td>
                        <td style={{ padding: '10px 14px', textAlign: 'center', fontWeight: 900, color: '#047857' }}>{c.actualCount}</td>
                        <td style={{ padding: '10px 14px', textAlign: 'center' }}>{c.standardCount ?? 0}</td>
                        <td style={{ padding: '10px 14px', textAlign: 'center', fontWeight: (c.vegetarianCount ?? 0) > 0 ? 800 : 400 }}>{c.vegetarianCount ?? 0}</td>
                        <td style={{ padding: '10px 14px', textAlign: 'center', fontWeight: (c.specialDietCount ?? 0) > 0 ? 800 : 400 }}>{c.specialDietCount ?? 0}</td>
                      </tr>
                    ))}
                  </tbody>
                </table>
              </div>
            </>
          )}
        </div>
      )}

      {/* ===================================================================== */}
      {/* MODAL CHI TIẾT KHẨU PHẦN TỪNG LỚP                                     */}
      {/* ===================================================================== */}
      <GeometricModal
        open={Boolean(detailClass)}
        onClose={() => setDetailClass(null)}
        title={detailClass ? `Chi Tiết Suất Ăn ${formatClassLabel(detailClass.className, detailClass.classId)}` : 'Chi Tiết Suất Ăn'}
        subtitle={`Ngày ${formatDateDDMMYYYY(selectedDate)} • Danh sách học sinh và chế độ ăn của lớp`}
        badgeText={`Thực ăn: ${detailClass?.actualCount ?? 0}/${detailClass?.initialRegistered ?? 0} suất`}
        badgeColor="mint"
        icon={<Soup size={22} color="var(--color-fg)" />}
        iconBg="var(--color-quaternary)"
        maxWidth="lg"
      >
        {detailClass && (
          <div>
            {(() => {
              const classMeals = (board?.studentMeals ?? []).filter((s) => s.classId === detailClass.classId);
              const activeMeals = classMeals.filter((s) => s.isActive);
              return (
                <>
                  <div style={{ display: 'flex', gap: 8, flexWrap: 'wrap', marginBottom: 14 }}>
                    <span className="badge badge--mint" style={{ border: '1.5px solid var(--color-fg)', fontWeight: 800 }}>
                      Tiêu chuẩn: {activeMeals.filter((s) => s.mealType === 'StandardLunch' || s.mealType === 1).length}
                    </span>
                    <span className="badge badge--amber" style={{ border: '1.5px solid var(--color-fg)', fontWeight: 800 }}>
                      Ăn chay: {activeMeals.filter((s) => s.mealType === 'VegetarianLunch' || s.mealType === 2).length}
                    </span>
                    <span className="badge badge--pink" style={{ border: '1.5px solid var(--color-fg)', fontWeight: 800 }}>
                      Đặc biệt: {activeMeals.filter((s) => s.mealType === 'SpecialDiet' || s.mealType === 3).length}
                    </span>
                    <span className="badge badge--muted" style={{ border: '1.5px solid var(--color-fg)', fontWeight: 700 }}>
                      Đã hủy: {classMeals.filter((s) => !s.isActive).length}
                    </span>
                  </div>

                  <div style={{ maxHeight: 360, overflowY: 'auto', border: '2px solid var(--color-fg)', borderRadius: 'var(--radius-md)' }}>
                    <table style={{ width: '100%', borderCollapse: 'collapse', fontSize: 'var(--fs-sm)' }}>
                      <thead>
                        <tr style={{ borderBottom: '2px solid var(--color-fg)', background: '#F8FAFC' }}>
                          <th style={{ padding: '10px 12px', textAlign: 'left', fontWeight: 800 }}>Mã HS</th>
                          <th style={{ padding: '10px 12px', textAlign: 'left', fontWeight: 800 }}>Họ và tên</th>
                          <th style={{ padding: '10px 12px', textAlign: 'center', fontWeight: 800 }}>Chế độ ăn</th>
                          <th style={{ padding: '10px 12px', textAlign: 'center', fontWeight: 800 }}>Khay ăn</th>
                          <th style={{ padding: '10px 12px', textAlign: 'center', fontWeight: 800 }}>Trạng thái</th>
                          <th style={{ padding: '10px 12px', textAlign: 'left', fontWeight: 800 }}>Ghi chú dị ứng / Lý do hủy</th>
                        </tr>
                      </thead>
                      <tbody>
                        {classMeals.length === 0 ? (
                          <tr>
                            <td colSpan={6} style={{ padding: 24, textAlign: 'center', color: 'var(--color-muted-fg)' }}>
                              Chưa có học sinh đăng ký ăn trưa trong lớp này.
                            </td>
                          </tr>
                        ) : (
                          classMeals.map((s) => {
                            return (
                            <tr key={s.registrationId} style={{ borderBottom: '1px solid var(--color-border)' }}>
                              <td style={{ padding: '8px 12px', fontWeight: 800, color: 'var(--color-accent)' }}>{s.studentCode}</td>
                              <td style={{ padding: '8px 12px', fontWeight: 700 }}>{s.studentName}</td>
                              <td style={{ padding: '8px 12px', textAlign: 'center' }}>
                                <span className={`badge ${getMealTypeBadgeClass(s.mealType)}`} style={{ border: '1px solid var(--color-fg)', fontWeight: 800 }}>
                                  {MEAL_TYPE_TEXT[String(s.mealType)] ?? 'Tiêu chuẩn'}
                                </span>
                              </td>
                              <td style={{ padding: '8px 12px', textAlign: 'center' }}>
                                {s.isActive ? (
                                  <button
                                    type="button"
                                    onClick={() => setSelectedStudentTray(s)}
                                    style={{
                                      display: 'inline-flex',
                                      alignItems: 'center',
                                      gap: 4,
                                      padding: '4px 10px',
                                      borderRadius: 'var(--radius-full)',
                                      border: '1.5px solid var(--color-fg)',
                                      background: '#FFFDF5',
                                      boxShadow: '2px 2px 0 0 var(--color-fg)',
                                      fontWeight: 800,
                                      fontSize: '11px',
                                      color: 'var(--color-fg)',
                                      cursor: 'pointer',
                                      whiteSpace: 'nowrap',
                                    }}
                                  >
                                    <UtensilsCrossed size={11} color="var(--color-accent)" strokeWidth={2.5} />
                                    Xem khay ăn
                                  </button>
                                ) : (
                                  <span style={{ fontSize: '11px', color: 'var(--color-muted-fg)', fontStyle: 'italic' }}>Đã hủy suất</span>
                                )}
                              </td>
                              <td style={{ padding: '8px 12px', textAlign: 'center' }}>
                                {s.isActive ? (
                                  <span className="badge badge--mint" style={{ border: '1px solid var(--color-fg)' }}>Thực ăn</span>
                                ) : (
                                  <span className="badge badge--pink" style={{ border: '1px solid var(--color-fg)' }}>Đã hủy</span>
                                )}
                              </td>
                              <td style={{ padding: '8px 12px', fontSize: 'var(--fs-xs)', color: 'var(--color-muted-fg)' }}>
                                {s.isActive
                                  ? (getDistinctKitchenRemark(s) || s.allergyWarning || '—')
                                  : (s.cancellationReason || '—')}
                              </td>
                            </tr>
                          );
                        })
                        )}
                      </tbody>
                    </table>
                  </div>
                </>
              );
            })()}
          </div>
        )}
      </GeometricModal>

      {/* ===================================================================== */}
      {/* MODAL KHAY ĂN HỌC SINH (PLAYFUL GEOMETRIC BENTO TRAY)                 */}
      {/* ===================================================================== */}
      <GeometricModal
        open={Boolean(selectedStudentTray)}
        onClose={() => setSelectedStudentTray(null)}
        title={`Khay Ăn Bán Trú • ${selectedStudentTray?.studentName ?? ''}`}
        subtitle={`${selectedStudentTray ? formatClassLabel(selectedStudentTray.className, selectedStudentTray.classId) : ''} • Mã HS: ${selectedStudentTray?.studentCode ?? ''} • Ngày ${formatDateDDMMYYYY(selectedDate)}`}
        badgeText={selectedStudentTray ? (MEAL_TYPE_TEXT[String(selectedStudentTray.mealType)] ?? 'Tiêu chuẩn') : ''}
        badgeColor={
          selectedStudentTray && normalizeMealTypeNumber(selectedStudentTray.mealType) === 3
            ? 'pink'
            : selectedStudentTray && normalizeMealTypeNumber(selectedStudentTray.mealType) === 2
              ? 'amber'
              : 'mint'
        }
        icon={<Soup size={22} color="var(--color-fg)" strokeWidth={2.5} />}
        iconBg="var(--color-tertiary)"
        maxWidth="md"
      >
        {selectedStudentTray && (() => {
          const {
            mealTypeNum,
            dishes,
            liveConflicts,
            conflictingDishNames,
            hasStudentAllergyProfile,
          } = evaluateTrayConflicts(selectedStudentTray);
          const conflictingSet = new Set(conflictingDishNames);
          const declaredTags = selectedStudentTray.studentAllergenTags ?? [];
          const declaredAllergyNotes = (selectedStudentTray.studentAllergyNotes ?? '').trim();
          const kitchenRemarkDisplay = getDistinctKitchenRemark(selectedStudentTray);
          const fallbackLegacyAllergy =
            declaredTags.length === 0 && !declaredAllergyNotes
              ? (selectedStudentTray.allergyWarning || '')
              : '';
          const showSafeBanner =
            dishes.length > 0 &&
            liveConflicts.length === 0 &&
            hasStudentAllergyProfile &&
            (mealTypeNum === 3 || (selectedStudentTray.allergyConflicts?.length ?? 0) > 0);

          const isResolvedByKitchen = hasKitchenResolution(selectedStudentTray.remarks);
          const kitchenResolutionText = getCleanKitchenResolutionText(selectedStudentTray.remarks);

          return (
            <div style={{ display: 'flex', flexDirection: 'column', gap: 16 }}>
              {liveConflicts.length > 0 && isResolvedByKitchen ? (
                <div
                  style={{
                    padding: '10px 14px',
                    borderRadius: 'var(--radius-md)',
                    border: '2px solid #059669',
                    background: '#ECFDF5',
                    color: '#065F46',
                    fontSize: 'var(--fs-xs)',
                    fontWeight: 800,
                    display: 'flex',
                    alignItems: 'flex-start',
                    gap: 8,
                  }}
                >
                  <CheckCircle2 size={16} color="#059669" style={{ flexShrink: 0, marginTop: 1 }} />
                  <div>
                    <div>Nhà bếp đã xử lý đổi món tránh dị ứng: {kitchenResolutionText}</div>
                    <div style={{ fontWeight: 600, marginTop: 2, color: '#047857' }}>
                      Các món gốc cần loại bỏ/thay thế: {liveConflicts.join(' • ')}
                    </div>
                  </div>
                </div>
              ) : liveConflicts.length > 0 ? (
                <div
                  style={{
                    padding: '10px 14px',
                    borderRadius: 'var(--radius-md)',
                    border: '2px solid #DC2626',
                    background: '#FEF2F2',
                    color: '#991B1B',
                    fontSize: 'var(--fs-xs)',
                    fontWeight: 800,
                    display: 'flex',
                    alignItems: 'flex-start',
                    gap: 8,
                  }}
                >
                  <AlertTriangle size={16} style={{ flexShrink: 0, marginTop: 1 }} />
                  <div>
                    <div>Cảnh báo trùng thành phần dị ứng trong khay ăn đang chọn:</div>
                    <div style={{ fontWeight: 700, marginTop: 2 }}>{liveConflicts.join(' • ')}</div>
                  </div>
                </div>
              ) : showSafeBanner ? (
                <div
                  style={{
                    padding: '10px 14px',
                    borderRadius: 'var(--radius-md)',
                    border: '2px solid #059669',
                    background: '#ECFDF5',
                    color: '#065F46',
                    fontSize: 'var(--fs-xs)',
                    fontWeight: 800,
                    display: 'flex',
                    alignItems: 'center',
                    gap: 8,
                  }}
                >
                  <CheckCircle2 size={16} color="#059669" style={{ flexShrink: 0 }} />
                  <div>
                    Khẩu phần đã đăng ký không chứa thành phần gây dị ứng đã khai báo.
                  </div>
                </div>
              ) : null}
              <div
                style={{
                  background: '#FFFDF5',
                  border: '2.5px solid var(--color-fg)',
                  borderRadius: 'var(--radius-lg)',
                  boxShadow: '5px 5px 0 0 #E2E8F0',
                  padding: '16px',
                }}
              >
                <div style={{ display: 'flex', alignItems: 'center', justifyContent: 'space-between', marginBottom: 12, flexWrap: 'wrap', gap: 8 }}>
                  <span
                    style={{
                      fontFamily: 'var(--font-heading)',
                      fontWeight: 800,
                      fontSize: 'var(--fs-sm)',
                      textTransform: 'uppercase',
                      letterSpacing: '0.04em',
                      color: 'var(--color-fg)',
                      display: 'inline-flex',
                      alignItems: 'center',
                      gap: 6,
                    }}
                  >
                    <UtensilsCrossed size={15} color="var(--color-accent)" strokeWidth={2.5} />
                    Các món trong khay ({dishes.length} món)
                  </span>
                  <span className={`badge ${getMealTypeBadgeClass(mealTypeNum)}`} style={{ border: '1.5px solid var(--color-fg)', fontWeight: 800, fontSize: '11px' }}>
                    {MEAL_TYPE_TEXT[String(mealTypeNum)] ?? 'Tiêu chuẩn'}
                  </span>
                </div>

                <div style={{ display: 'grid', gridTemplateColumns: 'repeat(auto-fit, minmax(190px, 1fr))', gap: 10 }}>
                  {dishes.length === 0 ? (
                    <div style={{ gridColumn: '1 / -1', padding: '14px 12px', textAlign: 'center', fontSize: 'var(--fs-xs)', color: 'var(--color-muted-fg)' }}>
                      Ngày này chưa cấu hình thực đơn chi tiết cho chế độ ăn này. Bấm <strong>Cấu hình ngày</strong> hoặc áp dụng <strong>Thực đơn tuần</strong> để thiết lập món.
                    </div>
                  ) : (
                    dishes.map((dish, idx) => {
                      const slot = COURSE_SLOTS[idx % COURSE_SLOTS.length];
                      const isDishAllergic = conflictingSet.has(dish.toLowerCase());
                      const isDishResolved = isDishAllergic && isResolvedByKitchen;
                      return (
                        <div
                          key={idx}
                          style={{
                            display: 'flex',
                            alignItems: 'center',
                            gap: 10,
                            padding: '10px 12px',
                            background: isDishResolved ? '#ECFDF5' : isDishAllergic ? '#FEF2F2' : '#FFFFFF',
                            border: isDishResolved
                              ? '2px solid #059669'
                              : isDishAllergic
                                ? '2px solid #DC2626'
                                : '2px solid var(--color-fg)',
                            borderRadius: 'var(--radius-md)',
                            boxShadow: isDishResolved
                              ? '3px 3px 0 0 #059669'
                              : isDishAllergic
                                ? '3px 3px 0 0 #DC2626'
                                : '3px 3px 0 0 var(--color-fg)',
                          }}
                        >
                          <div
                            style={{
                              width: 38,
                              height: 38,
                              borderRadius: '50%',
                              background: isDishResolved ? '#D1FAE5' : isDishAllergic ? '#FEE2E2' : slot.bg,
                              border: isDishResolved
                                ? '2px solid #059669'
                                : isDishAllergic
                                  ? '2px solid #DC2626'
                                  : '2px solid var(--color-fg)',
                              display: 'grid',
                              placeItems: 'center',
                              fontSize: 18,
                              flexShrink: 0,
                            }}
                          >
                            {isDishResolved ? '✓' : isDishAllergic ? '⚠️' : slot.emoji}
                          </div>
                          <div style={{ minWidth: 0 }}>
                            <div
                              style={{
                                fontSize: '10px',
                                fontWeight: 800,
                                textTransform: 'uppercase',
                                color: isDishResolved ? '#059669' : isDishAllergic ? '#DC2626' : slot.accent,
                              }}
                            >
                              {isDishResolved ? 'ĐÃ XỬ LÝ ĐỔI MÓN' : isDishAllergic ? 'TRÙNG DỊ ỨNG' : slot.role}
                            </div>
                            <div
                              style={{
                                fontFamily: 'var(--font-heading)',
                                fontWeight: 800,
                                fontSize: 'var(--fs-sm)',
                                color: isDishResolved ? '#065F46' : isDishAllergic ? '#991B1B' : 'var(--color-fg)',
                                textDecoration: isDishResolved ? 'line-through' : 'none',
                                marginTop: 2,
                              }}
                            >
                              {dish}
                            </div>
                          </div>
                        </div>
                      );
                    })
                  )}
                </div>
              </div>

              <div
                style={{
                  padding: '12px 14px',
                  borderRadius: 'var(--radius-md)',
                  border: (declaredTags.length > 0 || declaredAllergyNotes || fallbackLegacyAllergy) ? '2px solid #FB7185' : '2px dashed var(--color-fg)',
                  background: (declaredTags.length > 0 || declaredAllergyNotes || fallbackLegacyAllergy) ? '#FFF1F2' : '#F8FAFC',
                  fontSize: 'var(--fs-xs)',
                  display: 'flex',
                  flexDirection: 'column',
                  gap: 6,
                }}
              >
                <div style={{ fontWeight: 800, color: (declaredTags.length > 0 || declaredAllergyNotes || fallbackLegacyAllergy) ? '#BE123C' : 'var(--color-fg)', display: 'flex', alignItems: 'center', gap: 6 }}>
                  <ShieldAlert size={14} /> Hồ sơ dị ứng phụ huynh khai báo:
                </div>

                {declaredTags.length > 0 && (
                  <div style={{ display: 'flex', gap: 6, flexWrap: 'wrap' }}>
                    {declaredTags.map((tag) => (
                      <span
                        key={tag}
                        style={{
                          padding: '3px 9px',
                          borderRadius: '999px',
                          border: '1.5px solid #DC2626',
                          background: '#FEE2E2',
                          color: '#991B1B',
                          fontWeight: 800,
                          fontSize: '11px',
                        }}
                      >
                        {getAllergenFullLabel(tag)}
                      </span>
                    ))}
                  </div>
                )}

                {declaredAllergyNotes && (
                  <div style={{ fontWeight: 700, color: '#9F1239' }}>
                    Ghi chú dị ứng tự do: <span style={{ fontWeight: 600 }}>{declaredAllergyNotes}</span>
                  </div>
                )}

                {fallbackLegacyAllergy && (
                  <div style={{ fontWeight: 700, color: '#9F1239' }}>
                    {fallbackLegacyAllergy}
                  </div>
                )}

                {declaredTags.length === 0 && !declaredAllergyNotes && !fallbackLegacyAllergy && (
                  <div style={{ fontWeight: 600, color: 'var(--color-muted-fg)' }}>
                    Phụ huynh không khai báo dị ứng thực phẩm nào cho học sinh.
                  </div>
                )}

                {kitchenRemarkDisplay && (
                  <div style={{ marginTop: 2, paddingTop: 6, borderTop: '1px dashed #FDA4AF', color: '#475569', fontWeight: 700 }}>
                    📝 Ghi chú chế biến bếp hiện tại: <span style={{ fontWeight: 600 }}>{kitchenRemarkDisplay}</span>
                  </div>
                )}
              </div>

              {board?.canManage && !board.isLocked && selectedStudentTray.isActive && (liveConflicts.length > 0 || isResolvedByKitchen) && (
                <div
                  style={{
                    padding: '12px 14px',
                    borderRadius: 'var(--radius-md)',
                    border: '2px solid var(--color-fg)',
                    background: '#ECFDF5',
                    display: 'flex',
                    flexDirection: 'column',
                    gap: 10,
                  }}
                >
                  <div style={{ display: 'flex', justifyContent: 'space-between', alignItems: 'center', gap: 8 }}>
                    <span style={{ fontWeight: 800, fontSize: 'var(--fs-xs)', textTransform: 'uppercase', color: '#065F46' }}>
                      Phương án đổi món / xử lý dị ứng của Nhà bếp
                    </span>
                  </div>
                  <select
                    className="select"
                    value={kitchenNotePreset}
                    onChange={(e) => {
                      const val = e.target.value;
                      setKitchenNotePreset(val);
                      if (val !== '__CUSTOM__') {
                        setKitchenNote(val);
                      } else {
                        setKitchenNote('');
                      }
                    }}
                    style={{ fontSize: 'var(--fs-xs)', fontWeight: 700, border: '2px solid var(--color-fg)', background: '#FFFFFF' }}
                  >
                    <option value="">-- Chọn phương án xử lý nhanh (hoặc để trống để bỏ ghi chú) --</option>
                    {KITCHEN_SUBSTITUTION_OPTIONS.map((opt) => (
                      <option key={opt} value={opt}>
                        {opt}
                      </option>
                    ))}
                    <option value="__CUSTOM__">Khác (tự nhập ghi chú...)</option>
                  </select>
                  {kitchenNotePreset === '__CUSTOM__' && (
                    <input
                      type="text"
                      className="input"
                      maxLength={235}
                      placeholder="Nhập phương án đổi món cụ thể cho bếp..."
                      value={kitchenNote}
                      onChange={(e) => setKitchenNote(e.target.value)}
                      style={{ fontSize: 'var(--fs-xs)', border: '2px solid var(--color-fg)', background: '#FFFFFF' }}
                    />
                  )}
                  <div style={{ display: 'flex', justifyContent: 'space-between', alignItems: 'center', flexWrap: 'wrap', gap: 8 }}>
                    <span style={{ fontSize: 11, color: '#065F46', fontWeight: 600 }}>
                      Phương án này sẽ hiển thị công khai trên lịch bán trú của Phụ huynh &amp; danh sách lớp của GVCN.
                    </span>
                    <button
                      type="button"
                      className="btn btn--primary btn--sm"
                      onClick={handleSaveKitchenNote}
                      disabled={savingKitchenNote}
                    >
                      {savingKitchenNote ? 'Đang lưu...' : 'Lưu phương án bếp'}
                    </button>
                  </div>
                </div>
              )}

              <div style={{ display: 'flex', justifyContent: 'flex-end' }}>
                <button
                  type="button"
                  className="btn btn--primary"
                  onClick={() => setSelectedStudentTray(null)}
                  style={{
                    borderRadius: 'var(--radius-full)',
                    border: '2px solid var(--color-fg)',
                    boxShadow: '3px 3px 0 0 var(--color-fg)',
                    fontWeight: 800,
                    padding: '8px 22px',
                    background: 'var(--color-accent)',
                    color: '#FFFFFF',
                  }}
                >
                  Đóng khay ăn
                </button>
              </div>
            </div>
          );
        })()}
      </GeometricModal>

      {/* ===================================================================== */}
      {/* MODAL UC-32: Cấu hình Ngày phục vụ, Hạn đăng ký/hủy & Thực đơn        */}
      {/* ===================================================================== */}
      <GeometricModal
        open={showConfigModal}
        onClose={() => setShowConfigModal(false)}
        title="Cấu hình Ngày Phục Vụ Bán Trú"
        subtitle="Thiết lập trạng thái phục vụ, giờ chốt suất ăn và thực đơn trưa của trường"
        badgeText={`Ngày ${formatDateDDMMYYYY(selectedDate)}`}
        badgeColor="mint"
        icon={<Settings size={22} color="var(--color-fg)" />}
        iconBg="var(--color-quaternary)"
        maxWidth="md"
      >
        <div style={{ display: 'flex', flexDirection: 'column', gap: 18 }}>
          {/* 1. Trạng thái phục vụ (2-Card Segmented Selector) */}
          <div>
            <div style={{ display: 'flex', alignItems: 'center', justifyContent: 'space-between', marginBottom: 8 }}>
              <label className="label" style={{ marginBottom: 0, fontSize: 'var(--fs-xs)', letterSpacing: '0.05em' }}>
                1. TRẠNG THÁI PHỤC VỤ TRONG NGÀY
              </label>
              <span className={cfgIsServing ? 'badge badge--mint' : 'badge badge--pink'} style={{ fontSize: 11, padding: '2px 8px' }}>
                {cfgIsServing ? <CheckCircle2 size={12} /> : <AlertTriangle size={12} />}
                {cfgIsServing ? 'Đang mở phục vụ' : 'Tạm nghỉ phục vụ'}
              </span>
            </div>

            <div className="responsive-grid-2">
              <button
                type="button"
                onClick={() => setCfgIsServing(true)}
                style={{
                  padding: '12px 14px',
                  borderRadius: 'var(--radius-md)',
                  border: '2px solid var(--color-fg)',
                  background: cfgIsServing ? '#ECFDF5' : 'var(--color-card)',
                  boxShadow: cfgIsServing ? '4px 4px 0 0 var(--color-fg)' : 'none',
                  textAlign: 'left',
                  cursor: 'pointer',
                  transition: 'all 0.15s cubic-bezier(0.34, 1.56, 0.64, 1)',
                  display: 'flex',
                  flexDirection: 'column',
                  gap: 4,
                  transform: cfgIsServing ? 'translate(-1px, -1px)' : 'none',
                }}
              >
                <div style={{ display: 'flex', alignItems: 'center', justifyContent: 'space-between', gap: 6 }}>
                  <span style={{ fontWeight: 800, fontSize: 'var(--fs-sm)', color: '#065F46', display: 'inline-flex', alignItems: 'center', gap: 6 }}>
                    <CheckCircle2 size={15} color="#059669" /> Phục vụ bán trú
                  </span>
                  {cfgIsServing && (
                    <span className="badge badge--mint" style={{ fontSize: 10, padding: '2px 6px' }}>
                      Đang chọn
                    </span>
                  )}
                </div>
                <span style={{ fontSize: 'var(--fs-xs)', color: 'var(--color-muted-fg)' }}>
                  Mở đăng ký & nhà bếp nấu suất ăn bình thường
                </span>
              </button>

              <button
                type="button"
                onClick={() => setCfgIsServing(false)}
                style={{
                  padding: '12px 14px',
                  borderRadius: 'var(--radius-md)',
                  border: '2px solid var(--color-fg)',
                  background: !cfgIsServing ? '#FFF1F2' : 'var(--color-card)',
                  boxShadow: !cfgIsServing ? '4px 4px 0 0 var(--color-fg)' : 'none',
                  textAlign: 'left',
                  cursor: 'pointer',
                  transition: 'all 0.15s cubic-bezier(0.34, 1.56, 0.64, 1)',
                  display: 'flex',
                  flexDirection: 'column',
                  gap: 4,
                  transform: !cfgIsServing ? 'translate(-1px, -1px)' : 'none',
                }}
              >
                <div style={{ display: 'flex', alignItems: 'center', justifyContent: 'space-between', gap: 6 }}>
                  <span style={{ fontWeight: 800, fontSize: 'var(--fs-sm)', color: '#9F1239', display: 'inline-flex', alignItems: 'center', gap: 6 }}>
                    <AlertTriangle size={15} color="#E11D48" /> Nghỉ phục vụ
                  </span>
                  {!cfgIsServing && (
                    <span className="badge badge--pink" style={{ fontSize: 10, padding: '2px 6px' }}>
                      Đang chọn
                    </span>
                  )}
                </div>
                <span style={{ fontSize: 'var(--fs-xs)', color: 'var(--color-muted-fg)' }}>
                  Nghỉ lễ, hoạt động ngoại khóa hoặc nghỉ bếp
                </span>
              </button>
            </div>
          </div>

          {/* 2. Giờ chốt (Cutoff Time) */}
          <div>
            <div style={{ display: 'flex', alignItems: 'center', justifyContent: 'space-between', marginBottom: 8 }}>
              <label className="label" htmlFor="cfg-cutoff" style={{ marginBottom: 0, fontSize: 'var(--fs-xs)', letterSpacing: '0.05em', display: 'inline-flex', alignItems: 'center', gap: 6 }}>
                <Clock size={14} color="var(--color-accent)" /> 2. HẠN ĐĂNG KÝ / HỦY SUẤT ĂN
              </label>
              <span className="badge badge--amber" style={{ fontSize: '11px', padding: '2px 8px' }}>
                Mốc chuẩn: 08:15
              </span>
            </div>

            <div style={{ position: 'relative' }}>
              <input
                id="cfg-cutoff"
                type="time"
                min="06:00"
                max="11:00"
                className="input"
                value={cfgCutoffTime}
                onChange={(e) => setCfgCutoffTime(e.target.value)}
                style={{
                  fontWeight: 800,
                  fontSize: 'var(--fs-base)',
                  letterSpacing: '0.05em',
                  paddingLeft: 40,
                  border: '2px solid var(--color-fg)',
                }}
              />
              <Clock
                size={18}
                color="var(--color-accent)"
                style={{ position: 'absolute', left: 14, top: '50%', transform: 'translateY(-50%)', pointerEvents: 'none' }}
              />
            </div>

            {/* Quick preset chips */}
            <div style={{ display: 'flex', alignItems: 'center', gap: 8, marginTop: 10, flexWrap: 'wrap' }}>
              <span style={{ fontSize: 'var(--fs-xs)', fontWeight: 700, color: 'var(--color-muted-fg)' }}>Gợi ý nhanh:</span>
              {[
                { time: '08:00', label: '08:00' },
                { time: '08:15', label: '08:15 (Tân Lập A)' },
                { time: '08:30', label: '08:30' },
                { time: '09:00', label: '09:00' },
              ].map((preset) => (
                <button
                  key={preset.time}
                  type="button"
                  onClick={() => setCfgCutoffTime(preset.time)}
                  style={{
                    padding: '4px 12px',
                    borderRadius: 'var(--radius-full)',
                    border: '2px solid var(--color-fg)',
                    background: cfgCutoffTime === preset.time ? 'var(--color-tertiary)' : 'var(--color-card)',
                    boxShadow: cfgCutoffTime === preset.time ? '2px 2px 0 0 var(--color-fg)' : 'none',
                    fontWeight: 800,
                    fontSize: 'var(--fs-xs)',
                    cursor: 'pointer',
                    transition: 'all 0.15s ease',
                  }}
                >
                  {preset.label}
                </button>
              ))}
            </div>
          </div>

          {/* 3. Chọn Thực đơn tuần (thay vì chọn từng món ăn lẻ) */}
          {cfgIsServing ? (
            <div>
              <div style={{ display: 'flex', alignItems: 'center', justifyContent: 'space-between', marginBottom: 8, flexWrap: 'wrap', gap: 8 }}>
                <label className="label" htmlFor="cfg-template-select" style={{ marginBottom: 0, fontSize: 'var(--fs-xs)', letterSpacing: '0.05em', display: 'inline-flex', alignItems: 'center', gap: 6 }}>
                  <UtensilsCrossed size={14} color="var(--color-secondary)" /> 3. CHỌN BỘ THỰC ĐƠN TUẦN ÁP DỤNG
                </label>
                <button
                  type="button"
                  className="btn btn--secondary btn--sm"
                  onClick={() => {
                    setShowConfigModal(false);
                    setShowTemplateModal(true);
                  }}
                >
                  <Sparkles size={12} /> Thiết kế Thực đơn tuần
                </button>
              </div>

              <select
                id="cfg-template-select"
                className="select"
                value={cfgSelectedTemplateId}
                onChange={(e) => setCfgSelectedTemplateId(e.target.value ? Number(e.target.value) : '')}
                style={{ fontWeight: 700, marginBottom: 10, border: '2px solid var(--color-fg)' }}
              >
                <option value="">-- Giữ nguyên thực đơn hiện tại --</option>
                {menuTemplates.map((tpl) => (
                  <option key={tpl.id} value={tpl.id}>
                    {tpl.name}
                  </option>
                ))}
              </select>

              {/* Khung xem trước trực quan */}
              <div
                style={{
                  border: '2px solid var(--color-fg)',
                  borderRadius: 'var(--radius-md)',
                  boxShadow: '3px 3px 0 0 var(--color-fg)',
                  padding: '12px 14px',
                  background: '#FFFDF5',
                  fontSize: 'var(--fs-xs)',
                  display: 'flex',
                  flexDirection: 'column',
                  gap: 6,
                }}
              >
                {selectedConfigTemplate ? (
                  cfgApplyFullWeek ? (
                    <>
                      <div style={{ fontWeight: 800, color: 'var(--color-accent)' }}>
                        Xem trước cả tuần sẽ áp dụng từ &ldquo;{selectedConfigTemplate.name}&rdquo;:
                      </div>
                      {[1, 2, 3, 4, 5].map((dow) => {
                        const stdNames = selectedConfigTemplate.items
                          .filter((i) => i.dayOfWeek === dow && normalizeMealTypeNumber(i.mealType) === 1)
                          .map((i) => i.dishName);
                        return (
                          <div key={dow} style={{ lineHeight: 1.4 }}>
                            <strong>Thứ {dow + 1}:</strong> {stdNames.join(', ') || 'Chưa cấu hình món'}
                          </div>
                        );
                      })}
                      <div style={{ color: 'var(--color-muted-fg)', fontWeight: 600, marginTop: 2 }}>
                        ✓ Tự động đi kèm đầy đủ thực đơn <strong>Ăn chay</strong> và <strong>Chế độ đặc biệt</strong>.
                      </div>
                    </>
                  ) : (
                    (() => {
                      const preview = previewTemplateForDate(selectedConfigTemplate, selectedDate);
                      return (
                        <>
                          <div style={{ fontWeight: 800, color: 'var(--color-accent)' }}>
                            Xem trước món ngày {formatDateDDMMYYYY(selectedDate)} theo &ldquo;{selectedConfigTemplate.name}&rdquo;:
                          </div>
                          <div>
                            <strong>Tiêu chuẩn:</strong> {preview.standard.join(', ') || 'Chưa có món'}
                          </div>
                          <div>
                            <strong>Ăn chay:</strong> {preview.vegetarian.join(', ') || 'Chưa có món'}
                          </div>
                          <div>
                            <strong>Đặc biệt:</strong> {preview.special.join(', ') || 'Chưa có món'}
                          </div>
                        </>
                      );
                    })()
                  )
                ) : board?.menus ? (
                  <>
                    <div style={{ fontWeight: 800, color: '#059669' }}>
                      ✓ Thực đơn đang áp dụng cho ngày {formatDateDDMMYYYY(selectedDate)}:
                    </div>
                    <div>
                      <strong>Tiêu chuẩn:</strong> {board.menus.standard.map((d) => d.name).join(', ') || '—'}
                    </div>
                    <div>
                      <strong>Ăn chay:</strong> {board.menus.vegetarian.map((d) => d.name).join(', ') || '—'}
                    </div>
                    <div>
                      <strong>Đặc biệt:</strong> {board.menus.special.map((d) => d.name).join(', ') || '—'}
                    </div>
                  </>
                ) : (
                  <div style={{ color: 'var(--color-muted-fg)', fontWeight: 600 }}>
                    {board?.serviceRemarks
                      ? `Thực đơn hiện tại: ${board.serviceRemarks}`
                      : 'Chưa chọn bộ Thực đơn tuần. Hãy chọn một Thực đơn tuần ở trên để tự động áp dụng đủ 3 chế độ ăn.'}
                  </div>
                )}
              </div>
            </div>
          ) : (
            <div>
              <label className="label" htmlFor="cfg-remarks" style={{ fontSize: 'var(--fs-xs)' }}>
                3. LÝ DO NGHỈ PHỤC VỤ BÁN TRÚ (GỬI THÔNG BÁO PHỤ HUYNH)
              </label>
              <textarea
                id="cfg-remarks"
                className="input"
                rows={2}
                maxLength={250}
                placeholder="VD: Nhà trường tổ chức hoạt động ngoại khóa cả ngày..."
                value={cfgRemarks}
                onChange={(e) => setCfgRemarks(e.target.value)}
                style={{ resize: 'vertical', minHeight: 68, width: '100%', boxSizing: 'border-box', border: '2px solid var(--color-fg)', fontWeight: 600 }}
              />
            </div>
          )}

          {/* 4. Áp dụng cả tuần (Playful Sticker Card) */}
          <div
            onClick={() => setCfgApplyFullWeek(!cfgApplyFullWeek)}
            style={{
              display: 'flex',
              alignItems: 'center',
              gap: 14,
              padding: '12px 16px',
              background: cfgApplyFullWeek ? '#FEF3C7' : '#FFFDF5',
              borderRadius: 'var(--radius-md)',
              border: '2px solid var(--color-fg)',
              boxShadow: cfgApplyFullWeek ? '4px 4px 0 0 var(--color-fg)' : '2px 2px 0 0 var(--color-fg)',
              cursor: 'pointer',
              transition: 'all 0.15s cubic-bezier(0.34, 1.56, 0.64, 1)',
              userSelect: 'none',
            }}
          >
            <div
              style={{
                width: 40,
                height: 40,
                borderRadius: '50%',
                background: cfgApplyFullWeek ? 'var(--color-tertiary)' : 'var(--color-muted)',
                border: '2px solid var(--color-fg)',
                display: 'grid',
                placeItems: 'center',
                flexShrink: 0,
              }}
            >
              <CalendarRange size={20} color="var(--color-fg)" />
            </div>

            <div style={{ flex: 1 }}>
              <div style={{ fontWeight: 800, fontSize: 'var(--fs-sm)', color: 'var(--color-fg)' }}>
                Áp dụng cho toàn bộ tuần học đang chọn
              </div>
              <div style={{ fontSize: 'var(--fs-xs)', color: 'var(--color-muted-fg)', marginTop: 2 }}>
                Tự động áp Thực đơn tuần & giờ chốt cho các ngày học chưa khóa trong tuần (tự bỏ qua ngày nghỉ lễ)
              </div>
            </div>

            <span
              aria-hidden="true"
              style={{
                width: 24,
                height: 24,
                borderRadius: 6,
                border: '2px solid var(--color-fg)',
                background: cfgApplyFullWeek ? 'var(--color-accent)' : '#FFFFFF',
                color: '#FFFFFF',
                display: 'grid',
                placeItems: 'center',
                flexShrink: 0,
                boxShadow: cfgApplyFullWeek ? '2px 2px 0 0 var(--color-fg)' : 'none',
              }}
            >
              {cfgApplyFullWeek && <CheckCircle2 size={15} />}
            </span>
          </div>

          {/* 5. Candy Action Buttons */}
          <div style={{ display: 'flex', justifyContent: 'flex-end', gap: 12, paddingTop: 10, borderTop: '1.5px dashed var(--color-border)' }}>
            <button
              type="button"
              className="btn btn--secondary"
              onClick={() => setShowConfigModal(false)}
              disabled={actionLoading}
              style={{
                borderRadius: 'var(--radius-full)',
                border: '2px solid var(--color-fg)',
                boxShadow: '2px 2px 0 0 var(--color-fg)',
                fontWeight: 700,
                padding: '10px 20px',
              }}
            >
              Hủy
            </button>
            <button
              type="button"
              className="btn btn--primary"
              onClick={handleSaveConfig}
              disabled={actionLoading}
              style={{
                borderRadius: 'var(--radius-full)',
                border: '2px solid var(--color-fg)',
                boxShadow: '4px 4px 0 0 var(--color-fg)',
                fontWeight: 800,
                padding: '10px 24px',
                background: 'var(--color-accent)',
                color: '#FFFFFF',
                display: 'inline-flex',
                alignItems: 'center',
                gap: 8,
              }}
            >
              <CheckCircle2 size={18} />
              {actionLoading ? 'Đang lưu...' : 'Lưu cấu hình phục vụ'}
            </button>
          </div>
        </div>
      </GeometricModal>

      {/* ===================================================================== */}
      {/* MODAL 1: Confirm Lock Headcount                                       */}
      {/* ===================================================================== */}
      <GeometricModal
        open={showLockModal}
        onClose={() => setShowLockModal(false)}
        title={`Chốt số liệu Bán trú lúc ${effectiveCutoffLabel}`}
        badgeText="Khóa số liệu nhà bếp"
        badgeColor="amber"
        icon={<Lock size={24} color="var(--color-fg)" />}
        iconBg="var(--color-tertiary)"
        maxWidth="md"
      >
        <div>
          <div
            className="card"
            style={{
              background: '#FFFDF5',
              border: '2px solid var(--color-fg)',
              boxShadow: '3px 3px 0 0 var(--color-fg)',
              borderRadius: 'var(--radius-md)',
              padding: '16px 20px',
              marginBottom: 16,
              fontSize: 'var(--fs-sm)',
            }}
          >
            <div style={{ display: 'flex', justifyContent: 'space-between', marginBottom: 8, alignItems: 'center' }}>
              <span style={{ fontWeight: 600 }}>Tổng suất cần nấu ngày {formatDateDDMMYYYY(selectedDate)}:</span>
              <span className="badge badge--mint" style={{ fontSize: 'var(--fs-sm)', padding: '4px 10px', fontWeight: 800 }}>
                {board?.totalActiveToCook} suất
              </span>
            </div>
            <div style={{ display: 'flex', justifyContent: 'space-between', alignItems: 'center', color: '#e11d48' }}>
              <span style={{ fontWeight: 600 }}>Tổng suất hủy hợp lệ:</span>
              <strong style={{ fontSize: 'var(--fs-sm)' }}>
                {(board?.totalAutoCancelled ?? 0) > 0 ? `-${board?.totalAutoCancelled}` : '0'} suất
              </strong>
            </div>
          </div>

          <p style={{ fontSize: 'var(--fs-sm)', color: 'var(--color-muted-fg)', marginBottom: 16, lineHeight: 1.5 }}>
            Sau khi bấm chốt, hệ thống sẽ <strong>khóa bảng số liệu</strong> ngày {formatDateDDMMYYYY(selectedDate)}. Các đơn nghỉ hoặc yêu cầu hủy gửi muộn sau {effectiveCutoffLabel} sẽ giữ nguyên đăng ký hiện tại theo quy định.
          </p>

          <div className="field" style={{ marginBottom: 20 }}>
            <label className="label" htmlFor="lock-notes">Ghi chú gửi Nhà bếp / Kế toán (tối đa 250 ký tự):</label>
            <textarea
              id="lock-notes"
              className="input"
              maxLength={250}
              value={lockNotes}
              onChange={(e) => setLockNotes(e.target.value)}
              rows={3}
              style={{ minHeight: 80, resize: 'vertical' }}
            />
          </div>

          <div style={{ display: 'flex', justifyContent: 'flex-end', gap: 10 }}>
            <button
              type="button"
              className="btn btn--secondary"
              onClick={() => setShowLockModal(false)}
              disabled={actionLoading}
              style={{
                borderRadius: 'var(--radius-full)',
                border: '2px solid var(--color-fg)',
                boxShadow: '2px 2px 0 0 var(--color-fg)',
                fontWeight: 700,
                padding: '10px 20px',
              }}
            >
              Hủy bỏ
            </button>
            <button
              type="button"
              className="btn btn--primary"
              onClick={handleLockDay}
              disabled={actionLoading}
              style={{
                borderRadius: 'var(--radius-full)',
                border: '2px solid var(--color-fg)',
                boxShadow: '4px 4px 0 0 var(--color-fg)',
                fontWeight: 800,
                padding: '10px 24px',
                background: 'var(--color-accent)',
                color: '#FFFFFF',
                display: 'inline-flex',
                alignItems: 'center',
                gap: 8,
              }}
            >
              <Lock size={16} />
              {actionLoading ? 'Đang chốt...' : 'Xác nhận chốt số liệu'}
            </button>
          </div>
        </div>
      </GeometricModal>

      {/* Modal Mở khóa ngày bán trú (SSAWP-134) */}
      <GeometricModal
        open={showUnlockModal}
        onClose={() => setShowUnlockModal(false)}
        title={`Mở khóa ngày bán trú (${formatDateDDMMYYYY(selectedDate)})`}
        maxWidth="sm"
      >
        <div style={{ display: 'flex', flexDirection: 'column', gap: 14 }}>
          <div style={{ padding: 12, borderRadius: 12, background: '#FEF3C7', border: '1.5px solid #F59E0B', fontSize: 'var(--fs-xs)', color: '#92400E', lineHeight: 1.5 }}>
            Thao tác mở khóa sẽ được ghi lại trong <strong>Nhật ký hệ thống (Audit Log)</strong> kèm người thực hiện và lý do.
          </div>

          <div>
            <label style={{ display: 'block', fontWeight: 800, fontSize: 'var(--fs-xs)', marginBottom: 6 }}>
              Lý do mở khóa *
            </label>
            <textarea
              className="input"
              rows={3}
              maxLength={250}
              value={unlockReason}
              onChange={(e) => setUnlockReason(e.target.value)}
              placeholder="Ví dụ: Bấm nhầm khóa sớm, cần mở lại để điều chỉnh suất ăn..."
              style={{ width: '100%' }}
            />
          </div>

          <div style={{ display: 'flex', justifyContent: 'flex-end', gap: 10 }}>
            <button
              type="button"
              className="btn btn--secondary"
              onClick={() => setShowUnlockModal(false)}
              disabled={actionLoading}
            >
              Hủy
            </button>
            <button
              type="button"
              className="btn btn--primary"
              onClick={handleUnlockDay}
              disabled={actionLoading}
            >
              {actionLoading ? 'Đang mở khóa...' : 'Xác nhận mở khóa'}
            </button>
          </div>
        </div>
      </GeometricModal>

      <DishLibraryModal
        open={showDishLibrary}
        onClose={() => setShowDishLibrary(false)}
        onChanged={() => {
          loadMenuTemplates();
          fetchBoard(selectedDate);
        }}
      />

      <WeeklyMenuTemplateModal
        open={showTemplateModal}
        onClose={() => setShowTemplateModal(false)}
        defaultDate={selectedDate}
        onApplied={() => {
          loadMenuTemplates();
          fetchBoard(selectedDate);
        }}
      />

      {/* MODAL GỬI THÔNG BÁO MỞ KỲ ĐĂNG KÝ BÁN TRÚ (25–28 CUỐI THÁNG) */}
      <GeometricModal
        open={showNotifyWindowModal}
        onClose={() => setShowNotifyWindowModal(false)}
        title="Thông báo Mở Kỳ Đăng Ký Bán Trú (25–28)"
        subtitle="Gửi chuông thông báo tới toàn bộ Phụ huynh — khi bấm vào sẽ hiển thị trong Sổ liên lạc điện tử kèm nút chuyển sang form đăng ký"
        badgeText="Chuông thông báo → Sổ liên lạc"
        badgeColor="amber"
        icon={<Bell size={22} color="var(--color-fg)" />}
        iconBg="#FEF3C7"
        maxWidth="md"
      >
        <div style={{ display: 'flex', flexDirection: 'column', gap: 14 }}>
          <div className="field">
            <label className="label" htmlFor="notify-target-month" style={{ fontWeight: 800 }}>
              THÁNG ĐĂNG KÝ BÁN TRÚ ÁP DỤNG
            </label>
            <input
              id="notify-target-month"
              type="month"
              className="input"
              value={notifyTargetMonth}
              onChange={(e) => setNotifyTargetMonth(e.target.value)}
              style={{ border: '2px solid var(--color-fg)', fontWeight: 700 }}
            />
          </div>

          <div
            style={{
              padding: '12px 14px',
              borderRadius: 'var(--radius-md)',
              border: '2px solid var(--color-fg)',
              background: '#FFFBEB',
              fontSize: 'var(--fs-xs)',
              lineHeight: 1.55,
            }}
          >
            <div style={{ fontWeight: 800, marginBottom: 4, color: 'var(--color-fg)' }}>
              Nội dung mặc định sẽ gửi tới Phụ huynh:
            </div>
            <div>
              Kính gửi Quý Phụ huynh, kỳ đăng ký suất ăn bán trú tháng{' '}
              <strong>
                {notifyTargetMonth ? `${notifyTargetMonth.slice(5, 7)}/${notifyTargetMonth.slice(0, 4)}` : 'kế tiếp'}
              </strong>{' '}
              diễn ra từ ngày <strong>25 đến ngày 28 cuối tháng</strong>. Phụ huynh vui lòng mở Sổ liên lạc điện tử để chuyển sang đơn đăng ký chọn ngày ăn, chế độ ăn và khai báo dị ứng (nếu có). Lưu ý: Mốc{' '}
              <strong>{LUNCH_CUTOFF_LABEL}</strong> là hạn cuối để thay đổi hoặc hủy suất ăn ngày hôm đó.
            </div>
          </div>

          <div className="field">
            <div style={{ display: 'flex', justifyContent: 'space-between', alignItems: 'center' }}>
              <label className="label" htmlFor="notify-custom-msg" style={{ fontWeight: 800 }}>
                GHI CHÚ BỔ SUNG (TÙY CHỌN)
              </label>
              <span style={{ fontSize: 'var(--fs-xs)', color: 'var(--color-muted-fg)' }}>
                {notifyCustomMessage.length}/300
              </span>
            </div>
            <textarea
              id="notify-custom-msg"
              className="input"
              rows={3}
              maxLength={300}
              placeholder="VD: Đề nghị phụ huynh hoàn tất đăng ký trước 17h00 ngày 28 để nhà trường chốt danh sách..."
              value={notifyCustomMessage}
              onChange={(e) => setNotifyCustomMessage(e.target.value)}
              style={{ width: '100%', border: '2px solid var(--color-fg)' }}
            />
          </div>

          <div style={{ display: 'flex', justifyContent: 'flex-end', gap: 10 }}>
            <button
              type="button"
              className="btn btn--secondary"
              onClick={() => setShowNotifyWindowModal(false)}
              disabled={sendingWindowNotify}
            >
              Đóng
            </button>
            <button
              type="button"
              className="btn btn--primary"
              onClick={handleSendRegistrationWindowNotice}
              disabled={sendingWindowNotify}
            >
              {sendingWindowNotify ? 'Đang gửi...' : 'Gửi thông báo tới Phụ huynh'}
            </button>
          </div>
        </div>
      </GeometricModal>
    </div>
  );
}
