import { apiClient } from '../apiClient';

export interface ClassMealSummary {
  classId: number;
  className: string;
  grade: number;
  initialRegistered: number;
  cancelledCount: number;
  actualCount: number;
}

export interface MealAuditLog {
  studentId: number;
  studentCode: string;
  studentName: string;
  className: string;
  changeType: 'AutoCancelled' | 'Restored' | 'LateAdded' | string;
  reason: string;
  timestamp: string;
  isBeforeCutoff: boolean;
}

export interface DailyMealBoard {
  date: string;
  isLocked: boolean;
  lockedAt?: string;
  cutoffTime: string;
  totalInitialRegistered: number;
  totalAutoCancelled: number;
  totalActiveToCook: number;
  totalAnomalies: number;
  classBreakdowns: ClassMealSummary[];
  mealChangeLogs: MealAuditLog[];
}

export interface LockMealDayPayload {
  date: string;
  notes?: string;
}

export interface RestoreMealPayload {
  studentId: number;
  date: string;
  reason: string;
}

export const lunchApi = {
  getDailyMealBoard: async (date?: string): Promise<DailyMealBoard> => {
    const res = await apiClient.get<DailyMealBoard>('/lunch/live-board', {
      params: date ? { date } : {}
    });
    return res.data;
  },

  lockDailyMeal: async (payload: LockMealDayPayload): Promise<boolean> => {
    const res = await apiClient.post<boolean>('/lunch/lock-day', payload);
    return res.data;
  },

  restoreMeal: async (payload: RestoreMealPayload): Promise<boolean> => {
    const res = await apiClient.post<boolean>('/lunch/restore-meal', payload);
    return res.data;
  }
};
