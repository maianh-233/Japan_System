import { useMemo, useState } from 'react';
import { Rate } from 'antd';
import dayjs from 'dayjs';
import { BellRing, MessageSquareQuote, Star } from 'lucide-react';
import { Button, EmptyState, Select, UserCell, useNotify } from '@/components/common';
import { useCrmStore } from '@/features/crm/crmStore';
import { useStudentStore } from '@/features/student/studentStore';
import { useAuth } from '@/hooks/useAuth';
import { cn } from '@/lib/cn';
import { useClassStore } from '../classStore';
import { teacherName } from '../constants';
import type { ClassInfo, ReviewCriterion, ReviewPeriod, TeacherReview } from '../types';

const PERIOD: Record<ReviewPeriod, string> = { mid: 'Giữa kỳ', final: 'Cuối kỳ' };
const CRITERIA: Record<ReviewCriterion, string> = {
  method: 'Phương pháp giảng dạy',
  clarity: 'Giảng dễ hiểu',
  enthusiasm: 'Nhiệt tình, hỗ trợ',
  punctuality: 'Đúng giờ',
};

const avg = (xs: number[]) => (xs.length ? xs.reduce((a, b) => a + b, 0) / xs.length : 0);

/** Tab Đánh giá GV: học viên đánh giá giáo viên của lớp (khảo sát giữa kỳ / cuối kỳ trên Cổng học viên) */
export function ClassTeacherReviewsTab({ cls }: { cls: ClassInfo }) {
  const { can } = useAuth();
  const notify = useNotify();
  const all = useClassStore((s) => s.teacherReviews);
  const students = useCrmStore((s) => s.students);
  const activeCount = useStudentStore((s) => s.enrollments.filter((e) => e.classId === cls.id && (e.status === 'studying' || e.status === 'completed')).length);
  const reviews = useMemo(() => all.filter((r) => r.classId === cls.id), [all, cls.id]);
  const periods = (['mid', 'final'] as ReviewPeriod[]).filter((p) => reviews.some((r) => r.period === p));
  const [period, setPeriod] = useState<ReviewPeriod | undefined>(periods[periods.length - 1]);
  const [stars, setStars] = useState<number>();

  if (!reviews.length)
    return (
      <EmptyState
        size="compact"
        title="Chưa có đánh giá"
        description="Đợt đánh giá giữa kỳ mở khi lớp học được nửa số buổi, cuối kỳ khi lớp kết thúc. Học viên đánh giá trên Cổng học viên."
        action={
          can('academic.classes') && cls.status === 'ongoing' ? (
            <Button icon={BellRing} variant="outline" onClick={() => notify.toast.success(`Đã mở khảo sát và gửi tới ${activeCount} học viên`)}>
              Mở khảo sát ngay
            </Button>
          ) : undefined
        }
      />
    );

  const inPeriod = reviews.filter((r) => !period || r.period === period);
  const respondents = new Set(inPeriod.map((r) => r.studentId)).size;

  return (
    <div className="flex flex-col gap-5">
      <div className="flex flex-wrap items-center gap-2">
        <Select<ReviewPeriod>
          value={period}
          onChange={setPeriod}
          showSearch={false}
          className="!w-40"
          options={periods.map((p) => ({ value: p, label: `Đợt ${PERIOD[p].toLowerCase()}` }))}
        />
        <span className="tabular text-[13px] text-ink-muted">
          {respondents}/{activeCount} học viên đã đánh giá ({activeCount ? Math.round((respondents / activeCount) * 100) : 0}%)
        </span>
        {can('academic.classes') && cls.status === 'ongoing' && respondents < activeCount && (
          <Button icon={BellRing} variant="outline" collapseOnMobile className="ml-auto" onClick={() => notify.toast.success(`Đã nhắc ${activeCount - respondents} học viên chưa đánh giá`)}>
            Nhắc học viên chưa đánh giá
          </Button>
        )}
      </div>

      {cls.teacherIds.map((tid) => (
        <TeacherBlock
          key={tid}
          teacherId={tid}
          reviews={inPeriod.filter((r) => r.teacherId === tid)}
          compare={period === 'final' ? reviews.filter((r) => r.teacherId === tid && r.period === 'mid') : undefined}
          stars={stars}
          onStars={setStars}
          studentName={(id) => students.find((s) => s.id === id)?.fullName ?? id}
        />
      ))}
    </div>
  );
}

