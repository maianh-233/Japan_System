import dayjs from 'dayjs';
import { WORK_SHIFTS } from '@/features/hr/constants';

/** Giờ hành chính mặc định để SINH dữ liệu giả (không đọc Cài đặt chung — tránh vòng import lúc khởi động) */
const OFFICE_HOURS = { from: '08:00', to: '17:30', satTo: '12:00' };
import type { Candidate, HrRequest, Job, Onboarding, TimeRecord } from '@/features/hr/types';
import { MOCK_EMPLOYEES } from './employees';
import { MOCK_SHIFTS } from './work';

/**
 * Dữ liệu giả — chấm công (sinh từ lịch ca + giờ hành chính), đơn từ, tuyển dụng, onboarding.
 * Tình huống chấm công: đi muộn, về sớm, quên check-out, vắng ca không phép, nghỉ phép đã duyệt, sửa công tay.
 */

const ago = (n: number, h = 9, m = 0) => dayjs().subtract(n, 'day').hour(h).minute(m).second(0).toISOString();
const day = (n: number) => dayjs().add(n, 'day').format('YYYY-MM-DD');
const shift = (t: string, min: number) => dayjs(`2000-01-01T${t}`).add(min, 'minute').format('HH:mm');
const today = dayjs().format('YYYY-MM-DD');
const nowHM = dayjs().format('HH:mm');

/** Ngày nghỉ phép đã duyệt (không sinh chấm công) */
const LEAVE_DAYS: Record<string, string[]> = { 'e-mai': [day(-6), day(-5)], 'e-han': [day(-3)] };
/** Vắng ca không phép */
const ABSENT: Record<string, string[]> = { 'e-tung': [day(-4)] };

const records: TimeRecord[] = [];
let n = 0;
const push = (r: Omit<TimeRecord, 'id'>) => records.push({ ...r, id: `TR-${++n}` });

const staff = MOCK_EMPLOYEES.filter((e) => e.kind === 'staff' && (e.status === 'active' || e.status === 'probation'));
for (let i = 20; i >= 0; i--) {
  const date = day(-i);
  const wd = ((dayjs(date).day() + 6) % 7) + 1;
  staff.forEach((e, k) => {
    if (LEAVE_DAYS[e.id]?.includes(date) || ABSENT[e.id]?.includes(date)) return;
    let from: string;
    let to: string;
    if (e.shiftBased) {
      const mine = MOCK_SHIFTS.filter((s) => s.employeeId === e.id && s.date === date).map((s) => WORK_SHIFTS[s.shift]);
      if (!mine.length) return;
      from = mine.map((s) => s.from).sort()[0];
      to = mine.map((s) => s.to).sort().reverse()[0];
    } else {
      if (wd === 7) return;
      from = OFFICE_HOURS.from;
      to = wd === 6 ? OFFICE_HOURS.satTo : OFFICE_HOURS.to;
    }
    if (date === today && nowHM < from) return;
    const seed = (i * 7 + k * 3) % 23;
    const late = seed === 4 || seed === 11 ? 8 + (seed % 9) : 0;
    const early = seed === 17 ? 25 : 0;
    const checkIn = shift(from, late ? late : -((seed % 10) + 1));
    const forgot = (i === 2 && k % 5 === 1) || (date === today && nowHM < to);
    push({
      employeeId: e.id,
      date,
      checkIn,
      checkOut: forgot ? undefined : shift(to, early ? -early : seed % 6),
      edited: i === 9 && k === 2 ? { by: 'Lê Hoàng Nam', reason: 'Máy chấm công lỗi sáng thứ 2 — xác nhận có mặt' } : undefined,
    });
  });
}
export const MOCK_TIME_RECORDS = records;

// ---------------- Đơn từ ----------------

const step = (role: string, approver: string, decision?: 'approved' | 'rejected', at?: string, comment?: string) => ({ role, approver, decision, at, comment });

