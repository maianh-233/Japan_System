import { useState } from 'react';
import { Drawer, Radio, Tooltip } from 'antd';
import dayjs from 'dayjs';
import { ExternalLink, MessageCircle, Phone, PhoneCall, Star, Users, Video } from 'lucide-react';
import { useNavigate } from 'react-router-dom';
import { Button, DateInput, Select, StatusTag, TextArea, UserCell, useNotify } from '@/components/common';
import { useClassStore } from '@/features/academic/classes/classStore';
import type { StudentStats } from '@/features/academic/classes/studentInsights';
import type { ClassInfo } from '@/features/academic/classes/types';
import { useCrmStore } from '@/features/crm/crmStore';
import { useAuth } from '@/hooks/useAuth';
import { useBreakpoint } from '@/hooks/useBreakpoint';
import { cn } from '@/lib/cn';
import { fmtDate, fmtDateTime } from '@/lib/date';
import { formatCurrency } from '@/lib/format';
import { GUARDIAN_ROLE } from '../constants';
import { CARE_STAFF, CARE_STATUS, CARE_STATUS_OPTIONS, caseKey, RISK_LEVEL, riskLevel, useAtRiskStore, type CareStatus } from './atRisk';

const CHANNEL = { call: { label: 'Gọi điện', icon: PhoneCall }, zalo: { label: 'Zalo', icon: MessageCircle }, meet: { label: 'Gặp trực tiếp', icon: Video } } as const;
const DOT: Record<string, string> = { present: 'bg-success-600', late: 'bg-warning-600', excused: 'bg-primary-400', absent: 'bg-danger-600' };

