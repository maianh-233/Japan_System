import { useMemo, useState, type Key } from 'react';
import { Modal } from 'antd';
import dayjs from 'dayjs';
import { Award, BadgeCheck, CircleX, Eye, Hourglass, Mail, QrCode, Search, Send, ShieldAlert } from 'lucide-react';
import { Button, DataTable, PageHeader, Select, StatCard, StatusTag, UserCell, useConfirm, useNotify, type DataColumn } from '@/components/common';
import { useClassStore } from '@/features/academic/classes/classStore';
import { FakeQr } from '@/features/academic/classes/detail/SessionAttendanceTab';
import { CertificateModal, certificateChecks } from '@/features/academic/classes/detail/StudentActions';
import { useAllStudentStats } from '@/features/academic/classes/studentInsights';
import type { ClassInfo } from '@/features/academic/classes/types';
import { useCrmStore } from '@/features/crm/crmStore';
import { useStudentStore } from '@/features/student/studentStore';
import type { Certificate } from '@/features/student/types';
import { useAuth } from '@/hooks/useAuth';
import { cn } from '@/lib/cn';
import { fmtDate } from '@/lib/date';
import { getBranches } from '@/features/system/useBranches';
import { ALL_BRANCHES } from '@/types/auth';

type Tab = 'ready' | 'blocked' | 'issued';

interface Row {
  id: string;
  enrollmentId: string;
  cls: ClassInfo;
  studentId: string;
  name: string;
  code: string;
  email?: string;
  checks: { ok: boolean; label: string }[];
  eligible: boolean;
  cert?: Certificate;
  endedAt?: string;
}

