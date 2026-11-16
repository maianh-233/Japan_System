import {
  AirVent,
  Camera,
  CheckCircle2,
  CircleAlert,
  CircleDot,
  DoorOpen,
  Hammer,
  Mic,
  Monitor,
  OctagonAlert,
  PauseCircle,
  PenLine,
  Presentation,
  Speaker,
  TriangleAlert,
  Tv,
  Users,
  Wrench,
  type LucideIcon,
} from 'lucide-react';
import type { Tone } from '@/components/common';
import type { Equipment, IssueSeverity, IssueStatus, RoomStatus, RoomType } from './types';

type Meta = { label: string; tone: Tone; icon: LucideIcon };

export const ROOM_STATUS: Record<RoomStatus, Meta & { hint: string }> = {
  active: { label: 'Đang sử dụng', tone: 'success', icon: CheckCircle2, hint: 'Xếp lớp được' },
  maintenance: { label: 'Bảo trì', tone: 'warning', icon: Wrench, hint: 'Tạm thời không xếp lớp mới' },
  inactive: { label: 'Ngừng sử dụng', tone: 'neutral', icon: PauseCircle, hint: 'Không còn dùng để dạy' },
};
export const ROOM_STATUS_OPTIONS = (Object.keys(ROOM_STATUS) as RoomStatus[]).map((k) => ({ value: k, label: ROOM_STATUS[k].label }));

export const ROOM_TYPES: Record<RoomType, { label: string; icon: LucideIcon }> = {
  classroom: { label: 'Phòng học', icon: DoorOpen },
  lab: { label: 'Phòng máy / Lab nghe', icon: Monitor },
  workshop: { label: 'Xưởng thực hành', icon: PenLine },
  meeting: { label: 'Phòng họp / 1-1', icon: Users },
};
export const ROOM_TYPE_OPTIONS = (Object.keys(ROOM_TYPES) as RoomType[]).map((k) => ({ value: k, label: ROOM_TYPES[k].label }));

export const EQUIPMENT: Record<Equipment, { label: string; icon: LucideIcon }> = {
  projector: { label: 'Máy chiếu', icon: Presentation },
  tv: { label: 'TV / màn hình', icon: Tv },
  whiteboard: { label: 'Bảng trắng', icon: PenLine },
  aircon: { label: 'Điều hòa', icon: AirVent },
  speaker: { label: 'Loa', icon: Speaker },
  mic: { label: 'Micro', icon: Mic },
  camera: { label: 'Camera (học hybrid)', icon: Camera },
  computer: { label: 'Máy tính học viên', icon: Monitor },
};
export const EQUIPMENT_OPTIONS = (Object.keys(EQUIPMENT) as Equipment[]).map((k) => ({ value: k, label: EQUIPMENT[k].label }));

/** Giờ mở cửa trung tâm — mẫu số khi tính tỷ lệ lấp đầy */
export const OPEN_HOURS = { start: '08:00', end: '21:30' } as const;
/** Khung giờ cao điểm (tối các ngày trong tuần + cuối tuần cả ngày) — trung tâm tiếng Nhật đông nhất lúc này */
export const PEAK = { start: '17:30', end: '21:30' } as const;

/** Ngưỡng tô màu tỷ lệ lấp đầy */
export const occupancyTone = (pct: number): Tone => (pct >= 85 ? 'danger' : pct >= 60 ? 'success' : pct >= 25 ? 'info' : 'neutral');

export const ISSUE_SEVERITY: Record<IssueSeverity, Meta & { hint: string }> = {
  low: { label: 'Nhẹ', tone: 'neutral', icon: CircleDot, hint: 'Vẫn dạy bình thường, sửa khi tiện' },
  medium: { label: 'Ảnh hưởng', tone: 'warning', icon: TriangleAlert, hint: 'Dạy được nhưng bất tiện — nên sửa trong vài ngày' },
  high: { label: 'Nghiêm trọng', tone: 'danger', icon: OctagonAlert, hint: 'Không dạy được — cần sửa gấp hoặc đổi phòng' },
};
export const ISSUE_SEVERITY_OPTIONS = (Object.keys(ISSUE_SEVERITY) as IssueSeverity[]).map((k) => ({ value: k, label: `${ISSUE_SEVERITY[k].label} — ${ISSUE_SEVERITY[k].hint}` }));

export const ISSUE_STATUS: Record<IssueStatus, Meta> = {
  open: { label: 'Mới báo', tone: 'danger', icon: CircleAlert },
  in_progress: { label: 'Đang sửa', tone: 'warning', icon: Hammer },
  resolved: { label: 'Đã sửa xong', tone: 'success', icon: CheckCircle2 },
};
export const ISSUE_STATUS_OPTIONS = (Object.keys(ISSUE_STATUS) as IssueStatus[]).map((k) => ({ value: k, label: ISSUE_STATUS[k].label }));

/** Báo hỏng chưa xử lý xong */
export const isOpenIssue = (i: { status: IssueStatus }) => i.status !== 'resolved';
