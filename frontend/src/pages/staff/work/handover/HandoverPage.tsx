import { useMemo, useState } from 'react';
import { Form } from 'antd';
import dayjs from 'dayjs';
import { ArrowRight, Banknote, CheckCircle2, CircleDashed, ClipboardList, Minus, Plus, Target, TriangleAlert, Users } from 'lucide-react';
import { Button, EmptyState, ModalForm, NumberInput, PageHeader, Select, StatCard, StatusTag, TextArea, TextInput, UserCell, useNotify } from '@/components/common';
import { useCrmStore } from '@/features/crm/crmStore';
import { SHIFT_KEYS, WORK_SHIFTS } from '@/features/hr/constants';
import { useHrStore } from '@/features/hr/hrStore';
import { meOf, nameOf } from '@/features/hr/hrUtils';
import type { ShiftKey } from '@/features/hr/types';
import type { Handover, HandoverStatus } from '@/features/work/types';
import { useWorkStore } from '@/features/work/workStore';
import { useStudentStore } from '@/features/student/studentStore';
import { useAuth } from '@/hooks/useAuth';
import { cn } from '@/lib/cn';
import { fmtDate, fmtDateTime } from '@/lib/date';
import { formatCurrency } from '@/lib/format';
import { getBranches } from '@/features/system/useBranches';
import { ALL_BRANCHES } from '@/types/auth';

const STATUS: Record<HandoverStatus, { label: string; tone: 'warning' | 'success' | 'danger'; icon: typeof CheckCircle2 }> = {
  pending: { label: 'Chờ nhận', tone: 'warning', icon: CircleDashed },
  received: { label: 'Đã nhận đủ', tone: 'success', icon: CheckCircle2 },
  disputed: { label: 'Lệch — đã báo kế toán', tone: 'danger', icon: TriangleAlert },
};
/** Quỹ tiền lẻ để sẵn đầu mỗi ca */
const FLOAT = 1_000_000;

interface NewHandover {
  branchId: string;
  fromShift: ShiftKey;
  fromId: string;
  toId: string;
  expected: number;
  counted: number;
  cashNote?: string;
  leads?: { leadId: string; note: string }[];
  pending?: string[];
  issues?: string;
}

