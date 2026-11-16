import { CheckCircle2, CircleSlash, Mail, MessageCircle, Phone, Smartphone, Sparkles, TriangleAlert, type LucideIcon } from 'lucide-react';
import type { Tone } from '@/components/common';
import type { PromoKind, ReminderChannel, TxnStatus } from './types';

export const PROMO_KIND: Record<PromoKind, string> = { percent: 'Giảm %', amount: 'Giảm tiền', combo: 'Combo nhiều khóa' };

export const TXN_STATUS: Record<TxnStatus, { label: string; tone: Tone; icon: LucideIcon }> = {
  auto: { label: 'Tự khớp', tone: 'success', icon: Sparkles },
  matched: { label: 'Khớp tay', tone: 'primary', icon: CheckCircle2 },
  unmatched: { label: 'Chưa khớp', tone: 'warning', icon: TriangleAlert },
  ignored: { label: 'Bỏ qua', tone: 'neutral', icon: CircleSlash },
};

export const REMINDER_CHANNEL: Record<ReminderChannel, { label: string; icon: LucideIcon }> = {
  zalo: { label: 'Zalo', icon: MessageCircle },
  email: { label: 'Email', icon: Mail },
  sms: { label: 'SMS', icon: Smartphone },
  call: { label: 'Gọi điện', icon: Phone },
};

/** Tài khoản nhận tiền theo chi nhánh (VietQR) */
export const BANK_ACCOUNTS: Record<string, { bank: string; no: string; name: string }> = {
  cg: { bank: 'Vietcombank', no: '0011004455661', name: 'CONG TY JP CENTER - CN CAU GIAY' },
  q1: { bank: 'Techcombank', no: '19037788990011', name: 'CONG TY JP CENTER - CN QUAN 1' },
  hc: { bank: 'BIDV', no: '56010001234567', name: 'CONG TY JP CENTER - CN HAI CHAU' },
};

/** Tuổi nợ */
export const AGING = [
  { key: 'not_due', label: 'Chưa đến hạn', tone: 'info' as Tone },
  { key: 'd30', label: 'Quá hạn 1–30 ngày', tone: 'warning' as Tone },
  { key: 'd60', label: 'Quá hạn 31–60 ngày', tone: 'danger' as Tone },
  { key: 'd60p', label: 'Quá hạn > 60 ngày', tone: 'danger' as Tone },
] as const;
export type AgingKey = (typeof AGING)[number]['key'];

/** Lương nhân viên */
export const STANDARD_DAYS = 26;
export const INSURANCE_RATE = 0.105; // BHXH 8% + BHYT 1.5% + BHTN 1%
export const LATE_FINE = 50_000;
export const NO_OUT_FINE = 30_000;
/** Giảm trừ gia cảnh bản thân (TNCN) */
export const PERSONAL_DEDUCTION = 11_000_000;
/** Lương GV: phụ cấp dạy thay mỗi buổi */
export const SUBSTITUTE_BONUS = 50_000;
/** Phạt GV đi muộn mỗi buổi */
export const TEACHER_LATE_FINE = 30_000;
