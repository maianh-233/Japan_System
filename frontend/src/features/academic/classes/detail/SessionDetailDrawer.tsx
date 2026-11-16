import { useEffect, useMemo, useState, type ReactNode } from 'react';
import { Drawer, Radio, Tabs } from 'antd';
import dayjs, { type Dayjs } from 'dayjs';
import {
  BadgeCheck,
  BookOpen,
  CalendarClock,
  CircleCheck,
  CircleX,
  HeartPulse,
  ClipboardCheck,
  Clock,
  History,
  Flag,
  Info,
  LogIn,
  MapPin,
  NotebookPen,
  Repeat,
  TriangleAlert,
  Undo2,
  UserCheck,
  UserRound,
  UserX,
  Users,
  Video,
  XCircle,
} from 'lucide-react';
import { Button, EmptyState, Select, STATUS_PRESETS, StatusTag, TextInput, TimeInput, UserCell, useConfirm, useNotify } from '@/components/common';
import { useAuth } from '@/hooks/useAuth';
import { useBreakpoint } from '@/hooks/useBreakpoint';
import { cn } from '@/lib/cn';
import { fmtDate } from '@/lib/date';
import { EXAM_KINDS } from '../../curriculum/constants';
import { useCurriculumStore } from '../../curriculum/curriculumStore';
import { useClassStore } from '../classStore';
import { LESSON_COVERAGE, teacherName, useTeachers, WEEKDAYS, weekdayOf } from '../constants';
import { canTakeAttendance, notStarted, originOf, SESSION_ORIGIN, sessionRoom, summarize } from '../sessionUtils';
import type { AttendanceStatus, ClassInfo, ClassSession, TeacherAttendance } from '../types';
import { SessionAttendanceTab } from './SessionAttendanceTab';
import { PauseFollowUp, SupportLogSection } from '../../sessions/SessionSupport';
import { confirmChecklist, INCIDENT_ACTIONS, isPaused, INCIDENT_REASONS, ROLE_LABEL, RUN_STATE, runState, staffOf } from '../../sessions/sessionOps';

export type SessionTab = 'info' | 'students' | 'teachers';

const ATT_OPTIONS: { value: AttendanceStatus; label: string }[] = [
  { value: 'present', label: 'Có mặt' },
  { value: 'late', label: 'Muộn' },
  { value: 'excused', label: 'Có phép' },
  { value: 'absent', label: 'Vắng' },
];

const atTime = (t?: string) => {
  if (!t) return undefined;
  const [h, m] = t.split(':').map(Number);
  return dayjs().hour(h).minute(m).second(0);
};
const minutes = (a?: string, b?: string) => {
  if (!a || !b) return 0;
  const [h1, m1] = a.split(':').map(Number);
  const [h2, m2] = b.split(':').map(Number);
  return Math.max(0, h2 * 60 + m2 - (h1 * 60 + m1));
};

interface Props {
  cls: ClassInfo;
  sessionId: string | null;
  initialTab?: SessionTab;
  editable: boolean;
  onClose: () => void;
  onOpen: (id: string) => void;
  onReschedule: (s: ClassSession) => void;
  onCancel: (s: ClassSession) => void;
  onMakeup: (s: ClassSession) => void;
  /** Báo sự cố GV giữa buổi (trang Buổi học) — bỏ trống = ẩn nút */
  onIncident?: (s: ClassSession) => void;
}

