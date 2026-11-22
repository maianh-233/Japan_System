import { useState } from 'react';
import { Drawer } from 'antd';
import dayjs from 'dayjs';
import { ArrowDownToLine, CircleSlash, Link2, Link2Off, QrCode, RefreshCw, Sparkles, TriangleAlert, Wallet } from 'lucide-react';
import { Button, DataTable, PageHeader, Select, StatCard, StatusTag, UserCell, useConfirm, useNotify, type DataColumn } from '@/components/common';
import { TXN_STATUS } from '@/features/finance/constants';
import * as financeApi from '@/features/finance/financeApi';
import { USE_API } from '@/lib/api/config';
import { useFinanceStore } from '@/features/finance/financeStore';
import { autoMatchable, bankOfAccount, suggestMatches, transferContent, unaccent, useInvoiceRows } from '@/features/finance/financeUtils';
import { InvoiceDrawer } from '@/features/finance/InvoiceDrawer';
import type { BankTxn } from '@/features/finance/types';
import { useAuth } from '@/hooks/useAuth';
import { useBreakpoint } from '@/hooks/useBreakpoint';
import { cn } from '@/lib/cn';
import { fmtDate, fmtDateTime } from '@/lib/date';
import { formatCurrency } from '@/lib/format';
import { getBranches } from '@/features/system/useBranches';
import { ALL_BRANCHES } from '@/types/auth';

type Tab = 'unmatched' | 'matched' | 'ignored' | 'all';

