import { useMemo, useState } from 'react';
import { Drawer } from 'antd';
import dayjs from 'dayjs';
import { ArrowLeftRight, CalendarCheck, CalendarX2, CheckCircle2, Clock3, Eye, Inbox, UserRound, XCircle } from 'lucide-react';
import { Button, DataTable, PageHeader, Select, StatCard, StatusTag, UserCell, useConfirm, useNotify, type DataColumn, type Tone } from '@/components/common';
import { useClassStore } from '@/features/academic/classes/classStore';
import { teacherName } from '@/features/academic/classes/constants';
import { notStarted } from '@/features/academic/classes/sessionUtils';
import type { ClassSession } from '@/features/academic/classes/types';
import { REQUEST_TYPES } from '@/features/hr/constants';
import { useHrStore } from '@/features/hr/hrStore';
import type { HrRequest } from '@/features/hr/types';
import { HandleCancelModal } from '@/features/inbox/HandleCancelModal';
import { INBOX_KIND, sortPending, useInbox, type InboxItem, type InboxKind } from '@/features/inbox/useInbox';
import { usePortalStore } from '@/features/portal/portalStore';
import type { SubInvite } from '@/features/portal/types';
import { shortCode } from '@/features/portal/usePortal';
import { useAuth } from '@/hooks/useAuth';
import { useBreakpoint } from '@/hooks/useBreakpoint';
import { cn } from '@/lib/cn';
import { fmtDateTime, fromNow } from '@/lib/date';

type Tab = 'pending' | 'history' | 'all';
type Decision = 'approved' | 'rejected';
type Result = { label: string; tone: Tone };

const kindOptions = (Object.keys(INBOX_KIND) as InboxKind[]).map((value) => ({ value, label: INBOX_KIND[value] }));
const statusMeta = {
  pending: { label: 'Chờ duyệt', tone: 'warning' as const, icon: Clock3 },
  approved: { label: 'Đã duyệt', tone: 'success' as const, icon: CheckCircle2 },
  rejected: { label: 'Từ chối', tone: 'danger' as const, icon: XCircle },
  cancelled: { label: 'Đã hủy', tone: 'neutral' as const, icon: XCircle },
};
const RESULT_TEXT: Record<Tone, string> = {
  primary: 'text-primary-700',
  info: 'text-primary-700',
  success: 'text-success-700',
  warning: 'text-warning-700',
  danger: 'text-danger-700',
  neutral: 'text-ink-muted',
};
const INVITE_STATUS: Record<SubInvite['status'], { label: string; tone: Tone }> = {
  pending: { label: 'Chờ trả lời', tone: 'warning' },
  accepted: { label: 'Đã nhận dạy', tone: 'success' },
  declined: { label: 'Đã từ chối', tone: 'neutral' },
  expired: { label: 'Hết hiệu lực', tone: 'neutral' },
};

function requestTitle(item: InboxItem) {
  return item.kind === 'hr' ? REQUEST_TYPES[item.req.type].label : INBOX_KIND[item.kind];
}

function sessionLabel(item: InboxItem) {
  if (!('session' in item)) return undefined;
  if (!item.session) return 'Không tìm thấy buổi học';
  const cls = item.cls?.code.replace(/^GV[VN]*_/, '') ?? item.session.classId;
  return `${cls} · buổi ${item.session.no} · ${dayjs(item.session.date).format('dd DD/MM')} ${item.session.startTime}–${item.session.endTime}`;
}

function statusLabel(item: InboxItem) {
  if (item.kind === 'hr' && item.status === 'approved' && item.req.status === 'pending') return 'Đã duyệt bước';
  return statusMeta[item.status].label;
}

function RequestStatus({ item }: { item: InboxItem }) {
  const meta = statusMeta[item.status];
  return <StatusTag tone={meta.tone} icon={meta.icon} size="sm">{statusLabel(item)}</StatusTag>;
}

function hrSummary(req: HrRequest) {
  const span = req.from === req.to ? dayjs(req.from).format('DD/MM/YYYY') : `${dayjs(req.from).format('DD/MM')}–${dayjs(req.to).format('DD/MM/YYYY')}`;
  return `${req.code} · ${span}${req.amount ? ` · ${req.amount.toLocaleString('vi-VN')} đ` : ''}`;
}

