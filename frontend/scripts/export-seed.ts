/**
 * Xuất dữ liệu giả (mock) của frontend thành JSON để backend nạp vào PostgreSQL làm dữ liệu mẫu.
 * Dữ liệu mẫu backend vì thế giống hệt những gì giao diện đang hiển thị.
 *
 * Chạy (từ thư mục frontend):  npm run export-seed
 * Kết quả: backend/src/Modules/{System,Hr,Academic,Students,Crm}/.../Infrastructure/Persistence/Seed/*.seed.json
 * Thời gian tương đối (dayjs "x ngày trước") được chốt tại lúc chạy script.
 */
import { createHash } from 'node:crypto';
import { mkdirSync, writeFileSync } from 'node:fs';
import { dirname, resolve } from 'node:path';
import { MOCK_EMPLOYEES } from '@/mocks/data/employees';
import { MOCK_COURSES } from '@/mocks/data/courses';
import { MOCK_COURSE_CURRICULA, MOCK_CURRICULA } from '@/mocks/data/curricula';
import { MOCK_CLASSES } from '@/mocks/data/classes';
import { MOCK_SESSIONS } from '@/mocks/data/attendance';
import { MOCK_ROOM_ISSUES, MOCK_ROOMS } from '@/mocks/data/rooms';
import { MOCK_QUESTIONS } from '@/mocks/data/questions';
import { MOCK_CONSULTATIONS, MOCK_LEADS, MOCK_LINKS } from '@/mocks/data/crm';
import { MOCK_ACCOUNTS as PORTAL_ACCOUNTS, MOCK_ENROLLMENTS, MOCK_STUDENTS } from '@/mocks/data/students';
import {
  MOCK_AUDIT_LOGS,
  MOCK_BRANCH_PROFILES,
  MOCK_INTEGRATION_LOGS,
  MOCK_INTEGRATIONS,
  MOCK_ROLES,
  MOCK_USER_ACCESS,
} from '@/mocks/data/system';
import { MOCK_ACCOUNTS as DEMO_ACCOUNTS } from '@/mocks/data/users';
import { PERM_CATALOG } from '@/features/system/constants';

const backend = resolve(process.cwd(), '..', 'backend', 'src', 'Modules');

function write(path: string, data: unknown) {
  mkdirSync(dirname(path), { recursive: true });
  writeFileSync(path, JSON.stringify(data, null, 2) + '\n', 'utf8');
  console.log('✓', path);
}

// Email đăng nhập demo (superadmin@, giaovu@...) thay cho email hồ sơ để đăng nhập giống bản mock
const demoEmailByEmployee = new Map(DEMO_ACCOUNTS.filter((a) => a.employeeId).map((a) => [a.employeeId!, a.email]));
// Tài khoản demo học viên / phụ huynh → gắn vào tài khoản cổng tương ứng
const DEMO_PORTAL: Record<string, string> = { 'ST-1': 'hocvien@jpcenter.vn', 'L-0001': 'phuhuynh@jpcenter.vn' };

const studentName = new Map(MOCK_STUDENTS.map((s) => [s.id, s.fullName]));
const leadName = new Map(MOCK_LEADS.map((l) => [l.id, l.fullName]));

// Danh mục quyền (module → trang → hành động) — backend dùng để kiểm tra mã quyền hợp lệ, KHÔNG phải dữ liệu mẫu
write(resolve(backend, 'System', 'JPCenter.Modules.System', 'Infrastructure', 'Services', 'permission-catalog.json'), {
  generatedAt: new Date().toISOString(),
  globalOnlyModules: ['system'],
  modules: PERM_CATALOG,
});

write(resolve(backend, 'Hr', 'JPCenter.Modules.Hr', 'Infrastructure', 'Persistence', 'Seed', 'hr.seed.json'), {
  generatedAt: new Date().toISOString(),
  employees: MOCK_EMPLOYEES,
});

// Khóa học + lộ trình (user story 09). Người soạn: tên → Id nhân viên
const employeeIdByName = new Map(MOCK_EMPLOYEES.map((e) => [e.fullName, e.id]));
write(resolve(backend, 'Academic', 'JPCenter.Modules.Academic', 'Infrastructure', 'Persistence', 'Seed', 'academic.seed.json'), {
  generatedAt: new Date().toISOString(),
  courses: MOCK_COURSES,
  curricula: MOCK_CURRICULA.map((c) => ({ ...c, authorId: employeeIdByName.get(c.author) ?? null })),
  courseCurricula: MOCK_COURSE_CURRICULA,
});

