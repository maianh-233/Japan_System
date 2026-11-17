/**
 * Chế độ dữ liệu:
 *  - "api"  (mặc định): đăng nhập, phân quyền, duyệt cấp quyền, chi nhánh, nhân viên / giáo viên, khóa học, lộ trình gọi backend thật
 *    (cần chạy API + Docker — xem backend/README.md).
 *  - "mock": toàn bộ chạy bằng dữ liệu giả trong trình duyệt như trước (không cần backend).
 * Đặt trong frontend/.env.local:  VITE_API_MODE=mock
 * Các phần backend CHƯA làm (CRM, Lớp học, Học viên, Tài chính...) luôn dùng dữ liệu giả.
 */
export const USE_API = import.meta.env.VITE_API_MODE !== 'mock';

/** Gốc API — dev đi qua proxy Vite (vite.config.ts) nên cùng origin, cookie phiên hoạt động */
export const API_BASE = import.meta.env.VITE_API_BASE ?? '/api/v1';
