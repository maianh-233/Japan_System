import { useEffect, useState } from 'react';
import { Tooltip } from 'antd';
import dayjs from 'dayjs';
import { ChevronLeft, ChevronRight, Download, ExternalLink, Lock, Pause, Play, ZoomIn, ZoomOut } from 'lucide-react';
import { Button } from '@/components/common';
import { useAuth } from '@/hooks/useAuth';
import { cn } from '@/lib/cn';
import { DOC_CATEGORIES, DOC_FILE_TYPES } from './constants';
import type { LibraryDoc } from './types';

const counted = new Set<string>();

/** Bìa tài liệu (màu theo loại + chữ Nhật) */
export function DocCover({ doc, className }: { doc: LibraryDoc; className?: string }) {
  const cat = DOC_CATEGORIES[doc.category];
  const FT = DOC_FILE_TYPES[doc.fileType];
  return (
    <div className={cn('relative flex flex-col justify-between overflow-hidden rounded-lg bg-gradient-to-br p-3 text-white', cat.cover, className)}>
      <div className="flex items-start justify-between gap-1">
        <span className="rounded bg-white/20 px-1.5 py-0.5 text-[11px] font-semibold">{doc.levels.join(' · ') || '—'}</span>
        <FT.icon size={16} className="opacity-80" aria-hidden />
      </div>
      <p className="line-clamp-3 text-[13px] leading-snug font-semibold" lang="ja">
        {doc.subtitle ?? doc.title}
      </p>
      <cat.icon size={56} className="absolute -right-3 -bottom-3 opacity-15" aria-hidden />
    </div>
  );
}

/**
 * Trình xem tài liệu trực tuyến (mô phỏng):
 *  - Watermark lặp: tên người xem + thời gian → ảnh chụp màn hình lộ ra ngoài truy được nguồn
 *  - Chặn chuột phải / bôi đen / tải về (trừ khi tài liệu cho phép)
 */
