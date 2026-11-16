import { useCallback, useEffect, useRef, useState } from 'react';
import {
  ChevronLeft,
  ChevronRight,
  ExternalLink,
  FileAudio,
  FileImage,
  FileSpreadsheet,
  FileText,
  FileVideo,
  FolderOpen,
  Link2,
  Maximize2,
  Minimize2,
  Presentation,
  MonitorPlay,
  type LucideIcon,
} from 'lucide-react';
import { Button } from '@/components/common';
import { cn } from '@/lib/cn';
import { formatFileSize } from '@/lib/format';
import { embedUrl, isExternal, type TeachingDoc, type TeachingDocKind } from './teachingDocs';

export const DOC_KIND: Record<TeachingDocKind, { label: string; icon: LucideIcon; cls: string }> = {
  pdf: { label: 'PDF', icon: FileText, cls: 'bg-danger-50 text-danger-700' },
  doc: { label: 'Word', icon: FileText, cls: 'bg-primary-50 text-primary-700' },
  ppt: { label: 'Slide', icon: Presentation, cls: 'bg-warning-50 text-warning-700' },
  xls: { label: 'Excel', icon: FileSpreadsheet, cls: 'bg-success-50 text-success-700' },
  image: { label: 'Ảnh', icon: FileImage, cls: 'bg-primary-50 text-primary-700' },
  audio: { label: 'Audio', icon: FileAudio, cls: 'bg-warning-50 text-warning-700' },
  video: { label: 'Video', icon: FileVideo, cls: 'bg-danger-50 text-danger-700' },
  youtube: { label: 'YouTube', icon: MonitorPlay, cls: 'bg-danger-50 text-danger-700' },
  drive: { label: 'Drive', icon: FolderOpen, cls: 'bg-success-50 text-success-700' },
  link: { label: 'Link', icon: Link2, cls: 'bg-page text-ink-secondary' },
};

/** Thanh công cụ khi toàn màn hình: ẩn, hiện lại khi rê / di chuột */
const HIDE_AFTER_MS = 2500;

interface Props {
  doc: TeachingDoc | undefined;
  index: number;
  total: number;
  onPrev: () => void;
  onNext: () => void;
}

/**
 * Khung trình chiếu tài liệu khi dạy.
 *  - Nhúng trực tiếp: PDF, Word/Slide/Excel (Office viewer), Google Docs/Drive, YouTube, video, audio, ảnh
 *  - Toàn màn hình (Fullscreen API) — rê chuột lên sẽ hiện nút thoát + chuyển tài liệu; Esc cũng thoát
 */
