import { useEffect, useMemo, useState } from 'react';
import { Input, Tabs, Tooltip } from 'antd';
import dayjs from 'dayjs';
import { MessageSquareQuote, NotebookPen, Users } from 'lucide-react';
import { Link, useNavigate, useParams } from 'react-router-dom';
import { Button, DataTable, EmptyState, PageHeader, StatusTag, UserCell, useNotify, type DataColumn } from '@/components/common';
import { useClassStore } from '@/features/academic/classes/classStore';
import { EVAL_RECOMMENDATION, scheduleLabel, SESSION_STATUS } from '@/features/academic/classes/constants';
import { StudentLearningDrawer } from '@/features/academic/classes/detail/StudentLearningDrawer';
import { EvaluationModal } from '@/features/academic/classes/evaluation/EvaluationModal';
import { useClassStudentStats, type StudentStats } from '@/features/academic/classes/studentInsights';
import { notStarted } from '@/features/academic/classes/sessionUtils';
import type { ClassInfo, ClassSession } from '@/features/academic/classes/types';
import { useCurriculumStore } from '@/features/academic/curriculum/curriculumStore';
import * as learningApi from '@/features/academic/classes/learningApi';
import { useCrmStore } from '@/features/crm/crmStore';
import { shortCode, useTeacherId } from '@/features/portal/usePortal';
import { cn } from '@/lib/cn';
import { USE_API } from '@/lib/api/config';
import * as portalApi from '@/features/portal/portalApi';

type StudentRow = StudentStats & { id: string; name: string; code: string };

/** Cổng GV › chi tiết lớp: buổi học (mở để dạy) · học viên · kết quả thi & nhận xét */
export default function TeacherClassPage() {
  const { id } = useParams();
  const navigate = useNavigate();
  const me = useTeacherId();
  const cls = useClassStore((s) => s.classes.find((c) => c.id === id));
  // Chế độ API: nhận xét cuối khóa của lớp (chỉ GV của lớp xem / ghi)
  useEffect(() => {
    if (USE_API && id) void portalApi.loadEvaluations(id, true).catch(() => undefined);
  }, [id]);
  if (!cls || !cls.teacherIds.includes(me))
    return <EmptyState title="Không tìm thấy lớp" description="Lớp không tồn tại hoặc bạn không dạy lớp này." action={<Button action="back" onClick={() => navigate('/teacher/classes')}>Lớp của tôi</Button>} />;
  return <ClassView cls={cls} />;
}

