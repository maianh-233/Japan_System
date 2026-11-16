import { create } from 'zustand';
import { MOCK_NOTIFICATIONS } from '@/mocks/data/notifications';
import type { AppNotification, Portal } from '@/types/auth';

export type NotificationDraft = Pick<AppNotification, 'title' | 'description' | 'tone'> & Pick<AppNotification, 'link'>;

interface NotificationState {
  items: Record<Portal, AppNotification[]>;
  add: (portal: Portal, notification: NotificationDraft) => void;
  markRead: (portal: Portal, id: string) => void;
  markAllRead: (portal: Portal) => void;
}

let sequence = 0;

export const useNotificationStore = create<NotificationState>((set) => ({
  items: MOCK_NOTIFICATIONS,
  add: (portal, notification) =>
    set((state) => ({
      items: {
        ...state.items,
        [portal]: [
          { ...notification, id: `N-${Date.now()}-${++sequence}`, createdAt: new Date().toISOString(), read: false },
          ...state.items[portal],
        ].slice(0, 50),
      },
    })),
  markRead: (portal, id) =>
    set((state) => ({
      items: {
        ...state.items,
        [portal]: state.items[portal].map((item) => (item.id === id ? { ...item, read: true } : item)),
      },
    })),
  markAllRead: (portal) =>
    set((state) => ({
      items: { ...state.items, [portal]: state.items[portal].map((item) => ({ ...item, read: true })) },
    })),
}));
