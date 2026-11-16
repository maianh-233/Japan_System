import { CalendarClock, CheckCircle2, Hammer, RotateCcw, Wrench } from 'lucide-react';
import dayjs from 'dayjs';
import { Button, EmptyState, StatusTag } from '@/components/common';
import { cn } from '@/lib/cn';
import { fmtDate, fmtDateTime, fromNow } from '@/lib/date';
import { formatCurrency } from '@/lib/format';
import { EQUIPMENT, ISSUE_SEVERITY, ISSUE_STATUS } from './constants';
import type { RoomIssue } from './types';

interface Props {
  issues: RoomIssue[];
  editable: boolean;
  onStart: (i: RoomIssue) => void;
  onResolve: (i: RoomIssue) => void;
  onReopen: (i: RoomIssue) => void;
  /** Hiện tên phòng (khi danh sách gồm nhiều phòng) */
  roomName?: (roomId: string) => string;
}

/** Quá hạn hẹn sửa */
export const isIssueOverdue = (i: RoomIssue) => i.status !== 'resolved' && Boolean(i.dueDate) && dayjs(i.dueDate).isBefore(dayjs(), 'day');

/** Danh sách báo hỏng dạng thẻ: chưa xong lên trước (nặng → nhẹ), đã xong xuống dưới */
export function RoomIssueList({ issues, editable, onStart, onResolve, onReopen, roomName }: Props) {
  if (!issues.length) {
    return <EmptyState size="compact" icon={Wrench} title="Chưa có báo hỏng nào" description="Phòng và thiết bị đang hoạt động bình thường." />;
  }
  const rank = { high: 0, medium: 1, low: 2 };
  const sorted = [...issues].sort(
    (a, b) =>
      Number(a.status === 'resolved') - Number(b.status === 'resolved') ||
      (a.status === 'resolved' ? b.reportedAt.localeCompare(a.reportedAt) : rank[a.severity] - rank[b.severity] || a.reportedAt.localeCompare(b.reportedAt)),
  );

  return (
    <ul className="flex flex-col gap-2.5">
      {sorted.map((i) => {
        const sv = ISSUE_SEVERITY[i.severity];
        const st = ISSUE_STATUS[i.status];
        const Eq = i.equipment ? EQUIPMENT[i.equipment].icon : Wrench;
        const overdue = isIssueOverdue(i);
        return (
          <li
            key={i.id}
            className={cn(
              'flex flex-col gap-2.5 rounded-[var(--radius-card)] border bg-surface p-3.5',
              i.status === 'resolved' ? 'border-line opacity-80' : i.severity === 'high' ? 'border-danger-100 border-l-4 border-l-danger-600' : 'border-line',
            )}
          >
            <div className="flex flex-wrap items-start justify-between gap-2">
              <div className="flex min-w-0 gap-2.5">
                <span className="grid size-8 shrink-0 place-items-center rounded-lg bg-page text-ink-secondary">
                  <Eq size={16} aria-hidden />
                </span>
                <div className="min-w-0">
                  <p className="font-medium text-ink">
                    {roomName && <span className="text-primary-700">{roomName(i.roomId)} · </span>}
                    {i.title}
                  </p>
                  <p className="text-xs text-ink-muted">
                    {i.equipment ? EQUIPMENT[i.equipment].label : 'Cơ sở vật chất'} · {i.reportedBy} · <span title={fmtDateTime(i.reportedAt)}>{fromNow(i.reportedAt)}</span>
                  </p>
                </div>
              </div>
              <span className="flex flex-wrap gap-1">
                {i.status !== 'resolved' && (
                  <StatusTag tone={sv.tone} icon={sv.icon} size="sm">
                    {sv.label}
                  </StatusTag>
                )}
                <StatusTag tone={st.tone} icon={st.icon} size="sm">
                  {st.label}
                </StatusTag>
              </span>
            </div>

            {i.description && <p className="text-[13px] text-ink-secondary">{i.description}</p>}

            {(i.assignee || i.resolution) && (
              <dl className="grid grid-cols-1 gap-x-4 gap-y-1 rounded-lg bg-page px-3 py-2 text-[13px] sm:grid-cols-[110px_1fr]">
                {i.assignee && (
                  <>
                    <dt className="text-ink-muted">Người sửa</dt>
                    <dd className="text-ink">{i.assignee}</dd>
                  </>
                )}
                {i.dueDate && i.status !== 'resolved' && (
                  <>
                    <dt className="text-ink-muted">Hẹn xong</dt>
                    <dd className={cn('inline-flex items-center gap-1', overdue ? 'font-medium text-danger-600' : 'text-ink')}>
                      <CalendarClock size={14} aria-hidden /> {fmtDate(i.dueDate)}
                      {overdue && ' — quá hạn'}
                    </dd>
                  </>
                )}
                {i.resolution && (
                  <>
                    <dt className="text-ink-muted">Đã xử lý</dt>
                    <dd className="text-ink">
                      {i.resolution}
                      <span className="text-ink-muted">
                        {' '}
                        · {fmtDate(i.resolvedAt)}
                        {i.cost ? ` · ${formatCurrency(i.cost)}` : ''}
                      </span>
                    </dd>
                  </>
                )}
              </dl>
            )}

            {editable && (
              <div className="flex flex-wrap justify-end gap-1.5">
                {i.status === 'open' && (
                  <Button icon={Hammer} size="sm" variant="soft" onClick={() => onStart(i)}>
                    Nhận xử lý
                  </Button>
                )}
                {i.status === 'in_progress' && (
                  <Button icon={Hammer} size="sm" tone="neutral" variant="ghost" onClick={() => onStart(i)}>
                    Đổi người / hẹn
                  </Button>
                )}
                {i.status !== 'resolved' ? (
                  <Button icon={CheckCircle2} size="sm" tone="success" variant="soft" onClick={() => onResolve(i)}>
                    Đã sửa xong
                  </Button>
                ) : (
                  <Button icon={RotateCcw} size="sm" tone="neutral" variant="ghost" onClick={() => onReopen(i)}>
                    Vẫn còn hỏng
                  </Button>
                )}
              </div>
            )}
          </li>
        );
      })}
    </ul>
  );
}
