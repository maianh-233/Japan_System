import type { ReactNode } from 'react';
import { ArrowDownRight, ArrowUpRight, Minus, type LucideIcon } from 'lucide-react';
import { cn } from '@/lib/cn';
import { iconToneClasses, type Tone } from '../tones';

export interface StatCardProps {
  label: ReactNode;
  value: ReactNode;
  icon: LucideIcon;
  tone?: Tone;
  /** % thay đổi so với kỳ trước, vd 12.5 hoặc -3 */
  trend?: number;
  /** Mô tả kỳ so sánh, vd "so với tháng trước" */
  trendLabel?: string;
  /** true khi GIẢM là tốt (công nợ, tỷ lệ nghỉ học...) */
  trendInverse?: boolean;
  /** Dòng phụ dưới số liệu */
  hint?: ReactNode;
  loading?: boolean;
  /** Bấm vào để mở danh sách chi tiết */
  onClick?: () => void;
  className?: string;
}

/** Ô số liệu dashboard: nhãn · icon · giá trị lớn · xu hướng tăng/giảm. */
export function StatCard({
  label,
  value,
  icon: Icon,
  tone = 'primary',
  trend,
  trendLabel = 'so với kỳ trước',
  trendInverse = false,
  hint,
  loading = false,
  onClick,
  className,
}: StatCardProps) {
  const direction = trend === undefined ? null : trend > 0 ? 'up' : trend < 0 ? 'down' : 'flat';
  const good = direction === 'flat' ? null : (direction === 'up') !== trendInverse;
  const TrendIcon = direction === 'up' ? ArrowUpRight : direction === 'down' ? ArrowDownRight : Minus;

  const Wrapper = onClick ? 'button' : 'div';
  return (
    <Wrapper
      type={onClick ? 'button' : undefined}
      onClick={onClick}
      className={cn(
        'flex w-full flex-col gap-3 rounded-[var(--radius-card)] border border-line bg-surface p-4 text-left shadow-[var(--shadow-soft-sm)] sm:p-5',
        onClick &&
          'cursor-pointer transition-colors hover:border-primary-300 hover:bg-primary-50/50 focus-visible:ring-[3px] focus-visible:ring-primary-500/30 focus-visible:outline-none',
        className,
      )}
    >
      <div className="flex items-start justify-between gap-3">
        <span className="text-[13px] font-medium text-ink-secondary">{label}</span>
        <span className={cn('flex h-10 w-10 shrink-0 items-center justify-center rounded-lg', iconToneClasses[tone])}>
          <Icon size={20} aria-hidden />
        </span>
      </div>

      {loading ? (
        <div className="flex flex-col gap-2" aria-busy="true">
          <div className="h-7 w-28 animate-pulse rounded-md bg-line/70" />
          <div className="h-3 w-20 animate-pulse rounded-md bg-line/70" />
        </div>
      ) : (
        <>
          <div className="tabular text-2xl leading-8 font-semibold text-ink">{value}</div>
          {(direction || hint) && (
            <div className="flex flex-wrap items-center gap-x-2 gap-y-1 text-[13px]">
              {direction && (
                <span
                  className={cn(
                    'tabular inline-flex items-center gap-0.5 rounded-md px-1.5 py-0.5 font-medium',
                    good === null
                      ? 'bg-disabled text-ink-secondary'
                      : good
                        ? 'bg-success-50 text-success-700'
                        : 'bg-danger-50 text-danger-700',
                  )}
                >
                  <TrendIcon size={14} aria-hidden />
                  {Math.abs(trend!).toLocaleString('vi-VN', { maximumFractionDigits: 1 })}%
                </span>
              )}
              {direction && <span className="text-ink-muted">{trendLabel}</span>}
              {hint && <span className="text-ink-muted">{hint}</span>}
            </div>
          )}
        </>
      )}
    </Wrapper>
  );
}
