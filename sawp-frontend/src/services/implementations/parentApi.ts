import { apiClient } from '../apiClient';
import { unwrapData } from '../../utils/apiError';
import type { ChildSummary } from '../../types/parent.types';

export const parentApi = {
  getMyChildren(): Promise<ChildSummary[]> {
    return unwrapData(apiClient.get('/parents/my-children'));
  },
};
