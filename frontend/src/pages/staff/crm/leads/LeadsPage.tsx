import { useMemo, useState, type Key } from 'react';
import { Checkbox, Tooltip } from 'antd';
import dayjs from 'dayjs';
import { AlarmClock, Eye, EyeOff, GraduationCap, UserCheck } from 'lucide-react';
import {
  Button,
  DataTable,
  ModalForm,
  PageHeader,
  Select,
  StatusTag,
  UserCell,
  useNotify,
  type DataColumn,
} from '@/components/common';
import { cn } from '@/lib/cn';
import { fmtDate, fmtDateTime, fromNow } from '@/lib/date';
import { useAuth } from '@/hooks/useAuth';
import { ALL_BRANCHES } from '@/types/auth';
import { courseLabel, LEAD_SOURCE_OPTIONS, LEAD_SOURCES, LEAD_STATUS_OPTIONS, saleName, SALES, SLA_MINUTES } from '@/features/crm/constants';
import { isFollowUpOverdue, isSlaOverdue, useCrmStore } from '@/features/crm/crmStore';
import { formToLead, LeadFormFields, type LeadFormValues } from '@/features/crm/components/LeadFormFields';
import { LeadDetailDrawer, type LeadTab } from '@/features/crm/components/LeadDetailDrawer';
import { useHideLead } from '@/features/crm/useHideLead';
import type { Lead, LeadStatus } from '@/features/crm/types';

