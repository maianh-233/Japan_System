import { useState } from 'react';
import dayjs from 'dayjs';
import { ClipboardList, Undo2 } from 'lucide-react';
import { Button, EmptyState, PageHeader, StatusTag, useConfirm, useNotify } from '@/components/common';
import { useClassStore } from '@/features/academic/classes/classStore';
import { CancelRequestModal } from '@/features/portal/components/CancelRequestModal';
import { usePortalStore } from '@/features/portal/portalStore';
import { shortCode, useTeacherId } from '@/features/portal/usePortal';
import { cn } from '@/lib/cn';
import { fmtDateTime, fromNow } from '@/lib/date';
import { USE_API } from '@/lib/api/config';
import * as portalApi from '@/features/portal/portalApi';

/** Cổng GV › Đơn hủy buổi: đơn đã gửi + trạng thái duyệt; gửi đơn mới */
export default function TeacherRequestsPage() {
  const me = useTeacherId();
  const notify = useNotify();
  const { confirm, confirmDialog } = useConfirm();
  const all = usePortalStore((s) => s.cancelRequests);
  const withdraw = usePortalStore((s) => s.withdrawCancelRequest);
  const sessions = useClassStore((s) => s.sessions);
  const classes = useClassStore((s) => s.classes);
  const [creating, setCreating] = useState(false);
  const list = all.filter((r) => r.teacherId === me).sort((a, b) => b.createdAt.localeCompare(a.createdAt));

  return (
    <>
      <PageHeader
        title="Đơn hủy buổi"
        subtitle="Giáo viên chỉ xin hủy TỪNG BUỔI của mình. Hủy cả lớp do trung tâm quyết định."
        actions={
          <Button action="send" collapseOnMobile onClick={() => setCreating(true)}>
            Gửi đơn mới
          </Button>
        }
      />
      {!list.length && <EmptyState icon={ClipboardList} title="Chưa gửi đơn nào" />}
      <div className="flex flex-col gap-3">
        {list.map((r) => {
          const s = sessions.find((x) => x.id === r.sessionId);
          const cls = classes.find((c) => c.id === r.classId);
          return (
            <article key={r.id} className={cn('rounded-xl border bg-surface p-4', r.status === 'pending' ? 'border-warning-100' : 'border-line')}>
              <div className="flex flex-wrap items-start justify-between gap-2">
                <div>
                  <p className="font-semibold text-ink">
                    {cls ? shortCode(cls) : r.classId} · Buổi {s?.no} · {s ? `${dayjs(s.date).format('dddd DD/MM')} ${s.startTime}–${s.endTime}` : ''}
                  </p>
                  <p className="text-[13px] text-ink-secondary">
                    {r.reasonType}: {r.reason}
                  </p>
                </div>
                <StatusTag group="request" status={r.status} size="sm" />
              </div>
              <p className="mt-2 text-xs text-ink-muted">
                Gửi {fromNow(r.createdAt)}
                {r.makeupAt && ` · đề xuất dạy bù ${fmtDateTime(r.makeupAt)}`}
              </p>
              {r.decidedBy && (
                <p className={cn('mt-2 rounded-lg px-3 py-2 text-[13px]', r.status === 'approved' ? 'bg-success-50 text-success-700' : 'bg-danger-50 text-danger-700')}>
                  {r.decidedBy} {r.status === 'approved' ? 'đã duyệt' : 'từ chối'} {r.decidedAt && fromNow(r.decidedAt)}
                  {r.note && ` — ${r.note}`}
                </p>
              )}
              {r.status === 'pending' && (
                <Button
                  icon={Undo2}
                  size="sm"
                  variant="ghost"
                  tone="neutral"
                  className="mt-2"
                  onClick={async () => {
                    if (await confirm({ title: 'Rút lại đơn?', description: 'Buổi học giữ nguyên lịch, bạn dạy như bình thường.', tone: 'warning' })) {
                      try {
                        if (USE_API) await portalApi.withdrawCancelRequest(r.id);
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
              )}
            </article>
          );
        })}
      </div>
      <CancelRequestModal open={creating} onClose={() => setCreating(false)} />
      {confirmDialog}
    </>
  );
}
