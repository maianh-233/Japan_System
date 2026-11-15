import { useEffect, useMemo, useState, type HTMLAttributes, type Key, type ReactNode } from 'react';
import { Checkbox, Table, type TableColumnType } from 'antd';
import type { SorterResult } from 'antd/es/table/interface';
import { cn } from '@/lib/cn';
import { includesText } from '@/lib/text';
import { useBreakpoint } from '@/hooks/useBreakpoint';
import { EmptyState, ErrorState, NoResults, type EmptyStateProps } from '../Feedback/EmptyState';
import { LoadingSkeleton } from '../Feedback/LoadingSkeleton';
import { Pagination } from '../Pagination/Pagination';
import { SearchBar, type SearchBarProps } from '../SearchBar/SearchBar';
import { EllipsisCell, ResizableHeaderCell } from './cells';

/** Vai trò của cột khi hiển thị dạng THẺ trên mobile */
export type MobileRole = 'title' | 'subtitle' | 'aside' | 'field' | 'hidden';

export type DataColumn<T> = Omit<TableColumnType<T>, 'key' | 'sorter' | 'width' | 'ellipsis'> & {
  key: string;
  title: ReactNode;
  /** Mặc định: cột đầu = title, còn lại = field */
  mobile?: MobileRole;
  /** true = backend sắp xếp; hàm = sắp xếp tại client */
  sorter?: boolean | ((a: T, b: T) => number);
  /** Độ rộng ban đầu (px). Mặc định 160. */
  width?: number;
  minWidth?: number;
  maxWidth?: number;
  /** Cho kéo đổi độ rộng (mặc định true) */
  resizable?: boolean;
  /** Nội dung dài → "..." + tooltip khi bị cắt (mặc định true). false = xuống dòng. */
  ellipsis?: boolean;
};

const DEFAULT_COL_WIDTH = 160;

function loadWidths(storageKey?: string): Record<string, number> {
  if (!storageKey) return {};
  try {
    return JSON.parse(localStorage.getItem(`table-widths:${storageKey}`) ?? '{}') as Record<string, number>;
  } catch {
    return {};
  }
}

function saveWidths(storageKey: string | undefined, widths: Record<string, number>) {
  if (!storageKey) return;
  try {
    localStorage.setItem(`table-widths:${storageKey}`, JSON.stringify(widths));
  } catch {
    /* trình duyệt chặn lưu trữ — bỏ qua */
  }
}

export type SortState = { key: string; order: 'ascend' | 'descend' } | null;

export interface ServerPagination {
  page: number;
  pageSize: number;
  total: number;
  onChange: (page: number, pageSize: number) => void;
}

export interface DataTableProps<T extends object> {
  columns: DataColumn<T>[];
  data: T[] | undefined;
  rowKey: keyof T | ((record: T) => Key);
  loading?: boolean;
  /** Có lỗi → hiện ErrorState với nút thử lại */
  error?: unknown;
  onRetry?: () => void;

  /**
   * Thanh tìm kiếm phía trên.
   * - Có `onSearch` → tìm ở backend
   * - Có `searchKeys` (không có onSearch) → lọc ngay tại client, không phân biệt dấu
   */
  search?: Partial<Omit<SearchBarProps, 'onSearch'>> & { onSearch?: (keyword: string) => void };
  searchKeys?: (keyof T)[];

  /** Bỏ trống → phân trang tại client. Truyền object → phân trang backend. false → tắt. */
  pagination?: ServerPagination | false;
  defaultPageSize?: number;
  /** Sắp xếp phía backend (khi cột có sorter: true) */
  onSortChange?: (sort: SortState) => void;

  /** Nút thao tác mỗi dòng (Xem/Sửa/Xóa) — cột cuối trên desktop, chân thẻ trên mobile */
  rowActions?: (record: T) => ReactNode;
  onRowClick?: (record: T) => void;

  /** Chọn nhiều dòng */
  selectedKeys?: Key[];
  onSelectionChange?: (keys: Key[], rows: T[]) => void;
  /** Thanh thao tác hàng loạt — hiện khi có dòng được chọn */
  bulkActions?: (keys: Key[]) => ReactNode;

