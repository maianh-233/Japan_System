import { create } from 'zustand';
import { createJSONStorage, persist, type StateStorage } from 'zustand/middleware';
import { ALL_BRANCHES, type SessionUser } from '@/types/auth';
import { USE_API } from '@/lib/api/config';
import { api, setAccessToken } from '@/lib/api/http';

interface AuthState {
  user: SessionUser | null;
  /** Chi nhánh đang làm việc. "all" = toàn hệ thống (chỉ tài khoản global) */
  branchId: string;
  /** Ghi nhớ đăng nhập: true → localStorage, false → sessionStorage (đóng trình duyệt là hết) */
  remember: boolean;
  signIn: (user: SessionUser, remember: boolean) => void;
  signOut: () => void;
  setBranch: (branchId: string) => void;
}

const KEY = 'jp-center-auth';

/** Ghi vào local hoặc session tùy "Ghi nhớ đăng nhập"; mọi truy cập đều bọc try/catch */
const flexibleStorage: StateStorage = {
  getItem: (name) => {
    try {
      return localStorage.getItem(name) ?? sessionStorage.getItem(name);
    } catch {
      return null;
    }
  },
  setItem: (name, value) => {
    try {
      const remember = (JSON.parse(value) as { state?: { remember?: boolean } }).state?.remember ?? true;
      (remember ? localStorage : sessionStorage).setItem(name, value);
      (remember ? sessionStorage : localStorage).removeItem(name);
    } catch {
      /* trình duyệt chặn lưu trữ — phiên chỉ còn trong bộ nhớ */
    }
  },
  removeItem: (name) => {
    try {
      localStorage.removeItem(name);
      sessionStorage.removeItem(name);
    } catch {
      /* bỏ qua */
    }
  },
};

/** Chi nhánh mặc định sau khi đăng nhập */
export function defaultBranchOf(user: SessionUser): string {
  return user.scope === 'global' ? ALL_BRANCHES : (user.branchIds[0] ?? ALL_BRANCHES);
}

export const useAuthStore = create<AuthState>()(
  persist(
    (set) => ({
      user: null,
      branchId: ALL_BRANCHES,
      remember: true,
      signIn: (user, remember) => set({ user, remember, branchId: defaultBranchOf(user) }),
      signOut: () => {
        // API thật: thu hồi phiên trên server + xóa cookie refresh; lỗi mạng thì vẫn đăng xuất ở trình duyệt
        if (USE_API) void api('/system/auth/logout', { method: 'POST', noRefresh: true }).catch(() => undefined).finally(() => setAccessToken(null));
        set({ user: null, branchId: ALL_BRANCHES });
      },
      setBranch: (branchId) => set({ branchId }),
    }),
    {
      name: KEY,
      storage: createJSONStorage(() => flexibleStorage),
      partialize: (s) => ({ user: s.user, branchId: s.branchId, remember: s.remember }),
    },
  ),
);
