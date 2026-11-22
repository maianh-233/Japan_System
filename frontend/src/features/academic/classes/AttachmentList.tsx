import { Download, FileAudio, FileImage, FileText, Link2, type LucideIcon } from 'lucide-react';
import type { UploadItem } from '@/components/common';
import { cn } from '@/lib/cn';
import { formatFileSize } from '@/lib/format';
import type { SubmissionFile } from './types';

const KIND_ICON: Record<SubmissionFile['kind'], LucideIcon> = { pdf: FileText, doc: FileText, image: FileImage, audio: FileAudio, link: Link2 };

export const fileKindOf = (name: string): SubmissionFile['kind'] => {
  const ext = name.split('.').pop()?.toLowerCase() ?? '';
  if (ext === 'pdf') return 'pdf';
  if (['png', 'jpg', 'jpeg', 'webp', 'heic'].includes(ext)) return 'image';
  if (['mp3', 'm4a', 'wav', 'ogg'].includes(ext)) return 'audio';
  return 'doc';
};

/** File chọn trong form → tệp đính kèm (bản demo: link tạm của trình duyệt, khi có backend thay bằng URL đã upload) */
export const toAttachments = (items: UploadItem[] = [], links: string[] = []): SubmissionFile[] => [
  ...items.map((f) => ({ name: f.name, kind: fileKindOf(f.name), size: f.size, url: f.url ?? (f.file ? URL.createObjectURL(f.file) : '') })),
  ...links.filter(Boolean).map((url) => ({ name: url, kind: 'link' as const, url })),
];

/** Danh sách tệp đề bài / tài liệu kèm — bấm để mở / tải về */
/** <c>onOpen</c> (chế độ API): xin link tạm của tệp thứ i rồi mở — tệp lưu trên server không có địa chỉ trực tiếp */
export function AttachmentList({ files, className, onOpen }: { files: SubmissionFile[]; className?: string; onOpen?: (index: number) => void }) {
  if (!files.length) return null;
  return (
    <ul className={cn('flex flex-wrap gap-2', className)}>
      {files.map((f, i) => {
        const Icon = KIND_ICON[f.kind];
        const usable = Boolean(f.url);
        const viaServer = Boolean(onOpen) && f.kind !== 'link';
        return (
          <li key={`${f.name}-${i}`}>
            <a
              href={usable && !viaServer ? f.url : undefined}
              target="_blank"
              rel="noopener noreferrer"
              download={usable && !viaServer && f.kind !== 'link' ? f.name : undefined}
              role={viaServer ? 'button' : undefined}
              tabIndex={viaServer ? 0 : undefined}
              onClick={(e) => {
                e.stopPropagation();
                if (viaServer) {
                  e.preventDefault();
                  onOpen!(i);
                }
              }}
              className={cn(
                'inline-flex max-w-[280px] items-center gap-2 rounded-lg border border-line bg-surface px-2.5 py-1.5 text-[13px] transition-colors',
                usable ? 'cursor-pointer hover:border-primary-300 hover:bg-primary-50' : 'cursor-default opacity-70',
              )}
              title={usable ? 'Mở / tải về' : 'Bản demo — file chưa có trên máy chủ'}
            >
              <Icon size={15} className="shrink-0 text-primary-600" aria-hidden />
              <span className="truncate text-ink">{f.name}</span>
              {f.size ? <span className="shrink-0 text-xs text-ink-muted">{formatFileSize(f.size)}</span> : null}
              {usable && f.kind !== 'link' && <Download size={13} className="shrink-0 text-ink-muted" aria-hidden />}
            </a>
          </li>
        );
      })}
    </ul>
  );
}
