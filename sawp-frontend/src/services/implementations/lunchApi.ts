import { apiClient } from '../apiClient';
import { unwrapData } from '../../utils/apiError';
import type {
  ApplyMenuTemplatePayload,
  ApplyMenuTemplateResult,
  BatchLunchRegistrationPayload,
  BatchLunchRegistrationResult,
  CancelLunchRegistrationPayload,
  DailyMealBoard,
  Dish,
  LockMealDayPayload,
  LunchPeriodReport,
  LunchServiceDayConfig,
  MealHeadcount,
  NotifyLunchRegistrationWindowPayload,
  NotifyLunchRegistrationWindowResult,
  RestoreMealPayload,
  StudentLunchSchedule,
  StudentMealProfile,
  UpdateLunchRegistrationMealTypePayload,
  UpsertDishPayload,
  UpsertLunchServiceDaysPayload,
  UpsertStudentMealProfilePayload,
  UpsertWeeklyMenuTemplatePayload,
  WeeklyMenuTemplate,
} from '../../types/lunch.types';

export const lunchApi = {
  getHeadcount: (date: string): Promise<MealHeadcount> =>
    unwrapData(apiClient.get('/lunch/headcount', { params: { date } })),

  getDailyMealBoard: async (date?: string, classId?: number): Promise<DailyMealBoard> => {
    const params: Record<string, string | number> = {};
    if (date) params.date = date;
    if (classId) params.classId = classId;
    const res = await apiClient.get<{ success: boolean; data: DailyMealBoard }>('/lunch/live-board', {
      params,
    });
    return res.data.data;
  },

  lockDailyMeal: async (payload: LockMealDayPayload): Promise<boolean> => {
    const res = await apiClient.post<{ success: boolean; data: boolean; message: string }>('/lunch/lock-day', payload);
    return res.data.data ?? res.data.success;
  },

  unlockDailyMeal: async (payload: { date: string; reason: string }): Promise<boolean> => {
    const res = await apiClient.post<{ success: boolean; data: boolean; message: string }>('/lunch/unlock-day', payload);
    return res.data.data ?? res.data.success;
  },

  restoreMeal: async (payload: RestoreMealPayload): Promise<boolean> => {
    const res = await apiClient.post<{ success: boolean; data: boolean; message: string }>('/lunch/restore-meal', payload);
    return res.data.data ?? res.data.success;
  },

  // UC-32: Lấy & cấu hình ngày phục vụ, hạn đăng ký/hủy, ghi chú/thực đơn
  getServiceDays: async (from: string, to: string): Promise<LunchServiceDayConfig[]> => {
    const res = await apiClient.get<{ success: boolean; data: LunchServiceDayConfig[] }>('/lunch/service-days', {
      params: { from, to },
    });
    return res.data.data ?? [];
  },

  upsertServiceDays: async (payload: UpsertLunchServiceDaysPayload): Promise<{ data: LunchServiceDayConfig[]; message: string; code?: string }> => {
    const res = await apiClient.put<{ success: boolean; data: LunchServiceDayConfig[]; message: string; code?: string }>(
      '/lunch/service-days',
      payload
    );
    return {
      data: res.data.data ?? [],
      message: res.data.message,
      code: res.data.code,
    };
  },

  // UC-31: Phụ huynh xem lịch và trạng thái đăng ký của con
  getStudentSchedule: async (studentId: number, month: string): Promise<StudentLunchSchedule> => {
    const res = await apiClient.get<{ success: boolean; data: StudentLunchSchedule }>('/lunch/student-schedule', {
      params: { studentId, month },
    });
    return res.data.data;
  },

  // UC-29: Phụ huynh đăng ký 1 hoặc nhiều ngày phục vụ còn mở
  registerBatch: async (payload: BatchLunchRegistrationPayload): Promise<{ data: BatchLunchRegistrationResult; message: string; code?: string }> => {
    const res = await apiClient.post<{ success: boolean; data: BatchLunchRegistrationResult; message: string; code?: string }>(
      '/lunch/register-batch',
      payload
    );
    return {
      data: res.data.data,
      message: res.data.message,
      code: res.data.code,
    };
  },

  // UC-30: Phụ huynh hủy đăng ký trực tiếp trước hạn hủy
  cancelRegistration: async (payload: CancelLunchRegistrationPayload): Promise<{ success: boolean; message: string; code?: string }> => {
    const res = await apiClient.post<{ success: boolean; data: boolean; message: string; code?: string }>(
      '/lunch/cancel',
      payload
    );
    return {
      success: res.data.success,
      message: res.data.message,
      code: res.data.code,
    };
  },

  updateRegistrationMealType: async (payload: UpdateLunchRegistrationMealTypePayload): Promise<{ success: boolean; message: string }> => {
    const res = await apiClient.put<{ success: boolean; data: boolean; message: string }>(
      '/lunch/registration',
      payload
    );
    return {
      success: res.data.success,
      message: res.data.message,
    };
  },

  getPeriodReport: async (from: string, to: string, classId?: number): Promise<LunchPeriodReport> => {
    const params: Record<string, string | number> = { from, to };
    if (classId) params.classId = classId;
    const res = await apiClient.get<{ success: boolean; data: LunchPeriodReport }>('/lunch/report', { params });
    return res.data.data;
  },

  notifyRegistrationWindow: async (
    payload: NotifyLunchRegistrationWindowPayload
  ): Promise<{ success: boolean; message: string; data: NotifyLunchRegistrationWindowResult }> => {
    const res = await apiClient.post<{ success: boolean; message: string; data: NotifyLunchRegistrationWindowResult }>(
      '/lunch/notify-registration-window',
      payload
    );
    return res.data;
  },

  // ---- Thư viện món ăn ----
  getDishes: async (params?: { category?: number; mealType?: number; search?: string; includeInactive?: boolean }): Promise<Dish[]> => {
    const res = await apiClient.get<{ success: boolean; data: Dish[] }>('/lunch/dishes', { params });
    return res.data.data ?? [];
  },

  createDish: async (payload: UpsertDishPayload): Promise<{ data: Dish; message: string }> => {
    const res = await apiClient.post<{ success: boolean; data: Dish; message: string }>('/lunch/dishes', payload);
    return { data: res.data.data, message: res.data.message };
  },

  updateDish: async (id: number, payload: UpsertDishPayload): Promise<{ data: Dish; message: string }> => {
    const res = await apiClient.put<{ success: boolean; data: Dish; message: string }>(`/lunch/dishes/${id}`, payload);
    return { data: res.data.data, message: res.data.message };
  },

  deleteDish: async (id: number): Promise<{ message: string }> => {
    const res = await apiClient.delete<{ success: boolean; message: string }>(`/lunch/dishes/${id}`);
    return { message: res.data.message };
  },

  // ---- Mẫu thực đơn tuần ----
  getMenuTemplates: async (includeInactive = false): Promise<WeeklyMenuTemplate[]> => {
    const res = await apiClient.get<{ success: boolean; data: WeeklyMenuTemplate[] }>('/lunch/menu-templates', {
      params: { includeInactive },
    });
    return res.data.data ?? [];
  },

  createMenuTemplate: async (payload: UpsertWeeklyMenuTemplatePayload): Promise<{ data: WeeklyMenuTemplate; message: string }> => {
    const res = await apiClient.post<{ success: boolean; data: WeeklyMenuTemplate; message: string }>('/lunch/menu-templates', payload);
    return { data: res.data.data, message: res.data.message };
  },

  updateMenuTemplate: async (id: number, payload: UpsertWeeklyMenuTemplatePayload): Promise<{ data: WeeklyMenuTemplate; message: string }> => {
    const res = await apiClient.put<{ success: boolean; data: WeeklyMenuTemplate; message: string }>(`/lunch/menu-templates/${id}`, payload);
    return { data: res.data.data, message: res.data.message };
  },

  deleteMenuTemplate: async (id: number): Promise<{ message: string }> => {
    const res = await apiClient.delete<{ success: boolean; message: string }>(`/lunch/menu-templates/${id}`);
    return { message: res.data.message };
  },

  applyMenuTemplate: async (id: number, payload: ApplyMenuTemplatePayload): Promise<{ data: ApplyMenuTemplateResult; message: string }> => {
    const res = await apiClient.post<{ success: boolean; data: ApplyMenuTemplateResult; message: string }>(
      `/lunch/menu-templates/${id}/apply`,
      payload
    );
    return { data: res.data.data, message: res.data.message };
  },

  // ---- Hồ sơ chế độ ăn học sinh ----
  getStudentMealProfile: async (studentId: number): Promise<StudentMealProfile> => {
    const res = await apiClient.get<{ success: boolean; data: StudentMealProfile }>(`/lunch/students/${studentId}/meal-profile`);
    return res.data.data;
  },

  upsertStudentMealProfile: async (
    studentId: number,
    payload: UpsertStudentMealProfilePayload
  ): Promise<{ data: StudentMealProfile; message: string }> => {
    const res = await apiClient.put<{ success: boolean; data: StudentMealProfile; message: string }>(
      `/lunch/students/${studentId}/meal-profile`,
      payload
    );
    return { data: res.data.data, message: res.data.message };
  },
};
