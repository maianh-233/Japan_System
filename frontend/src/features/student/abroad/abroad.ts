import dayjs from 'dayjs';
import { create } from 'zustand';
import { FileStack, Handshake, MessagesSquare, Plane, ScrollText, Stamp, type LucideIcon } from 'lucide-react';
import type { Tone } from '@/components/common';
import type { QLevel } from '@/features/academic/questions/types';
import { MOCK_ABROAD } from '@/mocks/data/abroad';
import { DOC_TEMPLATES, docTemplate } from './docs';

/**
 * Du học / Xuất khẩu lao động (bật nếu trung tâm có mảng này).
 *  Tư vấn → Hồ sơ giấy tờ → Phỏng vấn (trường / xí nghiệp) → Nộp & nhận COE → Visa → Xuất cảnh
 *  COE (Giấy chứng nhận tư cách lưu trú) do Cục XNC Nhật cấp — thường mất 1–3 tháng.
 */

export type Program = 'study' | 'tokutei' | 'tts' | 'engineer';
export type Stage = 'consult' | 'documents' | 'interview' | 'coe' | 'visa' | 'departed';
export type DocState = 'missing' | 'received' | 'verified';

export const PROGRAMS: Record<Program, { label: string; short: string; tone: Tone }> = {
  study: { label: 'Du học', short: 'Du học', tone: 'primary' },
  tokutei: { label: 'Kỹ năng đặc định (Tokutei)', short: 'Tokutei', tone: 'success' },
  tts: { label: 'Thực tập sinh kỹ năng', short: 'TTS', tone: 'warning' },
  engineer: { label: 'Kỹ sư / Biên phiên dịch', short: 'Kỹ sư', tone: 'info' },
};
export const PROGRAM_OPTIONS = (Object.keys(PROGRAMS) as Program[]).map((k) => ({ value: k, label: PROGRAMS[k].label }));

export const STAGES: Record<Stage, { label: string; icon: LucideIcon; hint: string }> = {
  consult: { label: 'Tư vấn', icon: Handshake, hint: 'Chọn chương trình, trường / đơn hàng' },
  documents: { label: 'Hồ sơ giấy tờ', icon: FileStack, hint: 'Thu & kiểm tra giấy tờ' },
  interview: { label: 'Phỏng vấn', icon: MessagesSquare, hint: 'Trường / xí nghiệp phỏng vấn' },
  coe: { label: 'Chờ COE', icon: ScrollText, hint: 'Đã nộp Cục XNC, chờ cấp COE' },
  visa: { label: 'Xin visa', icon: Stamp, hint: 'Nộp ĐSQ / LSQ Nhật' },
  departed: { label: 'Đã xuất cảnh', icon: Plane, hint: 'Đã bay sang Nhật' },
};
export const STAGE_ORDER: Stage[] = ['consult', 'documents', 'interview', 'coe', 'visa', 'departed'];

export { DOC_TEMPLATES, docTemplate };

export interface AbroadCase {
  id: string;
  studentId: string;
  program: Program;
  /** Trường Nhật ngữ / nghiệp đoàn / công ty tiếp nhận */
  partner: string;
  /** Tỉnh / thành phố ở Nhật */
  destination: string;
  /** Ngành nghề (XKLĐ) */
  job?: string;
  /** Kỳ nhập học / dự kiến xuất cảnh — YYYY-MM */
  intake: string;
  /** Yêu cầu tiếng Nhật */
  jlptRequired?: QLevel;
  stage: Stage;
  cancelled?: { reason: string; at: string };
  docs: { name: string; state: DocState }[];
  interview?: { date: string; result?: 'pass' | 'fail' };
  coe?: { submittedAt?: string; grantedAt?: string };
  visa?: { appliedAt?: string; issuedAt?: string };
  flightDate?: string;
  fee: { total: number; paid: number };
  assignee: string;
  notes: { at: string; by: string; text: string }[];
  createdAt: string;
}

export const ABROAD_STAFF = ['Lý Thanh Vy (Tư vấn du học)', 'Võ Thị Hạnh (Phụ trách XKLĐ)', 'Đinh Quốc Bảo (Sale)'];

export const docProgress = (c: AbroadCase) => ({ done: c.docs.filter((d) => d.state === 'verified').length, total: c.docs.length });

interface AbroadState {
  cases: AbroadCase[];
  add: (c: Omit<AbroadCase, 'id' | 'createdAt' | 'notes' | 'docs'>, by: string) => AbroadCase;
  update: (id: string, patch: Partial<AbroadCase>) => void;
  note: (id: string, text: string, by: string) => void;
}

export const useAbroadStore = create<AbroadState>((set) => ({
  cases: MOCK_ABROAD,
  add: (c, by) => {
    const item: AbroadCase = { ...c, id: `AB-${Date.now()}`, createdAt: new Date().toISOString(), docs: docTemplate(c.program).map((name) => ({ name, state: 'missing' })), notes: [{ at: new Date().toISOString(), by, text: 'Tạo hồ sơ.' }] };
    set((s) => ({ cases: [item, ...s.cases] }));
    return item;
  },
  update: (id, patch) => set((s) => ({ cases: s.cases.map((c) => (c.id === id ? { ...c, ...patch } : c)) })),
  note: (id, text, by) => set((s) => ({ cases: s.cases.map((c) => (c.id === id ? { ...c, notes: [...c.notes, { at: new Date().toISOString(), by, text }] } : c)) })),
}));

export const daysTo = (d?: string) => (d ? dayjs(d).diff(dayjs().startOf('day'), 'day') : undefined);
