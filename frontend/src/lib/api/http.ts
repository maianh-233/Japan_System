import { API_BASE } from './config';

/**
 * Gọi API backend.
 * - Access token giữ TRONG BỘ NHỚ (không lưu localStorage → script lạ khó lấy); tải lại trang thì xin lại bằng refresh token.
 * - Refresh token là cookie HttpOnly do server đặt — JavaScript không đọc được, trình duyệt tự gửi khi gọi /system/auth/*.
 * - Gặp 401 → tự làm mới phiên 1 lần (nhiều request cùng lúc chỉ làm mới 1 lần) rồi gửi lại; vẫn 401 → báo hết phiên.
 */

/** Lỗi từ server (ProblemDetails): `code` ổn định để xử lý, `message` tiếng Việt để hiển thị */
export class ApiError extends Error {
  constructor(
    message: string,
    public code: string,
    public status = 0,
    /** Lỗi theo trường khi validate: { name: ["Chưa nhập tên"] } */
    public fields?: Record<string, string[]>,
  ) {
    super(message);
  }
}

interface TokenResponse {
  accessToken: string;
  accessTokenExpiresAt: string;
}

let accessToken: string | null = null;
let refreshing: Promise<boolean> | null = null;
let onSessionExpired: (() => void) | null = null;

export const setAccessToken = (token: string | null) => {
  accessToken = token;
};

export const hasAccessToken = () => accessToken !== null;

/** Đăng ký hành động khi phiên hết hạn hẳn (vd đăng xuất về trang đăng nhập) */
export const onExpired = (fn: () => void) => {
  onSessionExpired = fn;
};

async function toError(res: Response): Promise<ApiError> {
  let body: { title?: string; code?: string; errors?: Record<string, string[]> } = {};
  try {
    body = await res.json();
  } catch {
    /* không phải JSON */
  }
  if (res.status === 429 && !body.title) body.title = 'Bạn thao tác quá nhanh, vui lòng không bấm liên tục.';
  const fieldMessage = body.errors ? Object.values(body.errors).flat()[0] : undefined;
  return new ApiError(fieldMessage ?? body.title ?? `Lỗi ${res.status}`, body.code ?? `http.${res.status}`, res.status, body.errors);
}

/** Đổi cookie refresh lấy access token mới. Trả false nếu không còn phiên. */
export function refreshSession(): Promise<boolean> {
  refreshing ??= (async () => {
    try {
      const res = await fetch(`${API_BASE}/system/auth/refresh`, { method: 'POST', credentials: 'include' });
      if (!res.ok) {
        accessToken = null;
        return false;
      }
      const data = (await res.json()) as TokenResponse;
      accessToken = data.accessToken;
      lastSignIn = data;
      return true;
    } catch {
      return false;
    } finally {
      setTimeout(() => (refreshing = null), 0);
    }
  })();
  return refreshing;
}

/** Kết quả đăng nhập / làm mới gần nhất (chứa `user`) — authApi đọc lại sau refreshSession() */
export let lastSignIn: unknown = null;

export interface RequestOptions {
  method?: string;
  body?: unknown;
  /** Header thêm (vd Idempotency-Key) */
  headers?: Record<string, string>;
  /** Không tự làm mới khi 401 (dùng cho chính các API đăng nhập) */
  noRefresh?: boolean;
}

export async function api<T = void>(path: string, opts: RequestOptions = {}): Promise<T> {
  // FormData (tải file) → trình duyệt tự đặt Content-Type multipart kèm boundary
  const isForm = opts.body instanceof FormData;
  const send = () =>
    fetch(`${API_BASE}${path}`, {
      method: opts.method ?? (opts.body === undefined ? 'GET' : 'POST'),
      credentials: 'include',
      headers: {
        Accept: 'application/json',
        ...(opts.body !== undefined && !isForm ? { 'Content-Type': 'application/json' } : {}),
        ...(accessToken ? { Authorization: `Bearer ${accessToken}` } : {}),
        ...opts.headers,
      },
      body: opts.body === undefined ? undefined : isForm ? (opts.body as FormData) : JSON.stringify(opts.body),
    });

  let res: Response;
  try {
    res = await send();
  } catch {
    throw new ApiError('Không kết nối được máy chủ. Kiểm tra mạng hoặc backend đã chạy chưa.', 'network');
  }

  if (res.status === 401 && !opts.noRefresh) {
    if (await refreshSession()) res = await send();
    else {
      onSessionExpired?.();
      throw new ApiError('Phiên đăng nhập đã hết hạn, vui lòng đăng nhập lại.', 'auth.session_expired', 401);
    }
  }

  if (!res.ok) throw await toError(res);
  if (res.status === 204) return undefined as T;
  const text = await res.text();
  return (text ? JSON.parse(text) : undefined) as T;
}
