import { useMemo, useState } from 'react';
import { Tooltip } from 'antd';
import dayjs from 'dayjs';
import { Archive, CheckCircle2, DoorOpen, Gauge, MessageSquareWarning, MoonStar, TriangleAlert, Wrench } from 'lucide-react';
import { Button, DataTable, DrawerForm, PageHeader, Select, StatCard, StatusTag, useNotify, type DataColumn } from '@/components/common';
import { useClassStore } from '@/features/academic/classes/classStore';
import { EQUIPMENT, EQUIPMENT_OPTIONS, isOpenIssue, ROOM_STATUS, ROOM_STATUS_OPTIONS, ROOM_TYPE_OPTIONS, ROOM_TYPES } from '@/features/academic/rooms/constants';
import { OccupancyBar, RoomDetailDrawer, type RoomTab } from '@/features/academic/rooms/RoomDetailDrawer';
import { RoomFormFields, type RoomFormValues } from '@/features/academic/rooms/RoomFormFields';
import { bookingsByRoom, occupancyOf, openClassesIn, roomConflicts, roomKey, weekStartOf, type Occupancy } from '@/features/academic/rooms/roomUsage';
import { useRoomStore } from '@/features/academic/rooms/roomStore';
import * as roomApi from '@/features/academic/rooms/roomApi';
import type { Room, RoomStatus } from '@/features/academic/rooms/types';
import { useRoomStatus } from '@/features/academic/rooms/useRoomStatus';
import { useRoomIssues } from '@/features/academic/rooms/useRoomIssues';
import { isIssueOverdue } from '@/features/academic/rooms/RoomIssueList';
import { RoomIssuesView } from './RoomIssuesView';
import { useAuth } from '@/hooks/useAuth';
import { cn } from '@/lib/cn';
import { fmtDate } from '@/lib/date';
import { getBranches } from '@/features/system/useBranches';
import { ALL_BRANCHES } from '@/types/auth';

/** Dòng bảng = phòng + số liệu suy ra từ buổi học */
interface Row extends Room {
  branchName: string;
  openClasses: { code: string; maxStudents: number }[];
  occ: Occupancy;
  conflicts: number;
  /** Lớp có sĩ số tối đa > sức chứa */
  overCapacity: string[];
  /** Báo hỏng chưa sửa xong */
  openIssues: number;
  severeIssues: number;
}

type Dialog = { kind: 'create' } | { kind: 'edit'; room: Room } | null;

const NEW_ROOM: Partial<RoomFormValues> = { type: 'classroom', capacity: 20, equipment: ['whiteboard', 'aircon'] };

