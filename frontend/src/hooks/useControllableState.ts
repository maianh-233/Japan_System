import { useCallback, useRef, useState } from 'react';

type Updater<T> = T | ((prev: T) => T);

/**
 * State dùng được cả 2 kiểu: controlled (truyền value) và uncontrolled (defaultValue).
 * setState nhận updater dạng hàm và luôn dựa trên giá trị MỚI NHẤT
 * → an toàn cho cập nhật bất đồng bộ liên tiếp (vd: tiến độ upload).
 */
export function useControllableState<T>(
  value: T | undefined,
  defaultValue: T,
  onChange?: (next: T) => void,
): [T, (updater: Updater<T>) => void] {
  const isControlled = value !== undefined;
  const [internal, setInternal] = useState<T>(defaultValue);
  const current = isControlled ? (value as T) : internal;

  const latest = useRef(current);
  latest.current = current;
  const onChangeRef = useRef(onChange);
  onChangeRef.current = onChange;

  const setState = useCallback(
    (updater: Updater<T>) => {
      const next = typeof updater === 'function' ? (updater as (p: T) => T)(latest.current) : updater;
      latest.current = next;
      if (!isControlled) setInternal(next);
      onChangeRef.current?.(next);
    },
    [isControlled],
  );

  return [current, setState];
}
