export interface AuditLogDto {
  id: number;
  userId: number | null;
  username: string | null;
  userFullName: string | null;
  action: string;
  entityName: string;
  entityId: string | null;
  oldValues: string | null;
  newValues: string | null;
  ipAddress: string | null;
  timestamp: string;
}

export interface AuditLogQuery {
  pageIndex?: number;
  pageSize?: number;
  action?: string;
  entityName?: string;
  search?: string;
}
