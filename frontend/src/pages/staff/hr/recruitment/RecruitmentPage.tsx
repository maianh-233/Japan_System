import { useState } from 'react';
import { Drawer, Form, Radio, Steps } from 'antd';
import dayjs, { type Dayjs } from 'dayjs';
import { Banknote, Briefcase, CalendarClock, FileText, Mail, MessageSquarePlus, Pencil, Phone, Star, UserCheck, UserPlus, Users, XCircle } from 'lucide-react';
import { useNavigate } from 'react-router-dom';
import { Button, DataTable, DateInput, ModalForm, NumberInput, PageHeader, Select, StatCard, StatusTag, TextArea, TextInput, UserCell, useConfirm, useNotify, type DataColumn } from '@/components/common';
import { CANDIDATE_STAGES, DEPARTMENT_OPTIONS, DEPARTMENTS, EMPLOYEE_KIND, ONBOARDING_TEMPLATE, PIPELINE } from '@/features/hr/constants';
import { useHrStore } from '@/features/hr/hrStore';
import type { Candidate, CandidateStage, Department, EmployeeKind, Job } from '@/features/hr/types';
import { useAuth } from '@/hooks/useAuth';
import { useBreakpoint } from '@/hooks/useBreakpoint';
import { cn } from '@/lib/cn';
import { fmtDate, fmtDateTime } from '@/lib/date';
import { formatCurrency } from '@/lib/format';
import { getBranches } from '@/features/system/useBranches';
import { ALL_BRANCHES } from '@/types/auth';

type View = 'pipeline' | 'jobs';
const SOURCES = ['Website', 'Facebook', 'TopCV', 'VietnamWorks', 'Giới thiệu nội bộ', 'Đại học (hợp tác)'];
const CHANNELS = ['Website', 'Facebook', 'TopCV', 'VietnamWorks', 'Nhóm GV tiếng Nhật', 'Đại học (hợp tác)'];
const JOB_STATUS = { open: { label: 'Đang tuyển', tone: 'success' as const }, paused: { label: 'Tạm dừng', tone: 'warning' as const }, closed: { label: 'Đã đóng', tone: 'neutral' as const } };

interface JobValues {
  title: string;
  branchId: string;
  kind: EmployeeKind;
  department: Department;
  quantity: number;
  salaryRange?: string;
  deadline: Dayjs;
  channels: string[];
  status: Job['status'];
  description?: string;
}
interface CandValues {
  jobId: string;
  fullName: string;
  phone: string;
  email: string;
  source: string;
  japanese?: string;
  expectedSalary?: number;
  cvName?: string;
}

