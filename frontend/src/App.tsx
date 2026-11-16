import { RouterProvider } from 'react-router-dom';
import { AppProvider } from '@/app/providers/AppProvider';
import { router } from '@/app/router';
import { useParentAlerts } from '@/features/portal/parentAlerts';
import { useWheelScrollX } from '@/hooks/useWheelScrollX';

/** Giả lập job phía server chạy ở mọi cổng: báo phụ huynh con đến lớp / chưa đến, gửi báo cáo tuần */
function BackgroundJobs() {
  useParentAlerts();
  // Thanh cuộn ẩn toàn hệ thống → lăn chuột cũng cuộn ngang được
  useWheelScrollX();
  return null;
}

export default function App() {
  return (
    <AppProvider>
      <BackgroundJobs />
      <RouterProvider router={router} future={{ v7_startTransition: true }} />
    </AppProvider>
  );
}
