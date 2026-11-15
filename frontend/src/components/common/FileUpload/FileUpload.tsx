import { useEffect, useId, useRef, useState, type DragEvent, type KeyboardEvent } from 'react';
import { Progress } from 'antd';
import {
  File as FileIcon,
  FileAudio,
  FileImage,
  FileSpreadsheet,
  FileText,
  FileVideo,
  UploadCloud,
  type LucideIcon,
} from 'lucide-react';
import { cn } from '@/lib/cn';
import { formatFileSize } from '@/lib/format';
import { useControllableState } from '@/hooks/useControllableState';
import { Button } from '../Button/Button';
import { FormField, type FieldProps } from '../FormField/FormField';

export type FileKind = 'document' | 'spreadsheet' | 'audio' | 'image' | 'video';

/** Phần mở rộng cho từng loại file */
export const FILE_KIND_EXTENSIONS: Record<FileKind, string[]> = {
  document: ['pdf', 'doc', 'docx', 'ppt', 'pptx', 'txt'],
  spreadsheet: ['xls', 'xlsx', 'csv'],
  audio: ['mp3', 'wav', 'm4a', 'ogg'],
  image: ['jpg', 'jpeg', 'png', 'webp'],
  video: ['mp4', 'webm'],
};

const KIND_ICON: Record<FileKind | 'other', LucideIcon> = {
  document: FileText,
  spreadsheet: FileSpreadsheet,
  audio: FileAudio,
  image: FileImage,
  video: FileVideo,
  other: FileIcon,
};

export interface UploadItem {
  uid: string;
  name: string;
  size: number;
  /** URL sau khi upload xong (hoặc URL file đã có từ server) */
  url?: string;
  /** File gốc — còn giữ khi chưa upload (để gửi cùng form) */
  file?: File;
  status: 'ready' | 'uploading' | 'done' | 'error';
  progress?: number;
  error?: string;
}

export interface FileUploadProps extends FieldProps {
  value?: UploadItem[];
  defaultValue?: UploadItem[];
  onChange?: (items: UploadItem[]) => void;
  /** Loại file cho phép (một hoặc nhiều) */
  kind?: FileKind | FileKind[];
  maxSizeMB?: number;
  maxFiles?: number;
  /**
   * Hàm upload lên server. Có → tự upload ngay khi chọn, hiện tiến độ.
   * Không có → chỉ giữ file (status "ready") để gửi cùng form.
   */
  upload?: (file: File, onProgress: (percent: number) => void) => Promise<{ url: string }>;
  disabled?: boolean;
  /** Dòng chữ trong vùng kéo-thả */
  placeholder?: string;
  className?: string;
  id?: string;
}

function extOf(name: string): string {
  return name.split('.').pop()?.toLowerCase() ?? '';
}

function kindOf(name: string): FileKind | 'other' {
  const ext = extOf(name);
  return (Object.keys(FILE_KIND_EXTENSIONS) as FileKind[]).find((k) => FILE_KIND_EXTENSIONS[k].includes(ext)) ?? 'other';
}

let uidSeed = 0;
const nextUid = () => `upl-${Date.now()}-${uidSeed++}`;

