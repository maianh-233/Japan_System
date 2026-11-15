import { useCallback, useEffect, useRef, useState, type ReactNode } from 'react';
import { Modal } from 'antd';
import { CircleAlert, CircleCheck, CircleHelp, TriangleAlert, type LucideIcon } from 'lucide-react';
import { cn } from '@/lib/cn';
import { Button } from '../Button/Button';
import type { ButtonAction } from '../Button/buttonActions';
import { TextArea } from '../TextArea/TextArea';
import { TextInput } from '../TextInput/TextInput';
import { iconToneClasses } from '../tones';

export type ConfirmTone = 'danger' | 'warning' | 'primary' | 'success';

export interface ConfirmOptions {
  title: ReactNode;
  description?: ReactNode;
  /** Liệt kê HẬU QUẢ cụ thể (vd: "12 học viên sẽ nhận thông báo") */
  consequences?: ReactNode[];
  tone?: ConfirmTone;
  /** Action của nút xác nhận → quyết định icon + nhãn mặc định */
  confirmAction?: ButtonAction;
  confirmText?: string;
  cancelText?: string;
  /** Bắt buộc nhập lý do (từ chối đơn, hủy buổi...) */
  requireReason?: boolean | { label?: string; placeholder?: string; minLength?: number };
  /** Phải gõ đúng chuỗi này mới bật nút (thao tác rất nguy hiểm: hủy cả lớp, xóa chi nhánh) */
  typeToConfirm?: string;
  /** Chạy khi bấm xác nhận. Lỗi (throw) → giữ hộp thoại và hiện lỗi. */
  onConfirm?: (ctx: { reason?: string }) => Promise<void> | void;
}

const toneMeta: Record<ConfirmTone, { icon: LucideIcon; action: ButtonAction }> = {
  danger: { icon: TriangleAlert, action: 'delete' },
  warning: { icon: CircleAlert, action: 'save' },
  primary: { icon: CircleHelp, action: 'save' },
  success: { icon: CircleCheck, action: 'approve' },
};

export interface ConfirmDialogProps extends ConfirmOptions {
  open: boolean;
  onCancel: () => void;
  /** Gọi sau khi onConfirm chạy xong không lỗi */
  onDone?: (ctx: { reason?: string }) => void;
}

/** Hộp xác nhận: nêu rõ hậu quả, có thể bắt nhập lý do hoặc gõ xác nhận. */
export function ConfirmDialog({
  open,
  onCancel,
  onDone,
  title,
  description,
  consequences,
  tone = 'danger',
  confirmAction,
  confirmText,
  cancelText = 'Hủy',
  requireReason,
  typeToConfirm,
  onConfirm,
}: ConfirmDialogProps) {
  const [reason, setReason] = useState('');
  const [typed, setTyped] = useState('');
  const [loading, setLoading] = useState(false);
  const [error, setError] = useState<string | null>(null);

  useEffect(() => {
    if (open) {
      setReason('');
      setTyped('');
      setError(null);
      setLoading(false);
    }
  }, [open]);

  const meta = toneMeta[tone];
  const Icon = meta.icon;
  const reasonCfg = typeof requireReason === 'object' ? requireReason : {};
  const minLength = reasonCfg.minLength ?? 5;
  const reasonInvalid = Boolean(requireReason) && reason.trim().length < minLength;
  const typedInvalid = Boolean(typeToConfirm) && typed.trim() !== typeToConfirm;

  const handleConfirm = async () => {
    if (reasonInvalid || typedInvalid) return;
    const ctx = { reason: requireReason ? reason.trim() : undefined };
    setLoading(true);
    setError(null);
    try {
      await onConfirm?.(ctx);
      onDone?.(ctx);
    } catch (e) {
      setError(e instanceof Error ? e.message : 'Có lỗi xảy ra, vui lòng thử lại.');
    } finally {
      setLoading(false);
    }
  };

  return (
    <Modal
      open={open}
      onCancel={loading ? undefined : onCancel}
      maskClosable={!loading}
      closable={!loading}
      footer={null}
      width={480}
      centered
      destroyOnHidden
      title={null}
    >
      <div className="flex flex-col gap-4 sm:flex-row sm:items-start">
        <span className={cn('flex h-11 w-11 shrink-0 items-center justify-center rounded-full', iconToneClasses[tone])}>
          <Icon size={22} aria-hidden />
        </span>
        <div className="flex min-w-0 flex-1 flex-col gap-3">
          <div className="flex flex-col gap-1 pr-6">
            <h2 className="text-base leading-6 font-semibold text-ink">{title}</h2>
            {description && <div className="text-ink-secondary">{description}</div>}
          </div>

          {consequences && consequences.length > 0 && (
            <ul className="flex list-disc flex-col gap-1 rounded-lg bg-page py-2.5 pr-3 pl-7 text-[13px] text-ink-secondary">
              {consequences.map((c, i) => (
                <li key={i}>{c}</li>
              ))}
            </ul>
          )}

          {requireReason && (
            <TextArea
              label={reasonCfg.label ?? 'Lý do'}
              required
              placeholder={reasonCfg.placeholder ?? 'Nhập lý do...'}
              value={reason}
              onChange={(e) => setReason(e.target.value)}
              minRows={2}
              maxLength={500}
              hint={reasonInvalid && reason.length > 0 ? `Tối thiểu ${minLength} ký tự` : undefined}
            />
          )}

          {typeToConfirm && (
            <TextInput
              label={
                <>
                  Gõ <strong className="text-ink">{typeToConfirm}</strong> để xác nhận
                </>
              }
              value={typed}
              onChange={(e) => setTyped(e.target.value)}
              autoComplete="off"
              allowClear={false}
            />
          )}

          {error && (
            <p role="alert" className="rounded-lg bg-danger-50 px-3 py-2 text-[13px] text-danger-700">
              {error}
            </p>
          )}
        </div>
      </div>

      <div className="mt-6 flex flex-col-reverse gap-2 sm:flex-row sm:justify-end">
        <Button action="cancel" onClick={onCancel} disabled={loading} block className="sm:w-auto">
          {cancelText}
        </Button>
        <Button
          action={confirmAction ?? meta.action}
          variant="solid"
          tone={tone}
          loading={loading}
          disabled={reasonInvalid || typedInvalid}
          onClick={handleConfirm}
          block
          className="sm:w-auto"
        >
          {confirmText}
        </Button>
      </div>
    </Modal>
  );
}

/**
 * Gọi hộp xác nhận kiểu "await":
 *   const { confirm, confirmDialog } = useConfirm();
 *   const ok = await confirm({ title: 'Xóa học viên?', onConfirm: () => api.delete(id) });
 *   if (ok) ...   // ok = { reason } hoặc null nếu bấm Hủy
 *   return <>{confirmDialog} ...</>
 */
export function useConfirm() {
  const [options, setOptions] = useState<ConfirmOptions | null>(null);
  const [open, setOpen] = useState(false);
  const resolver = useRef<((v: { reason?: string } | null) => void) | null>(null);

  const confirm = useCallback((opts: ConfirmOptions) => {
    resolver.current?.(null);
    setOptions(opts);
    setOpen(true);
    return new Promise<{ reason?: string } | null>((resolve) => {
      resolver.current = resolve;
    });
  }, []);

  const finish = (result: { reason?: string } | null) => {
    resolver.current?.(result);
    resolver.current = null;
    setOpen(false);
  };

  const confirmDialog = options ? (
    <ConfirmDialog {...options} open={open} onCancel={() => finish(null)} onDone={(ctx) => finish(ctx)} />
  ) : null;

  return { confirm, confirmDialog };
}
