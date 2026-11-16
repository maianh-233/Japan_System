import { useMemo, useState } from 'react';
import dayjs, { type Dayjs } from 'dayjs';
import {
  BadgeCheck,
  CalendarDays,
  CalendarRange,
  CheckCircle2,
  ChevronLeft,
  ChevronRight,
  CircleDashed,
  HeartPulse,
  MapPin,
  PauseCircle,
  Siren,
  Radio,
  UserRoundCog,
  Video,
} from 'lucide-react';
import { Button, DateInput, PageHeader, Select, StatCard, StatusTag, useConfirm, useNotify, type Tone } from '@/components/common';
import { useClassStore } from '@/features/academic/classes/classStore';
import { TEACHERS, ASSISTANTS, teacherName } from '@/features/academic/classes/constants';
import { SessionDetailDrawer } from '@/features/academic/classes/detail/SessionDetailDrawer';
import { SessionScheduleModal, type ScheduleMode } from '@/features/academic/classes/detail/SessionScheduleModal';
import { notStarted, sessionRoom } from '@/features/academic/classes/sessionUtils';
import type { ClassInfo, ClassSession } from '@/features/academic/classes/types';
import { useRoomStore } from '@/features/academic/rooms/roomStore';
import { roomKey } from '@/features/academic/rooms/roomUsage';
import { DayTimeline, WeekShiftBoard, type BoardSession, type RoomRow } from '@/features/academic/sessions/SessionBoards';
import { weekStartOf } from '@/features/academic/rooms/roomUsage';
import { IncidentModal } from '@/features/academic/sessions/IncidentModal';
import { confirmChecklist, isPaused, RUN_STATE, runState, staffOf, type RunState } from '@/features/academic/sessions/sessionOps';
import { Modal } from 'antd';
import { useStudentStore } from '@/features/student/studentStore';
import { useAuth } from '@/hooks/useAuth';
import { cn } from '@/lib/cn';
import { fmtDate } from '@/lib/date';
import { getBranches } from '@/features/system/useBranches';
import { ALL_BRANCHES } from '@/types/auth';

type View = 'week' | 'day';
type Mode = 'all' | 'offline' | 'online';
type Quick = 'all' | 'live' | 'awaiting' | 'incident' | 'cancelled';

const MODE_TONE: Record<'offline' | 'online', Tone> = { offline: 'primary', online: 'success' };

interface Item {
  s: ClassSession;
  cls: ClassInfo;
  state: RunState;
}

