import dayjs from 'dayjs';
import { PERM_CATALOG } from '@/features/system/constants';
import type { AuditLog, BranchProfile, Integration, IntegrationLog, PermAction, PermScope, Role, UserAccess } from '@/features/system/types';
import { MOCK_BRANCHES } from './branches';
import { MOCK_EMPLOYEES } from './employees';

/**
 * Dữ liệu giả — Hệ thống: hồ sơ chi nhánh, role + ma trận quyền, quyền từng người, tích hợp, nhật ký.
 * Tình huống: chi nhánh đang chuẩn bị mở, role tự tạo, người kiêm 2 role ở 2 chi nhánh, quyền lẻ có thời hạn (còn hạn / hết hạn),
 * quyền bị chặn, tài khoản bị khóa (nghỉ việc), tích hợp lỗi token, nhật ký có thao tác nghiêm trọng.
 */

const ago = (min: number) => dayjs().subtract(min, 'minute').toISOString();
const inDays = (d: number) => dayjs().add(d, 'day').endOf('day').toISOString();

// ---------------- Chi nhánh ----------------

const BRANCH_EXTRA: Record<string, Omit<BranchProfile, 'id' | 'code' | 'name' | 'address'>> = {
  cg: {
    phone: '024 3765 8899', email: 'caugiay@jpcenter.vn', managerId: 'e-nam', openHours: 'T2–T7 08:00–21:30 · CN 08:00–17:00',
    status: 'active', openedAt: '2021-03-15', invoicePrefix: 'CG', bank: { bank: 'Vietcombank', account: '0011004455667', holder: 'CONG TY TNHH JP CENTER' },
  },
  q1: {
    phone: '028 3822 6677', email: 'quan1@jpcenter.vn', managerId: 'e-q1m', openHours: 'T2–T7 08:00–21:30 · CN 08:00–12:00',
    status: 'active', openedAt: '2022-08-01', invoicePrefix: 'Q1', bank: { bank: 'Techcombank', account: '19036677889900', holder: 'CONG TY TNHH JP CENTER CN HCM' },
  },
  hc: {
    phone: '0236 3889 112', email: 'haichau@jpcenter.vn', managerId: 'e-hcm', openHours: 'T2–T7 08:00–21:00',
    status: 'active', openedAt: '2023-09-10', invoicePrefix: 'HC', bank: { bank: 'BIDV', account: '56010001122334', holder: 'CONG TY TNHH JP CENTER CN DA NANG' },
  },
};

export const MOCK_BRANCH_PROFILES: BranchProfile[] = [
  ...MOCK_BRANCHES.map((b) => ({ ...b, ...BRANCH_EXTRA[b.id] })),
  {
    id: 'td', code: 'TD', name: 'Thủ Đức', address: '25 Võ Văn Ngân, TP. Thủ Đức, TP.HCM', phone: '028 3896 5544', email: 'thuduc@jpcenter.vn',
    openHours: 'T2–T7 08:00–21:30', status: 'preparing', openedAt: dayjs().add(45, 'day').format('YYYY-MM-DD'), invoicePrefix: 'TD',
    note: 'Đang hoàn thiện phòng học, dự kiến khai trương sau 1,5 tháng. Chưa nhận lead / ghi danh.',
  },
];

// ---------------- Role ----------------

/** { "crm.leads": ["view,create", "own"], "crm.*": ["*", "all"] } → grants */
function g(spec: Record<string, [string, PermScope]>): Record<string, PermScope> {
  const out: Record<string, PermScope> = {};
  Object.entries(spec).forEach(([key, [acts, scope]]) => {
    const [mod, grp] = key.split('.');
    const groups = PERM_CATALOG.find((m) => m.key === mod)?.groups.filter((x) => grp === '*' || x.key === key) ?? [];
    groups.forEach((x) => {
      const list = acts === '*' ? x.actions : (acts.split(',') as PermAction[]).filter((a) => x.actions.includes(a));
      list.forEach((a) => (out[`${x.key}.${a}`] = scope));
    });
  });
  return out;
}

const base = { updatedAt: ago(60 * 24 * 30), updatedBy: 'Đỗ Quang Huy' };
const SELF = { 'hr.requests': ['view,create', 'own'], 'work.tasks': ['view,update', 'own'] } as Record<string, [string, PermScope]>;

