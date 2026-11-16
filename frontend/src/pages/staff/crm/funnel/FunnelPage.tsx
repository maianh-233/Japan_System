import { useMemo, useState } from 'react';
import { Drawer, Segmented, Tooltip } from 'antd';
import dayjs from 'dayjs';
import { ArrowDown, Hourglass, TriangleAlert } from 'lucide-react';
import { DataTable, PageHeader, Select, StatusTag, UserCell, type DataColumn } from '@/components/common';
import { useAppointmentStore } from '@/features/crm/appointments/appointments';
import { useAdmissionStore } from '@/features/crm/admission/admissionStore';
import { LeadDetailDrawer } from '@/features/crm/components/LeadDetailDrawer';
import { LEAD_SOURCES, saleName } from '@/features/crm/constants';
import { useCrmStore } from '@/features/crm/crmStore';
import { fmtDuration, journeyOf, STAGE_LABEL, STAGES, STUCK_DAYS, summarize, type FunnelStage, type FunnelSummary, type LeadJourney } from '@/features/crm/funnel';
import type { LeadSource } from '@/features/crm/types';
import { useMe } from '@/features/me/useMe';
import { useAuth } from '@/hooks/useAuth';
import { useBreakpoint } from '@/hooks/useBreakpoint';
import { cn } from '@/lib/cn';
import { ALL_BRANCHES } from '@/types/auth';

type GroupBy = 'sale' | 'campaign' | 'source';
type StuckStage = Exclude<FunnelStage, 'won'>;
const PERIODS = [
  { value: 30, label: '30 ngày' },
  { value: 90, label: '90 ngày' },
  { value: 180, label: '6 tháng' },
  { value: 0, label: 'Tất cả' },
];
const pct = (r?: number) => (r === undefined ? '—' : `${Math.round(r * 100)}%`);

interface GroupRow extends FunnelSummary {
  key: string;
  label: string;
  journeys: LeadJourney[];
}

