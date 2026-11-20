import { useEffect, useMemo, useState } from 'react';
import dayjs from 'dayjs';
import { CircleDashed, OctagonAlert, ShieldCheck, TriangleAlert, UserX } from 'lucide-react';
import { DataTable, PageHeader, Select, StatCard, StatusTag, UserCell, type DataColumn } from '@/components/common';
import { useClassStore } from '@/features/academic/classes/classStore';
import { ALERT_RULES, useAllStudentStats, type StudentStats } from '@/features/academic/classes/studentInsights';
import type { ClassInfo } from '@/features/academic/classes/types';
import { useCrmStore } from '@/features/crm/crmStore';
import { CARE_STAFF, CARE_STATUS, CARE_STATUS_OPTIONS, caseKey, RISK_LEVEL, riskLevel, useAtRiskStore, type CareCase, type RiskLevel } from '@/features/student/atRisk/atRisk';
import { AtRiskDrawer } from '@/features/student/atRisk/AtRiskDrawer';
import { useAuth } from '@/hooks/useAuth';
import { cn } from '@/lib/cn';
import { fmtDate, fromNow } from '@/lib/date';
import { ALL_BRANCHES } from '@/types/auth';
import { USE_API } from '@/lib/api/config';
import { loadCareCases, useCareStaff } from '@/features/student/atRisk/careApi';

interface Row {
  id: string;
  cls: ClassInfo;
  st: StudentStats;
  name: string;
  code: string;
  phone?: string;
  level: RiskLevel;
  care: CareCase;
  lastContact?: string;
}

const REASONS = [
  { value: 'absent', label: 'Vắng nhiều' },
  { value: 'streak', label: 'Vắng liên tiếp' },
  { value: 'rate', label: 'Chuyên cần thấp' },
  { value: 'submit', label: 'Không nộp bài' },
  { value: 'score', label: 'Điểm thấp' },
  { value: 'exam', label: 'Bài thi' },
  { value: 'tuition', label: 'Nợ học phí' },
];
const DOT: Record<string, string> = { present: 'bg-success-600', late: 'bg-warning-600', excused: 'bg-primary-400', absent: 'bg-danger-600' };

