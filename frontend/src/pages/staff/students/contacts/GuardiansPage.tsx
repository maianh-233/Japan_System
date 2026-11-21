import { useMemo, useState } from 'react';
import { Tooltip } from 'antd';
import { ChevronDown, ChevronUp, ContactRound, KeyRound, Phone, Star, TriangleAlert, UserRoundPlus } from 'lucide-react';
import { useNavigate } from 'react-router-dom';
import { Button, DataTable, DrawerForm, PageHeader, Select, StatusTag, UserCell, useNotify, type DataColumn } from '@/components/common';
import { useClassStore } from '@/features/academic/classes/classStore';
import { useCrmStore } from '@/features/crm/crmStore';
import { GuardianDetailDrawer, type GuardianTab } from '@/features/student/guardians/GuardianDetailDrawer';
import { GuardianFormFields, type GuardianFormValues } from '@/features/student/guardians/GuardianFormFields';
import { ADULT_AGE, ageOf, buildGuardianRows, guardianGaps, NOTIFY_CHANNELS, type GuardianRow } from '@/features/student/guardians/guardianUtils';
import { GUARDIAN_ROLE } from '@/features/student/constants';
import { createGuardian, updateGuardian } from '@/features/student/guardians/guardianApi';
import { USE_API } from '@/lib/api/config';
import { useStudentStore } from '@/features/student/studentStore';
import { useAuth } from '@/hooks/useAuth';
import { useOpenParam } from '@/hooks/useOpenParam';
import { cn } from '@/lib/cn';
import { formatCurrency } from '@/lib/format';
import { getBranches } from '@/features/system/useBranches';
import { ALL_BRANCHES } from '@/types/auth';

type AccountFilter = 'all' | 'active' | 'locked' | 'none';
type Dialog = { kind: 'create'; studentIds?: string[] } | { kind: 'edit'; leadId: string } | null;

const ACCOUNT_TABS: { value: AccountFilter; label: string }[] = [
  { value: 'all', label: 'Tất cả' },
  { value: 'active', label: 'Có tài khoản' },
  { value: 'none', label: 'Chưa cấp tài khoản' },
  { value: 'locked', label: 'Tài khoản bị khóa' },
];
const ROLE_FILTER = [
  { value: 'primary', label: 'Liên hệ chính' },
  { value: 'billing', label: 'Nhận hóa đơn' },
];
const TUITION_FILTER = [
  { value: 'debt', label: 'Con còn nợ' },
  { value: 'overdue', label: 'Có khoản quá hạn' },
];
const STUDY_FILTER = [
  { value: 'studying', label: 'Có con đang học' },
  { value: 'none', label: 'Không còn con đang học' },
];

/**
 * Phụ huynh / Người giám hộ (HV-12) — người liên hệ (lead) có liên kết với học viên.
 * Chế độ API: nạp sau đăng nhập (/crm/guardians, quyền students.contacts); thêm / sửa / liên kết gọi server. ⏳ Cấp tài khoản cổng (HV-13) còn giả.
 */
