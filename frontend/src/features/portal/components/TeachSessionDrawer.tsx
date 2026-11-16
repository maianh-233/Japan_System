import { useMemo, useState } from 'react';
import { Drawer, Form, Tabs } from 'antd';
import dayjs from 'dayjs';
import { BookOpen, FileText, LogIn, LogOut, NotebookPen, Video } from 'lucide-react';
import { Button, StatusTag, TextArea, useNotify } from '@/components/common';
import { useClassStore } from '@/features/academic/classes/classStore';
import { SESSION_STATUS } from '@/features/academic/classes/constants';
import { SessionAttendanceTab } from '@/features/academic/classes/detail/SessionAttendanceTab';
import { sessionRoom, startOf } from '@/features/academic/classes/sessionUtils';
import type { ClassInfo, SessionLog, TeacherAttendance } from '@/features/academic/classes/types';
import { useCurriculumStore } from '@/features/academic/curriculum/curriculumStore';
import { useAuth } from '@/hooks/useAuth';
import { useBreakpoint } from '@/hooks/useBreakpoint';
import { fmtDateTime } from '@/lib/date';
import { usePortalStore } from '../portalStore';
import { shortCode, useTeacherId } from '../usePortal';

interface Props {
  cls: ClassInfo;
  sessionId: string | null;
  onClose: () => void;
}

const toMin = (t: string) => Number(t.slice(0, 2)) * 60 + Number(t.slice(3, 5));