export const MOCK_REQUESTS: HrRequest[] = [
  { id: 'RQ-1', code: 'DT-0201', type: 'leave', employeeId: 'e-mai', from: day(-6), to: day(-5), days: 2, paidLeave: true, reason: 'Về quê dự đám cưới em gái', status: 'approved', steps: [step('Quản lý chi nhánh', 'Lê Hoàng Nam', 'approved', ago(12, 10), 'Đồng ý, nhờ chị Hồng trực thay.')], createdAt: ago(13) },
  { id: 'RQ-2', code: 'DT-0202', type: 'leave', employeeId: 'e-bao', from: day(5), to: day(9), days: 5, paidLeave: true, reason: 'Nghỉ phép năm — đi du lịch cùng gia đình', status: 'pending', steps: [step('Quản lý chi nhánh', 'Lê Hoàng Nam', 'approved', ago(0, 9), 'Ổn, Tùng nhận thêm ca tối.'), step('Phó giám đốc (nghỉ > 3 ngày)', 'Phạm Thu Trang')], createdAt: ago(1) },
  { id: 'RQ-3', code: 'DT-0203', type: 'shift_swap', employeeId: 'e-nhu', from: day(1), to: day(1), reason: 'Lịch thi cuối kỳ ở trường vào sáng mai', status: 'pending', steps: [step('Quản lý chi nhánh', 'Lê Hoàng Nam')], createdAt: ago(0, 8) },
  { id: 'RQ-4', code: 'DT-0204', type: 'late_early', employeeId: 'e-hang', from: day(2), to: day(2), reason: 'Đưa con đi tiêm phòng — đến muộn 1 tiếng', status: 'pending', steps: [step('Quản lý chi nhánh', 'Lê Hoàng Nam')], createdAt: ago(0, 7) },
  { id: 'RQ-5', code: 'DT-0205', type: 'proposal', employeeId: 'e-tuan', from: today, to: today, title: 'Mua 2 bóng đèn máy chiếu + 10 tai nghe phòng nghe', amount: 7_800_000, reason: 'P.301 máy chiếu nhòe, P.303 hỏng 5 tai nghe (báo hỏng RI-1, RI-4)', status: 'pending', steps: [step('Quản lý chi nhánh', 'Lê Hoàng Nam', 'approved', ago(0, 10)), step('Kế toán trưởng (> 5 triệu)', 'Hoàng Thị Lan'), step('Giám đốc', 'Đỗ Quang Huy')], createdAt: ago(1) },
  { id: 'RQ-6', code: 'DT-0206', type: 'advance', employeeId: 'e-tung', from: today, to: today, amount: 3_000_000, reason: 'Tạm ứng lương — chi phí khám bệnh cho mẹ', status: 'approved', steps: [step('Quản lý chi nhánh', 'Lê Hoàng Nam', 'approved', ago(3, 11)), step('Kế toán trưởng', 'Hoàng Thị Lan', 'approved', ago(3, 15))], createdAt: ago(4) },
  { id: 'RQ-7', code: 'DT-0207', type: 'business_trip', employeeId: 'e-hong', from: day(10), to: day(11), reason: 'Hỗ trợ chi nhánh Hải Châu tổ chức thi thử JLPT', status: 'pending', steps: [step('Quản lý chi nhánh', 'Lê Hoàng Nam')], createdAt: ago(0, 11) },
  { id: 'RQ-8', code: 'DT-0208', type: 'leave', employeeId: 'e-khoa', from: day(-20), to: day(-20), days: 1, paidLeave: false, reason: 'Việc cá nhân', status: 'rejected', steps: [step('Quản lý chi nhánh', 'Châu Minh Đức', 'rejected', ago(21, 9), 'Ngày cao điểm khai giảng, thiếu người trực tư vấn.')], createdAt: ago(22) },
  { id: 'RQ-9', code: 'DT-0209', type: 'leave', employeeId: 'e-han', from: day(-3), to: day(-3), days: 1, paidLeave: true, reason: 'Ốm', status: 'approved', steps: [step('Quản lý chi nhánh', 'Châu Minh Đức', 'approved', ago(4, 8))], createdAt: ago(4) },
];

// ---------------- Tuyển dụng ----------------

