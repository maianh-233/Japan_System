import { forwardRef, useId } from 'react';
import { Input } from 'antd';
import type { TextAreaProps as AntTextAreaProps, TextAreaRef } from 'antd/es/input/TextArea';
import { FormField, hasFieldChrome, type FieldProps } from '../FormField/FormField';

export interface TextAreaProps extends AntTextAreaProps, FieldProps {
  /** Số dòng tối thiểu / tối đa khi tự giãn */
  minRows?: number;
  maxRows?: number;
}

/** Ô nhập văn bản dài: tự giãn chiều cao, tự đếm ký tự khi có maxLength. */
export const TextArea = forwardRef<TextAreaRef, TextAreaProps>(function TextArea(
  {
    label,
    hint,
    error,
    required,
    labelExtra,
    fieldClassName,
    minRows = 3,
    maxRows = 10,
    id,
    status,
    maxLength,
    showCount,
    autoSize,
    ...rest
  },
  ref,
) {
  const autoId = useId();
  const inputId = id ?? autoId;

  const control = (
    <Input.TextArea
      ref={ref}
      id={inputId}
      status={error ? 'error' : status}
      maxLength={maxLength}
      showCount={showCount ?? Boolean(maxLength)}
      autoSize={autoSize ?? { minRows, maxRows }}
      aria-required={required || undefined}
      aria-invalid={error ? true : undefined}
      aria-describedby={error || hint ? `${inputId}-message` : undefined}
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
