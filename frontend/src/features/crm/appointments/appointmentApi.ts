/**
 * Nối Lịch hẹn tại trung tâm với backend thật (/api/v1/crm/appointments — CRM-14, CRM-15).
 * Server kiểm trùng phòng / buổi học / người tiếp / giờ dạy / nghỉ dài ngày / lead, nhắc trước 60 phút (job), ghi lịch sử tư vấn.
 */
import dayjs, { type Dayjs } from 'dayjs';
import { api, ApiError } from '@/lib/api/http';
import { fromGuid, toGuid } from '@/lib/api/ids';
import { useAppointmentStore, type Appointment, type AppointmentKind, type AppointmentStatus, type Conflict } from './appointments';

interface ServerAppointment {
  id: string;
  code: string;
  leadId: string;
  leadName: string;
  leadPhone: string;
  kind: AppointmentKind;
  branchId: string;
  roomId?: string | null;
  staffId: string;
  staffName: string;
  start: string;
  end: string;
  status: AppointmentStatus;
  note?: string | null;
  outcome?: string | null;
  createdAt: string;
  remindedAt?: string | null;
  cancelReason?: string | null;
  version: number;
}

const versions = new Map<string, number>();

const toAppointment = (a: ServerAppointment): Appointment => {
  const id = fromGuid(a.id);
  versions.set(id, a.version);
  return {
    id,
    code: a.code,
    leadId: fromGuid(a.leadId),
    kind: a.kind,
    branchId: fromGuid(a.branchId),
    roomId: a.roomId ? fromGuid(a.roomId) : undefined,
    staffId: fromGuid(a.staffId),
    start: a.start,
    end: a.end,
    status: a.status,
    note: a.note ?? undefined,
    outcome: a.outcome ?? undefined,
    createdBy: a.staffName,
    createdAt: a.createdAt,
    remindedAt: a.remindedAt ?? undefined,
    cancelReason: a.cancelReason ?? undefined,
    leadName: a.leadName,
    leadPhone: a.leadPhone,
  };
};

function upsert(a: ServerAppointment) {
  const ap = toAppointment(a);
  useAppointmentStore.setState((s) => ({
    appointments: s.appointments.some((x) => x.id === ap.id) ? s.appointments.map((x) => (x.id === ap.id ? ap : x)) : [...s.appointments, ap],
  }));
  return ap;
}

/** Lịch hẹn trong phạm vi từ 90 ngày trước (lọc ngày / người tiếp tại chỗ). Không có quyền → rỗng */
export async function loadAppointments() {
  try {
    const list = await api<ServerAppointment[]>(`/crm/appointments?from=${encodeURIComponent(dayjs().subtract(90, 'day').startOf('day').toISOString())}`);
    useAppointmentStore.setState({ appointments: list.map(toAppointment) });
  } catch (e) {
    if (e instanceof ApiError && e.status === 403) useAppointmentStore.setState({ appointments: [] });
    else throw e;
  }
}

export interface AppointmentDraft {
  id?: string;
  leadId: string;
  kind: AppointmentKind;
  branchId: string;
  roomId?: string;
  staffId: string;
  start: Dayjs;
  end: Dayjs;
  note?: string;
}

/** Kiểm trùng trên server (cả buổi học, giờ dạy, nghỉ dài ngày) */
export const checkAppointment = async (d: AppointmentDraft): Promise<Conflict[]> =>
  api<Conflict[]>('/crm/appointments/check', {
    body: {
      id: d.id ? toGuid(d.id) : null,
      leadId: toGuid(d.leadId),
      kind: d.kind,
      roomId: d.roomId ? toGuid(d.roomId) : null,
      staffId: toGuid(d.staffId),
      start: d.start.toISOString(),
      end: d.end.toISOString(),
    },
  });

/** Đặt lịch (trùng → lỗi 409 `crm.appointment.conflict`); server ghi lịch sử tư vấn, hẹn test → lead "Đã hẹn test" */
export const createAppointment = async (d: AppointmentDraft) =>
  upsert(
    await api<ServerAppointment>('/crm/appointments', {
      body: {
        leadId: toGuid(d.leadId),
        kind: d.kind,
        branchId: toGuid(d.branchId),
        roomId: d.roomId ? toGuid(d.roomId) : null,
        staffId: toGuid(d.staffId),
        start: d.start.toISOString(),
        end: d.end.toISOString(),
        note: d.note ?? null,
      },
    }),
  );

export const rescheduleAppointment = async (id: string, d: Pick<AppointmentDraft, 'roomId' | 'staffId' | 'start' | 'end' | 'note'>) =>
  upsert(
    await api<ServerAppointment>(`/crm/appointments/${toGuid(id)}`, {
      method: 'PUT',
      body: {
        roomId: d.roomId ? toGuid(d.roomId) : null,
        staffId: toGuid(d.staffId),
        start: d.start.toISOString(),
        end: d.end.toISOString(),
        note: d.note ?? null,
        version: versions.get(id) ?? 0,
      },
    }),
  );

/** Đã đến (kết quả bắt buộc) / không đến / hủy (lý do bắt buộc) */
export const closeAppointment = async (id: string, outcome: 'done' | 'no-show' | 'cancel', text?: string) =>
  upsert(await api<ServerAppointment>(`/crm/appointments/${toGuid(id)}/${outcome}`, { body: { text: text ?? null } }));
