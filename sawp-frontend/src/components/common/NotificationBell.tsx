import { Bell } from 'lucide-react';
import { useUnreadCount } from '../../hooks/useUnreadCount';

export function NotificationBell({ onOpen }: { onOpen: () => void }) {
  const { count } = useUnreadCount();
  return (
    <button
      id="notification-bell"
      type="button"
      className="btn btn--secondary btn--sm"
      onClick={onOpen}
      aria-label={`Thông báo, ${count} chưa đọc`}
    >
      <Bell size={18} aria-hidden="true" />
      {count > 0 && <span className="badge badge--pink">{count > 99 ? '99+' : count}</span>}
    </button>
  );
}
