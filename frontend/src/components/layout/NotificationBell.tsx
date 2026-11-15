import { useState } from 'react';
import { Badge, Popover } from 'antd';
import dayjs from 'dayjs';
import relativeTime from 'dayjs/plugin/relativeTime';
import { Bell, BellOff, CheckCheck } from 'lucide-react';
import { useNavigate } from 'react-router-dom';
import { cn } from '@/lib/cn';
import { useBreakpoint } from '@/hooks/useBreakpoint';
import { Button, EmptyState } from '@/components/common';
import type { AppNotification } from '@/types/auth';

dayjs.extend(relativeTime);

const dotTone: Record<AppNotification['tone'], string> = {
  info: 'bg-primary-500',
  success: 'bg-success-600',
  warning: 'bg-warning-600',
  danger: 'bg-danger-600',
};

/** Chuông thông báo: badge số chưa đọc + danh sách, đánh dấu đã đọc. */
export function NotificationBell({ initial }: { initial: AppNotification[] }) {
  const [items, setItems] = useState(initial);
  const [open, setOpen] = useState(false);
  const navigate = useNavigate();
  const { isMobile } = useBreakpoint();
  const unread = items.filter((n) => !n.read).length;

  const openItem = (n: AppNotification) => {
    setItems((list) => list.map((x) => (x.id === n.id ? { ...x, read: true } : x)));
    if (n.link) {
      setOpen(false);
      navigate(n.link);
    }
  };

  const content = (
    <div className="flex w-[min(92vw,380px)] flex-col">
      <div className="flex items-center justify-between border-b border-line px-4 py-3">
        <span className="font-semibold text-ink">Thông báo</span>
        <Button
          icon={CheckCheck}
          size="sm"
          variant="ghost"
          disabled={unread === 0}
          onClick={() => setItems((l) => l.map((x) => ({ ...x, read: true })))}
        >
          Đánh dấu đã đọc
        </Button>
      </div>
      {items.length === 0 ? (
        <EmptyState icon={BellOff} size="compact" title="Chưa có thông báo" />
      ) : (
        <ul className="max-h-[420px] overflow-y-auto py-1">
          {items.map((n) => (
            <li key={n.id}>
              <button
                type="button"
                onClick={() => openItem(n)}
                className={cn(
                  'flex w-full gap-3 px-4 py-3 text-left transition-colors hover:bg-primary-50',
                  !n.read && 'bg-primary-50/60',
                )}
              >
                <span className={cn('mt-1.5 h-2 w-2 shrink-0 rounded-full', n.read ? 'bg-transparent' : dotTone[n.tone])} aria-hidden />
                <span className="flex min-w-0 flex-col gap-0.5">
                  <span className={cn('text-sm leading-5 text-ink', !n.read && 'font-semibold')}>{n.title}</span>
                  <span className="text-[13px] leading-5 text-ink-secondary">{n.description}</span>
                  <span className="text-xs text-ink-muted">{dayjs(n.createdAt).fromNow()}</span>
                </span>
                {!n.read && <span className="sr-only">(chưa đọc)</span>}
              </button>
            </li>
          ))}
        </ul>
      )}
    </div>
  );

  return (
    <Popover
      open={open}
      onOpenChange={setOpen}
      trigger="click"
      placement={isMobile ? 'bottom' : 'bottomRight'}
      content={content}
      arrow={false}
      styles={{ body: { padding: 0 } }}
    >
      <Badge count={unread} size="small" offset={[-6, 6]}>
        <Button
          icon={Bell}
          iconOnly
          tone="neutral"
          variant="ghost"
          aria-label={unread ? `Thông báo, ${unread} chưa đọc` : 'Thông báo'}
          tooltip={null}
        />
      </Badge>
    </Popover>
  );
}
