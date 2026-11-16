import dayjs from 'dayjs';
import type { Department, Employee, EmployeeKind, EmployeeStatus, ShiftKey, TeacherProfile } from '@/features/hr/types';

/**
 * Dữ liệu giả — Nhân sự. Giáo viên / trợ giảng ở đây là nguồn duy nhất cho phần Đào tạo (xếp lớp, dạy thay).
 * Tình huống: NV thử việc, hợp đồng sắp hết hạn, nghỉ dài hạn, đã nghỉ việc, chưa có tài khoản, NV làm theo ca.
 */

const d = (n: number) => dayjs().add(n, 'day').format('YYYY-MM-DD');
const ago = (n: number) => dayjs().subtract(n, 'day').format('YYYY-MM-DD');

let seq = 0;
interface Seed {
  id: string;
  name: string;
  gender: 'male' | 'female';
  branchId: string;
  department: Department;
  position: string;
  kind?: EmployeeKind;
  shift?: boolean;
  status?: EmployeeStatus;
  joined: number;
  contract: Employee['contract'];
  leave?: number;
  account?: boolean;
  manager?: string;
  teacher?: TeacherProfile;
  note?: string;
}

const fixed = (start: number, months: number, salary: number, allowance = 0): Employee['contract'] => ({
  type: 'fixed',
  no: '',
  start: ago(start),
  end: dayjs().subtract(start, 'day').add(months, 'month').format('YYYY-MM-DD'),
  baseSalary: salary,
  allowance,
});

const avail = (m: Partial<Record<number, ShiftKey[]>>): Record<number, ShiftKey[]> => ({ 1: [], 2: [], 3: [], 4: [], 5: [], 6: [], 7: [], ...m });

