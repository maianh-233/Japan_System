import { useMemo, useState } from 'react';
import { Drawer, Form, Progress } from 'antd';
import dayjs from 'dayjs';
import { Banknote, Megaphone, MousePointerClick, Pause, Play, Target, Trophy, Wallet } from 'lucide-react';
import {
  Button,
  DataTable,
  DateInput,
  ModalForm,
  NumberInput,
  PageHeader,
  Select,
  StatCard,
  StatusTag,
  TextArea,
  TextInput,
  useNotify,
  type DataColumn,
} from '@/components/common';
import { useAdmissionStore, type CampaignInput } from '@/features/crm/admission/admissionStore';
import { LeadDetailDrawer } from '@/features/crm/components/LeadDetailDrawer';
import { CAMPAIGN_STATUS, CAMPAIGN_STATUS_OPTIONS, COURSE_OPTIONS, courseLabel, LEAD_SOURCE_OPTIONS, LEAD_SOURCES, LEAD_STATUS_OPTIONS, saleName } from '@/features/crm/constants';
import { useCrmStore } from '@/features/crm/crmStore';
import type { Campaign, Lead, LeadSource } from '@/features/crm/types';
import { useAuth } from '@/hooks/useAuth';
import { useBreakpoint } from '@/hooks/useBreakpoint';
import { cn } from '@/lib/cn';
import { fmtDate } from '@/lib/date';
import { formatCurrency } from '@/lib/format';
import { getBranches } from '@/features/system/useBranches';
import { ALL_BRANCHES } from '@/types/auth';

const short = (v: number) => (v >= 1_000_000 ? `${(v / 1_000_000).toLocaleString('vi-VN', { maximumFractionDigits: 1 })} tr` : v >= 1000 ? `${Math.round(v / 1000)}k` : formatCurrency(v));
const branchName = (id: string) => getBranches(false).find((b) => b.id === id)?.name ?? id;

interface Row extends Campaign {
  leads: Lead[];
  won: number;
  /** Chi phí / lead */
  cpl?: number;
  /** Chi phí / học viên chốt */
  cpa?: number;
}

type FormValues = Omit<CampaignInput, 'startDate' | 'endDate'> & { startDate: dayjs.Dayjs; endDate: dayjs.Dayjs };

