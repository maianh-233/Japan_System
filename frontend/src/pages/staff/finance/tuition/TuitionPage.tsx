import { useState } from 'react';
import { Form, Radio, Switch } from 'antd';
import dayjs, { type Dayjs } from 'dayjs';
import { BadgePercent, CalendarRange, Layers, Pencil, Plus, Tags, Trash2 } from 'lucide-react';
import { Button, DataTable, DateInput, ModalForm, NumberInput, PageHeader, Select, StatCard, StatusTag, TextArea, TextInput, useNotify, type DataColumn } from '@/components/common';
import { useCourseStore } from '@/features/academic/courses/courseStore';
import type { Course } from '@/features/academic/courses/types';
import { PROMO_KIND } from '@/features/finance/constants';
import { useFinanceStore } from '@/features/finance/financeStore';
import * as financeApi from '@/features/finance/financeApi';
import { USE_API } from '@/lib/api/config';
import type { InstallmentPlan, PromoKind, Promotion } from '@/features/finance/types';
import { useAuth } from '@/hooks/useAuth';
import { cn } from '@/lib/cn';
import { fmtDate } from '@/lib/date';
import { formatCurrency } from '@/lib/format';
import { getBranches } from '@/features/system/useBranches';

type Tab = 'prices' | 'promos' | 'plans';

interface PromoValues {
  code: string;
  name: string;
  kind: PromoKind;
  value: number;
  courseIds: string[];
  branchIds: string[];
  from: Dayjs;
  to: Dayjs;
  limit?: number;
  active: boolean;
  note?: string;
}