const seeds: Seed[] = [
  // ---- Ban giám đốc / văn phòng
  { id: 'e-huy', name: 'Đỗ Quang Huy', gender: 'male', branchId: 'cg', department: 'board', position: 'Giám đốc', joined: 1500, contract: { type: 'indefinite', no: '', start: ago(1500), baseSalary: 45_000_000 } },
  { id: 'e-trang', name: 'Phạm Thu Trang', gender: 'female', branchId: 'cg', department: 'board', position: 'Phó giám đốc vận hành', joined: 1100, contract: { type: 'indefinite', no: '', start: ago(1100), baseSalary: 32_000_000 }, manager: 'e-huy' },
  { id: 'e-nam', name: 'Lê Hoàng Nam', gender: 'male', branchId: 'cg', department: 'board', position: 'Quản lý chi nhánh Cầu Giấy', joined: 800, contract: { type: 'indefinite', no: '', start: ago(800), baseSalary: 22_000_000, allowance: 2_000_000 }, manager: 'e-trang' },
  { id: 'e-q1m', name: 'Châu Minh Đức', gender: 'male', branchId: 'q1', department: 'board', position: 'Quản lý chi nhánh Quận 1', joined: 700, contract: { type: 'indefinite', no: '', start: ago(700), baseSalary: 22_000_000, allowance: 2_000_000 }, manager: 'e-trang' },
  { id: 'e-hcm', name: 'Lâm Thị Hiền', gender: 'female', branchId: 'hc', department: 'board', position: 'Quản lý chi nhánh Hải Châu', joined: 520, contract: fixed(520, 36, 18_000_000, 1_500_000), manager: 'e-trang' },
  { id: 'e-mai', name: 'Vũ Ngọc Mai', gender: 'female', branchId: 'cg', department: 'academic', position: 'Giáo vụ', joined: 600, contract: fixed(600, 24, 11_000_000, 800_000), manager: 'e-nam', leave: 7 },
  { id: 'e-hong', name: 'Phạm Thị Hồng', gender: 'female', branchId: 'cg', department: 'academic', position: 'Giáo vụ', joined: 700, contract: fixed(700, 24, 11_500_000, 800_000), manager: 'e-nam', leave: 4, note: 'Hợp đồng sắp hết — cân nhắc ký không thời hạn.' },
  { id: 'e-chau', name: 'Phan Minh Châu', gender: 'female', branchId: 'q1', department: 'academic', position: 'Giáo vụ', joined: 300, contract: fixed(300, 12, 10_500_000), manager: 'e-q1m' },
  { id: 'e-duyen', name: 'Trương Mỹ Duyên', gender: 'female', branchId: 'hc', department: 'academic', position: 'Giáo vụ', joined: 400, contract: fixed(400, 24, 9_500_000), manager: 'e-hcm' },
  { id: 'e-lan', name: 'Hoàng Thị Lan', gender: 'female', branchId: 'cg', department: 'finance', position: 'Kế toán trưởng', joined: 900, contract: { type: 'indefinite', no: '', start: ago(900), baseSalary: 18_000_000 }, manager: 'e-huy' },
  { id: 'e-lan2', name: 'Ngô Thị Lan', gender: 'female', branchId: 'cg', department: 'finance', position: 'Kế toán thu ngân', joined: 350, contract: fixed(350, 12, 10_000_000), manager: 'e-lan' },
  { id: 'e-baoanh', name: 'Ngô Bảo Anh', gender: 'female', branchId: 'cg', department: 'marketing', position: 'Trưởng nhóm Marketing', joined: 450, contract: fixed(450, 24, 16_000_000), manager: 'e-trang' },
  { id: 'e-tuan', name: 'Phạm Anh Tuấn', gender: 'male', branchId: 'cg', department: 'it', position: 'IT nội bộ / cơ sở vật chất', joined: 250, contract: fixed(250, 12, 12_000_000), manager: 'e-nam', account: false },
  // ---- Làm theo ca: Sale + lễ tân
  { id: 'e-bao', name: 'Đinh Quốc Bảo', gender: 'male', branchId: 'cg', department: 'sales', position: 'Tư vấn tuyển sinh', shift: true, joined: 380, contract: fixed(380, 12, 8_000_000), manager: 'e-nam', leave: 9 },
  { id: 'e-tung', name: 'Nguyễn Thanh Tùng', gender: 'male', branchId: 'cg', department: 'sales', position: 'Tư vấn tuyển sinh', shift: true, joined: 200, contract: fixed(200, 12, 8_000_000), manager: 'e-nam' },
  { id: 'e-nhu', name: 'Hồ Quỳnh Như', gender: 'female', branchId: 'cg', department: 'sales', position: 'Tư vấn tuyển sinh', shift: true, status: 'probation', joined: 35, contract: { type: 'probation', no: '', start: ago(35), end: d(25), baseSalary: 6_800_000 }, manager: 'e-nam', leave: 0 },
  { id: 'e-hang', name: 'Đặng Thu Hằng', gender: 'female', branchId: 'cg', department: 'reception', position: 'Lễ tân', shift: true, joined: 500, contract: fixed(500, 24, 7_500_000), manager: 'e-nam' },
  { id: 'e-khoa', name: 'Trần Minh Khoa', gender: 'male', branchId: 'q1', department: 'sales', position: 'Tư vấn tuyển sinh', shift: true, joined: 420, contract: fixed(420, 12, 8_500_000), manager: 'e-q1m' },
  { id: 'e-han', name: 'Bùi Ngọc Hân', gender: 'female', branchId: 'q1', department: 'sales', position: 'Tư vấn tuyển sinh', shift: true, joined: 160, contract: fixed(160, 12, 8_000_000), manager: 'e-q1m' },
  { id: 'e-linh', name: 'Lưu Gia Linh', gender: 'female', branchId: 'q1', department: 'reception', position: 'Lễ tân', shift: true, status: 'leave', joined: 600, contract: fixed(600, 24, 7_500_000), manager: 'e-q1m', note: 'Nghỉ thai sản đến hết tháng 2.' },
  { id: 'e-vy', name: 'Lý Thanh Vy', gender: 'female', branchId: 'hc', department: 'sales', position: 'Tư vấn tuyển sinh & du học', shift: true, joined: 330, contract: fixed(330, 12, 8_500_000), manager: 'e-hcm' },
  { id: 'e-dat', name: 'Mai Văn Đạt', gender: 'male', branchId: 'q1', department: 'sales', position: 'Tư vấn tuyển sinh', status: 'quit', joined: 500, contract: fixed(500, 12, 8_000_000), manager: 'e-q1m', account: false, note: 'Nghỉ việc — đã bàn giao lead cho Bùi Ngọc Hân.' },
  // ---- Giáo viên
  { id: 't-ha', name: 'Nguyễn Thu Hà', gender: 'female', branchId: 'cg', department: 'teaching', position: 'Giáo viên — Tổ trưởng chuyên môn', kind: 'teacher', joined: 1000, contract: { type: 'indefinite', no: '', start: ago(1000), baseSalary: 6_000_000 }, teacher: { type: 'vn', levels: ['N5', 'N4', 'N3', 'N2'], certificates: ['JLPT N1 (2016)', 'Thạc sĩ Ngôn ngữ Nhật'], rate: { offline: 300_000, online: 270_000 }, extraBranchIds: ['q1'], availability: avail({ 1: ['evening'], 2: ['morning', 'evening'], 3: ['evening'], 4: ['morning', 'evening'], 5: ['evening'], 6: ['morning', 'afternoon'] }) } },
  { id: 't-anh', name: 'Trần Mai Anh', gender: 'female', branchId: 'cg', department: 'teaching', position: 'Giáo viên', kind: 'teacher', joined: 700, contract: fixed(700, 24, 0), teacher: { type: 'vn', levels: ['N5', 'N4'], certificates: ['JLPT N2 (2019)', 'Chứng chỉ sư phạm'], rate: { offline: 220_000, online: 200_000 }, availability: avail({ 1: ['morning'], 3: ['morning'], 5: ['morning'], 6: ['morning'], 7: ['morning', 'afternoon'] }) } },
  { id: 't-long', name: 'Phạm Đức Long', gender: 'male', branchId: 'cg', department: 'teaching', position: 'Giáo viên', kind: 'teacher', joined: 650, contract: fixed(650, 24, 0), teacher: { type: 'vn', levels: ['N5', 'N4', 'N3'], certificates: ['JLPT N1 (2020)'], rate: { offline: 250_000, online: 230_000 }, extraBranchIds: ['hc'], availability: avail({ 1: ['evening'], 2: ['evening'], 3: ['evening'], 4: ['evening'], 5: ['evening'] }) } },
  { id: 't-thu', name: 'Lê Minh Thư', gender: 'female', branchId: 'q1', department: 'teaching', position: 'Giáo viên', kind: 'teacher', joined: 500, contract: fixed(500, 24, 0), teacher: { type: 'vn', levels: ['N5', 'N4', 'N3'], certificates: ['JLPT N2 (2018)', 'Chứng chỉ sư phạm'], rate: { offline: 230_000, online: 210_000 }, availability: avail({ 1: ['evening'], 2: ['evening'], 3: ['evening'], 4: ['evening'], 5: ['evening'], 6: ['morning'] }) } },
  { id: 't-hanh', name: 'Võ Thị Hạnh', gender: 'female', branchId: 'hc', department: 'teaching', position: 'Giáo viên — phụ trách XKLĐ', kind: 'teacher', joined: 600, contract: fixed(600, 24, 4_000_000), teacher: { type: 'vn', levels: ['N5', 'N4'], certificates: ['JLPT N2 (2017)', 'Kinh nghiệm 3 năm tại Nhật'], rate: { offline: 220_000, online: 200_000 }, availability: avail({ 1: ['morning', 'afternoon'], 2: ['morning', 'afternoon'], 3: ['morning', 'afternoon'], 4: ['morning', 'afternoon'], 5: ['morning', 'afternoon'] }) } },
  { id: 't-sato', name: 'Sato Yuki', gender: 'female', branchId: 'q1', department: 'teaching', position: 'Giáo viên người Nhật', kind: 'teacher', joined: 400, contract: fixed(400, 12, 0), teacher: { type: 'jp', levels: ['N4', 'N3', 'N2', 'N1'], certificates: ['Chứng chỉ giáo viên tiếng Nhật 420 giờ', 'Đại học Waseda'], rate: { offline: 450_000, online: 420_000 }, extraBranchIds: ['cg'], availability: avail({ 2: ['afternoon', 'evening'], 4: ['afternoon', 'evening'], 6: ['morning'], 7: ['morning'] }), bio: 'Sống tại Việt Nam 4 năm, chuyên Kaiwa và tiếng Nhật thương mại.' } },
  { id: 't-tanaka', name: 'Tanaka Ken', gender: 'male', branchId: 'cg', department: 'teaching', position: 'Giáo viên người Nhật', kind: 'teacher', joined: 120, contract: { type: 'freelance', no: '', start: ago(120), end: d(18) }, teacher: { type: 'jp', levels: ['N3', 'N2', 'N1'], certificates: ['Kentei giáo viên tiếng Nhật'], rate: { offline: 420_000, online: 400_000 }, extraBranchIds: ['hc'], availability: avail({ 1: ['evening'], 2: ['evening'], 3: ['evening'], 4: ['evening'], 5: ['evening'], 6: ['afternoon'] }) }, note: 'Hợp đồng CTV hết hạn cuối tháng — đang đàm phán gia hạn.' },
  // ---- Trợ giảng
  { id: 'ta-linh', name: 'Đỗ Thùy Linh', gender: 'female', branchId: 'cg', department: 'teaching', position: 'Trợ giảng', kind: 'assistant', joined: 180, contract: { type: 'part_time', no: '', start: ago(180), end: d(185) }, teacher: { type: 'vn', levels: ['N5', 'N4'], certificates: ['JLPT N3'], rate: { offline: 90_000, online: 80_000 }, availability: avail({ 1: ['evening'], 3: ['evening'], 5: ['evening'], 6: ['morning'], 7: ['morning'] }) } },
  { id: 'ta-quan', name: 'Hà Minh Quân', gender: 'male', branchId: 'hc', department: 'teaching', position: 'Trợ giảng', kind: 'assistant', joined: 140, contract: { type: 'part_time', no: '', start: ago(140), end: d(220) }, teacher: { type: 'vn', levels: ['N5'], certificates: ['JLPT N3'], rate: { offline: 90_000, online: 80_000 }, availability: avail({ 1: ['morning'], 2: ['morning'], 3: ['morning'], 4: ['morning'], 5: ['morning'] }) } },
  { id: 'ta-mai', name: 'Lâm Ngọc Mai', gender: 'female', branchId: 'q1', department: 'teaching', position: 'Trợ giảng', kind: 'assistant', joined: 90, contract: { type: 'part_time', no: '', start: ago(90), end: d(270) }, teacher: { type: 'vn', levels: ['N5', 'N4', 'N3'], certificates: ['JLPT N2'], rate: { offline: 100_000, online: 90_000 }, extraBranchIds: ['cg'], availability: avail({ 1: ['evening'], 3: ['evening'], 5: ['evening'], 6: ['morning'] }) } },
];

