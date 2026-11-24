import { useMemo, useState } from 'react';
import { Drawer, Form, Progress } from 'antd';
import dayjs from 'dayjs';
import { BadgePercent, Crown, Plus, Settings2, Target, Trash2, TrendingUp, Trophy } from 'lucide-react';
import { Button, DataTable, ModalForm, NumberInput, PageHeader, Select, StatCard, StatusTag, UserCell, useNotify, type DataColumn } from '@/components/common';
import { useFinanceStore } from '@/features/finance/financeStore';
import { reassignInvoiceSale, saveCommissionTiers, saveSaleTarget, useServerCommissions } from '@/features/finance/payrollApi';
import { monthOptions, saleResults, tierOf, useInvoiceRows, type SaleResult } from '@/features/finance/financeUtils';
import type { CommissionTier } from '@/features/finance/types';
import { useHrStore } from '@/features/hr/hrStore';
import { useAuth } from '@/hooks/useAuth';
import { useBreakpoint } from '@/hooks/useBreakpoint';
import { cn } from '@/lib/cn';
import { fmtDate } from '@/lib/date';
import { formatCurrency } from '@/lib/format';
import { getBranches } from '@/features/system/useBranches';
import { USE_API } from '@/lib/api/config';
import { reportError } from '@/lib/api/report';
import { ALL_BRANCHES } from '@/types/auth';

const short = (v: number) => (v >= 1_000_000 ? `${(v / 1_000_000).toLocaleString('vi-VN', { maximumFractionDigits: 1 })} tr` : formatCurrency(v));

/**
 * Hoa hồng & KPI Sale: doanh số theo Sale / tháng, bậc hoa hồng, chỉ tiêu, xếp hạng.
 * Chế độ API: server tính thực thu / bậc / hoa hồng theo Sale ghi nhận trên hóa đơn (payrollApi); chế độ mock: tự tính.
 */