/**
 * Kết quả xử lý 1 đơn — với đơn hủy buổi là 1 trong 2 cách dạy thay (modules.md mục 4):
 *  mời GV dạy thay (giữ lịch, ai nhận trước thì dạy) hoặc hủy buổi & xếp học bù.
 */
function substituteResult(item: Extract<InboxItem, { kind: 'cancel' }>, invites: SubInvite[], sessions: ClassSession[]): Result {
  const list = invites.filter((i) => i.sessionId === item.req.sessionId);
  const accepted = list.find((i) => i.status === 'accepted');
  if (accepted) return { label: `${teacherName(accepted.toTeacherId)} đã nhận dạy thay`, tone: 'success' };
  if (list.some((i) => i.status === 'pending')) return { label: 'Đang chờ GV trả lời lời mời', tone: 'warning' };
  const s = item.session;
  if (s?.status === 'cancelled') {
    const makeup = sessions.find((x) => x.makeupForId === s.id && x.status !== 'cancelled');
    return makeup ? { label: `Đã hủy buổi · bù ${dayjs(makeup.date).format('dd DD/MM')}`, tone: 'info' } : { label: 'Đã hủy buổi — chưa xếp học bù', tone: 'warning' };
  }
  return s && notStarted(s) ? { label: 'Chưa có người dạy thay', tone: 'danger' } : { label: '—', tone: 'neutral' };
}

function resultOf(item: InboxItem, invites: SubInvite[], sessions: ClassSession[]): Result {
  if (item.status === 'pending') {
    return item.kind === 'hr' && item.step ? { label: `Bước ${item.step.index}/${item.step.total} — chờ ${item.step.role}`, tone: 'info' } : { label: '—', tone: 'neutral' };
  }
  if (item.kind === 'cancel') return substituteResult(item, invites, sessions);
  if (item.kind === 'leave') {
    const lr = item.req;
    if (lr.makeupResult === 'present' || lr.makeupResult === 'late') return { label: `Đã học bù · ${lr.makeupResult === 'late' ? 'đi muộn' : 'có mặt'}`, tone: 'success' };
    if (lr.makeupSessionId) return { label: `Đã đăng ký học bù${item.makeupCls ? ` — ${shortCode(item.makeupCls)}` : ''}`, tone: 'info' };
    return { label: 'Vắng có phép', tone: 'neutral' };
  }
  if (item.req.status === 'pending') return { label: 'Chờ cấp duyệt tiếp theo', tone: 'info' };
  return { label: '—', tone: 'neutral' };
}

/** Đơn hủy đã duyệt mà buổi sắp diễn ra vẫn chưa ai nhận dạy → còn phải mời dạy thay / xếp bù */
function followUpOf(item: InboxItem, invites: SubInvite[]): boolean {
  return (
    item.kind === 'cancel' &&
    item.status === 'approved' &&
    Boolean(item.session && notStarted(item.session) && item.session.status !== 'cancelled') &&
    !invites.some((i) => i.sessionId === item.req.sessionId && (i.status === 'accepted' || i.status === 'pending'))
  );
}

