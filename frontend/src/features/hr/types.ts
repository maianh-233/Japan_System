/**
 * NHÂN SỰ
 *
 *  Employee (nhân viên / giáo viên / trợ giảng — 1 bảng, phân biệt bằng `kind`)
 *   ├─ contract         hợp đồng hiện tại
 *   ├─ teacher          hồ sơ giảng dạy (chỉ kind = teacher): cấp độ dạy, chứng chỉ, đơn giá, lịch rảnh
 *   ├─ TimeRecord[]     chấm công check-in / check-out (nhân viên văn phòng & làm theo ca)
 *   └─ HrRequest[]      đơn từ: nghỉ phép, đổi ca, công tác, đi muộn / về sớm, đề xuất, tạm ứng — duyệt nhiều cấp
 *  Tuyển dụng: Job → Candidate (pipeline) → Onboarding → tạo Employee + tài khoản.
 *  Giáo viên chấm công THEO BUỔI DẠY (Buổi học → chấm công GV), không chấm công văn phòng.
 */

import type { QLevel } from '@/features/academic/questions/types';

export type EmployeeKind = 'staff' | 'teacher' | 'assistant';
export type Department = 'board' | 'academic' | 'sales' | 'marketing' | 'finance' | 'reception' | 'it' | 'teaching';
export type EmployeeStatus = 'active' | 'probation' | 'leave' | 'quit';
export type ContractType = 'probation' | 'fixed' | 'indefinite' | 'part_time' | 'freelance';
export type ShiftKey = 'morning' | 'afternoon' | 'evening';

export interface Contract {
  type: ContractType;
  no: string;
  start: string;
  end?: string;
  /** Lương cơ bản / tháng (GV: để trống, tính theo giờ dạy) */
  baseSalary?: number;
  allowance?: number;
}

export interface TeacherProfile {
  type: 'vn' | 'jp';
  levels: QLevel[];
  certificates: string[];
  /** Đơn giá / giờ */
  rate: { offline: number; online: number };
  /** Lịch rảnh: thứ (1=T2..7=CN) → ca */
  availability: Record<number, ShiftKey[]>;
  /** Chi nhánh dạy thêm ngoài chi nhánh chính (`Employee.branchId`) — dùng khi xếp lớp / dạy thay */
  extraBranchIds?: string[];
  bio?: string;
}

export interface Employee {
  id: string;
  code: string;
  fullName: string;
  gender: 'male' | 'female';
  dob?: string;
  phone: string;
  email: string;
  branchId: string;
  kind: EmployeeKind;
  department: Department;
  position: string;
  /** Làm theo ca (Sale, lễ tân...) → xếp ca hàng tuần; còn lại giờ hành chính */
  shiftBased: boolean;
  status: EmployeeStatus;
  joinedAt: string;
  quitAt?: string;
  managerId?: string;
  contract: Contract;
  /** Ngày phép năm còn lại */
  leaveBalance: number;
  /** Đã có tài khoản đăng nhập */
  hasAccount: boolean;
  teacher?: TeacherProfile;
  note?: string;
  /** Lý do nghỉ việc */
  quitReason?: string;
  /** Đã hẹn ngày nghỉ (`quitAt` ở tương lai, chưa tới ngày) */
  quitScheduled?: boolean;
  /** Người xem không có quyền xem lương — lương / phụ cấp / đơn giá bị ẩn (server không trả) */
  salaryHidden?: boolean;
  /** Hợp đồng trước đây (mới nhất trước) — chỉ có khi tải chi tiết từ server */
  contractHistory?: Contract[];
}

// ---------------- Chấm công ----------------

export interface TimeRecord {
  id: string;
  employeeId: string;
  date: string;
  /** HH:mm */
  checkIn?: string;
  checkOut?: string;
  /** Sửa tay bởi quản lý (quên chấm, lỗi máy...) */
  edited?: { by: string; reason: string };
}

// ---------------- Đơn từ ----------------

export type RequestType = 'leave' | 'shift_swap' | 'business_trip' | 'late_early' | 'proposal' | 'advance';
export type RequestStatus = 'pending' | 'approved' | 'rejected' | 'cancelled';

export interface ApprovalStep {
  role: string;
  approver: string;
  decision?: 'approved' | 'rejected';
  at?: string;
  comment?: string;
}

export interface HrRequest {
  id: string;
  code: string;
  type: RequestType;
  employeeId: string;
  from: string;
  to: string;
  /** Số ngày nghỉ (nghỉ phép) */
  days?: number;
  /** Nghỉ phép có lương / không lương */
  paidLeave?: boolean;
  /** Đổi ca: ca của mình ↔ ca của đồng nghiệp */
  swap?: { myShiftId: string; withEmployeeId: string; theirShiftId: string };
  /** Đề xuất / tạm ứng */
  amount?: number;
  title?: string;
  reason: string;
  status: RequestStatus;
  steps: ApprovalStep[];
  createdAt: string;
}

// ---------------- Tuyển dụng & Onboarding ----------------

export type CandidateStage = 'applied' | 'screening' | 'interview' | 'offer' | 'hired' | 'rejected';

export interface Job {
  id: string;
  title: string;
  branchId: string;
  department: Department;
  kind: EmployeeKind;
  quantity: number;
  salaryRange?: string;
  status: 'open' | 'paused' | 'closed';
  postedAt: string;
  deadline: string;
  channels: string[];
  description?: string;
}

export interface Candidate {
  id: string;
  jobId: string;
  fullName: string;
  phone: string;
  email: string;
  source: string;
  stage: CandidateStage;
  appliedAt: string;
  cvName?: string;
  /** Trình độ tiếng Nhật (GV) */
  japanese?: string;
  expectedSalary?: number;
  interview?: { at: string; interviewer: string; mode: 'offline' | 'online'; score?: number; note?: string };
  offer?: { salary: number; startDate: string; sentAt: string; accepted?: boolean };
  rejectReason?: string;
  notes: { at: string; by: string; text: string }[];
}

export interface OnboardingItem {
  key: string;
  group: 'docs' | 'account' | 'equipment' | 'training';
  label: string;
  done: boolean;
  doneBy?: string;
  doneAt?: string;
}

export interface Onboarding {
  id: string;
  candidateId?: string;
  fullName: string;
  phone: string;
  email: string;
  position: string;
  department: Department;
  kind: EmployeeKind;
  branchId: string;
  startDate: string;
  mentor: string;
  items: OnboardingItem[];
  /** Đã tạo hồ sơ nhân viên */
  employeeId?: string;
  createdAt: string;
}