/** Tuyển dụng: vị trí tuyển + pipeline ứng viên → onboarding. DỮ LIỆU GIẢ. */
export default function RecruitmentPage() {
  const { user, branchId } = useAuth();
  const { isMobile } = useBreakpoint();
  const notify = useNotify();
  const navigate = useNavigate();
  const { confirm, confirmDialog } = useConfirm();
  const jobs = useHrStore((s) => s.jobs);
  const candidates = useHrStore((s) => s.candidates);
  const onboardings = useHrStore((s) => s.onboardings);
  const addJob = useHrStore((s) => s.addJob);
  const updateJob = useHrStore((s) => s.updateJob);
  const addCandidate = useHrStore((s) => s.addCandidate);
  const updateCandidate = useHrStore((s) => s.updateCandidate);
  const noteCandidate = useHrStore((s) => s.noteCandidate);
  const addOnboarding = useHrStore((s) => s.addOnboarding);

  const [view, setView] = useState<View>('pipeline');
  const [jobFilter, setJobFilter] = useState<string>();
  const [showRejected, setShowRejected] = useState(false);
  const [openId, setOpenId] = useState<string | null>(null);
  const [jobEdit, setJobEdit] = useState<{ job?: Job } | null>(null);
  const [candNew, setCandNew] = useState(false);
  const [interviewFor, setInterviewFor] = useState<Candidate | null>(null);
  const [offerFor, setOfferFor] = useState<Candidate | null>(null);
  const [note, setNote] = useState('');
  const [dragId, setDragId] = useState<string | null>(null);

  const scopedJobs = jobs.filter((j) => branchId === ALL_BRANCHES || j.branchId === branchId);
  const jobIds = new Set(scopedJobs.map((j) => j.id));
  const cands = candidates.filter((c) => jobIds.has(c.jobId) && (!jobFilter || c.jobId === jobFilter));
  const jobOf = (id: string) => jobs.find((j) => j.id === id);
  const open = candidates.find((c) => c.id === openId);
  const today = dayjs();

  const stats = {
    open: scopedJobs.filter((j) => j.status === 'open').reduce((n, j) => n + j.quantity, 0),
    active: cands.filter((c) => c.stage !== 'hired' && c.stage !== 'rejected').length,
    interviews: cands.filter((c) => c.stage === 'interview' && c.interview && dayjs(c.interview.at).isAfter(today.startOf('day')) && dayjs(c.interview.at).isBefore(today.add(7, 'day'))).length,
    offers: cands.filter((c) => c.stage === 'offer' && c.offer?.accepted === undefined).length,
  };

  /** Nhận việc → tạo checklist onboarding */
  const hire = async (c: Candidate) => {
    const job = jobOf(c.jobId);
    const ok = await confirm({
      title: `${c.fullName} nhận việc?`,
      description: `Tạo checklist onboarding cho vị trí ${job?.title}. Ngày bắt đầu: ${c.offer ? fmtDate(c.offer.startDate) : 'chưa có offer'}.`,
      confirmText: 'Nhận việc & tạo onboarding',
      tone: 'primary',
    });
    if (!ok || !job) return;
    updateCandidate(c.id, { stage: 'hired', offer: c.offer ? { ...c.offer, accepted: true } : undefined });
    const ob = addOnboarding({
      candidateId: c.id,
      fullName: c.fullName,
      phone: c.phone,
      email: c.email,
      position: job.title,
      department: job.department,
      kind: job.kind,
      branchId: job.branchId,
      startDate: c.offer?.startDate ?? today.add(7, 'day').format('YYYY-MM-DD'),
      mentor: user?.fullName ?? '',
      items: ONBOARDING_TEMPLATE.filter((t) => !t.teacherOnly || job.kind !== 'staff').map((t) => ({ key: t.key, group: t.group, label: t.label, done: false })),
    });
    const hiredCount = candidates.filter((x) => x.jobId === job.id && x.stage === 'hired').length + 1;
    if (hiredCount >= job.quantity && job.status === 'open') updateJob(job.id, { status: 'closed' });
    notify.toast.success(`Đã tạo onboarding ${ob.id}${hiredCount >= job.quantity ? ' · vị trí đã đủ người, tự đóng tin tuyển' : ''}`);
    setOpenId(null);
    navigate('/admin/hr/onboarding');
  };

  const reject = async (c: Candidate) => {
    const res = await confirm({ title: `Loại ứng viên ${c.fullName}?`, tone: 'danger', confirmAction: 'reject', confirmText: 'Loại', requireReason: { label: 'Lý do', placeholder: 'VD: chưa đạt N2, lương kỳ vọng quá cao' } });
    if (!res) return;
    updateCandidate(c.id, { stage: 'rejected', rejectReason: typeof res === 'object' ? res.reason : undefined });
    notify.toast.info('Đã loại ứng viên — gửi email cảm ơn tự động');
  };

  /** Chuyển giai đoạn (kéo thả hoặc nút) — giai đoạn cần thông tin sẽ mở form */
  const moveTo = (c: Candidate, stage: CandidateStage) => {
    if (stage === c.stage) return;
    if (stage === 'interview' && !c.interview) return setInterviewFor(c);
    if (stage === 'offer' && !c.offer) return setOfferFor(c);
    if (stage === 'hired') return void hire(c);
    if (stage === 'rejected') return void reject(c);
    updateCandidate(c.id, { stage });
    notify.toast.success(`${c.fullName} → ${CANDIDATE_STAGES[stage].label}`);
  };

  const nextStage = (c: Candidate): CandidateStage | undefined => {
    const i = PIPELINE.indexOf(c.stage);
    return i >= 0 && i < PIPELINE.length - 1 ? PIPELINE[i + 1] : undefined;
  };

  const jobColumns: DataColumn<Job>[] = [
    {
      key: 'title',
      title: 'Vị trí',
      width: 240,
      mobile: 'title',
      render: (_, j) => (
        <div className="leading-5">
          <p className="font-medium text-ink">{j.title}</p>
          <p className="text-xs text-ink-muted">{DEPARTMENTS[j.department]} · {getBranches(false).find((b) => b.id === j.branchId)?.name}</p>
        </div>
      ),
    },
    { key: 'quantity', title: 'Số lượng', width: 100, align: 'center', render: (_, j) => <span className="tabular">{candidates.filter((c) => c.jobId === j.id && c.stage === 'hired').length}/{j.quantity}</span> },
    {
      key: 'cands',
      title: 'Ứng viên',
      width: 200,
      render: (_, j) => {
        const list = candidates.filter((c) => c.jobId === j.id);
        return (
          <span className="flex flex-wrap gap-1 text-xs">
            <span className="rounded bg-disabled px-1.5 py-0.5">{list.length} hồ sơ</span>
            {list.filter((c) => c.stage === 'interview').length > 0 && <span className="rounded bg-primary-50 px-1.5 py-0.5 text-primary-700">{list.filter((c) => c.stage === 'interview').length} PV</span>}
            {list.filter((c) => c.stage === 'offer').length > 0 && <span className="rounded bg-warning-50 px-1.5 py-0.5 text-warning-700">{list.filter((c) => c.stage === 'offer').length} offer</span>}
          </span>
        );
      },
    },
    { key: 'salaryRange', title: 'Mức lương', width: 150, render: (v) => (v as string) ?? '—' },
    { key: 'deadline', title: 'Hạn nộp', width: 120, render: (_, j) => <span className={cn(j.status === 'open' && dayjs(j.deadline).isBefore(today, 'day') && 'font-medium text-danger-600')}>{fmtDate(j.deadline)}</span> },
    { key: 'channels', title: 'Kênh đăng', width: 200, render: (_, j) => <span className="text-xs text-ink-secondary">{j.channels.join(', ')}</span> },
    { key: 'status', title: 'Trạng thái', width: 120, mobile: 'aside', render: (_, j) => <StatusTag tone={JOB_STATUS[j.status].tone} icon={Briefcase} size="sm">{JOB_STATUS[j.status].label}</StatusTag> },
  ];

  const columnsStages: CandidateStage[] = showRejected ? [...PIPELINE, 'rejected'] : PIPELINE;

  return (
    <>
      <PageHeader
        title="Tuyển dụng"
        subtitle="Đăng tin tuyển → sàng lọc CV → phỏng vấn → offer → nhận việc (tự tạo checklist onboarding)"
        breadcrumb={[{ title: 'Nhân sự' }, { title: 'Tuyển dụng' }]}
        actions={
          <div className="flex gap-2">
            <Button icon={Briefcase} variant="outline" tone="neutral" onClick={() => setJobEdit({})} collapseOnMobile>
              Đăng tin tuyển
            </Button>
            <Button icon={UserPlus} onClick={() => setCandNew(true)} collapseOnMobile>
              Thêm ứng viên
            </Button>
          </div>
        }
      />

      <div className="mb-4 grid grid-cols-2 gap-3 lg:grid-cols-4">
        <StatCard label="Chỉ tiêu đang tuyển" value={stats.open} icon={Briefcase} onClick={() => setView('jobs')} />
        <StatCard label="Ứng viên đang xử lý" value={stats.active} icon={Users} tone="info" onClick={() => setView('pipeline')} />
        <StatCard label="Phỏng vấn 7 ngày tới" value={stats.interviews} icon={CalendarClock} tone="primary" />
        <StatCard label="Offer chờ phản hồi" value={stats.offers} icon={Banknote} tone={stats.offers ? 'warning' : 'success'} />
      </div>

      <div className="mb-3 flex flex-wrap items-center gap-2">
        {(
          [
            ['pipeline', 'Pipeline ứng viên'],
            ['jobs', 'Vị trí tuyển'],
          ] as [View, string][]
        ).map(([k, l]) => (
          <button key={k} type="button" onClick={() => setView(k)} className={cn('h-9 rounded-full border px-3.5 text-sm font-medium', view === k ? 'border-primary-600 bg-primary-600 text-white' : 'border-line bg-surface text-ink-secondary hover:bg-primary-50')}>
            {l}
          </button>
        ))}
        {view === 'pipeline' && (
          <>
            <Select placeholder="Tất cả vị trí" value={jobFilter} onChange={setJobFilter} options={scopedJobs.map((j) => ({ value: j.id, label: j.title }))} className="!w-64" />
            <label className="ml-auto flex cursor-pointer items-center gap-1.5 text-sm text-ink-secondary">
              <input type="checkbox" checked={showRejected} onChange={(e) => setShowRejected(e.target.checked)} className="accent-primary-600" />
              Hiện ứng viên bị loại ({cands.filter((c) => c.stage === 'rejected').length})
            </label>
          </>
        )}
      </div>

      {view === 'pipeline' ? (
        <div className="-mx-4 overflow-x-auto px-4 pb-2 lg:mx-0 lg:px-0">
          <div className="flex min-w-max gap-3">
            {columnsStages.map((st) => {
              const S = CANDIDATE_STAGES[st];
              const list = cands.filter((c) => c.stage === st).sort((a, b) => b.appliedAt.localeCompare(a.appliedAt));
              return (
                <div
                  key={st}
                  onDragOver={(e) => e.preventDefault()}
                  onDrop={() => {
                    const c = candidates.find((x) => x.id === dragId);
                    setDragId(null);
                    if (c) moveTo(c, st);
                  }}
                  className={cn('flex w-64 shrink-0 flex-col rounded-xl border border-line bg-canvas', dragId && 'border-dashed border-primary-300')}
                >
                  <div className="flex items-center gap-2 border-b border-line px-3 py-2.5">
                    <S.icon size={15} className="text-ink-muted" aria-hidden />
                    <span className="text-sm font-semibold text-ink">{S.label}</span>
                    <span className="tabular ml-auto rounded-full bg-disabled px-2 text-xs text-ink-muted">{list.length}</span>
                  </div>
                  <div className="flex min-h-32 flex-col gap-2 p-2">
                    {list.map((c) => {
                      const job = jobOf(c.jobId);
                      const stale = st !== 'hired' && st !== 'rejected' && today.diff(dayjs(c.notes[c.notes.length - 1]?.at ?? c.appliedAt), 'day') > 5;
                      return (
                        <button
                          key={c.id}
                          type="button"
                          draggable
                          onDragStart={() => setDragId(c.id)}
                          onDragEnd={() => setDragId(null)}
                          onClick={() => setOpenId(c.id)}
                          className="flex flex-col gap-1.5 rounded-lg border border-line bg-surface p-2.5 text-left shadow-xs hover:border-primary-300"
                        >
                          <UserCell size="sm" name={c.fullName} subtitle={job?.title} />
                          <div className="flex flex-wrap items-center gap-1 text-[11px]">
                            <span className="rounded bg-disabled px-1.5 py-0.5 text-ink-secondary">{c.source}</span>
                            {c.japanese && <span className="rounded bg-primary-50 px-1.5 py-0.5 text-primary-700">{c.japanese}</span>}
                            {c.interview?.score !== undefined && (
                              <span className="inline-flex items-center gap-0.5 rounded bg-warning-50 px-1.5 py-0.5 text-warning-700">
                                <Star size={10} aria-hidden /> {c.interview.score}/10
                              </span>
                            )}
                          </div>
                          {st === 'interview' && c.interview && <p className="text-xs text-ink-secondary">PV: {fmtDateTime(c.interview.at)} · {c.interview.mode === 'online' ? 'Online' : 'Tại TT'}</p>}
                          {st === 'offer' && c.offer && <p className="text-xs text-ink-secondary">{formatCurrency(c.offer.salary)} · bắt đầu {fmtDate(c.offer.startDate)}</p>}
                          {st === 'rejected' && c.rejectReason && <p className="line-clamp-2 text-xs text-ink-muted">{c.rejectReason}</p>}
                          {stale && <p className="text-[11px] font-medium text-warning-700">Chưa xử lý &gt; 5 ngày</p>}
                        </button>
                      );
                    })}
                    {!list.length && <p className="py-6 text-center text-xs text-ink-muted">Kéo ứng viên vào đây</p>}
                  </div>
                </div>
              );
            })}
          </div>
        </div>
      ) : (
        <DataTable<Job>
          storageKey="hr-jobs"
          columns={jobColumns}
          data={scopedJobs}
          rowKey="id"
          unitLabel="vị trí"
          searchKeys={['title']}
          onRowClick={(j) => {
            setJobFilter(j.id);
            setView('pipeline');
          }}
          rowActions={(j) => <Button icon={Pencil} iconOnly size="sm" variant="ghost" tone="neutral" tooltip="Sửa" aria-label="Sửa" onClick={() => setJobEdit({ job: j })} />}
          actionsWidth={70}
          empty={{ icon: Briefcase, title: 'Chưa có vị trí tuyển' }}
        />
      )}

      {/* Chi tiết ứng viên */}
      <Drawer open={Boolean(open)} onClose={() => setOpenId(null)} width={isMobile ? '100%' : 560} destroyOnHidden title={open?.fullName}>
        {open && (
          <div className="flex flex-col gap-4 text-[13px]">
            <div className="flex flex-wrap items-center gap-2">
              <StatusTag tone={CANDIDATE_STAGES[open.stage].tone} icon={CANDIDATE_STAGES[open.stage].icon}>{CANDIDATE_STAGES[open.stage].label}</StatusTag>
              <span className="text-ink-secondary">ứng tuyển {jobOf(open.jobId)?.title} · {fmtDate(open.appliedAt)}</span>
            </div>
            {open.stage !== 'rejected' && (
              <Steps
                size="small"
                current={PIPELINE.indexOf(open.stage)}
                items={PIPELINE.map((s) => ({ title: CANDIDATE_STAGES[s].label }))}
                responsive={false}
                className="[&_.ant-steps-item-title]:!text-xs"
              />
            )}
            <dl className="grid grid-cols-[120px_1fr] gap-y-1.5">
              <dt className="text-ink-muted">Liên hệ</dt>
              <dd className="flex flex-wrap gap-x-3">
                <a href={`tel:${open.phone}`} className="inline-flex items-center gap-1 text-primary-600"><Phone size={12} />{open.phone}</a>
                <a href={`mailto:${open.email}`} className="inline-flex items-center gap-1 text-primary-600"><Mail size={12} />{open.email}</a>
              </dd>
              <dt className="text-ink-muted">Nguồn</dt>
              <dd>{open.source}</dd>
              {open.japanese && (<><dt className="text-ink-muted">Tiếng Nhật</dt><dd>{open.japanese}</dd></>)}
              {open.expectedSalary && (<><dt className="text-ink-muted">Lương kỳ vọng</dt><dd className="tabular">{formatCurrency(open.expectedSalary)}</dd></>)}
              {open.cvName && (<><dt className="text-ink-muted">CV</dt><dd className="inline-flex items-center gap-1 text-primary-600"><FileText size={13} />{open.cvName}</dd></>)}
              {open.interview && (
                <>
                  <dt className="text-ink-muted">Phỏng vấn</dt>
                  <dd>
                    {fmtDateTime(open.interview.at)} · {open.interview.mode === 'online' ? 'Online' : 'Tại trung tâm'} · {open.interview.interviewer}
                    {open.interview.score !== undefined && <span className="ml-1 font-semibold">— {open.interview.score}/10</span>}
                    {open.interview.note && <p className="text-ink-secondary">{open.interview.note}</p>}
                  </dd>
                </>
              )}
              {open.offer && (
                <>
                  <dt className="text-ink-muted">Offer</dt>
                  <dd>
                    {formatCurrency(open.offer.salary)} · bắt đầu {fmtDate(open.offer.startDate)} · gửi {fmtDate(open.offer.sentAt)}
                    {open.offer.accepted === true && <span className="ml-1 text-success-700">· đã đồng ý</span>}
                    {open.offer.accepted === false && <span className="ml-1 text-danger-600">· từ chối</span>}
                  </dd>
                </>
              )}
              {open.rejectReason && (<><dt className="text-ink-muted">Lý do loại</dt><dd>{open.rejectReason}</dd></>)}
            </dl>

            {open.stage !== 'hired' && open.stage !== 'rejected' && (
              <div className="flex flex-wrap gap-2">
                {(open.stage === 'screening' || open.stage === 'interview') && (
                  <Button size="sm" variant="soft" icon={CalendarClock} onClick={() => setInterviewFor(open)}>
                    {open.interview ? 'Sửa lịch / chấm điểm PV' : 'Hẹn phỏng vấn'}
                  </Button>
                )}
                {(open.stage === 'interview' || open.stage === 'offer') && (
                  <Button size="sm" variant="soft" icon={Banknote} onClick={() => setOfferFor(open)}>
                    {open.offer ? 'Sửa offer' : 'Gửi offer'}
                  </Button>
                )}
                {open.stage === 'offer' && open.offer && (
                  <Button size="sm" variant="soft" tone="neutral" icon={XCircle} onClick={() => (updateCandidate(open.id, { stage: 'rejected', offer: { ...open.offer!, accepted: false }, rejectReason: 'Ứng viên từ chối offer' }), notify.toast.info('Đã ghi nhận ứng viên từ chối offer'))}>
                    Ứng viên từ chối offer
                  </Button>
                )}
                {nextStage(open) && (
                  <Button size="sm" icon={nextStage(open) === 'hired' ? UserCheck : CANDIDATE_STAGES[nextStage(open)!].icon} onClick={() => moveTo(open, nextStage(open)!)}>
                    {nextStage(open) === 'hired' ? 'Nhận việc → onboarding' : `Chuyển: ${CANDIDATE_STAGES[nextStage(open)!].label}`}
                  </Button>
                )}
                <Button size="sm" tone="danger" variant="ghost" icon={XCircle} onClick={() => reject(open)}>
                  Loại
                </Button>
              </div>
            )}
            {open.stage === 'hired' && (
              <Button size="sm" variant="soft" icon={UserCheck} onClick={() => navigate('/admin/hr/onboarding')}>
                Xem onboarding {onboardings.find((o) => o.candidateId === open.id)?.id ?? ''}
              </Button>
            )}

            <div className="border-t border-line pt-3">
              <p className="mb-2 font-semibold text-ink">Ghi chú trao đổi</p>
              <ul className="mb-2 flex flex-col gap-2">
                {open.notes.map((n, i) => (
                  <li key={i} className="rounded-lg bg-canvas px-3 py-2">
                    <p className="text-xs text-ink-muted">{n.by} · {fmtDateTime(n.at)}</p>
                    <p>{n.text}</p>
                  </li>
                ))}
                {!open.notes.length && <li className="text-ink-muted">Chưa có ghi chú</li>}
              </ul>
              <div className="flex gap-2">
                <TextInput value={note} onChange={(e) => setNote(e.target.value)} placeholder="VD: đã gọi, hẹn gửi bằng N2 bản gốc" onPressEnter={() => note.trim() && (noteCandidate(open.id, user?.fullName ?? '', note.trim()), setNote(''))} />
                <Button icon={MessageSquarePlus} iconOnly aria-label="Thêm ghi chú" disabled={!note.trim()} onClick={() => (noteCandidate(open.id, user?.fullName ?? '', note.trim()), setNote(''))} />
              </div>
            </div>
          </div>
        )}
      </Drawer>

      {/* Hẹn phỏng vấn */}
      <ModalForm<{ at: Dayjs; interviewer: string; mode: 'offline' | 'online'; score?: number; note?: string }>
        key={interviewFor?.id ?? 'iv'}
        open={Boolean(interviewFor)}
        onClose={() => setInterviewFor(null)}
        title={`Phỏng vấn — ${interviewFor?.fullName ?? ''}`}
        submitAction="save"
        successMessage="Đã lưu lịch phỏng vấn — gửi email mời cho ứng viên"
        initialValues={interviewFor?.interview ? { ...interviewFor.interview, at: dayjs(interviewFor.interview.at) } : { mode: 'offline', interviewer: user?.fullName }}
        onSubmit={(v) => {
          updateCandidate(interviewFor!.id, { stage: 'interview', interview: { at: v.at.format('YYYY-MM-DDTHH:mm'), interviewer: v.interviewer, mode: v.mode, score: v.score ?? undefined, note: v.note?.trim() || undefined } });
        }}
      >
        <Form.Item name="at" label="Thời gian" rules={[{ required: true, message: 'Chọn thời gian' }]}>
          <DateInput showTime={{ format: 'HH:mm', minuteStep: 15 }} />
        </Form.Item>
        <Form.Item name="mode" label="Hình thức">
          <Radio.Group options={[{ value: 'offline', label: 'Tại trung tâm' }, { value: 'online', label: 'Online (Google Meet)' }]} />
        </Form.Item>
        <Form.Item name="interviewer" label="Người phỏng vấn" rules={[{ required: true, whitespace: true }]}>
          <TextInput placeholder="VD: Nguyễn Văn Nam, Sato Kenji (dạy thử)" />
        </Form.Item>
        <Form.Item name="score" label="Điểm đánh giá (sau PV)" extra="Để trống nếu chưa phỏng vấn.">
          <NumberInput min={0} max={10} step={0.5} />
        </Form.Item>
        <Form.Item name="note" label="Nhận xét">
          <TextArea rows={2} />
        </Form.Item>
      </ModalForm>

      {/* Offer */}
      <ModalForm<{ salary: number; startDate: Dayjs }>
        key={offerFor?.id ?? 'of'}
        open={Boolean(offerFor)}
        onClose={() => setOfferFor(null)}
        title={`Gửi offer — ${offerFor?.fullName ?? ''}`}
        submitAction="send"
        submitText="Gửi offer"
        successMessage="Đã gửi thư mời nhận việc qua email"
        initialValues={offerFor?.offer ? { salary: offerFor.offer.salary, startDate: dayjs(offerFor.offer.startDate) } : { salary: offerFor?.expectedSalary, startDate: dayjs().add(14, 'day') }}
        onSubmit={(v) => {
          updateCandidate(offerFor!.id, { stage: 'offer', offer: { salary: v.salary, startDate: v.startDate.format('YYYY-MM-DD'), sentAt: dayjs().format('YYYY-MM-DD') } });
        }}
      >
        <Form.Item name="salary" label="Mức lương offer (thử việc)" rules={[{ required: true, message: 'Nhập mức lương' }]} extra={offerFor?.expectedSalary ? `Ứng viên kỳ vọng ${formatCurrency(offerFor.expectedSalary)}` : undefined}>
          <NumberInput currency min={0} />
        </Form.Item>
        <Form.Item name="startDate" label="Ngày bắt đầu làm" rules={[{ required: true, message: 'Chọn ngày' }]}>
          <DateInput />
        </Form.Item>
      </ModalForm>

      {/* Thêm ứng viên */}
      <ModalForm<CandValues>
        open={candNew}
        onClose={() => setCandNew(false)}
        width={620}
        title="Thêm ứng viên"
        submitAction="create"
        successMessage="Đã thêm ứng viên"
        initialValues={{ jobId: jobFilter ?? scopedJobs.find((j) => j.status === 'open')?.id, source: 'Website' }}
        columns={2}
        onSubmit={(v) => {
          addCandidate({ ...v, fullName: v.fullName.trim(), stage: 'applied', cvName: v.cvName?.trim() || undefined, japanese: v.japanese?.trim() || undefined });
          setView('pipeline');
        }}
      >
        <Form.Item name="jobId" label="Vị trí" rules={[{ required: true }]} className="md:col-span-2">
          <Select options={scopedJobs.filter((j) => j.status !== 'closed').map((j) => ({ value: j.id, label: j.title }))} />
        </Form.Item>
        <Form.Item name="fullName" label="Họ tên" rules={[{ required: true, whitespace: true }]}>
          <TextInput />
        </Form.Item>
        <Form.Item name="source" label="Nguồn">
          <Select options={SOURCES.map((s) => ({ value: s, label: s }))} showSearch={false} />
        </Form.Item>
        <Form.Item name="phone" label="Điện thoại" rules={[{ required: true, pattern: /^0\d{9}$/, message: 'SĐT 10 số' }]}>
          <TextInput maxLength={10} inputMode="tel" />
        </Form.Item>
        <Form.Item name="email" label="Email" rules={[{ required: true, type: 'email' }]}>
          <TextInput />
        </Form.Item>
        <Form.Item name="japanese" label="Trình độ tiếng Nhật">
          <TextInput placeholder="VD: N2 (2023)" />
        </Form.Item>
        <Form.Item name="expectedSalary" label="Lương kỳ vọng">
          <NumberInput currency min={0} />
        </Form.Item>
        <Form.Item name="cvName" label="Tên file CV" className="md:col-span-2">
          <TextInput placeholder="VD: CV_NguyenVanA.pdf" />
        </Form.Item>
      </ModalForm>

      {/* Đăng / sửa tin tuyển */}
      <ModalForm<JobValues>
        key={jobEdit?.job?.id ?? 'new-job'}
        open={Boolean(jobEdit)}
        onClose={() => setJobEdit(null)}
        width={680}
        title={jobEdit?.job ? 'Sửa tin tuyển' : 'Đăng tin tuyển dụng'}
        submitAction={jobEdit?.job ? 'save' : 'create'}
        successMessage={jobEdit?.job ? 'Đã lưu' : 'Đã đăng tin tuyển'}
        columns={2}
        initialValues={
          jobEdit?.job
            ? { ...jobEdit.job, deadline: dayjs(jobEdit.job.deadline) }
            : { kind: 'staff', department: 'sales', quantity: 1, status: 'open', branchId: branchId === ALL_BRANCHES ? getBranches(false)[0].id : branchId, deadline: dayjs().add(30, 'day'), channels: ['Website', 'Facebook'] }
        }
        onSubmit={(v) => {
          const data = { ...v, title: v.title.trim(), department: v.kind === 'staff' ? v.department : ('teaching' as Department), deadline: v.deadline.format('YYYY-MM-DD') };
          if (jobEdit?.job) updateJob(jobEdit.job.id, data);
          else addJob(data);
          setView('jobs');
        }}
      >
        <Form.Item name="title" label="Tên vị trí" rules={[{ required: true, whitespace: true }]} className="md:col-span-2">
          <TextInput placeholder="VD: Giáo viên tiếng Nhật N3–N2 (part-time)" />
        </Form.Item>
        <Form.Item name="kind" label="Loại">
          <Radio.Group options={(Object.keys(EMPLOYEE_KIND) as EmployeeKind[]).map((k) => ({ value: k, label: EMPLOYEE_KIND[k] }))} />
        </Form.Item>
        <Form.Item name="department" label="Phòng ban">
          <Select options={DEPARTMENT_OPTIONS} showSearch={false} allowClear={false} />
        </Form.Item>
        <Form.Item name="branchId" label="Chi nhánh">
          <Select options={getBranches(false).map((b) => ({ value: b.id, label: b.name }))} showSearch={false} allowClear={false} />
        </Form.Item>
        <Form.Item name="quantity" label="Số lượng cần tuyển" rules={[{ required: true }]}>
          <NumberInput min={1} />
        </Form.Item>
        <Form.Item name="salaryRange" label="Mức lương">
          <TextInput placeholder="VD: 9–12 triệu + hoa hồng" />
        </Form.Item>
        <Form.Item name="deadline" label="Hạn nộp hồ sơ" rules={[{ required: true }]}>
          <DateInput />
        </Form.Item>
        <Form.Item name="channels" label="Kênh đăng tin" className="md:col-span-2">
          <Select mode="multiple" options={CHANNELS.map((c) => ({ value: c, label: c }))} />
        </Form.Item>
        <Form.Item name="status" label="Trạng thái">
          <Radio.Group options={(Object.keys(JOB_STATUS) as Job['status'][]).map((k) => ({ value: k, label: JOB_STATUS[k].label }))} />
        </Form.Item>
        <Form.Item name="description" label="Mô tả công việc / yêu cầu" className="md:col-span-2">
          <TextArea rows={3} />
        </Form.Item>
      </ModalForm>
      {confirmDialog}
    </>
  );
}

