import { useMemo, useState } from 'react';
import { Checkbox, Form, Switch, Tooltip } from 'antd';
import dayjs from 'dayjs';
import { AlarmClock, ArrowDown, ArrowUp, BellRing, Check, CircleQuestionMark, FlaskConical, Lightbulb, Repeat2, Shuffle, Timer, UserRoundCheck, Users, Wand2 } from 'lucide-react';
import {
  Button,
  DataTable,
  ModalForm,
  NumberInput,
  PageHeader,
  Select,
  StatCard,
  StatusTag,
  TextInput,
  useConfirm,
  useNotify,
  type DataColumn,
} from '@/components/common';
import { useAdmissionStore, type RuleInput } from '@/features/crm/admission/admissionStore';
import { isOnShift, openLoad, routeLead, slaOf } from '@/features/crm/admission/routing';
import { COURSE_OPTIONS, courseLabel, ESCALATE_OPTIONS, LEAD_SOURCE_OPTIONS, LEAD_SOURCES, ROUTE_METHODS, saleName, useSales } from '@/features/crm/constants';
import { isSlaOverdue, useCrmStore } from '@/features/crm/crmStore';
import type { AssignmentLog, Lead, LeadRule, LeadSource } from '@/features/crm/types';
import { useWorkStore } from '@/features/work/workStore';
import { useAuth } from '@/hooks/useAuth';
import { cn } from '@/lib/cn';
import { fmtDateTime, fromNow } from '@/lib/date';
import { getBranches } from '@/features/system/useBranches';
import { ALL_BRANCHES } from '@/types/auth';

const branchName = (id: string) => getBranches(false).find((b) => b.id === id)?.name ?? id;
const branchOptions = () => getBranches(false).map((b) => ({ value: b.id, label: b.name }));
const METHOD_OPTIONS = (Object.keys(ROUTE_METHODS) as LeadRule['method'][]).map((k) => ({ value: k, label: `${ROUTE_METHODS[k].label} — ${ROUTE_METHODS[k].hint}` }));

/** Chip điều kiện "Mọi ..." khi bỏ trống */
function Cond({ label, values }: { label: string; values: string[] }) {
  return (
    <span className="inline-flex flex-wrap items-center gap-1">
      <span className="text-ink-muted">{label}:</span>
      {values.length ? (
        values.map((v) => (
          <span key={v} className="rounded bg-primary-50 px-1.5 py-0.5 text-primary-700">
            {v}
          </span>
        ))
      ) : (
        <span className="text-ink-muted italic">mọi</span>
      )}
    </span>
  );
}

/** Diễn giải quy tắc thành 1 câu dễ hiểu */
function describeRule(r: LeadRule): string {
  const src = r.sources.length ? `từ ${r.sources.map((s) => LEAD_SOURCES[s].label).join(' / ')}` : 'từ mọi nguồn';
  const br = r.branchIds.length ? `ở ${r.branchIds.map(branchName).join(' / ')}` : 'ở mọi chi nhánh';
  const it = r.interests.length ? `, quan tâm ${r.interests.map((v) => courseLabel(v) ?? v).join(' / ')}` : '';
  const names = r.saleIds.map((id) => saleName(id) ?? id);
  const who =
    r.method === 'round_robin'
      ? `chia lần lượt: ${names.join(' → ')}`
      : r.method === 'least_load'
        ? `giao cho người đang giữ ít lead nhất trong ${names.length} Sale`
        : `ưu tiên ${names[0] ?? '—'}, rồi tới người kế tiếp`;
  return `Lead ${src} ${br}${it} → ${who}${r.onlyOnShift ? ' (chỉ người đang trong ca)' : ''}. Sale phải gọi trong ${r.slaMinutes} phút.`;
}

