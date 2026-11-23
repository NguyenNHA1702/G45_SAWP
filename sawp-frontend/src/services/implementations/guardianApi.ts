import { apiClient } from '../apiClient';
import type { ApiResponse } from '../../types/common.types';
import type {
  ChildDashboardDto,
  LinkedChildSummaryDto,
} from '../../types/guardian.types';

export const guardianApi = {
  // Parent get linked children
  async getMyChildren(): Promise<ApiResponse<LinkedChildSummaryDto[]>> {
    const res = await apiClient.get<ApiResponse<LinkedChildSummaryDto[]>>('/guardians/my-children');
    return res.data;
  },

  // Parent get child dashboard
  async getChildDashboard(studentId: number): Promise<ApiResponse<ChildDashboardDto>> {
    const res = await apiClient.get<ApiResponse<ChildDashboardDto>>(`/guardians/my-children/${studentId}/dashboard`);
    return res.data;
  },
};
