import {
  ArrowLeft,
  Ban,
  Check,
  Download,
  Eye,
  Filter,
  LogIn,
  LogOut,
  Pencil,
  Plus,
  Printer,
  RefreshCw,
  RotateCcw,
  Save,
  Search,
  Send,
  Trash2,
  Upload,
  X,
  type LucideIcon,
} from 'lucide-react';

/** Màu nút theo ý nghĩa */
export type ButtonTone = 'primary' | 'success' | 'warning' | 'danger' | 'neutral';

/** Độ nổi bật: solid (đặc) > soft (nền nhạt) > outline (viền) > ghost (trong suốt) > link */
export type ButtonVariant = 'solid' | 'soft' | 'outline' | 'ghost' | 'link';

interface ActionPreset {
  icon: LucideIcon;
  tone: ButtonTone;
  variant: ButtonVariant;
  label: string;
}

/**
 * Nút theo CHỨC NĂNG → tự có màu, icon, nhãn mặc định.
 * Quy tắc màu:
 *  - primary (xanh dương)  : tạo, lưu, gửi, tìm kiếm, xem — hành động chính/tích cực
 *  - success (xanh lá)     : duyệt, xuất/nhập dữ liệu, check-in — hoàn tất/dữ liệu
 *  - warning (cam)         : sửa, check-out — thay đổi dữ liệu đang có
 *  - danger  (đỏ)          : xóa, từ chối — không hoàn tác được
 *  - neutral (xám)         : hủy, quay lại, lọc, đặt lại, làm mới, in — điều hướng/phụ trợ
 */
export const BUTTON_ACTIONS = {
  create: { icon: Plus, tone: 'primary', variant: 'solid', label: 'Thêm mới' },
  save: { icon: Save, tone: 'primary', variant: 'solid', label: 'Lưu' },
  send: { icon: Send, tone: 'primary', variant: 'solid', label: 'Gửi' },
  search: { icon: Search, tone: 'primary', variant: 'solid', label: 'Tìm kiếm' },
  view: { icon: Eye, tone: 'primary', variant: 'ghost', label: 'Xem' },

  approve: { icon: Check, tone: 'success', variant: 'solid', label: 'Duyệt' },
  export: { icon: Download, tone: 'success', variant: 'outline', label: 'Xuất Excel' },
  import: { icon: Upload, tone: 'success', variant: 'soft', label: 'Nhập file' },
  checkin: { icon: LogIn, tone: 'success', variant: 'solid', label: 'Check-in' },

  edit: { icon: Pencil, tone: 'warning', variant: 'soft', label: 'Sửa' },
  checkout: { icon: LogOut, tone: 'warning', variant: 'solid', label: 'Check-out' },

  delete: { icon: Trash2, tone: 'danger', variant: 'soft', label: 'Xóa' },
  reject: { icon: Ban, tone: 'danger', variant: 'outline', label: 'Từ chối' },

  cancel: { icon: X, tone: 'neutral', variant: 'outline', label: 'Hủy' },
  back: { icon: ArrowLeft, tone: 'neutral', variant: 'ghost', label: 'Quay lại' },
  filter: { icon: Filter, tone: 'neutral', variant: 'outline', label: 'Bộ lọc' },
  reset: { icon: RotateCcw, tone: 'neutral', variant: 'ghost', label: 'Đặt lại' },
  refresh: { icon: RefreshCw, tone: 'neutral', variant: 'outline', label: 'Làm mới' },
  print: { icon: Printer, tone: 'neutral', variant: 'outline', label: 'In' },
  logout: { icon: LogOut, tone: 'neutral', variant: 'ghost', label: 'Đăng xuất' },
} as const satisfies Record<string, ActionPreset>;

export type ButtonAction = keyof typeof BUTTON_ACTIONS;