const GUIDE_KEY = 'lead-rules-guide-hidden';
const readGuideHidden = () => {
  try {
    return localStorage.getItem(GUIDE_KEY) === '1';
  } catch {
    return false;
  }
};
const writeGuideHidden = (hidden: boolean) => {
  try {
    localStorage.setItem(GUIDE_KEY, hidden ? '1' : '0');
  } catch {
    /* trình duyệt chặn lưu — bỏ qua */
  }
};

const GUIDE_STEPS = [
  {
    title: 'Lead mới chưa có Sale',
    body: 'Khi thêm lead ở trang Lead mà để trống ô "Sale phụ trách" (hoặc lead tự về từ Facebook, form web...).',
  },
  {
    title: 'Dò quy tắc từ trên xuống',
    body: 'Gặp quy tắc ĐẦU TIÊN khớp nguồn + chi nhánh + khóa quan tâm thì dừng. Ô điều kiện bỏ trống = khớp tất cả.',
  },
  {
    title: 'Chọn 1 Sale và bấm giờ',
    body: 'Chọn Sale trong nhóm theo cách chia (lần lượt / ít việc nhất / ưu tiên). Bắt đầu đếm thời hạn gọi (SLA) — trễ sẽ hiện ở khung "Đang quá SLA".',
  },
];

/** Khung hướng dẫn cách trang hoạt động */
function HowItWorks({ onHide }: { onHide: () => void }) {
  return (
    <section className="mb-4 rounded-xl border border-primary-200 bg-primary-50/60 p-4">
      <div className="mb-3 flex items-start justify-between gap-3">
        <p className="flex items-center gap-1.5 text-sm font-semibold text-ink">
          <CircleQuestionMark size={16} className="text-primary-600" aria-hidden /> Trang này hoạt động thế nào?
        </p>
        <Button icon={Check} size="sm" variant="ghost" tone="neutral" onClick={onHide}>
          Đã hiểu, ẩn đi
        </Button>
      </div>
      <ol className="grid gap-3 md:grid-cols-3">
        {GUIDE_STEPS.map((s, i) => (
          <li key={s.title} className="flex gap-2.5 rounded-lg bg-surface p-3">
            <span className="tabular grid size-6 shrink-0 place-items-center rounded-full bg-primary-600 text-xs font-bold text-white">{i + 1}</span>
            <span className="text-[13px] leading-5">
              <span className="block font-semibold text-ink">{s.title}</span>
              <span className="text-ink-secondary">{s.body}</span>
            </span>
          </li>
        ))}
      </ol>
      <div className="mt-3 rounded-lg bg-surface p-3 text-[13px] leading-5 text-ink-secondary">
        <p className="mb-1 font-semibold text-ink">Ví dụ</p>
        <p>
          Quy tắc ① "Facebook ở Cầu Giấy → chia lần lượt Bảo → Tùng → Như", quy tắc ② "Mọi nguồn → Trưởng nhóm".
          <br />
          Lead Facebook ở Cầu Giấy → khớp ① → lần này tới lượt Bảo, lead sau tới Tùng. Lead TikTok → không khớp ① → rơi xuống ② → giao Trưởng nhóm.
        </p>
      </div>
      <p className="mt-3 flex items-start gap-1.5 text-[13px] text-ink-secondary">
        <Lightbulb size={14} className="mt-0.5 shrink-0 text-warning-600" aria-hidden />
        <span>
          Đặt quy tắc <b>cụ thể ở trên</b>, quy tắc <b>chung (bỏ trống điều kiện) ở cuối</b> làm lưới an toàn. Dùng khung <b>"Thử chia một lead"</b> bên phải để kiểm tra trước khi áp dụng.
        </span>
      </p>
    </section>
  );
}

