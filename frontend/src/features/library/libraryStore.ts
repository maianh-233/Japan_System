import { create } from 'zustand';
import { MOCK_LIBRARY } from '@/mocks/data/library';
import type { LibraryDoc } from './types';

/** Store THƯ VIỆN TÀI LIỆU giả lập. */

export type DocInput = Omit<LibraryDoc, 'id' | 'stats' | 'uploadedAt' | 'uploadedBy'>;

interface LibraryState {
  docs: LibraryDoc[];
  add: (input: DocInput, by: string) => LibraryDoc;
  update: (id: string, patch: Partial<DocInput>) => void;
  /** Ghi 1 lượt xem (khi mở trình xem) */
  view: (id: string, name: string, role: string) => void;
}

export const useLibraryStore = create<LibraryState>((set) => ({
  docs: MOCK_LIBRARY,

  add: (input, by) => {
    const d: LibraryDoc = { ...input, id: `DOC-${Date.now()}`, uploadedBy: by, uploadedAt: new Date().toISOString(), stats: { views: 0, readers: 0, avgMinutes: 0, daily: Array(14).fill(0), top: [] } };
    set((s) => ({ docs: [d, ...s.docs] }));
    return d;
  },

  update: (id, patch) => set((s) => ({ docs: s.docs.map((d) => (d.id === id ? { ...d, ...patch } : d)) })),

  view: (id, name, role) =>
    set((s) => ({
      docs: s.docs.map((d) => {
        if (d.id !== id) return d;
        const top = [...d.stats.top];
        const i = top.findIndex((t) => t.name === name);
        if (i >= 0) top[i] = { ...top[i], views: top[i].views + 1 };
        else top.push({ name, role, views: 1 });
        const daily = [...d.stats.daily];
        daily[daily.length - 1] += 1;
        return {
          ...d,
          stats: { ...d.stats, views: d.stats.views + 1, readers: d.stats.readers + (i >= 0 ? 0 : 1), lastViewedAt: new Date().toISOString(), daily, top: top.sort((a, b) => b.views - a.views).slice(0, 6) },
        };
      }),
    })),
}));