/** Đối soát giao dịch ngân hàng (VietQR) với hóa đơn. DỮ LIỆU GIẢ. */
export default function ReconciliationPage() {
  const { user, branchId } = useAuth();
  const { isMobile } = useBreakpoint();
  const notify = useNotify();
  const { confirm, confirmDialog } = useConfirm();
  const txns = useFinanceStore((s) => s.txns);
  const { receiveTxn, matchTxn, ignoreTxn, unmatchTxn } = useFinanceStore();
  const allRows = useInvoiceRows();
  const rows = allRows.filter((r) => branchId === ALL_BRANCHES || r.branchId === branchId);

  const [tab, setTab] = useState<Tab>('unmatched');
  const [openId, setOpenId] = useState<string | null>(null);
  const [invoiceId, setInvoiceId] = useState<string | null>(null);
  const [manual, setManual] = useState<string>();

  const scoped = txns.filter((t) => branchId === ALL_BRANCHES || t.branchId === branchId);
  const byTab: Record<Tab, (t: BankTxn) => boolean> = {
    unmatched: (t) => t.status === 'unmatched',
    matched: (t) => t.status === 'auto' || t.status === 'matched',
    ignored: (t) => t.status === 'ignored',
    all: () => true,
  };
  const data = scoped.filter(byTab[tab]);
  const today = dayjs();
  const month = scoped.filter((t) => dayjs(t.at).isSame(today, 'month'));
  const unmatched = scoped.filter(byTab.unmatched);
  const monthMatched = month.filter(byTab.matched);
  const autoRate = monthMatched.length ? Math.round((monthMatched.filter((t) => t.status === 'auto').length / monthMatched.length) * 100) : 0;

  const open = txns.find((t) => t.id === openId);
  const suggestions = open?.status === 'unmatched' ? suggestMatches(open, allRows) : [];

  /** Mô phỏng ngân hàng gửi webhook giao dịch mới */
  const simulate = () => {
    const owing = rows.filter((r) => r.sum.remaining > 0);
    if (!owing.length) return notify.toast.info('Không còn hóa đơn nợ để mô phỏng');
    const r = owing[Math.floor(Math.random() * owing.length)];
    const correct = Math.random() < 0.7;
    const amount = correct ? r.sum.remaining : Math.round(r.sum.remaining / 2 / 1000) * 1000;
    const t = receiveTxn({
      ref: `FT${today.format('YYMMDD')}${Math.floor(Math.random() * 90000 + 10000)}`,
      at: new Date().toISOString(),
      amount,
      content: correct ? transferContent(r) : `CK HOC PHI ${unaccent(r.student?.fullName ?? '')}`,
      payer: unaccent(r.student?.fullName ?? ''),
      account: r.branchId,
      branchId: r.branchId,
    });
    const hit = autoMatchable(t, allRows);
    if (hit) {
      matchTxn(t.id, hit.id, 'Hệ thống', true);
      notify.toast.success(`+${formatCurrency(amount)} — tự khớp ${hit.invoice.code} (${hit.student?.fullName})`);
      setTab('matched');
    } else {
      notify.toast.warning(`+${formatCurrency(amount)} — không có mã hóa đơn, cần khớp tay`);
      setTab('unmatched');
    }
  };

  const reload = () =>
    financeApi
      .loadTransactions()
      .then(() => notify.toast.success('Đã tải lại giao dịch'))
      .catch((e: Error) => notify.error({ title: 'Chưa tải được giao dịch', description: e.message }));

  const rerun = () => {
    // Công nợ giảm dần sau mỗi lần khớp trong cùng lượt → trừ dần ở bản sao cục bộ
    const used = new Map<string, number>();
    let n = 0;
    unmatched.forEach((t) => {
      const live = allRows.map((r) => ({ ...r, sum: { ...r.sum, remaining: r.sum.remaining - (used.get(r.id) ?? 0) } }));
      const hit = autoMatchable(t, live);
      if (!hit) return;
      matchTxn(t.id, hit.id, 'Hệ thống', true);
      used.set(hit.id, (used.get(hit.id) ?? 0) + t.amount);
      n++;
    });
    notify.toast.info(n ? `Đã tự khớp thêm ${n} giao dịch` : 'Không có giao dịch nào khớp được tự động');
  };

  const doMatch = async (t: BankTxn, invId: string) => {
    const r = allRows.find((x) => x.id === invId)!;
    const over = t.amount > r.sum.remaining;
    const ok = await confirm({
      title: `Khớp ${formatCurrency(t.amount)} vào ${r.invoice.code}?`,
      description: `${r.student?.fullName} · còn nợ ${formatCurrency(r.sum.remaining)}.${over ? ' ⚠ Số tiền chuyển lớn hơn số còn nợ — phần dư cần hoàn lại hoặc trừ vào khóa sau.' : t.amount < r.sum.remaining ? ' Hóa đơn sẽ chuyển sang "Thu một phần".' : ''}`,
      confirmText: 'Khớp',
      tone: over ? 'warning' : 'primary',
    });
    if (!ok) return;
    if (USE_API) {
      try {
        await financeApi.matchTxn(t.id, invId);
      } catch (e) {
        return notify.error({ title: 'Chưa khớp được', description: e instanceof Error ? e.message : undefined });
      }
    } else matchTxn(t.id, invId, user?.fullName ?? 'Kế toán');
    setManual(undefined);
    notify.toast.success('Đã khớp — ghi nhận thanh toán VietQR vào hóa đơn');
  };

  const columns: DataColumn<BankTxn>[] = [
    {
      key: 'at',
      title: 'Thời gian',
      width: 140,
      render: (_, t) => (
        <div className="leading-5 text-[13px]">
          <p>{fmtDateTime(t.at)}</p>
          <p className="font-mono text-[11px] text-ink-muted">{t.ref}</p>
        </div>
      ),
    },
    { key: 'amount', title: 'Số tiền', width: 130, align: 'right', mobile: 'aside', render: (_, t) => <span className="tabular font-semibold text-success-700">+{formatCurrency(t.amount)}</span> },
    {
      key: 'content',
      title: 'Nội dung chuyển khoản',
      width: 300,
      mobile: 'title',
      render: (_, t) => (
        <div className="leading-5">
          <p className="font-mono text-[13px] text-ink">{t.content}</p>
          <p className="text-xs text-ink-muted">{t.payer}</p>
        </div>
      ),
    },
    {
      key: 'invoice',
      title: 'Hóa đơn',
      width: 200,
      mobile: 'subtitle',
      render: (_, t) => {
        const r = allRows.find((x) => x.id === t.invoiceId);
        if (r)
          return (
            <button type="button" onClick={(e) => (e.stopPropagation(), setInvoiceId(r.id))} className="text-left leading-5 hover:underline">
              <p className="font-mono text-[13px] text-primary-700">{r.invoice.code}</p>
              <p className="text-xs text-ink-muted">{r.student?.fullName}</p>
            </button>
          );
        if (t.status === 'unmatched') {
          const n = suggestMatches(t, allRows).length;
          return <span className="text-xs text-warning-700">{n ? `${n} gợi ý` : 'Không có gợi ý'}</span>;
        }
        return <span className="text-xs text-ink-muted">{t.note ?? '—'}</span>;
      },
    },
    { key: 'branch', title: 'Tài khoản', width: 140, render: (_, t) => <span className="text-xs text-ink-secondary">{bankOfAccount(t.account)?.bank} · {getBranches(false).find((b) => b.id === t.branchId)?.name}</span> },
    { key: 'status', title: 'Trạng thái', width: 120, render: (_, t) => <StatusTag tone={TXN_STATUS[t.status].tone} icon={TXN_STATUS[t.status].icon} size="sm">{TXN_STATUS[t.status].label}</StatusTag> },
  ];

  return (
    <>
      <PageHeader
        title="Đối soát VietQR"
        subtitle="Ngân hàng đẩy giao dịch về hệ thống → tự khớp theo mã hóa đơn trong nội dung chuyển khoản. Giao dịch ghi sai / thiếu mã cần khớp tay."
        breadcrumb={[{ title: 'Tài chính' }, { title: 'Đối soát VietQR' }]}
        actions={
          <div className="flex gap-2">
            {USE_API ? (
              // Server tự khớp ngay khi ngân hàng gửi giao dịch — chỉ cần tải lại danh sách
              <Button icon={RefreshCw} variant="outline" tone="neutral" onClick={reload} collapseOnMobile>
                Tải lại giao dịch
              </Button>
            ) : (
              <>
                <Button icon={RefreshCw} variant="outline" tone="neutral" onClick={rerun} collapseOnMobile>
                  Chạy tự khớp lại
                </Button>
                <Button icon={ArrowDownToLine} onClick={simulate} collapseOnMobile>
                  Mô phỏng GD mới
                </Button>
              </>
            )}
          </div>
        }
      />

      <div className="mb-4 grid grid-cols-2 gap-3 lg:grid-cols-4">
        <StatCard label="Tiền về tháng này" value={formatCurrency(month.filter((t) => t.status !== 'ignored').reduce((n, t) => n + t.amount, 0))} icon={Wallet} tone="success" hint={`${month.length} giao dịch`} />
        <StatCard label="Tỷ lệ tự khớp" value={`${autoRate}%`} icon={Sparkles} tone="primary" hint="Trong các giao dịch đã khớp tháng này" />
        <StatCard label="Chưa khớp" value={unmatched.length} icon={TriangleAlert} tone={unmatched.length ? 'warning' : 'success'} hint={formatCurrency(unmatched.reduce((n, t) => n + t.amount, 0))} onClick={() => setTab('unmatched')} />
        <StatCard label="Đã bỏ qua" value={scoped.filter(byTab.ignored).length} icon={CircleSlash} onClick={() => setTab('ignored')} />
      </div>

      <div className="mb-3 flex flex-wrap gap-1.5">
        {(
          [
            ['unmatched', 'Chưa khớp'],
            ['matched', 'Đã khớp'],
            ['ignored', 'Bỏ qua'],
            ['all', 'Tất cả'],
          ] as [Tab, string][]
        ).map(([k, l]) => (
          <button key={k} type="button" onClick={() => setTab(k)} className={cn('inline-flex h-9 items-center gap-2 rounded-full border px-3.5 text-sm font-medium', tab === k ? 'border-primary-600 bg-primary-600 text-white' : 'border-line bg-surface text-ink-secondary hover:bg-primary-50')}>
            {l}
            <span className={cn('tabular rounded-full px-1.5 text-xs', tab === k ? 'bg-white/20' : 'bg-disabled text-ink-muted')}>{scoped.filter(byTab[k]).length}</span>
          </button>
        ))}
      </div>

      <DataTable<BankTxn>
        storageKey="fin-recon"
        columns={columns}
        data={data}
        rowKey="id"
        unitLabel="giao dịch"
        searchKeys={['content', 'payer', 'ref']}
        onRowClick={(t) => setOpenId(t.id)}
        empty={{ icon: QrCode, title: tab === 'unmatched' ? 'Đã khớp hết giao dịch' : 'Không có giao dịch' }}
      />

      <Drawer open={Boolean(open)} onClose={() => (setOpenId(null), setManual(undefined))} width={isMobile ? '100%' : 560} destroyOnHidden title={open && `Giao dịch ${open.ref}`}>
        {open && (
          <div className="flex flex-col gap-4 text-[13px]">
            <div className="rounded-xl border border-line p-3">
              <p className="tabular text-2xl font-semibold text-success-700">+{formatCurrency(open.amount)}</p>
              <p className="mt-1 font-mono text-ink">{open.content}</p>
              <p className="text-xs text-ink-muted">
                {open.payer} · {fmtDateTime(open.at)} · {bankOfAccount(open.account)?.bank} {bankOfAccount(open.account)?.no}
              </p>
              <div className="mt-2">
                <StatusTag tone={TXN_STATUS[open.status].tone} icon={TXN_STATUS[open.status].icon} size="sm">
                  {TXN_STATUS[open.status].label}
                </StatusTag>
                {open.matchedBy && open.status !== 'unmatched' && <span className="ml-2 text-xs text-ink-muted">{open.matchedBy} · {fmtDateTime(open.matchedAt)}</span>}
              </div>
              {open.note && <p className="mt-1 text-ink-secondary">{open.note}</p>}
            </div>

            {open.status === 'unmatched' ? (
              <>
                <div>
                  <p className="mb-2 font-semibold text-ink">Gợi ý hóa đơn</p>
                  <div className="flex flex-col gap-2">
                    {suggestions.map((m) => (
                      <div key={m.row.id} className="flex items-center justify-between gap-3 rounded-lg border border-line p-2.5">
                        <div className="min-w-0">
                          <UserCell size="sm" name={m.row.student?.fullName ?? '—'} subtitle={`${m.row.invoice.code} · ${m.row.cls?.code} · nợ ${formatCurrency(m.row.sum.remaining)} · hạn ${fmtDate(m.row.invoice.dueDate)}`} />
                          <div className="mt-1 flex flex-wrap gap-1">
                            {m.reasons.map((x) => (
                              <span key={x} className="rounded bg-success-50 px-1.5 py-0.5 text-[11px] text-success-700">{x}</span>
                            ))}
                          </div>
                        </div>
                        <Button icon={Link2} size="sm" onClick={() => doMatch(open, m.row.id)}>
                          Khớp
                        </Button>
                      </div>
                    ))}
                    {!suggestions.length && <p className="text-ink-muted">Không tìm thấy hóa đơn phù hợp — tìm thủ công bên dưới.</p>}
                  </div>
                </div>
                <div>
                  <p className="mb-2 font-semibold text-ink">Tìm hóa đơn khác</p>
                  <div className="flex gap-2">
                    <Select
                      value={manual}
                      onChange={setManual}
                      placeholder="Mã HĐ / tên học viên"
                      options={allRows.filter((r) => r.sum.remaining > 0).map((r) => ({ value: r.id, label: `${r.invoice.code} · ${r.student?.fullName} · nợ ${formatCurrency(r.sum.remaining)}` }))}
                    />
                    <Button icon={Link2} disabled={!manual} onClick={() => manual && doMatch(open, manual)}>
                      Khớp
                    </Button>
                  </div>
                </div>
                <div className="flex justify-end border-t border-line pt-3">
                  <Button
                    icon={CircleSlash}
                    variant="ghost"
                    tone="neutral"
                    onClick={async () => {
                      const res = await confirm({ title: 'Bỏ qua giao dịch?', description: 'Giao dịch không phải học phí (hoàn ứng, chuyển nội bộ...) — không ghi vào hóa đơn nào.', confirmText: 'Bỏ qua', tone: 'warning', requireReason: { label: 'Lý do', placeholder: 'VD: Hoàn ứng nội bộ' } });
                      if (!res) return;
                      const note = typeof res === 'object' ? (res.reason ?? '') : '';
                      if (USE_API) {
                        try {
                          await financeApi.ignoreTxn(open.id, note);
                        } catch (e) {
                          return notify.error({ title: 'Chưa bỏ qua được', description: e instanceof Error ? e.message : undefined });
                        }
                      } else ignoreTxn(open.id, user?.fullName ?? 'Kế toán', note);
                      notify.toast.info('Đã bỏ qua giao dịch');
                    }}
                  >
                    Không phải học phí — bỏ qua
                  </Button>
                </div>
              </>
            ) : (
              <div className="flex flex-wrap justify-end gap-2 border-t border-line pt-3">
                {open.invoiceId && (
                  <Button icon={QrCode} variant="soft" onClick={() => setInvoiceId(open.invoiceId!)}>
                    Xem hóa đơn
                  </Button>
                )}
                <Button
                  icon={Link2Off}
                  variant="ghost"
                  tone="danger"
                  onClick={async () => {
                    const ok = await confirm({ title: 'Gỡ khớp giao dịch?', description: open.invoiceId ? 'Lần thanh toán tương ứng sẽ bị xóa khỏi hóa đơn, công nợ quay lại như cũ.' : undefined, confirmText: 'Gỡ khớp', tone: 'danger' });
                    if (!ok) return;
                    if (USE_API) {
                      try {
                        await financeApi.unmatchTxn(open.id);
                      } catch (e) {
                        return notify.error({ title: 'Chưa gỡ khớp được', description: e instanceof Error ? e.message : undefined });
                      }
                    } else unmatchTxn(open.id);
                    notify.toast.info('Đã gỡ khớp');
                  }}
                >
                  {open.status === 'ignored' ? 'Đưa về chưa khớp' : 'Gỡ khớp'}
                </Button>
              </div>
            )}
          </div>
        )}
      </Drawer>

      <InvoiceDrawer row={allRows.find((r) => r.id === invoiceId)} onClose={() => setInvoiceId(null)} />
      {confirmDialog}
    </>
  );
}
