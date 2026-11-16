import { Avatar } from '@/components/common';
import { cn } from '@/lib/cn';
import { usePortalStore } from '../portalStore';
import { useChildren, useStudentId } from '../usePortal';

/** Phụ huynh có nhiều con → chọn con đang xem (giữ lựa chọn khi chuyển trang) */
export function ChildSwitcher({ className }: { className?: string }) {
  const children = useChildren();
  const current = useStudentId();
  const setChild = usePortalStore((s) => s.setChild);
  if (children.length < 2) return null;
  return (
    <div className={cn('mb-4 flex gap-2 overflow-x-auto pb-1', className)} role="tablist" aria-label="Chọn con">
      {children.map((c) => {
        const active = c.id === current;
        return (
          <button
            key={c.id}
            type="button"
            role="tab"
            aria-selected={active}
            onClick={() => setChild(c.id)}
            className={cn(
              'inline-flex shrink-0 items-center gap-2 rounded-full border py-1 pl-1 pr-3.5 text-sm font-medium transition-colors',
              active ? 'border-primary-600 bg-primary-600 text-white' : 'border-line bg-surface text-ink-secondary hover:border-primary-300',
            )}
          >
            <Avatar name={c.fullName} size="sm" />
            {c.fullName.split(' ').slice(-2).join(' ')}
          </button>
        );
      })}
    </div>
  );
}