export const MOCK_ROLES: Role[] = [
  { id: 'R-SA', name: 'Super Admin', description: 'Toàn quyền mọi chi nhánh, kể cả phân quyền và tích hợp.', system: true, superAdmin: true, grants: {}, ...base },
  {
    id: 'R-AD', name: 'Admin', description: 'Vận hành toàn hệ thống. Không phân quyền, không sửa tích hợp.', system: true, ...base,
    grants: g({
      'dashboard.*': ['*', 'all'], 'crm.*': ['*', 'all'], 'students.*': ['*', 'all'], 'academic.*': ['*', 'all'], 'library.*': ['*', 'all'],
      'finance.*': ['*', 'all'], 'hr.*': ['*', 'all'], 'work.*': ['*', 'all'], 'system.branches': ['view,update', 'all'], 'system.audit': ['*', 'all'],
      'system.approvals': ['view,approve', 'all'], 'system.trash': ['*', 'all'], 'system.settings': ['view,update', 'all'],
    }),
  },
  {
    id: 'R-BM', name: 'Quản lý chi nhánh', description: 'Toàn quyền nghiệp vụ trong chi nhánh được gán.', system: true, ...base,
    grants: g({
      'dashboard.*': ['*', 'branch'], 'crm.*': ['*', 'branch'], 'students.*': ['*', 'branch'], 'academic.*': ['*', 'branch'], 'library.*': ['*', 'branch'],
      'finance.*': ['view,create,update,export,approve', 'branch'], 'hr.*': ['*', 'branch'], 'work.*': ['*', 'branch'],
      'system.approvals': ['view,approve', 'branch'],
    }),
  },
  {
    id: 'R-GV', name: 'Giáo vụ', description: 'Học viên, lớp, buổi học, điểm danh; tạo hóa đơn khi ghi danh.', system: true, ...base,
    grants: g({
      'dashboard.overview': ['view', 'branch'], 'students.*': ['view,create,update,export', 'branch'], 'academic.*': ['view,create,update,export', 'branch'],
      // Giáo vụ duyệt đơn hủy buổi / xin nghỉ, mời dạy thay (DT-16, DT-18)
      'academic.requests': ['view,create,update,approve,export', 'branch'], 'crm.promotions': ['view', 'branch'],
      'academic.questions': ['view,create,update', 'own'], 'library.documents': ['view,create', 'branch'], 'finance.invoices': ['view,create', 'branch'], ...SELF,
    }),
  },
  {
    id: 'R-KT', name: 'Kế toán', description: 'Học phí, hóa đơn, đối soát, công nợ, lương toàn hệ thống.', system: true, ...base,
    grants: g({ 'dashboard.*': ['view,export', 'all'], 'finance.*': ['*', 'all'], 'system.approvals': ['view,approve', 'all'], 'students.profiles': ['view', 'all'], 'hr.timesheets': ['view,export', 'all'], ...SELF }),
  },
  {
    id: 'R-SALE', name: 'Sale', description: 'Tư vấn lead được giao, gửi test xếp lớp.', system: true, ...base,
    grants: g({
      'dashboard.overview': ['view', 'own'], 'crm.leads': ['view,create,update', 'own'], 'crm.placement-tests': ['view,create', 'branch'], 'crm.campaigns': ['view', 'all'],
      'crm.appointments': ['view,create,update', 'branch'], 'crm.funnel': ['view', 'own'], 'crm.message-templates': ['view', 'all'], 'crm.promotions': ['view,update', 'own'],
      'students.profiles': ['view', 'branch'], 'work.shifts': ['view', 'own'], 'work.handover': ['view,create', 'own'], ...SELF,
    }),
  },
  {
    id: 'R-MKT', name: 'Marketing', description: 'Chiến dịch, nguồn lead, báo cáo chuyển đổi.', system: true, ...base,
    grants: g({ 'dashboard.*': ['view,export', 'all'], 'crm.*': ['*', 'all'], ...SELF }),
  },
  { id: 'R-TEACHER', name: 'Giáo viên', description: 'Chỉ dùng Cổng giáo viên — không có quyền trong cổng nhân viên.', system: true, grants: {}, ...base },
  {
    id: 'R-LT', name: 'Lễ tân', description: 'Tiếp khách, nhập lead đến trực tiếp, thu tiền tại quầy.', system: false, updatedAt: ago(60 * 24 * 12), updatedBy: 'Phạm Thu Trang',
    grants: g({
      'crm.leads': ['view,create', 'branch'], 'crm.appointments': ['*', 'branch'], 'students.profiles': ['view', 'branch'], 'academic.sessions': ['view', 'branch'], 'academic.rooms': ['view', 'branch'],
      'finance.invoices': ['view,create', 'branch'], 'work.shifts': ['view', 'own'], 'work.handover': ['view,create', 'own'], ...SELF,
    }),
  },
  {
    id: 'R-TL', name: 'Trưởng nhóm Sale', description: 'Như Sale + xem lead cả chi nhánh, chia lead, xem hoa hồng nhóm.', system: false, updatedAt: ago(60 * 24 * 5), updatedBy: 'Lê Hoàng Nam',
    grants: g({
      'dashboard.*': ['view', 'branch'], 'crm.leads': ['*', 'branch'], 'crm.lead-rules': ['view,update', 'branch'], 'crm.placement-tests': ['*', 'branch'], 'crm.campaigns': ['view', 'all'],
      'crm.appointments': ['*', 'branch'], 'crm.funnel': ['view', 'branch'], 'crm.message-templates': ['*', 'branch'], 'crm.promotions': ['*', 'branch'],
      'students.profiles': ['view', 'branch'], 'finance.commissions': ['view', 'branch'], 'work.shifts': ['view,approve', 'branch'], 'work.handover': ['*', 'branch'], ...SELF,
    }),
  },
];

