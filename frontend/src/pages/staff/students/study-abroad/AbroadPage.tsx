import { useEffect, useMemo, useState } from 'react';
import { Form } from 'antd';
import dayjs from 'dayjs';
import { Columns3, FolderOpen, List, Plane, ScrollText, Stamp, Users } from 'lucide-react';
import { Button, DataTable, ModalForm, PageHeader, Select, StatCard, StatusTag, TextInput, UserCell, useNotify, type DataColumn } from '@/components/common';
import { useCrmStore } from '@/features/crm/crmStore';
import { ABROAD_STAFF, assigneeOptions, daysTo, docProgress, PROGRAM_OPTIONS, PROGRAMS, STAGE_ORDER, STAGES, useAbroadStore, type AbroadCase, type Program } from '@/features/student/abroad/abroad';
import { createAbroad, loadAbroad } from '@/features/student/abroad/abroadApi';
import { useHrStore } from '@/features/hr/hrStore';
import { AbroadDrawer } from '@/features/student/abroad/AbroadDrawer';
import { useAuth } from '@/hooks/useAuth';
import { USE_API } from '@/lib/api/config';
import { cn } from '@/lib/cn';
import { fmtDate } from '@/lib/date';
import { ALL_BRANCHES } from '@/types/auth';

interface Row extends AbroadCase {
  name: string;
  code: string;
  branchId?: string;
}

