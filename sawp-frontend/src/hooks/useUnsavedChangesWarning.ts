import { useEffect } from 'react';

/** Cảnh báo khi đóng/refresh tab mà còn thay đổi chưa lưu */
export function useUnsavedChangesWarning(when: boolean) {
  useEffect(() => {
    if (!when) return;
    const handler = (e: BeforeUnloadEvent) => {
      e.preventDefault();
      e.returnValue = '';
    };
    window.addEventListener('beforeunload', handler);
    return () => window.removeEventListener('beforeunload', handler);
  }, [when]);
}
