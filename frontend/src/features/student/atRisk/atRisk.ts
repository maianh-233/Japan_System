import dayjs from 'dayjs';
import { create } from 'zustand';
import { CircleCheck, CircleDashed, Eye, PhoneCall, type LucideIcon } from 'lucide-react';
import type { Tone } from '@/components/common';
import type { StudentStats } from '@/features/academic/classes/studentInsights';
import { getSettings } from '@/features/system/settings';
import { USE_API } from '@/lib/api/config';

/**
 * Cảnh báo bỏ học = học viên ĐANG HỌC có dấu hiệu nghỉ: vắng nhiều / vắng liền, chuyên cần thấp, không nộp bài,
 * điểm thấp, nợ học phí quá hạn (quy tắc ở ALERT_RULES — studentInsights.ts).
 * Mỗi trường hợp (học viên × lớp) có người phụ trách chăm sóc + trạng thái + lịch hẹn gọi lại.
 * Nhật ký liên hệ dùng chung CareLog của lớp (hiện cả ở tab Học viên của lớp).
 */

export type RiskLevel = 'high' | 'medium' | 'low';
export type CareStatus = 'new' | 'contacting' | 'monitoring' | 'resolved';

export const RISK_LEVEL: Record<RiskLevel, { label: string; tone: Tone; hint: string }> = {
  high: { label: 'Nguy cơ cao', tone: 'danger', hint: 'Liên hệ trong ngày' },
  medium: { label: 'Trung bình', tone: 'warning', hint: 'Liên hệ trong 2–3 ngày' },
  low: { label: 'Thấp', tone: 'info', hint: 'Theo dõi thêm' },
};

export const CARE_STATUS: Record<CareStatus, { label: string; tone: Tone; icon: LucideIcon }> = {
  new: { label: 'Chưa liên hệ', tone: 'danger', icon: CircleDashed },
  contacting: { label: 'Đang liên hệ', tone: 'warning', icon: PhoneCall },
  monitoring: { label: 'Đang theo dõi', tone: 'info', icon: Eye },
  resolved: { label: 'Đã ổn định', tone: 'success', icon: CircleCheck },
};
export const CARE_STATUS_OPTIONS = (Object.keys(CARE_STATUS) as CareStatus[]).map((k) => ({ value: k, label: CARE_STATUS[k].label }));

/** Nhân viên nhận chăm sóc (dữ liệu giả — sau lấy từ Nhân sự) */
export const CARE_STAFF = ['Phạm Thị Hồng (Giáo vụ)', 'Lý Thanh Vy (Giáo vụ)', 'Đinh Quốc Bảo (Sale)', 'Trần Minh Khoa (Sale)'];

export function riskLevel(st: StudentStats): RiskLevel | undefined {
  if (!st.flags.length) return undefined;
  const { highFrom, mediumFrom } = getSettings().atRisk;
  if (st.risk >= highFrom || st.attendance.streak >= 3) return 'high';
  if (st.risk >= mediumFrom) return 'medium';
  return 'low';
}

export interface CareCase {
  assignee?: string;
  status: CareStatus;
  /** Hẹn liên hệ lại */
  nextAt?: string;
  updatedAt: string;
}

export const caseKey = (classId: string, studentId: string) => `${classId}:${studentId}`;

interface AtRiskState {
  cases: Record<string, CareCase>;
  setCase: (key: string, patch: Partial<CareCase>) => void;
}

const ago = (d: number) => dayjs().subtract(d, 'day').toISOString();

export const useAtRiskStore = create<AtRiskState>((set) => ({
  // Một vài trường hợp đã được giao (dữ liệu giả) — còn lại mặc định "Chưa liên hệ"
  // Chế độ API: tải từ server (careApi.loadCareCases)
  cases: USE_API ? ({} as Record<string, CareCase>) : {
    'C-N3K12:ST-20': { assignee: CARE_STAFF[0], status: 'contacting', nextAt: dayjs().add(1, 'day').format('YYYY-MM-DD'), updatedAt: ago(1) },
    'C-N4K15:ST-21': { assignee: CARE_STAFF[1], status: 'monitoring', updatedAt: ago(4) },
  },
  setCase: (key, patch) =>
    set((s) => ({ cases: { ...s.cases, [key]: { ...(s.cases[key] ?? { status: 'new' }), ...patch, updatedAt: new Date().toISOString() } } })),
}));
