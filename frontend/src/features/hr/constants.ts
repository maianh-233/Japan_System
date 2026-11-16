import {
  Banknote,
  Briefcase,
  CalendarOff,
  CheckCircle2,
  CircleDashed,
  Clock,
  FileSignature,
  HandCoins,
  Hourglass,
  Lightbulb,
  PauseCircle,
  Plane,
  Repeat,
  UserCheck,
  UserX,
  XCircle,
  type LucideIcon,
} from 'lucide-react';
import type { Tone } from '@/components/common';
import type { CandidateStage, ContractType, Department, EmployeeKind, EmployeeStatus, RequestStatus, RequestType, ShiftKey } from './types';
import { getSettings } from '@/features/system/settings';

type Meta = { label: string; tone: Tone; icon: LucideIcon };

export const DEPARTMENTS: Record<Department, string> = {
  board: 'Ban giám đốc',
  academic: 'Giáo vụ',
  sales: 'Tuyển sinh (Sale)',
  marketing: 'Marketing',
  finance: 'Kế toán',
  reception: 'Lễ tân',
  it: 'IT / Cơ sở vật chất',
  teaching: 'Giảng dạy',
};
export const DEPARTMENT_OPTIONS = (Object.keys(DEPARTMENTS) as Department[]).map((k) => ({ value: k, label: DEPARTMENTS[k] }));

export const EMPLOYEE_KIND: Record<EmployeeKind, string> = { staff: 'Nhân viên', teacher: 'Giáo viên', assistant: 'Trợ giảng' };

export const EMPLOYEE_STATUS: Record<EmployeeStatus, Meta> = {
  active: { label: 'Đang làm', tone: 'success', icon: UserCheck },
  probation: { label: 'Thử việc', tone: 'info', icon: Hourglass },
  leave: { label: 'Nghỉ dài hạn', tone: 'warning', icon: PauseCircle },
  quit: { label: 'Đã nghỉ việc', tone: 'neutral', icon: UserX },
};
export const EMPLOYEE_STATUS_OPTIONS = (Object.keys(EMPLOYEE_STATUS) as EmployeeStatus[]).map((k) => ({ value: k, label: EMPLOYEE_STATUS[k].label }));

export const CONTRACT_TYPES: Record<ContractType, string> = {
  probation: 'Thử việc',
  fixed: 'Xác định thời hạn',
  indefinite: 'Không xác định thời hạn',
  part_time: 'Bán thời gian',
  freelance: 'Cộng tác viên / theo giờ',
};
export const CONTRACT_TYPE_OPTIONS = (Object.keys(CONTRACT_TYPES) as ContractType[]).map((k) => ({ value: k, label: CONTRACT_TYPES[k] }));

/** Ca làm việc của nhân viên (khác ca học) */
export const WORK_SHIFTS: Record<ShiftKey, { label: string; short: string; from: string; to: string; tone: Tone }> = {
  morning: { label: 'Ca sáng', short: 'S', from: '08:00', to: '12:00', tone: 'info' },
  afternoon: { label: 'Ca chiều', short: 'C', from: '13:00', to: '17:30', tone: 'warning' },
  evening: { label: 'Ca tối', short: 'T', from: '17:30', to: '21:30', tone: 'primary' },
};
export const SHIFT_KEYS: ShiftKey[] = ['morning', 'afternoon', 'evening'];

/** Giờ hành chính */
/** Giờ hành chính — đọc từ Cài đặt chung (Hệ thống › Cài đặt chung) */
export const OFFICE_HOURS = {
  get from() {
    return getSettings().office.from;
  },
  get to() {
    return getSettings().office.to;
  },
  get satTo() {
    return getSettings().office.satTo;
  },
};
/** Đến sau giờ bắt đầu quá N phút = đi muộn; về trước giờ kết thúc quá N phút = về sớm */
/** Đi muộn / về sớm quá N phút mới tính vi phạm — Cài đặt chung */
export const lateGraceMin = () => getSettings().office.lateGraceMin;

