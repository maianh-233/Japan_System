import { useEffect, useMemo, useState, type ReactNode } from 'react';
import { Drawer, Switch, Tabs, Tooltip } from 'antd';
import { History, Info, KeyRound, Link2, MessageSquareText, Phone, Star, Unlink, UserRound, Users } from 'lucide-react';
import { useNavigate } from 'react-router-dom';
import { Button, EmptyState, Select, StatusTag, UserCell, useConfirm, useNotify } from '@/components/common';
import { useClassStore } from '@/features/academic/classes/classStore';
import { CONSULT_CHANNELS, CONSULT_RESULTS, LEAD_SOURCES } from '@/features/crm/constants';
import { useCrmStore } from '@/features/crm/crmStore';
import { useAuth } from '@/hooks/useAuth';
import { useBreakpoint } from '@/hooks/useBreakpoint';
import { fmtDate, fmtDateTime } from '@/lib/date';
import { formatCurrency } from '@/lib/format';
import { getBranches } from '@/features/system/useBranches';
import { AccountCard } from '../components/AccountCard';
import { GUARDIAN_ROLE, STUDY_STATUS } from '../constants';
import { useStudentStore } from '../studentStore';
import { buildChildInfo, GUARDIAN_ROLE_OPTIONS, NOTIFY_CHANNELS, type GuardianRelationship } from './guardianUtils';
import { usePortalAccountActions } from '../portalAccountApi';
import { currentFlags, linkGuardianStudent, unlinkGuardianStudent, updateGuardianLink, type LinkFlags } from './guardianApi';
import { USE_API } from '@/lib/api/config';
import { ApiError } from '@/lib/api/http';

export type GuardianTab = 'children' | 'info' | 'account' | 'history';

interface Props {
  leadId: string | null;
  initialTab?: GuardianTab;
  editable: boolean;
  onClose: () => void;
  onEdit: (leadId: string) => void;
}

const label = (Icon: typeof Info, text: string) => (
  <span className="inline-flex items-center gap-1.5">
    <Icon size={16} aria-hidden /> {text}
  </span>
);

