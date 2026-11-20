/**
 * Nối Phòng học với backend thật (/api/v1/academic/rooms — user story 07 đợt 1).
 * Đọc rồi ĐỔ VÀO useRoomStore (Id mock R-CG-201… nhờ seedIds) — trang khác (lịch hẹn CRM, báo cáo) vẫn đọc store như cũ.
 * Mỗi thao tác ghi: chế độ API gọi server rồi cập nhật store; chế độ mock gọi thẳng store (giữ hành vi cũ).
 */
import { USE_API } from '@/lib/api/config';
import { api, ApiError } from '@/lib/api/http';
import { fromGuid, toGuid } from '@/lib/api/ids';
import { useRoomStore, type IssueInput, type RoomInput } from './roomStore';
import type { Equipment, IssueSeverity, IssueStatus, Room, RoomIssue, RoomStatus, RoomType } from './types';

interface ServerRoom {
  id: string;
  branchId: string;
  name: string;
  floor?: string;
  type: RoomType;
  capacity: number;
  area?: number;
  equipment: Equipment[];
  status: RoomStatus;
  maintenanceReason?: string;
  maintenanceUntil?: string;
  note?: string;
  openClasses: string[];
  createdAt: string;
  rowVersion: number;
}

interface ServerOption {
  id: string;
  branchId: string;
  name: string;
  capacity: number;
  status: RoomStatus;
  maintenanceReason?: string;
}

interface ServerIssue {
  id: string;
  roomId: string;
  equipment?: Equipment;
  title: string;
  description?: string;
  severity: IssueSeverity;
  status: IssueStatus;
  reportedBy: string;
  reportedAt: string;
  assignee?: string;
  dueDate?: string;
  startedAt?: string;
  resolvedAt?: string;
  resolution?: string;
  cost?: number;
}

const versions = new Map<string, number>();

function toRoom(r: ServerRoom): Room {
  const id = fromGuid(r.id);
  versions.set(id, r.rowVersion);
  return {
    id,
    branchId: fromGuid(r.branchId),
    name: r.name,
    floor: r.floor,
    type: r.type,
    capacity: r.capacity,
    area: r.area,
    equipment: r.equipment,
    status: r.status,
    maintenanceReason: r.maintenanceReason,
    maintenanceUntil: r.maintenanceUntil,
    note: r.note,
    createdAt: r.createdAt,
  };
}

const toIssue = (i: ServerIssue): RoomIssue => ({ ...i, id: fromGuid(i.id), roomId: fromGuid(i.roomId) });

function upsertRoom(r: ServerRoom): Room {
  const room = toRoom(r);
  useRoomStore.setState((s) => ({
    rooms: s.rooms.some((x) => x.id === room.id) ? s.rooms.map((x) => (x.id === room.id ? room : x)) : [...s.rooms, room],
  }));
  return room;
}

function upsertIssue(i: ServerIssue) {
  const issue = toIssue(i);
  useRoomStore.setState((s) => ({
    issues: s.issues.some((x) => x.id === issue.id) ? s.issues.map((x) => (x.id === issue.id ? issue : x)) : [issue, ...s.issues],
  }));
}

/** Tải phòng + báo hỏng. Không có quyền trang Phòng học → danh sách rút gọn (/options) cho ô chọn phòng ở trang khác */
export async function loadRooms() {
  try {
    const [rooms, issues] = await Promise.all([api<ServerRoom[]>('/academic/rooms'), api<ServerIssue[]>('/academic/rooms/issues')]);
    useRoomStore.setState({ rooms: rooms.map(toRoom), issues: issues.map(toIssue) });
  } catch (e) {
    if (!(e instanceof ApiError) || e.status !== 403) throw e;
    const options = await api<ServerOption[]>('/academic/rooms/options');
    useRoomStore.setState({
      rooms: options.map((o) => ({
        id: fromGuid(o.id),
        branchId: fromGuid(o.branchId),
        name: o.name,
        type: 'classroom',
        capacity: o.capacity,
        equipment: [],
        status: o.status,
        maintenanceReason: o.maintenanceReason,
        createdAt: '',
      })),
      issues: [],
    });
  }
}

const body = (v: RoomInput) => ({
  branchId: toGuid(v.branchId),
  name: v.name,
  floor: v.floor || null,
  type: v.type,
  capacity: v.capacity,
  area: v.area ?? null,
  equipment: v.equipment ?? [],
  note: v.note || null,
});

export async function createRoom(v: RoomInput): Promise<Room> {
  if (!USE_API) return useRoomStore.getState().addRoom(v);
  return upsertRoom(await api<ServerRoom>('/academic/rooms', { body: body(v) }));
}

/** Sửa phòng — server chặn sửa đè (rowVersion), không đổi chi nhánh, sức chứa ≥ sĩ số max của lớp đang dùng */
export async function updateRoom(id: string, v: RoomInput) {
  if (!USE_API) return useRoomStore.getState().updateRoom(id, v);
  upsertRoom(await api<ServerRoom>(`/academic/rooms/${toGuid(id)}`, { method: 'PUT', body: { ...body(v), rowVersion: versions.get(id) ?? 0 } }));
}

/** Bảo trì (lý do + hạn) · dùng lại · ngừng (server chặn khi còn lớp đang mở dùng phòng) */
export async function setRoomStatus(id: string, status: RoomStatus, opts?: { reason?: string; until?: string }) {
  if (!USE_API) return useRoomStore.getState().setStatus(id, status, opts);
  upsertRoom(await api<ServerRoom>(`/academic/rooms/${toGuid(id)}/status`, { body: { status, reason: opts?.reason ?? null, until: opts?.until ?? null } }));
}

/** Báo hỏng — người báo lấy theo tài khoản đăng nhập (chế độ API) */
export async function reportIssue(v: IssueInput) {
  if (!USE_API) return void useRoomStore.getState().reportIssue(v);
  upsertIssue(
    await api<ServerIssue>('/academic/rooms/issues', {
      body: { roomId: toGuid(v.roomId), equipment: v.equipment ?? null, title: v.title, description: v.description ?? null, severity: v.severity },
    }),
  );
}

export async function startIssue(id: string, opts: { assignee: string; dueDate?: string }) {
  if (!USE_API) return useRoomStore.getState().startIssue(id, opts);
  upsertIssue(await api<ServerIssue>(`/academic/rooms/issues/${toGuid(id)}/start`, { body: { assignee: opts.assignee, dueDate: opts.dueDate ?? null } }));
}

export async function resolveIssue(id: string, opts: { resolution: string; cost?: number }) {
  if (!USE_API) return useRoomStore.getState().resolveIssue(id, opts);
  upsertIssue(await api<ServerIssue>(`/academic/rooms/issues/${toGuid(id)}/resolve`, { body: { resolution: opts.resolution, cost: opts.cost ?? null } }));
}

export async function reopenIssue(id: string) {
  if (!USE_API) return useRoomStore.getState().reopenIssue(id);
  upsertIssue(await api<ServerIssue>(`/academic/rooms/issues/${toGuid(id)}/reopen`, { method: 'POST' }));
}

/** Tên phòng (frontend lưu phòng của lớp / buổi theo TÊN trong chi nhánh) → Id cho API */
export const roomIdOf = (branchId: string, name?: string) =>
  name ? useRoomStore.getState().rooms.find((r) => r.branchId === branchId && r.name === name)?.id : undefined;
