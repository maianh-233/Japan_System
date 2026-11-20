import { USE_API } from '@/lib/api/config';
import { create } from 'zustand';
import { useStudentStore } from '@/features/student/studentStore';
import { MOCK_PROMOTIONS } from '@/mocks/data/promotions';
import type { PromotionCandidate, PromotionNote } from './types';

/** Store LÊN LỚP giả lập (trong bộ nhớ) — khi có backend thay bằng API, giữ nguyên chữ ký */
interface PromotionState {
  candidates: PromotionCandidate[];
  /** Thêm / cập nhật theo (học viên, lớp A) — mỗi học viên 1 bản ghi / lớp A */
  upsertMany: (list: Omit<PromotionCandidate, 'id' | 'createdAt' | 'notes'>[]) => void;
  update: (id: string, patch: Partial<PromotionCandidate>) => void;
  remove: (id: string) => void;
  /** Ghi chú tư vấn (1 cuộc gọi cho phụ huynh nhiều con → ghi cho cả nhóm) */
  addNote: (ids: string[], note: Omit<PromotionNote, 'id' | 'at'>) => void;
  /** Lớp A kết thúc → giao các học viên trong danh sách cho Sale */
  handOver: (fromClassId: string) => number;
  /** Chốt: ghi danh thật vào lớp B (tạo hóa đơn) */
  confirm: (id: string, toClassId: string, by: string) => boolean;
  markInterest: (id: string, by: 'student' | 'parent') => void;
}

const now = () => new Date().toISOString();

export const usePromotionStore = create<PromotionState>((set, get) => ({
  // Chế độ API: tải theo lớp / cổng (portalApi)
  candidates: USE_API ? [] : MOCK_PROMOTIONS,

  upsertMany: (list) =>
    set((s) => {
      const next = [...s.candidates];
      list.forEach((c) => {
        const i = next.findIndex((x) => x.studentId === c.studentId && x.fromClassId === c.fromClassId);
        if (i >= 0) next[i] = { ...next[i], ...c };
        else next.unshift({ ...c, id: `PR-${Date.now()}-${c.studentId}`, createdAt: now(), notes: [] });
      });
      return { candidates: next };
    }),
  update: (id, patch) => set((s) => ({ candidates: s.candidates.map((c) => (c.id === id ? { ...c, ...patch } : c)) })),
  remove: (id) => set((s) => ({ candidates: s.candidates.filter((c) => c.id !== id) })),
  addNote: (ids, note) =>
    set((s) => ({
      candidates: s.candidates.map((c) =>
        ids.includes(c.id) ? { ...c, status: c.status === 'pending' ? 'consulting' : c.status, notes: [{ ...note, id: `PN-${Date.now()}-${c.id}`, at: now() }, ...c.notes] } : c,
      ),
    })),
  handOver: (fromClassId) => {
    const ids = get().candidates.filter((c) => c.fromClassId === fromClassId && !c.handedOverAt).map((c) => c.id);
    set((s) => ({ candidates: s.candidates.map((c) => (ids.includes(c.id) ? { ...c, handedOverAt: now() } : c)) }));
    return ids.length;
  },
  confirm: (id, toClassId, by) => {
    const c = get().candidates.find((x) => x.id === id);
    if (!c) return false;
    const res = useStudentStore.getState().enroll(c.studentId, toClassId);
    if (!res) return false;
    set((s) => ({
      candidates: s.candidates.map((x) =>
        x.id === id ? { ...x, toClassId, status: 'confirmed', enrollmentId: res.enrollment.id, confirmedAt: now(), confirmedBy: by, handedOverAt: x.handedOverAt ?? now() } : x,
      ),
    }));
    return true;
  },
  markInterest: (id, by) => set((s) => ({ candidates: s.candidates.map((c) => (c.id === id ? { ...c, interest: { at: now(), by } } : c)) })),
}));
