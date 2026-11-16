import { useEffect, useState, type ReactNode } from 'react';
import dayjs, { type Dayjs } from 'dayjs';
import { CalendarX2, MapPin, TriangleAlert, Video } from 'lucide-react';
import { EmptyState, type Tone } from '@/components/common';
import { accentBorderClasses, softToneClasses } from '@/components/common/tones';
import { useBreakpoint } from '@/hooks/useBreakpoint';
import { cn } from '@/lib/cn';
import { packLanes, SHIFTS, shiftOf, toMin } from './shifts';

/** 1 buổi học đã chuẩn bị để hiển thị */
export interface BoardSession {
  id: string;
  date: string;
  start: string;
  end: string;
  /** Mã lớp */
  title: string;
  /** GV · trợ giảng */
  people: string;
  online: boolean;
  /** Phòng (offline) — online không có */
  room?: string;
  /** Khóa nhóm phòng (chi nhánh + tên phòng) */
  roomKey?: string;
  tone: Tone;
  cancelled?: boolean;
  badge?: ReactNode;
}

function Chip({ s, onClick, dense }: { s: BoardSession; onClick: () => void; dense?: boolean }) {
  return (
    <button
      type="button"
      onClick={onClick}
      title={`${s.start}–${s.end} · ${s.title} · ${s.online ? 'Online' : s.room} · ${s.people}`}
      className={cn(
        'flex w-full flex-col gap-0.5 rounded-md border border-l-[3px] px-2 py-1.5 text-left transition-shadow hover:shadow-[var(--shadow-soft-md)] focus-visible:ring-[3px] focus-visible:ring-primary-500/30 focus-visible:outline-none',
        softToneClasses[s.tone],
        accentBorderClasses[s.tone],
        s.cancelled && 'opacity-55',
      )}
    >
      <span className="tabular flex items-center justify-between gap-1 text-[11px] leading-4">
        <span>
          {s.start}–{s.end}
        </span>
        {s.badge && <span className="flex shrink-0 items-center gap-0.5">{s.badge}</span>}
      </span>
      <span className={cn('truncate text-xs leading-4 font-semibold', s.cancelled && 'line-through')}>{s.title}</span>
      {!dense && (
        <span className="flex items-center gap-1 truncate text-[11px] leading-4">
          {s.online ? <Video size={11} className="shrink-0" aria-hidden /> : <MapPin size={11} className="shrink-0" aria-hidden />}
          <span className="truncate">
            {s.online ? 'Online' : s.room} · {s.people}
          </span>
        </span>
      )}
    </button>
  );
}

// =================================================================
// TUẦN: bảng CA × NGÀY — mỗi ô liệt kê các buổi trong ca (offline trước, online sau)
// =================================================================

export function WeekShiftBoard({ weekStart, sessions, onOpen }: { weekStart: Dayjs; sessions: BoardSession[]; onOpen: (id: string) => void }) {
  const { md } = useBreakpoint();
  const days = Array.from({ length: 7 }, (_, i) => weekStart.add(i, 'day'));
  const today = dayjs().format('YYYY-MM-DD');
  const cell = (date: string, shiftKey: string) =>
    sessions
      .filter((s) => s.date === date && shiftOf(s.start).key === shiftKey)
      .sort((a, b) => Number(a.online) - Number(b.online) || a.start.localeCompare(b.start));

  if (!sessions.length) return <EmptyState icon={CalendarX2} title="Không có buổi học trong tuần" description="Đổi tuần hoặc bỏ bớt bộ lọc." />;

  // Mobile: từng ngày → từng ca
  if (!md) {
    return (
      <div className="flex flex-col gap-3">
        {days.map((d) => {
          const date = d.format('YYYY-MM-DD');
          const any = sessions.some((s) => s.date === date);
          if (!any) return null;
          return (
            <section key={date} className="rounded-[var(--radius-card)] border border-line bg-surface p-3">
              <h3 className={cn('mb-2 text-sm font-semibold first-letter:uppercase', date === today ? 'text-primary-700' : 'text-ink')}>{d.format('dddd, DD/MM')}</h3>
              {SHIFTS.map((sh) => {
                const list = cell(date, sh.key);
                return list.length ? (
                  <div key={sh.key} className="mb-2">
                    <p className="mb-1 flex items-center gap-1 text-xs text-ink-muted">
                      <sh.icon size={13} aria-hidden /> {sh.label}
                    </p>
                    <div className="flex flex-col gap-1.5">
                      {list.map((s) => (
                        <Chip key={s.id} s={s} onClick={() => onOpen(s.id)} />
                      ))}
                    </div>
                  </div>
                ) : null;
              })}
            </section>
          );
        })}
      </div>
    );
  }

  return (
    <div className="overflow-auto rounded-[var(--radius-card)] border border-line bg-surface">
      <div className="grid min-w-[980px]" style={{ gridTemplateColumns: '92px repeat(7, minmax(0, 1fr))' }}>
        {/* Tiêu đề ngày */}
        <div className="sticky top-0 z-10 border-b border-line bg-primary-50" />
        {days.map((d) => {
          const date = d.format('YYYY-MM-DD');
          const isToday = date === today;
          const n = sessions.filter((s) => s.date === date && !s.cancelled).length;
          return (
            <div key={date} className={cn('sticky top-0 z-10 flex flex-col items-center border-b border-l border-line bg-primary-50 py-2', isToday && 'bg-primary-100')}>
              <span className={cn('text-xs font-medium', isToday ? 'text-primary-700' : 'text-ink-muted')}>{d.format('dd')}</span>
              <span className={cn('tabular grid size-7 place-items-center rounded-full text-sm font-semibold', isToday ? 'bg-primary-600 text-white' : 'text-ink')}>{d.format('D')}</span>
              <span className="tabular text-[11px] text-ink-muted">{n ? `${n} buổi` : '—'}</span>
            </div>
          );
        })}

        {/* Các ca */}
        {SHIFTS.map((sh) => (
          <div key={sh.key} className="contents">
            <div className="flex flex-col gap-0.5 border-b border-line px-2 py-2">
              <span className="flex items-center gap-1 text-[13px] font-semibold text-ink">
                <sh.icon size={14} aria-hidden /> {sh.label}
              </span>
              <span className="tabular text-[11px] text-ink-muted">
                {sh.from}–{sh.to}
              </span>
            </div>
            {days.map((d) => {
              const date = d.format('YYYY-MM-DD');
              const list = cell(date, sh.key);
              const off = list.filter((s) => !s.online);
              const on = list.filter((s) => s.online);
              return (
                <div key={date} className={cn('flex min-h-[72px] flex-col gap-1.5 border-b border-l border-line p-1.5', date === today && 'bg-primary-50/40')}>
                  {off.map((s) => (
                    <Chip key={s.id} s={s} onClick={() => onOpen(s.id)} />
                  ))}
                  {on.length > 0 && off.length > 0 && <div className="border-t border-dashed border-line" />}
                  {on.map((s) => (
                    <Chip key={s.id} s={s} onClick={() => onOpen(s.id)} />
                  ))}
                </div>
              );
            })}
          </div>
        ))}
      </div>
    </div>
  );
}

