import { useMemo, useState } from 'react';
import { CheckCircle2, Hammer, RotateCcw, Wrench } from 'lucide-react';
import { Button, DataTable, Select, StatusTag, type DataColumn } from '@/components/common';
import { EQUIPMENT, ISSUE_SEVERITY, ISSUE_STATUS, ISSUE_STATUS_OPTIONS } from '@/features/academic/rooms/constants';
import { isIssueOverdue } from '@/features/academic/rooms/RoomIssueList';
import type { IssueStatus, Room, RoomIssue } from '@/features/academic/rooms/types';
import { cn } from '@/lib/cn';
import { fmtDate, fmtDateTime, fromNow } from '@/lib/date';
import { formatCurrency } from '@/lib/format';

interface Row extends RoomIssue {
  roomName: string;
  overdue: boolean;
}

interface Props {
  issues: RoomIssue[];
  /** Phòng trong chi nhánh đang xem */
  rooms: Room[];
  editable: boolean;
  onOpenRoom: (roomId: string) => void;
  onReport: () => void;
  onStart: (i: RoomIssue) => void;
  onResolve: (i: RoomIssue) => void;
  onReopen: (i: RoomIssue) => void;
}

const SEVERITY_FILTER = (Object.keys(ISSUE_SEVERITY) as (keyof typeof ISSUE_SEVERITY)[]).map((k) => ({ value: k, label: ISSUE_SEVERITY[k].label }));
const RANK = { high: 0, medium: 1, low: 2 };

