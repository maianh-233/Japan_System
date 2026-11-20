import dayjs from 'dayjs';
import { ArrowLeftRight, CalendarX2, Undo2 } from 'lucide-react';
import { Button, EmptyState, StatusTag, useConfirm, useNotify } from '@/components/common';
import { useClassStore } from '@/features/academic/classes/classStore';
import { notStarted } from '@/features/academic/classes/sessionUtils';
import { cn } from '@/lib/cn';
import { fromNow } from '@/lib/date';
import { usePortalStore } from '../portalStore';
import { shortCode } from '../usePortal';
import { USE_API } from '@/lib/api/config';
import * as portalApi from '@/features/portal/portalApi';

/** Danh sách đơn xin nghỉ của 1 học viên (Cổng HV › Đơn xin nghỉ, Cổng PH › Chuyên cần) */
export function LeaveRequestList({ studentId }: { studentId: string }) {
  const notify = useNotify();
  const { confirm, confirmDialog } = useConfirm();
  const all = usePortalStore((s) => s.leaveRequests);
  const withdraw = usePortalStore((s) => s.withdrawLeaveRequest);
  const sessions = useClassStore((s) => s.sessions);
  const classes = useClassStore((s) => s.classes);
  const list = all.filter((r) => r.studentId === studentId).sort((a, b) => b.createdAt.localeCompare(a.createdAt));

  if (!list.length) return <EmptyState icon={CalendarX2} title="Chưa có đơn xin nghỉ" />;

  const label = (id?: string) => {
    const s = sessions.find((x) => x.id === id);
    const c = classes.find((x) => x.id === s?.classId);
    return s && c ? `${shortCode(c)} · buổi ${s.no} · ${dayjs(s.date).format('dd DD/MM')} ${s.startTime}` : '—';
  };

  return (
    <div className="flex flex-col gap-3">
      {list.map((r) => {
        const s = sessions.find((x) => x.id === r.sessionId);
        return (
          <article key={r.id} className={cn('rounded-xl border bg-surface p-4', r.status === 'pending' ? 'border-warning-100' : 'border-line')}>
            <div className="flex flex-wrap items-start justify-between gap-2">
              <div>
                <p className="font-semibold text-ink">{label(r.sessionId)}</p>
                <p className="text-[13px] text-ink-secondary">
                  {r.reasonType}: {r.reason}
                </p>
              </div>
              <StatusTag group="request" status={r.status} size="sm" />
            </div>
            <p className="mt-1 text-xs text-ink-muted">
              {r.by === 'parent' ? 'Phụ huynh' : 'Học viên'} {r.byName} gửi {fromNow(r.createdAt)}
              {r.decidedBy && ` · ${r.decidedBy} ${r.status === 'approved' ? 'đã duyệt' : 'từ chối'}`}
              {r.note && ` — ${r.note}`}
            </p>
            {r.makeupSessionId && (
              <p className="mt-2 inline-flex items-center gap-1.5 rounded-lg bg-warning-50 px-3 py-1.5 text-[13px] text-warning-700">
                <ArrowLeftRight size={14} aria-hidden /> Học bù: {label(r.makeupSessionId)}
                {r.makeupResult && (
                  <b className={r.makeupResult === 'present' || r.makeupResult === 'late' ? 'text-success-700' : 'text-danger-700'}>
                    {' '}
                    · {r.makeupResult === 'present' || r.makeupResult === 'late' ? 'đã học bù ✓' : 'vắng buổi học bù'}
                  </b>
                )}
              </p>
            )}
            {r.status === 'pending' && s && notStarted(s) && (
              <div>
                <Button
                  icon={Undo2}
                  size="sm"
                  variant="ghost"
                  tone="neutral"
                  className="mt-2"
                  onClick={async () => {
                    if (await confirm({ title: 'Rút đơn xin nghỉ?', description: 'Bạn sẽ đi học buổi này như bình thường.', tone: 'warning' })) {
                      try {
                        if (USE_API) await portalApi.withdrawLeaveRequest(r.id);
                        else withdraw(r.id);
                        notify.toast.success('Đã rút đơn');
                      } catch (e) {
                        notify.error({ title: 'Không rút được đơn', description: (e as Error).message });
                      }
                    }
                  }}
                >
                  Rút đơn
                </Button>
              </div>
            )}
          </article>
        );
      })}
      {confirmDialog}
    </div>
  );
}
