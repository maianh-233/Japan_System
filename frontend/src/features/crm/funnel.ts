import dayjs from 'dayjs';
import type { Appointment } from './appointments/appointments';
import type { Consultation, Lead, LeadStudentLink, PlacementAttempt } from './types';

/**
 * PHỄU CHUYỂN ĐỔI: Lead → Đã gọi → Test → Chốt
 * Mốc thời gian mỗi bước lấy từ dữ liệu thật (không cần nhập tay):
 *  - Lead:    ngày tạo lead
 *  - Đã gọi:  lần tư vấn đầu tiên liên lạc được (không tính "không nghe máy" và ghi chú hệ thống)
 *  - Test:    sớm nhất trong: tư vấn "Hẹn test", gửi link test online, đặt lịch test tại trung tâm
 *  - Chốt:    tư vấn "Đã đăng ký học" hoặc ngày tạo hồ sơ học viên từ lead
 * Lead bỏ qua 1 bước (vd chốt luôn không test) vẫn tính là ĐÃ QUA bước đó, nhưng không có thời gian → không vào trung bình.
 * "Kẹt": lead còn mở (chưa chốt / chưa mất) nằm ở 1 bước quá lâu (STUCK_DAYS).
 */

export type FunnelStage = 'lead' | 'called' | 'test' | 'won';
export const STAGES: FunnelStage[] = ['lead', 'called', 'test', 'won'];
export const STAGE_LABEL: Record<FunnelStage, string> = { lead: 'Lead', called: 'Đã gọi', test: 'Test', won: 'Chốt' };
/** Nằm ở bước này quá X ngày (chưa sang bước sau) → coi là kẹt */
export const STUCK_DAYS: Record<Exclude<FunnelStage, 'won'>, number> = { lead: 1, called: 7, test: 7 };

export interface LeadJourney {
  lead: Lead;
  /** Thời điểm đạt từng bước (có thể thiếu nếu bỏ qua bước) */
  at: Partial<Record<FunnelStage, string>>;
  reached: Record<FunnelStage, boolean>;
  /** Bước cao nhất đã đạt */
  stage: FunnelStage;
  lost: boolean;
  /** Đang kẹt ở bước hiện tại (số ngày) */
  stuckDays?: number;
}

const minIso = (xs: (string | undefined)[]) => xs.filter(Boolean).sort()[0] as string | undefined;

export function journeyOf(
  lead: Lead,
  ctx: { consults: Consultation[]; attempts: PlacementAttempt[]; appointments: Appointment[]; links: LeadStudentLink[] },
  now = dayjs(),
): LeadJourney {
  const cs = ctx.consults.filter((c) => !c.system);
  const called = minIso(cs.filter((c) => c.result !== 'no_answer').map((c) => c.createdAt)) ?? (lead.status !== 'new' ? lead.lastContactAt : undefined);
  const test = minIso([
    ...cs.filter((c) => c.result === 'test_booked').map((c) => c.createdAt),
    ...ctx.attempts.map((a) => a.sentAt),
    ...ctx.appointments.filter((a) => a.kind === 'test' && a.status !== 'cancelled').map((a) => a.createdAt),
  ]);
  const won = lead.status === 'won' ? minIso([...ctx.consults.filter((c) => c.result === 'enrolled').map((c) => c.createdAt), ...ctx.links.map((k) => k.createdAt)]) : undefined;

  const reachedWon = lead.status === 'won';
  const reachedTest = reachedWon || lead.status === 'test_scheduled' || Boolean(test);
  const reachedCalled = reachedTest || Boolean(called) || lead.status !== 'new';
  const reached = { lead: true, called: reachedCalled, test: reachedTest, won: reachedWon };
  const stage: FunnelStage = reachedWon ? 'won' : reachedTest ? 'test' : reachedCalled ? 'called' : 'lead';
  const at: LeadJourney['at'] = { lead: lead.createdAt, called, test, won };
  const lost = lead.status === 'lost';

  let stuckDays: number | undefined;
  if (!lost && stage !== 'won' && !lead.hidden) {
    const since = at[stage] ?? lead.createdAt;
    const days = now.diff(dayjs(since), 'hour') / 24;
    if (days >= STUCK_DAYS[stage]) stuckDays = Math.floor(days);
  }
  return { lead, at, reached, stage, lost, stuckDays };
}

export interface FunnelSummary {
  total: number;
  count: Record<FunnelStage, number>;
  /** Tỉ lệ sang bước kế (index = bước trước) */
  stepRate: (number | undefined)[];
  /** Giờ trung bình từ bước i → i+1 (chỉ lead có đủ 2 mốc) */
  avgHours: (number | undefined)[];
  /** Số lead dùng để tính trung bình */
  avgN: number[];
  stuck: Record<Exclude<FunnelStage, 'won'>, LeadJourney[]>;
  lost: number;
  /** Bước có tỉ lệ chuyển thấp nhất (đủ dữ liệu) — "nghẽn" */
  weakestStep?: number;
  /** Bước có nhiều lead kẹt nhất */
  mostStuck?: Exclude<FunnelStage, 'won'>;
}

/** Tối thiểu lead ở bước trước để xét "nghẽn" — tránh kết luận từ 1–2 lead */
const MIN_FOR_RATE = 3;

export function summarize(js: LeadJourney[]): FunnelSummary {
  const count = { lead: 0, called: 0, test: 0, won: 0 } as Record<FunnelStage, number>;
  js.forEach((j) => STAGES.forEach((s) => j.reached[s] && count[s]++));
  const stepRate = STAGES.slice(0, -1).map((s, i) => (count[s] ? count[STAGES[i + 1]] / count[s] : undefined));
  const avgN: number[] = [];
  const avgHours = STAGES.slice(0, -1).map((s, i) => {
    const next = STAGES[i + 1];
    const diffs = js.filter((j) => j.at[s] && j.at[next]).map((j) => Math.max(0, dayjs(j.at[next]).diff(dayjs(j.at[s]), 'minute') / 60));
    avgN.push(diffs.length);
    return diffs.length ? diffs.reduce((a, b) => a + b, 0) / diffs.length : undefined;
  });
  const stuck = {
    lead: js.filter((j) => j.stuckDays !== undefined && j.stage === 'lead'),
    called: js.filter((j) => j.stuckDays !== undefined && j.stage === 'called'),
    test: js.filter((j) => j.stuckDays !== undefined && j.stage === 'test'),
  };
  let weakestStep: number | undefined;
  stepRate.forEach((r, i) => {
    if (r === undefined || count[STAGES[i]] < MIN_FOR_RATE) return;
    if (weakestStep === undefined || r < stepRate[weakestStep]!) weakestStep = i;
  });
  const mostStuck = (Object.keys(stuck) as (keyof typeof stuck)[]).reduce<keyof typeof stuck | undefined>((best, k) => (stuck[k].length && (!best || stuck[k].length > stuck[best].length) ? k : best), undefined);
  return { total: js.length, count, stepRate, avgHours, avgN, stuck, lost: js.filter((j) => j.lost).length, weakestStep, mostStuck };
}

/** "3,5 giờ" / "2,1 ngày" */
export function fmtDuration(hours?: number): string {
  if (hours === undefined) return '—';
  if (hours < 1) return `${Math.max(1, Math.round(hours * 60))} phút`;
  if (hours < 48) return `${(Math.round(hours * 10) / 10).toLocaleString('vi-VN')} giờ`;
  return `${(Math.round((hours / 24) * 10) / 10).toLocaleString('vi-VN')} ngày`;
}
