import { useId } from 'react';
import { DatePicker, type DatePickerProps } from 'antd';
import type { Dayjs } from 'dayjs';
import { cn } from '@/lib/cn';
import { useBreakpoint } from '@/hooks/useBreakpoint';
import { FormField, hasFieldChrome, type FieldProps } from '../FormField/FormField';

export const DATE_FORMAT = 'DD/MM/YYYY';
export const DATE_TIME_FORMAT = 'DD/MM/YYYY HH:mm';

export type DateInputProps = Omit<DatePickerProps<Dayjs>, 'multiple'> & FieldProps;

/**
 * Chọn ngày, định dạng dd/mm/yyyy. Thêm `showTime` để chọn cả giờ.
 * Mobile: chặn bàn phím ảo bật lên (chỉ chọn trên lịch).
 */
export function DateInput({
  label,
  hint,
  error,
  required,
  labelExtra,
  fieldClassName,
  id,
  status,
  className,
  format,
  showTime,
  placeholder,
  ...rest
}: DateInputProps) {
  const autoId = useId();
  const inputId = id ?? autoId;
  const { isMobile } = useBreakpoint();

  const control = (
    <DatePicker
      id={inputId}
      status={error ? 'error' : status}
      format={format ?? (showTime ? DATE_TIME_FORMAT : DATE_FORMAT)}
      showTime={showTime === true ? { format: 'HH:mm', minuteStep: 5 } : showTime}
      placeholder={placeholder ?? (showTime ? 'dd/mm/yyyy hh:mm' : 'dd/mm/yyyy')}
      inputReadOnly={isMobile}
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
