import { useEffect, useMemo, useRef, useState } from 'react';
import { useNavigate, useParams } from 'react-router-dom';
import { Drawer } from 'antd';
import dayjs from 'dayjs';
import {
  ArrowLeftRight,
  BookOpen,
  CalendarClock,
  CheckCircle2,
  CircleDot,
  ClipboardCheck,
  FileWarning,
  Fingerprint,
  Flag,
  History,
  NotebookPen,
  QrCode,
  RotateCcw,
  Users,
  Video,
} from 'lucide-react';
import { Button, EmptyState, PageHeader, StatusTag, useNotify, type Tone } from '@/components/common';
import { useClassStore } from '@/features/academic/classes/classStore';
import { LESSON_COVERAGE } from '@/features/academic/classes/constants';
import { applyQrScan, sessionRecords } from '@/features/academic/classes/attendanceQr';
import { QrModal, SessionAttendanceTab } from '@/features/academic/classes/detail/SessionAttendanceTab';
import { SESSION_ORIGIN, originOf, rosterFor, sessionRoom, sessionTeachers, startOf, summarize } from '@/features/academic/classes/sessionUtils';
import type { ClassSession, TeacherAttendance } from '@/features/academic/classes/types';
import { useCurriculumStore } from '@/features/academic/curriculum/curriculumStore';
import { useCrmStore } from '@/features/crm/crmStore';
import { usePortalStore } from '@/features/portal/portalStore';
import { DOC_KIND, DocViewer } from '@/features/portal/teaching/DocViewer';
import { EndSessionModal } from '@/features/portal/teaching/EndSessionModal';
import { teachingDocsFor } from '@/features/portal/teaching/teachingDocs';
import { shortCode, useTeacherId } from '@/features/portal/usePortal';
import { useStudentStore } from '@/features/student/studentStore';
import { useAuth } from '@/hooks/useAuth';
import { useBreakpoint } from '@/hooks/useBreakpoint';
import { cn } from '@/lib/cn';
import { fmtDateTime } from '@/lib/date';

const toMin = (t: string) => Number(t.slice(0, 2)) * 60 + Number(t.slice(3, 5));
/** Chấm công sau giờ bắt đầu quá số phút này → tính "muộn" */
const TEACHER_LATE_MIN = 5;

type Phase = 'cancelled' | 'upcoming' | 'live' | 'past';
const PHASE: Record<Phase, { label: string; tone: Tone }> = {
  cancelled: { label: 'Đã hủy', tone: 'danger' },
  upcoming: { label: 'Sắp diễn ra', tone: 'info' },
  live: { label: 'Đang diễn ra', tone: 'success' },
  past: { label: 'Đã diễn ra', tone: 'neutral' },
};

const phaseOf = (s: ClassSession): Phase => {
  if (s.status === 'cancelled') return 'cancelled';
  const now = dayjs();
  if (startOf(s).isAfter(now)) return 'upcoming';
  return dayjs(`${s.date}T${s.endTime}`).isAfter(now) ? 'live' : 'past';
};

/**
 * Cổng GV › Dạy học — mở từ Thời khóa biểu (bấm vào 1 buổi).
 *  Header: thông tin buổi · Chấm công (theo buổi, căn cứ tính lương) · Mở QR điểm danh · Kết thúc buổi (báo cáo tiến độ, sửa lại được)
 *  Trái:  hôm nay học gì (bài trong lộ trình, phần còn dở buổi trước) + danh sách tài liệu
 *  Phải:  khung trình chiếu tài liệu (toàn màn hình)
 *  Không giới hạn thời điểm mở — buổi đã qua / sắp tới chỉ hiện trạng thái tương ứng.
 */
