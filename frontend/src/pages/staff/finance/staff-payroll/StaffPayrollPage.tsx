import { useMemo, useState } from 'react';
import { Drawer } from 'antd';
import dayjs from 'dayjs';
import { BadgePercent, CalendarX2, HandCoins, Plus, ShieldCheck, Trash2, Users } from 'lucide-react';
import { Button, DataTable, PageHeader, StatCard, UserCell, type DataColumn } from '@/components/common';
import { INSURANCE_RATE, LATE_FINE, NO_OUT_FINE, PERSONAL_DEDUCTION, STANDARD_DAYS } from '@/features/finance/constants';
import { useFinanceStore } from '@/features/finance/financeStore';
import { saleResults, staffPayroll, useInvoiceRows, type StaffPay } from '@/features/finance/financeUtils';
import { AdjustModal, PayrollBar, usePayrollLock } from '@/features/finance/PayrollBar';
import { DEPARTMENTS, EMPLOYEE_STATUS } from '@/features/hr/constants';
import { useHrStore } from '@/features/hr/hrStore';
import { useWorkStore } from '@/features/work/workStore';
import { useAuth } from '@/hooks/useAuth';
import { useBreakpoint } from '@/hooks/useBreakpoint';
import { cn } from '@/lib/cn';
import { formatCurrency } from '@/lib/format';
import { ALL_BRANCHES } from '@/types/auth';

