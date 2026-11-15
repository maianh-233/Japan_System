import { forwardRef, useId } from 'react';
import { Input, type InputProps, type InputRef } from 'antd';
import type { LucideIcon } from 'lucide-react';
import { FormField, hasFieldChrome, type FieldProps } from '../FormField/FormField';

export interface TextInputProps extends Omit<InputProps, 'prefix' | 'type'>, FieldProps {
  /** Icon lucide hiển thị đầu ô nhập */
  icon?: LucideIcon;
  type?: 'text' | 'email' | 'tel' | 'password' | 'url';
}

/**
 * Ô nhập văn bản một dòng.
 * Dùng độc lập (truyền label/error) hoặc bên trong <Form.Item> (không truyền label).
 */
export const TextInput = forwardRef<InputRef, TextInputProps>(function TextInput(
  { label, hint, error, required, labelExtra, fieldClassName, icon: Icon, type = 'text', id, status, allowClear = true, ...rest },
  ref,
) {
  const autoId = useId();
  const inputId = id ?? autoId;
  const common = {
    ref,
    id: inputId,
    status: error ? ('error' as const) : status,
    prefix: Icon ? <Icon size={16} className="text-ink-placeholder" aria-hidden /> : undefined,
    'aria-required': required || undefined,
    'aria-invalid': error ? true : undefined,
    'aria-describedby': error || hint ? `${inputId}-message` : undefined,
    ...rest,
  };

  const control =
    type === 'password' ? (
      <Input.Password {...common} />
    ) : (
      <Input
        {...common}
        type={type}
        allowClear={allowClear}
        inputMode={type === 'tel' ? 'tel' : type === 'email' ? 'email' : undefined}
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
