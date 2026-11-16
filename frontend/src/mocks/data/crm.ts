import dayjs from 'dayjs';
import type { Consultation, Lead, LeadSource, LeadStatus, LeadStudentLink } from '@/features/crm/types';
import { EXTRA_LINKS } from './students';

export { MOCK_STUDENTS } from './students';

/** Dữ liệu giả — CRM. Có sẵn các tình huống: phụ huynh 2 con, bố mẹ cùng liên hệ, lead tự học, lead đã ẩn, lead quá SLA. */

const ago = (min: number) => dayjs().subtract(min, 'minute').toISOString();
const inDays = (d: number, h = 10) => dayjs().add(d, 'day').hour(h).minute(0).second(0).toISOString();

const FIRST = ['Nguyễn', 'Trần', 'Lê', 'Phạm', 'Hoàng', 'Vũ', 'Đặng', 'Bùi', 'Đỗ', 'Ngô'];
const MIDDLE = ['Văn', 'Thị', 'Minh', 'Đức', 'Thu', 'Ngọc', 'Hải', 'Quang'];
const LAST = ['An', 'Bình', 'Chi', 'Dũng', 'Giang', 'Hà', 'Khánh', 'Linh', 'Nam', 'Phương', 'Quân', 'Trang', 'Tuấn', 'Yến'];
const SOURCES: LeadSource[] = ['facebook', 'website', 'zalo', 'tiktok', 'referral', 'walk_in', 'hotline', 'event'];
const STATUSES: LeadStatus[] = ['new', 'consulting', 'consulting', 'test_scheduled', 'won', 'lost', 'consulting'];
const BRANCHES = ['cg', 'q1', 'hc'];
/** Sale theo chi nhánh = nhân viên phòng Sale trong Nhân sự (mocks/data/employees.ts) */
const SALE_BY_BRANCH: Record<string, string[]> = { cg: ['e-tung', 'e-bao', 'e-nhu'], q1: ['e-khoa', 'e-han'], hc: ['e-vy'] };
const COURSES = ['n5', 'n4', 'n3', 'n2', 'kaiwa', 'xkld', 'n1'];

const special: Lead[] = [
  // Phụ huynh có 2 con đang học
  {
    id: 'L-0001', code: 'LD0001', fullName: 'Nguyễn Văn Bình', phone: '0912345678', email: 'binh.nguyen@gmail.com',
    source: 'referral', status: 'won', branchId: 'cg', assigneeId: 'e-tung', interest: 'kids',
    note: 'Phụ huynh đăng ký cho 2 con. Con lớn học N3, con nhỏ học lớp thiếu nhi.',
    lastContactAt: ago(60 * 24 * 20), createdAt: ago(60 * 24 * 60), createdBy: 'Nguyễn Thanh Tùng', hidden: false, notifyChannels: ['zalo', 'email'],
  },
  // Mẹ của An cũng từng liên hệ (bố mẹ cùng là lead của 1 học viên)
  {
    id: 'L-0002', code: 'LD0002', fullName: 'Trần Thị Mai', phone: '0987654321', source: 'facebook', status: 'won',
    branchId: 'cg', assigneeId: 'e-bao', interest: 'n3', note: 'Mẹ của bé An — hỏi lịch học cuối tuần.',
    lastContactAt: ago(60 * 24 * 40), createdAt: ago(60 * 24 * 70), createdBy: 'Đinh Quốc Bảo', hidden: false, notifyChannels: ['zalo'],
  },
  // Người học tự đăng ký
  {
    id: 'L-0003', code: 'LD0003', fullName: 'Lê Hoàng Phúc', phone: '0901112233', email: 'phuc.le@outlook.com',
    source: 'website', status: 'won', branchId: 'q1', assigneeId: 'e-khoa', interest: 'n2',
    note: 'Đi làm công ty Nhật, cần N2 trong 1 năm.', lastContactAt: ago(60 * 24 * 10), createdAt: ago(60 * 24 * 30),
    createdBy: 'Trần Minh Khoa', hidden: false,
  },
  // Lead mới quá SLA (chưa ai gọi)
  {
    id: 'L-0004', code: 'LD0004', fullName: 'Phạm Gia Huy', phone: '0933444555', source: 'facebook', status: 'new',
    branchId: 'q1', assigneeId: 'e-khoa', interest: 'n5', createdAt: ago(48), createdBy: 'Hệ thống (Facebook Lead Ads)', hidden: false,
  },
  {
    id: 'L-0005', code: 'LD0005', fullName: 'Võ Thanh Tâm', phone: '0944555666', source: 'tiktok', status: 'new',
    branchId: 'cg', assigneeId: 'e-tung', interest: 'kaiwa', createdAt: ago(6), createdBy: 'Hệ thống (Form website)', hidden: false,
  },
  // Lead đã ẩn nhưng có học viên liên kết
  {
    id: 'L-0006', code: 'LD0006', fullName: 'Đỗ Minh Tuấn', phone: '0977888999', source: 'hotline', status: 'won',
    branchId: 'hc', assigneeId: 'e-vy', interest: 'xkld', note: 'Lead trùng — đã gộp, ẩn đi.',
    createdAt: ago(60 * 24 * 90), createdBy: 'Lý Thanh Vy', hidden: true, hiddenAt: ago(60 * 24 * 5),
  },
];

