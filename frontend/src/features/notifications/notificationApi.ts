/**
 * Chuông thông báo với backend (/api/v1/notifications — user story 10, TB-01 → TB-03).
 * Mỗi tài khoản chỉ thấy thông báo của mình; thông báo mới đến qua SignalR `notification.created` (xem features/portal/realtime.ts).
 */
import { api } from '@/lib/api/http';
import type { AppNotification, Portal } from '@/types/auth';
import { useNotificationStore } from './notificationStore';

export interface ServerNotification {
  id: string;
  title: string;
  description: string;
  createdAt: string;
  read: boolean;
  tone: AppNotification['tone'];
  link?: string | null;
}

export const toNotification = (n: ServerNotification): AppNotification => ({
  id: n.id,
  title: n.title,
  description: n.description,
  createdAt: n.createdAt,
  read: n.read,
  tone: n.tone,
  link: n.link ?? undefined,
});

/** 50 thông báo mới nhất + số chưa đọc → thay danh sách chuông của cổng */
export async function loadNotifications(portal: Portal) {
  const r = await api<{ items: ServerNotification[]; unread: number }>('/notifications');
  useNotificationStore.getState().setAll(portal, r.items.map(toNotification), r.unread);
}

export const markNotificationRead = (id: string) => api(`/notifications/${id}/read`, { method: 'POST' });

export const markAllNotificationsRead = () => api<{ marked: number }>('/notifications/read-all', { method: 'POST' });