/** Cảnh báo bỏ học — tự phát hiện từ điểm danh, bài tập, điểm, học phí. DỮ LIỆU GIẢ. */
export default function AtRiskPage() {
  const { branchId, currentBranch } = useAuth();
  const classes = useClassStore((s) => s.classes);
  const students = useCrmStore((s) => s.students);
  const cases = useAtRiskStore((s) => s.cases);
  const staffNames = useCareStaff();
  // Chế độ API: phần chăm sóc (người phụ trách, tiến độ, hẹn) lưu ở server
  useEffect(() => {
    if (USE_API) void loadCareCases().catch(() => undefined);
  }, []);

  const [level, setLevel] = useState<RiskLevel | 'all'>('all');
  const [classId, setClassId] = useState<string>();
  const [reason, setReason] = useState<string>();
  const [care, setCare] = useState<string>();
  const [assignee, setAssignee] = useState<string>();
  const [open, setOpen] = useState<Row | null>(null);

  const scope = useMemo(() => classes.filter((c) => c.status === 'ongoing' && (branchId === ALL_BRANCHES || c.branchId === branchId)), [classes, branchId]);
  const all = useAllStudentStats(scope);

  const rows: Row[] = useMemo(
    () =>
      all.flatMap(({ cls, map }) =>
        [...map.values()].flatMap((st) => {
          const lv = riskLevel(st);
          if (!lv) return [];
          const s = students.find((x) => x.id === st.enrollment.studentId);
          const key = caseKey(cls.id, st.enrollment.studentId);
          return [
            {
              id: key,
              cls,
              st,
              name: s?.fullName ?? st.enrollment.studentId,
              code: s?.code ?? '',
              phone: s?.phone,
              level: lv,
              care: cases[key] ?? { status: 'new' as const, updatedAt: '' },
              lastContact: [...st.careLogs].sort((a, b) => b.at.localeCompare(a.at))[0]?.at,
            },
          ];
        }),
      ),
    [all, students, cases],
  );

  const base = rows.filter(
    (r) => (!classId || r.cls.id === classId) && (!reason || r.st.flags.some((f) => f.key === reason)) && (!care || r.care.status === care) && (!assignee || (assignee === 'none' ? !r.care.assignee : r.care.assignee === assignee)),
  );
  const counts: Record<string, number> = { all: base.length };
  base.forEach((r) => (counts[r.level] = (counts[r.level] ?? 0) + 1));
  const order = { high: 0, medium: 1, low: 2 };
  const data = (level === 'all' ? base : base.filter((r) => r.level === level)).sort((a, b) => order[a.level] - order[b.level] || b.st.risk - a.st.risk);

  const today = dayjs().format('YYYY-MM-DD');
  const stats = {
    high: rows.filter((r) => r.level === 'high').length,
    medium: rows.filter((r) => r.level === 'medium').length,
    untouched: rows.filter((r) => r.care.status === 'new').length,
    due: rows.filter((r) => r.care.nextAt && r.care.nextAt <= today && r.care.status !== 'resolved').length,
  };

  const columns: DataColumn<Row>[] = [
    {
      key: 'name',
      title: 'Học viên',
      width: 210,
      mobile: 'title',
      sorter: (a, b) => a.name.localeCompare(b.name, 'vi'),
      render: (_, r) => <UserCell size="sm" name={r.name} subtitle={`${r.code}${r.phone ? ` · ${r.phone}` : ''}`} />,
    },
    { key: 'cls', title: 'Lớp', width: 130, mobile: 'subtitle', render: (_, r) => <span className="font-mono text-xs">{r.cls.code}</span> },
    {
      key: 'flags',
      title: 'Dấu hiệu',
      width: 280,
      render: (_, r) => (
        <span className="flex flex-wrap gap-1">
          {r.st.flags.map((f, i) => (
            <span key={i} className={cn('rounded px-1.5 py-0.5 text-[11px] font-medium', f.tone === 'danger' ? 'bg-danger-50 text-danger-700' : 'bg-warning-50 text-warning-700')}>
              {f.label}
            </span>
          ))}
        </span>
      ),
    },
    {
      key: 'timeline',
      title: 'Điểm danh gần đây',
      width: 140,
      render: (_, r) => (
        <span className="flex gap-0.5" aria-label="Điểm danh gần đây">
          {r.st.attendance.timeline.slice(-8).map((t) => (
            <span key={t.sessionId} className={cn('h-4 w-2.5 rounded-sm', t.status ? DOT[t.status] : 'bg-disabled')} />
          ))}
        </span>
      ),
    },
    {
      key: 'level',
      title: 'Mức độ',
      width: 120,
      sorter: (a, b) => order[a.level] - order[b.level],
      render: (_, r) => (
        <StatusTag tone={RISK_LEVEL[r.level].tone} icon={r.level === 'high' ? OctagonAlert : TriangleAlert} size="sm">
          {RISK_LEVEL[r.level].label}
        </StatusTag>
      ),
    },
    {
      key: 'care',
      title: 'Chăm sóc',
      width: 190,
      mobile: 'aside',
      render: (_, r) => {
        const cs = CARE_STATUS[r.care.status];
        return (
          <div className="flex flex-col gap-0.5">
            <StatusTag tone={cs.tone} icon={cs.icon} size="sm">
              {cs.label}
            </StatusTag>
            <span className="truncate text-[11px] text-ink-muted">
              {r.care.assignee ?? 'Chưa giao'}
              {r.care.nextAt && ` · hẹn ${fmtDate(r.care.nextAt)}`}
            </span>
          </div>
        );
      },
    },
    {
      key: 'last',
      title: 'Liên hệ gần nhất',
      width: 130,
      render: (_, r) => (r.lastContact ? <span className="text-[13px] text-ink-secondary">{fromNow(r.lastContact)}</span> : <span className="text-[13px] text-danger-600">Chưa liên hệ</span>),
    },
  ];

  return (
    <>
      <PageHeader
        title="Cảnh báo bỏ học"
        subtitle={`Tự phát hiện: vắng ≥ ${ALERT_RULES.absent} buổi / ${ALERT_RULES.streak} buổi liền, chuyên cần < ${ALERT_RULES.attendanceRate}%, không nộp bài, điểm thấp, nợ quá hạn · ${currentBranch ? `Chi nhánh ${currentBranch.name}` : 'Toàn hệ thống'}`}
        breadcrumb={[{ title: 'Học viên' }, { title: 'Cảnh báo bỏ học' }]}
      />

      <div className="mb-4 grid grid-cols-2 gap-3 lg:grid-cols-4">
        <StatCard label="Nguy cơ cao" value={stats.high} icon={OctagonAlert} tone="danger" hint="Liên hệ trong ngày" onClick={() => setLevel('high')} />
        <StatCard label="Nguy cơ trung bình" value={stats.medium} icon={TriangleAlert} tone="warning" onClick={() => setLevel('medium')} />
        <StatCard label="Chưa ai liên hệ" value={stats.untouched} icon={CircleDashed} tone={stats.untouched ? 'danger' : 'success'} onClick={() => setCare('new')} />
        <StatCard label="Đến hẹn gọi lại" value={stats.due} icon={stats.due ? UserX : ShieldCheck} tone={stats.due ? 'warning' : 'success'} hint="Hôm nay hoặc đã quá hẹn" />
      </div>

      <div className="-mx-4 mb-4 flex gap-2 overflow-x-auto px-4 pb-1 [scrollbar-width:none] sm:mx-0 sm:flex-wrap sm:px-0 [&::-webkit-scrollbar]:hidden" role="tablist" aria-label="Lọc theo mức độ">
        {(['all', 'high', 'medium', 'low'] as const).map((k) => {
          const active = level === k;
          return (
            <button
              key={k}
              type="button"
              role="tab"
              aria-selected={active}
              onClick={() => setLevel(k)}
              className={cn(
                'inline-flex h-9 shrink-0 items-center gap-2 rounded-full border px-3.5 text-sm font-medium transition-colors',
                active ? 'border-primary-600 bg-primary-600 text-white' : 'border-line bg-surface text-ink-secondary hover:border-primary-300 hover:bg-primary-50',
              )}
            >
              {k === 'all' ? 'Tất cả' : RISK_LEVEL[k].label}
              <span className={cn('tabular rounded-full px-1.5 text-xs', active ? 'bg-white/20' : 'bg-disabled text-ink-muted')}>{counts[k] ?? 0}</span>
            </button>
          );
        })}
      </div>

      <DataTable<Row>
        storageKey="students-at-risk"
        columns={columns}
        data={data}
        rowKey="id"
        unitLabel="trường hợp"
        searchKeys={['name', 'code', 'phone']}
        onRowClick={setOpen}
        search={{
          placeholder: 'Tìm học viên theo tên, mã, SĐT...',
          activeFilterCount: [classId, reason, care, assignee].filter(Boolean).length,
          filters: (
            <>
              <Select placeholder="Lớp" value={classId} onChange={setClassId} options={scope.map((c) => ({ value: c.id, label: c.code }))} className="lg:!w-40" />
              <Select placeholder="Dấu hiệu" value={reason} onChange={setReason} options={REASONS} showSearch={false} className="lg:!w-40" />
              <Select placeholder="Chăm sóc" value={care} onChange={setCare} options={CARE_STATUS_OPTIONS} showSearch={false} className="lg:!w-40" />
              <Select placeholder="Phụ trách" value={assignee} onChange={setAssignee} options={[{ value: 'none', label: 'Chưa giao' }, ...(USE_API ? staffNames : CARE_STAFF).map((x) => ({ value: x, label: x }))]} className="lg:!w-52" />
            </>
          ),
          onReset: () => {
            setClassId(undefined);
            setReason(undefined);
            setCare(undefined);
            setAssignee(undefined);
            setLevel('all');
          },
        }}
        empty={{ icon: ShieldCheck, title: 'Không có học viên nào cần cảnh báo', description: 'Tất cả học viên đang học đều ổn định.' }}
      />

      {open && <AtRiskDrawer item={rows.find((r) => r.id === open.id) ?? open} onClose={() => setOpen(null)} />}
    </>
  );
}
