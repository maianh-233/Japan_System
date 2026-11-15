import { cn } from '@/lib/cn';

export const SYSTEM_NAME = 'JP Center';
export const SYSTEM_TAGLINE = 'Hệ thống quản lý Trung tâm Nhật ngữ';

interface LogoProps {
  /** Ẩn chữ, chỉ còn biểu tượng */
  compact?: boolean;
  /** Dòng phụ dưới tên (vd: tên chi nhánh) */
  subtitle?: string;
  /** Dùng trên nền tối */
  inverted?: boolean;
  className?: string;
}

/** Logo hệ thống: ô vuông bo góc chữ 日 + tên. */
export function Logo({ compact = false, subtitle, inverted = false, className }: LogoProps) {
  return (
    <span className={cn('inline-flex min-w-0 items-center gap-2.5', className)}>
      <span
        className={cn(
          'flex h-9 w-9 shrink-0 items-center justify-center rounded-[10px] font-jp text-lg font-semibold shadow-[var(--shadow-soft-sm)]',
          inverted ? 'bg-white text-primary-700' : 'bg-primary-600 text-white',
        )}
        aria-hidden
      >
        日
      </span>
      {!compact && (
        <span className="flex min-w-0 flex-col leading-tight">
          <span className={cn('truncate text-base font-semibold', inverted ? 'text-white' : 'text-ink')}>{SYSTEM_NAME}</span>
          {subtitle && (
            <span className={cn('truncate text-xs', inverted ? 'text-primary-100' : 'text-ink-muted')}>{subtitle}</span>
          )}
        </span>
      )}
    </span>
  );
}
