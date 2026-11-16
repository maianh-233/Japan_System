/**
 * Phòng học — thuộc 1 chi nhánh.
 *
 *  Room
 *   ├─ ClassInfo.room      phòng mặc định của lớp (lưu theo TÊN phòng, trong cùng chi nhánh)
 *   └─ ClassSession.room   đổi phòng cho riêng 1 buổi
 *  → Lịch sử dụng / tỷ lệ lấp đầy / trùng lịch đều SUY RA từ buổi học, không lưu riêng.
 *
 *  Trạng thái:
 *   - active       đang dùng, xếp lớp được
 *   - maintenance  tạm bảo trì (có hạn) — không xếp lớp MỚI vào, lớp cũ vẫn thấy cảnh báo
 *   - inactive     ngừng sử dụng — chỉ được chuyển khi không còn lớp đang mở dùng phòng
 */

export type RoomType = 'classroom' | 'lab' | 'workshop' | 'meeting';
export type RoomStatus = 'active' | 'maintenance' | 'inactive';
export type Equipment = 'projector' | 'tv' | 'whiteboard' | 'aircon' | 'speaker' | 'mic' | 'camera' | 'computer';

export interface Room {
  id: string;
  branchId: string;
  /** Tên hiển thị, duy nhất trong chi nhánh — vd P.301 */
  name: string;
  /** Tầng / khu */
  floor?: string;
  type: RoomType;
  /** Số chỗ ngồi tối đa */
  capacity: number;
  /** m² */
  area?: number;
  equipment: Equipment[];
  status: RoomStatus;
  /** Bảo trì: lý do + dự kiến xong */
  maintenanceReason?: string;
  maintenanceUntil?: string;
  note?: string;
  createdAt: string;
}

// ---------------- Báo hư hỏng ----------------

/**
 * Báo hư hỏng phòng / thiết bị — giáo viên, giáo vụ, lễ tân đều báo được.
 *  Vòng đời: open (mới báo) → in_progress (đã có người nhận sửa) → resolved (đã sửa xong)
 *  Mức "high" = không dạy được → gợi ý chuyển phòng sang Bảo trì / đổi phòng cho buổi học.
 */
export type IssueSeverity = 'low' | 'medium' | 'high';
export type IssueStatus = 'open' | 'in_progress' | 'resolved';

export interface RoomIssue {
  id: string;
  roomId: string;
  /** Thiết bị hỏng — bỏ trống = cơ sở vật chất khác (tường, cửa, đèn, bàn ghế...) */
  equipment?: Equipment;
  title: string;
  description?: string;
  severity: IssueSeverity;
  status: IssueStatus;
  reportedBy: string;
  reportedAt: string;
  /** Người / đơn vị nhận sửa */
  assignee?: string;
  /** Hẹn sửa xong */
  dueDate?: string;
  startedAt?: string;
  resolvedAt?: string;
  /** Đã làm gì để sửa */
  resolution?: string;
  /** Chi phí sửa (đ) */
  cost?: number;
}
