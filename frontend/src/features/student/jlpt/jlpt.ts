import dayjs from 'dayjs';
import { create } from 'zustand';
import { BadgeCheck, CircleDashed, FileCheck2, Send, Ticket, UserX, Wallet, XCircle, type LucideIcon } from 'lucide-react';
import type { Tone } from '@/components/common';
import type { QLevel } from '@/features/academic/questions/types';
import { USE_API } from '@/lib/api/config';
import { MOCK_JLPT } from '@/mocks/data/jlpt';

/**
 * Thi JLPT (Năng lực Nhật ngữ) — 2 kỳ / năm: tháng 7 và tháng 12.
 * Trung tâm hỗ trợ học viên: tư vấn cấp độ → nhận hồ sơ (ảnh, CCCD, phiếu ĐK) → thu lệ phí → nộp hồ sơ lên hội đồng thi
 * → phát phiếu dự thi → sau kỳ thi nhập kết quả → thống kê tỉ lệ đỗ theo lớp / giáo viên.
 */

export interface JlptSession {
  id: string;
  label: string;
  examDate: string;
  /** Hạn trung tâm nhận hồ sơ (trước hạn hội đồng thi vài ngày) */
  regClose: string;
  resultDate: string;
  /** Lệ phí theo cấp độ — chế độ API lấy theo kỳ (server); dữ liệu giả dùng JLPT_FEE */
  fees?: Record<QLevel, number>;
  /** Địa điểm thi của kỳ — dữ liệu giả dùng JLPT_VENUES */
  venues?: string[];
}

export const JLPT_SESSIONS: JlptSession[] = [
  { id: '2026-12', label: 'Tháng 12/2026', examDate: '2026-12-06', regClose: '2026-08-25', resultDate: '2027-01-26' },
  { id: '2026-07', label: 'Tháng 7/2026', examDate: '2026-07-05', regClose: '2026-04-03', resultDate: '2026-08-25' },
  { id: '2025-12', label: 'Tháng 12/2025', examDate: '2025-12-07', regClose: '2025-08-26', resultDate: '2026-01-26' },
];

export const JLPT_VENUES = ['Hà Nội — ĐH Ngoại ngữ (ĐHQGHN)', 'Hà Nội — ĐH Hà Nội', 'TP.HCM — ĐH KHXH&NV', 'Đà Nẵng — ĐH Ngoại ngữ Đà Nẵng'];

export const JLPT_FEE: Record<QLevel, number> = { N1: 700_000, N2: 650_000, N3: 600_000, N4: 550_000, N5: 550_000 };

/** Lệ phí / địa điểm của 1 kỳ (kỳ không có → bảng chung) */
export const feeOf = (s: JlptSession | undefined, level: QLevel) => s?.fees?.[level] ?? JLPT_FEE[level];
export const venuesOf = (s: JlptSession | undefined) => (s?.venues?.length ? s.venues : JLPT_VENUES);

export type JlptStatus = 'registered' | 'paid' | 'submitted' | 'admit' | 'done' | 'absent' | 'cancelled';

export const JLPT_STATUS: Record<JlptStatus, { label: string; tone: Tone; icon: LucideIcon; step: number }> = {
  registered: { label: 'Đã đăng ký', tone: 'neutral', icon: CircleDashed, step: 0 },
  paid: { label: 'Đã đóng lệ phí', tone: 'info', icon: Wallet, step: 1 },
  submitted: { label: 'Đã nộp hội đồng', tone: 'primary', icon: Send, step: 2 },
  admit: { label: 'Có phiếu dự thi', tone: 'success', icon: Ticket, step: 3 },
  done: { label: 'Đã có kết quả', tone: 'success', icon: BadgeCheck, step: 4 },
  absent: { label: 'Vắng thi', tone: 'danger', icon: UserX, step: 4 },
  cancelled: { label: 'Hủy đăng ký', tone: 'neutral', icon: XCircle, step: -1 },
};

export const DOCS = [
  { key: 'photo', label: 'Ảnh 3×4' },
  { key: 'idCard', label: 'CCCD' },
  { key: 'form', label: 'Phiếu ĐK đã ký' },
] as const;
export type DocKey = (typeof DOCS)[number]['key'];

