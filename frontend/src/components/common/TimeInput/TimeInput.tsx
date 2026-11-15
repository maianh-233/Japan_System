import { useId } from 'react';
import { TimePicker, type TimePickerProps } from 'antd';
import type { TimeRangePickerProps } from 'antd/es/time-picker';
import type { Dayjs } from 'dayjs';
import { cn } from '@/lib/cn';
import { useBreakpoint } from '@/hooks/useBreakpoint';
import { FormField, hasFieldChrome, type FieldProps } from '../FormField/FormField';

export const TIME_FORMAT = 'HH:mm';

export type TimeInputProps = Omit<TimePickerProps, 'multiple'> & FieldProps;

/** Chọn giờ HH:mm, bước 5 phút (đổi bằng minuteStep). */
export function TimeInput({
  label,
  hint,
  error,
  required,
  labelExtra,
  fieldClassName,
  id,
  status,
  className,
  format = TIME_FORMAT,
  minuteStep = 5,
  placeholder = 'hh:mm',
  ...rest
}: TimeInputProps) {
  const autoId = useId();
  const inputId = id ?? autoId;
  const { isMobile } = useBreakpoint();

  const control = (
    <TimePicker
      id={inputId}
      status={error ? 'error' : status}
      format={format}
      minuteStep={minuteStep}
      placeholder={placeholder}
      showNow={false}
      needConfirm={false}
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

export type TimeRange = [Dayjs | null, Dayjs | null] | null;
export type TimeRangeInputProps = Omit<TimeRangePickerProps, 'id'> & FieldProps & { id?: string };

/** Chọn khung giờ (bắt đầu – kết thúc), vd: giờ học 18:00 – 19:30. */
export function TimeRangeInput({
  label,
  hint,
  error,
  required,
  labelExtra,
  fieldClassName,
  id,
  status,
  className,
  format = TIME_FORMAT,
  minuteStep = 5,
  placeholder = ['Bắt đầu', 'Kết thúc'],
  ...rest
}: TimeRangeInputProps) {
  const autoId = useId();
  const inputId = id ?? autoId;
  const { isMobile } = useBreakpoint();

  const control = (
    <TimePicker.RangePicker
      id={{ start: inputId }}
      status={error ? 'error' : status}
      format={format}
      minuteStep={minuteStep}
      placeholder={placeholder}
      needConfirm={false}
      order
      inputReadOnly={isMobile}
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
