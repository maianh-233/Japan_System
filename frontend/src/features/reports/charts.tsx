import { Tooltip } from 'antd';
import type { ReactNode } from 'react';
import { cn } from '@/lib/cn';

/**
 * Biểu đồ nhẹ dùng cho báo cáo (CSS thuần, không thư viện):
 *  - MỘT chuỗi / biểu đồ, một sắc độ (primary) → không cần chú giải, tiêu đề nêu đại lượng
 *  - Thanh mảnh, đầu bo 4px, khoảng hở giữa các thanh; số liệu trong tooltip khi hover / focus
 *  - Chữ dùng màu chữ (ink), không dùng màu của thanh
 */

export interface Datum {
  key: string;
  label: string;
  value: number;
  /** Nội dung tooltip (mặc định = giá trị đã định dạng) */
  tip?: ReactNode;
  /** Làm nổi bật (vd tháng hiện tại) */
  highlight?: boolean;
}

/** Cột dọc theo thời gian */
export function ColumnChart({ data, format, height = 160, ariaLabel }: { data: Datum[]; format: (v: number) => string; height?: number; ariaLabel: string }) {
  const max = Math.max(1, ...data.map((d) => d.value));
  return (
    <figure aria-label={ariaLabel}>
      <div className="relative flex items-end gap-1.5 border-b border-line" style={{ height }}>
        {/* Lưới mờ 50% */}
        <span className="pointer-events-none absolute inset-x-0 border-t border-dashed border-line/70" style={{ bottom: height / 2 }} aria-hidden />
        {data.map((d) => (
          <Tooltip key={d.key} title={d.tip ?? `${d.label}: ${format(d.value)}`}>
            <button type="button" className="group relative flex h-full flex-1 items-end justify-center focus:outline-none" aria-label={`${d.label}: ${format(d.value)}`}>
              <span
                className={cn('w-full max-w-10 rounded-t transition-colors', d.highlight ? 'bg-primary-600' : 'bg-primary-400 group-hover:bg-primary-500 group-focus-visible:bg-primary-500')}
                style={{ height: `${Math.max(d.value ? 2 : 0, (d.value / max) * 100)}%` }}
              />
            </button>
          </Tooltip>
        ))}
      </div>
      <div className="mt-1 flex gap-1.5">
        {data.map((d) => (
          <span key={d.key} className={cn('flex-1 text-center text-[11px]', d.highlight ? 'font-semibold text-ink' : 'text-ink-muted')}>
            {d.label}
          </span>
        ))}
      </div>
    </figure>
  );
}

/** Thanh ngang xếp hạng (nhãn trái · thanh · giá trị phải) */
export function BarList({ data, format, max, labelWidth = 120, ariaLabel }: { data: Datum[]; format: (v: number) => string; max?: number; labelWidth?: number; ariaLabel: string }) {
  const top = max ?? Math.max(1, ...data.map((d) => d.value));
  return (
    <ul className="flex flex-col gap-2" aria-label={ariaLabel}>
      {data.map((d) => (
        <li key={d.key} className="grid items-center gap-2 text-[13px]" style={{ gridTemplateColumns: `${labelWidth}px 1fr 64px` }}>
          <span className="truncate text-ink-secondary" title={d.label}>
            {d.label}
          </span>
          <Tooltip title={d.tip ?? `${d.label}: ${format(d.value)}`}>
            <span className="h-4 rounded bg-canvas" tabIndex={0} aria-label={`${d.label}: ${format(d.value)}`}>
              <span className={cn('block h-full rounded', d.highlight ? 'bg-primary-600' : 'bg-primary-400')} style={{ width: `${Math.min(100, (d.value / top) * 100)}%` }} />
            </span>
          </Tooltip>
          <span className="tabular text-right text-xs text-ink">{format(d.value)}</span>
        </li>
      ))}
    </ul>
  );
}

/** Khung 1 biểu đồ */
export function ChartCard({ title, subtitle, children, className, extra }: { title: string; subtitle?: string; children: ReactNode; className?: string; extra?: ReactNode }) {
  return (
    <section className={cn('rounded-xl border border-line bg-surface p-4', className)}>
      <div className="mb-3 flex items-start justify-between gap-2">
        <div>
          <h3 className="text-sm font-semibold text-ink">{title}</h3>
          {subtitle && <p className="text-xs text-ink-muted">{subtitle}</p>}
        </div>
        {extra}
      </div>
      {children}
    </section>
  );
}

/** "1.234.567 ₫" → "1,2 tr" */
export const shortMoney = (v: number) =>
  Math.abs(v) >= 1_000_000_000 ? `${(v / 1_000_000_000).toLocaleString('vi-VN', { maximumFractionDigits: 2 })} tỷ` : Math.abs(v) >= 1_000_000 ? `${(v / 1_000_000).toLocaleString('vi-VN', { maximumFractionDigits: 1 })} tr` : Math.abs(v) >= 1000 ? `${Math.round(v / 1000)}k` : `${v}`;
