import { useEffect, useMemo, useState } from 'react';
import { Tooltip } from 'antd';
import dayjs from 'dayjs';
import { CalendarDays, ChevronLeft, ChevronRight, CircleCheck, CircleDashed, CircleX, ClipboardCheck, Copy, Repeat, TriangleAlert, UserRound } from 'lucide-react';
import { useNavigate } from 'react-router-dom';
import { Button, EmptyState, PageHeader, StatusTag, useConfirm, useNotify } from '@/components/common';
import { DEPARTMENTS, SHIFT_KEYS, WORK_SHIFTS } from '@/features/hr/constants';
import { useHrStore } from '@/features/hr/hrStore';
import { meOf, onLeave, toMin } from '@/features/hr/hrUtils';
import type { Employee, ShiftKey } from '@/features/hr/types';
import { COVERAGE } from '@/features/work/constants';
import { useWorkStore } from '@/features/work/workStore';
import { useAuth } from '@/hooks/useAuth';
import { cn } from '@/lib/cn';
import { getBranches } from '@/features/system/useBranches';
import { ALL_BRANCHES } from '@/types/auth';
import { USE_API } from '@/lib/api/config';
import { fillWeek, loadShiftWeek } from '@/features/work/workApi';

const wd = (d: dayjs.Dayjs) => ((d.day() + 6) % 7) + 1;
const CHIP: Record<ShiftKey, { on: string; pref: string }> = {
  morning: { on: 'bg-primary-500 text-white border-primary-500', pref: 'border-primary-400 text-primary-700' },
  afternoon: { on: 'bg-warning-600 text-white border-warning-600', pref: 'border-warning-600 text-warning-700' },
  evening: { on: 'bg-primary-800 text-white border-primary-800', pref: 'border-primary-700 text-primary-800' },
};
const hours = (k: ShiftKey) => (toMin(WORK_SHIFTS[k].to) - toMin(WORK_SHIFTS[k].from)) / 60;

