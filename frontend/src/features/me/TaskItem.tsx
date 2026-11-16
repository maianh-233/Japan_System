import type { ReactNode } from 'react';
import dayjs from 'dayjs';
import { AlarmClock, CalendarClock, Link2, ListChecks, MessageSquare, Repeat } from 'lucide-react';
import { StatusTag } from '@/components/common';
import { TASK_PRIORITY, TASK_REPEAT, TASK_STATUS } from '@/features/work/constants';
import type { Task } from '@/features/work/types';
import { cn } from '@/lib/cn';
import { fromNow } from '@/lib/date';

/** Hạn task → nhãn + màu */
export function dueInfo(t: Task) {
  if (!t.due) return undefined;
  const today = dayjs().format('YYYY-MM-DD');
  if (t.status === 'done') return { label: `Hạn ${dayjs(t.due).format('DD/MM')}`, cls: 'text-ink-muted' };
  if (t.due < today) return { label: `Quá hạn ${dayjs().diff(dayjs(t.due), 'day')} ngày`, cls: 'text-danger-700 font-semibold', overdue: true };
  if (t.due === today) return { label: 'Hạn hôm nay', cls: 'text-warning-700 font-semibold' };
  return { label: `Hạn ${dayjs(t.due).format('dd DD/MM')}`, cls: 'text-ink-secondary' };
}

interface Props {
  task: Task;
  /** Dòng "giao bởi …" hoặc "giao cho …" */
  people: ReactNode;
  actions?: ReactNode;
  onOpen: () => void;
}

/** Thẻ task gọn: ưu tiên · hạn · checklist · bình luận; bấm để mở chi tiết */
export function TaskItem({ task: t, people, actions, onOpen }: Props) {
  const due = dueInfo(t);
  const doneItems = t.checklist.filter((c) => c.done).length;
  const P = TASK_PRIORITY[t.priority];
  const S = TASK_STATUS[t.status];
  return (
    <article className={cn('relative overflow-hidden rounded-xl border bg-surface pl-4 pr-3 py-3', due?.overdue ? 'border-danger-100' : 'border-line')}>
      <span className={cn('absolute inset-y-0 left-0 w-1', P.bar)} aria-hidden />
      <div className="flex items-start gap-3">
        <button type="button" onClick={onOpen} className="min-w-0 flex-1 text-left">
          <p className={cn('font-medium text-ink', t.status === 'done' && 'text-ink-muted line-through')}>{t.title}</p>
          <p className="mt-0.5 flex flex-wrap items-center gap-x-3 gap-y-0.5 text-xs text-ink-muted">
            <span>{people}</span>
            {due && (
              <span className={cn('inline-flex items-center gap-1', due.cls)}>
                {due.overdue ? <AlarmClock size={12} aria-hidden /> : <CalendarClock size={12} aria-hidden />} {due.label}
              </span>
            )}
            {t.checklist.length > 0 && (
              <span className={cn('inline-flex items-center gap-1', doneItems === t.checklist.length && 'text-success-700')}>
                <ListChecks size={12} aria-hidden /> {doneItems}/{t.checklist.length}
              </span>
            )}
            {t.comments.length > 0 && (
              <span className="inline-flex items-center gap-1">
                <MessageSquare size={12} aria-hidden /> {t.comments.length}
              </span>
            )}
            {t.repeat !== 'none' && (
              <span className="inline-flex items-center gap-1">
                <Repeat size={12} aria-hidden /> {TASK_REPEAT[t.repeat]}
              </span>
            )}
            {t.link && (
              <span className="inline-flex items-center gap-1">
                <Link2 size={12} aria-hidden /> {t.link.label}
              </span>
            )}
            {t.completedAt && <span className="text-success-700">xong {fromNow(t.completedAt)}</span>}
          </p>
        </button>
        <div className="flex shrink-0 flex-col items-end gap-2">
          <StatusTag tone={S.tone} icon={S.icon} size="sm">
            {S.label}
          </StatusTag>
          {actions && <div className="flex flex-wrap justify-end gap-1.5">{actions}</div>}
        </div>
      </div>
    </article>
  );
}