/** Hồ sơ chăm sóc 1 học viên có nguy cơ bỏ học */
export function AtRiskDrawer({ item, onClose }: { item: { cls: ClassInfo; st: StudentStats } | null; onClose: () => void }) {
  const { isMobile } = useBreakpoint();
  const { user } = useAuth();
  const notify = useNotify();
  const navigate = useNavigate();
  const students = useCrmStore((s) => s.students);
  const links = useCrmStore((s) => s.links);
  const leads = useCrmStore((s) => s.leads);
  const addCareLog = useClassStore((s) => s.addCareLog);
  const cases = useAtRiskStore((s) => s.cases);
  const setCase = useAtRiskStore((s) => s.setCase);
  const [channel, setChannel] = useState<'call' | 'zalo' | 'meet'>('call');
  const [note, setNote] = useState('');

  if (!item) return <Drawer open={false} onClose={onClose} />;
  const { cls, st } = item;
  const sid = st.enrollment.studentId;
  const student = students.find((s) => s.id === sid);
  const key = caseKey(cls.id, sid);
  const c = cases[key] ?? { status: 'new' as CareStatus };
  const level = riskLevel(st);
  const guardians = links
    .filter((k) => k.studentId === sid && k.relationship !== 'self')
    .map((k) => ({ link: k, lead: leads.find((l) => l.id === k.leadId) }))
    .filter((x) => x.lead)
    .sort((a, b) => Number(Boolean(b.link.primary)) - Number(Boolean(a.link.primary)));
  const me = user ? `${user.fullName} (${user.roleName})` : 'Nhân viên';

  const log = () => {
    if (!note.trim()) return;
    addCareLog({ classId: cls.id, studentId: sid, channel, note: note.trim(), by: me });
    if (c.status === 'new') setCase(key, { status: 'contacting', assignee: c.assignee ?? me });
    setNote('');
    notify.toast.success('Đã ghi nhận lần liên hệ');
  };

  const timeline = st.attendance.timeline.slice(-12);

  return (
    <Drawer
      open
      onClose={onClose}
      width={isMobile ? '100%' : 720}
      destroyOnHidden
      title={
        <div className="flex min-w-0 flex-col gap-1 pr-2">
          <div className="flex flex-wrap items-center gap-2">
            <span className="truncate text-base font-semibold">{student?.fullName}</span>
            {level && (
              <StatusTag tone={RISK_LEVEL[level].tone} icon={CARE_STATUS.new.icon} size="sm">
                {RISK_LEVEL[level].label}
              </StatusTag>
            )}
          </div>
          <span className="text-xs font-normal text-ink-muted">
            {student?.code} · Lớp {cls.code}
          </span>
        </div>
      }
      extra={
        <div className="flex gap-1.5">
          <Button icon={ExternalLink} size="sm" tone="neutral" variant="outline" collapseOnMobile onClick={() => navigate(`/admin/students/profiles?open=${sid}`)}>
            Hồ sơ
          </Button>
          <Button icon={ExternalLink} size="sm" tone="neutral" variant="outline" collapseOnMobile onClick={() => navigate(`/admin/academic/classes/${cls.id}`)}>
            Lớp
          </Button>
        </div>
      }
    >
      <div className="flex flex-col gap-5 text-[13px]">
        {/* Lý do */}
        <section>
          <h3 className="mb-2 text-sm font-semibold text-ink">Dấu hiệu</h3>
          <div className="flex flex-wrap gap-1.5">
            {st.flags.map((f, i) => (
              <span key={i} className={cn('rounded-md px-2 py-0.5 text-xs font-medium', f.tone === 'danger' ? 'bg-danger-50 text-danger-700' : 'bg-warning-50 text-warning-700')}>
                {f.label}
              </span>
            ))}
          </div>
          <div className="mt-3 grid grid-cols-2 gap-2 sm:grid-cols-4">
            <Mini label="Chuyên cần" value={st.attendance.rate !== undefined ? `${st.attendance.rate}%` : '—'} hint={`${st.attendance.absent} vắng · ${st.attendance.excused} có phép`} />
            <Mini label="Nộp bài" value={st.assignments.due ? `${st.assignments.submitted}/${st.assignments.due}` : '—'} hint={st.assignments.late ? `${st.assignments.late} bài nộp trễ` : undefined} />
            <Mini label="Điểm TB" value={st.assignments.avgPct !== undefined ? `${st.assignments.avgPct}%` : '—'} hint={st.exams.avgPct !== undefined ? `Thi ${st.exams.avgPct}%` : undefined} />
            <Mini label="Học phí" value={st.tuition?.remaining ? formatCurrency(st.tuition.remaining) : 'Đủ'} hint={st.tuition?.status === 'overdue' ? 'Quá hạn' : undefined} />
          </div>
          <div className="mt-3">
            <p className="mb-1 text-xs text-ink-muted">Điểm danh {timeline.length} buổi gần nhất</p>
            <div className="flex gap-1">
              {timeline.map((t) => (
                <Tooltip key={t.sessionId} title={`Buổi ${t.no} · ${fmtDate(t.date)} · ${t.status ?? 'chưa điểm danh'}${t.note ? ` — ${t.note}` : ''}`}>
                  <span className={cn('h-6 flex-1 rounded', t.status ? DOT[t.status] : 'bg-disabled')} />
                </Tooltip>
              ))}
            </div>
            <p className="mt-1 flex gap-3 text-[11px] text-ink-muted">
              <span className="inline-flex items-center gap-1"><i className="size-2 rounded-sm bg-success-600" /> Có mặt</span>
              <span className="inline-flex items-center gap-1"><i className="size-2 rounded-sm bg-warning-600" /> Muộn</span>
              <span className="inline-flex items-center gap-1"><i className="size-2 rounded-sm bg-primary-400" /> Có phép</span>
              <span className="inline-flex items-center gap-1"><i className="size-2 rounded-sm bg-danger-600" /> Vắng</span>
            </p>
          </div>
        </section>

        {/* Liên hệ */}
        <section>
          <h3 className="mb-2 flex items-center gap-1.5 text-sm font-semibold text-ink">
            <Users size={16} aria-hidden /> Liên hệ
          </h3>
          <ul className="divide-y divide-line rounded-[var(--radius-card)] border border-line">
            <li className="flex items-center justify-between gap-2 px-3 py-2">
              <UserCell size="sm" name={student?.fullName ?? sid} subtitle="Học viên" />
              {student?.phone ? (
                <a href={`tel:${student.phone}`} className="inline-flex items-center gap-1 font-medium text-primary-700 hover:underline">
                  <Phone size={14} aria-hidden /> {student.phone}
                </a>
              ) : (
                <span className="text-ink-muted">Không có SĐT</span>
              )}
            </li>
            {guardians.map(({ link, lead }) => (
              <li key={lead!.id} className="flex items-center justify-between gap-2 px-3 py-2">
                <UserCell
                  size="sm"
                  name={lead!.fullName}
                  subtitle={
                    <span className="inline-flex items-center gap-1">
                      {GUARDIAN_ROLE[link.relationship as keyof typeof GUARDIAN_ROLE]}
                      {link.primary && (
                        <>
                          · <Star size={11} className="fill-warning-600 text-warning-600" aria-hidden /> liên hệ chính
                        </>
                      )}
                    </span>
                  }
                />
                <a href={`tel:${lead!.phone}`} className="inline-flex items-center gap-1 font-medium text-primary-700 hover:underline">
                  <Phone size={14} aria-hidden /> {lead!.phone}
                </a>
              </li>
            ))}
          </ul>
        </section>

        {/* Chăm sóc */}
        <section className="flex flex-col gap-3">
          <h3 className="text-sm font-semibold text-ink">Chăm sóc</h3>
          <div className="grid grid-cols-1 gap-2 sm:grid-cols-3">
            <label className="flex flex-col gap-1">
              <span className="text-xs text-ink-muted">Người phụ trách</span>
              <Select value={c.assignee} placeholder="Chưa giao" onChange={(v) => setCase(key, { assignee: v as string })} options={CARE_STAFF.map((x) => ({ value: x, label: x }))} />
            </label>
            <label className="flex flex-col gap-1">
              <span className="text-xs text-ink-muted">Trạng thái</span>
              <Select value={c.status} onChange={(v) => setCase(key, { status: v as CareStatus })} options={CARE_STATUS_OPTIONS} showSearch={false} allowClear={false} />
            </label>
            <label className="flex flex-col gap-1">
              <span className="text-xs text-ink-muted">Hẹn liên hệ lại</span>
              <DateInput value={c.nextAt ? dayjs(c.nextAt) : null} onChange={(d) => setCase(key, { nextAt: d?.format('YYYY-MM-DD') })} />
            </label>
          </div>

          <div className="flex flex-col gap-2 rounded-[var(--radius-card)] border border-line bg-page p-3">
            <Radio.Group size="small" optionType="button" value={channel} onChange={(e) => setChannel(e.target.value)} options={(Object.keys(CHANNEL) as (keyof typeof CHANNEL)[]).map((k) => ({ value: k, label: CHANNEL[k].label }))} />
            <TextArea rows={2} value={note} onChange={(e) => setNote(e.target.value)} placeholder="Kết quả liên hệ: lý do vắng, cam kết đi học lại, đề xuất bảo lưu / chuyển ca..." maxLength={500} />
            <div className="flex justify-end">
              <Button icon={PhoneCall} size="sm" disabled={!note.trim()} onClick={log}>
                Ghi nhận liên hệ
              </Button>
            </div>
          </div>

          {st.careLogs.length ? (
            <ol className="flex flex-col gap-2 border-l-2 border-line pl-3">
              {[...st.careLogs].sort((a, b) => b.at.localeCompare(a.at)).map((l) => {
                const Ch = CHANNEL[l.channel];
                return (
                  <li key={l.id}>
                    <p className="flex items-center gap-1.5 text-xs text-ink-muted">
                      <Ch.icon size={12} aria-hidden /> {Ch.label} · {l.by} · {fmtDateTime(l.at)}
                    </p>
                    <p className="text-ink">{l.note}</p>
                  </li>
                );
              })}
            </ol>
          ) : (
            <p className="text-ink-muted">Chưa có lần liên hệ nào.</p>
          )}
        </section>
      </div>
    </Drawer>
  );
}

function Mini({ label, value, hint }: { label: string; value: string; hint?: string }) {
  return (
    <div className="rounded-lg border border-line p-2.5">
      <p className="text-[11px] text-ink-muted">{label}</p>
      <p className="tabular text-base font-semibold text-ink">{value}</p>
      {hint && <p className="text-[11px] text-ink-muted">{hint}</p>}
    </div>
  );
}