// Ngân hàng câu hỏi (user story 11). Người soạn / người duyệt lưu theo TÊN → Id nhân viên; bài học giữ Id mock ("CUR-N5-V2-L3" = bài thứ 3)
const nameId = (name?: string) => (name ? (employeeIdByName.get(name.replace(/\s*\(.*\)\s*$/, '').trim()) ?? null) : null);
write(resolve(backend, 'Academic', 'JPCenter.Modules.Academic', 'Infrastructure', 'Persistence', 'Seed', 'questions.seed.json'), {
  generatedAt: new Date().toISOString(),
  questions: MOCK_QUESTIONS.map((q) => ({
    id: q.id, code: q.code, level: q.level, skill: q.skill, format: q.format, difficulty: q.difficulty, stem: q.stem, passage: q.passage,
    audio: q.audio, transcript: q.transcript, options: q.options, answer: q.answer, explanation: q.explanation, curriculumId: q.curriculumId,
    lessonIds: q.lessonIds, tags: q.tags, status: q.status, authorId: nameId(q.author), createdAt: q.createdAt,
    review: q.review ? { byId: nameId(q.review.by), at: q.review.at, note: q.review.note } : null,
    stats: q.stats,
  })),
});

// Phòng, lớp, buổi học (user story 07). Người thao tác lưu theo TÊN ("Phạm Thị Hồng (Giáo vụ)") → Id nhân viên nếu có, không có → null
const personId = (name?: string) => (name ? (employeeIdByName.get(name.replace(/\s*\(.*\)\s*$/, '').trim()) ?? null) : null);
write(resolve(backend, 'Academic', 'JPCenter.Modules.Academic', 'Infrastructure', 'Persistence', 'Seed', 'classes.seed.json'), {
  generatedAt: new Date().toISOString(),
  rooms: MOCK_ROOMS,
  roomIssues: MOCK_ROOM_ISSUES.map((i) => ({ ...i, reportedById: personId(i.reportedBy) })),
  classes: MOCK_CLASSES,
  sessions: MOCK_SESSIONS.map((x) => ({
    ...x,
    attendanceById: personId(x.attendanceBy?.name),
    confirmedById: personId(x.confirmation?.by),
    cancelledById: personId(x.cancelledBy?.name),
    supportLog: x.supportLog?.map((e) => ({ ...e, byId: personId(e.by) })),
    incident: x.incident && { ...x.incident, reportedById: personId(x.incident.reportedBy), outcomeById: personId(x.incident.outcome?.by) },
    teacherLog: x.teacherLog && { ...x.teacherLog, authorId: personId(x.teacherLog.author) },
  })),
});

// Học viên + ghi danh + chứng nhận (user story 06). Người gửi chứng nhận: tên → Id nhân viên
write(resolve(backend, 'Students', 'JPCenter.Modules.Students', 'Infrastructure', 'Persistence', 'Seed', 'students.seed.json'), {
  generatedAt: new Date().toISOString(),
  students: MOCK_STUDENTS,
  // Chi nhánh của LỚP (Students không đọc bảng Academic)
  enrollments: MOCK_ENROLLMENTS.map((e) => ({
    ...e,
    branchId: MOCK_CLASSES.find((c) => c.id === e.classId)?.branchId,
    certificate: e.certificate && { ...e.certificate, sentById: personId(e.certificate.sentBy) },
  })),
});

// Đơn hủy buổi, lời mời dạy thay, đơn xin nghỉ, lên lớp, nhận xét cuối khóa (user story 07 — giai đoạn 15).
// Import ĐỘNG sau khi đã ghi classes.seed.json: mock đơn xin nghỉ sửa điểm danh của MOCK_SESSIONS lúc nạp module —
// backend tự áp hiệu lực đơn (vắng có phép, khách học bù) bằng method domain, nên dữ liệu buổi học xuất ở trên phải là bản gốc.
const { MOCK_CANCEL_REQUESTS, MOCK_SUB_INVITES, MOCK_LEAVE_REQUESTS } = await import('@/mocks/data/portal');
const { MOCK_PROMOTIONS } = await import('@/mocks/data/promotions');
const { MOCK_EVALUATIONS, MOCK_CARE_LOGS } = await import('@/mocks/data/classExtras');
const { useAtRiskStore } = await import('@/features/student/atRisk/atRisk');
const classBranch = (classId: string) => MOCK_CLASSES.find((c) => c.id === classId)?.branchId ?? null;
/** Tài khoản cổng gửi đơn: HV → tài khoản của HV; PH → tài khoản PH có tên trùng người gửi */
const portalAccountOf = (studentId: string, by: 'student' | 'parent', byName: string) =>
  (by === 'student'
    ? PORTAL_ACCOUNTS.find((a) => a.ownerType === 'student' && a.ownerId === studentId)
    : PORTAL_ACCOUNTS.find((a) => a.ownerType !== 'student' && leadName.get(a.ownerId) === byName)
  )?.id ?? null;
