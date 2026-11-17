import { useEffect, useMemo, useState, type ReactNode } from 'react';
import { Drawer, Form, Rate, Tabs } from 'antd';
import dayjs, { type Dayjs } from 'dayjs';
import { CalendarClock, ClipboardCheck, FileSignature, KeyRound, PauseCircle, PlayCircle, School, Star, UserMinus, UserRound } from 'lucide-react';
import { useNavigate } from 'react-router-dom';
import { Button, DateInput, ModalForm, NumberInput, StatusTag, useConfirm, useNotify } from '@/components/common';
import { useClassStore } from '@/features/academic/classes/classStore';
import { CLASS_STATUS } from '@/features/academic/classes/constants';
import { useWorkStore } from '@/features/work/workStore';
import { useBreakpoint } from '@/hooks/useBreakpoint';
import { USE_API } from '@/lib/api/config';
import { cn } from '@/lib/cn';
import { fmtDate } from '@/lib/date';
import { formatCurrency } from '@/lib/format';
import { getBranches } from '@/features/system/useBranches';
import { CONTRACT_TYPES, DEPARTMENTS, EMPLOYEE_KIND, EMPLOYEE_STATUS, SHIFT_KEYS, WORK_SHIFTS } from './constants';
import * as hrApi from './hrApi';
import { useHrStore } from './hrStore';
import { contractEndsIn, DAY_STATE, dayState, nameOf, toMin, type DayState } from './hrUtils';
import type { ShiftKey } from './types';

const WEEKDAYS = ['T2', 'T3', 'T4', 'T5', 'T6', 'T7', 'CN'];

