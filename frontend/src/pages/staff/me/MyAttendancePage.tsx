import { useEffect, useMemo, useState } from 'react';
import { Form, Steps, Switch, Tabs, Tooltip } from 'antd';
import dayjs, { type Dayjs } from 'dayjs';
import { CalendarOff, ChevronLeft, ChevronRight, Clock, LogIn, LogOut, Send, TimerOff, TriangleAlert, Undo2 } from 'lucide-react';
import { Button, DateInput, EmptyState, ModalForm, NumberInput, PageHeader, Select, StatCard, StatusTag, TextArea, TextInput, useConfirm, useNotify } from '@/components/common';
import { LATE_GRACE_MIN, REQUEST_STATUS, REQUEST_TYPES } from '@/features/hr/constants';
import { useHrStore } from '@/features/hr/hrStore';
import { buildSteps, DAY_STATE, dayState, nameOf, workDays, type DayState } from '@/features/hr/hrUtils';
import type { RequestType } from '@/features/hr/types';
import { useMe } from '@/features/me/useMe';
import { useWorkStore } from '@/features/work/workStore';
import { cn } from '@/lib/cn';
import { fmtDate, fromNow } from '@/lib/date';
import { formatCurrency } from '@/lib/format';

const CELL: Record<DayState, string> = {
  ok: 'bg-success-50 text-success-700',
  late: 'bg-warning-50 text-warning-700',
  early: 'bg-warning-50 text-warning-700',
  late_early: 'bg-danger-50 text-danger-700',
  no_out: 'bg-danger-50 text-danger-700',
  absent: 'bg-danger-50 text-danger-700',
  leave: 'bg-primary-50 text-primary-700',
  off: 'bg-canvas text-ink-muted',
  future: 'bg-surface text-ink-muted',
  working: 'bg-primary-100 text-primary-700',
};

/** Loại đơn cá nhân tự tạo ở đây (đổi ca làm ở trang Xếp ca) */
const MY_TYPES: RequestType[] = ['leave', 'late_early', 'business_trip', 'proposal', 'advance'];

interface Values {
  type: RequestType;
  range?: [Dayjs, Dayjs];
  date?: Dayjs;
  paidLeave?: boolean;
  title?: string;
  amount?: number;
  reason: string;
}

/**
 * Cá nhân › Chấm công của tôi: check-in / check-out, bảng công tháng của BẢN THÂN, tạo đơn (nghỉ phép, đi muộn / về sớm...).
 * Khác Nhân sự › Chấm công (phòng nhân sự theo dõi TOÀN BỘ nhân viên, sửa công).
 */