export const MOCK_JOBS: Job[] = [
  { id: 'JB-1', title: 'Giáo viên tiếng Nhật (N2 trở lên)', branchId: 'cg', department: 'teaching', kind: 'teacher', quantity: 2, salaryRange: '220.000 – 300.000đ / giờ', status: 'open', postedAt: day(-25), deadline: day(10), channels: ['TopCV', 'Facebook nhóm GV tiếng Nhật', 'Giới thiệu nội bộ'], description: 'Dạy lớp N5–N3 buổi tối, ưu tiên có chứng chỉ sư phạm.' },
  { id: 'JB-2', title: 'Tư vấn tuyển sinh (làm theo ca)', branchId: 'q1', department: 'sales', kind: 'staff', quantity: 2, salaryRange: '7 – 9 triệu + hoa hồng', status: 'open', postedAt: day(-15), deadline: day(15), channels: ['TopCV', 'VietnamWorks'] },
  { id: 'JB-3', title: 'Trợ giảng lớp thiếu nhi (part-time)', branchId: 'cg', department: 'teaching', kind: 'assistant', quantity: 1, salaryRange: '90.000đ / giờ', status: 'paused', postedAt: day(-40), deadline: day(-5), channels: ['Facebook'] },
];

const note = (by: string, text: string, n2: number) => ({ at: ago(n2, 14), by, text });

export const MOCK_CANDIDATES: Candidate[] = [
  { id: 'CD-1', jobId: 'JB-1', fullName: 'Nguyễn Khánh Linh', phone: '0912888111', email: 'khanhlinh.jp@gmail.com', source: 'TopCV', stage: 'interview', appliedAt: ago(18), cvName: 'CV_KhanhLinh_N1.pdf', japanese: 'JLPT N1', expectedSalary: 280_000, interview: { at: dayjs().add(1, 'day').hour(15).minute(0).toISOString(), interviewer: 'Nguyễn Thu Hà', mode: 'offline' }, notes: [note('Lê Hoàng Nam', 'CV tốt, 2 năm dạy tại trung tâm khác. Hẹn dạy thử 30 phút.', 5)] },
  { id: 'CD-2', jobId: 'JB-1', fullName: 'Trần Đức Anh', phone: '0987111222', email: 'ducanh.tran@gmail.com', source: 'Giới thiệu nội bộ', stage: 'offer', appliedAt: ago(22), cvName: 'TranDucAnh_CV.pdf', japanese: 'JLPT N2', expectedSalary: 230_000, interview: { at: ago(8, 15), interviewer: 'Nguyễn Thu Hà', mode: 'offline', score: 8, note: 'Phát âm chuẩn, dạy thử bài 12 Minna rõ ràng.' }, offer: { salary: 230_000, startDate: day(14), sentAt: ago(2, 10) }, notes: [] },
  { id: 'CD-3', jobId: 'JB-1', fullName: 'Phạm Thảo Vy', phone: '0933222444', email: 'thaovy.p@gmail.com', source: 'Facebook nhóm GV tiếng Nhật', stage: 'screening', appliedAt: ago(4), cvName: 'CV_ThaoVy.pdf', japanese: 'JLPT N2', notes: [] },
  { id: 'CD-4', jobId: 'JB-1', fullName: 'Hoàng Minh Quang', phone: '0901555666', email: 'quang.hm@gmail.com', source: 'TopCV', stage: 'rejected', appliedAt: ago(20), cvName: 'HMQuang.pdf', japanese: 'JLPT N3', rejectReason: 'Chưa đạt yêu cầu N2', notes: [] },
  { id: 'CD-5', jobId: 'JB-1', fullName: 'Ishikawa Mao', phone: '0977333444', email: 'mao.ishikawa@gmail.com', source: 'Giới thiệu nội bộ', stage: 'applied', appliedAt: ago(1), cvName: 'Ishikawa_Mao_Resume.pdf', japanese: 'Người bản xứ', notes: [note('Sato Yuki', 'Bạn của Sato, muốn dạy Kaiwa cuối tuần.', 1)] },
  { id: 'CD-6', jobId: 'JB-2', fullName: 'Lê Thị Phương', phone: '0909444555', email: 'phuong.le@gmail.com', source: 'VietnamWorks', stage: 'interview', appliedAt: ago(10), cvName: 'LeThiPhuong.pdf', expectedSalary: 8_500_000, interview: { at: dayjs().hour(16).minute(0).toISOString(), interviewer: 'Châu Minh Đức', mode: 'online' }, notes: [] },
  { id: 'CD-7', jobId: 'JB-2', fullName: 'Võ Hoàng Long', phone: '0938666777', email: 'hoanglong.vo@gmail.com', source: 'TopCV', stage: 'screening', appliedAt: ago(6), cvName: 'VoHoangLong.pdf', expectedSalary: 9_000_000, notes: [] },
  { id: 'CD-8', jobId: 'JB-2', fullName: 'Đào Thanh Hương', phone: '0918777888', email: 'thanhhuong.dao@gmail.com', source: 'TopCV', stage: 'applied', appliedAt: ago(2), cvName: 'DaoThanhHuong.pdf', notes: [] },
  { id: 'CD-9', jobId: 'JB-2', fullName: 'Nguyễn Hữu Phát', phone: '0907888999', email: 'huuphat@gmail.com', source: 'VietnamWorks', stage: 'hired', appliedAt: ago(14), cvName: 'NguyenHuuPhat.pdf', expectedSalary: 8_000_000, interview: { at: ago(9, 10), interviewer: 'Châu Minh Đức', mode: 'offline', score: 8 }, offer: { salary: 8_000_000, startDate: day(3), sentAt: ago(6), accepted: true }, notes: [] },
  { id: 'CD-10', jobId: 'JB-3', fullName: 'Trịnh Bảo Ngọc', phone: '0966111000', email: 'baongoc.t@gmail.com', source: 'Facebook', stage: 'interview', appliedAt: ago(30), japanese: 'JLPT N3', interview: { at: ago(20, 10), interviewer: 'Trần Mai Anh', mode: 'offline', score: 6, note: 'Hợp với trẻ nhỏ, tiếng Nhật cần cải thiện.' }, notes: [] },
];

