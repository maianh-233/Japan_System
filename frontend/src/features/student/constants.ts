import { CheckCircle2, CircleDashed, PauseCircle, PlayCircle, UserX, type LucideIcon } from 'lucide-react';
import type { Tone } from '@/components/common';
import type { Relationship } from '@/features/crm/types';
import type { EnrollmentStatus, Gender, PaymentMethod, StudyStatus } from './types';

export const GENDERS: Record<Gender, string> = { male: 'Nam', female: 'Nữ', other: 'Khác' };
export const GENDER_OPTIONS = (Object.keys(GENDERS) as Gender[]).map((k) => ({ value: k, label: GENDERS[k] }));

export const STUDY_STATUS: Record<StudyStatus, { label: string; tone: Tone; icon: LucideIcon }> = {
  studying: { label: 'Đang học', tone: 'success', icon: PlayCircle },
  reserved: { label: 'Bảo lưu', tone: 'warning', icon: PauseCircle },
  finished: { label: 'Đã hoàn thành', tone: 'info', icon: CheckCircle2 },
  dropped: { label: 'Đã nghỉ', tone: 'danger', icon: UserX },
  none: { label: 'Chưa xếp lớp', tone: 'neutral', icon: CircleDashed },
};
export const STUDY_STATUS_OPTIONS = (Object.keys(STUDY_STATUS) as StudyStatus[]).map((k) => ({ value: k, label: STUDY_STATUS[k].label }));

export const ENROLLMENT_STATUS: Record<EnrollmentStatus, { label: string; tone: Tone; icon: LucideIcon }> = {
  studying: { label: 'Đang học', tone: 'success', icon: PlayCircle },
  reserved: { label: 'Bảo lưu', tone: 'warning', icon: PauseCircle },
  completed: { label: 'Hoàn thành', tone: 'info', icon: CheckCircle2 },
  transferred: { label: 'Đã chuyển lớp', tone: 'neutral', icon: CircleDashed },
  dropped: { label: 'Đã nghỉ', tone: 'danger', icon: UserX },
};

/** Ghi danh ở các trạng thái này = học viên CÒN trong lớp → không được ẩn */
export const ACTIVE_ENROLLMENT: EnrollmentStatus[] = ['studying', 'reserved'];

export const PAYMENT_METHODS: Record<PaymentMethod, string> = {
  cash: 'Tiền mặt',
  transfer: 'Chuyển khoản',
  vietqr: 'VietQR',
  card: 'Thẻ',
};

/**
 * Quan hệ lưu trên link là "học viên là ... của người liên hệ".
 * Ở hồ sơ học viên cần chiều ngược lại: "người liên hệ là ... của học viên".
 */
export const GUARDIAN_ROLE: Record<Exclude<Relationship, 'self'>, string> = {
  child: 'Phụ huynh',
  sibling: 'Anh / chị / em',
  spouse: 'Vợ / chồng',
  other: 'Người thân khác',
};
