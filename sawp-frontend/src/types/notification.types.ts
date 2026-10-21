export interface AppNotification {
  id: number;
  type: string;
  title: string;
  content: string;
  isRead: boolean;
  linkUrl: string | null;
  createdAt: string;
}
