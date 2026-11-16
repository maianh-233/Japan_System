import { useState } from 'react';
import { Check, Pause, Play } from 'lucide-react';
import { cn } from '@/lib/cn';
import { OPTION_LABELS } from './constants';
import { Furigana } from './Furigana';
import type { Question } from './types';

interface Props {
  q: Pick<Question, 'stem' | 'options' | 'answer' | 'passage' | 'audio' | 'format'>;
  /** Hiện đáp án đúng */
  showAnswer?: boolean;
  hideFurigana?: boolean;
  /** Số lượt chọn từng đáp án → thanh phân bố */
  picks?: number[];
  compact?: boolean;
}

/** Câu hỏi như học viên nhìn thấy khi làm bài (kèm đáp án / phân bố chọn cho GV) */
export function QuestionCard({ q, showAnswer, hideFurigana, picks, compact }: Props) {
  const total = picks?.reduce((s, x) => s + x, 0) ?? 0;
  return (
    <div className={cn('flex flex-col gap-3', compact ? 'text-sm' : 'text-base')}>
      {q.passage && (
        <div className="rounded-lg border border-line bg-page px-4 py-3">
          <Furigana text={q.passage} hide={hideFurigana} />
        </div>
      )}
      {q.audio && <FakeAudio name={q.audio.name} duration={q.audio.durationSec} />}
      <p className="text-ink">
        <Furigana text={q.stem || '（Chưa nhập đề bài）'} hide={hideFurigana} />
      </p>
      <ol className="grid grid-cols-1 gap-2 sm:grid-cols-2">
        {q.options.map((o, i) => {
          const right = showAnswer && i === q.answer;
          const pct = total ? Math.round(((picks?.[i] ?? 0) / total) * 100) : undefined;
          return (
            <li
              key={i}
              className={cn(
                'relative flex items-start gap-2 overflow-hidden rounded-lg border px-3 py-2',
                right ? 'border-success-600 bg-success-50' : 'border-line bg-surface',
              )}
            >
              {pct !== undefined && (
                <span className={cn('absolute inset-y-0 left-0', right ? 'bg-success-100' : 'bg-disabled')} style={{ width: `${pct}%` }} aria-hidden />
              )}
              <span className={cn('relative grid size-6 shrink-0 place-items-center rounded-full text-xs font-semibold', right ? 'bg-success-600 text-white' : 'bg-page text-ink-secondary')}>
                {right ? <Check size={13} aria-hidden /> : OPTION_LABELS[i]}
              </span>
              <span className="relative min-w-0 flex-1 text-ink">
                <Furigana text={o || '—'} hide={hideFurigana} />
              </span>
              {pct !== undefined && <span className="tabular relative text-xs text-ink-muted">{pct}%</span>}
            </li>
          );
        })}
      </ol>
    </div>
  );
}

/** Thanh phát audio giả (demo) */
function FakeAudio({ name, duration }: { name: string; duration: number }) {
  const [playing, setPlaying] = useState(false);
  const mm = (s: number) => `${Math.floor(s / 60)}:${String(s % 60).padStart(2, '0')}`;
  return (
    <div className="flex items-center gap-3 rounded-lg border border-line bg-page px-3 py-2">
      <button
        type="button"
        onClick={() => setPlaying((p) => !p)}
        className="grid size-8 shrink-0 place-items-center rounded-full bg-primary-600 text-white"
        aria-label={playing ? 'Tạm dừng' : 'Phát'}
      >
        {playing ? <Pause size={15} aria-hidden /> : <Play size={15} aria-hidden />}
      </button>
      <div className="min-w-0 flex-1">
        <p className="truncate text-[13px] text-ink">{name}</p>
        <div className="mt-1 h-1 rounded-full bg-disabled">
          <div className={cn('h-full rounded-full bg-primary-500 transition-all duration-[30s] ease-linear', playing ? 'w-full' : 'w-0')} />
        </div>
      </div>
      <span className="tabular text-xs text-ink-muted">{mm(duration)}</span>
    </div>
  );
}
