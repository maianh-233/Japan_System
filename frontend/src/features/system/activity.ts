import { useAuthStore } from '@/stores/authStore';
import type { AuditCategory, AuditSeverity } from './types';
import { useSystemStore, type Actor } from './systemStore';
import { useNotificationStore, type NotificationDraft } from '@/features/notifications/notificationStore';
import type { Portal } from '@/types/auth';

interface ActivityNotification extends NotificationDraft {
  portal: Portal;
}

interface ActivityEvent {
  category: AuditCategory;
  severity: AuditSeverity;
  action: string;
  target: string;
  detail?: string;
  changes?: { field: string; before?: string; after?: string }[];
  branchId?: string;
  actor?: Actor;
  notifications?: ActivityNotification[];
}

export function recordActivity(event: ActivityEvent): void {
  const user = useAuthStore.getState().user;
  const actor = event.actor ?? { name: user?.fullName ?? 'Hệ thống', role: user?.roleName ?? 'Hệ thống' };
  const { category, severity, action, target, detail, changes, branchId, notifications } = event;

  useSystemStore.getState().audit(actor, { category, severity, action, target, detail, changes, branchId });
  notifications?.forEach(({ portal, ...notification }) => useNotificationStore.getState().add(portal, notification));
}
