import { Form } from 'antd';
import dayjs from 'dayjs';
import { AlarmClock, BadgeCheck, Download, Lock, LockOpen, Wallet } from 'lucide-react';
import { Button, ModalForm, NumberInput, Select, StatusTag, TextInput, useConfirm, useNotify } from '@/components/common';
import { useAuth } from '@/hooks/useAuth';
import { fmtDateTime } from '@/lib/date';
import { formatCurrency } from '@/lib/format';
import { useSettings } from '@/features/system/settings';
import { getBranches } from '@/features/system/useBranches';
import { USE_API } from '@/lib/api/config';
import { reportError } from '@/lib/api/report';
import { ALL_BRANCHES } from '@/types/auth';
import { useFinanceStore } from './financeStore';
import { addPayAdjust, payrollAction } from './payrollApi';
import { monthOptions } from './financeUtils';
import type { PayrollLock } from './types';

/** Các chi nhánh của phạm vi đang xem */
export const branchesOf = (branchId: string) => (branchId === ALL_BRANCHES ? getBranches(false).map((b) => b.id) : [branchId]);

/** Trạng thái chốt lương của tháng trong phạm vi chi nhánh */
export function usePayrollLock(kind: PayrollLock['kind'], month: string) {
  const { branchId } = useAuth();
  const locks = useFinanceStore((s) => s.locks);
  const ids = branchesOf(branchId);
  const mine = ids.map((b) => locks.find((l) => l.kind === kind && l.month === month && l.branchId === b));
  const locked = mine.every(Boolean);
  const paid = locked && mine.every((l) => l?.paidAt);
  return { ids, locks: mine.filter(Boolean) as PayrollLock[], locked, partial: !locked && mine.some(Boolean), paid };
}

/** Thanh chọn tháng + chốt / mở chốt / đã chi lương */
export function PayrollBar({
  kind,
  month,
  onMonth,
  totals,
  pendingWarning,
}: {
  kind: PayrollLock['kind'];
  month: string;
  onMonth: (m: string) => void;
  /** Tổng lương theo chi nhánh (để lưu vào bản chốt) */
  totals: Record<string, number>;
  /** Cảnh báo trước khi chốt (VD còn buổi chưa xác nhận) */
  pendingWarning?: string;
}) {
  const { user, can } = useAuth();
  const notify = useNotify();
  const { confirm, confirmDialog } = useConfirm();
  const { lockPayroll, unlockPayroll, markPaid } = useFinanceStore();
  const st = usePayrollLock(kind, month);
  const editable = can(kind === 'teacher' ? 'finance.teacher-payroll' : 'finance.staff-payroll');
  const sum = st.ids.reduce((n, b) => n + (totals[b] ?? 0), 0);
  // Hạn khóa: ngày `lockDay` của tháng SAU tháng lương (Cài đặt chung)
  const lockDay = useSettings((x) => x.payroll.lockDay);
  const nextMonth = dayjs(`${month}-01`).add(1, 'month');
  const deadline = nextMonth.date(Math.min(lockDay, nextMonth.daysInMonth()));
  const overdue = !st.locked && dayjs().isAfter(deadline.endOf('day'));

  const lock = async () => {
    const ok = await confirm({
      title: `Chốt bảng lương tháng ${month.slice(5)}/${month.slice(0, 4)}?`,
      description: `${pendingWarning ? `⚠ ${pendingWarning}. ` : ''}Tổng ${formatCurrency(sum)}. Sau khi chốt, số liệu không đổi nữa và phiếu lương được gửi cho từng người.`,
      confirmText: 'Chốt lương',
      tone: pendingWarning ? 'warning' : 'primary',
    });
    if (!ok) return;
    if (USE_API) {
      // Chỉ chốt chi nhánh chưa chốt (chi nhánh đã chốt server từ chối chốt lại khi chưa mở khóa)
      const open = st.ids.filter((b) => !st.locks.some((l) => l.branchId === b));
      const warnings = await run(() => payrollAction(kind, month, 'lock', open));
      if (!warnings) return;
      warnings.forEach((w) => notify.toast.warning(w));
    } else st.ids.forEach((b) => lockPayroll({ kind, month, branchId: b, lockedBy: user?.fullName ?? 'Kế toán', total: totals[b] ?? 0 }));
    notify.toast.success('Đã chốt bảng lương — phiếu lương đã gửi qua email / cổng nhân sự');
  };

  /** Chế độ API: gọi server, lỗi (đã chi, chưa chốt…) báo lại; trả undefined khi lỗi */
  const run = async <T,>(f: () => Promise<T>) => {
    try {
      return await f();
    } catch (e) {
      reportError(e);
      return undefined;
    }
  };

  const unlock = async () => {
    if (USE_API) {
      if ((await run(() => payrollAction(kind, month, 'unlock', st.ids))) === undefined) return;
    } else st.ids.forEach((b) => unlockPayroll(kind, month, b));
    notify.toast.info('Đã mở chốt — có thể điều chỉnh lại');
  };

  const paid = async () => {
    if (USE_API) {
      if ((await run(() => payrollAction(kind, month, 'paid', st.ids))) === undefined) return;
    } else st.ids.forEach((b) => markPaid(kind, month, b));
    notify.toast.success('Đã ghi nhận chuyển khoản lương');
  };

  return (
    <div className="mb-4 flex flex-wrap items-center gap-2 rounded-xl border border-line bg-surface p-3">
      <Select value={month} onChange={onMonth} options={monthOptions()} showSearch={false} allowClear={false} className="!w-44" />
      {st.paid ? (
        <StatusTag tone="success" icon={BadgeCheck}>Đã chi lương</StatusTag>
      ) : st.locked ? (
        <StatusTag tone="primary" icon={Lock}>Đã chốt</StatusTag>
      ) : (
        <StatusTag tone="warning" icon={LockOpen}>{st.partial ? 'Chốt một phần chi nhánh' : 'Đang tạm tính'}</StatusTag>
      )}
      {st.locks[0] && <span className="text-xs text-ink-muted">Chốt bởi {st.locks[0].lockedBy} · {fmtDateTime(st.locks[0].lockedAt)}</span>}
      {!st.locked &&
        (overdue ? (
          <StatusTag tone="danger" icon={AlarmClock}>Quá hạn khóa {deadline.format('DD/MM')}</StatusTag>
        ) : (
          <span className="text-xs text-ink-muted">Hạn khóa: {deadline.format('DD/MM/YYYY')}</span>
        ))}
      <div className="ml-auto flex flex-wrap gap-2">
        <Button icon={Download} variant="ghost" tone="neutral" size="sm" onClick={() => notify.toast.info('Đã xuất file Excel bảng lương (mô phỏng)')}>
          Xuất Excel
        </Button>
        {editable && !st.locked && (
          <Button icon={Lock} size="sm" onClick={lock}>
            Chốt bảng lương
          </Button>
        )}
        {editable && st.locked && !st.paid && (
          <>
            <Button icon={LockOpen} size="sm" variant="ghost" tone="neutral" onClick={() => void unlock()}>
              Mở chốt
            </Button>
            <Button icon={Wallet} size="sm" tone="success" onClick={() => void paid()}>
              Đã chuyển lương
            </Button>
          </>
        )}
      </div>
      {confirmDialog}
    </div>
  );
}