/** Chiến dịch marketing: ngân sách, số lead mang về, tỷ lệ chốt, chi phí / lead. DỮ LIỆU GIẢ. */
export default function CampaignsPage() {
  const { user, branchId, can } = useAuth();
  const { isMobile } = useBreakpoint();
  const notify = useNotify();
  const { campaigns, addCampaign, updateCampaign } = useAdmissionStore();
  const leads = useCrmStore((s) => s.leads);
  const editable = can('crm.campaigns');

  const [status, setStatus] = useState<string>();
  const [channel, setChannel] = useState<string>();
  const [openId, setOpenId] = useState<string | null>(null);
  const [leadId, setLeadId] = useState<string | null>(null);
  const [editing, setEditing] = useState<Campaign | 'new' | null>(null);

  // Số liệu luôn tính trên TOÀN chiến dịch (chi phí không tách theo chi nhánh)
  const all: Row[] = useMemo(
    () =>
      campaigns.map((c) => {
        const ls = leads.filter((l) => l.campaignId === c.id && !l.hidden);
        const won = ls.filter((l) => l.status === 'won').length;
        return { ...c, leads: ls, won, cpl: ls.length ? c.spent / ls.length : undefined, cpa: won ? c.spent / won : undefined };
      }),
    [campaigns, leads],
  );
  const rows = all.filter((c) => (branchId === ALL_BRANCHES || c.branchIds.includes(branchId)) && (!status || c.status === status) && (!channel || c.channel === channel));
  const open = all.find((c) => c.id === openId);

  const spent = rows.reduce((s, c) => s + c.spent, 0);
  const budget = rows.reduce((s, c) => s + c.budget, 0);
  const nLeads = rows.reduce((s, c) => s + c.leads.length, 0);
  const nWon = rows.reduce((s, c) => s + c.won, 0);
  const ranked = rows.filter((c) => c.cpl !== undefined).sort((a, b) => a.cpl! - b.cpl!);
  const maxCpl = Math.max(1, ...ranked.map((c) => c.cpl!));

  // Lead theo nguồn: từ chiến dịch vs tự nhiên (không gắn chiến dịch)
  const bySource = useMemo(() => {
    const scope = leads.filter((l) => !l.hidden && (branchId === ALL_BRANCHES || l.branchId === branchId));
    return (Object.keys(LEAD_SOURCES) as LeadSource[])
      .map((s) => {
        const ls = scope.filter((l) => l.source === s);
        return { source: s, total: ls.length, paid: ls.filter((l) => l.campaignId).length };
      })
      .filter((x) => x.total)
      .sort((a, b) => b.total - a.total);
  }, [leads, branchId]);
  const maxSrc = Math.max(1, ...bySource.map((x) => x.total));

  const columns: DataColumn<Row>[] = [
    {
      key: 'name',
      title: 'Chiến dịch',
      width: 260,
      mobile: 'title',
      render: (_, r) => {
        const Icon = LEAD_SOURCES[r.channel].icon;
        return (
          <span className="flex items-start gap-2 leading-5">
            <Icon size={16} className="mt-0.5 shrink-0 text-ink-muted" aria-label={LEAD_SOURCES[r.channel].label} />
            <span className="min-w-0">
              <span className="block font-medium">{r.name}</span>
              <span className="block text-xs text-ink-muted">
                {r.code} · {r.owner}
              </span>
            </span>
          </span>
        );
      },
    },
    {
      key: 'time',
      title: 'Thời gian',
      width: 150,
      sorter: (a, b) => dayjs(a.startDate).valueOf() - dayjs(b.startDate).valueOf(),
      render: (_, r) => {
        const left = dayjs(r.endDate).diff(dayjs(), 'day');
        return (
          <span className="tabular leading-5 text-[13px]">
            {dayjs(r.startDate).format('DD/MM')} – {fmtDate(r.endDate)}
            {r.status === 'running' && <span className={cn('block text-xs', left <= 3 ? 'text-warning-700' : 'text-ink-muted')}>{left >= 0 ? `còn ${left} ngày` : 'đã quá ngày kết thúc'}</span>}
          </span>
        );
      },
    },
    {
      key: 'status',
      title: 'Trạng thái',
      width: 120,
      mobile: 'aside',
      render: (_, r) => (
        <StatusTag tone={CAMPAIGN_STATUS[r.status].tone} icon={CAMPAIGN_STATUS[r.status].icon} size="sm">
          {CAMPAIGN_STATUS[r.status].label}
        </StatusTag>
      ),
    },
    {
      key: 'budget',
      title: 'Đã chi / Ngân sách',
      width: 180,
      sorter: (a, b) => a.spent - b.spent,
      render: (_, r) => (
        <div className="leading-4">
          <Progress percent={r.budget ? Math.round((r.spent / r.budget) * 100) : 0} size="small" showInfo={false} status={r.spent > r.budget ? 'exception' : 'normal'} />
          <p className="tabular text-xs text-ink-secondary">
            {short(r.spent)} / {short(r.budget)}
          </p>
        </div>
      ),
    },
    {
      key: 'leads',
      title: 'Lead',
      width: 100,
      align: 'center',
      sorter: (a, b) => a.leads.length - b.leads.length,
      render: (_, r) => (
        <span className="tabular leading-5">
          <b>{r.leads.length}</b>
          {r.targetLeads ? <span className="block text-xs text-ink-muted">/ {r.targetLeads} chỉ tiêu</span> : null}
        </span>
      ),
    },
    {
      key: 'won',
      title: 'Chốt',
      width: 90,
      align: 'center',
      sorter: (a, b) => a.won - b.won,
      render: (_, r) => (
        <span className="tabular leading-5">
          <b>{r.won}</b>
          {r.leads.length > 0 && <span className="block text-xs text-ink-muted">{Math.round((r.won / r.leads.length) * 100)}%</span>}
        </span>
      ),
    },
    { key: 'cpl', title: 'Chi phí / lead', width: 120, align: 'right', sorter: (a, b) => (a.cpl ?? Infinity) - (b.cpl ?? Infinity), render: (_, r) => <span className="tabular">{r.cpl !== undefined ? short(r.cpl) : '—'}</span> },
    { key: 'cpa', title: 'Chi phí / chốt', width: 120, align: 'right', sorter: (a, b) => (a.cpa ?? Infinity) - (b.cpa ?? Infinity), render: (_, r) => <span className="tabular font-semibold">{r.cpa !== undefined ? short(r.cpa) : '—'}</span> },
  ];

  const toForm = (c: Campaign): Partial<FormValues> => ({ ...c, startDate: dayjs(c.startDate), endDate: dayjs(c.endDate) });

  return (
    <>
      <PageHeader
        title="Chiến dịch"
        subtitle="Lead gắn chiến dịch qua mã UTM của form quảng cáo, hoặc Sale chọn khi nhập tay. Chi phí / lead tính trên toàn chiến dịch."
        breadcrumb={[{ title: 'Tuyển sinh' }, { title: 'Chiến dịch' }]}
        actions={
          <>
            <Button action="export" variant="outline" collapseOnMobile onClick={() => notify.toast.info('Xuất Excel (giả lập)')} />
            {editable && (
              <Button action="create" collapseOnMobile onClick={() => setEditing('new')}>
                Tạo chiến dịch
              </Button>
            )}
          </>
        }
      />

      <div className="mb-4 grid grid-cols-2 gap-3 lg:grid-cols-4">
        <StatCard label="Đã chi" value={formatCurrency(spent)} icon={Wallet} tone="primary" hint={`${budget ? Math.round((spent / budget) * 100) : 0}% của ngân sách ${short(budget)}`} />
        <StatCard label="Lead từ chiến dịch" value={nLeads} icon={MousePointerClick} tone="info" hint={`${rows.filter((c) => c.status === 'running').length} chiến dịch đang chạy`} />
        <StatCard label="Chi phí / lead TB" value={nLeads ? short(spent / nLeads) : '—'} icon={Banknote} tone="warning" />
        <StatCard label="Tỷ lệ chốt" value={nLeads ? `${Math.round((nWon / nLeads) * 100)}%` : '—'} icon={Trophy} tone="success" hint={`${nWon} học viên · ${nWon ? short(spent / nWon) : '—'} / chốt`} />
      </div>

      <div className="mb-4 grid gap-4 lg:grid-cols-2">
        <section className="rounded-xl border border-line bg-surface p-4">
          <p className="mb-3 text-sm font-semibold text-ink">Chi phí / lead — thấp là tốt</p>
          <div className="flex flex-col gap-2.5">
            {ranked.map((c, i) => (
              <button key={c.id} type="button" onClick={() => setOpenId(c.id)} className="grid grid-cols-[120px_1fr_60px] items-center gap-2 text-left text-[13px] hover:opacity-80">
                <span className="truncate text-ink-secondary" title={c.name}>
                  {c.code}
                </span>
                <div className="h-5 rounded bg-canvas">
                  <div className={cn('h-full rounded', i === 0 ? 'bg-success-600' : 'bg-primary-500')} style={{ width: `${(c.cpl! / maxCpl) * 100}%` }} />
                </div>
                <span className="tabular text-right text-xs">{short(c.cpl!)}</span>
              </button>
            ))}
            {!ranked.length && <p className="text-[13px] text-ink-muted">Chưa có chiến dịch nào mang về lead.</p>}
          </div>
        </section>

        <section className="rounded-xl border border-line bg-surface p-4">
          <p className="mb-3 text-sm font-semibold text-ink">Lead theo nguồn — từ quảng cáo vs tự nhiên</p>
          <div className="flex flex-col gap-2.5">
            {bySource.map((x) => (
              <div key={x.source} className="grid grid-cols-[120px_1fr_40px] items-center gap-2 text-[13px]">
                <span className="truncate text-ink-secondary">{LEAD_SOURCES[x.source].label}</span>
                <div className="flex h-5 overflow-hidden rounded bg-canvas" style={{ width: `${(x.total / maxSrc) * 100}%` }}>
                  <div className="h-full bg-primary-500" style={{ width: `${(x.paid / x.total) * 100}%` }} title={`${x.paid} lead từ chiến dịch`} />
                  <div className="h-full flex-1 bg-primary-200" title={`${x.total - x.paid} lead tự nhiên`} />
                </div>
                <span className="tabular text-right text-xs">{x.total}</span>
              </div>
            ))}
          </div>
          <p className="mt-3 flex items-center gap-3 text-[11px] text-ink-muted">
            <span className="inline-flex items-center gap-1">
              <span className="inline-block h-2 w-3 rounded-sm bg-primary-500" /> Từ chiến dịch
            </span>
            <span className="inline-flex items-center gap-1">
              <span className="inline-block h-2 w-3 rounded-sm bg-primary-200" /> Tự nhiên / giới thiệu
            </span>
          </p>
        </section>
      </div>

      <DataTable<Row>
        storageKey="crm-campaigns"
        columns={columns}
        data={rows}
        rowKey="id"
        unitLabel="chiến dịch"
        searchKeys={['name', 'code', 'owner', 'utm']}
        onRowClick={(r) => setOpenId(r.id)}
        search={{
          placeholder: 'Tìm theo tên, mã, UTM, người phụ trách...',
          activeFilterCount: [status, channel].filter(Boolean).length,
          filters: (
            <>
              <Select placeholder="Trạng thái" value={status} onChange={setStatus} options={CAMPAIGN_STATUS_OPTIONS} showSearch={false} className="lg:!w-40" />
              <Select placeholder="Kênh" value={channel} onChange={setChannel} options={LEAD_SOURCE_OPTIONS} className="lg:!w-40" />
            </>
          ),
          onReset: () => {
            setStatus(undefined);
            setChannel(undefined);
          },
        }}
        rowActions={(r) =>
          editable ? (
            <>
              <Button action="edit" iconOnly size="sm" onClick={() => setEditing(r)} />
              {(r.status === 'running' || r.status === 'paused') && (
                <Button
                  icon={r.status === 'running' ? Pause : Play}
                  iconOnly
                  size="sm"
                  variant="ghost"
                  tone="neutral"
                  aria-label={r.status === 'running' ? 'Tạm dừng' : 'Chạy tiếp'}
                  onClick={() => {
                    updateCampaign(r.id, { status: r.status === 'running' ? 'paused' : 'running' });
                    notify.toast.success(r.status === 'running' ? 'Đã tạm dừng chiến dịch' : 'Chiến dịch chạy tiếp');
                  }}
                />
              )}
            </>
          ) : null
        }
        actionsWidth={100}
        empty={{ icon: Megaphone, title: 'Chưa có chiến dịch', action: editable ? <Button action="create" onClick={() => setEditing('new')}>Tạo chiến dịch</Button> : undefined }}
      />

      {/* ===== Chi tiết chiến dịch ===== */}
      <Drawer open={Boolean(open)} onClose={() => setOpenId(null)} width={isMobile ? '100%' : 620} destroyOnHidden title={open?.name}>
        {open && (
          <div className="flex flex-col gap-4 text-[13px]">
            <div className="flex flex-wrap items-center gap-2 text-ink-secondary">
              <StatusTag tone={CAMPAIGN_STATUS[open.status].tone} icon={CAMPAIGN_STATUS[open.status].icon} size="sm">
                {CAMPAIGN_STATUS[open.status].label}
              </StatusTag>
              <span>
                {LEAD_SOURCES[open.channel].label} · {fmtDate(open.startDate)} – {fmtDate(open.endDate)} · {open.owner}
              </span>
            </div>
            <div className="grid grid-cols-2 gap-2 rounded-xl border border-line p-3 sm:grid-cols-4">
              {(
                [
                  ['Đã chi', formatCurrency(open.spent)],
                  ['Lead', `${open.leads.length}${open.targetLeads ? ` / ${open.targetLeads}` : ''}`],
                  ['Chi phí / lead', open.cpl !== undefined ? formatCurrency(Math.round(open.cpl)) : '—'],
                  ['Chi phí / chốt', open.cpa !== undefined ? formatCurrency(Math.round(open.cpa)) : '—'],
                ] as [string, string][]
              ).map(([l, v]) => (
                <div key={l}>
                  <p className="text-xs text-ink-muted">{l}</p>
                  <p className="tabular font-semibold">{v}</p>
                </div>
              ))}
            </div>
            <dl className="grid grid-cols-[110px_1fr] gap-x-3 gap-y-1.5">
              <dt className="text-ink-muted">Chi nhánh</dt>
              <dd>{open.branchIds.map(branchName).join(', ')}</dd>
              <dt className="text-ink-muted">Khóa quảng bá</dt>
              <dd>{open.courses.map((c) => courseLabel(c)).join(', ') || 'Tất cả'}</dd>
              <dt className="text-ink-muted">Mã UTM</dt>
              <dd>{open.utm ? <code className="rounded bg-canvas px-1.5 py-0.5">utm_campaign={open.utm}</code> : '—'}</dd>
              {open.note && (
                <>
                  <dt className="text-ink-muted">Ghi chú</dt>
                  <dd>{open.note}</dd>
                </>
              )}
            </dl>

            {/* Phễu theo trạng thái lead */}
            <div className="rounded-xl border border-line p-3">
              <p className="mb-2 font-semibold text-ink">Phễu chuyển đổi</p>
              <div className="flex flex-col gap-1.5">
                {LEAD_STATUS_OPTIONS.map((s) => {
                  const n = open.leads.filter((l) => l.status === s.value).length;
                  return (
                    <div key={s.value} className="grid grid-cols-[100px_1fr_32px] items-center gap-2">
                      <span className="text-ink-secondary">{s.label}</span>
                      <div className="h-4 rounded bg-canvas">
                        <div className={cn('h-full rounded', s.value === 'won' ? 'bg-success-600' : s.value === 'lost' ? 'bg-ink-muted/40' : 'bg-primary-500')} style={{ width: `${open.leads.length ? (n / open.leads.length) * 100 : 0}%` }} />
                      </div>
                      <span className="tabular text-right">{n}</span>
                    </div>
                  );
                })}
              </div>
            </div>

            <div>
              <p className="mb-2 font-semibold text-ink">Lead ({open.leads.length})</p>
              <ul className="flex flex-col divide-y divide-line rounded-xl border border-line">
                {open.leads.map((l) => (
                  <li key={l.id}>
                    <button type="button" onClick={() => setLeadId(l.id)} className="flex w-full items-center justify-between gap-2 px-3 py-2 text-left hover:bg-canvas">
                      <span className="min-w-0 leading-5">
                        <span className="block truncate font-medium">{l.fullName}</span>
                        <span className="block text-xs text-ink-muted">
                          {l.code} · {branchName(l.branchId)} · {saleName(l.assigneeId) ?? 'Chưa giao'}
                        </span>
                      </span>
                      <StatusTag group="lead" status={l.status} size="sm" />
                    </button>
                  </li>
                ))}
                {!open.leads.length && <li className="px-3 py-2 text-ink-muted">Chưa có lead nào gắn chiến dịch này.</li>}
              </ul>
            </div>

            {editable && (
              <div className="flex items-end gap-2 rounded-xl bg-canvas p-3">
                <div className="flex-1">
                  <p className="mb-1 text-xs text-ink-muted">Cập nhật chi phí thực tế (theo báo cáo nền tảng quảng cáo)</p>
                  <NumberInput
                    currency
                    min={0}
                    step={100_000}
                    defaultValue={open.spent}
                    onBlur={(e) => {
                      const v = Number(String(e.target.value).replace(/\D/g, ''));
                      if (!Number.isNaN(v) && v !== open.spent) {
                        updateCampaign(open.id, { spent: v });
                        notify.toast.success('Đã cập nhật chi phí');
                      }
                    }}
                  />
                </div>
                <Target size={18} className="mb-2.5 text-ink-muted" aria-hidden />
              </div>
            )}
          </div>
        )}
      </Drawer>

      <LeadDetailDrawer leadId={leadId} onClose={() => setLeadId(null)} onOpenLead={setLeadId} />

      {/* ===== Tạo / sửa chiến dịch ===== */}
      <ModalForm<FormValues>
        key={editing === 'new' ? 'new' : (editing?.id ?? 'none')}
        open={Boolean(editing)}
        onClose={() => setEditing(null)}
        title={editing === 'new' ? 'Tạo chiến dịch' : 'Sửa chiến dịch'}
        columns={2}
        width={720}
        submitAction="save"
        successMessage="Đã lưu chiến dịch"
        initialValues={
          editing && editing !== 'new'
            ? toForm(editing)
            : { status: 'planned', channel: 'facebook', branchIds: branchId === ALL_BRANCHES ? getBranches(false).map((b) => b.id) : [branchId], courses: [], spent: 0, owner: user?.fullName, startDate: dayjs(), endDate: dayjs().add(30, 'day') }
        }
        onSubmit={(v) => {
          if (v.endDate.isBefore(v.startDate, 'day')) throw new Error('Ngày kết thúc phải sau ngày bắt đầu');
          const input: CampaignInput = { ...v, courses: v.courses ?? [], startDate: v.startDate.format('YYYY-MM-DD'), endDate: v.endDate.format('YYYY-MM-DD') };
          if (editing === 'new') addCampaign(input);
          else if (editing) updateCampaign(editing.id, input);
        }}
      >
        <Form.Item name="name" label="Tên chiến dịch" rules={[{ required: true, message: 'Nhập tên' }]} className="md:col-span-2">
          <TextInput placeholder="Vd: Facebook Lead Ads — Khai giảng N5 tháng 12" />
        </Form.Item>
        <Form.Item name="code" label="Mã" rules={[{ required: true, message: 'Nhập mã' }]}>
          <TextInput placeholder="FB-N5-T12" />
        </Form.Item>
        <Form.Item name="channel" label="Kênh" rules={[{ required: true }]}>
          <Select options={LEAD_SOURCE_OPTIONS} allowClear={false} />
        </Form.Item>
        <Form.Item name="startDate" label="Bắt đầu" rules={[{ required: true, message: 'Chọn ngày' }]}>
          <DateInput />
        </Form.Item>
        <Form.Item name="endDate" label="Kết thúc" rules={[{ required: true, message: 'Chọn ngày' }]}>
          <DateInput />
        </Form.Item>
        <Form.Item name="budget" label="Ngân sách" rules={[{ required: true, message: 'Nhập ngân sách' }]}>
          <NumberInput currency min={0} step={500_000} />
        </Form.Item>
        <Form.Item name="spent" label="Đã chi">
          <NumberInput currency min={0} step={100_000} />
        </Form.Item>
        <Form.Item name="targetLeads" label="Chỉ tiêu số lead">
          <NumberInput min={0} />
        </Form.Item>
        <Form.Item name="status" label="Trạng thái">
          <Select options={CAMPAIGN_STATUS_OPTIONS} allowClear={false} showSearch={false} />
        </Form.Item>
        <Form.Item name="branchIds" label="Chi nhánh" rules={[{ required: true, message: 'Chọn chi nhánh' }]}>
          <Select mode="multiple" options={getBranches(false).map((b) => ({ value: b.id, label: b.name }))} />
        </Form.Item>
        <Form.Item name="courses" label="Khóa quảng bá">
          <Select mode="multiple" placeholder="Tất cả" options={COURSE_OPTIONS} />
        </Form.Item>
        <Form.Item name="owner" label="Người phụ trách" rules={[{ required: true, message: 'Nhập người phụ trách' }]}>
          <TextInput />
        </Form.Item>
        <Form.Item name="utm" label="Mã UTM" tooltip="Gắn vào link quảng cáo (utm_campaign=...) — lead từ form tự gắn chiến dịch này">
          <TextInput placeholder="fb_n5_t12" />
        </Form.Item>
        <Form.Item name="note" label="Ghi chú" className="md:col-span-2">
          <TextArea rows={2} maxLength={500} placeholder="Ưu đãi, nội dung chính, đối tượng..." />
        </Form.Item>
      </ModalForm>
    </>
  );
}