/** Bàn giao ca — DỮ LIỆU GIẢ. */
export default function HandoverPage() {
  const { user, branchId } = useAuth();
  const notify = useNotify();
  const handovers = useWorkStore((s) => s.handovers);
  const shifts = useWorkStore((s) => s.shifts);
  const addHandover = useWorkStore((s) => s.addHandover);
  const updateHandover = useWorkStore((s) => s.updateHandover);
  const employees = useHrStore((s) => s.employees);
  const leads = useCrmStore((s) => s.leads);
  const invoices = useStudentStore((s) => s.invoices);
  const me = meOf(employees, user);

  const [status, setStatus] = useState<HandoverStatus | 'all'>('all');
  const [creating, setCreating] = useState(false);
  const [init, setInit] = useState<Partial<NewHandover>>();
  const [receiving, setReceiving] = useState<Handover | null>(null);
  const [form] = Form.useForm<NewHandover>();
  const fBranch = Form.useWatch('branchId', form);
  const fShift = Form.useWatch('fromShift', form);
  const fExpected = Form.useWatch('expected', form) ?? 0;
  const fCounted = Form.useWatch('counted', form) ?? 0;

  const today = dayjs().format('YYYY-MM-DD');
  const list = useMemo(
    () => handovers.filter((h) => (branchId === ALL_BRANCHES || h.branchId === branchId) && (status === 'all' || h.status === status)).sort((a, b) => b.createdAt.localeCompare(a.createdAt)),
    [handovers, branchId, status],
  );
  const all = handovers.filter((h) => branchId === ALL_BRANCHES || h.branchId === branchId);
  const myPending = all.filter((h) => h.status === 'pending' && h.toId === me?.id);

  // Ca của tôi hôm nay + người nhận ca kế tiếp (gợi ý)
  const myShift = me && shifts.filter((s) => s.employeeId === me.id && s.date === today).sort((a, b) => WORK_SHIFTS[b.shift].from.localeCompare(WORK_SHIFTS[a.shift].from))[0];

  /** Tiền mặt dự kiến: quỹ đầu ca + tiền mặt thu trong ca (theo hóa đơn) */
  const cashIn = (date: string, k: ShiftKey) => {
    const { from, to } = WORK_SHIFTS[k];
    return invoices.flatMap((i) => i.payments).filter((p) => p.method === 'cash' && dayjs(p.paidAt).format('YYYY-MM-DD') === date && dayjs(p.paidAt).format('HH:mm') >= from && dayjs(p.paidAt).format('HH:mm') < to).reduce((n, p) => n + p.amount, 0);
  };

  const startCreate = () => {
    const b = me?.branchId ?? (branchId === ALL_BRANCHES ? 'cg' : branchId);
    const k: ShiftKey = myShift?.shift ?? (dayjs().hour() < 12 ? 'morning' : dayjs().hour() < 17 ? 'afternoon' : 'evening');
    const idx = SHIFT_KEYS.indexOf(k);
    const next = shifts.find((s) => s.date === today && SHIFT_KEYS.indexOf(s.shift) > idx && s.employeeId !== me?.id && employees.find((e) => e.id === s.employeeId)?.branchId === b);
    const expected = FLOAT + cashIn(today, k);
    setInit({ branchId: b, fromShift: k, fromId: me?.id, toId: next?.employeeId, expected, counted: expected, leads: [], pending: [] });
    setCreating(true);
  };

  const branchStaff = employees.filter((e) => e.shiftBased && e.status !== 'quit' && e.branchId === fBranch).map((e) => ({ value: e.id, label: `${e.fullName} · ${e.position}` }));
  const branchLeads = leads.filter((l) => !l.hidden && l.branchId === fBranch && !['won', 'lost'].includes(l.status)).map((l) => ({ value: l.id, label: `${l.code} — ${l.fullName}` }));
  const leadName = (id: string) => {
    const l = leads.find((x) => x.id === id);
    return l ? `${l.fullName} (${l.phone})` : id;
  };

  return (
    <>
      <PageHeader
        title="Bàn giao ca"
        subtitle="Cuối ca: tiền mặt tại quầy, lead đang dở, việc tồn → ca sau xác nhận đã nhận"
        breadcrumb={[{ title: 'Công việc' }, { title: 'Bàn giao ca' }]}
        actions={
          <Button action="create" onClick={startCreate} collapseOnMobile>
            Tạo bàn giao
          </Button>
        }
      />

      {myShift && (
        <div className="mb-4 flex flex-wrap items-center justify-between gap-2 rounded-[var(--radius-card)] border border-primary-200 bg-primary-50 px-4 py-3 text-[13px] text-primary-800">
          <span>
            Hôm nay bạn làm <strong>{WORK_SHIFTS[myShift.shift].label}</strong> ({WORK_SHIFTS[myShift.shift].from}–{WORK_SHIFTS[myShift.shift].to}). Trước khi về, tạo bàn giao cho ca sau.
          </span>
          <Button size="sm" icon={ClipboardList} onClick={startCreate}>
            Bàn giao ca của tôi
          </Button>
        </div>
      )}
      {myPending.length > 0 && (
        <div className="mb-4 rounded-[var(--radius-card)] border border-warning-100 bg-warning-50 px-4 py-3 text-[13px] text-warning-700">
          Bạn có <strong>{myPending.length}</strong> bàn giao chờ nhận — đếm lại tiền và xác nhận trước khi bắt đầu ca.
        </div>
      )}

      <div className="mb-4 grid grid-cols-2 gap-3 lg:grid-cols-4">
        <StatCard label="Chờ nhận" value={all.filter((h) => h.status === 'pending').length} icon={CircleDashed} tone="warning" onClick={() => setStatus('pending')} />
        <StatCard label="Lệch tiền" value={all.filter((h) => h.status === 'disputed').length} icon={TriangleAlert} tone="danger" onClick={() => setStatus('disputed')} />
        <StatCard label="Bàn giao hôm nay" value={all.filter((h) => h.date === today).length} icon={ClipboardList} />
        <StatCard label="Lead chuyển ca hôm nay" value={all.filter((h) => h.date === today).reduce((n, h) => n + h.leads.length, 0)} icon={Target} tone="info" />
      </div>

      <div className="mb-3 flex flex-wrap gap-1.5">
        {(['all', 'pending', 'received', 'disputed'] as const).map((k) => (
          <button key={k} type="button" onClick={() => setStatus(k)} className={cn('h-8 rounded-full border px-3 text-[13px] font-medium', status === k ? 'border-primary-600 bg-primary-600 text-white' : 'border-line text-ink-secondary hover:bg-primary-50')}>
            {k === 'all' ? 'Tất cả' : STATUS[k].label}
          </button>
        ))}
      </div>

      {list.length === 0 ? (
        <EmptyState icon={ClipboardList} title="Chưa có bàn giao" />
      ) : (
        <ul className="flex flex-col gap-3">
          {list.map((h) => {
            const S = STATUS[h.status];
            const diff = h.cash.counted - h.cash.expected;
            const recvDiff = h.cash.receivedCount !== undefined ? h.cash.receivedCount - h.cash.counted : 0;
            const canReceive = h.status === 'pending' && (h.toId === me?.id || user?.roleName !== 'Sale');
            return (
              <li key={h.id} className={cn('rounded-[var(--radius-card)] border bg-surface p-4', h.status === 'pending' ? 'border-warning-100' : h.status === 'disputed' ? 'border-danger-100' : 'border-line')}>
                <div className="flex flex-wrap items-center justify-between gap-2">
                  <div className="flex flex-wrap items-center gap-2 text-[13px]">
                    <UserCell size="sm" name={nameOf(employees, h.fromId)} subtitle={`${WORK_SHIFTS[h.fromShift].label} · ${fmtDate(h.date)}`} />
                    <ArrowRight size={16} className="text-ink-muted" aria-hidden />
                    <UserCell size="sm" name={nameOf(employees, h.toId)} subtitle={`Ca sau · ${getBranches(false).find((b) => b.id === h.branchId)?.name}`} />
                  </div>
                  <div className="flex items-center gap-2">
                    <StatusTag tone={S.tone} icon={S.icon} size="sm">
                      {S.label}
                    </StatusTag>
                    {canReceive && (
                      <Button size="sm" icon={CheckCircle2} onClick={() => setReceiving(h)}>
                        Xác nhận nhận ca
                      </Button>
                    )}
                  </div>
                </div>

                <div className="mt-3 grid grid-cols-1 gap-3 text-[13px] md:grid-cols-3">
                  <section className="rounded-lg bg-page p-3">
                    <p className="mb-1 flex items-center gap-1.5 font-medium text-ink-secondary">
                      <Banknote size={14} aria-hidden /> Tiền mặt tại quầy
                    </p>
                    <p className="tabular">
                      Hệ thống: <strong>{formatCurrency(h.cash.expected)}</strong>
                    </p>
                    <p className="tabular">
                      Bàn giao đếm: <strong>{formatCurrency(h.cash.counted)}</strong>{' '}
                      {diff !== 0 && <span className="text-danger-600">({diff > 0 ? '+' : ''}{formatCurrency(diff)})</span>}
                    </p>
                    {h.cash.receivedCount !== undefined && (
                      <p className="tabular">
                        Người nhận đếm: <strong>{formatCurrency(h.cash.receivedCount)}</strong> {recvDiff !== 0 && <span className="text-danger-600">({formatCurrency(recvDiff)})</span>}
                      </p>
                    )}
                    {h.cash.note && <p className="mt-1 text-xs text-warning-700">{h.cash.note}</p>}
                  </section>
                  <section className="rounded-lg bg-page p-3">
                    <p className="mb-1 flex items-center gap-1.5 font-medium text-ink-secondary">
                      <Users size={14} aria-hidden /> Lead đang dở ({h.leads.length})
                    </p>
                    {h.leads.length ? (
                      <ul className="flex flex-col gap-1">
                        {h.leads.map((l) => (
                          <li key={l.leadId}>
                            <span className="font-medium text-ink">{leadName(l.leadId)}</span>
                            <span className="text-ink-muted"> — {l.note}</span>
                          </li>
                        ))}
                      </ul>
                    ) : (
                      <p className="text-ink-muted">Không có</p>
                    )}
                  </section>
                  <section className="rounded-lg bg-page p-3">
                    <p className="mb-1 flex items-center gap-1.5 font-medium text-ink-secondary">
                      <ClipboardList size={14} aria-hidden /> Việc tồn
                    </p>
                    {h.pending.length ? (
                      <ul className="flex flex-col gap-1">
                        {h.pending.map((p, i) => (
                          <li key={i} className={cn(p.done && 'text-ink-muted line-through')}>
                            <label className="inline-flex items-start gap-1.5">
                              <input
                                type="checkbox"
                                checked={Boolean(p.done)}
                                onChange={(e) => updateHandover(h.id, { pending: h.pending.map((x, k) => (k === i ? { ...x, done: e.target.checked } : x)) })}
                                className="mt-0.5"
                              />
                              {p.text}
                            </label>
                          </li>
                        ))}
                      </ul>
                    ) : (
                      <p className="text-ink-muted">Không có</p>
                    )}
                    {h.issues && <p className="mt-1 text-xs text-danger-600">Sự cố: {h.issues}</p>}
                  </section>
                </div>
                <p className="mt-2 text-xs text-ink-muted">
                  Tạo {fmtDateTime(h.createdAt)}
                  {h.receivedAt && ` · Nhận ${fmtDateTime(h.receivedAt)}`}
                  {h.receiverNote && ` — ${h.receiverNote}`}
                </p>
              </li>
            );
          })}
        </ul>
      )}

      {/* Tạo bàn giao */}
      <ModalForm<NewHandover>
        key={init ? JSON.stringify(init) : 'ho'}
        form={form}
        initialValues={init}
        open={creating}
        onClose={() => setCreating(false)}
        width={720}
        columns={2}
        title="Bàn giao ca"
        description={`Tiền mặt hệ thống = quỹ đầu ca ${formatCurrency(FLOAT)} + tiền mặt thu trong ca (theo hóa đơn)`}
        submitAction="send"
        submitText="Gửi bàn giao"
        successMessage="Đã gửi bàn giao — người nhận ca được thông báo"
        onSubmit={(v) => {
          addHandover({
            branchId: v.branchId,
            date: today,
            fromShift: v.fromShift,
            fromId: v.fromId,
            toId: v.toId,
            cash: { expected: v.expected, counted: v.counted, note: v.cashNote?.trim() || undefined },
            leads: (v.leads ?? []).filter((l) => l?.leadId),
            pending: (v.pending ?? []).filter((p) => p?.trim()).map((text) => ({ text: text.trim() })),
            issues: v.issues?.trim() || undefined,
          });
        }}
      >
        <Form.Item name="branchId" label="Chi nhánh" rules={[{ required: true }]}>
          <Select options={getBranches(false).map((b) => ({ value: b.id, label: b.name }))} showSearch={false} allowClear={false} />
        </Form.Item>
        <Form.Item name="fromShift" label="Ca bàn giao" rules={[{ required: true }]}>
          <Select options={SHIFT_KEYS.map((k) => ({ value: k, label: `${WORK_SHIFTS[k].label} ${WORK_SHIFTS[k].from}–${WORK_SHIFTS[k].to}` }))} showSearch={false} allowClear={false} onChange={(k) => form.setFieldsValue({ expected: FLOAT + cashIn(today, k as ShiftKey) })} />
        </Form.Item>
        <Form.Item name="fromId" label="Người bàn giao" rules={[{ required: true, message: 'Chọn người bàn giao' }]}>
          <Select options={branchStaff} />
        </Form.Item>
        <Form.Item name="toId" label="Người nhận ca" rules={[{ required: true, message: 'Chọn người nhận' }, ({ getFieldValue }) => ({ validator: (_, v) => (v && v === getFieldValue('fromId') ? Promise.reject(new Error('Phải khác người bàn giao')) : Promise.resolve()) })]}>
          <Select options={branchStaff} />
        </Form.Item>
        <Form.Item name="expected" label={`Tiền mặt theo hệ thống${fShift ? ` (${WORK_SHIFTS[fShift as ShiftKey].label.toLowerCase()})` : ''}`}>
          <NumberInput currency min={0} />
        </Form.Item>
        <Form.Item
          name="counted"
          label="Tiền đếm thực tế"
          rules={[{ required: true, message: 'Nhập số tiền đếm được' }]}
          extra={fCounted !== fExpected ? <span className="text-danger-600">Lệch {formatCurrency(fCounted - fExpected)} — ghi rõ lý do</span> : 'Khớp hệ thống'}
        >
          <NumberInput currency min={0} />
        </Form.Item>
        {fCounted !== fExpected && (
          <Form.Item name="cashNote" label="Lý do lệch tiền" className="md:col-span-2" rules={[{ required: true, whitespace: true, message: 'Ghi lý do lệch' }]}>
            <TextInput placeholder="VD: trả lại tiền thừa nhầm cho khách" />
          </Form.Item>
        )}

        <div className="md:col-span-2">
          <p className="mb-1.5 text-[13px] font-medium text-ink">Lead đang dở</p>
          <Form.List name="leads">
            {(fields, { add, remove }) => (
              <div className="flex flex-col gap-2">
                {fields.map((f) => (
                  <div key={f.key} className="grid grid-cols-[1fr_1fr_auto] gap-2">
                    <Form.Item name={[f.name, 'leadId']} noStyle rules={[{ required: true, message: 'Chọn lead' }]}>
                      <Select options={branchLeads} placeholder="Chọn lead" optionFilterProp="label" />
                    </Form.Item>
                    <Form.Item name={[f.name, 'note']} noStyle rules={[{ required: true, message: 'Ghi chú' }]}>
                      <TextInput placeholder="Đang ở bước nào, hẹn gì" />
                    </Form.Item>
                    <Button icon={Minus} iconOnly tone="neutral" variant="ghost" aria-label="Bỏ" onClick={() => remove(f.name)} />
                  </div>
                ))}
                <Button icon={Plus} size="sm" variant="soft" onClick={() => add({ leadId: undefined, note: '' })} className="self-start">
                  Thêm lead
                </Button>
              </div>
            )}
          </Form.List>
        </div>
        <div className="md:col-span-2">
          <p className="mb-1.5 text-[13px] font-medium text-ink">Việc tồn cho ca sau</p>
          <Form.List name="pending">
            {(fields, { add, remove }) => (
              <div className="flex flex-col gap-2">
                {fields.map((f) => (
                  <div key={f.key} className="flex gap-2">
                    <Form.Item name={f.name} noStyle>
                      <TextInput placeholder="VD: khách quay lại lấy biên lai lúc 18h" />
                    </Form.Item>
                    <Button icon={Minus} iconOnly tone="neutral" variant="ghost" aria-label="Bỏ" onClick={() => remove(f.name)} />
                  </div>
                ))}
                <Button icon={Plus} size="sm" variant="soft" onClick={() => add('')} className="self-start">
                  Thêm việc
                </Button>
              </div>
            )}
          </Form.List>
        </div>
        <Form.Item name="issues" label="Sự cố trong ca" className="md:col-span-2">
          <TextArea rows={2} placeholder="Máy in hỏng, khách phàn nàn..." />
        </Form.Item>
      </ModalForm>

      {/* Nhận ca */}
      {receiving && (
        <ModalForm<{ count: number; note?: string }>
          open
          onClose={() => setReceiving(null)}
          title="Xác nhận nhận ca"
          description={`${nameOf(employees, receiving.fromId)} bàn giao ${formatCurrency(receiving.cash.counted)} tiền mặt`}
          submitAction="save"
          submitText="Xác nhận"
          successMessage="Đã nhận ca"
          initialValues={{ count: receiving.cash.counted }}
          onSubmit={(v) => {
            const off = v.count !== receiving.cash.counted || receiving.cash.counted !== receiving.cash.expected;
            updateHandover(receiving.id, { status: off ? 'disputed' : 'received', receivedAt: new Date().toISOString(), receiverNote: v.note?.trim() || undefined, cash: { ...receiving.cash, receivedCount: v.count } });
            if (off) notify.warning({ title: 'Đã ghi nhận lệch tiền', description: 'Kế toán sẽ kiểm tra với 2 ca.' });
          }}
        >
          <Form.Item name="count" label="Tiền mặt tôi đếm lại" rules={[{ required: true }]}>
            <NumberInput currency min={0} />
          </Form.Item>
          <Form.Item name="note" label="Ghi chú">
            <TextInput placeholder="VD: nhận đủ, đã đọc lead đang dở" />
          </Form.Item>
        </ModalForm>
      )}
    </>
  );
}
