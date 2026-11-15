import { useEffect, useId, useRef, useState, type DragEvent, type KeyboardEvent } from 'react';
import { Image } from 'antd';
import { ImagePlus, RefreshCw, ZoomIn } from 'lucide-react';
import { cn } from '@/lib/cn';
import { formatFileSize } from '@/lib/format';
import { Button } from '../Button/Button';
import { FormField, type FieldProps } from '../FormField/FormField';

export type ImageShape = 'square' | 'circle' | 'wide';

export interface ImagePickerProps extends FieldProps {
  /** URL ảnh hiện tại (ảnh từ server hoặc object URL vừa chọn) */
  value?: string | null;
  /** file = null khi người dùng xóa ảnh */
  onChange?: (value: string | null, file: File | null) => void;
  /** Hình dạng khung: vuông (mặc định), tròn (avatar), ngang 16:9 (banner) */
  shape?: ImageShape;
  /** Dung lượng tối đa (MB) */
  maxSizeMB?: number;
  /** Định dạng chấp nhận */
  accept?: string[];
  disabled?: boolean;
  /** Dòng chữ trong khung khi chưa có ảnh */
  placeholder?: string;
  id?: string;
  className?: string;
}

const DEFAULT_ACCEPT = ['image/jpeg', 'image/png', 'image/webp'];

const shapeClasses: Record<ImageShape, string> = {
  square: 'h-40 w-40 rounded-[var(--radius-card)]',
  circle: 'h-32 w-32 rounded-full',
  wide: 'aspect-video w-full max-w-md rounded-[var(--radius-card)]',
};

/** Khung chọn ảnh: bấm / kéo-thả / bàn phím, xem trước, phóng to, đổi, xóa, kiểm tra định dạng & dung lượng. */
export function ImagePicker({
  value,
  onChange,
  shape = 'square',
  maxSizeMB = 2,
  accept = DEFAULT_ACCEPT,
  disabled = false,
  placeholder = 'Bấm hoặc kéo ảnh vào đây',
  label,
  hint,
  error,
  required,
  labelExtra,
  fieldClassName,
  id,
  className,
}: ImagePickerProps) {
  const autoId = useId();
  const inputId = id ?? autoId;
  const inputRef = useRef<HTMLInputElement>(null);
  const objectUrlRef = useRef<string | null>(null);
  const [dragging, setDragging] = useState(false);
  const [previewOpen, setPreviewOpen] = useState(false);
  const [localError, setLocalError] = useState<string | null>(null);

  // Giải phóng object URL do component tạo khi unmount
  useEffect(
    () => () => {
      if (objectUrlRef.current) URL.revokeObjectURL(objectUrlRef.current);
    },
    [],
  );

  const releaseObjectUrl = () => {
    if (objectUrlRef.current) {
      URL.revokeObjectURL(objectUrlRef.current);
      objectUrlRef.current = null;
    }
  };

  const handleFile = (file: File | undefined) => {
    if (!file) return;
    if (!accept.includes(file.type)) {
      setLocalError(`Chỉ chấp nhận ảnh ${accept.map((t) => t.replace('image/', '').toUpperCase()).join(', ')}.`);
      return;
    }
    if (file.size > maxSizeMB * 1024 * 1024) {
      setLocalError(`Ảnh ${formatFileSize(file.size)} vượt quá giới hạn ${maxSizeMB} MB.`);
      return;
    }
    setLocalError(null);
    releaseObjectUrl();
    const url = URL.createObjectURL(file);
    objectUrlRef.current = url;
    onChange?.(url, file);
  };

  const openPicker = () => {
    if (!disabled) inputRef.current?.click();
  };

  const remove = () => {
    releaseObjectUrl();
    setLocalError(null);
    onChange?.(null, null);
  };

  const onDrop = (e: DragEvent<HTMLDivElement>) => {
    e.preventDefault();
    setDragging(false);
    if (!disabled) handleFile(e.dataTransfer.files?.[0]);
  };

  const onKeyDown = (e: KeyboardEvent<HTMLDivElement>) => {
    if (e.key === 'Enter' || e.key === ' ') {
      e.preventDefault();
      openPicker();
    }
  };

  const shownError = error ?? localError;
  const defaultHint = `${accept.map((t) => t.replace('image/', '').toUpperCase()).join(', ')} · tối đa ${maxSizeMB} MB`;

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
        accept={accept.join(',')}
        className="sr-only"
        tabIndex={-1}
        disabled={disabled}
        onChange={(e) => {
          handleFile(e.target.files?.[0]);
          e.target.value = ''; // cho phép chọn lại cùng một file
        }}
      />

      {value ? (
        <div className="flex flex-col items-start gap-2">
          <div
            className={cn(
              'relative overflow-hidden border border-line bg-primary-50 shadow-[var(--shadow-soft-sm)]',
              shapeClasses[shape],
            )}
          >
            <Image
              src={value}
              alt="Ảnh đã chọn"
              width="100%"
              height="100%"
              className="object-cover"
              rootClassName="!block h-full w-full [&_img]:h-full [&_img]:object-cover"
              preview={{ visible: previewOpen, onVisibleChange: setPreviewOpen, mask: false }}
            />
          </div>
          <div className="flex flex-wrap gap-1.5">
            <Button icon={ZoomIn} size="sm" tone="neutral" variant="ghost" onClick={() => setPreviewOpen(true)}>
              Xem
            </Button>
            <Button icon={RefreshCw} size="sm" tone="primary" variant="soft" onClick={openPicker} disabled={disabled}>
              Đổi ảnh
            </Button>
            <Button action="delete" size="sm" variant="ghost" onClick={remove} disabled={disabled} />
          </div>
        </div>
      ) : (
        <div
          role="button"
          tabIndex={disabled ? -1 : 0}
          aria-disabled={disabled || undefined}
          aria-label={placeholder}
          onClick={openPicker}
          onKeyDown={onKeyDown}
          onDragOver={(e) => {
            e.preventDefault();
            if (!disabled) setDragging(true);
          }}
          onDragLeave={() => setDragging(false)}
          onDrop={onDrop}
          className={cn(
            'flex cursor-pointer flex-col items-center justify-center gap-2 border-2 border-dashed p-4 text-center',
            'transition-colors duration-150 focus-visible:ring-[3px] focus-visible:ring-primary-500/30 focus-visible:outline-none',
            shapeClasses[shape],
            dragging
              ? 'border-primary-500 bg-primary-100'
              : shownError
                ? 'border-danger-600 bg-danger-50'
                : 'border-line-strong bg-surface hover:border-primary-400 hover:bg-primary-50',
            disabled && 'pointer-events-none opacity-50',
          )}
        >
          <span className="flex h-10 w-10 items-center justify-center rounded-full bg-primary-100 text-primary-600">
            <ImagePlus size={20} aria-hidden />
          </span>
          <span className="text-[13px] leading-5 text-ink-secondary">{placeholder}</span>
        </div>
      )}
    </FormField>
  );
}
