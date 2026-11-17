import { RouterProvider } from 'react-router-dom';
import { AppProvider } from '@/app/providers/AppProvider';
import { router } from '@/app/router';
import { useParentAlerts } from '@/features/portal/parentAlerts';
import { useWheelScrollX } from '@/hooks/useWheelScrollX';
import { useSessionRestore } from '@/features/auth/session';

/** Giả lập job phía server chạy ở mọi cổng: báo phụ huynh con đến lớp / chưa đến, gửi báo cáo tuần */
function BackgroundJobs() {
  useParentAlerts();
  // Thanh cuộn ẩn toàn hệ thống → lăn chuột cũng cuộn ngang được
  useWheelScrollX();
  return null;
}

export default function App() {
  // API thật: tải lại trang → khôi phục phiên từ cookie trước khi vẽ trang (tránh nháy về trang đăng nhập)
  const ready = useSessionRestore();
  return (
    <AppProvider>
      <BackgroundJobs />
      {ready ? (
        <RouterProvider router={router} future={{ v7_startTransition: true }} />
      ) : (
        <div className="flex min-h-dvh items-center justify-center text-ink-muted" role="status">
          Đang khôi phục phiên đăng nhập…
        </div>
      )}
    </AppProvider>
  );
}
