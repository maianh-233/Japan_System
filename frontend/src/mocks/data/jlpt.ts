import dayjs from 'dayjs';
import type { QLevel } from '@/features/academic/questions/types';
import type { JlptReg, JlptStatus } from '@/features/student/jlpt/jlpt';
import { MOCK_CLASSES } from './classes';
import { MOCK_ENROLLMENTS, MOCK_STUDENTS } from './students';

/**
 * Dữ liệu giả — Đăng ký thi JLPT.
 *  - Kỳ 12/2026 (sắp thi): đủ các bước — thiếu hồ sơ, chưa đóng lệ phí, đã nộp hội đồng, có phiếu dự thi
 *  - Kỳ 07/2026 & 12/2025: đã có kết quả (đỗ / trượt điểm liệt / trượt điểm tổng / vắng thi)
 */

const LEVEL_OF: Record<string, QLevel> = { 'CRS-01': 'N5', 'CRS-02': 'N4', 'CRS-03': 'N3', 'CRS-04': 'N2', 'CRS-05': 'N3', 'CRS-07': 'N4', 'CRS-06': 'N5' };
const VENUE_OF: Record<string, string> = { cg: 'Hà Nội — ĐH Ngoại ngữ (ĐHQGHN)', q1: 'TP.HCM — ĐH KHXH&NV', hc: 'Đà Nẵng — ĐH Ngoại ngữ Đà Nẵng' };

const ago = (d: number) => dayjs().subtract(d, 'day').toISOString();

/** Điểm giả theo "năng lực" 0..1 */
function scoresFor(level: QLevel, skill: number, i: number): number[] {
  const base = level === 'N4' || level === 'N5' ? [120, 60] : [60, 60, 60];
  return base.map((max, k) => Math.max(5, Math.min(max, Math.round(max * (skill + (((i + k * 3) % 5) - 2) / 20)))));
}

const regs: JlptReg[] = [];
let n = 0;
const push = (r: Omit<JlptReg, 'id' | 'createdAt'>, age: number) => regs.push({ ...r, id: `JR-${++n}`, createdAt: ago(age) });

// ---- Kỳ 12/2026: học viên đang học các lớp JLPT
const studying = MOCK_ENROLLMENTS.filter((e) => e.status === 'studying');
studying.slice(0, 26).forEach((e, i) => {
  const cls = MOCK_CLASSES.find((c) => c.id === e.classId);
  const st = MOCK_STUDENTS.find((s) => s.id === e.studentId);
  if (!cls || !st) return;
  const level = LEVEL_OF[cls.courseId] ?? 'N4';
  const status: JlptStatus = (['admit', 'submitted', 'paid', 'registered', 'admit', 'admit', 'cancelled'] as const)[i % 7];
  const docsOk = status !== 'registered' || i % 2 === 0;
  push(
    {
      studentId: st.id,
      sessionId: '2026-12',
      level,
      venue: VENUE_OF[cls.branchId],
      status,
      docs: { photo: docsOk || i % 3 === 0, idCard: docsOk, form: docsOk && i % 4 !== 3 },
      paidAt: ['paid', 'submitted', 'admit'].includes(status) ? ago(100 - i) : undefined,
      admitNo: status === 'admit' ? `${level}${String(10_000 + i * 37).slice(0, 5)}-${String(i + 1).padStart(4, '0')}` : undefined,
      note: status === 'cancelled' ? 'Đổi sang thi kỳ 7/2027 — chưa đủ thời gian ôn' : undefined,
    },
    110 - i,
  );
});

// ---- Kỳ 7/2026 và 12/2025: đã có kết quả
(['2026-07', '2025-12'] as const).forEach((sessionId, si) => {
  MOCK_STUDENTS.slice(si * 6, si * 6 + 18).forEach((st, i) => {
    const enr = MOCK_ENROLLMENTS.find((e) => e.studentId === st.id);
    const cls = enr && MOCK_CLASSES.find((c) => c.id === enr.classId);
    if (!cls) return;
    const level = LEVEL_OF[cls.courseId] ?? 'N4';
    const absent = i % 9 === 5;
    const skill = 0.38 + ((i * 7 + si * 3) % 10) / 22;
    push(
      {
        studentId: st.id,
        sessionId,
        level,
        venue: VENUE_OF[cls.branchId],
        status: absent ? 'absent' : 'done',
        docs: { photo: true, idCard: true, form: true },
        paidAt: ago(si ? 420 : 250),
        admitNo: `${level}${String(20_000 + i * 53).slice(0, 5)}-${String(i + 1).padStart(4, '0')}`,
        // 1 trường hợp đủ điểm tổng nhưng trượt vì điểm liệt phần Nghe
        scores: absent ? undefined : i === 2 ? (level === 'N4' || level === 'N5' ? [105, 15] : [50, 48, 16]) : scoresFor(level, skill, i),
        note: st.id === 'ST-3' && sessionId === '2026-07' ? 'Đỗ N3 tháng 7' : undefined,
      },
      si ? 420 : 250,
    );
  });
});

export const MOCK_JLPT: JlptReg[] = regs;
