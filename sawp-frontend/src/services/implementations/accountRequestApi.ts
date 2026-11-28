import { apiClient } from '../apiClient';
import { unwrap } from '../../utils/apiError';
import type { ApiResponse, PaginatedResult } from '../../types/common.types';
import type {
  AccountRequestDecisionDto,
  AccountRequestDecisionResultDto,
  AccountRequestDto,
  AccountRequestItemDto,
  AccountRequestRowCheckDto,
  EmailChangeRequestDto,
  GuardianWithAccountDto,
  GuardianWithoutAccountDto,
  SaveAccountRequestDto,
  SaveAccountRequestResultDto,
} from '../../types/accountRequest.types';

/** UC-67 (Office Staff) and UC-68 (System Administrator). Errors are thrown with the API's message (asApiError). */
export const accountRequestApi = {
  guardiansWithoutAccount: (search?: string): Promise<ApiResponse<GuardianWithoutAccountDto[]>> =>
    unwrap(apiClient.get('/account-requests/guardians-without-account', { params: { search } })),

  guardiansWithoutAccountPaged: (search?: string, pageIndex = 1, pageSize = 25): Promise<ApiResponse<PaginatedResult<GuardianWithoutAccountDto>>> =>
    unwrap(apiClient.get('/account-requests/guardians-without-account/paged', {
      params: { ...(search ? { search } : {}), pageIndex, pageSize },
    })),

  checkRows: (guardianIds: number[]): Promise<ApiResponse<AccountRequestRowCheckDto[]>> =>
    unwrap(apiClient.post('/account-requests/check', { guardianIds })),

  create: (payload: SaveAccountRequestDto): Promise<ApiResponse<SaveAccountRequestResultDto>> =>
    unwrap(apiClient.post('/account-requests', payload)),

  updateDraft: (id: number, payload: SaveAccountRequestDto): Promise<ApiResponse<SaveAccountRequestResultDto>> =>
    unwrap(apiClient.put(`/account-requests/${id}`, payload)),

  mine: (): Promise<ApiResponse<AccountRequestDto[]>> => unwrap(apiClient.get('/account-requests/mine')),

  minePaged: (pageIndex = 1, pageSize = 15): Promise<ApiResponse<PaginatedResult<AccountRequestDto>>> =>
    unwrap(apiClient.get('/account-requests/mine/paged', { params: { pageIndex, pageSize } })),

  get: (id: number): Promise<ApiResponse<AccountRequestDto>> => unwrap(apiClient.get(`/account-requests/${id}`)),

  resubmit: (id: number, itemIds: number[]): Promise<ApiResponse<SaveAccountRequestResultDto>> =>
    unwrap(apiClient.post(`/account-requests/${id}/resubmit`, { itemIds })),

  closeRows: (id: number, itemIds: number[], note: string): Promise<ApiResponse<AccountRequestDto>> =>
    unwrap(apiClient.post(`/account-requests/${id}/close-rows`, { itemIds, note })),

  resendActivation: (itemId: number, email?: string): Promise<ApiResponse<AccountRequestItemDto>> =>
    unwrap(apiClient.post(`/account-requests/items/${itemId}/resend-activation`, { email })),

  review: (status?: string): Promise<ApiResponse<AccountRequestDto[]>> =>
    unwrap(apiClient.get('/account-requests/review', { params: { status } })),

  reviewPaged: (status?: string, search?: string, pageIndex = 1, pageSize = 15): Promise<ApiResponse<PaginatedResult<AccountRequestDto>>> =>
    unwrap(apiClient.get('/account-requests/review/paged', {
      params: {
        ...(status ? { status } : {}),
        ...(search ? { search } : {}),
        pageIndex,
        pageSize,
      },
    })),

  decide: (id: number, decision: AccountRequestDecisionDto): Promise<ApiResponse<AccountRequestDecisionResultDto>> =>
    unwrap(apiClient.post(`/account-requests/${id}/decisions`, decision)),

  guardiansWithAccountPaged: (search?: string, pageIndex = 1, pageSize = 25): Promise<ApiResponse<PaginatedResult<GuardianWithAccountDto>>> =>
    unwrap(apiClient.get('/account-requests/guardians-with-account/paged', {
      params: { ...(search ? { search } : {}), pageIndex, pageSize },
    })),

  createEmailChange: (payload: { guardianId: number; newEmail: string; reason: string }): Promise<ApiResponse<EmailChangeRequestDto>> =>
    unwrap(apiClient.post('/account-requests/email-change', payload)),

  myEmailChangePaged: (pageIndex = 1, pageSize = 20): Promise<ApiResponse<PaginatedResult<EmailChangeRequestDto>>> =>
    unwrap(apiClient.get('/account-requests/email-change/mine', { params: { pageIndex, pageSize } })),

  reviewEmailChangePaged: (status?: string, search?: string, pageIndex = 1, pageSize = 20): Promise<ApiResponse<PaginatedResult<EmailChangeRequestDto>>> =>
    unwrap(apiClient.get('/account-requests/email-change/review', {
      params: { ...(status ? { status } : {}), ...(search ? { search } : {}), pageIndex, pageSize },
    })),

  decideEmailChange: (id: number, decision: { approve: boolean; rejectReason?: string }): Promise<ApiResponse<EmailChangeRequestDto>> =>
    unwrap(apiClient.post(`/account-requests/email-change/${id}/decide`, decision)),

  resendEmailChangeLink: (id: number): Promise<ApiResponse<boolean>> =>
    unwrap(apiClient.post(`/account-requests/email-change/${id}/resend`)),
};
