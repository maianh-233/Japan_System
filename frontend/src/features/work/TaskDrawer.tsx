import { useState } from 'react';
import { Checkbox, Drawer } from 'antd';
import dayjs from 'dayjs';
import { ExternalLink, Link2, MessageSquare, Plus, Repeat, Send } from 'lucide-react';
import { useNavigate } from 'react-router-dom';
import { Button, DateInput, Select, StatusTag, TextInput, UserCell, useNotify } from '@/components/common';
import { useHrStore } from '@/features/hr/hrStore';
import { nameOf } from '@/features/hr/hrUtils';
import { useAuth } from '@/hooks/useAuth';
import { USE_API } from '@/lib/api/config';
import { useBreakpoint } from '@/hooks/useBreakpoint';
import { cn } from '@/lib/cn';
import { fmtDate, fmtDateTime } from '@/lib/date';
import { TASK_PRIORITY, TASK_PRIORITY_OPTIONS, TASK_REPEAT, TASK_STATUS, TASK_STATUS_ORDER } from './constants';
import type { Task, TaskStatus } from './types';
import { useWorkStore } from './workStore';

/** Khi hoàn thành task lặp lại → tạo lần kế tiếp */
export function completeTask(task: Task) {
  const { updateTask, addTask } = useWorkStore.getState();
  updateTask(task.id, { status: 'done' });
  // Chế độ API: server sinh lần kế tiếp (chỉ 1 lần / task — CV-02.E2)
  if (USE_API) return task.repeat !== 'none' && task.status !== 'done';
  if (task.repeat !== 'none') {
    const unit = task.repeat === 'daily' ? 'day' : task.repeat === 'weekly' ? 'week' : 'month';
    const base = task.due ? dayjs(task.due) : dayjs();
    addTask({ ...task, status: 'todo', due: base.add(1, unit).format('YYYY-MM-DD'), checklist: task.checklist.map((c) => ({ ...c, done: false })), completedAt: undefined });
    return true;
  }
  return false;
}

export const linkPath = (l: NonNullable<Task['link']>) =>
  l.type === 'class' ? `/admin/academic/classes/${l.id}` : l.type === 'student' ? `/admin/students/profiles?open=${l.id}` : '/admin/crm/leads';

