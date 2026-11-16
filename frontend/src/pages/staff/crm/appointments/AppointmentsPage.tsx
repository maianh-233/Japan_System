import { useMemo, useState } from 'react';
import { Dropdown, Segmented } from 'antd';
import dayjs from 'dayjs';
import { BellRing, CalendarCheck, CalendarClock, CalendarPlus, CheckCircle2, ClipboardCheck, Ellipsis, MessageCircle, MessagesSquare, TriangleAlert, UserX, XCircle, type LucideIcon } from 'lucide-react';
import { Button, DataTable, PageHeader, Select, StatusTag, Timetable, UserCell, useConfirm, useNotify, type DataColumn } from '@/components/common';
import { useRoomStore } from '@/features/academic/rooms/roomStore';
import { APPOINTMENT_KIND, APPOINTMENT_STATUS, useAppointmentStore, type Appointment, type AppointmentKind } from '@/features/crm/appointments/appointments';
import { BookAppointmentModal } from '@/features/crm/appointments/BookAppointmentModal';
import { LeadDetailDrawer } from '@/features/crm/components/LeadDetailDrawer';
import { useCrmStore } from '@/features/crm/crmStore';
import { SendMessageModal, type MessageTarget } from '@/features/crm/messages/SendMessageModal';
import type { MessageScenario } from '@/features/crm/messages/messages';
import { useHrStore } from '@/features/hr/hrStore';
import { useMe } from '@/features/me/useMe';
import { useAuth } from '@/hooks/useAuth';
import { useOpenParam } from '@/hooks/useOpenParam';
import { ALL_BRANCHES } from '@/types/auth';

interface Row extends Appointment {
  leadName: string;
  leadPhone: string;
  staffName: string;
  roomName?: string;
}

type View = 'upcoming' | 'today' | 'past' | 'week';
const STATUS_ICON: Record<Appointment['status'], LucideIcon> = { scheduled: CalendarCheck, done: CheckCircle2, no_show: UserX, cancelled: XCircle };

