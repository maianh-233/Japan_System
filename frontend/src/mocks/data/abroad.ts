import dayjs from 'dayjs';
import type { AbroadCase, DocState, Program, Stage } from '@/features/student/abroad/abroad';
import { docTemplate } from '@/features/student/abroad/docs';

/** Dữ liệu giả — hồ sơ Du học / XKLĐ ở đủ các bước, 1 hồ sơ bị hủy (trượt phỏng vấn), 1 hồ sơ sắp bay. */

const d = (n: number) => dayjs().add(n, 'day').format('YYYY-MM-DD');
const ago = (n: number) => dayjs().subtract(n, 'day').toISOString();

interface Seed {
  studentId: string;
  program: Program;
  partner: string;
  destination: string;
  job?: string;
  intake: string;
  stage: Stage;
  docsDone: number;
  fee: [number, number];
  assignee: string;
  interview?: AbroadCase['interview'];
  coe?: AbroadCase['coe'];
  visa?: AbroadCase['visa'];
  flightDate?: string;
  cancelled?: AbroadCase['cancelled'];
  notes?: string[];
  age: number;
}

const VY = 'Lý Thanh Vy (Tư vấn du học)';
const HANH = 'Võ Thị Hạnh (Phụ trách XKLĐ)';

const seeds: Seed[] = [
  { studentId: 'ST-4', program: 'tts', partner: 'Nghiệp đoàn Chubu Kyodo', destination: 'Aichi', job: 'Chế biến thực phẩm', intake: '2027-03', stage: 'coe', docsDone: 8, fee: [120_000_000, 90_000_000], assignee: HANH, interview: { date: d(-50), result: 'pass' }, coe: { submittedAt: d(-30) }, notes: ['Đỗ phỏng vấn đơn hàng bánh kẹo Aichi (3 năm).', 'Đã nộp hồ sơ COE qua nghiệp đoàn.'], age: 120 },
  { studentId: 'ST-3', program: 'engineer', partner: 'Công ty TNHH Sakura IT Solutions', destination: 'Tokyo', job: 'Kỹ sư phần mềm', intake: '2027-01', stage: 'visa', docsDone: 8, fee: [35_000_000, 35_000_000], assignee: VY, interview: { date: d(-75), result: 'pass' }, coe: { submittedAt: d(-60), grantedAt: d(-6) }, visa: { appliedAt: d(-2) }, notes: ['Đã nhận COE bản gốc qua bưu điện.', 'Nộp visa tại ĐSQ Nhật Hà Nội.'], age: 150 },
  { studentId: 'ST-6', program: 'study', partner: 'Trường Nhật ngữ Osaka YMCA', destination: 'Osaka', intake: '2027-04', stage: 'coe', docsDone: 7, fee: [180_000_000, 120_000_000], assignee: VY, interview: { date: d(-20), result: 'pass' }, coe: { submittedAt: d(-12) }, notes: ['Phỏng vấn online với trường — đạt.', 'Kỳ tháng 4/2027: Cục XNC trả COE khoảng cuối tháng 2.'], age: 90 },
  { studentId: 'ST-9', program: 'study', partner: 'Học viện Nhật ngữ Fukuoka', destination: 'Fukuoka', intake: '2027-04', stage: 'documents', docsDone: 4, fee: [170_000_000, 30_000_000], assignee: VY, notes: ['Thiếu sổ tiết kiệm + xác nhận thu nhập của bố.'], age: 40 },
  { studentId: 'ST-12', program: 'tokutei', partner: 'Công ty Kaigo Hikari', destination: 'Saitama', job: 'Điều dưỡng (Kaigo)', intake: '2027-02', stage: 'interview', docsDone: 7, fee: [60_000_000, 30_000_000], assignee: HANH, interview: { date: d(5) }, notes: ['Đã đỗ kỳ thi kỹ năng Kaigo + tiếng Nhật JFT-Basic.'], age: 60 },
  { studentId: 'ST-15', program: 'tts', partner: 'Nghiệp đoàn Kyushu Build', destination: 'Kumamoto', job: 'Xây dựng — cốp pha', intake: '2027-05', stage: 'documents', docsDone: 3, fee: [110_000_000, 20_000_000], assignee: HANH, age: 25 },
  { studentId: 'ST-18', program: 'study', partner: 'Trường Nhật ngữ ISI Tokyo', destination: 'Tokyo', intake: '2027-07', stage: 'consult', docsDone: 0, fee: [190_000_000, 0], assignee: VY, notes: ['Phụ huynh muốn kỳ tháng 7 hoặc tháng 10.'], age: 8 },
  { studentId: 'ST-21', program: 'tokutei', partner: 'Công ty Nhà hàng Washoku Group', destination: 'Kanagawa', job: 'Nhà hàng', intake: '2027-03', stage: 'consult', docsDone: 0, fee: [55_000_000, 5_000_000], assignee: HANH, age: 5 },
  { studentId: 'ST-24', program: 'tts', partner: 'Nghiệp đoàn Chubu Kyodo', destination: 'Gifu', job: 'Cơ khí', intake: '2026-12', stage: 'departed', docsDone: 8, fee: [115_000_000, 115_000_000], assignee: HANH, interview: { date: d(-200), result: 'pass' }, coe: { submittedAt: d(-150), grantedAt: d(-80) }, visa: { appliedAt: d(-60), issuedAt: d(-50) }, flightDate: d(-20), notes: ['Đã xuất cảnh, nghiệp đoàn đón tại sân bay Chubu.'], age: 260 },
  { studentId: 'ST-27', program: 'tts', partner: 'Nghiệp đoàn Chubu Kyodo', destination: 'Aichi', job: 'Chế biến thực phẩm', intake: '2026-12', stage: 'visa', docsDone: 8, fee: [120_000_000, 120_000_000], assignee: HANH, interview: { date: d(-120), result: 'pass' }, coe: { submittedAt: d(-90), grantedAt: d(-40) }, visa: { appliedAt: d(-20), issuedAt: d(-8) }, flightDate: d(12), notes: ['Đã có visa. Vé máy bay HAN → NGO.'], age: 200 },
  { studentId: 'ST-30', program: 'tts', partner: 'Nghiệp đoàn Kyushu Build', destination: 'Fukuoka', job: 'Xây dựng', intake: '2027-03', stage: 'interview', docsDone: 6, fee: [110_000_000, 20_000_000], assignee: HANH, interview: { date: d(-10), result: 'fail' }, cancelled: { reason: 'Trượt phỏng vấn đơn hàng — chờ đơn hàng khác', at: ago(9) }, age: 70 },
  { studentId: 'ST-33', program: 'study', partner: 'Trường Nhật ngữ Kyoto Minsai', destination: 'Kyoto', intake: '2027-04', stage: 'interview', docsDone: 7, fee: [175_000_000, 50_000_000], assignee: VY, interview: { date: d(2) }, age: 50 },
];

export const MOCK_ABROAD: AbroadCase[] = seeds.map((s, i) => ({
  id: `AB-${i + 1}`,
  studentId: s.studentId,
  program: s.program,
  partner: s.partner,
  destination: s.destination,
  job: s.job,
  intake: s.intake,
  jlptRequired: s.program === 'study' ? 'N5' : s.program === 'engineer' ? 'N3' : 'N4',
  stage: s.stage,
  cancelled: s.cancelled,
  docs: docTemplate(s.program).map((name, k): { name: string; state: DocState } => ({ name, state: k < s.docsDone ? 'verified' : k === s.docsDone ? 'received' : 'missing' })),
  interview: s.interview,
  coe: s.coe,
  visa: s.visa,
  flightDate: s.flightDate,
  fee: { total: s.fee[0], paid: s.fee[1] },
  assignee: s.assignee,
  notes: (s.notes ?? []).map((text, k) => ({ text, by: s.assignee, at: ago(Math.max(1, s.age - (k + 1) * 15)) })),
  createdAt: ago(s.age),
}));
