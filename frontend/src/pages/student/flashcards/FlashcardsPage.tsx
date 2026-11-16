import { useMemo, useState } from 'react';
import { Modal, Progress } from 'antd';
import dayjs from 'dayjs';
import { Brain, Check, Flame, Layers, RotateCcw, X } from 'lucide-react';
import { Button, PageHeader, StatCard } from '@/components/common';
import { BOX_INTERVAL, streakOf, usePortalStore } from '@/features/portal/portalStore';
import type { FlashCard, FlashDeck } from '@/features/portal/types';
import { useBreakpoint } from '@/hooks/useBreakpoint';
import { cn } from '@/lib/cn';

const SESSION_SIZE = 20;

/**
 * Cổng HV › Flashcard: từ vựng & Kanji theo bài học.
 * Ôn kiểu hộp Leitner: nhớ → lên hộp (lần ôn sau giãn ra 1 → 3 → 7 → 14 → 30 ngày), quên → về hộp đầu, ôn lại ngay.
 */
export default function FlashcardsPage() {
  const { isMobile } = useBreakpoint();
  const decks = usePortalStore((s) => s.decks);
  const progress = usePortalStore((s) => s.cardProgress);
  const studyDays = usePortalStore((s) => s.studyDays);
  const review = usePortalStore((s) => s.reviewCard);

  const [deck, setDeck] = useState<FlashDeck | null>(null);
  const [queue, setQueue] = useState<FlashCard[]>([]);
  const [flipped, setFlipped] = useState(false);
  const [done, setDone] = useState({ ok: 0, again: 0 });

  const now = dayjs();
  const statOf = (d: FlashDeck) => {
    const seen = d.cards.filter((c) => progress[c.id]);
    return {
      learned: seen.filter((c) => progress[c.id].box >= 3).length,
      seen: seen.length,
      due: d.cards.filter((c) => !progress[c.id] || !dayjs(progress[c.id].due).isAfter(now)).length,
    };
  };
  const totalDue = decks.reduce((n, d) => n + statOf(d).due, 0);
  const mastered = Object.values(progress).filter((p) => p.box >= 4).length;
  const streak = streakOf(studyDays);
  const last14 = useMemo(() => Array.from({ length: 14 }, (_, i) => dayjs().subtract(13 - i, 'day').format('YYYY-MM-DD')), []);

  const start = (d: FlashDeck, all = false) => {
    const due = d.cards.filter((c) => all || !progress[c.id] || !dayjs(progress[c.id].due).isAfter(dayjs()));
    // Thẻ đến hạn trước (hộp thấp trước), thẻ mới sau
    const sorted = [...due].sort((a, b) => (progress[a.id]?.box ?? 9) - (progress[b.id]?.box ?? 9));
    setDeck(d);
    setQueue(sorted.slice(0, SESSION_SIZE));
    setFlipped(false);
    setDone({ ok: 0, again: 0 });
  };

  const answer = (remembered: boolean) => {
    const [card, ...rest] = queue;
    review(card.id, remembered);
    setDone((x) => (remembered ? { ...x, ok: x.ok + 1 } : { ...x, again: x.again + 1 }));
    // Quên → đưa lại cuối hàng đợi để ôn ngay trong phiên
    setQueue(remembered ? rest : [...rest, card]);
    setFlipped(false);
  };

  const card = queue[0];
  const box = card ? (progress[card.id]?.box ?? 0) : 0;

  return (
    <>
      <PageHeader title="Flashcard" subtitle="Từ vựng & Kanji theo bài học. Bấm thẻ để lật, tự chấm Nhớ / Quên — hệ thống tự hẹn ngày ôn lại." />

      <div className="mb-4 grid grid-cols-1 gap-3 sm:grid-cols-3">
        <StatCard label="Chuỗi ngày học" value={`${streak} ngày`} icon={Flame} tone="warning" hint={studyDays.includes(dayjs().format('YYYY-MM-DD')) ? 'Hôm nay đã ôn ✓' : 'Ôn 1 thẻ hôm nay để giữ chuỗi'} />
        <StatCard label="Thẻ cần ôn hôm nay" value={totalDue} icon={Brain} tone="primary" />
        <StatCard label="Đã thuộc chắc" value={mastered} icon={Layers} tone="success" hint="Ở hộp 4–5" />
      </div>

      <div className="mb-5 flex gap-1" aria-label="14 ngày gần nhất">
        {last14.map((d) => (
          <span key={d} title={dayjs(d).format('DD/MM')} className={cn('h-6 flex-1 rounded', studyDays.includes(d) ? 'bg-warning-600' : 'bg-canvas')} />
        ))}
      </div>

      <div className="grid gap-3 md:grid-cols-2">
        {decks.map((d) => {
          const st = statOf(d);
          return (
            <article key={d.id} className="flex flex-col rounded-xl border border-line bg-surface p-4">
              <div className="flex items-start justify-between gap-2">
                <div>
                  <p className="text-xs text-ink-muted">
                    {d.level} · {d.kind === 'kanji' ? 'Kanji' : 'Từ vựng'} · {d.lesson}
                  </p>
                  <h3 className="font-semibold text-ink">{d.title}</h3>
                </div>
                {st.due > 0 && <span className="tabular rounded-full bg-primary-50 px-2 py-0.5 text-xs font-semibold text-primary-700">{st.due} cần ôn</span>}
              </div>
              <div className="mt-3">
                <div className="flex justify-between text-xs text-ink-muted">
                  <span>Đã thuộc</span>
                  <span className="tabular">
                    {st.learned}/{d.cards.length}
                  </span>
                </div>
                <Progress percent={Math.round((st.learned / d.cards.length) * 100)} success={{ percent: Math.round((st.learned / d.cards.length) * 100) }} showInfo={false} size="small" />
              </div>
              <div className="mt-auto flex gap-2 pt-3">
                <Button icon={Brain} size="sm" disabled={!st.due} onClick={() => start(d)}>
                  {st.seen ? 'Ôn tập' : 'Bắt đầu học'}
                </Button>
                <Button icon={RotateCcw} size="sm" variant="ghost" tone="neutral" onClick={() => start(d, true)}>
                  Ôn cả bộ
                </Button>
              </div>
            </article>
          );
        })}
      </div>

      <Modal open={Boolean(deck)} onCancel={() => setDeck(null)} footer={null} width={isMobile ? '100%' : 520} centered destroyOnHidden title={deck?.title}>
        {card ? (
          <div className="flex flex-col gap-4">
            <p className="tabular text-center text-xs text-ink-muted">
              Còn {queue.length} thẻ · hộp {box} · nhớ {done.ok} · quên {done.again}
            </p>
            <button
              type="button"
              onClick={() => setFlipped((f) => !f)}
              className="flex min-h-[240px] flex-col items-center justify-center gap-2 rounded-2xl border border-line bg-canvas p-6 text-center transition-colors hover:border-primary-300"
              aria-label={flipped ? 'Mặt sau thẻ' : 'Bấm để lật thẻ'}
            >
              <span lang="ja" className={cn('font-semibold text-ink', card.front.length > 4 ? 'text-4xl' : 'text-6xl')}>
                {card.front}
              </span>
              {flipped ? (
                <>
                  {card.reading && (
                    <span lang="ja" className="text-lg text-primary-700">
                      {card.reading}
                    </span>
                  )}
                  <span className="text-lg font-medium text-ink">{card.meaning}</span>
                  {card.example && (
                    <span lang="ja" className="mt-2 text-sm text-ink-secondary">
                      {card.example}
                    </span>
                  )}
                </>
              ) : (
                <span className="text-xs text-ink-muted">Bấm để xem nghĩa</span>
              )}
            </button>
            <div className="grid grid-cols-2 gap-2">
              <Button icon={X} variant="outline" tone="danger" disabled={!flipped} onClick={() => answer(false)}>
                Quên
              </Button>
              <Button icon={Check} tone="success" disabled={!flipped} onClick={() => answer(true)}>
                Nhớ · ôn lại sau {BOX_INTERVAL[Math.min(5, box + 1)]} ngày
              </Button>
            </div>
          </div>
        ) : (
          <div className="flex flex-col items-center gap-3 py-6 text-center">
            <span className="text-4xl">🎉</span>
            <p className="text-lg font-semibold text-ink">Xong phiên ôn tập!</p>
            <p className="text-ink-secondary">
              Nhớ {done.ok} thẻ · ôn lại {done.again} lần. Chuỗi hiện tại: {streakOf(studyDays)} ngày.
            </p>
            <Button action="cancel" onClick={() => setDeck(null)}>
              Đóng
            </Button>
          </div>
        )}
      </Modal>
    </>
  );
}
