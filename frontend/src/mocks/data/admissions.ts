import dayjs from 'dayjs';
import type { AssignmentLog, Campaign, LeadRule, PlacementAttempt, PlacementTest } from '@/features/crm/types';
import { grade, pickQuestions, simulateAnswers } from '@/features/crm/admission/placement';
import { MOCK_QUESTIONS } from './questions';

/**
 * Dữ liệu giả — Tuyển sinh: quy tắc chia lead, bài test xếp lớp + lượt làm bài, chiến dịch marketing.
 * Lead được gắn chiến dịch theo nguồn trong mocks/data/crm.ts.
 */

const ago = (min: number) => dayjs().subtract(min, 'minute').toISOString();
const day = (n: number) => dayjs().add(n, 'day').format('YYYY-MM-DD');

// ---------------- Quy tắc chia lead (thứ tự = độ ưu tiên) ----------------

export const MOCK_LEAD_RULES: LeadRule[] = [
  {
    id: 'R-1', name: 'XKLĐ / đi làm → chuyên viên', active: true,
    sources: [], branchIds: [], interests: ['xkld'],
    method: 'fixed', saleIds: ['e-vy', 'e-bao'], onlyOnShift: true,
    slaMinutes: 30, escalateTo: ['Quản lý chi nhánh'], reassignOnBreach: false, cursor: 0,
  },
  {
    id: 'R-2', name: 'Cầu Giấy — xoay vòng trong ca', active: true,
    sources: [], branchIds: ['cg'], interests: [],
    method: 'round_robin', saleIds: ['e-tung', 'e-bao', 'e-nhu'], onlyOnShift: true,
    slaMinutes: 15, escalateTo: ['Trưởng nhóm Sale', 'Quản lý chi nhánh'], reassignOnBreach: true, cursor: 1,
  },
  {
    id: 'R-3', name: 'Quận 1 — theo tải', active: true,
    sources: [], branchIds: ['q1'], interests: [],
    method: 'least_load', saleIds: ['e-khoa', 'e-han'], onlyOnShift: true,
    slaMinutes: 15, escalateTo: ['Quản lý chi nhánh'], reassignOnBreach: false, cursor: 0,
  },
  {
    id: 'R-4', name: 'Hải Châu', active: true,
    sources: [], branchIds: ['hc'], interests: [],
    method: 'fixed', saleIds: ['e-vy'], onlyOnShift: false,
    slaMinutes: 20, escalateTo: ['Quản lý chi nhánh'], reassignOnBreach: false, cursor: 0,
  },
  {
    id: 'R-5', name: 'Lead hội thảo — SLA nới', active: false,
    sources: ['event'], branchIds: [], interests: [],
    method: 'round_robin', saleIds: ['e-khoa', 'e-han', 'e-tung', 'e-bao', 'e-nhu', 'e-vy'], onlyOnShift: false,
    slaMinutes: 120, escalateTo: ['Giám đốc tuyển sinh'], reassignOnBreach: false, cursor: 0,
  },
];

export const MOCK_ASSIGN_LOGS: AssignmentLog[] = [
  { id: 'AL-1', leadId: 'L-0005', saleId: 'e-tung', ruleId: 'R-2', at: ago(6), reason: 'Quy tắc "Cầu Giấy — xoay vòng trong ca" → Nguyễn Thanh Tùng (1/3 Sale đang trong ca)' },
  { id: 'AL-2', leadId: 'L-0004', saleId: 'e-khoa', ruleId: 'R-3', at: ago(48), reason: 'Quy tắc "Quận 1 — theo tải" → Trần Minh Khoa (đang giữ 9 lead mở — ít nhất)' },
  { id: 'AL-3', leadId: 'L-0007', saleId: 'e-bao', ruleId: 'R-2', at: ago(180), reason: 'Quy tắc "Cầu Giấy — xoay vòng trong ca" → Đinh Quốc Bảo (2/3 Sale đang trong ca)' },
  { id: 'AL-4', leadId: 'L-0014', saleId: 'e-tung', ruleId: 'R-2', at: ago(60 * 9), reason: 'Quy tắc "Cầu Giấy — xoay vòng trong ca" → Nguyễn Thanh Tùng (không ai trong ca → chia cho cả nhóm)' },
  { id: 'AL-5', leadId: 'L-0013', saleId: 'e-vy', ruleId: 'R-1', at: ago(60 * 26), reason: 'Quy tắc "XKLĐ / đi làm → chuyên viên" → Lý Thanh Vy (1/2 Sale đang trong ca)' },
];

// ---------------- Test xếp lớp ----------------

