import { forwardRef, type ButtonHTMLAttributes, type ReactNode } from 'react';
import { Tooltip } from 'antd';
import { Loader2, type LucideIcon } from 'lucide-react';
import { cn } from '@/lib/cn';
import { BUTTON_ACTIONS, type ButtonAction, type ButtonTone, type ButtonVariant } from './buttonActions';

export type ButtonSize = 'sm' | 'md' | 'lg';

interface BaseProps extends Omit<ButtonHTMLAttributes<HTMLButtonElement>, 'type'> {
  children?: ReactNode;
  tone?: ButtonTone;
  variant?: ButtonVariant;
  size?: ButtonSize;
  loading?: boolean;
  /** Chiếm hết chiều ngang (hay dùng cho nút trong form mobile) */
  block?: boolean;
  /** Chỉ hiển thị icon (tự có tooltip + aria-label từ nhãn) */
  iconOnly?: boolean;
  /** Màn < 640px: ẩn chữ, chỉ còn icon */
  collapseOnMobile?: boolean;
  /** Mặc định "button" để không vô tình submit form */
  htmlType?: 'button' | 'submit' | 'reset';
  /** Nội dung tooltip; mặc định dùng nhãn khi iconOnly */
  tooltip?: ReactNode;
}

/**
 * Nút BẮT BUỘC có icon:
 *  - dùng `action` → icon/màu/nhãn tự gán theo chức năng, hoặc
 *  - tự truyền `icon` (+ `tone`, `variant`).
 */
export type ButtonProps = BaseProps &
  ({ action: ButtonAction; icon?: LucideIcon } | { action?: undefined; icon: LucideIcon });

const toneClasses: Record<ButtonTone, Record<ButtonVariant, string>> = {
  primary: {
    solid: 'bg-primary-600 text-white hover:bg-primary-500 active:bg-primary-700',
    soft: 'bg-primary-100 text-primary-700 hover:bg-primary-200 active:bg-primary-300',
    outline: 'border-primary-600 text-primary-700 bg-surface hover:bg-primary-50 active:bg-primary-100',
    ghost: 'text-primary-700 hover:bg-primary-100 active:bg-primary-200',
    link: 'text-primary-600 hover:text-primary-500 hover:underline',
  },
  success: {
    solid: 'bg-success-600 text-white hover:bg-success-700 active:bg-success-700',
    soft: 'bg-success-50 text-success-700 hover:bg-success-100',
    outline: 'border-success-600 text-success-700 bg-surface hover:bg-success-50',
    ghost: 'text-success-700 hover:bg-success-50',
    link: 'text-success-600 hover:underline',
  },
  warning: {
    solid: 'bg-warning-600 text-white hover:bg-warning-700 active:bg-warning-700',
    soft: 'bg-warning-50 text-warning-700 hover:bg-warning-100',
    outline: 'border-warning-600 text-warning-700 bg-surface hover:bg-warning-50',
    ghost: 'text-warning-700 hover:bg-warning-50',
    link: 'text-warning-600 hover:underline',
  },
  danger: {
    solid: 'bg-danger-600 text-white hover:bg-danger-700 active:bg-danger-700',
    soft: 'bg-danger-50 text-danger-700 hover:bg-danger-100',
    outline: 'border-danger-600 text-danger-700 bg-surface hover:bg-danger-50',
    ghost: 'text-danger-700 hover:bg-danger-50',
    link: 'text-danger-600 hover:underline',
  },
  neutral: {
    solid: 'bg-ink-secondary text-white hover:bg-ink active:bg-ink',
    soft: 'bg-disabled text-ink hover:bg-line',
    outline: 'border-line-strong text-ink bg-surface hover:bg-primary-50 hover:border-primary-400',
    ghost: 'text-ink-secondary hover:bg-primary-50 hover:text-ink',
    link: 'text-ink-secondary hover:text-ink hover:underline',
  },
};

const sizeClasses: Record<ButtonSize, { box: string; iconOnly: string; icon: number }> = {
  sm: { box: 'h-8 px-3 text-[13px] gap-1.5', iconOnly: 'h-8 w-8', icon: 14 },
  md: { box: 'h-10 px-4 text-sm gap-2', iconOnly: 'h-10 w-10', icon: 16 },
  lg: { box: 'h-11 px-5 text-[15px] gap-2', iconOnly: 'h-11 w-11', icon: 18 },
};

export const Button = forwardRef<HTMLButtonElement, ButtonProps>(function Button(
  {
    action,
    icon,
    tone,
    variant,
    size = 'md',
    loading = false,
    block = false,
    iconOnly = false,
    collapseOnMobile = false,
    htmlType = 'button',
    tooltip,
    disabled,
    className,
    children,
    'aria-label': ariaLabel,
    ...rest
  },
  ref,
) {
  const preset = action ? BUTTON_ACTIONS[action] : undefined;
  const Icon = (icon ?? preset?.icon) as LucideIcon;
  const finalTone = tone ?? preset?.tone ?? 'primary';
  const finalVariant = variant ?? preset?.variant ?? 'solid';
  const label = children ?? preset?.label;
  const textLabel = typeof label === 'string' ? label : undefined;
  const s = sizeClasses[size];
  const isLink = finalVariant === 'link';

  if (import.meta.env.DEV && iconOnly && !textLabel && !ariaLabel) {
    console.warn('[Button] Nút chỉ có icon cần `aria-label` hoặc nhãn dạng chữ.');
  }

  const button = (
    <button
      ref={ref}
      type={htmlType}
      disabled={disabled || loading}
      aria-busy={loading || undefined}
      aria-label={ariaLabel ?? (iconOnly || collapseOnMobile ? textLabel : undefined)}
      className={cn(
        'inline-flex shrink-0 select-none items-center justify-center whitespace-nowrap font-medium',
        'rounded-[var(--radius-control)] border border-transparent',
        'transition-colors duration-150 ease-out',
        'focus-visible:outline-none focus-visible:ring-[3px] focus-visible:ring-primary-500/30',
        'disabled:pointer-events-none disabled:opacity-50',
        isLink ? 'h-auto gap-1.5 px-0 text-sm' : iconOnly ? s.iconOnly : s.box,
        // Thu gọn trên mobile: vuông như iconOnly, từ sm trở lên trở lại bình thường
        collapseOnMobile && !iconOnly && !isLink && 'max-sm:w-10 max-sm:px-0',
        block && 'w-full',
        toneClasses[finalTone][finalVariant],
        className,
      )}
      {...rest}
    >
      {loading ? (
        <Loader2 size={s.icon} className="animate-spin" aria-hidden />
      ) : (
        <Icon size={s.icon} strokeWidth={2} aria-hidden />
      )}
      {!iconOnly && label !== undefined && (
        <span className={cn(collapseOnMobile && 'max-sm:sr-only')}>{label}</span>
      )}
    </button>
  );

  const tip = tooltip ?? (iconOnly ? label : undefined);
  return tip ? <Tooltip title={tip}>{button}</Tooltip> : button;
});
