import type { ReactNode } from 'react';
import { Breadcrumb } from 'antd';
import { cn } from '@/lib/cn';
import { Button } from '../Button/Button';

export interface BreadcrumbItem {
  title: ReactNode;
  href?: string;
  onClick?: () => void;
}

export interface PageHeaderProps {
  title: ReactNode;
  /** Mô tả ngắn dưới tiêu đề */
  subtitle?: ReactNode;
  breadcrumb?: BreadcrumbItem[];
  /** Hiện nút quay lại bên trái tiêu đề */
  onBack?: () => void;
  /** Tag/trạng thái cạnh tiêu đề (vd: <StatusTag />) */
  tags?: ReactNode;
  /** Nút hành động bên phải — mobile xuống dòng dưới tiêu đề */
  actions?: ReactNode;
  /** Nội dung phụ dưới cùng (tabs, thông tin tóm tắt...) */
  footer?: ReactNode;
  className?: string;
}

/** Đầu trang thống nhất: breadcrumb · (quay lại) tiêu đề + tag · mô tả · hành động. */
export function PageHeader({ title, subtitle, breadcrumb, onBack, tags, actions, footer, className }: PageHeaderProps) {
  return (
    <header className={cn('flex flex-col gap-3 pb-4 sm:pb-6', className)}>
      {breadcrumb && breadcrumb.length > 0 && (
        <Breadcrumb
          className="text-[13px] max-sm:hidden"
          items={breadcrumb.map((b) => ({
            title: b.title,
            href: b.href,
            onClick: b.onClick
              ? (e) => {
                  e.preventDefault();
                  b.onClick?.();
                }
              : undefined,
          }))}
        />
      )}

      <div className="flex flex-col gap-3 md:flex-row md:items-start md:justify-between">
        <div className="flex min-w-0 items-start gap-2">
          {onBack && <Button action="back" iconOnly onClick={onBack} className="-ml-2 mt-[-4px]" />}
          <div className="min-w-0">
            <div className="flex flex-wrap items-center gap-2">
              <h1 className="text-xl font-semibold leading-7 text-ink sm:text-2xl sm:leading-8">{title}</h1>
              {tags}
            </div>
            {subtitle && <p className="mt-1 text-ink-muted">{subtitle}</p>}
          </div>
        </div>
        {actions && <div className="flex flex-wrap items-center gap-2 md:shrink-0 md:justify-end">{actions}</div>}
      </div>

      {footer}
    </header>
  );
}