write(resolve(backend, 'Academic', 'JPCenter.Modules.Academic', 'Infrastructure', 'Persistence', 'Seed', 'requests.seed.json'), {
  generatedAt: new Date().toISOString(),
  cancelRequests: MOCK_CANCEL_REQUESTS.map((r) => ({ ...r, branchId: classBranch(r.classId), decidedById: personId(r.decidedBy) })),
  subInvites: MOCK_SUB_INVITES.map((i) => ({ ...i, branchId: classBranch(i.classId), invitedById: personId(i.invitedBy) })),
  leaveRequests: MOCK_LEAVE_REQUESTS.map((r) => ({
    ...r,
    branchId: classBranch(r.classId),
    byAccountId: portalAccountOf(r.studentId, r.by, r.byName),
    decidedById: personId(r.decidedBy),
  })),
  promotions: MOCK_PROMOTIONS.map((p) => ({
    ...p,
    branchId: classBranch(p.fromClassId),
    notes: p.notes.map((n) => ({ ...n, byId: personId(n.by) })),
  })),
  evaluations: MOCK_EVALUATIONS,
});

// Tuyển sinh (user story 05 — giai đoạn 18): lead, lịch sử tư vấn, liên kết HV, quy tắc chia lead, test xếp lớp, lịch hẹn, chiến dịch, mẫu tin
const { MOCK_ASSIGN_LOGS, MOCK_ATTEMPTS, MOCK_CAMPAIGNS, MOCK_LEAD_RULES, MOCK_PLACEMENT_TESTS } = await import('@/mocks/data/admissions');
const { useAppointmentStore } = await import('@/features/crm/appointments/appointments');
const { useMessageStore } = await import('@/features/crm/messages/messages');
write(resolve(backend, 'Crm', 'JPCenter.Modules.Crm', 'Infrastructure', 'Persistence', 'Seed', 'crm.seed.json'), crmSeed());

// Cảnh báo bỏ học (HV-14): trường hợp đã giao người chăm sóc + nhật ký liên hệ. Mức rủi ro / dấu hiệu do job backend tính lại
write(resolve(backend, 'Students', 'JPCenter.Modules.Students', 'Infrastructure', 'Persistence', 'Seed', 'care.seed.json'), {
  generatedAt: new Date().toISOString(),
  cases: Object.entries(useAtRiskStore.getState().cases).map(([key, c]) => {
    const [classId, studentId] = key.split(':');
    return { classId, studentId, branchId: classBranch(classId), assigneeId: personId(c.assignee), status: c.status, nextAt: c.nextAt, updatedAt: c.updatedAt };
  }),
  logs: MOCK_CARE_LOGS.map((l) => ({ ...l, branchId: classBranch(l.classId), byId: personId(l.by) })),
});

write(resolve(backend, 'System', 'JPCenter.Modules.System', 'Infrastructure', 'Persistence', 'Seed', 'system.seed.json'), {
  generatedAt: new Date().toISOString(),
  branches: MOCK_BRANCH_PROFILES,
  roles: MOCK_ROLES,
  staffAccounts: MOCK_USER_ACCESS.map((a) => ({
    ...a,
    email: demoEmailByEmployee.get(a.userId) ?? a.email,
    fullName: MOCK_EMPLOYEES.find((e) => e.id === a.userId)?.fullName ?? a.email,
  })),
  portalAccounts: PORTAL_ACCOUNTS.map((a) => ({
    ...a,
    email: DEMO_PORTAL[a.ownerId] ?? a.email,
    displayName: (a.ownerType === 'student' ? studentName.get(a.ownerId) : leadName.get(a.ownerId)) ?? a.username,
  })),
  integrations: MOCK_INTEGRATIONS,
  integrationLogs: MOCK_INTEGRATION_LOGS,
  auditLogs: MOCK_AUDIT_LOGS,
});