/** Trang Lead — DỮ LIỆU GIẢ (store trong bộ nhớ, tải lại trang sẽ về dữ liệu mẫu). */
export default function LeadsPage() {
  const { user, branchId, currentBranch, can } = useAuth();
  const notify = useNotify();
  const leads = useCrmStore((s) => s.leads);
  const links = useCrmStore((s) => s.links);
  const addLead = useCrmStore((s) => s.addLead);
  const updateLead = useCrmStore((s) => s.updateLead);
  const { toggleHidden, confirmDialog } = useHideLead();

  const [status, setStatus] = useState<LeadStatus | 'all'>('all');
  const [source, setSource] = useState<string>();
  const [assignee, setAssignee] = useState<string>();
  const [showHidden, setShowHidden] = useState(false);
  const [selected, setSelected] = useState<Key[]>([]);
  const [creating, setCreating] = useState(false);
  const [openId, setOpenId] = useState<string | null>(null);
  const [openTab, setOpenTab] = useState<LeadTab>('info');

  const open = (id: string, tab: LeadTab = 'info') => {
    setOpenTab(tab);
    setOpenId(id);
  };

  const studentCount = useMemo(() => {
    const m = new Map<string, number>();
    links.forEach((k) => m.set(k.leadId, (m.get(k.leadId) ?? 0) + 1));
    return m;
  }, [links]);

  // Lọc theo chi nhánh đang chọn + ẩn/hiện + nguồn + sale (TRƯỚC lọc trạng thái → để đếm số theo trạng thái)
  const base = useMemo(
    () =>
      leads.filter(
        (l) =>
          (branchId === ALL_BRANCHES || l.branchId === branchId) &&
          (showHidden || !l.hidden) &&
          (!source || l.source === source) &&
          (!assignee || l.assigneeId === assignee),
      ),
    [leads, branchId, showHidden, source, assignee],
  );
  const counts = useMemo(() => {
    const c: Record<string, number> = { all: base.length };
    base.forEach((l) => (c[l.status] = (c[l.status] ?? 0) + 1));
    return c;
  }, [base]);
  const data = status === 'all' ? base : base.filter((l) => l.status === status);
  const slaCount = base.filter((l) => isSlaOverdue(l, SLA_MINUTES)).length;

  const columns: DataColumn<Lead>[] = [
    {
      key: 'fullName',
      title: 'Lead',
      width: 230,
      mobile: 'title',
      sorter: (a, b) => a.fullName.localeCompare(b.fullName, 'vi'),
      render: (_, r) => (
        <div className={cn(r.hidden && 'opacity-60')}>
          <UserCell name={r.fullName} subtitle={r.code} size="sm" />
        </div>
      ),
    },
    { key: 'phone', title: 'Điện thoại', dataIndex: 'phone', width: 135, mobile: 'subtitle' },
    { key: 'email', title: 'Email', dataIndex: 'email', width: 190 },
    {
      key: 'source',
      title: 'Nguồn',
      width: 140,
      render: (_, r) => {
        const s = LEAD_SOURCES[r.source];
        const Icon = s.icon;
        return (
          <span className="inline-flex items-center gap-1.5">
            <Icon size={14} className="text-ink-muted" aria-hidden />
            {s.label}
          </span>
        );
      },
    },
    {
      key: 'status',
      title: 'Trạng thái',
      width: 170,
      mobile: 'aside',
      render: (_, r) => (
        <span className="inline-flex flex-wrap items-center gap-1">
          <StatusTag group="lead" status={r.status} size="sm" />
          {isSlaOverdue(r, SLA_MINUTES) && (
            <Tooltip title={`Lead mới chưa được liên hệ sau ${SLA_MINUTES} phút (tạo ${fromNow(r.createdAt)})`}>
              <span>
                <StatusTag tone="danger" icon={AlarmClock} size="sm">
                  Quá SLA
                </StatusTag>
              </span>
            </Tooltip>
          )}
          {r.hidden && (
            <StatusTag tone="neutral" icon={EyeOff} size="sm">
              Đã ẩn
            </StatusTag>
          )}
        </span>
      ),
    },
    { key: 'interest', title: 'Quan tâm', width: 160, render: (_, r) => courseLabel(r.interest) ?? '—' },
    { key: 'assignee', title: 'Sale phụ trách', width: 150, render: (_, r) => saleName(r.assigneeId) ?? <span className="text-ink-muted">Chưa giao</span> },
    {
      key: 'students',
      title: 'Học viên',
      width: 110,
      align: 'center',
      sorter: (a, b) => (studentCount.get(a.id) ?? 0) - (studentCount.get(b.id) ?? 0),
      render: (_, r) => {
        const n = studentCount.get(r.id) ?? 0;
        return n ? (
          <button
            type="button"
            onClick={(e) => {
              e.stopPropagation();
              open(r.id, 'students');
            }}
            className="inline-flex items-center gap-1 rounded-md bg-success-50 px-1.5 py-0.5 text-[13px] font-medium text-success-700 hover:bg-success-100"
          >
            <GraduationCap size={14} aria-hidden /> {n}
          </button>
        ) : (
          '—'
        );
      },
    },
    {
      key: 'nextFollowUpAt',
      title: 'Hẹn gọi lại',
      width: 150,
      sorter: (a, b) => dayjs(a.nextFollowUpAt ?? 0).valueOf() - dayjs(b.nextFollowUpAt ?? 0).valueOf(),
      render: (_, r) =>
        r.nextFollowUpAt ? (
          <span className={cn('tabular', isFollowUpOverdue(r) ? 'font-medium text-danger-600' : 'text-ink')} title={isFollowUpOverdue(r) ? 'Đã quá hẹn' : undefined}>
            {fmtDateTime(r.nextFollowUpAt)}
          </span>
        ) : (
          '—'
        ),
    },
    {
      key: 'createdAt',
      title: 'Ngày tạo',
      width: 120,
      sorter: (a, b) => dayjs(a.createdAt).valueOf() - dayjs(b.createdAt).valueOf(),
      render: (_, r) => <span title={fmtDateTime(r.createdAt)}>{fmtDate(r.createdAt)}</span>,
    },
  ];

  const defaultBranch = currentBranch?.id ?? user?.branchIds[0] ?? 'cg';

  return (
    <>
      <PageHeader
        title="Lead"
        subtitle={`Khách hàng tiềm năng · ${currentBranch ? `Chi nhánh ${currentBranch.name}` : 'Toàn hệ thống'}`}
        breadcrumb={[{ title: 'Tuyển sinh' }, { title: 'Lead' }]}
        actions={
          <>
            <Button action="export" variant="outline" collapseOnMobile onClick={() => notify.toast.info('Xuất Excel (giả lập)')} />
            {can('crm.leads') && (
              <Button action="create" collapseOnMobile onClick={() => setCreating(true)}>
                Thêm lead
              </Button>
            )}
          </>
        }
      />

      {/* Lọc nhanh theo trạng thái */}
      <div className="-mx-4 mb-4 flex gap-2 overflow-x-auto px-4 pb-1 [scrollbar-width:none] sm:mx-0 sm:flex-wrap sm:px-0 [&::-webkit-scrollbar]:hidden" role="tablist" aria-label="Lọc theo trạng thái">
        {[{ value: 'all', label: 'Tất cả' } as const, ...LEAD_STATUS_OPTIONS].map((o) => {
          const active = status === o.value;
          return (
            <button
              key={o.value}
              type="button"
              role="tab"
              aria-selected={active}
              onClick={() => setStatus(o.value)}
              className={cn(
                'inline-flex h-9 shrink-0 items-center gap-2 rounded-full border px-3.5 text-sm font-medium transition-colors',
                active ? 'border-primary-600 bg-primary-600 text-white' : 'border-line bg-surface text-ink-secondary hover:border-primary-300 hover:bg-primary-50',
              )}
            >
              {o.label}
              <span className={cn('tabular rounded-full px-1.5 text-xs', active ? 'bg-white/20' : 'bg-disabled text-ink-muted')}>{counts[o.value] ?? 0}</span>
            </button>
          );
        })}
        {slaCount > 0 && (
          <span className="inline-flex h-9 shrink-0 items-center gap-1.5 rounded-full bg-danger-50 px-3.5 text-sm font-medium text-danger-700">
            <AlarmClock size={16} aria-hidden /> {slaCount} lead quá SLA {SLA_MINUTES} phút
          </span>
        )}
      </div>

      <DataTable<Lead>
        storageKey="crm-leads"
        columns={columns}
        data={data}
        rowKey="id"
        unitLabel="lead"
        searchKeys={['fullName', 'phone', 'email', 'code']}
        onRowClick={(r) => open(r.id)}
        search={{
          placeholder: 'Tìm theo tên, SĐT, email, mã lead...',
          activeFilterCount: [source, assignee, showHidden || undefined].filter(Boolean).length,
          filters: (
            <>
              <Select placeholder="Nguồn lead" value={source} onChange={setSource} options={LEAD_SOURCE_OPTIONS} className="lg:!w-40" />
              <Select
                placeholder="Sale phụ trách"
                value={assignee}
                onChange={setAssignee}
                options={SALES.filter((s) => branchId === ALL_BRANCHES || s.branchId === branchId).map((s) => ({ value: s.id, label: s.name }))}
                className="lg:!w-44"
              />
              <Checkbox checked={showHidden} onChange={(e) => setShowHidden(e.target.checked)} className="whitespace-nowrap">
                Hiện lead đã ẩn
              </Checkbox>
            </>
          ),
          onReset: () => {
            setSource(undefined);
            setAssignee(undefined);
            setShowHidden(false);
            setStatus('all');
          },
        }}
        selectedKeys={selected}
        onSelectionChange={(keys) => setSelected(keys)}
        bulkActions={(keys) => (
          <>
            <Select
              size="small"
              placeholder="Giao cho Sale..."
              className="!w-44"
              options={SALES.map((s) => ({ value: s.id, label: s.name }))}
              onChange={(v) => {
                if (!v) return;
                keys.forEach((k) => updateLead(String(k), { assigneeId: v as string }));
                notify.success({ title: `Đã giao ${keys.length} lead`, description: `cho ${saleName(v as string)}` });
                setSelected([]);
              }}
            />
            <Button icon={UserCheck} size="sm" tone="neutral" variant="ghost" onClick={() => setSelected([])}>
              Bỏ chọn
            </Button>
          </>
        )}
        rowActions={(r) => (
          <>
            <Button action="edit" iconOnly size="sm" onClick={() => open(r.id)} />
            <Button
              icon={r.hidden ? Eye : EyeOff}
              iconOnly
              size="sm"
              tone="neutral"
              variant="soft"
              aria-label={r.hidden ? 'Bỏ ẩn lead' : 'Ẩn lead'}
              onClick={() => toggleHidden(r)}
            />
          </>
        )}
        actionsWidth={110}
        empty={{
          title: 'Chưa có lead nào',
          description: 'Thêm lead thủ công hoặc kết nối nguồn lead (Facebook, website) ở mục Tích hợp.',
          action: <Button action="create" onClick={() => setCreating(true)}>Thêm lead</Button>,
        }}
      />

      {/* ===== Thêm lead thủ công ===== */}
      <ModalForm<LeadFormValues>
        open={creating}
        onClose={() => setCreating(false)}
        title="Thêm lead"
        description="Nhập thủ công (khách gọi hotline, đến trực tiếp, được giới thiệu...)"
        columns={2}
        submitAction="create"
        submitText="Thêm lead"
        successMessage="Đã thêm lead mới"
        initialValues={{ status: 'new', branchId: defaultBranch, assigneeId: user?.id === 'u-sale' ? 'u-sale' : undefined }}
        onSubmit={async (v) => {
          await new Promise((r) => setTimeout(r, 700)); // giả lập gọi API
          const lead = addLead(formToLead(v), user?.fullName ?? 'Nhân viên');
          setStatus('all');
          setTimeout(() => notify.info({ title: `Lead ${lead.code} đã được tạo`, description: 'Bấm vào lead để thêm lịch sử tư vấn.' }), 300);
        }}
      >
        <LeadFormFields
          onOpenLead={(id) => {
            setCreating(false);
            open(id);
          }}
        />
      </ModalForm>

      <LeadDetailDrawer leadId={openId} initialTab={openTab} onClose={() => setOpenId(null)} onOpenLead={(id) => open(id)} />
      {confirmDialog}
    </>
  );
}
