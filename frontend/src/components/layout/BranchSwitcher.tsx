import { Dropdown } from 'antd';
import { Building2, Check, ChevronDown, Globe, MapPin } from 'lucide-react';
import { cn } from '@/lib/cn';
import { useAuth } from '@/hooks/useAuth';
import { useNotify } from '@/components/common';
import { ALL_BRANCHES } from '@/types/auth';

/**
 * Chi nhánh đang quản lý (header cổng nhân viên).
 *  - Tài khoản global: mặc định "Toàn hệ thống", có dropdown chọn 1 chi nhánh bất kỳ.
 *  - Tài khoản chi nhánh: hiện tên chi nhánh; có dropdown nếu được gán ≥ 2 chi nhánh.
 */
export function BranchSwitcher({ className }: { className?: string }) {
  const { user, branches, branchId, currentBranch, setBranch } = useAuth();
  const notify = useNotify();
  if (!user) return null;

  const isGlobal = user.scope === 'global';
  const label = currentBranch ? currentBranch.name : 'Toàn hệ thống';
  const Icon = currentBranch ? Building2 : Globe;
  const options = [
    ...(isGlobal ? [{ id: ALL_BRANCHES, name: 'Toàn hệ thống', address: 'Xem dữ liệu tất cả chi nhánh' }] : []),
    ...branches,
  ];
  const switchable = options.length > 1;

  const chip = (
    <span
      className={cn(
        'inline-flex h-10 max-w-[220px] items-center gap-2 rounded-[var(--radius-control)] border border-line bg-surface px-3 text-sm',
        switchable && 'cursor-pointer hover:border-primary-400 hover:bg-primary-50',
        className,
      )}
    >
      <Icon size={16} className="shrink-0 text-primary-600" aria-hidden />
      <span className="truncate font-medium text-ink">{label}</span>
      {switchable && <ChevronDown size={14} className="shrink-0 text-ink-muted" aria-hidden />}
    </span>
  );

  if (!switchable) return chip;

  return (
    <Dropdown
      trigger={['click']}
      placement="bottomRight"
      menu={{
        selectable: true,
        selectedKeys: [branchId],
        onClick: ({ key }) => {
          if (key === branchId) return;
          setBranch(key);
          const name = options.find((o) => o.id === key)?.name;
          notify.toast.info(`Đang làm việc tại: ${name}`);
        },
        items: options.map((o) => ({
          key: o.id,
          label: (
            <span className="flex min-w-[240px] items-start gap-3 py-1">
              {o.id === ALL_BRANCHES ? (
                <Globe size={18} className="mt-0.5 text-primary-600" aria-hidden />
              ) : (
                <MapPin size={18} className="mt-0.5 text-ink-muted" aria-hidden />
              )}
              <span className="flex flex-1 flex-col">
                <span className="font-medium">{o.name}</span>
                <span className="text-xs text-ink-muted">{o.address}</span>
              </span>
              {o.id === branchId && <Check size={16} className="mt-0.5 text-primary-600" aria-hidden />}
            </span>
          ),
        })),
      }}
    >
      <button type="button" aria-label={`Chi nhánh: ${label}. Bấm để đổi`} className="rounded-[var(--radius-control)] focus-visible:ring-[3px] focus-visible:ring-primary-500/30 focus-visible:outline-none">
        {chip}
      </button>
    </Dropdown>
  );
}
