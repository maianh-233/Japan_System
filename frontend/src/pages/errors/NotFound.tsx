import { MapPinOff } from 'lucide-react';
import { useNavigate } from 'react-router-dom';
import { Button, EmptyState } from '@/components/common';

export default function NotFound() {
  const navigate = useNavigate();
  return (
    <main className="flex min-h-dvh items-center justify-center bg-page p-4">
      <div className="w-full max-w-md rounded-[var(--radius-card)] border border-line bg-surface">
        <EmptyState
          icon={MapPinOff}
          title="Không tìm thấy trang"
          description="Đường dẫn không tồn tại hoặc đã bị thay đổi."
          action={<Button action="back" variant="solid" tone="primary" onClick={() => navigate('/', { replace: true })}>Về trang chủ</Button>}
        />
      </div>
    </main>
  );
}
