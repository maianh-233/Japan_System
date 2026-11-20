/**
 * Realtime (SignalR — hub /hubs/realtime) cho chế độ API: lời mời dạy thay, kết quả duyệt đơn, HV vừa quét QR.
 * Mở 1 kết nối sau khi đăng nhập, tự nối lại; nhận sự kiện → làm mới store + thêm chuông thông báo của cổng.
 * Server chỉ đẩy xuống (client không gọi method nào). Token gửi qua query vì WebSocket không gửi được header.
 */
import { HubConnectionBuilder, LogLevel, type HubConnection } from '@microsoft/signalr';
import { getAccessToken, refreshSession as refreshToken } from '@/lib/api/http';
import { fromGuid } from '@/lib/api/ids';
import { useNotificationStore } from '@/features/notifications/notificationStore';
import { toNotification, type ServerNotification } from '@/features/notifications/notificationApi';
import type { Portal } from '@/types/auth';
import { loadLearningPortal, loadStaffRequests, loadTeacherPortal, refreshSession } from './portalApi';

let connection: HubConnection | null = null;

interface InvitePayload {
  sessionId: string;
  classCode: string;
  toTeacherName: string;
  session?: { no: number; date: string; startTime: string };
}

const when = (p: InvitePayload) => (p.session ? ` · buổi ${p.session.no} ngày ${p.session.date.split('-').reverse().join('/')} ${p.session.startTime}` : '');

export async function startRealtime(portal: Portal, ownerId?: string) {
  await stopRealtime();
  const bell = (title: string, description: string, tone: 'info' | 'success' | 'warning' = 'info') =>
    useNotificationStore.getState().add(portal, { title, description, tone });
  // Làm mới dữ liệu theo cổng (gộp nhiều sự kiện liên tiếp thành 1 lần tải)
  let timer: ReturnType<typeof setTimeout> | undefined;
  const reload = () => {
    clearTimeout(timer);
    timer = setTimeout(() => {
      const job =
        portal === 'teacher' ? loadTeacherPortal() : portal === 'staff' ? loadStaffRequests() : loadLearningPortal(ownerId, portal === 'parent');
      void job.catch(() => undefined);
    }, 300);
  };

  const conn = new HubConnectionBuilder()
    .withUrl('/hubs/realtime', {
      accessTokenFactory: async () => getAccessToken() ?? ((await refreshToken()) ? (getAccessToken() ?? '') : ''),
    })
    .withAutomaticReconnect()
    .configureLogging(LogLevel.Warning)
    .build();

  conn.on('substitute-invite.created', (p: InvitePayload) => {
    bell('Lời mời dạy thay mới', `Lớp ${p.classCode}${when(p)} — vào "Lời mời dạy thay" để nhận.`);
    reload();
  });
  conn.on('substitute-invite.accepted', (p: InvitePayload) => {
    bell('Đã có GV nhận dạy thay', `${p.toTeacherName} dạy thay lớp ${p.classCode}${when(p)}.`, 'success');
    reload();
  });
  conn.on('substitute-invite.declined', (p: InvitePayload) => {
    bell('GV từ chối lời mời dạy thay', `${p.toTeacherName} · lớp ${p.classCode}${when(p)}.`, 'warning');
    reload();
  });
  conn.on('substitute-invite.closed', (p: InvitePayload) => {
    bell('Lời mời dạy thay đã hết hiệu lực', `Lớp ${p.classCode}${when(p)}.`);
    reload();
  });
  conn.on('substitute-invite.expired', (p: InvitePayload) => {
    bell('Lời mời dạy thay quá hạn', `Chưa ai nhận lớp ${p.classCode}${when(p)} — chọn cách khác (mời GV khác / hủy & xếp bù).`, 'warning');
    reload();
  });
  // Kết quả duyệt đơn: server đã gửi thông báo chuông lưu DB (notification.created) → ở đây chỉ làm mới dữ liệu
  conn.on('cancel-request.decided', () => reload());
  conn.on('leave-request.decided', () => reload());
  // Chuông (user story 10, TB-03): thông báo lưu trên server — đơn mới, kết quả duyệt, vấn đề sau buổi, HV quan tâm lớp kỳ sau…
  conn.on('notification.created', (n: ServerNotification) => {
    useNotificationStore.getState().receive(portal, toNotification(n));
    // Đơn mới / quan tâm lớp kỳ sau → tải lại danh sách đơn của nhân viên
    if (portal === 'staff') reload();
  });
  // GV đang mở QR → điểm danh cập nhật ngay khi HV quét
  conn.on('attendance.scanned', (p: { sessionId: string }) => void refreshSession(fromGuid(p.sessionId), portal === 'teacher').catch(() => undefined));

  connection = conn;
  try {
    await conn.start();
  } catch {
    // Không nối được realtime → ứng dụng vẫn chạy, dữ liệu cập nhật khi tải lại trang
  }
}

export async function stopRealtime() {
  const c = connection;
  connection = null;
  if (c) await c.stop().catch(() => undefined);
}