// ---------------- Quyền từng người ----------------

function defaultRole(e: (typeof MOCK_EMPLOYEES)[number]): string {
  if (e.id === 'e-huy') return 'R-SA';
  if (e.id === 'e-trang') return 'R-AD';
  if (e.department === 'board') return 'R-BM';
  return { academic: 'R-GV', finance: 'R-KT', marketing: 'R-MKT', sales: 'R-SALE', reception: 'R-LT', teaching: 'R-TEACHER', it: 'R-AD' }[e.department as string] ?? 'R-SALE';
}
const GLOBAL_ROLES = new Set(['R-SA', 'R-AD', 'R-KT', 'R-MKT']);

/** Người đã nghỉ việc vẫn giữ bản ghi tài khoản (đã khóa) để tra nhật ký */
export const MOCK_USER_ACCESS: UserAccess[] = MOCK_EMPLOYEES.filter((e) => e.hasAccount || e.status === 'quit').map((e, i) => {
  const roleId = defaultRole(e);
  const a: UserAccess = {
    userId: e.id,
    email: e.email,
    status: e.status === 'quit' ? 'locked' : e.status === 'probation' && i % 2 ? 'invited' : 'active',
    roles: [{ roleId, branchIds: GLOBAL_ROLES.has(roleId) ? [] : [e.branchId] }],
    overrides: [],
    lastLoginAt: e.status === 'quit' ? ago(60 * 24 * 40) : ago(30 + ((i * 397) % (60 * 48))),
    lockedReason: e.status === 'quit' ? 'Nghỉ việc — khóa tự động khi chấm dứt hợp đồng' : undefined,
  };
  return a;
});

const patch = (id: string, f: (a: UserAccess) => void) => {
  const a = MOCK_USER_ACCESS.find((x) => x.userId === id);
  if (a) f(a);
};
// Giáo vụ phụ trách thêm chi nhánh Hải Châu + được cấp tạm xem công nợ
patch('e-mai', (a) => {
  a.roles = [{ roleId: 'R-GV', branchIds: ['cg', 'hc'] }];
  a.overrides.push({ id: 'OV-1', perm: 'finance.debts.view', effect: 'grant', scope: 'branch', until: inDays(7), reason: 'Hỗ trợ nhắc nợ đợt khai giảng tháng 11', by: 'Lê Hoàng Nam', at: ago(60 * 24 * 2) });
});
// Bảo kiêm Trưởng nhóm Sale ở CG
patch('e-bao', (a) => (a.roles = [{ roleId: 'R-TL', branchIds: ['cg'] }]));
// Khoa bị chặn xuất lead
patch('e-khoa', (a) =>
  a.overrides.push({ id: 'OV-2', perm: 'crm.leads.export', effect: 'deny', reason: 'Chính sách bảo mật dữ liệu khách hàng — Sale không xuất file lead', by: 'Phạm Thu Trang', at: ago(60 * 24 * 20) }),
);
// Hồng: quyền lẻ đã hết hạn
patch('e-hong', (a) =>
  a.overrides.push({ id: 'OV-3', perm: 'finance.reconciliation.view', effect: 'grant', scope: 'branch', until: dayjs().subtract(3, 'day').toISOString(), reason: 'Thay kế toán nghỉ phép', by: 'Hoàng Thị Lan', at: ago(60 * 24 * 10) }),
);
// Lễ tân Q1 kiêm Sale ở Q1 buổi tối
patch('e-linh', (a) => a.roles.push({ roleId: 'R-SALE', branchIds: ['q1'] }));

