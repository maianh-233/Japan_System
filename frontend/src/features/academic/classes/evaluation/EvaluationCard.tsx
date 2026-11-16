import { Star } from 'lucide-react';
import { StatusTag } from '@/components/common';
import { cn } from '@/lib/cn';
import { fmtDate } from '@/lib/date';
import { EVAL_RECOMMENDATION, EVAL_SKILLS } from '../constants';
import type { EvalSkill, StudentEvaluation } from '../types';

function Stars({ value }: { value: number }) {
  return (
    <span className="inline-flex gap-0.5" aria-label={`${value}/5`}>
      {[1, 2, 3, 4, 5].map((i) => (
        <Star key={i} size={13} className={cn(i <= value ? 'fill-[#f5a524] text-[#f5a524]' : 'text-line')} aria-hidden />
      ))}
    </span>
  );
}

/** Thẻ nhận xét cuối khóa — dùng ở Cổng GV, chi tiết lớp (giáo vụ) và hồ sơ học viên (Sale) */
export function EvaluationCard({ ev, classLabel, className }: { ev: StudentEvaluation; classLabel?: string; className?: string }) {
  const rec = EVAL_RECOMMENDATION[ev.recommendation];
  return (
    <div className={cn('flex flex-col gap-3 rounded-[var(--radius-card)] border border-line bg-surface p-4 text-[13px]', className)}>
      <div className="flex flex-wrap items-center gap-2">
        <span className="text-ink-muted">Đề xuất:</span>
        <StatusTag tone={rec.tone} icon={Star} size="sm">
          {rec.label}
        </StatusTag>
        <span className="ml-auto text-xs text-ink-muted">
          {classLabel ? `${classLabel} · ` : ''}
          {ev.author} · {fmtDate(ev.updatedAt)}
        </span>
      </div>
      <div className="grid gap-x-6 gap-y-1 sm:grid-cols-2">
        {(Object.keys(EVAL_SKILLS) as EvalSkill[]).map((k) => (
          <span key={k} className="flex items-center justify-between gap-2">
            <span className="text-ink-secondary">{EVAL_SKILLS[k]}</span>
            <Stars value={ev.skills[k]} />
          </span>
        ))}
        <span className="flex items-center justify-between gap-2">
          <span className="text-ink-secondary">Thái độ, chuyên cần</span>
          <Stars value={ev.attitude} />
        </span>
      </div>
      {ev.strengths && (
        <p>
          <span className="font-medium text-success-700">Điểm mạnh: </span>
          {ev.strengths}
        </p>
      )}
      {ev.weaknesses && (
        <p>
          <span className="font-medium text-warning-700">Cần cải thiện: </span>
          {ev.weaknesses}
        </p>
      )}
      {ev.advice && <p className="rounded-lg bg-primary-50 px-3 py-2 text-primary-800">Ghi chú cho Sale: {ev.advice}</p>}
    </div>
  );
}