/** Xếp ca làm việc cho nhân viên "Làm theo ca" theo tuần. Chế độ API: workApi (bấm ô cập nhật lạc quan, server kiểm bận / ngày qua / đơn đổi ca). */
export default function ShiftsPage() {
  const { user, branchId, can } = useAuth();
  const notify = useNotify();
  const navigate = useNavigate();
  const { confirm, confirmDialog } = useConfirm();
  const employees = useHrStore((s) => s.employees);
  const requests = useHrStore((s) => s.requests);
  const shifts = useWorkStore((s) => s.shifts);
  const prefs = useWorkStore((s) => s.prefs);
  const availability = useWorkStore((s) => s.availability);
  const toggleShift = useWorkStore((s) => s.toggleShift);
  const applyPrefs = useWorkStore((s) => s.applyPrefs);
  const copyWeek = useWorkStore((s) => s.copyWeek);
  const editable = can('work.shifts') && user?.roleName !== 'Sale';
  const me = meOf(employees, user);

  const [week, setWeek] = useState(() => dayjs().subtract(wd(dayjs()) - 1, 'day').startOf('day'));
  const days = Array.from({ length: 7 }, (_, i) => week.add(i, 'day'));
  const dates = days.map((d) => d.format('YYYY-MM-DD'));
  const today = dayjs().format('YYYY-MM-DD');

  const branches = getBranches(false).filter((b) => branchId === ALL_BRANCHES || b.id === branchId);
  const weekKey = week.format('YYYY-MM-DD');
  const branchKey = branches.map((b) => b.id).join(',');
  // Chế độ API: lưới tuần của từng chi nhánh đang xem (ca, rảnh / bận) — chi nhánh ngoài phạm vi thì bỏ qua
  useEffect(() => {
    if (!USE_API) return;
    branchKey.split(',').filter(Boolean).forEach((b) => void loadShiftWeek(b, weekKey).catch((e: Error) => notify.toast.error(e.message)));
  }, [weekKey, branchKey]); // eslint-disable-line react-hooks/exhaustive-deps
  /** XC-02 ở chế độ API: 1 request / chi nhánh, server bỏ qua ô bận, ngày đã qua, NV nghỉ */
  const fillAll = async (mode: 'registrations' | 'copy') => {
    try {
      const counts = await Promise.all(branches.map((b) => fillWeek(b.id, weekKey, mode).catch(() => 0)));
      const n = counts.reduce((a, c) => a + c, 0);
      notify.toast.success(mode === 'copy' ? `Đã chép ${n} ca từ tuần trước (ô trống)` : `Đã xếp ${n} ca theo đăng ký`);
    } catch (e) {
      notify.toast.error((e as Error).message);
    }
  };
  const people =employees.filter((e) => e.shiftBased && e.status !== 'quit' && branches.some((b) => b.id === e.branchId));
  const ids = people.map((e) => e.id);
  const availabilityStatus = (employeeId: string, date: string, shift: ShiftKey) =>
    availability.find((item) => item.employeeId === employeeId && item.date === date && item.shift === shift)?.status;

  const has = (e: string, date: string, k: ShiftKey) => shifts.some((s) => s.employeeId === e && s.date === date && s.shift === k);
  const pref = (e: string, date: string, k: ShiftKey) => prefs.some((p) => p.employeeId === e && p.date === date && p.shift === k);
  const prefCount = prefs.filter(
    (p) => dates.includes(p.date) && ids.includes(p.employeeId) && !has(p.employeeId, p.date, p.shift) && availabilityStatus(p.employeeId, p.date, p.shift) !== 'busy',
  ).length;
  const futureDates = dates.filter((date) => date >= today);
  const weekAvailability = availability.filter((item) => ids.includes(item.employeeId) && futureDates.includes(item.date));
  const availableCount = weekAvailability.filter((item) => item.status === 'available').length;
  const busyCount = weekAvailability.filter((item) => item.status === 'busy').length;
  const unconfirmedCount = people.length * futureDates.length * SHIFT_KEYS.length - availableCount - busyCount;
  const swaps = requests.filter((r) => r.type === 'shift_swap' && r.status === 'pending' && ids.includes(r.employeeId));

  /** Thiếu người theo bộ phận / ca / ngày (từng chi nhánh) */
  const shortages = useMemo(() => {
    const out: { branch: string; dept: 'sales' | 'reception'; date: string; shift: ShiftKey; need: number; have: number }[] = [];
    branches.forEach((b) =>
      (['sales', 'reception'] as const).forEach((dept) => {
        const req = COVERAGE[dept];
        const staff = people.filter((e) => e.branchId === b.id && e.department === dept && e.status !== 'leave');
        if (!req || !staff.length) return;
        days.forEach((d) => {
          const date = d.format('YYYY-MM-DD');
          SHIFT_KEYS.forEach((k) => {
            const need = (wd(d) >= 6 ? req.weekend : req.weekday)[k];
            const have = staff.filter((e) => has(e.id, date, k) && availabilityStatus(e.id, date, k) !== 'busy' && !onLeave(e.id, date, requests)).length;
            if (have < need) out.push({ branch: b.id, dept, date, shift: k, need, have });
          });
        });
      }),
    );
    return out;
  }, [branches, people, days, shifts, requests, availability]); // eslint-disable-line react-hooks/exhaustive-deps

  const click = async (e: Employee, date: string, k: ShiftKey) => {
    if (!editable) return;
    if (date < today) {
      notify.toast.warning('Không sửa ca của ngày đã qua — sửa công ở Chấm công');
      return;
    }
    if (!has(e.id, date, k) && availabilityStatus(e.id, date, k) === 'busy') {
      notify.toast.warning(`${e.fullName} đã báo bận ca này — hãy chọn nhân viên khác hoặc xác nhận lại với nhân viên`);
      return;
    }
    const leave = onLeave(e.id, date, requests);
    if (leave && !has(e.id, date, k)) {
      const ok = await confirm({ title: `${e.fullName} đang nghỉ phép ngày này`, description: `Đơn ${leave.code} đã duyệt. Vẫn xếp ca?`, tone: 'warning', confirmAction: 'save', confirmText: 'Vẫn xếp' });
      if (!ok) return;
    }
    toggleShift(e.id, date, k);
  };

  const prevDates = days.map((d) => d.subtract(7, 'day').format('YYYY-MM-DD'));

  return (
    <>
      <PageHeader
        title="Xếp ca làm việc"
        subtitle="Nhân viên làm theo ca (Sale, lễ tân) — bấm S / C / T để xếp; ô viền đứt là ca nhân viên đã đăng ký"
        breadcrumb={[{ title: 'Công việc' }, { title: 'Xếp ca làm việc' }]}
        actions={
          editable && (
            <>
              <Button icon={Copy} variant="outline" collapseOnMobile onClick={() => (USE_API ? void fillAll('copy') : notify.toast.success(`Đã chép ${copyWeek(prevDates, dates, ids)} ca từ tuần trước (ô trống)`))}>
                Chép tuần trước
              </Button>
              <Button icon={ClipboardCheck} disabled={!prefCount} collapseOnMobile onClick={() => (USE_API ? void fillAll('registrations') : notify.toast.success(`Đã xếp ${applyPrefs(dates, ids)} ca theo đăng ký`))}>
                Áp dụng ca đăng ký ({prefCount})
              </Button>
            </>
          )
        }
      />

      <div className="mb-3 flex flex-wrap items-center gap-2">
        <div className="flex items-center gap-1">
          <Button icon={ChevronLeft} tone="neutral" variant="outline" iconOnly aria-label="Tuần trước" onClick={() => setWeek((w) => w.subtract(7, 'day'))} />
          <Button icon={ChevronRight} tone="neutral" variant="outline" iconOnly aria-label="Tuần sau" onClick={() => setWeek((w) => w.add(7, 'day'))} />
        </div>
        <Button icon={CalendarDays} variant="soft" onClick={() => setWeek(dayjs().subtract(wd(dayjs()) - 1, 'day').startOf('day'))}>
          Tuần này
        </Button>
        <span className="tabular font-semibold text-ink">
          {week.format('DD/MM')} – {week.add(6, 'day').format('DD/MM/YYYY')}
        </span>
        <span className="ml-auto flex flex-wrap items-center gap-3 text-xs text-ink-secondary">
          {SHIFT_KEYS.map((k) => (
            <span key={k} className="inline-flex items-center gap-1">
              <span className={cn('grid h-5 w-5 place-items-center rounded border text-[10px] font-bold', CHIP[k].on)}>{WORK_SHIFTS[k].short}</span>
              {WORK_SHIFTS[k].label} {WORK_SHIFTS[k].from}–{WORK_SHIFTS[k].to}
            </span>
          ))}
          <span className="inline-flex items-center gap-1">
            <span className="h-5 w-5 rounded border border-dashed border-primary-400" /> Đã đăng ký
          </span>
          <span className="inline-flex items-center gap-1">
            <span className="grid h-5 w-5 place-items-center rounded border border-danger-400 bg-danger-50 text-[10px] font-bold text-danger-700">!</span> Báo bận
          </span>
        </span>
      </div>

      {(shortages.length > 0 || swaps.length > 0) && (
        <div className="mb-3 grid grid-cols-1 gap-2 lg:grid-cols-2">
          {shortages.length > 0 && (
            <div className="rounded-[var(--radius-card)] border border-danger-100 bg-danger-50 px-3 py-2 text-[13px] text-danger-700">
              <p className="flex items-center gap-1.5 font-semibold">
                <TriangleAlert size={15} aria-hidden /> {shortages.length} ca thiếu người
              </p>
              <p className="mt-1 text-xs">
                {shortages
                  .slice(0, 6)
                  .map((s) => `${dayjs(s.date).format('dd DD/MM')} ${WORK_SHIFTS[s.shift].label.toLowerCase()} — ${DEPARTMENTS[s.dept]} ${getBranches(false).find((b) => b.id === s.branch)?.name} (${s.have}/${s.need})`)
                  .join(' · ')}
                {shortages.length > 6 && ` · +${shortages.length - 6}`}
              </p>
            </div>
          )}
          {swaps.length > 0 && (
            <button type="button" onClick={() => navigate('/admin/hr/requests')} className="rounded-[var(--radius-card)] border border-warning-100 bg-warning-50 px-3 py-2 text-left text-[13px] text-warning-700 hover:bg-warning-100">
              <p className="flex items-center gap-1.5 font-semibold">
                <Repeat size={15} aria-hidden /> {swaps.length} đơn xin đổi ca chờ duyệt
              </p>
              <p className="mt-1 text-xs">Duyệt ở Nhân sự → Đơn từ & phê duyệt — duyệt xong lịch ca tự cập nhật.</p>
            </button>
          )}
        </div>
      )}

      {people.length > 0 && (
        <div className="mb-3 flex flex-wrap items-center gap-x-5 gap-y-2 rounded-[var(--radius-card)] border border-line bg-surface px-3 py-2 text-xs">
          <span className="font-semibold text-ink">Lịch nhân viên trong tuần:</span>
          <span className="inline-flex items-center gap-1 text-success-700"><CircleCheck size={14} aria-hidden /> {availableCount} ca rảnh</span>
          <span className="inline-flex items-center gap-1 text-danger-700"><CircleX size={14} aria-hidden /> {busyCount} ca bận</span>
          <span className="inline-flex items-center gap-1 text-ink-muted"><CircleDashed size={14} aria-hidden /> {unconfirmedCount} ca chưa xác nhận</span>
        </div>
      )}

      {people.length === 0 ? (
        <EmptyState icon={UserRound} title="Chi nhánh chưa có nhân viên làm theo ca" description="Bật thuộc tính “Làm theo ca” ở Nhân sự → Nhân viên." />
      ) : (
        <div className="overflow-auto rounded-[var(--radius-card)] border border-line bg-surface">
          <table className="w-full min-w-[980px] border-collapse text-[13px]">
            <thead className="sticky top-0 z-10 bg-primary-50">
              <tr>
                <th className="w-56 border-b border-line px-3 py-2 text-left font-semibold text-ink">Nhân viên</th>
                {days.map((d) => {
                  const date = d.format('YYYY-MM-DD');
                  return (
                    <th key={date} className={cn('border-b border-l border-line px-1 py-2 font-medium', date === today ? 'bg-primary-100 text-primary-800' : 'text-ink-secondary')}>
                      <span className="block text-xs">{d.format('dd')}</span>
                      <span className="tabular text-sm font-semibold text-ink">{d.format('DD/MM')}</span>
                    </th>
                  );
                })}
                <th className="w-20 border-b border-l border-line px-2 py-2 text-right font-semibold text-ink">Giờ</th>
              </tr>
            </thead>
            <tbody>
              {branches.map((b) => {
                const list = people.filter((e) => e.branchId === b.id).sort((a, x) => a.department.localeCompare(x.department));
                if (!list.length) return null;
                return [
                  <tr key={`b-${b.id}`}>
                    <td colSpan={9} className="border-b border-line bg-page px-3 py-1.5 text-xs font-semibold tracking-wide text-ink-secondary uppercase">
                      Chi nhánh {b.name}
                    </td>
                  </tr>,
                  ...list.map((e) => {
                    const total = shifts.filter((s) => s.employeeId === e.id && dates.includes(s.date)).reduce((n, s) => n + hours(s.shift), 0);
                    const isMe = me?.id === e.id;
                    return (
                      <tr key={e.id} className={cn(isMe && 'bg-primary-50/40')}>
                        <td className="border-b border-line px-3 py-1.5">
                          <p className="font-medium text-ink">
                            {e.fullName} {isMe && <span className="text-xs text-primary-700">(tôi)</span>}
                          </p>
                          <p className="text-xs text-ink-muted">
                            {e.position}
                            {e.status === 'probation' && ' · thử việc'}
                          </p>
                          {e.status === 'leave' && (
                            <StatusTag tone="warning" icon={UserRound} size="sm">
                              Nghỉ dài hạn
                            </StatusTag>
                          )}
                        </td>
                        {days.map((d) => {
                          const date = d.format('YYYY-MM-DD');
                          const leave = onLeave(e.id, date, requests);
                          return (
                            <td key={date} className={cn('border-b border-l border-line px-1 py-1 text-center', date === today && 'bg-primary-50/50', date < today && 'bg-page/60')}>
                              {leave ? (
                                <Tooltip title={`${leave.code} — ${leave.reason}`}>
                                  <span className="inline-block rounded bg-primary-50 px-1.5 py-0.5 text-[11px] font-medium text-primary-700">Nghỉ phép</span>
                                </Tooltip>
                              ) : e.status === 'leave' ? (
                                <span className="text-[11px] text-ink-muted">—</span>
                              ) : (
                                <span className="inline-flex gap-0.5">
                                  {SHIFT_KEYS.map((k) => {
                                    const on = has(e.id, date, k);
                                    const p = pref(e.id, date, k);
                                    const status = availabilityStatus(e.id, date, k);
                                    const busy = status === 'busy';
                                    return (
                                      <Tooltip key={k} title={`${WORK_SHIFTS[k].label} ${WORK_SHIFTS[k].from}–${WORK_SHIFTS[k].to}${busy ? ' · Nhân viên đã báo bận' : p && !on ? ' · NV đã xác nhận rảnh' : ''}`}>
                                        <button
                                          type="button"
                                          disabled={!editable}
                                          onClick={() => click(e, date, k)}
                                          className={cn(
                                            'grid h-6 w-6 place-items-center rounded border text-[10px] font-bold transition-colors',
                                            on
                                              ? cn(CHIP[k].on, busy && 'ring-2 ring-danger-500 ring-offset-1')
                                              : busy
                                                ? 'border-danger-400 bg-danger-50 text-danger-700'
                                                : p
                                                  ? cn('border-dashed bg-surface', CHIP[k].pref)
                                                  : 'border-line text-ink-muted/50 hover:border-primary-300 hover:text-primary-600',
                                            !editable && 'cursor-default',
                                          )}
                                          aria-pressed={on}
                                          aria-label={`${e.fullName} ${dayjs(date).format('DD/MM')} ${WORK_SHIFTS[k].label}${busy ? ' — đã báo bận' : ''}`}
                                        >
                                          {busy && !on ? '!' : WORK_SHIFTS[k].short}
                                        </button>
                                      </Tooltip>
                                    );
                                  })}
                                </span>
                              )}
                            </td>
                          );
                        })}
                        <td className="tabular border-b border-l border-line px-2 text-right font-semibold text-ink">{total ? `${total}h` : '—'}</td>
                      </tr>
                    );
                  }),
                  // Hàng kiểm tra đủ người
                  ...(['sales', 'reception'] as const)
                    .filter((dept) => list.some((e) => e.department === dept))
                    .map((dept) => (
                      <tr key={`cov-${b.id}-${dept}`} className="bg-page/50">
                        <td className="border-b border-line px-3 py-1 text-xs text-ink-muted">Đủ người — {DEPARTMENTS[dept]}</td>
                        {days.map((d) => {
                          const date = d.format('YYYY-MM-DD');
                          return (
                            <td key={date} className="border-b border-l border-line px-1 py-1 text-center">
                              <span className="inline-flex gap-0.5">
                                {SHIFT_KEYS.map((k) => {
                                  const s = shortages.find((x) => x.branch === b.id && x.dept === dept && x.date === date && x.shift === k);
                                  const need = (wd(d) >= 6 ? COVERAGE[dept]!.weekend : COVERAGE[dept]!.weekday)[k];
                                  return (
                                    <span key={k} title={s ? `Thiếu: ${s.have}/${s.need}` : need ? 'Đủ' : 'Không yêu cầu'} className={cn('tabular grid h-5 w-6 place-items-center rounded text-[10px] font-semibold', s ? 'bg-danger-600 text-white' : need ? 'text-success-700' : 'text-ink-muted/40')}>
                                      {s ? `${s.have}/${s.need}` : need ? '✓' : '·'}
                                    </span>
                                  );
                                })}
                              </span>
                            </td>
                          );
                        })}
                        <td className="border-b border-l border-line" />
                      </tr>
                    )),
                ];
              })}
            </tbody>
          </table>
        </div>
      )}
      {confirmDialog}
    </>
  );
}
