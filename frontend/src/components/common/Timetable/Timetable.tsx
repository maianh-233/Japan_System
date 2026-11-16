import { useEffect, useMemo, useState, type ReactNode } from 'react';
import dayjs, { type Dayjs } from 'dayjs';
import { CalendarCheck, CalendarX2, ChevronLeft, ChevronRight, Clock, MapPin } from 'lucide-react';
import { cn } from '@/lib/cn';
import { useBreakpoint } from '@/hooks/useBreakpoint';
import { Button } from '../Button/Button';
import { EmptyState } from '../Feedback/EmptyState';
import { LoadingSkeleton } from '../Feedback/LoadingSkeleton';
import { accentBorderClasses, softToneClasses, type Tone } from '../tones';

export interface TimetableEvent {
  id: string | number;
  start: Dayjs | string | Date;
  end: Dayjs | string | Date;
  title: string;
  /** Dòng phụ: giáo viên, lớp, khóa học... */
  subtitle?: string;
  /** Phòng học / link online */
  location?: string;
  tone?: Tone;
  /** Buổi đã hủy → gạch ngang, mờ */
  cancelled?: boolean;
  /** Nội dung thêm (vd: <StatusTag />) — hiện ở dạng danh sách mobile */
  extra?: ReactNode;
  /** Biểu tượng nhỏ ở góc ô (vd: đã xác nhận, sự cố) — hiện ở lưới tuần */
  badge?: ReactNode;
}

export interface TimetableProps {
  events: TimetableEvent[];
  /** Một ngày bất kỳ trong tuần muốn xem (controlled). Bỏ trống → tuần hiện tại. */
  week?: Dayjs;
  onWeekChange?: (weekStart: Dayjs) => void;
  onEventClick?: (event: TimetableEvent) => void;
  /** Giờ bắt đầu/kết thúc của lưới (mặc định 7h–22h) */
  startHour?: number;
  endHour?: number;
  /** Chiều cao 1 giờ (px) */
  hourHeight?: number;
  loading?: boolean;
  /** Nút/ô lọc thêm trên thanh công cụ */
  toolbarExtra?: ReactNode;
  className?: string;
}

/** Thứ 2 đầu tuần */
function startOfWeek(d: Dayjs): Dayjs {
  return d.subtract((d.day() + 6) % 7, 'day').startOf('day');
}

type Placed = TimetableEvent & { s: Dayjs; e: Dayjs; col: number; cols: number };

/** Xếp các buổi trùng giờ cạnh nhau trong một ngày */
function layoutDay(events: (TimetableEvent & { s: Dayjs; e: Dayjs })[]): Placed[] {
  const sorted = [...events].sort((a, b) => a.s.valueOf() - b.s.valueOf() || b.e.valueOf() - a.e.valueOf());
  const result: Placed[] = [];
  let cluster: Placed[] = [];
  let clusterEnd = 0;
  const flush = () => {
    const cols = Math.max(1, ...cluster.map((c) => c.col + 1));
    cluster.forEach((c) => result.push({ ...c, cols }));
    cluster = [];
  };
  for (const ev of sorted) {
    if (cluster.length && ev.s.valueOf() >= clusterEnd) flush();
    const used = new Set(cluster.filter((c) => c.e.valueOf() > ev.s.valueOf()).map((c) => c.col));
    let col = 0;
    while (used.has(col)) col++;
    cluster.push({ ...ev, col, cols: 1 });
    clusterEnd = Math.max(clusterEnd, ev.e.valueOf());
  }
  if (cluster.length) flush();
  return result;
}

const fmtTime = (d: Dayjs) => d.format('HH:mm');

/**
 * Thời khóa biểu theo tuần (Thứ 2 → Chủ nhật).
 * Desktop/tablet: lưới giờ, các buổi trùng giờ xếp cạnh nhau, vạch đỏ giờ hiện tại.
 * Mobile: thanh chọn ngày + danh sách buổi trong ngày.
 */
