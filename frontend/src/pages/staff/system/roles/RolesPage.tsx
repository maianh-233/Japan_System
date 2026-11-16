import { useMemo, useState } from 'react';
import { Form, Tooltip } from 'antd';
import { ChevronDown, Copy, Crown, Lock, RotateCcw, Save, ShieldCheck, Users } from 'lucide-react';
import { Button, ModalForm, PageHeader, Select, StatusTag, TextArea, TextInput, useConfirm, useNotify } from '@/components/common';
import { canGrant, roleGrants } from '@/features/system/access';
import { ACTION_KEYS, GLOBAL_ONLY_MODULES, PERM_ACTIONS, PERM_CATALOG, PERM_SCOPES, permLabel, SCOPE_KEYS } from '@/features/system/constants';
import { useSystemStore } from '@/features/system/systemStore';
import type { PermAction, PermScope } from '@/features/system/types';
import { useActor } from '@/features/system/useActor';
import { useAuth } from '@/hooks/useAuth';
import { cn } from '@/lib/cn';
import { fromNow } from '@/lib/date';

const SCOPE_STYLE: Record<PermScope, string> = {
  own: 'bg-warning-100 text-warning-700',
  branch: 'bg-primary-100 text-primary-700',
  all: 'bg-success-100 text-success-700',
};

/** Thứ tự xoay khi bấm ô: trống → mình → chi nhánh → toàn hệ thống → trống */
function nextScope(cur: PermScope | undefined, globalOnly: boolean): PermScope | undefined {
  if (globalOnly) return cur ? undefined : 'all';
  const order: (PermScope | undefined)[] = [undefined, 'own', 'branch', 'all'];
  return order[(order.indexOf(cur) + 1) % order.length];
}

