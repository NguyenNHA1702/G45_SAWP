import type { ApiResponse, PaginatedResult } from '../../types/common.types';
import type { AuditLogDto, AuditLogQuery } from '../../types/system.types';

export interface ISystemApi {
  getAuditLogs(query: AuditLogQuery): Promise<ApiResponse<PaginatedResult<AuditLogDto>>>;
}
