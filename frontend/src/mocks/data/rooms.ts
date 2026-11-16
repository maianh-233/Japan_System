import dayjs from 'dayjs';
import type { Room, RoomIssue } from '@/features/academic/rooms/types';

/**
 * Dữ liệu giả — Phòng học. Tên phòng khớp với `room` của lớp trong mocks/data/classes.ts.
 * Tình huống: phòng đang bảo trì, phòng ngừng sử dụng, phòng trống, lớp có sĩ số tối đa > sức chứa (P.203).
 */

const ago = (days: number) => dayjs().subtract(days, 'day').hour(9).minute(0).second(0).toISOString();
const d = (days: number) => dayjs().add(days, 'day').format('YYYY-MM-DD');

export const MOCK_ROOMS: Room[] = [
  // ---- Cầu Giấy
  { id: 'R-CG-201', branchId: 'cg', name: 'P.201', floor: 'Tầng 2', type: 'classroom', capacity: 20, area: 36, equipment: ['projector', 'whiteboard', 'aircon', 'speaker'], status: 'active', createdAt: ago(900) },
  { id: 'R-CG-205', branchId: 'cg', name: 'P.205', floor: 'Tầng 2', type: 'classroom', capacity: 16, area: 30, equipment: ['tv', 'whiteboard', 'aircon'], status: 'active', note: 'Phòng thảm — ưu tiên lớp thiếu nhi.', createdAt: ago(900) },
  { id: 'R-CG-301', branchId: 'cg', name: 'P.301', floor: 'Tầng 3', type: 'classroom', capacity: 20, area: 38, equipment: ['projector', 'whiteboard', 'aircon', 'speaker', 'mic'], status: 'active', createdAt: ago(800) },
  { id: 'R-CG-302', branchId: 'cg', name: 'P.302', floor: 'Tầng 3', type: 'classroom', capacity: 22, area: 40, equipment: ['projector', 'whiteboard', 'aircon', 'camera'], status: 'active', createdAt: ago(800) },
  { id: 'R-CG-303', branchId: 'cg', name: 'P.303', floor: 'Tầng 3', type: 'lab', capacity: 12, area: 28, equipment: ['computer', 'speaker', 'aircon'], status: 'maintenance', maintenanceReason: 'Thay tai nghe + nâng cấp máy tính phòng nghe', maintenanceUntil: d(9), createdAt: ago(600) },
  { id: 'R-CG-401', branchId: 'cg', name: 'P.401', floor: 'Tầng 4', type: 'meeting', capacity: 4, area: 12, equipment: ['tv', 'whiteboard', 'aircon'], status: 'active', note: 'Dùng cho lớp 1-1, test xếp lớp, tư vấn.', createdAt: ago(400) },
  // ---- Quận 1
  { id: 'R-Q1-101', branchId: 'q1', name: 'P.101', floor: 'Tầng 1', type: 'classroom', capacity: 22, area: 40, equipment: ['projector', 'whiteboard', 'aircon', 'speaker'], status: 'active', createdAt: ago(700) },
  { id: 'R-Q1-102', branchId: 'q1', name: 'P.102', floor: 'Tầng 1', type: 'classroom', capacity: 20, area: 36, equipment: ['tv', 'whiteboard', 'aircon'], status: 'active', createdAt: ago(700) },
  { id: 'R-Q1-203', branchId: 'q1', name: 'P.203', floor: 'Tầng 2', type: 'classroom', capacity: 10, area: 22, equipment: ['tv', 'whiteboard', 'aircon'], status: 'active', note: 'Phòng nhỏ — hợp lớp Kaiwa.', createdAt: ago(500) },
  { id: 'R-Q1-204', branchId: 'q1', name: 'P.204', floor: 'Tầng 2', type: 'classroom', capacity: 18, area: 32, equipment: ['whiteboard'], status: 'inactive', note: 'Ngừng dùng từ tháng trước — chuyển thành kho tài liệu.', createdAt: ago(650) },
  // ---- Hải Châu
  { id: 'R-HC-A1', branchId: 'hc', name: 'P.A1', floor: 'Tầng 1', type: 'classroom', capacity: 28, area: 50, equipment: ['projector', 'whiteboard', 'aircon', 'speaker', 'mic'], status: 'active', createdAt: ago(500) },
  { id: 'R-HC-A2', branchId: 'hc', name: 'P.A2', floor: 'Tầng 1', type: 'classroom', capacity: 22, area: 40, equipment: ['projector', 'whiteboard', 'aircon'], status: 'active', createdAt: ago(500) },
  { id: 'R-HC-WS', branchId: 'hc', name: 'Xưởng thực hành', floor: 'Tầng trệt', type: 'workshop', capacity: 20, area: 80, equipment: ['whiteboard', 'speaker'], status: 'active', note: 'Mô phỏng nhà xưởng cho lớp XKLĐ (chào hỏi, 5S, an toàn lao động).', createdAt: ago(300) },
];

