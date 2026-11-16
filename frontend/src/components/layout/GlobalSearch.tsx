import { useEffect, useMemo, useRef, useState } from 'react';
import { Modal } from 'antd';
import { CornerDownLeft, Search } from 'lucide-react';
import { useNavigate } from 'react-router-dom';
import { Button } from '@/components/common';
import { SEARCH_GROUPS, useGlobalSearch, type SearchGroup, type SearchResult } from '@/features/search/useGlobalSearch';
import { useBreakpoint } from '@/hooks/useBreakpoint';
import { useDebounce } from '@/hooks/useDebounce';
import { cn } from '@/lib/cn';

const isMac = typeof navigator !== 'undefined' && /Mac|iPhone|iPad/.test(navigator.platform);
const SHORTCUT = isMac ? '⌘K' : 'Ctrl K';

/** Gõ phím tắt khi đang nhập liệu thì không mở (trừ Ctrl/⌘+K) */
const isTyping = (el: EventTarget | null) => el instanceof HTMLElement && (el.isContentEditable || ['INPUT', 'TEXTAREA', 'SELECT'].includes(el.tagName));

/** Tìm kiếm toàn hệ thống — nút trên header + hộp tìm kiếm (Ctrl/⌘+K hoặc phím "/") */
export function GlobalSearch() {
  const [open, setOpen] = useState(false);

  useEffect(() => {
    const onKey = (e: KeyboardEvent) => {
      if ((e.ctrlKey || e.metaKey) && e.key.toLowerCase() === 'k') {
        e.preventDefault();
        setOpen((v) => !v);
      } else if (e.key === '/' && !isTyping(e.target)) {
        e.preventDefault();
        setOpen(true);
      }
    };
    window.addEventListener('keydown', onKey);
    return () => window.removeEventListener('keydown', onKey);
  }, []);

  return (
    <>
      {/* Màn rộng: ô giả dạng input; màn hẹp: nút icon */}
      <button
        type="button"
        onClick={() => setOpen(true)}
        className="hidden h-9 w-56 items-center gap-2 rounded-lg border border-line bg-page px-3 text-left text-[13px] text-ink-muted transition-colors hover:border-primary-300 focus-visible:ring-[3px] focus-visible:ring-primary-500/30 focus-visible:outline-none lg:flex xl:w-72"
        aria-label="Tìm kiếm toàn hệ thống"
      >
        <Search size={16} aria-hidden />
        <span className="flex-1 truncate">Tìm lead, học viên, lớp, hóa đơn...</span>
        <kbd className="rounded border border-line bg-surface px-1.5 py-0.5 font-sans text-[11px] text-ink-muted">{SHORTCUT}</kbd>
      </button>
      <Button icon={Search} iconOnly tone="neutral" variant="ghost" aria-label="Tìm kiếm" className="lg:hidden" onClick={() => setOpen(true)} />
      {open && <SearchDialog onClose={() => setOpen(false)} />}
    </>
  );
}

