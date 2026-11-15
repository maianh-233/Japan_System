import { useState, type ReactNode } from 'react';
import { cn } from '@/lib/cn';
import { getInitials, hashString } from '@/lib/text';

export type AvatarSize = 'xs' | 'sm' | 'md' | 'lg' | 'xl';

const sizes: Record<AvatarSize, { box: string; text: string; dot: string }> = {
  xs: { box: 'h-6 w-6', text: 'text-[10px]', dot: 'h-2 w-2' },
  sm: { box: 'h-8 w-8', text: 'text-xs', dot: 'h-2.5 w-2.5' },
  md: { box: 'h-10 w-10', text: 'text-sm', dot: 'h-3 w-3' },
  lg: { box: 'h-12 w-12', text: 'text-base', dot: 'h-3 w-3' },
  xl: { box: 'h-16 w-16', text: 'text-xl', dot: 'h-3.5 w-3.5' },
};

/** Cặp màu nền/chữ cho chữ viết tắt — đều đạt tương phản AA */
const palette = [
  'bg-primary-100 text-primary-800',
  'bg-success-50 text-success-700',
  'bg-warning-50 text-warning-700',
  'bg-[#F3E8FF] text-[#6B21A8]',
  'bg-[#E0F2FE] text-[#075985]',
  'bg-[#FCE7F3] text-[#9D174D]',
  'bg-[#ECFCCB] text-[#3F6212]',
];

export interface AvatarProps {
  src?: string | null;
  /** Họ tên — dùng làm chữ viết tắt khi không có ảnh và làm alt */
  name?: string;
  size?: AvatarSize;
  shape?: 'circle' | 'square';
  /** Chấm trạng thái góc dưới (vd: đang online / đã check-in) */
  status?: 'online' | 'offline' | 'busy';
  className?: string;
}

/** Ảnh đại diện; lỗi ảnh hoặc không có ảnh → chữ viết tắt với màu cố định theo tên. */
export function Avatar({ src, name, size = 'md', shape = 'circle', status, className }: AvatarProps) {
  const [broken, setBroken] = useState(false);
  const s = sizes[size];
  const showImage = src && !broken;
  const color = palette[hashString(name ?? '') % palette.length];

  return (
    <span className={cn('relative inline-flex shrink-0', className)}>
      <span
        className={cn(
          'inline-flex items-center justify-center overflow-hidden font-semibold select-none',
          s.box,
          s.text,
          shape === 'circle' ? 'rounded-full' : 'rounded-lg',
          !showImage && color,
        )}
        role="img"
        aria-label={name}
      >
        {showImage ? (
          <img src={src} alt="" className="h-full w-full object-cover" onError={() => setBroken(true)} />
        ) : (
          getInitials(name)
        )}
      </span>
      {status && (
        <span
          className={cn(
            'absolute right-0 bottom-0 rounded-full ring-2 ring-surface',
            s.dot,
            status === 'online' ? 'bg-success-600' : status === 'busy' ? 'bg-warning-600' : 'bg-ink-placeholder',
          )}
          aria-label={status === 'online' ? 'Đang hoạt động' : status === 'busy' ? 'Đang bận' : 'Ngoại tuyến'}
        />
      )}
    </span>
  );
}

export interface UserCellProps extends Pick<AvatarProps, 'src' | 'status' | 'shape'> {
  name: string;
  /** Dòng phụ: mã HV, email, số điện thoại, chức vụ... */
  subtitle?: ReactNode;
  /** Nội dung cuối dòng (tag, icon...) */
  extra?: ReactNode;
  size?: 'sm' | 'md' | 'lg';
  onClick?: () => void;
  className?: string;
}

/** Ảnh + tên + dòng phụ — dùng trong bảng, danh sách, header. */
export function UserCell({ name, subtitle, extra, size = 'md', onClick, className, ...avatar }: UserCellProps) {
  const content = (
    <>
      <Avatar name={name} size={size} {...avatar} />
      <span className="flex min-w-0 flex-col text-left">
        <span className="truncate font-medium text-ink">{name}</span>
        {subtitle && <span className="truncate text-[13px] text-ink-muted">{subtitle}</span>}
      </span>
      {extra && <span className="ml-auto shrink-0">{extra}</span>}
    </>
  );
  const base = cn('flex min-w-0 items-center gap-3', className);
  return onClick ? (
    <button
      type="button"
      onClick={onClick}
      className={cn(base, 'rounded-lg text-left hover:text-primary-700 focus-visible:ring-[3px] focus-visible:ring-primary-500/30 focus-visible:outline-none')}
    >
      {content}
    </button>
  ) : (
    <div className={base}>{content}</div>
  );
}
