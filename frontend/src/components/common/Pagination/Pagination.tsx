import { Pagination as AntPagination } from 'antd';
import { cn } from '@/lib/cn';
import { formatNumber } from '@/lib/format';
import { useBreakpoint } from '@/hooks/useBreakpoint';

export interface PaginationProps {
  /** Trang hiện tại (bắt đầu từ 1) */
  page: number;
  pageSize: number;
  total: number;
  onChange: (page: number, pageSize: number) => void;
  pageSizeOptions?: number[];
  /** Ẩn khi chỉ có 1 trang */
  hideOnSinglePage?: boolean;
  /** Đơn vị trong câu tóm tắt: "bản ghi", "học viên", "lead"... */
  unitLabel?: string;
  disabled?: boolean;
  className?: string;
}

/**
 * Phân trang: "Hiển thị 1–10 / 120 học viên" + chọn số dòng.
 * Mobile: dạng rút gọn ‹ 3 / 12 ›, ẩn chọn số dòng.
 */
export function Pagination({
  page,
  pageSize,
  total,
  onChange,
  pageSizeOptions = [10, 20, 50, 100],
  hideOnSinglePage = false,
  unitLabel = 'bản ghi',
  disabled,
  className,
}: PaginationProps) {
  const { isMobile, md } = useBreakpoint();
  if (hideOnSinglePage && total <= pageSize) return null;

  const from = total === 0 ? 0 : (page - 1) * pageSize + 1;
  const to = Math.min(page * pageSize, total);

  return (
    <nav
      aria-label="Phân trang"
      className={cn('flex flex-col items-center gap-3 py-3 sm:flex-row sm:justify-between', className)}
    >
      <p className="tabular text-[13px] text-ink-muted">
        Hiển thị <span className="font-medium text-ink">{formatNumber(from)}</span>–
        <span className="font-medium text-ink">{formatNumber(to)}</span> / {formatNumber(total)} {unitLabel}
      </p>
      <AntPagination
        current={page}
        pageSize={pageSize}
        total={total}
        onChange={onChange}
        disabled={disabled}
        simple={isMobile}
        showSizeChanger={md}
        pageSizeOptions={pageSizeOptions}
        showLessItems={!md}
        locale={{ items_per_page: '/ trang' }}
        className="tabular"
      />
    </nav>
  );
}