/** Chi tiết 1 buổi học: thông tin + nhật ký GV · điểm danh học viên · chấm công giáo viên */
export function SessionDetailDrawer({ cls, sessionId, initialTab = 'info', editable, onClose, onOpen, onReschedule, onCancel, onMakeup, onIncident }: Props) {
  const { isMobile } = useBreakpoint();
  const session = useClassStore((s) => s.sessions.find((x) => x.id === sessionId));
  const classSessions = useClassStore((s) => s.sessions);
  const [tab, setTab] = useState<SessionTab>(initialTab);

  useEffect(() => {
    if (sessionId) setTab(initialTab);
  }, [sessionId, initialTab]);

  const siblings = useMemo(() => classSessions.filter((x) => x.classId === cls.id), [classSessions, cls.id]);
  const s = session;
  const origin = s && originOf(s);
  const recordCount = s?.records?.length;

  return (
    <Drawer
      open={Boolean(sessionId)}
      onClose={onClose}
      width={isMobile ? '100%' : 760}
      destroyOnHidden
      styles={{ body: { paddingTop: 0 } }}
      title={
        s && (
          <div className="flex min-w-0 flex-col gap-1 pr-2">
            <div className="flex flex-wrap items-center gap-2">
              <span className="text-base font-semibold">{s.no ? `Buổi ${s.no}` : 'Buổi học thêm'}</span>
              <StatusTag group="session" status={s.status} size="sm" />
              {(() => {
                const rs = RUN_STATE[runState(s)];
                return s.status !== 'cancelled' ? (
                  <StatusTag tone={rs.tone} icon={rs.icon} size="sm">
                    {rs.label}
                  </StatusTag>
                ) : null;
              })()}
              {s.incident && (
                <StatusTag tone="danger" icon={HeartPulse} size="sm">
                  Sự cố GV
                </StatusTag>
              )}
              {origin && origin !== 'regular' && (
                <StatusTag tone={origin === 'extra' ? 'primary' : 'warning'} icon={origin === 'extra' ? BookOpen : Repeat} size="sm">
                  {SESSION_ORIGIN[origin].label}
                </StatusTag>
              )}
            </div>
            <span className="tabular text-xs font-normal text-ink-muted">
              {WEEKDAYS[weekdayOf(dayjs(s.date)) - 1].label}, {fmtDate(s.date)} · {s.startTime}–{s.endTime} · {cls.code}
            </span>
          </div>
        )
      }
    >
      {!s ? (
        <EmptyState size="compact" title="Không tìm thấy buổi học" />
      ) : (
        <Tabs
          activeKey={tab}
          onChange={(k) => setTab(k as SessionTab)}
          items={[
            {
              key: 'info',
              label: tabLabel(Info, 'Tổng quan'),
              children: <InfoTab cls={cls} s={s} siblings={siblings} editable={editable} onOpen={onOpen} onReschedule={onReschedule} onCancel={onCancel} onMakeup={onMakeup} onIncident={onIncident} onGoto={setTab} />,
            },
            {
              key: 'students',
              label: tabLabel(ClipboardCheck, `Điểm danh HV${recordCount ? ` (${s.attendance?.present}/${recordCount})` : ''}`),
              children: <SessionAttendanceTab key={s.id} cls={cls} s={s} siblings={siblings} editable={editable} />,
            },
            { key: 'teachers', label: tabLabel(UserRound, 'Chấm công GV'), children: <TeachersTab key={s.id} cls={cls} s={s} editable={editable} /> },
          ]}
        />
      )}
    </Drawer>
  );
}

const tabLabel = (Icon: typeof Info, text: string) => (
  <span className="inline-flex items-center gap-1.5">
    <Icon size={16} aria-hidden /> {text}
  </span>
);

// ======================= Tổng quan =======================

function Row({ icon: Icon, label, children }: { icon: typeof Info; label: string; children: ReactNode }) {
  return (
    <div className="flex gap-3 py-2">
      <Icon size={16} className="mt-0.5 shrink-0 text-ink-muted" aria-hidden />
      <span className="w-28 shrink-0 text-[13px] text-ink-muted">{label}</span>
      <div className="min-w-0 flex-1 text-[13px] text-ink">{children}</div>
    </div>
  );
}