const test = (t: Omit<PlacementTest, 'questionIds'>): PlacementTest => ({ ...t, questionIds: pickQuestions(MOCK_QUESTIONS, t.sections) });

export const MOCK_PLACEMENT_TESTS: PlacementTest[] = [
  test({
    id: 'PT-01', code: 'PT-SC', name: 'Test đầu vào Sơ cấp (N5–N4)',
    description: 'Cho người đã học qua tiếng Nhật, muốn biết nên vào N5 hay học tiếp N4 / N3.',
    sections: [
      { level: 'N5', skill: 'vocab', count: 3 },
      { level: 'N5', skill: 'kanji', count: 2 },
      { level: 'N5', skill: 'grammar', count: 3 },
      { level: 'N4', skill: 'vocab', count: 2 },
      { level: 'N4', skill: 'grammar', count: 3 },
    ],
    durationMin: 25,
    bands: [
      { minPct: 0, level: 'Chưa có nền tảng', course: 'n5' },
      { minPct: 45, level: 'Tương đương N5', course: 'n4' },
      { minPct: 80, level: 'Tương đương N4', course: 'n3' },
    ],
    status: 'active', author: 'Trần Mai Anh', createdAt: ago(60 * 24 * 90),
  }),
  test({
    id: 'PT-02', code: 'PT-TC', name: 'Test đầu vào Trung cấp (N3–N2)',
    description: 'Cho người đã có N4 trở lên, xếp vào lớp N3 / N2 / N1.',
    sections: [
      { level: 'N4', skill: 'grammar', count: 1 },
      { level: 'N3', skill: 'vocab', count: 2 },
      { level: 'N3', skill: 'kanji', count: 2 },
      { level: 'N3', skill: 'grammar', count: 3 },
      { level: 'N2', skill: 'vocab', count: 1 },
      { level: 'N2', skill: 'grammar', count: 1 },
    ],
    durationMin: 30,
    bands: [
      { minPct: 0, level: 'Dưới N3', course: 'n3' },
      { minPct: 50, level: 'Tương đương N3', course: 'n2' },
      { minPct: 85, level: 'Tương đương N2', course: 'n1' },
    ],
    status: 'active', author: 'Nguyễn Thu Hà', createdAt: ago(60 * 24 * 60),
  }),
  test({
    id: 'PT-03', code: 'PT-KID', name: 'Test thiếu nhi (8–12 tuổi)',
    description: 'Bài ngắn, chủ yếu từ vựng & chữ cái — đang soạn thêm câu có hình.',
    sections: [
      { level: 'N5', skill: 'vocab', count: 3 },
      { level: 'N5', skill: 'kanji', count: 1 },
    ],
    durationMin: 15,
    bands: [
      { minPct: 0, level: 'Mới bắt đầu', course: 'kids' },
      { minPct: 70, level: 'Đã biết chữ cái', course: 'kids' },
    ],
    status: 'draft', author: 'Trần Mai Anh', createdAt: ago(60 * 24 * 4),
  }),
];

const done = (id: string, testId: string, leadId: string, ability: number, sentMin: number, logged: boolean, sentBy: string): PlacementAttempt => {
  const t = MOCK_PLACEMENT_TESTS.find((x) => x.id === testId)!;
  const answers = simulateAnswers(t.questionIds, MOCK_QUESTIONS, ability, id.length * 97 + Math.round(ability * 1000));
  return {
    id, testId, leadId, token: `${testId.toLowerCase()}-${leadId.slice(2)}x${sentMin % 97}`, channel: 'zalo', sentBy,
    sentAt: ago(sentMin), expiresAt: ago(sentMin - 60 * 24 * 3), status: 'done',
    startedAt: ago(sentMin - 90), submittedAt: ago(sentMin - 90 - t.durationMin + 3),
    answers, ...grade(t, answers, MOCK_QUESTIONS), logged,
  };
};

const pending = (a: Omit<PlacementAttempt, 'status'> & { status?: PlacementAttempt['status'] }): PlacementAttempt => ({ status: 'sent', ...a });

