export interface AppNotification {
  id: number;
  type: string;
  title: string;
  content: string;
  isRead: boolean;
  linkUrl: string | null;
  createdAt: string;
}

export interface BroadcastNotificationPayload {
  targetScope: 'All' | 'Role' | 'Grade' | 'Class';
  targetRole?: string;
  grade?: number;
  classId?: number;
  title: string;
  content: string;
  linkUrl?: string;
}

export interface BroadcastNotificationResult {
  recipientCount: number;
  referenceKey: string;
  sentAt: string;
}

export interface BroadcastHistoryItem {
  referenceKey: string;
  title: string;
  content: string;
  recipientCount: number;
  readCount: number;
  sentAt: string;
}
