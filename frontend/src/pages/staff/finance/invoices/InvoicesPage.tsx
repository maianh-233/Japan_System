import { useState } from 'react';
import dayjs from 'dayjs';
import { CalendarClock, CircleDollarSign, HandCoins, Receipt, TriangleAlert } from 'lucide-react';
import { DataTable, PageHeader, Select, StatCard, StatusTag, UserCell, type DataColumn } from '@/components/common';
import { InvoiceDrawer } from '@/features/finance/InvoiceDrawer';
import { daysLate, unaccent, useInvoiceRows, type InvoiceRow } from '@/features/finance/financeUtils';
import { useHrStore } from '@/features/hr/hrStore';
import { nameOf } from '@/features/hr/hrUtils';
import type { TuitionStatus } from '@/features/student/types';
import { useAuth } from '@/hooks/useAuth';
import { cn } from '@/lib/cn';
import { fmtDate } from '@/lib/date';
import { formatCurrency } from '@/lib/format';
import { ALL_BRANCHES } from '@/types/auth';

const STATUS_OPTIONS: { value: TuitionStatus; label: string }[] = [
  { value: 'unpaid', label: 'Chưa thu' },
  { value: 'partial', label: 'Thu một phần' },
  { value: 'overdue', label: 'Quá hạn' },
  { value: 'paid', label: 'Đã thu' },
  { value: 'refunded', label: 'Hoàn tiền' },
  { value: 'cancelled', label: 'Đã hủy' },
];

