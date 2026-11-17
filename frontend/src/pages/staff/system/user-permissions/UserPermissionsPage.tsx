import { useMemo, useState } from 'react';
import { Form, Tooltip } from 'antd';
import dayjs from 'dayjs';
import { Ban, CalendarClock, KeyRound, UserCheck, UserPlus, Users, UserX } from 'lucide-react';
import { Button, DataTable, ModalForm, PageHeader, Select, StatCard, StatusTag, UserCell, type DataColumn } from '@/components/common';
import { useHrStore } from '@/features/hr/hrStore';
import type { Employee } from '@/features/hr/types';
import { canGrant, countUsable, effectivePerms, isActiveOverride, roleGrants } from '@/features/system/access';
import { ACCOUNT_STATUS } from '@/features/system/constants';
import { useSystemStore } from '@/features/system/systemStore';
import type { UserAccess } from '@/features/system/types';
import { UserAccessDrawer } from '@/features/system/UserAccessDrawer';
import { useActor } from '@/features/system/useActor';
import { useAuth } from '@/hooks/useAuth';
import { fmtDateTime, fromNow } from '@/lib/date';
import { getBranches } from '@/features/system/useBranches';
import { ALL_BRANCHES } from '@/types/auth';

const branchCode = (id: string) => getBranches(false).find((b) => b.id === id)?.code ?? id;

interface Row extends UserAccess {
  emp: Employee;
  name: string;
  perms: number;
}

