import { useMemo, useState, type Key } from 'react';
import { Form, Tooltip } from 'antd';
import dayjs, { type Dayjs } from 'dayjs';
import { Award, Bell, CalendarDays, Check, ClipboardPen, FileWarning, Percent, Ticket, Trophy, UserPlus, Users, Wallet, X, PackageCheck, HandHelping } from 'lucide-react';
import { Button, DataTable, DateInput, ModalForm, NumberInput, PageHeader, Select, StatCard, StatusTag, TextInput, UserCell, useNotify, type DataColumn } from '@/components/common';
import { useClassStore } from '@/features/academic/classes/classStore';
import { teacherName } from '@/features/academic/classes/constants';
import type { QLevel } from '@/features/academic/questions/types';
import { Q_LEVELS } from '@/features/academic/questions/constants';
import { useCrmStore } from '@/features/crm/crmStore';
import { CERT_STAGE, certStage, DOCS, JLPT_FEE, JLPT_SESSIONS, JLPT_STATUS, JLPT_VENUES, judge, PASS_TOTAL, SECTIONS, sessionPhase, useJlptStore, type JlptReg, type JlptStatus } from '@/features/student/jlpt/jlpt';
import { useStudentStore } from '@/features/student/studentStore';
import { useAuth } from '@/hooks/useAuth';
import { cn } from '@/lib/cn';
import { fmtDate } from '@/lib/date';
import { formatCurrency } from '@/lib/format';
import { ALL_BRANCHES } from '@/types/auth';

interface Row extends JlptReg {
  name: string;
  code: string;
  phone?: string;
  branchId?: string;
  classCode?: string;
  teacherIds: string[];
  missingDocs: number;
  result?: ReturnType<typeof judge>;
}