/** Hóa đơn & thu tiền. Hóa đơn sinh tự động khi ghi danh vào lớp. DỮ LIỆU GIẢ. */
export default function InvoicesPage() {
  const { branchId } = useAuth();
  const employees = useHrStore((s) => s.employees);
  const rows = useInvoiceRows().filter((r) => branchId === ALL_BRANCHES || r.branchId === branchId);
  const [status, setStatus] = useState<TuitionStatus[]>([]);
  const [period, setPeriod] = useState<'all' | 'month' | 'due7'>('all');
  const [openId, setOpenId] = useState<string | null>(null);
  const [q, setQ] = useState('');

  const now = dayjs();
  const monthPays = rows.flatMap((r) => r.invoice.payments.filter((p) => dayjs(p.paidAt).isSame(now, 'month')));
  const stats = {
    collected: monthPays.reduce((n, p) => n + p.amount, 0),
    count: monthPays.length,
    remaining: rows.reduce((n, r) => n + r.sum.remaining, 0),
    overdue: rows.filter((r) => r.sum.status === 'overdue'),
    due7: rows.filter((r) => r.sum.remaining > 0 && r.sum.status !== 'overdue' && dayjs(r.invoice.dueDate).diff(now.startOf('day'), 'day') <= 7),
  };

  const data = rows
    .filter((r) => !status.length || status.includes(r.sum.status))
    .filter((r) => !q || [r.invoice.code, r.student?.fullName, r.student?.code, r.student?.phone, r.cls?.code].some((x) => x && unaccent(x).includes(unaccent(q.trim()))))
    .filter((r) => (period === 'month' ? dayjs(r.invoice.issuedAt).isSame(now, 'month') : period === 'due7' ? stats.due7.includes(r) : true))
    .sort((a, b) => b.invoice.issuedAt.localeCompare(a.invoice.issuedAt));

  const columns: DataColumn<InvoiceRow>[] = [
    {
      key: 'code',
      title: 'Hóa đơn',
      width: 120,
      render: (_, r) => (
        <div className="leading-5">
          <p className="font-mono font-medium text-ink">{r.invoice.code}</p>
          <p className="text-xs text-ink-muted">{fmtDate(r.invoice.issuedAt)}</p>
        </div>
      ),
    },
    { key: 'student', title: 'Học viên', width: 210, mobile: 'title', render: (_, r) => <UserCell size="sm" name={r.student?.fullName ?? '—'} subtitle={r.student?.code} /> },
    { key: 'class', title: 'Lớp', width: 160, mobile: 'subtitle', render: (_, r) => <span className="text-[13px]">{r.cls?.code}</span> },
    { key: 'total', title: 'Phải thu', width: 130, align: 'right', render: (_, r) => <span className="tabular">{formatCurrency(r.sum.total)}</span> },
    { key: 'paid', title: 'Đã thu', width: 130, align: 'right', render: (_, r) => <span className="tabular text-success-700">{formatCurrency(r.sum.paid)}</span> },
    { key: 'remaining', title: 'Còn nợ', width: 130, align: 'right', render: (_, r) => <span className={cn('tabular font-semibold', r.sum.remaining > 0 ? 'text-danger-600' : 'text-ink-muted')}>{formatCurrency(r.sum.remaining)}</span> },
    {
      key: 'due',
      title: 'Hạn đóng',
      width: 130,
      render: (_, r) => (
        <div className="leading-5 text-[13px]">
          <p>{fmtDate(r.invoice.dueDate)}</p>
          {r.sum.status === 'overdue' && <p className="text-xs font-medium text-danger-600">trễ {daysLate(r)} ngày</p>}
        </div>
      ),
    },
    { key: 'sale', title: 'Sale', width: 150, render: (_, r) => <span className="text-[13px] text-ink-secondary">{r.saleId ? nameOf(employees, r.saleId) : '—'}</span> },
    { key: 'status', title: 'Trạng thái', width: 130, mobile: 'aside', render: (_, r) => <StatusTag group="invoice" status={r.sum.status} size="sm" /> },
  ];

  return (
    <>
      <PageHeader
        title="Hóa đơn & thu tiền"
        subtitle="Hóa đơn sinh tự động khi ghi danh học viên vào lớp (Học viên → Ghi danh). Thu nhiều lần, hoàn tiền, nhắc học phí, VietQR riêng từng hóa đơn."
        breadcrumb={[{ title: 'Tài chính' }, { title: 'Hóa đơn & thu tiền' }]}
      />

      <div className="mb-4 grid grid-cols-2 gap-3 lg:grid-cols-4">
        <StatCard label="Đã thu tháng này" value={formatCurrency(stats.collected)} icon={HandCoins} tone="success" hint={`${stats.count} lần thu`} />
        <StatCard label="Tổng còn phải thu" value={formatCurrency(stats.remaining)} icon={CircleDollarSign} tone="info" onClick={() => (setStatus(['unpaid', 'partial', 'overdue']), setPeriod('all'))} />
        <StatCard label="Đến hạn trong 7 ngày" value={stats.due7.length} icon={CalendarClock} tone="warning" onClick={() => (setStatus([]), setPeriod('due7'))} hint={formatCurrency(stats.due7.reduce((n, r) => n + r.sum.remaining, 0))} />
        <StatCard label="Quá hạn" value={stats.overdue.length} icon={TriangleAlert} tone="danger" onClick={() => (setStatus(['overdue']), setPeriod('all'))} hint={formatCurrency(stats.overdue.reduce((n, r) => n + r.sum.remaining, 0))} />
      </div>

      <DataTable<InvoiceRow>
        storageKey="fin-invoices"
        columns={columns}
        data={data}
        rowKey="id"
        unitLabel="hóa đơn"
        onRowClick={(r) => setOpenId(r.id)}
        search={{
          placeholder: 'Tìm mã HĐ, học viên, SĐT, lớp...',
          onSearch: setQ,
          activeFilterCount: (status.length ? 1 : 0) + (period !== 'all' ? 1 : 0),
          filters: (
            <>
              <Select mode="multiple" placeholder="Trạng thái" value={status} onChange={setStatus} options={STATUS_OPTIONS} className="lg:!w-56" maxTagCount="responsive" />
              <Select
                value={period}
                onChange={setPeriod}
                options={[
                  { value: 'all', label: 'Mọi thời gian' },
                  { value: 'month', label: 'Phát hành tháng này' },
                  { value: 'due7', label: 'Đến hạn 7 ngày tới' },
                ]}
                showSearch={false}
                allowClear={false}
                className="lg:!w-48"
              />
            </>
          ),
          onReset: () => (setStatus([]), setPeriod('all')),
        }}
        empty={{ icon: Receipt, title: 'Không có hóa đơn' }}
      />

      <InvoiceDrawer row={rows.find((r) => r.id === openId)} onClose={() => setOpenId(null)} />
    </>
  );
}