/** Chi tiết phụ huynh: Con (vai trò với từng con) · Thông tin liên hệ · Tài khoản Cổng phụ huynh · Lịch sử liên hệ */
export function GuardianDetailDrawer({ leadId, initialTab = 'children', editable, onClose, onEdit }: Props) {
  const { isMobile } = useBreakpoint();
  const { user } = useAuth();
  const notify = useNotify();
  const navigate = useNavigate();
  const { confirm, confirmDialog } = useConfirm();

  const lead = useCrmStore((s) => s.leads.find((l) => l.id === leadId));
  const links = useCrmStore((s) => s.links);
  const students = useCrmStore((s) => s.students);
  const consultations = useCrmStore((s) => s.consultations);
  const updateLinkLocal = useCrmStore((s) => s.updateLink);
  const linkStudent = useCrmStore((s) => s.linkStudent);
  const unlinkStudent = useCrmStore((s) => s.unlinkStudent);
  /** Đổi cờ / quan hệ của 1 liên kết — chế độ API gọi server (giữ các cờ khác), báo cảnh báo; lỗi → false */
  const updateLink = async (leadId: string, studentId: string, patch: Partial<LinkFlags>) => {
    if (!USE_API) {
      updateLinkLocal(leadId, studentId, patch);
      return true;
    }
    const cur = currentFlags(leadId, studentId);
    if (!cur) return false;
    try {
      (await updateGuardianLink(leadId, studentId, { ...cur, ...patch })).forEach((w) => notify.toast.warning(w));
      return true;
    } catch (e) {
      notify.toast.error(e instanceof ApiError ? e.message : 'Không lưu được thay đổi');
      return false;
    }
  };
  const enrollments = useStudentStore((s) => s.enrollments);
  const invoices = useStudentStore((s) => s.invoices);
  const accounts = useStudentStore((s) => s.accounts);
  const { issue: issueAccount } = usePortalAccountActions();
  const classes = useClassStore((s) => s.classes);

  const [tab, setTab] = useState<GuardianTab>(initialTab);
  const [addId, setAddId] = useState<string>();
  const [addRole, setAddRole] = useState<GuardianRelationship>('child');

  useEffect(() => {
    if (leadId) {
      setTab(initialTab);
      setAddId(undefined);
    }
  }, [leadId, initialTab]);

  const children = useMemo(() => {
    const info = buildChildInfo({ enrollments, invoices, classes });
    return links
      .filter((k) => k.leadId === leadId && k.relationship !== 'self')
      .map((k) => {
        const st = students.find((s) => s.id === k.studentId);
        return st ? info(st, k) : null;
      })
      .filter((c): c is NonNullable<typeof c> => Boolean(c));
  }, [links, leadId, students, enrollments, invoices, classes]);

  /** Người liên hệ chính hiện tại của từng con (nếu là người khác) */
  const otherPrimary = (studentId: string) => {
    const k = links.find((x) => x.studentId === studentId && x.primary && x.leadId !== leadId);
    return k && useCrmStore.getState().leads.find((l) => l.id === k.leadId);
  };

  const account = accounts.find((a) => a.ownerType === 'guardian' && a.ownerId === leadId);
  const history = consultations.filter((c) => c.leadId === leadId);

  if (!lead) return <Drawer open={false} onClose={onClose} />;

  const staff = user?.fullName ?? 'Giáo vụ';

  const setPrimary = async (studentId: string, name: string, on: boolean) => {
    if (on) {
      const other = otherPrimary(studentId);
      if (other) {
        const ok = await confirm({
          title: `Đổi người liên hệ chính của ${name}?`,
          description: `Hiện là ${other.fullName} (${other.phone}). Giáo vụ sẽ gọi ${lead.fullName} trước khi học viên vắng / có vấn đề.`,
          tone: 'primary',
          confirmAction: 'save',
          confirmText: 'Đổi người liên hệ chính',
        });
        if (!ok) return;
      }
    }
    if (!(await updateLink(lead.id, studentId, { primary: on, ...(USE_API && on ? { billing: true } : {}) }))) return;
    notify.toast.success(on ? `${lead.fullName} là người liên hệ chính của ${name}` : 'Đã bỏ người liên hệ chính');
  };

  const unlink = async (studentId: string, name: string, primary?: boolean) => {
    const ok = await confirm({
      title: `Gỡ liên kết ${lead.fullName} khỏi ${name}?`,
      description: 'Hồ sơ phụ huynh và học viên vẫn giữ nguyên, chỉ bỏ quan hệ giữa hai người.',
      consequences: [
        'Phụ huynh không còn xem lịch học, chuyên cần, học phí của học viên này trên Cổng phụ huynh',
        'Ngừng gửi thông báo / hóa đơn của học viên này cho phụ huynh',
        ...(primary ? [`${name} sẽ chưa có người liên hệ chính`] : []),
      ],
      tone: 'danger',
      confirmAction: 'delete',
      confirmText: 'Gỡ liên kết',
    });
    if (!ok) return;
    if (USE_API) {
      try {
        (await unlinkGuardianStudent(lead.id, studentId)).forEach((w) => notify.toast.warning(w));
      } catch (e) {
        return notify.toast.error(e instanceof ApiError ? e.message : 'Không gỡ được liên kết');
      }
    } else unlinkStudent(lead.id, studentId);
    notify.success({ title: 'Đã gỡ liên kết', description: `${lead.fullName} — ${name}` });
  };

  const addChild = async () => {
    if (!addId) return;
    const hasPrimary = links.some((k) => k.studentId === addId && k.primary);
    if (USE_API) {
      try {
        const warnings = await linkGuardianStudent(lead.id, addId, { relationship: addRole, primary: !hasPrimary, billing: !hasPrimary, notify: true });
        warnings.forEach((w) => notify.toast.warning(w));
      } catch (e) {
        return notify.toast.error(e instanceof ApiError ? e.message : 'Không liên kết được học viên');
      }
      notify.success({ title: 'Đã liên kết học viên', description: students.find((s) => s.id === addId)?.fullName });
      setAddId(undefined);
      return;
    }
    linkStudent(lead.id, addId, addRole, staff);
    updateLinkLocal(lead.id, addId, { notify: true, ...(hasPrimary ? {} : { primary: true, billing: true }) });
    notify.success({ title: 'Đã liên kết học viên', description: students.find((s) => s.id === addId)?.fullName });
    setAddId(undefined);
  };

  const linkedIds = new Set(children.map((c) => c.student.id));
  const addOptions = students
    .filter((s) => !s.hidden && !linkedIds.has(s.id) && s.branchId === lead.branchId)
    .map((s) => ({ value: s.id, label: `${s.fullName} · ${s.code}` }));

  const info: [string, ReactNode][] = [
    ['Điện thoại', <a href={`tel:${lead.phone}`} className="font-medium text-primary-700 hover:underline">{lead.phone}</a>],
    ['Email', lead.email ? <a href={`mailto:${lead.email}`} className="text-primary-700 hover:underline">{lead.email}</a> : '—'],
    [
      'Kênh nhận thông báo',
      lead.notifyChannels?.length ? (
        <span className="flex flex-wrap gap-1">
          {lead.notifyChannels.map((c) => (
            <StatusTag key={c} tone="info" icon={NOTIFY_CHANNELS[c].icon} size="sm">
              {NOTIFY_CHANNELS[c].label}
            </StatusTag>
          ))}
        </span>
      ) : (
        <span className="text-warning-700">Chưa chọn — mặc định gửi SMS</span>
      ),
    ],
    ['Chi nhánh', getBranches(false).find((b) => b.id === lead.branchId)?.name],
    ['Mã người liên hệ', <span className="font-mono">{lead.code}</span>],
    ['Nguồn', LEAD_SOURCES[lead.source].label],
    ['Ngày tạo', `${fmtDate(lead.createdAt)} · ${lead.createdBy}`],
    ['Ghi chú', lead.note ?? '—'],
  ];

  return (
    <Drawer
      open={Boolean(leadId)}
      onClose={onClose}
      width={isMobile ? '100%' : 780}
      destroyOnHidden
      styles={{ body: { paddingTop: 0 } }}
      title={
        <div className="flex min-w-0 flex-col gap-1 pr-2">
          <div className="flex flex-wrap items-center gap-2">
            <span className="truncate text-base font-semibold">{lead.fullName}</span>
            {account ? <StatusTag group="account" status={account.status} size="sm" /> : <StatusTag tone="neutral" icon={KeyRound} size="sm">Chưa có tài khoản</StatusTag>}
          </div>
          <span className="text-xs font-normal text-ink-muted">
            {lead.phone} · {children.length} học viên liên kết
          </span>
        </div>
      }
      extra={
        <div className="flex gap-1.5">
          <Button icon={Phone} size="sm" tone="neutral" variant="outline" collapseOnMobile onClick={() => (window.location.href = `tel:${lead.phone}`)}>
            Gọi
          </Button>
          {editable && <Button action="edit" size="sm" collapseOnMobile onClick={() => onEdit(lead.id)} />}
        </div>
      }
    >
      <Tabs
        activeKey={tab}
        onChange={(k) => setTab(k as GuardianTab)}
        items={[
          {
            key: 'children',
            label: label(Users, `Học viên (${children.length})`),
            children: (
              <div className="flex flex-col gap-3">
                {children.length === 0 && (
                  <div className="rounded-[var(--radius-card)] border border-line">
                    <EmptyState size="compact" icon={Users} title="Chưa liên kết học viên nào" description="Chọn học viên bên dưới để liên kết." />
                  </div>
                )}
                {children.map((c) => {
                  const st = STUDY_STATUS[c.study];
                  const role = GUARDIAN_ROLE[c.link.relationship as GuardianRelationship];
                  const other = !c.link.primary ? otherPrimary(c.student.id) : undefined;
                  return (
                    <div key={c.student.id} className="flex flex-col gap-3 rounded-[var(--radius-card)] border border-line bg-surface p-4">
                      <div className="flex flex-wrap items-start justify-between gap-2">
                        <button type="button" className="min-w-0 text-left" onClick={() => navigate(`/admin/students/profiles?open=${c.student.id}`)}>
                          <UserCell name={c.student.fullName} subtitle={`${c.student.code}${c.age !== undefined ? ` · ${c.age} tuổi` : ''}`} />
                        </button>
                        <span className="flex flex-wrap gap-1">
                          <StatusTag tone="neutral" icon={UserRound} size="sm">
                            {role}
                          </StatusTag>
                          {c.link.primary && (
                            <StatusTag tone="primary" icon={Star} size="sm">
                              Liên hệ chính
                            </StatusTag>
                          )}
                          <StatusTag tone={st.tone} icon={st.icon} size="sm">
                            {st.label}
                          </StatusTag>
                        </span>
                      </div>

                      <dl className="grid grid-cols-1 gap-x-4 gap-y-1.5 text-[13px] sm:grid-cols-[120px_1fr]">
                        <dt className="text-ink-muted">Lớp đang học</dt>
                        <dd className="text-ink">{c.classes.length ? c.classes.join(', ') : '—'}</dd>
                        <dt className="text-ink-muted">Học phí</dt>
                        <dd className="flex flex-wrap items-center gap-2 text-ink">
                          {c.tuitionStatus ? <StatusTag group="invoice" status={c.tuitionStatus} size="sm" /> : '—'}
                          {c.debt > 0 && <span className="tabular font-medium text-danger-600">Còn nợ {formatCurrency(c.debt)}</span>}
                        </dd>
                      </dl>

                      <div className="flex flex-col gap-2.5 border-t border-line pt-3 text-[13px]">
                        <div className="flex flex-wrap items-center gap-x-5 gap-y-2">
                          <Toggle
                            checked={Boolean(c.link.primary)}
                            disabled={!editable}
                            onChange={(v) => setPrimary(c.student.id, c.student.fullName, v)}
                            label="Liên hệ chính"
                            hint={other ? `Hiện là ${other.fullName}` : 'Giáo vụ gọi người này trước'}
                          />
                          <Toggle
                            checked={Boolean(c.link.billing)}
                            disabled={!editable}
                            onChange={(v) => updateLink(lead.id, c.student.id, { billing: v })}
                            label="Nhận hóa đơn"
                            hint="Hóa đơn, nhắc học phí"
                          />
                          <Toggle
                            checked={Boolean(c.link.notify)}
                            disabled={!editable}
                            onChange={(v) => updateLink(lead.id, c.student.id, { notify: v })}
                            label="Nhận thông báo"
                            hint="Điểm danh, kết quả học"
                          />
                        </div>
                        {editable && (
                          <div className="flex flex-wrap items-center justify-between gap-2">
                            <div className="flex items-center gap-2">
                              <span className="text-ink-muted">Vai trò</span>
                              <Select
                                size="small"
                                value={c.link.relationship as GuardianRelationship}
                                options={GUARDIAN_ROLE_OPTIONS}
                                showSearch={false}
                                allowClear={false}
                                onChange={(v) => updateLink(lead.id, c.student.id, { relationship: v as GuardianRelationship })}
                                className="!w-44"
                              />
                            </div>
                            <Button icon={Unlink} size="sm" tone="danger" variant="ghost" onClick={() => unlink(c.student.id, c.student.fullName, c.link.primary)}>
                              Gỡ liên kết
                            </Button>
                          </div>
                        )}
                      </div>
                    </div>
                  );
                })}

                {editable && (
                  <div className="flex flex-col gap-2 rounded-[var(--radius-card)] border border-dashed border-line bg-page p-3 sm:flex-row sm:items-center">
                    <Select
                      placeholder="Liên kết thêm học viên (anh chị em)..."
                      value={addId}
                      onChange={setAddId}
                      options={addOptions}
                      optionFilterProp="label"
                      className="min-w-0 flex-1"
                    />
                    <Select value={addRole} onChange={(v) => setAddRole(v as GuardianRelationship)} options={GUARDIAN_ROLE_OPTIONS} showSearch={false} allowClear={false} className="sm:!w-44" />
                    <Button icon={Link2} disabled={!addId} onClick={addChild}>
                      Liên kết
                    </Button>
                  </div>
                )}
              </div>
            ),
          },
          {
            key: 'info',
            label: label(Info, 'Thông tin'),
            children: (
              <dl className="grid grid-cols-1 gap-x-4 gap-y-2.5 text-sm sm:grid-cols-[160px_1fr]">
                {info.map(([k, v]) => (
                  <div key={k} className="contents">
                    <dt className="text-ink-muted">{k}</dt>
                    <dd className="min-w-0 break-words text-ink">{v}</dd>
                  </div>
                ))}
              </dl>
            ),
          },
          {
            key: 'account',
            label: label(KeyRound, 'Tài khoản'),
            children: (
              <AccountCard
                account={account}
                ownerName={lead.fullName}
                portalLabel="Cổng phụ huynh"
                emptyHint="Chưa có tài khoản Cổng phụ huynh (xem lịch học, chuyên cần, kết quả và học phí của con)."
                onIssue={
                  editable
                    ? () => void issueAccount({ ownerType: 'guardian', ownerId: lead.id, username: `ph.${lead.phone}`, email: lead.email, ownerName: lead.fullName })
                    : undefined
                }
              />
            ),
          },
          {
            key: 'history',
            label: label(History, `Lịch sử liên hệ (${history.length})`),
            children: history.length ? (
              <ol className="flex flex-col gap-2.5">
                {history.map((h) => {
                  const ch = CONSULT_CHANNELS[h.channel];
                  const rs = CONSULT_RESULTS[h.result];
                  return (
                    <li key={h.id} className="flex gap-3 rounded-[var(--radius-card)] border border-line bg-surface p-3">
                      <span className="grid size-8 shrink-0 place-items-center rounded-full bg-primary-50 text-primary-600">
                        <ch.icon size={16} aria-hidden />
                      </span>
                      <div className="min-w-0 flex-1">
                        <div className="flex flex-wrap items-center justify-between gap-2">
                          <span className="text-[13px] font-medium text-ink">
                            {ch.label} · {h.staffName}
                          </span>
                          <StatusTag tone={rs.tone} icon={MessageSquareText} size="sm">
                            {rs.label}
                          </StatusTag>
                        </div>
                        <p className="mt-1 text-[13px] text-ink-secondary">{h.content}</p>
                        <p className="mt-1 text-xs text-ink-muted">{fmtDateTime(h.createdAt)}</p>
                      </div>
                    </li>
                  );
                })}
              </ol>
            ) : (
              <EmptyState size="compact" icon={History} title="Chưa có lịch sử liên hệ" />
            ),
          },
        ]}
      />
      {confirmDialog}
    </Drawer>
  );
}

function Toggle({ checked, disabled, onChange, label: text, hint }: { checked: boolean; disabled?: boolean; onChange: (v: boolean) => void; label: string; hint: string }) {
  return (
    <Tooltip title={hint}>
      <label className="inline-flex cursor-pointer items-center gap-2">
        <Switch size="small" checked={checked} disabled={disabled} onChange={onChange} />
        <span className="text-ink">{text}</span>
      </label>
    </Tooltip>
  );
}
