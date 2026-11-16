import { useState } from 'react';
import { Form, Progress } from 'antd';
import dayjs, { type Dayjs } from 'dayjs';
import { CalendarDays, CheckCircle2, ClipboardCheck, IdCard, KeyRound, Plus, Rocket, UserCheck, UserPlus } from 'lucide-react';
import { useNavigate } from 'react-router-dom';
import { Button, DateInput, EmptyState, ModalForm, PageHeader, Select, StatCard, StatusTag, TextInput, UserCell, useConfirm, useNotify } from '@/components/common';
import { DEPARTMENT_OPTIONS, DEPARTMENTS, EMPLOYEE_KIND, ONBOARDING_GROUPS, ONBOARDING_TEMPLATE } from '@/features/hr/constants';
import { useHrStore } from '@/features/hr/hrStore';
import type { Department, EmployeeKind, Onboarding, OnboardingItem } from '@/features/hr/types';
import { useAuth } from '@/hooks/useAuth';
import { cn } from '@/lib/cn';
import { fmtDate, fmtDateTime } from '@/lib/date';
import { getBranches } from '@/features/system/useBranches';
import { ALL_BRANCHES } from '@/types/auth';

type Filter = 'active' | 'done' | 'all';
const GROUP_KEYS = Object.keys(ONBOARDING_GROUPS) as OnboardingItem['group'][];

interface NewValues {
  fullName: string;
  phone: string;
  email: string;
  position: string;
  kind: EmployeeKind;
  department: Department;
  branchId: string;
  startDate: Dayjs;
  mentor: string;
}

const progressOf = (o: Onboarding) => Math.round((o.items.filter((i) => i.done).length / Math.max(1, o.items.length)) * 100);

