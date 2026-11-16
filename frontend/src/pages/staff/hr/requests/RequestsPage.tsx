import { useState } from 'react';
import { Drawer, Form, Steps, Switch } from 'antd';
import dayjs, { type Dayjs } from 'dayjs';
import { CalendarOff, CheckCircle2, CircleDashed, HandCoins, Inbox, Send, XCircle } from 'lucide-react';
import { Button, DataTable, DateInput, ModalForm, NumberInput, PageHeader, Select, StatCard, StatusTag, TextArea, TextInput, UserCell, useConfirm, useNotify, type DataColumn } from '@/components/common';
import { REQUEST_STATUS, REQUEST_TYPE_OPTIONS, REQUEST_TYPES, WORK_SHIFTS } from '@/features/hr/constants';
import { useHrStore } from '@/features/hr/hrStore';
import { buildSteps, meOf, nameOf, workDays } from '@/features/hr/hrUtils';
import type { HrRequest, RequestType } from '@/features/hr/types';
import { useWorkStore } from '@/features/work/workStore';
import { useAuth } from '@/hooks/useAuth';
import { useBreakpoint } from '@/hooks/useBreakpoint';
import { cn } from '@/lib/cn';
import { fmtDate, fmtDateTime } from '@/lib/date';
import { formatCurrency } from '@/lib/format';
import { ALL_BRANCHES } from '@/types/auth';

type Tab = 'todo' | 'mine' | 'all';

interface Values {
  employeeId: string;
  type: RequestType;
  range?: [Dayjs, Dayjs];
  date?: Dayjs;
  paidLeave?: boolean;
  myShiftId?: string;
  withEmployeeId?: string;
  theirShiftId?: string;
  title?: string;
  amount?: number;
  reason: string;
}

