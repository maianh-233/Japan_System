import { cn } from '@/lib/cn';

export type SkeletonVariant = 'table' | 'cards' | 'list' | 'form' | 'detail' | 'stats';

export interface LoadingSkeletonProps {
  variant?: SkeletonVariant;
  /** Số dòng / thẻ */
  rows?: number;
  className?: string;
}

function Bone({ className }: { className?: string }) {
  return <div className={cn('animate-pulse rounded-md bg-line/70', className)} />;
}

/** Khung xương khi đang tải — giữ bố cục ổn định, không nhảy trang. */
export function LoadingSkeleton({ variant = 'list', rows = 5, className }: LoadingSkeletonProps) {
  const items = Array.from({ length: rows });

  const body = (() => {
    switch (variant) {
      case 'table':
        return (
          <div className="overflow-hidden rounded-[var(--radius-card)] border border-line">
            <div className="flex gap-4 border-b border-line bg-primary-50 px-4 py-3">
              {[30, 20, 25, 15].map((w) => (
                <div key={w} style={{ width: `${w}%` }}>
                  <Bone className="h-4" />
                </div>
              ))}
            </div>
            {items.map((_, r) => (
              <div key={r} className="flex items-center gap-4 border-b border-line px-4 py-4 last:border-b-0">
                <div className="flex w-[30%] items-center gap-3">
                  <Bone className="h-8 w-8 shrink-0 rounded-full" />
                  <Bone className="h-4 flex-1" />
                </div>
                <Bone className="h-4 w-[20%]" />
                <Bone className="h-4 w-[25%]" />
                <Bone className="h-6 w-[15%] rounded-md" />
              </div>
            ))}
          </div>
        );
      case 'cards':
        return (
          <div className="grid grid-cols-1 gap-3 sm:grid-cols-2 lg:grid-cols-3">
            {items.map((_, i) => (
              <div key={i} className="flex flex-col gap-3 rounded-[var(--radius-card)] border border-line bg-surface p-4">
                <div className="flex items-center gap-3">
                  <Bone className="h-10 w-10 rounded-full" />
                  <div className="flex flex-1 flex-col gap-2">
                    <Bone className="h-4 w-2/3" />
                    <Bone className="h-3 w-1/3" />
                  </div>
                </div>
                <Bone className="h-3 w-full" />
                <Bone className="h-3 w-4/5" />
              </div>
            ))}
          </div>
        );
      case 'form':
        return (
          <div className="grid grid-cols-1 gap-4 md:grid-cols-2">
            {items.map((_, i) => (
              <div key={i} className="flex flex-col gap-2">
                <Bone className="h-3 w-24" />
                <Bone className="h-10 w-full rounded-[var(--radius-control)]" />
              </div>
            ))}
          </div>
        );
      case 'detail':
        return (
          <div className="flex flex-col gap-6">
            <div className="flex items-center gap-4">
              <Bone className="h-16 w-16 rounded-full" />
              <div className="flex flex-1 flex-col gap-2">
                <Bone className="h-5 w-1/3" />
                <Bone className="h-4 w-1/4" />
              </div>
            </div>
            <div className="grid grid-cols-1 gap-4 sm:grid-cols-2">
              {items.map((_, i) => (
                <div key={i} className="flex flex-col gap-2">
                  <Bone className="h-3 w-20" />
                  <Bone className="h-4 w-3/4" />
                </div>
              ))}
            </div>
          </div>
        );
      case 'stats':
        return (
          <div className="grid grid-cols-1 gap-3 sm:grid-cols-2 xl:grid-cols-4">
            {items.map((_, i) => (
              <div key={i} className="flex flex-col gap-3 rounded-[var(--radius-card)] border border-line bg-surface p-4">
                <div className="flex items-center justify-between">
                  <Bone className="h-4 w-24" />
                  <Bone className="h-10 w-10 rounded-lg" />
                </div>
                <Bone className="h-7 w-32" />
                <Bone className="h-3 w-20" />
              </div>
            ))}
          </div>
        );
      default:
        return (
          <div className="flex flex-col divide-y divide-line">
            {items.map((_, i) => (
              <div key={i} className="flex items-center gap-3 py-3">
                <Bone className="h-10 w-10 shrink-0 rounded-full" />
                <div className="flex flex-1 flex-col gap-2">
                  <Bone className="h-4 w-1/2" />
                  <Bone className="h-3 w-1/3" />
                </div>
              </div>
            ))}
          </div>
        );
    }
  })();

  return (
    <div role="status" aria-busy="true" aria-live="polite" className={className}>
      <span className="sr-only">Đang tải...</span>
      {body}
    </div>
  );
}
