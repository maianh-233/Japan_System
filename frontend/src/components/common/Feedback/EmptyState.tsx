import type { ReactNode } from 'react';
import { CloudOff, Inbox, SearchX, type LucideIcon } from 'lucide-react';
import { cn } from '@/lib/cn';
import { Button } from '../Button/Button';
import { iconToneClasses, type Tone } from '../tones';

export interface EmptyStateProps {
  icon?: LucideIcon;
  title?: ReactNode;
  description?: ReactNode;
  /** Nút gợi ý hành động tiếp theo (vd: "Thêm học viên đầu tiên") */
  action?: ReactNode;
  tone?: Tone;
  /** compact: dùng trong card/bảng nhỏ */
  size?: 'compact' | 'default';
  className?: string;
}

/** Trạng thái rỗng — luôn nói rõ VÌ SAO rỗng và NÊN LÀM GÌ tiếp. */
export function EmptyState({
  icon: Icon = Inbox,
  title = 'Chưa có dữ liệu',
  description,
  action,
  tone = 'primary',
  size = 'default',
  className,
}: EmptyStateProps) {
  const compact = size === 'compact';
  return (
    <div
      className={cn(
        'flex flex-col items-center justify-center text-center',
        compact ? 'gap-2 px-4 py-6' : 'gap-3 px-6 py-12',
        className,
      )}
    >
      <span
        className={cn(
          'flex items-center justify-center rounded-full',
          compact ? 'h-10 w-10' : 'h-14 w-14',
          iconToneClasses[tone],
        )}
      >
        <Icon size={compact ? 20 : 28} aria-hidden />
      </span>
      <div className="flex max-w-sm flex-col gap-1">
        <p className={cn('font-semibold text-ink', compact ? 'text-sm' : 'text-base')}>{title}</p>
        {description && <p className="text-[13px] text-ink-muted">{description}</p>}
      </div>
      {action && <div className="mt-1 flex flex-wrap justify-center gap-2">{action}</div>}
    </div>
  );
}

/** Không có kết quả tìm kiếm/lọc */
export function NoResults({ onReset, keyword }: { onReset?: () => void; keyword?: string }) {
  return (
    <EmptyState
      icon={SearchX}
      tone="neutral"
      title="Không tìm thấy kết quả"
      description={keyword ? `Không có kết quả cho “${keyword}”. Thử từ khóa khác hoặc bỏ bớt bộ lọc.` : 'Thử bỏ bớt bộ lọc.'}
      action={
        onReset && (
          <Button action="reset" onClick={onReset}>
            Xóa bộ lọc
          </Button>
        )
      }
    />
  );
}

export interface ErrorStateProps {
  title?: ReactNode;
  description?: ReactNode;
  onRetry?: () => void;
  retrying?: boolean;
  size?: 'compact' | 'default';
  className?: string;
}

/** Lỗi tải dữ liệu, có nút thử lại */
export function ErrorState({
  title = 'Không tải được dữ liệu',
  description = 'Kiểm tra kết nối mạng rồi thử lại. Nếu vẫn lỗi, hãy báo quản trị viên.',
  onRetry,
  retrying,
  size,
  className,
}: ErrorStateProps) {
  return (
    <EmptyState
      icon={CloudOff}
      tone="danger"
      title={title}
      description={description}
      size={size}
      className={className}
      action={onRetry && <Button action="refresh" onClick={onRetry} loading={retrying}>Thử lại</Button>}
    />
  );
}
