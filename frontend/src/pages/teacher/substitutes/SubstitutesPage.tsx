import { useMemo, useState } from 'react';
import dayjs from 'dayjs';
import { ArrowLeftRight, BookOpen, Check, CircleCheck, Clock, Hourglass, Inbox, MapPin, TriangleAlert, UserRound, Users, Video, Wallet, X } from 'lucide-react';
import { Button, EmptyState, PageHeader, StatusTag, useConfirm, useNotify, type Tone } from '@/components/common';
import { useClassStore } from '@/features/academic/classes/classStore';
import { teacherName } from '@/features/academic/classes/constants';
import { notStarted, sessionRoom, sessionTeachers } from '@/features/academic/classes/sessionUtils';
import type { ClassInfo, ClassSession } from '@/features/academic/classes/types';
import { useCurriculumStore } from '@/features/academic/curriculum/curriculumStore';
import { useHrStore } from '@/features/hr/hrStore';
import { usePortalStore } from '@/features/portal/portalStore';
import type { InviteStatus, SubInvite } from '@/features/portal/types';
import { shortCode, useTeacherId } from '@/features/portal/usePortal';
import { useStudentStore } from '@/features/student/studentStore';
import { cn } from '@/lib/cn';
import { fromNow } from '@/lib/date';
import { formatCurrency } from '@/lib/format';
import { USE_API } from '@/lib/api/config';
import * as portalApi from '@/features/portal/portalApi';

const STATUS: Record<InviteStatus, { label: string; tone: Tone; icon: typeof Check }> = {
  pending: { label: 'Chờ trả lời', tone: 'warning', icon: Hourglass },
  accepted: { label: 'Đã nhận dạy', tone: 'success', icon: CircleCheck },
  declined: { label: 'Đã từ chối', tone: 'neutral', icon: X },
  expired: { label: 'Hết hạn', tone: 'neutral', icon: Clock },
};
const toMin = (t: string) => Number(t.slice(0, 2)) * 60 + Number(t.slice(3, 5));

type Item = SubInvite & { s?: ClassSession; cls?: ClassInfo };

