import { useMemo } from 'react';
import dayjs from 'dayjs';
import { AlarmClock, ClipboardCheck, Inbox, PhoneCall, Receipt, ScanLine, type LucideIcon } from 'lucide-react';
import { useClassStore } from '@/features/academic/classes/classStore';
import { useAdmissionStore } from '@/features/crm/admission/admissionStore';
import { slaOf } from '@/features/crm/admission/routing';
import { courseLabel, saleName } from '@/features/crm/constants';
import { useCrmStore } from '@/features/crm/crmStore';
import { useFinanceStore } from '@/features/finance/financeStore';
import { useInvoiceRows } from '@/features/finance/financeUtils';
import { INBOX_KIND, useInbox } from '@/features/inbox/useInbox';
import { useMe } from '@/features/me/useMe';
import { useAuth } from '@/hooks/useAuth';
import { formatCurrency } from '@/lib/format';
import { ALL_BRANCHES } from '@/types/auth';

/**
 * VIỆC HÔM NAY — danh sách việc cụ thể theo vai trò, mỗi dòng bấm vào là mở đúng bản ghi.
 *  - Sale:    lead cần gọi lại (hẹn tới hôm nay), lead đã / sắp quá SLA liên hệ lần đầu
 *  - Giáo vụ: buổi học đã diễn ra chưa điểm danh, đơn chờ duyệt
 *  - Kế toán: hóa đơn đến hạn / quá hạn, giao dịch VietQR chưa khớp
 * Vai trò suy ra từ QUYỀN (không theo tên vai trò) → Admin / QL chi nhánh thấy đủ 3 nhóm.
 * Nhân viên phòng Sale chỉ thấy lead của chính mình; quản lý thấy cả chi nhánh.
 */

export type WorkTone = 'danger' | 'warning' | 'info';
export type WorkRole = 'sale' | 'academic' | 'finance';

export interface WorkItem {
  key: string;
  title: string;
  subtitle?: string;
  /** Nhãn ngắn bên phải: "quá 2 giờ", "hạn hôm nay" */
  meta?: string;
  tone: WorkTone;
  path: string;
}

export interface WorkGroup {
  key: string;
  label: string;
  icon: LucideIcon;
  items: WorkItem[];
  /** Xem tất cả */
  allPath: string;
  empty: string;
}

export interface RoleBoard {
  role: WorkRole;
  label: string;
  groups: WorkGroup[];
  /** Tổng số việc (để hiện trên tab) */
  count: number;
}

/** Hẹn gọi lại trước cuối ngày hôm nay; nhắc thêm lead đã quá SLA này khi chỉ còn ≤ 30% thời gian */
const NEAR_SLA_RATIO = 0.3;
/** Buổi học quá X ngày vẫn chưa điểm danh thì coi như bỏ sót, vẫn nhắc */
const ATTENDANCE_LOOKBACK_DAYS = 7;
/** Hóa đơn "đến hạn" = hạn trong X ngày tới */
const DUE_SOON_DAYS = 3;

const sortByTone = (a: WorkItem, b: WorkItem) => toneRank(a.tone) - toneRank(b.tone);
const toneRank = (t: WorkTone) => (t === 'danger' ? 0 : t === 'warning' ? 1 : 2);

function humanMinutes(min: number): string {
  if (min < 60) return `${min} phút`;
  if (min < 60 * 24) return `${Math.floor(min / 60)} giờ`;
  return `${Math.floor(min / 60 / 24)} ngày`;
}

