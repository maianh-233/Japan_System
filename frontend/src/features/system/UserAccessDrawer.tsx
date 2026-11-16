import { useMemo, useState } from 'react';
import { Drawer, Form, Switch, Tabs, Tooltip } from 'antd';
import dayjs from 'dayjs';
import { Ban, KeyRound, Lock, LockOpen, Mail, Plus, ShieldCheck, Trash2 } from 'lucide-react';
import { Button, DateInput, ModalForm, Select, StatusTag, TextInput, UserCell, useConfirm, useNotify } from '@/components/common';
import { useHrStore } from '@/features/hr/hrStore';
import { useBreakpoint } from '@/hooks/useBreakpoint';
import { cn } from '@/lib/cn';
import { fmtDate, fmtDateTime, fromNow } from '@/lib/date';
import { getBranches } from '@/features/system/useBranches';
import { canGrant, effectivePerms, isActiveOverride, roleGrants, usableScope } from './access';
import { ACCOUNT_STATUS, PERM_ACTIONS, PERM_CATALOG, PERM_OPTIONS, PERM_SCOPES, permLabel, SCOPE_KEYS } from './constants';
import { useSystemStore } from './systemStore';
import type { PermAction, PermOverride, PermScope, RoleAssignment } from './types';
import { useActor } from './useActor';

const branchNames = (ids: string[]) => (ids.length ? ids.map((id) => getBranches(false).find((b) => b.id === id)?.name ?? id).join(', ') : 'Mọi chi nhánh');
const SCOPE_TONE: Record<PermScope, string> = { own: 'bg-warning-100 text-warning-700', branch: 'bg-primary-100 text-primary-700', all: 'bg-success-100 text-success-700' };

interface Props {
  userId: string | null;
  editable: boolean;
  onClose: () => void;
}

type OverrideForm = { effect: PermOverride['effect']; perm: string; scope?: PermScope; until?: dayjs.Dayjs; reason: string };

