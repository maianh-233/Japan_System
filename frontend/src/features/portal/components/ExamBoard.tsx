import { useMemo, useState } from 'react';
import { Modal, Progress, Tabs } from 'antd';
import dayjs from 'dayjs';
import { CalendarClock, ClipboardCheck, FileText, MessageSquareText, Monitor, Play, Trophy } from 'lucide-react';
import { Button, EmptyState, StatusTag, useNotify } from '@/components/common';
import * as learningApi from '@/features/academic/classes/learningApi';
import { OPTION_LABELS } from '@/features/academic/questions/constants';
import { Furigana } from '@/features/academic/questions/Furigana';
import { useBreakpoint } from '@/hooks/useBreakpoint';
import { cn } from '@/lib/cn';
import { fmtDate } from '@/lib/date';
import { usePortalStore } from '../portalStore';
import type { MyExam } from '../types';
import { QuizRunner, type RunnerQuestion } from './QuizRunner';

interface Running {
  exam: MyExam;
  attemptId: string;
  deadlineAt: string;
  questions: RunnerQuestion[];
  answers: Record<string, number>;
}

/**
 * Cổng HV / PH › Bài thi (chế độ API — user story 12, ND-05 / ND-06 / ND-11):
 * lịch thi theo kỳ thi trung tâm đã phát hành; thi online vào thi trong khung giờ (đề riêng, tự lưu, vào lại làm tiếp, hết giờ tự nộp);
 * kết quả + nhận xét chỉ hiện khi đã công bố; thi online xem lại bài (đáp án) sau công bố. PH chỉ xem.
 */