/** Chứng nhận hoàn thành khóa — DỮ LIỆU GIẢ. */
export default function CertificatesPage() {
  const { user, branchId, currentBranch } = useAuth();
  const notify = useNotify();
  const { confirm, confirmDialog } = useConfirm();
  const classes = useClassStore((s) => s.classes);
  const students = useCrmStore((s) => s.students);
  const sendCertificate = useStudentStore((s) => s.sendCertificate);
  const [tab, setTab] = useState<Tab>('ready');
  const [classId, setClassId] = useState<string>();
  const [selected, setSelected] = useState<Key[]>([]);
  const [issuing, setIssuing] = useState<Row | null>(null);
  const [preview, setPreview] = useState<Row | null>(null);
  const [lookup, setLookup] = useState('');
  const [checked, setChecked] = useState<string | null>(null);

  const scope = useMemo(() => classes.filter((c) => (c.status === 'finished' || c.status === 'ongoing') && (branchId === ALL_BRANCHES || c.branchId === branchId)), [classes, branchId]);
  const all = useAllStudentStats(scope);

  const rows: Row[] = useMemo(
    () =>
      all.flatMap(({ cls, map }) =>
        [...map.values()]
          .filter((st) => st.enrollment.status === 'completed' || st.enrollment.certificate)
          .map((st) => {
            const s = students.find((x) => x.id === st.enrollment.studentId);
            const checks = certificateChecks(cls, st);
            return {
              id: st.enrollment.id,
              enrollmentId: st.enrollment.id,
              cls,
              studentId: st.enrollment.studentId,
              name: s?.fullName ?? st.enrollment.studentId,
              code: s?.code ?? '',
              email: s?.email,
              checks,
              eligible: checks.every((c) => c.ok),
              cert: st.enrollment.certificate,
              endedAt: st.enrollment.endedAt,
            };
          }),
      ),
    [all, students],
  );

  const inClass = rows.filter((r) => !classId || r.cls.id === classId);
  const groups: Record<Tab, Row[]> = {
    ready: inClass.filter((r) => !r.cert && r.eligible),
    blocked: inClass.filter((r) => !r.cert && !r.eligible),
    issued: inClass.filter((r) => r.cert),
  };
  const data = groups[tab];
  const me = user?.fullName ?? 'Giáo vụ';

  const bulkIssue = async () => {
    const list = groups.ready.filter((r) => selected.includes(r.id));
    const noMail = list.filter((r) => !r.email);
    const ok = await confirm({
      title: `Cấp & gửi chứng nhận cho ${list.length - noMail.length} học viên?`,
      description: 'Hệ thống sinh mã chứng nhận + mã QR xác thực, gửi file PDF qua email học viên.',
      consequences: noMail.length ? [`Bỏ qua ${noMail.length} học viên chưa có email: ${noMail.map((r) => r.name).join(', ')}`] : undefined,
      tone: 'primary',
      confirmAction: 'send',
      confirmText: 'Cấp & gửi',
    });
    if (!ok) return;
    list.filter((r) => r.email).forEach((r) => sendCertificate(r.enrollmentId, { email: r.email!, by: me }));
    notify.success({ title: `Đã cấp ${list.length - noMail.length} chứng nhận` });
    setSelected([]);
  };

  const found = checked !== null ? rows.find((r) => r.cert?.code.toLowerCase() === checked.trim().toLowerCase()) : undefined;

  const columns: DataColumn<Row>[] = [
    { key: 'name', title: 'Học viên', width: 220, mobile: 'title', sorter: (a, b) => a.name.localeCompare(b.name, 'vi'), render: (_, r) => <UserCell size="sm" name={r.name} subtitle={`${r.code}${r.email ? ` · ${r.email}` : ' · chưa có email'}`} /> },
    { key: 'cls', title: 'Khóa / lớp', width: 220, mobile: 'subtitle', render: (_, r) => <div className="leading-5"><p className="text-ink">{r.cls.courseLabel}</p><p className="font-mono text-xs text-ink-muted">{r.cls.code}</p></div> },
    ...(tab === 'issued'
      ? ([
          { key: 'certCode', title: 'Mã chứng nhận', width: 210, render: (_, r) => <span className="font-mono text-xs">{r.cert!.code}</span> },
          {
            key: 'sent',
            title: 'Đã gửi',
            width: 220,
            render: (_, r) => (
              <div className="leading-5 text-[13px]">
                <p className="text-ink">{fmtDate(r.cert!.issuedAt)} · {r.cert!.sendCount} lần</p>
                <p className="truncate text-xs text-ink-muted">{r.cert!.sentTo}</p>
                {r.cert!.exception && <p className="text-xs text-warning-700">Cấp ngoại lệ: {r.cert!.exception}</p>}
              </div>
            ),
          },
        ] as DataColumn<Row>[])
      : ([
          {
            key: 'checks',
            title: 'Điều kiện',
            width: 360,
            render: (_, r) => (
              <ul className="flex flex-col gap-0.5 text-xs">
                {r.checks.map((c) => (
                  <li key={c.label} className={cn('flex items-center gap-1', c.ok ? 'text-success-700' : 'text-danger-600')}>
                    {c.ok ? <BadgeCheck size={12} aria-hidden /> : <CircleX size={12} aria-hidden />} {c.label}
                  </li>
                ))}
              </ul>
            ),
          },
        ] as DataColumn<Row>[])),
    { key: 'endedAt', title: 'Kết thúc', width: 110, mobile: 'aside', render: (_, r) => (r.endedAt ? fmtDate(r.endedAt) : '—') },
  ];

  return (
    <>
      <PageHeader
        title="Chứng nhận hoàn thành khóa"
        subtitle={`Chứng nhận của TRUNG TÂM cấp cho học viên pass lớp (PDF + mã QR xác thực) — khác chứng chỉ JLPT do hội đồng thi cấp · ${currentBranch ? `Chi nhánh ${currentBranch.name}` : 'Toàn hệ thống'}`}
        breadcrumb={[{ title: 'Học viên' }, { title: 'Chứng nhận hoàn thành khóa' }]}
      />

      <div className="mb-4 grid grid-cols-1 gap-3 lg:grid-cols-[1fr_1fr_1fr_1.4fr]">
        <StatCard label="Chờ cấp" value={groups.ready.length} icon={Hourglass} tone="warning" hint="Đủ điều kiện, chưa gửi" onClick={() => setTab('ready')} />
        <StatCard label="Chưa đủ điều kiện" value={groups.blocked.length} icon={ShieldAlert} tone="danger" hint="Chuyên cần / bài thi / học phí" onClick={() => setTab('blocked')} />
        <StatCard label="Đã cấp" value={groups.issued.length} icon={Award} tone="success" onClick={() => setTab('issued')} />
        {/* Tra cứu mã */}
        <div className="flex flex-col gap-2 rounded-[var(--radius-card)] border border-line bg-surface p-4 shadow-[var(--shadow-soft-sm)]">
          <p className="flex items-center gap-1.5 text-[13px] font-medium text-ink-secondary">
            <QrCode size={15} aria-hidden /> Tra cứu chứng nhận (đơn vị tuyển dụng / trường gọi hỏi)
          </p>
          <div className="flex gap-2">
            <input
              value={lookup}
              onChange={(e) => {
                setLookup(e.target.value);
                setChecked(null);
              }}
              onKeyDown={(e) => e.key === 'Enter' && setChecked(lookup)}
              placeholder="VD: CN-2026-N4K11-0001"
              className="h-9 min-w-0 flex-1 rounded-[var(--radius-control)] border border-line px-3 font-mono text-sm outline-none focus:border-primary-500"
            />
            <Button icon={Search} size="sm" onClick={() => setChecked(lookup)} disabled={!lookup.trim()}>
              Tra
            </Button>
          </div>
          {checked !== null &&
            (found ? (
              <p className="text-xs text-success-700">
                ✓ Hợp lệ — {found.name}, {found.cls.courseLabel} ({found.cls.code}), cấp {fmtDate(found.cert!.issuedAt)}
              </p>
            ) : (
              <p className="text-xs text-danger-600">✗ Không tìm thấy mã này — chứng nhận có thể giả mạo.</p>
            ))}
        </div>
      </div>

      <div className="mb-4 flex flex-wrap items-center gap-2">
        {(
          [
            ['ready', 'Chờ cấp'],
            ['blocked', 'Chưa đủ điều kiện'],
            ['issued', 'Đã cấp'],
          ] as [Tab, string][]
        ).map(([k, l]) => (
          <button
            key={k}
            type="button"
            onClick={() => {
              setTab(k);
              setSelected([]);
            }}
            className={cn(
              'inline-flex h-9 items-center gap-2 rounded-full border px-3.5 text-sm font-medium transition-colors',
              tab === k ? 'border-primary-600 bg-primary-600 text-white' : 'border-line bg-surface text-ink-secondary hover:border-primary-300 hover:bg-primary-50',
            )}
          >
            {l}
            <span className={cn('tabular rounded-full px-1.5 text-xs', tab === k ? 'bg-white/20' : 'bg-disabled text-ink-muted')}>{groups[k].length}</span>
          </button>
        ))}
      </div>

      <DataTable<Row>
        storageKey={`students-certificates-${tab}`}
        columns={columns}
        data={data}
        rowKey="id"
        unitLabel="học viên"
        searchKeys={['name', 'code', 'email']}
        onRowClick={(r) => (r.cert ? setPreview(r) : setIssuing(r))}
        selectedKeys={tab === 'ready' ? selected : undefined}
        onSelectionChange={tab === 'ready' ? (k) => setSelected(k) : undefined}
        bulkActions={
          tab === 'ready'
            ? () => (
                <Button icon={Send} size="sm" onClick={bulkIssue}>
                  Cấp & gửi ({selected.length})
                </Button>
              )
            : undefined
        }
        search={{
          placeholder: 'Tìm học viên theo tên, mã, email...',
          activeFilterCount: classId ? 1 : 0,
          filters: <Select placeholder="Lớp" value={classId} onChange={setClassId} options={scope.map((c) => ({ value: c.id, label: c.code }))} className="lg:!w-48" />,
          onReset: () => setClassId(undefined),
        }}
        rowActions={(r) =>
          r.cert ? (
            <>
              <Button icon={Eye} iconOnly size="sm" variant="soft" tooltip="Xem chứng nhận" aria-label="Xem" onClick={() => setPreview(r)} />
              <Button icon={Mail} iconOnly size="sm" tone="neutral" variant="soft" tooltip="Gửi lại email" aria-label="Gửi lại" onClick={() => setIssuing(r)} />
            </>
          ) : (
            <Button icon={Send} iconOnly size="sm" variant="soft" tooltip={r.eligible ? 'Cấp & gửi' : 'Xem điều kiện / cấp ngoại lệ'} aria-label="Cấp" onClick={() => setIssuing(r)} />
          )
        }
        actionsWidth={110}
        empty={{ icon: Award, title: tab === 'ready' ? 'Không có học viên chờ cấp' : tab === 'blocked' ? 'Không có học viên thiếu điều kiện' : 'Chưa cấp chứng nhận nào' }}
      />

      {issuing && <CertificateModal cls={issuing.cls} studentId={issuing.studentId} onClose={() => setIssuing(null)} />}

      <Modal open={Boolean(preview)} onCancel={() => setPreview(null)} footer={null} width={760} title="Chứng nhận hoàn thành khóa học" destroyOnHidden>
        {preview?.cert && <CertificateView row={preview} />}
      </Modal>
      {confirmDialog}
    </>
  );
}