/** Chi tiết task: trạng thái · người làm · hạn · checklist · bình luận */
export function TaskDrawer({ taskId, onClose }: { taskId: string | null; onClose: () => void }) {
  const { isMobile } = useBreakpoint();
  const { user } = useAuth();
  const notify = useNotify();
  const navigate = useNavigate();
  const task = useWorkStore((s) => s.tasks.find((t) => t.id === taskId));
  const updateTask = useWorkStore((s) => s.updateTask);
  const comment = useWorkStore((s) => s.commentTask);
  const employees = useHrStore((s) => s.employees);
  const [text, setText] = useState('');
  const [item, setItem] = useState('');

  if (!task) return <Drawer open={false} onClose={onClose} />;
  const overdue = task.status !== 'done' && task.due && task.due < dayjs().format('YYYY-MM-DD');
  const me = user?.fullName ?? 'Nhân viên';
  const staffOptions = employees.filter((e) => e.status !== 'quit').map((e) => ({ value: e.id, label: `${e.fullName} · ${e.position}` }));

  const setStatus = (s: TaskStatus) => {
    if (s === 'done') {
      const next = completeTask(task);
      notify.toast.success(next ? `Đã xong — tạo lần tiếp theo (${TASK_REPEAT[task.repeat].toLowerCase()})` : 'Đã hoàn thành task');
    } else updateTask(task.id, { status: s });
  };

  return (
    <Drawer
      open
      onClose={onClose}
      width={isMobile ? '100%' : 640}
      destroyOnHidden
      title={
        <div className="flex min-w-0 flex-col gap-1 pr-2">
          <div className="flex flex-wrap items-center gap-2">
            <span className="font-mono text-xs text-ink-muted">{task.code}</span>
            <StatusTag tone={TASK_PRIORITY[task.priority].tone} icon={TASK_STATUS[task.status].icon} size="sm">
              {TASK_PRIORITY[task.priority].label}
            </StatusTag>
            {task.repeat !== 'none' && (
              <StatusTag tone="info" icon={Repeat} size="sm">
                {TASK_REPEAT[task.repeat]}
              </StatusTag>
            )}
          </div>
          <span className="text-base font-semibold">{task.title}</span>
        </div>
      }
    >
      <div className="flex flex-col gap-5 text-[13px]">
        {/* Trạng thái */}
        <div className="grid grid-cols-4 gap-1 rounded-lg bg-page p-1">
          {TASK_STATUS_ORDER.map((s) => {
            const S = TASK_STATUS[s];
            return (
              <button
                key={s}
                type="button"
                onClick={() => setStatus(s)}
                className={cn('flex items-center justify-center gap-1 rounded-md py-1.5 text-xs font-medium', task.status === s ? 'bg-surface text-primary-700 shadow-[var(--shadow-soft-sm)]' : 'text-ink-secondary hover:text-ink')}
              >
                <S.icon size={13} aria-hidden /> {S.label}
              </button>
            );
          })}
        </div>

        {task.description && <p className="whitespace-pre-line text-ink">{task.description}</p>}

        <div className="grid grid-cols-1 gap-3 sm:grid-cols-2">
          <label className="flex flex-col gap-1">
            <span className="text-xs text-ink-muted">Người thực hiện</span>
            <Select value={task.assigneeId} options={staffOptions} onChange={(v) => updateTask(task.id, { assigneeId: v as string })} allowClear={false} />
          </label>
          <label className="flex flex-col gap-1">
            <span className="text-xs text-ink-muted">Hạn {overdue && <span className="font-medium text-danger-600">— quá hạn</span>}</span>
            <DateInput value={task.due ? dayjs(task.due) : null} onChange={(d) => updateTask(task.id, { due: d?.format('YYYY-MM-DD') })} />
          </label>
          <label className="flex flex-col gap-1">
            <span className="text-xs text-ink-muted">Ưu tiên</span>
            <Select value={task.priority} options={TASK_PRIORITY_OPTIONS} onChange={(v) => updateTask(task.id, { priority: v as Task['priority'] })} showSearch={false} allowClear={false} />
          </label>
          <label className="flex flex-col gap-1">
            <span className="text-xs text-ink-muted">Người theo dõi</span>
            <Select mode="multiple" value={task.watcherIds} options={staffOptions} onChange={(v) => updateTask(task.id, { watcherIds: v as string[] })} placeholder="Không có" />
          </label>
        </div>

        <dl className="grid grid-cols-[110px_1fr] gap-y-1.5">
          <dt className="text-ink-muted">Người giao</dt>
          <dd className="text-ink">{nameOf(employees, task.creatorId)} · {fmtDate(task.createdAt)}</dd>
          {task.link && (
            <>
              <dt className="text-ink-muted">Liên quan</dt>
              <dd>
                <button type="button" onClick={() => navigate(linkPath(task.link!))} className="inline-flex items-center gap-1 font-medium text-primary-700 hover:underline">
                  <Link2 size={13} aria-hidden /> {task.link.label} <ExternalLink size={12} aria-hidden />
                </button>
              </dd>
            </>
          )}
          {task.completedAt && (
            <>
              <dt className="text-ink-muted">Hoàn thành</dt>
              <dd className="text-success-700">{fmtDateTime(task.completedAt)}</dd>
            </>
          )}
        </dl>

        {/* Checklist */}
        <section>
          <h3 className="mb-2 text-sm font-semibold text-ink">
            Checklist {task.checklist.length > 0 && <span className="font-normal text-ink-muted">({task.checklist.filter((c) => c.done).length}/{task.checklist.length})</span>}
          </h3>
          <ul className="flex flex-col gap-1.5">
            {task.checklist.map((c) => (
              <li key={c.id}>
                <Checkbox checked={c.done} onChange={(e) => updateTask(task.id, { checklist: task.checklist.map((x) => (x.id === c.id ? { ...x, done: e.target.checked } : x)) })}>
                  <span className={cn(c.done && 'text-ink-muted line-through')}>{c.text}</span>
                </Checkbox>
              </li>
            ))}
          </ul>
          <div className="mt-2 flex gap-2">
            <TextInput size="small" value={item} onChange={(e) => setItem(e.target.value)} placeholder="Thêm mục..." onPressEnter={() => item.trim() && (updateTask(task.id, { checklist: [...task.checklist, { id: `c${Date.now()}`, text: item.trim(), done: false }] }), setItem(''))} />
            <Button icon={Plus} size="sm" variant="soft" disabled={!item.trim()} onClick={() => (updateTask(task.id, { checklist: [...task.checklist, { id: `c${Date.now()}`, text: item.trim(), done: false }] }), setItem(''))}>
              Thêm
            </Button>
          </div>
        </section>

        {/* Bình luận */}
        <section>
          <h3 className="mb-2 flex items-center gap-1.5 text-sm font-semibold text-ink">
            <MessageSquare size={15} aria-hidden /> Trao đổi
          </h3>
          <ol className="flex flex-col gap-2">
            {task.comments.map((c, i) => (
              <li key={i} className="rounded-lg bg-page px-3 py-2">
                <UserCell size="sm" name={c.by} subtitle={fmtDateTime(c.at)} />
                <p className="mt-1 pl-9 text-ink">{c.text}</p>
              </li>
            ))}
          </ol>
          <div className="mt-2 flex gap-2">
            <TextInput value={text} onChange={(e) => setText(e.target.value)} placeholder="Báo tiến độ, hỏi thêm..." onPressEnter={() => text.trim() && (comment(task.id, me, text.trim()), setText(''))} />
            <Button icon={Send} disabled={!text.trim()} onClick={() => (comment(task.id, me, text.trim()), setText(''))}>
              Gửi
            </Button>
          </div>
        </section>
      </div>
    </Drawer>
  );
}
