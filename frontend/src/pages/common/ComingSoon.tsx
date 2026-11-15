import { CircleCheck, Construction, Sparkles } from 'lucide-react';
import { useLocation } from 'react-router-dom';
import { EmptyState, PageHeader, StatusTag } from '@/components/common';
import { findNavItem } from '@/config/navigation';

/**
 * Trang chờ cho các mục chưa xây — tự lấy tiêu đề từ menu
 * và hiển thị danh sách "Chức năng dự kiến" (đặc tả cho team).
 */
export default function ComingSoon() {
  const { pathname } = useLocation();
  const found = findNavItem(pathname);
  const item = found?.item;
  const title = item?.label ?? 'Trang';

  return (
    <>
      <PageHeader
        title={title}
        tags={item?.isNew ? <StatusTag tone="success" icon={Sparkles}>Chức năng mới</StatusTag> : undefined}
        breadcrumb={found?.module ? [{ title: found.module.label }, { title }] : undefined}
      />
      <div className="grid grid-cols-1 gap-4 lg:grid-cols-[minmax(0,1fr)_minmax(0,1.2fr)]">
        <div className="rounded-[var(--radius-card)] border border-line bg-surface">
          <EmptyState
            icon={Construction}
            tone="warning"
            title="Trang đang được xây dựng"
            description={`Chức năng “${title}” sẽ được bổ sung ở các bước tiếp theo.`}
          />
        </div>
        {item?.features && item.features.length > 0 && (
          <section className="rounded-[var(--radius-card)] border border-line bg-surface p-4 sm:p-6">
            <h2 className="text-base font-semibold text-ink">Chức năng dự kiến</h2>
            <ul className="mt-3 flex flex-col gap-2.5">
              {item.features.map((f) => (
                <li key={f} className="flex items-start gap-2.5 text-ink-secondary">
                  <CircleCheck size={18} className="mt-0.5 shrink-0 text-primary-500" aria-hidden />
                  <span>{f}</span>
                </li>
              ))}
            </ul>
          </section>
        )}
      </div>
    </>
  );
}