/** Tải file: kéo-thả nhiều file, kiểm tra loại & dung lượng, tiến độ upload, nghe thử audio. */
export function FileUpload({
  value,
  defaultValue = [],
  onChange,
  kind = ['document', 'spreadsheet'],
  maxSizeMB = 20,
  maxFiles = 10,
  upload,
  disabled = false,
  placeholder,
  label,
  hint,
  error,
  required,
  labelExtra,
  fieldClassName,
  className,
  id,
}: FileUploadProps) {
  const autoId = useId();
  const inputId = id ?? autoId;
  const inputRef = useRef<HTMLInputElement>(null);
  const [items, setItems] = useControllableState(value, defaultValue, onChange);
  const [dragging, setDragging] = useState(false);
  const [rejectMsg, setRejectMsg] = useState<string | null>(null);

  // object URL để nghe/xem thử file chưa upload — giải phóng khi gỡ
  const previews = useRef(new Map<string, string>());
  useEffect(() => {
    const map = previews.current;
    return () => map.forEach((u) => URL.revokeObjectURL(u));
  }, []);

  const kinds = Array.isArray(kind) ? kind : [kind];
  const allowedExt = kinds.flatMap((k) => FILE_KIND_EXTENSIONS[k]);
  const reachedMax = items.length >= maxFiles;

  const startUpload = async (item: UploadItem) => {
    if (!upload || !item.file) return;
    const patch = (p: Partial<UploadItem>) => setItems((prev) => prev.map((x) => (x.uid === item.uid ? { ...x, ...p } : x)));
    patch({ status: 'uploading', progress: 0, error: undefined });
    try {
      const res = await upload(item.file, (percent) => patch({ progress: Math.round(percent) }));
      patch({ status: 'done', progress: 100, url: res.url });
    } catch (e) {
      patch({ status: 'error', error: e instanceof Error ? e.message : 'Tải lên thất bại' });
    }
  };

  const addFiles = (list: FileList | null) => {
    if (!list || disabled) return;
    const rejected: string[] = [];
    const accepted: UploadItem[] = [];
    for (const file of Array.from(list)) {
      if (items.length + accepted.length >= maxFiles) {
        rejected.push(`Tối đa ${maxFiles} file`);
        break;
      }
      if (!allowedExt.includes(extOf(file.name))) {
        rejected.push(`${file.name}: không đúng định dạng`);
        continue;
      }
      if (file.size > maxSizeMB * 1024 * 1024) {
        rejected.push(`${file.name}: vượt quá ${maxSizeMB} MB`);
        continue;
      }
      const uid = nextUid();
      if (['audio', 'image', 'video'].includes(kindOf(file.name))) {
        previews.current.set(uid, URL.createObjectURL(file));
      }
      accepted.push({ uid, name: file.name, size: file.size, file, status: 'ready' });
    }
    setRejectMsg(rejected.length ? rejected.join(' · ') : null);
    if (accepted.length) {
      setItems((prev) => [...prev, ...accepted]);
      accepted.forEach(startUpload);
    }
  };

  const remove = (uid: string) => {
    const url = previews.current.get(uid);
    if (url) {
      URL.revokeObjectURL(url);
      previews.current.delete(uid);
    }
    setItems((prev) => prev.filter((x) => x.uid !== uid));
  };

  const openPicker = () => {
    if (!disabled && !reachedMax) inputRef.current?.click();
  };

  const onKeyDown = (e: KeyboardEvent<HTMLDivElement>) => {
    if (e.key === 'Enter' || e.key === ' ') {
      e.preventDefault();
      openPicker();
    }
  };

  const onDrop = (e: DragEvent<HTMLDivElement>) => {
    e.preventDefault();
    setDragging(false);
    addFiles(e.dataTransfer.files);
  };

  const shownError = error ?? rejectMsg;
  const defaultHint = `${allowedExt.map((e) => e.toUpperCase()).join(', ')} · tối đa ${maxSizeMB} MB/file · ${maxFiles} file`;

  return (
    <FormField
      label={label}
      hint={hint ?? defaultHint}
      error={shownError}
      required={required}
      labelExtra={labelExtra}
      htmlFor={inputId}
      className={cn(className, fieldClassName)}
    >
      <input
        ref={inputRef}
        id={inputId}
        type="file"
        multiple={maxFiles > 1}
        accept={allowedExt.map((e) => `.${e}`).join(',')}
        className="sr-only"
        tabIndex={-1}
        disabled={disabled}
        onChange={(e) => {
          addFiles(e.target.files);
          e.target.value = '';
        }}
      />

      <div
        role="button"
        tabIndex={disabled || reachedMax ? -1 : 0}
        aria-disabled={disabled || reachedMax || undefined}
        onClick={openPicker}
        onKeyDown={onKeyDown}
        onDragOver={(e) => {
          e.preventDefault();
          if (!disabled && !reachedMax) setDragging(true);
        }}
        onDragLeave={() => setDragging(false)}
        onDrop={onDrop}
        className={cn(
          'flex cursor-pointer flex-col items-center justify-center gap-2 rounded-[var(--radius-card)] border-2 border-dashed px-4 py-6 text-center transition-colors',
          'focus-visible:ring-[3px] focus-visible:ring-primary-500/30 focus-visible:outline-none',
          dragging
            ? 'border-primary-500 bg-primary-100'
            : 'border-line-strong bg-surface hover:border-primary-400 hover:bg-primary-50',
          (disabled || reachedMax) && 'pointer-events-none opacity-50',
        )}
      >
        <span className="flex h-11 w-11 items-center justify-center rounded-full bg-primary-100 text-primary-600">
          <UploadCloud size={22} aria-hidden />
        </span>
        <span className="text-ink-secondary">
          {reachedMax ? (
            `Đã đủ ${maxFiles} file`
          ) : (
            placeholder ?? (
              <>
                <span className="font-medium text-primary-700">Bấm để chọn</span>
                <span className="max-sm:hidden"> hoặc kéo-thả file vào đây</span>
              </>
            )
          )}
        </span>
      </div>

      {items.length > 0 && (
        <ul className="mt-1 flex flex-col gap-2">
          {items.map((it) => {
            const k = kindOf(it.name);
            const Icon = KIND_ICON[k];
            const previewUrl = it.url ?? previews.current.get(it.uid);
            return (
              <li
                key={it.uid}
                className={cn(
                  'flex flex-col gap-2 rounded-lg border px-3 py-2.5',
                  it.status === 'error' ? 'border-danger-100 bg-danger-50' : 'border-line bg-surface',
                )}
              >
                <div className="flex items-center gap-3">
                  <span className="flex h-9 w-9 shrink-0 items-center justify-center rounded-lg bg-primary-50 text-primary-600">
                    <Icon size={18} aria-hidden />
                  </span>
                  <div className="min-w-0 flex-1">
                    {it.url ? (
                      <a href={it.url} target="_blank" rel="noreferrer" className="block truncate font-medium text-primary-700 hover:underline">
                        {it.name}
                      </a>
                    ) : (
                      <p className="truncate font-medium text-ink">{it.name}</p>
                    )}
                    <p className="tabular text-xs text-ink-muted">
                      {formatFileSize(it.size)}
                      {it.status === 'uploading' && ` · Đang tải lên ${it.progress ?? 0}%`}
                      {it.status === 'done' && ' · Đã tải lên'}
                      {it.status === 'ready' && upload === undefined && ' · Sẵn sàng'}
                      {it.status === 'error' && <span className="text-danger-700"> · {it.error}</span>}
                    </p>
                  </div>
                  {it.status === 'error' && upload && (
                    <Button action="refresh" iconOnly size="sm" tooltip="Thử lại" onClick={() => startUpload(it)} />
                  )}
                  <Button
                    action="delete"
                    variant="ghost"
                    iconOnly
                    size="sm"
                    aria-label={`Gỡ ${it.name}`}
                    disabled={disabled || it.status === 'uploading'}
                    onClick={() => remove(it.uid)}
                  />
                </div>
                {it.status === 'uploading' && (
                  <Progress percent={it.progress ?? 0} size="small" showInfo={false} strokeColor="#1F6FC5" />
                )}
                {k === 'audio' && previewUrl && (
                  <audio controls preload="none" src={previewUrl} className="h-9 w-full">
                    Trình duyệt không hỗ trợ phát audio.
                  </audio>
                )}
              </li>
            );
          })}
        </ul>
      )}
    </FormField>
  );
}