/** Vai trò & ma trận quyền: module → nhóm → hành động, mỗi ô chọn phạm vi. DỮ LIỆU GIẢ. */
export default function RolesPage() {
  const { can } = useAuth();
  const notify = useNotify();
  const { confirm, confirmDialog } = useConfirm();
  const { actor, granter } = useActor();
  const { roles, access, saveRole, removeRole } = useSystemStore();
  const editable = can('system.roles');

  const [selectedId, setSelectedId] = useState(roles[1]?.id ?? roles[0].id);
  const [draft, setDraft] = useState<Record<string, PermScope> | null>(null);
  const [collapsed, setCollapsed] = useState<Set<string>>(new Set());
  const [creating, setCreating] = useState(false);

  const role = roles.find((r) => r.id === selectedId) ?? roles[0];
  const grants = draft ?? roleGrants(role);
  const locked = !editable || role.superAdmin;
  const userCount = (id: string) => access.filter((a) => a.status !== 'locked' && a.roles.some((r) => r.roleId === id)).length;

  const diff = useMemo(() => {
    if (!draft) return [];
    const before = role.grants;
    const keys = new Set([...Object.keys(before), ...Object.keys(draft)]);
    return [...keys].filter((k) => before[k] !== draft[k]).map((k) => ({ field: k, before: before[k] ? PERM_SCOPES[before[k]].label : '—', after: draft[k] ? PERM_SCOPES[draft[k]].label : '—' }));
  }, [draft, role]);

  const select = async (id: string) => {
    if (draft && diff.length) {
      const ok = await confirm({ title: 'Bỏ các thay đổi chưa lưu?', description: `${diff.length} quyền của "${role.name}" đã sửa nhưng chưa lưu.`, tone: 'warning' });
      if (!ok) return;
    }
    setDraft(null);
    setSelectedId(id);
  };

  /** Gán phạm vi cho danh sách quyền (bỏ qua quyền vượt quá quyền của người sửa) */
  const setMany = (perms: string[], scope: PermScope | undefined) => {
    const next = { ...grants };
    let blocked = 0;
    perms.forEach((p) => {
      const sc = scope && GLOBAL_ONLY_MODULES.has(p.split('.')[0]) ? 'all' : scope;
      if (sc && !canGrant(granter, p, sc)) {
        blocked++;
        return;
      }
      if (sc) next[p] = sc;
      else delete next[p];
    });
    setDraft(next);
    if (blocked) notify.toast.warning(`${blocked} quyền vượt quá quyền của bạn — giữ nguyên`);
  };

  const save = () => {
    saveRole({ ...role, grants: draft! }, actor, diff.slice(0, 30));
    setDraft(null);
    notify.success({ title: `Đã lưu role ${role.name}`, description: `${diff.length} quyền thay đổi · áp dụng cho ${userCount(role.id)} người ngay lần thao tác tới.` });
  };

  const remove = async () => {
    const n = userCount(role.id);
    if (n) {
      notify.error({ title: 'Không xóa được role đang dùng', description: `${n} người đang được gán "${role.name}". Đổi role cho họ trước.` });
      return;
    }
    const ok = await confirm({ title: `Xóa role "${role.name}"?`, tone: 'danger', confirmAction: 'delete' });
    if (ok) {
      removeRole(role.id, actor);
      setSelectedId(roles[0].id);
      setDraft(null);
    }
  };

  const total = Object.keys(grants).length;

  return (
    <>
      <PageHeader
        title="Vai trò & quyền"
        subtitle="Quyền = Module › Nhóm › Hành động, mỗi quyền có phạm vi: của mình / chi nhánh / toàn hệ thống. Bấm vào ô để đổi phạm vi."
        breadcrumb={[{ title: 'Hệ thống' }, { title: 'Vai trò & quyền' }]}
        actions={
          editable ? (
            <Button action="create" collapseOnMobile onClick={() => setCreating(true)}>
              Tạo role
            </Button>
          ) : undefined
        }
      />

      <div className="grid gap-4 lg:grid-cols-[260px_1fr]">
        {/* ===== Danh sách role ===== */}
        <nav aria-label="Danh sách role" className="flex gap-2 overflow-x-auto pb-1 lg:flex-col lg:overflow-visible">
          {roles.map((r) => {
            const active = r.id === role.id;
            return (
              <button
                key={r.id}
                type="button"
                onClick={() => select(r.id)}
                className={cn(
                  'min-w-[200px] rounded-xl border p-3 text-left transition-colors lg:min-w-0',
                  active ? 'border-primary-600 bg-primary-50' : 'border-line bg-surface hover:border-primary-300',
                )}
              >
                <span className="flex items-center gap-1.5 font-semibold text-ink">
                  {r.superAdmin ? <Crown size={14} className="text-warning-600" aria-hidden /> : r.system ? <ShieldCheck size={14} className="text-ink-muted" aria-hidden /> : null}
                  {r.name}
                </span>
                <span className="mt-0.5 flex gap-3 text-xs text-ink-muted">
                  <span className="inline-flex items-center gap-1">
                    <Users size={12} aria-hidden /> {userCount(r.id)}
                  </span>
                  <span>{r.superAdmin ? 'Toàn quyền' : `${Object.keys(r.grants).length} quyền`}</span>
                  {!r.system && <span className="text-primary-700">Tự tạo</span>}
                </span>
              </button>
            );
          })}
        </nav>

        {/* ===== Ma trận quyền ===== */}
        <section className="min-w-0 rounded-xl border border-line bg-surface">
          <header className="flex flex-wrap items-start justify-between gap-3 border-b border-line p-4">
            <div className="min-w-0">
              <h2 className="flex flex-wrap items-center gap-2 text-base font-semibold text-ink">
                {role.name}
                {role.system && (
                  <StatusTag tone="neutral" icon={Lock} size="sm">
                    Role mặc định
                  </StatusTag>
                )}
              </h2>
              <p className="text-[13px] text-ink-secondary">{role.description}</p>
              <p className="mt-1 text-xs text-ink-muted">
                {role.superAdmin ? 'Toàn quyền — không chỉnh sửa' : `${total} quyền`} · {userCount(role.id)} người · sửa {fromNow(role.updatedAt)} bởi {role.updatedBy}
              </p>
            </div>
            {!locked && (
              <div className="flex flex-wrap gap-1.5">
                {draft && diff.length > 0 && (
                  <>
                    <Button icon={RotateCcw} size="sm" variant="ghost" tone="neutral" onClick={() => setDraft(null)}>
                      Hoàn tác
                    </Button>
                    <Button icon={Save} size="sm" onClick={save}>
                      Lưu {diff.length} thay đổi
                    </Button>
                  </>
                )}
                {!role.system && <Button action="delete" size="sm" onClick={remove} />}
              </div>
            )}
          </header>

          {/* Chú thích */}
          <div className="flex flex-wrap items-center gap-2 border-b border-line px-4 py-2 text-xs text-ink-secondary">
            {SCOPE_KEYS.map((s) => (
              <Tooltip key={s} title={PERM_SCOPES[s].hint}>
                <span className={cn('rounded px-1.5 py-0.5 font-medium', SCOPE_STYLE[s])}>{PERM_SCOPES[s].label}</span>
              </Tooltip>
            ))}
            <span className="text-ink-muted">· ô trống = không có quyền · Hệ thống chỉ có phạm vi toàn hệ thống</span>
          </div>

          {role.id === 'R-TEACHER' && <p className="m-4 rounded-lg bg-warning-50 px-3 py-2 text-[13px] text-warning-700">Giáo viên dùng Cổng giáo viên (quyền cố định theo lớp được phân công). Chỉ cấp quyền ở đây nếu giáo viên kiêm nhiệm việc văn phòng.</p>}

          <div className="overflow-x-auto">
            <table className="w-full min-w-[720px] text-[13px]">
              <thead>
                <tr className="text-xs text-ink-muted">
                  <th className="sticky left-0 bg-surface px-4 py-2 text-left font-medium">Nhóm quyền</th>
                  {ACTION_KEYS.map((a) => (
                    <th key={a} className="w-[84px] px-1 py-2 text-center font-medium">
                      {PERM_ACTIONS[a]}
                    </th>
                  ))}
                </tr>
              </thead>
              {PERM_CATALOG.map((m) => {
                const open = !collapsed.has(m.key);
                const modPerms = m.groups.flatMap((g) => g.actions.map((a) => `${g.key}.${a}`));
                const on = modPerms.filter((p) => grants[p]).length;
                const globalOnly = GLOBAL_ONLY_MODULES.has(m.key);
                return (
                  <tbody key={m.key} className="border-t border-line">
                    <tr className="bg-canvas">
                      <td className="sticky left-0 bg-canvas px-4 py-2" colSpan={1}>
                        <button
                          type="button"
                          className="inline-flex items-center gap-1.5 font-semibold text-ink"
                          aria-expanded={open}
                          onClick={() => setCollapsed((s) => {
                            const n = new Set(s);
                            if (n.has(m.key)) n.delete(m.key);
                            else n.add(m.key);
                            return n;
                          })}
                        >
                          <ChevronDown size={14} className={cn('transition-transform', !open && '-rotate-90')} aria-hidden />
                          {m.label}
                          <span className="tabular text-xs font-normal text-ink-muted">
                            {on}/{modPerms.length}
                          </span>
                        </button>
                      </td>
                      <td colSpan={ACTION_KEYS.length} className="px-2 py-1.5 text-right">
                        {!locked && (
                          <Select
                            size="small"
                            className="!w-44"
                            placeholder="Đặt cả module..."
                            value={null as unknown as string}
                            showSearch={false}
                            options={[...(globalOnly ? (['all'] as PermScope[]) : SCOPE_KEYS).map((s) => ({ value: s, label: `Tất cả: ${PERM_SCOPES[s].label}` })), { value: 'none', label: 'Bỏ hết quyền' }]}
                            onChange={(v) => v && setMany(modPerms, v === 'none' ? undefined : (v as PermScope))}
                            aria-label={`Đặt quyền cả module ${m.label}`}
                          />
                        )}
                      </td>
                    </tr>
                    {open &&
                      m.groups.map((g) => (
                        <tr key={g.key} className="border-t border-line/60 hover:bg-canvas/60">
                          <td className="sticky left-0 bg-surface px-4 py-1.5">
                            <span className="text-ink">{g.label}</span>
                            {!locked && (
                              <button
                                type="button"
                                className="ml-2 text-[11px] text-primary-700 hover:underline"
                                onClick={() => {
                                  const ps = g.actions.map((a) => `${g.key}.${a}`);
                                  const anyOn = ps.some((p) => grants[p]);
                                  setMany(ps, anyOn ? undefined : globalOnly ? 'all' : 'branch');
                                }}
                              >
                                {g.actions.some((a) => grants[`${g.key}.${a}`]) ? 'bỏ hết' : 'chọn hết'}
                              </button>
                            )}
                          </td>
                          {ACTION_KEYS.map((a: PermAction) => {
                            if (!g.actions.includes(a))
                              return (
                                <td key={a} className="text-center text-ink-muted/40">
                                  ·
                                </td>
                              );
                            const p = `${g.key}.${a}`;
                            const sc = grants[p];
                            const changed = draft && role.grants[p] !== draft[p];
                            return (
                              <td key={a} className="px-1 py-1 text-center">
                                <button
                                  type="button"
                                  disabled={locked}
                                  title={`${permLabel(p)}: ${sc ? PERM_SCOPES[sc].label : 'không có'}`}
                                  onClick={() => setMany([p], nextScope(sc, globalOnly))}
                                  className={cn(
                                    'h-7 w-[72px] rounded-md border text-xs font-medium transition-colors disabled:cursor-default',
                                    sc ? cn(SCOPE_STYLE[sc], 'border-transparent') : 'border-dashed border-line text-ink-muted hover:border-primary-300',
                                    changed && 'ring-2 ring-primary-400 ring-offset-1',
                                  )}
                                >
                                  {sc ? PERM_SCOPES[sc].short : '—'}
                                </button>
                              </td>
                            );
                          })}
                        </tr>
                      ))}
                  </tbody>
                );
              })}
            </table>
          </div>
        </section>
      </div>

      {/* ===== Tạo role ===== */}
      <ModalForm<{ name: string; description: string; copyFrom?: string }>
        open={creating}
        onClose={() => setCreating(false)}
        title="Tạo role"
        description="Nên sao chép từ role gần giống rồi chỉnh — nhanh và ít sót quyền."
        submitAction="create"
        successMessage="Đã tạo role"
        onSubmit={(v) => {
          if (roles.some((r) => r.name.toLowerCase() === v.name.trim().toLowerCase())) throw new Error('Tên role đã tồn tại');
          const src = roles.find((r) => r.id === v.copyFrom);
          // Chỉ chép những quyền người tạo được phép cấp
          const grants = Object.fromEntries(Object.entries(src ? roleGrants(src) : {}).filter(([p, sc]) => canGrant(granter, p, sc)));
          const id = `R-${Date.now()}`;
          saveRole({ id, name: v.name.trim(), description: v.description ?? '', system: false, grants }, actor);
          setSelectedId(id);
          setDraft(null);
        }}
      >
        <Form.Item name="name" label="Tên role" rules={[{ required: true, message: 'Nhập tên role' }]}>
          <TextInput placeholder="Vd: Trợ lý giáo vụ" />
        </Form.Item>
        <Form.Item name="description" label="Mô tả">
          <TextArea rows={2} maxLength={200} placeholder="Role này dùng cho ai, làm gì" />
        </Form.Item>
        <Form.Item name="copyFrom" label="Sao chép quyền từ">
          <Select placeholder="Bắt đầu từ trống" options={roles.filter((r) => !r.superAdmin).map((r) => ({ value: r.id, label: r.name }))} suffixIcon={<Copy size={14} />} />
        </Form.Item>
      </ModalForm>
      {confirmDialog}
    </>
  );
}