/** Học phí & bảng giá: giá theo khóa / chi nhánh, ưu đãi – combo, chính sách đóng nhiều đợt. DỮ LIỆU GIẢ. */
export default function TuitionPage() {
  const { can } = useAuth();
  const notify = useNotify();
  const courses = useCourseStore((s) => s.courses);
  const { prices, promotions, plans, setPrice, savePromotion, togglePromotion, savePlan } = useFinanceStore();
  const editable = can('finance.tuition');

  const [tab, setTab] = useState<Tab>('prices');
  const [priceEdit, setPriceEdit] = useState<{ course: Course; branchId: string } | null>(null);
  const [promoEdit, setPromoEdit] = useState<{ promo?: Promotion } | null>(null);
  const [planEdit, setPlanEdit] = useState<{ plan?: InstallmentPlan } | null>(null);

  const today = dayjs().format('YYYY-MM-DD');
  const priceOf = (c: Course, b: string) => prices.find((p) => p.courseId === c.id && p.branchId === b)?.fee;
  const sellable = courses;
  const running = promotions.filter((p) => p.active && p.from <= today && p.to >= today);

  const priceCols: DataColumn<Course>[] = [
    {
      key: 'name',
      title: 'Khóa học',
      width: 260,
      mobile: 'title',
      render: (_, c) => (
        <div className="leading-5">
          <p className="font-medium text-ink">{c.name}</p>
          <p className="text-xs text-ink-muted">
            <span className="font-mono">{c.code}</span> · {c.sessions} buổi × {c.minutesPerSession}′{c.format === 'private' ? ' · giá / buổi' : ''}
          </p>
        </div>
      ),
    },
    { key: 'fee', title: 'Giá niêm yết', width: 140, align: 'right', render: (_, c) => <span className="tabular font-semibold">{formatCurrency(c.fee)}</span> },
    ...getBranches(false).map<DataColumn<Course>>((b) => ({
      key: `b-${b.id}`,
      title: b.name,
      width: 150,
      align: 'right',
      render: (_, c) => {
        if (!c.branchIds.includes(b.id)) return <span className="text-xs text-ink-muted">Không mở</span>;
        const own = priceOf(c, b.id);
        return (
          <button
            type="button"
            disabled={!editable}
            onClick={() => setPriceEdit({ course: c, branchId: b.id })}
            className={cn('tabular rounded px-1.5 py-0.5 text-right', editable && 'hover:bg-primary-50', own !== undefined ? 'font-semibold text-primary-700' : 'text-ink-secondary')}
            title={own !== undefined ? 'Giá riêng chi nhánh — bấm để sửa' : 'Theo giá niêm yết — bấm để đặt giá riêng'}
          >
            {formatCurrency(own ?? c.fee)}
            {own !== undefined && <span className={cn('ml-1 text-[11px]', own > c.fee ? 'text-danger-600' : 'text-success-700')}>{own > c.fee ? '+' : ''}{Math.round(((own - c.fee) / c.fee) * 100)}%</span>}
          </button>
        );
      },
    })),
    { key: 'status', title: 'Trạng thái', width: 110, render: (_, c) => <StatusTag tone={c.status === 'active' ? 'success' : c.status === 'draft' ? 'info' : 'warning'} icon={Tags} size="sm">{c.status === 'active' ? 'Đang bán' : c.status === 'draft' ? 'Nháp' : 'Tạm dừng'}</StatusTag> },
  ];

  const promoCols: DataColumn<Promotion>[] = [
    {
      key: 'code',
      title: 'Ưu đãi',
      width: 280,
      mobile: 'title',
      render: (_, p) => (
        <div className="leading-5">
          <p className="font-medium text-ink">{p.name}</p>
          <p className="text-xs text-ink-muted">
            <span className="rounded bg-disabled px-1 font-mono">{p.code}</span> · {PROMO_KIND[p.kind]}
          </p>
        </div>
      ),
    },
    { key: 'value', title: 'Mức giảm', width: 130, align: 'right', render: (_, p) => <span className="tabular font-semibold text-success-700">{p.kind === 'percent' ? `−${p.value}%` : `−${formatCurrency(p.value)}`}</span> },
    {
      key: 'scope',
      title: 'Áp dụng',
      width: 220,
      render: (_, p) => (
        <div className="text-xs leading-5 text-ink-secondary">
          <p>{p.courseIds.length ? p.courseIds.map((id) => courses.find((c) => c.id === id)?.code ?? id).join(p.kind === 'combo' ? ' + ' : ', ') : 'Mọi khóa'}</p>
          <p>{p.branchIds.length === getBranches(false).length ? 'Mọi chi nhánh' : p.branchIds.map((b) => getBranches(false).find((x) => x.id === b)?.name).join(', ')}</p>
        </div>
      ),
    },
    { key: 'period', title: 'Thời gian', width: 170, render: (_, p) => <span className={cn('text-[13px]', p.to < today && 'text-ink-muted')}>{fmtDate(p.from)} – {fmtDate(p.to)}</span> },
    { key: 'used', title: 'Đã dùng', width: 100, align: 'center', render: (_, p) => <span className={cn('tabular', p.limit !== undefined && p.used >= p.limit && 'font-semibold text-danger-600')}>{p.used}{p.limit ? `/${p.limit}` : ''}</span> },
    {
      key: 'active',
      title: 'Trạng thái',
      width: 130,
      mobile: 'aside',
      render: (_, p) =>
        p.to < today ? (
          <StatusTag tone="neutral" icon={CalendarRange} size="sm">Hết hạn</StatusTag>
        ) : p.active ? (
          <StatusTag tone="success" icon={BadgePercent} size="sm">{p.from > today ? 'Sắp chạy' : 'Đang chạy'}</StatusTag>
        ) : (
          <StatusTag tone="warning" icon={BadgePercent} size="sm">Tạm tắt</StatusTag>
        ),
    },
  ];

  const editingPrice = priceEdit ? priceOf(priceEdit.course, priceEdit.branchId) : undefined;

  return (
    <>
      <PageHeader
        title="Học phí & bảng giá"
        subtitle="Giá niêm yết lấy từ Khóa học; mỗi chi nhánh có thể đặt giá riêng. Ưu đãi và chính sách đóng đợt được chọn khi ghi danh."
        breadcrumb={[{ title: 'Tài chính' }, { title: 'Học phí & bảng giá' }]}
        actions={
          editable && tab !== 'prices' ? (
            <Button icon={Plus} onClick={() => (tab === 'promos' ? setPromoEdit({}) : setPlanEdit({}))} collapseOnMobile>
              {tab === 'promos' ? 'Tạo ưu đãi' : 'Thêm chính sách'}
            </Button>
          ) : undefined
        }
      />

      <div className="mb-4 grid grid-cols-1 gap-3 sm:grid-cols-3">
        <StatCard label="Khóa đang bán" value={sellable.filter((c) => c.status === 'active').length} icon={Tags} onClick={() => setTab('prices')} hint={`${prices.length} giá riêng theo chi nhánh`} />
        <StatCard label="Ưu đãi đang chạy" value={running.length} icon={BadgePercent} tone="success" onClick={() => setTab('promos')} hint={`${running.reduce((n, p) => n + p.used, 0)} lượt đã dùng`} />
        <StatCard label="Chính sách đóng đợt" value={plans.filter((p) => p.active).length} icon={Layers} tone="info" onClick={() => setTab('plans')} />
      </div>

      <div className="mb-3 flex flex-wrap gap-1.5">
        {(
          [
            ['prices', 'Bảng giá'],
            ['promos', 'Ưu đãi & combo'],
            ['plans', 'Đóng nhiều đợt'],
          ] as [Tab, string][]
        ).map(([k, l]) => (
          <button key={k} type="button" onClick={() => setTab(k)} className={cn('h-9 rounded-full border px-3.5 text-sm font-medium', tab === k ? 'border-primary-600 bg-primary-600 text-white' : 'border-line bg-surface text-ink-secondary hover:bg-primary-50')}>
            {l}
          </button>
        ))}
      </div>

      {tab === 'prices' && <DataTable<Course> storageKey="fin-prices" columns={priceCols} data={sellable} rowKey="id" unitLabel="khóa" searchKeys={['name', 'code']} />}

      {tab === 'promos' && (
        <DataTable<Promotion>
          storageKey="fin-promos"
          columns={promoCols}
          data={[...promotions].sort((a, b) => Number(b.active) - Number(a.active) || b.to.localeCompare(a.to))}
          rowKey="id"
          unitLabel="ưu đãi"
          searchKeys={['code', 'name']}
          onRowClick={editable ? (p) => setPromoEdit({ promo: p }) : undefined}
          rowActions={(p) =>
            editable && p.to >= today ? (
              <Switch size="small" checked={p.active} onChange={() =>
                  (USE_API ? financeApi.togglePromotion(p.id, !p.active) : Promise.resolve(togglePromotion(p.id)))
                    .then(() => notify.toast.success(p.active ? 'Đã tắt ưu đãi' : 'Đã bật ưu đãi'))
                    .catch((e: Error) => notify.error({ title: 'Chưa đổi được', description: e.message }))
                } aria-label="Bật / tắt" />
            ) : null
          }
          actionsWidth={70}
        />
      )}

      {tab === 'plans' && (
        <div className="grid gap-3 md:grid-cols-2">
          {plans.map((p) => (
            <div key={p.id} className={cn('rounded-xl border border-line bg-surface p-4', !p.active && 'opacity-60')}>
              <div className="mb-3 flex items-start justify-between gap-2">
                <div>
                  <p className="font-semibold text-ink">{p.name}</p>
                  <p className="text-xs text-ink-muted">
                    Khóa từ {formatCurrency(p.minFee)}
                    {p.surcharge > 0 && ` · phụ thu ${p.surcharge}%`}
                    {!p.active && ' · đang tắt'}
                  </p>
                </div>
                {editable && <Button icon={Pencil} iconOnly size="sm" variant="ghost" tone="neutral" aria-label="Sửa" onClick={() => setPlanEdit({ plan: p })} />}
              </div>
              <div className="flex h-7 overflow-hidden rounded-md">
                {p.parts.map((x, i) => (
                  <div key={i} style={{ width: `${x.percent}%` }} className={cn('grid place-items-center text-[11px] font-semibold text-white', ['bg-primary-600', 'bg-primary-400', 'bg-primary-300'][i % 3])}>
                    {x.percent}%
                  </div>
                ))}
              </div>
              <ol className="mt-2 flex flex-col gap-0.5 text-[13px] text-ink-secondary">
                {p.parts.map((x, i) => (
                  <li key={i}>
                    Đợt {i + 1}: {x.percent}% — hạn {x.offsetDays < 0 ? `${-x.offsetDays} ngày trước khai giảng` : x.offsetDays === 0 ? 'ngày khai giảng' : `${x.offsetDays} ngày sau khai giảng`}
                  </li>
                ))}
              </ol>
              <p className="mt-2 text-xs text-ink-muted">VD khóa 6.800.000đ: {p.parts.map((x) => formatCurrency(Math.round((6_800_000 * (1 + p.surcharge / 100) * x.percent) / 100 / 1000) * 1000)).join(' + ')}</p>
            </div>
          ))}
        </div>
      )}

      {/* Giá riêng chi nhánh */}
      <ModalForm<{ fee?: number }>
        key={priceEdit ? `${priceEdit.course.id}-${priceEdit.branchId}` : 'price'}
        open={Boolean(priceEdit)}
        onClose={() => setPriceEdit(null)}
        title={`Giá tại ${getBranches(false).find((b) => b.id === priceEdit?.branchId)?.name ?? ''}`}
        description={priceEdit && `${priceEdit.course.name} · niêm yết ${formatCurrency(priceEdit.course.fee)}. Chỉ áp dụng cho ghi danh MỚI — hóa đơn đã phát hành giữ nguyên giá.`}
        submitAction="save"
        successMessage="Đã cập nhật giá"
        initialValues={{ fee: editingPrice }}
        footerExtra={
          editingPrice !== undefined ? (
            <Button icon={Trash2} variant="ghost" tone="danger" onClick={async () => {
              try {
                await (USE_API ? financeApi.setPrice : setPrice)(priceEdit!.course.id, priceEdit!.branchId, null);
                setPriceEdit(null);
                notify.toast.success('Đã về giá niêm yết');
              } catch (e) {
                notify.error({ title: 'Chưa đổi được giá', description: e instanceof Error ? e.message : undefined });
              }
            }}>
              Dùng giá niêm yết
            </Button>
          ) : undefined
        }
        onSubmit={(v) => (USE_API ? financeApi.setPrice : setPrice)(priceEdit!.course.id, priceEdit!.branchId, v.fee ?? null)}
      >
        <Form.Item name="fee" label={priceEdit?.course.format === 'private' ? 'Giá mỗi buổi' : 'Giá trọn khóa'} rules={[{ required: true, message: 'Nhập giá' }]}>
          <NumberInput currency min={0} step={100_000} />
        </Form.Item>
      </ModalForm>

      {/* Ưu đãi */}
      <ModalForm<PromoValues>
        key={promoEdit?.promo?.id ?? 'promo-new'}
        open={Boolean(promoEdit)}
        onClose={() => setPromoEdit(null)}
        width={680}
        columns={2}
        title={promoEdit?.promo ? 'Sửa ưu đãi' : 'Tạo ưu đãi'}
        submitAction={promoEdit?.promo ? 'save' : 'create'}
        successMessage="Đã lưu ưu đãi"
        initialValues={
          promoEdit?.promo
            ? { ...promoEdit.promo, from: dayjs(promoEdit.promo.from), to: dayjs(promoEdit.promo.to) }
            : { kind: 'percent', courseIds: [], branchIds: getBranches(false).map((b) => b.id), from: dayjs(), to: dayjs().add(30, 'day'), active: true }
        }
        onSubmit={async (v) => {
          if (v.kind === 'combo' && v.courseIds.length < 2) throw new Error('Combo cần chọn ít nhất 2 khóa');
          if (v.kind === 'percent' && v.value > 50) throw new Error('Giảm tối đa 50%');
          await (USE_API ? financeApi.savePromotion : savePromotion)({ ...v, id: promoEdit?.promo?.id, code: v.code.trim().toUpperCase(), name: v.name.trim(), from: v.from.format('YYYY-MM-DD'), to: v.to.format('YYYY-MM-DD'), note: v.note?.trim() || undefined });
        }}
      >
        <PromoFields courses={sellable} />
      </ModalForm>

      {/* Chính sách đóng đợt */}
      <ModalForm<{ name: string; minFee: number; surcharge: number; active: boolean; parts: { percent: number; offsetDays: number }[] }>
        key={planEdit?.plan?.id ?? 'plan-new'}
        open={Boolean(planEdit)}
        onClose={() => setPlanEdit(null)}
        width={600}
        title={planEdit?.plan ? 'Sửa chính sách đóng đợt' : 'Thêm chính sách đóng đợt'}
        submitAction="save"
        successMessage="Đã lưu chính sách"
        initialValues={planEdit?.plan ?? { minFee: 4_000_000, surcharge: 0, active: true, parts: [{ percent: 50, offsetDays: -3 }, { percent: 50, offsetDays: 30 }] }}
        onSubmit={async (v) => {
          const sum = v.parts.reduce((n, p) => n + (p.percent ?? 0), 0);
          if (sum !== 100) throw new Error(`Tổng các đợt phải bằng 100% (hiện ${sum}%)`);
          await (USE_API ? financeApi.savePlan : savePlan)({ ...v, id: planEdit?.plan?.id, name: v.name.trim() });
        }}
      >
        <Form.Item name="name" label="Tên chính sách" rules={[{ required: true, whitespace: true }]}>
          <TextInput placeholder="VD: 2 đợt (50% – 50%)" />
        </Form.Item>
        <div className="grid grid-cols-2 gap-x-3">
          <Form.Item name="minFee" label="Áp dụng khóa từ">
            <NumberInput currency min={0} step={500_000} />
          </Form.Item>
          <Form.Item name="surcharge" label="Phụ thu (%)">
            <NumberInput min={0} max={20} />
          </Form.Item>
        </div>
        <Form.List name="parts">
          {(fields, { add, remove }) => (
            <div className="mb-3 flex flex-col gap-2">
              <p className="text-sm font-medium text-ink">Các đợt đóng</p>
              {fields.map((f, i) => (
                <div key={f.key} className="grid grid-cols-[60px_1fr_1fr_36px] items-start gap-2">
                  <span className="pt-2 text-sm text-ink-secondary">Đợt {i + 1}</span>
                  <Form.Item name={[f.name, 'percent']} rules={[{ required: true, message: '%' }]} className="!mb-0">
                    <NumberInput min={1} max={100} placeholder="%" />
                  </Form.Item>
                  <Form.Item name={[f.name, 'offsetDays']} rules={[{ required: true, message: 'Ngày' }]} className="!mb-0" extra={i === 0 ? 'Số ngày so với khai giảng (âm = trước)' : undefined}>
                    <NumberInput min={-60} max={365} placeholder="Hạn (ngày)" />
                  </Form.Item>
                  <Button icon={Trash2} iconOnly size="sm" variant="ghost" tone="danger" aria-label="Xóa đợt" disabled={fields.length <= 1} onClick={() => remove(f.name)} />
                </div>
              ))}
              <Button icon={Plus} size="sm" variant="soft" className="self-start" disabled={fields.length >= 6} onClick={() => add({ percent: 0, offsetDays: 30 * fields.length })}>
                Thêm đợt
              </Button>
            </div>
          )}
        </Form.List>
        <Form.Item name="active" label="Đang áp dụng" valuePropName="checked">
          <Switch />
        </Form.Item>
      </ModalForm>
    </>
  );
}

