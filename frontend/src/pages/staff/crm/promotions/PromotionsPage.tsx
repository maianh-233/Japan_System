import { useMemo, useState } from 'react';
import { Drawer, Form, Radio, Tooltip } from 'antd';
import dayjs from 'dayjs';
import { ArrowRight, CircleCheck, Eye, Hourglass, Inbox, MessageSquarePlus, Phone, Sparkles, UserRound, Users, XCircle } from 'lucide-react';
import { Button, EmptyState, ModalForm, PageHeader, SearchBar, Select, StatCard, StatusTag, TextArea, UserCell, useNotify } from '@/components/common';
import { useClassStore } from '@/features/academic/classes/classStore';
import { EVAL_RECOMMENDATION, effectiveStart, scheduleLabel } from '@/features/academic/classes/constants';
import { EvaluationCard } from '@/features/academic/classes/evaluation/EvaluationCard';
import { useAllStudentStats } from '@/features/academic/classes/studentInsights';
import type { ClassInfo } from '@/features/academic/classes/types';
import { usePromotionStore } from '@/features/academic/promotion/promotionStore';
import { ageOf, DECLINE_REASONS, isOpen, PROMOTION_STATUS, seatActive, viewStatus, type Decider } from '@/features/academic/promotion/promotionUtils';
import type { PromotionCandidate } from '@/features/academic/promotion/types';
import { useDecider } from '@/features/academic/promotion/usePromotion';
import { CONSULT_CHANNELS, saleName, useSales } from '@/features/crm/constants';
import { useCrmStore } from '@/features/crm/crmStore';
import type { ConsultChannel } from '@/features/crm/types';
import { meOf } from '@/features/hr/hrUtils';
import { useHrStore } from '@/features/hr/hrStore';
import { useAuth } from '@/hooks/useAuth';
import { cn } from '@/lib/cn';
import { fmtDate, fmtDateTime, fromNow } from '@/lib/date';
import { formatCurrency } from '@/lib/format';
import { ALL_BRANCHES } from '@/types/auth';

type Tab = 'todo' | 'upcoming' | 'done';

const REC_CLS: Partial<Record<string, string>> = {
  success: 'bg-success-50 text-success-700',
  primary: 'bg-primary-50 text-primary-700',
  info: 'bg-primary-50 text-primary-700',
  warning: 'bg-warning-50 text-warning-700',
};

interface Item {
  c: PromotionCandidate;
  from?: ClassInfo;
  to?: ClassInfo;
  name: string;
  age?: number;
  dec: Decider;
  saleId?: string;
}

interface Group {
  key: string;
  dec: Decider;
  items: Item[];
}

/**
 * Tuyển sinh › Tư vấn lên lớp
 * Lớp kết thúc → học viên trong danh sách lên lớp được giao cho Sale. Sale tư vấn NGƯỜI QUYẾT ĐỊNH:
 * học viên lớn tự quyết; học viên nhỏ → phụ huynh. Các con của cùng 1 phụ huynh gom vào 1 thẻ (gọi 1 lần cho cả nhà).
 */