/** Lịch hẹn tư vấn / test tại trung tâm — đặt lịch, kiểm tra trùng phòng & người, nhắc trước 1 giờ, ghi kết quả */
export default function AppointmentsPage() {
  const { branchId, currentBranch, can } = useAuth();
  const me = useMe();
  const notify = useNotify();
  const { confirm, confirmDialog } = useConfirm();
  const appointments = useAppointmentStore((s) => s.appointments);
  const update = useAppointmentStore((s) => s.update);
  const leads = useCrmStore((s) => s.leads);
  const employees = useHrStore((s) => s.employees);
  const rooms = useRoomStore((s) => s.rooms);

  const [view, setView] = useState<View>('upcoming');
  const [kind, setKind] = useState<AppointmentKind>();
  const [staffId, setStaffId] = useState<string | undefined>(me?.department === 'sales' ? me.id : undefined);
  const [booking, setBooking] = useState(false);
  const [editing, setEditing] = useState<Appointment>();
  const [leadId, setLeadId] = useState<string | null>(null);
  const [msg, setMsg] = useState<{ target: MessageTarget; scenario: MessageScenario } | null>(null);
  const [week, setWeek] = useState(dayjs());
  const editable = can('crm.appointments');

  useOpenParam((id) => {
    const a = useAppointmentStore.getState().appointments.find((x) => x.id === id);
    if (!a) return;
    setView(dayjs(a.start).isSame(dayjs(), 'day') ? 'today' : dayjs(a.start).isBefore(dayjs()) ? 'past' : 'upcoming');
  });

  const rows: Row[] = useMemo(
    () =>
      appointments
        .filter((a) => (branchId === ALL_BRANCHES || a.branchId === branchId) && (!kind || a.kind === kind) && (!staffId || a.staffId === staffId))
        .map((a) => {
          const l = leads.find((x) => x.id === a.leadId);
          return { ...a, leadName: l?.fullName ?? a.leadId, leadPhone: l?.phone ?? '', staffName: employees.find((e) => e.id === a.staffId)?.fullName ?? '—', roomName: rooms.find((r) => r.id === a.roomId)?.name };
        }),
    [appointments, branchId, kind, staffId, leads, employees, rooms],
  );

  const now = dayjs();
  const list = rows
    .filter((r) =>
      view === 'today' ? dayjs(r.start).isSame(now, 'day') : view === 'past' ? dayjs(r.end).isBefore(now) && !dayjs(r.start).isSame(now, 'day') : view === 'upcoming' ? dayjs(r.end).isAfter(now) && r.status === 'scheduled' : true,
    )
    .sort((a, b) => (view === 'past' ? b.start.localeCompare(a.start) : a.start.localeCompare(b.start)));
  const counts = {
    today: rows.filter((r) => dayjs(r.start).isSame(now, 'day')).length,
    upcoming: rows.filter((r) => dayjs(r.end).isAfter(now) && r.status === 'scheduled').length,
    unresolved: rows.filter((r) => r.status === 'scheduled' && dayjs(r.end).isBefore(now)).length,
  };

  const markDone = async (r: Row) => {
    const res = await confirm({
      title: `${r.leadName} đã đến?`,
      description: `${APPOINTMENT_KIND[r.kind].label} · ${dayjs(r.start).format('HH:mm DD/MM')}`,
      tone: 'success',
      confirmText: 'Đã đến',
      requireReason: { label: 'Kết quả buổi hẹn', placeholder: r.kind === 'test' ? 'VD: Đạt mức N4, gợi ý lớp N3 tối 3-5' : 'VD: Ưng lớp N4 tối 2-4-6, hẹn test thứ 7', minLength: 3 },
    });
    if (!res) return;
    update(r.id, { status: 'done', outcome: res.reason });
    useCrmStore.getState().addConsultation({ leadId: r.leadId, channel: 'meeting', result: r.kind === 'test' ? 'test_booked' : 'interested', content: `Đến ${APPOINTMENT_KIND[r.kind].label.toLowerCase()} (${r.code}): ${res.reason}`, staffName: r.staffName });
    notify.toast.success('Đã ghi kết quả vào lịch sử tư vấn');
  };
  const markNoShow = async (r: Row) => {
    const ok = await confirm({ title: `${r.leadName} không đến?`, description: 'Lead được đặt hẹn gọi lại sau 1 ngày.', tone: 'warning', confirmText: 'Không đến' });
    if (!ok) return;
    update(r.id, { status: 'no_show' });
    useCrmStore.getState().updateLead(r.leadId, { nextFollowUpAt: dayjs().add(1, 'day').hour(10).minute(0).toISOString() });
    notify.toast.info('Đã đánh dấu không đến · hẹn gọi lại ngày mai');
  };
  const cancel = async (r: Row) => {
    const res = await confirm({ title: `Hủy lịch ${r.code}?`, tone: 'danger', confirmAction: 'reject', confirmText: 'Hủy lịch', requireReason: { label: 'Lý do', placeholder: 'VD: Khách báo bận, chưa hẹn lại' } });
    if (!res) return;
    update(r.id, { status: 'cancelled', cancelReason: res.reason });
    notify.toast.info('Đã hủy lịch hẹn');
  };

  const columns: DataColumn<Row>[] = [
    {
      key: 'start',
      title: 'Thời gian',
      width: 150,
      mobile: 'subtitle',
      sorter: (a, b) => a.start.localeCompare(b.start),
      render: (_, r) => (
        <span className="flex flex-col">
          <span className="tabular font-medium text-ink">
            {dayjs(r.start).format('HH:mm')}–{dayjs(r.end).format('HH:mm')}
          </span>
          <span className="text-xs text-ink-muted">{dayjs(r.start).format('dd DD/MM')}</span>
        </span>
      ),
    },
    {
      key: 'lead',
      title: 'Lead',
      width: 210,
      mobile: 'title',
      render: (_, r) => <UserCell name={r.leadName} subtitle={`${r.leadPhone} · ${r.code}`} size="sm" onClick={() => setLeadId(r.leadId)} />,
    },
    { key: 'kind', title: 'Loại', width: 110, render: (_, r) => <StatusTag tone={r.kind === 'test' ? 'primary' : 'info'} icon={r.kind === 'test' ? ClipboardCheck : MessagesSquare} size="sm">{APPOINTMENT_KIND[r.kind].short}</StatusTag> },
    { key: 'staff', title: 'Người tiếp', dataIndex: 'staffName', width: 160 },
    { key: 'room', title: 'Phòng', width: 90, render: (_, r) => r.roomName ?? <span className="text-ink-muted">Quầy</span> },
    {
      key: 'status',
      title: 'Trạng thái',
      width: 170,
      mobile: 'aside',
      ellipsis: false,
      render: (_, r) => {
        const late = r.status === 'scheduled' && dayjs(r.end).isBefore(now);
        const st = APPOINTMENT_STATUS[r.status];
        return (
          <span className="flex flex-col items-start gap-0.5">
            <StatusTag tone={late ? 'warning' : st.tone} icon={late ? TriangleAlert : STATUS_ICON[r.status]} size="sm">
              {late ? 'Chưa cập nhật kết quả' : st.label}
            </StatusTag>
            {r.status === 'scheduled' && !late && (
              <span className="flex items-center gap-1 text-xs text-ink-muted">
                <BellRing size={11} aria-hidden /> {r.remindedAt ? `Đã nhắc ${dayjs(r.remindedAt).format('HH:mm')}` : `Nhắc lúc ${dayjs(r.start).subtract(1, 'hour').format('HH:mm DD/MM')}`}
              </span>
            )}
            {r.outcome && <span className="line-clamp-2 text-xs text-ink-secondary" title={r.outcome}>{r.outcome}</span>}
            {r.cancelReason && <span className="text-xs text-ink-muted">{r.cancelReason}</span>}
          </span>
        );
      },
    },
  ];

  const scenarioFor = (r: Row): MessageScenario => (r.kind === 'test' ? 'test_reminder' : 'consult_reminder');

  return (
    <>
      <PageHeader
        title="Lịch hẹn"
        subtitle={`Tư vấn & test xếp lớp tại trung tâm · ${currentBranch ? `Chi nhánh ${currentBranch.name}` : 'Toàn hệ thống'}`}
        breadcrumb={[{ title: 'Tuyển sinh' }, { title: 'Lịch hẹn' }]}
        actions={
          editable && (
            <Button icon={CalendarPlus} collapseOnMobile onClick={() => setBooking(true)}>
              Đặt lịch hẹn
            </Button>
          )
        }
      />

      <div className="mb-4 flex flex-wrap items-center gap-2">
        <Segmented<View>
          value={view}
          onChange={setView}
          options={[
            { value: 'upcoming', label: `Sắp tới (${counts.upcoming})` },
            { value: 'today', label: `Hôm nay (${counts.today})` },
            { value: 'past', label: 'Đã qua' },
            { value: 'week', label: 'Lịch tuần' },
          ]}
        />
        <Select className="!w-40" placeholder="Mọi loại hẹn" value={kind} onChange={(v) => setKind(v as AppointmentKind | undefined)} options={(Object.keys(APPOINTMENT_KIND) as AppointmentKind[]).map((k) => ({ value: k, label: APPOINTMENT_KIND[k].short }))} />
        <Select className="!w-52" placeholder="Mọi người tiếp" value={staffId} onChange={(v) => setStaffId(v as string | undefined)} options={[...new Set(appointments.map((a) => a.staffId))].map((id) => ({ value: id, label: employees.find((e) => e.id === id)?.fullName ?? id }))} />
        {counts.unresolved > 0 && (
          <button type="button" onClick={() => setView('past')} className="ml-auto rounded-full bg-warning-50 px-3 py-1 text-[13px] font-medium text-warning-700 hover:bg-warning-100">
            {counts.unresolved} lịch đã qua chưa cập nhật kết quả
          </button>
        )}
      </div>

      {view === 'week' ? (
        <Timetable
          week={week}
          onWeekChange={setWeek}
          startHour={8}
          endHour={21}
          events={rows
            .filter((r) => r.status !== 'cancelled')
            .map((r) => ({
              id: r.id,
              start: r.start,
              end: r.end,
              title: `${APPOINTMENT_KIND[r.kind].short} · ${r.leadName}`,
              subtitle: r.staffName,
              location: r.roomName ?? 'Quầy',
              tone: r.status === 'done' ? 'success' : r.status === 'no_show' ? 'warning' : r.kind === 'test' ? 'primary' : 'info',
            }))}
          onEventClick={(e) => setLeadId(rows.find((r) => r.id === e.id)?.leadId ?? null)}
        />
      ) : (
        <DataTable<Row>
          storageKey="crm-appointments"
          columns={columns}
          data={list}
          rowKey="id"
          unitLabel="lịch hẹn"
          searchKeys={['leadName', 'leadPhone', 'code', 'staffName']}
          rowActions={(r) => {
            const open = r.status === 'scheduled';
            return (
              <>
                <Button icon={MessageCircle} iconOnly size="sm" variant="ghost" tooltip="Nhắn tin cho lead" aria-label="Nhắn tin" onClick={() => setMsg({ target: { kind: 'lead', leadId: r.leadId }, scenario: scenarioFor(r) })} />
                {editable && open && (
                  <>
                    <Button icon={CheckCircle2} iconOnly size="sm" tone="success" variant="soft" tooltip="Đã đến" aria-label="Đã đến" disabled={dayjs(r.start).isAfter(now.add(1, 'hour'))} onClick={() => markDone(r)} />
                    <Dropdown
                      trigger={['click']}
                      menu={{
                        items: [
                          { key: 'resched', icon: <CalendarClock size={15} />, label: 'Đổi lịch', onClick: () => setEditing(r) },
                          { key: 'noshow', icon: <UserX size={15} />, label: 'Không đến', disabled: dayjs(r.start).isAfter(now), onClick: () => markNoShow(r) },
                          { type: 'divider' as const },
                          { key: 'cancel', icon: <XCircle size={15} />, label: 'Hủy lịch', danger: true, onClick: () => cancel(r) },
                        ],
                      }}
                    >
                      <Button icon={Ellipsis} iconOnly size="sm" variant="ghost" aria-label="Thao tác khác" />
                    </Dropdown>
                  </>
                )}
              </>
            );
          }}
          actionsWidth={130}
          empty={{ icon: CalendarClock, title: view === 'upcoming' ? 'Chưa có lịch hẹn sắp tới' : 'Không có lịch hẹn', description: 'Đặt lịch cho lead đến tư vấn hoặc làm test xếp lớp tại trung tâm.', action: editable ? <Button icon={CalendarPlus} onClick={() => setBooking(true)}>Đặt lịch hẹn</Button> : undefined }}
        />
      )}

      <BookAppointmentModal open={booking} onClose={() => setBooking(false)} />
      <BookAppointmentModal open={Boolean(editing)} editing={editing} onClose={() => setEditing(undefined)} />
      <SendMessageModal target={msg?.target ?? null} scenario={msg?.scenario} onClose={() => setMsg(null)} />
      <LeadDetailDrawer leadId={leadId} onClose={() => setLeadId(null)} onOpenLead={setLeadId} />
      {confirmDialog}
    </>
  );
}