export default function TeachingSessionPage() {
  const { sessionId } = useParams();
  const navigate = useNavigate();
  const notify = useNotify();
  const { user } = useAuth();
  const { lg: isDesktop } = useBreakpoint();
  const me = useTeacherId();

  const session = useClassStore((s) => s.sessions.find((x) => x.id === sessionId));
  const allSessions = useClassStore((s) => s.sessions);
  const cls = useClassStore((s) => s.classes.find((c) => c.id === session?.classId));
  const updateSession = useClassStore((s) => s.updateSession);
  const curriculum = useCurriculumStore((s) => s.curricula.find((c) => c.id === cls?.curriculumId));
  const allDocs = usePortalStore((s) => s.teacherDocs);
  const leaves = usePortalStore((s) => s.leaveRequests);
  const recordMakeup = usePortalStore((s) => s.recordMakeup);
  const enrollments = useStudentStore((s) => s.enrollments);
  const students = useCrmStore((s) => s.students);

  const [docId, setDocId] = useState<string>();
  const [qrOpen, setQrOpen] = useState(false);
  const [attOpen, setAttOpen] = useState(false);
  const [endOpen, setEndOpen] = useState(false);
  const viewerRef = useRef<HTMLDivElement>(null);
  // Đồng hồ để trạng thái "Đang diễn ra" tự cập nhật
  const [, setTick] = useState(0);
  useEffect(() => {
    const t = setInterval(() => setTick((x) => x + 1), 30_000);
    return () => clearInterval(t);
  }, []);

  const siblings = useMemo(() => allSessions.filter((x) => x.classId === session?.classId), [allSessions, session?.classId]);
  const lesson = curriculum?.lessons.find((l) => l.id === session?.lessonId);
  const docs = useMemo(
    () => (session ? teachingDocsFor(session, lesson, allDocs.filter((d) => d.teacherId === me)) : []),
    [session, lesson, allDocs, me],
  );
  // Mặc định mở tài liệu đầu tiên
  const docIndex = Math.max(0, docs.findIndex((d) => d.id === docId));
  const doc = docs[docIndex];

  const roster = useMemo(() => (session && cls ? rosterFor(cls.id, session.date, enrollments) : []), [session, cls, enrollments]);

  if (!session || !cls)
    return <EmptyState title="Không tìm thấy buổi học" description="Buổi học không tồn tại hoặc đã bị xóa khỏi lịch." action={<Button action="back" onClick={() => navigate('/teacher/timetable')}>Thời khóa biểu</Button>} />;
  if (!sessionTeachers(session, cls).includes(me) && !(session.assistantIds ?? cls.assistantIds ?? []).includes(me))
    return <EmptyState title="Bạn không dạy buổi này" description="Chỉ giáo viên / trợ giảng của buổi mới mở được trang dạy học." action={<Button action="back" onClick={() => navigate('/teacher/timetable')}>Thời khóa biểu</Button>} />;

  const phase = phaseOf(session);
  const cancelled = phase === 'cancelled';
  const origin = originOf(session);
  const makeupOf = session.makeupForId ? siblings.find((x) => x.id === session.makeupForId) : undefined;
  const lessonNo = curriculum && lesson ? curriculum.lessons.indexOf(lesson) + 1 : 0;
  const exam = curriculum?.exams.find((e) => e.id === session.examId);
  const isSub = !cls.teacherIds.includes(me);
  const room = sessionRoom(session, cls);
  const log = session.teacherLog;

  // Buổi trước còn dạy dở → nhắc dạy tiếp hôm nay
  const prev = siblings
    .filter((x) => x.status !== 'cancelled' && x.teacherLog && (x.date < session.date || (x.date === session.date && x.startTime < session.startTime)))
    .sort((a, b) => b.date.localeCompare(a.date) || b.startTime.localeCompare(a.startTime))[0];
  const prevLesson = prev && curriculum?.lessons.find((l) => l.id === prev.lessonId);
  const leftover =
    prev?.teacherLog?.coverage && prev.teacherLog.coverage !== 'full'
      ? (prevLesson?.chapters ?? []).filter((c) => !(prev.teacherLog!.coveredChapterIds ?? []).includes(c.id))
      : [];

  // ---------- Chấm công (theo buổi) ----------
  const mine = session.teacherAttendance?.find((t) => t.teacherId === me);
  const punch = () => {
    if (mine?.checkIn) return;
    const hm = dayjs().format('HH:mm');
    const late = phase !== 'upcoming' && toMin(hm) > toMin(session.startTime) + TEACHER_LATE_MIN && session.date === dayjs().format('YYYY-MM-DD');
    const role: TeacherAttendance['role'] = isSub ? 'substitute' : cls.teacherIds.includes(me) ? 'teacher' : 'assistant';
    const next: TeacherAttendance = { ...(mine ?? { teacherId: me, role }), source: 'self', checkedAt: new Date().toISOString(), checkIn: hm, status: late ? 'late' : 'present' };
    updateSession(session.id, { teacherAttendance: [...(session.teacherAttendance ?? []).filter((t) => t.teacherId !== me), next] });
    notify.toast.success(`Đã chấm công buổi ${session.no || ''} lúc ${hm}${late ? ' (muộn)' : ''}`);
  };

  // ---------- Điểm danh QR ----------
  const records = sessionRecords(session, roster, leaves);
  const sum = summarize(records);
  const waiting = records.filter((r) => r.status === 'absent' && roster.find((e) => e.studentId === r.studentId)?.status !== 'reserved').map((r) => r.studentId);
  const nameOf = (id: string) => students.find((x) => x.id === id)?.fullName ?? id;
  const scan = (studentId: string) => {
    const next = applyQrScan(records, session, studentId);
    const sm = summarize(next);
    updateSession(session.id, {
      records: next,
      attendance: { present: sm.attended, total: sm.total },
      attendanceBy: { name: user?.fullName ?? 'Giáo viên', at: new Date().toISOString() },
    });
    next.filter((r) => r.studentId === studentId && r.makeupFor).forEach((r) => recordMakeup(r.makeupFor!.leaveRequestId, r.status));
  };

  const openDoc = (id: string) => {
    setDocId(id);
    if (!isDesktop) viewerRef.current?.scrollIntoView({ behavior: 'smooth', block: 'start' });
  };
  const step = (d: number) => docs.length && setDocId(docs[(docIndex + d + docs.length) % docs.length].id);

  // Nhóm tài liệu theo phần / nguồn
  const groups = docs.reduce<{ name: string; items: typeof docs }[]>((acc, d) => {
    const g = acc.find((x) => x.name === d.group);
    if (g) g.items.push(d);
    else acc.push({ name: d.group, items: [d] });
    return acc;
  }, []);

  return (
    <>
      <PageHeader
        onBack={() => (window.history.length > 1 ? navigate(-1) : navigate('/teacher/timetable'))}
        breadcrumb={[{ title: 'Thời khóa biểu', onClick: () => navigate('/teacher/timetable') }, { title: 'Dạy học' }]}
        title={`${shortCode(cls)} · ${session.no ? `Buổi ${session.no}` : 'Buổi học thêm'}`}
        tags={
          <>
            <StatusTag tone={PHASE[phase].tone} icon={phase === 'live' ? CircleDot : CalendarClock} size="sm">
              {PHASE[phase].label}
            </StatusTag>
            {origin !== 'regular' && (
              <StatusTag tone="warning" icon={RotateCcw} size="sm">
                {origin === 'makeup' ? 'Buổi bù' : SESSION_ORIGIN.extra.label}
              </StatusTag>
            )}
            {isSub && (
              <StatusTag tone="primary" icon={ArrowLeftRight} size="sm">
                Dạy thay
              </StatusTag>
            )}
            {log?.endedAt && (
              <StatusTag tone={LESSON_COVERAGE[log.coverage ?? 'full'].tone} icon={CheckCircle2} size="sm">
                Đã báo cáo · {LESSON_COVERAGE[log.coverage ?? 'full'].label}
              </StatusTag>
            )}
            {session.confirmation && (
              <StatusTag tone="success" icon={ClipboardCheck} size="sm">
                Giáo vụ đã xác nhận
              </StatusTag>
            )}
          </>
        }
        subtitle={
          <span className="flex flex-wrap gap-x-3 gap-y-0.5">
            <span>
              {dayjs(session.date).format('dddd, DD/MM/YYYY')} · {session.startTime}–{session.endTime}
            </span>
            <span>{cls.online ? 'Lớp online' : `Phòng ${room ?? '—'}`}</span>
            <span>{cls.courseLabel}</span>
            <span className="inline-flex items-center gap-1">
              <Users size={14} aria-hidden /> {roster.length} học viên
            </span>
          </span>
        }
        footer={
          !cancelled && (
            <div className="flex flex-col gap-3 rounded-[var(--radius-card)] border border-line bg-surface px-3 py-2.5 md:flex-row md:items-center md:justify-between">
              <div className="flex flex-wrap items-center gap-x-4 gap-y-1 text-[13px]">
                <span className="inline-flex items-center gap-1.5">
                  <Fingerprint size={15} className="shrink-0 text-ink-muted" aria-hidden />
                  <span className="whitespace-nowrap">Chấm công:</span>
                  {mine?.checkIn ? (
                    <b className={mine.status === 'late' ? 'text-warning-700' : 'text-success-700'}>
                      {mine.checkIn}
                      {mine.checkOut ? ` – ${mine.checkOut}` : ''}
                      {mine.status === 'late' ? ' (muộn)' : ''}
                      {mine.source === 'staff' ? ' · giáo vụ chấm hộ' : ''}
                    </b>
                  ) : (
                    <span className="text-ink-muted">chưa chấm</span>
                  )}
                </span>
                <button type="button" onClick={() => setAttOpen(true)} className="inline-flex items-center gap-1.5 hover:text-primary-700">
                  <ClipboardCheck size={15} className="text-ink-muted" aria-hidden />
                  Điểm danh: <b className="tabular">{session.records ? `${sum.attended}/${sum.total} có mặt` : 'chưa có'}</b>
                  <span className="text-xs text-primary-700 underline">xem / sửa</span>
                </button>
              </div>
              <div className="flex flex-wrap items-center gap-2 lg:shrink-0 lg:flex-nowrap">
                {cls.online && cls.meetingUrl && (
                  <Button icon={Video} tone="success" variant="outline" onClick={() => window.open(cls.meetingUrl, '_blank', 'noopener')}>
                    Vào lớp online
                  </Button>
                )}
                {!mine?.checkIn && (
                  <Button icon={Fingerprint} tone="success" onClick={punch} tooltip="Chấm công buổi dạy — căn cứ tính lương theo buổi. Giờ ra ghi khi bấm Kết thúc buổi học.">
                    Chấm công
                  </Button>
                )}
                <Button icon={QrCode} variant="outline" onClick={() => setQrOpen(true)}>
                  Mở QR điểm danh
                </Button>
                <Button icon={log?.endedAt ? NotebookPen : Flag} tone={log?.endedAt ? 'warning' : 'danger'} variant={log?.endedAt ? 'soft' : 'solid'} onClick={() => setEndOpen(true)}>
                  {log?.endedAt ? 'Sửa báo cáo tiến độ' : 'Kết thúc buổi học'}
                </Button>
              </div>
            </div>
          )
        }
      />

      {cancelled && (
        <p className="mb-4 rounded-lg border border-danger-100 bg-danger-50 px-3 py-2 text-[13px] text-danger-700">
          Buổi học đã hủy{session.note ? ` — ${session.note}` : ''}. Giáo vụ sẽ xếp lịch học bù.
        </p>
      )}

      <div className="grid gap-4 lg:grid-cols-[minmax(300px,360px)_1fr]">
        {/* ---------- Cột trái: hôm nay học gì + tài liệu ---------- */}
        <aside className="flex min-w-0 flex-col gap-4 lg:max-h-[calc(100dvh-220px)] lg:overflow-y-auto lg:pr-1">
          <section className="rounded-[var(--radius-card)] border border-line bg-surface p-4">
            <h2 className="mb-2 flex items-center gap-1.5 text-sm font-semibold text-ink">
              <BookOpen size={16} className="text-primary-600" aria-hidden /> Hôm nay học gì
            </h2>

            {origin === 'makeup' && (
              <p className="mb-2 rounded-lg bg-warning-50 px-3 py-2 text-[13px] text-warning-700">
                Buổi bù{makeupOf ? ` cho buổi ${makeupOf.no} (${dayjs(makeupOf.date).format('DD/MM')}) đã hủy` : ''} — dạy nội dung của buổi bị hủy.
              </p>
            )}
            {origin === 'extra' && <p className="mb-2 rounded-lg bg-warning-50 px-3 py-2 text-[13px] text-warning-700">{SESSION_ORIGIN.extra.hint}.</p>}

            {session.kind === 'exam' ? (
              <div className="rounded-lg bg-danger-50 p-3 text-[13px] text-danger-700">
                <p className="font-semibold">Buổi thi: {exam?.title ?? 'Kiểm tra'}</p>
                {exam?.note && <p>{exam.note}</p>}
                <p className="mt-1 text-xs">Đề do giáo vụ tạo từ Ngân hàng câu hỏi — GV coi thi, chấm phần tự luận (nếu có).</p>
              </div>
            ) : lesson ? (
              <>
                <p className="text-xs text-ink-muted">{curriculum?.name}</p>
                <p className="text-base font-semibold text-ink">
                  Bài {lessonNo}: {lesson.title}
                </p>
                <ol className="mt-2 flex flex-col gap-1 text-[13px]">
                  {lesson.chapters.map((c, i) => {
                    const done = log?.coveredChapterIds?.includes(c.id);
                    return (
                      <li key={c.id} className="flex items-start gap-2">
                        <span className={cn('tabular mt-px grid size-5 shrink-0 place-items-center rounded-full text-[11px] font-medium', done ? 'bg-success-100 text-success-700' : 'bg-page text-ink-secondary')}>
                          {done ? <CheckCircle2 size={13} aria-hidden /> : i + 1}
                        </span>
                        <span className={cn('flex-1', done && 'text-ink-muted')}>
                          {c.title}
                          {c.materials.length > 0 && <span className="ml-1 text-xs text-ink-muted">· {c.materials.length} tài liệu</span>}
                        </span>
                      </li>
                    );
                  })}
                </ol>
                {lesson.hasQuiz && <p className="mt-2 text-xs text-primary-700">Cuối bài có quiz nhỏ — giao ở trang Bài tập.</p>}
              </>
            ) : (
              <p className="text-[13px] text-ink-muted">Buổi chưa gắn bài học trong lộ trình.</p>
            )}

            {leftover.length > 0 && prev && (
              <div className="mt-3 rounded-lg border border-warning-100 bg-warning-50 px-3 py-2 text-[13px] text-warning-700">
                <p className="flex items-center gap-1.5 font-medium">
                  <History size={14} aria-hidden /> Buổi {prev.no} còn dạy dở
                </p>
                <ul className="mt-1 list-disc pl-5">
                  {leftover.map((c) => (
                    <li key={c.id}>{c.title}</li>
                  ))}
                </ul>
              </div>
            )}
            {session.note && !cancelled && <p className="mt-3 rounded-lg bg-primary-50 px-3 py-2 text-[13px] text-primary-800">Ghi chú giáo vụ: {session.note}</p>}
          </section>

          {/* Báo cáo tiến độ đã gửi */}
          {log?.endedAt && (
            <section className="rounded-[var(--radius-card)] border border-line bg-surface p-4 text-[13px]">
              <div className="mb-1.5 flex items-center justify-between gap-2">
                <h2 className="flex items-center gap-1.5 text-sm font-semibold text-ink">
                  <NotebookPen size={16} className="text-primary-600" aria-hidden /> Báo cáo tiến độ
                </h2>
                <Button action="edit" size="sm" onClick={() => setEndOpen(true)} />
              </div>
              <p className="text-ink">{log.content}</p>
              {log.homework && <p className="mt-1 text-ink-secondary">Bài về nhà: {log.homework}</p>}
              {log.issues && (
                <p className="mt-1 flex gap-1.5 text-warning-700">
                  <FileWarning size={14} className="mt-0.5 shrink-0" aria-hidden /> {log.issues}
                </p>
              )}
              <p className="mt-1.5 text-xs text-ink-muted">Kết thúc {fmtDateTime(log.endedAt)} · sửa lần cuối {fmtDateTime(log.at)}</p>
            </section>
          )}

          <section className="rounded-[var(--radius-card)] border border-line bg-surface p-2">
            <h2 className="px-2 pb-1 pt-2 text-sm font-semibold text-ink">Tài liệu buổi học ({docs.length})</h2>
            {!docs.length && <p className="px-2 pb-3 text-[13px] text-ink-muted">Chưa có tài liệu — thêm ở “Tài liệu của tôi” và gắn với buổi này.</p>}
            {groups.map((g) => (
              <div key={g.name} className="mt-1">
                <p className="px-2 py-1 text-xs font-medium uppercase tracking-wide text-ink-muted">{g.name}</p>
                <ul>
                  {g.items.map((d) => {
                    const K = DOC_KIND[d.kind];
                    const active = d.id === doc?.id;
                    return (
                      <li key={d.id}>
                        <button
                          type="button"
                          onClick={() => openDoc(d.id)}
                          aria-current={active || undefined}
                          className={cn('flex w-full items-center gap-2.5 rounded-lg px-2 py-2 text-left transition-colors', active ? 'bg-primary-50 ring-1 ring-primary-200' : 'hover:bg-page')}
                        >
                          <span className={cn('grid size-8 shrink-0 place-items-center rounded-md', K.cls)}>
                            <K.icon size={16} aria-hidden />
                          </span>
                          <span className="min-w-0 flex-1">
                            <span className={cn('block truncate text-[13px]', active ? 'font-semibold text-primary-800' : 'text-ink')}>{d.name}</span>
                            <span className="text-xs text-ink-muted">
                              {K.label}
                              {d.teacherOnly && <span className="ml-1.5 rounded bg-warning-50 px-1 text-warning-700">Chỉ GV</span>}
                            </span>
                          </span>
                        </button>
                      </li>
                    );
                  })}
                </ul>
              </div>
            ))}
          </section>
        </aside>

        {/* ---------- Cột phải: trình chiếu ---------- */}
        <div ref={viewerRef} className="min-w-0 scroll-mt-4 lg:h-[calc(100dvh-220px)]">
          <DocViewer doc={doc} index={docIndex} total={docs.length} onPrev={() => step(-1)} onNext={() => step(1)} />
        </div>
      </div>

      {qrOpen && <QrModal s={session} cls={cls} scanned={records.filter((r) => r.method === 'qr' && r.checkInAt).length} total={records.length} waiting={waiting} nameOf={nameOf} onScan={scan} onClose={() => setQrOpen(false)} />}

      <Drawer open={attOpen} onClose={() => setAttOpen(false)} width={isDesktop ? 720 : '100%'} title={`Điểm danh · ${shortCode(cls)} · Buổi ${session.no}`} destroyOnHidden>
        <SessionAttendanceTab key={String(attOpen)} cls={cls} s={session} siblings={siblings} editable={!cancelled} />
      </Drawer>

      <EndSessionModal open={endOpen} onClose={() => setEndOpen(false)} session={session} lesson={lesson} teacherId={me} />
    </>
  );
}
