import { useMemo, useState } from 'react';
import { Form } from 'antd';
import dayjs from 'dayjs';
import { AlarmClock, CalendarCheck, CheckCircle2, Columns3, Eye, Link2, List, ListChecks, MessageSquare, Repeat } from 'lucide-react';
import { Avatar, Button, DataTable, DateInput, ModalForm, PageHeader, Select, StatCard, StatusTag, TextArea, TextInput, useNotify, type DataColumn } from '@/components/common';
import { useClassStore } from '@/features/academic/classes/classStore';
import { useCrmStore } from '@/features/crm/crmStore';
import { useHrStore } from '@/features/hr/hrStore';
import { meOf, nameOf } from '@/features/hr/hrUtils';
import { PRIORITY_RANK, TASK_PRIORITY, TASK_PRIORITY_OPTIONS, TASK_REPEAT_OPTIONS, TASK_STATUS, TASK_STATUS_ORDER } from '@/features/work/constants';
import { completeTask, TaskDrawer } from '@/features/work/TaskDrawer';
import type { Task, TaskPriority, TaskRepeat, TaskStatus } from '@/features/work/types';
import { useWorkStore } from '@/features/work/workStore';
import { useAuth } from '@/hooks/useAuth';
import { cn } from '@/lib/cn';
import { fmtDate } from '@/lib/date';
import { USE_API } from '@/lib/api/config';
import { createTask } from '@/features/work/workApi';

type Scope = 'mine' | 'assigned' | 'watching' | 'all';

interface NewTask {
  title: string;
  description?: string;
  assigneeId: string;
  watcherIds?: string[];
  priority: TaskPriority;
  due?: dayjs.Dayjs;
  repeat: TaskRepeat;
  linkType?: 'lead' | 'class' | 'student';
  linkId?: string;
}