/** Onboarding nhân sự mới: checklist giấy tờ / tài khoản / thiết bị / đào tạo → tạo hồ sơ NV. DỮ LIỆU GIẢ. */
export default function OnboardingPage() {
  const { user, branchId } = useAuth();
  const notify = useNotify();
  const navigate = useNavigate();
  const { confirm, confirmDialog } = useConfirm();
  const onboardings = useHrStore((s) => s.onboardings);
  const employees = useHrStore((s) => s.employees);
  const updateOnboarding = useHrStore((s) => s.updateOnboarding);
  const addOnboarding = useHrStore((s) => s.addOnboarding);
  const addEmployee = useHrStore((s) => s.addEmployee);

  const [filter, setFilter] = useState<Filter>('active');
  const [creating, setCreating] = useState(false);
  const scoped = onboardings.filter((o) => branchId === ALL_BRANCHES || o.branchId === branchId);
  const isDone = (o: Onboarding) => Boolean(o.employeeId) && progressOf(o) === 100;
  const list = scoped.filter((o) => (filter === 'all' ? true : filter === 'done' ? isDone(o) : !isDone(o))).sort((a, b) => a.startDate.localeCompare(b.startDate));
  const [selId, setSelId] = useState<string | null>(null);
  const sel = scoped.find((o) => o.id === selId) ?? list[0];
  const today = dayjs();

  const stats = {
    active: scoped.filter((o) => !isDone(o)).length,
    week: scoped.filter((o) => !isDone(o) && dayjs(o.startDate).diff(today, 'day') <= 7).length,
    noAccount: scoped.filter((o) => !o.employeeId).length,
    done: scoped.filter(isDone).length,
  };

  const toggle = (o: Onboarding, key: string) => {
    updateOnboarding(o.id, {
      items: o.items.map((i) => (i.key === key ? (i.done ? { ...i, done: false, doneBy: undefined, doneAt: undefined } : { ...i, done: true, doneBy: user?.fullName, doneAt: dayjs().format('YYYY-MM-DDTHH:mm') }) : i)),
    });
  };

  /** Tạo hồ sơ nhân viên + tài khoản */
  const createEmployee = async (o: Onboarding) => {
    const docsOk = o.items.filter((i) => i.group === 'docs').every((i) => i.done);
    const ok = await confirm({
      title: `Tạo hồ sơ nhân viên cho ${o.fullName}?`,
      description: `${docsOk ? '' : '⚠ Giấy tờ chưa đủ. '}Hệ thống tạo hồ sơ ${EMPLOYEE_KIND[o.kind].toLowerCase()} (thử việc, HĐ 2 tháng từ ${fmtDate(o.startDate)}) và tài khoản đăng nhập gửi qua ${o.email}.`,
      confirmText: 'Tạo hồ sơ & tài khoản',
      tone: docsOk ? 'primary' : 'warning',
    });
    if (!ok) return;
    const start = dayjs(o.startDate);
    const e = addEmployee({
      fullName: o.fullName,
      gender: 'female',
      phone: o.phone,
      email: o.email,
      branchId: o.branchId,
      kind: o.kind,
      department: o.department,
      position: o.position,
      shiftBased: o.department === 'sales' || o.department === 'reception',
      status: 'probation',
      joinedAt: o.startDate,
      contract: { type: 'probation', no: `HĐTV-${start.year()}/${Date.now().toString().slice(-3)}`, start: o.startDate, end: start.add(2, 'month').format('YYYY-MM-DD') },
      leaveBalance: 0,
      hasAccount: true,
      teacher: o.kind === 'staff' ? undefined : { type: 'vn', levels: [], certificates: [], rate: { offline: 0, online: 0 }, availability: { 1: [], 2: [], 3: [], 4: [], 5: [], 6: [], 7: [] } },
    });
    updateOnboarding(o.id, {
      employeeId: e.id,
      items: o.items.map((i) => (i.key === 'account' && !i.done ? { ...i, done: true, doneBy: user?.fullName, doneAt: dayjs().format('YYYY-MM-DDTHH:mm') } : i)),
    });
    notify.toast.success(`Đã tạo hồ sơ ${e.code} — bổ sung đơn giá / lịch rảnh ở trang ${o.kind === 'staff' ? 'Nhân viên' : 'Giáo viên'}`);
  };

  return (
    <>
      <PageHeader
        title="Onboarding"
        subtitle="Checklist tiếp nhận nhân sự mới: giấy tờ, tài khoản hệ thống, thiết bị, đào tạo hội nhập"
        breadcrumb={[{ title: 'Nhân sự' }, { title: 'Onboarding' }]}
        actions={
          <Button icon={Plus} onClick={() => setCreating(true)} collapseOnMobile>
            Thêm người mới
          </Button>
        }
      />

      <div className="mb-4 grid grid-cols-2 gap-3 lg:grid-cols-4">
        <StatCard label="Đang onboarding" value={stats.active} icon={Rocket} onClick={() => setFilter('active')} />
        <StatCard label="Bắt đầu trong 7 ngày" value={stats.week} icon={CalendarDays} tone={stats.week ? 'warning' : 'neutral'} />
        <StatCard label="Chưa có hồ sơ / tài khoản" value={stats.noAccount} icon={KeyRound} tone={stats.noAccount ? 'danger' : 'success'} />
        <StatCard label="Đã hoàn tất" value={stats.done} icon={CheckCircle2} tone="success" onClick={() => setFilter('done')} />
      </div>

      <div className="grid gap-4 lg:grid-cols-[340px_1fr]">
        <div className="flex flex-col gap-2">
          <div className="flex gap-1.5">
            {(
              [
                ['active', 'Đang làm'],
                ['done', 'Hoàn tất'],
                ['all', 'Tất cả'],
              ] as [Filter, string][]
            ).map(([k, l]) => (
              <button key={k} type="button" onClick={() => setFilter(k)} className={cn('h-8 rounded-full border px-3 text-sm font-medium', filter === k ? 'border-primary-600 bg-primary-600 text-white' : 'border-line bg-surface text-ink-secondary hover:bg-primary-50')}>
                {l}
              </button>
            ))}
          </div>
          {list.map((o) => {
            const p = progressOf(o);
            const days = dayjs(o.startDate).diff(today.startOf('day'), 'day');
            return (
              <button key={o.id} type="button" onClick={() => setSelId(o.id)} className={cn('flex flex-col gap-2 rounded-xl border bg-surface p-3 text-left', sel?.id === o.id ? 'border-primary-500 ring-2 ring-primary-100' : 'border-line hover:border-primary-300')}>
                <div className="flex items-start justify-between gap-2">
                  <UserCell size="sm" name={o.fullName} subtitle={o.position} />
                  {o.employeeId ? (
                    <StatusTag tone="success" icon={UserCheck} size="sm">Có hồ sơ</StatusTag>
                  ) : (
                    <StatusTag tone="warning" icon={KeyRound} size="sm">Chưa hồ sơ</StatusTag>
                  )}
                </div>
                <Progress percent={p} size="small" strokeColor={p === 100 ? 'var(--color-success-600)' : undefined} />
                <p className={cn('text-xs', days < 0 ? 'text-ink-muted' : days <= 3 ? 'font-medium text-warning-700' : 'text-ink-secondary')}>
                  Bắt đầu {fmtDate(o.startDate)} {days > 0 ? `· còn ${days} ngày` : days === 0 ? '· hôm nay' : `· đã làm ${-days} ngày`}
                </p>
              </button>
            );
          })}
          {!list.length && <EmptyState icon={ClipboardCheck} title="Không có hồ sơ onboarding" />}
        </div>

        {sel ? (
          <section className="rounded-xl border border-line bg-surface p-4">
            <div className="mb-4 flex flex-wrap items-start justify-between gap-3">
              <div>
                <h2 className="text-lg font-semibold text-ink">{sel.fullName}</h2>
                <p className="text-sm text-ink-secondary">
                  {sel.position} · {DEPARTMENTS[sel.department]} · {getBranches(false).find((b) => b.id === sel.branchId)?.name}
                </p>
                <p className="text-xs text-ink-muted">
                  {sel.phone} · {sel.email} · Người hướng dẫn: {sel.mentor} · Bắt đầu {fmtDate(sel.startDate)}
                </p>
              </div>
              {sel.employeeId ? (
                <Button icon={IdCard} variant="soft" onClick={() => navigate(sel.kind === 'staff' ? '/admin/hr/employees' : '/admin/hr/teachers')}>
                  Hồ sơ {employees.find((e) => e.id === sel.employeeId)?.code}
                </Button>
              ) : (
                <Button icon={UserPlus} onClick={() => createEmployee(sel)}>
                  Tạo hồ sơ NV + tài khoản
                </Button>
              )}
            </div>

            <div className="grid gap-4 md:grid-cols-2">
              {GROUP_KEYS.map((g) => {
                const items = sel.items.filter((i) => i.group === g);
                if (!items.length) return null;
                const done = items.filter((i) => i.done).length;
                return (
                  <div key={g} className="rounded-lg border border-line">
                    <div className="flex items-center justify-between border-b border-line bg-canvas px-3 py-2">
                      <span className="text-sm font-semibold text-ink">{ONBOARDING_GROUPS[g]}</span>
                      <span className={cn('tabular text-xs', done === items.length ? 'text-success-700' : 'text-ink-muted')}>{done}/{items.length}</span>
                    </div>
                    <ul className="flex flex-col">
                      {items.map((i) => (
                        <li key={i.key} className="border-b border-line last:border-0">
                          <label className="flex cursor-pointer items-start gap-2.5 px-3 py-2 hover:bg-canvas">
                            <input type="checkbox" checked={i.done} onChange={() => toggle(sel, i.key)} className="mt-0.5 size-4 accent-primary-600" />
                            <span className="text-[13px] leading-5">
                              <span className={cn(i.done && 'text-ink-muted line-through')}>{i.label}</span>
                              {i.done && i.doneBy && <span className="block text-[11px] text-ink-muted">{i.doneBy} · {i.doneAt ? fmtDateTime(i.doneAt) : ''}</span>}
                            </span>
                          </label>
                        </li>
                      ))}
                    </ul>
                  </div>
                );
              })}
            </div>
          </section>
        ) : null}
      </div>

      <ModalForm<NewValues>
        open={creating}
        onClose={() => setCreating(false)}
        width={640}
        columns={2}
        title="Thêm người mới (không qua tuyển dụng)"
        description="VD: nhân sự điều chuyển, giáo viên giới thiệu trực tiếp. Ứng viên từ trang Tuyển dụng sẽ tự tạo onboarding khi nhận việc."
        submitAction="create"
        successMessage="Đã tạo checklist onboarding"
        initialValues={{ kind: 'staff', department: 'sales', branchId: branchId === ALL_BRANCHES ? getBranches(false)[0].id : branchId, startDate: dayjs().add(7, 'day'), mentor: user?.fullName }}
        onSubmit={(v) => {
          const ob = addOnboarding({
            fullName: v.fullName.trim(),
            phone: v.phone,
            email: v.email,
            position: v.position.trim(),
            kind: v.kind,
            department: v.kind === 'staff' ? v.department : 'teaching',
            branchId: v.branchId,
            startDate: v.startDate.format('YYYY-MM-DD'),
            mentor: v.mentor,
            items: ONBOARDING_TEMPLATE.filter((t) => !t.teacherOnly || v.kind !== 'staff').map((t) => ({ key: t.key, group: t.group, label: t.label, done: false })),
          });
          setFilter('active');
          setSelId(ob.id);
        }}
      >
        <Form.Item name="fullName" label="Họ tên" rules={[{ required: true, whitespace: true }]}>
          <TextInput />
        </Form.Item>
        <Form.Item name="kind" label="Loại">
          <Select options={(Object.keys(EMPLOYEE_KIND) as EmployeeKind[]).map((k) => ({ value: k, label: EMPLOYEE_KIND[k] }))} showSearch={false} allowClear={false} />
        </Form.Item>
        <Form.Item name="phone" label="Điện thoại" rules={[{ required: true, pattern: /^0\d{9}$/, message: 'SĐT 10 số' }]}>
          <TextInput maxLength={10} inputMode="tel" />
        </Form.Item>
        <Form.Item name="email" label="Email" rules={[{ required: true, type: 'email' }]}>
          <TextInput />
        </Form.Item>
        <Form.Item name="position" label="Vị trí" rules={[{ required: true, whitespace: true }]}>
          <TextInput />
        </Form.Item>
        <Form.Item name="department" label="Phòng ban">
          <Select options={DEPARTMENT_OPTIONS} showSearch={false} allowClear={false} />
        </Form.Item>
        <Form.Item name="branchId" label="Chi nhánh">
          <Select options={getBranches(false).map((b) => ({ value: b.id, label: b.name }))} showSearch={false} allowClear={false} />
        </Form.Item>
        <Form.Item name="startDate" label="Ngày bắt đầu" rules={[{ required: true }]}>
          <DateInput />
        </Form.Item>
        <Form.Item name="mentor" label="Người hướng dẫn" rules={[{ required: true }]}>
          <Select options={employees.filter((e) => e.kind === 'staff' && e.status !== 'quit').map((e) => ({ value: e.fullName, label: `${e.fullName} · ${e.position}` }))} />
        </Form.Item>
      </ModalForm>
      {confirmDialog}
    </>
  );
}