export function DocViewer({ doc, onViewed }: { doc: LibraryDoc; onViewed?: () => void }) {
  const { user } = useAuth();
  const [page, setPage] = useState(1);
  const [zoom, setZoom] = useState(100);
  const [playing, setPlaying] = useState(false);
  const stamp = `${user?.fullName ?? 'Người xem'} · ${user?.email ?? ''} · ${dayjs().format('DD/MM/YYYY HH:mm')}`;

  useEffect(() => {
    // Mỗi lần mở trình xem tính 1 lượt (chặn đếm đôi khi React dev chạy effect 2 lần)
    const key = `${doc.id}-${dayjs().format('YYYYMMDDHHmm')}`;
    if (counted.has(key)) return;
    counted.add(key);
    onViewed?.();
    // chỉ tính 1 lượt khi mở
    // eslint-disable-next-line react-hooks/exhaustive-deps
  }, [doc.id]);

  const total = doc.pages ?? 1;
  const toolbar = (
    <div className="flex flex-wrap items-center gap-2 border-b border-line bg-page px-3 py-2">
      {doc.fileType === 'pdf' && (
        <>
          <Button icon={ChevronLeft} iconOnly size="sm" tone="neutral" variant="ghost" aria-label="Trang trước" disabled={page <= 1} onClick={() => setPage((p) => p - 1)} />
          <span className="tabular text-[13px] text-ink-secondary">
            Trang {page}/{total}
          </span>
          <Button icon={ChevronRight} iconOnly size="sm" tone="neutral" variant="ghost" aria-label="Trang sau" disabled={page >= total} onClick={() => setPage((p) => p + 1)} />
          <span className="mx-1 h-4 w-px bg-line" />
          <Button icon={ZoomOut} iconOnly size="sm" tone="neutral" variant="ghost" aria-label="Thu nhỏ" disabled={zoom <= 70} onClick={() => setZoom((z) => z - 10)} />
          <span className="tabular w-10 text-center text-[13px] text-ink-secondary">{zoom}%</span>
          <Button icon={ZoomIn} iconOnly size="sm" tone="neutral" variant="ghost" aria-label="Phóng to" disabled={zoom >= 150} onClick={() => setZoom((z) => z + 10)} />
        </>
      )}
      <span className="ml-auto flex items-center gap-2">
        {doc.watermark && (
          <span className="inline-flex items-center gap-1 text-xs text-ink-muted">
            <Lock size={12} aria-hidden /> Có watermark
          </span>
        )}
        <Tooltip title={doc.allowDownload ? undefined : 'Tài liệu chỉ xem trực tuyến — đã chặn tải về / in'}>
          <span>
            <Button icon={Download} size="sm" variant="outline" disabled={!doc.allowDownload}>
              Tải về
            </Button>
          </span>
        </Tooltip>
      </span>
    </div>
  );

  const watermark = doc.watermark && (
    <div className="pointer-events-none absolute inset-0 overflow-hidden" aria-hidden>
      <div className="absolute -inset-1/2 flex rotate-[-28deg] flex-col justify-around">
        {Array.from({ length: 9 }, (_, i) => (
          <p key={i} className="whitespace-nowrap text-sm font-semibold tracking-wide text-ink/10" style={{ marginLeft: (i % 2) * 120 }}>
            {`${stamp}    ·    `.repeat(4)}
          </p>
        ))}
      </div>
    </div>
  );

  return (
    <div
      className="overflow-hidden rounded-[var(--radius-card)] border border-line bg-surface select-none"
      onContextMenu={(e) => !doc.allowDownload && e.preventDefault()}
    >
      {toolbar}
      <div className="relative flex min-h-[420px] items-start justify-center overflow-auto bg-disabled/60 p-4">
        {doc.fileType === 'pdf' && (
          <div className="relative w-full max-w-[520px] origin-top rounded-sm bg-white p-8 shadow-[var(--shadow-soft-md)]" style={{ transform: `scale(${zoom / 100})` }}>
            {page === 1 ? (
              <div className="flex flex-col items-center gap-3 py-16 text-center">
                <p className="text-xs tracking-widest text-ink-muted">{doc.publisher ?? 'JP Center'}</p>
                <p className="text-xl font-bold text-ink" lang="ja">
                  {doc.subtitle ?? doc.title}
                </p>
                <p className="text-sm text-ink-secondary">{doc.title}</p>
                <p className="mt-6 text-xs text-ink-muted">{doc.levels.join(' · ')}</p>
              </div>
            ) : (
              <div className="flex flex-col gap-2.5">
                <p className="mb-2 text-sm font-semibold text-ink" lang="ja">
                  第{page - 1}課 · {['文法のポイント', '練習問題', '語彙リスト', '読解', '聴解スクリプト'][page % 5]}
                </p>
                {Array.from({ length: 16 }, (_, i) => (
                  <div key={i} className="h-2 rounded-full bg-disabled" style={{ width: `${60 + ((i * 37 + page * 11) % 40)}%` }} />
                ))}
              </div>
            )}
            <p className="tabular absolute right-4 bottom-2 text-[10px] text-ink-muted">{page}</p>
            {watermark}
          </div>
        )}
        {(doc.fileType === 'audio' || doc.fileType === 'video') && (
          <div className={cn('relative flex w-full max-w-[560px] flex-col items-center justify-center gap-4 rounded-lg p-8', doc.fileType === 'video' ? 'aspect-video bg-ink text-white' : 'bg-white')}>
            <button type="button" onClick={() => setPlaying((p) => !p)} className="grid size-14 place-items-center rounded-full bg-primary-600 text-white shadow-lg" aria-label={playing ? 'Tạm dừng' : 'Phát'}>
              {playing ? <Pause size={24} aria-hidden /> : <Play size={24} aria-hidden />}
            </button>
            <p className={cn('text-center text-sm', doc.fileType === 'video' ? 'text-white/80' : 'text-ink-secondary')}>{doc.fileName ?? doc.title}</p>
            <div className="h-1.5 w-full rounded-full bg-line">
              <div className={cn('h-full rounded-full bg-primary-500 transition-all duration-[60s] ease-linear', playing ? 'w-full' : 'w-0')} />
            </div>
            <p className="tabular text-xs text-ink-muted">{doc.duration} phút</p>
            {watermark}
          </div>
        )}
        {doc.fileType === 'link' && (
          <div className="flex flex-col items-center gap-3 py-16 text-center">
            <ExternalLink size={32} className="text-primary-600" aria-hidden />
            <p className="text-sm text-ink-secondary">Tài liệu là liên kết ngoài — mở ở tab mới.</p>
            <Button icon={ExternalLink} variant="soft">
              Mở liên kết
            </Button>
          </div>
        )}
      </div>
    </div>
  );
}