/** Thi JLPT — DỮ LIỆU GIẢ. */
export default function JlptPage() {
  const { branchId, currentBranch, user } = useAuth();
  const notify = useNotify();
  const regs = useJlptStore((s) => s.regs);
  const add = useJlptStore((s) => s.add);
  const update = useJlptStore((s) => s.update);
  const updateMany = useJlptStore((s) => s.updateMany);
  const students = useCrmStore((s) => s.students);
  const enrollments = useStudentStore((s) => s.enrollments);
  const classes = useClassStore((s) => s.classes);

  const [sessionId, setSessionId] = useState(JLPT_SESSIONS[0].id);
  const [level, setLevel] = useState<QLevel | 'all'>('all');
  const [status, setStatus] = useState<string>();
  const [result, setResult] = useState<string>();
  const [selected, setSelected] = useState<Key[]>([]);
  const [registering, setRegistering] = useState(false);
  const [scoring, setScoring] = useState<Row | null>(null);
  const [certFor, setCertFor] = useState<Row | null>(null);

  const session = JLPT_SESSIONS.find((s) => s.id === sessionId)!;
  const phase = sessionPhase(session);

  const rows: Row[] = useMemo(
    () =>
      regs
        .filter((r) => r.sessionId === sessionId)
        .map((r) => {
          const st = students.find((s) => s.id === r.studentId);
          const enr = enrollments.filter((e) => e.studentId === r.studentId).sort((a, b) => b.enrolledAt.localeCompare(a.enrolledAt))[0];
          const cls = classes.find((c) => c.id === enr?.classId);
          return {
            ...r,
            name: st?.fullName ?? r.studentId,
            code: st?.code ?? '',
            phone: st?.phone,
            branchId: st?.branchId,
            classCode: cls?.code,
            teacherIds: cls?.teacherIds ?? [],
            missingDocs: DOCS.filter((d) => !r.docs[d.key]).length,
            result: judge(r.level, r.scores),
          };
        })
        .filter((r) => branchId === ALL_BRANCHES || r.branchId === branchId),
    [regs, sessionId, students, enrollments, classes, branchId],
  );
  const base = rows.filter(
    (r) =>
      (!status || r.status === status) &&
      (!result || (result === 'pass' ? r.result?.passed : result === 'fail' ? r.result && !r.result.passed : result === 'none' ? r.status !== 'absent' && r.status !== 'cancelled' && !r.scores : r.status === 'absent')),
  );
  const counts: Record<string, number> = { all: base.length };
  base.forEach((r) => (counts[r.level] = (counts[r.level] ?? 0) + 1));
  const data = level === 'all' ? base : base.filter((r) => r.level === level);

  const active = rows.filter((r) => r.status !== 'cancelled');
  const sat = active.filter((r) => r.scores);
  const passed = sat.filter((r) => r.result?.passed);

  // Tỉ lệ đỗ theo cấp / lớp / GV
  const passBy = (key: (r: Row) => string[]) => {
    const m = new Map<string, { sat: number; pass: number }>();
    sat.forEach((r) => key(r).forEach((k) => m.set(k, { sat: (m.get(k)?.sat ?? 0) + 1, pass: (m.get(k)?.pass ?? 0) + (r.result?.passed ? 1 : 0) })));
    return [...m.entries()].sort((a, b) => b[1].sat - a[1].sat);
  };

  const columns: DataColumn<Row>[] = [
    { key: 'name', title: 'Học viên', width: 210, mobile: 'title', sorter: (a, b) => a.name.localeCompare(b.name, 'vi'), render: (_, r) => <UserCell size="sm" name={r.name} subtitle={`${r.code}${r.classCode ? ` · ${r.classCode}` : ''}`} /> },
    { key: 'level', title: 'Cấp', width: 70, align: 'center', mobile: 'subtitle', render: (_, r) => <span className="rounded-md bg-primary-50 px-1.5 py-0.5 text-xs font-semibold text-primary-700">{r.level}</span> },
    ...(phase === 'upcoming'
      ? ([
          {
            key: 'docs',
            title: 'Hồ sơ',
            width: 190,
            render: (_, r) => (
              <span className="flex flex-wrap gap-1">
                {DOCS.map((d) => (
                  <button
                    key={d.key}
                    type="button"
                    onClick={(e) => {
                      e.stopPropagation();
                      update(r.id, { docs: { ...r.docs, [d.key]: !r.docs[d.key] } });
                    }}
                    className={cn('inline-flex items-center gap-0.5 rounded px-1.5 py-0.5 text-[11px] font-medium', r.docs[d.key] ? 'bg-success-50 text-success-700' : 'bg-danger-50 text-danger-700')}
                    title="Bấm để đánh dấu đã nhận / chưa nhận"
                  >
                    {r.docs[d.key] ? <Check size={11} aria-hidden /> : <X size={11} aria-hidden />} {d.label}
                  </button>
                ))}
              </span>
            ),
          },
          {
            key: 'fee',
            title: 'Lệ phí',
            width: 130,
            render: (_, r) =>
              r.paidAt ? (
                <span className="text-[13px] text-success-700">Đã đóng {fmtDate(r.paidAt)}</span>
              ) : r.status === 'cancelled' ? (
                '—'
              ) : (
                <button
                  type="button"
                  onClick={(e) => {
                    e.stopPropagation();
                    update(r.id, { paidAt: new Date().toISOString(), status: r.status === 'registered' ? 'paid' : r.status });
                    notify.toast.success(`Đã thu ${formatCurrency(JLPT_FEE[r.level])} — ${r.name}`);
                  }}
                  className="text-[13px] font-medium text-danger-600 hover:underline"
                >
                  Chưa đóng · thu {formatCurrency(JLPT_FEE[r.level])}
                </button>
              ),
          },
          { key: 'admitNo', title: 'Số báo danh', width: 150, render: (_, r) => (r.admitNo ? <span className="font-mono text-xs">{r.admitNo}</span> : <span className="text-ink-muted">—</span>) },
        ] as DataColumn<Row>[])
      : ([
          {
            key: 'scores',
            title: 'Điểm từng phần',
            width: 210,
            render: (_, r) =>
              r.scores ? (
                <span className="flex flex-wrap gap-1">
                  {SECTIONS[r.level].map((s, i) => (
                    <Tooltip key={s.name} title={`${s.name}: ${r.scores![i]}/${s.max} (điểm liệt ${s.min})`}>
                      <span className={cn('tabular rounded px-1.5 py-0.5 text-[11px] font-medium', r.scores![i] < s.min ? 'bg-danger-50 text-danger-700' : 'bg-page text-ink-secondary')}>
                        {r.scores![i]}/{s.max}
                      </span>
                    </Tooltip>
                  ))}
                </span>
              ) : (
                <span className="text-ink-muted">{r.status === 'absent' ? 'Vắng thi' : 'Chưa nhập'}</span>
              ),
          },
          {
            key: 'total',
            title: 'Tổng',
            width: 90,
            align: 'right',
            sorter: (a, b) => (a.result?.total ?? -1) - (b.result?.total ?? -1),
            render: (_, r) => (r.result ? <span className="tabular font-semibold">{r.result.total}/180</span> : '—'),
          },
          {
            key: 'result',
            title: 'Kết quả',
            width: 170,
            render: (_, r) =>
              r.result ? (
                <Tooltip title={r.result.failSections.length ? `Dưới điểm liệt: ${r.result.failSections.join(', ')}` : `Điểm đỗ ${PASS_TOTAL[r.level]}/180`}>
                  <span>
                    <StatusTag tone={r.result.passed ? 'success' : 'danger'} icon={r.result.passed ? Trophy : X} size="sm">
                      {r.result.passed ? 'Đỗ' : r.result.failSections.length ? 'Trượt (điểm liệt)' : 'Trượt'}
                    </StatusTag>
                  </span>
                </Tooltip>
              ) : (
                '—'
              ),
          },
          {
            key: 'cert',
            title: 'Chứng chỉ gốc',
            width: 210,
            render: (_, r) => {
              if (!r.result?.passed) return <span className="text-ink-muted">—</span>;
              const stage = certStage(r);
              return (
                <span className="flex flex-col items-start gap-1 leading-5">
                  <StatusTag tone={CERT_STAGE[stage].tone} icon={Award} size="sm">
                    {stage === 'waiting' ? 'Chờ về' : stage === 'received' ? 'Đã về — chờ phát' : 'Đã phát'}
                  </StatusTag>
                  {r.certificate && <span className="font-mono text-xs text-ink-muted">{r.certificate.no}</span>}
                  {stage === 'waiting' && (
                    <Button size="sm" variant="ghost" icon={PackageCheck} onClick={(e) => { e.stopPropagation(); setCertFor(r); }}>
                      Nhận về
                    </Button>
                  )}
                  {stage === 'received' && (
                    <Button
                      size="sm"
                      variant="ghost"
                      tone="success"
                      icon={HandHelping}
                      onClick={(e) => {
                        e.stopPropagation();
                        update(r.id, { certificate: { ...r.certificate!, handedAt: new Date().toISOString(), handedBy: user?.fullName ?? 'Giáo vụ', handedTo: r.name } });
                        notify.toast.success(`Đã phát chứng chỉ cho ${r.name}`);
                      }}
                    >
                      Phát cho HV
                    </Button>
                  )}
                </span>
              );
            },
          },
        ] as DataColumn<Row>[])),
    {
      key: 'status',
      title: 'Trạng thái',
      width: 160,
      mobile: 'aside',
      render: (_, r) => {
        const s = JLPT_STATUS[r.status];
        return (
          <StatusTag tone={s.tone} icon={s.icon} size="sm">
            {s.label}
          </StatusTag>
        );
      },
    },
  ];

  const daysLeft = dayjs(session.examDate).diff(dayjs().startOf('day'), 'day');
  const bulkStep = (to: JlptStatus) => {
    const ids = selected.map(String);
    updateMany(ids, { status: to });
    notify.toast.success(`Đã cập nhật ${ids.length} học viên → ${JLPT_STATUS[to].label}`);
    setSelected([]);
  };

  return (
    <>
      <PageHeader
        title="Chứng chỉ JLPT"
        subtitle={`Học viên thi JLPT qua trung tâm: đăng ký, hồ sơ, lệ phí, phiếu dự thi, kết quả, nhận & phát chứng chỉ gốc (do hội đồng thi cấp — khác chứng nhận hoàn thành khóa) · ${currentBranch ? `Chi nhánh ${currentBranch.name}` : 'Toàn hệ thống'}`}
        breadcrumb={[{ title: 'Học viên' }, { title: 'Chứng chỉ JLPT' }]}
        actions={
          phase === 'upcoming' && (
            <Button icon={UserPlus} onClick={() => setRegistering(true)} collapseOnMobile>
              Đăng ký thi
            </Button>
          )
        }
      />

      {/* Kỳ thi */}
      <div className="mb-4 grid grid-cols-1 gap-2 sm:grid-cols-3">
        {JLPT_SESSIONS.map((s) => {
          const ph = sessionPhase(s);
          const activeTab = s.id === sessionId;
          const n = regs.filter((r) => r.sessionId === s.id && r.status !== 'cancelled').length;
          return (
            <button
              key={s.id}
              type="button"
              onClick={() => {
                setSessionId(s.id);
                setStatus(undefined);
                setResult(undefined);
                setSelected([]);
              }}
              className={cn('flex flex-col gap-1 rounded-[var(--radius-card)] border p-3 text-left transition-colors', activeTab ? 'border-primary-600 bg-primary-50' : 'border-line bg-surface hover:border-primary-300')}
            >
              <span className="flex items-center justify-between gap-2">
                <span className="font-semibold text-ink">Kỳ {s.label}</span>
                <StatusTag tone={ph === 'upcoming' ? 'warning' : ph === 'waiting' ? 'info' : 'success'} icon={CalendarDays} size="sm">
                  {ph === 'upcoming' ? `Còn ${dayjs(s.examDate).diff(dayjs().startOf('day'), 'day')} ngày` : ph === 'waiting' ? 'Chờ kết quả' : 'Có kết quả'}
                </StatusTag>
              </span>
              <span className="text-[13px] text-ink-muted">
                Thi {fmtDate(s.examDate)} · {n} học viên
              </span>
            </button>
          );
        })}
      </div>

      {phase === 'upcoming' ? (
        <>
          <div className="mb-3 flex flex-wrap items-center gap-2 rounded-[var(--radius-card)] border border-warning-100 bg-warning-50 px-4 py-2.5 text-[13px] text-warning-700">
            <CalendarDays size={16} aria-hidden />
            Thi Chủ nhật {fmtDate(session.examDate)} (còn {daysLeft} ngày) · Trung tâm nhận hồ sơ đến {fmtDate(session.regClose)}
            {dayjs().isAfter(session.regClose) && ' — đã khóa đăng ký mới với hội đồng, chỉ nhận chuyển kỳ sau'}
          </div>
          <div className="mb-4 grid grid-cols-2 gap-3 lg:grid-cols-4">
            <StatCard label="Đăng ký dự thi" value={active.length} icon={Users} hint={Q_LEVELS.map((l) => `${l}: ${active.filter((r) => r.level === l).length}`).join(' · ')} />
            <StatCard label="Thiếu hồ sơ" value={active.filter((r) => r.missingDocs).length} icon={FileWarning} tone="warning" onClick={() => setStatus(undefined)} />
            <StatCard label="Chưa đóng lệ phí" value={active.filter((r) => !r.paidAt).length} icon={Wallet} tone="danger" onClick={() => setStatus('registered')} />
            <StatCard label="Đã có phiếu dự thi" value={active.filter((r) => r.status === 'admit').length} icon={Ticket} tone="success" hint="Phát cho học viên trước ngày thi" onClick={() => setStatus('admit')} />
          </div>
        </>
      ) : (
        <>
          <div className="mb-4 grid grid-cols-2 gap-3 lg:grid-cols-4">
            <StatCard label="Dự thi" value={sat.length} icon={Users} hint={`${active.filter((r) => r.status === 'absent').length} vắng thi`} />
            <StatCard label="Đỗ" value={passed.length} icon={Trophy} tone="success" onClick={() => setResult('pass')} />
            <StatCard label="Tỉ lệ đỗ" value={sat.length ? `${Math.round((passed.length / sat.length) * 100)}%` : '—'} icon={Percent} tone="info" />
            <StatCard label="Chưa nhập điểm" value={active.filter((r) => r.status !== 'absent' && !r.scores).length} icon={ClipboardPen} tone="warning" onClick={() => setResult('none')} />
          </div>
          {sat.length > 0 && (
            <div className="mb-4 grid grid-cols-1 gap-3 lg:grid-cols-3">
              {(
                [
                  ['Theo cấp độ', passBy((r) => [r.level])],
                  ['Theo lớp', passBy((r) => (r.classCode ? [r.classCode] : []))],
                  ['Theo giáo viên', passBy((r) => r.teacherIds.map(teacherName))],
                ] as const
              ).map(([title, list]) => (
                <section key={title} className="rounded-[var(--radius-card)] border border-line bg-surface p-3">
                  <h3 className="mb-2 text-sm font-semibold text-ink">Tỉ lệ đỗ {title.toLowerCase()}</h3>
                  <ul className="flex flex-col gap-1.5">
                    {list.slice(0, 5).map(([k, v]) => {
                      const p = Math.round((v.pass / v.sat) * 100);
                      return (
                        <li key={k} className="text-[13px]">
                          <div className="flex justify-between">
                            <span className="truncate text-ink">{k}</span>
                            <span className="tabular text-ink-secondary">
                              {v.pass}/{v.sat} · <strong className={p >= 60 ? 'text-success-700' : 'text-warning-700'}>{p}%</strong>
                            </span>
                          </div>
                          <div className="mt-0.5 h-1.5 rounded-full bg-disabled">
                            <div className={cn('h-full rounded-full', p >= 60 ? 'bg-success-600' : 'bg-warning-600')} style={{ width: `${p}%` }} />
                          </div>
                        </li>
                      );
                    })}
                  </ul>
                </section>
              ))}
            </div>
          )}
        </>
      )}

      <div className="-mx-4 mb-4 flex gap-2 overflow-x-auto px-4 pb-1 [scrollbar-width:none] sm:mx-0 sm:flex-wrap sm:px-0 [&::-webkit-scrollbar]:hidden" role="tablist" aria-label="Lọc theo cấp">
        {(['all', ...Q_LEVELS] as const).map((l) => {
          const on = level === l;
          return (
            <button
              key={l}
              type="button"
              role="tab"
              aria-selected={on}
              onClick={() => setLevel(l)}
              className={cn(
                'inline-flex h-9 shrink-0 items-center gap-2 rounded-full border px-3.5 text-sm font-medium transition-colors',
                on ? 'border-primary-600 bg-primary-600 text-white' : 'border-line bg-surface text-ink-secondary hover:border-primary-300 hover:bg-primary-50',
              )}
            >
              {l === 'all' ? 'Tất cả' : l}
              <span className={cn('tabular rounded-full px-1.5 text-xs', on ? 'bg-white/20' : 'bg-disabled text-ink-muted')}>{counts[l] ?? 0}</span>
            </button>
          );
        })}
      </div>

      <DataTable<Row>
        storageKey={`students-jlpt-${phase === 'upcoming' ? 'reg' : 'result'}`}
        columns={columns}
        data={data}
        rowKey="id"
        unitLabel="học viên"
        searchKeys={['name', 'code', 'phone', 'admitNo', 'classCode']}
        onRowClick={phase !== 'upcoming' ? (r) => r.status !== 'cancelled' && setScoring(r) : undefined}
        selectedKeys={phase === 'upcoming' ? selected : undefined}
        onSelectionChange={phase === 'upcoming' ? (k) => setSelected(k) : undefined}
        bulkActions={
          phase === 'upcoming'
            ? () => (
                <>
                  <Button icon={Bell} size="sm" variant="soft" onClick={() => (notify.toast.success(`Đã nhắc ${selected.length} học viên qua Zalo / SMS`), setSelected([]))}>
                    Nhắc hồ sơ / lệ phí
                  </Button>
                  <Button icon={ClipboardPen} size="sm" variant="soft" onClick={() => bulkStep('submitted')}>
                    Đã nộp hội đồng
                  </Button>
                  <Button icon={Ticket} size="sm" tone="success" variant="soft" onClick={() => bulkStep('admit')}>
                    Đã có phiếu dự thi
                  </Button>
                </>
              )
            : undefined
        }
        search={{
          placeholder: 'Tìm theo tên, mã HV, SĐT, số báo danh, lớp...',
          activeFilterCount: [status, result].filter(Boolean).length,
          filters: (
            <>
              <Select placeholder="Trạng thái" value={status} onChange={setStatus} options={(Object.keys(JLPT_STATUS) as JlptStatus[]).map((k) => ({ value: k, label: JLPT_STATUS[k].label }))} showSearch={false} className="lg:!w-44" />
              {phase !== 'upcoming' && (
                <Select
                  placeholder="Kết quả"
                  value={result}
                  onChange={setResult}
                  options={[
                    { value: 'pass', label: 'Đỗ' },
                    { value: 'fail', label: 'Trượt' },
                    { value: 'absent', label: 'Vắng thi' },
                    { value: 'none', label: 'Chưa nhập điểm' },
                  ]}
                  showSearch={false}
                  className="lg:!w-40"
                />
              )}
            </>
          ),
          onReset: () => {
            setStatus(undefined);
            setResult(undefined);
            setLevel('all');
          },
        }}
        rowActions={
          phase !== 'upcoming'
            ? (r) => (r.status !== 'cancelled' ? <Button icon={ClipboardPen} iconOnly size="sm" variant="soft" tooltip="Nhập / sửa điểm" aria-label="Nhập điểm" onClick={() => setScoring(r)} /> : null)
            : (r) =>
                r.status !== 'cancelled' && r.status !== 'admit' ? (
                  <Button icon={X} iconOnly size="sm" tone="neutral" variant="ghost" tooltip="Hủy đăng ký" aria-label="Hủy đăng ký" onClick={() => update(r.id, { status: 'cancelled' })} />
                ) : null
        }
        actionsWidth={70}
        empty={{ icon: Users, title: 'Chưa có học viên đăng ký kỳ này' }}
      />

      {/* Đăng ký */}
      <ModalForm<{ studentIds: string[]; level: QLevel; venue: string; paid: boolean }>
        open={registering}
        onClose={() => setRegistering(false)}
        title={`Đăng ký thi JLPT kỳ ${session.label}`}
        description={`Lệ phí: N1 ${formatCurrency(JLPT_FEE.N1)} · N2 ${formatCurrency(JLPT_FEE.N2)} · N3 ${formatCurrency(JLPT_FEE.N3)} · N4/N5 ${formatCurrency(JLPT_FEE.N4)}`}
        submitAction="create"
        submitText="Đăng ký"
        successMessage="Đã đăng ký thi"
        initialValues={{ level: 'N4', venue: JLPT_VENUES[0], studentIds: [] }}
        onSubmit={(v) => {
          const taken = new Set(regs.filter((r) => r.sessionId === sessionId && r.status !== 'cancelled').map((r) => r.studentId));
          v.studentIds.filter((id) => !taken.has(id)).forEach((studentId) => add({ studentId, sessionId, level: v.level, venue: v.venue, status: 'registered', docs: { photo: false, idCard: false, form: false } }));
        }}
      >
        <Form.Item name="studentIds" label="Học viên" rules={[{ required: true, type: 'array', min: 1, message: 'Chọn học viên' }]}>
          <Select
            mode="multiple"
            optionFilterProp="label"
            placeholder="Tìm theo tên, mã HV"
            options={students.filter((s) => !s.hidden && (branchId === ALL_BRANCHES || s.branchId === branchId)).map((s) => ({ value: s.id, label: `${s.fullName} · ${s.code}`, disabled: regs.some((r) => r.sessionId === sessionId && r.studentId === s.id && r.status !== 'cancelled') }))}
          />
        </Form.Item>
        <div className="grid grid-cols-1 gap-x-3 sm:grid-cols-[120px_1fr]">
          <Form.Item name="level" label="Cấp độ" rules={[{ required: true }]}>
            <Select options={Q_LEVELS.map((l) => ({ value: l, label: l }))} showSearch={false} allowClear={false} />
          </Form.Item>
          <Form.Item name="venue" label="Địa điểm thi" rules={[{ required: true }]}>
            <Select options={JLPT_VENUES.map((x) => ({ value: x, label: x }))} showSearch={false} allowClear={false} />
          </Form.Item>
        </div>
      </ModalForm>

      {/* Nhập điểm */}
      {scoring && <ScoreModal row={scoring} onClose={() => setScoring(null)} onSave={(scores, absent) => update(scoring.id, absent ? { status: 'absent', scores: undefined } : { status: 'done', scores })} />}
      <ModalForm<{ no: string; receivedAt: Dayjs }>
        key={`cert-${certFor?.id}`}
        open={Boolean(certFor)}
        onClose={() => setCertFor(null)}
        title={`Nhận chứng chỉ gốc · ${certFor?.name ?? ''} (JLPT ${certFor?.level ?? ''})`}
        description="Chứng chỉ do hội đồng thi gửi về trung tâm. Lưu số chứng chỉ để tra cứu; học viên được báo đến nhận."
        submitAction="save"
        successMessage="Đã ghi nhận chứng chỉ về — học viên được thông báo đến nhận"
        initialValues={{ receivedAt: dayjs() }}
        onSubmit={(v) => update(certFor!.id, { certificate: { no: v.no.trim(), receivedAt: v.receivedAt.format('YYYY-MM-DD'), receivedBy: user?.fullName ?? 'Giáo vụ' } })}
      >
        <Form.Item name="no" label="Số chứng chỉ" rules={[{ required: true, message: 'Nhập số chứng chỉ' }]}>
          <TextInput placeholder="Vd: N3A123456789" />
        </Form.Item>
        <Form.Item name="receivedAt" label="Ngày về trung tâm" rules={[{ required: true }]}>
          <DateInput />
        </Form.Item>
      </ModalForm>
    </>
  );
}