/** Lương nhân viên: lương cứng theo công thực tế (Chấm công / Xếp ca) + phụ cấp + hoa hồng − khấu trừ. DỮ LIỆU GIẢ. */
export default function StaffPayrollPage() {
  const { branchId } = useAuth();
  const { isMobile } = useBreakpoint();
  const employees = useHrStore((s) => s.employees);
  const records = useHrStore((s) => s.records);
  const requests = useHrStore((s) => s.requests);
  const shifts = useWorkStore((s) => s.shifts);
  const { adjusts, tiers, targets, removeAdjust } = useFinanceStore();
  const invoiceRows = useInvoiceRows();

  const [month, setMonth] = useState(dayjs().format('YYYY-MM'));
  const [openId, setOpenId] = useState<string | null>(null);
  const [adjFor, setAdjFor] = useState<StaffPay | null>(null);
  const { locked } = usePayrollLock('staff', month);
  const current = month === dayjs().format('YYYY-MM');

  const commissions = useMemo(() => Object.fromEntries(saleResults(month, employees, invoiceRows, tiers, targets).map((r) => [r.employee.id, r.commission])), [month, employees, invoiceRows, tiers, targets]);
  const all = useMemo(() => staffPayroll(month, employees, records, shifts, requests, adjusts, commissions), [month, employees, records, shifts, requests, adjusts, commissions]);
  const rows = all.filter((p) => branchId === ALL_BRANCHES || p.employee.branchId === branchId).sort((a, b) => a.employee.department.localeCompare(b.employee.department) || b.net - a.net);
  const totals = rows.reduce<Record<string, number>>((m, p) => ({ ...m, [p.employee.branchId]: (m[p.employee.branchId] ?? 0) + p.net }), {});
  const open = rows.find((p) => p.employee.id === openId);

  const money = (v: number, tone?: 'plus' | 'minus') =>
    v ? <span className={cn('tabular text-[13px]', tone === 'plus' && 'text-success-700', tone === 'minus' && 'text-danger-600')}>{tone === 'minus' ? '−' : tone === 'plus' ? '+' : ''}{formatCurrency(Math.abs(v))}</span> : <span className="text-ink-muted">—</span>;

  const columns: DataColumn<StaffPay>[] = [
    { key: 'name', title: 'Nhân viên', width: 210, mobile: 'title', render: (_, p) => <UserCell size="sm" name={p.employee.fullName} subtitle={`${DEPARTMENTS[p.employee.department]}${p.employee.status !== 'active' ? ` · ${EMPLOYEE_STATUS[p.employee.status].label}` : ''}`} /> },
    {
      key: 'days',
      title: `Công / ${STANDARD_DAYS}`,
      width: 110,
      align: 'center',
      render: (_, p) => (
        <div className="leading-5">
          <p className="tabular font-medium">{p.payDays}</p>
          <p className="text-[11px] text-ink-muted">
            {p.paidLeave > 0 && `${p.paidLeave} phép`}
            {p.absent > 0 && <span className="text-danger-600"> {p.absent} vắng</span>}
          </p>
        </div>
      ),
    },
    { key: 'base', title: 'Lương theo công', width: 140, align: 'right', render: (_, p) => <span className="tabular text-[13px]">{formatCurrency(p.basePay)}</span> },
    { key: 'allowance', title: 'Phụ cấp', width: 110, align: 'right', render: (_, p) => money(p.allowance) },
    { key: 'commission', title: 'Hoa hồng', width: 120, align: 'right', render: (_, p) => money(p.commission, 'plus') },
    { key: 'adjust', title: 'Thưởng / phạt', width: 120, align: 'right', render: (_, p) => money(p.adjust, p.adjust > 0 ? 'plus' : 'minus') },
    { key: 'fines', title: 'Đi muộn / quên chấm', width: 130, align: 'right', render: (_, p) => money(p.fines, 'minus') },
    { key: 'ins', title: 'BH + thuế', width: 130, align: 'right', render: (_, p) => money(p.insurance + p.tax, 'minus') },
    { key: 'net', title: 'Thực nhận', width: 140, align: 'right', mobile: 'aside', render: (_, p) => <span className="tabular font-semibold text-ink">{formatCurrency(p.net)}</span> },
  ];

  return (
    <>
      <PageHeader
        title="Lương nhân viên"
        subtitle={`Lương cứng × công thực tế / ${STANDARD_DAYS} (từ Chấm công & Xếp ca, nghỉ phép có lương được tính công) + phụ cấp + hoa hồng Sale − đi muộn, BHXH ${Math.round(INSURANCE_RATE * 1000) / 10}%, thuế TNCN.`}
        breadcrumb={[{ title: 'Tài chính' }, { title: 'Lương nhân viên' }]}
      />

      <PayrollBar kind="staff" month={month} onMonth={setMonth} totals={totals} pendingWarning={current ? 'Tháng chưa kết thúc — công các ngày còn lại sẽ không được tính' : undefined} />

      <div className="mb-4 grid grid-cols-2 gap-3 lg:grid-cols-4">
        <StatCard label="Tổng thực nhận" value={formatCurrency(rows.reduce((n, p) => n + p.net, 0))} icon={HandCoins} tone="primary" hint={`${rows.length} nhân viên${current ? ' · tạm tính đến hôm nay' : ''}`} />
        <StatCard label="Hoa hồng Sale" value={formatCurrency(rows.reduce((n, p) => n + p.commission, 0))} icon={BadgePercent} tone="success" />
        <StatCard label="BHXH + thuế khấu trừ" value={formatCurrency(rows.reduce((n, p) => n + p.insurance + p.tax, 0))} icon={ShieldCheck} />
        <StatCard label="Ngày vắng không phép" value={rows.reduce((n, p) => n + p.absent, 0)} icon={CalendarX2} tone="warning" hint={`${rows.reduce((n, p) => n + p.late, 0)} lượt đi muộn`} />
      </div>

      <DataTable<StaffPay>
        storageKey="fin-staff-pay"
        columns={columns}
        data={rows}
        rowKey={(p) => p.employee.id}
        unitLabel="nhân viên"
        onRowClick={(p) => setOpenId(p.employee.id)}
        rowActions={(p) => (!locked ? <Button icon={Plus} iconOnly size="sm" variant="ghost" tone="neutral" tooltip="Thưởng / phạt" aria-label="Thưởng / phạt" onClick={() => setAdjFor(p)} /> : null)}
        actionsWidth={60}
        empty={{ icon: Users, title: 'Không có nhân viên' }}
      />

      <Drawer open={Boolean(open)} onClose={() => setOpenId(null)} width={isMobile ? '100%' : 520} destroyOnHidden title={open && `Phiếu lương ${month.slice(5)}/${month.slice(0, 4)} · ${open.employee.fullName}`}>
        {open && (
          <div className="flex flex-col gap-4 text-[13px]">
            <div className="grid grid-cols-3 gap-2 rounded-xl border border-line p-3 text-center">
              {(
                [
                  ['Đi làm', open.worked],
                  ['Phép có lương', open.paidLeave],
                  ['Nghỉ không lương', open.unpaidLeave],
                  ['Vắng', open.absent],
                  ['Đi muộn', open.late],
                  ['Quên chấm ra', open.noOut],
                ] as [string, number][]
              ).map(([l, v]) => (
                <div key={l}>
                  <p className={cn('tabular text-lg font-semibold', (l === 'Vắng' || l === 'Đi muộn') && v > 0 && 'text-danger-600')}>{v}</p>
                  <p className="text-xs text-ink-muted">{l}</p>
                </div>
              ))}
            </div>

            <table className="w-full">
              <tbody className="[&_td]:py-1.5 [&_tr]:border-b [&_tr]:border-line">
                <Line label={`Lương cơ bản ${formatCurrency(open.base)} × ${open.payDays}/${STANDARD_DAYS} công`} value={open.basePay} />
                <Line label="Phụ cấp" value={open.allowance} />
                <Line label="Hoa hồng Sale" value={open.commission} plus />
                {open.adjusts.map((a) => (
                  <Line
                    key={a.id}
                    label={
                      <span className="flex items-center gap-1">
                        {a.reason}
                        {!locked && <Button icon={Trash2} iconOnly size="sm" variant="ghost" tone="danger" aria-label="Xóa" onClick={() => removeAdjust(a.id)} />}
                      </span>
                    }
                    value={a.amount}
                    plus={a.amount > 0}
                  />
                ))}
                <Line label="Tổng thu nhập" value={open.gross} bold />
                <Line label={`Đi muộn ${open.late} × ${formatCurrency(LATE_FINE)} · quên chấm ${open.noOut} × ${formatCurrency(NO_OUT_FINE)}`} value={-open.fines} />
                <Line label={open.employee.status === 'probation' ? 'BHXH (thử việc — không đóng)' : `BHXH, BHYT, BHTN ${Math.round(INSURANCE_RATE * 1000) / 10}%`} value={-open.insurance} />
                <Line label={`Thuế TNCN (giảm trừ bản thân ${formatCurrency(PERSONAL_DEDUCTION)})`} value={-open.tax} />
                <Line label="Thực nhận" value={open.net} bold big />
              </tbody>
            </table>
            {!locked && (
              <Button icon={Plus} variant="soft" className="self-start" onClick={() => setAdjFor(open)}>
                Thêm thưởng / phạt
              </Button>
            )}
          </div>
        )}
      </Drawer>

      <AdjustModal open={Boolean(adjFor)} onClose={() => setAdjFor(null)} month={month} employee={adjFor?.employee} />
    </>
  );
}

function Line({ label, value, bold, big, plus }: { label: React.ReactNode; value: number; bold?: boolean; big?: boolean; plus?: boolean }) {
  return (
    <tr>
      <td className={cn('pr-3 text-ink-secondary', bold && 'font-semibold text-ink')}>{label}</td>
      <td className={cn('tabular whitespace-nowrap text-right', bold && 'font-semibold', big && 'text-lg text-primary-700', value < 0 && 'text-danger-600', plus && value > 0 && 'text-success-700')}>
        {value < 0 ? '−' : plus && value > 0 ? '+' : ''}
        {formatCurrency(Math.abs(value))}
      </td>
    </tr>
  );
}
