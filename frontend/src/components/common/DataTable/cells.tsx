import { useRef, useState, type KeyboardEvent, type PointerEvent, type ReactNode, type ThHTMLAttributes } from 'react';
import { Tooltip } from 'antd';
import { cn } from '@/lib/cn';

/**
 * Nội dung ô một dòng: quá dài → "...". Chỉ khi BỊ CẮT mới hiện tooltip nội dung đầy đủ.
 * Nới rộng cột → hiện thêm chữ tương ứng.
 */
export function EllipsisCell({ children }: { children: ReactNode }) {
  const ref = useRef<HTMLDivElement>(null);
  const [overflow, setOverflow] = useState(false);
  const check = () => {
    const el = ref.current;
    if (el) setOverflow(el.scrollWidth > el.clientWidth + 1);
  };
  return (
    <Tooltip title={overflow ? children : undefined} placement="topLeft" mouseEnterDelay={0.35}>
      <div ref={ref} onMouseEnter={check} onFocus={check} className="truncate">
        {children}
      </div>
    </Tooltip>
  );
}

export interface ResizableHeaderProps extends ThHTMLAttributes<HTMLTableCellElement> {
  /** Có → hiện tay nắm kéo đổi độ rộng */
  onResize?: (width: number) => void;
  onResizeReset?: () => void;
  resizeWidth?: number;
  minWidth?: number;
  maxWidth?: number;
}

/**
 * Ô tiêu đề có tay nắm ở mép phải:
 *  - Kéo chuột / chạm để đổi độ rộng
 *  - Bàn phím: focus tay nắm rồi ← → (Shift = bước lớn)
 *  - Nhấp đúp: về độ rộng mặc định
 */
export function ResizableHeaderCell({
  onResize,
  onResizeReset,
  resizeWidth,
  minWidth = 80,
  maxWidth = 800,
  children,
  className,
  style,
  ...rest
}: ResizableHeaderProps) {
  const [dragging, setDragging] = useState(false);
  if (!onResize || resizeWidth === undefined) {
    return (
      <th className={className} style={style} {...rest}>
        {children}
      </th>
    );
  }

  const clamp = (w: number) => Math.min(maxWidth, Math.max(minWidth, Math.round(w)));

  const onPointerDown = (e: PointerEvent<HTMLSpanElement>) => {
    e.preventDefault();
    e.stopPropagation();
    const startX = e.clientX;
    const startWidth = resizeWidth;
    setDragging(true);
    document.body.style.cursor = 'col-resize';
    document.body.style.userSelect = 'none';

    const move = (ev: globalThis.PointerEvent) => onResize(clamp(startWidth + ev.clientX - startX));
    const up = () => {
      setDragging(false);
      document.body.style.cursor = '';
      document.body.style.userSelect = '';
      window.removeEventListener('pointermove', move);
      window.removeEventListener('pointerup', up);
    };
    window.addEventListener('pointermove', move);
    window.addEventListener('pointerup', up);
  };

  const onKeyDown = (e: KeyboardEvent<HTMLSpanElement>) => {
    const step = e.shiftKey ? 64 : 16;
    if (e.key === 'ArrowRight') onResize(clamp(resizeWidth + step));
    else if (e.key === 'ArrowLeft') onResize(clamp(resizeWidth - step));
    else return;
    e.preventDefault();
    e.stopPropagation();
  };

  return (
    <th className={cn(className, 'group/th')} style={{ ...style, position: 'relative' }} {...rest}>
      {children}
      <span
        role="separator"
        aria-orientation="vertical"
        aria-label="Kéo để đổi độ rộng cột (nhấp đúp để đặt lại)"
        aria-valuenow={resizeWidth}
        aria-valuemin={minWidth}
        aria-valuemax={maxWidth}
        tabIndex={0}
        onPointerDown={onPointerDown}
        onKeyDown={onKeyDown}
        onClick={(e) => e.stopPropagation()}
        onDoubleClick={(e) => {
          e.stopPropagation();
          onResizeReset?.();
        }}
        className={cn(
          'group/handle absolute top-0 -right-1 z-[2] flex h-full w-3 cursor-col-resize touch-none justify-center',
          'focus-visible:outline-none',
        )}
      >
        <span
          className={cn(
            'my-2 w-0.5 rounded-full transition-colors',
            dragging
              ? 'bg-primary-600'
              : 'bg-transparent group-hover/th:bg-line-strong group-hover/handle:bg-primary-500 group-focus-visible/handle:bg-primary-600',
          )}
        />
      </span>
    </th>
  );
}