export function DocViewer({ doc, index, total, onPrev, onNext }: Props) {
  const stageRef = useRef<HTMLDivElement>(null);
  const [fullscreen, setFullscreen] = useState(false);
  const [toolbar, setToolbar] = useState(true);
  const hideTimer = useRef<number>();

  useEffect(() => {
    const onChange = () => setFullscreen(document.fullscreenElement === stageRef.current);
    document.addEventListener('fullscreenchange', onChange);
    return () => document.removeEventListener('fullscreenchange', onChange);
  }, []);

  /** Hiện thanh công cụ rồi tự ẩn (chỉ khi toàn màn hình) */
  const reveal = useCallback(() => {
    setToolbar(true);
    window.clearTimeout(hideTimer.current);
    hideTimer.current = window.setTimeout(() => setToolbar(false), HIDE_AFTER_MS);
  }, []);
  useEffect(() => {
    if (fullscreen) reveal();
    else {
      window.clearTimeout(hideTimer.current);
      setToolbar(true);
    }
    return () => window.clearTimeout(hideTimer.current);
  }, [fullscreen, reveal]);

  // Phím ← → chuyển tài liệu khi đang trình chiếu
  useEffect(() => {
    if (!fullscreen) return;
    const onKey = (e: KeyboardEvent) => {
      if (e.key === 'ArrowRight' || e.key === 'PageDown') onNext();
      if (e.key === 'ArrowLeft' || e.key === 'PageUp') onPrev();
    };
    window.addEventListener('keydown', onKey);
    return () => window.removeEventListener('keydown', onKey);
  }, [fullscreen, onNext, onPrev]);

  const enter = () => stageRef.current?.requestFullscreen?.().catch(() => undefined);
  const exit = () => document.fullscreenElement && document.exitFullscreen().catch(() => undefined);

  const Kind = doc ? DOC_KIND[doc.kind] : undefined;

  return (
    <div
      ref={stageRef}
      onMouseMove={fullscreen ? reveal : undefined}
      className={cn(
        'group relative flex min-h-[420px] flex-col overflow-hidden bg-surface',
        fullscreen ? 'h-screen w-screen bg-black' : 'h-full rounded-[var(--radius-card)] border border-line',
      )}
    >
      {/* Thanh tiêu đề (chế độ thường) */}
      {!fullscreen && (
        <div className="flex items-center gap-2 border-b border-line px-3 py-2">
          {doc && Kind ? (
            <>
              <span className={cn('grid size-7 shrink-0 place-items-center rounded-md', Kind.cls)}>
                <Kind.icon size={15} aria-hidden />
              </span>
              <div className="min-w-0 flex-1">
                <p className="truncate text-[13px] font-medium text-ink">{doc.name}</p>
                <p className="truncate text-xs text-ink-muted">
                  {Kind.label} · {doc.group}
                  {doc.size ? ` · ${formatFileSize(doc.size)}` : ''}
                </p>
              </div>
            </>
          ) : (
            <p className="flex-1 text-[13px] text-ink-muted">Chọn một tài liệu ở danh sách bên trái</p>
          )}
          {total > 1 && (
            <span className="tabular flex items-center gap-0.5 text-xs text-ink-muted">
              <Button icon={ChevronLeft} iconOnly size="sm" variant="ghost" tone="neutral" onClick={onPrev} tooltip="Tài liệu trước" aria-label="Tài liệu trước" disabled={!doc} />
              {index + 1}/{total}
              <Button icon={ChevronRight} iconOnly size="sm" variant="ghost" tone="neutral" onClick={onNext} tooltip="Tài liệu sau" aria-label="Tài liệu sau" disabled={!doc} />
            </span>
          )}
          {doc && isExternal(doc.url) && (
            <Button icon={ExternalLink} iconOnly size="sm" variant="ghost" tone="neutral" tooltip="Mở ở tab mới" aria-label="Mở ở tab mới" onClick={() => window.open(doc.url, '_blank', 'noopener')} />
          )}
          <Button icon={Maximize2} size="sm" variant="soft" onClick={enter} disabled={!doc}>
            Toàn màn hình
          </Button>
        </div>
      )}

      <div className="relative min-h-0 flex-1">{doc ? <DocContent key={doc.id} doc={doc} dark={fullscreen} /> : <Empty />}</div>

      {/* Toàn màn hình: dải nhận chuột mép trên (nội dung iframe không báo sự kiện chuột ra ngoài) */}
      {fullscreen && <div className="absolute inset-x-0 top-0 z-10 h-4" onMouseEnter={reveal} aria-hidden />}

      {/* Toàn màn hình: thanh điều khiển nổi, hiện khi rê chuột */}
      {fullscreen && (
        <div
          onMouseEnter={() => {
            window.clearTimeout(hideTimer.current);
            setToolbar(true);
          }}
          onMouseLeave={reveal}
          className={cn(
            'absolute right-4 top-4 z-20 flex items-center gap-2 rounded-xl bg-black/70 px-3 py-2 text-white shadow-lg backdrop-blur transition-opacity duration-200',
            toolbar ? 'opacity-100' : 'pointer-events-none opacity-0',
            'focus-within:pointer-events-auto focus-within:opacity-100',
          )}
        >
          <span className="max-w-[40vw] truncate text-sm">{doc?.name}</span>
          {total > 1 && (
            <span className="tabular flex items-center gap-1 text-xs text-white/70">
              <button type="button" onClick={onPrev} className="grid size-8 place-items-center rounded-lg hover:bg-white/15" aria-label="Tài liệu trước">
                <ChevronLeft size={18} aria-hidden />
              </button>
              {index + 1}/{total}
              <button type="button" onClick={onNext} className="grid size-8 place-items-center rounded-lg hover:bg-white/15" aria-label="Tài liệu sau">
                <ChevronRight size={18} aria-hidden />
              </button>
            </span>
          )}
          <button
            type="button"
            onClick={exit}
            className="inline-flex h-9 items-center gap-1.5 rounded-lg bg-white px-3 text-sm font-medium text-ink hover:bg-white/90"
          >
            <Minimize2 size={16} aria-hidden /> Thoát toàn màn hình
          </button>
        </div>
      )}
    </div>
  );
}