export default function RequestsPage() {
  const { user } = useAuth();
  const { isMobile } = useBreakpoint();
  const notify = useNotify();
  const { confirm, confirmDialog } = useConfirm();
  const inbox = useInbox();
  const employees = useHrStore((s) => s.employees);
  const subInvites = usePortalStore((s) => s.subInvites);
  const sessions = useClassStore((s) => s.sessions);
  const [tab, setTab] = useState<Tab>('pending');
  const [kind, setKind] = useState<InboxKind>();
  const [selected, setSelected] = useState<InboxItem>();
  const [cancelItem, setCancelItem] = useState<Extract<InboxItem, { kind: 'cancel' }> | null>(null);

  const counts = useMemo(() => {
    const pending = inbox.filter((i) => i.status === 'pending');
    return {
      pending: pending.length,
      byKind: {
        cancel: pending.filter((i) => i.kind === 'cancel').length,
        leave: pending.filter((i) => i.kind === 'leave').length,
        hr: pending.filter((i) => i.kind === 'hr').length,
      } satisfies Record<InboxKind, number>,
      history: inbox.filter((i) => i.status !== 'pending').length,
      total: inbox.length,
    };
  }, [inbox]);

  const rows = useMemo(() => {
    const filtered = inbox.filter((item) => !kind || item.kind === kind);
    if (tab === 'pending') return sortPending(filtered);
    if (tab === 'history') return filtered.filter((item) => item.status !== 'pending').sort((a, b) => (b.decidedAt ?? b.at).localeCompare(a.decidedAt ?? a.at));
    const pending = sortPending(filtered);
    const history = filtered.filter((item) => item.status !== 'pending').sort((a, b) => (b.decidedAt ?? b.at).localeCompare(a.decidedAt ?? a.at));
    return [...pending, ...history];
  }, [inbox, kind, tab]);

  const decide = async (item: InboxItem, decision: Decision) => {
    if (item.kind === 'cancel' && decision === 'approved') {
      if (!item.session || !item.cls) {
        notify.error({ title: 'Không thể xử lý đơn', description: 'Buổi học hoặc lớp học liên kết không còn tồn tại.' });
        return;
      }
      // Duyệt đơn NGAY: chốt đơn + đánh dấu buổi "đang tìm người dạy thay" → mở luôn bước chọn cách dạy thay
      if (notStarted(item.session)) {
        const by = user?.fullName ?? 'Giáo vụ';
        usePortalStore.getState().decideCancelRequest(item.id, true, by, 'Đã duyệt — đang chọn cách dạy thay / học bù');
        useClassStore.getState().updateSession(item.session.id, {
          note: `${teacherName(item.req.teacherId)} xin nghỉ (${item.req.reasonType}) — giáo vụ đang tìm người dạy thay / xếp học bù`,
        });
        notify.success({ title: 'Đã duyệt đơn', description: 'Buổi đã được đánh dấu. Chọn mời GV dạy thay hoặc hủy buổi & xếp học bù bên dưới.' });
      }
      setSelected(undefined);
      setCancelItem(item);
      return;
    }

    const isApproval = decision === 'approved';
    const result = await confirm({
      title: isApproval ? `Duyệt ${requestTitle(item).toLowerCase()} của ${item.who}?` : `Từ chối đơn của ${item.who}?`,
      description: item.kind === 'hr' && isApproval && (item.step?.index ?? 0) < (item.step?.total ?? 0)
        ? 'Đơn sẽ chuyển tiếp đến cấp duyệt tiếp theo.'
        : item.kind === 'leave' && isApproval
          ? 'Buổi học sẽ được ghi nhận vắng có phép theo đơn.'
          : undefined,
      tone: isApproval ? 'success' : 'danger',
      confirmAction: isApproval ? 'approve' : 'reject',
      confirmText: isApproval ? 'Duyệt đơn' : 'Từ chối đơn',
      ...(!isApproval ? { requireReason: { label: 'Lý do từ chối', placeholder: 'Nhập lý do để người gửi nắm được kết quả' } } : {}),
    });
    if (!result) return;

    const by = user?.fullName ?? 'Giáo vụ';
    const note = typeof result === 'object' ? result.reason : undefined;
    if (item.kind === 'leave') {
      usePortalStore.getState().decideLeaveRequest(item.id, isApproval, by, note);
    } else if (item.kind === 'cancel') {
      usePortalStore.getState().decideCancelRequest(item.id, false, by, note);
    } else {
      useHrStore.getState().decide(item.id, by, decision, note);
    }
    notify.toast.success(isApproval ? 'Đã duyệt đơn' : 'Đã từ chối đơn');
    setSelected(undefined);
  };

  const columns: DataColumn<InboxItem>[] = [
    {
      key: 'kind',
      title: 'Loại đơn',
      width: 190,
      mobile: 'title',
      render: (_, item) => {
        const Icon = item.kind === 'cancel' ? CalendarX2 : item.kind === 'leave' ? CalendarCheck : REQUEST_TYPES[item.req.type].icon;
        return (
          <div className="flex items-center gap-2">
            <span className="grid size-8 shrink-0 place-items-center rounded-lg bg-primary-50 text-primary-600"><Icon size={16} aria-hidden /></span>
            <span className="font-medium text-ink">{requestTitle(item)}</span>
          </div>
        );
      },
    },
    { key: 'who', title: 'Người gửi', width: 190, mobile: 'subtitle', render: (_, item) => <UserCell size="sm" name={item.who} subtitle={item.kind === 'hr' ? employees.find((employee) => employee.id === item.req.employeeId)?.position : item.kind === 'leave' ? (item.req.by === 'parent' ? 'Phụ huynh' : 'Học viên') : item.cls?.code} /> },
    {
      key: 'detail',
      title: 'Thông tin',
      width: 280,
      render: (_, item) => (
        <div className="min-w-0 text-[13px] leading-5">
          <p className="line-clamp-1 text-ink">{item.kind === 'hr' ? item.req.title ?? item.req.reason : `${item.req.reasonType}: ${item.req.reason}`}</p>
          <p className="truncate text-xs text-ink-muted">{item.kind === 'hr' ? hrSummary(item.req) : sessionLabel(item) ?? '—'}</p>
        </div>
      ),
    },
    ...(tab !== 'pending'
      ? ([{
          key: 'result',
          title: 'Kết quả xử lý',
          width: 230,
          render: (_, item) => {
            const r = resultOf(item, subInvites, sessions);
            return <span className={cn('text-[13px] font-medium', RESULT_TEXT[r.tone])}>{r.label}</span>;
          },
        }] satisfies DataColumn<InboxItem>[])
      : []),
    { key: 'at', title: 'Thời gian gửi', width: 150, render: (_, item) => <span className="text-[13px] text-ink-secondary" title={fmtDateTime(item.at)}>{fromNow(item.at)}</span> },
    { key: 'status', title: 'Trạng thái', width: 140, mobile: 'aside', render: (_, item) => <RequestStatus item={item} /> },
  ];

  const openRequest = (item: InboxItem) => setSelected(item);
  const request = selected;
  const sessionInvites = request?.kind === 'cancel' ? subInvites.filter((i) => i.sessionId === request.req.sessionId) : [];

  return (
    <>
      <PageHeader
        title="Đơn chờ xử lý"
        subtitle="Đơn hủy buổi (GV) · đơn xin nghỉ (HV/PH) · đơn từ nhân viên — gom một nơi kèm lịch sử. Duyệt đơn hủy buổi sẽ đánh dấu buổi và mở ngay bước mời GV dạy thay hoặc hủy & xếp học bù."
        breadcrumb={[{ title: 'Đào tạo' }, { title: 'Đơn chờ xử lý' }]}
      />

      <div className="mb-4 grid grid-cols-1 gap-3 sm:grid-cols-2 xl:grid-cols-4">
        <StatCard label="Chờ xử lý" value={counts.pending} icon={Inbox} tone={counts.pending ? 'warning' : 'success'} onClick={() => { setTab('pending'); setKind(undefined); }} />
        <StatCard label="GV xin hủy buổi" value={counts.byKind.cancel} icon={CalendarX2} tone="warning" hint="Duyệt → mời GV dạy thay hoặc xếp học bù" onClick={() => { setTab('pending'); setKind('cancel'); }} />
        <StatCard label="HV / PH xin nghỉ" value={counts.byKind.leave} icon={CalendarCheck} tone="info" onClick={() => { setTab('pending'); setKind('leave'); }} />
        <StatCard label="Đơn từ nhân viên" value={counts.byKind.hr} icon={UserRound} onClick={() => { setTab('pending'); setKind('hr'); }} />
      </div>

      <div className="mb-3 flex flex-wrap items-center justify-between gap-2">
        <div className="flex flex-wrap gap-1.5">
          {([
            ['pending', 'Chờ xử lý', counts.pending],
            ['history', 'Lịch sử', counts.history],
            ['all', 'Tất cả', counts.total],
          ] as [Tab, string, number][]).map(([key, label, count]) => (
            <button key={key} type="button" onClick={() => setTab(key)} className={cn('inline-flex h-9 items-center gap-2 rounded-full border px-3.5 text-sm font-medium', tab === key ? 'border-primary-600 bg-primary-600 text-white' : 'border-line bg-surface text-ink-secondary hover:bg-primary-50')}>
              {label}<span className={cn('tabular rounded-full px-1.5 text-xs', tab === key ? 'bg-white/20' : 'bg-disabled text-ink-muted')}>{count}</span>
            </button>
          ))}
        </div>
        <Select aria-label="Lọc loại đơn" placeholder="Tất cả loại đơn" value={kind} onChange={(value) => setKind(value)} options={kindOptions} allowClear className="w-full sm:!w-56" />
      </div>

      <DataTable<InboxItem>
        storageKey="workflow-requests"
        columns={columns}
        data={rows}
        rowKey={(item) => `${item.kind}-${item.id}`}
        searchKeys={['id', 'who', 'note']}
        search={{ placeholder: 'Tìm theo người gửi, mã đơn, ghi chú...' }}
        onRowClick={openRequest}
        rowActions={(item) => (
          <div className="flex items-center gap-1">
            <Button icon={Eye} iconOnly size="sm" tone="neutral" variant="ghost" tooltip="Xem chi tiết" aria-label="Xem chi tiết" onClick={() => openRequest(item)} />
            {item.kind === 'cancel' && item.status === 'approved' && followUpOf(item, subInvites) && (
              <Button icon={ArrowLeftRight} iconOnly size="sm" tone="primary" variant="soft" tooltip="Mời dạy thay / xếp học bù" aria-label="Mời dạy thay" onClick={() => setCancelItem(item)} />
            )}
            {item.canDecide && <Button icon={CheckCircle2} iconOnly size="sm" tone="success" variant="soft" tooltip={item.kind === 'cancel' ? 'Duyệt & mời dạy thay' : 'Duyệt'} aria-label="Duyệt" onClick={() => void decide(item, 'approved')} />}
            {item.canDecide && <Button icon={XCircle} iconOnly size="sm" tone="danger" variant="soft" tooltip="Từ chối" aria-label="Từ chối" onClick={() => void decide(item, 'rejected')} />}
          </div>
        )}
        actionsWidth={125}
        unitLabel="đơn"
        empty={{ icon: Inbox, title: tab === 'pending' ? 'Không có đơn nào cần xử lý' : 'Chưa có đơn phù hợp' }}
      />

      <Drawer open={Boolean(request)} onClose={() => setSelected(undefined)} width={isMobile ? '100%' : 560} destroyOnHidden title={request && `${requestTitle(request)} · ${request.id}`}>
        {request && (
          <div className="flex flex-col gap-4 text-[13px]">
            <UserCell name={request.who} subtitle={`Gửi ${fmtDateTime(request.at)} · ${request.branchId ?? 'Chưa xác định chi nhánh'}`} />
            <div className="flex flex-wrap items-center justify-between gap-2">
              <span className="font-medium text-ink">{sessionLabel(request) ?? (request.kind === 'hr' ? hrSummary(request.req) : '—')}</span>
              <RequestStatus item={request} />
            </div>
            {request.kind === 'cancel' && (
              <>
                <p><b>{request.req.reasonType}:</b> {request.req.reason}</p>
                {request.req.makeupAt && <p className="text-ink-secondary">GV đề xuất dạy bù: {fmtDateTime(request.req.makeupAt)}</p>}
                {request.status === 'pending' && (
                  <p className="rounded-lg bg-primary-50 p-3 text-primary-700">
                    Duyệt đơn sẽ đánh dấu buổi "đang tìm người dạy thay" và mở bước chọn 1 trong 2 cách: mời GV dạy thay (giữ lịch — ai nhận trước thì dạy) hoặc hủy buổi & xếp học bù.
                  </p>
                )}
                {sessionInvites.length > 0 && (
                  <section className="rounded-xl border border-line p-3">
                    <h3 className="mb-2 font-semibold text-ink">Lời mời dạy thay đã gửi</h3>
                    <ol className="flex flex-col gap-2">
                      {sessionInvites.map((inv) => (
                        <li key={inv.id} className="flex items-start justify-between gap-2">
                          <div className="min-w-0">
                            <p className="font-medium text-ink">{teacherName(inv.toTeacherId)}<span className="text-xs font-normal text-ink-muted"> · mời {fromNow(inv.createdAt)}</span></p>
                            {inv.respondedAt && <p className="text-xs text-ink-muted">Trả lời {fmtDateTime(inv.respondedAt)}{inv.declineReason ? ` — ${inv.declineReason}` : ''}</p>}
                          </div>
                          <StatusTag tone={INVITE_STATUS[inv.status].tone} icon={ArrowLeftRight} size="sm">{INVITE_STATUS[inv.status].label}</StatusTag>
                        </li>
                      ))}
                    </ol>
                  </section>
                )}
              </>
            )}
            {request.kind === 'leave' && (
              <>
                <p><b>{request.req.by === 'parent' ? 'Phụ huynh' : 'Học viên'} ({request.req.byName}):</b> {request.req.reasonType} — {request.req.reason}</p>
                {request.makeup && <p className="rounded-lg bg-warning-50 p-3 text-warning-700"><ArrowLeftRight size={14} className="mr-1 inline" aria-hidden /> Học bù: {request.makeupCls ? shortCode(request.makeupCls) : request.makeup.classId} · {dayjs(request.makeup.date).format('DD/MM/YYYY')} {request.makeup.startTime}</p>}
              </>
            )}
            {request.kind === 'hr' && (
              <>
                <p>{request.req.title && <><b>{request.req.title}</b><br /></>}{request.req.reason}</p>
                {request.req.amount && <p className="font-semibold tabular text-ink">{request.req.amount.toLocaleString('vi-VN')} đ</p>}
                <section className="rounded-xl border border-line p-3">
                  <h3 className="mb-2 font-semibold text-ink">Lịch sử duyệt</h3>
                  <ol className="flex flex-col gap-2">
                    {request.req.steps.map((step, index) => (
                      <li key={`${step.role}-${index}`} className="flex items-start gap-2">
                        <span className={cn('mt-0.5 grid size-6 shrink-0 place-items-center rounded-full text-[11px] font-semibold', step.decision === 'approved' ? 'bg-success-50 text-success-700' : step.decision === 'rejected' ? 'bg-danger-50 text-danger-700' : 'bg-disabled text-ink-muted')}>{index + 1}</span>
                        <div><p className="font-medium">{step.role} · {step.approver}</p><p className="text-xs text-ink-muted">{step.decision ? `${step.decision === 'approved' ? 'Đã duyệt' : 'Từ chối'}${step.at ? ` · ${fmtDateTime(step.at)}` : ''}${step.comment ? ` · ${step.comment}` : ''}` : 'Chờ xử lý'}</p></div>
                      </li>
                    ))}
                  </ol>
                </section>
                {request.step && <p className="rounded-lg bg-primary-50 p-3 text-primary-700">Bước {request.step.index}/{request.step.total} · {request.step.role}</p>}
              </>
            )}
            {(request.decidedBy || request.note) && (
              <p className="rounded-lg bg-canvas p-3 text-ink-secondary">
                {request.decidedBy && <><b>{request.decidedBy}</b>{request.decidedAt ? ` · ${fmtDateTime(request.decidedAt)}` : ''}<br /></>}
                {request.note}
              </p>
            )}
            <div className="flex flex-wrap justify-end gap-2 border-t border-line pt-4">
              {request.kind === 'cancel' && followUpOf(request, subInvites) && (
                <Button icon={ArrowLeftRight} onClick={() => setCancelItem(request)}>
                  Mời dạy thay / xếp bù
                </Button>
              )}
              {request.canDecide && (
                <>
                  <Button action="reject" onClick={() => void decide(request, 'rejected')} />
                  <Button action="approve" onClick={() => void decide(request, 'approved')}>{request.kind === 'cancel' ? 'Duyệt & mời dạy thay' : 'Duyệt đơn'}</Button>
                </>
              )}
            </div>
          </div>
        )}
      </Drawer>

      {cancelItem?.session && cancelItem.cls && (
        <HandleCancelModal request={cancelItem.req} session={cancelItem.session} cls={cancelItem.cls} onClose={() => setCancelItem(null)} />
      )}
      {confirmDialog}
    </>
  );
}
