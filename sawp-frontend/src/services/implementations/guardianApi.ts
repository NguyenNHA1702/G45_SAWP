import { apiClient } from '../apiClient';
import type { ApiResponse } from '../../types/common.types';
import type {
  ChildDashboardDto,
  ChildLinkRequestItemDto,
  ChildLinkRequestResultDto,
  LinkedChildSummaryDto,
  SubmitChildLinkDto,
  VerifyChildLinkDto,
} from '../../types/guardian.types';

export const guardianApi = {
  // Parent submit link request
  async submitChildLink(payload: SubmitChildLinkDto): Promise<ApiResponse<ChildLinkRequestResultDto>> {
    const res = await apiClient.post<ApiResponse<ChildLinkRequestResultDto>>('/guardians/link-requests', payload);
    return res.data;
  },

  // OfficeStaff list link requests
  async getLinkRequests(status?: string): Promise<ApiResponse<ChildLinkRequestItemDto[]>> {
    const res = await apiClient.get<ApiResponse<ChildLinkRequestItemDto[]>>('/guardians/link-requests', {
      params: { status },
    });
    return res.data;
  },

  // OfficeStaff verify / approve / reject
  async verifyLinkRequest(id: number, payload: VerifyChildLinkDto): Promise<ApiResponse<boolean>> {
    const res = await apiClient.post<ApiResponse<boolean>>(`/guardians/link-requests/${id}/verify`, payload);
    return res.data;
  },

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