// ---------------- Tích hợp ----------------

export const MOCK_INTEGRATIONS: Integration[] = [
  {
    id: 'zalo-oa', name: 'Zalo OA', category: 'notify', description: 'Gửi thông báo ZNS cho phụ huynh, học viên: điểm danh, nhắc học phí, lịch học.',
    status: 'connected', account: 'JP Center – Tiếng Nhật', connectedAt: ago(60 * 24 * 200), connectedBy: 'Đỗ Quang Huy', lastSyncAt: ago(4),
    fields: [{ key: 'oaId', label: 'OA ID' }, { key: 'appId', label: 'App ID' }, { key: 'secret', label: 'Secret key', secret: true }],
    config: { oaId: '4318826621947740', appId: '1290556631', secret: 'zx9K2mPq7vL0aB4n' },
    events: [
      { key: 'attendance', label: 'Báo vắng học cho phụ huynh', on: true },
      { key: 'tuition', label: 'Nhắc học phí sắp đến hạn / quá hạn', on: true },
      { key: 'schedule', label: 'Đổi lịch / hủy buổi học', on: true },
      { key: 'result', label: 'Kết quả kiểm tra', on: false },
    ],
    usage: { label: 'tin ZNS đã gửi', value: 1842, failed: 37 },
  },
  {
    id: 'sms', name: 'SMS Brandname', category: 'notify', description: 'Dự phòng khi Zalo không gửi được; gửi OTP đăng nhập.',
    status: 'connected', account: 'JPCENTER', connectedAt: ago(60 * 24 * 300), connectedBy: 'Đỗ Quang Huy', lastSyncAt: ago(55),
    fields: [{ key: 'brandname', label: 'Brandname' }, { key: 'apiKey', label: 'API key', secret: true }],
    config: { brandname: 'JPCENTER', apiKey: 'sk_live_88f1c0a2d9' },
    events: [{ key: 'otp', label: 'OTP đăng nhập / quên mật khẩu', on: true }, { key: 'fallback', label: 'Gửi thay khi Zalo lỗi', on: true }],
    usage: { label: 'SMS đã gửi', value: 312, failed: 2 },
  },
  {
    id: 'email', name: 'Email (SMTP)', category: 'notify', description: 'Gửi hóa đơn, phiếu thu, link test xếp lớp, thư mời tài khoản.',
    status: 'connected', account: 'no-reply@jpcenter.vn', connectedAt: ago(60 * 24 * 400), connectedBy: 'Phạm Anh Tuấn', lastSyncAt: ago(12),
    fields: [{ key: 'host', label: 'SMTP host' }, { key: 'port', label: 'Cổng' }, { key: 'user', label: 'Tài khoản' }, { key: 'pass', label: 'Mật khẩu', secret: true }],
    config: { host: 'smtp.gmail.com', port: '587', user: 'no-reply@jpcenter.vn', pass: 'app-password-xyz' },
    events: [{ key: 'invoice', label: 'Hóa đơn & phiếu thu', on: true }, { key: 'invite', label: 'Mời tạo tài khoản', on: true }, { key: 'test', label: 'Link test xếp lớp', on: true }],
    usage: { label: 'email đã gửi', value: 964, failed: 5 },
  },
  {
    id: 'vietqr', name: 'VietQR + Webhook ngân hàng', category: 'payment', description: 'Sinh mã QR theo hóa đơn, nhận biến động số dư để tự đối soát.',
    status: 'connected', account: '3 tài khoản chi nhánh', connectedAt: ago(60 * 24 * 150), connectedBy: 'Hoàng Thị Lan', lastSyncAt: ago(2),
    fields: [{ key: 'clientId', label: 'Client ID' }, { key: 'apiKey', label: 'API key', secret: true }, { key: 'checksum', label: 'Checksum key', secret: true }],
    config: { clientId: 'jpc-2291', apiKey: 'qr_live_7d1e9a', checksum: 'cs_31be77' },
    events: [{ key: 'qr', label: 'In mã QR trên hóa đơn', on: true }, { key: 'autoMatch', label: 'Tự khớp giao dịch theo nội dung chuyển khoản', on: true }],
    usage: { label: 'giao dịch nhận', value: 428, failed: 9 },
    webhook: 'https://api.jpcenter.vn/webhooks/bank/7f3a9c',
  },
  {
    id: 'zoom', name: 'Zoom', category: 'meeting', description: 'Tự tạo phòng họp cho buổi học online, gửi link vào lịch học viên.',
    status: 'error', account: 'zoom@jpcenter.vn', connectedAt: ago(60 * 24 * 90), connectedBy: 'Phạm Anh Tuấn', lastSyncAt: ago(60 * 26),
    error: 'Token hết hạn (401 Unauthorized) — cần kết nối lại. 3 buổi học online sắp tới chưa có link.',
    fields: [{ key: 'accountId', label: 'Account ID' }, { key: 'clientId', label: 'Client ID' }, { key: 'clientSecret', label: 'Client secret', secret: true }],
    config: { accountId: 'aZk2-91pQ', clientId: 'Zm_8812', clientSecret: 'zs_old_secret' },
    events: [{ key: 'autoRoom', label: 'Tự tạo phòng khi lên lịch buổi online', on: true }, { key: 'record', label: 'Lưu bản ghi vào tài liệu lớp', on: false }],
    usage: { label: 'phòng đã tạo', value: 64, failed: 3 },
  },
  {
    id: 'meet', name: 'Google Meet', category: 'meeting', description: 'Thay thế Zoom cho lớp nhỏ / học thử.',
    status: 'disconnected', fields: [{ key: 'domain', label: 'Google Workspace domain' }], config: {},
    events: [{ key: 'autoRoom', label: 'Tự tạo phòng khi lên lịch buổi online', on: false }],
  },
  {
    id: 'fb-lead', name: 'Facebook Lead Ads', category: 'lead', description: 'Lead từ form quảng cáo Facebook tự vào CRM, gắn chiến dịch theo UTM, chia theo quy tắc.',
    status: 'connected', account: 'Fanpage: JP Center – Học tiếng Nhật', connectedAt: ago(60 * 24 * 120), connectedBy: 'Ngô Bảo Anh', lastSyncAt: ago(48),
    fields: [{ key: 'pageId', label: 'Page ID' }, { key: 'token', label: 'Page access token', secret: true }],
    config: { pageId: '102938475610', token: 'EAAG_page_token' },
    events: [{ key: 'import', label: 'Tạo lead tự động', on: true }, { key: 'dedupe', label: 'Bỏ qua nếu trùng SĐT (ghi chú vào lead cũ)', on: true }],
    usage: { label: 'lead nhận về', value: 86, failed: 1 },
  },
  {
    id: 'web-form', name: 'Form website', category: 'lead', description: 'Form đăng ký tư vấn / học thử trên jpcenter.vn gửi thẳng vào CRM.',
    status: 'connected', account: 'jpcenter.vn', connectedAt: ago(60 * 24 * 380), connectedBy: 'Phạm Anh Tuấn', lastSyncAt: ago(6),
    fields: [{ key: 'domain', label: 'Tên miền cho phép' }], config: { domain: 'jpcenter.vn' },
    events: [{ key: 'import', label: 'Tạo lead tự động', on: true }, { key: 'captcha', label: 'Bắt buộc captcha', on: true }],
    usage: { label: 'lead nhận về', value: 54, failed: 0 },
    webhook: 'https://api.jpcenter.vn/webhooks/lead/web-4c21',
  },
  {
    id: 'tiktok-lead', name: 'TikTok Lead Generation', category: 'lead', description: 'Lead từ form Instant Form trên TikTok.',
    status: 'disconnected', fields: [{ key: 'advertiserId', label: 'Advertiser ID' }, { key: 'token', label: 'Access token', secret: true }], config: {},
    events: [{ key: 'import', label: 'Tạo lead tự động', on: false }],
  },
];