/** Phân quyền người dùng: vai trò theo chi nhánh, quyền lẻ có thời hạn, khóa / mở tài khoản. DỮ LIỆU GIẢ. */
export default function UserPermissionsPage() {
  const { branchId, can } = useAuth();
  const { actor, granter } = useActor();
  const { roles, access, addAccess } = useSystemStore();
  const employees = useHrStore((s) => s.employees);
  const editable = can('system.user-permissions');

  const [role, setRole] = useState<string>();
  const [status, setStatus] = useState<string>();
  const [special, setSpecial] = useState<string>();
  const [openId, setOpenId] = useState<string | null>(null);
  const [inviting, setInviting] = useState(false);

  const roleName = (id: string) => roles.find((r) => r.id === id)?.name ?? id;

  const all: Row[] = useMemo(
    () =>
      access
        .map((a) => {
          const emp = employees.find((e) => e.id === a.userId);
          return emp ? { ...a, emp, name: emp.fullName, perms: countUsable(effectivePerms(a, roles)) } : null;
        })
        .filter((x): x is Row => !!x),
    [access, employees, roles],
  );

  const inBranch = (r: Row) => branchId === ALL_BRANCHES || r.emp.branchId === branchId || r.roles.some((x) => !x.branchIds.length || x.branchIds.includes(branchId));
  const base = all.filter(inBranch);
  const idle = (r: Row) => r.status === 'active' && (!r.lastLoginAt || dayjs().diff(dayjs(r.lastLoginAt), 'day') > 30);
  const soon = (r: Row) => r.overrides.some((o) => o.until && isActiveOverride(o.until) && dayjs(o.until).diff(dayjs(), 'day') <= 7);
  const rows = base.filter(
    (r) =>
      (!role || r.roles.some((x) => x.roleId === role)) &&
      (!status || r.status === status) &&
      (!special ||
        (special === 'override' && r.overrides.some((o) => isActiveOverride(o.until))) ||
        (special === 'multi' && r.roles.length > 1) ||
        (special === 'idle' && idle(r))),
  );

  const noAccount = employees.filter((e) => e.status !== 'quit' && !access.some((a) => a.userId === e.id));

  const columns: DataColumn<Row>[] = [
    { key: 'name', title: 'Người dùng', width: 230, mobile: 'title', sorter: (a, b) => a.name.localeCompare(b.name, 'vi'), render: (_, r) => <UserCell name={r.name} subtitle={r.email} size="sm" /> },
    {
      key: 'roles',
      title: 'Vai trò @ chi nhánh',
      width: 260,
      render: (_, r) => (
        <span className="flex flex-wrap gap-1">
          {r.roles.map((x, i) => (
            <span key={i} className="rounded bg-primary-50 px-1.5 py-0.5 text-xs text-primary-700">
              {roleName(x.roleId)}
              <span className="text-primary-700/70"> @ {x.branchIds.length ? x.branchIds.map(branchCode).join(', ') : 'tất cả'}</span>
            </span>
          ))}
        </span>
      ),
    },
    { key: 'perms', title: 'Số quyền', width: 90, align: 'center', sorter: (a, b) => a.perms - b.perms, render: (_, r) => <span className="tabular">{r.perms}</span> },
    {
      key: 'overrides',
      title: 'Quyền lẻ',
      width: 130,
      render: (_, r) => {
        const act = r.overrides.filter((o) => isActiveOverride(o.until));
        const g = act.filter((o) => o.effect === 'grant').length;
        const d = act.filter((o) => o.effect === 'deny').length;
        if (!act.length) return <span className="text-ink-muted">—</span>;
        return (
          <span className="flex flex-wrap gap-1 text-xs">
            {g > 0 && <span className="rounded bg-success-50 px-1.5 py-0.5 text-success-700">+{g} cấp</span>}
            {d > 0 && <span className="rounded bg-danger-50 px-1.5 py-0.5 text-danger-700">−{d} chặn</span>}
            {soon(r) && (
              <Tooltip title="Có quyền lẻ hết hạn trong 7 ngày">
                <CalendarClock size={14} className="text-warning-600" aria-label="Sắp hết hạn" />
              </Tooltip>
            )}
          </span>
        );
      },
    },
    {
      key: 'status',
      title: 'Tài khoản',
      width: 140,
      mobile: 'aside',
      render: (_, r) => (
        <StatusTag tone={ACCOUNT_STATUS[r.status].tone} icon={ACCOUNT_STATUS[r.status].icon} size="sm">
          {ACCOUNT_STATUS[r.status].label}
        </StatusTag>
      ),
    },
    {
      key: 'login',
      title: 'Đăng nhập gần nhất',
      width: 150,
      sorter: (a, b) => dayjs(a.lastLoginAt ?? 0).valueOf() - dayjs(b.lastLoginAt ?? 0).valueOf(),
      render: (_, r) => (r.lastLoginAt ? <span title={fmtDateTime(r.lastLoginAt)}>{fromNow(r.lastLoginAt)}</span> : <span className="text-ink-muted">Chưa đăng nhập</span>),
    },
  ];

  return (
    <>
      <PageHeader
        title="Phân quyền người dùng"
        subtitle="Gán vai trò theo chi nhánh cho từng người; cấp / chặn quyền lẻ có thời hạn. Chỉ cấp được quyền mình đang có."
        breadcrumb={[{ title: 'Hệ thống' }, { title: 'Phân quyền người dùng' }]}
        actions={
          editable ? (
            <Button icon={UserPlus} collapseOnMobile onClick={() => setInviting(true)}>
              Cấp tài khoản
            </Button>
          ) : undefined
        }
      />

      <div className="mb-4 grid grid-cols-2 gap-3 lg:grid-cols-4">
        <StatCard label="Đang hoạt động" value={base.filter((r) => r.status === 'active').length} icon={UserCheck} tone="success" hint={`${base.filter((r) => r.status === 'invited').length} đã mời, chưa kích hoạt`} />
        <StatCard label="Đã khóa" value={base.filter((r) => r.status === 'locked').length} icon={Ban} tone="neutral" onClick={() => setStatus('locked')} />
        <StatCard label="Có quyền lẻ" value={base.filter((r) => r.overrides.some((o) => isActiveOverride(o.until))).length} icon={KeyRound} tone="warning" hint={`${base.filter(soon).length} sắp hết hạn`} onClick={() => setSpecial('override')} />
        <StatCard label="Không đăng nhập > 30 ngày" value={base.filter(idle).length} icon={UserX} tone="danger" hint={`${noAccount.length} nhân viên chưa có tài khoản`} onClick={() => setSpecial('idle')} />
      </div>

      <DataTable<Row>
        storageKey="sys-user-perms"
        columns={columns}
        data={rows}
        rowKey="userId"
        unitLabel="người dùng"
        searchKeys={['name', 'email']}
        onRowClick={(r) => setOpenId(r.userId)}
        search={{
          placeholder: 'Tìm theo tên, email...',
          activeFilterCount: [role, status, special].filter(Boolean).length,
          filters: (
            <>
              <Select placeholder="Vai trò" value={role} onChange={setRole} options={roles.map((r) => ({ value: r.id, label: r.name }))} className="lg:!w-44" />
              <Select placeholder="Tài khoản" value={status} onChange={setStatus} options={Object.entries(ACCOUNT_STATUS).map(([value, v]) => ({ value, label: v.label }))} showSearch={false} className="lg:!w-36" />
              <Select
                placeholder="Lọc nhanh"
                value={special}
                onChange={setSpecial}
                showSearch={false}
                className="lg:!w-52"
                options={[
                  { value: 'override', label: 'Đang có quyền lẻ' },
                  { value: 'multi', label: 'Kiêm nhiều vai trò' },
                  { value: 'idle', label: 'Không đăng nhập > 30 ngày' },
                ]}
              />
            </>
          ),
          onReset: () => {
            setRole(undefined);
            setStatus(undefined);
            setSpecial(undefined);
          },
        }}
        empty={{ icon: Users, title: 'Không có người dùng phù hợp' }}
      />

      {openId && <UserAccessDrawer key={openId} userId={openId} editable={editable} onClose={() => setOpenId(null)} />}

      {/* ===== Cấp tài khoản cho nhân viên ===== */}
      <ModalForm<{ userId: string; roleId: string; branchIds: string[] }>
        open={inviting}
        onClose={() => setInviting(false)}
        title="Cấp tài khoản"
        description="Nhân viên nhận email mời đặt mật khẩu. Tài khoản ở trạng thái “Đã mời” cho đến khi đăng nhập lần đầu."
        submitAction="send"
        submitText="Gửi lời mời"
        successMessage="Đã gửi lời mời"
        initialValues={{ branchIds: branchId === ALL_BRANCHES ? [] : [branchId] }}
        onSubmit={(v) => {
          const emp = employees.find((e) => e.id === v.userId)!;
          addAccess({ userId: emp.id, email: emp.email, status: 'invited', roles: [{ roleId: v.roleId, branchIds: v.branchIds ?? [] }], overrides: [] }, actor);
        }}
      >
        <Form.Item name="userId" label="Nhân viên" rules={[{ required: true, message: 'Chọn nhân viên' }]} extra={!noAccount.length ? 'Mọi nhân viên đều đã có tài khoản' : undefined}>
          <Select placeholder="Nhân viên chưa có tài khoản" options={noAccount.map((e) => ({ value: e.id, label: `${e.fullName} · ${e.position} · ${branchCode(e.branchId)}` }))} />
        </Form.Item>
        <Form.Item name="roleId" label="Vai trò" rules={[{ required: true, message: 'Chọn vai trò' }]}>
          <Select
            options={roles.map((r) => ({
              value: r.id,
              label: r.name,
              disabled: !Object.entries(roleGrants(r)).every(([p, sc]) => canGrant(granter, p, sc)),
            }))}
          />
        </Form.Item>
        <Form.Item name="branchIds" label="Áp dụng tại chi nhánh" tooltip="Bỏ trống = mọi chi nhánh">
          <Select mode="multiple" placeholder="Mọi chi nhánh" options={getBranches(false).map((b) => ({ value: b.id, label: b.name }))} />
        </Form.Item>
      </ModalForm>
    </>
  );
}
