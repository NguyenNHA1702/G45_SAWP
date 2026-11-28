import { apiClient } from '../apiClient';
import type { ApiResponse, PaginatedResult } from '../../types/common.types';
import type { AuditLogDto, AuditLogQuery } from '../../types/system.types';
import type { ISystemApi } from '../contracts/ISystemApi';

export const systemApi: ISystemApi = {
  getAuditLogs: async (query: AuditLogQuery): Promise<ApiResponse<PaginatedResult<AuditLogDto>>> => {
    const res = await apiClient.get<ApiResponse<PaginatedResult<AuditLogDto>>>('/system/audit-logs', {
      params: {
        pageIndex: query.pageIndex ?? 1,
        pageSize: query.pageSize ?? 20,
        ...(query.action ? { action: query.action } : {}),
        ...(query.entityName ? { entityName: query.entityName } : {}),
        ...(query.search ? { search: query.search } : {}),
        ...(query.fromDate ? { fromDate: query.fromDate } : {}),
        ...(query.toDate ? { toDate: query.toDate } : {}),
      },
    });
    return res.data;
  },
};