export interface JlptReg {
  id: string;
  studentId: string;
  sessionId: string;
  level: QLevel;
  venue: string;
  status: JlptStatus;
  docs: Record<DocKey, boolean>;
  paidAt?: string;
  /** Số báo danh */
  admitNo?: string;
  /** Điểm từng phần theo cấu trúc đề của cấp độ */
  scores?: number[];
  /**
   * CHỨNG CHỈ GỐC (do hội đồng thi JLPT cấp — khác "chứng nhận hoàn thành khóa" của trung tâm).
   * Đỗ → chứng chỉ gửi về trung tâm (khoảng 1–2 tháng sau khi có điểm) → trung tâm phát cho học viên.
   */
  certificate?: { no: string; receivedAt: string; receivedBy: string; handedAt?: string; handedBy?: string; handedTo?: string };
  note?: string;
  createdAt: string;
  /** Chế độ API: tên / mã HV server trả (HV ngoài danh sách đã nạp vẫn hiện), lệ phí chốt lúc đăng ký */
  studentName?: string;
  studentCode?: string;
  branchId?: string;
  fee?: number;
}

/** Tình trạng chứng chỉ gốc của 1 lượt thi đã đỗ */
export const certStage = (r: Pick<JlptReg, 'certificate'>) => (r.certificate?.handedAt ? 'handed' : r.certificate ? 'received' : 'waiting');
export const CERT_STAGE = {
  waiting: { label: 'Chờ chứng chỉ gốc về', tone: 'warning' as Tone },
  received: { label: 'Chứng chỉ đã về — chờ nhận', tone: 'info' as Tone },
  handed: { label: 'Đã nhận chứng chỉ', tone: 'success' as Tone },
};

// ---------------- Quy chế chấm điểm ----------------

/** Cấu trúc phần thi + điểm liệt (theo JLPT) */
export const SECTIONS: Record<QLevel, { name: string; max: number; min: number }[]> = {
  N1: [
    { name: 'Kiến thức ngôn ngữ', max: 60, min: 19 },
    { name: 'Đọc hiểu', max: 60, min: 19 },
    { name: 'Nghe hiểu', max: 60, min: 19 },
  ],
  N2: [
    { name: 'Kiến thức ngôn ngữ', max: 60, min: 19 },
    { name: 'Đọc hiểu', max: 60, min: 19 },
    { name: 'Nghe hiểu', max: 60, min: 19 },
  ],
  N3: [
    { name: 'Kiến thức ngôn ngữ', max: 60, min: 19 },
    { name: 'Đọc hiểu', max: 60, min: 19 },
    { name: 'Nghe hiểu', max: 60, min: 19 },
  ],
  N4: [
    { name: 'Kiến thức ngôn ngữ · Đọc hiểu', max: 120, min: 38 },
    { name: 'Nghe hiểu', max: 60, min: 19 },
  ],
  N5: [
    { name: 'Kiến thức ngôn ngữ · Đọc hiểu', max: 120, min: 38 },
    { name: 'Nghe hiểu', max: 60, min: 19 },
  ],
};
/** Điểm tổng tối thiểu để đỗ (thang 180) */
export const PASS_TOTAL: Record<QLevel, number> = { N1: 100, N2: 90, N3: 95, N4: 90, N5: 80 };

export function judge(level: QLevel, scores?: number[]) {
  if (!scores) return undefined;
  const total = scores.reduce((a, b) => a + b, 0);
  const failSections = SECTIONS[level].filter((s, i) => (scores[i] ?? 0) < s.min).map((s) => s.name);
  return { total, passed: total >= PASS_TOTAL[level] && !failSections.length, failSections };
}

export const sessionPhase = (s: JlptSession) => {
  const today = dayjs().format('YYYY-MM-DD');
  if (today < s.examDate) return 'upcoming' as const;
  if (today < s.resultDate) return 'waiting' as const;
  return 'result' as const;
};

// ---------------- Store (chế độ API: rỗng lúc đầu, jlptApi đổ dữ liệu server; thao tác đi qua jlptApi) ----------------

interface JlptState {
  /** Kỳ thi (mới nhất trước) — chế độ API: server */
  sessions: JlptSession[];
  regs: JlptReg[];
  add: (r: Omit<JlptReg, 'id' | 'createdAt'>) => void;
  update: (id: string, patch: Partial<JlptReg>) => void;
  updateMany: (ids: string[], patch: Partial<JlptReg>) => void;
}

export const useJlptStore = create<JlptState>((set) => ({
  sessions: USE_API ? [] : JLPT_SESSIONS,
  regs: USE_API ? [] : MOCK_JLPT,
  add: (r) => set((s) => ({ regs: [...s.regs, { ...r, id: `JR-${Date.now()}`, createdAt: new Date().toISOString() }] })),
  update: (id, patch) => set((s) => ({ regs: s.regs.map((r) => (r.id === id ? { ...r, ...patch } : r)) })),
  updateMany: (ids, patch) => set((s) => ({ regs: s.regs.map((r) => (ids.includes(r.id) ? { ...r, ...patch } : r)) })),
}));

export const DOC_ICON = FileCheck2;
