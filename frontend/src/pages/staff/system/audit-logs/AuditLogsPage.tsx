import { useEffect, useMemo, useState } from 'react';
import { Drawer } from 'antd';
import dayjs from 'dayjs';
import { ArrowRight, Download, History, LogIn, ShieldAlert } from 'lucide-react';
import { Button, DataTable, DateRangeInput, PageHeader, Select, StatCard, StatusTag, useNotify, type DataColumn, type DateRange } from '@/components/common';
import { AUDIT_CATEGORIES, AUDIT_SEVERITY } from '@/features/system/constants';
import { exportAuditLogs, fetchAuditActors, fetchAuditLogs, fetchAuditStats, MAX_ROWS, type AuditActor, type AuditStats } from '@/features/system/auditApi';
import { USE_API } from '@/lib/api/config';
import { ApiError } from '@/lib/api/http';
import { useSystemStore } from '@/features/system/systemStore';
import type { AuditLog } from '@/features/system/types';
import { useActor } from '@/features/system/useActor';
import { useAuth } from '@/hooks/useAuth';
import { useBreakpoint } from '@/hooks/useBreakpoint';
import { fmtDateTime, fromNow } from '@/lib/date';
import { getBranches } from '@/features/system/useBranches';
import { ALL_BRANCHES } from '@/types/auth';

const branchName = (id?: string) => (id ? (getBranches(false).find((b) => b.id === id)?.name ?? id) : 'Toàn hệ thống');

/**
 * Nhật ký hệ thống: ai làm gì, lúc nào, từ đâu. CHỈ ĐỌC — không sửa / xóa được.
 * Chế độ API: server lọc (phạm vi quyền, chi nhánh, loại, mức độ, người, khoảng ngày — mặc định 30 ngày), trang tìm trong tối đa 1000 dòng mới nhất.
 */