/** Du học / XKLĐ (HV-17) — chế độ API: hồ sơ từ server, mỗi thao tác gọi API (server kiểm điều kiện sang bước, tự ghi nhật ký). */
export default function AbroadPage() {
  const { user, branchId, can } = useAuth();
  const cases = useAbroadStore((s) => s.cases);
  const add = useAbroadStore((s) => s.add);
  const students = useCrmStore((s) => s.students);
  const editable = can('students.study-abroad');
  const notify = useNotify();
  const employees = useHrStore((s) => s.employees);
  const staffOptions = assigneeOptions(employees);
  const [loading, setLoading] = useState(USE_API);

  useEffect(() => {
    if (!USE_API) return;
    loadAbroad()
      .catch((e: Error) => notify.error({ title: 'Không tải được hồ sơ du học / XKLĐ', description: e.message }))
      .finally(() => setLoading(false));
    // eslint-disable-next-line react-hooks/exhaustive-deps
  }, []);

  const [program, setProgram] = useState<Program | 'all'>('all');
  const [assignee, setAssignee] = useState<string>();
  const [partner, setPartner] = useState<string>();
  const [showCancelled, setShowCancelled] = useState(false);
  const [view, setView] = useState<'kanban' | 'list'>('kanban');
  const [openId, setOpenId] = useState<string | null>(null);
  const [creating, setCreating] = useState(false);
  const [keyword, setKeyword] = useState('');

  const rows: Row[] = useMemo(
    () =>
      cases
        .map((c) => {
          const s = students.find((x) => x.id === c.studentId);
          return { ...c, name: s?.fullName ?? c.studentName ?? c.studentId, code: s?.code ?? c.studentCode ?? '', branchId: c.branchId ?? s?.branchId };
        })
        .filter((r) => branchId === ALL_BRANCHES || r.branchId === branchId),
    [cases, students, branchId],
  );
  const base = rows.filter(
    (r) =>
      (showCancelled || !r.cancelled) &&
      (!assignee || r.assignee === assignee) &&
      (!partner || r.partner === partner) &&
      (!keyword || `${r.name} ${r.code} ${r.partner} ${r.destination}`.toLowerCase().includes(keyword.toLowerCase())),
  );
  const counts: Record<string, number> = { all: base.length };
  base.forEach((r) => (counts[r.program] = (counts[r.program] ?? 0) + 1));
  const data = program === 'all' ? base : base.filter((r) => r.program === program);

  const live = rows.filter((r) => !r.cancelled);
  const stats = {
    active: live.filter((r) => r.stage !== 'departed').length,
    coe: live.filter((r) => r.stage === 'coe').length,
    visa: live.filter((r) => r.stage === 'visa').length,
    flying: live.filter((r) => r.flightDate && (daysTo(r.flightDate) ?? -1) >= 0 && (daysTo(r.flightDate) ?? 99) <= 30).length,
    departed: live.filter((r) => r.stage === 'departed' && dayjs(r.flightDate).year() === dayjs().year()).length,
  };

  const Card = ({ r }: { r: Row }) => {
    const dp = docProgress(r);
    const pr = PROGRAMS[r.program];
    const iv = daysTo(r.interview?.date);
    const fl = daysTo(r.flightDate);
    return (
      <button
        type="button"
        onClick={() => setOpenId(r.id)}
        className={cn('flex w-full flex-col gap-1.5 rounded-lg border border-line bg-surface p-2.5 text-left shadow-[var(--shadow-soft-sm)] hover:shadow-[var(--shadow-soft-md)]', r.cancelled && 'opacity-60')}
      >
        <span className="flex items-start justify-between gap-1">
          <span className="min-w-0">
            <span className="block truncate text-[13px] font-semibold text-ink">{r.name}</span>
            <span className="block truncate text-[11px] text-ink-muted">{r.partner}</span>
          </span>
          <StatusTag tone={pr.tone} icon={STAGES[r.stage].icon} size="sm">
            {pr.short}
          </StatusTag>
        </span>
        <span className="text-[11px] text-ink-secondary">
          {r.destination} · kỳ {dayjs(`${r.intake}-01`).format('MM/YYYY')}
          {r.job && ` · ${r.job}`}
        </span>
        <span className="flex items-center gap-1.5">
          <span className="h-1.5 flex-1 rounded-full bg-disabled">
            <span className={cn('block h-full rounded-full', dp.done === dp.total ? 'bg-success-600' : 'bg-primary-500')} style={{ width: `${(dp.done / dp.total) * 100}%` }} />
          </span>
          <span className="tabular text-[11px] text-ink-muted">
            {dp.done}/{dp.total} giấy tờ
          </span>
        </span>
        {r.cancelled && <span className="text-[11px] text-danger-600">Đã dừng: {r.cancelled.reason}</span>}
        {!r.cancelled && r.stage === 'interview' && r.interview && !r.interview.result && iv !== undefined && <span className={cn('text-[11px] font-medium', iv <= 3 ? 'text-danger-600' : 'text-warning-700')}>Phỏng vấn {iv >= 0 ? `sau ${iv} ngày` : 'đã qua — nhập kết quả'} ({fmtDate(r.interview.date)})</span>}
        {!r.cancelled && r.stage === 'coe' && r.coe?.submittedAt && <span className="text-[11px] text-ink-muted">Chờ COE {-(daysTo(r.coe.submittedAt) ?? 0)} ngày</span>}
        {!r.cancelled && fl !== undefined && fl >= 0 && <span className="text-[11px] font-medium text-primary-700">✈ Bay {fmtDate(r.flightDate)} (còn {fl} ngày)</span>}
      </button>
    );
  };

  const columns: DataColumn<Row>[] = [
    { key: 'name', title: 'Học viên', width: 200, mobile: 'title', render: (_, r) => <UserCell size="sm" name={r.name} subtitle={r.code} /> },
    { key: 'program', title: 'Chương trình', width: 190, mobile: 'subtitle', render: (_, r) => <div className="leading-5"><p className="text-ink">{PROGRAMS[r.program].label}</p><p className="text-xs text-ink-muted">{r.partner}</p></div> },
    { key: 'destination', title: 'Nơi đến · kỳ', width: 150, render: (_, r) => `${r.destination} · ${dayjs(`${r.intake}-01`).format('MM/YYYY')}` },
    { key: 'docs', title: 'Giấy tờ', width: 110, render: (_, r) => { const dp = docProgress(r); return <span className={cn('tabular', dp.done < dp.total ? 'text-warning-700' : 'text-success-700')}>{dp.done}/{dp.total}</span>; } },
    { key: 'stage', title: 'Bước', width: 160, mobile: 'aside', render: (_, r) => <StatusTag tone={r.cancelled ? 'danger' : r.stage === 'departed' ? 'success' : 'info'} icon={STAGES[r.stage].icon} size="sm">{r.cancelled ? 'Đã dừng' : STAGES[r.stage].label}</StatusTag> },
    { key: 'assignee', title: 'Phụ trách', width: 200, render: (_, r) => <span className="text-[13px] text-ink-secondary">{r.assignee}</span> },
  ];

  const partners = [...new Set(rows.map((r) => r.partner))].sort();

  return (
    <>
      <PageHeader
        title="Du học / XKLĐ"
        subtitle="Hồ sơ du học, thực tập sinh, kỹ năng đặc định, kỹ sư: giấy tờ → phỏng vấn → COE → visa → xuất cảnh"
        breadcrumb={[{ title: 'Học viên' }, { title: 'Du học / XKLĐ' }]}
        actions={
          editable && (
            <Button action="create" onClick={() => setCreating(true)} collapseOnMobile>
              Tạo hồ sơ
            </Button>
          )
        }
      />

      <div className="mb-4 grid grid-cols-2 gap-3 lg:grid-cols-5">
        <StatCard label="Đang xử lý" value={stats.active} icon={FolderOpen} />
        <StatCard label="Chờ COE" value={stats.coe} icon={ScrollText} tone="warning" />
        <StatCard label="Đang xin visa" value={stats.visa} icon={Stamp} tone="info" />
        <StatCard label="Bay trong 30 ngày" value={stats.flying} icon={Plane} tone="success" />
        <StatCard label={`Đã xuất cảnh ${dayjs().year()}`} value={stats.departed} icon={Users} tone="success" />
      </div>

      <div className="mb-4 flex flex-col gap-3 rounded-[var(--radius-card)] border border-line bg-surface p-3 lg:flex-row lg:items-center">
        <div className="flex flex-wrap gap-1.5">
          {[{ value: 'all', label: 'Tất cả' }, ...PROGRAM_OPTIONS].map((o) => (
            <button
              key={o.value}
              type="button"
              onClick={() => setProgram(o.value as Program | 'all')}
              className={cn('inline-flex h-8 items-center gap-1.5 rounded-full border px-3 text-[13px] font-medium', program === o.value ? 'border-primary-600 bg-primary-600 text-white' : 'border-line text-ink-secondary hover:bg-primary-50')}
            >
              {o.value === 'all' ? o.label : PROGRAMS[o.value as Program].short}
              <span className={cn('tabular rounded-full px-1.5 text-xs', program === o.value ? 'bg-white/20' : 'bg-disabled text-ink-muted')}>{counts[o.value] ?? 0}</span>
            </button>
          ))}
        </div>
        <div className="flex flex-1 flex-wrap items-center gap-2 lg:justify-end">
          <TextInput value={keyword} onChange={(e) => setKeyword(e.target.value)} placeholder="Tìm học viên, trường, nghiệp đoàn..." className="!w-full sm:!w-60" />
          <Select placeholder="Nơi tiếp nhận" value={partner} onChange={setPartner} options={partners.map((p) => ({ value: p, label: p }))} className="!w-52" />
          <Select
            placeholder="Phụ trách"
            value={assignee}
            onChange={setAssignee}
            options={(USE_API ? [...new Set(rows.map((r) => r.assignee))].sort() : ABROAD_STAFF).map((x) => ({ value: x, label: x }))}
            className="!w-52"
          />
          <label className="inline-flex items-center gap-1.5 text-[13px] text-ink-secondary">
            <input type="checkbox" checked={showCancelled} onChange={(e) => setShowCancelled(e.target.checked)} /> Hiện hồ sơ đã dừng
          </label>
          <div className="inline-flex rounded-lg border border-line p-0.5">
            {(
              [
                ['kanban', Columns3, 'Theo bước'],
                ['list', List, 'Danh sách'],
              ] as const
            ).map(([k, Icon, l]) => (
              <button key={k} type="button" aria-label={l} aria-pressed={view === k} onClick={() => setView(k)} className={cn('grid h-8 w-9 place-items-center rounded-md', view === k ? 'bg-primary-600 text-white' : 'text-ink-secondary hover:bg-page')}>
                <Icon size={16} aria-hidden />
              </button>
            ))}
          </div>
        </div>
      </div>

      {view === 'kanban' ? (
        <div className="-mx-4 overflow-x-auto px-4 pb-2 sm:mx-0 sm:px-0">
          <div className="grid min-w-[1150px] grid-cols-6 gap-3">
            {STAGE_ORDER.map((st) => {
              const S = STAGES[st];
              const list = data.filter((r) => r.stage === st);
              return (
                <section key={st} className="flex flex-col gap-2 rounded-[var(--radius-card)] bg-page p-2">
                  <h3 className="flex items-center justify-between px-1 text-[13px] font-semibold text-ink">
                    <span className="inline-flex items-center gap-1.5">
                      <S.icon size={14} aria-hidden /> {S.label}
                    </span>
                    <span className="tabular rounded-full bg-surface px-1.5 text-xs text-ink-muted">{list.length}</span>
                  </h3>
                  <p className="px-1 text-[11px] text-ink-muted">{S.hint}</p>
                  {list.map((r) => (
                    <Card key={r.id} r={r} />
                  ))}
                </section>
              );
            })}
          </div>
        </div>
      ) : (
        <DataTable<Row> storageKey="students-abroad" columns={columns} data={data} loading={loading} rowKey="id" unitLabel="hồ sơ" onRowClick={(r) => setOpenId(r.id)} searchKeys={['name', 'code', 'partner', 'destination']} />
      )}

      {openId && <AbroadDrawer caseId={openId} editable={editable} onClose={() => setOpenId(null)} />}

      <ModalForm<{ studentId: string; program: Program; partner: string; destination: string; job?: string; intake: string; assignee: string; feeTotal?: string }>
        open={creating}
        onClose={() => setCreating(false)}
        title="Tạo hồ sơ du học / XKLĐ"
        description="Danh sách giấy tờ cần nộp được tạo theo chương trình."
        submitAction="create"
        submitText="Tạo hồ sơ"
        successMessage="Đã tạo hồ sơ"
        initialValues={{ program: 'study', intake: dayjs().add(5, 'month').format('YYYY-MM'), assignee: USE_API ? (employees.find((e) => e.fullName === user?.fullName)?.id ?? staffOptions[0]?.value) : ABROAD_STAFF[0] }}
        onSubmit={async (v) => {
          const fee = Number(String(v.feeTotal ?? '0').replace(/\D/g, '')) || 0;
          if (USE_API) {
            const created = await createAbroad(v.studentId, {
              program: v.program,
              partner: v.partner.trim(),
              destination: v.destination.trim(),
              job: v.job?.trim() || undefined,
              intake: v.intake,
              feeTotal: fee,
              assigneeId: v.assignee,
            });
            setTimeout(() => setOpenId(created.id), 300);
            return;
          }
          const c = add(
            { studentId: v.studentId, program: v.program, partner: v.partner.trim(), destination: v.destination.trim(), job: v.job?.trim() || undefined, intake: v.intake, stage: 'consult', fee: { total: Number(String(v.feeTotal ?? '0').replace(/\D/g, '')) || 0, paid: 0 }, assignee: v.assignee },
            user?.fullName ?? 'Nhân viên',
          );
          setTimeout(() => setOpenId(c.id), 300);
        }}
      >
        <Form.Item name="studentId" label="Học viên" rules={[{ required: true, message: 'Chọn học viên' }]}>
          <Select optionFilterProp="label" options={students.filter((s) => !s.hidden).map((s) => ({ value: s.id, label: `${s.fullName} · ${s.code}` }))} placeholder="Tìm học viên" />
        </Form.Item>
        <Form.Item name="program" label="Chương trình" rules={[{ required: true }]}>
          <Select options={PROGRAM_OPTIONS} showSearch={false} allowClear={false} />
        </Form.Item>
        <div className="grid grid-cols-1 gap-x-3 sm:grid-cols-2">
          <Form.Item name="partner" label="Trường / nghiệp đoàn / công ty" rules={[{ required: true, whitespace: true, message: 'Nhập nơi tiếp nhận' }]}>
            <TextInput placeholder="VD: Trường Nhật ngữ Osaka YMCA" />
          </Form.Item>
          <Form.Item name="destination" label="Tỉnh / thành ở Nhật" rules={[{ required: true, whitespace: true, message: 'Nhập nơi đến' }]}>
            <TextInput placeholder="VD: Osaka" />
          </Form.Item>
          <Form.Item name="job" label="Ngành nghề (XKLĐ)">
            <TextInput placeholder="VD: Chế biến thực phẩm" />
          </Form.Item>
          <Form.Item name="intake" label="Kỳ nhập học / xuất cảnh (YYYY-MM)" rules={[{ required: true, pattern: /^\d{4}-\d{2}$/, message: 'Dạng 2027-04' }]}>
            <TextInput placeholder="2027-04" />
          </Form.Item>
          <Form.Item name="feeTotal" label="Phí dịch vụ (đ)">
            <TextInput placeholder="180000000" inputMode="numeric" />
          </Form.Item>
          <Form.Item name="assignee" label="Phụ trách" rules={[{ required: true }]}>
            <Select options={staffOptions} optionFilterProp="label" allowClear={false} />
          </Form.Item>
        </div>
      </ModalForm>
    </>
  );
}