function ScoreModal({ row, onClose, onSave }: { row: Row; onClose: () => void; onSave: (scores: number[], absent: boolean) => void }) {
  const [form] = Form.useForm<{ s: number[] }>();
  const s = Form.useWatch('s', form) ?? row.scores ?? [];
  const j = s.length === SECTIONS[row.level].length && s.every((x) => typeof x === 'number') ? judge(row.level, s) : undefined;
  return (
    <ModalForm<{ s: number[] }>
      form={form}
      open
      onClose={onClose}
      title={`Kết quả JLPT ${row.level} — ${row.name}`}
      description={`Điểm đỗ ${PASS_TOTAL[row.level]}/180 và mỗi phần không dưới điểm liệt`}
      submitAction="save"
      submitText="Lưu kết quả"
      successMessage="Đã lưu kết quả"
      initialValues={{ s: row.scores ?? [] }}
      onSubmit={(v) => onSave(v.s, false)}
      footerExtra={
        <Button tone="danger" variant="ghost" icon={X} onClick={() => (onSave([], true), onClose())}>
          Vắng thi
        </Button>
      }
    >
      {SECTIONS[row.level].map((sec, i) => (
        <Form.Item key={sec.name} name={['s', i]} label={`${sec.name} (0–${sec.max}, liệt < ${sec.min})`} rules={[{ required: true, message: 'Nhập điểm' }, { type: 'number', min: 0, max: sec.max, message: `0–${sec.max}` }]}>
          <NumberInput min={0} max={sec.max} />
        </Form.Item>
      ))}
      {j && (
        <p className={cn('rounded-lg px-3 py-2 text-[13px] font-medium', j.passed ? 'bg-success-50 text-success-700' : 'bg-danger-50 text-danger-700')}>
          Tổng {j.total}/180 → {j.passed ? 'ĐỖ' : j.failSections.length ? `TRƯỢT — dưới điểm liệt: ${j.failSections.join(', ')}` : `TRƯỢT — thiếu ${PASS_TOTAL[row.level] - j.total} điểm`}
        </p>
      )}
    </ModalForm>
  );
}