export const MOCK_INTEGRATION_LOGS: IntegrationLog[] = [
  { id: 'IL-1', integrationId: 'zalo-oa', at: ago(4), ok: true, message: 'Gửi ZNS "Báo vắng" cho PH Nguyễn Văn Bình (HV Nguyễn Văn An)' },
  { id: 'IL-2', integrationId: 'zalo-oa', at: ago(35), ok: false, message: 'Gửi ZNS thất bại: người nhận chưa quan tâm OA → chuyển SMS' },
  { id: 'IL-3', integrationId: 'zalo-oa', at: ago(90), ok: true, message: 'Gửi 42 ZNS "Nhắc học phí" (lô tự động 9:00)' },
  { id: 'IL-4', integrationId: 'sms', at: ago(35), ok: true, message: 'Gửi SMS thay thế cho 0987654321' },
  { id: 'IL-5', integrationId: 'vietqr', at: ago(2), ok: true, message: 'Nhận +4.500.000₫ "HD CG2611 NGUYEN VAN AN" → khớp hóa đơn tự động' },
  { id: 'IL-6', integrationId: 'vietqr', at: ago(70), ok: false, message: 'Nhận +1.200.000₫ nội dung "chuyen tien hoc" → không khớp, chờ đối soát tay' },
  { id: 'IL-7', integrationId: 'zoom', at: ago(60 * 26), ok: false, message: '401 Unauthorized khi tạo phòng cho buổi N3-K12 #18' },
  { id: 'IL-8', integrationId: 'zoom', at: ago(60 * 27), ok: true, message: 'Tạo phòng cho buổi KAIWA-ON #6' },
  { id: 'IL-9', integrationId: 'fb-lead', at: ago(48), ok: true, message: 'Lead mới Phạm Gia Huy (form "Khai giảng N5") → chia cho Trần Minh Khoa' },
  { id: 'IL-10', integrationId: 'fb-lead', at: ago(60 * 5), ok: false, message: 'Lead trùng SĐT 0912345678 → ghi chú vào LD0001' },
  { id: 'IL-11', integrationId: 'web-form', at: ago(6), ok: true, message: 'Lead mới Võ Thanh Tâm (form "Học thử Kaiwa")' },
  { id: 'IL-12', integrationId: 'email', at: ago(12), ok: true, message: 'Gửi hóa đơn HD-CG-0192 tới binh.nguyen@gmail.com' },
];

