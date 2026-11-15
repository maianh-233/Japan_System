/** Bỏ dấu tiếng Việt + chữ thường: "Nguyễn Đức" → "nguyen duc". Dùng cho tìm kiếm không phân biệt dấu. */
export function normalizeText(input: unknown): string {
  return String(input ?? '')
    .normalize('NFD')
    .replace(/[̀-ͯ]/g, '')
    .replace(/đ/g, 'd')
    .replace(/Đ/g, 'D')
    .toLowerCase()
    .trim();
}

/** `haystack` có chứa `needle` không (không phân biệt dấu / hoa thường) */
export function includesText(haystack: unknown, needle: string): boolean {
  return normalizeText(haystack).includes(normalizeText(needle));
}

/** Chữ cái viết tắt từ họ tên: "Nguyễn Văn An" → "NA", "Tanaka" → "TA" */
export function getInitials(name: string | undefined): string {
  const words = (name ?? '').trim().split(/\s+/).filter(Boolean);
  if (words.length === 0) return '?';
  if (words.length === 1) return words[0].slice(0, 2).toUpperCase();
  return (words[0][0] + words[words.length - 1][0]).toUpperCase();
}

/** Băm chuỗi thành số ổn định (chọn màu avatar theo tên) */
export function hashString(input: string): number {
  let hash = 0;
  for (let i = 0; i < input.length; i++) hash = (hash * 31 + input.charCodeAt(i)) | 0;
  return Math.abs(hash);
}
