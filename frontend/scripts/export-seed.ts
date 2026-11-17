/**
 * Xuất dữ liệu giả (mock) của frontend thành JSON để backend nạp vào PostgreSQL làm dữ liệu mẫu.
 * Dữ liệu mẫu backend vì thế giống hệt những gì giao diện đang hiển thị.
 *
 * Chạy (từ thư mục frontend):  npm run export-seed
 * Kết quả: backend/src/Modules/{System,Hr}/.../Infrastructure/Persistence/Seed/*.seed.json
 * Thời gian tương đối (dayjs "x ngày trước") được chốt tại lúc chạy script.
 */
import { mkdirSync, writeFileSync } from 'node:fs';
import { dirname, resolve } from 'node:path';
import { MOCK_EMPLOYEES } from '@/mocks/data/employees';
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
