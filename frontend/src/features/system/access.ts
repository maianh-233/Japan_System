import dayjs from 'dayjs';
import { ALL_PERMS, GLOBAL_ONLY_MODULES, PERM_SCOPES, widerScope } from './constants';
import type { PermScope, Role, UserAccess } from './types';

/** Logic QUYỀN HIỆU LỰC (thuần) — backend tính đúng như vậy khi kiểm tra quyền. */

export interface EffectivePerm {
  scope?: PermScope;
  /** Quyền đến từ đâu: tên role / "Cấp lẻ" */
  sources: string[];
  /** Bị chặn bởi override */
  denied?: { reason: string; until?: string };
}

/** Quyền của role (Super Admin = mọi quyền, toàn hệ thống) */
export function roleGrants(role: Role): Record<string, PermScope> {
  if (role.superAdmin) return Object.fromEntries(ALL_PERMS.map((p) => [p, 'all' as PermScope]));
  return role.grants;
}

export const isActiveOverride = (until?: string) => !until || dayjs(until).isAfter(dayjs());

export function effectivePerms(access: UserAccess, roles: Role[]): Map<string, EffectivePerm> {
  const out = new Map<string, EffectivePerm>();
  const get = (p: string) => {
    if (!out.has(p)) out.set(p, { sources: [] });
    return out.get(p)!;
  };

  access.roles.forEach((as) => {
    const role = roles.find((r) => r.id === as.roleId);
    if (!role) return;
    Object.entries(roleGrants(role)).forEach(([p, sc]) => {
      // Role chỉ gán ở vài chi nhánh → phạm vi "toàn hệ thống" thu về "chi nhánh"
      const scope: PermScope = as.branchIds.length && sc === 'all' && !GLOBAL_ONLY_MODULES.has(p.split('.')[0]) ? 'branch' : sc;
      const e = get(p);
      e.scope = widerScope(e.scope, scope);
      e.sources.push(role.name);
    });
  });

  access.overrides.filter((o) => isActiveOverride(o.until)).forEach((o) => {
    const e = get(o.perm);
    if (o.effect === 'grant') {
      e.scope = widerScope(e.scope, o.scope ?? 'own');
      e.sources.push('Cấp lẻ');
    } else e.denied = { reason: o.reason, until: o.until };
  });

  return out;
}

/** Mã menu dạng "module.group" chỉ khi quyền Xem còn hiệu lực. */
export function menuPermissions(access: UserAccess, roles: Role[]): string[] {
  return [...effectivePerms(access, roles)]
    .filter(([permission, grant]) => permission.endsWith('.view') && Boolean(usableScope(grant)))
    .map(([permission]) => permission.slice(0, -'.view'.length));
}

/** Phạm vi thực dùng được (đã trừ bị chặn) */
export const usableScope = (e?: EffectivePerm) => (e && !e.denied ? e.scope : undefined);

/** Người cấp chỉ cấp được quyền mình có, với phạm vi ≤ của mình */
export function canGrant(granter: Map<string, EffectivePerm> | 'super', perm: string, scope: PermScope): boolean {
  if (granter === 'super') return true;
  const mine = usableScope(granter.get(perm));
  return Boolean(mine && PERM_SCOPES[mine].rank >= PERM_SCOPES[scope].rank);
}

/** Đếm số quyền đang dùng được */
export const countUsable = (m: Map<string, EffectivePerm>) => [...m.values()].filter((e) => usableScope(e)).length;