/** Thêm thưởng / phạt / phụ cấp tháng */
export function AdjustModal({ open, onClose, month, employee }: { open: boolean; onClose: () => void; month: string; employee?: { id: string; fullName: string } }) {
  const { user } = useAuth();
  const addAdjust = useFinanceStore((s) => s.addAdjust);
  return (
    <ModalForm<{ sign: '+' | '-'; amount: number; reason: string }>
      key={employee?.id ?? 'adj'}
      open={open}
      onClose={onClose}
      title={`Thưởng / phạt — ${employee?.fullName ?? ''}`}
      submitAction="save"
      successMessage="Đã thêm điều chỉnh"
      initialValues={{ sign: '+' }}
      onSubmit={async (v) => {
        const a = { month, employeeId: employee!.id, amount: v.sign === '-' ? -v.amount : v.amount, reason: v.reason.trim() };
        // Chế độ API: lỗi (tháng đã chốt…) hiện ngay trong form
        if (USE_API) await addPayAdjust(a);
        else addAdjust({ ...a, by: user?.fullName ?? 'Kế toán' });
      }}
    >
      <div className="grid grid-cols-[120px_1fr] gap-x-3">
        <Form.Item name="sign" label="Loại">
          <Select
            options={[
              { value: '+', label: 'Cộng' },
              { value: '-', label: 'Trừ' },
            ]}
            showSearch={false}
            allowClear={false}
          />
        </Form.Item>
        <Form.Item name="amount" label="Số tiền" rules={[{ required: true, message: 'Nhập số tiền' }]}>
          <NumberInput currency min={1000} step={100_000} />
        </Form.Item>
      </div>
      <Form.Item name="reason" label="Lý do" rules={[{ required: true, whitespace: true, message: 'Nhập lý do' }]}>
        <TextInput placeholder="VD: Thưởng lớp đạt tỷ lệ đỗ cao / Làm hỏng thiết bị" />
      </Form.Item>
    </ModalForm>
  );
}