export function useTodayWork(): RoleBoard[] {
  const { can, branchId } = useAuth();
  const me = useMe();
  const leads = useCrmStore((s) => s.leads);
  const rules = useAdmissionStore((s) => s.rules);
  const classes = useClassStore((s) => s.classes);
  const sessions = useClassStore((s) => s.sessions);
  const txns = useFinanceStore((s) => s.txns);
  const invoiceRows = useInvoiceRows();
  const inbox = useInbox();

  return useMemo(() => {
    const now = dayjs();
    const endOfToday = now.endOf('day');
    const today = now.format('YYYY-MM-DD');
    const inB = (b?: string) => branchId === ALL_BRANCHES || b === branchId;
    const boards: RoleBoard[] = [];

    // ================= Sale =================
    if (can('crm.leads')) {
      const ownOnly = me?.department === 'sales';
      const mine = leads.filter((l) => !l.hidden && inB(l.branchId) && !['won', 'lost'].includes(l.status) && (!ownOnly || l.assigneeId === me?.id));

      const callback: WorkItem[] = mine
        .filter((l) => l.nextFollowUpAt && dayjs(l.nextFollowUpAt).isBefore(endOfToday))
        .sort((a, b) => a.nextFollowUpAt!.localeCompare(b.nextFollowUpAt!))
        .map((l): WorkItem => {
          const at = dayjs(l.nextFollowUpAt);
          const late = at.isBefore(now.startOf('day'));
          return {
            key: `cb:${l.id}`,
            title: l.fullName,
            subtitle: [l.phone, courseLabel(l.interest) ?? l.interest, !ownOnly && saleName(l.assigneeId)].filter(Boolean).join(' · '),
            meta: late ? `trễ hẹn ${now.startOf('day').diff(at.startOf('day'), 'day')} ngày` : `hẹn ${at.format('HH:mm')}`,
            tone: late ? 'danger' : at.isBefore(now) ? 'warning' : 'info',
            path: `/admin/crm/leads?open=${l.id}`,
          };
        });

      const sla: WorkItem[] = mine
        .filter((l) => l.status === 'new' && !l.lastContactAt)
        .map((l) => {
          const limit = slaOf(rules, l);
          const elapsed = now.diff(dayjs(l.createdAt), 'minute');
          return { l, limit, left: limit - elapsed };
        })
        .filter((x) => x.left <= x.limit * NEAR_SLA_RATIO)
        .sort((a, b) => a.left - b.left)
        .map(({ l, left }): WorkItem => ({
          key: `sla:${l.id}`,
          title: l.fullName,
          subtitle: [l.phone, !ownOnly && (saleName(l.assigneeId) ?? 'Chưa giao Sale')].filter(Boolean).join(' · '),
          meta: left <= 0 ? `quá ${humanMinutes(-left)}` : `còn ${humanMinutes(left)}`,
          tone: left <= 0 ? 'danger' : 'warning',
          path: `/admin/crm/leads?open=${l.id}`,
        }));

      boards.push({
        role: 'sale',
        label: 'Sale',
        count: callback.length + sla.length,
        groups: [
          { key: 'sla', label: 'Lead đã / sắp quá SLA', icon: AlarmClock, items: sla, allPath: '/admin/crm/leads', empty: 'Không có lead mới nào sắp quá hạn liên hệ.' },
          { key: 'callback', label: 'Lead cần gọi lại hôm nay', icon: PhoneCall, items: callback.sort(sortByTone), allPath: '/admin/crm/leads', empty: 'Không có lịch hẹn gọi lại hôm nay.' },
        ],
      });
    }

    // ================= Giáo vụ =================
    if (can('academic.sessions')) {
      const cls = new Map(classes.map((c) => [c.id, c]));
      const since = now.subtract(ATTENDANCE_LOOKBACK_DAYS, 'day').format('YYYY-MM-DD');
      const noAttendance: WorkItem[] = sessions
        .filter((s) => {
          if (s.status === 'cancelled' || s.attendance || s.date < since || s.date > today) return false;
          const c = cls.get(s.classId);
          return c && c.status !== 'cancelled' && inB(c.branchId) && dayjs(`${s.date}T${s.startTime}`).isBefore(now);
        })
        .sort((a, b) => `${a.date}${a.startTime}`.localeCompare(`${b.date}${b.startTime}`))
        .map((s): WorkItem => {
          const c = cls.get(s.classId)!;
          const isToday = s.date === today;
          return {
            key: `att:${s.id}`,
            title: c.code,
            subtitle: `${dayjs(s.date).format('dd DD/MM')} · ${s.startTime}–${s.endTime}${s.kind === 'exam' ? ' · Buổi thi' : ''}`,
            meta: isToday ? 'hôm nay' : `${now.startOf('day').diff(dayjs(s.date), 'day')} ngày trước`,
            tone: isToday ? 'warning' : 'danger',
            path: `/admin/academic/classes/${c.id}?tab=attendance`,
          };
        });

      const pending: WorkItem[] = inbox
        .filter((i) => i.status === 'pending' && i.canDecide)
        .sort((a, b) => a.at.localeCompare(b.at))
        .map((i): WorkItem => {
          const soon = i.kind !== 'hr' && i.session && dayjs(`${i.session.date}T${i.session.startTime}`).diff(now, 'hour') < 24;
          return {
            key: `inbox:${i.kind}:${i.id}`,
            title: i.who,
            subtitle: `${INBOX_KIND[i.kind]}${i.kind !== 'hr' && i.cls ? ` · ${i.cls.code}` : ''}`,
            meta: soon ? 'buổi học < 24 giờ' : `gửi ${dayjs(i.at).format('DD/MM')}`,
            tone: soon ? 'danger' : 'warning',
            path: '/admin/academic/requests',
          };
        })
        .sort(sortByTone);

      boards.push({
        role: 'academic',
        label: 'Giáo vụ',
        count: noAttendance.length + pending.length,
        groups: [
          { key: 'attendance', label: 'Buổi học chưa điểm danh', icon: ClipboardCheck, items: noAttendance, allPath: '/admin/academic/sessions', empty: `Mọi buổi học ${ATTENDANCE_LOOKBACK_DAYS} ngày qua đã điểm danh.` },
          { key: 'requests', label: 'Đơn chờ duyệt', icon: Inbox, items: pending, allPath: '/admin/academic/requests', empty: 'Không còn đơn nào chờ bạn duyệt.' },
        ],
      });
    }

    // ================= Kế toán =================
    if (can('finance.invoices')) {
      const dueLimit = now.add(DUE_SOON_DAYS, 'day').format('YYYY-MM-DD');
      const due: WorkItem[] = invoiceRows
        .filter((r) => !r.invoice.cancelled && r.sum.remaining > 0 && inB(r.branchId) && r.invoice.dueDate <= dueLimit)
        .sort((a, b) => a.invoice.dueDate.localeCompare(b.invoice.dueDate))
        .map((r): WorkItem => {
          const late = now.startOf('day').diff(dayjs(r.invoice.dueDate), 'day');
          return {
            key: `inv:${r.id}`,
            title: `${r.invoice.code} — ${r.student?.fullName ?? '—'}`,
            subtitle: `${r.cls?.code ?? ''} · còn nợ ${formatCurrency(r.sum.remaining)}`,
            meta: late > 0 ? `quá hạn ${late} ngày` : late === 0 ? 'hạn hôm nay' : `hạn ${dayjs(r.invoice.dueDate).format('DD/MM')}`,
            tone: late > 0 ? 'danger' : late === 0 ? 'warning' : 'info',
            path: `/admin/finance/invoices?open=${r.id}`,
          };
        })
        .sort(sortByTone);

      const unmatched: WorkItem[] = can('finance.reconciliation')
        ? txns
            .filter((t) => t.status === 'unmatched' && inB(t.branchId))
            .sort((a, b) => a.at.localeCompare(b.at))
            .map((t): WorkItem => {
              const age = now.diff(dayjs(t.at), 'day');
              return {
                key: `txn:${t.id}`,
                title: `${formatCurrency(t.amount)} — ${t.payer}`,
                subtitle: t.content,
                meta: age > 0 ? `${age} ngày trước` : dayjs(t.at).format('HH:mm'),
                tone: age >= 2 ? 'danger' : 'warning',
                path: '/admin/finance/reconciliation',
              };
            })
        : [];

      boards.push({
        role: 'finance',
        label: 'Kế toán',
        count: due.length + unmatched.length,
        groups: [
          { key: 'due', label: `Hóa đơn đến hạn (${DUE_SOON_DAYS} ngày tới) & quá hạn`, icon: Receipt, items: due, allPath: '/admin/finance/debts', empty: 'Không có hóa đơn nào đến hạn.' },
          ...(can('finance.reconciliation')
            ? [{ key: 'txn', label: 'Giao dịch VietQR chưa khớp', icon: ScanLine, items: unmatched, allPath: '/admin/finance/reconciliation', empty: 'Mọi giao dịch đã được khớp.' }]
            : []),
        ],
      });
    }

    return boards;
  }, [can, branchId, me, leads, rules, classes, sessions, txns, invoiceRows, inbox]);
}