function PromoFields({ courses }: { courses: Course[] }) {
  const form = Form.useFormInstance<PromoValues>();
  const kind = Form.useWatch('kind', form);
  return (
    <>
      <Form.Item name="name" label="Tên ưu đãi" rules={[{ required: true, whitespace: true }]} className="md:col-span-2">
        <TextInput placeholder="VD: Đăng ký sớm trước khai giảng 14 ngày" />
      </Form.Item>
      <Form.Item name="code" label="Mã" rules={[{ required: true, pattern: /^[A-Za-z0-9]{3,15}$/, message: '3–15 ký tự chữ / số' }]}>
        <TextInput className="uppercase" />
      </Form.Item>
      <Form.Item name="kind" label="Loại">
        <Radio.Group options={(Object.keys(PROMO_KIND) as PromoKind[]).map((k) => ({ value: k, label: PROMO_KIND[k] }))} />
      </Form.Item>
      <Form.Item name="value" label={kind === 'percent' ? 'Giảm (%)' : 'Số tiền giảm'} rules={[{ required: true, message: 'Nhập mức giảm' }]}>
        {kind === 'percent' ? <NumberInput min={1} max={50} /> : <NumberInput currency min={0} step={100_000} />}
      </Form.Item>
      <Form.Item name="limit" label="Giới hạn lượt dùng" extra="Để trống = không giới hạn">
        <NumberInput min={1} />
      </Form.Item>
      <Form.Item name="courseIds" label={kind === 'combo' ? 'Các khóa trong combo' : 'Khóa áp dụng'} extra={kind === 'combo' ? 'Học viên đăng ký đủ các khóa này cùng lúc' : 'Để trống = mọi khóa'} className="md:col-span-2">
        <Select mode="multiple" options={courses.map((c) => ({ value: c.id, label: `${c.code} · ${c.name}` }))} />
      </Form.Item>
      <Form.Item name="branchIds" label="Chi nhánh" rules={[{ required: true, type: 'array', min: 1, message: 'Chọn chi nhánh' }]} className="md:col-span-2">
        <Select mode="multiple" options={getBranches(false).map((b) => ({ value: b.id, label: b.name }))} />
      </Form.Item>
      <Form.Item name="from" label="Từ ngày" rules={[{ required: true }]}>
        <DateInput />
      </Form.Item>
      <Form.Item name="to" label="Đến ngày" rules={[{ required: true }]}>
        <DateInput />
      </Form.Item>
      <Form.Item name="note" label="Ghi chú điều kiện" className="md:col-span-2">
        <TextArea rows={2} />
      </Form.Item>
      <Form.Item name="active" label="Bật ngay" valuePropName="checked">
        <Switch />
      </Form.Item>
    </>
  );
}