function crmSeed() {
  // Mã khóa quan tâm của mock ("n5", "kaiwa", "kids"…) → khóa học đầu tiên khớp (nhóm, hoặc JLPT theo trình độ đích)
  const courseOf = (code: string) =>
    MOCK_COURSES.find((c) => c.category === code || (c.category === 'jlpt' && c.levelTo.toLowerCase() === code))?.id ?? null;
  return {
    generatedAt: new Date().toISOString(),
    leads: MOCK_LEADS.map((l) => ({
      id: l.id, code: l.code, fullName: l.fullName, phone: l.phone, email: l.email, source: l.source, status: l.status, branchId: l.branchId,
      assigneeId: l.assigneeId, interest: l.interest, note: l.note, lostReason: l.lostReason, hidden: l.hidden, hiddenAt: l.hiddenAt,
      createdAt: l.createdAt, nextFollowUpAt: l.nextFollowUpAt, notifyChannels: l.notifyChannels ?? [], campaignId: l.campaignId,
      slaEscalatedAt: l.slaEscalatedAt, slaReassignedAt: l.slaReassignedAt,
    })),
    consultations: MOCK_CONSULTATIONS.map((c) => ({ ...c, staffId: personId(c.staffName) })),
    links: MOCK_LINKS.map((k) => ({ ...k, studentName: studentName.get(k.studentId) ?? k.studentId })),
    rules: MOCK_LEAD_RULES.map((r) => ({ ...r, escalateToIds: r.escalateTo.map(personId).filter(Boolean) })),
    assignmentLogs: MOCK_ASSIGN_LOGS,
    placementTests: MOCK_PLACEMENT_TESTS.map((t) => ({ ...t, authorId: personId(t.author) })),
    attempts: MOCK_ATTEMPTS.map((a) => ({ ...a, sentById: personId(a.sentBy) })),
    appointments: useAppointmentStore.getState().appointments,
    campaigns: MOCK_CAMPAIGNS.map((c) => ({
      ...c, ownerId: personId(c.owner), courseIds: c.courses.map(courseOf).filter(Boolean),
    })),
    templates: useMessageStore.getState().templates,
  };
}

// Bảng đổi Id mock ↔ GUID backend (giống SeedData.IdOf: MD5 "jpcenter-seed:" + id, đánh dấu UUID v3) — frontend dùng khi nối API thật
function idOf(mockId: string): string {
  const h = createHash('md5').update('jpcenter-seed:' + mockId, 'utf8').digest();
  h[6] = (h[6] & 0x0f) | 0x30;
  h[8] = (h[8] & 0x3f) | 0x80;
  const x = h.toString('hex');
  return `${x.slice(0, 8)}-${x.slice(8, 12)}-${x.slice(12, 16)}-${x.slice(16, 20)}-${x.slice(20)}`;
}
const seedIds = [
  ...MOCK_BRANCH_PROFILES.map((b) => b.id),
  ...MOCK_ROLES.map((r) => r.id),
  ...MOCK_EMPLOYEES.map((e) => e.id),
  ...MOCK_STUDENTS.map((x) => x.id),
  ...MOCK_ENROLLMENTS.map((x) => x.id),
  ...MOCK_LEADS.map((x) => x.id),
  ...PORTAL_ACCOUNTS.map((a) => a.id),
  ...MOCK_COURSES.map((c) => c.id),
  ...MOCK_CURRICULA.map((c) => c.id),
  ...MOCK_ROOMS.map((r) => r.id),
  ...MOCK_CLASSES.map((c) => c.id),
  ...MOCK_SESSIONS.map((x) => x.id),
  ...MOCK_CANCEL_REQUESTS.map((x) => x.id),
  ...MOCK_SUB_INVITES.map((x) => x.id),
  ...MOCK_LEAVE_REQUESTS.map((x) => x.id),
  ...MOCK_PROMOTIONS.map((x) => x.id),
  ...MOCK_EVALUATIONS.map((x) => x.id),
  ...MOCK_CARE_LOGS.map((x) => x.id),
  ...MOCK_QUESTIONS.map((x) => x.id),
  ...MOCK_LEAD_RULES.map((x) => x.id),
  ...MOCK_PLACEMENT_TESTS.map((x) => x.id),
  ...MOCK_ATTEMPTS.map((x) => x.id),
  ...MOCK_CAMPAIGNS.map((x) => x.id),
  ...useAppointmentStore.getState().appointments.map((x) => x.id),
  ...useMessageStore.getState().templates.map((x) => x.id),
];
const idPath = resolve(process.cwd(), 'src', 'lib', 'api', 'seedIds.generated.ts');
mkdirSync(dirname(idPath), { recursive: true });
writeFileSync(
  idPath,
  `// TỰ SINH bởi \`npm run export-seed\` — không sửa tay. Id mock → GUID dữ liệu mẫu backend.
` +
    `export const SEED_IDS: Record<string, string> = ${JSON.stringify(Object.fromEntries([...new Set(seedIds)].map((id) => [id, idOf(id)])), null, 2)};
`,
  'utf8',
);
console.log('✓', idPath);