function TeacherBlock({
  teacherId,
  reviews,
  compare,
  stars,
  onStars,
  studentName,
}: {
  teacherId: string;
  reviews: TeacherReview[];
  compare?: TeacherReview[];
  stars?: number;
  onStars: (n?: number) => void;
  studentName: (id: string) => string;
}) {
  const overall = avg(reviews.map((r) => r.overall));
  const prev = compare?.length ? avg(compare.map((r) => r.overall)) : undefined;
  const dist = [5, 4, 3, 2, 1].map((n) => ({ n, c: reviews.filter((r) => r.overall === n).length }));
  const comments = reviews.filter((r) => r.comment && (!stars || r.overall === stars)).sort((a, b) => b.createdAt.localeCompare(a.createdAt));

  return (
    <section className="rounded-[var(--radius-card)] border border-line">
      <header className="flex flex-wrap items-center gap-3 border-b border-line px-4 py-3">
        <UserCell name={teacherName(teacherId)} subtitle={`${reviews.length} lượt đánh giá`} className="min-w-0 flex-1" />
        {!reviews.length && <span className="text-[13px] text-ink-muted">Chưa có đánh giá trong đợt này</span>}
      </header>
      {reviews.length > 0 && (
        <div className="grid grid-cols-1 gap-6 p-4 md:grid-cols-[180px_1fr_1fr]">
          {/* Điểm tổng */}
          <div className="flex flex-col items-center justify-center gap-1 text-center">
            <span className={cn('tabular text-4xl font-bold', overall < 3.5 ? 'text-danger-600' : overall < 4.2 ? 'text-warning-700' : 'text-success-700')}>{overall.toFixed(1)}</span>
            <Rate disabled allowHalf value={Math.round(overall * 2) / 2} className="!text-base" />
            {prev !== undefined && (
              <span className={cn('text-xs', overall >= prev ? 'text-success-700' : 'text-danger-600')}>
                {overall >= prev ? '▲' : '▼'} {Math.abs(overall - prev).toFixed(1)} so với giữa kỳ
              </span>
            )}
          </div>
          {/* Phân bố sao — bấm để lọc nhận xét */}
          <div className="flex flex-col gap-1.5">
            {dist.map(({ n, c }) => (
              <button
                key={n}
                type="button"
                onClick={() => onStars(stars === n ? undefined : n)}
                className={cn('flex items-center gap-2 rounded px-1 text-[13px] hover:bg-page', stars === n && 'bg-primary-50')}
              >
                <span className="tabular flex w-8 items-center gap-0.5 text-ink-secondary">
                  {n} <Star size={12} className="fill-current text-warning-600" aria-hidden />
                </span>
                <span className="h-2 flex-1 overflow-hidden rounded-full bg-page">
                  <span className="block h-full rounded-full bg-warning-600" style={{ width: `${(c / reviews.length) * 100}%` }} />
                </span>
                <span className="tabular w-6 text-right text-ink-muted">{c}</span>
              </button>
            ))}
          </div>
          {/* Tiêu chí */}
          <div className="flex flex-col gap-2">
            {(Object.keys(CRITERIA) as ReviewCriterion[]).map((k) => {
              const v = avg(reviews.map((r) => r.ratings[k]));
              return (
                <div key={k} className="text-[13px]">
                  <div className="flex justify-between">
                    <span className="text-ink-secondary">{CRITERIA[k]}</span>
                    <span className={cn('tabular font-medium', v < 3.5 ? 'text-danger-600' : 'text-ink')}>{v.toFixed(1)}</span>
                  </div>
                  <span className="mt-0.5 block h-1.5 overflow-hidden rounded-full bg-page">
                    <span className={cn('block h-full rounded-full', v < 3.5 ? 'bg-danger-600' : 'bg-primary-500')} style={{ width: `${(v / 5) * 100}%` }} />
                  </span>
                </div>
              );
            })}
          </div>
        </div>
      )}
      {reviews.length > 0 && (
        <div className="border-t border-line p-4">
          <h4 className="mb-2 flex items-center gap-1.5 text-[13px] font-semibold text-ink">
            <MessageSquareQuote size={15} aria-hidden /> Nhận xét ({comments.length}){stars ? ` · ${stars} sao` : ''}
            {stars && (
              <button type="button" className="ml-1 text-xs font-normal text-primary-700 hover:underline" onClick={() => onStars(undefined)}>
                Bỏ lọc
              </button>
            )}
          </h4>
          {comments.length ? (
            <ul className="flex flex-col gap-2">
              {comments.map((r) => (
                <li key={r.id} className="rounded-lg bg-page px-3 py-2 text-[13px]">
                  <div className="flex flex-wrap items-center gap-2">
                    <Rate disabled value={r.overall} className="!text-xs" />
                    <span className="text-xs text-ink-muted">
                      {r.anonymous ? 'Ẩn danh' : studentName(r.studentId)} · {dayjs(r.createdAt).format('DD/MM/YYYY')}
                    </span>
                  </div>
                  <p className="mt-1 text-ink">{r.comment}</p>
                </li>
              ))}
            </ul>
          ) : (
            <p className="text-[13px] text-ink-muted">Không có nhận xét bằng chữ.</p>
          )}
        </div>
      )}
    </section>
  );
}
