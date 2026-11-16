import { useEffect, useMemo, useState, type ReactNode } from 'react';
import { Drawer, Tabs } from 'antd';
import dayjs, { type Dayjs } from 'dayjs';
import { CalendarDays, CheckCircle2, Info, MessageSquareWarning, School, TriangleAlert, Users, Wrench } from 'lucide-react';
import { useNavigate } from 'react-router-dom';
import { Button, EmptyState, StatusTag, Timetable, type TimetableEvent } from '@/components/common';
import { useBreakpoint } from '@/hooks/useBreakpoint';
import { cn } from '@/lib/cn';
import { fmtDate } from '@/lib/date';
import { getBranches } from '@/features/system/useBranches';
import { useClassStore } from '../classes/classStore';
import { CLASS_STATUS, teacherNames } from '../classes/constants';
import { sessionTeachers } from '../classes/sessionUtils';
import { EQUIPMENT, isOpenIssue, occupancyTone, OPEN_HOURS, PEAK, ROOM_STATUS, ROOM_TYPES } from './constants';
import { bookingsByRoom, occupancyOf, roomConflicts, roomKey, weekStartOf } from './roomUsage';
import { RoomIssueList } from './RoomIssueList';
import { useRoomStore } from './roomStore';
import { useRoomIssues } from './useRoomIssues';
import type { RoomStatus } from './types';

export type RoomTab = 'info' | 'schedule' | 'classes' | 'issues';

interface Props {
  roomId: string | null;
  initialTab?: RoomTab;
  editable: boolean;
  onClose: () => void;
  onEdit: (roomId: string) => void;
  onChangeStatus: (roomId: string, status: RoomStatus) => void;
}

const label = (Icon: typeof Info, text: string) => (
  <span className="inline-flex items-center gap-1.5">
    <Icon size={16} aria-hidden /> {text}
  </span>
);

/** Thanh tỷ lệ lấp đầy */
export function OccupancyBar({ pct, className }: { pct: number; className?: string }) {
  const tone = occupancyTone(pct);
  const color = { danger: 'bg-danger-600', success: 'bg-success-600', info: 'bg-primary-500', neutral: 'bg-ink-muted', warning: 'bg-warning-600', primary: 'bg-primary-600' }[tone];
  return (
    <div className={cn('h-1.5 w-full overflow-hidden rounded-full bg-disabled', className)} role="presentation">
      <div className={cn('h-full rounded-full', color)} style={{ width: `${Math.max(pct, pct > 0 ? 3 : 0)}%` }} />
    </div>
  );
}