// ---------------- Onboarding ----------------

const items = (done: number, teacher = false) =>
  [
    ['contract', 'docs', 'Ký hợp đồng thử việc'],
    ['idcard', 'docs', 'Bản sao CCCD, sổ hộ khẩu / CT07'],
    ['degree', 'docs', 'Bằng cấp, chứng chỉ (JLPT, sư phạm...)'],
    ['bank', 'docs', 'Số tài khoản ngân hàng nhận lương'],
    ['account', 'account', 'Tạo tài khoản đăng nhập hệ thống + phân quyền'],
    ['email', 'account', 'Email công ty, nhóm Zalo nội bộ'],
    ['laptop', 'equipment', 'Bàn giao laptop / máy tính quầy'],
    ['card', 'equipment', 'Thẻ nhân viên, đồng phục, tủ cá nhân'],
    ['intro', 'training', 'Đào tạo hội nhập: nội quy, văn hóa trung tâm'],
    ['system', 'training', 'Hướng dẫn sử dụng hệ thống theo vị trí'],
    ...(teacher ? [['demo', 'training', 'Dạy thử / dự giờ 2 buổi']] : []),
  ].map(([key, group, label], i) => ({ key, group: group as 'docs', label, done: i < done, doneBy: i < done ? 'Vũ Ngọc Mai' : undefined, doneAt: i < done ? ago(10 - i) : undefined }));

export const MOCK_ONBOARDINGS: Onboarding[] = [
  { id: 'OB-1', candidateId: 'CD-9', fullName: 'Nguyễn Hữu Phát', phone: '0907888999', email: 'huuphat@gmail.com', position: 'Tư vấn tuyển sinh', department: 'sales', kind: 'staff', branchId: 'q1', startDate: day(3), mentor: 'Trần Minh Khoa', items: items(3), createdAt: ago(5) },
  { id: 'OB-2', fullName: 'Hồ Quỳnh Như', phone: '0981234567', email: 'nhu.e-nhu@jpcenter.vn', position: 'Tư vấn tuyển sinh', department: 'sales', kind: 'staff', branchId: 'cg', startDate: day(-35), mentor: 'Đinh Quốc Bảo', items: items(10), employeeId: 'e-nhu', createdAt: ago(40) },
];
