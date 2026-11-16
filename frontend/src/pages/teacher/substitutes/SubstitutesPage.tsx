import { useMemo } from 'react';
import dayjs from 'dayjs';
import { ArrowLeftRight, Check, MapPin, TriangleAlert, Users, Video, X } from 'lucide-react';
import { Button, EmptyState, PageHeader, StatusTag, useConfirm, useNotify } from '@/components/common';
import { useClassStore } from '@/features/academic/classes/classStore';
import { teacherName } from '@/features/academic/classes/constants';
import { notStarted, sessionRoom, sessionTeachers } from '@/features/academic/classes/sessionUtils';
import { useCurriculumStore } from '@/features/academic/curriculum/curriculumStore';
import { useHrStore } from '@/features/hr/hrStore';
import { usePortalStore } from '@/features/portal/portalStore';
import type { InviteStatus, SubInvite } from '@/features/portal/types';
import { shortCode, useTeacherId } from '@/features/portal/usePortal';
import { useStudentStore } from '@/features/student/studentStore';
import { cn } from '@/lib/cn';
import { fromNow } from '@/lib/date';
import { formatCurrency } from '@/lib/format';

const STATUS: Record<InviteStatus, { label: string; tone: 'warning' | 'success' | 'neutral' | 'danger' }> = {
  pending: { label: 'Chờ trả lời', tone: 'warning' },
  accepted: { label: 'Đã nhận', tone: 'success' },
  declined: { label: 'Đã từ chối', tone: 'neutral' },
  expired: { label: 'Hết hạn', tone: 'neutral' },
};
const toMin = (t: string) => Number(t.slice(0, 2)) * 60 + Number(t.slice(3, 5));

