import { useEffect, useMemo, useState } from 'react';
import dayjs, { type Dayjs } from 'dayjs';
import { CalendarDays, CheckCircle2, ChevronLeft, ChevronRight, CircleDashed, CircleX } from 'lucide-react';
import { Button, EmptyState, PageHeader, StatCard, useNotify } from '@/components/common';
import { SHIFT_KEYS, WORK_SHIFTS } from '@/features/hr/constants';
import type { ShiftKey } from '@/features/hr/types';
import { useMe } from '@/features/me/useMe';
import { useWorkStore } from '@/features/work/workStore';
import type { ShiftAvailabilityStatus } from '@/features/work/types';
import { cn } from '@/lib/cn';
import { USE_API } from '@/lib/api/config';
import { loadMyShifts } from '@/features/work/workApi';

const startOfWeek = (date: Dayjs) => date.subtract((date.day() + 6) % 7, 'day').startOf('day');
const shiftKey = (date: string, shift: ShiftKey) => `${date}:${shift}`;

/** Cá nhân › Đăng ký ca mình rảnh / bận để quản lý tham khảo khi xếp lịch. */
export default function MyShiftAvailabilityPage() {
  const me = useMe();
  const notify = useNotify();
  const availability = useWorkStore((state) => state.availability);
  const shifts = useWorkStore((state) => state.shifts);
  const setAvailability = useWorkStore((state) => state.setAvailability);
  const [week, setWeek] = useState(() => startOfWeek(dayjs()).add(7, 'day'));
  const today = dayjs().format('YYYY-MM-DD');
  const days = useMemo(() => Array.from({ length: 7 }, (_, index) => week.add(index, 'day')), [week]);
  const dates = useMemo(() => days.map((date) => date.format('YYYY-MM-DD')), [days]);
  // Chế độ API: ca đã xếp + rảnh / bận của tôi (2 tuần từ tuần đang xem); báo rảnh / bận lưu ngay (workApi)
  const weekKey = week.format('YYYY-MM-DD');
  useEffect(() => {
    if (USE_API) loadMyShifts(weekKey).catch((e: Error) => notify.toast.error(e.message));
  }, [weekKey]); // eslint-disable-line react-hooks/exhaustive-deps

  const myAvailability = useMemo(
    () => new Map(availability.filter((item) => item.employeeId === me?.id).map((item) => [shiftKey(item.date, item.shift), item.status])),
    [availability, me?.id],
  );
  const myShifts = useMemo(
    () => new Set(shifts.filter((item) => item.employeeId === me?.id).map((item) => shiftKey(item.date, item.shift))),
    [shifts, me?.id],
  );
  const weekAvailability = availability.filter((item) => item.employeeId === me?.id && dates.includes(item.date) && item.date >= today);
  const availableCount = weekAvailability.filter((item) => item.status === 'available').length;
  const busyCount = weekAvailability.filter((item) => item.status === 'busy').length;
  const unconfirmedCount = dates.filter((date) => date >= today).length * SHIFT_KEYS.length - availableCount - busyCount;

  if (!me) return <EmptyState icon={CalendarDays} title="Tài khoản chưa gắn hồ sơ nhân viên" description="Liên hệ phòng nhân sự để liên kết tài khoản với hồ sơ." />;
  if (!me.shiftBased) return <EmptyState icon={CalendarDays} title="Bạn không thuộc nhóm xếp ca" description="Phần đăng ký lịch rảnh / bận dành cho nhân sự làm theo ca." />;

  const setStatus = (date: string, shift: ShiftKey, status?: ShiftAvailabilityStatus) => {
    if (date < today) {
      notify.info({ title: 'Không thể cập nhật ngày đã qua', description: 'Chỉ xác nhận lịch từ hôm nay trở đi.' });
      return;
    }
    setAvailability(me.id, date, shift, status);
  };

  return (
    <>
      <PageHeader
        title="Lịch rảnh / bận của tôi"
        subtitle="Xác nhận từng ca để quản lý dễ nhận biết ai có thể xếp lịch. Đây là nguyện vọng, không thay thế lịch ca đã duyệt."
        breadcrumb={[{ title: 'Cá nhân' }, { title: 'Lịch rảnh / bận' }]}
      />

      <div className="mb-4 grid grid-cols-1 gap-3 sm:grid-cols-3">
        <StatCard label="Đã xác nhận rảnh" value={availableCount} icon={CheckCircle2} tone="success" />
        <StatCard label="Đã báo bận" value={busyCount} icon={CircleX} tone="danger" />
        <StatCard label="Chưa xác nhận" value={unconfirmedCount} icon={CircleDashed} tone="neutral" />
      </div>

      <div className="mb-3 flex flex-wrap items-center gap-2">
        <Button icon={ChevronLeft} tone="neutral" variant="outline" iconOnly aria-label="Tuần trước" onClick={() => setWeek((current) => current.subtract(7, 'day'))} />
        <Button icon={ChevronRight} tone="neutral" variant="outline" iconOnly aria-label="Tuần sau" onClick={() => setWeek((current) => current.add(7, 'day'))} />
        <Button icon={CalendarDays} variant="soft" onClick={() => setWeek(startOfWeek(dayjs()))}>
          Tuần này
        </Button>
        <span className="tabular font-semibold text-ink">
          {week.format('DD/MM')} – {week.add(6, 'day').format('DD/MM/YYYY')}
        </span>
      </div>

      <div className="mb-4 rounded-xl border border-primary-100 bg-primary-50 px-3 py-2.5 text-[13px] text-primary-800">
        Chọn <b>rảnh</b>, <b>bận</b> hoặc bỏ xác nhận cho từng ca. Ca chưa xác nhận không được xem là bận; quản lý vẫn có thể điều chỉnh lịch thực tế.
      </div>

      <div className="grid gap-3 sm:grid-cols-2 xl:grid-cols-4">
        {days.map((day) => {
          const date = day.format('YYYY-MM-DD');
          const past = date < today;
          return (
            <section key={date} className={cn('rounded-xl border bg-surface p-3', past ? 'border-line opacity-60' : 'border-line')}>
              <h2 className="mb-2 flex items-center justify-between font-semibold text-ink">
                <span>{day.format('dd DD/MM')}</span>
                {date === today && <span className="rounded-full bg-primary-50 px-2 py-0.5 text-[11px] text-primary-700">Hôm nay</span>}
              </h2>
              <div className="flex flex-col gap-2">
                {SHIFT_KEYS.map((shift) => {
                  const key = shiftKey(date, shift);
                  const status = myAvailability.get(key);
                  const assigned = myShifts.has(key);
                  return (
                    <div key={shift} className="rounded-lg border border-line p-2">
                      <div className="mb-1.5 flex items-center justify-between gap-2">
                        <div>
                          <p className="text-[13px] font-medium text-ink">{WORK_SHIFTS[shift].label}</p>
                          <p className="text-[11px] text-ink-muted">{WORK_SHIFTS[shift].from}–{WORK_SHIFTS[shift].to}</p>
                        </div>
                        <div className="flex gap-1">
                          <Button
                            icon={CheckCircle2}
                            iconOnly
                            size="sm"
                            tone={status === 'available' ? 'success' : 'neutral'}
                            variant={status === 'available' ? 'solid' : 'outline'}
                            disabled={past}
                            aria-label={`Xác nhận rảnh ${WORK_SHIFTS[shift].label} ${day.format('DD/MM')}`}
                            tooltip="Xác nhận rảnh"
                            onClick={() => setStatus(date, shift, 'available')}
                          />
                          <Button
                            icon={CircleX}
                            iconOnly
                            size="sm"
                            tone={status === 'busy' ? 'danger' : 'neutral'}
                            variant={status === 'busy' ? 'solid' : 'outline'}
                            disabled={past}
                            aria-label={`Báo bận ${WORK_SHIFTS[shift].label} ${day.format('DD/MM')}`}
                            tooltip="Báo bận"
                            onClick={() => setStatus(date, shift, 'busy')}
                          />
                          <Button
                            icon={CircleDashed}
                            iconOnly
                            size="sm"
                            tone="neutral"
                            variant={!status ? 'soft' : 'outline'}
                            disabled={past || !status}
                            aria-label={`Bỏ xác nhận ${WORK_SHIFTS[shift].label} ${day.format('DD/MM')}`}
                            tooltip="Bỏ xác nhận"
                            onClick={() => setStatus(date, shift)}
                          />
                        </div>
                      </div>
                      <p className={cn('text-[11px]', status === 'available' ? 'text-success-700' : status === 'busy' ? 'text-danger-700' : 'text-ink-muted')}>
                        {status === 'available' ? 'Đã xác nhận rảnh' : status === 'busy' ? 'Đã báo bận' : 'Chưa xác nhận'}
                        {assigned && ' · Đã được xếp ca'}
                      </p>
                      {assigned && status === 'busy' && <p className="mt-1 text-[11px] text-warning-700">Ca đã xếp trùng lịch bận — vui lòng báo quản lý.</p>}
                    </div>
                  );
                })}
              </div>
            </section>
          );
        })}
      </div>
    </>
  );
}