/** "Còn 3 giờ" / "Còn 2 ngày" để trả lời */
const timeLeft = (iso: string) => {
  const min = dayjs(iso).diff(dayjs(), 'minute');
  if (min < 60) return { text: `Còn ${Math.max(min, 1)} phút`, urgent: true };
  if (min < 60 * 24) return { text: `Còn ${Math.floor(min / 60)} giờ`, urgent: min < 60 * 6 };
  return { text: `Còn ${Math.floor(min / 60 / 24)} ngày`, urgent: false };
};

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
  const [tab, setTab] = useState<'pending' | 'history'>('pending');

  const list: Item[] = useMemo(
    () =>
      invites
        .filter((i) => i.toTeacherId === me)
        .map((i) => {
          const s = sessions.find((x) => x.id === i.sessionId);
          // Quá hạn trả lời / buổi đã qua mà chưa trả lời → hết hạn
          const status: InviteStatus = i.status === 'pending' && (dayjs(i.respondBy).isBefore(dayjs()) || (s && !notStarted(s))) ? 'expired' : i.status;
          return { ...i, status, s, cls: classes.find((c) => c.id === i.classId) };
        })
        .sort((a, b) => (a.status === 'pending' && b.status === 'pending' ? a.respondBy.localeCompare(b.respondBy) : b.createdAt.localeCompare(a.createdAt))),
    [invites, sessions, classes, me],
  );

  const rate = (online?: boolean) => (online ? meEmp?.teacher?.rate.online : meEmp?.teacher?.rate.offline) ?? 0;
  const hoursOf = (s?: ClassSession) => (s ? (toMin(s.endTime) - toMin(s.startTime)) / 60 : 0);
  const payOf = (inv: Item) => Math.round(hoursOf(inv.s) * rate(inv.cls?.online));

  /** Buổi của mình trùng giờ */
  const conflictOf = (inv: Item) =>
    inv.s &&
    sessions.find((x) => {
      const c = classes.find((k) => k.id === x.classId);
      return x.id !== inv.s!.id && x.date === inv.s!.date && x.status !== 'cancelled' && c && sessionTeachers(x, c).includes(me) && toMin(x.startTime) < toMin(inv.s!.endTime) && toMin(inv.s!.startTime) < toMin(x.endTime);
    });

  const accept = async (inv: Item) => {
    const conflict = conflictOf(inv);
    if (conflict) {
      notify.error({ title: 'Trùng lịch dạy', description: `Bạn đang có buổi ${conflict.startTime}–${conflict.endTime} cùng ngày.` });
      return;
    }
    const ok = await confirm({
      title: `Nhận dạy thay ${inv.cls ? shortCode(inv.cls) : ''}?`,
      tone: 'success',
      consequences: ['Buổi học hiện trên thời khóa biểu của bạn ngay.', 'Nhớ bấm “Chấm công” ở trang Dạy học để được tính lương dạy thay.'],
      confirmText: 'Nhận dạy',
    });
    if (!ok || !inv.s) return;
    if (USE_API) {
      try {
        await portalApi.respondInvite(inv.id, true);
      } catch (e) {
        return void notify.error({ title: 'Không nhận được lời mời', description: (e as Error).message });
      }
    } else {
      respond(inv.id, true);
      updateSession(inv.s.id, { teacherIds: [me], note: `Dạy thay GV ${teacherName(inv.fromTeacherId)} (${inv.reason})` });
    }
    notify.success({ title: 'Đã nhận dạy thay', description: 'Giáo vụ và học viên lớp được thông báo.' });
  };

  const decline = async (inv: SubInvite) => {
    const res = await confirm({ title: 'Từ chối lời mời?', tone: 'warning', requireReason: { label: 'Lý do', placeholder: 'Vd: trùng lịch cá nhân' }, confirmAction: 'reject' });
    if (res) {
      try {
        if (USE_API) await portalApi.respondInvite(inv.id, false, res.reason);
        else respond(inv.id, false, res.reason);
        notify.toast.success('Đã từ chối — giáo vụ sẽ mời GV khác');
      } catch (e) {
        notify.error({ title: 'Không từ chối được', description: (e as Error).message });
      }
    }
  };

  const pending = list.filter((i) => i.status === 'pending');
  const history = list.filter((i) => i.status !== 'pending');
  const monthAccepted = list.filter((i) => i.status === 'accepted' && i.s && dayjs(i.s.date).isSame(dayjs(), 'month'));
  const monthPay = monthAccepted.reduce((n, i) => n + payOf(i), 0);

  const lessonOf = (inv: Item) => {
    const cur = curricula.find((c) => c.id === inv.cls?.curriculumId);
    if (inv.s?.kind === 'exam') return cur?.exams.find((e) => e.id === inv.s!.examId)?.title ?? 'Buổi thi';
    const li = cur?.lessons.findIndex((l) => l.id === inv.s?.lessonId) ?? -1;
    return li >= 0 ? `Bài ${li + 1}: ${cur!.lessons[li].title}` : undefined;
  };

  return (
    <>
      <PageHeader title="Lời mời dạy thay" subtitle="Khi giáo viên khác nghỉ, giáo vụ mời bạn dạy thay. Nhận dạy → buổi chuyển sang lịch của bạn và tự cộng vào lương (đơn giá + phụ cấp dạy thay)." />

      {/* Tổng quan */}
      <div className="mb-5 grid grid-cols-1 gap-3 sm:grid-cols-3">
        {[
          { icon: Inbox, label: 'Đang chờ bạn trả lời', value: String(pending.length), tone: pending.length ? 'text-warning-700 bg-warning-50' : 'text-ink-secondary bg-page' },
          { icon: CircleCheck, label: 'Đã nhận dạy tháng này', value: `${monthAccepted.length} buổi`, tone: 'text-success-700 bg-success-50' },
          { icon: Wallet, label: 'Thu nhập dạy thay tháng này (ước tính)', value: formatCurrency(monthPay) || '0 ₫', tone: 'text-primary-700 bg-primary-50' },
        ].map((x) => (
          <div key={x.label} className="flex items-center gap-3 rounded-[var(--radius-card)] border border-line bg-surface px-4 py-3">
            <span className={cn('grid size-10 shrink-0 place-items-center rounded-xl', x.tone)}>
              <x.icon size={20} aria-hidden />
            </span>
            <span className="min-w-0">
              <span className="block text-xs text-ink-muted">{x.label}</span>
              <span className="tabular block text-lg font-semibold text-ink">{x.value}</span>
            </span>
          </div>
        ))}
      </div>

      {/* Chuyển tab */}
      <div className="mb-4 inline-flex rounded-xl border border-line bg-surface p-1" role="tablist">
        {(
          [
            ['pending', `Chờ trả lời (${pending.length})`],
            ['history', `Đã trả lời / hết hạn (${history.length})`],
          ] as const
        ).map(([k, label]) => (
          <button
            key={k}
            type="button"
            role="tab"
            aria-selected={tab === k}
            onClick={() => setTab(k)}
            className={cn('h-9 rounded-lg px-4 text-[13px] font-medium transition-colors', tab === k ? 'bg-primary-600 text-white shadow-sm' : 'text-ink-secondary hover:bg-page')}
          >
            {label}
          </button>
        ))}
      </div>

      {tab === 'pending' ? (
        pending.length ? (
          <div className="flex flex-col gap-4">
            {pending.map((inv) => {
              const { s, cls } = inv;
              const conflict = conflictOf(inv);
              const left = timeLeft(inv.respondBy);
              const lesson = lessonOf(inv);
              const roster = enrollments.filter((e) => e.classId === inv.classId && e.status === 'studying').length;
              return (
                <article key={inv.id} className="overflow-hidden rounded-[var(--radius-card)] border border-line bg-surface shadow-sm">
                  <div className="flex flex-col md:flex-row">
                    {/* Ngày dạy */}
                    {s && (
                      <div className="flex shrink-0 items-center gap-4 border-b border-line bg-primary-50 px-5 py-4 md:w-40 md:flex-col md:justify-center md:gap-1 md:border-b-0 md:border-r md:text-center">
                        <span className="text-xs font-medium uppercase tracking-wide text-primary-700">{dayjs(s.date).format('dddd')}</span>
                        <span className="tabular text-3xl font-bold leading-none text-primary-800">{dayjs(s.date).format('DD')}</span>
                        <span className="text-xs text-primary-700">tháng {dayjs(s.date).format('MM/YYYY')}</span>
                        <span className="tabular rounded-md bg-surface px-2 py-0.5 text-[13px] font-semibold text-ink md:mt-2">
                          {s.startTime}–{s.endTime}
                        </span>
                      </div>
                    )}

                    {/* Thông tin buổi */}
                    <div className="flex min-w-0 flex-1 flex-col gap-3 p-4 md:p-5">
                      <div className="flex flex-wrap items-center gap-2">
                        <h2 className="text-lg font-semibold text-ink">{cls ? shortCode(cls) : inv.classId}</h2>
                        <span className="rounded bg-page px-1.5 py-0.5 text-xs text-ink-secondary">{cls?.courseLabel}</span>
                        <span className="text-[13px] text-ink-muted">Buổi {s?.no}</span>
                      </div>
                      {lesson && (
                        <p className="flex items-center gap-1.5 text-[13px] text-ink">
                          <BookOpen size={15} className="shrink-0 text-primary-600" aria-hidden /> {lesson}
                        </p>
                      )}
                      <div className="flex flex-wrap gap-2 text-[13px]">
                        <span className="inline-flex items-center gap-1.5 rounded-lg bg-page px-2.5 py-1 text-ink-secondary">
                          {cls?.online ? <Video size={14} aria-hidden /> : <MapPin size={14} aria-hidden />}
                          {cls?.online ? 'Online' : `Phòng ${s && cls ? (sessionRoom(s, cls) ?? '—') : '—'}`}
                        </span>
                        <span className="inline-flex items-center gap-1.5 rounded-lg bg-page px-2.5 py-1 text-ink-secondary">
                          <Users size={14} aria-hidden /> {roster} học viên
                        </span>
                      </div>
                      <div className="flex items-start gap-2 rounded-lg border border-line px-3 py-2 text-[13px]">
                        <UserRound size={15} className="mt-0.5 shrink-0 text-ink-muted" aria-hidden />
                        <span>
                          Dạy thay cho <b className="text-ink">{teacherName(inv.fromTeacherId)}</b>
                          <span className="text-ink-secondary"> — {inv.reason}</span>
                          <span className="block text-xs text-ink-muted">
                            {inv.invitedBy} mời {fromNow(inv.createdAt)}
                          </span>
                        </span>
                      </div>
                      {conflict && (
                        <p className="flex items-center gap-1.5 rounded-lg bg-danger-50 px-3 py-2 text-[13px] text-danger-700">
                          <TriangleAlert size={15} className="shrink-0" aria-hidden /> Trùng buổi dạy của bạn {conflict.startTime}–{conflict.endTime} cùng ngày — không nhận được.
                        </p>
                      )}
                    </div>

                    {/* Thu nhập + trả lời */}
                    <div className="flex shrink-0 flex-col justify-between gap-3 border-t border-line bg-page/60 p-4 md:w-60 md:border-l md:border-t-0 md:p-5">
                      <div>
                        <p className="text-xs text-ink-muted">Ước tính thu nhập</p>
                        <p className="tabular text-xl font-bold text-success-700">{formatCurrency(payOf(inv)) || '—'}</p>
                        <p className="text-xs text-ink-muted">{hoursOf(s)} giờ · + phụ cấp dạy thay</p>
                      </div>
                      <div className={cn('rounded-lg px-3 py-2 text-[13px]', left.urgent ? 'bg-danger-50 text-danger-700' : 'bg-warning-50 text-warning-700')}>
                        <p className="flex items-center gap-1.5 font-medium">
                          <Hourglass size={14} className="shrink-0" aria-hidden /> {left.text} để trả lời
                        </p>
                        <p className="tabular text-xs opacity-80">Hạn {dayjs(inv.respondBy).format('HH:mm, DD/MM')}</p>
                      </div>
                      <div className="flex gap-2 md:flex-col">
                        <Button icon={Check} tone="success" block disabled={Boolean(conflict)} onClick={() => accept(inv)}>
                          Nhận dạy
                        </Button>
                        <Button icon={X} variant="outline" tone="neutral" block onClick={() => decline(inv)}>
                          Từ chối
                        </Button>
                      </div>
                    </div>
                  </div>
                </article>
              );
            })}
          </div>
        ) : (
          <div className="rounded-[var(--radius-card)] border border-line bg-surface">
            <EmptyState icon={ArrowLeftRight} title="Không có lời mời nào đang chờ" description="Khi giáo vụ mời bạn dạy thay, lời mời sẽ hiện ở đây và bạn nhận được thông báo." />
          </div>
        )
      ) : history.length ? (
        <ul className="divide-y divide-line overflow-hidden rounded-[var(--radius-card)] border border-line bg-surface">
          {history.map((inv) => {
            const st = STATUS[inv.status];
            return (
              <li key={inv.id} className="flex flex-col gap-2 px-4 py-3 sm:flex-row sm:items-center sm:gap-4">
                <span className="tabular w-28 shrink-0 text-[13px]">
                  <span className="block font-semibold text-ink">{inv.s ? dayjs(inv.s.date).format('DD/MM/YYYY') : '—'}</span>
                  <span className="text-xs text-ink-muted">{inv.s ? `${inv.s.startTime}–${inv.s.endTime}` : ''}</span>
                </span>
                <span className="min-w-0 flex-1 text-[13px]">
                  <span className="block font-medium text-ink">
                    {inv.cls ? shortCode(inv.cls) : inv.classId} · Buổi {inv.s?.no}
                    <span className="font-normal text-ink-muted"> · thay {teacherName(inv.fromTeacherId)}</span>
                  </span>
                  <span className="block truncate text-xs text-ink-muted">
                    {inv.reason}
                    {inv.declineReason && ` · Lý do từ chối: ${inv.declineReason}`}
                  </span>
                </span>
                {inv.status === 'accepted' && <span className="tabular text-[13px] font-medium text-success-700">{formatCurrency(payOf(inv))}</span>}
                <StatusTag tone={st.tone} icon={st.icon} size="sm">
                  {st.label}
                </StatusTag>
              </li>
            );
          })}
        </ul>
      ) : (
        <div className="rounded-[var(--radius-card)] border border-line bg-surface">
          <EmptyState icon={ArrowLeftRight} title="Chưa có lịch sử" />
        </div>
      )}
      {confirmDialog}
    </>
  );
}
