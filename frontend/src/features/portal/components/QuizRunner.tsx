import { useEffect, useMemo, useState } from 'react';
import { Modal, Progress } from 'antd';
import { ChevronLeft, ChevronRight, Clock, Flag, Send } from 'lucide-react';
import { Button, useConfirm, useNotify } from '@/components/common';
import { OPTION_LABELS } from '@/features/academic/questions/constants';
import { Furigana } from '@/features/academic/questions/Furigana';
import { useBreakpoint } from '@/hooks/useBreakpoint';
import { cn } from '@/lib/cn';

export interface QuizResult {
  answers: Record<string, number>;
  correct: number;
  total: number;
  durationSec: number;
}

/** Câu hỏi đưa vào bài làm. Chế độ API: không có đáp án (server chấm); options đã theo thứ tự hiển thị. */
export interface RunnerQuestion {
  id: string;
  stem: string;
  passage?: string | null;
  options: string[];
  answer?: number;
  explanation?: string | null;
  audio?: { name: string; durationSec: number };
  /** Link tạm file nghe (chế độ API) */
  audioUrl?: string | null;
}

/** Kết quả server trả sau khi nộp: chấm + đáp án theo vị trí hiển thị; có note = chỉ báo đã nộp (bài thi chờ công bố) */
export interface RunnerOutcome {
  correct?: number;
  total?: number;
  review?: { questionId: string; chosen?: number | null; answer: number; explanation?: string | null }[];
  note?: string;
}

interface Props {
  open: boolean;
  title: string;
  questions: RunnerQuestion[];
  /** Phút — bỏ trống = không giới hạn */
  timeLimit?: number;
  /** Hạn nộp chung (bài thi online) — thay cho timeLimit */
  deadlineAt?: string;
  /** Câu đã chọn trước đó (tiếp tục bài thi) */
  initialAnswers?: Record<string, number>;
  /** Mỗi lần chọn đáp án (tự lưu bài thi) */
  onAnswer?: (questionId: string, choice: number) => void;
  onClose: () => void;
  /** Mock: tự chấm tại chỗ. API: trả kết quả server */
  onSubmit: (r: QuizResult) => void | Promise<RunnerOutcome | void>;
}

const mmss = (s: number) => `${String(Math.floor(s / 60)).padStart(2, '0')}:${String(s % 60).padStart(2, '0')}`;

/**
 * Làm bài trắc nghiệm (bài tập quiz, đề luyện thi): từng câu, đánh dấu câu, đếm giờ, hết giờ tự nộp → chấm ngay, xem đáp án.
 */