  empty?: EmptyStateProps;
  /** "học viên", "lead"... dùng trong câu "Hiển thị 1–10 / 120 học viên" */
  unitLabel?: string;
  /** Tự vẽ thẻ mobile thay cho thẻ mặc định */
  renderMobileCard?: (record: T) => ReactNode;
  /** Cho kéo đổi độ rộng cột (mặc định true) */
  resizable?: boolean;
  /** Có → nhớ độ rộng cột người dùng đã chỉnh (lưu trên trình duyệt), vd "students" */
  storageKey?: string;
  /** Độ rộng cột Thao tác */
  actionsWidth?: number;
  className?: string;
}

function getValue<T>(record: T, dataIndex: TableColumnType<T>['dataIndex']): unknown {
  if (dataIndex === undefined || dataIndex === null) return undefined;
  const path = Array.isArray(dataIndex) ? dataIndex : [dataIndex];
  return path.reduce<unknown>((acc, k) => (acc == null ? acc : (acc as Record<string, unknown>)[k as string]), record);
}

function renderCell<T>(col: DataColumn<T>, record: T, index: number): ReactNode {
  const value = getValue(record, col.dataIndex);
  if (col.render) return col.render(value, record, index) as ReactNode;
  return value === null || value === undefined || value === '' ? '—' : (value as ReactNode);
}

/**
 * Bảng dữ liệu chuẩn của hệ thống.
 * Desktop/tablet (≥ 768px): bảng AntD. Mobile: danh sách thẻ.
 * Có sẵn: tìm kiếm, sắp xếp, phân trang, chọn nhiều, thao tác dòng, trạng thái tải/rỗng/lỗi.
 */
