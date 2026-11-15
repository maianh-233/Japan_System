import { useId } from 'react';
import { DatePicker } from 'antd';
import type { RangePickerProps } from 'antd/es/date-picker';
import type { Dayjs } from 'dayjs';
import { cn } from '@/lib/cn';
import { useBreakpoint } from '@/hooks/useBreakpoint';
import { FormField, hasFieldChrome, type FieldProps } from '../FormField/FormField';
import { DATE_FORMAT } from './DateInput';

export type DateRange = [Dayjs | null, Dayjs | null] | null;

export interface DateRangeInputProps
  extends Omit<RangePickerProps, 'value' | 'onChange' | 'defaultValue' | 'id'>,
    FieldProps {
  id?: string;
  value?: DateRange;
  onChange?: (value: DateRange) => void;
  /** Nhãn 2 ô trên mobile */
  startPlaceholder?: string;
  endPlaceholder?: string;
}

/**
 * Chọn khoảng ngày (từ – đến).
 * Desktop: RangePicker 2 tháng. Mobile (< 640px): 2 ô ngày riêng vì lịch đôi tràn màn hình.
 */
export function DateRangeInput({
  label,
  hint,
  error,
  required,
  labelExtra,
  fieldClassName,
  id,
  status,
  className,
  value,
  onChange,
  disabled,
  startPlaceholder = 'Từ ngày',
  endPlaceholder = 'Đến ngày',
  ...rest
}: DateRangeInputProps) {
  const autoId = useId();
  const inputId = id ?? autoId;
  const { isMobile } = useBreakpoint();
  const finalStatus = error ? 'error' : status;
  const [start, end] = value ?? [null, null];
  const isDisabled = typeof disabled === 'boolean' ? disabled : false;

  const control = isMobile ? (
    <div id={inputId} className={cn('grid grid-cols-2 gap-2', className)}>
      <DatePicker
        value={start}
        format={DATE_FORMAT}
        placeholder={startPlaceholder}
        inputReadOnly
        status={finalStatus}
        disabled={isDisabled}
        disabledDate={(d) => (end ? d.isAfter(end, 'day') : false)}
        onChange={(d) => onChange?.(d || end ? [d, end] : null)}
        aria-label={startPlaceholder}
      />
      <DatePicker
        value={end}
        format={DATE_FORMAT}
        placeholder={endPlaceholder}
        inputReadOnly
        status={finalStatus}
        disabled={isDisabled}
        disabledDate={(d) => (start ? d.isBefore(start, 'day') : false)}
        onChange={(d) => onChange?.(start || d ? [start, d] : null)}
        aria-label={endPlaceholder}
      />
    </div>
  ) : (
    <DatePicker.RangePicker
      id={{ start: inputId }}
      value={value}
      onChange={(v) => onChange?.(v ? [v[0], v[1]] : null)}
      format={DATE_FORMAT}
      placeholder={[startPlaceholder, endPlaceholder]}
      status={finalStatus}
      disabled={disabled}
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