export default function PromotionsPage() {
  const notify = useNotify();
  const { user, branchId, can } = useAuth();
  const employees = useHrStore((s) => s.employees);
  const me = meOf(employees, user);
  const sales = useSales(branchId);
  const classes = useClassStore((s) => s.classes);
  const evaluations = useClassStore((s) => s.evaluations);
  const students = useCrmStore((s) => s.students);
  const candidates = usePromotionStore((s) => s.candidates);
  const { addNote, update, confirm } = usePromotionStore();
  const decider = useDecider();
  const editable = can('crm.promotions');

  const [tab, setTab] = useState<Tab>('todo');
  const [kw, setKw] = useState('');
  const [saleFilter, setSaleFilter] = useState<string | undefined>(me?.department === 'sales' ? me.id : undefined);
  const [fromFilter, setFromFilter] = useState<string>();
  const [noteFor, setNoteFor] = useState<Group | null>(null);
  const [confirming, setConfirming] = useState<Item | null>(null);
  const [declining, setDeclining] = useState<Item | null>(null);
  const [detail, setDetail] = useState<Item | null>(null);

  const items: Item[] = useMemo(
    () =>
      candidates
        .map((c) => {
          const st = students.find((s) => s.id === c.studentId);
          const dec = decider(c.studentId);
          return { c, from: classes.find((x) => x.id === c.fromClassId), to: classes.find((x) => x.id === c.toClassId), name: st?.fullName ?? c.studentId, age: ageOf(st?.dob), dec, saleId: c.saleId ?? dec.saleId };
        })
        .filter((x) => branchId === ALL_BRANCHES || x.from?.branchId === branchId),
    [candidates, students, classes, decider, branchId],
  );

  const inTab = (x: Item) => (tab === 'todo' ? Boolean(x.c.handedOverAt) && isOpen(x.c) : tab === 'upcoming' ? !x.c.handedOverAt && isOpen(x.c) : !isOpen(x.c));
  const k = kw.trim().toLowerCase();
  const shown = items.filter(
    (x) =>
      inTab(x) &&
      (!saleFilter || x.saleId === saleFilter) &&
      (!fromFilter || x.c.fromClassId === fromFilter) &&
      (!k || x.name.toLowerCase().includes(k) || x.dec.name.toLowerCase().includes(k) || (x.dec.phone ?? '').includes(k)),
  );

  // Gom theo người quyết định (1 phụ huynh nhiều con → 1 thẻ)
  const groups: Group[] = useMemo(() => {
    const m = new Map<string, Group>();
    shown.forEach((x) => {
      const g = m.get(x.dec.groupKey) ?? { key: x.dec.groupKey, dec: x.dec, items: [] };
      g.items.push(x);
      m.set(x.dec.groupKey, g);
    });
    const urgency = (g: Group) => Math.min(...g.items.map((x) => (x.c.interest ? -1 : x.c.holdUntil ? dayjs(x.c.holdUntil).diff(dayjs(), 'day') : 99)));
    return [...m.values()].sort((a, b) => urgency(a) - urgency(b));
  }, [shown]);

  const fromClasses = useMemo(() => classes.filter((c) => items.some((x) => x.c.fromClassId === c.id)), [classes, items]);
  const statsByClass = useAllStudentStats(fromClasses);
  const statOf = (x: Item) => statsByClass.find((s) => s.cls.id === x.c.fromClassId)?.map.get(x.c.studentId);

  const mineAll = items.filter((x) => !saleFilter || x.saleId === saleFilter);
  const handed = mineAll.filter((x) => x.c.handedOverAt);
  const soon = mineAll.filter((x) => seatActive(x.c) && x.c.handedOverAt && dayjs(x.c.holdUntil).diff(dayjs(), 'day') <= 3);
  const rate = handed.length ? Math.round((handed.filter((x) => x.c.status === 'confirmed').length / handed.length) * 100) : 0;
  const by = user?.fullName ?? 'Sale';

  return (
    <>
      <PageHeader
        title="Tư vấn lên lớp"
        subtitle="Học viên lớp vừa kết thúc được giữ chỗ / đề xuất lên lớp kỳ sau. Tư vấn người quyết định — học viên nhỏ tuổi thì gọi phụ huynh; các con của cùng phụ huynh được gom chung."
      />

      <div className="mb-4 grid grid-cols-2 gap-3 lg:grid-cols-4">
        <StatCard label="Chờ tư vấn" value={mineAll.filter((x) => x.c.handedOverAt && x.c.status === 'pending').length} icon={Inbox} tone="warning" onClick={() => setTab('todo')} />
        <StatCard label="Đang tư vấn / chưa liên hệ được" value={mineAll.filter((x) => x.c.handedOverAt && (x.c.status === 'consulting' || x.c.status === 'unreachable')).length} icon={Phone} tone="info" onClick={() => setTab('todo')} />
        <StatCard label="Sắp hết hạn giữ chỗ (≤ 3 ngày)" value={soon.length} icon={Hourglass} tone={soon.length ? 'danger' : 'neutral'} onClick={() => setTab('todo')} />
        <StatCard label="Tỉ lệ lên lớp" value={`${rate}%`} icon={CircleCheck} tone="success" hint={`${handed.filter((x) => x.c.status === 'confirmed').length}/${handed.length} học viên đã giao`} onClick={() => setTab('done')} />
      </div>

      <div className="mb-3 inline-flex flex-wrap rounded-xl border border-line bg-surface p-1" role="tablist">
        {(
          [
            ['todo', `Cần tư vấn (${items.filter((x) => x.c.handedOverAt && isOpen(x.c) && (!saleFilter || x.saleId === saleFilter)).length})`],
            ['upcoming', `Lớp sắp kết thúc (${items.filter((x) => !x.c.handedOverAt && isOpen(x.c) && (!saleFilter || x.saleId === saleFilter)).length})`],
            ['done', 'Đã xong'],
          ] as const
        ).map(([key, label]) => (
          <button
            key={key}
            type="button"
            role="tab"
            aria-selected={tab === key}
            onClick={() => setTab(key)}
            className={cn('h-9 rounded-lg px-4 text-[13px] font-medium transition-colors', tab === key ? 'bg-primary-600 text-white shadow-sm' : 'text-ink-secondary hover:bg-page')}
          >
            {label}
          </button>
        ))}
      </div>
      {tab === 'upcoming' && <p className="mb-3 text-[13px] text-ink-muted">Lớp chưa kết thúc — đã giữ chỗ, chưa giao chính thức. Có thể liên hệ sớm với người đã bấm “Quan tâm”.</p>}

      <SearchBar
        onSearch={setKw}
        placeholder="Tìm học viên, phụ huynh, số điện thoại..."
        activeFilterCount={[saleFilter, fromFilter].filter(Boolean).length}
        filters={
          <>
            <Select placeholder="Sale phụ trách" value={saleFilter} onChange={setSaleFilter} options={sales.map((s) => ({ value: s.id, label: s.id === me?.id ? `${s.fullName} (tôi)` : s.fullName }))} className="lg:!w-48" />
            <Select placeholder="Lớp vừa học" value={fromFilter} onChange={setFromFilter} options={fromClasses.map((c) => ({ value: c.id, label: c.code }))} className="lg:!w-44" />
          </>
        }
        onReset={() => {
          setSaleFilter(undefined);
          setFromFilter(undefined);
        }}
        className="mb-4"
      />

      {!groups.length ? (
        <div className="rounded-[var(--radius-card)] border border-line bg-surface">
          <EmptyState icon={Users} title={tab === 'done' ? 'Chưa có kết quả' : 'Không có học viên cần tư vấn'} description={tab === 'todo' ? 'Khi lớp kết thúc, danh sách lên lớp của lớp đó sẽ hiện ở đây.' : undefined} />
        </div>
      ) : (
        <div className="flex flex-col gap-4">
          {groups.map((g) => {
            const lastNote = g.items.flatMap((x) => x.c.notes).sort((a, b) => b.at.localeCompare(a.at))[0];
            const interested = g.items.find((x) => x.c.interest);
            return (
              <article key={g.key} className="overflow-hidden rounded-[var(--radius-card)] border border-line bg-surface">
                {/* Người quyết định */}
                <header className="flex flex-col gap-2 border-b border-line bg-page/60 px-4 py-3 sm:flex-row sm:items-center">
                  <div className="flex min-w-0 flex-1 items-center gap-3">
                    <span className={cn('grid size-10 shrink-0 place-items-center rounded-full', g.dec.kind === 'guardian' ? 'bg-warning-50 text-warning-700' : 'bg-primary-50 text-primary-700')}>
                      {g.dec.kind === 'guardian' ? <Users size={18} aria-hidden /> : <UserRound size={18} aria-hidden />}
                    </span>
                    <div className="min-w-0">
                      <p className="flex flex-wrap items-center gap-2 font-semibold text-ink">
                        {g.dec.name}
                        <span className={cn('rounded px-1.5 py-0.5 text-[11px] font-medium', g.dec.kind === 'guardian' ? 'bg-warning-50 text-warning-700' : 'bg-primary-50 text-primary-700')}>
                          {g.dec.kind === 'guardian' ? `Phụ huynh — quyết định cho ${g.items.length > 1 ? `${g.items.length} con` : 'con'}` : 'Học viên tự quyết định'}
                        </span>
                        {interested && (
                          <span className="inline-flex items-center gap-1 rounded bg-success-50 px-1.5 py-0.5 text-[11px] font-medium text-success-700">
                            <Sparkles size={11} aria-hidden /> Đã bấm “Quan tâm” {fromNow(interested.c.interest!.at)}
                          </span>
                        )}
                      </p>
                      <p className="text-[13px] text-ink-muted">
                        {g.dec.phone ? (
                          <a href={`tel:${g.dec.phone}`} className="tabular text-primary-700 hover:underline">
                            {g.dec.phone}
                          </a>
                        ) : (
                          'Chưa có số điện thoại'
                        )}
                        {' · '}Sale: {saleName(g.items[0].saleId) ?? <span className="text-danger-700">chưa có</span>}
                      </p>
                    </div>
                  </div>
                  {editable && tab !== 'done' && !g.items[0].saleId && me && (
                    <Button
                      icon={UserRound}
                      size="sm"
                      variant="outline"
                      onClick={() => {
                        g.items.forEach((x) => update(x.c.id, { saleId: me.id }));
                        notify.toast.success(`Bạn đã nhận phụ trách ${g.dec.name}`);
                      }}
                    >
                      Nhận phụ trách
                    </Button>
                  )}
                  {editable && tab !== 'done' && (
                    <Button icon={MessageSquarePlus} size="sm" variant="soft" onClick={() => setNoteFor(g)}>
                      Ghi chú tư vấn{g.items.length > 1 ? ' (cả nhà)' : ''}
                    </Button>
                  )}
                </header>

                {/* Từng học viên */}
                <ul className="divide-y divide-line">
                  {g.items.map((x) => {
                    const v = PROMOTION_STATUS[viewStatus(x.c)];
                    const ev = evaluations.find((e) => e.classId === x.c.fromClassId && e.studentId === x.c.studentId);
                    const st = statOf(x);
                    const daysLeft = x.c.holdUntil ? dayjs(x.c.holdUntil).endOf('day').diff(dayjs(), 'day') : undefined;
                    return (
                      <li key={x.c.id} className="flex flex-col gap-3 px-4 py-3 lg:flex-row lg:items-center">
                        <div className="min-w-0 flex-1">
                          <p className="font-medium text-ink">
                            {x.name}
                            {x.age !== undefined && x.age < 18 && <span className="ml-1.5 text-xs font-normal text-ink-muted">{x.age} tuổi</span>}
                          </p>
                          <p className="flex flex-wrap items-center gap-1.5 text-[13px] text-ink-secondary">
                            <span className="font-mono">{x.from?.code ?? x.c.fromClassId}</span>
                            <ArrowRight size={13} className="text-ink-muted" aria-hidden />
                            <span className="font-mono font-medium text-ink">{x.to?.code ?? 'chưa chọn lớp'}</span>
                            {x.to && <span className="text-xs text-ink-muted">· khai giảng {fmtDate(effectiveStart(x.to))} · {scheduleLabel(x.to)}</span>}
                          </p>
                          <p className="mt-1 flex flex-wrap items-center gap-x-3 gap-y-1 text-xs text-ink-muted">
                            {ev ? (
                              <Tooltip title={[ev.strengths && `Mạnh: ${ev.strengths}`, ev.weaknesses && `Cần cải thiện: ${ev.weaknesses}`, ev.advice && `Ghi chú: ${ev.advice}`].filter(Boolean).join('\n')}>
                                <span className={cn('rounded px-1.5 py-0.5 font-medium', REC_CLS[EVAL_RECOMMENDATION[ev.recommendation].tone] ?? 'bg-page text-ink-secondary')}>
                                  GV: {EVAL_RECOMMENDATION[ev.recommendation].label}
                                </span>
                              </Tooltip>
                            ) : (
                              <span>GV chưa nhận xét</span>
                            )}
                            {st?.attendance.rate !== undefined && <span>Chuyên cần {st.attendance.rate}%</span>}
                            {st?.exams.avgPct !== undefined && <span>Điểm thi {st.exams.avgPct}%</span>}
                            {seatActive(x.c) && daysLeft !== undefined && (
                              <span className={cn('font-medium', daysLeft <= 3 ? 'text-danger-700' : 'text-warning-700')}>Giữ chỗ còn {Math.max(daysLeft, 0)} ngày ({fmtDate(x.c.holdUntil)})</span>
                            )}
                            {!x.c.seatHeld && isOpen(x.c) && <span>Không giữ chỗ</span>}
                            {x.c.declineReason && <span className="text-danger-700">Lý do: {x.c.declineReason}</span>}
                          </p>
                        </div>
                        <div className="flex flex-wrap items-center gap-2">
                          <StatusTag tone={v.tone} icon={v.icon} size="sm">
                            {v.label}
                          </StatusTag>
                          <Button icon={Eye} size="sm" variant="ghost" tone="neutral" onClick={() => setDetail(x)}>
                            Chi tiết
                          </Button>
                          {editable && isOpen(x.c) && (
                            <>
                              <Button icon={CircleCheck} size="sm" tone="success" onClick={() => setConfirming(x)}>
                                Chốt ghi danh
                              </Button>
                              <Button icon={XCircle} size="sm" variant="outline" tone="danger" onClick={() => setDeclining(x)}>
                                Không học tiếp
                              </Button>
                            </>
                          )}
                        </div>
                      </li>
                    );
                  })}
                </ul>
                {lastNote && (
                  <p className="border-t border-line px-4 py-2 text-xs text-ink-muted">
                    Lần cuối: <span className="text-ink-secondary">{lastNote.text}</span> — {lastNote.by}, {fromNow(lastNote.at)}
                  </p>
                )}
              </article>
            );
          })}
        </div>
      )}

      {/* Ghi chú tư vấn cho cả nhóm */}
      <ModalForm<{ channel: ConsultChannel; result: 'consulting' | 'unreachable'; text: string }>
        key={`note-${noteFor?.key}`}
        open={Boolean(noteFor)}
        onClose={() => setNoteFor(null)}
        title={`Ghi chú tư vấn · ${noteFor?.dec.name ?? ''}`}
        description={noteFor && noteFor.items.length > 1 ? `Ghi cho cả ${noteFor.items.length} học viên: ${noteFor.items.map((x) => x.name).join(', ')}` : undefined}
        submitAction="save"
        successMessage="Đã lưu ghi chú tư vấn"
        initialValues={{ channel: 'call', result: 'consulting' }}
        onSubmit={(v) => {
          const ids = noteFor!.items.filter((x) => isOpen(x.c)).map((x) => x.c.id);
          addNote(ids, { by, text: v.text, channel: v.channel });
          ids.forEach((id) => update(id, { status: v.result }));
        }}
      >
        <Form.Item name="channel" label="Kênh">
          <Radio.Group optionType="button" options={(Object.keys(CONSULT_CHANNELS) as ConsultChannel[]).map((c) => ({ value: c, label: CONSULT_CHANNELS[c].label }))} />
        </Form.Item>
        <Form.Item name="result" label="Kết quả">
          <Radio.Group
            options={[
              { value: 'consulting', label: 'Đã trao đổi — đang cân nhắc' },
              { value: 'unreachable', label: 'Chưa liên hệ được' },
            ]}
          />
        </Form.Item>
        <Form.Item name="text" label="Nội dung" rules={[{ required: true, message: 'Nhập nội dung trao đổi' }]}>
          <TextArea rows={3} maxLength={1000} placeholder="Vd: Phụ huynh đồng ý cho bé học tiếp, hỏi ưu đãi anh chị em..." />
        </Form.Item>
      </ModalForm>

      {/* Chốt ghi danh */}
      <ModalForm<{ toClassId: string }>
        key={`confirm-${confirming?.c.id}`}
        open={Boolean(confirming)}
        onClose={() => setConfirming(null)}
        title={`Chốt ghi danh · ${confirming?.name ?? ''}`}
        description="Tạo ghi danh chính thức + hóa đơn học phí. Chỗ giữ tạm được chuyển thành ghi danh."
        submitAction="approve"
        submitText="Ghi danh"
        successMessage="Đã ghi danh — hóa đơn học phí đã tạo"
        initialValues={{ toClassId: confirming?.c.toClassId }}
        onSubmit={(v) => {
          if (!confirm(confirming!.c.id, v.toClassId, by)) throw new Error('Học viên đã có trong lớp này');
        }}
      >
        <Form.Item name="toClassId" label="Lớp" rules={[{ required: true, message: 'Chọn lớp' }]}>
          <Select
            options={classes
              .filter((c) => c.status === 'enrolling' || c.status === 'ongoing')
              .sort((a, b) => Number(b.id === confirming?.c.toClassId) - Number(a.id === confirming?.c.toClassId) || Number(b.status === 'enrolling') - Number(a.status === 'enrolling'))
              .map((c) => ({ value: c.id, label: `${c.code} · ${c.courseLabel} · ${c.status === 'enrolling' ? `khai giảng ${fmtDate(effectiveStart(c))}` : 'đang học'} · ${formatCurrency(c.fee)}` }))}
          />
        </Form.Item>
      </ModalForm>

      {/* Không học tiếp */}
      <ModalForm<{ reason: string; text?: string }>
        key={`decline-${declining?.c.id}`}
        open={Boolean(declining)}
        onClose={() => setDeclining(null)}
        title={`Không học tiếp · ${declining?.name ?? ''}`}
        description="Trả lại chỗ đã giữ trong lớp mới."
        submitAction="save"
        successMessage="Đã ghi nhận"
        onSubmit={(v) => {
          update(declining!.c.id, { status: 'declined', declineReason: v.reason, seatHeld: false });
          if (v.text) addNote([declining!.c.id], { by, text: v.text });
        }}
      >
        <Form.Item name="reason" label="Lý do" rules={[{ required: true, message: 'Chọn lý do' }]}>
          <Select options={DECLINE_REASONS.map((r) => ({ value: r, label: r }))} showSearch={false} />
        </Form.Item>
        <Form.Item name="text" label="Ghi chú">
          <TextArea rows={2} maxLength={500} />
        </Form.Item>
      </ModalForm>

      {/* Chi tiết */}
      <Drawer open={Boolean(detail)} onClose={() => setDetail(null)} width={560} title={detail ? `${detail.name} · lên lớp` : ''} destroyOnHidden>
        {detail && (
          <div className="flex flex-col gap-5 text-[13px]">
            <UserCell name={detail.dec.name} subtitle={`${detail.dec.kind === 'guardian' ? 'Phụ huynh (người quyết định)' : 'Học viên tự quyết định'}${detail.dec.phone ? ` · ${detail.dec.phone}` : ''}`} />
            <div>
              <h3 className="mb-2 font-semibold text-ink">Nhận xét cuối khóa của giáo viên</h3>
              {(() => {
                const ev = evaluations.find((e) => e.classId === detail.c.fromClassId && e.studentId === detail.c.studentId);
                return ev ? <EvaluationCard ev={ev} classLabel={detail.from?.code} /> : <p className="text-ink-muted">Giáo viên chưa viết nhận xét cuối khóa.</p>;
              })()}
            </div>
            <div>
              <h3 className="mb-2 font-semibold text-ink">Lịch sử tư vấn</h3>
              {detail.c.notes.length ? (
                <ul className="flex flex-col gap-2">
                  {detail.c.notes.map((n) => (
                    <li key={n.id} className="rounded-lg border border-line px-3 py-2">
                      <p className="text-ink">{n.text}</p>
                      <p className="mt-0.5 text-xs text-ink-muted">
                        {n.channel ? `${CONSULT_CHANNELS[n.channel].label} · ` : ''}
                        {n.by} · {fmtDateTime(n.at)}
                      </p>
                    </li>
                  ))}
                </ul>
              ) : (
                <p className="text-ink-muted">Chưa có.</p>
              )}
            </div>
            <p className="text-xs text-ink-muted">
              Thêm vào danh sách bởi {detail.c.createdBy} · {fmtDateTime(detail.c.createdAt)}
              {detail.c.handedOverAt && ` · giao Sale ${fmtDateTime(detail.c.handedOverAt)}`}
              {detail.c.confirmedAt && ` · ghi danh bởi ${detail.c.confirmedBy} ${fmtDateTime(detail.c.confirmedAt)}`}
            </p>
          </div>
        )}
      </Drawer>
    </>
  );
}