// =================================================================
// NGÀY: lịch ngang theo giờ — Offline: mỗi phòng 1 dòng (nhiều ca / phòng) · Online: các làn tự xếp
// =================================================================

export interface RoomRow {
  key: string;
  label: string;
  hint?: string;
  warning?: string;
}

const START_H = 7;
const END_H = 22;
const SPAN = (END_H - START_H) * 60;
const leftPct = (t: string) => ((Math.max(toMin(t), START_H * 60) - START_H * 60) / SPAN) * 100;
const widthPct = (a: string, b: string) => ((Math.min(toMin(b), END_H * 60) - Math.max(toMin(a), START_H * 60)) / SPAN) * 100;
const LANE_H = 58;

export function DayTimeline({ date, rooms, sessions, showOffline, showOnline, onOpen }: { date: Dayjs; rooms: RoomRow[]; sessions: BoardSession[]; showOffline: boolean; showOnline: boolean; onOpen: (id: string) => void }) {
  const [now, setNow] = useState(() => dayjs());
  useEffect(() => {
    const t = setInterval(() => setNow(dayjs()), 60_000);
    return () => clearInterval(t);
  }, []);
  const isToday = date.isSame(now, 'day');
  const nowPct = leftPct(now.format('HH:mm'));
  const showNow = isToday && now.hour() >= START_H && now.hour() < END_H;
  const hours = Array.from({ length: END_H - START_H + 1 }, (_, i) => START_H + i);

  const offline = sessions.filter((s) => !s.online);
  const online = packLanes(sessions.filter((s) => s.online));
  const onlineLanes = Math.max(1, ...online.map((s) => s.lane + 1));

  const Bar = ({ s, top, height, conflict }: { s: BoardSession; top: number; height: number; conflict?: boolean }) => (
    <div className="absolute px-0.5" style={{ left: `${leftPct(s.start)}%`, width: `${widthPct(s.start, s.end)}%`, top, height }}>
      <button
        type="button"
        onClick={() => onOpen(s.id)}
        title={`${s.start}–${s.end} · ${s.title} · ${s.people}`}
        className={cn(
          'flex h-full w-full flex-col justify-center overflow-hidden rounded-md border border-l-[3px] px-2 text-left hover:shadow-[var(--shadow-soft-md)] focus-visible:ring-[3px] focus-visible:ring-primary-500/30 focus-visible:outline-none',
          softToneClasses[s.tone],
          accentBorderClasses[s.tone],
          s.cancelled && 'opacity-55',
          conflict && 'ring-2 ring-danger-600',
        )}
      >
        <span className="tabular flex items-center justify-between gap-1 text-[11px] leading-4">
          <span className="truncate">
            {s.start}–{s.end}
          </span>
          {s.badge && <span className="flex shrink-0 items-center gap-0.5">{s.badge}</span>}
        </span>
        <span className={cn('truncate text-xs leading-4 font-semibold', s.cancelled && 'line-through')}>{s.title}</span>
        <span className="truncate text-[11px] leading-4">{s.people}</span>
      </button>
    </div>
  );

  const Grid = () => (
    <div className="pointer-events-none absolute inset-0" aria-hidden>
      {SHIFTS.map((sh, i) => (
        <div key={sh.key} className={cn('absolute inset-y-0', i % 2 ? 'bg-page/70' : '')} style={{ left: `${leftPct(sh.from)}%`, width: `${widthPct(sh.from, sh.to)}%` }} />
      ))}
      {hours.slice(1, -1).map((h) => (
        <div key={h} className="absolute inset-y-0 border-l border-line/70" style={{ left: `${((h - START_H) / (END_H - START_H)) * 100}%` }} />
      ))}
      {showNow && <div className="absolute inset-y-0 z-[5] w-0.5 bg-danger-600" style={{ left: `${nowPct}%` }} />}
    </div>
  );

  const Section = ({ title, icon, children }: { title: string; icon: ReactNode; children: ReactNode }) => (
    <>
      <div className="sticky left-0 col-span-2 flex items-center gap-1.5 border-y border-line bg-page px-3 py-1.5 text-xs font-semibold tracking-wide text-ink-secondary uppercase">
        {icon} {title}
      </div>
      {children}
    </>
  );

  return (
    <div className="overflow-auto rounded-[var(--radius-card)] border border-line bg-surface">
      <div className="grid min-w-[1100px]" style={{ gridTemplateColumns: '150px 1fr' }}>
        {/* Trục giờ + ca */}
        <div className="sticky top-0 left-0 z-20 border-b border-line bg-primary-50" />
        <div className="sticky top-0 z-10 border-b border-line bg-primary-50">
          <div className="relative h-6">
            {SHIFTS.map((sh) => (
              <span key={sh.key} className="absolute top-1 flex -translate-x-0 items-center gap-1 pl-1.5 text-[11px] font-semibold text-primary-800" style={{ left: `${leftPct(sh.from)}%` }}>
                <sh.icon size={12} aria-hidden /> {sh.label}
              </span>
            ))}
          </div>
          <div className="relative h-5">
            {hours.map((h) => (
              <span key={h} className="tabular absolute -translate-x-1/2 text-[11px] text-ink-muted" style={{ left: `${((h - START_H) / (END_H - START_H)) * 100}%` }}>
                {h === START_H || h === END_H ? '' : `${h}h`}
              </span>
            ))}
          </div>
        </div>

        {showOffline && (
          <Section title="Offline — theo phòng" icon={<MapPin size={13} aria-hidden />}>
            {rooms.length === 0 && <div className="col-span-2 px-3 py-4 text-[13px] text-ink-muted">Không có phòng.</div>}
            {rooms.map((r) => {
              const list = packLanes(offline.filter((s) => s.roomKey === r.key));
              const lanes = Math.max(1, ...list.map((s) => s.lane + 1));
              const busy = list.filter((s) => !s.cancelled).reduce((n, s) => n + toMin(s.end) - toMin(s.start), 0);
              return (
                <div key={r.key} className="contents">
                  <div className={cn('sticky left-0 z-[6] flex flex-col justify-center border-b border-line bg-surface px-3', r.warning && 'bg-warning-50')}>
                    <span className="flex items-center gap-1 text-[13px] font-semibold text-ink">
                      {r.label}
                      {lanes > 1 && <TriangleAlert size={13} className="text-danger-600" aria-label="Trùng phòng" />}
                    </span>
                    <span className={cn('text-[11px]', r.warning ? 'text-warning-700' : 'text-ink-muted')}>{r.warning ?? `${r.hint ?? ''}${busy ? ` · ${(busy / 60).toFixed(1)}h` : ' · trống'}`}</span>
                  </div>
                  <div className="relative border-b border-line" style={{ height: lanes * LANE_H + 8 }}>
                    <Grid />
                    {list.map((s) => (
                      <Bar key={s.id} s={s} top={4 + s.lane * LANE_H} height={LANE_H - 4} conflict={lanes > 1 && !s.cancelled} />
                    ))}
                  </div>
                </div>
              );
            })}
          </Section>
        )}

        {showOnline && (
          <Section title={`Online — ${online.length} buổi`} icon={<Video size={13} aria-hidden />}>
            <div className="sticky left-0 z-[6] flex flex-col justify-center border-b border-line bg-success-50/60 px-3">
              <span className="text-[13px] font-semibold text-ink">Học online</span>
              <span className="text-[11px] text-ink-muted">Không cần phòng · {onlineLanes} lớp cùng lúc tối đa</span>
            </div>
            <div className="relative border-b border-line bg-success-50/20" style={{ height: onlineLanes * LANE_H + 8 }}>
              <Grid />
              {online.map((s) => (
                <Bar key={s.id} s={s} top={4 + s.lane * LANE_H} height={LANE_H - 4} />
              ))}
              {!online.length && <span className="absolute inset-0 grid place-items-center text-[13px] text-ink-muted">Không có buổi online</span>}
            </div>
          </Section>
        )}
      </div>
    </div>
  );
}
