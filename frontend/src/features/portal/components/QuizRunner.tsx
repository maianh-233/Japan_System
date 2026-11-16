import { useEffect, useMemo, useState } from 'react';
import { Modal, Progress } from 'antd';
import { ChevronLeft, ChevronRight, Clock, Flag, Send } from 'lucide-react';
import { Button, useConfirm } from '@/components/common';
import { OPTION_LABELS } from '@/features/academic/questions/constants';
import { Furigana } from '@/features/academic/questions/Furigana';
import type { Question } from '@/features/academic/questions/types';
import { useBreakpoint } from '@/hooks/useBreakpoint';
import { cn } from '@/lib/cn';

export interface QuizResult {
  answers: Record<string, number>;
  correct: number;
  total: number;
  durationSec: number;
}

interface Props {
  open: boolean;
  title: string;
  questions: Question[];
  /** Phút — bỏ trống = không giới hạn */
  timeLimit?: number;
  onClose: () => void;
  onSubmit: (r: QuizResult) => void;
}

const mmss = (s: number) => `${String(Math.floor(s / 60)).padStart(2, '0')}:${String(s % 60).padStart(2, '0')}`;

/**
 * Làm bài trắc nghiệm (bài tập quiz, đề luyện thi): từng câu, đánh dấu câu, đếm giờ, hết giờ tự nộp → chấm ngay, xem đáp án.
 */
export function QuizRunner({ open, title, questions, timeLimit, onClose, onSubmit }: Props) {
  const { isMobile } = useBreakpoint();
  const { confirm, confirmDialog } = useConfirm();
  const [i, setI] = useState(0);
  const [answers, setAnswers] = useState<Record<string, number>>({});
  const [flags, setFlags] = useState<Set<string>>(new Set());
  const [elapsed, setElapsed] = useState(0);
  const [result, setResult] = useState<QuizResult | null>(null);

  useEffect(() => {
    if (!open) return;
    setI(0);
    setAnswers({});
    setFlags(new Set());
    setElapsed(0);
    setResult(null);
  }, [open]);

  const left = timeLimit ? timeLimit * 60 - elapsed : undefined;

  const finish = useMemo(
    () => () => {
      const correct = questions.filter((q) => answers[q.id] === q.answer).length;
      const r = { answers, correct, total: questions.length, durationSec: elapsed };
      setResult(r);
      onSubmit(r);
    },
    [answers, questions, elapsed, onSubmit],
  );

  useEffect(() => {
    if (!open || result) return;
    const t = setInterval(() => setElapsed((e) => e + 1), 1000);
    return () => clearInterval(t);
  }, [open, result]);

  useEffect(() => {
    if (left !== undefined && left <= 0 && !result) finish();
  }, [left, result, finish]);

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
    if (ok) finish();
  };

  const requestClose = async () => {
    if (result) return onClose();
    const ok = await confirm({ title: 'Thoát khi chưa nộp?', description: 'Bài làm sẽ không được lưu.', tone: 'warning' });
    if (ok) onClose();
  };

  return (
    <Modal open={open} onCancel={requestClose} footer={null} width={isMobile ? '100%' : 760} centered destroyOnHidden maskClosable={false} title={title}>
      {result ? (
        <div className="flex flex-col gap-4">
          <div className="flex flex-col items-center gap-1 rounded-xl bg-canvas p-4">
            <Progress type="circle" size={110} percent={Math.round((result.correct / Math.max(1, result.total)) * 100)} format={(p) => <span className="tabular text-xl font-bold">{p}%</span>} />
            <p className="tabular text-ink-secondary">
              {result.correct}/{result.total} câu đúng · {mmss(result.durationSec)}
            </p>
          </div>
          <ol className="flex max-h-[50vh] flex-col gap-2 overflow-y-auto text-[13px]">
            {questions.map((x, k) => {
              const picked = result.answers[x.id];
              const ok = picked === x.answer;
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
                        Đáp án: <Furigana text={x.options[x.answer]} />
                      </span>
                    )}
                  </p>
                  {x.explanation && !ok && <p className="mt-1 text-xs text-ink-secondary">💡 {x.explanation}</p>}
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
          {q.audio && <p className="rounded-lg bg-primary-50 px-3 py-2 text-[13px] text-primary-700">🎧 {q.audio.name} ({q.audio.durationSec}s) — nghe tối đa 2 lần</p>}
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
                  onClick={() => setAnswers((a) => ({ ...a, [q.id]: k }))}
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
                <Button icon={Send} onClick={submit}>
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
