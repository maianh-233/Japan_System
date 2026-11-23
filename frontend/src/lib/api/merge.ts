/**
 * Ghép bản server vào danh sách trong store: bỏ bản cũ cùng khóa rồi thêm bản mới.
 * `items` có thể trùng khóa (vd cùng 1 đơn ở cả tab "chờ tôi duyệt" lẫn "tất cả") → giữ bản xuất hiện SAU.
 */
export function replaceBy<T>(list: readonly T[], items: readonly T[], key: (x: T) => string): T[] {
  const fresh = new Map<string, T>();
  for (const item of items) fresh.set(key(item), item);
  return [...list.filter((x) => !fresh.has(key(x))), ...fresh.values()];
}
