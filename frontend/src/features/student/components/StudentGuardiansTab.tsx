import { useMemo } from 'react';
import { Info, Mail, Phone, Users } from 'lucide-react';
import { EmptyState, UserCell } from '@/components/common';
import { useCrmStore } from '@/features/crm/crmStore';
import type { Lead, Relationship } from '@/features/crm/types';
import { GUARDIAN_ROLE } from '../constants';
import { useStudentStore } from '../studentStore';
import { usePortalAccountActions } from '../portalAccountApi';
import { AccountCard } from './AccountCard';

/**
 * Tab Người thân: các người liên hệ (lead) gắn với học viên — phụ huynh, anh chị, vợ/chồng...
 * Kèm tài khoản Cổng phụ huynh của từng người để tiện liên lạc / hỗ trợ đăng nhập.
 * Thêm / gỡ liên kết, đặt người liên hệ chính: Học viên → Phụ huynh / Người giám hộ (hoặc Tuyển sinh → Lead).
 */
export function StudentGuardiansTab({ studentId }: { studentId: string }) {
  const links = useCrmStore((s) => s.links);
  const leads = useCrmStore((s) => s.leads);
  const accounts = useStudentStore((s) => s.accounts);
  const { issue: issueAccount } = usePortalAccountActions();

  const { guardians, selfLead } = useMemo(() => {
    const mine = links.filter((k) => k.studentId === studentId);
    const find = (id: string) => leads.find((l) => l.id === id);
    return {
      guardians: mine
        .filter((k) => k.relationship !== 'self')
        .map((k) => ({ lead: find(k.leadId), relationship: k.relationship as Exclude<Relationship, 'self'> }))
        .filter((x): x is { lead: Lead; relationship: Exclude<Relationship, 'self'> } => Boolean(x.lead)),
      selfLead: find(mine.find((k) => k.relationship === 'self')?.leadId ?? ''),
    };
  }, [links, leads, studentId]);

  const issue = (lead: Lead) =>
    void issueAccount({ ownerType: 'guardian', ownerId: lead.id, username: `ph.${lead.phone}`, email: lead.email, ownerName: lead.fullName });

  return (
    <div className="flex flex-col gap-4">
      {selfLead && (
        <div className="flex gap-2.5 rounded-lg border border-primary-200 bg-primary-50 px-3 py-2.5 text-[13px] text-primary-800">
          <Info size={16} className="mt-0.5 shrink-0" aria-hidden />
          <p>
            Học viên tự đăng ký — hồ sơ tư vấn ở lead <strong>{selfLead.code}</strong>.
          </p>
        </div>
      )}

      {guardians.length === 0 ? (
        <div className="rounded-[var(--radius-card)] border border-line">
          <EmptyState
            size="compact"
            icon={Users}
            title="Chưa có người thân liên kết"
            description="Thêm phụ huynh / người giám hộ ở Học viên → Phụ huynh / Người giám hộ."
          />
        </div>
      ) : (
        <ul className="flex flex-col gap-3">
          {guardians.map(({ lead, relationship }) => {
            const account = accounts.find((a) => a.ownerType === 'guardian' && a.ownerId === lead.id);
            return (
              <li key={lead.id} className="flex flex-col gap-3 rounded-[var(--radius-card)] border border-line bg-surface p-4">
                <div className="flex flex-wrap items-start justify-between gap-2">
                  <UserCell name={lead.fullName} subtitle={`${GUARDIAN_ROLE[relationship]} · Lead ${lead.code}`} />
                </div>
                <div className="flex flex-wrap gap-x-5 gap-y-1.5 text-[13px]">
                  <a href={`tel:${lead.phone}`} className="inline-flex items-center gap-1.5 font-medium text-primary-700 hover:underline">
                    <Phone size={14} aria-hidden /> {lead.phone}
                  </a>
                  {lead.email ? (
                    <a href={`mailto:${lead.email}`} className="inline-flex items-center gap-1.5 text-primary-700 hover:underline">
                      <Mail size={14} aria-hidden /> {lead.email}
                    </a>
                  ) : (
                    <span className="inline-flex items-center gap-1.5 text-ink-muted">
                      <Mail size={14} aria-hidden /> Chưa có email
                    </span>
                  )}
                </div>
                <div className="border-t border-line pt-3">
                  <AccountCard account={account} ownerName={lead.fullName} portalLabel="Cổng phụ huynh" onIssue={() => issue(lead)} compact />
                </div>
              </li>
            );
          })}
        </ul>
      )}
    </div>
  );
}
