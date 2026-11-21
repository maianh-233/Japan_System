import {
  ThumbsUp,
  Globe,
  Mail,
  MessageCircle,
  MessagesSquare,
  Phone,
  PhoneCall,
  Smartphone,
  Store,
  UserRoundPlus,
  Users,
  CalendarHeart,
  Archive,
  CalendarClock,
  CheckCircle2,
  CirclePause,
  CirclePlay,
  FilePen,
  Flag,
  PenLine,
  Send,
  TimerOff,
  type LucideIcon,
} from 'lucide-react';
import { useMemo } from 'react';
import type { Tone } from '@/components/common';
import { useHrStore } from '@/features/hr/hrStore';
import type { Employee } from '@/features/hr/types';
import { ALL_BRANCHES } from '@/types/auth';
import type {
  AttemptStatus,
  CampaignStatus,
  ConsultChannel,
  ConsultResult,
  LeadSource,
  LeadStatus,
  PlacementAttempt,
  PlacementTestStatus,
  Relationship,
  RouteMethod,
} from './types';

type Opt<K extends string> = { value: K; label: string };

export const LEAD_STATUS_OPTIONS: Opt<LeadStatus>[] = [
  { value: 'new', label: 'Mới' },
  { value: 'consulting', label: 'Đang tư vấn' },
  { value: 'test_scheduled', label: 'Hẹn test' },
  { value: 'won', label: 'Đã chốt' },
  { value: 'lost', label: 'Mất' },
];

export const LEAD_SOURCES: Record<LeadSource, { label: string; icon: LucideIcon }> = {
  facebook: { label: 'Facebook', icon: ThumbsUp },
  website: { label: 'Website', icon: Globe },
  zalo: { label: 'Zalo', icon: MessageCircle },
  tiktok: { label: 'TikTok', icon: Smartphone },
  referral: { label: 'Giới thiệu', icon: UserRoundPlus },
  walk_in: { label: 'Đến trực tiếp', icon: Store },
  hotline: { label: 'Hotline', icon: Phone },
  event: { label: 'Sự kiện / Hội thảo', icon: CalendarHeart },
};
export const LEAD_SOURCE_OPTIONS = (Object.keys(LEAD_SOURCES) as LeadSource[]).map((k) => ({ value: k, label: LEAD_SOURCES[k].label }));

export const COURSE_OPTIONS = [
  { value: 'n5', label: 'JLPT N5 cơ bản' },
  { value: 'n4', label: 'JLPT N4' },
  { value: 'n3', label: 'JLPT N3' },
  { value: 'n2', label: 'JLPT N2' },
  { value: 'n1', label: 'JLPT N1' },
  { value: 'kaiwa', label: 'Kaiwa giao tiếp' },
  { value: 'kids', label: 'Tiếng Nhật thiếu nhi' },
  { value: 'xkld', label: 'Tiếng Nhật đi làm / XKLĐ' },
];
export const courseLabel = (v?: string) => COURSE_OPTIONS.find((c) => c.value === v)?.label;

/**
 * SALE = nhân viên phòng Sale (department 'sales') còn làm việc — lấy từ Nhân sự, là NGUỒN DUY NHẤT
 * cho: chia lead, Sale phụ trách lead, báo cáo theo Sale, hoa hồng, xếp ca. `Lead.assigneeId` = Employee.id.
 */
export const salesOf = (employees: Employee[], branchId?: string) =>
  employees.filter((e) => e.department === 'sales' && e.status !== 'quit' && (!branchId || branchId === ALL_BRANCHES || e.branchId === branchId));

/** Danh sách Sale (tự cập nhật khi Nhân sự thêm / cho nghỉ nhân viên) */
export function useSales(branchId?: string) {
  const employees = useHrStore((s) => s.employees);
  return useMemo(() => salesOf(employees, branchId), [employees, branchId]);
}

/** Tên người phụ trách lead (tra mọi nhân viên — kể cả người đã chuyển phòng / nghỉ, để lead cũ vẫn hiện tên) */
export const saleName = (id?: string) => (id ? useHrStore.getState().employees.find((e) => e.id === id)?.fullName : undefined);

export const CONSULT_CHANNELS: Record<ConsultChannel, { label: string; icon: LucideIcon }> = {
  call: { label: 'Gọi điện', icon: PhoneCall },
  zalo: { label: 'Zalo', icon: MessageCircle },
  meeting: { label: 'Gặp trực tiếp', icon: Users },
  message: { label: 'Nhắn tin (FB/SMS)', icon: MessagesSquare },
  email: { label: 'Email', icon: Mail },
};

