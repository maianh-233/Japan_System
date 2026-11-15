const numberFormatter = new Intl.NumberFormat('vi-VN');

/** 1500000 → "1.500.000" */
export function formatNumber(value: number | null | undefined): string {
  if (value === null || value === undefined || Number.isNaN(value)) return '';
  return numberFormatter.format(value);
}

/** "1.500.000" → 1500000 (giữ dấu thập phân kiểu vi-VN là dấu phẩy) */
export function parseNumber(text: string | undefined): number {
  if (!text) return NaN;
  const normalized = text.replace(/[^\d,-]/g, '').replace(',', '.');
  return normalized === '' || normalized === '-' ? NaN : Number(normalized);
}

/** 1500000 → "1.500.000 ₫" */
export function formatCurrency(value: number | null | undefined): string {
  const text = formatNumber(value);
  return text ? `${text} ₫` : '';
}

/** 2_500_000 bytes → "2,4 MB" */
export function formatFileSize(bytes: number): string {
  if (bytes < 1024) return `${bytes} B`;
  if (bytes < 1024 * 1024) return `${(bytes / 1024).toLocaleString('vi-VN', { maximumFractionDigits: 1 })} KB`;
  return `${(bytes / 1024 / 1024).toLocaleString('vi-VN', { maximumFractionDigits: 1 })} MB`;
}