export function ExamBoard({ studentId, canTake }: { studentId: string; canTake: boolean }) {
  const notify = useNotify();
  const { isMobile } = useBreakpoint();
  const all = usePortalStore((s) => s.myExams);
  const [running, setRunning] = useState<Running | null>(null);
  const [review, setReview] = useState<{ title: string; questions: learningApi.ReviewQuestion[] } | null>(null);

  const mine = useMemo(() => all.filter((x) => x.studentId === studentId), [all, studentId]);
  const now = dayjs();
  const upcoming = mine.filter((x) => x.attempt !== 'submitted' && !x.result && dayjs(x.exam.endsAt).isAfter(now)).sort((a, b) => a.exam.startsAt.localeCompare(b.exam.startsAt));
  const results = mine.filter((x) => x.result || x.attempt === 'submitted').sort((a, b) => b.exam.startsAt.localeCompare(a.exam.startsAt));

  const start = async (x: MyExam) => {
    try {
      const p = await learningApi.startExam(x.exam.id);
      const answers: Record<string, number> = {};
      p.answers.forEach((a) => (answers[a.questionId] = a.choice));
      setRunning({
        exam: x,
        attemptId: p.attemptId,
        deadlineAt: p.deadlineAt,
        answers,
        questions: p.questions.map((q) => ({ id: q.id, stem: q.stem, passage: q.passage, options: q.options, audioUrl: q.audioUrl })),
      });
    } catch (e) {
      notify.error({ title: (e as Error).message });
    }
  };

  const openReview = async (x: MyExam) => {
    try {
      const r = await learningApi.examReview(x.exam.id, studentId);
      setReview({ title: r.title, questions: r.questions });
    } catch (e) {
      notify.error({ title: (e as Error).message });
    }
  };

  const upcomingCard = (x: MyExam) => {
    const startAt = dayjs(x.exam.startsAt);
    const open = !startAt.isAfter(now);
    return (
      <article key={x.exam.id} className="rounded-xl border border-danger-100 bg-surface p-4">
        <div className="flex items-start justify-between gap-2">
          <div>
            <p className="font-semibold text-ink">{x.exam.title}</p>
            <p className="text-[13px] text-ink-secondary">{x.classCode}</p>
          </div>
          <StatusTag tone={open ? 'danger' : 'warning'} icon={CalendarClock} size="sm">
            {open ? 'Đang mở' : startAt.diff(now, 'day') === 0 ? 'Hôm nay' : `Còn ${Math.ceil(startAt.diff(now, 'hour') / 24)} ngày`}
          </StatusTag>
        </div>
        <p className="mt-2 flex flex-wrap gap-x-4 gap-y-1 text-[13px] text-ink-secondary">
          <span className="inline-flex items-center gap-1">
            <CalendarClock size={14} aria-hidden /> {startAt.format('dddd DD/MM HH:mm')} · {x.exam.durationMinutes} phút
          </span>
          <span className="inline-flex items-center gap-1">
            {x.exam.mode === 'online' ? <Monitor size={14} aria-hidden /> : <FileText size={14} aria-hidden />}
            {x.exam.mode === 'online' ? 'Thi online trên Cổng' : 'Thi giấy tại lớp'}
          </span>
        </p>
        <p className="mt-2 text-xs text-ink-muted">
          {x.exam.questionCount} câu · {x.exam.sections.map((s) => s.name).join(', ')} · đạt từ {x.exam.passPercent}%
        </p>
        {canTake && x.exam.mode === 'online' && (
          <Button icon={Play} size="sm" className="mt-3" disabled={!x.canStart} onClick={() => void start(x)}>
            {x.attempt === 'in_progress' ? 'Làm tiếp' : open ? 'Vào thi' : `Mở lúc ${startAt.format('HH:mm DD/MM')}`}
          </Button>
        )}
      </article>
    );
  };

  const resultCard = (x: MyExam) => {
    const r = x.result;
    const total = r?.sections.reduce((n, s) => n + s.score, 0) ?? 0;
    const max = r?.sections.reduce((n, s) => n + s.max, 0) ?? 0;
    const pct = max ? Math.round((total / max) * 100) : 0;
    return (
      <article key={x.exam.id} className="rounded-xl border border-line bg-surface p-4">
        <div className="flex flex-wrap items-start justify-between gap-2">
          <div>
            <p className="font-semibold text-ink">{x.exam.title}</p>
            <p className="text-xs text-ink-muted">
              {x.classCode} · {fmtDate(x.exam.startsAt)}
            </p>
          </div>
          {!r ? (
            <StatusTag tone="info" icon={ClipboardCheck} size="sm">
              Đã nộp — chờ công bố
            </StatusTag>
          ) : r.status === 'absent' ? (
            <StatusTag tone="neutral" icon={CalendarClock} size="sm">
              Vắng thi
            </StatusTag>
          ) : (
            <span className={cn('tabular rounded-lg px-2.5 py-1 text-sm font-bold', x.passed ? 'bg-success-50 text-success-700' : 'bg-danger-50 text-danger-700')}>
              {total}/{max} · {pct}% · {x.passed ? 'Đạt' : 'Chưa đạt'}
            </span>
          )}
        </div>
        {r?.status === 'done' && (
          <div className="mt-3 flex flex-col gap-1.5 text-[13px]">
            {r.sections.map((s) => (
              <div key={s.name} className="grid grid-cols-[130px_1fr_60px] items-center gap-2">
                <span className="truncate text-ink-secondary">{s.name}</span>
                <Progress percent={Math.round((s.score / s.max) * 100)} showInfo={false} size="small" status={s.score / s.max < 0.5 ? 'exception' : 'normal'} />
                <span className="tabular text-right">
                  {s.score}/{s.max}
                </span>
              </div>
            ))}
          </div>
        )}
        {r?.comment && (
          <p className="mt-3 flex gap-2 rounded-lg bg-primary-50 px-3 py-2 text-[13px] text-primary-800">
            <MessageSquareText size={16} className="mt-0.5 shrink-0" aria-hidden /> {r.comment}
          </p>
        )}
        {r && x.exam.mode === 'online' && x.attempt === 'submitted' && (
          <Button icon={ClipboardCheck} size="sm" variant="soft" className="mt-3" onClick={() => void openReview(x)}>
            Xem lại bài làm
          </Button>
        )}
      </article>
    );
  };

  return (
    <>
      <Tabs
        items={[
          {
            key: 'upcoming',
            label: `Lịch thi (${upcoming.length})`,
            children: upcoming.length ? <div className="grid gap-3 md:grid-cols-2">{upcoming.map(upcomingCard)}</div> : <EmptyState icon={CalendarClock} title="Chưa có lịch thi sắp tới" />,
          },
          {
            key: 'results',
            label: `Kết quả (${results.length})`,
            children: results.length ? <div className="flex flex-col gap-3">{results.map(resultCard)}</div> : <EmptyState icon={Trophy} title="Chưa có kết quả thi" />,
          },
        ]}
      />

      <QuizRunner
        open={Boolean(running)}
        title={running?.exam.exam.title ?? ''}
        questions={running?.questions ?? []}
        deadlineAt={running?.deadlineAt}
        initialAnswers={running?.answers}
        onAnswer={(questionId, choice) =>
          void learningApi.saveExamAnswers(running!.attemptId, [{ questionId, choice }]).catch((e: Error) => notify.error({ title: 'Chưa lưu được câu trả lời', description: e.message }))
        }
        onClose={() => setRunning(null)}
        onSubmit={async () => {
          const r = await learningApi.submitExam(running!.exam.exam.id, running!.attemptId);
          return {
            note: `Đã nộp bài${r.autoSubmitted ? ' (hết giờ, hệ thống tự nộp)' : ''}: trả lời ${r.answered}/${r.total} câu. Điểm và đáp án có khi trung tâm công bố kết quả.`,
          };
        }}
      />

      <Modal open={Boolean(review)} onCancel={() => setReview(null)} footer={null} width={isMobile ? '100%' : 760} centered title={review?.title}>
        <ol className="flex max-h-[65vh] flex-col gap-2 overflow-y-auto text-[13px]">
          {review?.questions.map((q, k) => (
            <li key={q.questionId} className={cn('rounded-lg border p-2.5', q.correct ? 'border-success-100' : 'border-danger-100 bg-danger-50/40')}>
              <p className="text-xs text-ink-muted">Câu {k + 1}</p>
              <p className="text-ink">
                <Furigana text={q.stem} />
              </p>
              <p className="mt-1 text-xs">
                <span className={q.correct ? 'text-success-700' : 'text-danger-700'}>
                  Bạn chọn: {q.chosen == null ? 'bỏ trống' : `${OPTION_LABELS[q.chosen]}. `}
                  {q.chosen != null && <Furigana text={q.options[q.chosen]} />}
                </span>
                {!q.correct && (
                  <span className="ml-3 text-success-700">
                    Đáp án: {OPTION_LABELS[q.answer]}. <Furigana text={q.options[q.answer]} />
                  </span>
                )}
              </p>
              {q.explanation && !q.correct && <p className="mt-1 text-xs text-ink-secondary">💡 {q.explanation}</p>}
            </li>
          ))}
        </ol>
      </Modal>
    </>
  );
}
