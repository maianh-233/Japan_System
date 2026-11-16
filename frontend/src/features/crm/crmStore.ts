import dayjs from 'dayjs';
import { create } from 'zustand';
import { MOCK_CONSULTATIONS, MOCK_LEADS, MOCK_LINKS, MOCK_STUDENTS } from '@/mocks/data/crm';
import { normalizeText } from '@/lib/text';
import { useStudentStore } from '@/features/student/studentStore';
import { useWorkStore } from '@/features/work/workStore';
import { useAdmissionStore } from './admission/admissionStore';
import { routeLead } from './admission/routing';
import type { Consultation, Lead, LeadStudentLink, Relationship, Student } from './types';

/**
 * Store CRM GIẢ LẬP (trong bộ nhớ, mất khi tải lại trang).
 * Khi có backend: thay các hàm dưới bằng gọi API, giữ nguyên chữ ký để component không phải sửa.
 */

export type LeadInput = Omit<Lead, 'id' | 'code' | 'createdAt' | 'createdBy' | 'hidden' | 'hiddenAt' | 'lastContactAt'>;
export type ConsultationInput = Omit<Consultation, 'id' | 'createdAt'>;
export interface NewStudentInput {
  fullName: string;
  dob?: string;
  phone?: string;
  relationship: Relationship;
  /** Xếp lớp luôn (tùy chọn) → tạo ghi danh + hóa đơn */
  classId?: string;
}

interface CrmState {
  leads: Lead[];
  consultations: Consultation[];
  students: Student[];
  links: LeadStudentLink[];

  addLead: (input: LeadInput, createdBy: string) => Lead;
  updateLead: (id: string, patch: Partial<Lead>) => void;
  setHidden: (id: string, hidden: boolean) => void;
  /** Lead khác có cùng SĐT (chuẩn hóa) */
  findDuplicate: (phone: string, excludeId?: string) => Lead | undefined;

  addConsultation: (input: ConsultationInput) => void;

  createStudentFromLead: (leadId: string, input: NewStudentInput, staffName: string) => Student;
  linkStudent: (leadId: string, studentId: string, relationship: Relationship, staffName: string) => void;
  unlinkStudent: (leadId: string, studentId: string) => void;
  /** Đổi quan hệ / vai trò của người liên hệ với học viên. Đặt `primary` → bỏ cờ chính của người khác cùng học viên */
  updateLink: (leadId: string, studentId: string, patch: Partial<Pick<LeadStudentLink, 'relationship' | 'primary' | 'billing' | 'notify'>>) => void;

  updateStudent: (id: string, patch: Partial<Student>) => void;
  /** Gọi từ UI SAU KHI đã kiểm tra học viên không còn học lớp nào */
  setStudentHidden: (id: string, hidden: boolean) => void;
}

const now = () => new Date().toISOString();
const digits = (p: string) => p.replace(/\D/g, '');
let seq = 1000;

