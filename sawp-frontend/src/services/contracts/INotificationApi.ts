import type { PaginatedResult } from '../../types/common.types';
import type { AppNotification } from '../../types/notification.types';

export interface INotificationApi {
  getUnreadCount(): Promise<number>;
  getPaged(page: number, size: number): Promise<PaginatedResult<AppNotification>>;
  markRead(id: number): Promise<void>;
  markAllRead(): Promise<void>;
}
