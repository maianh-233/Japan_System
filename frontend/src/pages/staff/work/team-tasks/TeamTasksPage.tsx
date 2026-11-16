import { useMemo, useState } from 'react';
import { Form, Tabs } from 'antd';
import dayjs from 'dayjs';
import { AlarmClock, CheckCircle2, ListChecks, RotateCcw, Users } from 'lucide-react';
import { Avatar, Button, DateInput, EmptyState, ModalForm, PageHeader, Select, StatCard, TextArea, TextInput, useNotify } from '@/components/common';
import { DEPARTMENTS } from '@/features/hr/constants';
import { useHrStore } from '@/features/hr/hrStore';
import { nameOf } from '@/features/hr/hrUtils';
import { TaskItem, dueInfo } from '@/features/me/TaskItem';
import { useMe, useMyReports } from '@/features/me/useMe';
import { PRIORITY_RANK, TASK_PRIORITY_OPTIONS, TASK_REPEAT_OPTIONS } from '@/features/work/constants';
import { TaskDrawer } from '@/features/work/TaskDrawer';
import type { TaskPriority, TaskRepeat } from '@/features/work/types';
import { useWorkStore } from '@/features/work/workStore';
import { cn } from '@/lib/cn';

interface Values {
  assigneeIds: string[];
  title: string;
  description?: string;
  checklist?: string;
  priority: TaskPriority;
  due?: dayjs.Dayjs;
  repeat: TaskRepeat;
}

/**
 * Công việc › Giao việc cấp dưới: giao task NHỎ cho nhân viên DƯỚI QUYỀN mình (theo cây quản lý, cả gián tiếp).
 * Khác "Task toàn hệ thống" (nhân sự / quản lý giao cho mọi nhân viên).
 */