export const useCrmStore = create<CrmState>((set, get) => ({
  leads: MOCK_LEADS,
  consultations: MOCK_CONSULTATIONS,
  students: MOCK_STUDENTS,
  links: MOCK_LINKS,

  addLead: (input, createdBy) => {
    const n = get().leads.length + 1 + seq++;
    // Chưa chọn Sale → chạy quy tắc chia lead (Tuyển sinh › Chia lead & SLA)
    let assigneeId = input.assigneeId;
    const id = `L-${n}`;
    if (!assigneeId) {
      const adm = useAdmissionStore.getState();
      const r = routeLead(input, adm.rules, get().leads, useWorkStore.getState().shifts);
      assigneeId = r.saleId;
      if (r.rule && r.cursor !== undefined) adm.updateRule(r.rule.id, { cursor: r.cursor });
      adm.addLog({ leadId: id, saleId: r.saleId, ruleId: r.rule?.id, reason: r.reason });
    }
    const lead: Lead = {
      ...input,
      assigneeId,
      id,
      code: `LD${String(n).padStart(4, '0')}`,
      createdAt: now(),
      createdBy,
      hidden: false,
    };
    set((s) => ({ leads: [lead, ...s.leads] }));
    return lead;
  },

  updateLead: (id, patch) => set((s) => ({ leads: s.leads.map((l) => (l.id === id ? { ...l, ...patch } : l)) })),

  setHidden: (id, hidden) =>
    set((s) => ({ leads: s.leads.map((l) => (l.id === id ? { ...l, hidden, hiddenAt: hidden ? now() : undefined } : l)) })),

  findDuplicate: (phone, excludeId) => {
    const p = digits(phone);
    return get().leads.find((l) => l.id !== excludeId && digits(l.phone) === p);
  },

  addConsultation: (input) => {
    const c: Consultation = { ...input, id: `C-${Date.now()}`, createdAt: now() };
    set((s) => ({
      consultations: [c, ...s.consultations],
      leads: s.leads.map((l) =>
        l.id === input.leadId
          ? { ...l, lastContactAt: c.createdAt, nextFollowUpAt: input.nextFollowUpAt ?? l.nextFollowUpAt, status: l.status === 'new' ? 'consulting' : l.status }
          : l,
      ),
    }));
  },

  createStudentFromLead: (leadId, input, staffName) => {
    const lead = get().leads.find((l) => l.id === leadId)!;
    const student: Student = {
      id: `ST-${Date.now()}`,
      code: `HV${1000 + get().students.length + seq++}`,
      fullName: input.fullName,
      dob: input.dob,
      phone: input.phone,
      branchId: lead.branchId,
      joinedAt: now(),
      hidden: false,
    };
    if (input.classId) useStudentStore.getState().enroll(student.id, input.classId);
    set((s) => ({
      students: [...s.students, student],
      links: [...s.links, { leadId, studentId: student.id, relationship: input.relationship, createdAt: now() }],
      leads: s.leads.map((l) => (l.id === leadId ? { ...l, status: 'won' } : l)),
      consultations: [
        {
          id: `C-${Date.now()}`,
          leadId,
          channel: 'meeting',
          result: 'enrolled',
          content: `Tạo hồ sơ học viên ${student.code} — ${student.fullName} (${input.relationship === 'self' ? 'chính người liên hệ' : 'người thân'}).`,
          staffName,
          createdAt: now(),
          system: true,
        },
        ...s.consultations,
      ],
    }));
    return student;
  },

  linkStudent: (leadId, studentId, relationship, staffName) => {
    if (get().links.some((k) => k.leadId === leadId && k.studentId === studentId)) return;
    const st = get().students.find((x) => x.id === studentId);
    set((s) => ({
      links: [...s.links, { leadId, studentId, relationship, createdAt: now() }],
      consultations: [
        {
          id: `C-${Date.now()}`,
          leadId,
          channel: 'meeting',
          result: 'enrolled',
          content: `Liên kết học viên có sẵn ${st?.code} — ${st?.fullName}.`,
          staffName,
          createdAt: now(),
          system: true,
        },
        ...s.consultations,
      ],
    }));
  },

  unlinkStudent: (leadId, studentId) =>
    set((s) => ({ links: s.links.filter((k) => !(k.leadId === leadId && k.studentId === studentId)) })),

  updateLink: (leadId, studentId, patch) =>
    set((s) => ({
      links: s.links.map((k) => {
        if (k.studentId !== studentId) return k;
        if (k.leadId === leadId) return { ...k, ...patch };
        return patch.primary ? { ...k, primary: false } : k;
      }),
    })),

  updateStudent: (id, patch) => set((s) => ({ students: s.students.map((x) => (x.id === id ? { ...x, ...patch } : x)) })),

  setStudentHidden: (id, hidden) =>
    set((s) => ({ students: s.students.map((x) => (x.id === id ? { ...x, hidden, hiddenAt: hidden ? now() : undefined } : x)) })),
}));

// ---------------- Selector tiện ích ----------------

/** Lead mới, chưa có lần liên hệ nào, tạo quá X phút */
export function isSlaOverdue(lead: Lead, minutes: number): boolean {
  return lead.status === 'new' && !lead.lastContactAt && dayjs().diff(dayjs(lead.createdAt), 'minute') >= minutes;
}

/** Hẹn gọi lại đã quá hạn (lead chưa chốt / chưa mất) */
export function isFollowUpOverdue(lead: Lead): boolean {
  return Boolean(lead.nextFollowUpAt) && dayjs(lead.nextFollowUpAt).isBefore(dayjs()) && !['won', 'lost'].includes(lead.status);
}

export function searchStudents(students: Student[], keyword: string): Student[] {
  const k = normalizeText(keyword);
  return students.filter((s) => normalizeText(`${s.fullName} ${s.code} ${s.phone ?? ''}`).includes(k));
}
