import { useMemo, type ReactNode } from 'react';
import { App } from 'antd';
import { CircleCheck, CircleX, Info, Loader2, TriangleAlert, CircleHelp, type LucideIcon } from 'lucide-react';
import { cn } from '@/lib/cn';
import { useBreakpoint } from '@/hooks/useBreakpoint';
import { Button } from '../Button/Button';
import type { ButtonAction } from '../Button/buttonActions';
import { accentBorderClasses, iconToneClasses, type Tone } from '../tones';

export type NotifyKind = 'success' | 'info' | 'warning' | 'error' | 'loading';

export interface NotifyOptions {
  title: ReactNode;
  description?: ReactNode;
  /** Giây; 0 = không tự đóng. Mặc định theo loại (lỗi giữ lâu hơn thành công). */
  duration?: number;
  /** Nút hành động trong thông báo (vd: "Xem chi tiết", "Hoàn tác") */
  actions?: ReactNode;
  /** Cùng key → cập nhật thông báo cũ thay vì tạo mới (loading → success) */
  key?: string;
  onClose?: () => void;
}

export interface NotifyConfirmOptions extends Omit<NotifyOptions, 'actions' | 'duration'> {
  tone?: 'warning' | 'primary' | 'danger' | 'success';
  confirmAction?: ButtonAction;
  confirmText?: string;
  cancelText?: string;
}

const META: Record<NotifyKind, { icon: LucideIcon; tone: Tone; duration: number }> = {
  success: { icon: CircleCheck, tone: 'success', duration: 3 },
  info: { icon: Info, tone: 'primary', duration: 4.5 },
  warning: { icon: TriangleAlert, tone: 'warning', duration: 6 },
  error: { icon: CircleX, tone: 'danger', duration: 8 },
  loading: { icon: Loader2, tone: 'primary', duration: 0 },
};

function IconBadge({ icon: Icon, tone, spin }: { icon: LucideIcon; tone: Tone; spin?: boolean }) {
  return (
    <span className={cn('flex h-8 w-8 items-center justify-center rounded-full', iconToneClasses[tone])}>
      <Icon size={18} aria-hidden className={spin ? 'animate-spin' : undefined} />
    </span>
  );
}

let keySeed = 0;

/**
 * Thông báo góc màn hình theo CHỨC NĂNG:
 *   const notify = useNotify();
 *   notify.success({ title: 'Đã lưu học viên' });
 *   notify.warning({ title: 'Lớp sắp đủ sĩ số', description: 'Còn 2 chỗ trống' });
 *   notify.error({ title: 'Không lưu được', description: err.message });
 *   const ok = await notify.confirm({ title: 'Có 3 đơn xin nghỉ mới', confirmText: 'Duyệt ngay' });
 *   await notify.promise(api.save(), { loading: 'Đang lưu...', success: 'Đã lưu', error: 'Lưu thất bại' });
 *   notify.toast.success('Đã sao chép');   // thông báo nhanh giữa màn hình
 */
export function useNotify() {
  const { notification, message } = App.useApp();
  const { isMobile } = useBreakpoint();

  return useMemo(() => {
    const show = (kind: NotifyKind, opts: NotifyOptions) => {
      const meta = META[kind];
      const key = opts.key ?? `ntf-${++keySeed}`;
      notification.open({
        key,
        message: <span className="font-semibold text-ink">{opts.title}</span>,
        description: opts.description ? <span className="text-ink-secondary">{opts.description}</span> : undefined,
        icon: <IconBadge icon={meta.icon} tone={meta.tone} spin={kind === 'loading'} />,
        duration: opts.duration ?? meta.duration,
        placement: isMobile ? 'top' : 'topRight',
        actions: opts.actions,
        onClose: opts.onClose,
        role: kind === 'error' || kind === 'warning' ? 'alert' : 'status',
        className: cn('!rounded-[var(--radius-card)] border-l-4', accentBorderClasses[meta.tone]),
        showProgress: (opts.duration ?? meta.duration) > 0,
        pauseOnHover: true,
      });
      return key;
    };

    const confirm = (opts: NotifyConfirmOptions) =>
      new Promise<boolean>((resolve) => {
        const key = opts.key ?? `ntf-confirm-${++keySeed}`;
        let settled = false;
        const settle = (v: boolean) => {
          if (settled) return;
          settled = true;
          notification.destroy(key);
          resolve(v);
        };
        const tone: Tone = opts.tone ?? 'warning';
        notification.open({
          key,
          message: <span className="font-semibold text-ink">{opts.title}</span>,
          description: opts.description ? <span className="text-ink-secondary">{opts.description}</span> : undefined,
          icon: <IconBadge icon={CircleHelp} tone={tone} />,
          duration: 0,
          placement: isMobile ? 'top' : 'topRight',
          role: 'alert',
          className: cn('!rounded-[var(--radius-card)] border-l-4', accentBorderClasses[tone]),
          onClose: () => {
            opts.onClose?.();
            settle(false);
          },
          actions: (
            <div className="flex gap-2">
              <Button action="cancel" size="sm" variant="ghost" onClick={() => settle(false)}>
                {opts.cancelText ?? 'Bỏ qua'}
              </Button>
              <Button
                action={opts.confirmAction ?? 'approve'}
                size="sm"
                variant="solid"
                tone={opts.tone && opts.tone !== 'warning' ? opts.tone : 'primary'}
                onClick={() => settle(true)}
              >
                {opts.confirmText ?? 'Xác nhận'}
              </Button>
            </div>
          ),
        });
      });

    const promise = async <T,>(
      task: Promise<T>,
      text: { loading: ReactNode; success: ReactNode | ((v: T) => ReactNode); error?: ReactNode | ((e: unknown) => ReactNode) },
    ): Promise<T> => {
      const key = show('loading', { title: text.loading });
      try {
        const v = await task;
        show('success', { key, title: typeof text.success === 'function' ? text.success(v) : text.success });
        return v;
      } catch (e) {
        const fallback = e instanceof Error ? e.message : 'Có lỗi xảy ra';
        show('error', {
          key,
          title: typeof text.error === 'function' ? text.error(e) : (text.error ?? 'Thao tác thất bại'),
          description: typeof text.error === 'function' ? undefined : fallback,
        });
        throw e;
      }
    };

    return {
      success: (o: NotifyOptions) => show('success', o),
      info: (o: NotifyOptions) => show('info', o),
      warning: (o: NotifyOptions) => show('warning', o),
      error: (o: NotifyOptions) => show('error', o),
      loading: (o: NotifyOptions) => show('loading', o),
      confirm,
      promise,
      close: (key: string) => notification.destroy(key),
      closeAll: () => notification.destroy(),
      /** Thông báo nhanh 1 dòng giữa trên màn hình */
      toast: {
        success: (text: ReactNode) => message.success(text),
        info: (text: ReactNode) => message.info(text),
        warning: (text: ReactNode) => message.warning(text),
        error: (text: ReactNode) => message.error(text),
      },
    };
  }, [notification, message, isMobile]);
}
