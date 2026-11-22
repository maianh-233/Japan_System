import { useMemo, useState } from 'react';
import { Segmented, Tooltip } from 'antd';
import { CheckCircle2, Clock, ShieldAlert, Undo2, XCircle } from 'lucide-react';
import { Button, DataTable, PageHeader, StatusTag, useConfirm, useNotify, type DataColumn } from '@/components/common';
import { APPROVAL_KIND, APPROVAL_STATUS, canApproveKind, useApprovalStore, type ApprovalKind, type ApprovalRequest } from '@/features/system/approvals';
import { useAuth } from '@/hooks/useAuth';
import { fmtDateTime, fromNow } from '@/lib/date';
import { ALL_BRANCHES } from '@/types/auth';
import { USE_API } from '@/lib/api/config';
import * as systemApi from '@/features/system/systemApi';

type Tab = 'todo' | 'mine' | 'history';
const STATUS_ICON = { pending: Clock, approved: CheckCircle2, rejected: XCircle, withdrawn: Undo2 } as const;

/** Duyệt thao tác nhạy cảm (2 người): hoàn tiền, hủy hóa đơn, cấp quyền tài chính */
export default function ApprovalsPage() {
  const { user, can, branchId } = useAuth();
  const notify = useNotify();
  const { confirm, confirmDialog } = useConfirm();
  const requests = useApprovalStore((s) => s.requests);
  const { approve, reject, withdraw } = useApprovalStore();
  const me = { id: user?.id ?? '', name: user?.fullName ?? '' };
  const mine = (r: ApprovalRequest) => r.requestedBy.id === me.id || r.requestedBy.name === me.name;

  const inScope = useMemo(() => requests.filter((r) => branchId === ALL_BRANCHES || !r.branchId || r.branchId === branchId), [requests, branchId]);
  const todo = inScope.filter((r) => r.status === 'pending' && !mine(r) && canApproveKind(r.kind, can));
  const own = inScope.filter((r) => mine(r));
  const [tab, setTab] = useState<Tab>(todo.length ? 'todo' : 'mine');
  const data = tab === 'todo' ? todo : tab === 'mine' ? own : inScope.filter((r) => r.status !== 'pending');

  const doApprove = async (r: ApprovalRequest) => {
    let warnings: string[] = [];
    const res = await confirm({
      title: `Duyệt ${r.code}: ${APPROVAL_KIND[r.kind]}?`,
      description: r.title,
      consequences: [
        r.kind === 'refund' ? 'Ghi khoản hoàn vào hóa đơn ngay — Kế toán chi tiền theo phiếu' : r.kind === 'void_invoice' ? 'Hóa đơn bị hủy, vào Thùng rác (khôi phục được trong thời hạn lưu)' : 'Quyền tài chính có hiệu lực ngay với người được cấp',
        `Nhật ký ghi: người tạo ${r.requestedBy.name} · người duyệt ${me.name}`,
      ],
      tone: 'warning',
      confirmAction: 'approve',
      confirmText: 'Duyệt & thực hiện',
      // Server áp dụng: cấp quyền tài chính (kiểm lại quyền người đề nghị) · hoàn tiền / hủy hóa đơn (Tài chính kiểm lại hoàn tối đa, còn tiền chưa hoàn)
      onConfirm: async () => {
        if (USE_API) warnings = await systemApi.approveRequest(r.id);
        else approve(r.id, me);
      },
    });
    if (res) notify.success({ title: `Đã duyệt ${r.code}`, description: [r.title, ...warnings].join(' — ') });
  };
  const doReject = async (r: ApprovalRequest) => {
    const res = await confirm({ title: `Từ chối ${r.code}?`, description: r.title, tone: 'danger', confirmAction: 'reject', confirmText: 'Từ chối', requireReason: { label: 'Lý do', placeholder: 'VD: Số tiền hoàn chưa đúng số buổi đã học' }, onConfirm: ({ reason }) => (USE_API ? systemApi.rejectRequest(r.id, reason ?? '') : reject(r.id, me, reason ?? '')) });
    if (res) notify.toast.info(`Đã từ chối ${r.code}`);
  };

  const columns: DataColumn<ApprovalRequest>[] = [
    {
      key: 'title',
      title: 'Yêu cầu',
      width: 340,
      mobile: 'title',
      ellipsis: false,
      render: (_, r) => (
        <span className="flex flex-col">
          <span className="font-medium text-ink">{r.title}</span>
          <span className="text-xs text-ink-muted">
            {r.code} · {APPROVAL_KIND[r.kind]}
            {r.detail ? ` · ${r.detail}` : ''}
          </span>
        </span>
      ),
    },
    {
      key: 'by',
      title: 'Người tạo',
      width: 170,
      mobile: 'subtitle',
      render: (_, r) => (
        <span className="flex flex-col">
          <span>{r.requestedBy.name}</span>
          <span className="text-xs text-ink-muted" title={fmtDateTime(r.requestedAt)}>{fromNow(r.requestedAt)}</span>
        </span>
      ),
    },
    {
      key: 'status',
      title: 'Trạng thái',
      width: 220,
      mobile: 'aside',
      ellipsis: false,
      render: (_, r) => {
        const st = APPROVAL_STATUS[r.status];
        return (
          <span className="flex flex-col items-start gap-0.5">
            <StatusTag tone={st.tone} icon={STATUS_ICON[r.status]} size="sm">{st.label}</StatusTag>
            {r.decidedBy && r.status !== 'withdrawn' && (
              <span className="text-xs text-ink-muted">
                {r.decidedBy.name} · {fmtDateTime(r.decidedAt!)}
                {r.note ? ` — ${r.note}` : ''}
              </span>
            )}
          </span>
        );
      },
    },
  ];

  return (
    <>
      <PageHeader
        title="Duyệt thao tác"
        subtitle="Hoàn tiền, hủy hóa đơn, cấp quyền tài chính phải có người thứ hai duyệt — người tạo không tự duyệt được. Mọi bước ghi Nhật ký hệ thống."
        breadcrumb={[{ title: 'Hệ thống' }, { title: 'Duyệt thao tác' }]}
      />
      <Segmented<Tab>
        className="mb-4"
        value={tab}
        onChange={setTab}
        options={[
          { value: 'todo', label: `Chờ tôi duyệt (${todo.length})` },
          { value: 'mine', label: `Tôi đã gửi (${own.length})` },
          { value: 'history', label: 'Đã xử lý' },
        ]}
      />
      <DataTable<ApprovalRequest>
        storageKey={`approvals-${tab}`}
        columns={columns}
        data={data}
        rowKey="id"
        unitLabel="yêu cầu"
        searchKeys={['title', 'code']}
        rowActions={(r) =>
          r.status !== 'pending' ? null : mine(r) ? (
            <Button icon={Undo2} size="sm" variant="ghost" tone="neutral" onClick={() =>
                USE_API
                  ? systemApi.withdrawRequest(r.id).catch((e: Error) => notify.error({ title: 'Chưa rút lại được', description: e.message }))
                  : withdraw(r.id, me)
              }
            >
              Rút lại
            </Button>
          ) : canApproveKind(r.kind as ApprovalKind, can) ? (
            <>
              <Button icon={CheckCircle2} size="sm" tone="success" variant="soft" onClick={() => doApprove(r)}>
                Duyệt
              </Button>
              <Button icon={XCircle} iconOnly size="sm" tone="danger" variant="ghost" aria-label="Từ chối" tooltip="Từ chối" onClick={() => doReject(r)} />
            </>
          ) : (
            <Tooltip title={r.kind === 'finance_perm' ? 'Cần quyền cấp quản trị (Nhật ký hệ thống) để duyệt' : 'Cần quyền Hóa đơn để duyệt'}>
              <span className="text-xs text-ink-muted">Không đủ quyền duyệt</span>
            </Tooltip>
          )
        }
        actionsWidth={170}
        empty={{
          icon: ShieldAlert,
          title: tab === 'todo' ? 'Không có yêu cầu nào chờ bạn duyệt' : tab === 'mine' ? 'Bạn chưa gửi yêu cầu nào' : 'Chưa có yêu cầu đã xử lý',
          description: 'Yêu cầu tạo từ: Hoàn tiền / Hủy hóa đơn (Tài chính › Hóa đơn, tab Học viên của lớp) và cấp quyền tài chính (Phân quyền người dùng).',
        }}
      />
      {confirmDialog}
    </>
  );
}