/** GV mở 1 buổi: nội dung cần dạy + tài liệu · chấm công của mình · điểm danh · nhật ký buổi */
export function TeachSessionDrawer({ cls, sessionId, onClose }: Props) {
  const { user } = useAuth();
  const { isMobile } = useBreakpoint();
  const notify = useNotify();
  const me = useTeacherId();
  const session = useClassStore((s) => s.sessions.find((x) => x.id === sessionId));
  const allSessions = useClassStore((s) => s.sessions);
  const siblings = useMemo(() => allSessions.filter((x) => x.classId === cls.id), [allSessions, cls.id]);
  const updateSession = useClassStore((s) => s.updateSession);
  const curriculum = useCurriculumStore((s) => s.curricula.find((c) => c.id === cls.curriculumId));
  const allDocs = usePortalStore((s) => s.teacherDocs);
  const docs = useMemo(() => allDocs.filter((d) => d.teacherId === me && (d.sessionId === sessionId || d.classIds.includes(cls.id))), [allDocs, me, sessionId, cls.id]);
  const [tab, setTab] = useState('content');
  const [form] = Form.useForm<Pick<SessionLog, 'content' | 'homework' | 'issues'>>();

  if (!session) return null;
  const lesson = curriculum?.lessons.find((l) => l.id === session.lessonId);
  const lessonNo = curriculum ? curriculum.lessons.findIndex((l) => l.id === session.lessonId) + 1 : 0;
  const exam = curriculum?.exams.find((e) => e.id === session.examId);
  const mine = session.teacherAttendance?.find((t) => t.teacherId === me);
  const isToday = session.date === dayjs().format('YYYY-MM-DD');
  const started = !startOf(session).isAfter(dayjs().add(30, 'minute'));
  const nowHm = dayjs().format('HH:mm');

  const punch = (kind: 'in' | 'out') => {
    const rest = (session.teacherAttendance ?? []).filter((t) => t.teacherId !== me);
    const base: TeacherAttendance = mine ?? { teacherId: me, status: 'present' };
    const next: TeacherAttendance =
      kind === 'in'
        ? { ...base, source: 'self', checkedAt: new Date().toISOString(), checkIn: nowHm, status: toMin(nowHm) > toMin(session.startTime) + 5 ? 'late' : 'present' }
        : { ...base, source: 'self', checkedAt: new Date().toISOString(), checkOut: nowHm };
    updateSession(session.id, { teacherAttendance: [...rest, next] });
    notify.toast.success(kind === 'in' ? `Đã check-in lúc ${nowHm}${next.status === 'late' ? ' (muộn)' : ''}` : `Đã check-out lúc ${nowHm}`);
  };

  const saveLog = (v: Pick<SessionLog, 'content' | 'homework' | 'issues'>) => {
    updateSession(session.id, { teacherLog: { ...v, author: user?.fullName ?? '', at: new Date().toISOString() } });
    notify.toast.success('Đã lưu nhật ký buổi');
  };

  return (
    <Drawer
      open
      onClose={onClose}
      width={isMobile ? '100%' : 760}
      title={`${shortCode(cls)} · Buổi ${session.no} · ${dayjs(session.date).format('dd DD/MM')} ${session.startTime}–${session.endTime}`}
      extra={
        <StatusTag tone={SESSION_STATUS[session.status].tone} icon={BookOpen} size="sm">
          {SESSION_STATUS[session.status].label}
        </StatusTag>
      }
    >
      {/* Chấm công nhanh */}
      <div className="mb-4 flex flex-wrap items-center justify-between gap-2 rounded-xl border border-line bg-canvas px-3 py-2.5 text-[13px]">
        <span>
          {cls.online ? 'Lớp online' : `Phòng ${sessionRoom(session, cls) ?? '—'}`} · Chấm công:{' '}
          {mine?.checkIn ? (
            <b className={mine.status === 'late' ? 'text-warning-700' : 'text-success-700'}>
              vào {mine.checkIn}
              {mine.checkOut ? ` – ra ${mine.checkOut}` : ''}
            </b>
          ) : (
            <span className="text-ink-muted">chưa chấm</span>
          )}
        </span>
        <span className="flex gap-1.5">
          {cls.online && cls.meetingUrl && (
            <Button icon={Video} size="sm" tone="success" onClick={() => notify.success({ title: 'Đang mở phòng học online', description: cls.meetingUrl })}>
              Vào lớp
            </Button>
          )}
          {isToday && session.status !== 'cancelled' && !mine?.checkIn && (
            <Button icon={LogIn} size="sm" disabled={!started} onClick={() => punch('in')} tooltip={!started ? 'Mở trước giờ học 30 phút' : undefined}>
              Check-in
            </Button>
          )}
          {isToday && mine?.checkIn && !mine.checkOut && (
            <Button icon={LogOut} size="sm" variant="soft" tone="warning" onClick={() => punch('out')}>
              Check-out
            </Button>
          )}
        </span>
      </div>

      <Tabs
        activeKey={tab}
        onChange={setTab}
        items={[
          {
            key: 'content',
            label: 'Nội dung buổi',
            children: (
              <div className="flex flex-col gap-4 text-[13px]">
                {session.kind === 'exam' ? (
                  <div className="rounded-xl bg-danger-50 p-3 text-danger-700">
                    <p className="font-semibold">Buổi thi: {exam?.title ?? 'Kiểm tra'}</p>
                    {exam?.note && <p>{exam.note}</p>}
                    <p className="mt-1 text-xs">Đề do giáo vụ tạo từ Ngân hàng câu hỏi — GV coi thi, chấm phần tự luận (nếu có).</p>
                  </div>
                ) : lesson ? (
                  <div className="rounded-xl border border-line p-3">
                    <p className="text-xs text-ink-muted">{curriculum?.name}</p>
                    <p className="text-base font-semibold text-ink">
                      Bài {lessonNo}: {lesson.title}
                    </p>
                    <ol className="mt-2 flex list-decimal flex-col gap-1 pl-5">
                      {lesson.chapters.map((c) => (
                        <li key={c.id}>
                          {c.title}
                          {c.materials.length > 0 && <span className="ml-1 text-xs text-ink-muted">· {c.materials.length} học liệu</span>}
                        </li>
                      ))}
                    </ol>
                    {lesson.hasQuiz && <p className="mt-2 text-xs text-primary-700">Cuối bài có quiz nhỏ — giao ở trang Bài tập.</p>}
                  </div>
                ) : (
                  <p className="text-ink-muted">Buổi chưa gắn bài học trong lộ trình.</p>
                )}
                <div>
                  <p className="mb-1.5 font-semibold text-ink">Tài liệu của tôi cho lớp này</p>
                  <ul className="flex flex-col gap-1">
                    {docs.map((d) => (
                      <li key={d.id} className="flex items-center gap-2">
                        <FileText size={14} className="text-ink-muted" aria-hidden /> {d.title}
                      </li>
                    ))}
                    {!docs.length && <li className="text-ink-muted">Chưa có — thêm ở “Tài liệu của tôi”.</li>}
                  </ul>
                </div>
                {session.note && <p className="rounded-lg bg-warning-50 px-3 py-2 text-warning-700">Ghi chú giáo vụ: {session.note}</p>}
              </div>
            ),
          },
          {
            key: 'attendance',
            label: `Điểm danh${session.attendance ? ` (${session.attendance.present}/${session.attendance.total})` : ''}`,
            children: <SessionAttendanceTab cls={cls} s={session} siblings={siblings} editable={session.status !== 'cancelled'} />,
          },
          {
            key: 'log',
            label: session.teacherLog ? 'Nhật ký buổi ✓' : 'Nhật ký buổi',
            children: (
              <Form form={form} layout="vertical" initialValues={session.teacherLog} onFinish={saveLog} disabled={session.status === 'cancelled' || startOf(session).isAfter(dayjs())}>
                {startOf(session).isAfter(dayjs()) && <p className="mb-3 text-[13px] text-ink-muted">Ghi nhật ký sau khi buổi học bắt đầu.</p>}
                <Form.Item name="content" label="Đã dạy gì, lớp tiếp thu thế nào" rules={[{ required: true, message: 'Nhập nội dung đã dạy' }]}>
                  <TextArea rows={3} maxLength={1000} placeholder="Vd: Hoàn thành mẫu ～ば, lớp nắm tốt; phần luyện nghe còn chậm." />
                </Form.Item>
                <Form.Item name="homework" label="Bài tập về nhà đã dặn">
                  <TextArea rows={2} maxLength={500} />
                </Form.Item>
                <Form.Item name="issues" label="Cần trung tâm hỗ trợ" extra="Giáo vụ nhận được ngay (HV vắng nhiều, máy chiếu hỏng...)">
                  <TextArea rows={2} maxLength={500} />
                </Form.Item>
                <div className="flex items-center justify-between gap-2">
                  <span className="text-xs text-ink-muted">{session.teacherLog && `Lưu lần cuối ${fmtDateTime(session.teacherLog.at)}`}</span>
                  <Button icon={NotebookPen} htmlType="submit">
                    Lưu nhật ký
                  </Button>
                </div>
              </Form>
            ),
          },
        ]}
      />
    </Drawer>
  );
}
