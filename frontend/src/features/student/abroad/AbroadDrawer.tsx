import { useState, type ReactNode } from 'react';
import { Drawer, Steps } from 'antd';
import dayjs, { type Dayjs } from 'dayjs';
import { ArrowRight, Ban, Check, Circle, CircleDot, ExternalLink, RotateCcw, Send } from 'lucide-react';
import { useNavigate } from 'react-router-dom';
import { Button, DateInput, Select, StatusTag, TextInput, useConfirm, useNotify } from '@/components/common';
import { useCrmStore } from '@/features/crm/crmStore';
import { useAuth } from '@/hooks/useAuth';
import { useBreakpoint } from '@/hooks/useBreakpoint';
import { cn } from '@/lib/cn';
import { fmtDate, fmtDateTime } from '@/lib/date';
import { formatCurrency } from '@/lib/format';
import { ABROAD_STAFF, daysTo, docProgress, PROGRAMS, STAGE_ORDER, STAGES, useAbroadStore, type DocState } from './abroad';

const NEXT_DOC: Record<DocState, DocState> = { missing: 'received', received: 'verified', verified: 'missing' };
const DOC_LABEL: Record<DocState, string> = { missing: 'Chưa có', received: 'Đã nhận', verified: 'Đã kiểm tra' };

/** Hồ sơ du học / XKLĐ: tiến trình · giấy tờ · mốc thời gian · học phí dịch vụ · ghi chú */
export function AbroadDrawer({ caseId, editable, onClose }: { caseId: string | null; editable: boolean; onClose: () => void }) {
  const { isMobile } = useBreakpoint();
  const { user } = useAuth();
  const notify = useNotify();
  const navigate = useNavigate();
  const { confirm, confirmDialog } = useConfirm();
  const c = useAbroadStore((s) => s.cases.find((x) => x.id === caseId));
  const update = useAbroadStore((s) => s.update);
  const addNote = useAbroadStore((s) => s.note);
  const student = useCrmStore((s) => s.students.find((x) => x.id === c?.studentId));
  const [note, setNote] = useState('');

  if (!c) return <Drawer open={false} onClose={onClose} />;
  const me = user?.fullName ?? 'Nhân viên';
  const idx = STAGE_ORDER.indexOf(c.stage);
  const dp = docProgress(c);
  const pr = PROGRAMS[c.program];

  /** Điều kiện sang bước tiếp */
  const blocker = (): string | undefined => {
    if (c.stage === 'documents' && dp.done < dp.total) return `Còn ${dp.total - dp.done} giấy tờ chưa kiểm tra`;
    if (c.stage === 'interview' && c.interview?.result !== 'pass') return 'Chưa có kết quả đỗ phỏng vấn';
    if (c.stage === 'coe' && !c.coe?.grantedAt) return 'Chưa nhận COE';
    if (c.stage === 'visa' && !c.visa?.issuedAt) return 'Chưa có visa';
    return undefined;
  };
  const next = () => {
    const b = blocker();
    if (b) {
      notify.toast.warning(b);
      return;
    }
    const to = STAGE_ORDER[idx + 1];
    update(c.id, { stage: to });
    addNote(c.id, `Chuyển sang bước "${STAGES[to].label}".`, me);
  };
  const cancel = async () => {
    const res = await confirm({ title: 'Dừng hồ sơ này?', tone: 'danger', confirmAction: 'reject', confirmText: 'Dừng hồ sơ', requireReason: { label: 'Lý do', placeholder: 'VD: trượt phỏng vấn, học viên đổi ý, không chứng minh được tài chính' } });
    if (!res) return;
    update(c.id, { cancelled: { reason: res.reason!, at: new Date().toISOString() } });
    addNote(c.id, `Dừng hồ sơ: ${res.reason}`, me);
  };

  const dateField = (label: string, value: string | undefined, onChange: (v?: string) => void) => (
    <label className="flex flex-col gap-1">
      <span className="text-xs text-ink-muted">{label}</span>
      <DateInput disabled={!editable} value={value ? dayjs(value) : null} onChange={(d: Dayjs | null) => onChange(d?.format('YYYY-MM-DD'))} />
    </label>
  );

  const info: [string, ReactNode][] = [
    ['Chương trình', pr.label],
    ['Nơi tiếp nhận', `${c.partner} · ${c.destination}`],
    ...(c.job ? ([['Ngành nghề', c.job]] as [string, ReactNode][]) : []),
    ['Kỳ nhập học / xuất cảnh', dayjs(`${c.intake}-01`).format('MM/YYYY')],
    ['Yêu cầu tiếng Nhật', c.jlptRequired ? `Tối thiểu ${c.jlptRequired}` : '—'],
    ['Phí dịch vụ', <span>{formatCurrency(c.fee.paid)} / {formatCurrency(c.fee.total)}{c.fee.paid < c.fee.total && <span className="text-danger-600"> · còn {formatCurrency(c.fee.total - c.fee.paid)}</span>}</span>],
  ];

  return (
    <Drawer
      open
      onClose={onClose}
      width={isMobile ? '100%' : 780}
      destroyOnHidden
      title={
        <div className="flex min-w-0 flex-col gap-1 pr-2">
          <div className="flex flex-wrap items-center gap-2">
            <span className="truncate text-base font-semibold">{student?.fullName}</span>
            <StatusTag tone={pr.tone} icon={STAGES[c.stage].icon} size="sm">
              {pr.short}
            </StatusTag>
            {c.cancelled && (
              <StatusTag tone="danger" icon={Ban} size="sm">
                Đã dừng
              </StatusTag>
            )}
          </div>
          <span className="text-xs font-normal text-ink-muted">
            {student?.code} · {c.partner}
          </span>
        </div>
      }
      extra={
        <div className="flex gap-1.5">
          <Button icon={ExternalLink} size="sm" tone="neutral" variant="outline" collapseOnMobile onClick={() => navigate(`/admin/students/profiles?open=${c.studentId}`)}>
            Hồ sơ HV
          </Button>
          <Select value={c.assignee} onChange={(v) => update(c.id, { assignee: v as string })} options={ABROAD_STAFF.map((x) => ({ value: x, label: x }))} disabled={!editable} className="!w-56" size="small" />
        </div>
      }
    >
      <div className="flex flex-col gap-5 text-[13px]">
        <Steps
          size="small"
          current={idx}
          status={c.cancelled ? 'error' : c.stage === 'departed' ? 'finish' : 'process'}
          items={STAGE_ORDER.map((s) => ({ title: STAGES[s].label }))}
          responsive
        />
        {c.cancelled ? (
          <div className="flex flex-wrap items-center justify-between gap-2 rounded-lg border border-danger-100 bg-danger-50 px-3 py-2 text-danger-700">
            <span>Dừng ngày {fmtDate(c.cancelled.at)}: {c.cancelled.reason}</span>
            {editable && (
              <Button icon={RotateCcw} size="sm" variant="ghost" onClick={() => (update(c.id, { cancelled: undefined }), addNote(c.id, 'Mở lại hồ sơ.', me))}>
                Mở lại
              </Button>
            )}
          </div>
        ) : (
          editable &&
          c.stage !== 'departed' && (
            <div className="flex flex-wrap items-center justify-between gap-2 rounded-lg bg-page px-3 py-2">
              <span className="text-ink-secondary">
                Đang ở bước <strong className="text-ink">{STAGES[c.stage].label}</strong> — {STAGES[c.stage].hint}
                {blocker() && <span className="text-warning-700"> · {blocker()}</span>}
              </span>
              <div className="flex gap-1.5">
                <Button icon={Ban} size="sm" tone="danger" variant="ghost" onClick={cancel}>
                  Dừng
                </Button>
                <Button icon={ArrowRight} size="sm" onClick={next}>
                  Sang "{STAGES[STAGE_ORDER[idx + 1]].label}"
                </Button>
              </div>
            </div>
          )
        )}

        <dl className="grid grid-cols-1 gap-x-4 gap-y-1.5 sm:grid-cols-[170px_1fr]">
          {info.map(([k, v]) => (
            <div key={k} className="contents">
              <dt className="text-ink-muted">{k}</dt>
              <dd className="text-ink">{v}</dd>
            </div>
          ))}
        </dl>

        {/* Giấy tờ */}
        <section>
          <h3 className="mb-2 flex items-center justify-between text-sm font-semibold text-ink">
            Giấy tờ
            <span className="tabular text-xs font-normal text-ink-muted">
              Đã kiểm tra {dp.done}/{dp.total} · bấm để đổi trạng thái
            </span>
          </h3>
          <ul className="grid grid-cols-1 gap-1.5 sm:grid-cols-2">
            {c.docs.map((d, i) => (
              <li key={d.name}>
                <button
                  type="button"
                  disabled={!editable}
                  onClick={() => update(c.id, { docs: c.docs.map((x, k) => (k === i ? { ...x, state: NEXT_DOC[x.state] } : x)) })}
                  className={cn(
                    'flex w-full items-center justify-between gap-2 rounded-lg border px-3 py-2 text-left',
                    d.state === 'verified' ? 'border-success-100 bg-success-50' : d.state === 'received' ? 'border-primary-200 bg-primary-50' : 'border-line bg-surface',
                  )}
                >
                  <span className="flex items-center gap-2 text-ink">
                    {d.state === 'verified' ? <Check size={15} className="text-success-700" aria-hidden /> : d.state === 'received' ? <CircleDot size={15} className="text-primary-600" aria-hidden /> : <Circle size={15} className="text-ink-muted" aria-hidden />}
                    {d.name}
                  </span>
                  <span className={cn('text-xs', d.state === 'missing' ? 'text-danger-600' : 'text-ink-muted')}>{DOC_LABEL[d.state]}</span>
                </button>
              </li>
            ))}
          </ul>
        </section>

        {/* Mốc thời gian */}
        <section>
          <h3 className="mb-2 text-sm font-semibold text-ink">Mốc thời gian</h3>
          <div className="grid grid-cols-1 gap-3 sm:grid-cols-3">
            {dateField('Ngày phỏng vấn', c.interview?.date, (v) => update(c.id, { interview: v ? { ...c.interview, date: v } : undefined }))}
            <label className="flex flex-col gap-1">
              <span className="text-xs text-ink-muted">Kết quả phỏng vấn</span>
              <Select
                disabled={!editable || !c.interview}
                value={c.interview?.result}
                placeholder="Chờ kết quả"
                onChange={(v) => c.interview && update(c.id, { interview: { ...c.interview, result: v as 'pass' | 'fail' } })}
                options={[
                  { value: 'pass', label: 'Đỗ' },
                  { value: 'fail', label: 'Trượt' },
                ]}
                showSearch={false}
              />
            </label>
            <div />
            {dateField('Nộp hồ sơ COE', c.coe?.submittedAt, (v) => update(c.id, { coe: { ...c.coe, submittedAt: v } }))}
            {dateField('Nhận COE', c.coe?.grantedAt, (v) => update(c.id, { coe: { ...c.coe, grantedAt: v } }))}
            <div className="flex items-end text-xs text-ink-muted">{c.coe?.submittedAt && !c.coe.grantedAt && `Đã chờ ${-(daysTo(c.coe.submittedAt) ?? 0)} ngày`}</div>
            {dateField('Nộp visa', c.visa?.appliedAt, (v) => update(c.id, { visa: { ...c.visa, appliedAt: v } }))}
            {dateField('Nhận visa', c.visa?.issuedAt, (v) => update(c.id, { visa: { ...c.visa, issuedAt: v } }))}
            {dateField('Ngày bay', c.flightDate, (v) => update(c.id, { flightDate: v }))}
          </div>
        </section>

        {/* Ghi chú */}
        <section>
          <h3 className="mb-2 text-sm font-semibold text-ink">Nhật ký xử lý</h3>
          {editable && (
            <div className="mb-2 flex gap-2">
              <TextInput value={note} onChange={(e) => setNote(e.target.value)} placeholder="VD: Gọi phụ huynh bổ sung sổ tiết kiệm trước 30/11" onPressEnter={() => note.trim() && (addNote(c.id, note.trim(), me), setNote(''))} />
              <Button icon={Send} disabled={!note.trim()} onClick={() => (addNote(c.id, note.trim(), me), setNote(''))}>
                Ghi
              </Button>
            </div>
          )}
          <ol className="flex flex-col gap-1.5 border-l-2 border-line pl-3">
            {[...c.notes].reverse().map((n, i) => (
              <li key={i}>
                <span className="text-xs text-ink-muted">{fmtDateTime(n.at)} · {n.by}</span>
                <p className="text-ink">{n.text}</p>
              </li>
            ))}
          </ol>
        </section>
      </div>
      {confirmDialog}
    </Drawer>
  );
}

