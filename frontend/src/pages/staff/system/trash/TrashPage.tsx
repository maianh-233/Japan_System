import { useEffect, useMemo, useState } from 'react';
import { Select as AntSelect } from 'antd';
import dayjs from 'dayjs';
import { ArchiveRestore, Trash2 } from 'lucide-react';
import { Button, DataTable, PageHeader, StatusTag, useConfirm, useNotify, type DataColumn } from '@/components/common';
import { useCrmStore } from '@/features/crm/crmStore';
import { useSettings } from '@/features/system/settings';
import { daysLeft, TRASH_KIND, useTrashStore, type TrashKind } from '@/features/system/trash';
import { getBranches } from '@/features/system/useBranches';
import { useAuth } from '@/hooks/useAuth';
import { fmtDateTime } from '@/lib/date';
import { ALL_BRANCHES } from '@/types/auth';

type Kind = TrashKind | 'lead' | 'student';
interface Row {
  key: string;
  kind: Kind;
  label: string;
  detail?: string;
  branchId?: string;
  at: string;
  by?: string;
  left: number;
  /** Mục chỉ bị ẩn (khôi phục = bỏ ẩn, không xóa vĩnh viễn được ở đây) */
  hiddenOnly: boolean;
  refId: string;
}

/** Thùng rác: dữ liệu đã xóa / ẩn còn trong hạn lưu — khôi phục hoặc xóa vĩnh viễn */
export default function TrashPage() {
  const { branchId } = useAuth();
  const notify = useNotify();
  const { confirm, confirmDialog } = useConfirm();
  const days = useSettings((s) => s.safety.trashDays);
  const items = useTrashStore((s) => s.items);
  const { restore, purge } = useTrashStore();
  const leads = useCrmStore((s) => s.leads);
  const students = useCrmStore((s) => s.students);
  const [kind, setKind] = useState<Kind>();

  // Quá hạn → xóa hẳn (khi có backend: job dọn hằng ngày)
  useEffect(() => {
    items.filter((i) => daysLeft(i.deletedAt) <= 0).forEach((i) => purge(i.id));
  }, [items, purge, days]);

  const rows: Row[] = useMemo(() => {
    const since = dayjs().subtract(days, 'day');
    const out: Row[] = [
      ...items.map((i) => ({ key: i.id, kind: i.kind, label: i.label, detail: i.detail, branchId: i.branchId, at: i.deletedAt, by: i.deletedBy, left: daysLeft(i.deletedAt), hiddenOnly: false, refId: i.id })),
      ...leads
        .filter((l) => l.hidden && l.hiddenAt && dayjs(l.hiddenAt).isAfter(since))
        .map((l) => ({ key: `lead:${l.id}`, kind: 'lead' as const, label: l.fullName, detail: `${l.code} · ${l.phone}`, branchId: l.branchId, at: l.hiddenAt!, left: daysLeft(l.hiddenAt!), hiddenOnly: true, refId: l.id })),
      ...students
        .filter((s) => s.hidden && s.hiddenAt && dayjs(s.hiddenAt).isAfter(since))
        .map((s) => ({ key: `student:${s.id}`, kind: 'student' as const, label: s.fullName, detail: `${s.code}${s.phone ? ` · ${s.phone}` : ''}`, branchId: s.branchId, at: s.hiddenAt!, left: daysLeft(s.hiddenAt!), hiddenOnly: true, refId: s.id })),
    ];
    return out.filter((r) => (branchId === ALL_BRANCHES || !r.branchId || r.branchId === branchId) && (!kind || r.kind === kind)).sort((a, b) => b.at.localeCompare(a.at));
  }, [items, leads, students, days, branchId, kind]);

  const doRestore = (r: Row) => {
    if (r.kind === 'lead') useCrmStore.getState().setHidden(r.refId, false);
    else if (r.kind === 'student') useCrmStore.getState().setStudentHidden(r.refId, false);
    else if (!restore(r.refId)) return notify.toast.error('Không khôi phục được mục này');
    notify.toast.success(`Đã khôi phục: ${r.label}`);
  };
  const doPurge = async (r: Row) => {
    const ok = await confirm({ title: `Xóa vĩnh viễn "${r.label}"?`, description: 'Không thể hoàn tác. Thao tác được ghi vào Nhật ký hệ thống.', tone: 'danger', confirmAction: 'delete', confirmText: 'Xóa vĩnh viễn', typeToConfirm: 'XOA' });
    if (!ok) return;
    purge(r.refId);
    notify.toast.info('Đã xóa vĩnh viễn');
  };

  const columns: DataColumn<Row>[] = [
    {
      key: 'label',
      title: 'Dữ liệu',
      width: 300,
      mobile: 'title',
      render: (_, r) => (
        <span className="flex flex-col">
          <span className="font-medium text-ink">{r.label}</span>
          {r.detail && <span className="text-xs text-ink-muted">{r.detail}</span>}
        </span>
      ),
    },
    { key: 'kind', title: 'Loại', width: 170, mobile: 'subtitle', render: (_, r) => TRASH_KIND[r.kind] },
    { key: 'branch', title: 'Chi nhánh', width: 110, render: (_, r) => (r.branchId ? getBranches().find((b) => b.id === r.branchId)?.name ?? r.branchId : '—') },
    { key: 'at', title: 'Xóa / ẩn lúc', width: 170, render: (_, r) => <span className="flex flex-col"><span className="tabular">{fmtDateTime(r.at)}</span>{r.by && <span className="text-xs text-ink-muted">{r.by}</span>}</span> },
    {
      key: 'left',
      title: 'Còn giữ',
      width: 110,
      mobile: 'aside',
      sorter: (a, b) => a.left - b.left,
      render: (_, r) => (
        <StatusTag tone={r.left <= 3 ? 'danger' : r.left <= 7 ? 'warning' : 'neutral'} icon={Trash2} size="sm">
          {r.left} ngày
        </StatusTag>
      ),
    },
  ];

  return (
    <>
      <PageHeader
        title="Thùng rác"
        subtitle={`Dữ liệu bị xóa hoặc ẩn trong ${days} ngày gần nhất — khôi phục được trước khi tự xóa hẳn (đổi số ngày ở Cài đặt chung)`}
        breadcrumb={[{ title: 'Hệ thống' }, { title: 'Thùng rác' }]}
      />
      <DataTable<Row>
        storageKey="system-trash"
        columns={columns}
        data={rows}
        rowKey="key"
        unitLabel="mục"
        searchKeys={['label', 'detail']}
        search={{
          placeholder: 'Tìm theo tên, mã...',
          filters: (
            <AntSelect<Kind>
              allowClear
              placeholder="Mọi loại dữ liệu"
              className="!w-52"
              value={kind}
              onChange={setKind}
              options={(Object.keys(TRASH_KIND) as Kind[]).map((k) => ({ value: k, label: TRASH_KIND[k] }))}
            />
          ),
        }}
        rowActions={(r) => (
          <>
            <Button icon={ArchiveRestore} size="sm" variant="soft" onClick={() => doRestore(r)}>
              {r.hiddenOnly ? 'Bỏ ẩn' : 'Khôi phục'}
            </Button>
            {!r.hiddenOnly && <Button icon={Trash2} iconOnly size="sm" tone="danger" variant="ghost" aria-label="Xóa vĩnh viễn" tooltip="Xóa vĩnh viễn" onClick={() => doPurge(r)} />}
          </>
        )}
        actionsWidth={150}
        empty={{ icon: Trash2, title: 'Thùng rác trống', description: `Dữ liệu bị xóa (mẫu tin, quy tắc chia lead, bài tập, vai trò, hóa đơn đã hủy...) và lead / học viên bị ẩn sẽ nằm ở đây ${days} ngày.` }}
      />
      {confirmDialog}
    </>
  );
}
