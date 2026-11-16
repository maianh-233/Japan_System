import { useState } from 'react';
import { Segmented } from 'antd';
import { ChevronRight, ListChecks } from 'lucide-react';
import { useNavigate } from 'react-router-dom';
import { Button } from '@/components/common';
import { useAuth } from '@/hooks/useAuth';
import { cn } from '@/lib/cn';
import { useTodayWork, type RoleBoard, type WorkRole, type WorkTone } from './useTodayWork';

/** Mỗi nhóm hiện tối đa N dòng; còn lại bấm "Xem tất cả" */
const PREVIEW = 5;

const DOT: Record<WorkTone, string> = { danger: 'bg-danger-600', warning: 'bg-warning-600', info: 'bg-primary-400' };
const META: Record<WorkTone, string> = { danger: 'text-danger-600', warning: 'text-warning-700', info: 'text-ink-muted' };

/** Tab mặc định theo tên vai trò (Admin thấy cả 3 → mặc định tab đầu tiên có việc) */
function defaultRole(roleName: string | undefined, boards: RoleBoard[]): WorkRole | undefined {
  const name = (roleName ?? '').toLowerCase();
  const byName: WorkRole | undefined = name.includes('kế toán') ? 'finance' : name.includes('giáo vụ') ? 'academic' : name.includes('sale') ? 'sale' : undefined;
  if (byName && boards.some((b) => b.role === byName)) return byName;
  return (boards.find((b) => b.count > 0) ?? boards[0])?.role;
}

/** Khối "Việc hôm nay" trên Dashboard — danh sách việc cụ thể theo vai trò, bấm là mở đúng bản ghi */
export function TodayWork() {
  const { user } = useAuth();
  const navigate = useNavigate();
  const boards = useTodayWork();
  const [picked, setPicked] = useState<WorkRole>();
  if (!boards.length) return null;

  const role = picked && boards.some((b) => b.role === picked) ? picked : defaultRole(user?.roleName, boards);
  const board = boards.find((b) => b.role === role) ?? boards[0];

  return (
    <section className="flex flex-col gap-3">
      <div className="flex flex-wrap items-center justify-between gap-2">
        <h2 className="flex items-center gap-2 text-lg font-semibold">
          <ListChecks size={20} className="text-primary-600" aria-hidden />
          Việc hôm nay
        </h2>
        {boards.length > 1 && (
          <Segmented<WorkRole>
            size="small"
            value={board.role}
            onChange={setPicked}
            options={boards.map((b) => ({ value: b.role, label: `${b.label}${b.count ? ` (${b.count})` : ''}` }))}
          />
        )}
      </div>

      <div className="grid grid-cols-1 gap-3 lg:grid-cols-2">
        {board.groups.map((g) => (
          <div key={g.key} className="flex flex-col rounded-[var(--radius-card)] border border-line bg-surface">
            <div className="flex items-center gap-2 border-b border-line px-4 py-2.5">
              <g.icon size={16} className="text-ink-secondary" aria-hidden />
              <span className="flex-1 text-[13px] font-semibold text-ink">{g.label}</span>
              <span className={cn('tabular rounded-full px-2 text-xs font-semibold', g.items.length ? 'bg-danger-50 text-danger-700' : 'bg-page text-ink-muted')}>{g.items.length}</span>
            </div>
            {g.items.length === 0 ? (
              <p className="flex-1 px-4 py-6 text-center text-[13px] text-ink-muted">{g.empty}</p>
            ) : (
              <ul className="flex-1 divide-y divide-line">
                {g.items.slice(0, PREVIEW).map((it) => (
                  <li key={it.key}>
                    <button type="button" onClick={() => navigate(it.path)} className="flex w-full items-center gap-3 px-4 py-2.5 text-left hover:bg-canvas">
                      <span className={cn('size-2 shrink-0 rounded-full', DOT[it.tone])} aria-hidden />
                      <span className="min-w-0 flex-1">
                        <span className="block truncate text-[13px] font-medium text-ink">{it.title}</span>
                        {it.subtitle && <span className="block truncate text-xs text-ink-muted">{it.subtitle}</span>}
                      </span>
                      {it.meta && <span className={cn('shrink-0 text-xs font-medium', META[it.tone])}>{it.meta}</span>}
                      <ChevronRight size={14} className="shrink-0 text-ink-muted" aria-hidden />
                    </button>
                  </li>
                ))}
              </ul>
            )}
            {g.items.length > PREVIEW && (
              <div className="border-t border-line px-2 py-1.5">
                <Button icon={ChevronRight} size="sm" variant="ghost" onClick={() => navigate(g.allPath)}>
                  Xem tất cả {g.items.length}
                </Button>
              </div>
            )}
          </div>
        ))}
      </div>
    </section>
  );
}