/** Báo hư hỏng — có đủ: mới báo (nghiêm trọng / nhẹ), đang sửa, đã sửa xong (kèm chi phí) */
const hoursAgo = (h: number) => dayjs().subtract(h, 'hour').toISOString();

export const MOCK_ROOM_ISSUES: RoomIssue[] = [
  { id: 'RI-1', roomId: 'R-CG-301', equipment: 'projector', title: 'Máy chiếu bị nhòe, lệch màu', description: 'Hình chiếu mờ ở góc phải, chữ Kanji nhỏ khó đọc. GV phải dùng bảng trắng thay.', severity: 'medium', status: 'open', reportedBy: 'Nguyễn Thu Hà (Giáo viên)', reportedAt: hoursAgo(20) },
  { id: 'RI-2', roomId: 'R-CG-205', equipment: 'aircon', title: 'Điều hòa chảy nước', description: 'Nước nhỏ xuống thảm khu bàn cuối, đã kê xô tạm.', severity: 'high', status: 'in_progress', reportedBy: 'Trần Mai Anh (Giáo viên)', reportedAt: hoursAgo(50), assignee: 'Điện lạnh Hòa Phát (0243 888 999)', startedAt: hoursAgo(30), dueDate: d(1) },
  { id: 'RI-3', roomId: 'R-CG-302', equipment: 'camera', title: 'Camera hybrid không nhận trên Zoom', severity: 'low', status: 'open', reportedBy: 'Lễ tân Cầu Giấy', reportedAt: hoursAgo(4) },
  { id: 'RI-4', roomId: 'R-CG-303', equipment: 'computer', title: '5 máy tính phòng nghe không lên nguồn', severity: 'high', status: 'in_progress', reportedBy: 'Phạm Đức Long (Giáo viên)', reportedAt: ago(6), assignee: 'IT nội bộ — Anh Tuấn', startedAt: ago(5), dueDate: d(9) },
  { id: 'RI-5', roomId: 'R-Q1-101', title: 'Bóng đèn cuối lớp nhấp nháy', severity: 'low', status: 'resolved', reportedBy: 'Lê Minh Thư (Giáo viên)', reportedAt: ago(12), assignee: 'Bảo vệ tòa nhà', startedAt: ago(11), resolvedAt: ago(11), resolution: 'Thay 2 bóng LED tuýp 1m2.', cost: 180_000 },
  { id: 'RI-6', roomId: 'R-HC-A1', equipment: 'mic', title: 'Micro không dây hết pin nhanh', severity: 'low', status: 'resolved', reportedBy: 'Võ Thị Hạnh (Giáo viên)', reportedAt: ago(20), assignee: 'Giáo vụ Hải Châu', startedAt: ago(19), resolvedAt: ago(18), resolution: 'Thay bộ pin sạc mới, dán lịch sạc sau mỗi ca.', cost: 350_000 },
  { id: 'RI-7', roomId: 'R-Q1-203', title: 'Tay nắm cửa bị lỏng', severity: 'low', status: 'open', reportedBy: 'Sato Yuki (Giáo viên)', reportedAt: ago(3) },
];
