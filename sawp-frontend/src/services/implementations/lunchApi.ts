import { apiClient } from '../apiClient';
import { unwrapData } from '../../utils/apiError';
import type {
  DailyMealBoard,
  LockMealDayPayload,
  MealHeadcount,
  RestoreMealPayload,
} from '../../types/lunch.types';

export const lunchApi = {
  getHeadcount: (date: string): Promise<MealHeadcount> =>
    unwrapData(apiClient.get('/lunch/headcount', { params: { date } })),

  getDailyMealBoard: async (date?: string): Promise<DailyMealBoard> => {
    const res = await apiClient.get<{ success: boolean; data: DailyMealBoard }>('/lunch/live-board', {
      params: date ? { date } : {}
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
  }
};