/** Phễu chuyển đổi Lead → Đã gọi → Test → Chốt, theo Sale / chiến dịch / nguồn — thời gian trung bình mỗi bước và lead đang kẹt */
export default function FunnelPage() {
  const { branchId, currentBranch, can } = useAuth();
  const me = useMe();
  /** Sale (không phải trưởng nhóm) chỉ xem phễu lead của mình */
  const ownOnly = me?.department === 'sales' && !can('crm.lead-rules');
  const { isMobile } = useBreakpoint();
  const leads = useCrmStore((s) => s.leads);
  const consults = useCrmStore((s) => s.consultations);
  const links = useCrmStore((s) => s.links);
  const attempts = useAdmissionStore((s) => s.attempts);
  const campaigns = useAdmissionStore((s) => s.campaigns);
  const appointments = useAppointmentStore((s) => s.appointments);

  const [period, setPeriod] = useState(90);
  const [groupBy, setGroupBy] = useState<GroupBy>('sale');
  const [groupKey, setGroupKey] = useState<string>();
  const [stuckOpen, setStuckOpen] = useState<StuckStage | null>(null);
  const [leadId, setLeadId] = useState<string | null>(null);

  const journeys = useMemo(() => {
    const since = period ? dayjs().subtract(period, 'day') : undefined;
    const by = <T extends { leadId: string }>(xs: T[]) => {
      const m = new Map<string, T[]>();
      xs.forEach((x) => m.set(x.leadId, [...(m.get(x.leadId) ?? []), x]));
      return m;
    };
    const c = by(consults);
    const a = by(attempts);
    const ap = by(appointments);
    const lk = by(links);
    return leads
      .filter((l) => (branchId === ALL_BRANCHES || l.branchId === branchId) && (!since || dayjs(l.createdAt).isAfter(since)) && (!ownOnly || l.assigneeId === me?.id))
      .map((l) => journeyOf(l, { consults: c.get(l.id) ?? [], attempts: a.get(l.id) ?? [], appointments: ap.get(l.id) ?? [], links: lk.get(l.id) ?? [] }));
  }, [leads, consults, attempts, appointments, links, branchId, period, ownOnly, me]);

  const keyOf = (j: LeadJourney): { key: string; label: string } => {
    const l = j.lead;
    if (groupBy === 'sale') return { key: l.assigneeId ?? '-', label: saleName(l.assigneeId) ?? 'Chưa giao Sale' };
    if (groupBy === 'campaign') return { key: l.campaignId ?? '-', label: campaigns.find((c) => c.id === l.campaignId)?.name ?? 'Không thuộc chiến dịch' };
    return { key: l.source, label: LEAD_SOURCES[l.source as LeadSource]?.label ?? l.source };
  };

  const groups: GroupRow[] = useMemo(() => {
    const m = new Map<string, { label: string; journeys: LeadJourney[] }>();
    journeys.forEach((j) => {
      const k = keyOf(j);
      const g = m.get(k.key) ?? { label: k.label, journeys: [] };
      g.journeys.push(j);
      m.set(k.key, g);
    });
    return [...m.entries()].map(([key, g]) => ({ key, label: g.label, journeys: g.journeys, ...summarize(g.journeys) })).sort((a, b) => b.total - a.total);
    // eslint-disable-next-line react-hooks/exhaustive-deps
  }, [journeys, groupBy, campaigns]);

  const active = groups.find((g) => g.key === groupKey);
  const scope = active?.journeys ?? journeys;
  const sum = useMemo(() => summarize(scope), [scope]);

  const columns: DataColumn<GroupRow>[] = [
    { key: 'label', title: groupBy === 'sale' ? 'Sale' : groupBy === 'campaign' ? 'Chiến dịch' : 'Nguồn', width: 200, mobile: 'title', render: (_, r) => <span className="font-medium text-ink">{r.label}</span> },
    ...STAGES.map(
      (s): DataColumn<GroupRow> => ({
        key: s,
        title: STAGE_LABEL[s],
        width: 80,
        align: 'right',
        sorter: (a, b) => a.count[s] - b.count[s],
        render: (_, r) => <span className="tabular">{r.count[s]}</span>,
      }),
    ),
    { key: 'rate', title: 'Tỉ lệ chốt', width: 96, align: 'right', mobile: 'aside', sorter: (a, b) => a.count.won / (a.total || 1) - b.count.won / (b.total || 1), render: (_, r) => <span className="tabular font-semibold">{pct(r.total ? r.count.won / r.total : undefined)}</span> },
    ...STAGES.slice(0, -1).map(
      (s, i): DataColumn<GroupRow> => ({
        key: `t${i}`,
        title: `${STAGE_LABEL[s]} → ${STAGE_LABEL[STAGES[i + 1]]}`,
        width: 120,
        align: 'right',
        sorter: (a, b) => (a.avgHours[i] ?? 0) - (b.avgHours[i] ?? 0),
        render: (_, r) => (
          <Tooltip title={r.avgN[i] ? `Trung bình của ${r.avgN[i]} lead · chuyển ${pct(r.stepRate[i])}` : 'Chưa đủ dữ liệu'}>
            <span className={cn('tabular', i === sum.weakestStep || r.weakestStep === i ? 'text-ink' : 'text-ink-secondary')}>
              {fmtDuration(r.avgHours[i])}
              <span className="ml-1 text-xs text-ink-muted">({pct(r.stepRate[i])})</span>
            </span>
          </Tooltip>
        ),
      }),
    ),
    {
      key: 'stuck',
      title: 'Đang kẹt',
      width: 170,
      ellipsis: false,
      render: (_, r) => {
        const n = r.stuck.lead.length + r.stuck.called.length + r.stuck.test.length;
        return n && r.mostStuck ? (
          <StatusTag tone="warning" icon={Hourglass} size="sm">
            {n} lead · nhiều nhất ở {STAGE_LABEL[r.mostStuck]}
          </StatusTag>
        ) : (
          <span className="text-ink-muted">—</span>
        );
      },
    },
  ];

  const stuckList = stuckOpen ? sum.stuck[stuckOpen].slice().sort((a, b) => (b.stuckDays ?? 0) - (a.stuckDays ?? 0)) : [];

  return (
    <>
      <PageHeader
        title="Phễu chuyển đổi"
        subtitle={`Lead → Đã gọi → Test → Chốt · ${currentBranch ? `Chi nhánh ${currentBranch.name}` : 'Toàn hệ thống'} · lead tạo trong ${period ? `${period} ngày qua` : 'mọi thời gian'}`}
        breadcrumb={[{ title: 'Tuyển sinh' }, { title: 'Phễu chuyển đổi' }]}
      />

      {/* Bộ lọc: 1 hàng phía trên biểu đồ */}
      <div className="mb-4 flex flex-wrap items-center gap-2">
        <Segmented value={period} onChange={(v) => setPeriod(Number(v))} options={PERIODS} />
        <Segmented<GroupBy>
          value={groupBy}
          onChange={(v) => {
            setGroupBy(v);
            setGroupKey(undefined);
          }}
          options={[
            { value: 'sale', label: 'Theo Sale' },
            { value: 'campaign', label: 'Theo chiến dịch' },
            { value: 'source', label: 'Theo nguồn' },
          ]}
        />
        <Select className="!w-60" placeholder={groupBy === 'sale' ? 'Tất cả Sale' : groupBy === 'campaign' ? 'Tất cả chiến dịch' : 'Tất cả nguồn'} value={groupKey} onChange={(v) => setGroupKey(v as string | undefined)} options={groups.map((g) => ({ value: g.key, label: `${g.label} (${g.total})` }))} />
      </div>

      <div className="grid grid-cols-1 gap-6 xl:grid-cols-[minmax(0,1fr)_340px]">
        {/* ===== Phễu ===== */}
        <section className="rounded-[var(--radius-card)] border border-line bg-surface p-4 sm:p-5" aria-label="Phễu chuyển đổi">
          <div className="mb-4 flex flex-wrap items-baseline justify-between gap-2">
            <h2 className="text-base font-semibold text-ink">{active ? active.label : 'Tất cả lead'}</h2>
            <span className="text-[13px] text-ink-muted">
              {sum.total} lead · {sum.lost} đã mất · tỉ lệ chốt <strong className="text-ink">{pct(sum.total ? sum.count.won / sum.total : undefined)}</strong>
            </span>
          </div>
          {sum.total === 0 ? (
            <p className="py-10 text-center text-ink-muted">Không có lead trong khoảng thời gian này.</p>
          ) : (
            <ol className="flex flex-col">
              {STAGES.map((s, i) => {
                const n = sum.count[s];
                const width = (n / sum.total) * 100;
                const stuck = s !== 'won' ? sum.stuck[s] : [];
                return (
                  <li key={s} className="flex flex-col">
                    <div className="grid grid-cols-[64px_1fr_92px] items-center gap-3">
                      <span className="text-[13px] font-medium text-ink">{STAGE_LABEL[s]}</span>
                      <Tooltip title={`${STAGE_LABEL[s]}: ${n} lead · ${pct(n / sum.total)} số lead ban đầu`}>
                        <span className="relative block h-9 rounded bg-page" tabIndex={0} aria-label={`${STAGE_LABEL[s]}: ${n} lead`}>
                          <span className={cn('absolute inset-y-0 left-0 rounded', s === 'won' ? 'bg-primary-600' : 'bg-primary-400')} style={{ width: `${Math.max(n ? 1.5 : 0, width)}%` }} />
                        </span>
                      </Tooltip>
                      <span className="tabular text-right text-[13px]">
                        <strong className="text-ink">{n}</strong> <span className="text-ink-muted">· {pct(n / sum.total)}</span>
                      </span>
                    </div>
                    {i < STAGES.length - 1 && (
                      <div className="grid grid-cols-[64px_1fr] gap-3 py-1.5">
                        <span />
                        <div className="flex flex-wrap items-center gap-x-3 gap-y-1 text-xs">
                          <span className={cn('inline-flex items-center gap-1', sum.weakestStep === i ? 'font-semibold text-danger-700' : 'text-ink-secondary')}>
                            <ArrowDown size={13} aria-hidden />
                            {pct(sum.stepRate[i])} sang bước sau
                            {sum.weakestStep === i && ' · tỉ lệ thấp nhất'}
                          </span>
                          <Tooltip title={sum.avgN[i] ? `Tính trên ${sum.avgN[i]} lead có đủ 2 mốc thời gian` : 'Chưa có lead nào đủ 2 mốc'}>
                            <span className="inline-flex items-center gap-1 text-ink-secondary">
                              <Hourglass size={13} aria-hidden /> TB {fmtDuration(sum.avgHours[i])}
                            </span>
                          </Tooltip>
                          {stuck.length > 0 && (
                            <button type="button" onClick={() => setStuckOpen(s as StuckStage)} className="inline-flex items-center gap-1 rounded-full bg-warning-50 px-2 py-0.5 font-medium text-warning-700 hover:bg-warning-100">
                              <TriangleAlert size={12} aria-hidden /> {stuck.length} lead kẹt &gt; {STUCK_DAYS[s as StuckStage]} ngày
                            </button>
                          )}
                        </div>
                      </div>
                    )}
                  </li>
                );
              })}
            </ol>
          )}
        </section>

        {/* ===== Nhận định ===== */}
        <section className="flex flex-col gap-3">
          <h2 className="text-base font-semibold text-ink">Lead đang kẹt ở đâu</h2>
          {(['lead', 'called', 'test'] as StuckStage[]).map((s) => {
            const n = sum.stuck[s].length;
            const next = STAGES[STAGES.indexOf(s) + 1];
            return (
              <button
                key={s}
                type="button"
                disabled={!n}
                onClick={() => setStuckOpen(s)}
                className={cn('flex items-center gap-3 rounded-xl border px-4 py-3 text-left', n ? 'border-warning-100 hover:bg-canvas' : 'border-line opacity-70', sum.mostStuck === s && 'border-warning-600')}
              >
                <span className={cn('tabular w-8 text-xl font-semibold', n ? 'text-warning-700' : 'text-ink-muted')}>{n}</span>
                <span className="flex-1 text-[13px]">
                  <span className="block font-medium text-ink">
                    Ở bước “{STAGE_LABEL[s]}” chưa sang “{STAGE_LABEL[next]}”
                  </span>
                  <span className="text-xs text-ink-muted">
                    quá {STUCK_DAYS[s]} ngày{sum.mostStuck === s ? ' · nhiều nhất' : ''}
                  </span>
                </span>
              </button>
            );
          })}
          <p className="text-xs text-ink-muted">
            Mốc thời gian lấy tự động: lần tư vấn liên lạc được đầu tiên, hẹn / gửi test, ghi nhận chốt. Lead bỏ qua bước (vd chốt không test) vẫn tính đã qua bước, nhưng không vào thời gian trung bình.
          </p>
        </section>
      </div>

      <h2 className="mt-6 mb-3 text-lg font-semibold">{groupBy === 'sale' ? 'So sánh theo Sale' : groupBy === 'campaign' ? 'So sánh theo chiến dịch' : 'So sánh theo nguồn'}</h2>
      <DataTable<GroupRow>
        storageKey={`crm-funnel-${groupBy}`}
        columns={columns}
        data={groups}
        rowKey="key"
        unitLabel="nhóm"
        onRowClick={(r) => setGroupKey(r.key === groupKey ? undefined : r.key)}
        pagination={false}
        empty={{ title: 'Chưa có lead', description: 'Chọn khoảng thời gian dài hơn.' }}
      />
      <p className="mt-2 text-xs text-ink-muted">Bấm 1 dòng để xem phễu riêng của nhóm đó. Cột thời gian: trung bình (tỉ lệ sang bước sau).</p>

      <Drawer open={Boolean(stuckOpen)} onClose={() => setStuckOpen(null)} width={isMobile ? '100%' : 480} title={stuckOpen && `Lead kẹt ở bước “${STAGE_LABEL[stuckOpen]}” (${stuckList.length})`} destroyOnHidden>
        <ul className="flex flex-col divide-y divide-line">
          {stuckList.map((j) => (
            <li key={j.lead.id}>
              <button type="button" onClick={() => setLeadId(j.lead.id)} className="flex w-full items-center gap-3 py-3 text-left hover:bg-canvas">
                <span className="min-w-0 flex-1">
                  <UserCell name={j.lead.fullName} subtitle={`${j.lead.phone} · ${saleName(j.lead.assigneeId) ?? 'Chưa giao Sale'}`} size="sm" />
                </span>
                <span className="shrink-0 text-xs font-medium text-warning-700">{j.stuckDays} ngày</span>
              </button>
            </li>
          ))}
        </ul>
      </Drawer>
      <LeadDetailDrawer leadId={leadId} onClose={() => setLeadId(null)} onOpenLead={setLeadId} />
    </>
  );
}
