import { apiClient } from '../apiClient';
import { unwrap, unwrapData } from '../../utils/apiError';
import type { INotificationApi } from '../contracts/INotificationApi';
import type { PaginatedResult } from '../../types/common.types';
import type { AppNotification } from '../../types/notification.types';

export const notificationApi: INotificationApi = {
  getUnreadCount: async () => {
    const data = await unwrapData<{ count: number }>(apiClient.get('/notifications/unread-count'));
    return data.count;
  },
  getPaged: (page: number, size: number): Promise<PaginatedResult<AppNotification>> =>
    unwrapData(apiClient.get('/notifications', { params: { page, size } })),
  markRead: async (id: number) => {
    await unwrap(apiClient.patch(`/notifications/${id}/read`));
  },
  markAllRead: async () => {
    await unwrap(apiClient.patch('/notifications/read-all'));
  },
};
