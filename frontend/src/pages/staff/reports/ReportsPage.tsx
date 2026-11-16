import { useState } from 'react';
import { Tabs } from 'antd';
import { Banknote, CalendarCheck, DoorOpen, GraduationCap, Percent, Target, TrendingUp, Wallet } from 'lucide-react';
import { Button, DataTable, PageHeader, Select, StatCard, StatusTag, useNotify, type DataColumn } from '@/components/common';
import { LEAD_STATUS_OPTIONS } from '@/features/crm/constants';
import { BarList, ChartCard, ColumnChart, shortMoney } from '@/features/reports/charts';
import { useReports, type ReportData } from '@/features/reports/useReports';
import { useSystemStore } from '@/features/system/systemStore';
import { useActor } from '@/features/system/useActor';
import { useAuth } from '@/hooks/useAuth';
import { cn } from '@/lib/cn';
import { formatCurrency } from '@/lib/format';

type BranchRow = ReportData['branches'][number];
const pctTxt = (v?: number) => (v === undefined ? '—' : `${v}%`);

/** Báo cáo: tuyển sinh · doanh thu & công nợ · đào tạo · so sánh chi nhánh. Số liệu tính từ dữ liệu hệ thống (giả). */
export default function ReportsPage() {
  const { branchId, currentBranch } = useAuth();
  const notify = useNotify();
  const { actor } = useActor();
  const audit = useSystemStore((s) => s.audit);
  const [months, setMonths] = useState(6);
  const r = useReports(branchId, months);
  const curMonth = r.monthKeys[r.monthKeys.length - 1];
  const growth = r.money.prevMonth ? Math.round(((r.money.thisMonth - r.money.prevMonth) / r.money.prevMonth) * 1000) / 10 : undefined;

  const exportXlsx = () => {
    audit(actor, { category: 'export', severity: 'warning', action: 'Xuất báo cáo', target: `Báo cáo ${months} tháng · ${currentBranch?.name ?? 'Toàn hệ thống'}` });
    notify.toast.info('Xuất Excel (giả lập) — đã ghi nhật ký hệ thống');
  };

  const branchCols: DataColumn<BranchRow>[] = [
    { key: 'label', title: 'Chi nhánh', width: 130, mobile: 'title', render: (_, b) => <b>{b.label}</b> },
    { key: 'leads', title: 'Lead', width: 80, align: 'right', sorter: (a, b) => a.leads - b.leads, render: (_, b) => <span className="tabular">{b.leads}</span> },
    { key: 'conv', title: 'Chốt', width: 80, align: 'right', sorter: (a, b) => (a.conv ?? 0) - (b.conv ?? 0), render: (_, b) => <span className="tabular">{pctTxt(b.conv)}</span> },
    { key: 'students', title: 'HV đang học', width: 110, align: 'right', sorter: (a, b) => a.students - b.students, render: (_, b) => <span className="tabular">{b.students}</span> },
    { key: 'fill', title: 'Lấp đầy lớp', width: 110, align: 'right', sorter: (a, b) => (a.fill ?? 0) - (b.fill ?? 0), render: (_, b) => <span className="tabular">{pctTxt(b.fill)}</span> },
    { key: 'att', title: 'Chuyên cần', width: 100, align: 'right', sorter: (a, b) => (a.attendance ?? 0) - (b.attendance ?? 0), render: (_, b) => <span className={cn('tabular', (b.attendance ?? 100) < 85 && 'font-semibold text-danger-700')}>{pctTxt(b.attendance)}</span> },
    { key: 'collected', title: `Thực thu ${months} tháng`, width: 150, align: 'right', sorter: (a, b) => a.collected - b.collected, render: (_, b) => <span className="tabular font-semibold">{formatCurrency(b.collected)}</span> },
    { key: 'debt', title: 'Công nợ', width: 140, align: 'right', sorter: (a, b) => a.debt - b.debt, render: (_, b) => <span className="tabular">{formatCurrency(b.debt)}</span> },
    { key: 'overdue', title: 'Quá hạn', width: 140, align: 'right', mobile: 'aside', sorter: (a, b) => a.overdue - b.overdue, render: (_, b) => <span className={cn('tabular', b.overdue > 0 && 'text-danger-700')}>{formatCurrency(b.overdue)}</span> },
  ];

  return (
    <>
      <PageHeader
        title="Báo cáo"
        subtitle={`${currentBranch ? `Chi nhánh ${currentBranch.name}` : 'Toàn hệ thống'} · ${months} tháng gần nhất. Đổi chi nhánh ở thanh trên cùng.`}
        breadcrumb={[{ title: 'Tổng quan' }, { title: 'Báo cáo' }]}
        actions={
          <>
            <Select
              value={months}
              onChange={(v) => v && setMonths(v as number)}
              options={[3, 6, 12].map((n) => ({ value: n, label: `${n} tháng` }))}
              showSearch={false}
              allowClear={false}
              className="!w-32"
              aria-label="Kỳ báo cáo"
            />
            <Button action="export" variant="outline" collapseOnMobile onClick={exportXlsx} />
          </>
        }
      />

      <Tabs
        items={[
          {
            key: 'crm',
            label: 'Tuyển sinh',
            children: (
              <>
                <div className="mb-4 grid grid-cols-2 gap-3 lg:grid-cols-4">
                  <StatCard label="Lead mới" value={r.lead.total} icon={Target} tone="primary" hint={`${months} tháng`} />
                  <StatCard label="Đã chốt" value={r.lead.won} icon={GraduationCap} tone="success" />
                  <StatCard label="Tỷ lệ chuyển đổi" value={pctTxt(r.lead.rate)} icon={Percent} tone="info" />
                  <StatCard label="Đang theo đuổi" value={r.lead.funnel.filter((f) => ['new', 'consulting', 'test_scheduled'].includes(f.key)).reduce((n, f) => n + f.value, 0)} icon={TrendingUp} tone="warning" hint="Mới · tư vấn · hẹn test" />
                </div>
                <div className="mb-4 grid gap-4 lg:grid-cols-2">
                  <ChartCard title="Lead mới theo tháng" subtitle="Số lead tạo trong tháng">
                    <ColumnChart ariaLabel="Lead mới theo tháng" data={r.lead.byMonth.map((p) => ({ key: p.month, label: p.label, value: p.value, highlight: p.month === curMonth }))} format={(v) => `${v} lead`} />
                  </ChartCard>
                  <ChartCard title="Phễu theo trạng thái" subtitle="Lead tạo trong kỳ, trạng thái hiện tại">
                    <BarList
                      ariaLabel="Phễu lead"
                      data={r.lead.funnel.map((f) => ({ key: f.key, label: LEAD_STATUS_OPTIONS.find((o) => o.value === f.key)?.label ?? f.key, value: f.value, highlight: f.key === 'won' }))}
                      format={(v) => String(v)}
                      labelWidth={100}
                    />
                  </ChartCard>
                </div>
                <div className="grid gap-4 lg:grid-cols-2">
                  <ChartCard title="Tỷ lệ chốt theo nguồn" subtitle="Tooltip: số lead · số chốt">
                    <BarList ariaLabel="Tỷ lệ chốt theo nguồn" data={r.lead.bySource.map((s) => ({ key: s.key, label: s.label, value: s.rate ?? 0, tip: `${s.label}: ${s.won}/${s.leads} lead chốt (${pctTxt(s.rate)})` }))} format={(v) => `${v}%`} max={100} />
                  </ChartCard>
                  <ChartCard title="Theo Sale" subtitle="Lead được giao trong kỳ">
                    <table className="w-full text-[13px]">
                      <thead>
                        <tr className="text-left text-xs text-ink-muted">
                          <th className="py-1 font-medium">Sale</th>
                          <th className="py-1 text-right font-medium">Lead</th>
                          <th className="py-1 text-right font-medium">Đang mở</th>
                          <th className="py-1 text-right font-medium">Chốt</th>
                          <th className="py-1 text-right font-medium">Tỷ lệ</th>
                        </tr>
                      </thead>
                      <tbody>
                        {r.lead.bySale.map((s) => (
                          <tr key={s.key} className="border-t border-line">
                            <td className="py-1.5">{s.label}</td>
                            <td className="tabular text-right">{s.leads}</td>
                            <td className="tabular text-right">{s.open}</td>
                            <td className="tabular text-right font-semibold">{s.won}</td>
                            <td className="tabular text-right">{pctTxt(s.rate)}</td>
                          </tr>
                        ))}
                      </tbody>
                    </table>
                  </ChartCard>
                  <ChartCard title="Chi phí / học viên chốt theo chiến dịch" subtitle="Thấp là tốt · chi tiết ở Tuyển sinh › Chiến dịch" className="lg:col-span-2">
                    <BarList
                      ariaLabel="Chi phí mỗi học viên chốt"
                      labelWidth={140}
                      data={r.lead.byCampaign.filter((c) => c.cpa !== undefined).map((c, i) => ({ key: c.key, label: c.label, value: c.cpa!, highlight: i === 0, tip: `${c.name}: chi ${formatCurrency(c.spent)} · ${c.leads} lead · ${c.won} chốt` }))}
                      format={shortMoney}
                    />
                  </ChartCard>
                </div>
              </>
            ),
          },
          {
            key: 'money',
            label: 'Doanh thu & công nợ',
            children: (
              <>
                <div className="mb-4 grid grid-cols-2 gap-3 lg:grid-cols-4">
                  <StatCard label="Thực thu tháng này" value={formatCurrency(r.money.thisMonth)} icon={Wallet} tone="success" trend={growth} trendLabel="so với tháng trước" />
                  <StatCard label={`Thực thu ${months} tháng`} value={shortMoney(r.money.collected)} icon={Banknote} tone="primary" hint={`Doanh số ký ${shortMoney(r.money.booked)}`} />
                  <StatCard label="Công nợ" value={shortMoney(r.money.debt)} icon={TrendingUp} tone="warning" />
                  <StatCard label="Quá hạn" value={shortMoney(r.money.overdue)} icon={TrendingUp} tone="danger" trendInverse />
                </div>
                <div className="grid gap-4 lg:grid-cols-[1.4fr_1fr]">
                  <ChartCard title="Thực thu theo tháng" subtitle="Tiền đã thu − tiền hoàn trong tháng">
                    <ColumnChart ariaLabel="Thực thu theo tháng" height={200} data={r.money.revenue.map((p) => ({ key: p.month, label: p.label, value: p.value, highlight: p.month === curMonth, tip: `${p.label}: ${formatCurrency(p.value)}` }))} format={shortMoney} />
                  </ChartCard>
                  <ChartCard title="Tuổi nợ" subtitle="Số tiền còn phải thu theo độ trễ">
                    <BarList ariaLabel="Tuổi nợ" labelWidth={130} data={r.money.aging.map((a) => ({ key: a.key, label: a.label, value: a.amount, tip: `${a.label}: ${formatCurrency(a.amount)} · ${a.count} hóa đơn` }))} format={shortMoney} />
                    <p className="mt-3 text-xs text-ink-muted">Danh sách nhắc nợ: Tài chính › Công nợ.</p>
                  </ChartCard>
                </div>
              </>
            ),
          },
          {
            key: 'study',
            label: 'Đào tạo',
            children: (
              <>
                <div className="mb-4 grid grid-cols-2 gap-3 lg:grid-cols-4">
                  <StatCard label="Học viên đang học" value={r.study.studying} icon={GraduationCap} tone="primary" />
                  <StatCard label="Lớp đang học" value={r.study.ongoing} icon={CalendarCheck} tone="info" hint={`${r.study.fill.length - r.study.ongoing} lớp chờ khai giảng`} />
                  <StatCard label="Chuyên cần tháng này" value={pctTxt(r.study.attendanceByMonth[r.study.attendanceByMonth.length - 1]?.value)} icon={CalendarCheck} tone="success" />
                  <StatCard label="Phòng dùng tuần này" value={pctTxt(r.study.roomUse.length ? Math.round(r.study.roomUse.reduce((n, x) => n + x.rate, 0) / r.study.roomUse.length) : undefined)} icon={DoorOpen} tone="warning" hint="TB công suất các phòng" />
                </div>
                <div className="mb-4 grid gap-4 lg:grid-cols-2">
                  <ChartCard title="Ghi danh mới theo tháng">
                    <ColumnChart ariaLabel="Ghi danh mới theo tháng" data={r.study.newEnroll.map((p) => ({ key: p.month, label: p.label, value: p.value, highlight: p.month === curMonth }))} format={(v) => `${v} ghi danh`} />
                  </ChartCard>
                  <ChartCard title="Chuyên cần theo tháng" subtitle="% lượt có mặt (kể cả đi muộn)">
                    <ColumnChart ariaLabel="Chuyên cần theo tháng" data={r.study.attendanceByMonth.map((p) => ({ key: p.month, label: p.label, value: p.value, highlight: p.month === curMonth }))} format={(v) => `${v}%`} />
                  </ChartCard>
                </div>
                <div className="grid gap-4 lg:grid-cols-2">
                  <ChartCard title="Tỷ lệ lấp đầy lớp" subtitle="Học viên / sĩ số tối đa — thấp nhất lên đầu">
                    <BarList ariaLabel="Lấp đầy lớp" labelWidth={110} max={100} data={r.study.fill.map((c) => ({ key: c.key, label: c.label, value: c.rate, tip: `${c.label} · ${c.course}: ${c.enrolled}/${c.max} học viên${c.status === 'enrolling' ? ' (chờ khai giảng)' : ''}` }))} format={(v) => `${v}%`} />
                  </ChartCard>
                  <ChartCard title="Công suất phòng tuần này" subtitle="Giờ có lớp / giờ mở cửa (81 giờ/tuần)">
                    <BarList ariaLabel="Công suất phòng" labelWidth={90} max={100} data={r.study.roomUse.map((x) => ({ key: x.key, label: x.label, value: x.rate, tip: `${x.label}: ${x.hours.toLocaleString('vi-VN', { maximumFractionDigits: 1 })} giờ` }))} format={(v) => `${v}%`} />
                  </ChartCard>
                </div>
              </>
            ),
          },
          {
            key: 'branches',
            label: 'So sánh chi nhánh',
            children: (
              <>
                <div className="mb-4 grid gap-4 lg:grid-cols-2">
                  <ChartCard title={`Thực thu ${months} tháng theo chi nhánh`}>
                    <BarList ariaLabel="Thực thu theo chi nhánh" data={r.branches.map((b) => ({ key: b.key, label: b.label, value: b.collected, tip: `${b.label}: ${formatCurrency(b.collected)}` }))} format={shortMoney} />
                  </ChartCard>
                  <ChartCard title="Học viên đang học theo chi nhánh">
                    <BarList ariaLabel="Học viên theo chi nhánh" data={r.branches.map((b) => ({ key: b.key, label: b.label, value: b.students }))} format={(v) => String(v)} />
                  </ChartCard>
                </div>
                <DataTable<BranchRow> storageKey="report-branches" columns={branchCols} data={r.branches} rowKey="key" unitLabel="chi nhánh" />
                <p className="mt-2 flex items-center gap-1.5 text-xs text-ink-muted">
                  <StatusTag tone="danger" icon={CalendarCheck} size="sm">
                    Đỏ
                  </StatusTag>
                  chuyên cần dưới 85% hoặc còn nợ quá hạn.
                </p>
              </>
            ),
          },
        ]}
      />
    </>
  );
}