function InfoTab({
  cls,
  s,
  siblings,
  editable,
  onOpen,
  onReschedule,
  onCancel,
  onMakeup,
  onIncident,
  onGoto,
}: {
  cls: ClassInfo;
  s: ClassSession;
  siblings: ClassSession[];
  editable: boolean;
  onOpen: (id: string) => void;
  onReschedule: (s: ClassSession) => void;
  onCancel: (s: ClassSession) => void;
  onMakeup: (s: ClassSession) => void;
  onIncident?: (s: ClassSession) => void;
  onGoto: (t: SessionTab) => void;
}) {
  const { user } = useAuth();
  const notify = useNotify();
  const { confirm, confirmDialog } = useConfirm();
  const updateSession = useClassStore((st) => st.updateSession);
  const state = runState(s);
  const staff = staffOf(s, cls);
  const checklist = confirmChecklist(s, cls);
  const started = state === 'live' || state === 'awaiting';

  const confirmHeld = async () => {
    const missing = checklist.filter((c, i) => !c.ok && i < 3).map((c) => c.label);
    const res = await confirm({
      title: `Xác nhận ${s.no ? `buổi ${s.no}` : 'buổi học thêm'} đã diễn ra?`,
      description: 'Buổi được tính vào tiến độ lớp, trừ vào gói học phí và làm căn cứ tính lương giáo viên.',
      consequences: missing.length ? missing.map((m) => `Lưu ý: ${m}`) : undefined,
      tone: missing.length ? 'warning' : 'primary',
      confirmAction: 'approve',
      confirmText: 'Xác nhận đã diễn ra',
    });
    if (!res) return;
    updateSession(s.id, {
      confirmation: { by: user ? `${user.fullName} (${user.roleName})` : 'Nhân viên', at: new Date().toISOString() },
      status: s.status === 'scheduled' ? 'completed' : s.status,
    });
    notify.toast.success('Đã xác nhận buổi học');
  };

  const unconfirm = async () => {
    const res = await confirm({
      title: 'Bỏ xác nhận buổi học?',
      description: 'Buổi quay lại trạng thái "Chờ xác nhận" — chưa tính lương cho tới khi xác nhận lại.',
      tone: 'warning',
      confirmAction: 'cancel',
      confirmText: 'Bỏ xác nhận',
    });
    if (res) updateSession(s.id, { confirmation: undefined });
  };
  const curriculum = useCurriculumStore((st) => st.curricula.find((c) => c.id === cls.curriculumId));
  const room = sessionRoom(s, cls);
  const makeupOf = s.makeupForId ? siblings.find((x) => x.id === s.makeupForId) : undefined;
  const makeupBy = siblings.find((x) => x.makeupForId === s.id && x.status !== 'cancelled');
  const sum = s.records ? summarize(s.records) : undefined;
  const upcoming = (s.status === 'scheduled' || (s.status === 'makeup' && !s.attendance)) && notStarted(s);

  let content: ReactNode = <span className="text-ink-muted">Chưa gán bài</span>;
  if (s.kind === 'exam') {
    const exam = curriculum?.exams.find((e) => e.id === s.examId);
    const meta = exam && EXAM_KINDS[exam.kind];
    content = meta ? (
      <StatusTag tone={meta.tone} icon={meta.icon} size="sm">
        {exam!.title}
      </StatusTag>
    ) : (
      'Buổi thi'
    );
  } else if (s.lessonId && curriculum) {
    const i = curriculum.lessons.findIndex((l) => l.id === s.lessonId);
    if (i >= 0) content = `Bài ${i + 1}: ${curriculum.lessons[i].title}`;
  } else if (s.origin === 'extra') content = 'Ôn tập / luyện đề';

  return (
    <div className="flex flex-col gap-5">
      <div className="divide-y divide-line rounded-[var(--radius-card)] border border-line px-4">
        <Row icon={Clock} label="Thời gian">
          <span className="tabular">
            {fmtDate(s.date)} · {s.startTime}–{s.endTime}
          </span>
          {s.rescheduledFrom && (
            <p className="mt-0.5 text-xs text-warning-700">
              Dời từ {fmtDate(s.rescheduledFrom.date)} {s.rescheduledFrom.startTime}–{s.rescheduledFrom.endTime}
              {s.rescheduledFrom.reason ? ` — ${s.rescheduledFrom.reason}` : ''}
            </p>
          )}
        </Row>
        <Row icon={BookOpen} label="Nội dung">
          {content}
        </Row>
        <Row icon={UserRound} label="Đứng lớp">
          <ul className="flex flex-col gap-0.5">
            {staff.map((x) => (
              <li key={x.id}>
                {teacherName(x.id)}{' '}
                <span className={cn('text-xs', x.role === 'substitute' ? 'text-warning-700' : 'text-ink-muted')}>· {ROLE_LABEL[x.role]}</span>
                {s.incident?.takeoverId === x.id && <span className="text-xs text-danger-600"> · dạy tiếp từ sự cố</span>}
              </li>
            ))}
          </ul>
          {s.teacherIds && <p className="mt-0.5 text-xs text-warning-700">GV lớp: {cls.teacherIds.map(teacherName).join(', ')}</p>}
        </Row>
        <Row icon={cls.online ? Video : MapPin} label={cls.online ? 'Online' : 'Phòng'}>
          {cls.online ? (
            cls.meetingUrl ? (
              <a href={cls.meetingUrl} target="_blank" rel="noreferrer" className="text-primary-700 hover:underline">
                {cls.meetingUrl}
              </a>
            ) : (
              'Chưa có link họp'
            )
          ) : (
            <>
              {room ?? '—'}
              {s.room && <span className="ml-1.5 text-xs text-warning-700">(đổi phòng — mặc định {cls.room})</span>}
            </>
          )}
        </Row>
        {makeupOf && (
          <Row icon={Repeat} label="Học bù cho">
            <button type="button" className="text-primary-700 hover:underline" onClick={() => onOpen(makeupOf.id)}>
              Buổi {makeupOf.no} ({fmtDate(makeupOf.date)})
            </button>
            {makeupOf.note && <span className="text-ink-muted"> — {makeupOf.note}</span>}
          </Row>
        )}
        {s.status === 'cancelled' && (
          <Row icon={XCircle} label="Lý do hủy">
            {s.note ?? '—'}
            {s.cancelledBy && (
              <p className="mt-0.5 text-xs text-ink-muted">
                {s.cancelledBy.name} · {dayjs(s.cancelledBy.at).format('HH:mm DD/MM/YYYY')}
              </p>
            )}
            <p className="mt-1 text-xs">
              {makeupBy ? (
                <button type="button" className="text-primary-700 hover:underline" onClick={() => onOpen(makeupBy.id)}>
                  Đã xếp học bù: {fmtDate(makeupBy.date)} {makeupBy.startTime}–{makeupBy.endTime}
                </button>
              ) : (
                <span className="text-warning-700">Chưa xếp học bù</span>
              )}
            </p>
          </Row>
        )}
        {s.status !== 'cancelled' && s.note && !makeupOf && (
          <Row icon={Info} label="Ghi chú">
            {s.note}
          </Row>
        )}
        <Row icon={Users} label="Điểm danh">
          {sum ? (
            <div className="flex flex-wrap items-center gap-1.5">
              <span className="tabular mr-1 font-medium">
                {sum.attended}/{sum.total} có mặt
              </span>
              {(['late', 'excused', 'absent'] as const).map((k) =>
                sum[k] ? (
                  <StatusTag key={k} tone={STATUS_PRESETS.attendance[k].tone} icon={STATUS_PRESETS.attendance[k].icon} size="sm">
                    {`${ATT_OPTIONS.find((o) => o.value === k)!.label} ${sum[k]}`}
                  </StatusTag>
                ) : null,
              )}
              <button type="button" className="text-xs text-primary-700 hover:underline" onClick={() => onGoto('students')}>
                Xem danh sách
              </button>
            </div>
          ) : s.attendance ? (
            `${s.attendance.present}/${s.attendance.total} có mặt`
          ) : (
            <span className="text-ink-muted">{s.status === 'cancelled' ? 'Buổi đã hủy' : notStarted(s) ? 'Chưa tới giờ học' : 'Chưa điểm danh'}</span>
          )}
          {s.attendanceBy && <p className="mt-0.5 text-xs text-ink-muted">Điểm danh bởi {s.attendanceBy.name}</p>}
        </Row>
      </div>

      {/* Sự cố giáo viên giữa buổi */}
      {s.incident && (
        <section className="flex flex-col gap-1.5 rounded-[var(--radius-card)] border border-danger-100 border-l-4 border-l-danger-600 bg-danger-50/60 px-4 py-3 text-[13px]">
          <p className="flex items-center gap-1.5 font-semibold text-danger-700">
            <HeartPulse size={16} aria-hidden /> Sự cố lúc {s.incident.at}: {INCIDENT_REASONS[s.incident.reason].label}
          </p>
          {s.incident.detail && <p className="text-ink">{s.incident.detail}</p>}
          <p className="text-ink">
            Xử lý: <strong>{INCIDENT_ACTIONS[s.incident.action].label}</strong>
            {s.incident.takeoverId && ` — ${teacherName(s.incident.takeoverId)}`}
          </p>
          {s.incident.outcome && (
            <p className="text-ink">
              Kết quả: {s.incident.outcome.kind === 'resumed' ? `Học tiếp lúc ${s.incident.outcome.at}${s.incident.outcome.teacherId ? ` — ${teacherName(s.incident.outcome.teacherId)} đứng lớp` : ''}` : 'Cho nghỉ, xếp học bù'}
            </p>
          )}
          <p className="text-xs text-ink-muted">
            {s.incident.reportedBy} · {dayjs(s.incident.reportedAt).format('HH:mm DD/MM/YYYY')} · {s.incident.notified ? 'Đã báo học viên / phụ huynh' : 'Chưa báo học viên / phụ huynh'}
          </p>
          <PauseFollowUp cls={cls} s={s} editable={editable} onMakeup={onMakeup} />
        </section>
      )}

      <SupportLogSection s={s} editable={editable && s.status !== 'cancelled'} />

      {/* Xác nhận của nhân viên */}
      {s.status !== 'cancelled' && (
        <section className="rounded-[var(--radius-card)] border border-line px-4 py-3">
          <div className="flex flex-wrap items-center justify-between gap-2">
            <h3 className="flex items-center gap-1.5 text-sm font-semibold text-ink">
              <BadgeCheck size={16} aria-hidden /> Xác nhận buổi học
            </h3>
            <StatusTag tone={RUN_STATE[state].tone} icon={RUN_STATE[state].icon} size="sm">
              {RUN_STATE[state].label}
            </StatusTag>
          </div>
          {s.confirmation ? (
            <div className="mt-2 flex flex-wrap items-center justify-between gap-2 text-[13px]">
              <p className="text-ink">
                Xác nhận bởi <strong>{s.confirmation.by}</strong> · {dayjs(s.confirmation.at).format('HH:mm DD/MM/YYYY')}
              </p>
              {editable && (
                <Button icon={Undo2} size="sm" tone="neutral" variant="ghost" onClick={unconfirm}>
                  Bỏ xác nhận
                </Button>
              )}
            </div>
          ) : !started ? (
            <p className="mt-2 text-[13px] text-ink-muted">Xác nhận được sau khi buổi học bắt đầu.</p>
          ) : (
            <>
              <ul className="mt-2 grid grid-cols-1 gap-1 text-[13px] sm:grid-cols-2">
                {checklist.map((c) => (
                  <li key={c.label} className={cn('flex items-center gap-1.5', c.ok ? 'text-success-700' : 'text-warning-700')}>
                    {c.ok ? <CircleCheck size={15} aria-hidden /> : <CircleX size={15} aria-hidden />} {c.label}
                  </li>
                ))}
              </ul>
              {isPaused(s) && <p className="mt-2 text-[13px] text-danger-700">Lớp đang tạm dừng — xử lý sự cố (cho học tiếp / cho nghỉ) trước khi xác nhận.</p>}
              {editable && !isPaused(s) && (
                <div className="mt-3 flex flex-wrap justify-end gap-2">
                  <Button icon={UserRound} size="sm" variant="outline" onClick={() => onGoto('teachers')}>
                    Xem chấm công
                  </Button>
                  <Button icon={BadgeCheck} size="sm" onClick={confirmHeld}>
                    Xác nhận đã diễn ra
                  </Button>
                </div>
              )}
            </>
          )}
        </section>
      )}

      {/* Nhật ký giáo viên */}
      <section>
        <h3 className="mb-2 flex items-center gap-1.5 text-sm font-semibold text-ink">
          <NotebookPen size={16} aria-hidden /> Nhật ký giáo viên
        </h3>
        {s.teacherLog ? (
          <div className="flex flex-col gap-2 rounded-[var(--radius-card)] bg-page px-4 py-3 text-[13px]">
            {s.teacherLog.coverage && (
              <div className="flex flex-wrap items-center gap-2">
                <StatusTag tone={LESSON_COVERAGE[s.teacherLog.coverage].tone} icon={Flag} size="sm">
                  {LESSON_COVERAGE[s.teacherLog.coverage].label}
                </StatusTag>
                {s.teacherLog.coverage === 'partial' &&
                  (() => {
                    const chapters = curriculum?.lessons.find((l) => l.id === s.lessonId)?.chapters ?? [];
                    const left = chapters.filter((c) => !(s.teacherLog!.coveredChapterIds ?? []).includes(c.id));
                    return left.length > 0 && <span className="text-warning-700">Còn dở: {left.map((c) => c.title).join(', ')}</span>;
                  })()}
                {s.teacherLog.endedAt && <span className="text-xs text-ink-muted">GV kết thúc buổi lúc {dayjs(s.teacherLog.endedAt).format('HH:mm DD/MM')}</span>}
              </div>
            )}
            <p className="text-ink">{s.teacherLog.content}</p>
            {s.teacherLog.homework && (
              <p>
                <span className="font-medium text-ink-secondary">Bài về nhà: </span>
                {s.teacherLog.homework}
              </p>
            )}
            {s.teacherLog.issues && (
              <p className="flex gap-1.5 text-warning-700">
                <TriangleAlert size={15} className="mt-0.5 shrink-0" aria-hidden /> {s.teacherLog.issues}
              </p>
            )}
            <p className="text-xs text-ink-muted">
              {s.teacherLog.author} · {dayjs(s.teacherLog.at).format('HH:mm DD/MM/YYYY')}
            </p>
          </div>
        ) : (
          <p className="text-[13px] text-ink-muted">{s.attendance ? 'Giáo viên chưa ghi nhật ký buổi này (ghi ở Cổng giáo viên).' : 'Giáo viên ghi nhật ký sau khi dạy.'}</p>
        )}
      </section>

      {editable && (
        <div className="flex flex-wrap justify-end gap-2 border-t border-line pt-4">
          {canTakeAttendance(s) && (
            <Button icon={ClipboardCheck} variant="outline" onClick={() => onGoto('students')}>
              Điểm danh
            </Button>
          )}
          {onIncident && started && !s.confirmation && !s.incident && (
            <Button icon={HeartPulse} tone="danger" variant="outline" onClick={() => onIncident(s)}>
              Ghi nhận sự cố
            </Button>
          )}
          {upcoming && (
            <Button icon={CalendarClock} variant="outline" onClick={() => onReschedule(s)}>
              Đổi lịch / dạy thay
            </Button>
          )}
          {upcoming && (
            <Button icon={XCircle} tone="danger" variant="soft" onClick={() => onCancel(s)}>
              Hủy buổi
            </Button>
          )}
          {s.status === 'cancelled' && !makeupBy && (
            <Button icon={Repeat} tone="warning" onClick={() => onMakeup(s)}>
              Xếp học bù
            </Button>
          )}
          {s.incident?.action === 'end_early' && (
            <Button icon={Repeat} tone="warning" onClick={() => onMakeup(s)}>
              Xếp học bù phần nghỉ sớm
            </Button>
          )}
        </div>
      )}
      {confirmDialog}
    </div>
  );
}