/** Chi tiết quyền 1 người: vai trò theo chi nhánh · quyền lẻ có thời hạn · quyền hiệu lực · khóa / mở tài khoản */
export function UserAccessDrawer({ userId, editable, onClose }: Props) {
  const { isMobile } = useBreakpoint();
  const notify = useNotify();
  const { confirm, confirmDialog } = useConfirm();
  const { actor, granter, employeeId } = useActor();
  const { roles, updateAccess, addOverride, removeOverride } = useSystemStore();
  const access = useSystemStore((s) => s.access.find((a) => a.userId === userId));
  const emp = useHrStore((s) => s.employees.find((e) => e.id === userId));

  const [tab, setTab] = useState('roles');
  const [draft, setDraft] = useState<RoleAssignment[] | null>(null);
  const [adding, setAdding] = useState(false);

  const eff = useMemo(() => (access ? effectivePerms(access, roles) : new Map()), [access, roles]);
  const isSelf = userId === employeeId;
  const canEdit = editable && !isSelf;
  const assignments = draft ?? access?.roles ?? [];

  /** Được gán role này không: mọi quyền trong role phải nằm trong quyền của người gán */
  const assignable = (roleId: string) => {
    const r = roles.find((x) => x.id === roleId);
    return Boolean(r && Object.entries(roleGrants(r)).every(([p, sc]) => canGrant(granter, p, sc)));
  };

  if (!access || !emp) return null;
  const S = ACCOUNT_STATUS[access.status];

  const saveRoles = () => {
    const list = assignments.filter((a) => a.roleId);
    if (!list.length) {
      notify.toast.error('Cần ít nhất 1 vai trò');
      return;
    }
    const fmt = (l: RoleAssignment[]) => l.map((a) => `${roles.find((r) => r.id === a.roleId)?.name} @ ${branchNames(a.branchIds)}`).join('; ');
    updateAccess(access.userId, { roles: list }, actor, { severity: 'warning', action: 'Đổi vai trò', changes: [{ field: 'Vai trò', before: fmt(access.roles), after: fmt(list) }] });
    setDraft(null);
    notify.toast.success('Đã lưu vai trò');
  };

  const toggleLock = async () => {
    if (access.status === 'locked') {
      updateAccess(access.userId, { status: 'active', lockedReason: undefined }, actor, { severity: 'warning', action: 'Mở khóa tài khoản' });
      notify.toast.success('Đã mở khóa tài khoản');
      return;
    }
    const res = await confirm({
      title: `Khóa tài khoản ${emp.fullName}?`,
      tone: 'danger',
      consequences: ['Đăng xuất khỏi mọi thiết bị ngay lập tức.', 'Lead / task đang giao vẫn giữ nguyên — nhớ bàn giao.'],
      requireReason: { label: 'Lý do khóa', placeholder: 'Nghỉ việc, nghi lộ mật khẩu...' },
      confirmText: 'Khóa tài khoản',
    });
    if (res) {
      updateAccess(access.userId, { status: 'locked', lockedReason: res.reason }, actor, { severity: 'critical', action: 'Khóa tài khoản', detail: res.reason });
      notify.toast.success('Đã khóa tài khoản');
    }
  };

  const activeOverrides = access.overrides.filter((o) => isActiveOverride(o.until));

  return (
    <Drawer
      open
      onClose={onClose}
      width={isMobile ? '100%' : 680}
      title={<UserCell name={emp.fullName} subtitle={`${emp.position} · ${access.email}`} size="sm" />}
      extra={
        <StatusTag tone={S.tone} icon={S.icon} size="sm">
          {S.label}
        </StatusTag>
      }
    >
      <div className="mb-3 flex flex-wrap items-center gap-x-4 gap-y-1 text-[13px] text-ink-secondary">
        <span>Đăng nhập gần nhất: {access.lastLoginAt ? fromNow(access.lastLoginAt) : 'chưa đăng nhập'}</span>
        <span className="inline-flex items-center gap-1.5">
          Bắt buộc 2 lớp (OTP)
          <Switch
            size="small"
            checked={access.twoFA}
            disabled={!canEdit}
            onChange={(v) => updateAccess(access.userId, { twoFA: v }, actor, { severity: v ? 'info' : 'warning', action: v ? 'Bật xác thực 2 lớp' : 'Tắt xác thực 2 lớp' })}
          />
        </span>
      </div>
      {access.status === 'locked' && access.lockedReason && <p className="mb-3 rounded-lg bg-danger-50 px-3 py-2 text-[13px] text-danger-700">Đã khóa: {access.lockedReason}</p>}
      {isSelf && <p className="mb-3 rounded-lg bg-warning-50 px-3 py-2 text-[13px] text-warning-700">Đây là tài khoản của bạn — không tự sửa quyền của mình được.</p>}
      {canEdit && (
        <div className="mb-3 flex flex-wrap gap-1.5">
          <Button icon={access.status === 'locked' ? LockOpen : Lock} size="sm" variant="soft" tone={access.status === 'locked' ? 'success' : 'danger'} onClick={toggleLock}>
            {access.status === 'locked' ? 'Mở khóa' : 'Khóa tài khoản'}
          </Button>
          <Button
            icon={KeyRound}
            size="sm"
            variant="ghost"
            tone="neutral"
            onClick={() => {
              useSystemStore.getState().audit(actor, { category: 'auth', severity: 'warning', action: 'Đặt lại mật khẩu', target: emp.fullName });
              notify.toast.success(`Đã gửi link đặt lại mật khẩu tới ${access.email}`);
            }}
          >
            Đặt lại mật khẩu
          </Button>
          {access.status === 'invited' && (
            <Button icon={Mail} size="sm" variant="ghost" tone="neutral" onClick={() => notify.toast.success('Đã gửi lại thư mời')}>
              Gửi lại thư mời
            </Button>
          )}
        </div>
      )}

      <Tabs
        activeKey={tab}
        onChange={setTab}
        items={[
          {
            key: 'roles',
            label: `Vai trò (${access.roles.length})`,
            children: (
              <div className="flex flex-col gap-2 text-[13px]">
                {assignments.map((a, i) => (
                  <div key={i} className="grid grid-cols-[1fr_1.3fr_36px] items-center gap-2 rounded-lg border border-line p-2">
                    <Select
                      size="small"
                      value={a.roleId || undefined}
                      disabled={!canEdit}
                      placeholder="Chọn role"
                      allowClear={false}
                      options={roles.map((r) => ({ value: r.id, label: r.name, disabled: !assignable(r.id) }))}
                      onChange={(v) => setDraft(assignments.map((x, k) => (k === i ? { ...x, roleId: v as string } : x)))}
                      aria-label="Vai trò"
                    />
                    <Select
                      size="small"
                      mode="multiple"
                      value={a.branchIds}
                      disabled={!canEdit}
                      placeholder="Mọi chi nhánh"
                      options={getBranches(false).map((b) => ({ value: b.id, label: b.name }))}
                      onChange={(v) => setDraft(assignments.map((x, k) => (k === i ? { ...x, branchIds: v as string[] } : x)))}
                      aria-label="Chi nhánh áp dụng"
                    />
                    {canEdit && <Button icon={Trash2} iconOnly size="sm" variant="ghost" tone="danger" aria-label="Bỏ vai trò" onClick={() => setDraft(assignments.filter((_, k) => k !== i))} />}
                  </div>
                ))}
                <p className="text-xs text-ink-muted">Bỏ trống chi nhánh = áp dụng mọi chi nhánh. Role gán theo chi nhánh thì quyền “toàn hệ thống” thu lại thành “chi nhánh”. Role có quyền vượt quá quyền của bạn bị khóa chọn.</p>
                {canEdit && (
                  <div className="flex gap-2">
                    <Button icon={Plus} size="sm" variant="soft" onClick={() => setDraft([...assignments, { roleId: '', branchIds: [emp.branchId] }])}>
                      Thêm vai trò
                    </Button>
                    {draft && (
                      <>
                        <Button action="save" size="sm" onClick={saveRoles} />
                        <Button size="sm" variant="ghost" tone="neutral" icon={Ban} onClick={() => setDraft(null)}>
                          Hủy
                        </Button>
                      </>
                    )}
                  </div>
                )}
              </div>
            ),
          },
          {
            key: 'overrides',
            label: `Quyền lẻ (${activeOverrides.length})`,
            children: (
              <div className="flex flex-col gap-2 text-[13px]">
                {access.overrides.map((o) => {
                  const active = isActiveOverride(o.until);
                  return (
                    <div key={o.id} className={cn('rounded-lg border p-2.5', !active ? 'border-dashed border-line opacity-60' : o.effect === 'deny' ? 'border-danger-100 bg-danger-50/50' : 'border-success-100 bg-success-50/50')}>
                      <div className="flex items-start justify-between gap-2">
                        <div className="min-w-0">
                          <p className="font-medium text-ink">
                            {o.effect === 'grant' ? '+ Cấp' : '− Chặn'} · {permLabel(o.perm)}
                            {o.scope && o.effect === 'grant' && <span className={cn('ml-1.5 rounded px-1.5 py-0.5 text-[11px]', SCOPE_TONE[o.scope])}>{PERM_SCOPES[o.scope].label}</span>}
                          </p>
                          <p className="text-xs text-ink-secondary">{o.reason}</p>
                          <p className="text-[11px] text-ink-muted">
                            {o.by} · {fmtDate(o.at)} · {o.until ? (active ? `hết hạn ${fromNow(o.until)} (${fmtDate(o.until)})` : `đã hết hạn ${fmtDate(o.until)}`) : 'vô thời hạn'}
                          </p>
                        </div>
                        {canEdit && <Button icon={Trash2} iconOnly size="sm" variant="ghost" tone="danger" aria-label="Thu hồi" onClick={() => removeOverride(access.userId, o.id, actor)} />}
                      </div>
                    </div>
                  );
                })}
                {!access.overrides.length && <p className="text-ink-muted">Không có quyền lẻ — chỉ dùng quyền theo vai trò.</p>}
                {canEdit && (
                  <Button icon={Plus} size="sm" variant="soft" className="self-start" onClick={() => setAdding(true)}>
                    Cấp / chặn quyền lẻ
                  </Button>
                )}
              </div>
            ),
          },
          {
            key: 'effective',
            label: 'Quyền hiệu lực',
            children: (
              <div className="flex flex-col gap-3 text-[13px]">
                {PERM_CATALOG.map((m) => {
                  const rows = m.groups.filter((g) => g.actions.some((a) => eff.get(`${g.key}.${a}`)));
                  if (!rows.length) return null;
                  return (
                    <div key={m.key}>
                      <p className="mb-1 text-xs font-semibold uppercase tracking-wide text-ink-muted">{m.label}</p>
                      <ul className="flex flex-col gap-1">
                        {rows.map((g) => (
                          <li key={g.key} className="flex flex-wrap items-center gap-1.5">
                            <span className="w-40 shrink-0 text-ink">{g.label}</span>
                            {g.actions.map((a: PermAction) => {
                              const e = eff.get(`${g.key}.${a}`);
                              if (!e) return null;
                              const sc = usableScope(e);
                              return (
                                <Tooltip key={a} title={e.denied ? `Bị chặn: ${e.denied.reason}` : `Từ: ${[...new Set(e.sources)].join(', ')}`}>
                                  <span className={cn('rounded px-1.5 py-0.5 text-[11px]', e.denied ? 'bg-danger-50 text-danger-700 line-through' : sc ? SCOPE_TONE[sc] : '', e.sources.includes('Cấp lẻ') && 'ring-1 ring-success-600')}>
                                    {PERM_ACTIONS[a]}
                                    {sc && !e.denied ? ` · ${PERM_SCOPES[sc].short}` : ''}
                                  </span>
                                </Tooltip>
                              );
                            })}
                          </li>
                        ))}
                      </ul>
                    </div>
                  );
                })}
                <p className="text-xs text-ink-muted">Viền xanh = có từ quyền lẻ · gạch ngang = bị chặn. Cập nhật lúc {fmtDateTime(new Date().toISOString())}.</p>
              </div>
            ),
          },
        ]}
      />

      <ModalForm<OverrideForm>
        open={adding}
        onClose={() => setAdding(false)}
        title={`Quyền lẻ cho ${emp.fullName}`}
        description="Dùng cho nhu cầu tạm thời (thay người nghỉ phép, hỗ trợ đợt cao điểm). Nên đặt ngày hết hạn."
        submitAction="save"
        successMessage="Đã lưu quyền lẻ"
        initialValues={{ effect: 'grant', scope: 'branch', until: dayjs().add(7, 'day') }}
        onSubmit={(v) => {
          if (v.effect === 'grant' && !canGrant(granter, v.perm, v.scope ?? 'own')) throw new Error('Bạn chỉ cấp được quyền mình đang có, với phạm vi không rộng hơn của mình');
          addOverride(access.userId, { perm: v.perm, effect: v.effect, scope: v.effect === 'grant' ? v.scope : undefined, until: v.until?.endOf('day').toISOString(), reason: v.reason }, actor);
          setTab('overrides');
        }}
      >
        <Form.Item name="effect" label="Loại">
          <Select
            options={[
              { value: 'grant', label: '+ Cấp thêm quyền' },
              { value: 'deny', label: '− Chặn quyền (kể cả khi role có)' },
            ]}
            allowClear={false}
            showSearch={false}
          />
        </Form.Item>
        <Form.Item noStyle shouldUpdate={(a, b) => a.effect !== b.effect || a.scope !== b.scope}>
          {({ getFieldValue }) => {
            const grant = getFieldValue('effect') === 'grant';
            const sc: PermScope = getFieldValue('scope') ?? 'own';
            return (
              <>
                <Form.Item name="perm" label="Quyền" rules={[{ required: true, message: 'Chọn quyền' }]}>
                  <Select placeholder="Gõ để tìm: công nợ, xuất file..." options={grant ? PERM_OPTIONS.filter((o) => canGrant(granter, o.value, 'own')) : PERM_OPTIONS} />
                </Form.Item>
                {grant && (
                  <Form.Item name="scope" label="Phạm vi" extra={PERM_SCOPES[sc]?.hint}>
                    <Select options={SCOPE_KEYS.map((s) => ({ value: s, label: PERM_SCOPES[s].label }))} allowClear={false} showSearch={false} />
                  </Form.Item>
                )}
              </>
            );
          }}
        </Form.Item>
        <Form.Item name="until" label="Hết hạn" tooltip="Bỏ trống = vô thời hạn">
          <DateInput disabledDate={(d) => d.isBefore(dayjs(), 'day')} />
        </Form.Item>
        <Form.Item name="reason" label="Lý do" rules={[{ required: true, message: 'Nhập lý do (ghi vào nhật ký)' }]}>
          <TextInput placeholder="Vd: Thay kế toán nghỉ phép 20–27/11" />
        </Form.Item>
        <p className="flex items-center gap-1 text-xs text-ink-muted">
          <ShieldCheck size={14} aria-hidden /> Chỉ hiện các quyền bạn đang có. Mọi thao tác được ghi vào Nhật ký hệ thống.
        </p>
      </ModalForm>
      {confirmDialog}
    </Drawer>
  );
}
