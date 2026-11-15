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
  type LucideIcon,
} from 'lucide-react';
import type { Tone } from '@/components/common';
import type { ConsultChannel, ConsultResult, LeadSource, LeadStatus, Relationship } from './types';

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

/** Sale (dữ liệu giả) */
export const SALES = [
  { id: 'u-sale', name: 'Trần Minh Khoa', branchId: 'q1' },
  { id: 's-02', name: 'Phạm Thị Hồng', branchId: 'cg' },
  { id: 's-03', name: 'Đinh Quốc Bảo', branchId: 'cg' },
  { id: 's-04', name: 'Lý Thanh Vy', branchId: 'hc' },
];
export const saleName = (id?: string) => SALES.find((s) => s.id === id)?.name;

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

/** Lead mới chưa được liên hệ sau X phút → cảnh báo SLA */
export const SLA_MINUTES = 15;
