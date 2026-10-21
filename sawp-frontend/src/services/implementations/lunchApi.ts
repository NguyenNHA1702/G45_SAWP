import { apiClient } from '../apiClient';
import { unwrapData } from '../../utils/apiError';
import type { MealHeadcount } from '../../types/lunch.types';

export const lunchApi = {
  getHeadcount: (date: string): Promise<MealHeadcount> =>
    unwrapData(apiClient.get('/lunch/headcount', { params: { date } })),
};