/** Chi tiết phòng: Thông tin · Lịch sử dụng (theo tuần) · Lớp sử dụng phòng */
export function RoomDetailDrawer({ roomId, initialTab = 'info', editable, onClose, onEdit, onChangeStatus }: Props) {
  const { isMobile } = useBreakpoint();
  const navigate = useNavigate();
  const room = useRoomStore((s) => s.rooms.find((r) => r.id === roomId));
  const allIssues = useRoomStore((s) => s.issues);
  const issues = useMemo(() => allIssues.filter((i) => i.roomId === roomId), [allIssues, roomId]);
  const openIssues = issues.filter(isOpenIssue);
  const severe = openIssues.filter((i) => i.severity === 'high');
  const issueActions = useRoomIssues();
  const classes = useClassStore((s) => s.classes);
  const sessions = useClassStore((s) => s.sessions);
  const [tab, setTab] = useState<RoomTab>(initialTab);
  const [week, setWeek] = useState<Dayjs>(() => dayjs());

  useEffect(() => {
    if (roomId) {
      setTab(initialTab);
      setWeek(dayjs());
    }
  }, [roomId, initialTab]);

  const bookings = useMemo(() => (room ? (bookingsByRoom(classes, sessions).get(roomKey(room.branchId, room.name)) ?? []) : []), [room, classes, sessions]);
  const conflicts = useMemo(() => roomConflicts(bookings), [bookings]);
  const occ = useMemo(() => occupancyOf(bookings, weekStartOf(week)), [bookings, week]);
  const thisWeek = useMemo(() => occupancyOf(bookings, weekStartOf(dayjs())), [bookings]);

  /** Lớp từng / đang dùng phòng (kể cả chỉ mượn vài buổi) */
  const usedBy = useMemo(() => {
    const m = new Map<string, { cls: (typeof classes)[number]; count: number; isDefault: boolean }>();
    for (const b of bookings) {
      const cur = m.get(b.cls.id);
      if (cur) cur.count++;
      else m.set(b.cls.id, { cls: b.cls, count: 1, isDefault: b.cls.room === room?.name });
    }
    // Lớp mới mở chưa có buổi
    classes
      .filter((c) => room && !c.online && c.branchId === room.branchId && c.room === room.name && !m.has(c.id))
      .forEach((c) => m.set(c.id, { cls: c, count: 0, isDefault: true }));
    const order = ['ongoing', 'enrolling', 'paused', 'finished', 'cancelled'];
    return [...m.values()].sort((a, b) => order.indexOf(a.cls.status) - order.indexOf(b.cls.status) || Number(b.isDefault) - Number(a.isDefault));
  }, [bookings, classes, room]);

  const events: TimetableEvent[] = useMemo(
    () =>
      bookings.map(({ session: s, cls }) => ({
        id: s.id,
        start: `${s.date}T${s.startTime}`,
        end: `${s.date}T${s.endTime}`,
        title: cls.code,
        subtitle: `Buổi ${s.no} · ${teacherNames(sessionTeachers(s, cls))}`,
        location: s.room && s.room !== cls.room ? 'Đổi phòng cho buổi này' : undefined,
        tone: s.status === 'makeup' ? 'warning' : s.kind === 'exam' ? 'danger' : s.status === 'completed' ? 'neutral' : 'primary',
        cancelled: s.status === 'cancelled',
        extra: <StatusTag group="session" status={s.status} size="sm" />,
      })),
    [bookings],
  );

  if (!room) return <Drawer open={false} onClose={onClose} />;

  const st = ROOM_STATUS[room.status];
  const type = ROOM_TYPES[room.type];
  const capacityIssues = usedBy.filter((u) => u.isDefault && ['enrolling', 'ongoing', 'paused'].includes(u.cls.status) && u.cls.maxStudents > room.capacity);

  const info: [string, ReactNode][] = [
    ['Chi nhánh', getBranches(false).find((b) => b.id === room.branchId)?.name],
    ['Tầng / khu', room.floor ?? '—'],
    ['Loại phòng', type.label],
    ['Sức chứa', `${room.capacity} chỗ`],
    ['Diện tích', room.area ? `${room.area} m²` : '—'],
    ['Ghi chú', room.note ?? '—'],
  ];

  return (
    <Drawer
      open={Boolean(roomId)}
      onClose={onClose}
      width={isMobile ? '100%' : 880}
      destroyOnHidden
      styles={{ body: { paddingTop: 0 } }}
      title={
        <div className="flex min-w-0 flex-col gap-1 pr-2">
          <div className="flex flex-wrap items-center gap-2">
            <span className="truncate text-base font-semibold">{room.name}</span>
            <StatusTag tone={st.tone} icon={st.icon} size="sm">
              {st.label}
            </StatusTag>
          </div>
          <span className="text-xs font-normal text-ink-muted">
            {type.label} · {room.capacity} chỗ · {getBranches(false).find((b) => b.id === room.branchId)?.name}
            {room.floor && ` · ${room.floor}`}
          </span>
        </div>
      }
      extra={
        <div className="flex gap-1.5">
          <Button icon={MessageSquareWarning} size="sm" tone="danger" variant="soft" collapseOnMobile onClick={() => issueActions.report(room.id)}>
            Báo hỏng
          </Button>
          {editable && (
          <div className="flex gap-1.5">
            {room.status === 'active' ? (
              <Button icon={Wrench} size="sm" tone="neutral" variant="outline" collapseOnMobile onClick={() => onChangeStatus(room.id, 'maintenance')}>
                Bảo trì
              </Button>
            ) : (
              <Button icon={CheckCircle2} size="sm" tone="success" variant="soft" collapseOnMobile onClick={() => onChangeStatus(room.id, 'active')}>
                Dùng lại
              </Button>
            )}
            <Button action="edit" size="sm" collapseOnMobile onClick={() => onEdit(room.id)} />
          </div>
          )}
        </div>
      }
    >
      {/* Cảnh báo luôn hiện trên đầu */}
      <div className="flex flex-col gap-2 pt-4 empty:hidden">
        {room.status === 'maintenance' && (
          <Alert tone="warning" icon={Wrench}>
            <strong>Đang bảo trì</strong>
            {room.maintenanceUntil && ` đến ${fmtDate(room.maintenanceUntil)}`}
            {room.maintenanceReason && ` — ${room.maintenanceReason}`}
          </Alert>
        )}
        {severe.length > 0 && (
          <Alert tone="danger" icon={MessageSquareWarning}>
            <strong>Hỏng nghiêm trọng chưa sửa:</strong> {severe.map((i) => i.title).join('; ')} —{' '}
            <button type="button" className="font-medium underline" onClick={() => setTab('issues')}>
              xem chi tiết
            </button>
          </Alert>
        )}
        {conflicts.length > 0 && (
          <Alert tone="danger" icon={TriangleAlert}>
            <strong>{conflicts.length} lần trùng lịch sắp tới:</strong>{' '}
            {conflicts
              .slice(0, 3)
              .map(([a, b]) => `${dayjs(a.session.date).format('DD/MM')} ${a.cls.code} ${a.session.startTime}–${a.session.endTime} ↔ ${b.cls.code} ${b.session.startTime}–${b.session.endTime}`)
              .join('; ')}
          </Alert>
        )}
        {capacityIssues.map((u) => (
          <Alert key={u.cls.id} tone="warning" icon={Users}>
            Lớp <strong>{u.cls.code}</strong> nhận tối đa {u.cls.maxStudents} học viên nhưng phòng chỉ có {room.capacity} chỗ.
          </Alert>
        ))}
      </div>

      <Tabs
        activeKey={tab}
        onChange={(k) => setTab(k as RoomTab)}
        items={[
          {
            key: 'info',
            label: label(Info, 'Thông tin'),
            children: (
              <div className="flex flex-col gap-5">
                <dl className="grid grid-cols-1 gap-x-4 gap-y-2 text-sm sm:grid-cols-[140px_1fr]">
                  {info.map(([k, v]) => (
                    <div key={k} className="contents">
                      <dt className="text-ink-muted">{k}</dt>
                      <dd className="min-w-0 break-words text-ink">{v}</dd>
                    </div>
                  ))}
                </dl>
                <section>
                  <h3 className="mb-2 text-sm font-semibold text-ink">Thiết bị</h3>
                  {room.equipment.length ? (
                    <ul className="flex flex-wrap gap-2">
                      {room.equipment.map((e) => {
                        const Icon = EQUIPMENT[e].icon;
                        return (
                          <li key={e} className="inline-flex items-center gap-1.5 rounded-lg border border-line bg-page px-2.5 py-1 text-[13px] text-ink-secondary">
                            <Icon size={15} aria-hidden /> {EQUIPMENT[e].label}
                          </li>
                        );
                      })}
                    </ul>
                  ) : (
                    <p className="text-[13px] text-ink-muted">Chưa khai báo thiết bị.</p>
                  )}
                </section>
                <section className="grid grid-cols-1 gap-3 sm:grid-cols-2">
                  <Meter title="Lấp đầy tuần này" pct={thisWeek.pct} hint={`${thisWeek.hours} / ${thisWeek.openHours} giờ mở cửa (${OPEN_HOURS.start}–${OPEN_HOURS.end})`} />
                  <Meter title="Khung cao điểm" pct={thisWeek.peakPct} hint={`${PEAK.start}–${PEAK.end} mỗi ngày`} />
                </section>
              </div>
            ),
          },
          {
            key: 'schedule',
            label: label(CalendarDays, 'Lịch sử dụng'),
            children: (
              <Timetable
                events={events}
                week={week}
                onWeekChange={setWeek}
                startHour={8}
                endHour={22}
                hourHeight={44}
                toolbarExtra={
                  <span className="text-[13px] text-ink-secondary">
                    Tuần này: <strong className="tabular text-ink">{occ.sessions}</strong> buổi · <strong className="tabular text-ink">{occ.hours}h</strong> · lấp đầy{' '}
                    <strong className="tabular text-ink">{occ.pct}%</strong>
                  </span>
                }
              />
            ),
          },
          {
            key: 'issues',
            label: label(Wrench, `Hư hỏng${openIssues.length ? ` (${openIssues.length})` : ''}`),
            children: (
              <RoomIssueList issues={issues} editable={editable} onStart={issueActions.start} onResolve={issueActions.resolve} onReopen={issueActions.reopen} />
            ),
          },
          {
            key: 'classes',
            label: label(School, `Lớp sử dụng (${usedBy.length})`),
            children: usedBy.length ? (
              <ul className="flex flex-col gap-2">
                {usedBy.map(({ cls, count, isDefault }) => {
                  const cs = CLASS_STATUS[cls.status];
                  return (
                    <li key={cls.id}>
                      <button
                        type="button"
                        onClick={() => navigate(`/admin/academic/classes/${cls.id}`)}
                        className="flex w-full flex-wrap items-center justify-between gap-2 rounded-[var(--radius-card)] border border-line bg-surface px-3.5 py-3 text-left transition-colors hover:border-primary-300 hover:bg-primary-50/40"
                      >
                        <div className="min-w-0">
                          <p className="font-medium text-ink">
                            <span className="font-mono text-primary-700">{cls.code}</span> <span className="text-ink-secondary">· {cls.name}</span>
                          </p>
                          <p className="text-xs text-ink-muted">
                            {teacherNames(cls.teacherIds)} · {cls.startTime}–{cls.endTime} · tối đa {cls.maxStudents} HV ·{' '}
                            {isDefault ? `phòng mặc định · ${count} buổi` : `mượn ${count} buổi (đổi phòng)`}
                          </p>
                        </div>
                        <StatusTag tone={cs.tone} icon={cs.icon} size="sm">
                          {cs.label}
                        </StatusTag>
                      </button>
                    </li>
                  );
                })}
              </ul>
            ) : (
              <EmptyState size="compact" icon={School} title="Chưa có lớp nào dùng phòng" description="Chọn phòng khi mở lớp ở Đào tạo → Lớp học." />
            ),
          },
        ]}
      />
      {issueActions.dialogs}
    </Drawer>
  );
}

function Alert({ tone, icon: Icon, children }: { tone: 'warning' | 'danger'; icon: typeof Info; children: ReactNode }) {
  return (
    <div
      className={cn(
        'flex gap-2.5 rounded-lg border px-3 py-2.5 text-[13px]',
        tone === 'danger' ? 'border-danger-100 bg-danger-50 text-danger-700' : 'border-warning-100 bg-warning-50 text-warning-700',
      )}
    >
      <Icon size={16} className="mt-0.5 shrink-0" aria-hidden />
      <p>{children}</p>
    </div>
  );
}

function Meter({ title, pct, hint }: { title: string; pct: number; hint: string }) {
  return (
    <div className="rounded-[var(--radius-card)] border border-line bg-surface p-3.5">
      <div className="mb-2 flex items-baseline justify-between gap-2">
        <span className="text-[13px] text-ink-secondary">{title}</span>
        <span className="tabular text-lg font-semibold text-ink">{pct}%</span>
      </div>
      <OccupancyBar pct={pct} />
      <p className="mt-1.5 text-xs text-ink-muted">{hint}</p>
    </div>
  );
}
