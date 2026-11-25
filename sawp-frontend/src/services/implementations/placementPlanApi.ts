import { apiClient } from '../apiClient';
import type { ApiResponse, PaginatedResult } from '../../types/common.types';
import type {
  GeneratePlacementPlanRequestDto,
  MoveStudentInPlanDto,
  PlacementPlanDetailDto,
  PlacementPlanSummaryDto,
  ReviewPlacementPlanDto,
  SwapStudentsInPlanDto,
} from '../../types/placement.types';

export const placementPlanApi = {
  async generateDraftPlan(
    payload: GeneratePlacementPlanRequestDto
  ): Promise<ApiResponse<PlacementPlanDetailDto>> {
    const res = await apiClient.post<ApiResponse<PlacementPlanDetailDto>>(
      '/placement-plans/generate',
      payload
    );
    return res.data;
  },

  async getPlanById(id: number): Promise<ApiResponse<PlacementPlanDetailDto>> {
    const res = await apiClient.get<ApiResponse<PlacementPlanDetailDto>>(
      `/placement-plans/${id}`
    );
    return res.data;
  },

  async getPlanHistory(
    schoolYearId?: number,
    grade?: number
  ): Promise<ApiResponse<PlacementPlanSummaryDto[]>> {
    const res = await apiClient.get<ApiResponse<PlacementPlanSummaryDto[]>>(
      '/placement-plans/history',
      {
        params: { schoolYearId, grade },
      }
    );
    return res.data;
  },

  async getPlanHistoryPaged(
    schoolYearId?: number,
    grade?: number,
    pageIndex = 1,
    pageSize = 20
  ): Promise<ApiResponse<PaginatedResult<PlacementPlanSummaryDto>>> {
    const res = await apiClient.get<ApiResponse<PaginatedResult<PlacementPlanSummaryDto>>>(
      '/placement-plans/history/paged',
      {
        params: { schoolYearId, grade, pageIndex, pageSize },
      }
    );
    return res.data;
  },

  async moveStudent(
    planId: number,
    payload: MoveStudentInPlanDto
  ): Promise<ApiResponse<PlacementPlanDetailDto>> {
    const res = await apiClient.post<ApiResponse<PlacementPlanDetailDto>>(
      `/placement-plans/${planId}/move-student`,
      payload
    );
    return res.data;
  },

  async swapStudents(
    planId: number,
    payload: SwapStudentsInPlanDto
  ): Promise<ApiResponse<PlacementPlanDetailDto>> {
    const res = await apiClient.post<ApiResponse<PlacementPlanDetailDto>>(
      `/placement-plans/${planId}/swap-students`,
      payload
    );
    return res.data;
  },

  async submitPlan(
    planId: number
  ): Promise<ApiResponse<PlacementPlanSummaryDto>> {
    const res = await apiClient.post<ApiResponse<PlacementPlanSummaryDto>>(
      `/placement-plans/${planId}/submit`
    );
    return res.data;
  },

  async reviewPlan(
    planId: number,
    payload: ReviewPlacementPlanDto
  ): Promise<ApiResponse<PlacementPlanSummaryDto>> {
    const res = await apiClient.post<ApiResponse<PlacementPlanSummaryDto>>(
      `/placement-plans/${planId}/review`,
      payload
    );
    return res.data;
  },
};
