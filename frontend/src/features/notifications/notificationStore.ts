import { create } from 'zustand';
import { USE_API } from '@/lib/api/config';
import { MOCK_NOTIFICATIONS } from '@/mocks/data/notifications';
import type { AppNotification, Portal } from '@/types/auth';

export type NotificationDraft = Pick<AppNotification, 'title' | 'description' | 'tone'> & Pick<AppNotification, 'link'>;

/**
 * Chuông thông báo theo cổng.
 * - Chế độ API: danh sách lấy từ server (`notificationApi.loadNotifications`), thông báo mới đến qua SignalR `notification.created`;
 *   đọc 1 / đọc hết gọi server. `add` chỉ còn dùng cho thông báo tạm tại chỗ (phần chưa có backend, lời mời dạy thay realtime).
 * - Chế độ mock: dữ liệu giả, lưu trong trình duyệt.
 */
interface NotificationState {
  items: Record<Portal, AppNotification[]>;
  /** Số chưa đọc theo server (có thể lớn hơn số đang hiện) — undefined = đếm trong danh sách */
  unread: Partial<Record<Portal, number>>;
  add: (portal: Portal, notification: NotificationDraft) => void;
  /** Thay toàn bộ danh sách bằng dữ liệu server */
  setAll: (portal: Portal, items: AppNotification[], unread: number) => void;
  /** Nhận 1 thông báo server (realtime) — bỏ qua nếu đã có */
  receive: (portal: Portal, notification: AppNotification) => void;
  markRead: (portal: Portal, id: string) => void;
  markAllRead: (portal: Portal) => void;
}

let sequence = 0;
const EMPTY: Record<Portal, AppNotification[]> = { staff: [], teacher: [], student: [], parent: [] };

/** Gọi server khi đọc (nạp động để store không phụ thuộc vòng với lớp API) */
const remote = (fn: (m: typeof import('./notificationApi')) => Promise<unknown>) =>
  USE_API ? void import('./notificationApi').then(fn).catch(() => undefined) : undefined;

export const useNotificationStore = create<NotificationState>((set) => ({
  items: USE_API ? EMPTY : MOCK_NOTIFICATIONS,
  unread: {},
  add: (portal, notification) =>
    set((state) => ({
      items: {
        ...state.items,
        [portal]: [
          { ...notification, id: `N-${Date.now()}-${++sequence}`, createdAt: new Date().toISOString(), read: false },
          ...state.items[portal],
        ].slice(0, 50),
      },
      unread: state.unread[portal] === undefined ? state.unread : { ...state.unread, [portal]: state.unread[portal]! + 1 },
    })),
  setAll: (portal, items, unread) => set((state) => ({ items: { ...state.items, [portal]: items }, unread: { ...state.unread, [portal]: unread } })),
  receive: (portal, notification) =>
    set((state) => {
      if (state.items[portal].some((n) => n.id === notification.id)) return state;
      return {
        items: { ...state.items, [portal]: [notification, ...state.items[portal]].slice(0, 50) },
        unread: state.unread[portal] === undefined || notification.read ? state.unread : { ...state.unread, [portal]: state.unread[portal]! + 1 },
      };
    }),
  markRead: (portal, id) => {
    set((state) => {
      const target = state.items[portal].find((item) => item.id === id);
      if (!target || target.read) return state;
      return {
        items: { ...state.items, [portal]: state.items[portal].map((item) => (item.id === id ? { ...item, read: true } : item)) },
        unread: state.unread[portal] === undefined ? state.unread : { ...state.unread, [portal]: Math.max(0, state.unread[portal]! - 1) },
      };
    });
    // Id tạm tại chỗ ("N-…") không có trên server
    if (!id.startsWith('N-')) remote((m) => m.markNotificationRead(id));
  },
  markAllRead: (portal) => {
    set((state) => ({
      items: { ...state.items, [portal]: state.items[portal].map((item) => ({ ...item, read: true })) },
      unread: state.unread[portal] === undefined ? state.unread : { ...state.unread, [portal]: 0 },
    }));
    remote((m) => m.markAllNotificationsRead());
  },
}));