const generated: Lead[] = Array.from({ length: 42 }, (_, i) => {
  const n = i + 7;
  const branchId = BRANCHES[i % 3];
  const status = STATUSES[i % STATUSES.length];
  const created = 60 * (3 + i * 17);
  return {
    id: `L-${String(n).padStart(4, '0')}`,
    code: `LD${String(n).padStart(4, '0')}`,
    fullName: `${FIRST[i % FIRST.length]} ${MIDDLE[(i * 3) % MIDDLE.length]} ${LAST[(i * 5) % LAST.length]}`,
    phone: `09${String(20000000 + i * 104729).slice(0, 8)}`,
    email: i % 3 === 0 ? `lead${n}@gmail.com` : undefined,
    source: SOURCES[i % SOURCES.length],
    status,
    branchId,
    assigneeId: SALE_BY_BRANCH[branchId][Math.floor(i / 3) % SALE_BY_BRANCH[branchId].length],
    interest: COURSES[i % COURSES.length],
    lostReason: status === 'lost' ? 'Học phí cao so với ngân sách' : undefined,
    nextFollowUpAt: status === 'consulting' || status === 'test_scheduled' ? inDays((i % 5) - 2) : undefined,
    lastContactAt: status === 'new' ? undefined : ago(created - 60),
    createdAt: ago(created),
    createdBy: 'Nhân viên tư vấn',
    hidden: i % 19 === 7,
    hiddenAt: i % 19 === 7 ? ago(600) : undefined,
  };
});

/** Lead từ kênh có chạy quảng cáo → gắn chiến dịch (xem mocks/data/admissions.ts). Website: 1/3 tự nhiên (SEO), còn lại từ Ads */
const CAMPAIGN_BY_SOURCE: Partial<Record<LeadSource, string>> = { facebook: 'CP-01', tiktok: 'CP-02', event: 'CP-03', website: 'CP-04', zalo: 'CP-05' };

export const MOCK_LEADS: Lead[] = [...special, ...generated].map((l, i) => {
  const campaignId = CAMPAIGN_BY_SOURCE[l.source];
  return campaignId && !(l.source === 'website' && i % 3 === 0) ? { ...l, campaignId } : l;
});

export const MOCK_CONSULTATIONS: Consultation[] = [
  { id: 'C-1', leadId: 'L-0001', channel: 'call', result: 'interested', content: 'Phụ huynh hỏi lớp thiếu nhi cho con nhỏ (9 tuổi) và lớp N3 cho con lớn.', staffName: 'Nguyễn Thanh Tùng', createdAt: ago(60 * 24 * 58) },
  { id: 'C-2', leadId: 'L-0001', channel: 'meeting', result: 'test_booked', content: 'Đưa 2 con đến test đầu vào. Con lớn đạt mức N4, xếp lớp N3 sau 1 tháng ôn.', staffName: 'Nguyễn Thanh Tùng', createdAt: ago(60 * 24 * 50) },
  { id: 'C-3', leadId: 'L-0001', channel: 'zalo', result: 'enrolled', content: 'Đã đóng học phí đợt 1 cho cả 2 con, ưu đãi anh chị em 10%.', staffName: 'Nguyễn Thanh Tùng', createdAt: ago(60 * 24 * 20) },
  { id: 'C-4', leadId: 'L-0002', channel: 'message', result: 'thinking', content: 'Hỏi lịch học cuối tuần cho con. Hẹn trao đổi thêm với chồng.', staffName: 'Đinh Quốc Bảo', createdAt: ago(60 * 24 * 69) },
  { id: 'C-5', leadId: 'L-0003', channel: 'call', result: 'enrolled', content: 'Chốt khóa N2 buổi tối, đóng 1 lần.', staffName: 'Trần Minh Khoa', createdAt: ago(60 * 24 * 10) },
  ...MOCK_LEADS.filter((l) => l.status !== 'new' && !l.id.startsWith('L-000')).slice(0, 30).map((l, i) => ({
    id: `C-g${i}`,
    leadId: l.id,
    channel: (['call', 'zalo', 'message', 'meeting'] as const)[i % 4],
    result: l.status === 'lost' ? ('not_interested' as const) : l.status === 'test_scheduled' ? ('test_booked' as const) : ('interested' as const),
    content: l.status === 'lost' ? 'Khách chê học phí cao, chưa có nhu cầu.' : 'Tư vấn lộ trình, gửi bảng giá và lịch khai giảng.',
    staffName: 'Nhân viên tư vấn',
    createdAt: l.lastContactAt ?? l.createdAt,
  })),
];

export const MOCK_LINKS: LeadStudentLink[] = [
  { leadId: 'L-0001', studentId: 'ST-1', relationship: 'child', createdAt: ago(60 * 24 * 20), primary: true, billing: true, notify: true },
  { leadId: 'L-0001', studentId: 'ST-2', relationship: 'child', createdAt: ago(60 * 24 * 20), primary: true, billing: true, notify: true },
  { leadId: 'L-0002', studentId: 'ST-1', relationship: 'child', createdAt: ago(60 * 24 * 19), notify: true },
  { leadId: 'L-0003', studentId: 'ST-3', relationship: 'self', createdAt: ago(60 * 24 * 10) },
  { leadId: 'L-0006', studentId: 'ST-4', relationship: 'self', createdAt: ago(60 * 24 * 80) },
  ...EXTRA_LINKS,
];
