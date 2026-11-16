import { useMemo, useState } from 'react';
import { Form, Tabs } from 'antd';
import dayjs from 'dayjs';
import { CheckCircle2, Clock, FileUp, ListChecks, MessageSquareText, NotebookPen, Play, TriangleAlert } from 'lucide-react';
import { Button, EmptyState, FileUpload, ModalForm, PageHeader, StatusTag, TextArea, useNotify, type UploadItem } from '@/components/common';
import { AttachmentList } from '@/features/academic/classes/AttachmentList';
import { useClassStore } from '@/features/academic/classes/classStore';
import type { Assignment, Submission, SubmissionFile } from '@/features/academic/classes/types';
import { useCurriculumStore } from '@/features/academic/curriculum/curriculumStore';
import { useQuestionStore } from '@/features/academic/questions/questionStore';
import type { Question } from '@/features/academic/questions/types';
import { QuizRunner } from '@/features/portal/components/QuizRunner';
import { levelOf, pickQuiz } from '@/features/portal/quiz';
import { shortCode, useStudentId, useStudentLearning } from '@/features/portal/usePortal';
import { cn } from '@/lib/cn';
import { fmtDateTime } from '@/lib/date';

type State = 'todo' | 'submitted' | 'graded' | 'missed';
interface Item {
  a: Assignment;
  classCode: string;
  sub?: Submission;
  state: State;
  dueAt: dayjs.Dayjs;
}

const fileKind = (name: string): SubmissionFile['kind'] => {
  const ext = name.split('.').pop()?.toLowerCase() ?? '';
  if (ext === 'pdf') return 'pdf';
  if (['png', 'jpg', 'jpeg', 'webp', 'heic'].includes(ext)) return 'image';
  if (['mp3', 'm4a', 'wav'].includes(ext)) return 'audio';
  return 'doc';
};