export default function GuardiansPage() {
  const { user, branchId, currentBranch, branches, can } = useAuth();
  const notify = useNotify();
  const navigate = useNavigate();
  const leads = useCrmStore((s) => s.leads);
  const links = useCrmStore((s) => s.links);
  const students = useCrmStore((s) => s.students);
  const addLead = useCrmStore((s) => s.addLead);
  const updateLead = useCrmStore((s) => s.updateLead);
  const linkStudent = useCrmStore((s) => s.linkStudent);
  const updateLink = useCrmStore((s) => s.updateLink);
  const findDuplicate = useCrmStore((s) => s.findDuplicate);
  const enrollments = useStudentStore((s) => s.enrollments);
  const invoices = useStudentStore((s) => s.invoices);
  const accounts = useStudentStore((s) => s.accounts);
  const issueAccount = useStudentStore((s) => s.issueAccount);
  const classes = useClassStore((s) => s.classes);
  const editable = can('students.contacts');

  const [accountTab, setAccountTab] = useState<AccountFilter>('all');
  const [role, setRole] = useState<string>();
  const [tuition, setTuition] = useState<string>();
  const [study, setStudy] = useState<string>();
  const [dialog, setDialog] = useState<Dialog>(null);
  const [openId, setOpenId] = useState<string | null>(null);
  useOpenParam(setOpenId);
  const [openTab, setOpenTab] = useState<GuardianTab>('children');
  const [showGaps, setShowGaps] = useState(false);

  const open = (id: string, tab: GuardianTab = 'children') => {
    setOpenTab(tab);
    setOpenId(id);
  };

  const rows = useMemo(
    () => buildGuardianRows({ leads, links, students, enrollments, invoices, classes, accounts }),
    [leads, links, students, enrollments, invoices, classes, accounts],
  );

  const inBranch = (id: string) => branchId === ALL_BRANCHES || id === branchId;

  const base = useMemo(
    () =>
      rows.filter(
        (r) =>
          // Phụ huynh thuộc chi nhánh HOẶC có con học ở chi nhánh đang xem
          (inBranch(r.branchId) || r.children.some((c) => inBranch(c.student.branchId))) &&
          (!role || (role === 'primary' ? r.isPrimary : r.isBilling)) &&
          (!tuition || (tuition === 'overdue' ? r.overdue : r.debt > 0)) &&
          (!study || (study === 'studying' ? r.studying > 0 : r.studying === 0)),
      ),
    // eslint-disable-next-line react-hooks/exhaustive-deps
    [rows, branchId, role, tuition, study],
  );
  const counts = useMemo(() => {
    const c: Record<AccountFilter, number> = { all: base.length, active: 0, locked: 0, none: 0 };
    base.forEach((r) => {
      if (!r.account) c.none++;
      else if (r.account.status === 'locked') c.locked++;
      else c.active++;
    });
    return c;
  }, [base]);
  const data =
    accountTab === 'all'
      ? base
      : base.filter((r) => (accountTab === 'none' ? !r.account : accountTab === 'locked' ? r.account?.status === 'locked' : r.account && r.account.status !== 'locked'));

  const gaps = useMemo(() => {
    const g = guardianGaps(students, links);
    return { minorsWithout: g.minorsWithout.filter((s) => inBranch(s.branchId)), noPrimary: g.noPrimary.filter((s) => inBranch(s.branchId)) };
    // eslint-disable-next-line react-hooks/exhaustive-deps
  }, [students, links, branchId]);
  const gapCount = gaps.minorsWithout.length + gaps.noPrimary.length;

  const columns: DataColumn<GuardianRow>[] = [
    {
      key: 'fullName',
      title: 'Phụ huynh / Người giám hộ',
      width: 230,
      mobile: 'title',
      sorter: (a, b) => a.fullName.localeCompare(b.fullName, 'vi'),
      render: (_, r) => <UserCell name={r.fullName} subtitle={`${r.code} · ${getBranches(false).find((b) => b.id === r.branchId)?.name ?? ''}`} size="sm" />,
    },
    {
      key: 'phone',
      title: 'Liên hệ',
      width: 190,
      mobile: 'subtitle',
      render: (_, r) => (
        <div className="leading-5">
          <a href={`tel:${r.phone}`} onClick={(e) => e.stopPropagation()} className="tabular font-medium text-primary-700 hover:underline">
            {r.phone}
          </a>
          <p className="truncate text-xs text-ink-muted">{r.email ?? 'Chưa có email'}</p>
        </div>
      ),
    },
    {
      key: 'children',
      title: 'Học viên liên kết',
      width: 300,
      sorter: (a, b) => a.children.length - b.children.length,
      render: (_, r) => (
        <ul className="flex flex-col gap-1">
          {r.children.map((c) => (
            <li key={c.student.id} className="flex min-w-0 items-center gap-1.5 text-[13px]">
              {c.link.primary ? (
                <Tooltip title="Người liên hệ chính">
                  <Star size={13} className="shrink-0 fill-warning-600 text-warning-600" aria-label="Liên hệ chính" />
                </Tooltip>
              ) : (
                <span className="w-[13px] shrink-0" />
              )}
              <span className={cn('shrink-0 font-medium text-ink', c.study !== 'studying' && c.study !== 'reserved' && 'text-ink-muted')}>{c.student.fullName}</span>
              <span className="min-w-0 truncate text-xs text-ink-muted">
                {GUARDIAN_ROLE[c.link.relationship as keyof typeof GUARDIAN_ROLE]}
                {c.classes.length > 0 && ` · ${c.classes.join(', ')}`}
              </span>
            </li>
          ))}
        </ul>
      ),
    },
    {
      key: 'debt',
      title: 'Học phí của con',
      width: 150,
      align: 'right',
      sorter: (a, b) => a.debt - b.debt,
      render: (_, r) =>
        r.debt ? (
          <div className="leading-5">
            <p className="tabular font-medium text-danger-600">{formatCurrency(r.debt)}</p>
            <p className="text-xs text-ink-muted">{r.overdue ? 'Có khoản quá hạn' : 'Còn nợ'}</p>
          </div>
        ) : (
          <span className="text-ink-muted">Đã đóng đủ</span>
        ),
    },
    {
      key: 'channels',
      title: 'Nhận tin qua',
      width: 130,
      render: (_, r) =>
        r.notifyChannels?.length ? (
          <span className="flex gap-1">
            {r.notifyChannels.map((c) => {
              const Icon = NOTIFY_CHANNELS[c].icon;
              return (
                <Tooltip key={c} title={NOTIFY_CHANNELS[c].label}>
                  <span className="grid size-7 place-items-center rounded-md bg-primary-50 text-primary-600" aria-label={NOTIFY_CHANNELS[c].label}>
                    <Icon size={15} aria-hidden />
                  </span>
                </Tooltip>
              );
            })}
          </span>
        ) : (
          <span className="text-xs text-ink-muted">Mặc định SMS</span>
        ),
    },
    {
      key: 'account',
      title: 'Cổng phụ huynh',
      width: 160,
      mobile: 'aside',
      render: (_, r) =>
        r.account ? (
          <StatusTag group="account" status={r.account.status} size="sm" />
        ) : editable ? (
          <button
            type="button"
            onClick={(e) => {
              e.stopPropagation();
              open(r.id, 'account');
            }}
            className="text-[13px] font-medium text-primary-700 hover:underline"
          >
            Cấp tài khoản
          </button>
        ) : (
          <span className="text-ink-muted">Chưa cấp</span>
        ),
    },
  ];

  const editingLead = dialog?.kind === 'edit' ? leads.find((l) => l.id === dialog.leadId) : undefined;

  const submit = async (v: GuardianFormValues) => {
    if (USE_API) {
      const contact = { fullName: v.fullName.trim(), phone: v.phone.trim(), email: v.email?.trim() || undefined, notifyChannels: v.notifyChannels, note: v.note?.trim() || undefined };
      const r = editingLead
        ? await updateGuardian(editingLead.id, { ...contact, branchId: v.branchId ?? editingLead.branchId })
        : await createGuardian({ ...contact, branchId: v.branchId!, relationship: v.relationship ?? 'child', studentIds: v.studentIds ?? [], primary: v.primary });
      const lead = useCrmStore.getState().leads.find((l) => l.id === r.leadId);
      // ⏳ Tài khoản cổng phụ huynh (HV-13) còn ở dữ liệu giả
      if (!editingLead && lead && v.issueAccount && !accounts.some((a) => a.ownerType === 'guardian' && a.ownerId === lead.id))
        issueAccount({ ownerType: 'guardian', ownerId: lead.id, username: `ph.${lead.phone}`, email: lead.email ?? contact.email });
      if (r.reused && lead) notify.info({ title: 'Đã liên kết vào người liên hệ có sẵn', description: `${lead.fullName} (${lead.code}) trùng số điện thoại` });
      r.warnings.forEach((w) => notify.toast.warning(w));
      if (!editingLead) setTimeout(() => open(r.leadId), 300);
      return;
    }
    await new Promise((r) => setTimeout(r, 400)); // giả lập gọi API
    const contact = { fullName: v.fullName.trim(), phone: v.phone.trim(), email: v.email?.trim() || undefined, notifyChannels: v.notifyChannels, note: v.note?.trim() || undefined };
    if (editingLead) {
      updateLead(editingLead.id, contact);
      return;
    }
    const staff = user?.fullName ?? 'Giáo vụ';
    // SĐT đã có → liên kết vào người có sẵn, không tạo trùng
    const existing = findDuplicate(v.phone);
    const lead =
      existing ??
      addLead({ ...contact, source: 'walk_in', status: 'won', branchId: v.branchId!, note: contact.note ?? 'Tạo từ trang Phụ huynh / Người giám hộ' }, staff);
    if (existing) updateLead(existing.id, { notifyChannels: v.notifyChannels ?? existing.notifyChannels, email: existing.email ?? contact.email });
    for (const sid of v.studentIds ?? []) {
      linkStudent(lead.id, sid, v.relationship ?? 'child', staff);
      updateLink(lead.id, sid, { notify: true, ...(v.primary ? { primary: true, billing: true } : {}) });
    }
    if (v.issueAccount && !accounts.some((a) => a.ownerType === 'guardian' && a.ownerId === lead.id)) {
      issueAccount({ ownerType: 'guardian', ownerId: lead.id, username: `ph.${lead.phone}`, email: lead.email ?? contact.email });
    }
    if (existing) notify.info({ title: 'Đã liên kết vào người liên hệ có sẵn', description: `${existing.fullName} (${existing.code}) trùng số điện thoại` });
    setTimeout(() => open(lead.id), 300);
  };

  return (
    <>
      <PageHeader
        title="Phụ huynh / Người giám hộ"
        subtitle={`Người liên hệ của học viên — nhận thông báo, hóa đơn & dùng Cổng phụ huynh · ${currentBranch ? `Chi nhánh ${currentBranch.name}` : 'Toàn hệ thống'}`}
        breadcrumb={[{ title: 'Học viên' }, { title: 'Phụ huynh / Người giám hộ' }]}
        actions={
          <>
            <Button action="export" variant="outline" collapseOnMobile onClick={() => notify.toast.info('Xuất Excel (giả lập)')} />
            {editable && (
              <Button action="create" collapseOnMobile onClick={() => setDialog({ kind: 'create' })}>
                Thêm phụ huynh
              </Button>
            )}
          </>
        }
      />

      {/* Học viên thiếu người giám hộ / người liên hệ chính */}
      {gapCount > 0 && (
        <div className="mb-4 rounded-[var(--radius-card)] border border-warning-100 bg-warning-50 px-4 py-3 text-[13px] text-warning-700">
          <div className="flex flex-wrap items-center justify-between gap-2">
            <p className="flex items-center gap-2">
              <TriangleAlert size={16} className="shrink-0" aria-hidden />
              <span>
                {gaps.minorsWithout.length > 0 && (
                  <>
                    <strong>{gaps.minorsWithout.length}</strong> học viên dưới {ADULT_AGE} tuổi chưa có phụ huynh
                  </>
                )}
                {gaps.minorsWithout.length > 0 && gaps.noPrimary.length > 0 && ' · '}
                {gaps.noPrimary.length > 0 && (
                  <>
                    <strong>{gaps.noPrimary.length}</strong> học viên chưa có người liên hệ chính
                  </>
                )}
              </span>
            </p>
            <Button icon={showGaps ? ChevronUp : ChevronDown} size="sm" variant="ghost" tone="warning" onClick={() => setShowGaps((s) => !s)}>
              {showGaps ? 'Thu gọn' : 'Xem danh sách'}
            </Button>
          </div>
          {showGaps && (
            <ul className="mt-3 flex flex-col divide-y divide-warning-100 border-t border-warning-100">
              {[...gaps.minorsWithout.map((s) => ({ s, kind: 'minor' as const })), ...gaps.noPrimary.map((s) => ({ s, kind: 'primary' as const }))].map(({ s, kind }) => {
                const guardian = kind === 'primary' ? links.find((k) => k.studentId === s.id && k.relationship !== 'self') : undefined;
                return (
                  <li key={`${kind}-${s.id}`} className="flex flex-wrap items-center justify-between gap-2 py-2">
                    <button type="button" className="text-left text-ink hover:underline" onClick={() => navigate(`/admin/students/profiles?open=${s.id}`)}>
                      <span className="font-medium">{s.fullName}</span>{' '}
                      <span className="text-ink-muted">
                        · {s.code} · {ageOf(s.dob)} tuổi · {kind === 'minor' ? 'chưa có phụ huynh' : 'chưa chọn liên hệ chính'}
                      </span>
                    </button>
                    {editable &&
                      (kind === 'minor' ? (
                        <Button icon={UserRoundPlus} size="sm" variant="soft" onClick={() => setDialog({ kind: 'create', studentIds: [s.id] })}>
                          Thêm phụ huynh
                        </Button>
                      ) : (
                        guardian && (
                          <Button icon={Star} size="sm" variant="soft" onClick={() => open(guardian.leadId)}>
                            Chọn liên hệ chính
                          </Button>
                        )
                      ))}
                  </li>
                );
              })}
            </ul>
          )}
        </div>
      )}

      {/* Lọc nhanh theo tài khoản Cổng phụ huynh */}
      <div className="-mx-4 mb-4 flex gap-2 overflow-x-auto px-4 pb-1 [scrollbar-width:none] sm:mx-0 sm:flex-wrap sm:px-0 [&::-webkit-scrollbar]:hidden" role="tablist" aria-label="Lọc theo tài khoản Cổng phụ huynh">
        {ACCOUNT_TABS.map((o) => {
          const active = accountTab === o.value;
          return (
            <button
              key={o.value}
              type="button"
              role="tab"
              aria-selected={active}
              onClick={() => setAccountTab(o.value)}
              className={cn(
                'inline-flex h-9 shrink-0 items-center gap-2 rounded-full border px-3.5 text-sm font-medium transition-colors',
                active ? 'border-primary-600 bg-primary-600 text-white' : 'border-line bg-surface text-ink-secondary hover:border-primary-300 hover:bg-primary-50',
              )}
            >
              {o.label}
              <span className={cn('tabular rounded-full px-1.5 text-xs', active ? 'bg-white/20' : 'bg-disabled text-ink-muted')}>{counts[o.value]}</span>
            </button>
          );
        })}
      </div>

      <DataTable<GuardianRow>
        storageKey="students-guardians"
        columns={columns}
        data={data}
        rowKey="id"
        unitLabel="phụ huynh"
        searchKeys={['fullName', 'code', 'phone', 'email']}
        onRowClick={(r) => open(r.id)}
        search={{
          placeholder: 'Tìm theo tên, SĐT, email phụ huynh...',
          activeFilterCount: [role, tuition, study].filter(Boolean).length,
          filters: (
            <>
              <Select placeholder="Vai trò" value={role} onChange={setRole} options={ROLE_FILTER} showSearch={false} className="lg:!w-40" />
              <Select placeholder="Học phí" value={tuition} onChange={setTuition} options={TUITION_FILTER} showSearch={false} className="lg:!w-44" />
              <Select placeholder="Tình trạng học" value={study} onChange={setStudy} options={STUDY_FILTER} showSearch={false} className="lg:!w-52" />
            </>
          ),
          onReset: () => {
            setRole(undefined);
            setTuition(undefined);
            setStudy(undefined);
            setAccountTab('all');
          },
        }}
        rowActions={(r) => (
          <>
            <Button icon={Phone} iconOnly size="sm" tone="neutral" variant="soft" tooltip={`Gọi ${r.phone}`} aria-label={`Gọi ${r.phone}`} onClick={() => (window.location.href = `tel:${r.phone}`)} />
            {editable && <Button action="edit" iconOnly size="sm" onClick={() => setDialog({ kind: 'edit', leadId: r.id })} />}
            <Button icon={KeyRound} iconOnly size="sm" tone="neutral" variant="soft" tooltip="Tài khoản Cổng phụ huynh" aria-label="Tài khoản Cổng phụ huynh" onClick={() => open(r.id, 'account')} />
          </>
        )}
        actionsWidth={140}
        empty={{
          icon: ContactRound,
          title: 'Chưa có phụ huynh / người giám hộ',
          description: 'Thêm phụ huynh và liên kết với học viên, hoặc liên kết từ Tuyển sinh → Lead → tab Học viên.',
          action: editable ? <Button action="create" onClick={() => setDialog({ kind: 'create' })}>Thêm phụ huynh</Button> : undefined,
        }}
      />

      <DrawerForm<GuardianFormValues>
        key={dialog?.kind === 'edit' ? `edit-${dialog.leadId}` : `create-${dialog?.kind === 'create' ? (dialog.studentIds ?? []).join() : ''}`}
        open={Boolean(dialog)}
        onClose={() => setDialog(null)}
        width={680}
        columns={2}
        title={editingLead ? `Sửa ${editingLead.fullName}` : 'Thêm phụ huynh / người giám hộ'}
        description={editingLead ? editingLead.code : 'Tạo người liên hệ và liên kết với học viên. SĐT đã có sẽ được dùng lại.'}
        submitAction={editingLead ? 'save' : 'create'}
        submitText={editingLead ? 'Lưu' : 'Thêm phụ huynh'}
        successMessage={editingLead ? 'Đã lưu thông tin phụ huynh' : 'Đã thêm phụ huynh'}
        initialValues={
          editingLead
            ? { fullName: editingLead.fullName, phone: editingLead.phone, email: editingLead.email, notifyChannels: editingLead.notifyChannels, note: editingLead.note }
            : (() => {
                const first = dialog?.kind === 'create' && dialog.studentIds?.length ? students.find((s) => s.id === dialog.studentIds![0]) : undefined;
                return {
                  branchId: first?.branchId ?? currentBranch?.id ?? branches[0]?.id,
                  relationship: 'child',
                  studentIds: dialog?.kind === 'create' ? dialog.studentIds : undefined,
                  primary: true,
                  issueAccount: true,
                  notifyChannels: ['zalo', 'sms'],
                };
              })()
        }
        onSubmit={submit}
      >
        <GuardianFormFields mode={editingLead ? 'edit' : 'create'} editingId={editingLead?.id} />
      </DrawerForm>

      <GuardianDetailDrawer
        leadId={openId}
        initialTab={openTab}
        editable={editable}
        onClose={() => setOpenId(null)}
        onEdit={(id) => setDialog({ kind: 'edit', leadId: id })}
      />
    </>
  );
}
