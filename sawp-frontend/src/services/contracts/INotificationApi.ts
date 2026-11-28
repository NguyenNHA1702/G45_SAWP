import type { PaginatedResult } from '../../types/common.types';
import type {
  AppNotification,
  BroadcastHistoryItem,
  BroadcastNotificationPayload,
  BroadcastNotificationResult,
} from '../../types/notification.types';

export interface INotificationApi {
  getUnreadCount(): Promise<number>;
  getPaged(page: number, size: number): Promise<PaginatedResult<AppNotification>>;
  markRead(id: number): Promise<void>;
  markAllRead(): Promise<void>;
  broadcast(payload: BroadcastNotificationPayload): Promise<{ result: BroadcastNotificationResult; message?: string | null }>;
  getBroadcastHistory(limit?: number): Promise<BroadcastHistoryItem[]>;
}
