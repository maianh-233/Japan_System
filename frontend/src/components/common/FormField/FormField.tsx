import type { ReactNode } from 'react';
import { CircleAlert } from 'lucide-react';
import { cn } from '@/lib/cn';

export interface FieldProps {
  /** Nhãn hiển thị phía trên control */
  label?: ReactNode;
  /** Dòng gợi ý dưới control */
  hint?: ReactNode;
  /** Thông báo lỗi — có lỗi thì control chuyển viền đỏ */
  error?: ReactNode;
  required?: boolean;
  /** Nội dung phụ bên phải nhãn (vd: link "Quên mật khẩu?") */
  labelExtra?: ReactNode;
  /** className cho khung bao (nhãn + control + gợi ý), vd: "md:col-span-2" */
  fieldClassName?: string;
}

interface FormFieldProps extends FieldProps {
  htmlFor?: string;
  className?: string;
  children: ReactNode;
}

/**
 * Khung nhãn + gợi ý + lỗi cho control dùng ĐỘC LẬP.
 * Khi nằm trong <Form.Item> của AntD thì không cần (Form.Item đã lo nhãn & lỗi).
 */
export function FormField({ label, hint, error, required, labelExtra, htmlFor, className, children }: FormFieldProps) {
  const messageId = htmlFor ? `${htmlFor}-message` : undefined;
  return (
    <div className={cn('flex flex-col gap-1.5', className)}>
      {(label || labelExtra) && (
        <div className="flex items-center justify-between gap-2">
          {label && (
            <label htmlFor={htmlFor} className="text-[13px] font-medium text-ink-secondary">
              {label}
              {required && (
                <span className="ml-0.5 text-danger-600" aria-hidden>
                  *
                </span>
              )}
            </label>
          )}
          {labelExtra && <div className="text-[13px]">{labelExtra}</div>}
        </div>
      )}
      {children}
      {error ? (
        <p id={messageId} role="alert" className="flex items-center gap-1 text-[13px] text-danger-600">
          <CircleAlert size={14} aria-hidden />
          {error}
        </p>
      ) : hint ? (
        <p id={messageId} className="text-[13px] text-ink-muted">
          {hint}
        </p>
      ) : null}
    </div>
  );
}

/** Có cần bọc FormField không */
export function hasFieldChrome(p: FieldProps): boolean {
  return Boolean(p.label || p.hint || p.error || p.labelExtra);
}
