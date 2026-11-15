import { useId } from 'react';
import { Select as AntSelect, type SelectProps as AntSelectProps } from 'antd';
import { Inbox } from 'lucide-react';
import { cn } from '@/lib/cn';
import { includesText } from '@/lib/text';
import { FormField, hasFieldChrome, type FieldProps } from '../FormField/FormField';

export type SelectOption = { value: string | number; label: string; disabled?: boolean };

export interface SelectProps<V = unknown> extends Omit<AntSelectProps<V>, 'status'>, FieldProps {
  status?: AntSelectProps<V>['status'];
}

/**
 * Ô chọn: mặc định có tìm kiếm KHÔNG PHÂN BIỆT DẤU ("nguyen" khớp "Nguyễn") và nút xóa.
 * `mode="multiple"` để chọn nhiều; trên mobile tự gọn tag thành "+N".
 */
export function Select<V = unknown>({
  label,
  hint,
  error,
  required,
  labelExtra,
  fieldClassName,
  id,
  status,
  className,
  showSearch = true,
  allowClear = true,
  filterOption,
  mode,
  maxTagCount,
  notFoundContent,
  ...rest
}: SelectProps<V>) {
  const autoId = useId();
  const inputId = id ?? autoId;

  const control = (
    <AntSelect<V>
      id={inputId}
      status={error ? 'error' : status}
      showSearch={showSearch}
      allowClear={allowClear}
      mode={mode}
      maxTagCount={maxTagCount ?? (mode ? 'responsive' : undefined)}
      filterOption={filterOption ?? ((input, option) => includesText(option?.label ?? option?.value, input))}
      notFoundContent={
        notFoundContent ?? (
          <div className="flex flex-col items-center gap-1 py-4 text-ink-muted">
            <Inbox size={20} aria-hidden />
            <span className="text-[13px]">Không có dữ liệu</span>
          </div>
        )
      }
      aria-required={required || undefined}
      aria-invalid={error ? true : undefined}
      className={cn('w-full', className)}
      {...rest}
    />
  );

  if (!hasFieldChrome({ label, hint, error, labelExtra })) {
    return fieldClassName ? <div className={fieldClassName}>{control}</div> : control;
  }
  return (
    <FormField label={label} hint={hint} error={error} required={required} labelExtra={labelExtra} htmlFor={inputId} className={fieldClassName}>
      {control}
    </FormField>
  );
}