function SearchDialog({ onClose }: { onClose: () => void }) {
  const navigate = useNavigate();
  const { isMobile } = useBreakpoint();
  const [query, setQuery] = useState('');
  const debounced = useDebounce(query, 120);
  const results = useGlobalSearch(debounced);
  const [active, setActive] = useState(0);
  const listRef = useRef<HTMLUListElement>(null);

  // Gom theo nhóm, giữ thứ tự nhóm cố định; chỉ số phẳng để điều hướng bằng phím
  const grouped = useMemo(() => {
    const order = Object.keys(SEARCH_GROUPS) as SearchGroup[];
    return order.map((g) => ({ group: g, items: results.filter((r) => r.group === g) })).filter((x) => x.items.length);
  }, [results]);
  const flat = grouped.flatMap((g) => g.items);

  useEffect(() => setActive(0), [debounced]);
  useEffect(() => {
    listRef.current?.querySelector(`[data-index="${active}"]`)?.scrollIntoView({ block: 'nearest' });
  }, [active]);

  const go = (r?: SearchResult) => {
    if (!r) return;
    onClose();
    navigate(r.path);
  };

  const onKeyDown = (e: React.KeyboardEvent) => {
    if (e.key === 'ArrowDown') {
      e.preventDefault();
      setActive((i) => (flat.length ? (i + 1) % flat.length : 0));
    } else if (e.key === 'ArrowUp') {
      e.preventDefault();
      setActive((i) => (flat.length ? (i - 1 + flat.length) % flat.length : 0));
    } else if (e.key === 'Enter') {
      e.preventDefault();
      go(flat[active]);
    }
  };

  const typing = query.trim().length >= 2 && query !== debounced;

  return (
    <Modal
      open
      onCancel={onClose}
      footer={null}
      closable={false}
      width={isMobile ? '100%' : 640}
      style={{ top: isMobile ? 0 : 80 }}
      styles={{ content: { padding: 0, overflow: 'hidden' } }}
      destroyOnHidden
      aria-label="Tìm kiếm toàn hệ thống"
    >
      <div className="flex items-center gap-2 border-b border-line px-4">
        <Search size={18} className="shrink-0 text-ink-muted" aria-hidden />
        <input
          autoFocus
          value={query}
          onChange={(e) => setQuery(e.target.value)}
          onKeyDown={onKeyDown}
          placeholder="Gõ tên, số điện thoại, mã lead / học viên / lớp / hóa đơn..."
          className="h-14 min-w-0 flex-1 bg-transparent text-[15px] text-ink outline-none placeholder:text-ink-muted"
          role="combobox"
          aria-expanded={flat.length > 0}
          aria-controls="global-search-results"
          aria-activedescendant={flat[active] ? `gs-${flat[active].key}` : undefined}
        />
        <button type="button" onClick={onClose} className="rounded border border-line px-1.5 py-0.5 text-[11px] text-ink-muted hover:text-ink">
          Esc
        </button>
      </div>

      <div className="max-h-[60vh] overflow-y-auto">
        {query.trim().length < 2 ? (
          <div className="px-4 py-6 text-[13px] text-ink-muted">
            <p className="mb-2 font-medium text-ink-secondary">Tìm nhanh mọi thứ bạn có quyền xem</p>
            <ul className="flex flex-col gap-1">
              <li>• Tên hoặc SĐT: <span className="text-ink">"nguyen van an"</span>, <span className="text-ink">"0912"</span> — không cần gõ dấu</li>
              <li>• Mã: <span className="text-ink">LD0012</span>, <span className="text-ink">HV1003</span>, <span className="text-ink">HD2410</span>, <span className="text-ink">N3-K12</span></li>
              <li>• Tên trang: <span className="text-ink">"công nợ"</span>, <span className="text-ink">"xếp ca"</span></li>
            </ul>
          </div>
        ) : flat.length === 0 ? (
          <p className="px-4 py-8 text-center text-[13px] text-ink-muted">{typing ? 'Đang tìm...' : `Không tìm thấy kết quả cho "${query.trim()}"`}</p>
        ) : (
          <ul ref={listRef} id="global-search-results" role="listbox" className="py-2">
            {grouped.map(({ group, items }) => (
              <li key={group} role="presentation">
                <p className="px-4 pt-2 pb-1 text-[11px] font-semibold tracking-wide text-ink-muted uppercase">{SEARCH_GROUPS[group]}</p>
                <ul role="presentation">
                  {items.map((r) => {
                    const index = flat.indexOf(r);
                    const Icon = r.icon;
                    return (
                      <li
                        key={r.key}
                        id={`gs-${r.key}`}
                        data-index={index}
                        role="option"
                        aria-selected={index === active}
                        onMouseMove={() => setActive(index)}
                        onClick={() => go(r)}
                        className={cn('mx-2 flex cursor-pointer items-center gap-3 rounded-lg px-2 py-2', index === active ? 'bg-primary-50' : 'hover:bg-page')}
                      >
                        <span className={cn('grid size-8 shrink-0 place-items-center rounded-lg', index === active ? 'bg-primary-100 text-primary-700' : 'bg-page text-ink-muted')}>
                          <Icon size={16} aria-hidden />
                        </span>
                        <span className="min-w-0 flex-1 leading-5">
                          <span className="flex items-center gap-2">
                            <span className="truncate text-[14px] font-medium text-ink">{r.title}</span>
                            {r.badge && <span className="shrink-0 rounded bg-page px-1.5 text-[11px] text-ink-muted">{r.badge}</span>}
                          </span>
                          {(r.subtitle || r.branch) && <span className="block truncate text-xs text-ink-muted">{[r.subtitle, r.branch].filter(Boolean).join(' · ')}</span>}
                        </span>
                        {index === active && <CornerDownLeft size={14} className="shrink-0 text-ink-muted" aria-hidden />}
                      </li>
                    );
                  })}
                </ul>
              </li>
            ))}
          </ul>
        )}
      </div>

      <div className="hidden items-center gap-4 border-t border-line bg-page px-4 py-2 text-[11px] text-ink-muted sm:flex">
        <span>↑ ↓ để chọn</span>
        <span>Enter để mở</span>
        <span>Esc để đóng</span>
        <span className="ml-auto">Chỉ hiện dữ liệu trong quyền & chi nhánh của bạn</span>
      </div>
    </Modal>
  );
}