export const MOCK_ATTEMPTS: PlacementAttempt[] = [
  done('AT-1', 'PT-01', 'L-0010', 0.55, 60 * 24 * 3, true, 'Trần Minh Khoa'),
  done('AT-2', 'PT-02', 'L-0017', 0.72, 60 * 20, false, 'Đinh Quốc Bảo'),
  pending({ id: 'AT-3', testId: 'PT-01', leadId: 'L-0024', token: 'pt-01-0024k7', channel: 'sms', sentBy: 'Lý Thanh Vy', sentAt: ago(70), expiresAt: ago(-60 * 24 * 3), status: 'in_progress', startedAt: ago(9) }),
  pending({ id: 'AT-4', testId: 'PT-01', leadId: 'L-0031', token: 'pt-01-0031q2', channel: 'zalo', sentBy: 'Nguyễn Thanh Tùng', sentAt: ago(60 * 26), expiresAt: ago(-60 * 46) }),
  pending({ id: 'AT-5', testId: 'PT-02', leadId: 'L-0038', token: 'pt-02-0038m1', channel: 'email', sentBy: 'Trần Minh Khoa', sentAt: ago(60 * 24 * 6), expiresAt: ago(60 * 24 * 3), status: 'expired' }),
  done('AT-6', 'PT-01', 'L-0011', 0.28, 60 * 24 * 12, true, 'Lý Thanh Vy'),
  done('AT-7', 'PT-01', 'L-0018', 0.85, 60 * 24 * 9, true, 'Nguyễn Thanh Tùng'),
  pending({ id: 'AT-8', testId: 'PT-02', leadId: 'L-0045', token: 'pt-02-0045z9', channel: 'zalo', sentBy: 'Đinh Quốc Bảo', sentAt: ago(120), expiresAt: ago(-60 * 24 * 3 + 120) }),
];

// ---------------- Chiến dịch ----------------

export const MOCK_CAMPAIGNS: Campaign[] = [
  {
    id: 'CP-01', code: 'FB-N5-T11', name: 'Facebook Lead Ads — Khai giảng N5 tháng 11', channel: 'facebook',
    branchIds: ['cg', 'q1', 'hc'], startDate: day(-35), endDate: day(15), budget: 6_000_000, spent: 3_850_000, targetLeads: 12,
    status: 'running', owner: 'Ngô Bảo Anh', utm: 'fb_n5_t11', courses: ['n5'], note: 'Ưu đãi giảm 10% đăng ký trước khai giảng 7 ngày.', createdAt: ago(60 * 24 * 40),
  },
  {
    id: 'CP-02', code: 'TT-KAIWA', name: 'TikTok — Kaiwa cho người đi làm', channel: 'tiktok',
    branchIds: ['cg', 'q1'], startDate: day(-28), endDate: day(2), budget: 4_000_000, spent: 2_640_000, targetLeads: 10,
    status: 'running', owner: 'Ngô Bảo Anh', utm: 'tt_kaiwa', courses: ['kaiwa'], createdAt: ago(60 * 24 * 30),
  },
  {
    id: 'CP-03', code: 'EV-DUHOC-10', name: 'Hội thảo Du học & Việc làm Nhật Bản', channel: 'event',
    branchIds: ['cg', 'hc'], startDate: day(-45), endDate: day(-20), budget: 5_000_000, spent: 4_920_000, targetLeads: 8,
    status: 'ended', owner: 'Ngô Đức Thịnh', courses: ['xkld', 'n4'], note: 'Thuê hội trường + in tài liệu + quà tặng.', createdAt: ago(60 * 24 * 60),
  },
  {
    id: 'CP-04', code: 'GG-SEARCH', name: 'Google Ads — từ khóa "học tiếng Nhật"', channel: 'website',
    branchIds: ['cg', 'q1', 'hc'], startDate: day(-60), endDate: day(30), budget: 5_000_000, spent: 1_980_000, targetLeads: 6,
    status: 'running', owner: 'Ngô Đức Thịnh', utm: 'gg_search', courses: ['n5', 'n4', 'n3'], createdAt: ago(60 * 24 * 62),
  },
  {
    id: 'CP-05', code: 'ZL-REF', name: 'Zalo OA — Học viên cũ giới thiệu bạn', channel: 'zalo',
    branchIds: ['cg', 'q1', 'hc'], startDate: day(-30), endDate: day(30), budget: 1_500_000, spent: 600_000, targetLeads: 8,
    status: 'paused', owner: 'Ngô Bảo Anh', utm: 'zl_ref', courses: [], note: 'Tạm dừng chờ duyệt mẫu tin ZNS mới.', createdAt: ago(60 * 24 * 31),
  },
  {
    id: 'CP-06', code: 'FB-TET27', name: 'Tết 2027 — Early bird khóa N4/N3', channel: 'facebook',
    branchIds: ['cg', 'q1'], startDate: day(20), endDate: day(75), budget: 10_000_000, spent: 0, targetLeads: 25,
    status: 'planned', owner: 'Ngô Bảo Anh', utm: 'fb_tet27', courses: ['n4', 'n3'], createdAt: ago(60 * 24 * 2),
  },
];