/** Mẫu chứng nhận (bản PDF gửi học viên trông như thế này) */
function CertificateView({ row }: { row: Row }) {
  const branch = getBranches(false).find((b) => b.id === row.cls.branchId);
  return (
    <div className="rounded-lg border-[6px] border-double border-primary-700 bg-white p-6 sm:p-10">
      <div className="flex flex-col items-center gap-2 text-center">
        <p className="text-xs tracking-[0.3em] text-ink-muted">JP CENTER · TRUNG TÂM TIẾNG NHẬT</p>
        <p className="text-sm text-primary-700" lang="ja">
          修了証書
        </p>
        <h2 className="text-2xl font-bold tracking-wide text-primary-800 sm:text-3xl">CHỨNG NHẬN HOÀN THÀNH</h2>
        <p className="mt-3 text-sm text-ink-secondary">Chứng nhận học viên</p>
        <p className="text-2xl font-semibold text-ink sm:text-3xl" style={{ fontFamily: 'Georgia, serif' }}>
          {row.name}
        </p>
        <p className="max-w-md text-sm text-ink-secondary">
          đã hoàn thành khóa học <strong className="text-ink">{row.cls.courseLabel}</strong> ({row.cls.totalHours} giờ) tại lớp {row.cls.code}, từ {fmtDate(row.cls.actualStartDate ?? row.cls.plannedStartDate)} đến{' '}
          {fmtDate(row.endedAt ?? row.cls.actualEndDate ?? row.cls.plannedEndDate)}.
        </p>
      </div>
      <div className="mt-8 flex items-end justify-between gap-4">
        <div className="flex items-end gap-3">
          <FakeQr seed={row.cert!.code} className="h-20 w-20 text-ink" />
          <div className="text-[11px] text-ink-muted">
            <p>Mã: <span className="font-mono text-ink">{row.cert!.code}</span></p>
            <p>Quét QR hoặc tra cứu mã tại jpcenter.vn/xac-thuc</p>
          </div>
        </div>
        <div className="text-center text-xs text-ink-secondary">
          <p>
            {branch?.name}, {dayjs(row.cert!.issuedAt).format('[ngày] DD [tháng] MM [năm] YYYY')}
          </p>
          <p className="mt-8 font-semibold text-ink">GIÁM ĐỐC TRUNG TÂM</p>
        </div>
      </div>
      {row.cert!.exception && <StatusTag tone="warning" icon={ShieldAlert} size="sm">Cấp ngoại lệ</StatusTag>}
    </div>
  );
}