export function DataTable<T extends object>({
  columns,
  data,
  rowKey,
  loading = false,
  error,
  onRetry,
  search,
  searchKeys,
  pagination,
  defaultPageSize = 10,
  onSortChange,
  rowActions,
  onRowClick,
  selectedKeys,
  onSelectionChange,
  bulkActions,
  empty,
  unitLabel,
  renderMobileCard,
  resizable = true,
  storageKey,
  actionsWidth = 132,
  className,
}: DataTableProps<T>) {
  const { md } = useBreakpoint();
  const [widths, setWidths] = useState<Record<string, number>>(() => loadWidths(storageKey));
  const widthOf = (c: DataColumn<T>) => widths[c.key] ?? c.width ?? DEFAULT_COL_WIDTH;

  // lưu độ rộng sau khi ngừng kéo 300ms
  useEffect(() => {
    const t = setTimeout(() => saveWidths(storageKey, widths), 300);
    return () => clearTimeout(t);
  }, [widths, storageKey]);

  const [keyword, setKeyword] = useState('');
  const [sort, setSort] = useState<SortState>(null);
  const [page, setPage] = useState(1);
  const [pageSize, setPageSize] = useState(defaultPageSize);

  const serverPaging = pagination !== undefined && pagination !== false ? pagination : null;
  const clientSearch = Boolean(searchKeys?.length) && !search?.onSearch;
  const getKey = (r: T): Key => (typeof rowKey === 'function' ? rowKey(r) : (r[rowKey] as Key));

  // 1) lọc + sắp xếp tại client (nếu dùng chế độ client)
  const processed = useMemo(() => {
    let rows = data ?? [];
    if (clientSearch && keyword) {
      rows = rows.filter((r) => searchKeys!.some((k) => includesText(r[k], keyword)));
    }
    if (sort) {
      const col = columns.find((c) => c.key === sort.key);
      if (col && typeof col.sorter === 'function') {
        const fn = col.sorter;
        rows = [...rows].sort((a, b) => (sort.order === 'ascend' ? fn(a, b) : fn(b, a)));
      }
    }
    return rows;
  }, [data, clientSearch, keyword, searchKeys, sort, columns]);

  // 2) phân trang
  const total = serverPaging ? serverPaging.total : processed.length;
  const curPage = serverPaging ? serverPaging.page : page;
  const curSize = serverPaging ? serverPaging.pageSize : pageSize;
  const rows =
    serverPaging || pagination === false ? processed : processed.slice((curPage - 1) * curSize, curPage * curSize);

  const changePage = (p: number, s: number) => {
    const next = s !== curSize ? 1 : p;
    if (serverPaging) serverPaging.onChange(next, s);
    else {
      setPage(next);
      setPageSize(s);
    }
  };

  const handleSearch = (k: string) => {
    setKeyword(k);
    if (!serverPaging) setPage(1);
    search?.onSearch?.(k);
  };

  const selectable = Boolean(onSelectionChange);
  const selected = selectedKeys ?? [];
  const toggleRow = (r: T, checked: boolean) => {
    const k = getKey(r);
    const keys = checked ? [...selected, k] : selected.filter((x) => x !== k);
    onSelectionChange?.(keys, (data ?? []).filter((d) => keys.includes(getKey(d))));
  };

  // ---------- trạng thái đặc biệt ----------
  const isEmpty = !loading && !error && rows.length === 0;
  const hasQuery = Boolean(keyword) || (search?.activeFilterCount ?? 0) > 0;

  const searchBar = search ? (
    <SearchBar {...search} value={search.value ?? keyword} onSearch={handleSearch} className="mb-4" />
  ) : null;

  const bulkBar =
    selectable && selected.length > 0 && bulkActions ? (
      <div className="mb-3 flex flex-wrap items-center gap-2 rounded-lg border border-primary-200 bg-primary-50 px-3 py-2">
        <span className="text-[13px] font-medium text-primary-800">Đã chọn {selected.length}</span>
        <div className="ml-auto flex flex-wrap gap-2">{bulkActions(selected)}</div>
      </div>
    ) : null;

  let content: ReactNode;
  if (error) {
    content = <ErrorState onRetry={onRetry} />;
  } else if (loading && !(data && data.length)) {
    content = <LoadingSkeleton variant={md ? 'table' : 'cards'} rows={Math.min(curSize, 6)} />;
  } else if (isEmpty) {
    content = hasQuery ? (
      <NoResults keyword={keyword} onReset={search?.onReset ?? (keyword ? () => handleSearch('') : undefined)} />
    ) : (
      <EmptyState {...empty} />
    );
  } else if (md) {
    content = (
      <Table<T>
        rowKey={(r) => getKey(r)}
        dataSource={rows}
        loading={loading}
        pagination={false}
        tableLayout="fixed"
        components={{ header: { cell: ResizableHeaderCell } }}
        scroll={{
          x:
            columns.reduce((sum, c) => sum + widthOf(c), 0) +
            (rowActions ? actionsWidth : 0) +
            (selectable ? 48 : 0),
        }}
        sticky
        showSorterTooltip={false}
        onRow={onRowClick ? (r) => ({ onClick: () => onRowClick(r), className: 'cursor-pointer' }) : undefined}
        rowSelection={
          selectable
            ? {
                selectedRowKeys: selected,
                onChange: (keys, selRows) => onSelectionChange?.(keys, selRows),
                preserveSelectedRowKeys: true,
              }
            : undefined
        }
        onChange={(_p, _f, sorter) => {
          const s = (Array.isArray(sorter) ? sorter[0] : sorter) as SorterResult<T>;
          const next: SortState = s?.order && s.columnKey ? { key: String(s.columnKey), order: s.order } : null;
          setSort(next);
          onSortChange?.(next);
        }}
        columns={[
          ...columns.map((col): TableColumnType<T> => {
            const { mobile, sorter, width, minWidth, maxWidth, resizable: colResizable, ellipsis, render, ...c } = col;
            void mobile;
            void width;
            const canResize = resizable && colResizable !== false;
            const useEllipsis = ellipsis !== false;
            return {
              ...c,
              width: widthOf(col),
              sorter: Boolean(sorter),
              sortOrder: sort?.key === c.key ? sort.order : null,
              render: (value: unknown, record: T, index: number) => {
                const node = render ? (render(value, record, index) as ReactNode) : renderCell(col, record, index);
                return useEllipsis ? <EllipsisCell>{node}</EllipsisCell> : node;
              },
              onHeaderCell: () =>
                (canResize
                  ? {
                      resizeWidth: widthOf(col),
                      minWidth,
                      maxWidth,
                      onResize: (w: number) => setWidths((prev) => ({ ...prev, [col.key]: w })),
                      onResizeReset: () =>
                        setWidths((prev) => {
                          const next = { ...prev };
                          delete next[col.key];
                          return next;
                        }),
                    }
                  : {}) as HTMLAttributes<HTMLElement>,
            };
          }),
          ...(rowActions
            ? [
                {
                  key: '__actions',
                  title: 'Thao tác',
                  align: 'right' as const,
                  fixed: 'right' as const,
                  width: actionsWidth,
                  render: (_: unknown, r: T) => (
                    <div className="flex justify-end gap-1" onClick={(e) => e.stopPropagation()}>
                      {rowActions(r)}
                    </div>
                  ),
                },
              ]
            : []),
        ]}
        className="overflow-hidden rounded-[var(--radius-card)] border border-line [&_.ant-table-thead>tr>th]:!bg-primary-50 [&_.ant-table-thead>tr>th]:!font-semibold [&_.ant-table-thead>tr>th]:!text-ink-secondary"
      />
    );
  } else {
    // ---------- MOBILE: dạng thẻ ----------
    const roleOf = (c: DataColumn<T>, i: number): MobileRole => c.mobile ?? (i === 0 ? 'title' : 'field');
    const titleCols = columns.filter((c, i) => roleOf(c, i) === 'title');
    const subtitleCols = columns.filter((c, i) => roleOf(c, i) === 'subtitle');
    const asideCols = columns.filter((c, i) => roleOf(c, i) === 'aside');
    const fieldCols = columns.filter((c, i) => roleOf(c, i) === 'field');

    content = (
      <ul className={cn('flex flex-col gap-3', loading && 'opacity-60')} aria-busy={loading || undefined}>
        {rows.map((r, idx) => {
          const k = getKey(r);
          return (
            <li
              key={k}
              className={cn(
                'rounded-[var(--radius-card)] border bg-surface p-4 shadow-[var(--shadow-soft-sm)]',
                selected.includes(k) ? 'border-primary-400 bg-primary-50/40' : 'border-line',
              )}
            >
              {renderMobileCard ? (
                renderMobileCard(r)
              ) : (
                <>
                  <div className="flex items-start gap-3">
                    {selectable && (
                      <Checkbox
                        checked={selected.includes(k)}
                        onChange={(e) => toggleRow(r, e.target.checked)}
                        className="mt-1"
                        aria-label="Chọn dòng"
                      />
                    )}
                    <div
                      className={cn('min-w-0 flex-1', onRowClick && 'cursor-pointer')}
                      onClick={onRowClick ? () => onRowClick(r) : undefined}
                    >
                      {titleCols.map((c) => (
                        <div key={c.key} className="font-medium text-ink">
                          {renderCell(c, r, idx)}
                        </div>
                      ))}
                      {subtitleCols.map((c) => (
                        <div key={c.key} className="text-[13px] text-ink-muted">
                          {renderCell(c, r, idx)}
                        </div>
                      ))}
                    </div>
                    {asideCols.length > 0 && (
                      <div className="flex shrink-0 flex-col items-end gap-1">
                        {asideCols.map((c) => (
                          <div key={c.key}>{renderCell(c, r, idx)}</div>
                        ))}
                      </div>
                    )}
                  </div>
                  {fieldCols.length > 0 && (
                    <dl className="mt-3 grid grid-cols-2 gap-x-4 gap-y-2 border-t border-line pt-3">
                      {fieldCols.map((c) => (
                        <div key={c.key} className="min-w-0">
                          <dt className="text-xs text-ink-muted">{c.title}</dt>
                          <dd className="truncate text-[13px] text-ink">{renderCell(c, r, idx)}</dd>
                        </div>
                      ))}
                    </dl>
                  )}
                  {rowActions && (
                    <div className="mt-3 flex justify-end gap-1 border-t border-line pt-3">{rowActions(r)}</div>
                  )}
                </>
              )}
            </li>
          );
        })}
      </ul>
    );
  }

  return (
    <div className={className}>
      {searchBar}
      {bulkBar}
      {content}
      {pagination !== false && !error && rows.length > 0 && (
        <Pagination page={curPage} pageSize={curSize} total={total} onChange={changePage} unitLabel={unitLabel} />
      )}
    </div>
  );
}
