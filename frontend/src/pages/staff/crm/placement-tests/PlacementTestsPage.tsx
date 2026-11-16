import { useMemo, useState } from 'react';
import { Form, Tabs, Tooltip } from 'antd';
import dayjs from 'dayjs';
import { Archive, CheckCircle2, ClipboardCheck, Copy, FlaskConical, Link2, NotebookPen, Plus, Send, Shuffle, Timer, Trash2, TriangleAlert } from 'lucide-react';
import {
  Button,
  DataTable,
  ModalForm,
  NumberInput,
  PageHeader,
  Select,
  StatCard,
  StatusTag,
  TextArea,
  TextInput,
  useNotify,
  type DataColumn,
} from '@/components/common';
import { Q_LEVELS, Q_SKILL_OPTIONS, Q_SKILLS } from '@/features/academic/questions/constants';
import { useQuestionStore } from '@/features/academic/questions/questionStore';
import { useAdmissionStore, type TestInput } from '@/features/crm/admission/admissionStore';
import { AttemptDrawer } from '@/features/crm/admission/AttemptDrawer';
import { grade, pctOf, pickQuestions, poolOf, simulateAnswers, testLink } from '@/features/crm/admission/placement';
import { ATTEMPT_STATUS, COURSE_OPTIONS, courseLabel, PLACEMENT_STATUS, SEND_CHANNELS } from '@/features/crm/constants';
import { useCrmStore } from '@/features/crm/crmStore';
import type { PlacementAttempt, PlacementTest } from '@/features/crm/types';
import { useAuth } from '@/hooks/useAuth';
import { cn } from '@/lib/cn';
import { fmtDateTime, fromNow } from '@/lib/date';
import { ALL_BRANCHES } from '@/types/auth';

/** Lượt làm bài + chuỗi tìm kiếm (tên, SĐT lead) */
type Row = PlacementAttempt & { q: string };

interface SendValues {
  testId: string;
  leadId: string;
  channel: PlacementAttempt['channel'];
  validDays: number;
}

