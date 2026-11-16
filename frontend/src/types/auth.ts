/** Cổng giao diện — quyết định chuyển trang sau khi đăng nhập */
export type Portal = 'staff' | 'teacher' | 'student' | 'parent';

export interface Branch {
  id: string;
  code: string;
  name: string;
  address: string;
}

/** ID đặc biệt: xem toàn hệ thống (chỉ tài khoản global) */
export const ALL_BRANCHES = 'all';

export interface SessionUser {
  id: string;
  email: string;
  fullName: string;
  avatarUrl?: string;
  portal: Portal;
  /** Tên vai trò hiển thị: "Super Admin", "Giáo vụ", "Giáo viên"... */
  roleName: string;
  /** global = quản lý tất cả chi nhánh; branch = chỉ chi nhánh được gán */
  scope: 'global' | 'branch';
  /** Chi nhánh được gán (scope = branch) */
  branchIds: string[];
  /** Tài khoản cổng HV/PH liên kết tới tài khoản và hồ sơ trong kho dùng chung */
  portalAccountId?: string;
  portalOwnerId?: string;
  /**
   * Mã quyền: "*" = toàn quyền; "crm.*" = cả module; "crm.leads" = một mục.
   * Tạm thời dạng đơn giản — sẽ thay bằng module → group → permission khi có backend.
   */
  permissions: string[];
}

export interface AppNotification {
  id: string;
  title: string;
  description: string;
  createdAt: string;
  read: boolean;
  tone: 'info' | 'success' | 'warning' | 'danger';
  link?: string;
}
