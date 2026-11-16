import { Fragment } from 'react';
import { cn } from '@/lib/cn';

/**
 * Hiển thị tiếng Nhật có furigana: "漢字{かんじ}を勉強{べんきょう}する" → <ruby>.
 * `hide` = ẩn furigana (luyện đọc). `mark` tô đậm chữ trong cặp **...** (từ cần hỏi).
 */
export function Furigana({ text, hide, className }: { text: string; hide?: boolean; className?: string }) {
  // Tách: **từ cần hỏi**, 漢字{よみ}, phần còn lại
  const parts = text.split(/(\*\*[^*]+\*\*)/g);
  return (
    <span className={cn('leading-[2.1]', className)} lang="ja">
      {parts.map((p, i) =>
        p.startsWith('**') && p.endsWith('**') ? (
          <span key={i} className="font-semibold underline decoration-primary-400 decoration-2 underline-offset-4">
            <Ruby text={p.slice(2, -2)} hide={hide} />
          </span>
        ) : (
          <Ruby key={i} text={p} hide={hide} />
        ),
      )}
    </span>
  );
}

function Ruby({ text, hide }: { text: string; hide?: boolean }) {
  const re = /([㐀-鿿々〆ヵヶ]+)\{([^}]+)\}/g;
  const out: React.ReactNode[] = [];
  let last = 0;
  let m: RegExpExecArray | null;
  while ((m = re.exec(text))) {
    if (m.index > last) out.push(<Fragment key={`t${last}`}>{text.slice(last, m.index)}</Fragment>);
    out.push(
      hide ? (
        <Fragment key={`r${m.index}`}>{m[1]}</Fragment>
      ) : (
        <ruby key={`r${m.index}`}>
          {m[1]}
          <rt className="text-[0.55em] font-normal text-ink-muted">{m[2]}</rt>
        </ruby>
      ),
    );
    last = m.index + m[0].length;
  }
  if (last < text.length) out.push(<Fragment key={`t${last}`}>{text.slice(last)}</Fragment>);
  return <>{out}</>;
}

/** Bỏ ký hiệu furigana / đánh dấu → chữ thuần (để tìm kiếm, hiển thị 1 dòng) */
export const plainText = (t: string) => t.replace(/\*\*/g, '').replace(/\{[^}]+\}/g, '');