/** Test xếp lớp: bài test bốc từ Ngân hàng câu hỏi, gửi link cho lead, tự chấm & gợi ý khóa. DỮ LIỆU GIẢ. */
export default function PlacementTestsPage() {
  const { user, branchId, can } = useAuth();
  const notify = useNotify();
  const { tests, attempts, addTest, updateTest, sendTest, updateAttempt } = useAdmissionStore();
  const leads = useCrmStore((s) => s.leads);
  const { updateLead, addConsultation } = useCrmStore();
  const questions = useQuestionStore((s) => s.questions);
  const editable = can('crm.placement-tests');

  const [tab, setTab] = useState<'attempts' | 'tests'>('attempts');
  const [status, setStatus] = useState<string>();
  const [openId, setOpenId] = useState<string | null>(null);
  const [editing, setEditing] = useState<PlacementTest | 'new' | null>(null);
  const [sending, setSending] = useState<Partial<SendValues> | null>(null);

  const leadOf = (id: string) => leads.find((l) => l.id === id);
  const testOf = (id: string) => tests.find((t) => t.id === id);
  const inBranch = (a: PlacementAttempt) => branchId === ALL_BRANCHES || leadOf(a.leadId)?.branchId === branchId;
  const rows = useMemo(
    () =>
      attempts
        .filter(inBranch)
        .map((a): Row => ({
          ...a,
          // Link quá hạn mà chưa nộp → hiển thị "Hết hạn"
          status: a.status !== 'done' && a.status !== 'expired' && dayjs(a.expiresAt).isBefore(dayjs()) ? 'expired' : a.status,
          q: `${leadOf(a.leadId)?.fullName ?? ''} ${leadOf(a.leadId)?.phone ?? ''}`,
        })),
    [attempts, leads, branchId],
  );
  const data = status ? rows.filter((a) => a.status === status) : rows;
  const doneRows = rows.filter((a) => a.status === 'done');
  const finished = rows.filter((a) => a.status === 'done' || a.status === 'expired').length;
  const avgPct = doneRows.length ? Math.round(doneRows.reduce((s, a) => s + pctOf(a), 0) / doneRows.length) : 0;

  const copy = (a: PlacementAttempt) => {
    void navigator.clipboard?.writeText(testLink(a.token));
    notify.toast.success('Đã sao chép link làm bài');
  };

  /** Giả lập lead làm xong bài (thay cho trang làm bài của lead — chưa xây) */
  const simulate = (a: PlacementAttempt) => {
    const t = testOf(a.testId)!;
    const answers = simulateAnswers(t.questionIds, questions, 0.25 + Math.random() * 0.65);
    updateAttempt(a.id, { status: 'done', startedAt: a.startedAt ?? dayjs().subtract(t.durationMin, 'minute').toISOString(), submittedAt: new Date().toISOString(), answers, ...grade(t, answers, questions) });
    notify.toast.success('Đã chấm bài tự động');
    setOpenId(a.id);
  };

  const columns: DataColumn<Row>[] = [
    {
      key: 'lead',
      title: 'Lead',
      width: 200,
      mobile: 'title',
      render: (_, r) => {
        const l = leadOf(r.leadId);
        return (
          <span className="leading-5">
            <span className="font-medium">{l?.fullName ?? r.leadId}</span>
            <span className="block text-xs text-ink-muted">
              {l?.code} · {l?.phone}
            </span>
          </span>
        );
      },
    },
    { key: 'test', title: 'Bài test', width: 220, render: (_, r) => testOf(r.testId)?.name ?? '—' },
    {
      key: 'status',
      title: 'Trạng thái',
      width: 170,
      mobile: 'aside',
      render: (_, r) => (
        <span className="inline-flex flex-col gap-0.5">
          <StatusTag tone={ATTEMPT_STATUS[r.status].tone} icon={ATTEMPT_STATUS[r.status].icon} size="sm">
            {ATTEMPT_STATUS[r.status].label}
          </StatusTag>
          {(r.status === 'sent' || r.status === 'in_progress') && <span className="text-[11px] text-ink-muted">hết hạn {fromNow(r.expiresAt)}</span>}
          {r.status === 'done' && !r.logged && <span className="text-[11px] font-medium text-warning-700">Chưa ghi vào hồ sơ</span>}
        </span>
      ),
    },
    {
      key: 'score',
      title: 'Điểm',
      width: 110,
      align: 'right',
      sorter: (a, b) => pctOf(a) - pctOf(b),
      render: (_, r) =>
        r.status === 'done' ? (
          <span className="leading-5">
            <span className="tabular font-semibold">{pctOf(r)}%</span>
            <span className="tabular block text-xs text-ink-muted">
              {r.correct}/{r.total} câu
            </span>
          </span>
        ) : (
          '—'
        ),
    },
    {
      key: 'suggest',
      title: 'Gợi ý xếp lớp',
      width: 200,
      render: (_, r) =>
        r.status === 'done' ? (
          <span className="leading-5">
            <span className="font-medium">{r.suggestedLevel}</span>
            <span className="block text-xs text-primary-700">→ {courseLabel(r.suggestedCourse)}</span>
          </span>
        ) : (
          '—'
        ),
    },
    {
      key: 'sent',
      title: 'Gửi',
      width: 170,
      sorter: (a, b) => dayjs(a.sentAt).valueOf() - dayjs(b.sentAt).valueOf(),
      render: (_, r) => (
        <span className="leading-5" title={fmtDateTime(r.sentAt)}>
          {fromNow(r.sentAt)}
          <span className="block text-xs text-ink-muted">
            {SEND_CHANNELS[r.channel]} · {r.sentBy}
          </span>
        </span>
      ),
    },
  ];

  const sendableLeads = leads.filter((l) => !l.hidden && l.status !== 'lost' && (branchId === ALL_BRANCHES || l.branchId === branchId));

  return (
    <>
      <PageHeader
        title="Test xếp lớp"
        subtitle="Bài test đầu vào bốc từ câu ĐÃ DUYỆT trong Ngân hàng câu hỏi. Gửi link cho lead → tự chấm → gợi ý trình độ & khóa phù hợp."
        breadcrumb={[{ title: 'Tuyển sinh' }, { title: 'Test xếp lớp' }]}
        actions={
          <>
            {editable && (
              <Button icon={Plus} variant="outline" collapseOnMobile onClick={() => setEditing('new')}>
                Tạo bài test
              </Button>
            )}
            <Button action="send" collapseOnMobile onClick={() => setSending({ channel: 'zalo', validDays: 3 })}>
              Gửi test cho lead
            </Button>
          </>
        }
      />

      <div className="mb-4 grid grid-cols-2 gap-3 lg:grid-cols-4">
        <StatCard label="Bài test đang dùng" value={tests.filter((t) => t.status === 'active').length} icon={ClipboardCheck} tone="primary" hint={`${tests.length} bài trong thư viện`} />
        <StatCard label="Link đã gửi" value={rows.length} icon={Link2} tone="info" hint={`${rows.filter((a) => a.status === 'sent' || a.status === 'in_progress').length} đang chờ làm`} />
        <StatCard label="Tỷ lệ hoàn thành" value={finished ? `${Math.round((doneRows.length / finished) * 100)}%` : '—'} icon={CheckCircle2} tone="success" hint={`Điểm TB ${avgPct}%`} />
        <StatCard
          label="Chờ ghi kết quả"
          value={doneRows.filter((a) => !a.logged).length}
          icon={NotebookPen}
          tone="warning"
          hint="Đã nộp, chưa vào lịch sử tư vấn"
          onClick={() => {
            setTab('attempts');
            setStatus('done');
          }}
        />
      </div>

      <Tabs
        activeKey={tab}
        onChange={(k) => setTab(k as typeof tab)}
        items={[
          {
            key: 'attempts',
            label: `Lượt làm bài (${rows.length})`,
            children: (
              <DataTable<Row>
                storageKey="crm-placement-attempts"
                columns={columns}
                data={data}
                rowKey="id"
                unitLabel="lượt"
                onRowClick={(r) => setOpenId(r.id)}
                search={{
                  placeholder: 'Tìm theo tên lead...',
                  activeFilterCount: status ? 1 : 0,
                  filters: (
                    <Select
                      placeholder="Trạng thái"
                      value={status}
                      onChange={setStatus}
                      options={Object.entries(ATTEMPT_STATUS).map(([value, v]) => ({ value, label: v.label }))}
                      showSearch={false}
                      className="lg:!w-40"
                    />
                  ),
                  onReset: () => setStatus(undefined),
                }}
                searchKeys={['q']}
                rowActions={(r) =>
                  r.status === 'sent' || r.status === 'in_progress' ? (
                    <>
                      <Button icon={Copy} iconOnly size="sm" variant="ghost" tone="neutral" tooltip="Sao chép link" aria-label="Sao chép link" onClick={() => copy(r)} />
                      <Button icon={FlaskConical} iconOnly size="sm" variant="ghost" tone="neutral" tooltip="Giả lập lead nộp bài (thử nghiệm)" aria-label="Giả lập nộp bài" onClick={() => simulate(r)} />
                    </>
                  ) : r.status === 'expired' ? (
                    <Button icon={Send} iconOnly size="sm" variant="ghost" tone="neutral" tooltip="Gửi lại" aria-label="Gửi lại" onClick={() => setSending({ testId: r.testId, leadId: r.leadId, channel: r.channel, validDays: 3 })} />
                  ) : null
                }
                actionsWidth={100}
                empty={{ icon: ClipboardCheck, title: 'Chưa gửi test cho lead nào', action: <Button action="send" onClick={() => setSending({ channel: 'zalo', validDays: 3 })}>Gửi test</Button> }}
              />
            ),
          },
          {
            key: 'tests',
            label: `Bài test (${tests.length})`,
            children: (
              <div className="grid gap-3 md:grid-cols-2 xl:grid-cols-3">
                {tests.map((t) => {
                  const lack = t.sections.filter((s) => poolOf(questions, s).length < s.count);
                  const used = attempts.filter((a) => a.testId === t.id);
                  const usedDone = used.filter((a) => a.status === 'done');
                  return (
                    <article key={t.id} className="flex flex-col rounded-xl border border-line bg-surface p-4">
                      <div className="flex items-start justify-between gap-2">
                        <div className="min-w-0">
                          <p className="text-xs text-ink-muted">{t.code}</p>
                          <h3 className="font-semibold text-ink">{t.name}</h3>
                        </div>
                        <StatusTag tone={PLACEMENT_STATUS[t.status].tone} icon={PLACEMENT_STATUS[t.status].icon} size="sm">
                          {PLACEMENT_STATUS[t.status].label}
                        </StatusTag>
                      </div>
                      {t.description && <p className="mt-1 text-[13px] text-ink-secondary">{t.description}</p>}
                      <p className="mt-2 flex flex-wrap gap-3 text-[13px] text-ink-secondary">
                        <span className="inline-flex items-center gap-1">
                          <ClipboardCheck size={14} aria-hidden /> {t.questionIds.length} câu
                        </span>
                        <span className="inline-flex items-center gap-1">
                          <Timer size={14} aria-hidden /> {t.durationMin} phút
                        </span>
                        <span>
                          {usedDone.length}/{used.length} lượt đã làm
                        </span>
                      </p>
                      <div className="mt-2 flex flex-wrap gap-1">
                        {t.sections.map((s, i) => (
                          <span key={i} className="rounded bg-canvas px-1.5 py-0.5 text-xs text-ink-secondary">
                            {s.level} {Q_SKILLS[s.skill].label} × {s.count}
                          </span>
                        ))}
                      </div>
                      <ul className="mt-3 flex flex-col gap-0.5 text-xs text-ink-secondary">
                        {[...t.bands]
                          .sort((a, b) => a.minPct - b.minPct)
                          .map((b, i) => (
                            <li key={i}>
                              <span className="tabular inline-block w-10 font-medium text-ink">{b.minPct}%+</span> {b.level} → {courseLabel(b.course)}
                            </li>
                          ))}
                      </ul>
                      {lack.length > 0 && (
                        <p className="mt-2 flex items-start gap-1 rounded-lg bg-warning-50 px-2 py-1.5 text-xs text-warning-700">
                          <TriangleAlert size={14} className="mt-px shrink-0" aria-hidden />
                          Ngân hàng thiếu câu đã duyệt: {lack.map((s) => `${s.level} ${Q_SKILLS[s.skill].label} (có ${poolOf(questions, s).length}/${s.count})`).join(', ')}
                        </p>
                      )}
                      <div className="mt-auto flex flex-wrap gap-1.5 pt-3">
                        {t.status === 'active' && (
                          <Button icon={Send} size="sm" variant="soft" onClick={() => setSending({ testId: t.id, channel: 'zalo', validDays: 3 })}>
                            Gửi
                          </Button>
                        )}
                        {editable && (
                          <>
                            <Button action="edit" size="sm" onClick={() => setEditing(t)} />
                            <Tooltip title="Bốc ngẫu nhiên bộ câu khác (cùng tiêu chí) — tránh lộ đề">
                              <span>
                                <Button
                                  icon={Shuffle}
                                  size="sm"
                                  variant="ghost"
                                  tone="neutral"
                                  onClick={() => {
                                    updateTest(t.id, { questionIds: pickQuestions(questions, t.sections, true) });
                                    notify.toast.success('Đã bốc lại câu hỏi');
                                  }}
                                >
                                  Bốc lại
                                </Button>
                              </span>
                            </Tooltip>
                            {t.status !== 'active' ? (
                              <Button icon={CheckCircle2} size="sm" variant="ghost" tone="success" onClick={() => updateTest(t.id, { status: 'active' })}>
                                Dùng
                              </Button>
                            ) : (
                              <Button icon={Archive} size="sm" variant="ghost" tone="neutral" onClick={() => updateTest(t.id, { status: 'archived' })}>
                                Ngừng
                              </Button>
                            )}
                          </>
                        )}
                      </div>
                    </article>
                  );
                })}
              </div>
            ),
          },
        ]}
      />

      <AttemptDrawer attemptId={openId} onClose={() => setOpenId(null)} />

      {/* ===== Gửi test ===== */}
      <ModalForm<SendValues>
        key={sending ? JSON.stringify(sending) : 'send'}
        open={Boolean(sending)}
        onClose={() => setSending(null)}
        title="Gửi test xếp lớp cho lead"
        description="Lead nhận link làm bài online, không cần tài khoản. Nộp bài → hệ thống tự chấm và báo cho Sale phụ trách."
        submitAction="send"
        submitText="Gửi link"
        successMessage="Đã gửi link làm bài"
        initialValues={sending ?? undefined}
        onSubmit={(v) => {
          const lead = leadOf(v.leadId)!;
          const t = testOf(v.testId)!;
          const a = sendTest({ testId: v.testId, leadId: v.leadId, channel: v.channel, sentBy: user?.fullName ?? 'Nhân viên' }, v.validDays);
          addConsultation({
            leadId: lead.id,
            channel: v.channel === 'email' ? 'email' : v.channel === 'zalo' ? 'zalo' : 'message',
            result: 'test_booked',
            content: `Gửi link ${t.name} qua ${SEND_CHANNELS[v.channel]} (hạn ${v.validDays} ngày): ${testLink(a.token)}`,
            staffName: user?.fullName ?? 'Nhân viên',
            system: true,
          });
          if (lead.status === 'new' || lead.status === 'consulting') updateLead(lead.id, { status: 'test_scheduled' });
          void navigator.clipboard?.writeText(testLink(a.token));
          setTab('attempts');
          setStatus(undefined);
        }}
      >
        <Form.Item name="testId" label="Bài test" rules={[{ required: true, message: 'Chọn bài test' }]}>
          <Select placeholder="Chọn bài test" options={tests.filter((t) => t.status === 'active').map((t) => ({ value: t.id, label: `${t.name} · ${t.questionIds.length} câu / ${t.durationMin}′` }))} />
        </Form.Item>
        <Form.Item name="leadId" label="Lead" rules={[{ required: true, message: 'Chọn lead' }]}>
          <Select placeholder="Tìm theo tên, SĐT..." options={sendableLeads.map((l) => ({ value: l.id, label: `${l.fullName} · ${l.phone}` }))} />
        </Form.Item>
        <div className="grid grid-cols-2 gap-3">
          <Form.Item name="channel" label="Gửi qua" rules={[{ required: true }]}>
            <Select options={Object.entries(SEND_CHANNELS).map(([value, label]) => ({ value, label }))} allowClear={false} showSearch={false} />
          </Form.Item>
          <Form.Item name="validDays" label="Hiệu lực (ngày)" rules={[{ required: true }]}>
            <NumberInput min={1} max={14} />
          </Form.Item>
        </div>
        <p className="text-xs text-ink-muted">Lead đang “Mới / Đang tư vấn” sẽ chuyển sang “Hẹn test”. Link được sao chép sẵn để dán khi cần.</p>
      </ModalForm>

      {/* ===== Tạo / sửa bài test ===== */}
      <ModalForm<TestInput>
        key={editing === 'new' ? 'new-test' : (editing?.id ?? 'none')}
        open={Boolean(editing)}
        onClose={() => setEditing(null)}
        title={editing === 'new' ? 'Tạo bài test xếp lớp' : `Sửa ${editing ? (editing as PlacementTest).name : ''}`}
        description="Câu hỏi được bốc tự động theo từng phần. Đổi phần → bộ câu được bốc lại."
        width={760}
        submitAction="save"
        successMessage="Đã lưu bài test"
        initialValues={
          editing && editing !== 'new'
            ? editing
            : {
                status: 'draft',
                durationMin: 25,
                sections: [{ level: 'N5', skill: 'vocab', count: 3 }],
                bands: [
                  { minPct: 0, level: 'Chưa có nền tảng', course: 'n5' },
                  { minPct: 50, level: 'Tương đương N5', course: 'n4' },
                ],
              }
        }
        onSubmit={(v) => {
          if (!v.bands?.some((b) => b.minPct === 0)) throw new Error('Thang gợi ý cần có mốc 0%');
          const sections = v.sections.filter((s) => s.count > 0);
          const same = editing !== 'new' && editing && JSON.stringify(editing.sections) === JSON.stringify(sections);
          const input: TestInput = { ...v, sections, questionIds: same ? (editing as PlacementTest).questionIds : pickQuestions(questions, sections) };
          if (editing === 'new') addTest(input, user?.fullName ?? 'Nhân viên');
          else if (editing) updateTest(editing.id, input);
        }}
      >
        <div className="grid gap-x-4 md:grid-cols-[1fr_140px_150px]">
          <Form.Item name="name" label="Tên bài test" rules={[{ required: true, message: 'Nhập tên' }]}>
            <TextInput placeholder="Vd: Test đầu vào Sơ cấp" />
          </Form.Item>
          <Form.Item name="durationMin" label="Thời gian (phút)" rules={[{ required: true }]}>
            <NumberInput min={5} max={120} step={5} />
          </Form.Item>
          <Form.Item name="status" label="Trạng thái">
            <Select options={Object.entries(PLACEMENT_STATUS).map(([value, v]) => ({ value, label: v.label }))} allowClear={false} showSearch={false} />
          </Form.Item>
        </div>
        <Form.Item name="description" label="Dành cho ai">
          <TextArea rows={2} maxLength={300} placeholder="Đối tượng, trình độ dự kiến..." />
        </Form.Item>

        <p className="mb-2 text-sm font-semibold text-ink">Các phần (bốc câu đã duyệt)</p>
        <Form.List name="sections">
          {(fields, { add, remove }) => (
            <div className="mb-4 flex flex-col gap-2">
              {fields.map((f) => (
                <div key={f.key} className="grid grid-cols-[80px_1fr_80px_auto_36px] items-start gap-2">
                  <Form.Item name={[f.name, 'level']} rules={[{ required: true }]} className="!mb-0">
                    <Select options={Q_LEVELS.map((l) => ({ value: l, label: l }))} allowClear={false} showSearch={false} aria-label="Trình độ" />
                  </Form.Item>
                  <Form.Item name={[f.name, 'skill']} rules={[{ required: true }]} className="!mb-0">
                    <Select options={Q_SKILL_OPTIONS} allowClear={false} showSearch={false} aria-label="Kỹ năng" />
                  </Form.Item>
                  <Form.Item name={[f.name, 'count']} rules={[{ required: true, message: 'Số câu' }]} className="!mb-0">
                    <NumberInput min={1} max={50} placeholder="Số câu" />
                  </Form.Item>
                  <Form.Item noStyle shouldUpdate>
                    {({ getFieldValue }) => {
                      const s = getFieldValue(['sections', f.name]);
                      const n = s?.level && s?.skill ? poolOf(questions, s).length : 0;
                      return <span className={cn('tabular whitespace-nowrap pt-2 text-xs', n < (s?.count ?? 0) ? 'text-danger-600' : 'text-ink-muted')}>có {n} câu</span>;
                    }}
                  </Form.Item>
                  <Button icon={Trash2} iconOnly size="sm" variant="ghost" tone="danger" aria-label="Xóa phần" disabled={fields.length <= 1} onClick={() => remove(f.name)} />
                </div>
              ))}
              <Button icon={Plus} size="sm" variant="soft" className="self-start" onClick={() => add({ level: 'N5', skill: 'grammar', count: 3 })}>
                Thêm phần
              </Button>
            </div>
          )}
        </Form.List>

        <p className="mb-2 text-sm font-semibold text-ink">Thang gợi ý xếp lớp</p>
        <Form.List name="bands">
          {(fields, { add, remove }) => (
            <div className="flex flex-col gap-2">
              {fields.map((f) => (
                <div key={f.key} className="grid grid-cols-[90px_1fr_1fr_36px] items-start gap-2">
                  <Form.Item name={[f.name, 'minPct']} rules={[{ required: true, message: '%' }]} className="!mb-0">
                    <NumberInput min={0} max={100} placeholder="Từ %" />
                  </Form.Item>
                  <Form.Item name={[f.name, 'level']} rules={[{ required: true, message: 'Nhập nhận xét' }]} className="!mb-0">
                    <TextInput placeholder="Trình độ: Tương đương N5" />
                  </Form.Item>
                  <Form.Item name={[f.name, 'course']} rules={[{ required: true, message: 'Chọn khóa' }]} className="!mb-0">
                    <Select options={COURSE_OPTIONS} placeholder="Gợi ý khóa" aria-label="Gợi ý khóa" />
                  </Form.Item>
                  <Button icon={Trash2} iconOnly size="sm" variant="ghost" tone="danger" aria-label="Xóa mốc" disabled={fields.length <= 1} onClick={() => remove(f.name)} />
                </div>
              ))}
              <Button icon={Plus} size="sm" variant="soft" className="self-start" onClick={() => add({ minPct: undefined, level: '', course: undefined })}>
                Thêm mốc
              </Button>
            </div>
          )}
        </Form.List>
      </ModalForm>
    </>
  );
}
