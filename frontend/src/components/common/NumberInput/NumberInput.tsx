import { forwardRef, useId, type ComponentRef } from 'react';
import { InputNumber, type InputNumberProps } from 'antd';
import { cn } from '@/lib/cn';
import { FormField, hasFieldChrome, type FieldProps } from '../FormField/FormField';

export interface NumberInputProps
  extends Omit<InputNumberProps<number>, 'formatter' | 'parser' | 'decimalSeparator' | 'suffix'>,
    FieldProps {
  /** Chế độ tiền VND: hậu tố ₫, không âm, không số lẻ */
  currency?: boolean;
  /** Đơn vị hiển thị cuối ô (vd: "buổi", "%", "học viên") */
  unit?: string;
}

/** 1234567.5 → "1.234.567,5" (định dạng vi-VN) */
function formatVi(value: string | number | undefined): string {
  if (value === undefined || value === null || value === '') return '';
  const [intPart, decPart] = String(value).split('.');
  const grouped = intPart.replace(/\B(?=(\d{3})+(?!\d))/g, '.');
  return decPart !== undefined ? `${grouped},${decPart}` : grouped;
}

/** "1.234.567,5" → 1234567.5 */
function parseVi(text: string | undefined): number {
  const normalized = (text ?? '').replace(/\./g, '').replace(',', '.').replace(/[^\d.-]/g, '');
  return normalized === '' || normalized === '-' ? NaN : Number(normalized);
}

/** Ô nhập số có phân cách hàng nghìn kiểu Việt Nam (1.000.000). */
export const NumberInput = forwardRef<ComponentRef<typeof InputNumber>, NumberInputProps>(function NumberInput(
  { label, hint, error, required, labelExtra, fieldClassName, currency = false, unit, id, status, min, precision, className, ...rest },
  ref,
) {
  const autoId = useId();
  const inputId = id ?? autoId;
  const suffixText = currency ? '₫' : unit;
  const finalPrecision = currency ? 0 : precision;

  const control = (
    <InputNumber<number>
      ref={ref}
      id={inputId}
      status={error ? 'error' : status}
      min={currency ? (min ?? 0) : min}
      precision={finalPrecision}
      formatter={(v) => formatVi(v)}
      parser={(t) => parseVi(t)}
      suffix={suffixText ? <span className="text-ink-muted">{suffixText}</span> : undefined}
      inputMode={finalPrecision === 0 ? 'numeric' : 'decimal'}
      aria-required={required || undefined}
      aria-invalid={error ? true : undefined}
      aria-describedby={error || hint ? `${inputId}-message` : undefined}
      className={cn('tabular w-full', className)}
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
});
