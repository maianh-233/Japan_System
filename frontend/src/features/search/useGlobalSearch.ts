import { useMemo } from 'react';
import { ContactRound, FileText, GraduationCap, Receipt, School, Target, UserCog, UserRound, type LucideIcon } from 'lucide-react';
import { STAFF_MODULES } from '@/config/navigation';
import { useClassStore } from '@/features/academic/classes/classStore';
import { useCrmStore } from '@/features/crm/crmStore';
import { useInvoiceRows } from '@/features/finance/financeUtils';
import { useHrStore } from '@/features/hr/hrStore';
import { useBranches } from '@/features/system/useBranches';
import { useAuth } from '@/hooks/useAuth';
import { formatCurrency } from '@/lib/format';
import { normalizeText } from '@/lib/text';

/**
 * TÌM KIẾM TOÀN HỆ THỐNG (Ctrl+K) — cổng nhân viên.
 *  - Chỉ tìm trong nhóm dữ liệu người dùng CÓ QUYỀN XEM (theo mã quyền của trang tương ứng)
 *  - Chỉ trong CHI NHÁNH được gán (tài khoản toàn hệ thống → mọi chi nhánh)
 *  - Không phân biệt dấu / hoa thường; SĐT so theo chữ số (gõ "0912 345" vẫn ra)
 * Khi có backend: thay phần lọc bằng 1 API /search?q=, giữ nguyên kiểu kết quả.
 */

export type SearchGroup = 'page' | 'lead' | 'student' | 'guardian' | 'class' | 'invoice' | 'employee';

export interface SearchResult {
  key: string;
  group: SearchGroup;
  title: string;
  subtitle?: string;
  /** Tên chi nhánh (khi người dùng xem nhiều chi nhánh) */
  branch?: string;
  /** Ghi chú trạng thái ngắn: "Đã ẩn", "Đã nghỉ"... */
  badge?: string;
  path: string;
  icon: LucideIcon;
  /** Điểm khớp — cao hơn xếp trước */
  score: number;
}

export const SEARCH_GROUPS: Record<SearchGroup, string> = {
  page: 'Trang',
  lead: 'Lead',
  student: 'Học viên',
  guardian: 'Phụ huynh / Người giám hộ',
  class: 'Lớp học',
  invoice: 'Hóa đơn',
  employee: 'Nhân viên & giáo viên',
};

const PER_GROUP = 5;
const digitsOf = (s?: string) => (s ?? '').replace(/\D/g, '');

/** Điểm khớp của 1 bản ghi: 3 = trùng khít / bắt đầu bằng, 2 = chứa, 0 = không khớp */
function scoreOf(q: string, qDigits: string, fields: (string | undefined)[], phones: (string | undefined)[] = []): number {
  let best = 0;
  for (const f of fields) {
    const n = normalizeText(f);
    if (!n) continue;
    if (n === q || n.startsWith(q)) best = Math.max(best, 3);
    else if (n.includes(q) || n.split(/\s+/).some((w) => w.startsWith(q))) best = Math.max(best, 2);
  }
  if (qDigits.length >= 3) {
    for (const p of phones) {
      const d = digitsOf(p);
      if (d && d.includes(qDigits)) best = Math.max(best, d.startsWith(qDigits) ? 3 : 2);
    }
  }
  return best;
}