function Empty() {
  return (
    <div className="grid h-full place-items-center p-6 text-center text-[13px] text-ink-muted">
      <div>
        <Presentation size={40} className="mx-auto mb-2 text-ink-placeholder" aria-hidden />
        Bấm vào tài liệu để mở và trình chiếu khi dạy
      </div>
    </div>
  );
}

/** Nội dung tài liệu theo loại */
function DocContent({ doc, dark }: { doc: TeachingDoc; dark: boolean }) {
  const src = embedUrl(doc);
  if (src) return <iframe src={src} title={doc.name} className="absolute inset-0 h-full w-full border-0 bg-white" allow="autoplay; fullscreen; encrypted-media" allowFullScreen />;

  const external = isExternal(doc.url);
  if (external && doc.kind === 'image') return <img src={doc.url} alt={doc.name} className="absolute inset-0 h-full w-full object-contain" />;
  if (external && doc.kind === 'video') return <video src={doc.url} controls className="absolute inset-0 h-full w-full bg-black object-contain" />;

  const Kind = DOC_KIND[doc.kind];
  return (
    <div className={cn('absolute inset-0 grid place-items-center overflow-auto p-6', dark ? 'bg-[#111] text-white' : 'bg-page')}>
      <div className={cn('flex w-full max-w-xl flex-col items-center gap-3 rounded-2xl p-8 text-center', dark ? 'bg-white/5' : 'bg-surface shadow-sm')}>
        <span className={cn('grid size-16 place-items-center rounded-2xl', Kind.cls)}>
          <Kind.icon size={32} aria-hidden />
        </span>
        <p className={cn('text-lg font-semibold', dark ? 'text-white' : 'text-ink')}>{doc.name}</p>
        <p className={cn('text-[13px]', dark ? 'text-white/60' : 'text-ink-muted')}>
          {Kind.label} · {doc.group}
          {doc.teacherOnly ? ' · chỉ giáo viên' : ''}
        </p>
        {doc.description && <p className={cn('text-[13px]', dark ? 'text-white/80' : 'text-ink-secondary')}>{doc.description}</p>}
        {doc.kind === 'audio' && external ? (
          <audio src={doc.url} controls className="mt-2 w-full" />
        ) : (
          <p className={cn('rounded-lg px-3 py-2 text-xs', dark ? 'bg-white/10 text-white/70' : 'bg-primary-50 text-primary-800')}>
            {external ? 'Trang này không cho phép nhúng — mở ở tab mới để trình chiếu.' : 'Bản demo: khi kết nối máy chủ, nội dung file hiển thị trực tiếp tại đây.'}
          </p>
        )}
        {external && (
          <Button icon={ExternalLink} size="sm" variant="outline" onClick={() => window.open(doc.url, '_blank', 'noopener')}>
            Mở ở tab mới
          </Button>
        )}
      </div>
    </div>
  );
}
