/**
 * Chế độ dữ liệu:
 *  - "api"  (mặc định): đăng nhập, phân quyền, duyệt cấp quyền, chi nhánh, nhân viên / giáo viên, khóa học, lộ trình, phòng học, lớp học, buổi học, học viên, ghi danh,
 *    điểm danh / chấm công / báo cáo buổi, đơn hủy buổi / xin nghỉ / dạy thay, lên lớp, nhận xét cuối khóa, chăm sóc HV có nguy cơ bỏ học,
 *    cổng GV / HV / PH (lớp, lịch, đơn, QR, Quan tâm), cài đặt chung, nhật ký hệ thống, thùng rác, chuông thông báo + realtime SignalR,
 *    ngân hàng câu hỏi, Tuyển sinh (lead, chia lead, test xếp lớp, lịch hẹn, chiến dịch, mẫu tin, phễu, tư vấn lên lớp) gọi backend thật
 *    (cần chạy API + Docker — xem backend/README.md).
 *  - "mock": toàn bộ chạy bằng dữ liệu giả trong trình duyệt như trước (không cần backend).
 * Đặt trong frontend/.env.local:  VITE_API_MODE=mock
 * Các phần backend CHƯA làm (Phụ huynh HV-12, Tài chính / hóa đơn, bài tập / thi, tài liệu GV, flashcard…) vẫn dùng dữ liệu giả.
 * Store đã nối API khởi tạo RỖNG ở chế độ "api" (không lẫn dữ liệu giả); dữ liệu giả chỉ còn cho chế độ "mock".
 */
export const USE_API = import.meta.env.VITE_API_MODE !== 'mock';

/** Gốc API — dev đi qua proxy Vite (vite.config.ts) nên cùng origin, cookie phiên hoạt động */
export const API_BASE = import.meta.env.VITE_API_BASE ?? '/api/v1';
