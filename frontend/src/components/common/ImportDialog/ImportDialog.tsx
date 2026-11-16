import { useMemo, useState, type ReactNode } from 'react';
import { Modal, Segmented, Upload } from 'antd';
import { CheckCircle2, CircleAlert, Copy, Download, FileSpreadsheet, RotateCcw, Upload as UploadIcon } from 'lucide-react';
import { buildXlsx, downloadBlob, readSpreadsheet, type Sheet } from '@/lib/spreadsheet';
import { cn } from '@/lib/cn';
import { normalizeText } from '@/lib/text';
import { useBreakpoint } from '@/hooks/useBreakpoint';
import { Button } from '../Button/Button';
import { DataTable, type DataColumn } from '../DataTable/DataTable';
import { StatusTag } from '../StatusTag/StatusTag';

/**
 * NHẬP DỮ LIỆU TỪ EXCEL — dùng chung cho lead, học viên, câu hỏi...
 *  1. Chọn file (.xlsx / .csv) — có nút tải file mẫu đúng cột
 *  2. Xem trước: mỗi dòng được kiểm tra → Hợp lệ / Lỗi (nêu rõ ô sai) / Trùng (đã có trong hệ thống hoặc lặp trong file)
 *  3. Chỉ nhập các dòng hợp lệ; tải được file các dòng lỗi (kèm cột "Lỗi") để sửa rồi nhập lại
 * Trang dùng chỉ cần khai báo cột + hàm `check` (kiểm tra cả file một lượt để bắt trùng trong file) + `onImport`.
 */

export interface ImportColumn {
  key: string;
  /** Tên cột trong file mẫu */
  label: string;
  required?: boolean;
  /** Tên cột khác vẫn nhận (không phân biệt dấu / hoa thường): "SĐT", "Số điện thoại", "Phone" */
  aliases?: string[];
  /** Ví dụ ghi vào dòng 2 của file mẫu */
  example?: string;
  /** Giải thích giá trị hợp lệ (hiện ở bước chọn file) */
  hint?: string;
  /** Độ rộng cột ở file mẫu (ký tự) */
  width?: number;
}

export type ImportRow = Record<string, string>;

export interface RowCheck<T> {
  /** Có giá trị + không lỗi + không trùng → được nhập */
  value?: T;
  errors: string[];
  /** Mô tả bản ghi bị trùng: "Trùng SĐT với lead LD0012 — Nguyễn Văn A" */
  duplicate?: string;
  /** Cảnh báo nhưng vẫn nhập */
  warnings?: string[];
}

export interface ImportDialogProps<T> {
  open: boolean;
  onClose: () => void;
  /** "Nhập lead từ Excel" */
  title: string;
  /** "lead", "học viên", "câu hỏi" */
  unitLabel: string;
  /** Tên file mẫu (không đuôi) */
  templateName: string;
  columns: ImportColumn[];
  check: (rows: ImportRow[]) => RowCheck<T>[];
  /** Trả về số bản ghi đã tạo (mặc định = số dòng hợp lệ) */
  onImport: (values: T[]) => void | number | Promise<void | number>;
  /** Thiết lập thêm ở bước chọn file (vd: chi nhánh mặc định) */
  settings?: ReactNode;
  /** Ghi chú hướng dẫn thêm */
  notes?: ReactNode;
  /** Cột hiển thị ở bảng xem trước (mặc định 4 cột đầu) */
  previewKeys?: string[];
}

type Status = 'ok' | 'error' | 'duplicate';
interface PreviewRow<T> {
  line: number;
  raw: ImportRow;
  status: Status;
  check: RowCheck<T>;
}

const MAX_ROWS = 5000;
const headerKey = (s: string) => normalizeText(s.replace(/\*|\(.*?\)/g, '')).replace(/[^a-z0-9]/g, '');