/** Phòng học — DỮ LIỆU GIẢ (store trong bộ nhớ). Lịch sử dụng / lấp đầy suy ra từ buổi học của các lớp. */
export default function RoomsPage() {
  const { branchId, currentBranch, branches, can } = useAuth();
  const notify = useNotify();
  const rooms = useRoomStore((s) => s.rooms);
  const classes = useClassStore((s) => s.classes);
  const sessions = useClassStore((s) => s.sessions);
  const { change, dialog: statusDialog } = useRoomStatus();
  const issueActions = useRoomIssues();
  const issues = useRoomStore((s) => s.issues);
  const [view, setView] = useState<'rooms' | 'issues'>('rooms');
  const editable = can('academic.rooms');

  const [status, setStatus] = useState<RoomStatus | 'all'>('all');
  const [type, setType] = useState<string>();
  const [equipment, setEquipment] = useState<string>();
  const [dialog, setDialog] = useState<Dialog>(null);
  const [openId, setOpenId] = useState<string | null>(null);
  const [openTab, setOpenTab] = useState<RoomTab>('info');

  const open = (id: string, tab: RoomTab = 'info') => {
    setOpenTab(tab);
    setOpenId(id);
  };

  const rows: Row[] = useMemo(() => {
    const byRoom = bookingsByRoom(classes, sessions);
    const week = weekStartOf(dayjs());
    return rooms.map((r) => {
      const bookings = byRoom.get(roomKey(r.branchId, r.name)) ?? [];
      const using = openClassesIn(r, classes);
      return {
        ...r,
        branchName: getBranches(false).find((b) => b.id === r.branchId)?.name ?? r.branchId,
        openClasses: using.map((c) => ({ code: c.code, maxStudents: c.maxStudents })),
        occ: occupancyOf(bookings, week),
        conflicts: roomConflicts(bookings).length,
        overCapacity: using.filter((c) => c.maxStudents > r.capacity).map((c) => c.code),
        openIssues: issues.filter((i) => i.roomId === r.id && isOpenIssue(i)).length,
        severeIssues: issues.filter((i) => i.roomId === r.id && isOpenIssue(i) && i.severity === 'high').length,
      };
    });
  }, [rooms, classes, sessions, issues]);

  const base = useMemo(
    () =>
      rows.filter(
        (r) =>
          (branchId === ALL_BRANCHES || r.branchId === branchId) &&
          (!type || r.type === type) &&
          (!equipment || r.equipment.includes(equipment as never)),
      ),
    [rows, branchId, type, equipment],
  );
  const counts = useMemo(() => {
    const c: Record<string, number> = { all: base.length };
    base.forEach((r) => (c[r.status] = (c[r.status] ?? 0) + 1));
    return c;
  }, [base]);
  const data = status === 'all' ? base : base.filter((r) => r.status === status);

  // Số liệu tổng (chỉ phòng đang sử dụng)
  const stats = useMemo(() => {
    const active = base.filter((r) => r.status === 'active');
    const avg = (f: (r: Row) => number) => (active.length ? Math.round(active.reduce((s, r) => s + f(r), 0) / active.length) : 0);
    return {
      active: active.length,
      seats: active.reduce((s, r) => s + r.capacity, 0),
      occ: avg((r) => r.occ.pct),
      peak: avg((r) => r.occ.peakPct),
      idle: active.filter((r) => r.occ.sessions === 0).length,
    };
  }, [base]);

  // Báo hỏng của các phòng trong chi nhánh đang xem
  const branchRooms = useMemo(() => rooms.filter((r) => branchId === ALL_BRANCHES || r.branchId === branchId), [rooms, branchId]);
  const issueStats = useMemo(() => {
    const ids = new Set(branchRooms.map((r) => r.id));
    const open = issues.filter((i) => ids.has(i.roomId) && isOpenIssue(i));
    return { open: open.length, severe: open.filter((i) => i.severity === 'high').length, overdue: open.filter(isIssueOverdue).length, unassigned: open.filter((i) => i.status === 'open').length };
  }, [issues, branchRooms]);

  const columns: DataColumn<Row>[] = [
    {
      key: 'name',
      title: 'Phòng',
      width: 200,
      mobile: 'title',
      sorter: (a, b) => a.name.localeCompare(b.name, 'vi', { numeric: true }),
      render: (_, r) => {
        const T = ROOM_TYPES[r.type];
        return (
          <div className={cn('flex min-w-0 items-center gap-2.5', r.status === 'inactive' && 'opacity-60')}>
            <span className="grid size-9 shrink-0 place-items-center rounded-lg bg-primary-50 text-primary-600">
              <T.icon size={18} aria-hidden />
            </span>
            <div className="min-w-0">
              <p className="truncate font-medium text-ink">{r.name}</p>
              <p className="truncate text-xs text-ink-muted">
                {T.label}
                {r.floor && ` · ${r.floor}`}
              </p>
            </div>
          </div>
        );
      },
    },
    { key: 'branchName', title: 'Chi nhánh', width: 110, mobile: 'subtitle', render: (_, r) => r.branchName },
    {
      key: 'capacity',
      title: 'Sức chứa',
      width: 110,
      align: 'right',
      sorter: (a, b) => a.capacity - b.capacity,
      render: (_, r) => (
        <span className="inline-flex items-center gap-1.5">
          {r.overCapacity.length > 0 && (
            <Tooltip title={`Lớp ${r.overCapacity.join(', ')} nhận nhiều học viên hơn sức chứa`}>
              <TriangleAlert size={14} className="text-warning-600" aria-label="Lớp vượt sức chứa" />
            </Tooltip>
          )}
          <span className="tabular">{r.capacity} chỗ</span>
        </span>
      ),
    },
    {
      key: 'equipment',
      title: 'Thiết bị',
      width: 170,
      render: (_, r) =>
        r.equipment.length ? (
          <span className="flex flex-wrap gap-1">
            {r.equipment.map((e) => {
              const Icon = EQUIPMENT[e].icon;
              return (
                <Tooltip key={e} title={EQUIPMENT[e].label}>
                  <span className="grid size-7 place-items-center rounded-md bg-page text-ink-secondary" aria-label={EQUIPMENT[e].label}>
                    <Icon size={15} aria-hidden />
                  </span>
                </Tooltip>
              );
            })}
          </span>
        ) : (
          <span className="text-ink-muted">—</span>
        ),
    },
    {
      key: 'classes',
      title: 'Lớp đang dùng',
      width: 170,
      render: (_, r) =>
        r.openClasses.length ? (
          <button
            type="button"
            onClick={(e) => {
              e.stopPropagation();
              open(r.id, 'classes');
            }}
            className="flex flex-wrap gap-1 text-left"
          >
            {r.openClasses.map((c) => (
              <span key={c.code} className="rounded-md bg-primary-50 px-1.5 py-0.5 font-mono text-xs font-medium text-primary-700 hover:bg-primary-100">
                {c.code}
              </span>
            ))}
          </button>
        ) : (
          <span className="text-ink-muted">Trống</span>
        ),
    },
    {
      key: 'occ',
      title: 'Lấp đầy tuần này',
      width: 170,
      sorter: (a, b) => a.occ.pct - b.occ.pct,
      render: (_, r) => (
        <button
          type="button"
          onClick={(e) => {
            e.stopPropagation();
            open(r.id, 'schedule');
          }}
          className="flex w-full flex-col gap-1 text-left"
        >
          <span className="flex items-baseline justify-between gap-2 text-[13px]">
            <span className="tabular font-medium text-ink">{r.occ.pct}%</span>
            <span className="tabular text-xs text-ink-muted">
              {r.occ.hours}h · tối {r.occ.peakPct}%
            </span>
          </span>
          <OccupancyBar pct={r.occ.pct} />
        </button>
      ),
    },
    {
      key: 'status',
      title: 'Trạng thái',
      width: 170,
      mobile: 'aside',
      render: (_, r) => {
        const s = ROOM_STATUS[r.status];
        return (
          <span className="inline-flex flex-col items-start gap-1">
            <span className="inline-flex flex-wrap gap-1">
              <StatusTag tone={s.tone} icon={s.icon} size="sm">
                {s.label}
              </StatusTag>
              {r.openIssues > 0 && (
                <Tooltip title="Báo hỏng chưa sửa xong — bấm để xem">
                  <button
                    type="button"
                    onClick={(e) => {
                      e.stopPropagation();
                      open(r.id, 'issues');
                    }}
                  >
                    <StatusTag tone={r.severeIssues ? 'danger' : 'warning'} icon={Wrench} size="sm">
                      Hỏng {r.openIssues}
                    </StatusTag>
                  </button>
                </Tooltip>
              )}
              {r.conflicts > 0 && (
                <StatusTag tone="danger" icon={TriangleAlert} size="sm">
                  Trùng {r.conflicts}
                </StatusTag>
              )}
            </span>
            {r.status === 'maintenance' && r.maintenanceUntil && <span className="text-xs text-ink-muted">đến {fmtDate(r.maintenanceUntil)}</span>}
          </span>
        );
      },
    },
  ];

  const editing = dialog?.kind === 'edit' ? dialog.room : undefined;
  const editingRow = editing && rows.find((r) => r.id === editing.id);
  const biggestClass = editingRow?.openClasses.reduce<{ value: number; classCode: string } | undefined>(
    (m, c) => (!m || c.maxStudents > m.value ? { value: c.maxStudents, classCode: c.code } : m),
    undefined,
  );

  return (
    <>
      <PageHeader
        title="Phòng học"
        subtitle={`Phòng theo chi nhánh, lịch sử dụng & tỷ lệ lấp đầy · ${currentBranch ? `Chi nhánh ${currentBranch.name}` : 'Toàn hệ thống'}`}
        breadcrumb={[{ title: 'Đào tạo' }, { title: 'Phòng học' }]}
        actions={
          <>
            <Button action="export" variant="outline" collapseOnMobile onClick={() => notify.toast.info('Xuất Excel (giả lập)')} />
            <Button icon={MessageSquareWarning} tone="danger" variant="soft" collapseOnMobile onClick={() => issueActions.report()}>
              Báo hỏng
            </Button>
            {editable && (
              <Button action="create" collapseOnMobile onClick={() => setDialog({ kind: 'create' })}>
                Thêm phòng
              </Button>
            )}
          </>
        }
      />

      <div className="mb-4 grid grid-cols-2 gap-3 lg:grid-cols-4">
        <StatCard label="Phòng đang dùng" value={`${stats.active} phòng`} icon={DoorOpen} hint={`${stats.seats} chỗ ngồi · ${stats.idle} phòng trống tuần này`} />
        <StatCard label="Lấp đầy tuần này" value={`${stats.occ}%`} icon={Gauge} tone="success" hint={`Trên giờ mở cửa 08:00–21:30`} />
        <StatCard label="Cao điểm buổi tối" value={`${stats.peak}%`} icon={MoonStar} tone="info" hint="Khung 17:30–21:30" />
        <StatCard
          label="Hư hỏng chưa sửa"
          value={issueStats.open}
          icon={issueStats.open ? Wrench : CheckCircle2}
          tone={issueStats.severe ? 'danger' : issueStats.open ? 'warning' : 'success'}
          hint={issueStats.open ? `${issueStats.severe} nghiêm trọng · ${issueStats.unassigned} chưa có người nhận${issueStats.overdue ? ` · ${issueStats.overdue} quá hẹn` : ''}` : 'Không có hư hỏng nào'}
          onClick={() => setView('issues')}
        />
      </div>

      {/* Chuyển giữa danh sách phòng và báo hư hỏng */}
      <div className="mb-4 flex gap-1 border-b border-line" role="tablist" aria-label="Chế độ xem">
        {[
          { value: 'rooms' as const, label: 'Danh sách phòng', icon: DoorOpen, count: base.length },
          { value: 'issues' as const, label: 'Báo hư hỏng', icon: Wrench, count: issueStats.open },
        ].map((t) => {
          const active = view === t.value;
          return (
            <button
              key={t.value}
              type="button"
              role="tab"
              aria-selected={active}
              onClick={() => setView(t.value)}
              className={cn(
                '-mb-px inline-flex h-11 items-center gap-2 border-b-2 px-3 text-sm font-medium transition-colors',
                active ? 'border-primary-600 text-primary-700' : 'border-transparent text-ink-secondary hover:text-ink',
              )}
            >
              <t.icon size={16} aria-hidden />
              {t.label}
              <span
                className={cn(
                  'tabular rounded-full px-1.5 text-xs',
                  t.value === 'issues' && issueStats.severe ? 'bg-danger-50 text-danger-700' : active ? 'bg-primary-50 text-primary-700' : 'bg-disabled text-ink-muted',
                )}
              >
                {t.count}
              </span>
            </button>
          );
        })}
      </div>

      {view === 'issues' ? (
        <RoomIssuesView
          issues={issues}
          rooms={branchRooms}
          editable={editable}
          onOpenRoom={(id) => open(id, 'issues')}
          onReport={() => issueActions.report()}
          onStart={issueActions.start}
          onResolve={issueActions.resolve}
          onReopen={issueActions.reopen}
        />
      ) : (
        <>

      {/* Lọc nhanh theo trạng thái */}
      <div className="-mx-4 mb-4 flex gap-2 overflow-x-auto px-4 pb-1 [scrollbar-width:none] sm:mx-0 sm:flex-wrap sm:px-0 [&::-webkit-scrollbar]:hidden" role="tablist" aria-label="Lọc theo trạng thái phòng">
        {[{ value: 'all', label: 'Tất cả' } as const, ...ROOM_STATUS_OPTIONS].map((o) => {
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
      </div>

      <DataTable<Row>
        storageKey="academic-rooms"
        columns={columns}
        data={data}
        rowKey="id"
        unitLabel="phòng"
        searchKeys={['name', 'floor', 'note', 'branchName']}
        onRowClick={(r) => open(r.id)}
        search={{
          placeholder: 'Tìm theo tên phòng, tầng, ghi chú...',
          activeFilterCount: [type, equipment].filter(Boolean).length,
          filters: (
            <>
              <Select placeholder="Loại phòng" value={type} onChange={setType} options={ROOM_TYPE_OPTIONS} showSearch={false} className="lg:!w-44" />
              <Select placeholder="Có thiết bị" value={equipment} onChange={setEquipment} options={EQUIPMENT_OPTIONS} showSearch={false} className="lg:!w-44" />
            </>
          ),
          onReset: () => {
            setType(undefined);
            setEquipment(undefined);
            setStatus('all');
          },
        }}
        rowActions={
          editable
            ? (r) => (
                <>
                  <Button action="edit" iconOnly size="sm" onClick={() => setDialog({ kind: 'edit', room: r })} />
                  <Button icon={MessageSquareWarning} iconOnly size="sm" tone="danger" variant="soft" tooltip="Báo hỏng" aria-label="Báo hỏng" onClick={() => issueActions.report(r.id)} />
                  {r.status === 'active' ? (
                    <Button icon={Wrench} iconOnly size="sm" tone="neutral" variant="soft" tooltip="Chuyển bảo trì" aria-label="Chuyển bảo trì" onClick={() => change(r, 'maintenance')} />
                  ) : (
                    <Button icon={CheckCircle2} iconOnly size="sm" tone="success" variant="soft" tooltip="Dùng lại" aria-label="Dùng lại" onClick={() => change(r, 'active')} />
                  )}
                  {r.status !== 'inactive' && (
                    <Button icon={Archive} iconOnly size="sm" tone="neutral" variant="ghost" tooltip="Ngừng sử dụng" aria-label="Ngừng sử dụng" onClick={() => change(r, 'inactive')} />
                  )}
                </>
              )
            : undefined
        }
        actionsWidth={175}
        empty={{
          title: 'Chưa có phòng học',
          description: 'Thêm phòng để chọn khi mở lớp và theo dõi lịch sử dụng.',
          action: editable ? <Button action="create" onClick={() => setDialog({ kind: 'create' })}>Thêm phòng</Button> : undefined,
        }}
      />

        </>
      )}

      <DrawerForm<RoomFormValues>
        key={editing ? `edit-${editing.id}` : 'create'}
        open={Boolean(dialog)}
        onClose={() => setDialog(null)}
        width={640}
        columns={2}
        title={editing ? `Sửa phòng ${editing.name}` : 'Thêm phòng học'}
        description={editing ? undefined : 'Phòng mới ở trạng thái "Đang sử dụng" — chọn được ngay khi mở lớp.'}
        submitAction={editing ? 'save' : 'create'}
        submitText={editing ? 'Lưu' : 'Thêm phòng'}
        successMessage={editing ? 'Đã lưu phòng học' : 'Đã thêm phòng học'}
        initialValues={editing ?? { ...NEW_ROOM, branchId: currentBranch?.id ?? branches[0]?.id }}
        onSubmit={async (v) => {
          const input = { ...v, name: v.name.trim(), floor: v.floor?.trim() || undefined, note: v.note?.trim() || undefined, equipment: v.equipment ?? [] };
          if (editing) await roomApi.updateRoom(editing.id, { ...input, status: editing.status });
          else await roomApi.createRoom({ ...input, status: 'active' });
        }}
      >
        <RoomFormFields editingId={editing?.id} lockBranch={Boolean(editingRow?.openClasses.length)} minCapacity={biggestClass} />
      </DrawerForm>

      <RoomDetailDrawer
        roomId={openId}
        initialTab={openTab}
        editable={editable}
        onClose={() => setOpenId(null)}
        onEdit={(id) => {
          const r = rooms.find((x) => x.id === id);
          if (r) setDialog({ kind: 'edit', room: r });
        }}
        onChangeStatus={(id, s) => {
          const r = rooms.find((x) => x.id === id);
          if (r) change(r, s);
        }}
      />
      {statusDialog}
      {issueActions.dialogs}
    </>
  );
}
