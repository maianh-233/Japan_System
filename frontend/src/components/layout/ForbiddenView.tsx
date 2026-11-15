import { ShieldX } from 'lucide-react';
import { useNavigate } from 'react-router-dom';
import { Button, EmptyState } from '@/components/common';

/** Hiển thị khi người dùng mở trang không có quyền */
export function ForbiddenView() {
  const navigate = useNavigate();
  return (
    <div className="rounded-[var(--radius-card)] border border-line bg-surface">
      <EmptyState
        icon={ShieldX}
        tone="danger"
        title="Bạn không có quyền truy cập trang này"
        description="Liên hệ quản trị viên nếu bạn cần được cấp quyền."
        action={<Button action="back" variant="outline" onClick={() => navigate(-1)} />}
      />
    </div>
  );
}