// ---------------- Nhật ký ----------------

const DEV = ['Chrome · Windows', 'Safari · iPhone', 'Edge · Windows', 'Chrome · macOS', 'Chrome · Android'];
let n = 0;
const log = (min: number, actor: string, actorRole: string, l: Omit<AuditLog, 'id' | 'at' | 'actor' | 'actorRole' | 'ip' | 'device'>): AuditLog => ({
  id: `AU-${++n}`, at: ago(min), actor, actorRole, ip: `113.190.${(n * 37) % 255}.${(n * 91) % 255}`, device: DEV[n % DEV.length], ...l,
});

export const MOCK_AUDIT_LOGS: AuditLog[] = [
  log(3, 'Trần Minh Khoa', 'Sale', { branchId: 'q1', category: 'auth', severity: 'info', action: 'Đăng nhập', target: 'sale@jpcenter.vn' }),
  log(9, 'Hoàng Thị Lan', 'Kế toán', { branchId: 'cg', category: 'finance', severity: 'warning', action: 'Hoàn tiền', target: 'Hóa đơn HD-CG-0174', detail: 'Hoàn 2.000.000₫ do học viên chuyển lớp.', changes: [{ field: 'Đã thu', before: '6.000.000₫', after: '4.000.000₫' }] }),
  log(22, 'Lê Hoàng Nam', 'Quản lý chi nhánh', { branchId: 'cg', category: 'permission', severity: 'warning', action: 'Cấp quyền lẻ', target: 'Vũ Ngọc Mai', detail: 'Tài chính › Công nợ › Xem (chi nhánh) đến hết 7 ngày tới.', changes: [{ field: 'finance.debts.view', before: '—', after: 'Chi nhánh' }] }),
  log(41, 'Ngô Bảo Anh', 'Marketing', { category: 'export', severity: 'warning', action: 'Xuất Excel', target: 'Danh sách lead (186 dòng)', detail: 'Lọc: nguồn Facebook, 30 ngày.' }),
  log(65, 'không xác định', '—', { category: 'auth', severity: 'critical', action: 'Đăng nhập thất bại 5 lần', target: 'admin@jpcenter.vn', detail: 'Tài khoản tạm khóa 15 phút do sai mật khẩu liên tiếp.' }),
  log(120, 'Vũ Ngọc Mai', 'Giáo vụ', { branchId: 'cg', category: 'data', severity: 'info', action: 'Chuyển lớp', target: 'HV Nguyễn Văn An', changes: [{ field: 'Lớp', before: 'N4-K08', after: 'N3-K12' }] }),
  log(180, 'Phạm Thu Trang', 'Admin', { category: 'setting', severity: 'info', action: 'Sửa bảng giá', target: 'Khóa JLPT N3 — Cầu Giấy', changes: [{ field: 'Học phí', before: '5.800.000₫', after: '6.200.000₫' }] }),
  log(240, 'Đỗ Quang Huy', 'Super Admin', { category: 'permission', severity: 'warning', action: 'Sửa role', target: 'Trưởng nhóm Sale', changes: [{ field: 'crm.lead-rules.update', before: '—', after: 'Chi nhánh' }] }),
  log(60 * 6, 'Lê Hoàng Nam', 'Quản lý chi nhánh', { branchId: 'cg', category: 'data', severity: 'critical', action: 'Hủy lớp', target: 'Lớp KIDS-K03', detail: 'Không đủ sĩ số. 4 học viên được chuyển / bảo lưu, 2 hóa đơn hoàn tiền.' }),
  log(60 * 8, 'Hoàng Thị Lan', 'Kế toán', { category: 'export', severity: 'warning', action: 'Xuất Excel', target: 'Bảng lương nhân viên tháng 10' }),
  log(60 * 11, 'Phạm Anh Tuấn', 'Admin', { category: 'setting', severity: 'warning', action: 'Sửa tích hợp', target: 'Email (SMTP)', changes: [{ field: 'Mật khẩu', before: '••••', after: '•••• (mới)' }] }),
  log(60 * 20, 'Đinh Quốc Bảo', 'Trưởng nhóm Sale', { branchId: 'cg', category: 'data', severity: 'info', action: 'Giao lead hàng loạt', target: '12 lead', detail: 'Giao cho Hồ Quỳnh Như.' }),
  log(60 * 26, 'Hệ thống', 'Tự động', { category: 'setting', severity: 'critical', action: 'Tích hợp lỗi', target: 'Zoom', detail: '401 Unauthorized — token hết hạn.' }),
  log(60 * 30, 'Lâm Thị Hiền', 'Quản lý chi nhánh', { branchId: 'hc', category: 'data', severity: 'warning', action: 'Ẩn học viên', target: 'HV Đỗ Minh Tuấn', detail: 'Đã hoàn thành khóa, không còn học lớp nào.' }),
  log(60 * 36, 'Phạm Thu Trang', 'Admin', { category: 'permission', severity: 'warning', action: 'Chặn quyền', target: 'Trần Minh Khoa', detail: 'Tuyển sinh › Lead › Xuất file.' }),
  log(60 * 48, 'Hoàng Thị Lan', 'Kế toán', { category: 'finance', severity: 'warning', action: 'Chốt lương', target: 'Lương giáo viên tháng 10', detail: 'Khóa sổ — không sửa được công / buổi dạy tháng 10.' }),
  log(60 * 50, 'Đỗ Quang Huy', 'Super Admin', { category: 'permission', severity: 'critical', action: 'Khóa tài khoản', target: 'Mai Văn Đạt', detail: 'Nghỉ việc. Đã bàn giao 23 lead cho Bùi Ngọc Hân.' }),
  log(60 * 72, 'Vũ Ngọc Mai', 'Giáo vụ', { branchId: 'hc', category: 'export', severity: 'info', action: 'In danh sách', target: 'Danh sách lớp N5-K21' }),
  log(60 * 96, 'Châu Minh Đức', 'Quản lý chi nhánh', { branchId: 'q1', category: 'finance', severity: 'info', action: 'Duyệt giảm giá', target: 'Hóa đơn HD-Q1-0088', changes: [{ field: 'Giảm giá', before: '0₫', after: '500.000₫' }] }),
  log(60 * 120, 'Đỗ Quang Huy', 'Super Admin', { category: 'setting', severity: 'info', action: 'Thêm chi nhánh', target: 'Thủ Đức', detail: 'Trạng thái: Đang chuẩn bị.' }),
];