export default function TeamTasksPage() {
  const notify = useNotify();
  const me = useMe();
  const reports = useMyReports();
  const tasks = useWorkStore((s) => s.tasks);
  const addTask = useWorkStore((s) => s.addTask);
  const updateTask = useWorkStore((s) => s.updateTask);
  const employees = useHrStore((s) => s.employees);
  const [who, setWho] = useState<string>();
  const [onlyMine, setOnlyMine] = useState(true);
  const [creating, setCreating] = useState<string[] | null>(null);
  const [openId, setOpenId] = useState<string | null>(null);

  const ids = useMemo(() => new Set(reports.map((r) => r.e.id)), [reports]);
  const teamTasks = useMemo(
    () => tasks.filter((t) => ids.has(t.assigneeId) && (!onlyMine || t.creatorId === me?.id)).sort((a, b) => PRIORITY_RANK[a.priority] - PRIORITY_RANK[b.priority] || (a.due ?? '9').localeCompare(b.due ?? '9')),
    [tasks, ids, onlyMine, me],
  );

  if (!me) return <EmptyState icon={Users} title="Tài khoản chưa gắn hồ sơ nhân viên" />;
  if (!reports.length)
    return (
      <>
        <PageHeader title="Giao việc cấp dưới" breadcrumb={[{ title: 'Công việc' }, { title: 'Giao việc cấp dưới' }]} />
        <EmptyState icon={Users} title="Bạn chưa có nhân viên dưới quyền" description="Trang này dành cho người quản lý. Cấp dưới được xác định theo “Quản lý trực tiếp” trong hồ sơ nhân viên." />
      </>
    );

  const list = teamTasks.filter((t) => !who || t.assigneeId === who);
  const open = list.filter((t) => t.status !== 'done');
  const done = list.filter((t) => t.status === 'done').sort((a, b) => (b.completedAt ?? '').localeCompare(a.completedAt ?? ''));
  const loadOf = (id: string) => {
    const x = teamTasks.filter((t) => t.assigneeId === id && t.status !== 'done');
    return { open: x.length, overdue: x.filter((t) => dueInfo(t)?.overdue).length };
  };
  const card = (t: (typeof list)[number]) => (
    <TaskItem
      key={t.id}
      task={t}
      people={`Giao cho ${nameOf(employees, t.assigneeId)}${t.creatorId !== me.id ? ` · do ${nameOf(employees, t.creatorId)} giao` : ''}`}
      onOpen={() => setOpenId(t.id)}
      actions={
        t.status === 'review' ? (
          <Button icon={CheckCircle2} size="sm" tone="success" onClick={() => (updateTask(t.id, { status: 'done' }), notify.toast.success('Đã duyệt hoàn thành'))}>
            Duyệt xong
          </Button>
        ) : t.status === 'done' && t.creatorId === me.id ? (
          <Button
            icon={RotateCcw}
            size="sm"
            variant="ghost"
            tone="neutral"
            onClick={() => {
              updateTask(t.id, { status: 'doing' });
              notify.toast.info(`Đã yêu cầu ${nameOf(employees, t.assigneeId)} làm lại`);
            }}
          >
            Yêu cầu làm lại
          </Button>
        ) : undefined
      }
    />
  );

  return (
    <>
      <PageHeader
        title="Giao việc cấp dưới"
        subtitle={`Giao task nhỏ cho ${reports.length} nhân viên dưới quyền bạn (${reports.filter((r) => r.depth === 1).length} trực tiếp). Nhân viên xác nhận hoàn thành ở “Việc của tôi”.`}
        breadcrumb={[{ title: 'Công việc' }, { title: 'Giao việc cấp dưới' }]}
        actions={
          <Button action="create" collapseOnMobile onClick={() => setCreating(who ? [who] : [])}>
            Giao việc
          </Button>
        }
      />

      <div className="mb-4 grid grid-cols-2 gap-3 lg:grid-cols-4">
        <StatCard label="Đang mở" value={teamTasks.filter((t) => t.status !== 'done').length} icon={ListChecks} tone="primary" />
        <StatCard label="Quá hạn" value={teamTasks.filter((t) => t.status !== 'done' && dueInfo(t)?.overdue).length} icon={AlarmClock} tone="danger" />
        <StatCard label="Chờ bạn duyệt" value={teamTasks.filter((t) => t.status === 'review').length} icon={CheckCircle2} tone="warning" />
        <StatCard label="Xong 7 ngày qua" value={teamTasks.filter((t) => t.completedAt && dayjs(t.completedAt).isAfter(dayjs().subtract(7, 'day'))).length} icon={CheckCircle2} tone="success" />
      </div>

      {/* Nhân viên dưới quyền — bấm để lọc */}
      <div className="mb-4 flex gap-2 overflow-x-auto pb-1">
        {reports.map(({ e, depth }) => {
          const l = loadOf(e.id);
          const active = who === e.id;
          return (
            <button
              key={e.id}
              type="button"
              onClick={() => setWho(active ? undefined : e.id)}
              className={cn('flex min-w-[190px] shrink-0 items-center gap-2.5 rounded-xl border p-2.5 text-left transition-colors', active ? 'border-primary-600 bg-primary-50' : 'border-line bg-surface hover:border-primary-300')}
            >
              <Avatar name={e.fullName} size="sm" />
              <span className="min-w-0 flex-1">
                <span className="block truncate text-[13px] font-medium text-ink">{e.fullName}</span>
                <span className="block truncate text-[11px] text-ink-muted">
                  {depth === 1 ? `${DEPARTMENTS[e.department]} · ${e.position}` : `gián tiếp · qua ${nameOf(employees, e.managerId)}`}
                </span>
              </span>
              <span className="text-right text-[11px] leading-4">
                <span className="tabular block font-semibold text-ink">{l.open}</span>
                {l.overdue > 0 && <span className="text-danger-700">{l.overdue} trễ</span>}
              </span>
            </button>
          );
        })}
      </div>

      <Tabs
        tabBarExtraContent={
          <Select
            size="small"
            value={onlyMine ? 'mine' : 'all'}
            onChange={(v) => setOnlyMine(v === 'mine')}
            options={[
              { value: 'mine', label: 'Task tôi giao' },
              { value: 'all', label: 'Mọi task của nhóm' },
            ]}
            allowClear={false}
            showSearch={false}
            className="!w-44"
          />
        }
        items={[
          { key: 'open', label: `Đang mở (${open.length})`, children: open.length ? <div className="flex flex-col gap-2">{open.map(card)}</div> : <EmptyState icon={ListChecks} title="Không có task đang mở" action={<Button action="create" onClick={() => setCreating(who ? [who] : [])}>Giao việc</Button>} /> },
          { key: 'done', label: `Đã xong (${done.length})`, children: done.length ? <div className="flex flex-col gap-2">{done.slice(0, 50).map(card)}</div> : <EmptyState icon={CheckCircle2} title="Chưa có task hoàn thành" /> },
        ]}
      />

      <TaskDrawer taskId={openId} onClose={() => setOpenId(null)} />

      <ModalForm<Values>
        key={creating ? creating.join() : 'none'}
        open={Boolean(creating)}
        onClose={() => setCreating(null)}
        title="Giao việc cho cấp dưới"
        description="Chọn nhiều người → mỗi người nhận 1 task riêng."
        width={620}
        submitAction="send"
        submitText="Giao việc"
        successMessage="Đã giao việc — nhân viên nhận thông báo"
        initialValues={{ assigneeIds: creating ?? [], priority: 'normal', repeat: 'none', due: dayjs().add(1, 'day') }}
        onSubmit={(v) => {
          const checklist = (v.checklist ?? '')
            .split('\n')
            .map((x) => x.trim())
            .filter(Boolean)
            .map((text, i) => ({ id: `CL-${Date.now()}-${i}`, text, done: false }));
          v.assigneeIds.forEach((assigneeId) =>
            addTask({ title: v.title.trim(), description: v.description, assigneeId, creatorId: me.id, watcherIds: [], priority: v.priority, status: 'todo', due: v.due?.format('YYYY-MM-DD'), repeat: v.repeat, checklist }),
          );
        }}
      >
        <Form.Item name="assigneeIds" label="Giao cho" rules={[{ required: true, message: 'Chọn ít nhất 1 người' }]}>
          <Select
            mode="multiple"
            placeholder="Chỉ nhân viên dưới quyền bạn"
            options={reports.map(({ e, depth }) => {
              const l = loadOf(e.id);
              return { value: e.id, label: `${e.fullName} · ${e.position}${depth > 1 ? ' (gián tiếp)' : ''} · ${l.open} việc đang mở` };
            })}
          />
        </Form.Item>
        <Form.Item name="title" label="Việc cần làm" rules={[{ required: true, whitespace: true, message: 'Nhập tên việc' }]}>
          <TextInput placeholder="VD: Gọi lại 5 lead hẹn test tuần này" />
        </Form.Item>
        <Form.Item name="description" label="Mô tả">
          <TextArea rows={2} maxLength={1000} />
        </Form.Item>
        <Form.Item name="checklist" label="Checklist (mỗi dòng 1 mục)">
          <TextArea rows={3} placeholder={'Gọi điện\nGhi lịch sử tư vấn\nCập nhật trạng thái lead'} />
        </Form.Item>
        <div className="grid grid-cols-1 gap-x-3 sm:grid-cols-3">
          <Form.Item name="priority" label="Ưu tiên">
            <Select options={TASK_PRIORITY_OPTIONS} allowClear={false} showSearch={false} />
          </Form.Item>
          <Form.Item name="due" label="Hạn">
            <DateInput disabledDate={(d) => d.isBefore(dayjs(), 'day')} />
          </Form.Item>
          <Form.Item name="repeat" label="Lặp lại">
            <Select options={TASK_REPEAT_OPTIONS} allowClear={false} showSearch={false} />
          </Form.Item>
        </div>
      </ModalForm>
    </>
  );
}
