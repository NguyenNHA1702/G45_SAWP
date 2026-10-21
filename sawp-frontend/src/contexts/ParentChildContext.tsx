import { createContext, useContext, useEffect, useState, useMemo, type ReactNode } from 'react';
import type { ChildSummary } from '../types/parent.types';
import { parentApi } from '../services/implementations/parentApi';
import { USER_KEY } from '../services/apiClient';

interface ParentChildContextValue {
  children: ChildSummary[];
  activeChild: ChildSummary | null;
  activeChildId: number | null;
  selectChild: (studentId: number) => void;
  loading: boolean;
  refreshChildren: () => Promise<void>;
}

const ParentChildContext = createContext<ParentChildContextValue | undefined>(undefined);

const STORAGE_KEY = 'sawp_active_child_id';

interface ParentChildProviderProps {
  children: ReactNode;
  userId?: number;
  userRoles?: string[];
}

export function ParentChildProvider({ children, userId, userRoles }: ParentChildProviderProps) {
  const [childrenList, setChildrenList] = useState<ChildSummary[]>([]);
  const [activeChildId, setActiveChildId] = useState<number | null>(() => {
    const saved = localStorage.getItem(STORAGE_KEY);
    return saved ? Number(saved) : null;
  });
  const [loading, setLoading] = useState(false);

  // Kiểm tra user có role Parent không
  const isParent = useMemo(() => {
    if (userRoles) {
      return userRoles.includes('Parent');
    }
    try {
      const userJson = localStorage.getItem(USER_KEY);
      if (!userJson) return false;
      const user = JSON.parse(userJson);
      return Array.isArray(user.roles) && user.roles.includes('Parent');
    } catch {
      return false;
    }
  }, [userRoles, userId]);

  const fetchChildren = async () => {
    if (!isParent) {
      setChildrenList([]);
      return;
    }
    try {
      setLoading(true);
      const data = await parentApi.getMyChildren();
      const list = Array.isArray(data) ? data : [];
      setChildrenList(list);

      if (list.length > 0) {
        // Nếu chưa chọn bé nào hoặc bé đã chọn không còn trong danh sách -> chọn bé đầu tiên
        if (!activeChildId || !list.some((c) => c.studentId === activeChildId)) {
          const firstId = list[0].studentId;
          setActiveChildId(firstId);
          localStorage.setItem(STORAGE_KEY, String(firstId));
        }
      }
    } catch (err) {
      console.error('Failed to load children for parent:', err);
    } finally {
      setLoading(false);
    }
  };

  useEffect(() => {
    fetchChildren();
  }, [isParent, userId]);

  const selectChild = (studentId: number) => {
    setActiveChildId(studentId);
    localStorage.setItem(STORAGE_KEY, String(studentId));
  };

  const activeChild = useMemo(() => {
    return childrenList.find((c) => c.studentId === activeChildId) ?? childrenList[0] ?? null;
  }, [childrenList, activeChildId]);

  return (
    <ParentChildContext.Provider
      value={{
        children: childrenList,
        activeChild,
        activeChildId: activeChild?.studentId ?? null,
        selectChild,
        loading,
        refreshChildren: fetchChildren,
      }}
    >
      {children}
    </ParentChildContext.Provider>
  );
}

export function useParentChild(): ParentChildContextValue {
  const context = useContext(ParentChildContext);
  if (!context) {
    throw new Error('useParentChild must be used within a ParentChildProvider');
  }
  return context;
}