/** Hồ sơ nhân viên / giáo viên / trợ giảng */
export function EmployeeDrawer({ employeeId, editable, onClose, onEdit }: { employeeId: string | null; editable: boolean; onClose: () => void; onEdit: (id: string) => void }) {
  const { isMobile } = useBreakpoint();
  const notify = useNotify();
  const navigate = useNavigate();
  const { confirm, confirmDialog } = useConfirm();
  const employees = useHrStore((s) => s.employees);
  const records = useHrStore((s) => s.records);
  const requests = useHrStore((s) => s.requests);
  const update = useHrStore((s) => s.updateEmployee);
  const shifts = useWorkStore((s) => s.shifts);
  const classes = useClassStore((s) => s.classes);
  const sessions = useClassStore((s) => s.sessions);
  const reviews = useClassStore((s) => s.teacherReviews);
  const [renewing, setRenewing] = useState(false);
  const e = employees.find((x) => x.id === employeeId);
  const kind = e?.kind;

  // Chế độ API: tải chi tiết (lịch sử hợp đồng, tên quản lý mới nhất) khi mở
  useEffect(() => {
    if (USE_API && employeeId && kind) hrApi.loadEmployee(employeeId, kind).catch(() => undefined);
  }, [employeeId, kind]);

  // ---- Chấm công tháng này (nhân viên)
  const month = useMemo(() => {
    if (!e || e.kind !== 'staff') return undefined;
    const start = dayjs().startOf('month');
    const days = Array.from({ length: dayjs().date() }, (_, i) => start.add(i, 'day').format('YYYY-MM-DD'));
    const list = days.map((date) => ({ date, ...dayState(e, date, records.find((r) => r.employeeId === e.id && r.date === date), shifts, requests), rec: records.find((r) => r.employeeId === e.id && r.date === date) }));
    const count = (s: DayState[]) => list.filter((x) => s.includes(x.state)).length;
    return { list, work: count(['ok', 'late', 'early', 'late_early', 'no_out', 'working']), late: count(['late', 'late_early']), early: count(['early', 'late_early']), absent: count(['absent']), leave: count(['leave']), noOut: count(['no_out']) };
  }, [e, records, shifts, requests]);

  // ---- Giảng dạy (giáo viên / trợ giảng)
  const teaching = useMemo(() => {
    if (!e || e.kind === 'staff') return undefined;
    const mine = classes.filter((c) => c.teacherIds.includes(e.id) || c.assistantIds?.includes(e.id));
    const monthStart = dayjs().startOf('month').format('YYYY-MM-DD');
    let minutes = 0;
    let sessionsCount = 0;
    let online = 0;
    sessions
      .filter((s) => s.date >= monthStart && s.status !== 'cancelled')
      .forEach((s) => {
        const t = s.teacherAttendance?.find((x) => x.teacherId === e.id && x.status !== 'absent');
        if (!t?.checkIn || !t.checkOut) return;
        const m = Math.max(0, toMin(t.checkOut) - toMin(t.checkIn));
        minutes += m;
        sessionsCount++;
        if (classes.find((c) => c.id === s.classId)?.online) online += m;
      });
    const rate = e.teacher?.rate ?? { offline: 0, online: 0 };
    const pay = ((minutes - online) / 60) * rate.offline + (online / 60) * rate.online;
    const rv = reviews.filter((r) => r.teacherId === e.id);
    return { mine, hours: minutes / 60, sessionsCount, pay, reviews: rv, avg: rv.length ? rv.reduce((n, r) => n + r.overall, 0) / rv.length : undefined };
  }, [e, classes, sessions, reviews]);

  if (!e) return <Drawer open={false} onClose={onClose} />;
  const st = EMPLOYEE_STATUS[e.status];
  const ends = contractEndsIn(e);

  const quit = async () => {
    const res = await confirm({
      title: `Cho ${e.fullName} nghỉ việc?`,
      consequences: ['Khóa tài khoản đăng nhập', e.shiftBased ? 'Gỡ khỏi lịch xếp ca từ hôm nay' : 'Không còn trong danh sách chấm công', e.kind !== 'staff' ? 'Không chọn được khi xếp lớp / dạy thay' : 'Bàn giao lead / task đang dở cho người khác'],
      tone: 'danger',
      confirmAction: 'reject',
      confirmText: 'Xác nhận nghỉ việc',
      requireReason: { label: 'Lý do', placeholder: 'VD: hết hợp đồng, xin nghỉ' },
      onConfirm: USE_API
        ? async ({ reason }) => {
            const warnings = await hrApi.quitEmployee(e, reason ?? '');
            if (warnings.length) notify.warning({ title: 'Cần xử lý thêm', description: warnings.join(' ') });
          }
        : undefined,
    });
    if (!res) return;
    if (USE_API) {
      notify.success({
        title: 'Đã cho nghỉ việc',
        description: e.hasAccount ? `${e.fullName} — tài khoản đăng nhập bị khóa, mọi phiên bị thu hồi.` : e.fullName,
      });
      return;
    }
    update(e.id, { status: 'quit', quitAt: dayjs().format('YYYY-MM-DD'), hasAccount: false, note: `Nghỉ việc: ${res.reason}` });
    notify.success({ title: 'Đã cập nhật nghỉ việc', description: e.fullName });
  };

  const longLeave = async (onLeave: boolean) => {
    try {
      if (USE_API) await hrApi.setLongLeave(e, onLeave);
      else update(e.id, { status: onLeave ? 'leave' : 'active' });
      notify.toast.success(onLeave ? 'Đã chuyển sang nghỉ dài hạn' : 'Đã quay lại làm việc');
    } catch (err) {
      notify.error({ title: 'Chưa đổi được trạng thái', description: (err as Error).message });
    }
  };

  const toggleShift = async (day: number, k: ShiftKey, on: boolean) => {
    const cur = e.teacher!.availability[day] ?? [];
    const availability = { ...e.teacher!.availability, [day]: on ? cur.filter((x) => x !== k) : [...cur, k] };
    if (!USE_API) {
      update(e.id, { teacher: { ...e.teacher!, availability } });
      return;
    }
    try {
      await hrApi.saveAvailability(e, availability);
    } catch (err) {
      notify.error({ title: 'Chưa lưu được lịch rảnh', description: (err as Error).message });
    }
  };

  const info: [string, ReactNode][] = [
    ['Mã', <span className="font-mono">{e.code}</span>],
    ['Vị trí', `${e.position} · ${DEPARTMENTS[e.department]}`],
    ['Chi nhánh', getBranches(false).find((b) => b.id === e.branchId)?.name],
    ['Quản lý trực tiếp', e.managerId ? nameOf(employees, e.managerId) : '—'],
    ['Điện thoại', <a href={`tel:${e.phone}`} className="text-primary-700 hover:underline">{e.phone}</a>],
    ['Email', e.email],
    ['Ngày sinh', e.dob ? fmtDate(e.dob) : '—'],
    ['Ngày vào làm', `${fmtDate(e.joinedAt)} (${dayjs().diff(e.joinedAt, 'month')} tháng)`],
    ...(e.kind === 'staff' ? ([['Làm theo ca', e.shiftBased ? 'Có — xếp ca hàng tuần' : 'Không — giờ hành chính']] as [string, ReactNode][]) : []),
    ['Phép năm còn', `${e.leaveBalance} ngày`],
    ...(e.quitAt
      ? ([[e.quitScheduled ? 'Hẹn nghỉ việc' : 'Nghỉ việc từ', `${fmtDate(e.quitAt)}${e.quitReason ? ` · ${e.quitReason}` : ''}`]] as [string, ReactNode][])
      : []),
    ...(e.note ? ([['Ghi chú', e.note]] as [string, ReactNode][]) : []),
  ];

  const profile = (
    <div className="flex flex-col gap-4 text-[13px]">
      <dl className="grid grid-cols-1 gap-x-4 gap-y-1.5 sm:grid-cols-[140px_1fr]">
        {info.map(([k, v]) => (
          <div key={k} className="contents">
            <dt className="text-ink-muted">{k}</dt>
            <dd className="text-ink">{v}</dd>
          </div>
        ))}
      </dl>
      <section className={cn('rounded-[var(--radius-card)] border p-3', ends !== undefined && ends <= 45 ? 'border-warning-100 bg-warning-50' : 'border-line')}>
        <div className="flex flex-wrap items-center justify-between gap-2">
          <p className="flex items-center gap-1.5 font-semibold text-ink">
            <FileSignature size={15} aria-hidden /> Hợp đồng {e.contract.no}
          </p>
          {editable && e.status !== 'quit' && (
            <Button size="sm" variant="soft" icon={CalendarClock} onClick={() => setRenewing(true)}>
              Gia hạn / ký mới
            </Button>
          )}
        </div>
        <p className="mt-1 text-ink-secondary">
          {CONTRACT_TYPES[e.contract.type]} · từ {fmtDate(e.contract.start)}
          {e.contract.end ? ` đến ${fmtDate(e.contract.end)}` : ''}
          {ends !== undefined && ends >= 0 && ends <= 45 && <strong className="text-warning-700"> — còn {ends} ngày</strong>}
          {ends !== undefined && ends < 0 && <strong className="text-danger-600"> — đã hết hạn</strong>}
        </p>
        {e.salaryHidden && <p className="text-ink-muted">Lương & phụ cấp: ẩn (bạn không có quyền xem lương)</p>}
        {(e.contract.baseSalary || e.contract.allowance) && (
          <p className="text-ink-secondary">
            {e.contract.baseSalary ? `Lương cơ bản ${formatCurrency(e.contract.baseSalary)}` : ''}
            {e.contract.allowance ? ` · phụ cấp ${formatCurrency(e.contract.allowance)}` : ''}
          </p>
        )}
        {e.contractHistory && e.contractHistory.length > 0 && (
          <details className="mt-2">
            <summary className="cursor-pointer text-xs text-ink-muted">Hợp đồng trước đây ({e.contractHistory.length})</summary>
            <ul className="mt-1 flex flex-col gap-1 text-xs text-ink-secondary">
              {e.contractHistory.map((c) => (
                <li key={c.no}>
                  {c.no} · {CONTRACT_TYPES[c.type]} · {fmtDate(c.start)}
                  {c.end ? ` – ${fmtDate(c.end)}` : ''}
                  {c.baseSalary ? ` · ${formatCurrency(c.baseSalary)}` : ''}
                </li>
              ))}
            </ul>
          </details>
        )}
      </section>
      <section className="flex flex-wrap items-center justify-between gap-2 rounded-[var(--radius-card)] border border-line p-3">
        <span className="flex items-center gap-1.5">
          <KeyRound size={15} aria-hidden /> {e.hasAccount ? 'Đã có tài khoản đăng nhập' : <span className="text-warning-700">Chưa có tài khoản đăng nhập</span>}
        </span>
        {editable && e.status !== 'quit' && (
          <Button icon={KeyRound} size="sm" variant="ghost" onClick={() => navigate('/admin/system/user-permissions')}>
            {e.hasAccount ? 'Phân quyền' : 'Cấp tài khoản'}
          </Button>
        )}
      </section>
      {editable && e.status !== 'quit' && (
        <div className="flex flex-wrap justify-end gap-2 border-t border-line pt-3">
          {e.status === 'leave' ? (
            <Button icon={PlayCircle} variant="ghost" onClick={() => longLeave(false)}>
              Quay lại làm việc
            </Button>
          ) : (
            <Button icon={PauseCircle} variant="ghost" onClick={() => longLeave(true)}>
              Nghỉ dài hạn
            </Button>
          )}
          {!e.quitScheduled && (
            <Button icon={UserMinus} tone="danger" variant="ghost" onClick={quit}>
              Cho nghỉ việc
            </Button>
          )}
        </div>
      )}
    </div>
  );

  const timesheet = month && (
    <div className="flex flex-col gap-3 text-[13px]">
      <div className="grid grid-cols-3 gap-2 sm:grid-cols-6">
        {(
          [
            ['Ngày công', month.work],
            ['Đi muộn', month.late],
            ['Về sớm', month.early],
            ['Quên check-out', month.noOut],
            ['Vắng', month.absent],
            ['Nghỉ phép', month.leave],
          ] as [string, number][]
        ).map(([k, v]) => (
          <div key={k} className="rounded-lg border border-line p-2 text-center">
            <p className="tabular text-lg font-semibold text-ink">{v}</p>
            <p className="text-[11px] text-ink-muted">{k}</p>
          </div>
        ))}
      </div>
      <ul className="divide-y divide-line rounded-[var(--radius-card)] border border-line">
        {[...month.list].reverse().filter((x) => x.state !== 'off' && x.state !== 'future').map((x) => (
          <li key={x.date} className="flex items-center justify-between gap-2 px-3 py-1.5">
            <span className="tabular w-24 text-ink-secondary first-letter:uppercase">{dayjs(x.date).format('dd DD/MM')}</span>
            <span className="tabular flex-1 text-ink">
              {x.exp ? `${x.exp.label} ${x.exp.from}–${x.exp.to}` : 'Ngoài lịch'} · vào {x.rec?.checkIn ?? '—'} · ra {x.rec?.checkOut ?? '—'}
            </span>
            <span className={cn('text-xs font-medium', x.state === 'ok' ? 'text-success-700' : x.state === 'leave' ? 'text-primary-700' : 'text-danger-600')}>{DAY_STATE[x.state].label}{x.lateMin > 5 && x.state.startsWith('late') ? ` ${x.lateMin}′` : ''}</span>
          </li>
        ))}
      </ul>
    </div>
  );

  const availability = e.teacher && (
    <div className="flex flex-col gap-2 text-[13px]">
      <p className="text-ink-muted">Ca giáo viên có thể nhận dạy (dùng để xếp lớp mới và tìm người dạy thay). Bấm để bật / tắt.</p>
      <table className="w-full border-collapse">
        <thead>
          <tr>
            <th />
            {WEEKDAYS.map((w) => (
              <th key={w} className="py-1 text-xs font-medium text-ink-muted">{w}</th>
            ))}
          </tr>
        </thead>
        <tbody>
          {SHIFT_KEYS.map((k) => (
            <tr key={k}>
              <td className="pr-2 text-xs text-ink-secondary">{WORK_SHIFTS[k].label}</td>
              {WEEKDAYS.map((_, i) => {
                const day = i + 1;
                const on = e.teacher!.availability[day]?.includes(k);
                return (
                  <td key={day} className="p-0.5">
                    <button
                      type="button"
                      disabled={!editable}
                      onClick={() => toggleShift(day, k as ShiftKey, Boolean(on))}
                      className={cn('h-8 w-full rounded-md border text-xs font-medium', on ? 'border-success-600 bg-success-50 text-success-700' : 'border-line text-ink-muted hover:bg-page')}
                    >
                      {on ? 'Rảnh' : '—'}
                    </button>
                  </td>
                );
              })}
            </tr>
          ))}
        </tbody>
      </table>
    </div>
  );

  const classesTab = teaching && (
    <div className="flex flex-col gap-3 text-[13px]">
      <div className="grid grid-cols-3 gap-2">
        <div className="rounded-lg border border-line p-2.5">
          <p className="text-[11px] text-ink-muted">Giờ dạy tháng {dayjs().month() + 1}</p>
          <p className="tabular text-lg font-semibold">{teaching.hours.toFixed(1)}h</p>
          <p className="text-[11px] text-ink-muted">{teaching.sessionsCount} buổi đã chấm công</p>
        </div>
        <div className="rounded-lg border border-line p-2.5">
          <p className="text-[11px] text-ink-muted">Lương dạy tạm tính</p>
          <p className="tabular text-lg font-semibold">{formatCurrency(Math.round(teaching.pay))}</p>
          <p className="text-[11px] text-ink-muted">Theo đơn giá offline / online</p>
        </div>
        <div className="rounded-lg border border-line p-2.5">
          <p className="text-[11px] text-ink-muted">Lớp đang dạy</p>
          <p className="tabular text-lg font-semibold">{teaching.mine.filter((c) => c.status === 'ongoing').length}</p>
        </div>
      </div>
      <ul className="divide-y divide-line rounded-[var(--radius-card)] border border-line">
        {teaching.mine.map((c) => (
          <li key={c.id}>
            <button type="button" onClick={() => navigate(`/admin/academic/classes/${c.id}`)} className="flex w-full items-center justify-between gap-2 px-3 py-2 text-left hover:bg-page">
              <span>
                <span className="font-mono font-medium text-primary-700">{c.code}</span>
                <span className="text-ink-secondary"> · {c.startTime}–{c.endTime} · {c.online ? 'Online' : c.room}</span>
                {c.assistantIds?.includes(e.id) && <span className="text-xs text-ink-muted"> (trợ giảng)</span>}
              </span>
              <StatusTag tone={CLASS_STATUS[c.status].tone} icon={CLASS_STATUS[c.status].icon} size="sm">
                {CLASS_STATUS[c.status].label}
              </StatusTag>
            </button>
          </li>
        ))}
        {!teaching.mine.length && <li className="px-3 py-4 text-center text-ink-muted">Chưa có lớp</li>}
      </ul>
    </div>
  );

  const reviewsTab = teaching && (
    <div className="flex flex-col gap-3 text-[13px]">
      {teaching.avg !== undefined ? (
        <p className="flex items-center gap-2">
          <Rate disabled allowHalf value={Math.round(teaching.avg * 2) / 2} />
          <strong className="tabular">{teaching.avg.toFixed(1)}/5</strong>
          <span className="text-ink-muted">· {teaching.reviews.length} đánh giá</span>
        </p>
      ) : (
        <p className="text-ink-muted">Chưa có đánh giá từ học viên.</p>
      )}
      <ul className="flex flex-col gap-2">
        {teaching.reviews
          .filter((r) => r.comment)
          .slice(0, 8)
          .map((r) => (
            <li key={r.id} className="rounded-lg bg-page px-3 py-2">
              <p className="flex items-center gap-1 text-xs text-ink-muted">
                <Star size={12} className="fill-warning-600 text-warning-600" aria-hidden /> {r.overall}/5 · {r.period === 'mid' ? 'Giữa kỳ' : 'Cuối kỳ'} · {classes.find((c) => c.id === r.classId)?.code}
              </p>
              <p className="text-ink">{r.comment}</p>
            </li>
          ))}
      </ul>
    </div>
  );

  const label = (Icon: typeof UserRound, t: string) => (
    <span className="inline-flex items-center gap-1.5">
      <Icon size={16} aria-hidden /> {t}
    </span>
  );

  return (
    <Drawer
      open
      onClose={onClose}
      width={isMobile ? '100%' : 720}
      destroyOnHidden
      styles={{ body: { paddingTop: 0 } }}
      title={
        <div className="flex min-w-0 flex-col gap-1 pr-2">
          <div className="flex flex-wrap items-center gap-2">
            <span className="truncate text-base font-semibold">{e.fullName}</span>
            <StatusTag tone={st.tone} icon={st.icon} size="sm">
              {st.label}
            </StatusTag>
            {e.teacher && <StatusTag tone="info" icon={School} size="sm">{e.teacher.type === 'jp' ? 'GV Nhật' : EMPLOYEE_KIND[e.kind]}</StatusTag>}
          </div>
          <span className="text-xs font-normal text-ink-muted">
            {e.code} · {e.position}
          </span>
        </div>
      }
      extra={editable && <Button action="edit" size="sm" collapseOnMobile onClick={() => onEdit(e.id)} />}
    >
      <Tabs
        items={[
          { key: 'profile', label: label(UserRound, 'Hồ sơ'), children: profile },
          ...(month ? [{ key: 'time', label: label(ClipboardCheck, 'Chấm công tháng này'), children: timesheet }] : []),
          ...(e.teacher
            ? [
                { key: 'avail', label: label(CalendarClock, 'Lịch rảnh'), children: availability },
                { key: 'classes', label: label(School, 'Lớp & giờ dạy'), children: classesTab },
                ...(e.kind === 'teacher' ? [{ key: 'reviews', label: label(Star, 'Đánh giá'), children: reviewsTab }] : []),
              ]
            : []),
        ]}
      />

      {renewing && (
        <ModalForm<{ end?: Dayjs; salary?: number; indefinite?: boolean }>
          open
          onClose={() => setRenewing(false)}
          title={`Gia hạn hợp đồng — ${e.fullName}`}
          description={`Hợp đồng hiện tại ${e.contract.no}${e.contract.end ? ` hết hạn ${fmtDate(e.contract.end)}` : ''}`}
          submitAction="save"
          submitText="Lưu hợp đồng mới"
          successMessage="Đã cập nhật hợp đồng"
          initialValues={{ end: dayjs(e.contract.end ?? undefined).add(12, 'month'), salary: e.contract.baseSalary }}
          onSubmit={async (v) => {
            if (USE_API) {
              await hrApi.renewContract(e, { end: v.end?.format('YYYY-MM-DD'), baseSalary: v.salary });
              return;
            }
            update(e.id, {
              status: e.status === 'probation' ? 'active' : e.status,
              contract: { ...e.contract, type: e.contract.type === 'probation' ? 'fixed' : e.contract.type, no: `HĐ-${dayjs().year()}/${Date.now().toString().slice(-3)}`, start: dayjs(e.contract.end ?? undefined).add(1, 'day').format('YYYY-MM-DD'), end: v.end?.format('YYYY-MM-DD'), baseSalary: v.salary },
            });
          }}
        >
          <Form.Item name="end" label="Hợp đồng mới đến ngày" rules={[{ required: true, message: 'Chọn ngày' }]}>
            <DateInput />
          </Form.Item>
          <Form.Item name="salary" label="Lương cơ bản mới">
            <NumberInput currency min={0} />
          </Form.Item>
          {e.status === 'probation' && <p className="text-[13px] text-success-700">Nhân viên thử việc → chuyển thành chính thức.</p>}
        </ModalForm>
      )}
      {confirmDialog}
    </Drawer>
  );
}
