import { useMemo, useState } from 'react';
import { Tabs } from 'antd';
import dayjs from 'dayjs';
import { AlarmClock, CheckCircle2, ListChecks, Play, RotateCcw } from 'lucide-react';
import { Button, EmptyState, PageHeader, Select, StatCard, useConfirm, useNotify } from '@/components/common';
import { useHrStore } from '@/features/hr/hrStore';
import { nameOf } from '@/features/hr/hrUtils';
import { TaskItem, dueInfo } from '@/features/me/TaskItem';
import { useMe } from '@/features/me/useMe';
import { PRIORITY_RANK, TASK_PRIORITY_OPTIONS } from '@/features/work/constants';
import { completeTask, TaskDrawer } from '@/features/work/TaskDrawer';
import type { Task } from '@/features/work/types';
import { useWorkStore } from '@/features/work/workStore';
import { useAuth } from '@/hooks/useAuth';

/** Nhóm theo hạn để biết làm gì trước */
const GROUPS: { key: string; label: string; test: (t: Task, today: string, weekEnd: string) => boolean }[] = [
  { key: 'overdue', label: 'Quá hạn', test: (t, d) => Boolean(t.due && t.due < d) },
  { key: 'today', label: 'Hôm nay', test: (t, d) => t.due === d },
  { key: 'week', label: 'Tuần này', test: (t, d, w) => Boolean(t.due && t.due > d && t.due <= w) },
  { key: 'later', label: 'Sau đó', test: (t, _d, w) => Boolean(t.due && t.due > w) },
  { key: 'none', label: 'Không có hạn', test: (t) => !t.due },
];

/** Cá nhân › Việc của tôi: chỉ task giao cho BẢN THÂN — bắt đầu làm, tick checklist, xác nhận hoàn thành */
export default function MyTasksPage() {
  const { user } = useAuth();
  const notify = useNotify();
  const { confirm, confirmDialog } = useConfirm();
  const me = useMe();
  const tasks = useWorkStore((s) => s.tasks);
  const updateTask = useWorkStore((s) => s.updateTask);
  const comment = useWorkStore((s) => s.commentTask);
  const employees = useHrStore((s) => s.employees);
  const [tab, setTab] = useState('open');
  const [priority, setPriority] = useState<string>();
  const [openId, setOpenId] = useState<string | null>(null);

  const mine = useMemo(() => tasks.filter((t) => t.assigneeId === me?.id), [tasks, me]);
  if (!me) return <EmptyState icon={ListChecks} title="Tài khoản chưa gắn hồ sơ nhân viên" />;

  const today = dayjs().format('YYYY-MM-DD');
  const weekEnd = dayjs().endOf('week').add(1, 'day').format('YYYY-MM-DD');
  const open = mine.filter((t) => t.status !== 'done' && (!priority || t.priority === priority)).sort((a, b) => PRIORITY_RANK[a.priority] - PRIORITY_RANK[b.priority] || (a.due ?? '9').localeCompare(b.due ?? '9'));
  const done = mine.filter((t) => t.status === 'done').sort((a, b) => (b.completedAt ?? '').localeCompare(a.completedAt ?? ''));

  const finish = async (t: Task) => {
    const left = t.checklist.filter((c) => !c.done);
    const ok = await confirm({
      title: `Xác nhận hoàn thành “${t.title}”?`,
      description: `${nameOf(employees, t.creatorId)} (người giao) sẽ nhận thông báo.`,
      consequences: left.length ? [`Còn ${left.length} mục checklist chưa tick: ${left.map((c) => c.text).join(', ')}`] : undefined,
      tone: left.length ? 'warning' : 'success',
      confirmText: 'Hoàn thành',
    });
    if (!ok) return;
    const repeated = completeTask(t);
    comment(t.id, user?.fullName ?? me.fullName, 'Đã xác nhận hoàn thành.');
    notify.toast.success(repeated ? 'Đã hoàn thành — đã tạo lần lặp tiếp theo' : 'Đã hoàn thành');
  };

  const card = (t: Task) => (
    <TaskItem
      key={t.id}
      task={t}
      people={`Giao bởi ${nameOf(employees, t.creatorId)}`}
      onOpen={() => setOpenId(t.id)}
      actions={
        t.status === 'done' ? (
          <Button
            icon={RotateCcw}
            size="sm"
            variant="ghost"
            tone="neutral"
            onClick={() => {
              updateTask(t.id, { status: 'doing' });
              notify.toast.info('Đã mở lại task');
            }}
          >
            Mở lại
          </Button>
        ) : (
          <>
            {t.status === 'todo' && (
              <Button icon={Play} size="sm" variant="soft" onClick={() => updateTask(t.id, { status: 'doing' })}>
                Bắt đầu
              </Button>
            )}
            <Button icon={CheckCircle2} size="sm" tone="success" onClick={() => finish(t)}>
              Hoàn thành
            </Button>
          </>
        )
      }
    />
  );

  return (
    <>
      <PageHeader title="Việc của tôi" subtitle="Task được giao cho bạn. Bấm vào task để xem chi tiết, tick checklist, bình luận với người giao." breadcrumb={[{ title: 'Cá nhân' }, { title: 'Việc của tôi' }]} />

      <div className="mb-4 grid grid-cols-2 gap-3 lg:grid-cols-4">
        <StatCard label="Đang mở" value={mine.filter((t) => t.status !== 'done').length} icon={ListChecks} tone="primary" />
        <StatCard label="Quá hạn" value={mine.filter((t) => t.status !== 'done' && dueInfo(t)?.overdue).length} icon={AlarmClock} tone="danger" />
        <StatCard label="Hạn hôm nay" value={mine.filter((t) => t.status !== 'done' && t.due === today).length} icon={AlarmClock} tone="warning" />
        <StatCard label="Xong 7 ngày qua" value={done.filter((t) => t.completedAt && dayjs(t.completedAt).isAfter(dayjs().subtract(7, 'day'))).length} icon={CheckCircle2} tone="success" />
      </div>

      <Tabs
        activeKey={tab}
        onChange={setTab}
        tabBarExtraContent={<Select placeholder="Ưu tiên" value={priority} onChange={setPriority} options={TASK_PRIORITY_OPTIONS} showSearch={false} className="!w-36" size="small" />}
        items={[
          {
            key: 'open',
            label: `Cần làm (${open.length})`,
            children: open.length ? (
              <div className="flex flex-col gap-5">
                {GROUPS.map((g) => {
                  const list = open.filter((t) => g.test(t, today, weekEnd));
                  if (!list.length) return null;
                  return (
                    <section key={g.key}>
                      <h2 className={`mb-2 text-sm font-semibold ${g.key === 'overdue' ? 'text-danger-700' : 'text-ink'}`}>
                        {g.label} ({list.length})
                      </h2>
                      <div className="flex flex-col gap-2">{list.map(card)}</div>
                    </section>
                  );
                })}
              </div>
            ) : (
              <EmptyState icon={CheckCircle2} title="Không còn việc nào 🎉" />
            ),
          },
          {
            key: 'done',
            label: `Đã xong (${done.length})`,
            children: done.length ? <div className="flex flex-col gap-2">{done.slice(0, 50).map(card)}</div> : <EmptyState icon={ListChecks} title="Chưa hoàn thành task nào" />,
          },
        ]}
      />
      <TaskDrawer taskId={openId} onClose={() => setOpenId(null)} />
      {confirmDialog}
    </>
  );
}