/** Cổng HV › Bài tập: làm & nộp bài (nộp file / trắc nghiệm tự chấm), xem điểm + nhận xét GV */
export default function StudentAssignmentsPage() {
  const notify = useNotify();
  const studentId = useStudentId();
  const learning = useStudentLearning(studentId);
  const assignments = useClassStore((s) => s.assignments);
  const submissions = useClassStore((s) => s.submissions);
  const submit = useClassStore((s) => s.submitAssignment);
  const questions = useQuestionStore((s) => s.questions);
  const curricula = useCurriculumStore((s) => s.curricula);
  const [tab, setTab] = useState<State>('todo');
  const [uploading, setUploading] = useState<Item | null>(null);
  const [quizFor, setQuizFor] = useState<Item | null>(null);

  const items: Item[] = useMemo(() => {
    const ids = learning.filter((x) => x.enrollment.status !== 'dropped').map((x) => x.cls);
    return assignments
      .filter((a) => ids.some((c) => c.id === a.classId))
      .map((a) => {
        const cls = ids.find((c) => c.id === a.classId)!;
        const sub = submissions.find((x) => x.assignmentId === a.id && x.studentId === studentId);
        const dueAt = dayjs(a.dueDate).endOf('day');
        const state: State = sub ? (sub.score !== undefined ? 'graded' : 'submitted') : dueAt.isBefore(dayjs()) ? 'missed' : 'todo';
        return { a, classCode: shortCode(cls), sub, state, dueAt };
      })
      .sort((x, y) => (x.state === 'todo' ? x.dueAt.valueOf() - y.dueAt.valueOf() : y.dueAt.valueOf() - x.dueAt.valueOf()));
  }, [learning, assignments, submissions, studentId]);

  const count = (s: State) => items.filter((i) => i.state === s).length;
  const list = items.filter((i) => i.state === tab || (tab === 'todo' && i.state === 'todo'));

  // Đề được bốc 1 lần lúc bắt đầu làm (không đổi khi nộp xong đang xem đáp án)
  const [quizQuestions, setQuizQuestions] = useState<Question[]>([]);
  const startQuiz = (i: Item) => {
    const q = i.a.quiz!;
    const cls = learning.find((x) => x.cls.id === i.a.classId)?.cls;
    const cur = curricula.find((c) => c.id === cls?.curriculumId);
    // shuffle = mỗi HV 1 đề; seed theo lượt làm để làm lại có đề khác
    setQuizQuestions(pickQuiz(questions, { level: levelOf(cur?.level), skills: q.skills, lessonIds: q.lessonIds, difficulty: q.difficulty, count: q.questionCount, seed: `${i.a.id}-${q.shuffle ? studentId : ''}-${i.sub?.quiz?.attempts ?? 0}` }));
    setQuizFor(i);
  };

  const attemptsLeft = (i: Item) => (i.a.quiz ? i.a.quiz.attempts - (i.sub?.quiz?.attempts ?? 0) : 0);

  const card = (i: Item) => {
    const late = i.sub && dayjs(i.sub.submittedAt).isAfter(i.dueAt);
    const left = i.dueAt.diff(dayjs(), 'hour');
    return (
      <article key={i.a.id} className={cn('rounded-xl border bg-surface p-4', i.state === 'todo' && left < 48 ? 'border-warning-100' : 'border-line')}>
        <div className="flex flex-wrap items-start justify-between gap-2">
          <div className="min-w-0">
            <p className="font-semibold text-ink">{i.a.title}</p>
            <p className="text-xs text-ink-muted">
              {i.classCode} · {i.a.type === 'quiz' ? `Trắc nghiệm ${i.a.quiz?.questionCount} câu${i.a.quiz?.timeLimitMinutes ? ` · ${i.a.quiz.timeLimitMinutes} phút` : ''}` : 'Nộp bài'} · hạn {i.dueAt.format('DD/MM')}
            </p>
          </div>
          {i.state === 'todo' && (
            <StatusTag tone={left < 48 ? 'warning' : 'info'} icon={Clock} size="sm">
              {left < 24 ? `Còn ${Math.max(0, left)} giờ` : `Còn ${Math.ceil(left / 24)} ngày`}
            </StatusTag>
          )}
          {i.state === 'missed' && (
            <StatusTag tone="danger" icon={TriangleAlert} size="sm">
              Quá hạn, chưa nộp
            </StatusTag>
          )}
          {i.state === 'submitted' && (
            <StatusTag tone="info" icon={CheckCircle2} size="sm">
              Đã nộp{late ? ' (muộn)' : ''}
            </StatusTag>
          )}
          {i.state === 'graded' && (
            <span className="tabular rounded-lg bg-success-50 px-2.5 py-1 text-sm font-bold text-success-700">
              {i.sub!.score}/{i.a.maxScore}
            </span>
          )}
        </div>
        {i.a.description && <p className="mt-2 text-[13px] text-ink-secondary">{i.a.description}</p>}
        {i.a.attachments?.length ? (
          <div className="mt-2">
            <p className="mb-1 text-xs font-medium text-ink-muted">Đề bài của giáo viên — bấm để mở / tải về</p>
            <AttachmentList files={i.a.attachments} />
          </div>
        ) : null}
        {i.sub && (
          <p className="mt-2 text-xs text-ink-muted">
            Nộp {fmtDateTime(i.sub.submittedAt)}
            {i.sub.quiz && ` · ${i.sub.quiz.correct}/${i.sub.quiz.total} câu đúng · đã làm ${i.sub.quiz.attempts}/${i.a.quiz?.attempts ?? 1} lần`}
            {i.sub.files.length > 0 && ` · ${i.sub.files.map((f) => f.name).join(', ')}`}
          </p>
        )}
        {i.sub?.comment && (
          <p className="mt-2 flex gap-2 rounded-lg bg-primary-50 px-3 py-2 text-[13px] text-primary-800">
            <MessageSquareText size={16} className="mt-0.5 shrink-0" aria-hidden />
            <span>
              {i.sub.comment}
              {i.sub.gradedBy && <span className="block text-xs text-primary-700">— {i.sub.gradedBy}</span>}
            </span>
          </p>
        )}
        <div className="mt-3 flex flex-wrap gap-2">
          {i.a.type === 'quiz' && attemptsLeft(i) > 0 && i.state !== 'missed' && (
            <Button icon={Play} size="sm" onClick={() => startQuiz(i)}>
              {i.sub ? `Làm lại (còn ${attemptsLeft(i)} lần)` : 'Làm bài'}
            </Button>
          )}
          {i.a.type === 'upload' && i.state !== 'graded' && (
            <Button icon={FileUp} size="sm" variant={i.sub ? 'outline' : 'solid'} onClick={() => setUploading(i)}>
              {i.sub ? 'Nộp lại' : i.state === 'missed' ? 'Nộp muộn' : 'Nộp bài'}
            </Button>
          )}
        </div>
      </article>
    );
  };

  return (
    <>
      <PageHeader title="Bài tập" subtitle="Bài trắc nghiệm được chấm ngay khi nộp. Bài nộp file chờ giáo viên chấm và nhận xét." />
      <Tabs
        activeKey={tab}
        onChange={(k) => setTab(k as State)}
        items={(
          [
            ['todo', 'Cần làm'],
            ['submitted', 'Đã nộp'],
            ['graded', 'Đã chấm'],
            ['missed', 'Quá hạn'],
          ] as [State, string][]
        ).map(([k, l]) => ({
          key: k,
          label: `${l} (${count(k)})`,
          children: list.length ? <div className="grid gap-3 lg:grid-cols-2">{list.map(card)}</div> : <EmptyState icon={k === 'todo' ? ListChecks : NotebookPen} title={k === 'todo' ? 'Không còn bài nào phải làm 🎉' : 'Không có bài nào'} />,
        }))}
      />

      <ModalForm<{ content?: string; files?: UploadItem[] }>
        key={uploading?.a.id ?? 'up'}
        open={Boolean(uploading)}
        onClose={() => setUploading(null)}
        title={uploading?.a.title ?? ''}
        description={uploading?.a.description ?? 'Chụp ảnh vở, file Word / PDF hoặc ghi âm (bài luyện nói).'}
        submitAction="send"
        submitText="Nộp bài"
        successMessage="Đã nộp bài — giáo viên sẽ chấm và nhận xét"
        initialValues={{ content: uploading?.sub?.content }}
        onSubmit={(v) => {
          if (!v.files?.length && !v.content?.trim()) throw new Error('Đính kèm file hoặc nhập bài làm');
          submit({
            assignmentId: uploading!.a.id,
            studentId,
            submittedAt: new Date().toISOString(),
            content: v.content,
            files: (v.files ?? []).map((f) => ({ name: f.name, size: f.size, kind: fileKind(f.name), url: '#' })),
          });
          setTab('submitted');
        }}
      >
        <Form.Item name="files" label="File bài làm" valuePropName="value">
          <FileUpload maxFiles={5} maxSizeMB={20} kind={['document', 'image', 'audio']} />
        </Form.Item>
        <Form.Item name="content" label="Hoặc làm trực tiếp / lời nhắn cho giáo viên">
          <TextArea rows={4} maxLength={3000} />
        </Form.Item>
      </ModalForm>

      <QuizRunner
        open={Boolean(quizFor)}
        title={quizFor?.a.title ?? ''}
        questions={quizQuestions}
        timeLimit={quizFor?.a.quiz?.timeLimitMinutes}
        onClose={() => setQuizFor(null)}
        onSubmit={(r) => {
          const a = quizFor!.a;
          submit({
            assignmentId: a.id,
            studentId,
            submittedAt: new Date().toISOString(),
            files: [],
            quiz: { correct: r.correct, total: r.total, attempts: 1, durationMinutes: Math.max(1, Math.round(r.durationSec / 60)) },
            score: Math.round((r.correct / Math.max(1, r.total)) * a.maxScore * 10) / 10,
            gradedAt: new Date().toISOString(),
            gradedBy: 'Tự chấm',
          });
          notify.toast.success(`Đã nộp: ${r.correct}/${r.total} câu đúng`);
        }}
      />
    </>
  );
}
