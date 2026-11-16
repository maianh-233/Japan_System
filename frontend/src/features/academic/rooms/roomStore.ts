import { create } from 'zustand';
import { MOCK_ROOM_ISSUES, MOCK_ROOMS } from '@/mocks/data/rooms';
import { useClassStore } from '../classes/classStore';
import type { Room, RoomIssue, RoomStatus } from './types';

/**
 * Store PHÒNG HỌC GIẢ LẬP (trong bộ nhớ).
 * Lớp / buổi học trỏ tới phòng theo TÊN trong chi nhánh → đổi tên phòng sẽ cập nhật theo ở lớp & buổi học.
 * Khi có backend: thay hàm bằng gọi API, giữ nguyên chữ ký.
 */

export type RoomInput = Omit<Room, 'id' | 'createdAt'>;
export type IssueInput = Pick<RoomIssue, 'roomId' | 'equipment' | 'title' | 'description' | 'severity' | 'reportedBy'>;

interface RoomState {
  rooms: Room[];
  addRoom: (input: RoomInput) => Room;
  updateRoom: (id: string, patch: Partial<RoomInput>) => void;
  setStatus: (id: string, status: RoomStatus, opts?: { reason?: string; until?: string }) => void;
  isNameTaken: (branchId: string, name: string, excludeId?: string) => boolean;

  issues: RoomIssue[];
  reportIssue: (input: IssueInput) => RoomIssue;
  /** Nhận xử lý: giao người / đơn vị sửa + hẹn ngày xong */
  startIssue: (id: string, opts: { assignee: string; dueDate?: string }) => void;
  resolveIssue: (id: string, opts: { resolution: string; cost?: number }) => void;
  /** Mở lại khi sửa xong nhưng vẫn hỏng */
  reopenIssue: (id: string) => void;
}

const norm = (s: string) => s.trim().toLowerCase();

export const useRoomStore = create<RoomState>((set, get) => ({
  rooms: MOCK_ROOMS,

  addRoom: (input) => {
    const room: Room = { ...input, name: input.name.trim(), id: `R-${Date.now()}`, createdAt: new Date().toISOString() };
    set((s) => ({ rooms: [...s.rooms, room] }));
    return room;
  },

  updateRoom: (id, patch) => {
    const old = get().rooms.find((r) => r.id === id);
    if (!old) return;
    const next = { ...old, ...patch, name: (patch.name ?? old.name).trim() };
    set((s) => ({ rooms: s.rooms.map((r) => (r.id === id ? next : r)) }));
    // Đổi tên → cập nhật lớp + buổi học đang trỏ tới tên cũ (cùng chi nhánh)
    if (next.name !== old.name) {
      useClassStore.setState((s) => {
        const inBranch = new Set(s.classes.filter((c) => c.branchId === old.branchId).map((c) => c.id));
        return {
          classes: s.classes.map((c) => (inBranch.has(c.id) && c.room === old.name ? { ...c, room: next.name } : c)),
          sessions: s.sessions.map((x) => (inBranch.has(x.classId) && x.room === old.name ? { ...x, room: next.name } : x)),
        };
      });
    }
  },

  setStatus: (id, status, opts) =>
    set((s) => ({
      rooms: s.rooms.map((r) =>
        r.id === id
          ? {
              ...r,
              status,
              maintenanceReason: status === 'maintenance' ? opts?.reason : undefined,
              maintenanceUntil: status === 'maintenance' ? opts?.until : undefined,
            }
          : r,
      ),
    })),

  issues: MOCK_ROOM_ISSUES,

  reportIssue: (input) => {
    const issue: RoomIssue = { ...input, id: `RI-${Date.now()}`, status: 'open', reportedAt: new Date().toISOString() };
    set((s) => ({ issues: [issue, ...s.issues] }));
    return issue;
  },

  startIssue: (id, opts) =>
    set((s) => ({ issues: s.issues.map((i) => (i.id === id ? { ...i, status: 'in_progress', assignee: opts.assignee, dueDate: opts.dueDate, startedAt: new Date().toISOString() } : i)) })),

  resolveIssue: (id, opts) =>
    set((s) => ({
      issues: s.issues.map((i) =>
        i.id === id ? { ...i, status: 'resolved', resolution: opts.resolution, cost: opts.cost, resolvedAt: new Date().toISOString(), startedAt: i.startedAt ?? new Date().toISOString() } : i,
      ),
    })),

  reopenIssue: (id) =>
    set((s) => ({ issues: s.issues.map((i) => (i.id === id ? { ...i, status: i.assignee ? 'in_progress' : 'open', resolvedAt: undefined, resolution: undefined, cost: undefined } : i)) })),

  isNameTaken: (branchId, name, excludeId) => get().rooms.some((r) => r.id !== excludeId && r.branchId === branchId && norm(r.name) === norm(name)),
}));

/**
 * Lựa chọn phòng cho ô "Phòng học" (form lớp, đổi phòng buổi học).
 * Phòng bảo trì / ngừng dùng bị khóa — trừ phòng đang được chọn sẵn (để không mất giá trị cũ).
 */
export function useRoomOptions(branchId: string | undefined, current?: string) {
  const rooms = useRoomStore((s) => s.rooms);
  return rooms
    .filter((r) => r.branchId === branchId && (r.status !== 'inactive' || r.name === current))
    .sort((a, b) => a.name.localeCompare(b.name, 'vi', { numeric: true }))
    .map((r) => ({
      value: r.name,
      label: `${r.name} · ${r.capacity} chỗ${r.status === 'maintenance' ? ' · đang bảo trì' : r.status === 'inactive' ? ' · ngừng dùng' : ''}`,
      disabled: r.status !== 'active' && r.name !== current,
    }));
}