export default function CommissionsPage() {
  const { branchId, can } = useAuth();
  const { isMobile } = useBreakpoint();
  const notify = useNotify();
  const employees = useHrStore((s) => s.employees);
  const { tiers, targets, setTiers, setTarget, assignSale } = useFinanceStore();
  const invoiceRows = useInvoiceRows();
  const editable = can('finance.commissions');

  const [month, setMonth] = useState(dayjs().format('YYYY-MM'));
  const [openId, setOpenId] = useState<string | null>(null);
  const [tierOpen, setTierOpen] = useState(false);
  const [targetFor, setTargetFor] = useState<SaleResult | null>(null);

  const server = useServerCommissions(month, branchId, invoiceRows);
  const local = useMemo(() => (USE_API ? [] : saleResults(month, employees, invoiceRows, tiers, targets)), [month, employees, invoiceRows, tiers, targets]);
  const all = server ?? local;
  const rows = all.filter((r) => branchId === ALL_BRANCHES || r.employee.branchId === branchId);
  const open = rows.find((r) => r.employee.id === openId);
  const sum = (f: (r: SaleResult) => number) => rows.reduce((n, r) => n + f(r), 0);
  const totalTarget = sum((r) => r.target);
  const salesOf = (b: string) => employees.filter((e) => e.department === 'sales' && e.status !== 'quit' && e.branchId === b);
  const top = rows.filter((r) => r.collected > 0).slice(0, 3);
  const maxBar = Math.max(1, ...rows.map((r) => Math.max(r.collected, r.target)));

  const columns: DataColumn<SaleResult>[] = [
    {
      key: 'rank',
      title: '#',
      width: 50,
      align: 'center',
      render: (_, r) => {
        const i = rows.indexOf(r);
        return i < 3 && r.collected > 0 ? <Trophy size={16} className={['text-warning-600', 'text-ink-muted', 'text-warning-700'][i]} aria-label={`Hạng ${i + 1}`} /> : <span className="tabular text-ink-muted">{i + 1}</span>;
      },
    },
    { key: 'name', title: 'Sale', width: 200, mobile: 'title', render: (_, r) => <UserCell size="sm" name={r.employee.fullName} subtitle={getBranches(false).find((b) => b.id === r.employee.branchId)?.name} /> },
    { key: 'deals', title: 'Ghi danh mới', width: 110, align: 'center', render: (_, r) => <span className="tabular">{r.deals.length}</span> },
    { key: 'booked', title: 'Doanh số ký', width: 130, align: 'right', render: (_, r) => <span className="tabular text-[13px]">{formatCurrency(r.booked)}</span> },
    { key: 'collected', title: 'Thực thu', width: 140, align: 'right', render: (_, r) => <span className="tabular font-semibold">{formatCurrency(r.collected)}</span> },
    {
      key: 'kpi',
      title: 'KPI',
      width: 190,
      render: (_, r) =>
        r.target ? (
          <div className="leading-4">
            <Progress percent={Math.round(r.kpi * 100)} size="small" status={r.kpi >= 1 ? 'success' : 'normal'} format={(p) => `${p}%`} />
            <p className="text-[11px] text-ink-muted">chỉ tiêu {short(r.target)}</p>
          </div>
        ) : (
          <span className="text-xs text-ink-muted">Chưa đặt chỉ tiêu</span>
        ),
    },
    { key: 'tier', title: 'Bậc', width: 80, align: 'center', render: (_, r) => <span className="tabular rounded bg-primary-50 px-1.5 py-0.5 text-xs font-semibold text-primary-700">{r.tier?.percent ?? 0}%</span> },
    { key: 'commission', title: 'Hoa hồng', width: 140, align: 'right', mobile: 'aside', render: (_, r) => <span className="tabular font-semibold text-success-700">{formatCurrency(r.commission)}</span> },
  ];

  return (
    <>
      <PageHeader
        title="Hoa hồng & KPI Sale"
        subtitle="Hoa hồng tính trên THỰC THU trong tháng của các hóa đơn Sale phụ trách (trừ phần hoàn tiền): đạt mốc nào thì toàn bộ thực thu hưởng % của mốc đó. Tự đưa vào Lương nhân viên."
        breadcrumb={[{ title: 'Tài chính' }, { title: 'Hoa hồng & KPI Sale' }]}
        actions={
          editable ? (
            <Button icon={Settings2} variant="outline" tone="neutral" onClick={() => setTierOpen(true)} collapseOnMobile>
              Bậc hoa hồng
            </Button>
          ) : undefined
        }
      />

      <div className="mb-4 flex flex-wrap items-center gap-2">
        <Select value={month} onChange={setMonth} options={monthOptions()} showSearch={false} allowClear={false} className="!w-44" />
        <span className="flex flex-wrap gap-1 text-xs text-ink-secondary">
          {tiers.map((t, i) => (
            <span key={i} className="rounded-full bg-disabled px-2 py-0.5">
              ≥ {short(t.from)}: {t.percent}%
            </span>
          ))}
        </span>
      </div>

      <div className="mb-4 grid grid-cols-2 gap-3 lg:grid-cols-4">
        <StatCard label="Thực thu của Sale" value={formatCurrency(sum((r) => r.collected))} icon={TrendingUp} tone="primary" hint={`${sum((r) => r.deals.length)} ghi danh mới`} />
        <StatCard label="Hoàn thành chỉ tiêu" value={totalTarget ? `${Math.round((sum((r) => r.collected) / totalTarget) * 100)}%` : '—'} icon={Target} tone="info" hint={`Tổng chỉ tiêu ${short(totalTarget)}`} />
        <StatCard label="Tổng hoa hồng" value={formatCurrency(sum((r) => r.commission))} icon={BadgePercent} tone="success" />
        <StatCard label="Đạt KPI" value={`${rows.filter((r) => r.target && r.kpi >= 1).length}/${rows.filter((r) => r.target).length}`} icon={Crown} tone="warning" />
      </div>

      <div className="mb-4 grid gap-4 lg:grid-cols-[1fr_320px]">
        {/* Thực thu so với chỉ tiêu */}
        <section className="rounded-xl border border-line bg-surface p-4">
          <p className="mb-3 text-sm font-semibold text-ink">Thực thu so với chỉ tiêu</p>
          <div className="flex flex-col gap-2.5">
            {rows.map((r) => (
              <div key={r.employee.id} className="grid grid-cols-[110px_1fr_70px] items-center gap-2 text-[13px]">
                <span className="truncate text-ink-secondary">{r.employee.fullName.split(' ').slice(-2).join(' ')}</span>
                <div className="relative h-5 rounded bg-canvas">
                  <div className={cn('absolute inset-y-0 left-0 rounded', r.target && r.kpi >= 1 ? 'bg-success-600' : 'bg-primary-500')} style={{ width: `${(r.collected / maxBar) * 100}%` }} />
                  {r.target > 0 && <div className="absolute inset-y-[-3px] w-0.5 bg-ink" style={{ left: `${(r.target / maxBar) * 100}%` }} title={`Chỉ tiêu ${formatCurrency(r.target)}`} />}
                </div>
                <span className="tabular text-right text-xs">{short(r.collected)}</span>
              </div>
            ))}
          </div>
          <p className="mt-3 flex items-center gap-3 text-[11px] text-ink-muted">
            <span className="inline-flex items-center gap-1"><span className="inline-block h-2 w-3 rounded-sm bg-primary-500" /> Thực thu</span>
            <span className="inline-flex items-center gap-1"><span className="inline-block h-3 w-0.5 bg-ink" /> Chỉ tiêu</span>
            <span className="inline-flex items-center gap-1"><span className="inline-block h-2 w-3 rounded-sm bg-success-600" /> Đạt KPI</span>
          </p>
        </section>

        {/* Bảng vàng */}
        <section className="rounded-xl border border-line bg-surface p-4">
          <p className="mb-3 flex items-center gap-1.5 text-sm font-semibold text-ink">
            <Trophy size={16} className="text-warning-600" aria-hidden /> Bảng vàng tháng {month.slice(5)}
          </p>
          <ol className="flex flex-col gap-2">
            {top.map((r, i) => (
              <li key={r.employee.id} className={cn('flex items-center gap-3 rounded-lg p-2', i === 0 ? 'bg-warning-50' : 'bg-canvas')}>
                <span className={cn('grid size-7 place-items-center rounded-full text-sm font-bold text-white', ['bg-warning-600', 'bg-ink-muted', 'bg-warning-700'][i])}>{i + 1}</span>
                <div className="min-w-0 flex-1">
                  <p className="truncate font-medium text-ink">{r.employee.fullName}</p>
                  <p className="text-xs text-ink-muted">
                    {short(r.collected)} · {r.deals.length} ghi danh
                  </p>
                </div>
                <span className="tabular text-sm font-semibold text-success-700">{short(r.commission)}</span>
              </li>
            ))}
            {!top.length && <li className="text-sm text-ink-muted">Chưa có doanh thu trong tháng</li>}
          </ol>
        </section>
      </div>

      <DataTable<SaleResult>
        storageKey="fin-commissions"
        columns={columns}
        data={rows}
        rowKey={(r) => r.employee.id}
        unitLabel="Sale"
        loading={USE_API && !server}
        onRowClick={(r) => setOpenId(r.employee.id)}
        rowActions={(r) => (editable ? <Button icon={Target} iconOnly size="sm" variant="ghost" tone="neutral" tooltip="Đặt chỉ tiêu" aria-label="Đặt chỉ tiêu" onClick={() => setTargetFor(r)} /> : null)}
        actionsWidth={60}
        empty={{ icon: TrendingUp, title: 'Không có Sale' }}
      />

      {/* Chi tiết Sale */}
      <Drawer open={Boolean(open)} onClose={() => setOpenId(null)} width={isMobile ? '100%' : 600} destroyOnHidden title={open && `${open.employee.fullName} · tháng ${month.slice(5)}/${month.slice(0, 4)}`}>
        {open && (
          <div className="flex flex-col gap-4 text-[13px]">
            <div className="grid grid-cols-2 gap-2 rounded-xl border border-line p-3 sm:grid-cols-4">
              {(
                [
                  ['Thực thu', formatCurrency(open.collected)],
                  ['Chỉ tiêu', open.target ? formatCurrency(open.target) : '—'],
                  ['Bậc', `${open.tier?.percent ?? 0}%`],
                  ['Hoa hồng', formatCurrency(open.commission)],
                ] as [string, string][]
              ).map(([l, v]) => (
                <div key={l}>
                  <p className="text-xs text-ink-muted">{l}</p>
                  <p className="tabular font-semibold">{v}</p>
                </div>
              ))}
            </div>
            {(() => {
              const next = [...tiers].sort((a, b) => a.from - b.from).find((t) => t.from > open.collected);
              return next ? (
                <p className="rounded-lg bg-primary-50 px-3 py-2 text-primary-700">
                  Thu thêm {formatCurrency(next.from - open.collected)} để lên bậc {next.percent}% (hoa hồng tăng thành {formatCurrency(Math.round((next.from * next.percent) / 100))}+).
                </p>
              ) : null;
            })()}
            <div>
              <p className="mb-2 font-semibold text-ink">Ghi danh mới trong tháng ({open.deals.length})</p>
              <div className="flex flex-col gap-2">
                {open.deals.map((d) => (
                  <div key={d.id} className="flex flex-wrap items-center justify-between gap-2 rounded-lg border border-line p-2.5">
                    <div className="leading-5">
                      <p className="font-medium">
                        {d.student?.fullName} · {d.cls?.code}
                      </p>
                      <p className="text-xs text-ink-muted">
                        <span className="font-mono">{d.invoice.code}</span> · {fmtDate(d.invoice.issuedAt)} · {formatCurrency(d.sum.total)} · đã thu {formatCurrency(d.sum.paid)}
                      </p>
                    </div>
                    <div className="flex items-center gap-2">
                      <StatusTag group="invoice" status={d.sum.status} size="sm" />
                      {editable && (
                        <Select
                          size="small"
                          value={d.saleId}
                          onChange={(v) => {
                            if (!v) return;
                            if (!USE_API) return void (assignSale(d.id, v), notify.toast.success('Đã ghi nhận doanh số cho Sale khác'));
                            reassignInvoiceSale(d.id, v).then(() => notify.toast.success('Đã ghi nhận doanh số cho Sale khác'), reportError);
                          }}
                          options={salesOf(d.branchId).map((e) => ({ value: e.id, label: e.fullName }))}
                          showSearch={false}
                          allowClear={false}
                          className="!w-40"
                          aria-label="Sale ghi nhận"
                        />
                      )}
                    </div>
                  </div>
                ))}
                {!open.deals.length && <p className="text-ink-muted">Chưa có ghi danh mới. Hoa hồng vẫn tính trên các đợt đóng tiếp của hóa đơn cũ.</p>}
              </div>
            </div>
          </div>
        )}
      </Drawer>

      {/* Chỉ tiêu */}
      <ModalForm<{ target: number }>
        key={targetFor?.employee.id ?? 'tg'}
        open={Boolean(targetFor)}
        onClose={() => setTargetFor(null)}
        title={`Chỉ tiêu tháng ${month.slice(5)}/${month.slice(0, 4)} — ${targetFor?.employee.fullName ?? ''}`}
        submitAction="save"
        successMessage="Đã đặt chỉ tiêu"
        initialValues={{ target: targetFor?.target || undefined }}
        onSubmit={async (v) => (USE_API ? await saveSaleTarget(targetFor!.employee.id, month, v.target) : setTarget(targetFor!.employee.id, month, v.target))}
      >
        <Form.Item name="target" label="Chỉ tiêu thực thu" rules={[{ required: true, message: 'Nhập chỉ tiêu' }]}>
          <NumberInput currency min={0} step={5_000_000} />
        </Form.Item>
      </ModalForm>

      {/* Bậc hoa hồng */}
      <ModalForm<{ tiers: CommissionTier[] }>
        open={tierOpen}
        onClose={() => setTierOpen(false)}
        title="Bậc hoa hồng"
        description="Thực thu trong tháng đạt mốc nào thì toàn bộ thực thu hưởng % của mốc đó."
        submitAction="save"
        successMessage="Đã lưu bậc hoa hồng"
        initialValues={{ tiers }}
        onSubmit={async (v) => {
          const list = v.tiers.filter((t) => t.from !== undefined && t.percent !== undefined);
          if (!list.some((t) => t.from === 0)) throw new Error('Cần có bậc bắt đầu từ 0đ');
          if (new Set(list.map((t) => t.from)).size !== list.length) throw new Error('Các mốc không được trùng nhau');
          if (USE_API) await saveCommissionTiers(list);
          else setTiers(list);
        }}
      >
        <Form.List name="tiers">
          {(fields, { add, remove }) => (
            <div className="flex flex-col gap-2">
              {fields.map((f) => (
                <div key={f.key} className="grid grid-cols-[1fr_110px_36px] items-start gap-2">
                  <Form.Item name={[f.name, 'from']} rules={[{ required: true, message: 'Mốc' }]} className="!mb-0">
                    <NumberInput currency min={0} step={10_000_000} placeholder="Thực thu từ" />
                  </Form.Item>
                  <Form.Item name={[f.name, 'percent']} rules={[{ required: true, message: '%' }]} className="!mb-0">
                    <NumberInput min={0} max={30} step={0.5} placeholder="%" />
                  </Form.Item>
                  <Button icon={Trash2} iconOnly size="sm" variant="ghost" tone="danger" aria-label="Xóa bậc" disabled={fields.length <= 1} onClick={() => remove(f.name)} />
                </div>
              ))}
              <Button icon={Plus} size="sm" variant="soft" className="self-start" onClick={() => add({ from: undefined, percent: undefined })}>
                Thêm bậc
              </Button>
            </div>
          )}
        </Form.List>
        <p className="mt-3 text-xs text-ink-muted">VD thực thu 70 triệu: {tierOf(tiers, 70_000_000)?.percent ?? 0}% → {formatCurrency(Math.round((70_000_000 * (tierOf(tiers, 70_000_000)?.percent ?? 0)) / 100))}</p>
      </ModalForm>
    </>
  );
}
