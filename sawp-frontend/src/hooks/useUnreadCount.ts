import { useCallback, useEffect, useState } from 'react';
import { notificationApi } from '../services/implementations/notificationApi';

const POLL_MS = 60_000;

export function useUnreadCount() {
  const [count, setCount] = useState(0);

  const refresh = useCallback(async () => {
    try {
      setCount(await notificationApi.getUnreadCount());
    } catch {
      /* im lặng – polling không nên bắn toast lỗi */
    }
  }, []);

  useEffect(() => {
    void refresh();
    const id = window.setInterval(() => {
      if (document.visibilityState === 'visible') void refresh();
    }, POLL_MS);
    const onVisible = () => { if (document.visibilityState === 'visible') void refresh(); };
    document.addEventListener('visibilitychange', onVisible);
    return () => {
      window.clearInterval(id);
      document.removeEventListener('visibilitychange', onVisible);
    };
  }, [refresh]);

  return { count, refresh };
}
