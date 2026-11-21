import type { ReactNode } from 'react';
import { KeyRound, Lock, LockOpen, UserRoundPlus } from 'lucide-react';
import { Button, StatusTag, useConfirm, useNotify } from '@/components/common';
import { fmtDateTime, fromNow } from '@/lib/date';
import { usePortalAccountActions } from '../portalAccountApi';
import type { PortalAccount } from '../types';

interface Props {
  account?: PortalAccount;
  /** Tên chủ tài khoản — dùng trong câu xác nhận */
  ownerName: string;
  /** "Cổng học viên" / "Cổng phụ huynh" */
  portalLabel: string;
  /** Không có tài khoản → gọi để cấp mới. Bỏ trống = không cho cấp ở đây */
  onIssue?: () => void;
  compact?: boolean;
  emptyHint?: ReactNode;
}

/** Thông tin tài khoản cổng + thao tác: đặt lại mật khẩu, khóa / mở khóa. Dùng cho cả học viên và phụ huynh. */
export function AccountCard({ account, ownerName, portalLabel, onIssue, compact, emptyHint }: Props) {
  const notify = useNotify();
  const { confirm, confirmDialog } = useConfirm();
  const { resetPassword: reset, setLocked } = usePortalAccountActions();

  if (!account) {
    return (
      <div className="flex flex-wrap items-center justify-between gap-3 rounded-lg border border-dashed border-line bg-page px-3 py-2.5">
        <p className="text-[13px] text-ink-muted">{emptyHint ?? `Chưa có tài khoản ${portalLabel}`}</p>
        {onIssue && (
          <Button icon={UserRoundPlus} size="sm" variant="soft" onClick={onIssue}>
            Cấp tài khoản
          </Button>
        )}
      </div>
    );
  }

  const resetPassword = async () => {
    const ok = await confirm({
      title: `Đặt lại mật khẩu cho ${ownerName}?`,
      description: 'Hệ thống tạo mật khẩu tạm và gửi qua email (chưa có email → hiện để đưa trực tiếp). Mọi phiên đang đăng nhập bị đăng xuất; người dùng phải đổi mật khẩu ở lần đăng nhập tiếp theo.',
      tone: 'primary',
      confirmAction: 'send',
      confirmText: 'Đặt lại & gửi',
    });
    if (ok) await reset(account);
  };

  const toggleLock = async () => {
    if (account.status === 'locked') {
      if (await setLocked(account, false)) notify.toast.success(`Đã mở khóa ${account.username}`);
      return;
    }
    const ok = await confirm({
      title: `Khóa tài khoản ${account.username}?`,
      description: `${ownerName} sẽ không đăng nhập được ${portalLabel} cho đến khi được mở khóa.`,
      tone: 'danger',
      confirmAction: 'reject',
      confirmText: 'Khóa tài khoản',
    });
    if (ok && (await setLocked(account, true))) notify.success({ title: 'Đã khóa tài khoản', description: account.username });
  };

  const rows: [string, ReactNode][] = [
    ['Tên đăng nhập', <span className="font-mono">{account.username}</span>],
    ['Email nhận thông báo', account.email ?? '—'],
    ['Đăng nhập gần nhất', account.lastLoginAt ? `${fmtDateTime(account.lastLoginAt)} (${fromNow(account.lastLoginAt)})` : 'Chưa đăng nhập lần nào'],
  ];

  return (
    <div className={compact ? 'flex flex-col gap-2' : 'flex flex-col gap-3 rounded-[var(--radius-card)] border border-line bg-surface p-4'}>
      <div className="flex flex-wrap items-center justify-between gap-2">
        <span className="text-[13px] font-medium text-ink-secondary">{portalLabel}</span>
        <StatusTag group="account" status={account.status} size="sm" />
      </div>
      <dl className="grid grid-cols-1 gap-x-4 gap-y-1.5 text-[13px] sm:grid-cols-[150px_1fr]">
        {rows.map(([k, v]) => (
          <div key={k} className="contents">
            <dt className="text-ink-muted">{k}</dt>
            <dd className="min-w-0 break-words text-ink">{v}</dd>
          </div>
        ))}
      </dl>
      <div className="flex flex-wrap justify-end gap-1 border-t border-line pt-3">
        <Button icon={KeyRound} size="sm" tone="neutral" variant="ghost" onClick={resetPassword} disabled={account.status === 'locked'}>
          Đặt lại mật khẩu
        </Button>
        <Button
          icon={account.status === 'locked' ? LockOpen : Lock}
          size="sm"
          tone={account.status === 'locked' ? 'success' : 'danger'}
          variant="soft"
          onClick={toggleLock}
        >
          {account.status === 'locked' ? 'Mở khóa' : 'Khóa'}
        </Button>
      </div>
      {confirmDialog}
    </div>
  );
}
