import { apiClient } from '../apiClient';
import { unwrapData } from '../../utils/apiError';
import type {
  BatchLunchRegistrationPayload,
  BatchLunchRegistrationResult,
  BoardingIncident,
  CancelLunchRegistrationPayload,
  CreateBoardingIncidentPayload,
  DailyMealBoard,
  LockMealDayPayload,
  LunchPeriodReport,
  LunchServiceDayConfig,
  MealHeadcount,
  MonthlyBoardingQuota,
  RestoreMealPayload,
  StudentLunchSchedule,
  UpdateLunchRegistrationMealTypePayload,
  UpsertLunchServiceDaysPayload,
  UpsertMonthlyBoardingQuotaPayload,
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

  getMonthlyQuota: async (month: string): Promise<MonthlyBoardingQuota> => {
    const res = await apiClient.get<{ success: boolean; data: MonthlyBoardingQuota }>('/lunch/monthly-quota', {
      params: { month },
    });
    return res.data.data;
  },

  upsertMonthlyQuota: async (payload: UpsertMonthlyBoardingQuotaPayload): Promise<MonthlyBoardingQuota> => {
    const res = await apiClient.put<{ success: boolean; data: MonthlyBoardingQuota }>('/lunch/monthly-quota', payload);
    return res.data.data;
  },

  getBoardingIncidents: async (
    date?: string,
    classId?: number,
    month?: string,
    studentId?: number,
    from?: string,
    to?: string
  ): Promise<BoardingIncident[]> => {
    const params: Record<string, string | number> = {};
    if (date) params.date = date;
    if (classId) params.classId = classId;
    if (month) params.month = month;
    if (studentId) params.studentId = studentId;
    if (from) params.from = from;
    if (to) params.to = to;
    const res = await apiClient.get<{ success: boolean; data: BoardingIncident[] }>('/lunch/incidents', { params });
    return res.data.data ?? [];
  },

  createBoardingIncident: async (payload: CreateBoardingIncidentPayload): Promise<BoardingIncident> => {
    const res = await apiClient.post<{ success: boolean; data: BoardingIncident }>('/lunch/incidents', payload);
    return res.data.data;
  },
};