export function QuizRunner({ open, title, questions, timeLimit, deadlineAt, initialAnswers, onAnswer, onClose, onSubmit }: Props) {
  const { isMobile } = useBreakpoint();
  const { confirm, confirmDialog } = useConfirm();
  const notify = useNotify();
  const [busy, setBusy] = useState(false);
  const [outcome, setOutcome] = useState<RunnerOutcome | null>(null);
  const [i, setI] = useState(0);
  const [answers, setAnswers] = useState<Record<string, number>>({});
  const [flags, setFlags] = useState<Set<string>>(new Set());
  const [elapsed, setElapsed] = useState(0);
  const [result, setResult] = useState<QuizResult | null>(null);

  useEffect(() => {
    if (!open) return;
    setI(0);
    setAnswers(initialAnswers ?? {});
    setOutcome(null);
    setBusy(false);
    setFlags(new Set());
    setElapsed(0);
    setResult(null);
    // eslint-disable-next-line react-hooks/exhaustive-deps
  }, [open]);

  // elapsed đổi mỗi giây → tính lại thời gian còn theo hạn chung
  const left = deadlineAt ? Math.floor((new Date(deadlineAt).getTime() - Date.now()) / 1000) : timeLimit ? timeLimit * 60 - elapsed : undefined;

  const finish = useMemo(
    () => async () => {
      if (busy) return;
      const correct = questions.filter((q) => q.answer !== undefined && answers[q.id] === q.answer).length;
      const r = { answers, correct, total: questions.length, durationSec: elapsed };
      setBusy(true);
      try {
        const server = await onSubmit(r);
        if (server) {
          setOutcome(server);
          setResult({ ...r, correct: server.correct ?? r.correct, total: server.total ?? r.total });
        } else setResult(r);
      } catch (e) {
        notify.error({ title: (e as Error).message });
      } finally {
        setBusy(false);
      }
    },
    [answers, questions, elapsed, onSubmit, busy, notify],
  );

  useEffect(() => {
    if (!open || result) return;
    const t = setInterval(() => setElapsed((e) => e + 1), 1000);
    return () => clearInterval(t);
  }, [open, result]);

  useEffect(() => {
    if (left !== undefined && left <= 0 && !result && !busy) void finish();
  }, [left, result, finish, busy]);

  const q = questions[i];
  const answered = Object.keys(answers).length;

  const submit = async () => {
    const missing = questions.length - answered;
    const ok = await confirm({
      title: 'Nộp bài?',
      description: missing ? `Còn ${missing} câu chưa trả lời — câu bỏ trống tính sai.` : 'Bạn đã trả lời hết các câu.',
      tone: missing ? 'warning' : 'primary',
      confirmAction: 'send',
      confirmText: 'Nộp bài',
    });
    if (ok) void finish();
  };

  const requestClose = async () => {
    if (result) return onClose();
    const ok = await confirm({
      title: 'Thoát khi chưa nộp?',
      description: onAnswer ? 'Câu đã chọn được lưu — vào lại để làm tiếp, giờ thi vẫn chạy.' : 'Bài làm sẽ không được lưu.',
      tone: 'warning',
    });
    if (ok) onClose();
  };

  return (
    <Modal open={open} onCancel={requestClose} footer={null} width={isMobile ? '100%' : 760} centered destroyOnHidden maskClosable={false} title={title}>
      {result && outcome?.note ? (
        <div className="flex flex-col gap-4">
          <p className="rounded-xl bg-canvas p-4 text-center text-ink">{outcome.note}</p>
          <Button action="cancel" onClick={onClose} className="self-end">
            Đóng
          </Button>
        </div>
      ) : result ? (
        <div className="flex flex-col gap-4">
          <div className="flex flex-col items-center gap-1 rounded-xl bg-canvas p-4">
            <Progress type="circle" size={110} percent={Math.round((result.correct / Math.max(1, result.total)) * 100)} format={(p) => <span className="tabular text-xl font-bold">{p}%</span>} />
            <p className="tabular text-ink-secondary">
              {result.correct}/{result.total} câu đúng · {mmss(result.durationSec)}
            </p>
          </div>
          <ol className="flex max-h-[50vh] flex-col gap-2 overflow-y-auto text-[13px]">
            {questions.map((x, k) => {
              const r = outcome?.review?.find((v) => v.questionId === x.id);
              const picked = r ? (r.chosen ?? undefined) : result.answers[x.id];
              const answer = r ? r.answer : (x.answer ?? -1);
              const explanation = r ? r.explanation : x.explanation;
              const ok = picked === answer;
              return (
                <li key={x.id} className={cn('rounded-lg border p-2.5', ok ? 'border-success-100' : 'border-danger-100 bg-danger-50/40')}>
                  <p className="text-xs text-ink-muted">Câu {k + 1}</p>
                  <p className="text-ink">
                    <Furigana text={x.stem} />
                  </p>
                  <p className="mt-1 text-xs">
                    <span className={ok ? 'text-success-700' : 'text-danger-700'}>
                      Bạn chọn: {picked === undefined ? 'bỏ trống' : <Furigana text={x.options[picked]} />}
                    </span>
                    {!ok && (
                      <span className="ml-3 text-success-700">
                        Đáp án: <Furigana text={x.options[answer] ?? '—'} />
                      </span>
                    )}
                  </p>
                  {explanation && !ok && <p className="mt-1 text-xs text-ink-secondary">💡 {explanation}</p>}
                </li>
              );
            })}
          </ol>
          <Button action="cancel" onClick={onClose} className="self-end">
            Đóng
          </Button>
        </div>
      ) : q ? (
        <div className="flex flex-col gap-4">
          <div className="flex items-center justify-between gap-3 text-[13px]">
            <span className="tabular text-ink-secondary">
              Câu {i + 1}/{questions.length} · đã làm {answered}
            </span>
            <span className={cn('tabular inline-flex items-center gap-1 rounded-full px-2.5 py-1 font-semibold', left !== undefined && left < 60 ? 'bg-danger-50 text-danger-700' : 'bg-canvas text-ink')}>
              <Clock size={14} aria-hidden /> {left !== undefined ? mmss(Math.max(0, left)) : mmss(elapsed)}
            </span>
          </div>
          <Progress percent={Math.round((answered / questions.length) * 100)} showInfo={false} size="small" />

          {q.passage && (
            <div className="max-h-48 overflow-y-auto rounded-lg border border-line bg-canvas px-4 py-3 text-[15px]">
              <Furigana text={q.passage} />
            </div>
          )}
          {q.audioUrl ? (
            <audio controls src={q.audioUrl} className="w-full" aria-label="File nghe" />
          ) : (
            q.audio && <p className="rounded-lg bg-primary-50 px-3 py-2 text-[13px] text-primary-700">🎧 {q.audio.name} ({q.audio.durationSec}s) — nghe tối đa 2 lần</p>
          )}
          <p className="text-lg text-ink">
            <Furigana text={q.stem} />
          </p>
          <div className="grid gap-2 sm:grid-cols-2" role="radiogroup" aria-label={`Đáp án câu ${i + 1}`}>
            {q.options.map((o, k) => {
              const on = answers[q.id] === k;
              return (
                <button
                  key={k}
                  type="button"
                  role="radio"
                  aria-checked={on}
                  onClick={() => {
                    setAnswers((a) => ({ ...a, [q.id]: k }));
                    onAnswer?.(q.id, k);
                  }}
                  className={cn('flex items-start gap-2 rounded-lg border px-3 py-2.5 text-left text-[15px] transition-colors', on ? 'border-primary-600 bg-primary-50' : 'border-line hover:border-primary-300')}
                >
                  <span className={cn('grid size-6 shrink-0 place-items-center rounded-full text-xs font-semibold', on ? 'bg-primary-600 text-white' : 'bg-canvas text-ink-secondary')}>{OPTION_LABELS[k]}</span>
                  <Furigana text={o} />
                </button>
              );
            })}
          </div>

          {/* Bảng câu */}
          <div className="flex flex-wrap gap-1.5">
            {questions.map((x, k) => (
              <button
                key={x.id}
                type="button"
                onClick={() => setI(k)}
                aria-label={`Câu ${k + 1}`}
                className={cn(
                  'tabular relative size-8 rounded-md border text-xs font-medium',
                  k === i ? 'border-primary-600 ring-2 ring-primary-200' : 'border-line',
                  answers[x.id] !== undefined ? 'bg-primary-600 text-white' : 'bg-surface text-ink-secondary',
                )}
              >
                {k + 1}
                {flags.has(x.id) && <span className="absolute -right-1 -top-1 size-2.5 rounded-full bg-warning-600" />}
              </button>
            ))}
          </div>

          <div className="flex flex-wrap items-center justify-between gap-2">
            <Button
              icon={Flag}
              size="sm"
              variant="ghost"
              tone={flags.has(q.id) ? 'warning' : 'neutral'}
              onClick={() =>
                setFlags((f) => {
                  const n = new Set(f);
                  if (n.has(q.id)) n.delete(q.id);
                  else n.add(q.id);
                  return n;
                })
              }
            >
              {flags.has(q.id) ? 'Bỏ đánh dấu' : 'Đánh dấu xem lại'}
            </Button>
            <div className="flex gap-2">
              <Button icon={ChevronLeft} variant="outline" tone="neutral" disabled={i === 0} onClick={() => setI(i - 1)}>
                Trước
              </Button>
              {i < questions.length - 1 ? (
                <Button icon={ChevronRight} variant="outline" onClick={() => setI(i + 1)}>
                  Tiếp
                </Button>
              ) : (
                <Button icon={Send} onClick={submit} loading={busy}>
                  Nộp bài
                </Button>
              )}
            </div>
          </div>
        </div>
      ) : (
        <p className="text-ink-muted">Chưa đủ câu hỏi đã duyệt trong ngân hàng cho bài này.</p>
      )}
      {confirmDialog}
    </Modal>
  );
}