export function useGlobalSearch(query: string): SearchResult[] {
  const { user, can } = useAuth();
  const leads = useCrmStore((s) => s.leads);
  const students = useCrmStore((s) => s.students);
  const links = useCrmStore((s) => s.links);
  const classes = useClassStore((s) => s.classes);
  const employees = useHrStore((s) => s.employees);
  const invoiceRows = useInvoiceRows();
  const branches = useBranches();

  return useMemo(() => {
    const q = normalizeText(query);
    if (q.length < 2) return [];
    const qDigits = digitsOf(query);

    const global = user?.scope === 'global';
    const allowed = new Set(user?.branchIds ?? []);
    const inScope = (branchId?: string) => global || !branchId || allowed.has(branchId);
    const multiBranch = global || allowed.size > 1;
    const branchName = (id?: string) => (multiBranch && id ? branches.find((b) => b.id === id)?.name : undefined);

    const out: SearchResult[] = [];
    const push = (list: SearchResult[]) => out.push(...list.sort((a, b) => b.score - a.score).slice(0, PER_GROUP));

    // ---- Trang (menu) ----
    push(
      STAFF_MODULES.flatMap((m) =>
        m.items
          .filter((i) => can(i.permission))
          .map((i) => ({ i, m, score: scoreOf(q, '', [i.label, `${m.label} ${i.label}`]) }))
          .filter((x) => x.score > 0)
          .map(({ i, m, score }) => ({ key: `page:${i.path}`, group: 'page' as const, title: i.label, subtitle: m.label, path: i.path, icon: i.icon, score })),
      ),
    );

    // ---- Lead ----
    if (can('crm.leads'))
      push(
        leads
          .filter((l) => inScope(l.branchId))
          .map((l) => ({ l, score: scoreOf(q, qDigits, [l.fullName, l.code, l.email], [l.phone]) }))
          .filter((x) => x.score > 0)
          .map(({ l, score }) => ({
            key: `lead:${l.id}`,
            group: 'lead' as const,
            title: l.fullName,
            subtitle: `${l.code} · ${l.phone}`,
            branch: branchName(l.branchId),
            badge: l.hidden ? 'Đã ẩn' : undefined,
            path: `/admin/crm/leads?open=${l.id}`,
            icon: Target,
            score: score - (l.hidden ? 1 : 0),
          })),
      );

    // ---- Học viên ----
    if (can('students.profiles'))
      push(
        students
          .filter((s) => inScope(s.branchId))
          .map((s) => ({ s, score: scoreOf(q, qDigits, [s.fullName, s.code, s.email], [s.phone]) }))
          .filter((x) => x.score > 0)
          .map(({ s, score }) => ({
            key: `student:${s.id}`,
            group: 'student' as const,
            title: s.fullName,
            subtitle: [s.code, s.phone].filter(Boolean).join(' · '),
            branch: branchName(s.branchId),
            badge: s.hidden ? 'Đã ẩn' : undefined,
            path: `/admin/students/profiles?open=${s.id}`,
            icon: GraduationCap,
            score: score - (s.hidden ? 1 : 0),
          })),
      );

    // ---- Phụ huynh: lead có liên kết với học viên không phải chính mình ----
    if (can('students.contacts')) {
      const guardianIds = new Set(links.filter((k) => k.relationship !== 'self').map((k) => k.leadId));
      push(
        leads
          .filter((l) => guardianIds.has(l.id) && inScope(l.branchId))
          .map((l) => ({ l, score: scoreOf(q, qDigits, [l.fullName, l.email], [l.phone]) }))
          .filter((x) => x.score > 0)
          .map(({ l, score }) => {
            const kids = links.filter((k) => k.leadId === l.id && k.relationship !== 'self').map((k) => students.find((s) => s.id === k.studentId)?.fullName).filter(Boolean);
            return {
              key: `guardian:${l.id}`,
              group: 'guardian' as const,
              title: l.fullName,
              subtitle: `${l.phone}${kids.length ? ` · PH của ${kids.join(', ')}` : ''}`,
              branch: branchName(l.branchId),
              path: `/admin/students/contacts?open=${l.id}`,
              icon: ContactRound,
              score,
            };
          }),
      );
    }

    // ---- Lớp học ----
    if (can('academic.classes'))
      push(
        classes
          .filter((c) => inScope(c.branchId))
          .map((c) => ({ c, score: scoreOf(q, '', [c.code, c.code.replace(/^GV[VN]*_/, ''), c.name, c.courseLabel]) }))
          .filter((x) => x.score > 0)
          .map(({ c, score }) => ({
            key: `class:${c.id}`,
            group: 'class' as const,
            title: c.code,
            subtitle: c.name,
            branch: branchName(c.branchId),
            badge: c.status === 'cancelled' ? 'Đã hủy' : c.status === 'finished' ? 'Đã kết thúc' : undefined,
            path: `/admin/academic/classes/${c.id}`,
            icon: School,
            score: score - (c.status === 'cancelled' || c.status === 'finished' ? 1 : 0),
          })),
      );

    // ---- Hóa đơn (theo mã HĐ, hoặc tên / SĐT học viên) ----
    if (can('finance.invoices'))
      push(
        invoiceRows
          .filter((r) => inScope(r.branchId))
          .map((r) => ({ r, score: scoreOf(q, qDigits, [r.invoice.code, r.student?.fullName], [r.student?.phone]) }))
          .filter((x) => x.score > 0)
          .map(({ r, score }) => ({
            key: `invoice:${r.id}`,
            group: 'invoice' as const,
            title: `${r.invoice.code} — ${r.student?.fullName ?? '—'}`,
            subtitle: [r.cls?.code, r.sum.remaining > 0 ? `còn nợ ${formatCurrency(r.sum.remaining)}` : 'đã đóng đủ'].filter(Boolean).join(' · '),
            branch: branchName(r.branchId),
            badge: r.invoice.cancelled ? 'Đã hủy' : undefined,
            path: `/admin/finance/invoices?open=${r.id}`,
            icon: r.sum.remaining > 0 ? Receipt : FileText,
            // Gõ đúng mã HĐ → ưu tiên; gõ tên → xếp sau học viên
            score: normalizeText(r.invoice.code).startsWith(q) ? score : score - 1,
          })),
      );

    // ---- Nhân viên / giáo viên ----
    const canStaff = can('hr.employees');
    const canTeacher = can('hr.teachers');
    if (canStaff || canTeacher)
      push(
        employees
          .filter((e) => (e.kind === 'staff' ? canStaff : canTeacher) && inScope(e.branchId))
          .map((e) => ({ e, score: scoreOf(q, qDigits, [e.fullName, e.code, e.email], [e.phone]) }))
          .filter((x) => x.score > 0)
          .map(({ e, score }) => ({
            key: `employee:${e.id}`,
            group: 'employee' as const,
            title: e.fullName,
            subtitle: `${e.code} · ${e.position}`,
            branch: branchName(e.branchId),
            badge: e.status === 'quit' ? 'Đã nghỉ' : undefined,
            path: `/admin/hr/${e.kind === 'staff' ? 'employees' : 'teachers'}?open=${e.id}`,
            icon: e.kind === 'staff' ? UserCog : UserRound,
            score: score - (e.status === 'quit' ? 1 : 0),
          })),
      );

    return out;
  }, [query, user, can, leads, students, links, classes, employees, invoiceRows, branches]);
}