export const CONSULT_RESULTS: Record<ConsultResult, { label: string; tone: Tone }> = {
  interested: { label: 'Quan tâm', tone: 'primary' },
  thinking: { label: 'Đang cân nhắc', tone: 'info' },
  test_booked: { label: 'Đặt lịch test', tone: 'warning' },
  no_answer: { label: 'Không nghe máy', tone: 'neutral' },
  not_interested: { label: 'Không có nhu cầu', tone: 'danger' },
  enrolled: { label: 'Đăng ký học', tone: 'success' },
};

export const RELATIONSHIPS: Record<Relationship, string> = {
  self: 'Bản thân',
  child: 'Con',
  sibling: 'Anh / chị / em',
  spouse: 'Vợ / chồng',
  other: 'Khác',
};

/** SLA MẶC ĐỊNH: lead mới chưa được liên hệ sau X phút → cảnh báo. Quy tắc chia lead có thể đặt SLA riêng. */
/** @deprecated Đọc getSettings().crm.defaultSlaMinutes (Hệ thống › Cài đặt chung) */
export const SLA_MINUTES = 15;

// ---------------- Chia lead ----------------

export const ROUTE_METHODS: Record<RouteMethod, { label: string; hint: string }> = {
  round_robin: { label: 'Xoay vòng', hint: 'Lần lượt từng Sale trong nhóm' },
  least_load: { label: 'Theo tải', hint: 'Sale đang giữ ít lead mở nhất' },
  fixed: { label: 'Ưu tiên cố định', hint: 'Sale đầu danh sách còn trong ca' },
};

/** Người / vai trò có thể nhận cảnh báo quá SLA */
export const ESCALATE_OPTIONS = ['Sale phụ trách', 'Trưởng nhóm Sale', 'Quản lý chi nhánh', 'Giám đốc tuyển sinh'].map((v) => ({ value: v, label: v }));

/**
 * Nhân viên (không phải giáo viên) còn làm việc — chọn người nhận cảnh báo quá SLA / người phụ trách chiến dịch (chế độ API: server lưu theo Id).
 * Giá trị = họ tên (store giữ tên để hiển thị, đổi tên → Id khi gửi server).
 */
export function useStaffNameOptions() {
  const employees = useHrStore((s) => s.employees);
  return useMemo(
    () =>
      employees
        .filter((e) => e.kind !== 'teacher' && e.status !== 'quit')
        .map((e) => ({ value: e.fullName, label: `${e.fullName} · ${e.position}` })),
    [employees],
  );
}

// ---------------- Test xếp lớp ----------------

export const PLACEMENT_STATUS: Record<PlacementTestStatus, { label: string; tone: Tone; icon: LucideIcon }> = {
  draft: { label: 'Nháp', tone: 'neutral', icon: FilePen },
  active: { label: 'Đang dùng', tone: 'success', icon: CheckCircle2 },
  archived: { label: 'Ngừng dùng', tone: 'warning', icon: Archive },
};

export const ATTEMPT_STATUS: Record<AttemptStatus, { label: string; tone: Tone; icon: LucideIcon }> = {
  sent: { label: 'Đã gửi link', tone: 'info', icon: Send },
  in_progress: { label: 'Đang làm', tone: 'warning', icon: PenLine },
  done: { label: 'Đã nộp', tone: 'success', icon: CheckCircle2 },
  expired: { label: 'Hết hạn', tone: 'neutral', icon: TimerOff },
};

export const SEND_CHANNELS: Record<PlacementAttempt['channel'], string> = { zalo: 'Zalo', sms: 'SMS', email: 'Email' };

// ---------------- Chiến dịch ----------------

export const CAMPAIGN_STATUS: Record<CampaignStatus, { label: string; tone: Tone; icon: LucideIcon }> = {
  planned: { label: 'Sắp chạy', tone: 'info', icon: CalendarClock },
  running: { label: 'Đang chạy', tone: 'success', icon: CirclePlay },
  paused: { label: 'Tạm dừng', tone: 'warning', icon: CirclePause },
  ended: { label: 'Đã kết thúc', tone: 'neutral', icon: Flag },
};
export const CAMPAIGN_STATUS_OPTIONS = (Object.keys(CAMPAIGN_STATUS) as CampaignStatus[]).map((k) => ({ value: k, label: CAMPAIGN_STATUS[k].label }));