/** Cổng GV › Lời mời dạy thay: nhận / từ chối; nhận → buổi chuyển sang mình, tự tính lương (vai trò dạy thay + phụ cấp) */
export default function SubstitutesPage() {
  const me = useTeacherId();
  const notify = useNotify();
  const { confirm, confirmDialog } = useConfirm();
  const invites = usePortalStore((s) => s.subInvites);
  const respond = usePortalStore((s) => s.respondInvite);
  const sessions = useClassStore((s) => s.sessions);
  const classes = useClassStore((s) => s.classes);
  const updateSession = useClassStore((s) => s.updateSession);
  const enrollments = useStudentStore((s) => s.enrollments);
  const curricula = useCurriculumStore((s) => s.curricula);
  const meEmp = useHrStore((s) => s.employees.find((e) => e.id === me));

  const list = useMemo(
    () =>
      invites
        .filter((i) => i.toTeacherId === me)
        .map((i) => {
          const s = sessions.find((x) => x.id === i.sessionId);
          // Quá hạn trả lời / buổi đã qua mà chưa trả lời → hết hạn
          const status: InviteStatus = i.status === 'pending' && (dayjs(i.respondBy).isBefore(dayjs()) || (s && !notStarted(s))) ? 'expired' : i.status;
          return { ...i, status, s, cls: classes.find((c) => c.id === i.classId) };
        })
        .sort((a, b) => Number(b.status === 'pending') - Number(a.status === 'pending') || b.createdAt.localeCompare(a.createdAt)),
    [invites, sessions, classes, me],
  );

  /** Buổi của mình trùng giờ */
  const conflictOf = (inv: (typeof list)[number]) =>
    inv.s &&
    sessions.find((x) => {
      const c = classes.find((k) => k.id === x.classId);
      return x.id !== inv.s!.id && x.date === inv.s!.date && x.status !== 'cancelled' && c && sessionTeachers(x, c).includes(me) && toMin(x.startTime) < toMin(inv.s!.endTime) && toMin(inv.s!.startTime) < toMin(x.endTime);
    });

  const accept = async (inv: (typeof list)[number]) => {
    const conflict = conflictOf(inv);
    if (conflict) {
      notify.error({ title: 'Trùng lịch dạy', description: `Bạn đang có buổi ${conflict.startTime}–${conflict.endTime} cùng ngày.` });
      return;
    }
    const ok = await confirm({
      title: `Nhận dạy thay ${inv.cls ? shortCode(inv.cls) : ''}?`,
      tone: 'success',
      consequences: ['Buổi học hiện trên thời khóa biểu của bạn ngay.', 'Nhớ check-in / check-out để được tính lương dạy thay.'],
      confirmText: 'Nhận dạy',
    });
    if (!ok || !inv.s) return;
    respond(inv.id, true);
    updateSession(inv.s.id, { teacherIds: [me], note: `Dạy thay GV ${teacherName(inv.fromTeacherId)} (${inv.reason})` });
    notify.success({ title: 'Đã nhận dạy thay', description: 'Giáo vụ và học viên lớp được thông báo.' });
  };

  const decline = async (inv: SubInvite) => {
    const res = await confirm({ title: 'Từ chối lời mời?', tone: 'warning', requireReason: { label: 'Lý do', placeholder: 'Vd: trùng lịch cá nhân' }, confirmAction: 'reject' });
    if (res) {
      respond(inv.id, false, res.reason);
      notify.toast.success('Đã từ chối — giáo vụ sẽ mời GV khác');
    }
  };

  const pending = list.filter((i) => i.status === 'pending');
  const rate = (online?: boolean) => (online ? meEmp?.teacher?.rate.online : meEmp?.teacher?.rate.offline) ?? 0;

  return (
    <>
      <PageHeader title="Lời mời dạy thay" subtitle="Khi GV khác nghỉ, giáo vụ mời bạn dạy thay. Nhận → buổi chuyển sang bạn và tự cộng vào lương (đơn giá + phụ cấp dạy thay)." />
      {!list.length && <EmptyState icon={ArrowLeftRight} title="Chưa có lời mời nào" />}
      {pending.length > 0 && <p className="mb-3 text-sm font-semibold text-ink">Chờ bạn trả lời ({pending.length})</p>}
      <div className="flex flex-col gap-3">
        {list.map((inv) => {
          const s = inv.s;
          const cls = inv.cls;
          const cur = curricula.find((c) => c.id === cls?.curriculumId);
          const li = cur?.lessons.findIndex((l) => l.id === s?.lessonId) ?? -1;
          const conflict = inv.status === 'pending' ? conflictOf(inv) : undefined;
          const hours = s ? (toMin(s.endTime) - toMin(s.startTime)) / 60 : 0;
          return (
            <article key={inv.id} className={cn('rounded-xl border bg-surface p-4', inv.status === 'pending' ? 'border-warning-100' : 'border-line opacity-80')}>
              <div className="flex flex-wrap items-start justify-between gap-2">
                <div>
                  <p className="text-base font-semibold text-ink">
                    {cls ? shortCode(cls) : inv.classId} · {s ? `${dayjs(s.date).format('dddd DD/MM')} ${s.startTime}–${s.endTime}` : ''}
                  </p>
                  <p className="text-[13px] text-ink-secondary">
                    {cls?.courseLabel} · Buổi {s?.no}
                    {li >= 0 && ` · Bài ${li + 1}: ${cur!.lessons[li].title}`}
                  </p>
                </div>
                <StatusTag tone={STATUS[inv.status].tone} icon={ArrowLeftRight} size="sm">
                  {STATUS[inv.status].label}
                </StatusTag>
              </div>
              <p className="mt-2 flex flex-wrap gap-x-4 gap-y-1 text-[13px] text-ink-secondary">
                <span className="inline-flex items-center gap-1">
                  {cls?.online ? <Video size={14} aria-hidden /> : <MapPin size={14} aria-hidden />} {cls?.online ? 'Online' : `Phòng ${s && cls ? (sessionRoom(s, cls) ?? '—') : '—'}`}
                </span>
                <span className="inline-flex items-center gap-1">
                  <Users size={14} aria-hidden /> {enrollments.filter((e) => e.classId === inv.classId && e.status === 'studying').length} học viên
                </span>
                <span>Dạy thay cho: {teacherName(inv.fromTeacherId)} — {inv.reason}</span>
              </p>
              <p className="mt-1 text-xs text-ink-muted">
                {inv.invitedBy} mời {fromNow(inv.createdAt)}
                {inv.status === 'pending' && ` · trả lời trước ${dayjs(inv.respondBy).format('HH:mm DD/MM')}`}
                {inv.status === 'pending' && hours > 0 && ` · ước tính ${formatCurrency(Math.round(hours * rate(cls?.online)))} + phụ cấp dạy thay`}
              </p>
              {inv.declineReason && <p className="mt-1 text-xs text-ink-muted">Lý do từ chối: {inv.declineReason}</p>}
              {conflict && (
                <p className="mt-2 flex items-center gap-1.5 rounded-lg bg-danger-50 px-3 py-2 text-[13px] text-danger-700">
                  <TriangleAlert size={14} aria-hidden /> Trùng buổi dạy của bạn {conflict.startTime}–{conflict.endTime}
                </p>
              )}
              {inv.status === 'pending' && (
                <div className="mt-3 flex gap-2">
                  <Button icon={Check} size="sm" tone="success" disabled={Boolean(conflict)} onClick={() => accept(inv)}>
                    Nhận dạy
                  </Button>
                  <Button icon={X} size="sm" variant="outline" tone="neutral" onClick={() => decline(inv)}>
                    Từ chối
                  </Button>
                </div>
              )}
            </article>
          );
        })}
      </div>
      {confirmDialog}
    </>
  );
}