/** Quy tắc chia lead tự động + SLA liên hệ lần đầu + cảnh báo quá hạn. DỮ LIỆU GIẢ. */
export default function LeadRulesPage() {
  const { branchId, can } = useAuth();
  const notify = useNotify();
  const { confirm, confirmDialog } = useConfirm();
  const { rules, logs, addRule, updateRule, removeRule, moveRule } = useAdmissionStore();
  const leads = useCrmStore((s) => s.leads);
  const updateLead = useCrmStore((s) => s.updateLead);
  const shifts = useWorkStore((s) => s.shifts);
  const editable = can('crm.lead-rules');

  const [editing, setEditing] = useState<LeadRule | 'new' | null>(null);
  const [guideHidden, setGuideHidden] = useState(readGuideHidden);
  const toggleGuide = (hidden: boolean) => {
    setGuideHidden(hidden);
    writeGuideHidden(hidden);
  };
  const [sim, setSim] = useState<{ source: LeadSource; branchId: string; interest?: string }>({ source: 'facebook', branchId: branchId === ALL_BRANCHES ? 'cg' : branchId });

  const inBranch = (b: string) => branchId === ALL_BRANCHES || b === branchId;
  const load = useMemo(() => openLoad(leads), [leads]);
  const sales = useSales(branchId);
  const allSales = useSales();
  const onShiftNow = sales.filter((s) => isOnShift(s.id, shifts));
  const overdue = leads
    .filter((l) => inBranch(l.branchId) && !l.hidden && isSlaOverdue(l, slaOf(rules, l)))
    .map((l) => ({ lead: l, sla: slaOf(rules, l), late: dayjs().diff(dayjs(l.createdAt), 'minute') - slaOf(rules, l) }))
    .sort((a, b) => b.late - a.late);
  const todayLogs = logs.filter((l) => dayjs(l.at).isSame(dayjs(), 'day'));
  const simResult = routeLead(sim, rules, leads, shifts);

  const remove = async (r: LeadRule) => {
    const ok = await confirm({
      title: `Xóa quy tắc "${r.name}"?`,
      consequences: ['Lead mới khớp điều kiện này sẽ rơi xuống quy tắc kế tiếp (hoặc chờ giao tay).', 'Lead đã chia trước đó giữ nguyên Sale.'],
      tone: 'danger',
      confirmAction: 'delete',
    });
    if (ok) {
      removeRule(r.id);
      notify.toast.success('Đã xóa quy tắc');
    }
  };

  const reassign = (lead: Lead, saleId: string) => {
    updateLead(lead.id, { assigneeId: saleId });
    useAdmissionStore.getState().addLog({ leadId: lead.id, saleId, reason: `Chuyển tay do quá SLA (trước: ${saleName(lead.assigneeId) ?? 'chưa giao'})` });
    notify.toast.success(`Đã chuyển ${lead.fullName} cho ${saleName(saleId)}`);
  };

  const logColumns: DataColumn<AssignmentLog>[] = [
    { key: 'at', title: 'Thời điểm', width: 150, render: (_, r) => <span title={fmtDateTime(r.at)}>{fromNow(r.at)}</span> },
    {
      key: 'lead',
      title: 'Lead',
      width: 190,
      mobile: 'title',
      render: (_, r) => {
        const l = leads.find((x) => x.id === r.leadId);
        return l ? (
          <span className="leading-5">
            <span className="font-medium">{l.fullName}</span>
            <span className="block text-xs text-ink-muted">
              {l.code} · {LEAD_SOURCES[l.source].label} · {branchName(l.branchId)}
            </span>
          </span>
        ) : (
          r.leadId
        );
      },
    },
    { key: 'sale', title: 'Giao cho', width: 150, mobile: 'aside', render: (_, r) => (r.saleId ? saleName(r.saleId) : <StatusTag tone="warning" icon={AlarmClock} size="sm">Chưa giao</StatusTag>) },
    { key: 'reason', title: 'Lý do', render: (_, r) => <span className="text-[13px] text-ink-secondary">{r.reason}</span> },
  ];

  return (
    <>
      <PageHeader
        title="Chia lead & SLA"
        subtitle="Lead mới chưa có Sale sẽ đi qua các quy tắc từ trên xuống — quy tắc ĐẦU TIÊN khớp sẽ chọn Sale và đặt thời hạn liên hệ."
        breadcrumb={[{ title: 'Tuyển sinh' }, { title: 'Chia lead & SLA' }]}
        actions={
          <>
            {guideHidden && (
              <Button icon={CircleQuestionMark} variant="outline" collapseOnMobile onClick={() => toggleGuide(false)}>
                Hướng dẫn
              </Button>
            )}
            {editable && (
              <Button action="create" collapseOnMobile onClick={() => setEditing('new')}>
                Thêm quy tắc
              </Button>
            )}
          </>
        }
      />

      {!guideHidden && <HowItWorks onHide={() => toggleGuide(true)} />}

      <div className="mb-4 grid grid-cols-2 gap-3 lg:grid-cols-4">
        <StatCard label="Quy tắc đang bật" value={`${rules.filter((r) => r.active).length}/${rules.length}`} icon={Shuffle} tone="primary" />
        <StatCard label="Tự chia hôm nay" value={todayLogs.filter((l) => l.ruleId).length} icon={Wand2} tone="info" hint={`${todayLogs.filter((l) => !l.saleId).length} lead chưa giao được`} />
        <StatCard label="Sale đang trong ca" value={`${onShiftNow.length}/${sales.length}`} icon={UserRoundCheck} tone="success" hint={onShiftNow.map((s) => s.fullName.split(' ').pop()).join(', ') || 'Ngoài giờ làm'} />
        <StatCard label="Lead quá SLA" value={overdue.length} icon={AlarmClock} tone={overdue.length ? 'danger' : 'neutral'} hint="Mới, chưa được liên hệ" />
      </div>

      <div className="mb-4 grid gap-4 lg:grid-cols-[1fr_340px]">
        {/* ===== Danh sách quy tắc theo thứ tự ưu tiên ===== */}
        <section className="flex flex-col gap-3">
          {rules.map((r, i) => (
            <article key={r.id} className={cn('rounded-xl border bg-surface p-4', r.active ? 'border-line' : 'border-dashed border-line opacity-70')}>
              <div className="flex items-start gap-3">
                <span className={cn('tabular grid size-7 shrink-0 place-items-center rounded-full text-sm font-bold', r.active ? 'bg-primary-600 text-white' : 'bg-disabled text-ink-muted')}>{i + 1}</span>
                <div className="min-w-0 flex-1">
                  <div className="flex flex-wrap items-center gap-2">
                    <h3 className="font-semibold text-ink">{r.name}</h3>
                    <StatusTag tone="info" size="sm" icon={Repeat2}>
                      {ROUTE_METHODS[r.method].label}
                    </StatusTag>
                    {r.onlyOnShift && (
                      <StatusTag tone="success" size="sm" icon={UserRoundCheck}>
                        Chỉ Sale trong ca
                      </StatusTag>
                    )}
                  </div>
                  <p className="mt-1.5 text-[13px] leading-5 text-ink-secondary">{describeRule(r)}</p>
                  <div className="mt-2 flex flex-wrap gap-x-4 gap-y-1.5 text-[13px]">
                    <Cond label="Nguồn" values={r.sources.map((s) => LEAD_SOURCES[s].label)} />
                    <Cond label="Chi nhánh" values={r.branchIds.map(branchName)} />
                    <Cond label="Quan tâm" values={r.interests.map((v) => courseLabel(v) ?? v)} />
                  </div>
                  <div className="mt-3 flex flex-wrap gap-1.5">
                    {r.saleIds.map((id, k) => {
                      const on = isOnShift(id, shifts);
                      const next = r.method === 'round_robin' && k === (r.cursor + 1) % r.saleIds.length;
                      return (
                        <Tooltip key={id} title={`${on ? 'Đang trong ca' : 'Ngoài ca'} · ${load.get(id) ?? 0} lead mở${next ? ' · lượt kế tiếp' : ''}`}>
                          <span className={cn('inline-flex items-center gap-1.5 rounded-full border px-2.5 py-1 text-[13px]', next ? 'border-primary-400 bg-primary-50' : 'border-line bg-canvas')}>
                            <span className={cn('size-2 rounded-full', on ? 'bg-success-600' : 'bg-ink-muted/40')} aria-hidden />
                            {saleName(id)}
                            <span className="tabular text-xs text-ink-muted">{load.get(id) ?? 0}</span>
                          </span>
                        </Tooltip>
                      );
                    })}
                  </div>
                  <p className="mt-3 flex flex-wrap items-center gap-x-4 gap-y-1 text-[13px] text-ink-secondary">
                    <span className="inline-flex items-center gap-1">
                      <Timer size={14} aria-hidden /> Liên hệ trong <b className="tabular text-ink">{r.slaMinutes} phút</b>
                    </span>
                    <span className="inline-flex items-center gap-1">
                      <BellRing size={14} aria-hidden /> Quá hạn báo: {r.escalateTo.join(', ') || '—'}
                    </span>
                    {r.reassignOnBreach && <span className="text-warning-700">Quá {r.slaMinutes * 2} phút → tự chuyển Sale khác</span>}
                  </p>
                </div>
                {editable && (
                  <div className="flex shrink-0 flex-col items-end gap-2">
                    <Switch size="small" checked={r.active} onChange={(v) => updateRule(r.id, { active: v })} aria-label={r.active ? 'Tắt quy tắc' : 'Bật quy tắc'} />
                    <div className="flex gap-1">
                      <Button icon={ArrowUp} iconOnly size="sm" variant="ghost" tone="neutral" aria-label="Lên trên" disabled={i === 0} onClick={() => moveRule(r.id, -1)} />
                      <Button icon={ArrowDown} iconOnly size="sm" variant="ghost" tone="neutral" aria-label="Xuống dưới" disabled={i === rules.length - 1} onClick={() => moveRule(r.id, 1)} />
                    </div>
                    <div className="flex gap-1">
                      <Button action="edit" iconOnly size="sm" onClick={() => setEditing(r)} />
                      <Button action="delete" iconOnly size="sm" onClick={() => remove(r)} />
                    </div>
                  </div>
                )}
              </div>
            </article>
          ))}
          <p className="rounded-xl border border-dashed border-line px-4 py-3 text-[13px] text-ink-muted">
            Không quy tắc nào khớp → lead để <b>chưa giao</b>, quản lý giao tay ở trang Lead. SLA mặc định 15 phút.
          </p>
        </section>

        <aside className="flex flex-col gap-4">
          {/* ===== Thử chia ===== */}
          <section className="rounded-xl border border-line bg-surface p-4">
            <p className="mb-3 flex items-center gap-1.5 text-sm font-semibold text-ink">
              <FlaskConical size={16} className="text-primary-600" aria-hidden /> Thử chia một lead
            </p>
            <div className="flex flex-col gap-2">
              <Select size="small" value={sim.source} onChange={(v) => v && setSim({ ...sim, source: v as LeadSource })} options={LEAD_SOURCE_OPTIONS} allowClear={false} aria-label="Nguồn" />
              <Select size="small" value={sim.branchId} onChange={(v) => v && setSim({ ...sim, branchId: v as string })} options={branchOptions()} allowClear={false} aria-label="Chi nhánh" />
              <Select size="small" value={sim.interest} onChange={(v) => setSim({ ...sim, interest: v as string | undefined })} options={COURSE_OPTIONS} placeholder="Khóa quan tâm (tùy chọn)" />
            </div>
            <div className={cn('mt-3 rounded-lg px-3 py-2 text-[13px]', simResult.saleId ? 'bg-success-50 text-success-700' : 'bg-warning-50 text-warning-700')}>
              <p className="font-semibold">{simResult.saleId ? `→ ${saleName(simResult.saleId)}` : '→ Chưa giao được'}</p>
              <p className="mt-0.5">{simResult.reason}</p>
              {simResult.rule && <p className="mt-0.5">SLA: {simResult.rule.slaMinutes} phút</p>}
            </div>
            <p className="mt-2 text-[11px] text-ink-muted">Chỉ mô phỏng theo ca làm việc lúc này, không tạo lead.</p>
          </section>

          {/* ===== Quá SLA ===== */}
          <section className="rounded-xl border border-line bg-surface p-4">
            <p className="mb-3 flex items-center gap-1.5 text-sm font-semibold text-ink">
              <AlarmClock size={16} className="text-danger-600" aria-hidden /> Đang quá SLA ({overdue.length})
            </p>
            <ul className="flex flex-col gap-2">
              {overdue.slice(0, 6).map(({ lead, sla, late }) => (
                <li key={lead.id} className="rounded-lg bg-danger-50/60 p-2.5 text-[13px]">
                  <div className="flex items-start justify-between gap-2">
                    <div className="min-w-0">
                      <p className="truncate font-medium text-ink">{lead.fullName}</p>
                      <p className="text-xs text-ink-muted">
                        {saleName(lead.assigneeId) ?? 'Chưa giao'} · SLA {sla}′ · trễ <b className="text-danger-700">{late >= 60 ? `${Math.floor(late / 60)} giờ` : `${late} phút`}</b>
                      </p>
                      {(lead.slaEscalatedAt || lead.slaReassignedAt) && (
                        <p className="mt-0.5 text-[11px] text-ink-muted">
                          {[lead.slaEscalatedAt && `Đã báo ${fromNow(lead.slaEscalatedAt)}`, lead.slaReassignedAt && `đã tự chuyển Sale ${fromNow(lead.slaReassignedAt)}`].filter(Boolean).join(' · ')}
                        </p>
                      )}
                    </div>
                  </div>
                  {editable && (
                    <Select
                      size="small"
                      className="mt-2 !w-full"
                      placeholder="Chuyển cho Sale khác..."
                      value={null as unknown as string}
                      options={allSales.filter((s) => s.branchId === lead.branchId && s.id !== lead.assigneeId).map((s) => ({
                        value: s.id,
                        label: `${s.fullName}${isOnShift(s.id, shifts) ? ' · trong ca' : ''}`,
                      }))}
                      onChange={(v) => v && reassign(lead, v as string)}
                    />
                  )}
                </li>
              ))}
              {!overdue.length && <li className="text-[13px] text-ink-muted">Không có lead nào quá hạn liên hệ 🎉</li>}
            </ul>
          </section>
        </aside>
      </div>

      <h2 className="mb-2 flex items-center gap-1.5 text-sm font-semibold text-ink">
        <Users size={16} aria-hidden /> Nhật ký chia lead
      </h2>
      <DataTable<AssignmentLog>
        storageKey="crm-assign-logs"
        columns={logColumns}
        data={logs.filter((l) => {
          const lead = leads.find((x) => x.id === l.leadId);
          return !lead || inBranch(lead.branchId);
        })}
        rowKey="id"
        unitLabel="lượt chia"
        empty={{ icon: Shuffle, title: 'Chưa có lượt chia nào', description: 'Lead mới thêm mà không chọn Sale sẽ được ghi vào đây.' }}
      />

      {/* ===== Thêm / sửa quy tắc ===== */}
      <ModalForm<RuleInput>
        key={editing === 'new' ? 'new' : (editing?.id ?? 'none')}
        open={Boolean(editing)}
        onClose={() => setEditing(null)}
        title={editing === 'new' ? 'Thêm quy tắc chia lead' : 'Sửa quy tắc chia lead'}
        description="Bước 1: chọn lead NÀO được áp (nguồn, chi nhánh, khóa — bỏ trống = tất cả). Bước 2: chọn AI nhận và chia thế nào. Bước 3: đặt thời hạn gọi. Quy tắc mới nằm cuối danh sách — dùng nút ↑ để đưa lên nếu cần ưu tiên."
        columns={2}
        width={720}
        submitAction="save"
        successMessage="Đã lưu quy tắc"
        initialValues={
          editing && editing !== 'new'
            ? editing
            : { active: true, sources: [], branchIds: branchId === ALL_BRANCHES ? [] : [branchId], interests: [], method: 'round_robin', saleIds: [], onlyOnShift: true, slaMinutes: 15, escalateTo: ['Trưởng nhóm Sale'], reassignOnBreach: false }
        }
        onSubmit={(v) => {
          const input = { ...v, sources: v.sources ?? [], branchIds: v.branchIds ?? [], interests: v.interests ?? [], escalateTo: v.escalateTo ?? [] };
          if (editing === 'new') addRule(input);
          else if (editing) updateRule(editing.id, input);
        }}
      >
        <Form.Item name="name" label="Tên quy tắc" rules={[{ required: true, message: 'Nhập tên quy tắc' }]} className="md:col-span-2">
          <TextInput placeholder="Vd: Cầu Giấy — xoay vòng trong ca" />
        </Form.Item>
        <Form.Item name="sources" label="Nguồn lead">
          <Select mode="multiple" placeholder="Mọi nguồn" options={LEAD_SOURCE_OPTIONS} />
        </Form.Item>
        <Form.Item name="branchIds" label="Chi nhánh">
          <Select mode="multiple" placeholder="Mọi chi nhánh" options={branchOptions()} />
        </Form.Item>
        <Form.Item name="interests" label="Khóa quan tâm">
          <Select mode="multiple" placeholder="Mọi khóa" options={COURSE_OPTIONS} />
        </Form.Item>
        <Form.Item name="method" label="Cách chia" rules={[{ required: true }]} extra="Không chắc thì chọn Xoay vòng — chia đều cho mọi người.">
          <Select options={METHOD_OPTIONS} allowClear={false} showSearch={false} />
        </Form.Item>
        <Form.Item name="saleIds" label="Nhóm Sale nhận lead" tooltip="Thứ tự chọn = thứ tự xoay vòng / ưu tiên" rules={[{ required: true, message: 'Chọn ít nhất 1 Sale' }]} className="md:col-span-2">
          <Select mode="multiple" placeholder="Chọn Sale" options={allSales.map((s) => ({ value: s.id, label: `${s.fullName} · ${branchName(s.branchId)}${s.status === 'probation' ? ' (thử việc)' : ''}` }))} />
        </Form.Item>
        <Form.Item name="slaMinutes" label="Thời hạn liên hệ lần đầu (phút)" rules={[{ required: true, message: 'Nhập SLA' }]}>
          <NumberInput min={5} max={1440} step={5} />
        </Form.Item>
        <Form.Item name="escalateTo" label="Quá hạn báo cho">
          <Select mode="multiple" placeholder="Chọn người nhận cảnh báo" options={ESCALATE_OPTIONS} />
        </Form.Item>
        <Form.Item name="onlyOnShift" valuePropName="checked" className="!mb-2 md:col-span-2">
          <Checkbox>Chỉ chia cho Sale đang trong ca làm việc (theo Xếp ca) — hết người trong ca thì chia cho cả nhóm</Checkbox>
        </Form.Item>
        <Form.Item name="reassignOnBreach" valuePropName="checked" className="!mb-2 md:col-span-2">
          <Checkbox>Quá gấp đôi SLA mà vẫn chưa liên hệ → tự chuyển lead cho Sale khác trong nhóm</Checkbox>
        </Form.Item>
        <Form.Item name="active" valuePropName="checked" className="md:col-span-2">
          <Checkbox>Bật quy tắc</Checkbox>
        </Form.Item>
      </ModalForm>
      {confirmDialog}
    </>
  );
}