export default function AuditLogsPage() {
  const { branchId, canDo } = useAuth();
  const { isMobile } = useBreakpoint();
  const notify = useNotify();
  const { actor } = useActor();
  const audits = useSystemStore((s) => s.audits);
  const audit = useSystemStore((s) => s.audit);

  const [category, setCategory] = useState<string>();
  const [severity, setSeverity] = useState<string>();
  const [who, setWho] = useState<string>();
  const [range, setRange] = useState<DateRange>(null);
  const [openId, setOpenId] = useState<string | null>(null);

  // ----- Chế độ API -----
  const [server, setServer] = useState<{ rows: AuditLog[]; total: number }>({ rows: [], total: 0 });
  const [stats, setStats] = useState<AuditStats>();
  const [people, setPeople] = useState<AuditActor[]>([]);
  const [loading, setLoading] = useState(USE_API);
  const filter = {
    branchId,
    category,
    severity,
    actorId: who,
    from: range?.[0]?.format('YYYY-MM-DD'),
    to: range?.[1]?.format('YYYY-MM-DD'),
  };
  const filterKey = JSON.stringify(filter);
  useEffect(() => {
    if (!USE_API) return;
    let cancelled = false;
    setLoading(true);
    const t = setTimeout(() => {
      fetchAuditLogs(JSON.parse(filterKey))
        .then((r) => !cancelled && setServer(r))
        .catch((e) => !cancelled && notify.toast.error(e instanceof ApiError ? e.message : 'Không tải được nhật ký'))
        .finally(() => !cancelled && setLoading(false));
    }, 250);
    return () => {
      cancelled = true;
      clearTimeout(t);
    };
    // eslint-disable-next-line react-hooks/exhaustive-deps
  }, [filterKey]);
  useEffect(() => {
    if (!USE_API) return;
    void fetchAuditStats(branchId).then(setStats).catch(() => undefined);
  }, [branchId, server]);
  useEffect(() => {
    if (USE_API) void fetchAuditActors().then(setPeople).catch(() => undefined);
  }, []);

  // Theo chi nhánh đang chọn: thấy log của chi nhánh đó + log toàn hệ thống
  const base = useMemo(() => audits.filter((a) => branchId === ALL_BRANCHES || !a.branchId || a.branchId === branchId), [audits, branchId]);
  const localRows = base.filter(
    (a) =>
      (!category || a.category === category) &&
      (!severity || a.severity === severity) &&
      (!who || a.actor === who) &&
      (!range?.[0] || !dayjs(a.at).isBefore(range[0].startOf('day'))) &&
      (!range?.[1] || !dayjs(a.at).isAfter(range[1].endOf('day'))),
  );
  const rows = USE_API ? server.rows : localRows;
  const open = rows.find((a) => a.id === openId);
  const week = base.filter((a) => dayjs().diff(dayjs(a.at), 'day') < 7);
  const counts: AuditStats = stats ?? {
    today: base.filter((a) => dayjs(a.at).isSame(dayjs(), 'day')).length,
    criticalWeek: week.filter((a) => a.severity === 'critical').length,
    exportWeek: week.filter((a) => a.category === 'export').length,
    failedLoginWeek: week.filter((a) => a.category === 'auth' && a.action.includes('thất bại')).length,
  };
  const actorOptions = USE_API
    ? people.map((p) => ({ value: p.id, label: `${p.name} · ${p.role}` }))
    : [...new Set(base.map((a) => a.actor))].sort((a, b) => a.localeCompare(b, 'vi')).map((a) => ({ value: a, label: a }));

  const doExport = async () => {
    if (!USE_API) {
      // Xuất nhật ký cũng là một thao tác phải ghi nhật ký
      audit(actor, { category: 'export', severity: 'warning', action: 'Xuất nhật ký hệ thống', target: `${rows.length} dòng` });
      return notify.toast.info(`Xuất ${rows.length} dòng nhật ký (giả lập)`);
    }
    try {
      const r = await exportAuditLogs(filter, branchName);
      notify.toast.success(r.truncated ? `Đã xuất ${r.count} dòng mới nhất (giới hạn 10.000 dòng)` : `Đã xuất ${r.count} dòng nhật ký`);
    } catch (e) {
      notify.toast.error(e instanceof ApiError ? e.message : 'Không xuất được nhật ký');
    }
  };

  const columns: DataColumn<AuditLog>[] = [
    {
      key: 'at',
      title: 'Thời điểm',
      width: 150,
      sorter: (a, b) => dayjs(a.at).valueOf() - dayjs(b.at).valueOf(),
      render: (_, r) => (
        <span className="leading-5" title={fmtDateTime(r.at)}>
          {fromNow(r.at)}
          <span className="tabular block text-xs text-ink-muted">{dayjs(r.at).format('DD/MM HH:mm')}</span>
        </span>
      ),
    },
    {
      key: 'actor',
      title: 'Người thực hiện',
      width: 180,
      render: (_, r) => (
        <span className="leading-5">
          <span className="font-medium">{r.actor}</span>
          <span className="block text-xs text-ink-muted">{r.actorRole}</span>
        </span>
      ),
    },
    {
      key: 'action',
      title: 'Hành động',
      width: 300,
      mobile: 'title',
      render: (_, r) => {
        const C = AUDIT_CATEGORIES[r.category];
        return (
          <span className="flex items-start gap-2 leading-5">
            <C.icon size={15} className="mt-0.5 shrink-0 text-ink-muted" aria-label={C.label} />
            <span className="min-w-0">
              <span className="font-medium">{r.action}</span> <span className="text-ink-secondary">· {r.target}</span>
              {r.detail && <span className="block truncate text-xs text-ink-muted">{r.detail}</span>}
            </span>
          </span>
        );
      },
    },
    { key: 'branch', title: 'Chi nhánh', width: 120, render: (_, r) => <span className="text-[13px]">{branchName(r.branchId)}</span> },
    {
      key: 'severity',
      title: 'Mức độ',
      width: 130,
      mobile: 'aside',
      render: (_, r) => (
        <StatusTag tone={AUDIT_SEVERITY[r.severity].tone} icon={AUDIT_SEVERITY[r.severity].icon} size="sm">
          {AUDIT_SEVERITY[r.severity].label}
        </StatusTag>
      ),
    },
    {
      key: 'ip',
      title: 'IP / thiết bị',
      width: 160,
      render: (_, r) => (
        <span className="leading-5 text-xs text-ink-secondary">
          <span className="tabular">{r.ip}</span>
          <span className="block">{r.device}</span>
        </span>
      ),
    },
  ];

  return (
    <>
      <PageHeader
        title="Nhật ký hệ thống"
        subtitle="Ghi tự động mọi thao tác quan trọng: đăng nhập, phân quyền, xóa / ẩn dữ liệu, xuất file, tiền, cấu hình. Không ai sửa hoặc xóa được nhật ký."
        breadcrumb={[{ title: 'Hệ thống' }, { title: 'Nhật ký hệ thống' }]}
        actions={
          canDo('system.audit', 'export') ? <Button action="export" variant="outline" collapseOnMobile onClick={() => void doExport()} /> : undefined
        }
      />

      <div className="mb-4 grid grid-cols-2 gap-3 lg:grid-cols-4">
        <StatCard label="Thao tác hôm nay" value={counts.today} icon={History} tone="primary" />
        <StatCard
          label="Nghiêm trọng (7 ngày)"
          value={counts.criticalWeek}
          icon={ShieldAlert}
          tone="danger"
          onClick={() => {
            setSeverity('critical');
            setCategory(undefined);
          }}
        />
        <StatCard label="Xuất file (7 ngày)" value={counts.exportWeek} icon={Download} tone="warning" onClick={() => setCategory('export')} />
        <StatCard label="Đăng nhập thất bại" value={counts.failedLoginWeek} icon={LogIn} tone="neutral" hint="7 ngày" onClick={() => setCategory('auth')} />
      </div>

      <DataTable<AuditLog>
        storageKey="sys-audit"
        columns={columns}
        data={rows}
        rowKey="id"
        loading={loading}
        unitLabel="dòng"
        searchKeys={['action', 'target', 'actor', 'detail', 'ip']}
        onRowClick={(r) => setOpenId(r.id)}
        search={{
          placeholder: 'Tìm hành động, đối tượng, người, IP...',
          activeFilterCount: [category, severity, who, range?.[0]].filter(Boolean).length,
          filters: (
            <>
              <Select placeholder="Loại" value={category} onChange={setCategory} options={Object.entries(AUDIT_CATEGORIES).map(([value, v]) => ({ value, label: v.label }))} showSearch={false} className="lg:!w-36" />
              <Select placeholder="Mức độ" value={severity} onChange={setSeverity} options={Object.entries(AUDIT_SEVERITY).map(([value, v]) => ({ value, label: v.label }))} showSearch={false} className="lg:!w-36" />
              <Select placeholder="Người thực hiện" value={who} onChange={setWho} options={actorOptions} className="lg:!w-48" />
              <DateRangeInput value={range} onChange={setRange} className="lg:!w-64" />
            </>
          ),
          onReset: () => {
            setCategory(undefined);
            setSeverity(undefined);
            setWho(undefined);
            setRange(null);
          },
        }}
        empty={{ icon: History, title: 'Không có nhật ký phù hợp', description: USE_API && !range?.[0] ? 'Đang xem 30 ngày gần nhất — chọn khoảng ngày để xem cũ hơn (tối đa 1 năm).' : undefined }}
      />
      {USE_API && server.total > server.rows.length && (
        <p className="mt-2 text-xs text-ink-muted">
          Có {server.total.toLocaleString('vi-VN')} dòng khớp — đang hiện {Math.min(server.rows.length, MAX_ROWS).toLocaleString('vi-VN')} dòng mới nhất. Thu hẹp bộ lọc / khoảng ngày, hoặc Xuất để lấy đủ.
        </p>
      )}

      <Drawer open={Boolean(open)} onClose={() => setOpenId(null)} width={isMobile ? '100%' : 520} destroyOnHidden title={open && `${open.action} · ${open.target}`}>
        {open && (
          <div className="flex flex-col gap-4 text-[13px]">
            <StatusTag tone={AUDIT_SEVERITY[open.severity].tone} icon={AUDIT_SEVERITY[open.severity].icon} size="sm" className="self-start">
              {AUDIT_SEVERITY[open.severity].label}
            </StatusTag>
            <dl className="grid grid-cols-[120px_1fr] gap-x-3 gap-y-1.5">
              <dt className="text-ink-muted">Thời điểm</dt>
              <dd className="tabular">{fmtDateTime(open.at)}</dd>
              <dt className="text-ink-muted">Người thực hiện</dt>
              <dd>
                {open.actor} · {open.actorRole}
              </dd>
              <dt className="text-ink-muted">Loại</dt>
              <dd>{AUDIT_CATEGORIES[open.category].label}</dd>
              <dt className="text-ink-muted">Chi nhánh</dt>
              <dd>{branchName(open.branchId)}</dd>
              <dt className="text-ink-muted">IP</dt>
              <dd className="tabular">{open.ip}</dd>
              <dt className="text-ink-muted">Thiết bị</dt>
              <dd>{open.device}</dd>
              <dt className="text-ink-muted">Mã nhật ký</dt>
              <dd className="font-mono text-xs">{open.id}</dd>
            </dl>
            {open.detail && <p className="rounded-lg bg-canvas px-3 py-2">{open.detail}</p>}
            {open.changes?.length ? (
              <div>
                <p className="mb-2 font-semibold text-ink">Thay đổi</p>
                <ul className="flex flex-col divide-y divide-line rounded-xl border border-line">
                  {open.changes.map((c, i) => (
                    <li key={i} className="px-3 py-2">
                      <p className="font-mono text-xs text-ink-muted">{c.field}</p>
                      <p className="flex flex-wrap items-center gap-2">
                        <span className="rounded bg-danger-50 px-1.5 text-danger-700 line-through">{c.before ?? '—'}</span>
                        <ArrowRight size={14} className="text-ink-muted" aria-hidden />
                        <span className="rounded bg-success-50 px-1.5 text-success-700">{c.after ?? '—'}</span>
                      </p>
                    </li>
                  ))}
                </ul>
              </div>
            ) : null}
          </div>
        )}
      </Drawer>
    </>
  );
}