/** Buổi học — lịch tuần / ngày của toàn bộ lớp trong chi nhánh, xác nhận buổi đã diễn ra, xử lý sự cố GV. DỮ LIỆU GIẢ. */
export default function SessionsPage() {
  const { user, branchId, currentBranch, can } = useAuth();
  const notify = useNotify();
  const { confirm, confirmDialog } = useConfirm();
  const classes = useClassStore((s) => s.classes);
  const sessions = useClassStore((s) => s.sessions);
  const updateSession = useClassStore((s) => s.updateSession);
  const enrollments = useStudentStore((s) => s.enrollments);
  const rooms = useRoomStore((s) => s.rooms);
  const editable = can('academic.sessions');

  const [view, setView] = useState<View>('week');
  const [date, setDate] = useState<Dayjs>(() => dayjs());
  const [mode, setMode] = useState<Mode>('all');
  const [quick, setQuick] = useState<Quick>('all');
  const [teacher, setTeacher] = useState<string>();
  const [classId, setClassId] = useState<string>();
  const [openId, setOpenId] = useState<string | null>(null);
  const [schedule, setSchedule] = useState<{ cls: ClassInfo; mode: ScheduleMode } | null>(null);
  const [incident, setIncident] = useState<string | null>(null);
  const [showAwaiting, setShowAwaiting] = useState(true);
  const [picking, setPicking] = useState(false);

  const now = dayjs();
  const inBranch = (c: ClassInfo) => branchId === ALL_BRANCHES || c.branchId === branchId;
  const scopeClasses = useMemo(() => classes.filter(inBranch), [classes, branchId]); // eslint-disable-line react-hooks/exhaustive-deps

  // Tất cả buổi trong chi nhánh (đã gắn lớp + trạng thái vận hành)
  const all: Item[] = useMemo(() => {
    const byId = new Map(scopeClasses.map((c) => [c.id, c]));
    return sessions.flatMap((s) => {
      const cls = byId.get(s.classId);
      return cls ? [{ s, cls, state: runState(s, now) }] : [];
    });
  }, [sessions, scopeClasses]); // eslint-disable-line react-hooks/exhaustive-deps

  const match = (it: Item) =>
    (mode === 'all' || (mode === 'online') === Boolean(it.cls.online)) &&
    (!classId || it.cls.id === classId) &&
    (!teacher || staffOf(it.s, it.cls).some((x) => x.id === teacher)) &&
    (quick === 'all' || (quick === 'incident' ? Boolean(it.s.incident) : it.state === quick));

  const filtered = useMemo(() => all.filter(match), [all, mode, classId, teacher, quick]); // eslint-disable-line react-hooks/exhaustive-deps

  // Số liệu
  const today = now.format('YYYY-MM-DD');
  const stats = useMemo(() => {
    const todays = all.filter((x) => x.s.date === today && x.s.status !== 'cancelled');
    const awaiting = all.filter((x) => x.state === 'awaiting');
    return {
      today: todays.length,
      todayOnline: todays.filter((x) => x.cls.online).length,
      live: all.filter((x) => x.state === 'live').length,
      awaiting: awaiting.length,
      missingCheck: awaiting.filter((x) => !confirmChecklist(x.s, x.cls)[0].ok || !confirmChecklist(x.s, x.cls)[1].ok).length,
      incidents: all.filter((x) => x.s.incident && dayjs(x.s.date).isAfter(now.subtract(7, 'day'))).length,
    };
  }, [all, today]); // eslint-disable-line react-hooks/exhaustive-deps

  const awaitingList = useMemo(
    () => all.filter((x) => x.state === 'awaiting' && match({ ...x, state: 'awaiting' })).sort((a, b) => a.s.date.localeCompare(b.s.date) || a.s.startTime.localeCompare(b.s.startTime)),
    [all, mode, classId, teacher], // eslint-disable-line react-hooks/exhaustive-deps
  );
  const readyToConfirm = awaitingList.filter((x) => !isPaused(x.s) && confirmChecklist(x.s, x.cls).slice(0, 3).every((c) => c.ok));

  // ---------- Hiển thị 1 buổi ----------
  const badgeOf = (it: Item) => (
    <>
      {isPaused(it.s) ? (
        <PauseCircle size={12} className="animate-pulse text-danger-600" aria-label="Đang tạm dừng" />
      ) : (
        it.s.incident && <HeartPulse size={12} className="text-danger-600" aria-label="Sự cố GV" />
      )}
      {it.s.teacherIds && <UserRoundCog size={12} className="text-warning-700" aria-label="Dạy thay" />}
      {it.state === 'live' && <Radio size={12} className="animate-pulse text-danger-600" aria-label="Đang diễn ra" />}
      {it.state === 'awaiting' && <CircleDashed size={12} className="text-warning-700" aria-label="Chờ xác nhận" />}
      {it.state === 'confirmed' && <CheckCircle2 size={12} className="text-success-700" aria-label="Đã xác nhận" />}
    </>
  );
  const subtitleOf = (it: Item) => {
    const staff = staffOf(it.s, it.cls);
    const lead = staff.filter((x) => x.role !== 'assistant').map((x) => teacherName(x.id).split(' ').slice(-2).join(' '));
    const ta = staff.filter((x) => x.role === 'assistant').map((x) => teacherName(x.id).split(' ').slice(-1)[0]);
    return `${lead.join(', ')}${ta.length ? ` · TG ${ta.join(', ')}` : ''}`;
  };
  const toneOf = (it: Item): Tone => (it.s.status === 'cancelled' ? 'neutral' : it.s.status === 'makeup' || it.s.origin === 'makeup' ? 'warning' : MODE_TONE[it.cls.online ? 'online' : 'offline']);
  const titleOf = (it: Item) => `${it.cls.code}${it.s.status === 'makeup' || it.s.origin === 'makeup' ? ' · Bù' : it.s.origin === 'extra' ? ' · Thêm' : ''}`;

  /** Buổi học → dữ liệu hiển thị (dùng chung cho bảng tuần + lịch ngày) */
  const toBoard = (it: Item): BoardSession => {
    const room = sessionRoom(it.s, it.cls);
    return {
      id: it.s.id,
      date: it.s.date,
      start: it.s.startTime,
      end: it.s.endTime,
      title: titleOf(it),
      people: subtitleOf(it),
      online: Boolean(it.cls.online),
      room: room ? `${room}${branchId === ALL_BRANCHES ? ` · ${getBranches(false).find((b) => b.id === it.cls.branchId)?.name}` : ''}` : undefined,
      roomKey: room ? roomKey(it.cls.branchId, room) : undefined,
      tone: toneOf(it),
      cancelled: it.s.status === 'cancelled',
      badge: badgeOf(it),
    };
  };
  const weekStart = weekStartOf(date);
  const weekEnd = weekStart.add(7, 'day').format('YYYY-MM-DD');
  const weekSessions = filtered.filter((it) => it.s.date >= weekStart.format('YYYY-MM-DD') && it.s.date < weekEnd).map(toBoard);

  // ---------- Lịch ngày: phòng (offline) + online ----------
  const dayStr = date.format('YYYY-MM-DD');
  const dayItems = filtered.filter((it) => it.s.date === dayStr);
  const daySessions = dayItems.map(toBoard);
  const roomRows: RoomRow[] = useMemo(() => {
    const branchName = (id: string) => getBranches(false).find((b) => b.id === id)?.name ?? id;
    const rows = new Map<string, RoomRow>();
    rooms
      .filter((r) => r.status !== 'inactive' && (branchId === ALL_BRANCHES || r.branchId === branchId))
      .sort((a, b) => a.branchId.localeCompare(b.branchId) || a.name.localeCompare(b.name, 'vi', { numeric: true }))
      .forEach((r) =>
        rows.set(roomKey(r.branchId, r.name), {
          key: roomKey(r.branchId, r.name),
          label: r.name,
          hint: `${r.capacity} chỗ${branchId === ALL_BRANCHES ? ` · ${branchName(r.branchId)}` : ''}`,
          warning: r.status === 'maintenance' ? 'Đang bảo trì' : undefined,
        }),
      );
    daySessions.forEach((x) => {
      if (x.roomKey && !rows.has(x.roomKey)) rows.set(x.roomKey, { key: x.roomKey, label: x.room ?? '—', hint: 'Ngoài danh sách phòng' });
    });
    return [...rows.values()];
  }, [rooms, branchId, daySessions]); // eslint-disable-line react-hooks/exhaustive-deps

  // ---------- Thao tác ----------
  const opened = openId ? all.find((x) => x.s.id === openId) : undefined;
  const incidentItem = incident ? all.find((x) => x.s.id === incident) : undefined;

  const cancel = async (s: ClassSession) => {
    const cls = classes.find((c) => c.id === s.classId);
    const count = enrollments.filter((e) => e.classId === s.classId && e.status === 'studying').length;
    const res = await confirm({
      title: `Hủy ${s.no ? `buổi ${s.no}` : 'buổi học thêm'} lớp ${cls?.code} (${fmtDate(s.date)} · ${s.startTime})?`,
      consequences: ['Không tính vào tiến độ và lương giáo viên', `${count} học viên nhận thông báo nghỉ học (Cổng học viên + Zalo)`, 'Có thể xếp học bù ngay sau bước này'],
      tone: 'warning',
      confirmAction: 'reject',
      confirmText: 'Hủy buổi',
      requireReason: { label: 'Lý do hủy', placeholder: 'VD: GV ốm, mất điện, nghỉ lễ đột xuất...' },
    });
    if (!res) return;
    if (!notStarted(s)) {
      notify.toast.error('Buổi học đã bắt đầu — dùng "Báo sự cố GV" thay vì hủy');
      return;
    }
    updateSession(s.id, { status: 'cancelled', note: res.reason, cancelledBy: { name: user?.fullName ?? 'Giáo vụ', at: new Date().toISOString() } });
    notify.info({ title: 'Đã hủy buổi học', description: 'Bấm "Xếp học bù" trong chi tiết buổi để xếp lịch bù.' });
  };

  const confirmReady = async () => {
    const ok = await confirm({
      title: `Xác nhận ${readyToConfirm.length} buổi đủ điều kiện?`,
      description: 'Chỉ gồm các buổi GV đã check-in / check-out và đã điểm danh học viên. Buổi thiếu thông tin cần mở ra kiểm tra từng buổi.',
      tone: 'primary',
      confirmAction: 'approve',
      confirmText: 'Xác nhận tất cả',
    });
    if (!ok) return;
    const by = user ? `${user.fullName} (${user.roleName})` : 'Nhân viên';
    readyToConfirm.forEach((x) => updateSession(x.s.id, { confirmation: { by, at: new Date().toISOString() } }));
    notify.success({ title: `Đã xác nhận ${readyToConfirm.length} buổi học` });
  };

  const teacherOptions = [
    ...TEACHERS.filter((t) => branchId === ALL_BRANCHES || t.branchIds.includes(branchId)).map((t) => ({ value: t.id, label: t.name })),
    ...ASSISTANTS.filter((t) => branchId === ALL_BRANCHES || t.branchIds.includes(branchId)).map((t) => ({ value: t.id, label: `${t.name} (trợ giảng)` })),
  ];
  const quickCounts: Record<Quick, number> = {
    all: all.length,
    live: stats.live,
    awaiting: stats.awaiting,
    incident: all.filter((x) => x.s.incident).length,
    cancelled: all.filter((x) => x.state === 'cancelled').length,
  };

  return (
    <>
      <PageHeader
        title="Buổi học"
        subtitle={`Lịch dạy toàn trung tâm, xác nhận buổi đã diễn ra, xử lý sự cố giáo viên · ${currentBranch ? `Chi nhánh ${currentBranch.name}` : 'Toàn hệ thống'}`}
        breadcrumb={[{ title: 'Đào tạo' }, { title: 'Buổi học' }]}
        actions={
          <>
            <Button action="export" variant="outline" collapseOnMobile onClick={() => notify.toast.info('Xuất Excel (giả lập)')} />
            {editable && (
              <Button icon={Siren} tone="danger" onClick={() => setPicking(true)}>
                Ghi nhận sự cố
              </Button>
            )}
          </>
        }
      />

      {/* Lớp đang tạm dừng vì sự cố */}
      {all
        .filter((x) => isPaused(x.s))
        .map((x) => (
          <button
            key={x.s.id}
            type="button"
            onClick={() => setOpenId(x.s.id)}
            className="mb-3 flex w-full flex-wrap items-center gap-2 rounded-[var(--radius-card)] border border-danger-100 border-l-4 border-l-danger-600 bg-danger-50 px-4 py-3 text-left text-[13px] text-danger-700"
          >
            <PauseCircle size={18} className="animate-pulse" aria-hidden />
            <strong>
              {x.cls.code} đang tạm dừng từ {x.s.incident!.at}
            </strong>
            <span className="text-ink">— {x.cls.online ? 'Online' : sessionRoom(x.s, x.cls)} · {subtitleOf(x)}</span>
            <span className="ml-auto font-medium underline">Xử lý tiếp</span>
          </button>
        ))}

      <div className="mb-4 grid grid-cols-2 gap-3 lg:grid-cols-4">
        <StatCard label="Buổi hôm nay" value={stats.today} icon={CalendarDays} hint={`${stats.today - stats.todayOnline} offline · ${stats.todayOnline} online`} onClick={() => { setView('day'); setDate(dayjs()); setQuick('all'); }} />
        <StatCard label="Đang diễn ra" value={stats.live} icon={Radio} tone="info" hint="Bấm để lọc" onClick={() => setQuick('live')} />
        <StatCard
          label="Chờ xác nhận"
          value={stats.awaiting}
          icon={CircleDashed}
          tone={stats.awaiting ? 'warning' : 'success'}
          hint={stats.missingCheck ? `${stats.missingCheck} buổi GV chưa chấm công đủ` : 'Đã kết thúc, chưa xác nhận'}
          onClick={() => setShowAwaiting(true)}
        />
        <StatCard label="Sự cố GV (7 ngày)" value={stats.incidents} icon={HeartPulse} tone={stats.incidents ? 'danger' : 'success'} hint="GV ốm / việc gấp giữa buổi" onClick={() => setQuick('incident')} />
      </div>

      {/* Danh sách chờ xác nhận */}
      {awaitingList.length > 0 && (
        <section className="mb-4 rounded-[var(--radius-card)] border border-warning-100 bg-warning-50/50">
          <div className="flex flex-wrap items-center justify-between gap-2 px-4 py-2.5">
            <button type="button" className="flex items-center gap-2 text-sm font-semibold text-warning-700" onClick={() => setShowAwaiting((v) => !v)}>
              <CircleDashed size={16} aria-hidden /> {awaitingList.length} buổi chờ xác nhận
              <span className="text-xs font-normal text-ink-muted">{showAwaiting ? 'Thu gọn' : 'Xem'}</span>
            </button>
            {editable && readyToConfirm.length > 0 && (
              <Button icon={BadgeCheck} size="sm" onClick={confirmReady}>
                Xác nhận {readyToConfirm.length} buổi đủ điều kiện
              </Button>
            )}
          </div>
          {showAwaiting && (
            <ul className="max-h-72 divide-y divide-warning-100 overflow-auto border-t border-warning-100">
              {awaitingList.map((it) => {
                const missing = confirmChecklist(it.s, it.cls)
                  .slice(0, 3)
                  .filter((c) => !c.ok)
                  .map((c) => c.label);
                return (
                  <li key={it.s.id}>
                    <button type="button" onClick={() => setOpenId(it.s.id)} className="flex w-full flex-wrap items-center justify-between gap-2 px-4 py-2 text-left text-[13px] hover:bg-warning-50">
                      <span className="flex min-w-0 items-center gap-2">
                        <span className={cn('h-2.5 w-2.5 shrink-0 rounded-full', it.cls.online ? 'bg-success-600' : 'bg-primary-600')} aria-hidden />
                        <span className="tabular text-ink-secondary">
                          {dayjs(it.s.date).format('DD/MM')} {it.s.startTime}
                        </span>
                        <span className="font-mono font-medium text-ink">{it.cls.code}</span>
                        <span className="truncate text-ink-muted">{subtitleOf(it)}</span>
                        {it.s.incident && <HeartPulse size={14} className="shrink-0 text-danger-600" aria-label="Sự cố" />}
                      </span>
                      <span className={cn('text-xs', missing.length ? 'text-warning-700' : 'text-success-700')}>{missing.length ? missing.join(' · ') : 'Đủ điều kiện xác nhận'}</span>
                    </button>
                  </li>
                );
              })}
            </ul>
          )}
        </section>
      )}

      {/* Thanh công cụ */}
      <div className="mb-3 flex flex-col gap-3 rounded-[var(--radius-card)] border border-line bg-surface p-3">
        <div className="flex flex-wrap items-center gap-2">
          <div className="inline-flex rounded-lg border border-line p-0.5" role="tablist" aria-label="Chế độ xem">
            {(
              [
                ['week', 'Tuần (theo ca)', CalendarRange],
                ['day', 'Ngày', CalendarDays],
              ] as const
            ).map(([k, label, Icon]) => (
              <button
                key={k}
                type="button"
                role="tab"
                aria-selected={view === k}
                onClick={() => setView(k)}
                className={cn('inline-flex h-8 items-center gap-1.5 rounded-md px-3 text-sm font-medium', view === k ? 'bg-primary-600 text-white' : 'text-ink-secondary hover:bg-page')}
              >
                <Icon size={15} aria-hidden /> {label}
              </button>
            ))}
          </div>
          <div className="inline-flex rounded-lg border border-line p-0.5" role="tablist" aria-label="Hình thức">
            {(
              [
                ['all', 'Tất cả', null],
                ['offline', 'Offline', MapPin],
                ['online', 'Online', Video],
              ] as const
            ).map(([k, label, Icon]) => (
              <button
                key={k}
                type="button"
                role="tab"
                aria-selected={mode === k}
                onClick={() => setMode(k)}
                className={cn(
                  'inline-flex h-8 items-center gap-1.5 rounded-md px-3 text-sm font-medium',
                  mode === k ? (k === 'online' ? 'bg-success-600 text-white' : 'bg-primary-600 text-white') : 'text-ink-secondary hover:bg-page',
                )}
              >
                {Icon && <Icon size={15} aria-hidden />} {label}
              </button>
            ))}
          </div>
          <Select placeholder="Giáo viên / trợ giảng" value={teacher} onChange={setTeacher} options={teacherOptions} className="w-full sm:!w-56" />
          <Select
            placeholder="Lớp"
            value={classId}
            onChange={setClassId}
            options={scopeClasses.filter((c) => c.status !== 'cancelled').map((c) => ({ value: c.id, label: c.code }))}
            className="w-full sm:!w-48"
          />
        </div>
        <div className="flex flex-wrap items-center gap-1.5">
          {(
            [
              ['all', 'Tất cả'],
              ['live', 'Đang diễn ra'],
              ['awaiting', 'Chờ xác nhận'],
              ['incident', 'Có sự cố GV'],
              ['cancelled', 'Đã hủy'],
            ] as [Quick, string][]
          ).map(([k, label]) => (
            <button
              key={k}
              type="button"
              onClick={() => setQuick(k)}
              className={cn(
                'tabular rounded-full border px-3 py-1 text-[13px] transition-colors',
                quick === k ? 'border-primary-500 bg-primary-50 font-medium text-primary-700' : 'border-line text-ink-secondary hover:bg-page',
              )}
            >
              {label} <span className="text-xs text-ink-muted">{quickCounts[k]}</span>
            </button>
          ))}
          {/* Chú thích màu */}
          <span className="ml-auto flex flex-wrap items-center gap-x-3 gap-y-1 text-xs text-ink-secondary">
            <Legend className="bg-primary-100 border-l-primary-600">Offline</Legend>
            <Legend className="bg-success-50 border-l-success-600">Online</Legend>
            <Legend className="bg-warning-50 border-l-warning-600">Học bù</Legend>
            <span className="inline-flex items-center gap-1">
              <CheckCircle2 size={12} className="text-success-700" /> Đã xác nhận
            </span>
            <span className="inline-flex items-center gap-1">
              <CircleDashed size={12} className="text-warning-700" /> Chờ xác nhận
            </span>
            <span className="inline-flex items-center gap-1">
              <HeartPulse size={12} className="text-danger-600" /> Sự cố
            </span>
            <span className="inline-flex items-center gap-1">
              <UserRoundCog size={12} className="text-warning-700" /> Dạy thay
            </span>
          </span>
        </div>
      </div>

      {view === 'week' ? (
        <>
          <div className="mb-3 flex flex-wrap items-center gap-2">
            <div className="flex items-center gap-1">
              <Button icon={ChevronLeft} tone="neutral" variant="outline" iconOnly aria-label="Tuần trước" onClick={() => setDate((d) => d.subtract(7, 'day'))} />
              <Button icon={ChevronRight} tone="neutral" variant="outline" iconOnly aria-label="Tuần sau" onClick={() => setDate((d) => d.add(7, 'day'))} />
            </div>
            <Button icon={CalendarDays} variant="soft" disabled={weekStartOf(dayjs()).isSame(weekStart, 'day')} onClick={() => setDate(dayjs())}>
              Tuần này
            </Button>
            <span className="tabular font-semibold text-ink">
              {weekStart.format('DD/MM')} – {weekStart.add(6, 'day').format('DD/MM/YYYY')}
            </span>
            <span className="text-[13px] text-ink-muted">
              · {weekSessions.filter((x) => !x.cancelled && !x.online).length} buổi offline · {weekSessions.filter((x) => !x.cancelled && x.online).length} buổi online
            </span>
          </div>
          <WeekShiftBoard weekStart={weekStart} sessions={weekSessions} onOpen={setOpenId} />
        </>
      ) : (
        <>
          <div className="mb-3 flex flex-wrap items-center gap-2">
            <div className="flex items-center gap-1">
              <Button icon={ChevronLeft} tone="neutral" variant="outline" iconOnly aria-label="Ngày trước" onClick={() => setDate((d) => d.subtract(1, 'day'))} />
              <Button icon={ChevronRight} tone="neutral" variant="outline" iconOnly aria-label="Ngày sau" onClick={() => setDate((d) => d.add(1, 'day'))} />
            </div>
            <Button icon={CalendarDays} variant="soft" disabled={date.isSame(dayjs(), 'day')} onClick={() => setDate(dayjs())}>
              Hôm nay
            </Button>
            <DateInput value={date} onChange={(d) => d && setDate(d)} allowClear={false} className="!w-40" />
            <span className="font-semibold text-ink first-letter:uppercase">{date.format('dddd, DD/MM/YYYY')}</span>
            <span className="text-[13px] text-ink-muted">· {dayItems.filter((x) => x.s.status !== 'cancelled').length} buổi</span>
          </div>
          <DayTimeline date={date} rooms={roomRows} sessions={daySessions} showOffline={mode !== 'online'} showOnline={mode !== 'offline'} onOpen={setOpenId} />
        </>
      )}

      {opened && (
        <SessionDetailDrawer
          cls={opened.cls}
          sessionId={openId}
          editable={editable}
          onClose={() => setOpenId(null)}
          onOpen={setOpenId}
          onReschedule={(s) => setSchedule({ cls: opened.cls, mode: { kind: 'edit', session: s } })}
          onCancel={cancel}
          onMakeup={(s) => setSchedule({ cls: opened.cls, mode: { kind: 'create', origin: 'makeup', makeupForId: s.id } })}
          onIncident={(s) => setIncident(s.id)}
        />
      )}
      {schedule && <SessionScheduleModal cls={schedule.cls} mode={schedule.mode} onClose={() => setSchedule(null)} />}
      {/* Chọn buổi để ghi nhận sự cố: ưu tiên buổi đang diễn ra */}
      <Modal open={picking} onCancel={() => setPicking(false)} footer={null} title="Ghi nhận sự cố — chọn buổi học" width={560} destroyOnHidden>
        {(() => {
          const candidates = all
            .filter((x) => x.s.date === today && x.s.status !== 'cancelled' && !x.s.confirmation && x.s.startTime <= now.format('HH:mm'))
            .sort((a, b) => Number(b.state === 'live') - Number(a.state === 'live') || b.s.startTime.localeCompare(a.s.startTime));
          return candidates.length ? (
            <ul className="flex flex-col gap-2 py-1">
              {candidates.map((x) => (
                <li key={x.s.id}>
                  <button
                    type="button"
                    disabled={Boolean(x.s.incident)}
                    onClick={() => {
                      setPicking(false);
                      setIncident(x.s.id);
                    }}
                    className="flex w-full items-center justify-between gap-2 rounded-lg border border-line px-3 py-2.5 text-left hover:border-danger-300 hover:bg-danger-50 disabled:cursor-not-allowed disabled:opacity-60"
                  >
                    <span className="min-w-0">
                      <span className="block font-medium text-ink">
                        {x.cls.code} · {x.s.startTime}–{x.s.endTime}
                      </span>
                      <span className="block truncate text-xs text-ink-muted">
                        {x.cls.online ? 'Online' : sessionRoom(x.s, x.cls)} · {subtitleOf(x)}
                      </span>
                    </span>
                    <StatusTag tone={x.s.incident ? 'danger' : RUN_STATE[x.state].tone} icon={x.s.incident ? HeartPulse : RUN_STATE[x.state].icon} size="sm">
                      {x.s.incident ? 'Đã ghi nhận' : RUN_STATE[x.state].label}
                    </StatusTag>
                  </button>
                </li>
              ))}
            </ul>
          ) : (
            <p className="py-4 text-center text-[13px] text-ink-muted">Hôm nay chưa có buổi học nào đã bắt đầu. Mở một buổi trên lịch để ghi nhận sự cố cho buổi đó.</p>
          );
        })()}
      </Modal>
      {incidentItem && <IncidentModal cls={incidentItem.cls} session={incidentItem.s} onClose={() => setIncident(null)} />}
      {confirmDialog}
    </>
  );
}

function Legend({ className, children }: { className: string; children: string }) {
  return (
    <span className="inline-flex items-center gap-1">
      <span className={cn('h-3 w-4 rounded-sm border-l-[3px]', className)} aria-hidden /> {children}
    </span>
  );
}