/** Tất cả báo hư hỏng của các phòng trong chi nhánh — cho người phụ trách cơ sở vật chất theo dõi */
export function RoomIssuesView({ issues, rooms, editable, onOpenRoom, onReport, onStart, onResolve, onReopen }: Props) {
  const [status, setStatus] = useState<IssueStatus | 'pending' | 'all'>('pending');
  const [severity, setSeverity] = useState<string>();
  const [roomId, setRoomId] = useState<string>();

  const rows: Row[] = useMemo(() => {
    const name = new Map(rooms.map((r) => [r.id, r.name]));
    return issues
      .filter((i) => name.has(i.roomId))
      .map((i) => ({ ...i, roomName: name.get(i.roomId)!, overdue: isIssueOverdue(i) }))
      .sort((a, b) => Number(a.status === 'resolved') - Number(b.status === 'resolved') || RANK[a.severity] - RANK[b.severity] || b.reportedAt.localeCompare(a.reportedAt));
  }, [issues, rooms]);

  const base = rows.filter((r) => (!severity || r.severity === severity) && (!roomId || r.roomId === roomId));
  const counts: Record<string, number> = { all: base.length, pending: base.filter((r) => r.status !== 'resolved').length };
  base.forEach((r) => (counts[r.status] = (counts[r.status] ?? 0) + 1));
  const data = status === 'all' ? base : status === 'pending' ? base.filter((r) => r.status !== 'resolved') : base.filter((r) => r.status === status);

  const columns: DataColumn<Row>[] = [
    {
      key: 'title',
      title: 'Sự cố',
      width: 280,
      mobile: 'title',
      render: (_, r) => {
        const Icon = r.equipment ? EQUIPMENT[r.equipment].icon : Wrench;
        return (
          <div className="flex min-w-0 gap-2.5">
            <span className="grid size-8 shrink-0 place-items-center rounded-lg bg-page text-ink-secondary">
              <Icon size={16} aria-hidden />
            </span>
            <div className="min-w-0">
              <p className="truncate font-medium text-ink">{r.title}</p>
              <p className="truncate text-xs text-ink-muted">{r.equipment ? EQUIPMENT[r.equipment].label : 'Cơ sở vật chất'}</p>
            </div>
          </div>
        );
      },
    },
    {
      key: 'roomName',
      title: 'Phòng',
      width: 110,
      mobile: 'subtitle',
      sorter: (a, b) => a.roomName.localeCompare(b.roomName, 'vi', { numeric: true }),
      render: (_, r) => (
        <button
          type="button"
          onClick={(e) => {
            e.stopPropagation();
            onOpenRoom(r.roomId);
          }}
          className="font-medium text-primary-700 hover:underline"
        >
          {r.roomName}
        </button>
      ),
    },
    {
      key: 'severity',
      title: 'Mức độ',
      width: 135,
      sorter: (a, b) => RANK[a.severity] - RANK[b.severity],
      render: (_, r) => {
        const s = ISSUE_SEVERITY[r.severity];
        return (
          <StatusTag tone={r.status === 'resolved' ? 'neutral' : s.tone} icon={s.icon} size="sm">
            {s.label}
          </StatusTag>
        );
      },
    },
    {
      key: 'reportedAt',
      title: 'Người báo',
      width: 190,
      sorter: (a, b) => a.reportedAt.localeCompare(b.reportedAt),
      render: (_, r) => (
        <div className="leading-5">
          <p className="truncate text-[13px] text-ink">{r.reportedBy}</p>
          <p className="text-xs text-ink-muted" title={fmtDateTime(r.reportedAt)}>
            {fromNow(r.reportedAt)}
          </p>
        </div>
      ),
    },
    {
      key: 'assignee',
      title: 'Xử lý',
      width: 220,
      render: (_, r) =>
        r.status === 'resolved' ? (
          <div className="leading-5">
            <p className="truncate text-[13px] text-ink">{r.resolution}</p>
            <p className="text-xs text-ink-muted">
              {fmtDate(r.resolvedAt)}
              {r.cost ? ` · ${formatCurrency(r.cost)}` : ''}
            </p>
          </div>
        ) : r.assignee ? (
          <div className="leading-5">
            <p className="truncate text-[13px] text-ink">{r.assignee}</p>
            {r.dueDate && <p className={cn('text-xs', r.overdue ? 'font-medium text-danger-600' : 'text-ink-muted')}>Hẹn {fmtDate(r.dueDate)}{r.overdue && ' — quá hạn'}</p>}
          </div>
        ) : (
          <span className="text-[13px] text-danger-600">Chưa có người nhận</span>
        ),
    },
    {
      key: 'status',
      title: 'Trạng thái',
      width: 135,
      mobile: 'aside',
      render: (_, r) => {
        const s = ISSUE_STATUS[r.status];
        return (
          <StatusTag tone={s.tone} icon={s.icon} size="sm">
            {s.label}
          </StatusTag>
        );
      },
    },
  ];

  return (
    <>
      <div className="-mx-4 mb-4 flex gap-2 overflow-x-auto px-4 pb-1 [scrollbar-width:none] sm:mx-0 sm:flex-wrap sm:px-0 [&::-webkit-scrollbar]:hidden" role="tablist" aria-label="Lọc theo trạng thái báo hỏng">
        {[{ value: 'pending', label: 'Chưa sửa xong' }, ...ISSUE_STATUS_OPTIONS, { value: 'all', label: 'Tất cả' }].map((o) => {
          const active = status === o.value;
          return (
            <button
              key={o.value}
              type="button"
              role="tab"
              aria-selected={active}
              onClick={() => setStatus(o.value as typeof status)}
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
      </div>

      <DataTable<Row>
        storageKey="academic-room-issues"
        columns={columns}
        data={data}
        rowKey="id"
        unitLabel="báo hỏng"
        searchKeys={['title', 'description', 'roomName', 'reportedBy', 'assignee']}
        onRowClick={(r) => onOpenRoom(r.roomId)}
        search={{
          placeholder: 'Tìm theo sự cố, phòng, người báo, người sửa...',
          activeFilterCount: [severity, roomId].filter(Boolean).length,
          filters: (
            <>
              <Select placeholder="Mức độ" value={severity} onChange={setSeverity} options={SEVERITY_FILTER} showSearch={false} className="lg:!w-40" />
              <Select placeholder="Phòng" value={roomId} onChange={setRoomId} options={rooms.map((r) => ({ value: r.id, label: r.name }))} className="lg:!w-40" />
            </>
          ),
          onReset: () => {
            setSeverity(undefined);
            setRoomId(undefined);
            setStatus('pending');
          },
        }}
        rowActions={
          editable
            ? (r) =>
                r.status === 'resolved' ? (
                  <Button icon={RotateCcw} iconOnly size="sm" tone="neutral" variant="soft" tooltip="Vẫn còn hỏng — mở lại" aria-label="Mở lại" onClick={() => onReopen(r)} />
                ) : (
                  <>
                    <Button icon={Hammer} iconOnly size="sm" variant="soft" tooltip={r.status === 'open' ? 'Nhận xử lý' : 'Đổi người / hẹn'} aria-label="Nhận xử lý" onClick={() => onStart(r)} />
                    <Button icon={CheckCircle2} iconOnly size="sm" tone="success" variant="soft" tooltip="Đã sửa xong" aria-label="Đã sửa xong" onClick={() => onResolve(r)} />
                  </>
                )
            : undefined
        }
        actionsWidth={110}
        empty={{
          icon: Wrench,
          title: status === 'pending' ? 'Không có hư hỏng nào chờ sửa' : 'Không có báo hỏng',
          description: 'Giáo viên / nhân viên báo hỏng tại đây hoặc trong chi tiết từng phòng.',
          action: <Button icon={Wrench} variant="soft" onClick={onReport}>Báo hỏng</Button>,
        }}
      />
    </>
  );
}