/** Đơn từ & phê duyệt nhiều cấp. DỮ LIỆU GIẢ. */
export default function RequestsPage() {
  const { user, branchId, can } = useAuth();
  const { isMobile } = useBreakpoint();
  const notify = useNotify();
  const { confirm, confirmDialog } = useConfirm();
  const employees = useHrStore((s) => s.employees);
  const requests = useHrStore((s) => s.requests);
  const addRequest = useHrStore((s) => s.addRequest);
  const decide = useHrStore((s) => s.decide);
  const cancelRequest = useHrStore((s) => s.cancelRequest);
  const shifts = useWorkStore((s) => s.shifts);
  const me = meOf(employees, user);
  const isAdmin = user?.roleName === 'Super Admin' || user?.roleName === 'Admin';
  const manager = can('hr.employees');

  const [tab, setTab] = useState<Tab>('todo');
  const [type, setType] = useState<string>();
  const [openId, setOpenId] = useState<string | null>(null);
  const [creating, setCreating] = useState(false);
  const [form] = Form.useForm<Values>();
  const fType = Form.useWatch('type', form);
  const fEmp = Form.useWatch('employeeId', form);
  const fRange = Form.useWatch('range', form);
  const fWith = Form.useWatch('withEmployeeId', form);
  const fAmount = Form.useWatch('amount', form) ?? 0;

  const today = dayjs().format('YYYY-MM-DD');
  const canDecide = (r: HrRequest) => {
    if (r.status !== 'pending') return false;
    const st = r.steps.find((s) => !s.decision);
    return Boolean(st) && (isAdmin || st!.approver === user?.fullName);
  };

  const scoped = requests.filter((r) => branchId === ALL_BRANCHES || employees.find((e) => e.id === r.employeeId)?.branchId === branchId);
  const lists: Record<Tab, HrRequest[]> = {
    todo: scoped.filter(canDecide),
    mine: requests.filter((r) => r.employeeId === me?.id),
    all: scoped,
  };
  const data = lists[tab].filter((r) => !type || r.type === type).sort((a, b) => b.createdAt.localeCompare(a.createdAt));

  const weekEnd = dayjs().add(6, 'day').format('YYYY-MM-DD');
  const stats = {
    todo: lists.todo.length,
    leaveWeek: scoped.filter((r) => r.type === 'leave' && r.status === 'approved' && r.from <= weekEnd && r.to >= today).length,
    money: scoped.filter((r) => r.status === 'pending' && r.amount).reduce((n, r) => n + (r.amount ?? 0), 0),
  };

  const open = requests.find((r) => r.id === openId);
  const act = async (r: HrRequest, decision: 'approved' | 'rejected') => {
    const res = await confirm({
      title: decision === 'approved' ? `Duyệt ${REQUEST_TYPES[r.type].label.toLowerCase()} của ${nameOf(employees, r.employeeId)}?` : 'Từ chối đơn?',
      description: decision === 'approved' && r.steps.filter((s) => !s.decision).length > 1 ? 'Đơn sẽ chuyển sang cấp duyệt tiếp theo.' : decision === 'approved' && r.type === 'shift_swap' ? 'Lịch ca của 2 người sẽ được đổi ngay.' : undefined,
      tone: decision === 'approved' ? 'primary' : 'danger',
      confirmAction: decision === 'approved' ? 'approve' : 'reject',
      confirmText: decision === 'approved' ? 'Duyệt' : 'Từ chối',
      ...(decision === 'rejected' ? { requireReason: { label: 'Lý do từ chối', placeholder: 'VD: ngày cao điểm, thiếu người trực' } } : {}),
    });
    if (!res) return;
    decide(r.id, user?.fullName ?? 'Quản lý', decision, typeof res === 'object' ? res.reason : undefined);
    notify.toast.success(decision === 'approved' ? 'Đã duyệt' : 'Đã từ chối');
  };

  const columns: DataColumn<HrRequest>[] = [
    {
      key: 'type',
      title: 'Đơn',
      width: 220,
      mobile: 'title',
      render: (_, r) => {
        const T = REQUEST_TYPES[r.type];
        return (
          <div className="flex items-center gap-2.5">
            <span className="grid size-8 shrink-0 place-items-center rounded-lg bg-primary-50 text-primary-600">
              <T.icon size={16} aria-hidden />
            </span>
            <div className="min-w-0 leading-5">
              <p className="font-medium text-ink">{T.label}</p>
              <p className="font-mono text-xs text-ink-muted">{r.code}</p>
            </div>
          </div>
        );
      },
    },
    { key: 'emp', title: 'Người gửi', width: 190, mobile: 'subtitle', render: (_, r) => <UserCell size="sm" name={nameOf(employees, r.employeeId)} subtitle={employees.find((e) => e.id === r.employeeId)?.position} /> },
    {
      key: 'when',
      title: 'Thời gian',
      width: 160,
      render: (_, r) => (
        <div className="leading-5 text-[13px]">
          <p>{r.from === r.to ? fmtDate(r.from) : `${fmtDate(r.from)} → ${fmtDate(r.to)}`}</p>
          {r.days && <p className="text-xs text-ink-muted">{r.days} ngày · {r.paidLeave ? 'có lương' : 'không lương'}</p>}
        </div>
      ),
    },
    { key: 'reason', title: 'Nội dung', width: 260, render: (_, r) => <div className="leading-5 text-[13px]"><p className="line-clamp-2 text-ink">{r.title ?? r.reason}</p>{r.amount && <p className="tabular font-medium text-ink">{formatCurrency(r.amount)}</p>}</div> },
    {
      key: 'steps',
      title: 'Cấp duyệt',
      width: 170,
      render: (_, r) => (
        <span className="flex items-center gap-1">
          {r.steps.map((s, i) => (
            <span key={i} title={`${s.role}: ${s.approver}${s.decision ? ` — ${s.decision === 'approved' ? 'đã duyệt' : 'từ chối'}` : ''}`} className={cn('grid size-6 place-items-center rounded-full text-[10px] font-bold', s.decision === 'approved' ? 'bg-success-600 text-white' : s.decision === 'rejected' ? 'bg-danger-600 text-white' : 'border border-dashed border-ink-muted text-ink-muted')}>
              {i + 1}
            </span>
          ))}
        </span>
      ),
    },
    { key: 'status', title: 'Trạng thái', width: 130, mobile: 'aside', render: (_, r) => <StatusTag tone={REQUEST_STATUS[r.status].tone} icon={REQUEST_STATUS[r.status].icon} size="sm">{REQUEST_STATUS[r.status].label}</StatusTag> },
  ];

  // ---- Form tạo đơn
  const emp = employees.find((e) => e.id === fEmp);
  const myUpcoming = shifts.filter((s) => s.employeeId === fEmp && s.date >= today).sort((a, b) => a.date.localeCompare(b.date)).slice(0, 14);
  const colleagues = employees.filter((e) => e.shiftBased && e.id !== fEmp && e.branchId === emp?.branchId && e.status !== 'quit');
  const theirUpcoming = shifts.filter((s) => s.employeeId === fWith && s.date >= today).sort((a, b) => a.date.localeCompare(b.date)).slice(0, 14);
  const shiftLabel = (s: { date: string; shift: keyof typeof WORK_SHIFTS }) => `${dayjs(s.date).format('dd DD/MM')} · ${WORK_SHIFTS[s.shift].label} ${WORK_SHIFTS[s.shift].from}–${WORK_SHIFTS[s.shift].to}`;
  const leaveDays = fRange ? workDays(fRange[0], fRange[1]) : 0;

  return (
    <>
      <PageHeader
        title="Đơn từ & phê duyệt"
        subtitle="Nghỉ phép, đổi ca, công tác, đi muộn / về sớm, đề xuất, tạm ứng — duyệt nhiều cấp, kết quả gửi thông báo cho người làm đơn"
        breadcrumb={[{ title: 'Nhân sự' }, { title: 'Đơn từ & phê duyệt' }]}
        actions={
          <Button icon={Send} onClick={() => setCreating(true)} collapseOnMobile>
            Tạo đơn
          </Button>
        }
      />

      <div className="mb-4 grid grid-cols-1 gap-3 sm:grid-cols-3">
        <StatCard label="Chờ tôi duyệt" value={stats.todo} icon={Inbox} tone={stats.todo ? 'warning' : 'success'} onClick={() => setTab('todo')} />
        <StatCard label="Nghỉ phép trong 7 ngày tới" value={stats.leaveWeek} icon={CalendarOff} tone="info" hint="Đã duyệt — lưu ý khi xếp ca / lịch dạy" />
        <StatCard label="Đề xuất / tạm ứng chờ duyệt" value={formatCurrency(stats.money)} icon={HandCoins} />
      </div>

      <div className="mb-3 flex flex-wrap gap-1.5">
        {(
          [
            ['todo', 'Chờ tôi duyệt'],
            ['mine', 'Đơn của tôi'],
            ['all', 'Tất cả'],
          ] as [Tab, string][]
        ).map(([k, l]) => (
          <button key={k} type="button" onClick={() => setTab(k)} className={cn('inline-flex h-9 items-center gap-2 rounded-full border px-3.5 text-sm font-medium', tab === k ? 'border-primary-600 bg-primary-600 text-white' : 'border-line bg-surface text-ink-secondary hover:bg-primary-50')}>
            {l}
            <span className={cn('tabular rounded-full px-1.5 text-xs', tab === k ? 'bg-white/20' : 'bg-disabled text-ink-muted')}>{lists[k].length}</span>
          </button>
        ))}
      </div>

      <DataTable<HrRequest>
        storageKey="hr-requests"
        columns={columns}
        data={data}
        rowKey="id"
        unitLabel="đơn"
        searchKeys={['code', 'reason', 'title']}
        onRowClick={(r) => setOpenId(r.id)}
        search={{ placeholder: 'Tìm theo mã đơn, nội dung...', activeFilterCount: type ? 1 : 0, filters: <Select placeholder="Loại đơn" value={type} onChange={setType} options={REQUEST_TYPE_OPTIONS} showSearch={false} className="lg:!w-44" />, onReset: () => setType(undefined) }}
        rowActions={(r) =>
          canDecide(r) ? (
            <>
              <Button icon={CheckCircle2} iconOnly size="sm" tone="success" variant="soft" tooltip="Duyệt" aria-label="Duyệt" onClick={() => act(r, 'approved')} />
              <Button icon={XCircle} iconOnly size="sm" tone="danger" variant="soft" tooltip="Từ chối" aria-label="Từ chối" onClick={() => act(r, 'rejected')} />
            </>
          ) : null
        }
        actionsWidth={110}
        empty={{ icon: Inbox, title: tab === 'todo' ? 'Không có đơn nào chờ bạn duyệt' : 'Chưa có đơn' }}
      />

      {/* Chi tiết đơn */}
      <Drawer open={Boolean(open)} onClose={() => setOpenId(null)} width={isMobile ? '100%' : 560} destroyOnHidden title={open && `${REQUEST_TYPES[open.type].label} · ${open.code}`}>
        {open && (
          <div className="flex flex-col gap-4 text-[13px]">
            <UserCell name={nameOf(employees, open.employeeId)} subtitle={`${employees.find((e) => e.id === open.employeeId)?.position} · gửi ${fmtDateTime(open.createdAt)}`} />
            <dl className="grid grid-cols-[120px_1fr] gap-y-1.5">
              <dt className="text-ink-muted">Thời gian</dt>
              <dd>{open.from === open.to ? fmtDate(open.from) : `${fmtDate(open.from)} → ${fmtDate(open.to)}`}{open.days ? ` (${open.days} ngày ${open.paidLeave ? 'có lương' : 'không lương'})` : ''}</dd>
              {open.swap && (
                <>
                  <dt className="text-ink-muted">Đổi ca</dt>
                  <dd>
                    {(() => {
                      const a = shifts.find((s) => s.id === open.swap!.myShiftId);
                      const b = shifts.find((s) => s.id === open.swap!.theirShiftId);
                      return `${a ? shiftLabel(a) : '—'} ↔ ${nameOf(employees, open.swap!.withEmployeeId)}: ${b ? shiftLabel(b) : '—'}`;
                    })()}
                  </dd>
                </>
              )}
              {open.title && (
                <>
                  <dt className="text-ink-muted">Đề xuất</dt>
                  <dd>{open.title}</dd>
                </>
              )}
              {open.amount && (
                <>
                  <dt className="text-ink-muted">Số tiền</dt>
                  <dd className="tabular font-semibold">{formatCurrency(open.amount)}</dd>
                </>
              )}
              <dt className="text-ink-muted">Lý do</dt>
              <dd>{open.reason}</dd>
            </dl>
            <Steps
              direction="vertical"
              size="small"
              current={open.steps.findIndex((s) => !s.decision)}
              status={open.status === 'rejected' ? 'error' : open.status === 'approved' ? 'finish' : 'process'}
              items={open.steps.map((s) => ({
                title: `${s.role} — ${s.approver}`,
                description: s.decision ? `${s.decision === 'approved' ? 'Đã duyệt' : 'Từ chối'} · ${fmtDateTime(s.at)}${s.comment ? ` — ${s.comment}` : ''}` : 'Đang chờ',
                icon: s.decision === 'approved' ? <CheckCircle2 size={18} className="text-success-600" /> : s.decision === 'rejected' ? <XCircle size={18} className="text-danger-600" /> : <CircleDashed size={18} className="text-ink-muted" />,
              }))}
            />
            <div className="flex justify-end gap-2 border-t border-line pt-3">
              {open.status === 'pending' && open.employeeId === me?.id && (
                <Button tone="neutral" variant="ghost" icon={XCircle} onClick={() => (cancelRequest(open.id), notify.toast.info('Đã hủy đơn'))}>
                  Hủy đơn
                </Button>
              )}
              {canDecide(open) && (
                <>
                  <Button icon={XCircle} tone="danger" variant="soft" onClick={() => act(open, 'rejected')}>
                    Từ chối
                  </Button>
                  <Button icon={CheckCircle2} tone="success" onClick={() => act(open, 'approved')}>
                    Duyệt
                  </Button>
                </>
              )}
            </div>
          </div>
        )}
      </Drawer>

      {/* Tạo đơn */}
      <ModalForm<Values>
        form={form}
        open={creating}
        onClose={() => setCreating(false)}
        width={620}
        title="Tạo đơn"
        submitAction="send"
        submitText="Gửi đơn"
        successMessage="Đã gửi đơn — người duyệt được thông báo"
        initialValues={{ employeeId: me?.id, type: 'leave', paidLeave: true }}
        onSubmit={(v) => {
          const e = employees.find((x) => x.id === v.employeeId)!;
          const from = (v.range?.[0] ?? v.date ?? dayjs()).format('YYYY-MM-DD');
          const to = (v.range?.[1] ?? v.date ?? dayjs()).format('YYYY-MM-DD');
          const days = v.type === 'leave' && v.range ? workDays(v.range[0], v.range[1]) : undefined;
          if (v.type === 'leave' && v.paidLeave && days && days > e.leaveBalance) throw new Error(`Chỉ còn ${e.leaveBalance} ngày phép — chọn nghỉ không lương cho phần vượt`);
          const swapDate = v.type === 'shift_swap' ? shifts.find((s) => s.id === v.myShiftId)?.date : undefined;
          addRequest({
            type: v.type,
            employeeId: v.employeeId,
            from: swapDate ?? from,
            to: swapDate ?? to,
            days,
            paidLeave: v.type === 'leave' ? v.paidLeave : undefined,
            swap: v.type === 'shift_swap' && v.myShiftId && v.withEmployeeId && v.theirShiftId ? { myShiftId: v.myShiftId, withEmployeeId: v.withEmployeeId, theirShiftId: v.theirShiftId } : undefined,
            title: v.title?.trim() || undefined,
            amount: v.amount,
            reason: v.reason.trim(),
            steps: buildSteps(e, employees, v.type, days, v.amount),
          });
          setTab('mine');
        }}
      >
        {(manager || isAdmin) && (
          <Form.Item name="employeeId" label="Người làm đơn" rules={[{ required: true }]} extra="Quản lý có thể tạo đơn thay cho nhân viên (VD: nhân viên gọi điện xin nghỉ ốm).">
            <Select options={employees.filter((e) => e.status !== 'quit').map((e) => ({ value: e.id, label: `${e.fullName} · ${e.position}` }))} />
          </Form.Item>
        )}
        <Form.Item name="type" label="Loại đơn" rules={[{ required: true }]} extra={fType && REQUEST_TYPES[fType as RequestType].hint}>
          <Select options={REQUEST_TYPE_OPTIONS} showSearch={false} allowClear={false} />
        </Form.Item>

        {(fType === 'leave' || fType === 'business_trip') && (
          <Form.Item
            name="range"
            label="Từ ngày — đến ngày"
            rules={[{ required: true, message: 'Chọn ngày' }]}
            extra={fType === 'leave' && fRange ? `${leaveDays} ngày làm việc · phép năm còn ${emp?.leaveBalance ?? 0} ngày${leaveDays > 3 ? ' · cần thêm Phó giám đốc duyệt' : ''}` : undefined}
          >
            <RangeInput />
          </Form.Item>
        )}
        {fType === 'leave' && (
          <Form.Item name="paidLeave" label="Trừ vào phép năm (có lương)" valuePropName="checked">
            <Switch />
          </Form.Item>
        )}
        {(fType === 'late_early' || fType === 'proposal' || fType === 'advance') && (
          <Form.Item name="date" label="Ngày" rules={[{ required: fType === 'late_early', message: 'Chọn ngày' }]}>
            <DateInput />
          </Form.Item>
        )}
        {fType === 'shift_swap' && (
          <>
            <Form.Item name="myShiftId" label="Ca của tôi muốn đổi" rules={[{ required: true, message: 'Chọn ca' }]}>
              <Select options={myUpcoming.map((s) => ({ value: s.id, label: shiftLabel(s) }))} placeholder={myUpcoming.length ? 'Chọn ca' : 'Không có ca sắp tới'} />
            </Form.Item>
            <Form.Item name="withEmployeeId" label="Đổi với đồng nghiệp" rules={[{ required: true, message: 'Chọn người' }]}>
              <Select options={colleagues.map((e) => ({ value: e.id, label: `${e.fullName} · ${e.position}` }))} onChange={() => form.setFieldValue('theirShiftId', undefined)} />
            </Form.Item>
            <Form.Item name="theirShiftId" label="Nhận ca của họ" rules={[{ required: true, message: 'Chọn ca' }]}>
              <Select options={theirUpcoming.map((s) => ({ value: s.id, label: shiftLabel(s) }))} disabled={!fWith} />
            </Form.Item>
          </>
        )}
        {fType === 'proposal' && (
          <Form.Item name="title" label="Đề xuất" rules={[{ required: true, whitespace: true, message: 'Nhập nội dung đề xuất' }]}>
            <TextInput placeholder="VD: Mua 2 bóng đèn máy chiếu" />
          </Form.Item>
        )}
        {(fType === 'proposal' || fType === 'advance') && (
          <Form.Item name="amount" label="Số tiền" rules={[{ required: fType === 'advance', message: 'Nhập số tiền' }]} extra={fType === 'proposal' && fAmount > 5_000_000 ? 'Trên 5 triệu → cần Kế toán trưởng & Giám đốc duyệt' : undefined}>
            <NumberInput currency min={0} />
          </Form.Item>
        )}
        <Form.Item name="reason" label="Lý do" rules={[{ required: true, whitespace: true, message: 'Nhập lý do' }]}>
          <TextArea rows={2} maxLength={500} />
        </Form.Item>
      </ModalForm>
      {confirmDialog}
    </>
  );
}

/** Chọn khoảng ngày (2 ô ngày) */
function RangeInput({ value, onChange }: { value?: [Dayjs, Dayjs]; onChange?: (v?: [Dayjs, Dayjs]) => void }) {
  return (
    <div className="grid grid-cols-2 gap-2">
      <DateInput value={value?.[0] ?? null} onChange={(d) => d && onChange?.([d, value?.[1] && !value[1].isBefore(d) ? value[1] : d])} />
      <DateInput value={value?.[1] ?? null} onChange={(d) => d && onChange?.([value?.[0] && !value[0].isAfter(d) ? value[0] : d, d])} />
    </div>
  );
}
