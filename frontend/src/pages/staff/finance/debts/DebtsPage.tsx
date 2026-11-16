import { useMemo, useState, type Key } from 'react';
import { Drawer } from 'antd';
import dayjs from 'dayjs';
import { BellRing, CircleDollarSign, Phone, TriangleAlert } from 'lucide-react';
import { Button, DataTable, PageHeader, StatCard, StatusTag, UserCell, type DataColumn } from '@/components/common';
import { useCrmStore } from '@/features/crm/crmStore';
import { AGING, REMINDER_CHANNEL, type AgingKey } from '@/features/finance/constants';
import { useFinanceStore } from '@/features/finance/financeStore';
import { agingOf, daysLate, useInvoiceRows, type InvoiceRow } from '@/features/finance/financeUtils';
import { InvoiceDrawer, RemindModal } from '@/features/finance/InvoiceDrawer';
import type { Reminder } from '@/features/finance/types';
import { useAuth } from '@/hooks/useAuth';
import { useBreakpoint } from '@/hooks/useBreakpoint';
import { cn } from '@/lib/cn';
import { fmtDate, fmtDateTime, fromNow } from '@/lib/date';
import { formatCurrency } from '@/lib/format';
import { getBranches } from '@/features/system/useBranches';
import { ALL_BRANCHES } from '@/types/auth';

type GroupBy = 'student' | 'class' | 'branch';

interface DebtGroup {
  id: string;
  label: string;
  sub?: string;
  rows: InvoiceRow[];
  remaining: number;
  buckets: Record<AgingKey, number>;
  maxLate: number;
  lastReminder?: Reminder;
  reminderCount: number;
  /** Người nhận hóa đơn (phụ huynh) */
  payer?: { name: string; phone: string; relation: string };
}

const BUCKET_TONE: Record<AgingKey, string> = { not_due: 'text-primary-700', d30: 'text-warning-700', d60: 'text-danger-600', d60p: 'font-semibold text-danger-700' };