/** Ghép tiêu đề trong file với cột khai báo. Trả về vị trí cột trong file cho từng key. */
function mapHeader(header: string[], columns: ImportColumn[]) {
  const pos: Record<string, number> = {};
  const names = header.map(headerKey);
  for (const c of columns) {
    const accepted = [c.label, ...(c.aliases ?? [])].map(headerKey);
    const i = names.findIndex((n) => n && accepted.includes(n));
    if (i >= 0) pos[c.key] = i;
  }
  return pos;
}

export function ImportDialog<T>({ open, onClose, title, unitLabel, templateName, columns, check, onImport, settings, notes, previewKeys }: ImportDialogProps<T>) {
  const { md } = useBreakpoint();
  const [fileName, setFileName] = useState<string>();
  const [rows, setRows] = useState<{ line: number; raw: ImportRow }[] | null>(null);
  const [fileError, setFileError] = useState<string>();
  const [reading, setReading] = useState(false);
  const [filter, setFilter] = useState<'all' | Status>('all');
  const [importing, setImporting] = useState(false);
  const [done, setDone] = useState<{ created: number; skipped: number } | null>(null);

  const reset = () => {
    setFileName(undefined);
    setRows(null);
    setFileError(undefined);
    setFilter('all');
    setDone(null);
  };
  const close = () => {
    reset();
    onClose();
  };

  const load = async (file: File) => {
    setReading(true);
    setFileError(undefined);
    try {
      const sheet: Sheet = await readSpreadsheet(file);
      const start = sheet.findIndex((r) => r.some((c) => c.trim()));
      if (start < 0) throw new Error('File trống.');
      const pos = mapHeader(sheet[start], columns);
      const missing = columns.filter((c) => c.required && pos[c.key] === undefined).map((c) => `"${c.label}"`);
      if (missing.length) throw new Error(`File thiếu cột bắt buộc: ${missing.join(', ')}. Dòng đầu tiên phải là tên cột — tải file mẫu để xem đúng tên.`);
      const data = sheet
        .slice(start + 1)
        .map((r, i) => ({ line: start + i + 2, raw: Object.fromEntries(columns.map((c) => [c.key, (pos[c.key] === undefined ? '' : (r[pos[c.key]] ?? '')).trim()])) }))
        .filter((r) => Object.values(r.raw).some(Boolean));
      if (!data.length) throw new Error('File chỉ có dòng tiêu đề, chưa có dữ liệu.');
      if (data.length > MAX_ROWS) throw new Error(`File có ${data.length} dòng — mỗi lần nhập tối đa ${MAX_ROWS} dòng. Hãy tách file.`);
      setFileName(file.name);
      setRows(data);
    } catch (e) {
      setFileError(e instanceof Error ? e.message : 'Không đọc được file.');
    } finally {
      setReading(false);
    }
  };

  const preview: PreviewRow<T>[] = useMemo(() => {
    if (!rows) return [];
    const results = check(rows.map((r) => r.raw));
    return rows.map((r, i) => {
      const c = results[i] ?? { errors: ['Không kiểm tra được dòng này'] };
      const status: Status = c.errors.length ? 'error' : c.duplicate ? 'duplicate' : 'ok';
      return { ...r, check: c, status };
    });
  }, [rows, check]);

  const count = { ok: 0, error: 0, duplicate: 0 } as Record<Status, number>;
  preview.forEach((p) => count[p.status]++);
  const shown = filter === 'all' ? preview : preview.filter((p) => p.status === filter);

  const downloadTemplate = () =>
    downloadBlob(
      buildXlsx([columns.map((c) => (c.required ? `${c.label} *` : c.label)), columns.map((c) => c.example ?? '')], { sheetName: unitLabel, widths: columns.map((c) => c.width ?? Math.max(14, c.label.length + 4)) }),
      `${templateName}.xlsx`,
    );

  const downloadErrors = () => {
    const bad = preview.filter((p) => p.status !== 'ok');
    downloadBlob(
      buildXlsx([[...columns.map((c) => c.label), 'Dòng gốc', 'Lỗi'], ...bad.map((p) => [...columns.map((c) => p.raw[c.key] ?? ''), String(p.line), p.status === 'error' ? p.check.errors.join('; ') : (p.check.duplicate ?? 'Trùng')])], {
        sheetName: 'Dòng lỗi',
        widths: [...columns.map((c) => c.width ?? Math.max(14, c.label.length + 4)), 10, 50],
      }),
      `${templateName}-dong-loi.xlsx`,
    );
  };

  const submit = async () => {
    const values = preview.filter((p) => p.status === 'ok').map((p) => p.check.value as T);
    if (!values.length) return;
    setImporting(true);
    try {
      const created = await onImport(values);
      setDone({ created: typeof created === 'number' ? created : values.length, skipped: preview.length - values.length });
    } finally {
      setImporting(false);
    }
  };

  const keys = previewKeys ?? columns.slice(0, 4).map((c) => c.key);
  const tableColumns: DataColumn<PreviewRow<T>>[] = [
    { key: 'line', title: 'Dòng', width: 76, mobile: 'aside', render: (_, r) => <span className="tabular text-ink-muted">#{r.line}</span> },
    ...keys.map((k, i): DataColumn<PreviewRow<T>> => {
      const col = columns.find((c) => c.key === k)!;
      return { key: k, title: col.label, width: i === 0 ? 180 : 140, mobile: i === 0 ? 'title' : i === 1 ? 'subtitle' : 'field', render: (_, r) => r.raw[k] || <span className="text-ink-muted">—</span> };
    }),
    {
      key: 'status',
      title: 'Kết quả kiểm tra',
      width: 300,
      ellipsis: false,
      render: (_, r) => (
        <div className="flex flex-col items-start gap-1 py-0.5">
          {r.status === 'ok' ? (
            <StatusTag tone="success" icon={CheckCircle2} size="sm">Hợp lệ</StatusTag>
          ) : r.status === 'duplicate' ? (
            <StatusTag tone="warning" icon={Copy} size="sm">Trùng — bỏ qua</StatusTag>
          ) : (
            <StatusTag tone="danger" icon={CircleAlert} size="sm">Lỗi</StatusTag>
          )}
          {r.status === 'error' && <span className="text-xs text-danger-700">{r.check.errors.join(' · ')}</span>}
          {r.status === 'duplicate' && <span className="text-xs text-warning-700">{r.check.duplicate}</span>}
          {r.check.warnings?.length ? <span className="text-xs text-ink-muted">{r.check.warnings.join(' · ')}</span> : null}
        </div>
      ),
    },
  ];

  const step = done ? 'done' : rows ? 'preview' : 'pick';

  return (
    <Modal
      open={open}
      onCancel={close}
      title={title}
      width={step === 'preview' ? 980 : 640}
      destroyOnHidden
      maskClosable={!rows}
      footer={
        step === 'pick' ? (
          <div className="flex justify-between gap-2">
            <Button icon={Download} variant="outline" onClick={downloadTemplate}>Tải file mẫu</Button>
            <Button action="cancel" variant="ghost" onClick={close} />
          </div>
        ) : step === 'preview' ? (
          <div className="flex flex-wrap justify-end gap-2">
            <Button icon={RotateCcw} variant="ghost" className="mr-auto" onClick={reset}>Chọn file khác</Button>
            {count.error + count.duplicate > 0 && (
              <Button icon={Download} variant="outline" onClick={downloadErrors}>Tải {count.error + count.duplicate} dòng lỗi / trùng</Button>
            )}
            <Button icon={UploadIcon} loading={importing} disabled={!count.ok} onClick={submit}>
              Nhập {count.ok} {unitLabel}
            </Button>
          </div>
        ) : (
          <Button icon={CheckCircle2} onClick={close}>Xong</Button>
        )
      }
    >
      {step === 'pick' && (
        <div className="flex flex-col gap-4">
          {settings}
          <Upload.Dragger
            accept=".xlsx,.csv"
            multiple={false}
            showUploadList={false}
            disabled={reading}
            beforeUpload={(file) => {
              void load(file);
              return false;
            }}
          >
            <div className="flex flex-col items-center gap-2 py-4">
              <FileSpreadsheet size={36} className="text-success-600" aria-hidden />
              <p className="font-medium text-ink">{reading ? 'Đang đọc file...' : 'Kéo thả file vào đây hoặc bấm để chọn'}</p>
              <p className="text-xs text-ink-muted">Excel (.xlsx) hoặc .csv · tối đa {MAX_ROWS.toLocaleString('vi-VN')} dòng · dòng đầu là tên cột</p>
            </div>
          </Upload.Dragger>
          {fileError && <p className="rounded-lg bg-danger-50 px-3 py-2 text-[13px] text-danger-700">{fileError}</p>}
          <div className="rounded-lg bg-page px-3 py-2.5 text-[13px]">
            <p className="mb-1.5 font-medium text-ink">Các cột trong file</p>
            <ul className="grid grid-cols-1 gap-x-4 gap-y-1 sm:grid-cols-2">
              {columns.map((c) => (
                <li key={c.key} className="text-ink-secondary">
                  <span className="font-medium text-ink">{c.label}</span>
                  {c.required && <span className="text-danger-600"> *</span>}
                  {c.hint && <span className="text-ink-muted"> — {c.hint}</span>}
                </li>
              ))}
            </ul>
            {notes && <div className="mt-2 border-t border-line pt-2 text-ink-muted">{notes}</div>}
          </div>
        </div>
      )}

      {step === 'preview' && (
        <div className="flex flex-col gap-3">
          <div className="flex flex-wrap items-center gap-2 text-[13px]">
            <FileSpreadsheet size={16} className="text-success-600" aria-hidden />
            <span className="font-medium text-ink">{fileName}</span>
            <span className="text-ink-muted">· {preview.length} dòng dữ liệu</span>
          </div>
          <div className="grid grid-cols-3 gap-2">
            {(
              [
                ['ok', 'Hợp lệ — sẽ nhập', 'text-success-700'],
                ['error', 'Lỗi — bỏ qua', 'text-danger-600'],
                ['duplicate', 'Trùng — bỏ qua', 'text-warning-700'],
              ] as const
            ).map(([k, label, cls]) => (
              <div key={k} className="rounded-lg bg-page px-3 py-2">
                <p className="text-xs text-ink-muted">{label}</p>
                <p className={cn('tabular text-lg font-semibold', count[k] ? cls : 'text-ink-muted')}>{count[k]}</p>
              </div>
            ))}
          </div>
          <Segmented
            size={md ? 'middle' : 'small'}
            value={filter}
            onChange={(v) => setFilter(v as typeof filter)}
            options={[
              { value: 'all', label: `Tất cả (${preview.length})` },
              { value: 'error', label: `Lỗi (${count.error})` },
              { value: 'duplicate', label: `Trùng (${count.duplicate})` },
              { value: 'ok', label: `Hợp lệ (${count.ok})` },
            ]}
          />
          <DataTable<PreviewRow<T>> columns={tableColumns} data={shown} rowKey="line" unitLabel="dòng" defaultPageSize={10} resizable={false} empty={{ title: 'Không có dòng nào', description: 'Chọn bộ lọc khác.' }} />
        </div>
      )}

      {step === 'done' && done && (
        <div className="flex flex-col items-center gap-2 py-6 text-center">
          <CheckCircle2 size={44} className="text-success-600" aria-hidden />
          <p className="text-lg font-semibold text-ink">
            Đã nhập {done.created} {unitLabel}
          </p>
          {done.skipped > 0 && (
            <>
              <p className="text-[13px] text-ink-muted">Bỏ qua {done.skipped} dòng lỗi / trùng — tải về, sửa rồi nhập lại.</p>
              <Button icon={Download} variant="outline" onClick={downloadErrors}>Tải {done.skipped} dòng lỗi / trùng</Button>
            </>
          )}
        </div>
      )}
    </Modal>
  );
}
