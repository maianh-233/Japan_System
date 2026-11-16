import { useMemo } from 'react';
import { ArrowUpRight, CalendarDays, CircleCheck, Clock, Heart, MapPin, Video } from 'lucide-react';
import { Button, useNotify } from '@/components/common';
import { useClassStore } from '@/features/academic/classes/classStore';
import { effectiveStart, scheduleLabel, teacherNames } from '@/features/academic/classes/constants';
import { usePromotionStore } from '@/features/academic/promotion/promotionStore';
import { isOpen, viewStatus } from '@/features/academic/promotion/promotionUtils';
import { useDecider } from '@/features/academic/promotion/usePromotion';
import { useCrmStore } from '@/features/crm/crmStore';
import { fmtDate } from '@/lib/date';
import { formatCurrency } from '@/lib/format';

/**
 * Đề xuất lên lớp trên cổng — hiện cho NGƯỜI QUYẾT ĐỊNH:
 *  - Cổng học viên: chỉ học viên tự quyết (người lớn)
 *  - Cổng phụ huynh: các con mà phụ huynh quyết định (gom tất cả các con, không theo con đang chọn)
 * Bấm "Quan tâm" → Sale thấy ngay và ưu tiên gọi.
 */
export function PromotionOffers({ studentIds, viewer }: { studentIds: string[]; viewer: 'student' | 'parent' }) {
  const notify = useNotify();
  const candidates = usePromotionStore((s) => s.candidates);
  const markInterest = usePromotionStore((s) => s.markInterest);
  const classes = useClassStore((s) => s.classes);
  const students = useCrmStore((s) => s.students);
  const decider = useDecider();

  const offers = useMemo(
    () =>
      candidates
        .filter((c) => studentIds.includes(c.studentId) && c.toClassId && (isOpen(c) || c.status === 'confirmed') && viewStatus(c) !== 'released')
        .filter((c) => (viewer === 'student' ? decider(c.studentId).kind === 'self' : decider(c.studentId).kind === 'guardian'))
        .map((c) => ({ c, to: classes.find((x) => x.id === c.toClassId)!, from: classes.find((x) => x.id === c.fromClassId), name: students.find((s) => s.id === c.studentId)?.fullName ?? '' }))
        .filter((x) => x.to),
    [candidates, studentIds, viewer, decider, classes, students],
  );

  if (!offers.length) return null;

  return (
    <section className="mb-6 flex flex-col gap-3">
      <h2 className="flex items-center gap-2 text-lg font-semibold">
        <ArrowUpRight size={20} className="text-primary-600" aria-hidden />
        {viewer === 'parent' ? 'Đề xuất lên lớp cho con' : 'Đề xuất lên lớp kỳ sau'}
      </h2>
      <div className="grid gap-3 md:grid-cols-2">
        {offers.map(({ c, to, from, name }) => (
          <article key={c.id} className="flex flex-col gap-3 rounded-[var(--radius-card)] border border-primary-200 bg-gradient-to-br from-primary-50 to-surface p-4">
            <div>
              {viewer === 'parent' && <p className="text-xs font-medium uppercase tracking-wide text-primary-700">{name}</p>}
              <p className="text-base font-semibold text-ink">
                {to.courseLabel} · lớp {to.code.replace(/^GV[VN]*_/, '')}
              </p>
              <p className="text-[13px] text-ink-secondary">
                {c.seatHeld ? 'Trung tâm đã giữ chỗ' : 'Trung tâm đề xuất'} sau khi {viewer === 'parent' ? 'con' : 'bạn'} hoàn thành lớp {from?.code.replace(/^GV[VN]*_/, '') ?? ''}.
              </p>
            </div>
            <ul className="flex flex-col gap-1 text-[13px] text-ink-secondary">
              <li className="flex items-center gap-1.5">
                <CalendarDays size={14} aria-hidden /> Khai giảng {fmtDate(effectiveStart(to))}
              </li>
              <li className="flex items-center gap-1.5">
                <Clock size={14} aria-hidden /> {scheduleLabel(to)} · GV {teacherNames(to.teacherIds)}
              </li>
              <li className="flex items-center gap-1.5">
                {to.online ? <Video size={14} aria-hidden /> : <MapPin size={14} aria-hidden />} {to.online ? 'Học online' : `Phòng ${to.room ?? '—'}`} · Học phí {formatCurrency(to.fee)}
              </li>
            </ul>
            {c.status === 'confirmed' ? (
              <p className="flex items-center gap-1.5 text-[13px] font-medium text-success-700">
                <CircleCheck size={16} aria-hidden /> Đã ghi danh — hóa đơn học phí ở mục Học phí.
              </p>
            ) : c.interest ? (
              <p className="flex items-center gap-1.5 text-[13px] font-medium text-success-700">
                <Heart size={16} className="fill-current" aria-hidden /> Đã báo quan tâm — tư vấn viên sẽ liên hệ sớm.
              </p>
            ) : (
              <div className="flex flex-wrap items-center gap-2">
                <Button
                  icon={Heart}
                  size="sm"
                  onClick={() => {
                    markInterest(c.id, viewer);
                    notify.success({ title: 'Đã gửi', description: 'Tư vấn viên sẽ liên hệ để hoàn tất ghi danh.' });
                  }}
                >
                  {viewer === 'parent' ? 'Cho con học tiếp' : 'Tôi muốn học tiếp'}
                </Button>
                {c.seatHeld && c.holdUntil && <span className="text-xs text-ink-muted">Giữ chỗ đến {fmtDate(c.holdUntil)}</span>}
              </div>
            )}
          </article>
        ))}
      </div>
    </section>
  );
}
