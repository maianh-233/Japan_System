import { useState } from 'react';
import { Megaphone } from 'lucide-react';
import { StatusTag } from '@/components/common';
import * as learningApi from '@/features/academic/classes/learningApi';
import { cn } from '@/lib/cn';
import { fromNow } from '@/lib/date';
import { usePortalStore } from '../portalStore';

/**
 * Thông báo lớp ở cổng HV / PH (chế độ API — ND-07): thông báo trung tâm / GV gửi tới HV (cổng HV) hoặc PH (cổng PH).
 * Mở 1 thông báo → tính "đã xem" (mỗi tài khoản 1 lần).
 */
export function ClassInbox({ classIds, limit = 5 }: { classIds?: string[]; limit?: number }) {
  const all = usePortalStore((s) => s.inbox);
  const [open, setOpen] = useState<string | null>(null);
  const list = all.filter((a) => !classIds || classIds.includes(a.classId)).slice(0, limit);
  if (!list.length) return null;
  const unread = list.filter((a) => !a.read).length;

  const toggle = (id: string, read: boolean) => {
    setOpen((x) => (x === id ? null : id));
    if (!read) void learningApi.markAnnouncementRead(id).catch(() => undefined);
  };

  return (
    <section className="mb-6 rounded-[var(--radius-card)] border border-line bg-surface p-4">
      <p className="mb-3 flex items-center gap-2 font-semibold text-ink">
        <Megaphone size={18} className="text-primary-600" aria-hidden /> Thông báo lớp
        {unread > 0 && (
          <StatusTag tone="danger" icon={Megaphone} size="sm">
            {unread} chưa xem
          </StatusTag>
        )}
      </p>
      <ul className="flex flex-col divide-y divide-line">
        {list.map((a) => (
          <li key={a.id}>
            <button type="button" onClick={() => toggle(a.id, a.read)} className="flex w-full flex-col gap-0.5 py-2.5 text-left" aria-expanded={open === a.id}>
              <span className="flex items-center gap-2">
                {!a.read && <span className="size-2 shrink-0 rounded-full bg-danger-600" aria-label="Chưa xem" />}
                <span className={cn('min-w-0 flex-1 truncate text-[14px]', a.read ? 'text-ink' : 'font-semibold text-ink')}>{a.title}</span>
                <span className="shrink-0 text-xs text-ink-muted">{fromNow(a.sentAt)}</span>
              </span>
              <span className="text-xs text-ink-muted">
                {a.classCode} · {a.sentBy}
              </span>
              {open === a.id && <span className="mt-1.5 whitespace-pre-line text-[13px] text-ink-secondary">{a.content}</span>}
            </button>
          </li>
        ))}
      </ul>
    </section>
  );
}