/** Task — cấp trên giao cho cấp dưới. Kéo thả thẻ giữa các cột để đổi trạng thái. Chế độ API: workApi (kéo thả cập nhật lạc quan). */
export default function TasksPage() {
  const { user } = useAuth();
  const notify = useNotify();
  const tasks = useWorkStore((s) => s.tasks);
  const updateTask = useWorkStore((s) => s.updateTask);
  const addTask = useWorkStore((s) => s.addTask);
  const employees = useHrStore((s) => s.employees);
  const leads = useCrmStore((s) => s.leads);
  const students = useCrmStore((s) => s.students);
  const classes = useClassStore((s) => s.classes);
  const me = meOf(employees, user);

  const [scope, setScope] = useState<Scope>('all');
  const [assignee, setAssignee] = useState<string>();
  const [priority, setPriority] = useState<string>();
  const [view, setView] = useState<'board' | 'list'>('board');
  const [openId, setOpenId] = useState<string | null>(null);
  const [creating, setCreating] = useState(false);
  const [dragId, setDragId] = useState<string | null>(null);
  const [overCol, setOverCol] = useState<TaskStatus | null>(null);
  const [form] = Form.useForm<NewTask>();
  const linkType = Form.useWatch('linkType', form);

  const today = dayjs().format('YYYY-MM-DD');
  const inScope = (t: Task) =>
    scope === 'all' || (scope === 'mine' ? t.assigneeId === me?.id : scope === 'assigned' ? t.creatorId === me?.id : t.watcherIds.includes(me?.id ?? ''));
  const data = useMemo(
    () =>
      tasks
        .filter((t) => inScope(t) && (!assignee || t.assigneeId === assignee) && (!priority || t.priority === priority))
        .sort((a, b) => PRIORITY_RANK[a.priority] - PRIORITY_RANK[b.priority] || (a.due ?? '9').localeCompare(b.due ?? '9')),
    [tasks, scope, assignee, priority, me], // eslint-disable-line react-hooks/exhaustive-deps
  );
  const open = data.filter((t) => t.status !== 'done');
  const stats = {
    overdue: open.filter((t) => t.due && t.due < today).length,
    today: open.filter((t) => t.due === today).length,
    review: data.filter((t) => t.status === 'review').length,
    doneWeek: data.filter((t) => t.completedAt && dayjs(t.completedAt).isAfter(dayjs().subtract(7, 'day'))).length,
  };

  const move = (id: string, status: TaskStatus) => {
    const t = tasks.find((x) => x.id === id);
    if (!t || t.status === status) return;
    if (status === 'done') {
      const next = completeTask(t);
      notify.toast.success(next ? 'Đã xong — tạo lần lặp tiếp theo' : 'Đã hoàn thành');
    } else updateTask(id, { status });
  };

  const Card = ({ t }: { t: Task }) => {
    const overdue = t.status !== 'done' && t.due && t.due < today;
    const done = t.checklist.filter((c) => c.done).length;
    return (
      <div
        draggable
        onDragStart={() => setDragId(t.id)}
        onDragEnd={() => {
          setDragId(null);
          setOverCol(null);
        }}
        onClick={() => setOpenId(t.id)}
        className={cn(
          'relative flex cursor-pointer flex-col gap-1.5 overflow-hidden rounded-lg border border-line bg-surface p-2.5 pl-3.5 shadow-[var(--shadow-soft-sm)] hover:shadow-[var(--shadow-soft-md)]',
          dragId === t.id && 'opacity-50',
        )}
      >
        <span className={cn('absolute inset-y-0 left-0 w-1', TASK_PRIORITY[t.priority].bar)} aria-hidden />
        <p className={cn('text-[13px] leading-snug font-medium text-ink', t.status === 'done' && 'text-ink-muted line-through')}>{t.title}</p>
        {t.link && (
          <span className="inline-flex items-center gap-1 truncate text-[11px] text-primary-700">
            <Link2 size={11} aria-hidden /> {t.link.label}
          </span>
        )}
        <div className="flex items-center justify-between gap-2 text-[11px] text-ink-muted">
          <span className="flex items-center gap-2">
            {t.due && (
              <span className={cn('inline-flex items-center gap-0.5', overdue ? 'font-semibold text-danger-600' : t.due === today ? 'font-medium text-warning-700' : '')}>
                <AlarmClock size={11} aria-hidden /> {t.due === today ? 'Hôm nay' : fmtDate(t.due).slice(0, 5)}
              </span>
            )}
            {t.repeat !== 'none' && <Repeat size={11} aria-label="Lặp lại" />}
            {t.checklist.length > 0 && (
              <span className="inline-flex items-center gap-0.5">
                <ListChecks size={11} aria-hidden /> {done}/{t.checklist.length}
              </span>
            )}
            {t.comments.length > 0 && (
              <span className="inline-flex items-center gap-0.5">
                <MessageSquare size={11} aria-hidden /> {t.comments.length}
              </span>
            )}
          </span>
          <span title={nameOf(employees, t.assigneeId)}>
            <Avatar name={nameOf(employees, t.assigneeId)} size="xs" />
          </span>
        </div>
      </div>
    );
  };

  const columns: DataColumn<Task>[] = [
    { key: 'title', title: 'Công việc', width: 320, mobile: 'title', render: (_, t) => <div className="leading-5"><p className="font-medium text-ink">{t.title}</p><p className="text-xs text-ink-muted">{t.code}{t.link && ` · ${t.link.label}`}</p></div> },
    { key: 'assignee', title: 'Người làm', width: 170, mobile: 'subtitle', render: (_, t) => nameOf(employees, t.assigneeId) },
    { key: 'priority', title: 'Ưu tiên', width: 110, sorter: (a, b) => PRIORITY_RANK[a.priority] - PRIORITY_RANK[b.priority], render: (_, t) => <StatusTag tone={TASK_PRIORITY[t.priority].tone} icon={AlarmClock} size="sm">{TASK_PRIORITY[t.priority].label}</StatusTag> },
    { key: 'due', title: 'Hạn', width: 110, sorter: (a, b) => (a.due ?? '9').localeCompare(b.due ?? '9'), render: (_, t) => (t.due ? <span className={cn(t.status !== 'done' && t.due < today && 'font-medium text-danger-600')}>{fmtDate(t.due)}</span> : '—') },
    { key: 'status', title: 'Trạng thái', width: 130, mobile: 'aside', render: (_, t) => <StatusTag tone={TASK_STATUS[t.status].tone} icon={TASK_STATUS[t.status].icon} size="sm">{TASK_STATUS[t.status].label}</StatusTag> },
    { key: 'creator', title: 'Người giao', width: 160, render: (_, t) => nameOf(employees, t.creatorId) },
  ];

  const staffOptions = employees.filter((e) => e.status !== 'quit').map((e) => ({ value: e.id, label: `${e.fullName} · ${e.position}` }));
  const linkOptions =
    linkType === 'lead'
      ? leads.filter((l) => !l.hidden).map((l) => ({ value: l.id, label: `${l.code} — ${l.fullName}` }))
      : linkType === 'class'
        ? classes.filter((c) => c.status !== 'cancelled' && c.status !== 'finished').map((c) => ({ value: c.id, label: `${c.code} — ${c.name}` }))
        : linkType === 'student'
          ? students.filter((s) => !s.hidden).map((s) => ({ value: s.id, label: `${s.code} — ${s.fullName}` }))
          : [];

  return (
    <>
      <PageHeader
        title="Task"
        subtitle="Giao việc, theo dõi tiến độ — kéo thả thẻ giữa các cột để đổi trạng thái"
        breadcrumb={[{ title: 'Công việc' }, { title: 'Task' }]}
        actions={
          <Button action="create" onClick={() => setCreating(true)} collapseOnMobile>
            Giao task
          </Button>
        }
      />

      <div className="mb-4 grid grid-cols-2 gap-3 lg:grid-cols-4">
        <StatCard label="Quá hạn" value={stats.overdue} icon={AlarmClock} tone={stats.overdue ? 'danger' : 'success'} />
        <StatCard label="Hạn hôm nay" value={stats.today} icon={CalendarCheck} tone="warning" />
        <StatCard label="Chờ duyệt" value={stats.review} icon={Eye} tone="info" hint="Người giao kiểm tra & chuyển Hoàn thành" />
        <StatCard label="Xong trong 7 ngày" value={stats.doneWeek} icon={CheckCircle2} tone="success" />
      </div>

      <div className="mb-4 flex flex-col gap-3 rounded-[var(--radius-card)] border border-line bg-surface p-3 lg:flex-row lg:items-center">
        <div className="flex flex-wrap gap-1.5">
          {(
            [
              ['mine', 'Giao cho tôi'],
              ['assigned', 'Tôi giao'],
              ['watching', 'Tôi theo dõi'],
              ['all', 'Tất cả'],
            ] as [Scope, string][]
          ).map(([k, l]) => (
            <button key={k} type="button" onClick={() => setScope(k)} className={cn('h-8 rounded-full border px-3 text-[13px] font-medium', scope === k ? 'border-primary-600 bg-primary-600 text-white' : 'border-line text-ink-secondary hover:bg-primary-50')}>
              {l}
            </button>
          ))}
        </div>
        <div className="flex flex-1 flex-wrap items-center gap-2 lg:justify-end">
          <Select placeholder="Người làm" value={assignee} onChange={setAssignee} options={staffOptions} className="!w-56" />
          <Select placeholder="Ưu tiên" value={priority} onChange={setPriority} options={TASK_PRIORITY_OPTIONS} showSearch={false} className="!w-36" />
          <div className="inline-flex rounded-lg border border-line p-0.5">
            {(
              [
                ['board', Columns3, 'Kanban'],
                ['list', List, 'Danh sách'],
              ] as const
            ).map(([k, Icon, l]) => (
              <button key={k} type="button" aria-label={l} aria-pressed={view === k} onClick={() => setView(k)} className={cn('grid h-8 w-9 place-items-center rounded-md', view === k ? 'bg-primary-600 text-white' : 'text-ink-secondary hover:bg-page')}>
                <Icon size={16} aria-hidden />
              </button>
            ))}
          </div>
        </div>
      </div>

      {view === 'board' ? (
        <div className="-mx-4 overflow-x-auto px-4 pb-2 sm:mx-0 sm:px-0">
          <div className="grid min-w-[900px] grid-cols-4 gap-3">
            {TASK_STATUS_ORDER.map((st) => {
              const S = TASK_STATUS[st];
              const list = data.filter((t) => t.status === st && (st !== 'done' || !t.completedAt || dayjs(t.completedAt).isAfter(dayjs().subtract(14, 'day'))));
              return (
                <section
                  key={st}
                  onDragOver={(e) => {
                    e.preventDefault();
                    setOverCol(st);
                  }}
                  onDragLeave={() => setOverCol((c) => (c === st ? null : c))}
                  onDrop={() => {
                    if (dragId) move(dragId, st);
                    setDragId(null);
                    setOverCol(null);
                  }}
                  className={cn('flex min-h-[300px] flex-col gap-2 rounded-[var(--radius-card)] bg-page p-2 transition-colors', overCol === st && 'bg-primary-50 ring-2 ring-primary-300')}
                >
                  <h3 className="flex items-center justify-between px-1 text-[13px] font-semibold text-ink">
                    <span className="inline-flex items-center gap-1.5">
                      <S.icon size={14} aria-hidden /> {S.label}
                    </span>
                    <span className="tabular rounded-full bg-surface px-1.5 text-xs text-ink-muted">{list.length}</span>
                  </h3>
                  {list.map((t) => (
                    <Card key={t.id} t={t} />
                  ))}
                  {!list.length && <p className="px-1 py-6 text-center text-xs text-ink-muted">Kéo thẻ vào đây</p>}
                </section>
              );
            })}
          </div>
        </div>
      ) : (
        <DataTable<Task> storageKey="work-tasks" columns={columns} data={data} rowKey="id" unitLabel="task" searchKeys={['title', 'code', 'description']} onRowClick={(t) => setOpenId(t.id)} />
      )}

      {openId && <TaskDrawer taskId={openId} onClose={() => setOpenId(null)} />}

      <ModalForm<NewTask>
        form={form}
        open={creating}
        onClose={() => setCreating(false)}
        width={640}
        columns={2}
        title="Giao task"
        submitAction="create"
        submitText="Giao việc"
        successMessage="Đã giao task — người nhận được thông báo"
        initialValues={{ priority: 'normal', repeat: 'none', due: dayjs().add(2, 'day') }}
        onSubmit={async (v) => {
          if (USE_API)
            return void (await createTask({
              title: v.title.trim(),
              description: v.description?.trim() || undefined,
              assigneeId: v.assigneeId,
              watcherIds: v.watcherIds ?? [],
              priority: v.priority,
              due: v.due?.format('YYYY-MM-DD'),
              repeat: v.repeat,
              link: v.linkType && v.linkId ? { type: v.linkType, id: v.linkId } : undefined,
            }));
          const label = v.linkId ? linkOptions.find((o) => o.value === v.linkId)?.label : undefined;
          addTask({
            title: v.title.trim(),
            description: v.description?.trim() || undefined,
            assigneeId: v.assigneeId,
            creatorId: me?.id ?? 'e-huy',
            watcherIds: v.watcherIds ?? [],
            priority: v.priority,
            status: 'todo',
            due: v.due?.format('YYYY-MM-DD'),
            repeat: v.repeat,
            link: v.linkType && v.linkId && label ? { type: v.linkType, id: v.linkId, label: `${v.linkType === 'class' ? 'Lớp' : v.linkType === 'lead' ? 'Lead' : 'HV'} ${label}` } : undefined,
          });
        }}
      >
        <Form.Item name="title" label="Công việc" className="md:col-span-2" rules={[{ required: true, whitespace: true, message: 'Nhập tên công việc' }]}>
          <TextInput maxLength={160} placeholder="VD: Gọi lại 5 lead hẹn test" />
        </Form.Item>
        <Form.Item name="description" label="Mô tả" className="md:col-span-2">
          <TextArea rows={2} maxLength={1000} />
        </Form.Item>
        <Form.Item name="assigneeId" label="Giao cho" rules={[{ required: true, message: 'Chọn người làm' }]}>
          <Select options={staffOptions} placeholder="Chọn nhân viên" />
        </Form.Item>
        <Form.Item name="watcherIds" label="Người theo dõi">
          <Select mode="multiple" options={staffOptions} placeholder="Không bắt buộc" />
        </Form.Item>
        <Form.Item name="priority" label="Ưu tiên">
          <Select options={TASK_PRIORITY_OPTIONS} showSearch={false} allowClear={false} />
        </Form.Item>
        <Form.Item name="due" label="Hạn">
          <DateInput />
        </Form.Item>
        <Form.Item name="repeat" label="Lặp lại" extra="Hoàn thành xong tự tạo lần tiếp theo.">
          <Select options={TASK_REPEAT_OPTIONS} showSearch={false} allowClear={false} />
        </Form.Item>
        <div className="grid grid-cols-[110px_1fr] gap-x-2">
          <Form.Item name="linkType" label="Gắn với">
            <Select
              options={[
                { value: 'lead', label: 'Lead' },
                { value: 'class', label: 'Lớp' },
                { value: 'student', label: 'Học viên' },
              ]}
              showSearch={false}
              onChange={() => form.setFieldValue('linkId', undefined)}
            />
          </Form.Item>
          <Form.Item name="linkId" label=" ">
            <Select options={linkOptions} disabled={!linkType} placeholder="Chọn" optionFilterProp="label" />
          </Form.Item>
        </div>
      </ModalForm>
    </>
  );
}