/** Công nợ học phí theo học viên / lớp / chi nhánh + tuổi nợ + nhắc nợ. DỮ LIỆU GIẢ. */
export default function DebtsPage() {
  const { branchId } = useAuth();
  const { isMobile } = useBreakpoint();
  const reminders = useFinanceStore((s) => s.reminders);
  const leads = useCrmStore((s) => s.leads);
  const links = useCrmStore((s) => s.links);
  const rows = useInvoiceRows().filter((r) => r.sum.remaining > 0 && (branchId === ALL_BRANCHES || r.branchId === branchId));

  const [groupBy, setGroupBy] = useState<GroupBy>('student');
  const [bucket, setBucket] = useState<AgingKey | 'todo' | null>(null);
  const [selected, setSelected] = useState<Key[]>([]);
  const [remindIds, setRemindIds] = useState<string[] | null>(null);
  const [groupId, setGroupId] = useState<string | null>(null);
  const [invoiceId, setInvoiceId] = useState<string | null>(null);

  const total = rows.reduce((n, r) => n + r.sum.remaining, 0);
  const bucketSum = (k: AgingKey) => rows.filter((r) => agingOf(r) === k).reduce((n, r) => n + r.sum.remaining, 0);
  const lastOf = (ids: string[]) => reminders.filter((x) => ids.includes(x.invoiceId)).sort((a, b) => b.at.localeCompare(a.at));

  const groups = useMemo<DebtGroup[]>(() => {
    const map = new Map<string, DebtGroup>();
    rows.forEach((r) => {
      const key = groupBy === 'student' ? (r.student?.id ?? r.id) : groupBy === 'class' ? (r.cls?.id ?? '-') : r.branchId;
      let g = map.get(key);
      if (!g) {
        let payer: DebtGroup['payer'];
        if (groupBy === 'student' && r.student) {
          const l = links.find((x) => x.studentId === r.student!.id && x.billing) ?? links.find((x) => x.studentId === r.student!.id && x.primary);
          const lead = l && leads.find((x) => x.id === l.leadId);
          if (lead) payer = { name: lead.fullName, phone: lead.phone, relation: l!.relationship === 'child' ? 'Phụ huynh' : 'Người thân' };
        }
        g = {
          id: key,
          label: groupBy === 'student' ? (r.student?.fullName ?? '—') : groupBy === 'class' ? (r.cls?.code ?? '—') : (getBranches(false).find((b) => b.id === r.branchId)?.name ?? r.branchId),
          sub: groupBy === 'student' ? `${r.student?.code} · ${r.student?.phone ?? ''}` : groupBy === 'class' ? r.cls?.name : undefined,
          rows: [],
          remaining: 0,
          buckets: { not_due: 0, d30: 0, d60: 0, d60p: 0 },
          maxLate: 0,
          reminderCount: 0,
          payer,
        };
        map.set(key, g);
      }
      g.rows.push(r);
      g.remaining += r.sum.remaining;
      g.buckets[agingOf(r)!] += r.sum.remaining;
      g.maxLate = Math.max(g.maxLate, daysLate(r));
    });
    return [...map.values()]
      .map((g) => {
        const rm = lastOf(g.rows.map((r) => r.id));
        return { ...g, lastReminder: rm[0], reminderCount: rm.length };
      })
      .sort((a, b) => b.maxLate - a.maxLate || b.remaining - a.remaining);
    // eslint-disable-next-line react-hooks/exhaustive-deps
  }, [rows.map((r) => r.id + r.sum.remaining).join(), groupBy, reminders, links, leads]);

  /** Cần nhắc: quá hạn và chưa nhắc trong 3 ngày */
  const needRemind = (g: DebtGroup) => g.maxLate > 0 && (!g.lastReminder || dayjs().diff(dayjs(g.lastReminder.at), 'day') >= 3);
  const data = groups.filter((g) => (bucket === 'todo' ? needRemind(g) : bucket ? g.buckets[bucket] > 0 : true));
  const openGroup = groups.find((g) => g.id === groupId);
  const invoicesOf = (keys: Key[]) => groups.filter((g) => keys.includes(g.id)).flatMap((g) => g.rows.map((r) => r.id));

  const columns: DataColumn<DebtGroup>[] = [
    {
      key: 'label',
      title: groupBy === 'student' ? 'Học viên' : groupBy === 'class' ? 'Lớp' : 'Chi nhánh',
      width: 230,
      mobile: 'title',
      render: (_, g) => (groupBy === 'student' ? <UserCell size="sm" name={g.label} subtitle={g.sub} /> : <div className="leading-5"><p className="font-medium text-ink">{g.label}</p>{g.sub && <p className="text-xs text-ink-muted">{g.sub}</p>}</div>),
    },
    ...(groupBy === 'student'
      ? [
          {
            key: 'payer',
            title: 'Người đóng tiền',
            width: 190,
            render: (_: unknown, g: DebtGroup) =>
              g.payer ? (
                <div className="leading-5 text-[13px]">
                  <p>{g.payer.name} <span className="text-xs text-ink-muted">({g.payer.relation})</span></p>
                  <a href={`tel:${g.payer.phone}`} onClick={(e) => e.stopPropagation()} className="inline-flex items-center gap-1 text-xs text-primary-600"><Phone size={11} />{g.payer.phone}</a>
                </div>
              ) : (
                <span className="text-xs text-ink-muted">Học viên tự đóng</span>
              ),
          } as DataColumn<DebtGroup>,
        ]
      : [{ key: 'count', title: 'Số HV nợ', width: 100, align: 'center', render: (_: unknown, g: DebtGroup) => <span className="tabular">{new Set(g.rows.map((r) => r.student?.id)).size}</span> } as DataColumn<DebtGroup>]),
    { key: 'remaining', title: 'Tổng nợ', width: 140, align: 'right', mobile: 'aside', render: (_, g) => <span className="tabular font-semibold text-danger-600">{formatCurrency(g.remaining)}</span> },
    ...AGING.map<DataColumn<DebtGroup>>((a) => ({
      key: a.key,
      title: a.label.replace('Quá hạn ', ''),
      width: 120,
      align: 'right',
      render: (_, g) => (g.buckets[a.key] ? <span className={cn('tabular text-[13px]', BUCKET_TONE[a.key])}>{formatCurrency(g.buckets[a.key])}</span> : <span className="text-ink-muted">—</span>),
    })),
    {
      key: 'remind',
      title: 'Nhắc gần nhất',
      width: 160,
      render: (_, g) =>
        g.lastReminder ? (
          <div className="leading-5 text-[13px]">
            <p>{REMINDER_CHANNEL[g.lastReminder.channel].label} · {fromNow(g.lastReminder.at)}</p>
            <p className="text-xs text-ink-muted">{g.reminderCount} lần nhắc</p>
          </div>
        ) : (
          <span className="text-xs text-warning-700">Chưa nhắc</span>
        ),
    },
  ];

  return (
    <>
      <PageHeader
        title="Công nợ"
        subtitle="Học phí còn phải thu theo học viên / lớp / chi nhánh, phân theo tuổi nợ. Nhắc nợ qua Zalo / Email / SMS kèm mã VietQR."
        breadcrumb={[{ title: 'Tài chính' }, { title: 'Công nợ' }]}
        actions={
          <Button icon={BellRing} onClick={() => setRemindIds(rows.filter((r) => daysLate(r) > 0).map((r) => r.id))} collapseOnMobile>
            Nhắc tất cả quá hạn
          </Button>
        }
      />

      <div className="mb-4 grid grid-cols-2 gap-3 lg:grid-cols-5">
        <StatCard label="Tổng công nợ" value={formatCurrency(total)} icon={CircleDollarSign} onClick={() => setBucket(null)} hint={`${rows.length} hóa đơn`} />
        {AGING.map((a) => (
          <StatCard key={a.key} label={a.label} value={formatCurrency(bucketSum(a.key))} icon={a.key === 'not_due' ? CircleDollarSign : TriangleAlert} tone={a.tone} onClick={() => setBucket(a.key)} hint={total ? `${Math.round((bucketSum(a.key) / total) * 100)}%` : undefined} />
        ))}
      </div>

      {/* Thanh tuổi nợ */}
      {total > 0 && (
        <div className="mb-4 flex h-2.5 overflow-hidden rounded-full bg-disabled" aria-hidden>
          {AGING.map((a, i) => (
            <div key={a.key} style={{ width: `${(bucketSum(a.key) / total) * 100}%` }} className={['bg-primary-400', 'bg-warning-600', 'bg-danger-600', 'bg-danger-700'][i]} />
          ))}
        </div>
      )}

      <div className="mb-3 flex flex-wrap items-center gap-1.5">
        <span className="mr-1 text-sm text-ink-secondary">Xem theo</span>
        {(
          [
            ['student', 'Học viên'],
            ['class', 'Lớp'],
            ['branch', 'Chi nhánh'],
          ] as [GroupBy, string][]
        ).map(([k, l]) => (
          <button key={k} type="button" onClick={() => (setGroupBy(k), setSelected([]))} className={cn('h-8 rounded-full border px-3 text-sm font-medium', groupBy === k ? 'border-primary-600 bg-primary-600 text-white' : 'border-line bg-surface text-ink-secondary hover:bg-primary-50')}>
            {l}
          </button>
        ))}
        <button type="button" onClick={() => setBucket(bucket === 'todo' ? null : 'todo')} className={cn('ml-auto inline-flex h-8 items-center gap-1.5 rounded-full border px-3 text-sm font-medium', bucket === 'todo' ? 'border-warning-600 bg-warning-600 text-white' : 'border-warning-100 bg-warning-50 text-warning-700')}>
          <BellRing size={14} aria-hidden /> Cần nhắc hôm nay ({groups.filter(needRemind).length})
        </button>
      </div>
      {bucket && bucket !== 'todo' && (
        <p className="mb-2 text-sm text-ink-secondary">
          Đang lọc: {AGING.find((a) => a.key === bucket)?.label}{' '}
          <button type="button" className="text-primary-600 hover:underline" onClick={() => setBucket(null)}>
            Bỏ lọc
          </button>
        </p>
      )}

      <DataTable<DebtGroup>
        storageKey={`fin-debts-${groupBy}`}
        columns={columns}
        data={data}
        rowKey="id"
        unitLabel={groupBy === 'student' ? 'học viên' : groupBy === 'class' ? 'lớp' : 'chi nhánh'}
        searchKeys={['label', 'sub']}
        onRowClick={(g) => setGroupId(g.id)}
        selectedKeys={selected}
        onSelectionChange={(k) => setSelected(k)}
        bulkActions={(keys) => (
          <Button icon={BellRing} size="sm" onClick={() => setRemindIds(invoicesOf(keys))}>
            Nhắc {invoicesOf(keys).length} hóa đơn
          </Button>
        )}
        empty={{ icon: CircleDollarSign, title: 'Không có công nợ', description: 'Tất cả học phí đã được thu đủ.' }}
      />

      {/* Chi tiết nhóm nợ */}
      <Drawer open={Boolean(openGroup)} onClose={() => setGroupId(null)} width={isMobile ? '100%' : 560} destroyOnHidden title={openGroup && `Công nợ · ${openGroup.label}`}>
        {openGroup && (
          <div className="flex flex-col gap-4 text-[13px]">
            <div className="flex items-center justify-between rounded-xl border border-line p-3">
              <div>
                <p className="text-xs text-ink-muted">Tổng còn nợ</p>
                <p className="tabular text-xl font-semibold text-danger-600">{formatCurrency(openGroup.remaining)}</p>
              </div>
              <Button icon={BellRing} onClick={() => setRemindIds(openGroup.rows.map((r) => r.id))}>
                Nhắc nợ
              </Button>
            </div>
            <div>
              <p className="mb-2 font-semibold text-ink">Hóa đơn còn nợ</p>
              <div className="flex flex-col gap-2">
                {openGroup.rows.map((r) => (
                  <button key={r.id} type="button" onClick={() => setInvoiceId(r.id)} className="flex items-center justify-between gap-2 rounded-lg border border-line p-2.5 text-left hover:border-primary-300">
                    <div className="leading-5">
                      <p className="font-medium">
                        <span className="font-mono">{r.invoice.code}</span> · {groupBy === 'student' ? r.cls?.code : r.student?.fullName}
                      </p>
                      <p className="text-xs text-ink-muted">
                        Hạn {fmtDate(r.invoice.dueDate)}
                        {daysLate(r) > 0 && <span className="text-danger-600"> · trễ {daysLate(r)} ngày</span>}
                      </p>
                    </div>
                    <div className="text-right">
                      <p className="tabular font-semibold text-danger-600">{formatCurrency(r.sum.remaining)}</p>
                      <StatusTag group="invoice" status={r.sum.status} size="sm" />
                    </div>
                  </button>
                ))}
              </div>
            </div>
            <div>
              <p className="mb-2 font-semibold text-ink">Lịch sử nhắc</p>
              <ul className="flex flex-col gap-2">
                {lastOf(openGroup.rows.map((r) => r.id)).map((m) => {
                  const C = REMINDER_CHANNEL[m.channel];
                  return (
                    <li key={m.id} className="flex gap-2">
                      <C.icon size={15} className="mt-0.5 shrink-0 text-ink-muted" aria-hidden />
                      <div>
                        <p>
                          {C.label} · {rows.find((r) => r.id === m.invoiceId)?.invoice.code}
                          {m.note && <span className="text-ink-secondary"> — {m.note}</span>}
                        </p>
                        <p className="text-xs text-ink-muted">{fmtDateTime(m.at)} · {m.by}</p>
                      </div>
                    </li>
                  );
                })}
                {!openGroup.reminderCount && <li className="text-ink-muted">Chưa nhắc lần nào</li>}
              </ul>
            </div>
          </div>
        )}
      </Drawer>

      <InvoiceDrawer row={rows.find((r) => r.id === invoiceId)} onClose={() => setInvoiceId(null)} />
      <RemindModal key={remindIds?.join() ?? 'rm'} open={Boolean(remindIds)} invoiceIds={remindIds ?? []} onClose={() => setRemindIds(null)} onDone={() => setSelected([])} />
    </>
  );
}
