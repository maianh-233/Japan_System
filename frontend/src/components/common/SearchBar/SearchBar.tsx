import { useEffect, useRef, useState, type ReactNode } from 'react';
import { Badge } from 'antd';
import { Search } from 'lucide-react';
import { cn } from '@/lib/cn';
import { useDebounce } from '@/hooks/useDebounce';
import { Button } from '../Button/Button';
import { TextInput } from '../TextInput/TextInput';

export interface SearchBarProps {
  /** Từ khóa (controlled). Bỏ trống để component tự quản lý. */
  value?: string;
  defaultValue?: string;
  /** Gọi sau khi ngừng gõ `debounceMs` ms, hoặc ngay khi nhấn Enter */
  onSearch: (keyword: string) => void;
  placeholder?: string;
  debounceMs?: number;
  /** Các ô lọc (Select, DatePicker...). Mobile: gom vào nút "Bộ lọc". */
  filters?: ReactNode;
  /** Số bộ lọc đang áp dụng — hiện badge trên nút Bộ lọc */
  activeFilterCount?: number;
  /** Có truyền thì hiện nút "Đặt lại" */
  onReset?: () => void;
  /** Nút hành động bên phải (vd: Thêm mới, Xuất Excel) */
  actions?: ReactNode;
  className?: string;
}

/** Thanh tìm kiếm + bộ lọc + hành động, tự xếp dọc trên mobile. */
export function SearchBar({
  value,
  defaultValue = '',
  onSearch,
  placeholder = 'Tìm kiếm...',
  debounceMs = 400,
  filters,
  activeFilterCount = 0,
  onReset,
  actions,
  className,
}: SearchBarProps) {
  const [text, setText] = useState(value ?? defaultValue);
  const [filtersOpen, setFiltersOpen] = useState(false);
  const debounced = useDebounce(text, debounceMs);
  const lastSent = useRef(text);

  // Đồng bộ khi cha thay đổi value (vd: bấm Đặt lại)
  useEffect(() => {
    if (value !== undefined && value !== text) {
      setText(value);
      lastSent.current = value;
    }
  }, [value]);

  const emit = (keyword: string) => {
    const k = keyword.trim();
    if (k === lastSent.current.trim()) return;
    lastSent.current = k;
    onSearch(k);
  };

  useEffect(() => {
    emit(debounced);
  }, [debounced]);

  const handleReset = () => {
    setText('');
    lastSent.current = '';
    onReset?.();
  };

  return (
    <div
      role="search"
      className={cn(
        'flex flex-col gap-3 rounded-[var(--radius-card)] border border-line bg-surface p-3 shadow-[var(--shadow-soft-sm)] sm:p-4',
        className,
      )}
    >
      <div className="flex flex-col gap-3 lg:flex-row lg:items-center">
        <div className="flex flex-1 gap-2">
          <TextInput
            icon={Search}
            value={text}
            placeholder={placeholder}
            aria-label={placeholder}
            onChange={(e) => setText(e.target.value)}
            onPressEnter={() => emit(text)}
            className="flex-1"
          />
          {filters && (
            <Badge count={activeFilterCount} size="small" color="#1F6FC5" className="lg:hidden">
              <Button
                action="filter"
                iconOnly
                variant={filtersOpen ? 'soft' : 'outline'}
                tone={filtersOpen ? 'primary' : 'neutral'}
                aria-expanded={filtersOpen}
                onClick={() => setFiltersOpen((o) => !o)}
              />
            </Badge>
          )}
        </div>

        {filters && (
          <div
            className={cn(
              'grid-cols-1 gap-3 sm:grid-cols-2 lg:flex lg:flex-wrap lg:items-center',
              // mobile/tablet: ẩn khi đóng · desktop: luôn hiện
              filtersOpen ? 'grid' : 'hidden',
            )}
          >
            {filters}
          </div>
        )}

        {(onReset || actions) && (
          <div className="flex flex-wrap items-center gap-2 lg:ml-auto">
            {onReset && <Button action="reset" onClick={handleReset} />}
            {actions}
          </div>
        )}
      </div>
    </div>
  );
}
