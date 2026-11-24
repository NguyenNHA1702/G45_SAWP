import { apiClient } from '../apiClient';
import { unwrap } from '../../utils/apiError';
import type { ApiResponse } from '../../types/common.types';
import type {
  AccountRequestDecisionDto,
  AccountRequestDecisionResultDto,
  AccountRequestDto,
  AccountRequestItemDto,
  AccountRequestRowCheckDto,
  GuardianWithoutAccountDto,
  SaveAccountRequestDto,
  SaveAccountRequestResultDto,
} from '../../types/accountRequest.types';

/** UC-67 (Office Staff) and UC-68 (System Administrator). Errors are thrown with the API's message (asApiError). */
export const accountRequestApi = {
  guardiansWithoutAccount: (search?: string): Promise<ApiResponse<GuardianWithoutAccountDto[]>> =>
    unwrap(apiClient.get('/account-requests/guardians-without-account', { params: { search } })),

  checkRows: (guardianIds: number[]): Promise<ApiResponse<AccountRequestRowCheckDto[]>> =>
    unwrap(apiClient.post('/account-requests/check', { guardianIds })),

  create: (payload: SaveAccountRequestDto): Promise<ApiResponse<SaveAccountRequestResultDto>> =>
    unwrap(apiClient.post('/account-requests', payload)),

  updateDraft: (id: number, payload: SaveAccountRequestDto): Promise<ApiResponse<SaveAccountRequestResultDto>> =>
    unwrap(apiClient.put(`/account-requests/${id}`, payload)),

  mine: (): Promise<ApiResponse<AccountRequestDto[]>> => unwrap(apiClient.get('/account-requests/mine')),

  get: (id: number): Promise<ApiResponse<AccountRequestDto>> => unwrap(apiClient.get(`/account-requests/${id}`)),

  resubmit: (id: number, itemIds: number[]): Promise<ApiResponse<SaveAccountRequestResultDto>> =>
    unwrap(apiClient.post(`/account-requests/${id}/resubmit`, { itemIds })),

  closeRows: (id: number, itemIds: number[], note: string): Promise<ApiResponse<AccountRequestDto>> =>
    unwrap(apiClient.post(`/account-requests/${id}/close-rows`, { itemIds, note })),

  resendActivation: (itemId: number, email?: string): Promise<ApiResponse<AccountRequestItemDto>> =>
    unwrap(apiClient.post(`/account-requests/items/${itemId}/resend-activation`, { email })),

  review: (status?: string): Promise<ApiResponse<AccountRequestDto[]>> =>
    unwrap(apiClient.get('/account-requests/review', { params: { status } })),

  decide: (id: number, decision: AccountRequestDecisionDto): Promise<ApiResponse<AccountRequestDecisionResultDto>> =>
    unwrap(apiClient.post(`/account-requests/${id}/decisions`, decision)),
};