export default function MyAttendancePage() {
  const notify = useNotify();
  const { confirm, confirmDialog } = useConfirm();
  const me = useMe();
  const employees = useHrStore((s) => s.employees);
  const records = useHrStore((s) => s.records);
  const requests = useHrStore((s) => s.requests);
  const { punch, addRequest, cancelRequest } = useHrStore();
  const shifts = useWorkStore((s) => s.shifts);
  const [month, setMonth] = useState(() => dayjs().startOf('month'));
  const [creating, setCreating] = useState<Partial<Values> | null>(null);
  const [clock, setClock] = useState(() => dayjs());
  const [form] = Form.useForm<Values>();
  const fType = Form.useWatch('type', form);
  const fRange = Form.useWatch('range', form);
  useEffect(() => {
    const t = setInterval(() => setClock(dayjs()), 30_000);
    return () => clearInterval(t);
  }, []);

  const today = dayjs().format('YYYY-MM-DD');
  const cells = useMemo(() => {
    if (!me) return [];
    return Array.from({ length: month.daysInMonth() }, (_, i) => {
      const date = month.add(i, 'day').format('YYYY-MM-DD');
      const rec = records.find((r) => r.employeeId === me.id && r.date === date);
      return { date, rec, ...dayState(me, date, rec, shifts, requests) };
    });
  }, [me, month, records, shifts, requests]);

  if (!me) return <EmptyState icon={Clock} title="Tài khoản chưa gắn hồ sơ nhân viên" description="Liên hệ phòng nhân sự để liên kết tài khoản với hồ sơ." />;

  const count = (s: DayState[]) => cells.filter((c) => s.includes(c.state)).length;
  const lateMin = cells.reduce((n, c) => n + (c.state === 'late' || c.state === 'late_early' ? c.lateMin : 0), 0);
  const hours = cells.reduce((n, c) => n + c.worked, 0) / 60;
  const todayState = cells.find((c) => c.date === today) ?? dayState(me, today, records.find((r) => r.employeeId === me.id && r.date === today), shifts, requests);
  const todayRec = records.find((r) => r.employeeId === me.id && r.date === today);
  const mine = requests.filter((r) => r.employeeId === me.id).sort((a, b) => b.createdAt.localeCompare(a.createdAt));
  const problems = cells.filter((c) => ['late', 'early', 'late_early', 'no_out', 'absent'].includes(c.state));

  const doPunch = () => {
    const kind = punch(me.id);
    const t = dayjs().format('HH:mm');
    if (kind === 'in') {
      const late = todayState.exp ? dayjs(`2000-01-01T${t}`).diff(dayjs(`2000-01-01T${todayState.exp.from}`), 'minute') : 0;
      notify.toast[late > LATE_GRACE_MIN ? 'warning' : 'success'](late > LATE_GRACE_MIN ? `Check-in ${t} — muộn ${late} phút` : `Check-in ${t}`);
    } else notify.toast.success(`Check-out ${t}`);
  };

  // Lùi lịch đầu tháng theo thứ (T2 đầu tuần)
  const lead = (month.day() + 6) % 7;
  const leaveDays = fRange ? workDays(fRange[0], fRange[1]) : 0;

  return (
    <>
      <PageHeader
        title="Chấm công của tôi"
        subtitle={`${me.fullName} · ${me.position} · ${me.shiftBased ? 'làm theo ca (theo lịch Xếp ca)' : 'giờ hành chính'}. Đi muộn / về sớm quá ${LATE_GRACE_MIN} phút bị đánh dấu.`}
        breadcrumb={[{ title: 'Cá nhân' }, { title: 'Chấm công của tôi' }]}
        actions={
          <Button action="send" collapseOnMobile onClick={() => setCreating({ type: 'leave', paidLeave: true })}>
            Tạo đơn
          </Button>
        }
      />

      {/* Check-in / out */}
      <div className="mb-4 flex flex-wrap items-center justify-between gap-4 rounded-[var(--radius-card)] border border-primary-100 bg-gradient-to-r from-primary-50 to-surface p-4">
        <div>
          <p className="text-[13px] text-ink-muted first-letter:uppercase">{clock.format('dddd, DD/MM/YYYY')}</p>
          <p className="tabular text-4xl font-bold text-ink">{clock.format('HH:mm')}</p>
          <p className="text-[13px] text-ink-secondary">
            {todayState.state === 'leave'
              ? 'Hôm nay bạn nghỉ phép'
              : todayState.exp
                ? `Hôm nay: ${todayState.exp.label === 'HC' ? 'giờ hành chính' : `ca ${todayState.exp.label}`} ${todayState.exp.from}–${todayState.exp.to}`
                : 'Hôm nay không có ca'}
            {todayRec?.checkIn && ` · vào ${todayRec.checkIn}`}
            {todayRec?.checkOut && ` · ra ${todayRec.checkOut}`}
          </p>
        </div>
        <div className="flex items-center gap-3">
          {todayState.state !== 'future' && todayState.state !== 'off' && (
            <StatusTag tone={DAY_STATE[todayState.state].tone} icon={Clock} size="sm">
              {DAY_STATE[todayState.state].label}
            </StatusTag>
          )}
          <Button icon={todayRec?.checkIn && !todayRec.checkOut ? LogOut : LogIn} size="lg" tone={todayRec?.checkIn && !todayRec.checkOut ? 'warning' : 'primary'} onClick={doPunch} disabled={Boolean(todayRec?.checkOut)}>
            {todayRec?.checkOut ? 'Đã check-out' : todayRec?.checkIn ? 'Check-out' : 'Check-in'}
          </Button>
        </div>
      </div>

      <Tabs
        items={[
          {
            key: 'month',
            label: 'Bảng công tháng',
            children: (
              <>
                <div className="mb-3 flex items-center gap-2">
                  <Button icon={ChevronLeft} tone="neutral" variant="outline" iconOnly aria-label="Tháng trước" onClick={() => setMonth((m) => m.subtract(1, 'month'))} />
                  <span className="min-w-32 text-center font-semibold text-ink">Tháng {month.format('MM/YYYY')}</span>
                  <Button icon={ChevronRight} tone="neutral" variant="outline" iconOnly aria-label="Tháng sau" disabled={month.isSame(dayjs(), 'month')} onClick={() => setMonth((m) => m.add(1, 'month'))} />
                </div>
                <div className="mb-4 grid grid-cols-2 gap-3 lg:grid-cols-5">
                  <StatCard label="Ngày công" value={count(['ok', 'late', 'early', 'late_early', 'no_out', 'working'])} icon={Clock} tone="primary" hint={`${hours.toLocaleString('vi-VN', { maximumFractionDigits: 1 })} giờ`} />
                  <StatCard label="Đi muộn" value={count(['late', 'late_early'])} icon={TimerOff} tone="warning" hint={lateMin ? `tổng ${lateMin} phút` : undefined} />
                  <StatCard label="Về sớm / quên check-out" value={`${count(['early', 'late_early'])} / ${count(['no_out'])}`} icon={TriangleAlert} tone="warning" />
                  <StatCard label="Vắng không phép" value={count(['absent'])} icon={TriangleAlert} tone={count(['absent']) ? 'danger' : 'neutral'} />
                  <StatCard label="Phép năm còn lại" value={`${me.leaveBalance} ngày`} icon={CalendarOff} tone="success" hint={`Nghỉ phép tháng này: ${count(['leave'])}`} />
                </div>

                <div className="grid grid-cols-7 gap-1.5 text-center text-xs" role="grid" aria-label={`Bảng công tháng ${month.format('MM/YYYY')}`}>
                  {['T2', 'T3', 'T4', 'T5', 'T6', 'T7', 'CN'].map((d) => (
                    <span key={d} className="py-1 font-medium text-ink-muted">
                      {d}
                    </span>
                  ))}
                  {Array.from({ length: lead }, (_, i) => (
                    <span key={`e${i}`} />
                  ))}
                  {cells.map((c) => (
                    <Tooltip
                      key={c.date}
                      title={
                        c.state === 'future' || c.state === 'off'
                          ? c.exp
                            ? `Ca ${c.exp.from}–${c.exp.to}`
                            : 'Không có ca'
                          : `${DAY_STATE[c.state].label}${c.exp ? ` · ca ${c.exp.from}–${c.exp.to}` : ''}${c.rec?.checkIn ? ` · vào ${c.rec.checkIn}` : ''}${c.rec?.checkOut ? ` · ra ${c.rec.checkOut}` : ''}${c.lateMin > LATE_GRACE_MIN ? ` · muộn ${c.lateMin}′` : ''}${c.rec?.edited ? ` · sửa bởi ${c.rec.edited.by}: ${c.rec.edited.reason}` : ''}`
                      }
                    >
                      <div className={cn('flex min-h-16 flex-col items-start rounded-lg border border-line p-1.5 text-left', CELL[c.state], c.date === today && 'ring-2 ring-primary-500')}>
                        <span className="tabular text-[13px] font-semibold">{dayjs(c.date).date()}</span>
                        {c.rec?.checkIn && (
                          <span className="tabular hidden text-[11px] sm:block">
                            {c.rec.checkIn}–{c.rec.checkOut ?? '?'}
                          </span>
                        )}
                        {c.state !== 'future' && c.state !== 'off' && c.state !== 'ok' && <span className="mt-auto text-[10px] font-semibold">{DAY_STATE[c.state].label}</span>}
                      </div>
                    </Tooltip>
                  ))}
                </div>

                {problems.length > 0 && (
                  <section className="mt-4 rounded-xl border border-warning-100 bg-surface p-4">
                    <p className="mb-2 font-semibold text-ink">Ngày cần giải trình ({problems.length})</p>
                    <ul className="flex flex-col divide-y divide-line text-[13px]">
                      {problems.map((c) => (
                        <li key={c.date} className="flex flex-wrap items-center justify-between gap-2 py-2">
                          <span>
                            {dayjs(c.date).format('dd DD/MM')} · <span className="font-medium">{DAY_STATE[c.state].label}</span>
                            {c.lateMin > LATE_GRACE_MIN && ` ${c.lateMin} phút`}
                          </span>
                          <Button
                            icon={Send}
                            size="sm"
                            variant="ghost"
                            onClick={() => setCreating(c.state === 'absent' ? { type: 'leave', paidLeave: true, range: [dayjs(c.date), dayjs(c.date)] } : { type: 'late_early', date: dayjs(c.date) })}
                          >
                            {c.state === 'absent' ? 'Bổ sung đơn nghỉ' : 'Giải trình'}
                          </Button>
                        </li>
                      ))}
                    </ul>
                    <p className="mt-2 text-xs text-ink-muted">Quên check-out: tạo đơn “Đi muộn / về sớm” ghi giờ ra thực tế — quản lý duyệt, phòng nhân sự sửa công.</p>
                  </section>
                )}
              </>
            ),
          },
          {
            key: 'requests',
            label: `Đơn của tôi (${mine.filter((r) => r.status === 'pending').length} chờ duyệt)`,
            children: mine.length ? (
              <div className="flex flex-col gap-3">
                {mine.map((r) => {
                  const T = REQUEST_TYPES[r.type];
                  const S = REQUEST_STATUS[r.status];
                  return (
                    <article key={r.id} className="rounded-xl border border-line bg-surface p-4">
                      <div className="flex flex-wrap items-start justify-between gap-2">
                        <div className="flex items-start gap-2">
                          <T.icon size={18} className="mt-0.5 text-ink-muted" aria-hidden />
                          <div>
                            <p className="font-semibold text-ink">
                              {T.label}
                              {r.title && `: ${r.title}`}
                            </p>
                            <p className="text-xs text-ink-muted">
                              {r.code} · {r.from === r.to ? fmtDate(r.from) : `${fmtDate(r.from)} → ${fmtDate(r.to)}`}
                              {r.days ? ` · ${r.days} ngày${r.paidLeave ? ' (trừ phép)' : ' (không lương)'}` : ''}
                              {r.amount ? ` · ${formatCurrency(r.amount)}` : ''} · gửi {fromNow(r.createdAt)}
                            </p>
                            <p className="text-[13px] text-ink-secondary">{r.reason}</p>
                          </div>
                        </div>
                        <StatusTag tone={S.tone} icon={S.icon} size="sm">
                          {S.label}
                        </StatusTag>
                      </div>
                      <Steps
                        size="small"
                        className="mt-3"
                        current={r.steps.findIndex((s) => !s.decision)}
                        status={r.status === 'rejected' ? 'error' : undefined}
                        items={r.steps.map((s) => ({ title: s.role, description: `${s.approver}${s.comment ? ` — ${s.comment}` : ''}` }))}
                      />
                      {r.status === 'pending' && (
                        <Button
                          icon={Undo2}
                          size="sm"
                          variant="ghost"
                          tone="neutral"
                          className="mt-2"
                          onClick={async () => {
                            if (await confirm({ title: 'Hủy đơn này?', tone: 'warning' })) {
                              cancelRequest(r.id);
                              notify.toast.success('Đã hủy đơn');
                            }
                          }}
                        >
                          Hủy đơn
                        </Button>
                      )}
                    </article>
                  );
                })}
              </div>
            ) : (
              <EmptyState icon={Send} title="Bạn chưa tạo đơn nào" />
            ),
          },
        ]}
      />

      <ModalForm<Values>
        key={creating ? JSON.stringify(creating.type) + String(creating.date ?? creating.range) : 'new'}
        form={form}
        open={Boolean(creating)}
        onClose={() => setCreating(null)}
        width={560}
        title="Tạo đơn"
        description={`Người duyệt đầu tiên: quản lý trực tiếp (${nameOf(employees, me.managerId)}).`}
        submitAction="send"
        submitText="Gửi đơn"
        successMessage="Đã gửi đơn — người duyệt được thông báo"
        initialValues={creating ?? undefined}
        onSubmit={(v) => {
          const from = (v.range?.[0] ?? v.date ?? dayjs()).format('YYYY-MM-DD');
          const to = (v.range?.[1] ?? v.date ?? dayjs()).format('YYYY-MM-DD');
          const days = v.type === 'leave' && v.range ? workDays(v.range[0], v.range[1]) : undefined;
          if (v.type === 'leave' && v.paidLeave && days && days > me.leaveBalance) throw new Error(`Chỉ còn ${me.leaveBalance} ngày phép — tắt "trừ phép" để nghỉ không lương`);
          addRequest({
            type: v.type,
            employeeId: me.id,
            from,
            to,
            days,
            paidLeave: v.type === 'leave' ? v.paidLeave : undefined,
            title: v.title?.trim() || undefined,
            amount: v.amount,
            reason: v.reason.trim(),
            steps: buildSteps(me, employees, v.type, days, v.amount),
          });
        }}
      >
        <Form.Item name="type" label="Loại đơn" rules={[{ required: true }]} extra={fType && REQUEST_TYPES[fType].hint}>
          <Select options={MY_TYPES.map((t) => ({ value: t, label: REQUEST_TYPES[t].label }))} showSearch={false} allowClear={false} />
        </Form.Item>
        {(fType === 'leave' || fType === 'business_trip') && (
          <Form.Item
            name="range"
            label="Từ ngày — đến ngày"
            rules={[{ required: true, message: 'Chọn ngày' }]}
            extra={fType === 'leave' && fRange ? `${leaveDays} ngày làm việc · phép còn ${me.leaveBalance} ngày${leaveDays > 3 ? ' · cần thêm Phó giám đốc duyệt' : ''}` : undefined}
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
        {fType === 'proposal' && (
          <Form.Item name="title" label="Đề xuất" rules={[{ required: true, whitespace: true, message: 'Nhập nội dung' }]}>
            <TextInput placeholder="VD: Mua 2 bóng đèn máy chiếu" />
          </Form.Item>
        )}
        {(fType === 'proposal' || fType === 'advance') && (
          <Form.Item name="amount" label="Số tiền" rules={[{ required: fType === 'advance', message: 'Nhập số tiền' }]}>
            <NumberInput currency min={0} />
          </Form.Item>
        )}
        <Form.Item name="reason" label={fType === 'late_early' ? 'Giải trình (giờ vào / ra thực tế, lý do)' : 'Lý do'} rules={[{ required: true, whitespace: true, message: 'Nhập lý do' }]}>
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
