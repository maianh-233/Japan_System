import dayjs from 'dayjs';
import { Mail, MessageCircle, Smartphone, type LucideIcon } from 'lucide-react';
import type { ClassInfo } from '@/features/academic/classes/types';
import type { Lead, LeadStudentLink, NotifyChannel, Relationship, Student } from '@/features/crm/types';
import { ACTIVE_ENROLLMENT, GUARDIAN_ROLE } from '../constants';
import { groupByStudent, studyStatusOf, summarizeTuition } from '../studentStore';
import type { Enrollment, Invoice, PortalAccount, StudyStatus, TuitionStatus } from '../types';

/**
 * Phụ huynh / người giám hộ = Lead có liên kết tới học viên với quan hệ KHÁC "self".
 * Không có bảng riêng — mọi thứ suy ra từ crmStore (lead + link) và studentStore (ghi danh, hóa đơn, tài khoản).
 */

export type GuardianRelationship = Exclude<Relationship, 'self'>;

export const NOTIFY_CHANNELS: Record<NotifyChannel, { label: string; icon: LucideIcon }> = {
  zalo: { label: 'Zalo', icon: MessageCircle },
  sms: { label: 'SMS', icon: Smartphone },
  email: { label: 'Email', icon: Mail },
};
export const NOTIFY_CHANNEL_OPTIONS = (Object.keys(NOTIFY_CHANNELS) as NotifyChannel[]).map((k) => ({ value: k, label: NOTIFY_CHANNELS[k].label }));

/** Chọn vai trò của NGƯỜI LIÊN HỆ với học viên (giá trị lưu là quan hệ của học viên với họ) */
export const GUARDIAN_ROLE_OPTIONS = (Object.keys(GUARDIAN_ROLE) as GuardianRelationship[]).map((k) => ({ value: k, label: GUARDIAN_ROLE[k] }));

/** Dưới tuổi này bắt buộc có phụ huynh / người giám hộ */
export const ADULT_AGE = 18;
export const ageOf = (dob?: string) => (dob ? dayjs().diff(dayjs(dob), 'year') : undefined);

export interface GuardianChild {
  student: Student;
  link: LeadStudentLink;
  age?: number;
  study: StudyStatus;
  /** Mã lớp đang học / bảo lưu */
  classes: string[];
  debt: number;
  tuitionStatus?: TuitionStatus;
}

export interface GuardianRow extends Lead {
  children: GuardianChild[];
  account?: PortalAccount;
  /** Tổng còn nợ học phí của các con */
  debt: number;
  overdue: boolean;
  studying: number;
  /** Là người liên hệ chính của ít nhất 1 con */
  isPrimary: boolean;
  isBilling: boolean;
}

interface Source {
  leads: Lead[];
  links: LeadStudentLink[];
  students: Student[];
  enrollments: Enrollment[];
  invoices: Invoice[];
  classes: ClassInfo[];
  accounts: PortalAccount[];
}

/** Dữ liệu từng học viên (lớp, học phí) — dùng chung cho bảng + drawer */
export function buildChildInfo({ enrollments, invoices, classes }: Pick<Source, 'enrollments' | 'invoices' | 'classes'>) {
  const { enr, inv } = groupByStudent(enrollments, invoices);
  const code = new Map(classes.map((c) => [c.id, c.code]));
  return (student: Student, link: LeadStudentLink): GuardianChild => {
    const list = enr.get(student.id) ?? [];
    const t = summarizeTuition(inv.get(student.id) ?? []);
    return {
      student,
      link,
      age: ageOf(student.dob),
      study: studyStatusOf(list),
      classes: list.filter((e) => ACTIVE_ENROLLMENT.includes(e.status)).map((e) => `${code.get(e.classId)}${e.status === 'reserved' ? ' (BL)' : ''}`),
      debt: t.remaining,
      tuitionStatus: t.status,
    };
  };
}

export function buildGuardianRows(src: Source): GuardianRow[] {
  const childInfo = buildChildInfo(src);
  const studentById = new Map(src.students.map((s) => [s.id, s]));
  const linksByLead = new Map<string, LeadStudentLink[]>();
  src.links
    .filter((k) => k.relationship !== 'self')
    .forEach((k) => linksByLead.set(k.leadId, [...(linksByLead.get(k.leadId) ?? []), k]));
  const accounts = new Map(src.accounts.filter((a) => a.ownerType === 'guardian').map((a) => [a.ownerId, a]));

  return src.leads
    .filter((l) => linksByLead.has(l.id))
    .map((l) => {
      const children = (linksByLead.get(l.id) ?? [])
        .map((k) => {
          const st = studentById.get(k.studentId);
          return st ? childInfo(st, k) : null;
        })
        .filter((c): c is GuardianChild => Boolean(c));
      return {
        ...l,
        children,
        account: accounts.get(l.id),
        debt: children.reduce((s, c) => s + c.debt, 0),
        overdue: children.some((c) => c.tuitionStatus === 'overdue'),
        studying: children.filter((c) => c.study === 'studying' || c.study === 'reserved').length,
        isPrimary: children.some((c) => c.link.primary),
        isBilling: children.some((c) => c.link.billing),
      };
    });
}

/** Học viên cần bổ sung người giám hộ: dưới 18 tuổi chưa có ai liên kết, hoặc có người thân nhưng chưa chọn người liên hệ chính */
export function guardianGaps(students: Student[], links: LeadStudentLink[]) {
  const byStudent = new Map<string, LeadStudentLink[]>();
  links.filter((k) => k.relationship !== 'self').forEach((k) => byStudent.set(k.studentId, [...(byStudent.get(k.studentId) ?? []), k]));
  const visible = students.filter((s) => !s.hidden);
  return {
    minorsWithout: visible.filter((s) => (ageOf(s.dob) ?? 99) < ADULT_AGE && !byStudent.has(s.id)),
    noPrimary: visible.filter((s) => byStudent.has(s.id) && !byStudent.get(s.id)!.some((k) => k.primary)),
  };
}