function ClassView({ cls }: { cls: ClassInfo }) {
  const navigate = useNavigate();
  const notify = useNotify();
  const me = useTeacherId();
  const allSessions = useClassStore((s) => s.sessions);
  const examResults = useClassStore((s) => s.examResults);
  const classExams = useClassStore((s) => s.classExams);
  const updateExamResult = useClassStore((s) => s.updateExamResult);
  const students = useCrmStore((s) => s.students);
  const curriculum = useCurriculumStore((s) => s.curricula.find((c) => c.id === cls.curriculumId));
  const [tab, setTab] = useState('sessions');
  const [openStudent, setOpenStudent] = useState<string | null>(null);
  const [evaluating, setEvaluating] = useState<string | null>(null);
  const evaluations = useClassStore((s) => s.evaluations);
  const [showPast, setShowPast] = useState(false);

  const sessions = useMemo(() => allSessions.filter((s) => s.classId === cls.id).sort((a, b) => (a.date + a.startTime).localeCompare(b.date + b.startTime)), [allSessions, cls.id]);
  const statsResult = useClassStudentStats(cls);

  const lessonTitle = (s: ClassSession) => {
    if (s.kind === 'exam') return curriculum?.exams.find((e) => e.id === s.examId)?.title ?? 'Bài thi';
    const i = curriculum?.lessons.findIndex((l) => l.id === s.lessonId) ?? -1;
    return i >= 0 ? `Bài ${i + 1}: ${curriculum!.lessons[i].title}` : '—';
  };
  const upcoming = sessions.filter((s) => notStarted(s) || s.date === dayjs().format('YYYY-MM-DD'));
  const past = sessions.filter((s) => !upcoming.includes(s)).reverse();
  const list = showPast ? past : upcoming.slice(0, 12);

  const studentRows: StudentRow[] = [...statsResult.map.values()]
    .filter((x) => x.enrollment.status !== 'dropped' && x.enrollment.status !== 'transferred')
    .map((x) => {
      const st = students.find((s) => s.id === x.enrollment.studentId);
      return { ...x, id: x.enrollment.studentId, name: st?.fullName ?? x.enrollment.studentId, code: st?.code ?? '' };
    })
    .sort((a, b) => b.risk - a.risk);

  // Chế độ API: mốc thi đã có kỳ thi phát hành (nhập điểm / công bố ở trang Kết quả thi)
  const exams =
    curriculum?.exams.filter(
      (e) =>
        examResults.some((r) => r.classId === cls.id && r.examId === e.id) ||
        (USE_API && classExams.some((x) => x.classId === cls.id && x.examId === e.id && x.status === 'published')),
    ) ?? [];

  const studentCols: DataColumn<StudentRow>[] = [
    { key: 'name', title: 'Học viên', width: 200, mobile: 'title', render: (_, r) => <UserCell name={r.name} subtitle={r.code} size="sm" /> },
    { key: 'att', title: 'Chuyên cần', width: 110, align: 'center', render: (_, r) => (r.attendance.rate !== undefined ? <span className={cn('tabular', r.attendance.rate < 80 && 'font-semibold text-danger-700')}>{r.attendance.rate}%</span> : '—') },
    { key: 'abs', title: 'Vắng', width: 80, align: 'center', render: (_, r) => <span className="tabular">{r.attendance.absent + r.attendance.excused}</span> },
    { key: 'sub', title: 'Nộp bài', width: 100, align: 'center', render: (_, r) => <span className="tabular">{r.assignments.submitted}/{r.assignments.due}</span> },
    { key: 'avg', title: 'Điểm BT', width: 90, align: 'center', render: (_, r) => (r.assignments.avgPct !== undefined ? <span className="tabular">{r.assignments.avgPct}%</span> : '—') },
    { key: 'exam', title: 'Điểm thi', width: 90, align: 'center', render: (_, r) => (r.exams.avgPct !== undefined ? <span className="tabular">{r.exams.avgPct}%</span> : '—') },
    {
      key: 'eval',
      title: 'Nhận xét cuối khóa',
      width: 190,
      render: (_, r) => {
        const ev = evaluations.find((e) => e.classId === cls.id && e.studentId === r.id);
        return ev ? (
          <button type="button" onClick={(e) => { e.stopPropagation(); setEvaluating(r.id); }} className="text-left" title="Bấm để sửa">
            <StatusTag tone={EVAL_RECOMMENDATION[ev.recommendation].tone} icon={MessageSquareQuote} size="sm">
              {EVAL_RECOMMENDATION[ev.recommendation].label}
            </StatusTag>
          </button>
        ) : (
          <Button size="sm" variant="ghost" icon={MessageSquareQuote} onClick={(e) => { e.stopPropagation(); setEvaluating(r.id); }}>
            Viết nhận xét
          </Button>
        );
      },
    },
    {
      key: 'flags',
      title: 'Cần chú ý',
      width: 220,
      mobile: 'aside',
      render: (_, r) =>
        r.flags.length ? (
          <span className="flex flex-wrap gap-1">
            {r.flags
              .filter((f) => f.key !== 'tuition')
              .map((f) => (
                <span key={f.key} className={cn('rounded px-1.5 py-0.5 text-[11px]', f.tone === 'danger' ? 'bg-danger-50 text-danger-700' : 'bg-warning-50 text-warning-700')}>
                  {f.label}
                </span>
              ))}
          </span>
        ) : (
          <span className="text-ink-muted">—</span>
        ),
    },
  ];

  return (
    <>
      <PageHeader
        title={shortCode(cls)}
        subtitle={`${cls.courseLabel} · ${scheduleLabel(cls)} · ${cls.online ? 'Online' : `Phòng ${cls.room ?? '—'}`}`}
        breadcrumb={[{ title: 'Lớp của tôi', href: '/teacher/classes' }, { title: shortCode(cls) }]}
        actions={
          <Button icon={NotebookPen} variant="outline" collapseOnMobile onClick={() => navigate('/teacher/assignments')}>
            Bài tập
          </Button>
        }
      />

      <Tabs
        activeKey={tab}
        onChange={setTab}
        items={[
          {
            key: 'sessions',
            label: `Buổi học (${sessions.length})`,
            children: (
              <>
                <div className="mb-3 flex gap-2">
                  {[
                    [false, `Sắp tới (${upcoming.length})`],
                    [true, `Đã dạy (${past.length})`],
                  ].map(([v, l]) => (
                    <button
                      key={String(v)}
                      type="button"
                      onClick={() => setShowPast(v as boolean)}
                      className={cn('h-8 rounded-full border px-3 text-[13px] font-medium', showPast === v ? 'border-primary-600 bg-primary-600 text-white' : 'border-line bg-surface text-ink-secondary')}
                    >
                      {l as string}
                    </button>
                  ))}
                </div>
                <ul className="flex flex-col divide-y divide-line rounded-xl border border-line bg-surface">
                  {list.map((s) => {
                    const today = s.date === dayjs().format('YYYY-MM-DD');
                    return (
                      <li key={s.id}>
                        <button type="button" onClick={() => navigate(`/teacher/teaching/${s.id}`)} className={cn('flex w-full items-center gap-3 px-4 py-3 text-left hover:bg-canvas', today && 'bg-primary-50/60')}>
                          <span className="w-16 shrink-0 text-center">
                            <span className="block text-xs text-ink-muted">{dayjs(s.date).format('dd')}</span>
                            <span className="tabular block font-semibold text-ink">{dayjs(s.date).format('DD/MM')}</span>
                          </span>
                          <span className="min-w-0 flex-1">
                            <span className={cn('block truncate font-medium', s.kind === 'exam' ? 'text-danger-700' : 'text-ink')}>
                              Buổi {s.no} · {lessonTitle(s)}
                            </span>
                            <span className="block text-xs text-ink-muted">
                              {s.startTime}–{s.endTime}
                              {s.attendance && ` · có mặt ${s.attendance.present}/${s.attendance.total}`}
                              {s.status === 'completed' && !s.teacherLog && <span className="text-danger-700"> · chưa ghi nhật ký</span>}
                              {s.teacherIds && !s.teacherIds.includes(me) && <span className="text-warning-700"> · GV khác dạy thay</span>}
                            </span>
                          </span>
                          <StatusTag tone={SESSION_STATUS[s.status].tone} icon={NotebookPen} size="sm">
                            {today ? 'Hôm nay' : SESSION_STATUS[s.status].label}
                          </StatusTag>
                        </button>
                      </li>
                    );
                  })}
                  {!list.length && <li className="px-4 py-6 text-center text-ink-muted">Không có buổi nào.</li>}
                </ul>
              </>
            ),
          },
          {
            key: 'students',
            label: `Học viên (${studentRows.length})`,
            children: (
              <>
              {(() => {
                const done = studentRows.filter((r) => evaluations.some((e) => e.classId === cls.id && e.studentId === r.id)).length;
                const left = sessions.filter((s) => s.status === 'scheduled').length;
                const nearEnd = cls.status === 'finished' || left <= 4;
                return (
                  <div className={cn('mb-3 flex flex-wrap items-center gap-x-3 gap-y-1 rounded-[var(--radius-card)] border px-3.5 py-2.5 text-[13px]', nearEnd && done < studentRows.length ? 'border-warning-100 bg-warning-50 text-warning-700' : 'border-line bg-surface text-ink-secondary')}>
                    <MessageSquareQuote size={16} className="shrink-0" aria-hidden />
                    <span>
                      Nhận xét cuối khóa: <b className="tabular">{done}/{studentRows.length}</b> học viên
                      {nearEnd && done < studentRows.length ? (cls.status === 'finished' ? ' — lớp đã kết thúc, hoàn thành nhận xét giúp Sale tư vấn khóa tiếp theo.' : ` — còn ${left} buổi là kết thúc khóa, nhớ viết nhận xét.`) : ' — Sale dùng để tư vấn học viên học khóa tiếp theo (học viên không thấy).'}
                    </span>
                  </div>
                );
              })()}
              <DataTable<StudentRow>
                storageKey="teacher-class-students"
                columns={studentCols}
                data={studentRows}
                rowKey="id"
                unitLabel="học viên"
                searchKeys={['name', 'code']}
                onRowClick={(r) => setOpenStudent(r.id)}
                empty={{ icon: Users, title: 'Lớp chưa có học viên' }}
              />
              </>
            ),
          },
          {
            key: 'exams',
            label: `Kết quả thi (${exams.length})`,
            children: exams.length ? (
              <div className="flex flex-col gap-4">
                {exams.map((e) => {
                  const rs = examResults.filter((r) => r.classId === cls.id && r.examId === e.id);
                  return (
                    <section key={e.id} className="rounded-xl border border-line bg-surface p-4">
                      <div className="mb-2 flex flex-wrap items-center justify-between gap-2">
                        <p className="font-semibold text-ink">{e.title}</p>
                        {USE_API && classExams.some((x) => x.classId === cls.id && x.examId === e.id && x.status === 'published') && (
                          <Link to={`/teacher/classes/${cls.id}/exams/${e.id}`} className="text-[13px] font-medium text-primary-700 hover:underline">
                            Nhập điểm / công bố →
                          </Link>
                        )}
                      </div>
                      <ul className="flex flex-col gap-2 text-[13px]">
                        {rs.map((r) => {
                          const total = r.sections.reduce((n, x) => n + x.score, 0);
                          const max = r.sections.reduce((n, x) => n + x.max, 0);
                          return (
                            <li key={r.id} className="grid items-center gap-2 sm:grid-cols-[180px_90px_1fr]">
                              <span className="truncate">{students.find((s) => s.id === r.studentId)?.fullName ?? r.studentId}</span>
                              <Tooltip title={r.sections.map((x) => `${x.name}: ${x.score}/${x.max}`).join(' · ')}>
                                <span className={cn('tabular font-semibold', r.status === 'absent' ? 'text-ink-muted' : max && total / max < 0.6 ? 'text-danger-700' : 'text-ink')}>{r.status === 'absent' ? 'Vắng' : `${total}/${max}`}</span>
                              </Tooltip>
                              <Input
                                size="small"
                                defaultValue={r.comment}
                                placeholder="Nhận xét cho học viên / phụ huynh..."
                                onBlur={(ev) => {
                                  if (ev.target.value === (r.comment ?? '')) return;
                                  const ce = classExams.find((x) => x.classId === cls.id && x.examId === e.id && x.status === 'published');
                                  if (USE_API) {
                                    if (!ce) return notify.warning({ title: 'Kết quả cũ (trước khi có kỳ thi) — không sửa được nhận xét ở đây' });
                                    void learningApi
                                      .saveExamResult(ce.id, r.studentId, { absent: r.status === 'absent', scores: r.sections.map((x) => x.score), comment: ev.target.value }, true)
                                      .then(() => notify.toast.success('Đã lưu nhận xét'))
                                      .catch((err: Error) => notify.error({ title: err.message }));
                                    return;
                                  }
                                  updateExamResult(r.id, { comment: ev.target.value });
                                  notify.toast.success('Đã lưu nhận xét');
                                }}
                              />
                            </li>
                          );
                        })}
                      </ul>
                    </section>
                  );
                })}
              </div>
            ) : (
              <EmptyState title="Chưa có kỳ thi nào có kết quả" description="Kết quả hiện ở đây sau khi lớp thi giữa kỳ / cuối kỳ." />
            ),
          },
        ]}
      />

      <StudentLearningDrawer cls={cls} studentId={openStudent} onClose={() => setOpenStudent(null)} viewer="teacher" onEvaluate={setEvaluating} />
      {evaluating && (
        <EvaluationModal
          open
          onClose={() => setEvaluating(null)}
          classId={cls.id}
          studentId={evaluating}
          studentName={students.find((s) => s.id === evaluating)?.fullName ?? evaluating}
          teacherId={me}
        />
      )}
    </>
  );
}