// ======================= Chấm công giáo viên =======================

function TeachersTab({ cls, s, editable }: { cls: ClassInfo; s: ClassSession; editable: boolean }) {
  const notify = useNotify();
  const updateSession = useClassStore((st) => st.updateSession);
  const teachers = useTeachers();
  const staff = staffOf(s, cls);
  const [rows, setRows] = useState<TeacherAttendance[]>(() => {
    const saved = new Map((s.teacherAttendance ?? []).map((t) => [t.teacherId, t]));
    return staff.map((x) => saved.get(x.id) ?? { teacherId: x.id, role: x.role, status: 'present' });
  });
  const [dirty, setDirty] = useState(false);
  const canEdit = editable && canTakeAttendance(s);
  const planned = minutes(s.startTime, s.endTime);

  const set = (teacherId: string, patch: Partial<TeacherAttendance>) => {
    // Giáo vụ sửa → ghi nhận là "chấm hộ"
    setRows((rs) => rs.map((r) => (r.teacherId === teacherId ? { ...r, ...patch, source: 'staff', checkedAt: new Date().toISOString() } : r)));
    setDirty(true);
  };

  const save = () => {
    const fixed = rows.map((r) => {
      if (r.status === 'absent') return { ...r, checkIn: undefined, checkOut: undefined };
      // Vào sau giờ học > 5 phút → tự đánh dấu muộn
      const late = r.checkIn && minutes(s.startTime, r.checkIn) > 5;
      return { ...r, status: late ? 'late' : r.status } as TeacherAttendance;
    });
    if (fixed.some((r) => r.status === 'absent' && !r.substituteId && r.role !== 'assistant')) {
      notify.toast.warning('GV vắng — chọn giáo viên dạy thay (hoặc hủy buổi nếu không có người thay)');
      return;
    }
    updateSession(s.id, { teacherAttendance: fixed });
    setRows(fixed);
    setDirty(false);
    notify.toast.success('Đã lưu chấm công giáo viên');
  };

  if (s.status === 'cancelled') return <EmptyState size="compact" title="Buổi đã hủy" description="Buổi hủy không tính công giáo viên." />;

  return (
    <div className="flex flex-col gap-3">
      {!canTakeAttendance(s) && (
        <p className="rounded-lg border border-primary-200 bg-primary-50 px-3 py-2 text-[13px] text-primary-800">Chưa tới giờ học — giáo viên check-in khi bắt đầu buổi (Cổng giáo viên hoặc Giáo vụ chấm hộ).</p>
      )}
      {rows.map((r) => {
        const worked = r.status === 'absent' ? 0 : minutes(r.checkIn, r.checkOut);
        const substituteOptions = teachers.filter((t) => t.id !== r.teacherId && t.branchIds.includes(cls.branchId)).map((t) => ({ value: t.id, label: t.name }));
        return (
          <div key={r.teacherId} className="flex flex-col gap-3 rounded-[var(--radius-card)] border border-line p-4">
            <div className="flex flex-wrap items-center gap-2">
              <UserCell
                size="sm"
                name={teacherName(r.teacherId)}
                subtitle={
                  <span className="flex flex-wrap gap-x-2">
                    <span>{ROLE_LABEL[r.role ?? staff.find((x) => x.id === r.teacherId)?.role ?? 'teacher']}</span>
                    {r.source === 'self' && <span className="text-success-700">GV tự chấm{r.checkedAt ? ` · ${dayjs(r.checkedAt).format('HH:mm DD/MM')}` : ''}</span>}
                    {r.source === 'staff' && <span className="text-warning-700">Giáo vụ chấm hộ / sửa</span>}
                    {!r.source && s.teacherAttendance && <span className="text-ink-muted">—</span>}
                  </span>
                }
                className="min-w-0 flex-1"
              />
              {canEdit ? (
                <Radio.Group
                  size="small"
                  optionType="button"
                  buttonStyle="solid"
                  value={r.status}
                  onChange={(e) => set(r.teacherId, { status: e.target.value })}
                  options={[
                    { value: 'present', label: 'Đúng giờ' },
                    { value: 'late', label: 'Đi muộn' },
                    { value: 'absent', label: 'Vắng' },
                  ]}
                />
              ) : s.teacherAttendance ? (
                <StatusTag
                  tone={r.status === 'present' ? 'success' : r.status === 'late' ? 'warning' : 'danger'}
                  icon={r.status === 'present' ? UserCheck : r.status === 'late' ? Clock : UserX}
                  size="sm"
                >
                  {r.status === 'present' ? 'Đúng giờ' : r.status === 'late' ? 'Đi muộn' : 'Vắng'}
                </StatusTag>
              ) : (
                <span className="text-[13px] text-ink-muted">Chưa chấm công</span>
              )}
            </div>

            {r.status === 'absent' ? (
              canEdit ? (
                <Select
                  placeholder="Chọn giáo viên dạy thay"
                  value={r.substituteId}
                  onChange={(v) => set(r.teacherId, { substituteId: v as string })}
                  options={substituteOptions}
                  allowClear
                />
              ) : (
                r.substituteId && <p className="text-[13px]">Dạy thay: {teacherName(r.substituteId)}</p>
              )
            ) : (
              <div className="flex flex-wrap items-end gap-3">
                {canEdit ? (
                  <>
                    <div className="flex flex-col gap-1">
                      <span className="text-xs text-ink-muted">Check-in</span>
                      <TimeInput value={atTime(r.checkIn)} onChange={(v: Dayjs | null) => set(r.teacherId, { checkIn: v?.format('HH:mm') })} minuteStep={5} />
                    </div>
                    <div className="flex flex-col gap-1">
                      <span className="text-xs text-ink-muted">Check-out</span>
                      <TimeInput value={atTime(r.checkOut)} onChange={(v: Dayjs | null) => set(r.teacherId, { checkOut: v?.format('HH:mm') })} minuteStep={5} />
                    </div>
                    {!r.checkIn && (
                      <Button icon={LogIn} variant="soft" size="sm" onClick={() => set(r.teacherId, { checkIn: dayjs().format('HH:mm') })}>
                        Check-in bây giờ
                      </Button>
                    )}
                  </>
                ) : (
                  <span className="tabular text-[13px]">
                    Vào {r.checkIn ?? '—'} · Ra {r.checkOut ?? '—'}
                  </span>
                )}
                <span className={cn('tabular ml-auto text-[13px]', worked && worked < planned ? 'text-warning-700' : 'text-ink-secondary')}>
                  Giờ dạy: {worked ? `${(worked / 60).toFixed(1)}h` : '—'} / {(planned / 60).toFixed(1)}h
                </span>
              </div>
            )}
            {canEdit ? (
              <TextInput size="small" placeholder="Ghi chú (VD: kẹt xe, đổi ca...)" value={r.note} onChange={(e) => set(r.teacherId, { note: e.target.value || undefined })} />
            ) : (
              r.note && <p className="text-xs text-ink-muted">{r.note}</p>
            )}
          </div>
        );
      })}
      <p className="flex items-center gap-1.5 text-xs text-ink-muted">
        <History size={14} aria-hidden /> Check-in trễ quá 5 phút tự chuyển "Đi muộn". Giờ dạy thực tế dùng để tính lương ở module Nhân sự.
      </p>
      {canEdit && (
        <div className="flex justify-end">
          <Button action="save" disabled={!dirty && Boolean(s.teacherAttendance)} onClick={save}>
            Lưu chấm công
          </Button>
        </div>
      )}
    </div>
  );
}
