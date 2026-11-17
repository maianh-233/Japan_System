/**
 * Xuất dữ liệu giả (mock) của frontend thành JSON để backend nạp vào PostgreSQL làm dữ liệu mẫu.
 * Dữ liệu mẫu backend vì thế giống hệt những gì giao diện đang hiển thị.
 *
 * Chạy (từ thư mục frontend):  npm run export-seed
 * Kết quả: backend/src/Modules/{System,Hr,Academic}/.../Infrastructure/Persistence/Seed/*.seed.json
 * Thời gian tương đối (dayjs "x ngày trước") được chốt tại lúc chạy script.
 */
import { createHash } from 'node:crypto';
import { mkdirSync, writeFileSync } from 'node:fs';
import { dirname, resolve } from 'node:path';
import { MOCK_EMPLOYEES } from '@/mocks/data/employees';
import { MOCK_COURSES } from '@/mocks/data/courses';
import { MOCK_COURSE_CURRICULA, MOCK_CURRICULA } from '@/mocks/data/curricula';
import { MOCK_LEADS } from '@/mocks/data/crm';
import { MOCK_ACCOUNTS as PORTAL_ACCOUNTS, MOCK_STUDENTS } from '@/mocks/data/students';
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
  ...MOCK_LEADS.map((x) => x.id),
  ...PORTAL_ACCOUNTS.map((a) => a.id),
  ...MOCK_COURSES.map((c) => c.id),
  ...MOCK_CURRICULA.map((c) => c.id),
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