export const REQUEST_TYPES: Record<RequestType, { label: string; icon: LucideIcon; hint: string }> = {
  leave: { label: 'Nghỉ phép', icon: CalendarOff, hint: 'Trừ vào phép năm (có lương) hoặc nghỉ không lương' },
  shift_swap: { label: 'Đổi ca', icon: Repeat, hint: 'Đổi ca với đồng nghiệp — duyệt xong tự cập nhật lịch ca' },
  business_trip: { label: 'Công tác', icon: Plane, hint: 'Đi công tác chi nhánh khác / đối tác' },
  late_early: { label: 'Đi muộn / về sớm', icon: Clock, hint: 'Xin phép trước — không tính vi phạm chấm công' },
  proposal: { label: 'Đề xuất', icon: Lightbulb, hint: 'Mua sắm, sửa chữa, ý tưởng...' },
  advance: { label: 'Tạm ứng', icon: HandCoins, hint: 'Tạm ứng lương / chi phí' },
};
export const REQUEST_TYPE_OPTIONS = (Object.keys(REQUEST_TYPES) as RequestType[]).map((k) => ({ value: k, label: REQUEST_TYPES[k].label }));

export const REQUEST_STATUS: Record<RequestStatus, Meta> = {
  pending: { label: 'Chờ duyệt', tone: 'warning', icon: CircleDashed },
  approved: { label: 'Đã duyệt', tone: 'success', icon: CheckCircle2 },
  rejected: { label: 'Từ chối', tone: 'danger', icon: XCircle },
  cancelled: { label: 'Đã hủy', tone: 'neutral', icon: XCircle },
};

export const CANDIDATE_STAGES: Record<CandidateStage, { label: string; tone: Tone; icon: LucideIcon }> = {
  applied: { label: 'Ứng tuyển', tone: 'neutral', icon: FileSignature },
  screening: { label: 'Sàng lọc CV', tone: 'info', icon: CircleDashed },
  interview: { label: 'Phỏng vấn', tone: 'primary', icon: Briefcase },
  offer: { label: 'Gửi offer', tone: 'warning', icon: Banknote },
  hired: { label: 'Nhận việc', tone: 'success', icon: UserCheck },
  rejected: { label: 'Loại', tone: 'danger', icon: XCircle },
};
export const PIPELINE: CandidateStage[] = ['applied', 'screening', 'interview', 'offer', 'hired'];

/** Checklist onboarding mặc định */
export const ONBOARDING_TEMPLATE: { key: string; group: 'docs' | 'account' | 'equipment' | 'training'; label: string; teacherOnly?: boolean }[] = [
  { key: 'contract', group: 'docs', label: 'Ký hợp đồng thử việc' },
  { key: 'idcard', group: 'docs', label: 'Bản sao CCCD, sổ hộ khẩu / CT07' },
  { key: 'degree', group: 'docs', label: 'Bằng cấp, chứng chỉ (JLPT, sư phạm...)' },
  { key: 'bank', group: 'docs', label: 'Số tài khoản ngân hàng nhận lương' },
  { key: 'account', group: 'account', label: 'Tạo tài khoản đăng nhập hệ thống + phân quyền' },
  { key: 'email', group: 'account', label: 'Email công ty, nhóm Zalo nội bộ' },
  { key: 'laptop', group: 'equipment', label: 'Bàn giao laptop / máy tính quầy' },
  { key: 'card', group: 'equipment', label: 'Thẻ nhân viên, đồng phục, tủ cá nhân' },
  { key: 'intro', group: 'training', label: 'Đào tạo hội nhập: nội quy, văn hóa trung tâm' },
  { key: 'system', group: 'training', label: 'Hướng dẫn sử dụng hệ thống theo vị trí' },
  { key: 'demo', group: 'training', label: 'Dạy thử / dự giờ 2 buổi', teacherOnly: true },
];
export const ONBOARDING_GROUPS = { docs: 'Giấy tờ', account: 'Tài khoản', equipment: 'Thiết bị', training: 'Đào tạo hội nhập' } as const;