export const MOCK_EMPLOYEES: Employee[] = seeds.map((s, i) => {
  seq++;
  const kind = s.kind ?? 'staff';
  return {
    id: s.id,
    code: `${kind === 'staff' ? 'NV' : kind === 'teacher' ? 'GV' : 'TG'}${String(100 + seq).padStart(4, '0')}`,
    fullName: s.name,
    gender: s.gender,
    dob: dayjs('1985-03-10').add(i * 397, 'day').format('YYYY-MM-DD'),
    phone: `09${String(81234567 + i * 13579).slice(0, 8)}`,
    email: `${s.name.normalize('NFD').replace(/[̀-ͯ]/g, '').replace(/đ/gi, 'd').toLowerCase().split(' ').slice(-1)[0]}.${s.id.replace(/^[a-z]+-/, '')}@jpcenter.vn`,
    branchId: s.branchId,
    kind,
    department: s.department,
    position: s.position,
    shiftBased: Boolean(s.shift),
    status: s.status ?? 'active',
    joinedAt: ago(s.joined),
    quitAt: s.status === 'quit' ? ago(20) : undefined,
    managerId: s.manager,
    contract: { ...s.contract, no: s.contract.no || `HĐ-${dayjs(s.contract.start).year()}/${String(seq).padStart(3, '0')}` },
    leaveBalance: s.leave ?? 12 - (i % 6),
    hasAccount: s.account ?? true,
    teacher: s.teacher,
    note: s.note,
  };
});