export function Timetable({
  events,
  week,
  onWeekChange,
  onEventClick,
  startHour = 7,
  endHour = 22,
  hourHeight = 56,
  loading = false,
  toolbarExtra,
  className,
}: TimetableProps) {
  const { md } = useBreakpoint();
  const [innerWeek, setInnerWeek] = useState(() => startOfWeek(dayjs()));
  const weekStart = week ? startOfWeek(week) : innerWeek;
  const days = useMemo(() => Array.from({ length: 7 }, (_, i) => weekStart.add(i, 'day')), [weekStart]);
  const [selectedDay, setSelectedDay] = useState<Dayjs>(() => dayjs().startOf('day'));
  const [now, setNow] = useState(() => dayjs());

  useEffect(() => {
    const t = setInterval(() => setNow(dayjs()), 60_000);
    return () => clearInterval(t);
  }, []);

  // giữ ngày đang chọn (mobile) nằm trong tuần đang xem
  useEffect(() => {
    if (selectedDay.isBefore(weekStart) || selectedDay.isAfter(weekStart.add(6, 'day'))) {
      const today = dayjs().startOf('day');
      setSelectedDay(today.isBefore(weekStart) || today.isAfter(weekStart.add(6, 'day')) ? weekStart : today);
    }
  }, [weekStart, selectedDay]);

  const goWeek = (target: Dayjs) => {
    const ws = startOfWeek(target);
    if (!week) setInnerWeek(ws);
    onWeekChange?.(ws);
  };

  const parsed = useMemo(
    () => events.map((ev) => ({ ...ev, s: dayjs(ev.start), e: dayjs(ev.end) })),
    [events],
  );
  const byDay = useMemo(
    () => days.map((d) => parsed.filter((ev) => ev.s.isSame(d, 'day'))),
    [days, parsed],
  );

  const weekLabel = `${weekStart.format('DD/MM')} – ${weekStart.add(6, 'day').format('DD/MM/YYYY')}`;
  const isCurrentWeek = startOfWeek(dayjs()).isSame(weekStart, 'day');

  const toolbar = (
    <div className="mb-3 flex flex-wrap items-center gap-2">
      <div className="flex items-center gap-1">
        <Button icon={ChevronLeft} tone="neutral" variant="outline" iconOnly aria-label="Tuần trước" onClick={() => goWeek(weekStart.subtract(7, 'day'))} />
        <Button icon={ChevronRight} tone="neutral" variant="outline" iconOnly aria-label="Tuần sau" onClick={() => goWeek(weekStart.add(7, 'day'))} />
      </div>
      <Button icon={CalendarCheck} tone="primary" variant="soft" disabled={isCurrentWeek} onClick={() => goWeek(dayjs())}>
        Hôm nay
      </Button>
      <span className="tabular font-semibold text-ink">{weekLabel}</span>
      {toolbarExtra && <div className="flex w-full flex-wrap gap-2 sm:ml-auto sm:w-auto">{toolbarExtra}</div>}
    </div>
  );

  const eventButton = (ev: TimetableEvent & { s: Dayjs; e: Dayjs }, compact: boolean) => (
    <button
      type="button"
      onClick={() => onEventClick?.(ev)}
      className={cn(
        'flex h-full w-full flex-col overflow-hidden rounded-md border border-l-[3px] text-left transition-shadow',
        'hover:shadow-[var(--shadow-soft-md)] focus-visible:ring-[3px] focus-visible:ring-primary-500/30 focus-visible:outline-none',
        softToneClasses[ev.tone ?? 'primary'],
        accentBorderClasses[ev.tone ?? 'primary'],
        compact ? 'px-1.5 py-1' : 'px-3 py-2.5',
        ev.cancelled && 'opacity-60',
      )}
      title={`${fmtTime(ev.s)}–${fmtTime(ev.e)} · ${ev.title}${ev.location ? ` · ${ev.location}` : ''}`}
    >
      <span className={cn('tabular flex items-center justify-between gap-1 text-[11px] leading-4 opacity-90', !compact && 'text-xs')}>
        <span className="truncate">
          {fmtTime(ev.s)}–{fmtTime(ev.e)}
        </span>
        {ev.badge && <span className="flex shrink-0 items-center gap-0.5">{ev.badge}</span>}
      </span>
      <span className={cn('truncate leading-4 font-semibold', compact ? 'text-xs' : 'text-sm leading-5', ev.cancelled && 'line-through')}>
        {ev.title}
      </span>
      {ev.subtitle && <span className="truncate text-[11px] leading-4">{ev.subtitle}</span>}
      {ev.location && compact && <span className="truncate text-[11px] leading-4">{ev.location}</span>}
    </button>
  );

  if (loading) {
    return (
      <div className={className}>
        {toolbar}
        <LoadingSkeleton variant={md ? 'table' : 'list'} rows={5} />
      </div>
    );
  }

  // ---------------- MOBILE ----------------
  if (!md) {
    const idx = days.findIndex((d) => d.isSame(selectedDay, 'day'));
    const list = (byDay[idx] ?? []).sort((a, b) => a.s.valueOf() - b.s.valueOf());
    return (
      <div className={className}>
        {toolbar}
        <div className="mb-3 grid grid-cols-7 gap-1" role="tablist" aria-label="Chọn ngày">
          {days.map((d, i) => {
            const active = d.isSame(selectedDay, 'day');
            const today = d.isSame(dayjs(), 'day');
            return (
              <button
                key={i}
                type="button"
                role="tab"
                aria-selected={active}
                onClick={() => setSelectedDay(d)}
                className={cn(
                  'flex flex-col items-center gap-0.5 rounded-lg py-2 transition-colors',
                  active ? 'bg-primary-600 text-white' : today ? 'bg-primary-100 text-primary-800' : 'text-ink-secondary hover:bg-primary-50',
                )}
              >
                <span className="text-[11px] font-medium">{d.format('dd')}</span>
                <span className="tabular text-base font-semibold">{d.format('D')}</span>
                <span
                  className={cn('h-1.5 w-1.5 rounded-full', byDay[i].length ? (active ? 'bg-white' : 'bg-primary-500') : 'bg-transparent')}
                  aria-hidden
                />
              </button>
            );
          })}
        </div>
        {list.length === 0 ? (
          <EmptyState icon={CalendarX2} size="compact" title="Không có lịch" description={selectedDay.format('dddd, DD/MM/YYYY')} />
        ) : (
          <ul className="flex flex-col gap-2">
            {list.map((ev) => (
              <li key={ev.id}>
                <button
                  type="button"
                  onClick={() => onEventClick?.(ev)}
                  className={cn(
                    'flex w-full flex-col gap-1 rounded-[var(--radius-card)] border border-l-4 border-line bg-surface p-3 text-left shadow-[var(--shadow-soft-sm)]',
                    accentBorderClasses[ev.tone ?? 'primary'],
                    ev.cancelled && 'opacity-60',
                  )}
                >
                  <div className="flex items-start justify-between gap-2">
                    <span className={cn('font-semibold text-ink', ev.cancelled && 'line-through')}>{ev.title}</span>
                    {ev.extra}
                  </div>
                  <span className="tabular flex items-center gap-1.5 text-[13px] text-ink-secondary">
                    <Clock size={14} aria-hidden />
                    {fmtTime(ev.s)} – {fmtTime(ev.e)}
                  </span>
                  {ev.location && (
                    <span className="flex items-center gap-1.5 text-[13px] text-ink-secondary">
                      <MapPin size={14} aria-hidden />
                      {ev.location}
                    </span>
                  )}
                  {ev.subtitle && <span className="text-[13px] text-ink-muted">{ev.subtitle}</span>}
                </button>
              </li>
            ))}
          </ul>
        )}
      </div>
    );
  }

  // ---------------- DESKTOP / TABLET ----------------
  const hours = Array.from({ length: endHour - startHour }, (_, i) => startHour + i);
  const gridHeight = hours.length * hourHeight;
  const toY = (d: Dayjs) => ((d.hour() + d.minute() / 60 - startHour) * hourHeight);
  const nowY = toY(now);
  const showNow = now.hour() >= startHour && now.hour() < endHour;

  return (
    <div className={className}>
      {toolbar}
      {/* Một vùng cuộn chung cho tiêu đề + lưới → cột luôn thẳng hàng */}
      <div className="max-h-[680px] overflow-auto rounded-[var(--radius-card)] border border-line bg-surface">
        <div className="min-w-[760px]">
          {/* Hàng tiêu đề ngày */}
          <div className="sticky top-0 z-10 grid grid-cols-[56px_repeat(7,minmax(0,1fr))] border-b border-line bg-primary-50">
            <div />
            {days.map((d, i) => {
              const today = d.isSame(dayjs(), 'day');
              return (
                <div key={i} className="flex flex-col items-center gap-0.5 border-l border-line py-2">
                  <span className={cn('text-xs font-medium', today ? 'text-primary-700' : 'text-ink-muted')}>{d.format('dd')}</span>
                  <span
                    className={cn(
                      'tabular flex h-7 w-7 items-center justify-center rounded-full text-sm font-semibold',
                      today ? 'bg-primary-600 text-white' : 'text-ink',
                    )}
                  >
                    {d.format('D')}
                  </span>
                </div>
              );
            })}
          </div>

          {/* Lưới giờ */}
          <div className="relative grid grid-cols-[56px_repeat(7,minmax(0,1fr))]">
            <div className="relative" style={{ height: gridHeight }}>
              {hours.map((h, i) => (
                <span key={h} className="tabular absolute right-2 -translate-y-1/2 text-[11px] text-ink-muted" style={{ top: i * hourHeight }}>
                  {i === 0 ? '' : `${String(h).padStart(2, '0')}:00`}
                </span>
              ))}
            </div>
            {days.map((d, di) => {
              const placed = layoutDay(byDay[di]);
              const today = d.isSame(dayjs(), 'day');
              return (
                <div
                  key={di}
                  className={cn('relative border-l border-line', today && 'bg-primary-50/40')}
                  style={{
                    height: gridHeight,
                    backgroundImage: `repeating-linear-gradient(to bottom, transparent 0, transparent ${hourHeight - 1}px, var(--color-line) ${hourHeight - 1}px, var(--color-line) ${hourHeight}px)`,
                  }}
                >
                  {placed.map((ev) => {
                    const top = Math.max(0, toY(ev.s));
                    const height = Math.max(24, toY(ev.e) - top - 2);
                    const width = 100 / ev.cols;
                    return (
                      <div
                        key={ev.id}
                        className="absolute px-0.5"
                        style={{ top, height, left: `${ev.col * width}%`, width: `${width}%` }}
                      >
                        {eventButton(ev, true)}
                      </div>
                    );
                  })}
                  {today && showNow && (
                    <div className="pointer-events-none absolute right-0 left-0 z-[5]" style={{ top: nowY }} aria-hidden>
                      <div className="relative h-0.5 bg-danger-600">
                        <span className="absolute -top-1 -left-1 h-2.5 w-2.5 rounded-full bg-danger-600" />
                      </div>
                    </div>
                  )}
                </div>
              );
            })}
          </div>
        </div>
      </div>
    </div>
  );
}
