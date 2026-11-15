/**
 * Kiểm tra quyền dạng đơn giản:
 *   "*"          → toàn quyền
 *   "crm.*"      → mọi mục trong module crm
 *   "crm.leads"  → đúng mục đó
 * `required` = "crm" (cả module) → đúng nếu có BẤT KỲ quyền nào trong module.
 */
export function hasPermission(granted: string[], required?: string): boolean {
  if (!required) return true;
  if (granted.includes('*')) return true;
  const [module] = required.split('.');
  return granted.some((g) => {
    if (g === required) return true;
    if (g === `${module}.*`) return true;
    // hỏi cả module: có quyền con nào thuộc module là đủ
    if (!required.includes('.') && g.startsWith(`${module}.`)) return true;
    return false;
  });
}
